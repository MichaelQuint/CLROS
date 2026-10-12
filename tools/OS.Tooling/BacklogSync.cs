using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using static OS.Tooling.BacklogManifest;

namespace OS.Tooling;

/// <summary>Manifest-driven reconciliation. Remote writes occur only with an explicit --apply option.</summary>
public static class BacklogSync
{
    public static async Task<int> RunAsync(string root, string[] args, TextWriter output, CancellationToken cancellation = default)
    {
        try { return await RunAsync(root, BacklogSyncOptions.Parse(args), output, cancellation); }
        catch (Exception e) { output.WriteLine("TEST_FAIL gitea.backlog.sync " + e.Message.ReplaceLineEndings(" ")); return 1; }
    }

    public static async Task<int> RunAsync(string root, BacklogSyncOptions options, TextWriter output, CancellationToken cancellation = default, BacklogApi? api = null)
    {
        output.WriteLine("TEST_START gitea.backlog.sync");
        try
        {
            options.Validate();
            var manifest = Read(Path.GetFullPath(options.ManifestPath.Length == 0 ? ".gitea/backlog/clros-issues.json" : options.ManifestPath, root));
            output.WriteLine($"SYNC_MANIFEST_VALID milestones={manifest.Milestones.Count} labels={manifest.Labels.Count} epics={manifest.Milestones.Count} children={manifest.Issues.Count - manifest.Milestones.Count} issues={manifest.Issues.Count}");
            if (options.SelfTest)
            {
                BacklogSelfTest.Run(manifest, output);
                output.WriteLine("TEST_PASS gitea.backlog.sync"); return 0;
            }
            if (options.IssueKeys.Length != 0)
            {
                manifest = manifest.SelectIssues(options.IssueKeys);
                output.WriteLine("SYNC_SELECTION " + string.Join(',', manifest.Issues.Select(x => Text(x, "key"))));
            }
            output.WriteLine("SYNC_MODE " + (options.Apply ? "apply" : "preview"));
            if (options.Offline)
            {
                output.WriteLine("SYNC_OFFLINE manifest_only");
                var actions = new Actions(output);
                foreach (var item in manifest.Labels) actions.Add("skipped", "label:" + Text(item, "name"));
                foreach (var item in manifest.Milestones) actions.Add("skipped", "milestone:" + Text(item, "key"));
                foreach (var item in manifest.Issues) actions.Add("skipped", "issue:" + Text(item, "key"));
                actions.Write(); output.WriteLine("TEST_PASS gitea.backlog.sync"); return 0;
            }
            using HttpClient? client = api is null ? CreateClient(options.Apply) : null;
            api ??= (method, path, body, token) => SendAsync(client!, method, path, body, token);
            return await ReconcileAsync(manifest, options.Apply, api, output, cancellation);
        }
        catch (Exception e)
        {
            // HTTP exception bodies may contain credential-bearing server/proxy content. SendAsync
            // deliberately replaces them with a status code; no headers or auth values are logged.
            output.WriteLine("TEST_FAIL gitea.backlog.sync " + e.Message.ReplaceLineEndings(" ")); return 1;
        }
    }

