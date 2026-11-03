using OS.Tooling;
using System.Text.Json.Nodes;
using static OS.Tooling.BacklogManifest;

internal static partial class BacklogSyncTests
{
    [HostTest("gitea backlog C# preview never writes")]
    internal static void Preview()
    {
        var fixture = new Fixture();
        using StringWriter output = new();
        HostCheck.Equal(0, BacklogSync.ReconcileAsync(fixture.Manifest, false, fixture.Api, output).GetAwaiter().GetResult());
        HostCheck.True(fixture.Calls.All(x => x.Method == "GET"));
        HostCheck.True(output.ToString().Contains("wouldUpdate=", StringComparison.Ordinal));
        HostCheck.True(fixture.Issues.All(x => Text(x, "state") == "open"));
    }

    [HostTest("gitea backlog C# apply closes dependencies first and converges")]
    internal static void Apply()
    {
        var fixture = new Fixture();
        using StringWriter output = new();
        HostCheck.Equal(0, BacklogSync.ReconcileAsync(fixture.Manifest, true, fixture.Api, output).GetAwaiter().GetResult());
        var closes = fixture.Calls.Where(x => x.Method == "PATCH" && x.Path.Contains("/issues/", StringComparison.Ordinal) && Text(x.Body, "state") == "closed").Select(x => x.Path.Split('/')[^1]);
        HostCheck.True(closes.SequenceEqual(new[] { "2", "3", "1" }));
        HostCheck.True(fixture.Calls.Where(x => x.Method == "PATCH" && x.Path.Contains("/issues/", StringComparison.Ordinal)).All(x => !x.Body!.ContainsKey("labels")));
        HostCheck.True(fixture.Calls.All(x => x.Method != "PUT"));
        HostCheck.Equal(fixture.Manifest.Issues.Count, fixture.Calls.Count(x => x.Method == "POST" && x.Path.EndsWith("/labels", StringComparison.Ordinal)));
        HostCheck.True(fixture.Calls.Where(x => x.Method == "PATCH" && x.Path.Contains("/issues/", StringComparison.Ordinal)).All(x => x.Body!["content_version"] is not null));
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
        var unreviewed = new Fixture
        {
            Reviewed = false
        };
        using StringWriter output = new();
        HostCheck.Equal(1, BacklogSync.ReconcileAsync(unreviewed.Manifest, true, unreviewed.Api, output).GetAwaiter().GetResult());
        HostCheck.True(!unreviewed.Calls.Any(x => x.Method == "PATCH" && Text(x.Body, "state") == "closed"));
        HostCheck.True(output.ToString().Contains("requires an evidence-review comment", StringComparison.Ordinal));
        var dropped = new Fixture
        {
            DropLabelMutation = true
        };
        HostCheck.Equal(1, BacklogSync.ReconcileAsync(dropped.Manifest, true, dropped.Api, output).GetAwaiter().GetResult());
        HostCheck.True(output.ToString().Contains("label readback differs", StringComparison.Ordinal));
        var duplicate = new Fixture();
        duplicate.Issues[0]["body"] = Text(duplicate.Issues[0], "body") + "\n<!-- clros-issue-key:M00-EPIC -->";
        bool rejected = false;
        try
        {
            BacklogSync.ReadRemoteAsync(duplicate.Manifest, duplicate.Api, output, default).GetAwaiter().GetResult();
        }
        catch (InvalidDataException)
        {
            rejected = true;
        }

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
        try
        {
            fixture.Manifest.SelectIssues("M99-999");
        }
        catch (InvalidDataException)
        {
            rejected = true;
        }

        HostCheck.True(rejected);
    }

    [HostTest("gitea wrong repository identity rejects before writes")]
    internal static void Identity()
    {
        foreach (var identity in new[]
        {
            (10, "admin/CLROS"),
            (9, "other/CLROS")
        }

        )
        {
            var fixture = new Fixture
            {
                RepositoryId = identity.Item1,
                RepositoryName = identity.Item2
            };
            using StringWriter output = new();
            bool rejected = false;
            try
            {
                BacklogSync.ReconcileAsync(fixture.Manifest, true, fixture.Api, output).GetAwaiter().GetResult();
            }
            catch (InvalidDataException)
            {
                rejected = true;
            }

            HostCheck.True(rejected);
            HostCheck.Equal(1, fixture.Calls.Count);
            HostCheck.True(fixture.Calls.All(x => x.Method == "GET"));
        }
    }

