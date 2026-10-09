using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using static OS.Tooling.BacklogManifest;

namespace OS.Tooling;

/// <summary>Offline policy controls shared by the CLI self-test and host suite. Never opens a socket.</summary>
public static class BacklogSelfTest
{
    private sealed record MilestoneCase(string Name, string Intent, string Current, string Action, string Desired, string Failure = "");
    private static readonly MilestoneCase[] MilestoneCases =
    [
        new("open-unchanged", "open", "open", "unchanged", "open"), new("preserve-closed", "open", "closed", "unchanged", "closed"),
        new("closed-unchanged", "closed", "closed", "unchanged", "closed"), new("closed-description", "open", "closed", "update", "closed"),
        new("explicit-reopen", "reopen", "closed", "update", "open"), new("reopen-unchanged", "reopen", "open", "unchanged", "open"),
        new("reviewed-close", "closed", "open", "update", "closed"), new("missing-open", "open", "missing", "create", "open"),
        new("missing-closed", "closed", "missing", "create", "open"), new("missing-reopen", "reopen", "missing", "create", "open"),
        new("missing-child", "closed", "open", "failed", "open", "absent from the tracker"),
        new("missing-epic", "closed", "open", "failed", "open", "is absent from the manifest"),
        new("missing-epic-label", "closed", "open", "failed", "open", "lacks exact type:epic label"),
        new("nonordinal-epic-label", "closed", "open", "failed", "open", "lacks exact type:epic label"),
        new("wrong-membership", "closed", "open", "failed", "open", "different milestone"),
        new("open-child", "closed", "open", "failed", "open", "not closed in the tracker"),
        new("unreviewed-child", "closed", "open", "failed", "open", "lacks reviewed evidence"),
        new("unreviewed-epic", "closed", "open", "failed", "open", "lacks reviewed evidence"),
        new("no-closed-intent", "closed", "open", "failed", "open", "lacks manifest closed intent"),
        new("open-dependency", "closed", "open", "failed", "open", "not closed in the tracker"),
        new("open-epic", "closed", "open", "failed", "open", "not closed in the tracker"),
        new("open-supplement", "closed", "open", "failed", "open", "actual milestone member '#4' is not closed"),
        new("inconsistent-closed", "open", "closed", "failed", "closed", "actual milestone member '#4' is not closed"),
        new("invalid-remote-state", "open", "CLOSED", "failed", "CLOSED", "invalid current tracker state"),
        new("array-remote-state", "open", "closed", "failed", "closed", "invalid current tracker state"),
        new("nonordinal-remote-state", "open", "closed", "failed", "closed", "invalid current tracker state"),
        new("nonordinal-supplement-state", "closed", "open", "failed", "open", "actual milestone member '#4' is not closed"),
        new("due-date", "closed", "closed", "unchanged", "closed")
    ];
    public static IReadOnlyList<string> MilestoneCaseNames { get; } = Array.AsReadOnly(MilestoneCases.Select(x => x.Name).ToArray());