    private const string RepoPath = "/repos/admin/CLROS";
    public static async Task<BacklogRemoteState> ReadRemoteAsync(BacklogManifest manifest, BacklogApi api, TextWriter output, CancellationToken cancellation)
    {
        BacklogRemoteState state = new();
        var repository = await api("GET", RepoPath, null, cancellation);
        if (Number(repository, "id") != BacklogContracts.RepositoryId || Text(repository, "full_name") != "admin/CLROS")
            throw new InvalidDataException("Refusing tracker repository identity mismatch.");
        output.WriteLine("SYNC_REPOSITORY_VERIFIED id=9 full_name=admin/CLROS");
        foreach (var item in await CollectionAsync(api, RepoPath + "/labels", cancellation))
            if (!state.Labels.TryAdd(Text(item, "name"), item)) throw new InvalidDataException("Remote tracker contains duplicate label names");
        foreach (var item in await CollectionAsync(api, RepoPath + "/milestones?state=all", cancellation))
            if (!state.Milestones.TryAdd(Text(item, "title"), item)) throw new InvalidDataException("Remote tracker contains duplicate milestone titles");
        foreach (var original in await CollectionAsync(api, RepoPath + "/issues?state=all&type=issues", cancellation))
        {
            if (original["pull_request"] is not null) continue;
            var item = original;
            if (Text(item, "body").Length == 0 && Number(item, "number") > 0)
                item = (await api("GET", RepoPath + "/issues/" + Number(item, "number"), null, cancellation))?.AsObject() ?? throw new InvalidDataException("Missing issue readback");
            state.AllIssues.Add(item);
            string key = BacklogPolicy.HiddenKey(Text(item, "body"));
            if (key.Length != 0 && !state.Issues.TryAdd(key, item)) throw new InvalidDataException("Remote tracker contains duplicate hidden key '" + key + "' on more than one issue.");
        }
        int requested = 0;
        foreach (var issue in manifest.IssueByKey.Values)
        {
            string key = Text(issue, "key");
            if (Text(issue, "desiredState") != "closed" || !state.Issues.TryGetValue(key, out var live)) continue;
            requested++;
            int number = Number(live, "number"); if (number <= 0) continue;
            string marker = BacklogContracts.EvidenceMarker(key);
            var comments = await CollectionAsync(api, RepoPath + "/issues/" + number + "/comments", cancellation);
            if (comments.Any(x => Text(x, "body").Contains(marker, StringComparison.Ordinal))) state.ReviewedClosures.Add(key);
        }
        output.WriteLine($"SYNC_REMOTE_READ labels={state.Labels.Count} milestones={state.Milestones.Count} keyedIssues={state.Issues.Count}");
        output.WriteLine($"SYNC_CLOSURE_EVIDENCE requested={requested} verified={state.ReviewedClosures.Count} marker=clros-evidence-reviewed");
        return state;
    }