    [HostTest("gitea sync refresh preserves a post-list human note and ownership")]
    internal static void ConcurrentListEdit()
    {
        var fixture = new Fixture();
        fixture.Manifest.IssueByKey["M00-001"]["desiredState"] = "open";
        bool injected = false;
        fixture.BeforeRequest = (method, path, _) =>
        {
            if (injected || method != "GET" || !path.EndsWith("/issues/2", StringComparison.Ordinal))
                return;
            injected = true;
            var issue = fixture.Issues[1];
            issue["body"] = Text(issue, "body") + "\nConcurrent operator note";
            issue["content_version"] = 2L;
            issue["labels"]!.AsArray().Add(new JsonObject { ["id"] = 50, ["name"] = "human:new" });
            var labels = issue["labels"]!.AsArray();
            labels.Remove(labels.Single(x => Text(x, "name") == "agent:review"));
            labels.Add(fixture.Manifest.LabelByName["agent:claimed"].DeepClone());
        };
        using StringWriter output = new();
        HostCheck.Equal(0, BacklogSync.ReconcileAsync(fixture.Manifest.SelectIssues("M00-001"), true, fixture.Api, output).GetAwaiter().GetResult());
        HostCheck.True(injected);
        HostCheck.True(Text(fixture.Issues[1], "body").Contains("Concurrent operator note", StringComparison.Ordinal));
        HostCheck.True(BacklogPolicy.RemoteLabels(fixture.Issues[1]).Contains("human:new", StringComparer.Ordinal));
        HostCheck.True(BacklogPolicy.RemoteLabels(fixture.Issues[1]).Contains("agent:claimed", StringComparer.Ordinal));
    }

    [HostTest("gitea sync content conflict preserves the second edit without retry")]
    internal static void ConcurrentPatchEdit()
    {
        var fixture = new Fixture();
        bool injected = false;
        fixture.BeforeRequest = (method, path, _) =>
        {
            if (injected || method != "PATCH" || !path.EndsWith("/issues/2", StringComparison.Ordinal))
                return;
            injected = true;
            fixture.Issues[1]["body"] = Text(fixture.Issues[1], "body") + "\nSecond operator edit";
            fixture.Issues[1]["content_version"] = 2L;
        };
        using StringWriter output = new();
        HostCheck.Equal(1, BacklogSync.ReconcileAsync(fixture.Manifest.SelectIssues("M00-001"), true, fixture.Api, output).GetAwaiter().GetResult());
        HostCheck.True(injected);
        HostCheck.True(Text(fixture.Issues[1], "body").Contains("Second operator edit", StringComparison.Ordinal));
        HostCheck.Equal("open", Text(fixture.Issues[1], "state"));
        HostCheck.Equal(1, fixture.Calls.Count(x => x.Method == "PATCH"));
        HostCheck.True(output.ToString().Contains("409", StringComparison.Ordinal));
    }

    [HostTest("gitea additive label writes preserve a concurrent human label")]
    internal static void ConcurrentLabelEdit()
    {
        var fixture = new Fixture();
        bool injected = false;
        fixture.BeforeRequest = (method, path, _) =>
        {
            if (injected || method != "POST" || !path.EndsWith("/issues/2/labels", StringComparison.Ordinal))
                return;
            injected = true;
            fixture.Issues[1]["labels"]!.AsArray().Add(new JsonObject { ["id"] = 50, ["name"] = "human:late" });
        };
        using StringWriter output = new();
        HostCheck.Equal(0, BacklogSync.ReconcileAsync(fixture.Manifest.SelectIssues("M00-001"), true, fixture.Api, output).GetAwaiter().GetResult());
        HostCheck.True(injected);
        HostCheck.True(BacklogPolicy.RemoteLabels(fixture.Issues[1]).Contains("human:late", StringComparer.Ordinal));
        fixture.Calls.Clear();
        HostCheck.Equal(0, BacklogSync.ReconcileAsync(fixture.Manifest.SelectIssues("M00-001"), true, fixture.Api, output).GetAwaiter().GetResult());
        HostCheck.True(fixture.Calls.All(x => x.Method == "GET"));
    }