    public static void Run(BacklogManifest manifest, TextWriter output)
    {
        var issue = manifest.IssueByKey["M00-001"];
        string managed = BacklogPolicy.RenderBody(issue, manifest.IssueByKey, new Dictionary<string, JsonObject>());
        string existing = "USER_NOTE_BEFORE\n<!-- clros-issue-key:M00-001 -->\n" + BacklogContracts.ManagedStart + "\nOLD_MANAGED_TEXT\n" + BacklogContracts.ManagedEnd + "\nUSER_NOTE_AFTER";
        string merged = BacklogPolicy.MergeBody(existing, "M00-001", managed);
        Require(merged.Contains("USER_NOTE_BEFORE", StringComparison.Ordinal) && merged.Contains("USER_NOTE_AFTER", StringComparison.Ordinal) && !merged.Contains("OLD_MANAGED_TEXT", StringComparison.Ordinal) &&
            merged.Contains(Text(issue, "summary"), StringComparison.Ordinal) && Regex.Matches(merged, Regex.Escape(BacklogContracts.ManagedStart)).Count == 1 &&
            Regex.Matches(merged, Regex.Escape(BacklogContracts.ManagedEnd)).Count == 1 && BacklogPolicy.HiddenKey(merged) == "M00-001", "managed_body");
        Require(BacklogPolicy.MergeBody(merged, "M00-001", managed) == merged, "managed_body_idempotence");
        output.WriteLine("SYNC_SELF_TEST_MANAGED_BODY_PASS");
        Require(BacklogPolicy.HiddenKey("<!-- clros-issue-key:M02-020 -->") == "M02-020", "adoption");
        bool rejected = false;
        try { BacklogPolicy.HiddenKey("<!-- clros-issue-key:M02-020 -->\n<!-- clros-issue-key:M02-020 -->"); }
        catch (InvalidDataException) { rejected = true; }
        Require(rejected, "duplicate_hidden_key");
        output.WriteLine("SYNC_SELF_TEST_HIDDEN_KEY_ADOPTION_PASS key=M02-020 markers=1 wouldCreate=0 duplicateMarkersRejected=true");
        TestStatePolicy(); output.WriteLine("SYNC_SELF_TEST_STATE_POLICY_PASS");
        foreach (var test in MilestoneCases)
        {
            foreach (bool apply in new[] { false, true }) TestMilestone(test, apply);
            output.WriteLine("SYNC_SELF_TEST_MILESTONE_CASE_PASS name=" + test.Name + " modes=preview,apply");
        }
        output.WriteLine("SYNC_SELF_TEST_MILESTONE_POLICY_PASS cases=" + MilestoneCases.Length + " modes=preview,apply actualApiCalls=0");
    }

    private static JsonObject Issue(string key, string state = "closed", string parent = "", string[]? dependencies = null, bool epic = false) => new()
    {
        ["key"] = key, ["milestone"] = key.Split('-')[0], ["desiredState"] = state, ["parentKey"] = parent,
        ["dependencyKeys"] = new JsonArray((dependencies ?? []).Select(x => (JsonNode?)JsonValue.Create(x)).ToArray()),
        ["labels"] = new JsonArray(epic ? "type:epic" : "type:implementation"), ["collisionLane"] = "", ["ownedPaths"] = new JsonArray("tests/selftest/" + key + ".cs")
    };
    private static JsonObject Remote(int number, int milestone = 7, string state = "closed") => new()
        { ["number"] = number, ["state"] = state, ["milestone"] = new JsonObject { ["id"] = milestone }, ["labels"] = new JsonArray() };

