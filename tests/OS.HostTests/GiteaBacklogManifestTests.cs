using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

internal static class GiteaBacklogManifestTests
{
    private static readonly IReadOnlyDictionary<string, int> ExpectedChildren =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["M00"] = 22,
            ["M01"] = 15,
            ["M02"] = 16,
            ["M03"] = 11,
            ["M04"] = 23,
            ["M05"] = 21,
            ["M06"] = 19,
            ["M07"] = 25,
            ["M08"] = 20,
            ["M09"] = 30,
            ["M10"] = 14,
            ["M11"] = 18,
            ["M12"] = 18,
            ["M13"] = 18,
            ["M14"] = 18,
            ["M15"] = 15
        };

    [ModuleInitializer]
    internal static void Run()
    {
        int failures = 0;
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string manifestPath = Path.Combine(repositoryRoot, ".gitea", "backlog", "clros-issues.json");
        string scriptPath = Path.Combine(repositoryRoot, "scripts", "sync-gitea-backlog.ps1");
        string tempRoot = Path.Combine(Path.GetTempPath(), "clros-gitea-backlog-tests-" + Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(tempRoot);
            RunTest("gitea backlog manifest is complete UTF-8 without BOM", () => TestManifestShape(manifestPath), ref failures);
            RunTest("gitea backlog offline preview validates without writes", () => TestOfflinePreview(scriptPath, manifestPath), ref failures);
            RunTest("gitea backlog managed body preserves human notes", () => TestManagedBodySelfTest(scriptPath), ref failures);
            RunTest("gitea backlog replaces labels through the dedicated endpoint", () => TestDedicatedIssueLabelEndpoint(scriptPath), ref failures);
            RunTest("gitea backlog sync refuses noncanonical target", () => TestCanonicalTargetRefusal(scriptPath), ref failures);
            RunTest("gitea backlog validation rejects missing required field", () => TestInvalidManifest(scriptPath, manifestPath, tempRoot, "missing", RemoveRequiredField, "missing required field 'summary'"), ref failures);
            RunTest("gitea backlog validation rejects duplicate key", () => TestInvalidManifest(scriptPath, manifestPath, tempRoot, "duplicate", DuplicateIssueKey, "Duplicate issue key"), ref failures);
            RunTest("gitea backlog validation rejects unknown dependency", () => TestInvalidManifest(scriptPath, manifestPath, tempRoot, "dependency", AddUnknownDependency, "references unknown dependency"), ref failures);
            RunTest("gitea backlog validation rejects dependency cycle", () => TestInvalidManifest(scriptPath, manifestPath, tempRoot, "cycle", AddDependencyCycle, "dependency graph contains a cycle"), ref failures);
            RunTest("gitea backlog validation rejects unserialized M00 ownership", () => TestInvalidManifest(scriptPath, manifestPath, tempRoot, "overlap", AddUnserializedM00Overlap, "unserialized owned-path overlap"), ref failures);
            RunTest("gitea backlog validation requires closure evidence policy", () => TestInvalidManifest(scriptPath, manifestPath, tempRoot, "evidence-policy", BreakClosureEvidencePolicy, "closureEvidenceMarkerFormat must be exactly"), ref failures);
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, recursive: true);
                }
            }
            catch
            {
                // Cleanup failure must not hide contract results.
            }
        }

        if (failures != 0)
        {
            throw new InvalidOperationException("Gitea backlog manifest tests failed: " + failures);
        }
    }

    private static void RunTest(string name, Func<bool> test, ref int failures)
    {
        Console.WriteLine("TEST_START " + name);
        try
        {
            if (test())
            {
                Console.WriteLine("TEST_PASS " + name);
                return;
            }

            failures++;
            Console.WriteLine("TEST_FAIL " + name + " assertion_failed");
        }
        catch (Exception ex)
        {
            failures++;
            Console.WriteLine("TEST_FAIL " + name + " " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static bool TestManifestShape(string manifestPath)
    {
        byte[] bytes = File.ReadAllBytes(manifestPath);
        if (bytes.Length < 3 ||
            (bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF))
        {
            return false;
        }

        string json;
        try
        {
            json = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return false;
        }

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        if (root.GetProperty("schemaVersion").GetInt32() != 1 ||
            root.GetProperty("target").GetProperty("baseUrl").GetString() != "http://localhost:3000" ||
            root.GetProperty("target").GetProperty("owner").GetString() != "admin" ||
            root.GetProperty("target").GetProperty("repository").GetString() != "CLROS" ||
            root.GetProperty("policy").GetProperty("closureEvidenceMarkerFormat").GetString() !=
                "<!-- clros-evidence-reviewed:{key} -->")
        {
            return false;
        }

        JsonElement milestones = root.GetProperty("milestones");
        JsonElement labels = root.GetProperty("labels");
        JsonElement issues = root.GetProperty("issues");
        if (milestones.GetArrayLength() != 16 || labels.GetArrayLength() != 68 || issues.GetArrayLength() != 319)
        {
            return false;
        }

        string[] requiredFields =
        [
            "key", "title", "milestone", "parentKey", "dependencyKeys", "labels", "priority",
            "ownedPaths", "forbiddenPaths", "collisionLane", "summary", "currentEvidence",
            "implementationNotes", "referenceInfluences", "acceptanceCriteria", "requiredMarkers",
            "requiredCommands", "documentationPaths", "evidenceBoundary", "desiredState"
        ];

        HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
        Dictionary<string, JsonElement> issueByKey = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (JsonElement issue in issues.EnumerateArray())
        {
            foreach (string field in requiredFields)
            {
                if (!issue.TryGetProperty(field, out _))
                {
                    return false;
                }
            }

            string key = issue.GetProperty("key").GetString() ?? string.Empty;
            if (!keys.Add(key))
            {
                return false;
            }
            issueByKey.Add(key, issue);

            if (issue.GetProperty("acceptanceCriteria").GetArrayLength() == 0 ||
                issue.GetProperty("requiredCommands").GetArrayLength() == 0 ||
                string.IsNullOrWhiteSpace(issue.GetProperty("currentEvidence").GetString()) ||
                string.IsNullOrWhiteSpace(issue.GetProperty("evidenceBoundary").GetString()))
            {
                return false;
            }

            if (!key.EndsWith("-EPIC", StringComparison.Ordinal))
            {
                string ownedPathList = string.Join(
                    ", ",
                    issue.GetProperty("ownedPaths")
                        .EnumerateArray()
                        .Select(path => path.GetString() ?? string.Empty));
                string expectedOwnedPathNote =
                    "The proposed owned paths are exact for this slice: " +
                    ownedPathList +
                    ". Any additional shared integration file requires a separately claimed collision-lane issue.";
                string[] exactPathNotes = issue.GetProperty("implementationNotes")
                    .EnumerateArray()
                    .Select(note => note.GetString() ?? string.Empty)
                    .Where(note => note.StartsWith(
                        "The proposed owned paths are exact for this slice:",
                        StringComparison.Ordinal))
                    .ToArray();
                if (exactPathNotes.Length != 1 ||
                    !string.Equals(exactPathNotes[0], expectedOwnedPathNote, StringComparison.Ordinal))
                {
                    return false;
                }
            }
        }

        foreach ((string milestone, int childCount) in ExpectedChildren)
        {
            if (!keys.Contains(milestone + "-EPIC"))
            {
                return false;
            }

            for (int number = 1; number <= childCount; number++)
            {
                if (!keys.Contains(milestone + "-" + number.ToString("000")))
                {
                    return false;
                }
            }
        }

        foreach (JsonElement issue in issues.EnumerateArray())
        {
            foreach (JsonElement dependency in issue.GetProperty("dependencyKeys").EnumerateArray())
            {
                if (!keys.Contains(dependency.GetString() ?? string.Empty))
                {
                    return false;
                }
            }
        }

        return ValidateM00Contracts(issueByKey) &&
            ValidateM00ExitGate(issueByKey) &&
            ValidateM00OwnedPathSerialization(issueByKey) &&
            DependenciesEqual(issueByKey["M06-001"], "M05-019") &&
            DependenciesEqual(issueByKey["M06-019"], "M06-018", "M05-021") &&
            DependenciesEqual(issueByKey["M07-001"], "M05-019") &&
            DependenciesEqual(issueByKey["M07-024"], "M07-023", "M05-021") &&
            DependenciesEqual(issueByKey["M09-001"], "M04-023", "M06-019", "M07-025", "M08-019") &&
            DependenciesEqual(issueByKey["M12-018"], "M12-016") &&
            DependenciesEqual(issueByKey["M13-018"], "M13-016") &&
            DependenciesEqual(issueByKey["M15-014"], "M15-009");
    }

    private static bool ValidateM00Contracts(IReadOnlyDictionary<string, JsonElement> issueByKey)
    {
        string[] staleDomainPhrases =
        [
            "Complete the valid packet or socket transition required by:",
            "Exercise a valid aligned, non-overflowing case for:",
            "Read or validate the exact well-formed storage object required by:",
            "bad or looping cluster chains",
            "retransmission timeout, reset, half-close",
            "prohibited writable-executable state as applicable"
        ];

        for (int number = 1; number <= 22; number++)
        {
            string key = "M00-" + number.ToString("000");
            JsonElement issue = issueByKey[key];
            string notes = string.Join(
                "\n",
                issue.GetProperty("implementationNotes")
                    .EnumerateArray()
                    .Select(note => note.GetString() ?? string.Empty));
            string criteria = string.Join(
                "\n",
                issue.GetProperty("acceptanceCriteria")
                    .EnumerateArray()
                    .Select(criterion => criterion.GetString() ?? string.Empty));
            string combined = notes + "\n" + criteria;
            if (staleDomainPhrases.Any(phrase =>
                combined.Contains(phrase, StringComparison.Ordinal)))
            {
                return false;
            }

            if (CountPrefix(issue.GetProperty("implementationNotes"), "Positive case:") != 1 ||
                CountPrefix(issue.GetProperty("implementationNotes"), "Failure cases:") != 1 ||
                CountPrefix(issue.GetProperty("implementationNotes"), "Explicit non-goal:") != 1)
            {
                return false;
            }
        }

        return HasOwnedPath(
                issueByKey["M00-001"],
                "tests/OS.HostTests/RepositoryRecoveryTests.cs") &&
            HasOwnedPath(
                issueByKey["M00-004"],
                "tests/OS.HostTests/HttpResponseParserTests.cs") &&
            HasOwnedPath(
                issueByKey["M00-014"],
                "scripts/check-naming.ps1") &&
            HasOwnedPath(
                issueByKey["M00-018"],
                "scripts/test-efi-artifact.ps1") &&
            HasOwnedPath(
                issueByKey["M00-018"],
                "tests/OS.HostTests/EfiArtifactVerificationScriptTests.cs") &&
            !HasOwnedPath(
                issueByKey["M00-016"],
                "tests/OS.HostTests/RepositoryRecoveryTests.cs") &&
            DependenciesEqual(issueByKey["M00-003"], "M00-002", "M00-015") &&
            DependenciesEqual(issueByKey["M00-006"], "M00-002", "M00-014") &&
            DependenciesEqual(
                issueByKey["M00-022"],
                "M00-017",
                "M00-018",
                "M00-020",
                "M00-021",
                "M00-001",
                "M00-009",
                "M00-010",
                "M00-012",
                "M00-013") &&
            JsonElementContains(
                issueByKey["M00-017"].GetProperty("requiredMarkers"),
                "EFI_PREFLIGHT_ILCOMPILER") &&
            JsonElementContains(
                issueByKey["M00-017"].GetProperty("requiredMarkers"),
                "EFI_PREFLIGHT_QEMU_IMG") &&
            JsonElementContains(
                issueByKey["M00-019"].GetProperty("requiredMarkers"),
                "SERIAL_MARKER_STREAM status=Passed terminal=Success") &&
            issueByKey["M00-022"].GetProperty("labels")
                .EnumerateArray()
                .Any(label => label.GetString() == "evidence:qemu") &&
            JsonElementContains(
                issueByKey["M00-015"].GetProperty("acceptanceCriteria"),
                "clros-ci-smoke-logs");
    }

    private static bool ValidateM00ExitGate(
        IReadOnlyDictionary<string, JsonElement> issueByKey)
    {
        for (int number = 1; number <= 22; number++)
        {
            string requiredKey = "M00-" + number.ToString("000");
            if (!DependsTransitively(issueByKey, "M01-001", requiredKey))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ValidateM00OwnedPathSerialization(
        IReadOnlyDictionary<string, JsonElement> issueByKey)
    {
        JsonElement[] m00Issues = issueByKey
            .Where(pair =>
                pair.Key.StartsWith("M00-", StringComparison.Ordinal) &&
                !pair.Key.EndsWith("-EPIC", StringComparison.Ordinal))
            .Select(pair => pair.Value)
            .ToArray();

        for (int leftIndex = 0; leftIndex < m00Issues.Length; leftIndex++)
        {
            JsonElement left = m00Issues[leftIndex];
            string leftKey = left.GetProperty("key").GetString() ?? string.Empty;
            HashSet<string> leftPaths = left.GetProperty("ownedPaths")
                .EnumerateArray()
                .Select(path => NormalizeOwnedPath(path.GetString()))
                .Where(path => path.Length != 0)
                .ToHashSet(StringComparer.Ordinal);

            for (int rightIndex = leftIndex + 1; rightIndex < m00Issues.Length; rightIndex++)
            {
                JsonElement right = m00Issues[rightIndex];
                string rightKey = right.GetProperty("key").GetString() ?? string.Empty;
                bool overlaps = right.GetProperty("ownedPaths")
                    .EnumerateArray()
                    .Select(path => NormalizeOwnedPath(path.GetString()))
                    .Any(leftPaths.Contains);
                if (!overlaps)
                {
                    continue;
                }

                bool dependencySerialized =
                    DependsTransitively(issueByKey, leftKey, rightKey) ||
                    DependsTransitively(issueByKey, rightKey, leftKey);
                string leftLane = left.GetProperty("collisionLane").ValueKind == JsonValueKind.Null
                    ? string.Empty
                    : left.GetProperty("collisionLane").GetString() ?? string.Empty;
                string rightLane = right.GetProperty("collisionLane").ValueKind == JsonValueKind.Null
                    ? string.Empty
                    : right.GetProperty("collisionLane").GetString() ?? string.Empty;
                bool laneSerialized =
                    leftLane.Length != 0 &&
                    string.Equals(leftLane, rightLane, StringComparison.Ordinal);
                if (!dependencySerialized && !laneSerialized)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool DependsTransitively(
        IReadOnlyDictionary<string, JsonElement> issueByKey,
        string key,
        string potentialAncestor)
    {
        HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal);
        Stack<string> pending = new Stack<string>();
        pending.Push(key);
        while (pending.Count != 0)
        {
            string current = pending.Pop();
            if (!visited.Add(current) || !issueByKey.TryGetValue(current, out JsonElement issue))
            {
                continue;
            }

            foreach (JsonElement dependency in issue.GetProperty("dependencyKeys").EnumerateArray())
            {
                string dependencyKey = dependency.GetString() ?? string.Empty;
                if (string.Equals(dependencyKey, potentialAncestor, StringComparison.Ordinal))
                {
                    return true;
                }
                pending.Push(dependencyKey);
            }
        }

        return false;
    }

    private static string NormalizeOwnedPath(string? path)
    {
        string normalized = (path ?? string.Empty)
            .Trim()
            .Replace('\\', '/')
            .ToLowerInvariant();
        while (normalized.StartsWith("./", StringComparison.Ordinal))
        {
            normalized = normalized[2..];
        }
        return normalized;
    }

    private static int CountPrefix(JsonElement array, string prefix)
    {
        return array.EnumerateArray().Count(value =>
            (value.GetString() ?? string.Empty).StartsWith(prefix, StringComparison.Ordinal));
    }

    private static bool HasOwnedPath(JsonElement issue, string path)
    {
        return issue.GetProperty("ownedPaths")
            .EnumerateArray()
            .Any(value => string.Equals(value.GetString(), path, StringComparison.Ordinal));
    }

    private static bool JsonElementContains(JsonElement array, string value)
    {
        return array.EnumerateArray().Any(element =>
            (element.GetString() ?? string.Empty).Contains(value, StringComparison.Ordinal));
    }

    private static bool DependenciesEqual(JsonElement issue, params string[] expected)
    {
        string[] actual = issue.GetProperty("dependencyKeys")
            .EnumerateArray()
            .Select(value => value.GetString() ?? string.Empty)
            .ToArray();
        return actual.SequenceEqual(expected, StringComparer.Ordinal);
    }

    private static bool TestOfflinePreview(string scriptPath, string manifestPath)
    {
        byte[] before = SHA256.HashData(File.ReadAllBytes(manifestPath));
        DateTime beforeWriteTime = File.GetLastWriteTimeUtc(manifestPath);

        ProcessResult result = RunScript(scriptPath, "-Offline");

        byte[] after = SHA256.HashData(File.ReadAllBytes(manifestPath));
        DateTime afterWriteTime = File.GetLastWriteTimeUtc(manifestPath);
        return result.ExitCode == 0 &&
            result.Output.Contains("SYNC_MANIFEST_VALID milestones=16 labels=68 epics=16 children=303 issues=319", StringComparison.Ordinal) &&
            result.Output.Contains("SYNC_MODE preview", StringComparison.Ordinal) &&
            result.Output.Contains("SYNC_OFFLINE manifest_only", StringComparison.Ordinal) &&
            result.Output.Contains("SYNC_KEYS action=skipped count=403", StringComparison.Ordinal) &&
            result.Output.Contains("SYNC_SUMMARY created=0 updated=0 unchanged=0 skipped=403 failed=0 wouldCreate=0 wouldUpdate=0", StringComparison.Ordinal) &&
            result.Output.Contains("TEST_PASS gitea.backlog.sync", StringComparison.Ordinal) &&
            before.AsSpan().SequenceEqual(after) &&
            beforeWriteTime == afterWriteTime;
    }

    private static bool TestManagedBodySelfTest(string scriptPath)
    {
        ProcessResult result = RunScript(scriptPath, "-SelfTest");
        return result.ExitCode == 0 &&
            result.Output.Contains("SYNC_SELF_TEST_MANAGED_BODY_PASS", StringComparison.Ordinal) &&
            result.Output.Contains("SYNC_SELF_TEST_STATE_POLICY_PASS", StringComparison.Ordinal) &&
            result.Output.Contains("TEST_PASS gitea.backlog.sync", StringComparison.Ordinal);
    }

    private static bool TestDedicatedIssueLabelEndpoint(string scriptPath)
    {
        string script = File.ReadAllText(scriptPath);
        return script.Contains(
                "[ValidateSet(\"GET\", \"POST\", \"PUT\", \"PATCH\")]",
                StringComparison.Ordinal)
            && script.Contains(
                "-Path \"$($RemoteState.repositoryPath)/issues/$number/labels\"",
                StringComparison.Ordinal)
            && script.Contains(
                "-Body @{ labels = $desiredLabelIds }",
                StringComparison.Ordinal)
            && script.Contains(
                "Gitea's EditIssueOption does not accept labels.",
                StringComparison.Ordinal);
    }

    private static bool TestCanonicalTargetRefusal(string scriptPath)
    {
        ProcessResult result = RunScript(
            scriptPath,
            "-Offline",
            "-BaseUrl",
            "http://localhost:3001");

        return result.ExitCode == 1 &&
            result.Output.Contains("TEST_FAIL gitea.backlog.sync", StringComparison.Ordinal) &&
            result.Output.Contains("Refusing non-canonical target", StringComparison.Ordinal);
    }

    private static bool TestInvalidManifest(
        string scriptPath,
        string sourceManifestPath,
        string tempRoot,
        string name,
        Action<JsonObject> mutate,
        string expectedFailure)
    {
        JsonObject root = JsonNode.Parse(File.ReadAllText(sourceManifestPath))?.AsObject()
            ?? throw new InvalidOperationException("Manifest did not parse as an object.");
        mutate(root);

        string path = Path.Combine(tempRoot, name + ".json");
        string json = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine;
        File.WriteAllText(path, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        ProcessResult result = RunScript(scriptPath, "-Offline", "-ManifestPath", path);
        return result.ExitCode == 1 &&
            result.Output.Contains("TEST_FAIL gitea.backlog.sync", StringComparison.Ordinal) &&
            result.Output.Contains(expectedFailure, StringComparison.Ordinal);
    }

    private static void RemoveRequiredField(JsonObject root)
    {
        JsonArray issues = root["issues"]?.AsArray()
            ?? throw new InvalidOperationException("issues array missing");
        issues[1]?.AsObject().Remove("summary");
    }

    private static void DuplicateIssueKey(JsonObject root)
    {
        JsonArray issues = root["issues"]?.AsArray()
            ?? throw new InvalidOperationException("issues array missing");
        string firstKey = issues[0]?["key"]?.GetValue<string>()
            ?? throw new InvalidOperationException("first key missing");
        issues[1]!["key"] = firstKey;
    }

    private static void AddUnknownDependency(JsonObject root)
    {
        JsonArray issues = root["issues"]?.AsArray()
            ?? throw new InvalidOperationException("issues array missing");
        JsonObject target = issues
            .Select(node => node?.AsObject())
            .First(node => node?["key"]?.GetValue<string>() == "M00-002")
            ?? throw new InvalidOperationException("M00-002 missing");
        target["dependencyKeys"] = new JsonArray("M99-999");
    }

    private static void AddDependencyCycle(JsonObject root)
    {
        JsonObject target = FindIssue(root, "M00-002");
        target["dependencyKeys"] = new JsonArray("M00-003");
    }

    private static void AddUnserializedM00Overlap(JsonObject root)
    {
        JsonObject target = FindIssue(root, "M00-010");
        JsonArray ownedPaths = target["ownedPaths"]?.AsArray()
            ?? throw new InvalidOperationException("M00-010 ownedPaths missing");
        ownedPaths.Add("docs/reference-map.md");
    }

    private static void BreakClosureEvidencePolicy(JsonObject root)
    {
        JsonObject policy = root["policy"]?.AsObject()
            ?? throw new InvalidOperationException("policy missing");
        policy["closureEvidenceMarkerFormat"] = "<!-- wrong:{key} -->";
    }

    private static JsonObject FindIssue(JsonObject root, string key)
    {
        JsonArray issues = root["issues"]?.AsArray()
            ?? throw new InvalidOperationException("issues array missing");
        return issues
            .Select(node => node?.AsObject())
            .First(node => node?["key"]?.GetValue<string>() == key)
            ?? throw new InvalidOperationException(key + " missing");
    }

    private static ProcessResult RunScript(string scriptPath, params string[] arguments)
    {
        ProcessStartInfo start = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-ExecutionPolicy");
        start.ArgumentList.Add("Bypass");
        start.ArgumentList.Add("-File");
        start.ArgumentList.Add(scriptPath);
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(start)
            ?? throw new InvalidOperationException("Failed to start PowerShell.");
        string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        if (!process.WaitForExit(120_000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("Gitea backlog script did not exit within 120 seconds.");
        }
        return new ProcessResult(process.ExitCode, output);
    }

    private readonly struct ProcessResult(int exitCode, string output)
    {
        internal int ExitCode { get; } = exitCode;

        internal string Output { get; } = output;
    }
}
