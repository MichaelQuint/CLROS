using OS.Tooling;
using System.Text.Json.Nodes;
using static OS.Tooling.BacklogManifest;

internal static partial class BacklogSyncTests
{
    private sealed class Fixture
    {
        public BacklogManifest Manifest
        {
            get;
        }
        public List<JsonObject> Issues { get; } = [];
        public List<(string Method, string Path, JsonObject? Body)> Calls { get; } = [];
        public bool Reviewed { get; init; } = true;
        public int RepositoryId { get; init; } = 9;
        public string RepositoryName { get; init; } = "admin/CLROS";
        public bool DropLabelMutation
        {
            get; init;
        }
        public bool DropContentMutation
        {
            get; init;
        }
        public Action<string, string, JsonObject?>? BeforeRequest
        {
            get; set;
        }

        private readonly List<JsonObject> labels = [];
        private readonly JsonObject milestone = new()
        {
            ["id"] = 7,
            ["title"] = "Fixture",
            ["description"] = "Current",
            ["state"] = "open",
            ["due_on"] = null
        };
        public Fixture()
        {
            string[] names = ["agent:done", "agent:review", "agent:claimed", "type:epic", "type:implementation", "human:owned"];
            for (int i = 0; i < names.Length; i++)
                labels.Add(new()
                {
                    ["id"] = i + 1,
                    ["name"] = names[i],
                    ["color"] = "aabbcc",
                    ["description"] = "Fixture"
                });
            JsonObject Issue(string key, string parent, bool epic, params string[] dependencies) => new()
            {
                ["key"] = key,
                ["title"] = key,
                ["milestone"] = "M00",
                ["parentKey"] = parent,
                ["desiredState"] = "closed",
                ["summary"] = "Summary " + key,
                ["dependencyKeys"] = new JsonArray(dependencies.Select(x => (JsonNode?)JsonValue.Create(x)).ToArray()),
                ["labels"] = new JsonArray("agent:done", epic ? "type:epic" : "type:implementation"),
                ["priority"] = "p1",
                ["collisionLane"] = "",
                ["currentEvidence"] = "Reviewed fixture",
                ["implementationNotes"] = new JsonArray("Do the work"),
                ["referenceInfluences"] = new JsonArray(),
                ["ownedPaths"] = new JsonArray("tests/" + key),
                ["forbiddenPaths"] = new JsonArray("src/"),
                ["acceptanceCriteria"] = new JsonArray("Checks pass"),
                ["requiredMarkers"] = new JsonArray("FIXTURE_OK"),
                ["requiredCommands"] = new JsonArray("dotnet run"),
                ["documentationPaths"] = new JsonArray("docs/issue-labels.md"),
                ["evidenceBoundary"] = "Offline fixture only"
            };
            JsonObject[] issues = [Issue("M00-EPIC", "", true), Issue("M00-001", "M00-EPIC", false), Issue("M00-002", "M00-EPIC", false, "M00-001")];
            JsonObject desiredMilestone = new()
            {
                ["key"] = "M00",
                ["title"] = "Fixture",
                ["description"] = "Current",
                ["state"] = "open"
            };
            Manifest = new()
            {
                Source = new(),
                Issues = issues,
                Labels = labels.Where(x => Text(x, "name") != "human:owned").ToArray(),
                Milestones = [desiredMilestone],
                IssueByKey = issues.ToDictionary(x => Text(x, "key"), StringComparer.Ordinal),
                LabelByName = labels.Where(x => Text(x, "name") != "human:owned").ToDictionary(x => Text(x, "name"), StringComparer.Ordinal),
                MilestoneByKey = new()
                {
                    ["M00"] = desiredMilestone
                },
                Dependencies = new()
            };
            for (int i = 0; i < issues.Length; i++)
                Issues.Add(new()
                {
                    ["number"] = i + 1,
                    ["title"] = Text(issues[i], "title"),
                    ["body"] = "Human note $keep\n<!-- clros-issue-key:" + Text(issues[i], "key") + " -->",
                    ["state"] = "open",
                    ["content_version"] = 1L,
                    ["milestone"] = new JsonObject { ["id"] = 7 },
                    ["labels"] = new JsonArray(labels.Single(x => Text(x, "name") == "agent:review").DeepClone(), labels.Single(x => Text(x, "name") == "human:owned").DeepClone())
                });
        }

