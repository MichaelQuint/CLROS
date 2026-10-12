using OS.Tooling;
using System.Text.Json.Nodes;
using static OS.Tooling.BacklogManifest;

internal static class BacklogSyncTests
{
    [HostTest("gitea backlog C# preview never writes")]
    internal static void Preview()
    {
        var fixture = new Fixture(); using StringWriter output = new();
        HostCheck.Equal(0, BacklogSync.ReconcileAsync(fixture.Manifest, false, fixture.Api, output).GetAwaiter().GetResult());
        HostCheck.True(fixture.Calls.All(x => x.Method == "GET"));
        HostCheck.True(output.ToString().Contains("wouldUpdate=", StringComparison.Ordinal));
        HostCheck.True(fixture.Issues.All(x => Text(x, "state") == "open"));
    }

    [HostTest("gitea backlog C# apply closes dependencies first and converges")]
    internal static void Apply()
    {
        var fixture = new Fixture(); using StringWriter output = new();
        HostCheck.Equal(0, BacklogSync.ReconcileAsync(fixture.Manifest, true, fixture.Api, output).GetAwaiter().GetResult());
        var closes = fixture.Calls.Where(x => x.Method == "PATCH" && x.Path.Contains("/issues/", StringComparison.Ordinal) && Text(x.Body, "state") == "closed").Select(x => x.Path.Split('/')[^1]);
        HostCheck.True(closes.SequenceEqual(new[] { "2", "3", "1" }));
        HostCheck.True(fixture.Calls.Where(x => x.Method == "PATCH" && x.Path.Contains("/issues/", StringComparison.Ordinal)).All(x => !x.Body!.ContainsKey("labels")));
        HostCheck.Equal(fixture.Manifest.Issues.Count, fixture.Calls.Count(x => x.Method == "PUT" && x.Path.EndsWith("/labels", StringComparison.Ordinal)));
        HostCheck.True(fixture.Issues.All(x => Text(x, "body").Contains("Human note $keep", StringComparison.Ordinal)));
        HostCheck.True(fixture.Issues.All(x => BacklogPolicy.RemoteLabels(x).Contains("human:owned", StringComparer.Ordinal)));
        HostCheck.True(output.ToString().Contains("SYNC_VERIFY_PASS", StringComparison.Ordinal));
        fixture.Calls.Clear();
        HostCheck.Equal(0, BacklogSync.ReconcileAsync(fixture.Manifest, true, fixture.Api, output).GetAwaiter().GetResult());
        HostCheck.True(fixture.Calls.All(x => x.Method == "GET"));
    }

    [HostTest("gitea backlog C# refuses unreviewed closure and checks readback")]
    internal static void Failures()
    {
        var unreviewed = new Fixture { Reviewed = false }; using StringWriter output = new();
        HostCheck.Equal(1, BacklogSync.ReconcileAsync(unreviewed.Manifest, true, unreviewed.Api, output).GetAwaiter().GetResult());
        HostCheck.True(!unreviewed.Calls.Any(x => x.Method == "PATCH" && Text(x.Body, "state") == "closed"));
        HostCheck.True(output.ToString().Contains("requires an evidence-review comment", StringComparison.Ordinal));
        var dropped = new Fixture { DropLabelReplacement = true };
        HostCheck.Equal(1, BacklogSync.ReconcileAsync(dropped.Manifest, true, dropped.Api, output).GetAwaiter().GetResult());
        HostCheck.True(output.ToString().Contains("issue_labels:", StringComparison.Ordinal));
        var duplicate = new Fixture(); duplicate.Issues[0]["body"] = Text(duplicate.Issues[0], "body") + "\n<!-- clros-issue-key:M00-EPIC -->";
        bool rejected = false;
        try { BacklogSync.ReadRemoteAsync(duplicate.Manifest, duplicate.Api, output, default).GetAwaiter().GetResult(); }
        catch (InvalidDataException) { rejected = true; }
        HostCheck.True(rejected);
    }

