using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using static OS.Tooling.BacklogManifest;

namespace OS.Tooling;

public static class BacklogPolicy
{
    private const string KeyPattern = @"<!--\s*clros-issue-key:([A-Z0-9-]+)\s*-->";
    public static string HiddenKey(string body)
    {
        var matches = Regex.Matches(body, KeyPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (matches.Count > 1) throw new InvalidDataException("Issue body must contain at most one hidden CLROS issue key marker; found " + matches.Count + ".");
        return matches.Count == 0 ? "" : matches[0].Groups[1].Value.ToUpperInvariant();
    }
    public static string MergeBody(string existing, string key, string managed)
    {
        _ = HiddenKey(existing);
        string hidden = "<!-- clros-issue-key:" + key + " -->";
        if (string.IsNullOrWhiteSpace(existing)) return hidden + "\n" + managed;
        string body = Regex.IsMatch(existing, KeyPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
            ? Regex.Replace(existing, KeyPattern, _ => hidden, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) : hidden + "\n" + existing;
        const string pattern = @"(?s)<!-- clros-managed-start -->.*?<!-- clros-managed-end -->";
        return Regex.IsMatch(body, pattern) ? Regex.Replace(body, pattern, _ => managed) : body.TrimEnd() + "\n\n" + managed;
    }
    public static string RenderBody(JsonObject issue, IReadOnlyDictionary<string, JsonObject> manifest, IReadOnlyDictionary<string, JsonObject> remote)
    {
        string key = Text(issue, "key"), parent = Text(issue, "parentKey");
        int NumberFor(string child) => remote.TryGetValue(child, out var value) ? Number(value, "number") : 0;
        string Link(string child) => NumberFor(child) is int number && number > 0 ? "#" + number + " (key " + child + ")" : child + " (will resolve after creation)";
        string List(string field) { var entries = Strings(issue, field); return entries.Length == 0 ? "None." : string.Join('\n', entries.Select(x => "- " + x)); }
        StringBuilder text = new();
        void Line(string value = "") => text.Append(value).Append('\n');
        void Section(string name, string content) { Line("## " + name); Line(); Line(content); Line(); }
        Line(BacklogContracts.ManagedStart);
        Section("Summary", Text(issue, "summary"));
        Line("## Coordination"); Line();
        Line("- **Manifest key:** `" + key + "`");
        Line("- **Parent:** " + (parent.Length == 0 ? "None (milestone epic)." : Link(parent)));
        foreach (var (label, field) in new[] { ("Milestone", "milestone"), ("Priority", "priority"), ("Collision lane", "collisionLane"), ("Desired state", "desiredState") })
            Line("- **" + label + ":** `" + (field == "collisionLane" && Text(issue, field).Length == 0 ? "None; dependencies and owned paths still apply." : Text(issue, field)) + "`");
        Line(); Line("### Declared managed labels"); Line(); Line(List("labels")); Line(); Line("### Dependencies"); Line();
        var dependencies = Strings(issue, "dependencyKeys");
        Line(dependencies.Length == 0 ? "- None; this slice is dependency-free." : string.Join('\n', dependencies.Select(x => "- [ ] " + Link(x))));
        if (key.EndsWith("-EPIC", StringComparison.Ordinal))
        {
            var children = manifest.Values.Where(x => Text(x, "parentKey") == key).OrderBy(x => Text(x, "key"), StringComparer.Ordinal).ToArray();
            if (children.Length != 0)
            {
                Line(); Line("### Child checklist"); Line();
                foreach (var child in children)
                {
                    string childKey = Text(child, "key");
                    string optional = RequiredForParent(child) ? "" : " [OPTIONAL]";
                    int number = NumberFor(childKey);
                    Line("- [ ] " + (number > 0 ? "#" + number + " (key " + childKey + ")" : childKey) + optional + " - " + Text(child, "summary") + (number > 0 ? "" : " (will resolve after creation)"));
                }
            }
        }
        Line(); Section("Current evidence", Text(issue, "currentEvidence")); Section("Implementation notes", List("implementationNotes"));
        var references = Objects(issue, "referenceInfluences");
        Section("Reference influences", references.Length == 0 ? "- None. Implement from CLROS contracts and authoritative specifications." :
            string.Join('\n', references.Select(x => "- **" + Text(x, "use") + "** " + Text(x, "path") + " - " + Text(x, "notes"))));
        Line("## File ownership"); Line(); Line("### Owned paths"); Line(); Line(List("ownedPaths")); Line(); Line("### Forbidden paths"); Line(); Line(List("forbiddenPaths")); Line();
        Section("Acceptance criteria", string.Join('\n', Strings(issue, "acceptanceCriteria").Select((x, i) => (i + 1) + ". " + x)));
        Section("Stable markers", List("requiredMarkers"));
        Section("Required commands", string.Join('\n', Strings(issue, "requiredCommands").Select(x => "- `" + x + "`")));
        Section("Canonical documentation", List("documentationPaths")); Section("Evidence boundary", Text(issue, "evidenceBoundary"));
        Line("Before closure, reserve a unique `done/stepNN.md` and post an evidence-review comment containing `" + BacklogContracts.EvidenceMarker(key) + "` plus exact commands, logs, hashes, failures, deferred work, and this boundary.");
        text.Append(BacklogContracts.ManagedEnd); return text.ToString();
    }

    public static string? ClosureFailure(string key, IReadOnlyDictionary<string, JsonObject> manifest, BacklogRemoteState remote, bool pending = false, HashSet<string>? visiting = null)
    {
        visiting ??= new(StringComparer.Ordinal);
        if (!visiting.Add(key)) return "closure prerequisite cycle includes '" + key + "'";
        try
        {
            if (!manifest.TryGetValue(key, out var issue)) return "closure prerequisite '" + key + "' is absent from the manifest";
            if (Text(issue, "desiredState") != "closed") return "closure prerequisite '" + key + "' lacks manifest closed intent";
            if (!remote.ReviewedClosures.Contains(key)) return "closure prerequisite '" + key + "' lacks reviewed evidence";
            if (!pending && (!remote.Issues.TryGetValue(key, out var live) || Text(live, "state") != "closed")) return "closure prerequisite '" + key + "' is not closed in the tracker";
            foreach (string child in Prerequisites(issue, manifest))
            { string? failure = ClosureFailure(child, manifest, remote, pending, visiting); if (failure is not null) return failure; }
            return null;
        }
        finally { visiting.Remove(key); }
    }

    public static void AssertClosureAllowed(JsonObject issue, IReadOnlyDictionary<string, JsonObject> manifest, BacklogRemoteState remote, bool pending)
    {
        string key = Text(issue, "key");
        if (!remote.ReviewedClosures.Contains(key)) throw new InvalidDataException("Closure for '" + key + "' requires an evidence-review comment containing '" + BacklogContracts.EvidenceMarker(key) + "'.");
        foreach (string prerequisite in Prerequisites(issue, manifest))
            if (ClosureFailure(prerequisite, manifest, remote, pending) is string failure) throw new InvalidDataException("Refusing to close '" + key + "': " + failure + ".");
    }

    /// <summary>Whether a child implicitly gates its parent; explicit dependency edges always remain required.</summary>
    public static bool RequiredForParent(JsonObject issue) => issue["requiredForParent"]?.GetValue<bool>() ?? true;

    private static IEnumerable<string> Prerequisites(JsonObject issue, IReadOnlyDictionary<string, JsonObject> manifest) =>
        Strings(issue, "dependencyKeys").Concat(Strings(issue, "labels").Contains("type:epic", StringComparer.Ordinal)
            ? manifest.Values.Where(x => Text(x, "parentKey") == Text(issue, "key") && RequiredForParent(x)).Select(x => Text(x, "key")) : []);

    public static IReadOnlyList<JsonObject> UpdateOrder(IReadOnlyList<JsonObject> issues)
    {
        var map = issues.ToDictionary(x => Text(x, "key"), StringComparer.Ordinal);
        var pending = issues.Where(x => Text(x, "desiredState") == "closed").ToDictionary(x => Text(x, "key"), StringComparer.Ordinal);
        List<JsonObject> ordered = [];
        while (pending.Count != 0)
        {
            int before = pending.Count;
            foreach (var issue in issues)
                if (pending.ContainsKey(Text(issue, "key")) && !Prerequisites(issue, map).Any(pending.ContainsKey))
                { ordered.Add(issue); pending.Remove(Text(issue, "key")); }
            if (before == pending.Count) throw new InvalidDataException("Could not order desired closures; unresolved dependency or parent cycle");
        }
        ordered.AddRange(issues.Where(x => Text(x, "desiredState") != "closed")); return ordered;
    }

    public static string AgentLabel(JsonObject issue, IReadOnlyDictionary<string, JsonObject> manifest, BacklogRemoteState remote)
    {
        string key = Text(issue, "key"), intent = Text(issue, "desiredState");
        remote.Issues.TryGetValue(key, out var live);
        if (intent == "closed") return live is not null && remote.ReviewedClosures.Contains(key) ? "agent:done" : "agent:review";
        if (live is not null)
        {
            if (Text(live, "state") == "closed" && intent != "reopen") return "agent:blocked";
            foreach (string sticky in new[] { "agent:claimed", "agent:review" }) if (RemoteLabels(live).Contains(sticky, StringComparer.Ordinal)) return sticky;
        }
        var labels = Strings(issue, "labels");
        foreach (string sticky in new[] { "agent:review", "agent:claimed" }) if (labels.Contains(sticky, StringComparer.Ordinal)) return sticky;
        if (labels.Contains("type:epic", StringComparer.Ordinal)) return "agent:blocked";
        if (Strings(issue, "dependencyKeys").Any(x => ClosureFailure(x, manifest, remote) is not null)) return "agent:blocked";
        foreach (var other in manifest.Values)
        {
            string otherKey = Text(other, "key");
            if (otherKey == key || !SharesScope(issue, other) || !remote.Issues.TryGetValue(otherKey, out var otherLive) || Text(otherLive, "state") != "open") continue;
            if (RemoteLabels(otherLive).Any(x => x is "agent:claimed" or "agent:review")) return "agent:blocked";
        }
        return "agent:ready";
    }
    public static bool SharesScope(JsonObject left, JsonObject right) => Text(left, "collisionLane").Length != 0 && Text(left, "collisionLane") == Text(right, "collisionLane") ||
        Strings(left, "ownedPaths").Select(NormalizePath).Intersect(Strings(right, "ownedPaths").Select(NormalizePath), StringComparer.Ordinal).Any();
    public static string[] RemoteLabels(JsonObject issue) => issue["labels"] is JsonArray labels ? labels.Select(x => Text(x, "name")).Where(x => x.Length != 0).ToArray() : [];
    public static string[] DesiredLabels(JsonObject issue, IReadOnlyDictionary<string, JsonObject> manifest, BacklogRemoteState remote, IReadOnlySet<string> managed) =>
        new[] { AgentLabel(issue, manifest, remote) }.Concat(Strings(issue, "labels").Where(x => !x.StartsWith("agent:", StringComparison.Ordinal)))
        .Concat(remote.Issues.TryGetValue(Text(issue, "key"), out var live) ? RemoteLabels(live).Where(x => !managed.Contains(x)) : []).Distinct(StringComparer.Ordinal).ToArray();

    public static string MilestoneIntent(JsonObject milestone)
    {
        string value = Text(milestone, "state");
        if (value is not ("open" or "closed" or "reopen")) throw new InvalidDataException("Milestone '" + Text(milestone, "key") + "' state must be exactly open, closed or reopen.");
        return value;
    }
    public static string ResolveMilestone(JsonObject milestone, JsonObject? live, IReadOnlyDictionary<string, JsonObject> manifest, BacklogRemoteState remote)
    {
        string intent = MilestoneIntent(milestone);
        if (live is null) return "open";
        string current = Text(live, "state");
        if (current is not ("open" or "closed")) throw new InvalidDataException("Milestone '" + Text(milestone, "key") + "' has invalid current tracker state.");
        string desired = intent == "reopen" ? "open" : intent == "closed" ? "closed" : current;
        if (desired == "closed" && MilestoneClosureFailure(milestone, live, manifest, remote) is string failure) throw new InvalidDataException("Milestone closure blocked: " + failure);
        return desired;
    }
    public static string? MilestoneClosureFailure(JsonObject milestone, JsonObject live, IReadOnlyDictionary<string, JsonObject> manifest, BacklogRemoteState remote)
    {
        string key = Text(milestone, "key"); int id = Number(live, "id");
        if (id <= 0) return "milestone '" + key + "' has no current tracker identity";
        foreach (var issue in manifest.Values.Where(x => Text(x, "milestone") == key))
        {
            string childKey = Text(issue, "key");
            if (!remote.Issues.TryGetValue(childKey, out var child)) return "canonical milestone member '" + childKey + "' is absent from the tracker";
            if (Number(child["milestone"], "id") != id) return "canonical milestone member '" + childKey + "' belongs to a different milestone";
        }
        string epicKey = key + "-EPIC";
        if (!manifest.TryGetValue(epicKey, out var epic)) return "expected canonical epic '" + epicKey + "' is absent from the manifest";
        if (!Strings(epic, "labels").Contains("type:epic", StringComparer.Ordinal)) return "expected canonical epic '" + epicKey + "' lacks exact type:epic label";
        if (ClosureFailure(epicKey, manifest, remote) is string failure) return failure;
        foreach (var issue in remote.AllIssues)
        {
            if (issue["pull_request"] is not null || Number(issue["milestone"], "id") != id || Text(issue, "state") == "closed") continue;
            string memberKey = HiddenKey(Text(issue, "body"));
            if (manifest.TryGetValue(memberKey, out var member) && Text(member, "parentKey") == epicKey && !RequiredForParent(member)) continue;
            return "actual milestone member '#" + Number(issue, "number") + "' is not closed";
        }
        return null;
    }
}
