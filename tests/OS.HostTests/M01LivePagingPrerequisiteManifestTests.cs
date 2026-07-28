using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

internal static class M01LivePagingPrerequisiteManifestTests
{
    private const string TestName = "m01.028";
    private const string ReadyMarker = "CLROS_M01_028_READY";

    private static readonly string[] RepairKeys =
    [
        "M01-028",
        "M01-029",
        "M01-030",
        "M01-031",
        "M01-032"
    ];

    [ModuleInitializer]
    internal static void Run()
    {
        Console.WriteLine("TEST_START " + TestName);
        try
        {
            string repositoryRoot = HostToolProcess.FindRepositoryRoot();
            string manifestPath = Path.Combine(
                repositoryRoot,
                ".gitea",
                "backlog",
                "clros-issues.json");
            byte[] manifestBytes = File.ReadAllBytes(manifestPath);
            string manifestText = new UTF8Encoding(
                encoderShouldEmitUTF8Identifier: false,
                throwOnInvalidBytes: true).GetString(manifestBytes);
            using JsonDocument manifest = JsonDocument.Parse(manifestText);
            IReadOnlyDictionary<string, JsonElement> issueByKey =
                IndexIssues(manifest.RootElement.GetProperty("issues"));

            Require(
                ValidateDependencyGraph(issueByKey),
                "dependency_graph");
            Require(
                ValidateHostOnlyEvidenceBoundaries(issueByKey),
                "host_evidence_boundary");
            Require(
                ValidateSerializedOwnership(issueByKey),
                "ownership_serialization");
            Require(
                ValidateIssueContracts(issueByKey),
                "issue_contract");
            Require(
                ValidateBlockerLedger(issueByKey),
                "blocker_ledger");

            Console.WriteLine(ReadyMarker);
            Console.WriteLine("TEST_PASS " + TestName);
        }
        catch (Exception exception)
        {
            string reason = SanitizeReason(exception.Message);
            Console.WriteLine(
                "TEST_FAIL " + TestName + " " + reason);
            throw new InvalidOperationException(
                "M01-028 live-paging prerequisite manifest tests failed: " +
                reason,
                exception);
        }
    }

    private static IReadOnlyDictionary<string, JsonElement> IndexIssues(
        JsonElement issues)
    {
        Dictionary<string, JsonElement> issueByKey =
            new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (JsonElement issue in issues.EnumerateArray())
        {
            string key =
                issue.GetProperty("key").GetString()
                ?? string.Empty;
            if (!issueByKey.TryAdd(key, issue))
            {
                throw new InvalidOperationException(
                    "duplicate_issue_" + key);
            }
        }

        return issueByKey;
    }

    private static bool ValidateDependencyGraph(
        IReadOnlyDictionary<string, JsonElement> issueByKey)
    {
        if (RepairKeys.Any(key => !issueByKey.ContainsKey(key)))
        {
            return false;
        }

        string[] expectedM01Order = Enumerable.Range(1, 19)
            .Select(number => "M01-" + number.ToString("000"))
            .Append("M01-027")
            .Concat(
                Enumerable.Range(28, 5)
                    .Select(number =>
                        "M01-" + number.ToString("000")))
            .Concat(
                Enumerable.Range(20, 6)
                    .Select(number =>
                        "M01-" + number.ToString("000")))
            .Concat(
                Enumerable.Range(33, 3)
                    .Select(number =>
                        "M01-" + number.ToString("000")))
            .Append("M01-026")
            .ToArray();
        string[] actualM01Order = issueByKey.Values
            .Select(issue =>
                issue.GetProperty("key").GetString()
                ?? string.Empty)
            .Where(key =>
                key.StartsWith("M01-", StringComparison.Ordinal) &&
                !key.EndsWith("-EPIC", StringComparison.Ordinal))
            .ToArray();

        return actualM01Order.SequenceEqual(
                expectedM01Order,
                StringComparer.Ordinal) &&
            DependenciesEqual(
                issueByKey["M01-029"],
                "M01-028",
                "M01-027") &&
            DependenciesEqual(issueByKey["M01-030"], "M01-029") &&
            DependenciesEqual(issueByKey["M01-031"], "M01-030") &&
            DependenciesEqual(issueByKey["M01-032"], "M01-031") &&
            DependenciesEqual(
                issueByKey["M01-025"],
                "M01-024",
                "M01-027",
                "M01-029",
                "M01-030",
                "M01-031",
                "M01-032") &&
            DependenciesEqual(issueByKey["M01-026"], "M01-035") &&
            DependsTransitively(
                issueByKey,
                "M01-032",
                "M01-028") &&
            DependsTransitively(
                issueByKey,
                "M01-032",
                "M01-027") &&
            DependsTransitively(
                issueByKey,
                "M01-025",
                "M01-024") &&
            DependsTransitively(
                issueByKey,
                "M01-026",
                "M01-032");
    }