    private static void TestMilestone(MilestoneCase test, bool apply)
    {
        JsonObject milestone = new() { ["key"] = "M00", ["title"] = "Self-test", ["description"] = "Current", ["state"] = test.Intent };
        var epic = Issue("M00-EPIC", dependencies: ["M01-001"], epic: true); var child = Issue("M00-001", parent: "M00-EPIC"); var dependency = Issue("M01-001");
        Dictionary<string, JsonObject> manifest = new(StringComparer.Ordinal) { ["M00-EPIC"] = epic, ["M00-001"] = child, ["M01-001"] = dependency };
        BacklogRemoteState remote = new();
        var liveEpic = Remote(1); var liveChild = Remote(2); var liveDependency = Remote(3, 8); var supplement = Remote(4);
        remote.Issues["M00-EPIC"] = liveEpic; remote.Issues["M00-001"] = liveChild; remote.Issues["M01-001"] = liveDependency;
        remote.ReviewedClosures.UnionWith(manifest.Keys);
        var unrelated = Remote(5, 8, "open"); var pr = Remote(6, 7, "open"); pr["pull_request"] = new JsonObject();
        remote.AllIssues.AddRange([liveEpic, liveChild, liveDependency, supplement, unrelated, pr]);
        JsonObject live = new() { ["id"] = 7, ["title"] = "Self-test", ["description"] = "Current", ["state"] = test.Current, ["due_on"] = null };
        if (test.Current != "missing") remote.Milestones["Self-test"] = live;
        switch (test.Name)
        {
            case "closed-description": live["description"] = "Old"; break;
            case "missing-child": remote.Issues.Remove("M00-001"); break;
            case "missing-epic": manifest.Remove("M00-EPIC"); break;
            case "missing-epic-label": epic["labels"] = new JsonArray("type:implementation"); remote.ReviewedClosures.Remove("M00-001"); break;
            case "nonordinal-epic-label": epic["labels"] = new JsonArray("type:e\0pic"); break;
            case "wrong-membership": liveChild["milestone"]!["id"] = 8; break;
            case "open-child": liveChild["state"] = "open"; break;
            case "unreviewed-child": remote.ReviewedClosures.Remove("M00-001"); break;
            case "unreviewed-epic": remote.ReviewedClosures.Remove("M00-EPIC"); break;
            case "no-closed-intent": child["desiredState"] = "open"; break;
            case "open-dependency": liveDependency["state"] = "open"; break;
            case "open-epic": liveEpic["state"] = "open"; break;
            case "open-supplement": case "inconsistent-closed": supplement["state"] = "open"; break;
            case "array-remote-state": live["state"] = new JsonArray("closed"); break;
            case "nonordinal-remote-state": live["state"] = "cl\0osed"; break;
            case "nonordinal-supplement-state": supplement["state"] = "cl\0osed"; break;
            case "due-date": live["due_on"] = "2030-01-01T00:00:00Z"; break;
        }
        List<(string Method, string Path, JsonObject Body)> calls = [];
        Task<JsonNode?> Api(string method, string path, JsonObject? body, CancellationToken _)
        {
            calls.Add((method, path, (JsonObject)body!.DeepClone()));
            var response = (JsonObject)body.DeepClone(); response["id"] = 7; response["due_on"] = null; return Task.FromResult<JsonNode?>(response);
        }
        using StringWriter output = new(); string actual = "", failure = "";
        try { actual = BacklogSync.ReconcileMilestoneAsync(milestone, manifest, remote, apply, Api, output).GetAwaiter().GetResult(); }
        catch (InvalidDataException e) { actual = "failed"; failure = e.Message; }
        string expected = test.Action switch { "create" => apply ? "created" : "wouldCreate", "update" => apply ? "updated" : "wouldUpdate", _ => test.Action };
        Require(actual == expected, "milestone " + test.Name + " action=" + actual);
        bool writes = apply && test.Action is "create" or "update";
        Require(calls.Count == (writes ? 1 : 0), "milestone unexpected API call " + test.Name);
        if (writes)
        {
            var call = calls[0];
            Require(call.Method == (test.Action == "create" ? "POST" : "PATCH") && call.Path == "/repos/admin/CLROS/milestones" + (test.Action == "create" ? "" : "/7") &&
                call.Body.Count == 3 && Text(call.Body, "title") == "Self-test" && Text(call.Body, "description") == "Current" && Text(call.Body, "state") == test.Desired, "milestone wrong payload " + test.Name);
        }
        if (test.Action == "failed") Require(failure.Contains(test.Failure, StringComparison.Ordinal), "milestone wrong refusal " + test.Name + " " + failure);
        if (test.Name == "missing-closed") Require(output.ToString().Contains("reason=missing_container createState=open", StringComparison.Ordinal), "missing deferred closure");
        var verifyManifest = new BacklogManifest { Source = new(), Milestones = [milestone], Labels = [], Issues = [], IssueByKey = manifest,
            MilestoneByKey = new() { ["M00"] = milestone }, LabelByName = new(), Dependencies = new() };
        bool verified = BacklogSync.VerifyConvergence(verifyManifest, remote).Count == 0;
        bool expectedVerified = test.Action != "failed" && test.Name is not ("missing-closed" or "due-date") && (apply || test.Action == "unchanged");
        Require(verified == expectedVerified, "milestone convergence mismatch " + test.Name);
        if (test.Name == "open-child") Require(BacklogPolicy.ClosureFailure("M00-EPIC", manifest, remote, true) is null, "pending closure test invalid");
    }