    public static async Task<int> ReconcileAsync(BacklogManifest manifest, bool apply, BacklogApi api, TextWriter output, CancellationToken cancellation = default)
    {
        var remote = await ReadRemoteAsync(manifest, api, output, cancellation);
        var actions = new Actions(output);
        var managed = manifest.LabelByName.Keys.ToHashSet(StringComparer.Ordinal);
        async Task Item(string key, Func<Task> action)
        {
            try { cancellation.ThrowIfCancellationRequested(); await action(); }
            catch (OperationCanceledException) { throw; }
            catch (Exception e) { actions.Add("failed", key); output.WriteLine("SYNC_ITEM_FAIL " + key + " reason=" + e.Message.ReplaceLineEndings(" ")); }
        }
        foreach (var label in manifest.Labels) await Item("label:" + Text(label, "name"), async () =>
        {
            string name = Text(label, "name"), key = "label:" + name;
            remote.Labels.TryGetValue(name, out var live);
            if (live is not null && LabelMatches(label, live)) { actions.Add("unchanged", key); return; }
            if (!apply) { actions.Add(live is null ? "wouldCreate" : "wouldUpdate", key); return; }
            var payload = new JsonObject { ["name"] = name, ["color"] = Text(label, "color"), ["description"] = Text(label, "description") };
            remote.Labels[name] = (await api(live is null ? "POST" : "PATCH", RepoPath + "/labels" + (live is null ? "" : "/" + Number(live, "id")), payload, cancellation))!.AsObject();
            actions.Add(live is null ? "created" : "updated", key);
        });
        foreach (var milestone in manifest.Milestones) await Item("milestone:" + Text(milestone, "key"), async () =>
        {
            actions.Add(await ReconcileMilestoneAsync(milestone, manifest.IssueByKey, remote, apply, api, output, cancellation), "milestone:" + Text(milestone, "key"));
        });
        int MilestoneId(JsonObject issue)
        {
            string title = Text(manifest.MilestoneByKey[Text(issue, "milestone")], "title");
            return remote.Milestones.TryGetValue(title, out var value) ? Number(value, "id") : 0;
        }
        JsonArray LabelIds(string[] names) => new(names.Select(name => (JsonNode?)JsonValue.Create(remote.Labels.TryGetValue(name, out var label)
            ? Number(label, "id") : throw new InvalidDataException("Required remote label '" + name + "' is unavailable."))).ToArray());
        foreach (var issue in manifest.Issues)
        {
            string key = Text(issue, "key"); if (remote.Issues.ContainsKey(key)) continue;
            if (!apply) { actions.Add("wouldCreate", "issue:" + key); continue; }
            await Item("issue:" + key, async () =>
            {
                int milestone = MilestoneId(issue); if (milestone <= 0) throw new InvalidDataException("Milestone was not created");
                string[] labels = BacklogPolicy.DesiredLabels(issue, manifest.IssueByKey, remote, managed);
                var payload = new JsonObject { ["title"] = Text(issue, "title"), ["body"] = BacklogPolicy.MergeBody("", key, BacklogPolicy.RenderBody(issue, manifest.IssueByKey, remote.Issues)),
                    ["labels"] = LabelIds(labels), ["milestone"] = milestone };
                remote.Issues[key] = (await api("POST", RepoPath + "/issues", payload, cancellation))!.AsObject(); actions.Add("created", "issue:" + key);
            });
        }
        foreach (var issue in BacklogPolicy.UpdateOrder(manifest.Issues))
        {
            string key = Text(issue, "key");
            if (!remote.Issues.TryGetValue(key, out var live)) { if (apply) actions.Add("skipped", "issue:" + key); continue; }
            await Item("issue:" + key, async () =>
            {
                if (Text(issue, "desiredState") == "closed") BacklogPolicy.AssertClosureAllowed(issue, manifest.IssueByKey, remote, !apply);
                string body = BacklogPolicy.MergeBody(Text(live, "body"), key, BacklogPolicy.RenderBody(issue, manifest.IssueByKey, remote.Issues));
                string[] labels = BacklogPolicy.DesiredLabels(issue, manifest.IssueByKey, remote, managed);
                bool labelsDiffer = !labels.ToHashSet(StringComparer.Ordinal).SetEquals(BacklogPolicy.RemoteLabels(live));
                int milestone = MilestoneId(issue);
                string? state = Text(issue, "desiredState") switch { "closed" when Text(live, "state") != "closed" => "closed", "reopen" when Text(live, "state") != "open" => "open", _ => null };
                bool fieldsDiffer = Text(live, "title") != Text(issue, "title") || Text(live, "body") != body || Number(live["milestone"], "id") != milestone || state is not null;
                if (!labelsDiffer && !fieldsDiffer) { actions.Add("unchanged", "issue:" + key); return; }
                if (!apply) { actions.Add("wouldUpdate", "issue:" + key); return; }
                if (milestone <= 0) throw new InvalidDataException("Required milestone is unavailable");
                int number = Number(live, "number");
                if (fieldsDiffer)
                {
                    var payload = new JsonObject { ["title"] = Text(issue, "title"), ["body"] = body, ["milestone"] = milestone };
                    if (state is not null) payload["state"] = state;
                    live = (await api("PATCH", RepoPath + "/issues/" + number, payload, cancellation))!.AsObject();
                }
                if (labelsDiffer)
                {
                    var replaced = await ReplaceLabelsAsync(api, number, LabelIds(labels), cancellation);
                    if (replaced.Count != labels.Length) throw new InvalidDataException("Gitea returned an unexpected label count after replacing labels for '" + key + "'.");
                    live = (await api("GET", RepoPath + "/issues/" + number, null, cancellation))!.AsObject();
                }
                remote.Issues[key] = live; actions.Add("updated", "issue:" + key);
            });
        }
        if (apply) await Item("verification:convergence", async () =>
        {
            var verified = await ReadRemoteAsync(manifest, api, output, cancellation);
            var failures = VerifyConvergence(manifest, verified);
            if (failures.Count != 0) throw new InvalidDataException("Convergence verification failed count=" + failures.Count + " sample=" + string.Join(',', failures.Take(20)));
            output.WriteLine($"SYNC_VERIFY_PASS milestones={manifest.Milestones.Count} labels={manifest.Labels.Count} issues={manifest.Issues.Count}");
        });
        actions.Write();
        bool succeeded = actions.Count("failed") == 0;
        output.WriteLine(succeeded ? "TEST_PASS gitea.backlog.sync" : "TEST_FAIL gitea.backlog.sync One or more synchronization items failed; rerun is safe after correcting the reported blocker.");
        return succeeded ? 0 : 1;
    }