    [HostTest("gitea scoped sync preserves unselected issues and full dependency graph")]
    internal static void Scoped()
    {
        var fixture = new Fixture();
        var selected = fixture.Manifest.SelectIssues("M00-001");
        HostCheck.Equal(1, selected.Issues.Count);
        HostCheck.Equal(3, selected.IssueByKey.Count);
        string untouched = fixture.Issues[2].ToJsonString();
        using StringWriter output = new();
        HostCheck.Equal(0, BacklogSync.ReconcileAsync(selected, true, fixture.Api, output).GetAwaiter().GetResult());
        HostCheck.Equal(untouched, fixture.Issues[2].ToJsonString());
        HostCheck.True(fixture.Calls.Where(x => x.Method != "GET").All(x => x.Path.StartsWith("/repos/admin/CLROS/issues/2", StringComparison.Ordinal)));
        fixture.Calls.Clear();
        HostCheck.Equal(0, BacklogSync.ReconcileAsync(selected, false, fixture.Api, output).GetAwaiter().GetResult());
        HostCheck.True(fixture.Calls.All(x => x.Method == "GET"));
        bool rejected = false;
        try { fixture.Manifest.SelectIssues("M99-999"); } catch (InvalidDataException) { rejected = true; }
        HostCheck.True(rejected);
    }

    [HostTest("gitea wrong repository identity rejects before writes")]
    internal static void Identity()
    {
        foreach (var identity in new[] { (10, "admin/CLROS"), (9, "other/CLROS") })
        {
            var fixture = new Fixture { RepositoryId = identity.Item1, RepositoryName = identity.Item2 };
            using StringWriter output = new();
            bool rejected = false;
            try { BacklogSync.ReconcileAsync(fixture.Manifest, true, fixture.Api, output).GetAwaiter().GetResult(); }
            catch (InvalidDataException) { rejected = true; }
            HostCheck.True(rejected);
            HostCheck.Equal(1, fixture.Calls.Count);
            HostCheck.True(fixture.Calls.All(x => x.Method == "GET"));
        }
    }

    private sealed class Fixture
    {
        public BacklogManifest Manifest { get; }
        public List<JsonObject> Issues { get; } = [];
        public List<(string Method, string Path, JsonObject? Body)> Calls { get; } = [];
        public bool Reviewed { get; init; } = true;
        public int RepositoryId { get; init; } = 9;
        public string RepositoryName { get; init; } = "admin/CLROS";
        public bool DropLabelReplacement { get; init; }
        private readonly List<JsonObject> labels = [];
        private readonly JsonObject milestone = new() { ["id"] = 7, ["title"] = "Fixture", ["description"] = "Current", ["state"] = "open", ["due_on"] = null };