    [HostTest("gitea missing version and content readback loss fail explicitly")]
    internal static void ContentReadback()
    {
        foreach (bool missing in new[]
        {
            true,
            false
        }

        )
        {
            var fixture = new Fixture
            {
                DropContentMutation = !missing
            };
            if (missing)
                fixture.Issues[1].Remove("content_version");
            using StringWriter output = new();
            HostCheck.Equal(1, BacklogSync.ReconcileAsync(fixture.Manifest.SelectIssues("M00-001"), true, fixture.Api, output).GetAwaiter().GetResult());
            HostCheck.Equal("open", Text(fixture.Issues[1], "state"));
            HostCheck.True(output.ToString().Contains(missing ? "content_version" : "content readback differs", StringComparison.Ordinal));
            if (missing)
                HostCheck.True(fixture.Calls.All(x => x.Method == "GET"));
        }
    }

    [HostTest("gitea current content versions accept zero and reject negative")]
    internal static void ContentVersionBoundaries()
    {
        var fresh = new Fixture();
        fresh.Issues[1]["content_version"] = 0L;
        using StringWriter output = new();
        HostCheck.Equal(0, BacklogSync.ReconcileAsync(fresh.Manifest.SelectIssues("M00-001"), true, fresh.Api, output).GetAwaiter().GetResult());
        HostCheck.True(fresh.Calls.Any(x => x.Method == "PATCH" && x.Body!["content_version"]!.GetValue<long>() == 0));
        HostCheck.Equal(1L, fresh.Issues[1]["content_version"]!.GetValue<long>());
        HostCheck.Equal("closed", Text(fresh.Issues[1], "state"));
        var invalid = new Fixture();
        invalid.Issues[1]["content_version"] = -1L;
        HostCheck.Equal(1, BacklogSync.ReconcileAsync(invalid.Manifest.SelectIssues("M00-001"), true, invalid.Api, output).GetAwaiter().GetResult());
        HostCheck.True(invalid.Calls.All(x => x.Method == "GET"));
        HostCheck.Equal("open", Text(invalid.Issues[1], "state"));
    }

    [HostTest("gitea ambiguous open managed ownership fails before sticky deletion")]
    internal static void AmbiguousOpenOwnership()
    {
        var fixture = new Fixture();
        fixture.Manifest.IssueByKey["M00-001"]["desiredState"] = "open";
        fixture.Issues[1]["labels"]!.AsArray().Add(fixture.Manifest.LabelByName["agent:claimed"].DeepClone());
        bool deletionAttempted = false;
        fixture.BeforeRequest = (method, path, _) =>
        {
            if (method != "DELETE" || !path.Contains("/issues/2/labels/", StringComparison.Ordinal))
                return;
            deletionAttempted = true;
            // Complete the operator's handoff immediately before a stale DELETE.
            // The production guard must fail before this unsafe request is issued.
            var labels = fixture.Issues[1]["labels"]!.AsArray();
            labels.Remove(labels.Single(x => Text(x, "name") == "agent:claimed"));
        };
        using StringWriter output = new();
        HostCheck.Equal(1, BacklogSync.ReconcileAsync(fixture.Manifest.SelectIssues("M00-001"), true, fixture.Api, output).GetAwaiter().GetResult());
        HostCheck.True(!deletionAttempted && fixture.Calls.All(x => x.Method != "DELETE"));
        HostCheck.True(BacklogPolicy.RemoteLabels(fixture.Issues[1]).Contains("agent:claimed", StringComparer.Ordinal) && BacklogPolicy.RemoteLabels(fixture.Issues[1]).Contains("agent:review", StringComparer.Ordinal));
        HostCheck.True(output.ToString().Contains("Open issue ownership labels conflict", StringComparison.Ordinal));
    }
}