    public static async Task<string> ReconcileMilestoneAsync(JsonObject milestone, IReadOnlyDictionary<string, JsonObject> manifest,
        BacklogRemoteState remote, bool apply, BacklogApi api, TextWriter output, CancellationToken cancellation = default)
    {
        string title = Text(milestone, "title");
        remote.Milestones.TryGetValue(title, out var live);
        string desired = BacklogPolicy.ResolveMilestone(milestone, live, manifest, remote);
        if (live is null && Text(milestone, "state") == "closed") output.WriteLine("SYNC_MILESTONE_DEFERRED key=" + Text(milestone, "key") + " reason=missing_container createState=open");
        if (live is not null && Text(live, "description") == Text(milestone, "description") && Text(live, "state") == desired) return "unchanged";
        if (!apply) return live is null ? "wouldCreate" : "wouldUpdate";
        var payload = new JsonObject { ["title"] = title, ["description"] = Text(milestone, "description"), ["state"] = desired };
        remote.Milestones[title] = (await api(live is null ? "POST" : "PATCH", RepoPath + "/milestones" + (live is null ? "" : "/" + Number(live, "id")), payload, cancellation))!.AsObject();
        return live is null ? "created" : "updated";
    }

    public static async Task<JsonArray> ReplaceLabelsAsync(BacklogApi api, int number, JsonArray ids, CancellationToken cancellation = default) =>
        (await api("PUT", RepoPath + "/issues/" + number + "/labels", new JsonObject { ["labels"] = ids.DeepClone() }, cancellation))?.AsArray()
        ?? throw new InvalidDataException("Missing label replacement response");

    public static IReadOnlyList<string> VerifyConvergence(BacklogManifest manifest, BacklogRemoteState remote)
    {
        List<string> failures = [];
        var managed = manifest.LabelByName.Keys.ToHashSet(StringComparer.Ordinal);
        foreach (var label in manifest.Labels)
        { string name = Text(label, "name"); if (!remote.Labels.TryGetValue(name, out var live)) failures.Add("missing_label:" + name); else if (!LabelMatches(label, live)) failures.Add("label_metadata:" + name); }
        foreach (var milestone in manifest.Milestones)
        {
            string key = Text(milestone, "key");
            if (!remote.Milestones.TryGetValue(Text(milestone, "title"), out var live)) { failures.Add("missing_milestone:" + key); continue; }
            try { if (Text(live, "description") != Text(milestone, "description") || Text(live, "state") != BacklogPolicy.ResolveMilestone(milestone, live, manifest.IssueByKey, remote)) failures.Add("milestone_metadata:" + key); }
            catch (InvalidDataException e) { failures.Add("milestone_state:" + key + ":" + e.Message); }
            if (live["due_on"] is not null && live["due_on"]!.ToString().Length != 0) failures.Add("milestone_due_date:" + key);
        }
        foreach (var issue in manifest.Issues)
        {
            string key = Text(issue, "key");
            if (!remote.Issues.TryGetValue(key, out var live)) { failures.Add("missing_issue:" + key); continue; }
            string body = Text(live, "body");
            if (Text(live, "title") != Text(issue, "title")) failures.Add("issue_title:" + key);
            if (BacklogPolicy.HiddenKey(body) != key) failures.Add("hidden_key:" + key);
            if (body != BacklogPolicy.MergeBody(body, key, BacklogPolicy.RenderBody(issue, manifest.IssueByKey, remote.Issues))) failures.Add("managed_body_content:" + key);
            string title = Text(manifest.MilestoneByKey[Text(issue, "milestone")], "title");
            if (!remote.Milestones.TryGetValue(title, out var milestone) || Number(milestone, "id") <= 0 || Number(live["milestone"], "id") != Number(milestone, "id")) failures.Add("issue_milestone:" + key);
            if (!BacklogPolicy.DesiredLabels(issue, manifest.IssueByKey, remote, managed).ToHashSet(StringComparer.Ordinal).SetEquals(BacklogPolicy.RemoteLabels(live))) failures.Add("issue_labels:" + key);
            if (Text(issue, "desiredState") == "closed")
            {
                if (Text(live, "state") != "closed") failures.Add("issue_state:" + key);
                if (!remote.ReviewedClosures.Contains(key)) failures.Add("closure_evidence:" + key);
                try { BacklogPolicy.AssertClosureAllowed(issue, manifest.IssueByKey, remote, false); } catch (InvalidDataException) { failures.Add("closure_prerequisite:" + key); }
            }
            if (Text(issue, "desiredState") == "reopen" && Text(live, "state") != "open") failures.Add("issue_state:" + key);
            foreach (string linked in Strings(issue, "dependencyKeys").Concat(Text(issue, "parentKey").Length == 0 ? [] : new[] { Text(issue, "parentKey") }))
                if (!remote.Issues.TryGetValue(linked, out var linkedIssue) || Number(linkedIssue, "number") <= 0 || !body.Contains("#" + Number(linkedIssue, "number"), StringComparison.Ordinal)) failures.Add("issue_link:" + key + ":" + linked);
        }
        return failures;
    }