        public Fixture()
        {
            string[] names = ["agent:done", "agent:review", "type:epic", "type:implementation", "human:owned"];
            for (int i = 0; i < names.Length; i++) labels.Add(new() { ["id"] = i + 1, ["name"] = names[i], ["color"] = "aabbcc", ["description"] = "Fixture" });
            JsonObject Issue(string key, string parent, bool epic, params string[] dependencies) => new()
            {
                ["key"] = key, ["title"] = key, ["milestone"] = "M00", ["parentKey"] = parent, ["desiredState"] = "closed", ["summary"] = "Summary " + key,
                ["dependencyKeys"] = new JsonArray(dependencies.Select(x => (JsonNode?)JsonValue.Create(x)).ToArray()), ["labels"] = new JsonArray("agent:done", epic ? "type:epic" : "type:implementation"),
                ["priority"] = "p1", ["collisionLane"] = "", ["currentEvidence"] = "Reviewed fixture", ["implementationNotes"] = new JsonArray("Do the work"),
                ["referenceInfluences"] = new JsonArray(), ["ownedPaths"] = new JsonArray("tests/" + key), ["forbiddenPaths"] = new JsonArray("src/"),
                ["acceptanceCriteria"] = new JsonArray("Checks pass"), ["requiredMarkers"] = new JsonArray("FIXTURE_OK"), ["requiredCommands"] = new JsonArray("dotnet run"),
                ["documentationPaths"] = new JsonArray("docs/issue-labels.md"), ["evidenceBoundary"] = "Offline fixture only"
            };
            JsonObject[] issues = [Issue("M00-EPIC", "", true), Issue("M00-001", "M00-EPIC", false), Issue("M00-002", "M00-EPIC", false, "M00-001")];
            JsonObject desiredMilestone = new() { ["key"] = "M00", ["title"] = "Fixture", ["description"] = "Current", ["state"] = "open" };
            Manifest = new() { Source = new(), Issues = issues, Labels = labels.Where(x => Text(x, "name") != "human:owned").ToArray(), Milestones = [desiredMilestone],
                IssueByKey = issues.ToDictionary(x => Text(x, "key"), StringComparer.Ordinal), LabelByName = labels.Where(x => Text(x, "name") != "human:owned").ToDictionary(x => Text(x, "name"), StringComparer.Ordinal),
                MilestoneByKey = new() { ["M00"] = desiredMilestone }, Dependencies = new() };
            for (int i = 0; i < issues.Length; i++) Issues.Add(new()
            {
                ["number"] = i + 1, ["title"] = Text(issues[i], "title"), ["body"] = "Human note $keep\n<!-- clros-issue-key:" + Text(issues[i], "key") + " -->",
                ["state"] = "open", ["milestone"] = new JsonObject { ["id"] = 7 },
                ["labels"] = new JsonArray(labels.Single(x => Text(x, "name") == "agent:review").DeepClone(), labels.Single(x => Text(x, "name") == "human:owned").DeepClone())
            });
        }

        public Task<JsonNode?> Api(string method, string path, JsonObject? body, CancellationToken cancellation)
        {
            Calls.Add((method, path, (JsonObject?)body?.DeepClone()));
            JsonNode? result;
            string clean = path.Split('?')[0];
            JsonArray Array(IEnumerable<JsonObject> values) => new(values.Select(x => x.DeepClone()).ToArray());
            if (method == "GET" && clean == "/repos/admin/CLROS")
                result = new JsonObject { ["id"] = RepositoryId, ["full_name"] = RepositoryName };
            else if (method == "GET" && clean.EndsWith("/comments", StringComparison.Ordinal))
            {
                int number = int.Parse(clean.Split('/')[^2]); string key = BacklogPolicy.HiddenKey(Text(Issues.Single(x => Number(x, "number") == number), "body"));
                result = Reviewed ? new JsonArray(new JsonObject { ["body"] = BacklogContracts.EvidenceMarker(key) }) : new JsonArray();
            }
            else if (method == "GET" && clean.EndsWith("/milestones", StringComparison.Ordinal)) result = new JsonArray(milestone.DeepClone());
            else if (method == "GET" && clean.EndsWith("/issues", StringComparison.Ordinal)) result = Array(Issues);
            else if (method == "GET" && clean.EndsWith("/labels", StringComparison.Ordinal)) result = Array(labels);
            else
            {
                bool isLabels = clean.EndsWith("/labels", StringComparison.Ordinal);
                int number = int.Parse(clean.Split('/')[isLabels ? ^2 : ^1]);
                var issue = Issues.Single(x => Number(x, "number") == number);
                if (method == "PATCH")
                {
                    foreach (var item in body!) issue[item.Key] = item.Key == "milestone" ? new JsonObject { ["id"] = item.Value!.GetValue<int>() } : item.Value?.DeepClone();
                    result = issue.DeepClone();
                }
                else if (method == "PUT" && isLabels)
                {
                    var values = body!["labels"]!.AsArray().Select(x => x!.GetValue<int>()).Select(id => labels.Single(x => Number(x, "id") == id));
                    var replacement = Array(values); if (!DropLabelReplacement) issue["labels"] = replacement.DeepClone(); result = replacement;
                }
                else if (method == "GET") result = issue.DeepClone();
                else throw new InvalidOperationException("Unexpected fixture request " + method + " " + path);
            }
            return Task.FromResult<JsonNode?>(result);
        }
    }
}