    private static void TestStatePolicy()
    {
        var dependency = Issue("DEPENDENCY", "open"); var dependent = Issue("DEPENDENT", "open", dependencies: ["DEPENDENCY"]);
        var map = new Dictionary<string, JsonObject> { ["DEPENDENCY"] = dependency, ["DEPENDENT"] = dependent };
        BacklogRemoteState remote = new(); remote.Issues["DEPENDENCY"] = Remote(1); remote.Issues["DEPENDENT"] = Remote(2, state: "open"); remote.ReviewedClosures.Add("DEPENDENCY");
        Require(BacklogPolicy.AgentLabel(dependency, map, remote) == "agent:blocked" && BacklogPolicy.AgentLabel(dependent, map, remote) == "agent:blocked", "remote-only closure promoted");
        dependency["desiredState"] = "closed"; Require(BacklogPolicy.AgentLabel(dependent, map, remote) == "agent:ready", "reviewed closure not ready");
        remote.ReviewedClosures.Clear(); Require(BacklogPolicy.AgentLabel(dependent, map, remote) == "agent:blocked", "missing evidence promoted");
        remote.ReviewedClosures.Add("DEPENDENCY");
        var ancestor = Issue("ROOT"); map["ROOT"] = ancestor; dependency["dependencyKeys"] = new JsonArray("ROOT"); remote.Issues["ROOT"] = Remote(3, state: "open"); remote.ReviewedClosures.Add("ROOT");
        Require(BacklogPolicy.AgentLabel(dependent, map, remote) == "agent:blocked", "open transitive ancestor promoted");
        remote.Issues["ROOT"]["state"] = "closed"; Require(BacklogPolicy.AgentLabel(dependent, map, remote) == "agent:ready", "closed transitive tree blocked");
        var epic = Issue("EPIC", epic: true); var child = Issue("CHILD", parent: "EPIC");
        var epicMap = new Dictionary<string, JsonObject> { ["EPIC"] = epic, ["CHILD"] = child };
        BacklogRemoteState epicRemote = new(); epicRemote.Issues["EPIC"] = Remote(4, state: "open"); epicRemote.Issues["CHILD"] = Remote(5, state: "open"); epicRemote.ReviewedClosures.UnionWith(epicMap.Keys);
        Require(BacklogPolicy.UpdateOrder([epic, child]).Select(x => Text(x, "key")).SequenceEqual(new[] { "CHILD", "EPIC" }), "epic ordered before child");
        bool rejected = false; try { BacklogPolicy.AssertClosureAllowed(epic, epicMap, epicRemote, false); } catch (InvalidDataException) { rejected = true; }
        Require(rejected, "premature epic close"); BacklogPolicy.AssertClosureAllowed(epic, epicMap, epicRemote, true);
        var reopen = Issue("REOPEN", "reopen"); var reopenMap = new Dictionary<string, JsonObject> { ["REOPEN"] = reopen }; BacklogRemoteState reopenRemote = new(); reopenRemote.Issues["REOPEN"] = Remote(6);
        Require(BacklogPolicy.AgentLabel(reopen, reopenMap, reopenRemote) == "agent:ready", "reopen not ready");
        ancestor["ownedPaths"] = dependent["ownedPaths"]!.DeepClone(); remote.Issues["ROOT"]["state"] = "open"; remote.Issues["ROOT"]["labels"] = new JsonArray(new JsonObject { ["name"] = "agent:claimed" });
        dependent["dependencyKeys"] = new JsonArray(); Require(BacklogPolicy.AgentLabel(dependent, map, remote) == "agent:blocked", "claimed overlap not blocked");
    }
    private static void Require(bool value, string reason) { if (!value) throw new InvalidOperationException("Backlog self-test: " + reason); }
}