    private static bool ValidateHostOnlyEvidenceBoundaries(
        IReadOnlyDictionary<string, JsonElement> issueByKey)
    {
        foreach (string key in RepairKeys)
        {
            JsonElement issue = issueByKey[key];
            string suffix = key[4..];
            string[] expectedMarkers =
            [
                "TEST_START m01." + suffix,
                "CLROS_M01_" + suffix + "_READY",
                "TEST_PASS m01." + suffix,
                "TEST_FAIL m01." + suffix + " <reason>"
            ];
            if (!JsonStringArrayEquals(
                    issue.GetProperty("requiredMarkers"),
                    expectedMarkers) ||
                !JsonArrayContainsExact(
                    issue.GetProperty("labels"),
                    "evidence:host") ||
                JsonArrayContainsExact(
                    issue.GetProperty("labels"),
                    "evidence:qemu") ||
                JsonArrayContainsExact(
                    issue.GetProperty("labels"),
                    "evidence:libvirt") ||
                JsonArrayContainsExact(
                    issue.GetProperty("labels"),
                    "evidence:aws") ||
                JsonArrayContainsExact(
                    issue.GetProperty("labels"),
                    "evidence:azure"))
            {
                return false;
            }

            foreach (JsonElement commandElement in
                issue.GetProperty("requiredCommands").EnumerateArray())
            {
                string command =
                    commandElement.GetString()
                    ?? string.Empty;
                if (command.Contains(
                        " -Qemu \"",
                        StringComparison.Ordinal) ||
                    command.Contains(
                        " -OvmfCode \"",
                        StringComparison.Ordinal) ||
                    (command.Contains(
                            "run-qemu-smoke.ps1",
                            StringComparison.OrdinalIgnoreCase) &&
                        !command.Contains(
                            "-DryRun",
                            StringComparison.Ordinal)))
                {
                    return false;
                }
            }

            string boundary =
                issue.GetProperty("evidenceBoundary").GetString()
                ?? string.Empty;
            string currentEvidence =
                issue.GetProperty("currentEvidence").GetString()
                ?? string.Empty;
            string combined =
                currentEvidence + "\n" + boundary;
            if (!combined.Contains(
                    "host",
                    StringComparison.OrdinalIgnoreCase) ||
                !combined.Contains(
                    "QEMU",
                    StringComparison.OrdinalIgnoreCase) ||
                (!combined.Contains(
                        "does not prove",
                        StringComparison.OrdinalIgnoreCase) &&
                    !combined.Contains(
                        "not live",
                        StringComparison.OrdinalIgnoreCase) &&
                    !combined.Contains(
                        "no live",
                        StringComparison.OrdinalIgnoreCase) &&
                    !combined.Contains(
                        "unproven",
                        StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ValidateSerializedOwnership(
        IReadOnlyDictionary<string, JsonElement> issueByKey)
    {
        foreach (string key in RepairKeys)
        {
            JsonElement issue = issueByKey[key];
            string lane =
                issue.GetProperty("collisionLane").GetString()
                ?? string.Empty;
            if (string.IsNullOrWhiteSpace(lane) ||
                !lane.StartsWith("lane:", StringComparison.Ordinal) ||
                CountPrefix(issue.GetProperty("labels"), "lane:") != 1 ||
                !JsonArrayContainsExact(
                    issue.GetProperty("labels"),
                    lane))
            {
                return false;
            }
        }

        for (int left = 0; left < RepairKeys.Length; left++)
        {
            JsonElement leftIssue = issueByKey[RepairKeys[left]];
            HashSet<string> leftPaths =
                leftIssue.GetProperty("ownedPaths")
                    .EnumerateArray()
                    .Select(path =>
                        path.GetString()
                        ?? string.Empty)
                    .ToHashSet(StringComparer.Ordinal);
            string leftLane =
                leftIssue.GetProperty("collisionLane").GetString()
                ?? string.Empty;

            for (int right = left + 1;
                right < RepairKeys.Length;
                right++)
            {
                JsonElement rightIssue =
                    issueByKey[RepairKeys[right]];
                string rightLane =
                    rightIssue.GetProperty("collisionLane").GetString()
                    ?? string.Empty;
                bool ownedPathOverlap =
                    rightIssue.GetProperty("ownedPaths")
                        .EnumerateArray()
                        .Select(path =>
                            path.GetString()
                            ?? string.Empty)
                        .Any(leftPaths.Contains);
                bool collisionLaneOverlap =
                    string.Equals(
                        leftLane,
                        rightLane,
                        StringComparison.Ordinal);
                if ((ownedPathOverlap || collisionLaneOverlap) &&
                    !DependsTransitively(
                        issueByKey,
                        RepairKeys[left],
                        RepairKeys[right]) &&
                    !DependsTransitively(
                        issueByKey,
                        RepairKeys[right],
                        RepairKeys[left]))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool ValidateIssueContracts(
        IReadOnlyDictionary<string, JsonElement> issueByKey)
    {
        JsonElement repair = issueByKey["M01-028"];
        JsonElement preparation = issueByKey["M01-029"];
        JsonElement identity = issueByKey["M01-030"];
        JsonElement arena = issueByKey["M01-031"];
        JsonElement callGate = issueByKey["M01-032"];

        return JsonArrayContainsExact(
                repair.GetProperty("labels"),
                "type:research") &&
            HasOwnedPath(
                repair,
                ".gitea/backlog/clros-issues.json") &&
            HasOwnedPath(
                repair,
                "scripts/sync-gitea-backlog.ps1") &&
            HasOwnedPath(
                repair,
                "tests/OS.HostTests/GiteaBacklogManifestTests.cs") &&
            HasOwnedPath(
                repair,
                "tests/OS.HostTests/M01LivePagingPrerequisiteManifestTests.cs") &&
            HasOwnedPath(
                repair,
                "tests/OS.HostTests/RepositoryRecoveryTests.cs") &&
            HasOwnedPath(repair, "docs/boot.md") &&
            HasOwnedPath(repair, "docs/architecture.md") &&
            HasOwnedPath(repair, "docs/repo-audit.md") &&
            HasOwnedPath(repair, "docs/issue-labels.md") &&
            HasContractTerms(
                repair,
                "durable",
                "graph",
                "blocker") &&
            HasOwnedPath(
                preparation,
                "src/OS.Kernel/Boot/M01LivePagingIntegration.cs") &&
            HasOwnedPath(
                preparation,
                "tests/OS.HostTests/M01LivePagingIntegrationTests.cs") &&
            HasContractTerms(
                preparation,
                "preparation",
                "synthetic",
                "execution") &&
            HasOwnedPath(
                identity,
                "src/OS.Kernel/Boot/M01LivePagingIntegration.cs") &&
            HasOwnedPath(
                identity,
                "src/OS.Kernel/Boot/X64BootIdentityMappingPlan.cs") &&
            HasOwnedPath(
                identity,
                "tests/OS.HostTests/M01LivePagingIntegrationTests.cs") &&
            HasOwnedPath(
                identity,
                "tests/OS.HostTests/X64BootIdentityMappingPlanTests.cs") &&
            HasContractTerms(
                identity,
                "serialstub",
                "acpi",
                "framebuffer",
                "identity") &&
            HasOwnedPath(
                arena,
                "src/OS.Kernel/Boot/M01LivePagingIntegration.cs") &&
            HasOwnedPath(
                arena,
                "tests/OS.HostTests/M01LivePagingIntegrationTests.cs") &&
            HasContractTerms(
                arena,
                "page table",
                "arena",
                "capacity",
                "physical") &&
            HasOwnedPath(
                callGate,
                "src/OS.Kernel/Boot/M01OwnedCr3CompositeTransition.cs") &&
            HasOwnedPath(
                callGate,
                "tests/OS.HostTests/M01OwnedCr3CompositeTransitionTests.cs") &&
            HasContractTerms(
                callGate,
                "call gate",
                "rflags",
                "return",
                "tf",
                "cet");
    }

    private static bool ValidateBlockerLedger(
        IReadOnlyDictionary<string, JsonElement> issueByKey)
    {
        return HasCurrentEvidenceTerms(
                issueByKey["M01-028"],
                "M01LivePagingIntegration",
                "synthetic",
                "historical identity aliases",
                "physical arena",
                "exact return address",
                "RFLAGS") &&
            HasCurrentEvidenceTerms(
                issueByKey["M01-029"],
                "caller-supplied",
                "synthetic",
                "preparation") &&
            HasCurrentEvidenceTerms(
                issueByKey["M01-030"],
                "AcpiScratch",
                "FramebufferMetadata",
                "SerialStub",
                "return target") &&
            HasCurrentEvidenceTerms(
                issueByKey["M01-031"],
                "disjoint flat entry workspace",
                "AllocateAnyPages",
                "TotalTablePageCount",
                "physical table pages") &&
            HasCurrentEvidenceTerms(
                issueByKey["M01-032"],
                "ordinary C#",
                "return address",
                "popfq",
                "RFLAGS");
    }

    private static bool HasCurrentEvidenceTerms(
        JsonElement issue,
        params string[] requiredTerms)
    {
        string normalized = NormalizeContractText(
            issue.GetProperty("currentEvidence").GetString()
            ?? string.Empty);
        return requiredTerms.All(term =>
            normalized.Contains(
                NormalizeContractText(term),
                StringComparison.Ordinal));
    }

    private static bool DependenciesEqual(
        JsonElement issue,
        params string[] expected)
    {
        string[] actual = issue.GetProperty("dependencyKeys")
            .EnumerateArray()
            .Select(value =>
                value.GetString()
                ?? string.Empty)
            .ToArray();
        return actual.SequenceEqual(expected, StringComparer.Ordinal);
    }

    private static bool DependsTransitively(
        IReadOnlyDictionary<string, JsonElement> issueByKey,
        string issueKey,
        string dependencyKey)
    {
        HashSet<string> visited =
            new HashSet<string>(StringComparer.Ordinal);
        Stack<string> pending = new Stack<string>();
        pending.Push(issueKey);
        while (pending.Count != 0)
        {
            string current = pending.Pop();
            if (!visited.Add(current) ||
                !issueByKey.TryGetValue(
                    current,
                    out JsonElement issue))
            {
                continue;
            }

            foreach (JsonElement dependency in
                issue.GetProperty("dependencyKeys").EnumerateArray())
            {
                string key =
                    dependency.GetString()
                    ?? string.Empty;
                if (string.Equals(
                        key,
                        dependencyKey,
                        StringComparison.Ordinal))
                {
                    return true;
                }

                pending.Push(key);
            }
        }

        return false;
    }

    private static bool HasOwnedPath(
        JsonElement issue,
        string expectedPath)
    {
        return JsonArrayContainsExact(
            issue.GetProperty("ownedPaths"),
            expectedPath);
    }

    private static bool HasContractTerms(
        JsonElement issue,
        params string[] requiredTerms)
    {
        StringBuilder builder = new StringBuilder();
        builder.Append(
            issue.GetProperty("title").GetString()
            ?? string.Empty);
        builder.Append('\n');
        builder.Append(
            issue.GetProperty("summary").GetString()
            ?? string.Empty);
        builder.Append('\n');
        AppendArrayText(
            builder,
            issue.GetProperty("implementationNotes"));
        AppendArrayText(
            builder,
            issue.GetProperty("acceptanceCriteria"));
        builder.Append(
            issue.GetProperty("evidenceBoundary").GetString()
            ?? string.Empty);
        string normalized = NormalizeContractText(
            builder.ToString());
        return requiredTerms.All(term =>
            normalized.Contains(
                NormalizeContractText(term),
                StringComparison.Ordinal));
    }

    private static void AppendArrayText(
        StringBuilder builder,
        JsonElement array)
    {
        foreach (JsonElement item in array.EnumerateArray())
        {
            builder.Append(
                item.GetString()
                ?? string.Empty);
            builder.Append('\n');
        }
    }

    private static string NormalizeContractText(string value)
    {
        StringBuilder builder = new StringBuilder(value.Length);
        bool previousWasSpace = true;
        foreach (char character in value)
        {
            char normalized = char.ToLowerInvariant(character);
            bool isWord =
                char.IsLetterOrDigit(normalized) ||
                normalized == '_';
            if (isWord)
            {
                builder.Append(normalized);
                previousWasSpace = false;
            }
            else if (!previousWasSpace)
            {
                builder.Append(' ');
                previousWasSpace = true;
            }
        }

        return builder.ToString().Trim();
    }

    private static bool JsonStringArrayEquals(
        JsonElement array,
        params string[] expected)
    {
        string[] actual = array.EnumerateArray()
            .Select(value =>
                value.GetString()
                ?? string.Empty)
            .ToArray();
        return actual.SequenceEqual(expected, StringComparer.Ordinal);
    }

    private static bool JsonArrayContainsExact(
        JsonElement array,
        string expected)
    {
        return array.EnumerateArray().Any(value =>
            string.Equals(
                value.GetString(),
                expected,
                StringComparison.Ordinal));
    }

    private static int CountPrefix(
        JsonElement array,
        string prefix)
    {
        return array.EnumerateArray().Count(value =>
            (value.GetString() ?? string.Empty).StartsWith(
                prefix,
                StringComparison.Ordinal));
    }

    private static void Require(bool condition, string reason)
    {
        if (!condition)
        {
            throw new InvalidOperationException(reason);
        }
    }

    private static string SanitizeReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return "unknown";
        }

        return reason
            .Replace('\r', '_')
            .Replace('\n', '_')
            .Replace(' ', '_');
    }
}
