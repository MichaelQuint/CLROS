using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace OS.Tooling;

public sealed class BacklogManifest
{
    public required JsonObject Source { get; init; }
    public required IReadOnlyList<JsonObject> Issues { get; init; }
    public required IReadOnlyList<JsonObject> Labels { get; init; }
    public required IReadOnlyList<JsonObject> Milestones { get; init; }
    public required Dictionary<string, JsonObject> IssueByKey { get; init; }
    public required Dictionary<string, JsonObject> MilestoneByKey { get; init; }
    public required Dictionary<string, JsonObject> LabelByName { get; init; }
    public required Dictionary<string, HashSet<string>> Dependencies { get; init; }

    public static BacklogManifest Read(string path)
    {
        const int maxBytes = 16 * 1024 * 1024;
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (file.Length is <= 0 or > maxBytes) throw new InvalidDataException("Manifest size limit exceeded");
        byte[] bytes = new byte[checked((int)file.Length)]; file.ReadExactly(bytes);
        return Validate(JsonNode.Parse(new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF'),
            documentOptions: new JsonDocumentOptions { MaxDepth = 64 })?.AsObject() ?? throw new InvalidDataException("Manifest must be an object"));
    }

    public static BacklogManifest Validate(JsonObject source)
    {
        foreach (string name in new[] { "schemaVersion", "project", "target", "policy", "milestones", "labels", "issues" })
            if (!source.ContainsKey(name)) Fail("Manifest is missing top-level field '" + name + "'.");
        if (source["schemaVersion"]?.GetValue<int>() != 1) Fail("Unsupported manifest schemaVersion");
        var target = source["target"];
        if (Text(target, "baseUrl") != BacklogContracts.BaseUrl || Text(target, "owner") != BacklogContracts.Owner || Text(target, "repository") != BacklogContracts.Repository)
            Fail("Manifest target must be exactly " + BacklogContracts.BaseUrl + "/" + BacklogContracts.Owner + "/" + BacklogContracts.Repository + ".");
        if (Text(source["policy"], "closureEvidenceMarkerFormat") != BacklogContracts.ClosureEvidenceMarkerFormat)
            Fail("Manifest policy closureEvidenceMarkerFormat must be exactly '" + BacklogContracts.ClosureEvidenceMarkerFormat + "'.");
        var milestones = Objects(source, "milestones"); var labels = Objects(source, "labels"); var issues = Objects(source, "issues");
        if (milestones.Length != BacklogContracts.Children.Count) Fail("Manifest must contain exactly " + BacklogContracts.Children.Count + " milestones; found " + milestones.Length + ".");
        if (issues.Length != BacklogContracts.IssueCount) Fail("Manifest must contain " + BacklogContracts.Children.Count + " epics plus " + BacklogContracts.ChildCount + " children; found " + issues.Length + ".");
        Dictionary<string, JsonObject> milestoneByKey = new(StringComparer.Ordinal), labelByName = new(StringComparer.Ordinal), issueByKey = new(StringComparer.Ordinal);
        HashSet<string> milestoneTitles = new(StringComparer.Ordinal);
        foreach (var milestone in milestones)
        {
            string key = Text(milestone, "key");
            if (!BacklogContracts.Children.ContainsKey(key)) Fail("Invalid milestone key '" + key + "'.");
            if (!milestoneByKey.TryAdd(key, milestone)) Fail("Duplicate milestone key '" + key + "'.");
            if (!milestoneTitles.Add(Text(milestone, "title"))) Fail("Duplicate milestone title '" + Text(milestone, "title") + "'.");
            _ = BacklogPolicy.MilestoneIntent(milestone);
        }
        foreach (var label in labels)
        {
            string name = Text(label, "name");
            if (string.IsNullOrWhiteSpace(name)) Fail("A label has an empty name.");
            if (!labelByName.TryAdd(name, label)) Fail("Duplicate label '" + name + "'.");
            if (!Regex.IsMatch(Text(label, "color"), "^[0-9a-fA-F]{6}$")) Fail("Label '" + name + "' has invalid six-digit color.");
        }
        foreach (string name in BacklogContracts.RequiredLabels)
            if (!labelByName.ContainsKey(name)) Fail("Manifest is missing required label '" + name + "'.");
        foreach (var issue in issues)
        {
            string key = Text(issue, "key");
            foreach (string field in BacklogContracts.RequiredIssueFields)
                if (!issue.ContainsKey(field)) Fail("Issue '" + key + "' is missing required field '" + field + "'.");
            if (!Regex.IsMatch(key, @"^M(?:0[0-9]|1[0-5])-(?:EPIC|[0-9]{3})$")) Fail("Invalid issue key '" + key + "'.");
            if (!issueByKey.TryAdd(key, issue)) Fail("Duplicate issue key '" + key + "'.");
            string milestone = Text(issue, "milestone");
            if (!milestoneByKey.ContainsKey(milestone)) Fail("Issue '" + key + "' references unknown milestone '" + milestone + "'.");
            if (!key.StartsWith(milestone + "-", StringComparison.Ordinal)) Fail("Issue '" + key + "' does not belong to its declared milestone.");
            foreach (string label in Strings(issue, "labels")) if (!labelByName.ContainsKey(label)) Fail("Issue '" + key + "' references unknown label '" + label + "'.");
            if (Text(issue, "desiredState") is not ("open" or "closed" or "reopen")) Fail("Issue '" + key + "' has invalid desiredState.");
            if (Strings(issue, "acceptanceCriteria").Length == 0 || Strings(issue, "requiredCommands").Length == 0 || string.IsNullOrWhiteSpace(Text(issue, "evidenceBoundary")))
                Fail("Issue '" + key + "' does not contain implementation-ready acceptance, commands, and evidence boundary.");
            HashSet<string> owned = new(StringComparer.Ordinal);
            foreach (string path in Strings(issue, "ownedPaths"))
            {
                string normalized = NormalizePath(path);
                if (normalized.Length == 0) Fail("Issue '" + key + "' contains an empty owned path.");
                if (Path.IsPathRooted(path) || normalized.Split('/').Contains("..", StringComparer.Ordinal)) Fail("Issue '" + key + "' contains non-repository owned path '" + path + "'.");
                if (!owned.Add(normalized)) Fail("Issue '" + key + "' contains duplicate owned path '" + path + "'.");
            }
        }
        foreach (var (milestone, count) in BacklogContracts.Children)
        {
            if (!issueByKey.ContainsKey(milestone + "-EPIC")) Fail("Manifest is missing epic '" + milestone + "-EPIC'.");
            for (int number = 1; number <= count; number++)
            { string key = milestone + "-" + number.ToString("D3"); if (!issueByKey.ContainsKey(key)) Fail("Manifest is missing child '" + key + "'."); }
        }
        foreach (var issue in issues)
        {
            string key = Text(issue, "key"), parent = Text(issue, "parentKey");
            if (key.EndsWith("-EPIC", StringComparison.Ordinal) ? parent.Length != 0 : parent != Text(issue, "milestone") + "-EPIC") Fail("Issue '" + key + "' has invalid milestone epic parent.");
            if (parent.Length != 0 && !issueByKey.ContainsKey(parent)) Fail("Issue '" + key + "' references unknown parent '" + parent + "'.");
            foreach (string dependency in Strings(issue, "dependencyKeys"))
            {
                if (dependency == key) Fail("Issue '" + key + "' depends on itself.");
                if (!issueByKey.ContainsKey(dependency)) Fail("Issue '" + key + "' references unknown dependency '" + dependency + "'.");
            }
        }
        Dictionary<string, HashSet<string>> closureByKey = new(StringComparer.Ordinal);
        foreach (var issue in issues)
        {
            string key = Text(issue, "key"); HashSet<string> closure = new(StringComparer.Ordinal); Stack<string> pending = new(Strings(issue, "dependencyKeys"));
            while (pending.TryPop(out string? next)) if (closure.Add(next)) foreach (string nested in Strings(issueByKey[next], "dependencyKeys")) pending.Push(nested);
            if (closure.Contains(key)) Fail("Issue dependency graph contains a cycle reachable from '" + key + "'.");
            closureByKey[key] = closure;
        }
        Dictionary<string, List<JsonObject>> owners = new(StringComparer.Ordinal);
        foreach (var issue in issues.Where(x => !Text(x, "key").EndsWith("-EPIC", StringComparison.Ordinal)))
            foreach (string path in Strings(issue, "ownedPaths").Select(NormalizePath))
            { if (!owners.TryGetValue(path, out var list)) owners[path] = list = []; list.Add(issue); }
        foreach (var (path, list) in owners)
            for (int i = 0; i < list.Count; i++) for (int j = i + 1; j < list.Count; j++)
            {
                var left = list[i]; var right = list[j]; string a = Text(left, "key"), b = Text(right, "key"), lane = Text(left, "collisionLane");
                if (closureByKey[a].Contains(b) || closureByKey[b].Contains(a) || lane.Length != 0 && lane == Text(right, "collisionLane")) continue;
                if (Text(left, "milestone") == "M00" && Text(right, "milestone") == "M00" || !new[] { "docs/", "artifacts/", "done/" }.Any(x => path.StartsWith(x, StringComparison.Ordinal)))
                    Fail("Issues '" + a + "' and '" + b + "' have unserialized owned-path overlap '" + path + "'. Add a dependency or a shared collision lane.");
            }
        for (int number = 1; number <= BacklogContracts.Children["M00"]; number++)
        { string key = "M00-" + number.ToString("D3"); if (!closureByKey["M01-001"].Contains(key)) Fail("M01-001 must remain transitively blocked by recovery issue '" + key + "'."); }
        return new() { Source = source, Issues = issues, Labels = labels, Milestones = milestones, IssueByKey = issueByKey, MilestoneByKey = milestoneByKey,
            LabelByName = labelByName, Dependencies = closureByKey };
    }

    public static string Text(JsonNode? node, string key, string fallback = "") => node is JsonObject value && value[key] is JsonValue item && item.TryGetValue<string>(out string? text) ? text : fallback;
    public static int Number(JsonNode? node, string key) => node is JsonObject value && value[key] is JsonValue item && item.TryGetValue<int>(out int number) ? number : 0;
    public static string[] Strings(JsonNode node, string key) => node[key] is JsonArray array ? array.Select(x => x?.GetValue<string>() ?? throw new InvalidDataException("Null string in " + key)).ToArray() : [];
    public static JsonObject[] Objects(JsonNode node, string key) => node[key] is JsonArray array ? array.Select(x => x?.AsObject() ?? throw new InvalidDataException("Null object in " + key)).ToArray() : throw new InvalidDataException("Expected array '" + key + "'");
    public static string NormalizePath(string path)
    { string value = path.Trim().Replace('\\', '/'); while (value.StartsWith("./", StringComparison.Ordinal)) value = value[2..]; return value.ToLowerInvariant(); }
    private static void Fail(string message) => throw new InvalidDataException(message);
}