    private static bool LabelMatches(JsonObject desired, JsonObject live) => Text(desired, "color").Equals(Text(live, "color").TrimStart('#'), StringComparison.OrdinalIgnoreCase) && Text(desired, "description") == Text(live, "description");
    private static async Task<IReadOnlyList<JsonObject>> CollectionAsync(BacklogApi api, string path, CancellationToken cancellation)
    {
        List<JsonObject> result = [];
        for (int page = 1; page <= 10000; page++)
        {
            var batch = (await api("GET", path + (path.Contains('?') ? "&" : "?") + "limit=50&page=" + page, null, cancellation)) as JsonArray ?? throw new InvalidDataException("Expected paged Gitea collection");
            result.AddRange(batch.Select(x => x!.AsObject())); if (batch.Count < 50) return result;
        }
        throw new InvalidDataException("Gitea pagination limit exceeded");
    }

    private static HttpClient CreateClient(bool requireAuthentication)
    {
        HttpClient client = new(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = new Uri(BacklogContracts.BaseUrl + "/api/v1"), Timeout = TimeSpan.FromSeconds(60) };
        string? token = new[] { "CLROS_GITEA_TOKEN", "GITEA_TOKEN", "TEA_TOKEN" }.Select(Environment.GetEnvironmentVariable).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
        token ??= TeaToken();
        if (!string.IsNullOrWhiteSpace(token)) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("token", token);
        else
        {
            string? username = Environment.GetEnvironmentVariable("CLROS_GITEA_USERNAME"), password = Environment.GetEnvironmentVariable("CLROS_GITEA_PASSWORD");
            if (string.IsNullOrWhiteSpace(username) && string.IsNullOrWhiteSpace(password))
            {
                username = Environment.GetEnvironmentVariable("CODEX_GITEA_USERNAME");
                password = Environment.GetEnvironmentVariable("CODEX_GITEA_PASSWORD");
            }
            if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password)) client.DefaultRequestHeaders.Authorization = new("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(username + ":" + password)));
            else if (requireAuthentication) { client.Dispose(); throw new InvalidOperationException("Authenticated Gitea access is required for --apply. Configure the local-gitea tea login or set CLROS_GITEA_TOKEN."); }
        }
        return client;
    }
    private static string? TeaToken()
    {
        var candidates = new List<string>();
        foreach (string name in new[] { "APPDATA", "LOCALAPPDATA" }) if (Environment.GetEnvironmentVariable(name) is string path) candidates.Add(Path.Combine(path, "tea", "config.yml"));
        if (Environment.GetEnvironmentVariable("USERPROFILE") is string profile)
            candidates.AddRange(new[] { ".config/tea/config.yml", ".tea/config.yml", ".gitea/tea.yml" }.Select(x => Path.Combine(profile, x)));
        foreach (string path in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!File.Exists(path)) continue;
            var records = new List<Dictionary<string, string>>(); Dictionary<string, string>? record = null;
            foreach (string line in File.ReadLines(path))
            {
                var start = Regex.Match(line, @"^\s*-\s+name\s*:\s*(.+?)\s*$");
                if (start.Success) { record = new(StringComparer.OrdinalIgnoreCase) { ["name"] = Scalar(start.Groups[1].Value) }; records.Add(record); }
                else if (record is not null)
                { var item = Regex.Match(line, @"^\s+(url|token)\s*:\s*(.+?)\s*$"); if (item.Success) record[item.Groups[1].Value] = Scalar(item.Groups[2].Value); }
            }
            foreach (var item in records)
                if (item.GetValueOrDefault("name") == "local-gitea" && item.GetValueOrDefault("url")?.TrimEnd('/') == BacklogContracts.BaseUrl && item.GetValueOrDefault("token") is string token && token.Length != 0) return token;
        }
        return null;
    }
    private static string Scalar(string value) => value.Length >= 2 && (value[0] == '"' && value[^1] == '"' || value[0] == '\'' && value[^1] == '\'') ? value[1..^1] : value;
    private static async Task<JsonNode?> SendAsync(HttpClient client, string method, string path, JsonObject? body, CancellationToken cancellation)
    {
        if (method is not ("GET" or "POST" or "PUT" or "PATCH") || !(path.StartsWith(RepoPath + "/", StringComparison.Ordinal) || method == "GET" && path == RepoPath) || path.Contains("..", StringComparison.Ordinal)) throw new InvalidOperationException("Invalid Gitea request scope");
        using HttpRequestMessage request = new(new HttpMethod(method), new Uri(BacklogContracts.BaseUrl + "/api/v1" + path));
        if (body is not null) request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellation);
        if (!response.IsSuccessStatusCode) throw new IOException("Gitea HTTP status " + (int)response.StatusCode);
        const int maxBytes = 16 * 1024 * 1024;
        if (response.Content.Headers.ContentLength > maxBytes) throw new IOException("Gitea response size limit exceeded");
        await using var stream = await response.Content.ReadAsStreamAsync(cancellation);
        using MemoryStream content = new(); byte[] buffer = new byte[8192];
        int read;
        while ((read = await stream.ReadAsync(buffer, cancellation)) != 0)
        { if (content.Length + read > maxBytes) throw new IOException("Gitea response size limit exceeded"); content.Write(buffer, 0, read); }
        return content.Length == 0 ? null : JsonNode.Parse(content.ToArray(), documentOptions: new JsonDocumentOptions { MaxDepth = 64 });
    }

    private sealed class Actions(TextWriter output)
    {
        private static readonly string[] Names = ["created", "updated", "unchanged", "skipped", "failed", "wouldCreate", "wouldUpdate"];
        private readonly Dictionary<string, List<string>> values = Names.ToDictionary(x => x, _ => new List<string>(), StringComparer.Ordinal);
        public void Add(string action, string key) => values[action].Add(key);
        public int Count(string action) => values[action].Count;
        public void Write()
        {
            foreach (string action in Names)
            {
                string[] keys = values[action].Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
                if (keys.Length == 0) output.WriteLine("SYNC_KEYS action=" + action + " count=0 keys=none");
                for (int offset = 0; offset < keys.Length; offset += 50)
                    output.WriteLine($"SYNC_KEYS action={action} count={keys.Length} chunk={offset / 50 + 1}/{(keys.Length + 49) / 50} keys={string.Join(',', keys.Skip(offset).Take(50))}");
            }
            output.WriteLine("SYNC_SUMMARY " + string.Join(' ', Names.Select(x => x + "=" + values[x].Count)));
        }
    }
}