        public Task<JsonNode?> Api(string method, string path, JsonObject? body, CancellationToken cancellation)
        {
            Calls.Add((method, path, (JsonObject?)body?.DeepClone()));
            BeforeRequest?.Invoke(method, path, body);
            JsonNode? result;
            string clean = path.Split('?')[0];
            JsonArray Array(IEnumerable<JsonObject> values) => new(values.Select(x => x.DeepClone()).ToArray());
            if (method == "GET" && clean == "/repos/admin/CLROS")
                result = new JsonObject
                {
                    ["id"] = RepositoryId,
                    ["full_name"] = RepositoryName
                };
            else if (method == "GET" && clean.EndsWith("/comments", StringComparison.Ordinal))
            {
                int number = int.Parse(clean.Split('/')[^2]);
                string key = BacklogPolicy.HiddenKey(Text(Issues.Single(x => Number(x, "number") == number), "body"));
                result = Reviewed ? new JsonArray(new JsonObject { ["body"] = BacklogContracts.EvidenceMarker(key) }) : new JsonArray();
            }
            else if (method == "GET" && clean.EndsWith("/milestones", StringComparison.Ordinal))
                result = new JsonArray(milestone.DeepClone());
            else if (method == "GET" && clean.EndsWith("/issues", StringComparison.Ordinal))
                result = Array(Issues);
            else if (method == "GET" && clean.EndsWith("/labels", StringComparison.Ordinal))
                result = Array(labels);
            else
            {
                bool isLabels = clean.EndsWith("/labels", StringComparison.Ordinal);
                bool deleteLabel = method == "DELETE" && clean.Contains("/labels/", StringComparison.Ordinal);
                int number = int.Parse(clean.Split('/')[deleteLabel ? ^3 : isLabels ? ^2 : ^1]);
                var issue = Issues.Single(x => Number(x, "number") == number);
                if (method == "PATCH")
                {
                    if (body!["content_version"]!.GetValue<long>() != issue["content_version"]!.GetValue<long>())
                        throw new IOException("Gitea HTTP status 409");
                    if (!DropContentMutation)
                    {
                        foreach (var item in body!)
                            if (item.Key != "content_version")
                                issue[item.Key] = item.Key == "milestone" ? new JsonObject
                                {
                                    ["id"] = item.Value!.GetValue<int>()
                                }

                                : item.Value?.DeepClone();
                        issue["content_version"] = issue["content_version"]!.GetValue<long>() + 1;
                    }

                    result = issue.DeepClone();
                }
                else if (method == "POST" && isLabels)
                {
                    var values = body!["labels"]!.AsArray().Select(x => x!.GetValue<int>()).Select(id => labels.Single(x => Number(x, "id") == id));
                    if (!DropLabelMutation)
                        foreach (var label in values)
                            if (!issue["labels"]!.AsArray().Any(x => Number(x, "id") == Number(label, "id")))
                                issue["labels"]!.AsArray().Add(label.DeepClone());
                    result = issue["labels"]!.DeepClone();
                }
                else if (deleteLabel)
                {
                    int id = int.Parse(clean.Split('/')[^1]);
                    var existing = issue["labels"]!.AsArray().FirstOrDefault(x => Number(x, "id") == id);
                    if (!DropLabelMutation && existing is not null)
                        issue["labels"]!.AsArray().Remove(existing);
                    result = null;
                }
                else if (method == "GET")
                    result = issue.DeepClone();
                else
                    throw new InvalidOperationException("Unexpected fixture request " + method + " " + path);
            }

            return Task.FromResult<JsonNode?>(result);
        }
    }
}
