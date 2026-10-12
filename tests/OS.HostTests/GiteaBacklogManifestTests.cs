using OS.Tooling;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

internal static class GiteaBacklogManifestTests
{
    private const string M01DurabilityRepairMarker =
        "CLROS_M01_036_READY epics=16 children=326 issues=342 managed=426 m01Children=36 m02Children=17 adoptedM01Issue=425 duplicateKeys=0";
    private const string M02DurableClosureMarker =
        "CLROS_M02_018_READY epics=16 children=327 issues=343 managed=427 m02Children=18 m02001Issue=41 m02001Pr=428 m02001Merge=85528a5513f1cf24396ff29acc6787bc015ec9c5 closureMarkers=2";
    private const string M02MaterializerDurableClosureMarker =
        "CLROS_M02_019_READY epics=16 children=328 issues=344 managed=428 m02Children=19 m02002Issue=42 m02002Pr=431 m02002Merge=da856db657575d8f8d67713c7afe96e113854e54 closureMarkers=2";
    private const string M02SafeStubDurableClosureMarker =
        "CLROS_M02_020_READY epics=16 children=329 issues=345 managed=429 m02Children=20 m02017Issue=427 m02017Pr=435 m02017Merge=d2872746059a7200431b6d3bc89c6e02cb42fa8b closureMarkers=2";

    private static readonly IReadOnlyDictionary<string, int> ExpectedChildren = BacklogContracts.Children;

    [HostFixture]
    internal static void Run()
    {
        int failures = 0;
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string manifestPath = Path.Combine(repositoryRoot, ".gitea", "backlog", "clros-issues.json");
        string scriptPath = Path.Combine(repositoryRoot, "tools", "OS.Tooling", "BacklogSync.cs");
        string tempRoot = Path.Combine(Path.GetTempPath(), "clros-gitea-backlog-tests-" + Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(tempRoot);
            RunTest("gitea backlog manifest is complete UTF-8 without BOM", () => TestManifestShape(manifestPath), ref failures);
            RunTest("m01.036", () => TestM01DurabilityRepairMarker(manifestPath), ref failures);
            RunTest("m02.018", () => TestM02DurableClosureMarker(manifestPath), ref failures);
            RunTest("m02.019", () => TestM02MaterializerDurableClosureMarker(manifestPath), ref failures);
            RunTest("m02.020", () => TestM02SafeStubDurableClosureMarker(manifestPath), ref failures);
            RunTest("gitea backlog offline preview validates without writes", () => TestOfflinePreview(scriptPath, manifestPath), ref failures);
            RunTest("gitea backlog managed body preserves human notes", () => TestManagedBodySelfTest(scriptPath), ref failures);
            RunTest("gitea backlog replaces labels through the dedicated endpoint", () => TestDedicatedIssueLabelEndpoint(scriptPath), ref failures);
            RunTest("gitea backlog sync refuses noncanonical target", () => TestCanonicalTargetRefusal(scriptPath), ref failures);
            RunTest("gitea backlog validation rejects missing required field", () => TestRequiredFieldsAndMilestoneIntent(scriptPath, manifestPath, tempRoot), ref failures);
            RunTest("gitea backlog validation rejects duplicate key", () => TestInvalidManifest(scriptPath, manifestPath, tempRoot, "duplicate", DuplicateIssueKey, "Duplicate issue key"), ref failures);
            RunTest("gitea backlog M01 instruction-publication chain is collision-safe", () => TestM01InstructionPublicationManifest(manifestPath), ref failures);
            RunTest("gitea backlog validation requires every M01 child through 036", () => TestInvalidManifest(scriptPath, manifestPath, tempRoot, "missing-m01-036", ReplaceLastM01ChildWithExtra, "Manifest is missing child 'M01-036'"), ref failures);
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

        HostCheck.Run("" + name, test, ref failures);
    }

    private static bool TestManifestShape(string manifestPath)
    {
        byte[] bytes = File.ReadAllBytes(manifestPath);
        if (bytes.Length < 3 ||
            (bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF))
        {
            return HostCheck.Result(false);
        }

        string json;
        try
        {
            json = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return HostCheck.Result(false);
        }

        if (json.Contains('\uFFFD') || json.Contains('\u00E2'))
        {
            return HostCheck.Result(false);
        }

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        if (root.GetProperty("schemaVersion").GetInt32() != 1 ||
            root.GetProperty("target").GetProperty("baseUrl").GetString() != BacklogContracts.BaseUrl ||
            root.GetProperty("target").GetProperty("owner").GetString() != "admin" ||
            root.GetProperty("target").GetProperty("repository").GetString() != "CLROS" ||
            root.GetProperty("policy").GetProperty("closureEvidenceMarkerFormat").GetString() !=
                "<!-- clros-evidence-reviewed:{key} -->")
        {
            return HostCheck.Result(false);
        }

        JsonElement milestones = root.GetProperty("milestones");
        JsonElement labels = root.GetProperty("labels");
        JsonElement issues = root.GetProperty("issues");
        if (milestones.GetArrayLength() != BacklogContracts.Children.Count || issues.GetArrayLength() != BacklogContracts.IssueCount || labels.GetArrayLength() < BacklogContracts.RequiredLabels.Count)
        {
            return HostCheck.Result(false);
        }

        var requiredFields = BacklogContracts.RequiredIssueFields;

        HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
        Dictionary<string, JsonElement> issueByKey = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (JsonElement issue in issues.EnumerateArray())
        {
            foreach (string field in requiredFields)
            {
                if (!issue.TryGetProperty(field, out _))
                {
                    return HostCheck.Result(false);
                }
            }

            string key = issue.GetProperty("key").GetString() ?? string.Empty;
            if (!keys.Add(key))
            {
                return HostCheck.Result(false);
            }
            issueByKey.Add(key, issue);

            if (issue.GetProperty("acceptanceCriteria").GetArrayLength() == 0 ||
                issue.GetProperty("requiredCommands").GetArrayLength() == 0 ||
                string.IsNullOrWhiteSpace(issue.GetProperty("currentEvidence").GetString()) ||
                string.IsNullOrWhiteSpace(issue.GetProperty("evidenceBoundary").GetString()))
            {
                return HostCheck.Result(false);
            }

            if (!key.EndsWith("-EPIC", StringComparison.Ordinal) &&
                !key.StartsWith("M01-", StringComparison.Ordinal) &&
                !string.Equals(key, "M03-007", StringComparison.Ordinal))
            {
                string expectedOwnedPathNote = BacklogContracts.OwnershipNote;
                string[] exactPathNotes = issue.GetProperty("implementationNotes")
                    .EnumerateArray()
                    .Select(note => note.GetString() ?? string.Empty)
                    .Where(note => note.StartsWith(
                        "The ownedPaths inventory is the exact scope for this slice.",
                        StringComparison.Ordinal))
                    .ToArray();
                if (exactPathNotes.Length != 1 ||
                    !string.Equals(exactPathNotes[0], expectedOwnedPathNote, StringComparison.Ordinal))
                {
                    return HostCheck.Result(false);
                }
            }
        }

        foreach ((string milestone, int childCount) in ExpectedChildren)
        {
            if (!keys.Contains(milestone + "-EPIC"))
            {
                return HostCheck.Result(false);
            }

            for (int number = 1; number <= childCount; number++)
            {
                if (!keys.Contains(milestone + "-" + number.ToString("000")))
                {
                    return HostCheck.Result(false);
                }
            }
        }

        foreach (JsonElement issue in issues.EnumerateArray())
        {
            foreach (JsonElement dependency in issue.GetProperty("dependencyKeys").EnumerateArray())
            {
                if (!keys.Contains(dependency.GetString() ?? string.Empty))
                {
                    return HostCheck.Result(false);
                }
            }
        }

        return HostCheck.Result(ValidateM00Contracts(issueByKey)) &&
            HostCheck.Result(ValidateM00ExitGate(issueByKey)) &&
            HostCheck.Result(ValidateM00OwnedPathSerialization(issueByKey)) &&
            HostCheck.Result(ValidateSyncVerifyRequiresApply(issueByKey)) &&
            HostCheck.Result(ValidateM01ContractsV2(issues, issueByKey)) &&
            HostCheck.Result(ValidateM03PermanentCr3Handoff(issueByKey)) &&
            HostCheck.Result(DependenciesEqual(issueByKey["M06-001"], "M05-019")) &&
            HostCheck.Result(DependenciesEqual(issueByKey["M06-019"], "M06-018", "M05-021")) &&
            HostCheck.Result(DependenciesEqual(issueByKey["M07-001"], "M05-019")) &&
            HostCheck.Result(DependenciesEqual(issueByKey["M07-024"], "M07-023", "M05-021")) &&
            HostCheck.Result(DependenciesEqual(issueByKey["M16-001"])) &&
            HostCheck.Result(DependenciesEqual(issueByKey["M09-001"], "M16-003")) &&
            HostCheck.Result(DependenciesEqual(issueByKey["M10-001"], "M16-001")) &&
            HostCheck.Result(DependenciesEqual(issueByKey["M14-001"], "M10-014")) &&
            HostCheck.Result(DependenciesEqual(issueByKey["M12-001"], "M14-016")) &&
            HostCheck.Result(DependenciesEqual(issueByKey["M13-001"], "M14-016")) &&
            HostCheck.Result(DependenciesEqual(issueByKey["M12-018"], "M12-016")) &&
            HostCheck.Result(DependenciesEqual(issueByKey["M13-018"], "M13-016")) &&
            HostCheck.Result(DependenciesEqual(issueByKey["M15-014"], "M15-009"));
    }

    private static bool TestM01InstructionPublicationManifest(string manifestPath)
    {
        using JsonDocument document = JsonDocument.Parse(
            File.ReadAllText(manifestPath));
        Dictionary<string, JsonElement> issueByKey =
            document.RootElement.GetProperty("issues")
                .EnumerateArray()
                .ToDictionary(
                    issue => issue.GetProperty("key").GetString()
                        ?? string.Empty,
                    issue => issue,
                    StringComparer.Ordinal);
        return HostCheck.Result(ValidateM01StageThreeClosure(issueByKey)) &&
            HostCheck.Result(ValidateM01InstructionPublicationContracts(issueByKey));
    }

    private static bool TestM01DurabilityRepairMarker(string manifestPath)
    {
        using JsonDocument document = JsonDocument.Parse(
            File.ReadAllText(manifestPath));
        Dictionary<string, JsonElement> issueByKey =
            document.RootElement.GetProperty("issues")
                .EnumerateArray()
                .ToDictionary(
                    issue => issue.GetProperty("key").GetString()
                        ?? string.Empty,
                    issue => issue,
                    StringComparer.Ordinal);
        if (!ValidateM01DurabilityRepairContracts(issueByKey))
        {
            return HostCheck.Result(false);
        }

        Console.WriteLine(M01DurabilityRepairMarker);
        return true;
    }

    private static bool TestM02DurableClosureMarker(string manifestPath)
    {
        using JsonDocument document = JsonDocument.Parse(
            File.ReadAllText(manifestPath));
        Dictionary<string, JsonElement> issueByKey =
            document.RootElement.GetProperty("issues")
                .EnumerateArray()
                .ToDictionary(
                    issue => issue.GetProperty("key").GetString()
                        ?? string.Empty,
                    issue => issue,
                    StringComparer.Ordinal);
        if (!ValidateM02DurableClosureContracts(issueByKey))
        {
            return HostCheck.Result(false);
        }

        Console.WriteLine(M02DurableClosureMarker);
        return true;
    }

    private static bool TestM02MaterializerDurableClosureMarker(
        string manifestPath)
    {
        using JsonDocument document = JsonDocument.Parse(
            File.ReadAllText(manifestPath));
        Dictionary<string, JsonElement> issueByKey =
            document.RootElement.GetProperty("issues")
                .EnumerateArray()
                .ToDictionary(
                    issue => issue.GetProperty("key").GetString()
                        ?? string.Empty,
                    issue => issue,
                    StringComparer.Ordinal);
        if (!ValidateM02MaterializerDurableClosureContracts(issueByKey))
        {
            return HostCheck.Result(false);
        }

        Console.WriteLine(M02MaterializerDurableClosureMarker);
        return true;
    }

    private static bool TestM02SafeStubDurableClosureMarker(
        string manifestPath)
    {
        using JsonDocument document = JsonDocument.Parse(
            File.ReadAllText(manifestPath));
        Dictionary<string, JsonElement> issueByKey =
            document.RootElement.GetProperty("issues")
                .EnumerateArray()
                .ToDictionary(
                    issue => issue.GetProperty("key").GetString()
                        ?? string.Empty,
                    issue => issue,
                    StringComparer.Ordinal);
        if (!ValidateM02SafeStubDurableClosureContracts(issueByKey))
        {
            return HostCheck.Result(false);
        }

        Console.WriteLine(M02SafeStubDurableClosureMarker);
        return true;
    }

    private static bool ValidateM01StageThreeClosure(
        IReadOnlyDictionary<string, JsonElement> issueByKey)
    {
        JsonElement issue = issueByKey["M01-025"];
        JsonElement labels = issue.GetProperty("labels");
        string currentEvidence =
            issue.GetProperty("currentEvidence").GetString() ?? string.Empty;
        string evidenceBoundary =
            issue.GetProperty("evidenceBoundary").GetString() ?? string.Empty;
        string[] requiredEvidenceTerms =
        [
            "PR #409",
            "06131dbc5dae6b68e73a827a61407b9d60392046",
            "E9377EECB18F71A8EC329ACAA14F049757A4FD2887B76C85F6F3065B7BE24A1F",
            "0A674D1628D48C478D616F43548DA18BC9673D4BECBEC0D875C7ECDA05F646CB",
            "tracker evidence comment #6254",
            "source/control-flow",
            "M01-033 through M01-035"
        ];
        return HostCheck.Result(string.Equals(
                issue.GetProperty("desiredState").GetString(),
                "closed",
                StringComparison.Ordinal)) &&
            HostCheck.Result(JsonArrayContainsExact(labels, "agent:done")) &&
            HostCheck.Result(CountPrefix(labels, "agent:") == 1) &&
            HostCheck.Result(requiredEvidenceTerms.All(term =>
                currentEvidence.Contains(term, StringComparison.Ordinal))) &&
            HostCheck.Result(evidenceBoundary.Contains(
                "host oracle, not fake-EFI or live-firmware execution",
                StringComparison.Ordinal)) &&
            HostCheck.Result(evidenceBoundary.Contains(
                "immutable-text first-publication serialization",
                StringComparison.Ordinal)) &&
            HostCheck.Result(evidenceBoundary.Contains(
                "M01-033 through M01-035 must close before M01-026",
                StringComparison.Ordinal));
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
                return HostCheck.Result(false);
            }

            if (CountPrefix(issue.GetProperty("implementationNotes"), "Positive case:") != 1 ||
                CountPrefix(issue.GetProperty("implementationNotes"), "Failure cases:") != 1 ||
                CountPrefix(issue.GetProperty("implementationNotes"), "Explicit non-goal:") != 1)
            {
                return HostCheck.Result(false);
            }
        }

        JsonElement originalBacklog = issueByKey["M00-001"];
        JsonElement repair = issueByKey["M00-023"];
        return HostCheck.Result(JsonElementContains(
                originalBacklog.GetProperty("requiredMarkers"),
                "children=303 issues=319")) &&
            HostCheck.Result(JsonElementContains(
                originalBacklog.GetProperty("acceptanceCriteria"),
                "16-milestone/319-issue manifest")) &&
            HostCheck.Result(!JsonElementContains(
                originalBacklog.GetProperty("requiredMarkers"),
                "issues=331")) &&
            HostCheck.Result(DependenciesEqual(repair, "M00-022")) &&
            HostCheck.Result(JsonElementContains(
                repair.GetProperty("requiredMarkers"),
                "children=315 issues=331")) &&
            HostCheck.Result(JsonElementContains(
                repair.GetProperty("requiredMarkers"),
                "SYNC_VERIFY_PASS milestones=16 labels=68 issues=331")) &&
            HostCheck.Result(JsonElementContains(
                repair.GetProperty("requiredCommands"),
                "backlog sync -Apply")) &&
            HostCheck.Result(CountExact(
                repair.GetProperty("requiredCommands"),
                "dotnet run --project tools/OS.Tooling -- backlog sync") == 2) &&
            HostCheck.Result(JsonElementContains(
                repair.GetProperty("acceptanceCriteria"),
                "final no-write preview reports all 415 managed objects unchanged")) &&
            HostCheck.Result(JsonElementContains(
                repair.GetProperty("acceptanceCriteria"),
                "guarded pre-apply no-write preview reports exactly wouldCreate=11 and wouldUpdate=24")) &&
            HostCheck.Result(JsonElementContains(
                repair.GetProperty("implementationNotes"),
                "M03-007 gains the post-ExitBootServices permanent-CR3 handoff contract")) &&
            HostCheck.Result(JsonElementContains(
                repair.GetProperty("implementationNotes"),
                "M03-EPIC is consequently updated")) &&
            HostCheck.Result(JsonElementContains(
                repair.GetProperty("acceptanceCriteria"),
                "creates exactly 11 issues, updates exactly 24 existing issues")) &&
            HostCheck.Result(string.Equals(
                repair.GetProperty("desiredState").GetString(),
                "closed",
                StringComparison.Ordinal)) &&
            HostCheck.Result(string.Equals(
                issueByKey["M00-EPIC"].GetProperty("desiredState").GetString(),
                "closed",
                StringComparison.Ordinal)) &&
            HostCheck.Result(HasOwnedPath(
                issueByKey["M00-001"],
                "tests/OS.HostTests/RepositoryRecoveryTests.cs")) &&
            HostCheck.Result(HasOwnedPath(
                issueByKey["M00-004"],
                "tests/OS.HostTests/HttpResponseParserTests.cs")) &&
            HostCheck.Result(HasOwnedPath(
                issueByKey["M00-014"],
                "tools/OS.Tooling/RepositoryChecks.cs")) &&
            HostCheck.Result(HasOwnedPath(
                issueByKey["M00-018"],
                "tools/OS.Tooling/EfiVerify.cs")) &&
            HostCheck.Result(HasOwnedPath(
                issueByKey["M00-018"],
                "tests/OS.HostTests/EfiArtifactVerificationScriptTests.cs")) &&
            HostCheck.Result(!HasOwnedPath(
                issueByKey["M00-016"],
                "tests/OS.HostTests/RepositoryRecoveryTests.cs")) &&
            HostCheck.Result(DependenciesEqual(issueByKey["M00-003"], "M00-002", "M00-015")) &&
            HostCheck.Result(DependenciesEqual(issueByKey["M00-006"], "M00-002", "M00-014")) &&
            HostCheck.Result(DependenciesEqual(
                issueByKey["M00-022"],
                "M00-017",
                "M00-018",
                "M00-020",
                "M00-021",
                "M00-001",
                "M00-009",
                "M00-010",
                "M00-012",
                "M00-013")) &&
            HostCheck.Result(JsonElementContains(
                issueByKey["M00-017"].GetProperty("requiredMarkers"),
                "EFI_PREFLIGHT_ILCOMPILER")) &&
            HostCheck.Result(JsonElementContains(
                issueByKey["M00-017"].GetProperty("requiredMarkers"),
                "EFI_PREFLIGHT_QEMU_IMG")) &&
            HostCheck.Result(JsonElementContains(
                issueByKey["M00-019"].GetProperty("requiredMarkers"),
                "SERIAL_MARKER_STREAM status=Passed terminal=Success")) &&
            HostCheck.Result(issueByKey["M00-022"].GetProperty("labels")
                .EnumerateArray()
                .Any(label => label.GetString() == "evidence:qemu")) &&
            HostCheck.Result(JsonElementContains(
                issueByKey["M00-015"].GetProperty("acceptanceCriteria"),
                "clros-ci-smoke-logs"));
    }

    private static bool ValidateM00ExitGate(
        IReadOnlyDictionary<string, JsonElement> issueByKey)
    {
        for (int number = 1; number <= 23; number++)
        {
            string requiredKey = "M00-" + number.ToString("000");
            if (!DependsTransitively(issueByKey, "M01-001", requiredKey))
            {
                return HostCheck.Result(false);
            }
        }

        return true;
    }

    private static bool ValidateM01ContractsV2(
        JsonElement issues,
        IReadOnlyDictionary<string, JsonElement> issueByKey)
    {
        string[] expectedOrder = Enumerable.Range(1, 19)
            .Select(number => "M01-" + number.ToString("000"))
            .Append("M01-027")
            .Concat(
                Enumerable.Range(28, 5)
                    .Select(number => "M01-" + number.ToString("000")))
            .Concat(
                Enumerable.Range(20, 6)
                    .Select(number => "M01-" + number.ToString("000")))
            .Concat(
                Enumerable.Range(33, 3)
                    .Select(number => "M01-" + number.ToString("000")))
            .Append("M01-026")
            .Append("M01-036")
            .ToArray();
        string[] actualOrder = issues.EnumerateArray()
            .Select(issue => issue.GetProperty("key").GetString() ?? string.Empty)
            .Where(key =>
                key.StartsWith("M01-", StringComparison.Ordinal) &&
                !key.EndsWith("-EPIC", StringComparison.Ordinal))
            .ToArray();
        if (!actualOrder.SequenceEqual(expectedOrder, StringComparer.Ordinal))
        {
            return HostCheck.Result(false);
        }

        IReadOnlyDictionary<string, string> expectedTitles =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["M01-001"] = "[M01-001] Define typed physical, virtual, and device-address POD wrappers.",
                ["M01-002"] = "[M01-002] Define BootReservationKind/BootReservedRange and validate canonical reservation table.",
                ["M01-003"] = "[M01-003] Define and host-test loaded-image range capture/validation contract.",
                ["M01-004"] = "[M01-004] Define owned boot-stack layout/guard/alignment/lifetime.",
                ["M01-005"] = "[M01-005] Plan/audit x64 owned-stack switch and RSP containment.",
                ["M01-006"] = "[M01-006] Refactor allocator prerequisites and materialize caller-owned state.",
                ["M01-007"] = "[M01-007] Define self-contained bounded reservation subtraction over typed ranges.",
                ["M01-008"] = "[M01-008] Adapt BootPageAllocatorMutation for typed one-page metadata allocation results.",
                ["M01-009"] = "[M01-009] Adapt BootPageAllocatorMutation for typed free/double-free/coalescing metadata results.",
                ["M01-010"] = "[M01-010] Add deterministic seeded boot-page allocator shadow-model stress tests.",
                ["M01-011"] = "[M01-011] Define PageProtection/PageCachePolicy and encode x64 page-table entries.",
                ["M01-012"] = "[M01-012] Plan required boot identity mappings.",
                ["M01-013"] = "[M01-013] Plan/audit a reversible opt-in CR3 probe and firmware-CR3 restoration.",
                ["M01-014"] = "[M01-014] Plan live RW-to-RX, checked invlpg, and constant-return protection probe.",
                ["M01-015"] = "[M01-015] Define page-zero absence policy and disabled null-page-fault probe contract."
            };
        IReadOnlyDictionary<string, string[]> expectedLeafPaths =
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["M01-001"] =
                [
                    "src/OS.Kernel/Boot/TypedPhysicalVirtualDeviceAddressPODWrappers.cs",
                    "tests/OS.HostTests/TypedPhysicalVirtualDeviceAddressPODWrappersTests.cs",
                    "docs/boot.md",
                    "docs/architecture.md"
                ],
                ["M01-002"] =
                [
                    "src/OS.Kernel/Memory/BootReservedRange.cs",
                    "src/OS.Kernel/Memory/BootReservationTableValidator.cs",
                    "tests/OS.HostTests/BootReservationTableValidatorTests.cs",
                    "docs/boot.md",
                    "docs/architecture.md"
                ],
                ["M01-003"] =
                [
                    "src/OS.Kernel/Boot/EfiLoadedImageRange.cs",
                    "src/OS.Kernel/Boot/EfiLoadedImageRangeCapturePlan.cs",
                    "tests/OS.HostTests/EfiLoadedImageRangeCapturePlanTests.cs",
                    "docs/boot.md",
                    "docs/architecture.md"
                ],
                ["M01-004"] =
                [
                    "src/OS.Kernel/Boot/OwnedBootStackLayout.cs",
                    "tests/OS.HostTests/OwnedBootStackLayoutTests.cs",
                    "docs/boot.md",
                    "docs/architecture.md"
                ],
                ["M01-005"] =
                [
                    "src/OS.Kernel/Boot/OwnedBootStackSwitchPlan.cs",
                    "tests/OS.HostTests/OwnedBootStackSwitchPlanTests.cs",
                    "docs/boot.md",
                    "docs/architecture.md"
                ],
                ["M01-006"] =
                [
                    "src/OS.Kernel/Memory/BootPageAllocatorCoreContracts.cs",
                    "src/OS.Kernel/Memory/BootPageAllocatorState.cs",
                    "src/OS.Kernel/Memory/BootPageFreeListPlan.cs",
                    "src/OS.Kernel/Memory/BootPageAllocatorStateMaterializer.cs",
                    "tests/OS.HostTests/BootPageAllocatorStateMaterializerTests.cs",
                    "docs/boot.md",
                    "docs/architecture.md"
                ],
                ["M01-007"] =
                [
                    "src/OS.Kernel/Memory/BootReservationSubtractor.cs",
                    "tests/OS.HostTests/BootReservationSubtractorTests.cs",
                    "docs/boot.md",
                    "docs/architecture.md"
                ],
                ["M01-008"] =
                [
                    "src/OS.Kernel/Memory/BootPageAllocatorMutation.cs",
                    "tests/OS.HostTests/BootPageAllocatorOnePageAllocationTests.cs",
                    "docs/boot.md",
                    "docs/architecture.md"
                ],
                ["M01-009"] =
                [
                    "src/OS.Kernel/Memory/BootPageAllocatorMutation.cs",
                    "tests/OS.HostTests/BootPageAllocatorFreeCoalescingTests.cs",
                    "docs/boot.md",
                    "docs/architecture.md"
                ],
                ["M01-010"] =
                [
                    "tests/OS.HostTests/BootPageAllocatorStressTests.cs",
                    "docs/boot.md",
                    "docs/architecture.md"
                ],
                ["M01-011"] =
                [
                    "src/OS.Kernel/Memory/PageProtection.cs",
                    "src/OS.Kernel/Memory/PageCachePolicy.cs",
                    "src/OS.Kernel/Memory/X64PageTableEntry.cs",
                    "tests/OS.HostTests/X64PageTableEntryTests.cs",
                    "docs/boot.md",
                    "docs/architecture.md"
                ],
                ["M01-012"] =
                [
                    "src/OS.Kernel/Boot/X64BootIdentityMappingPlan.cs",
                    "tests/OS.HostTests/X64BootIdentityMappingPlanTests.cs",
                    "docs/boot.md",
                    "docs/architecture.md"
                ],
                ["M01-013"] =
                [
                    "src/OS.Kernel/Memory/X64Cr3LoadStubPlan.cs",
                    "src/OS.Kernel/Memory/X64PagingModePreflightStubPlan.cs",
                    "src/OS.Kernel/Memory/X64ReversibleCr3TransitionStubPlan.cs",
                    "tests/OS.HostTests/X64Cr3LoadStubPlanTests.cs",
                    "tests/OS.HostTests/X64PagingModePreflightStubPlanTests.cs",
                    "tests/OS.HostTests/X64ReversibleCr3TransitionStubPlanTests.cs",
                    "docs/boot.md",
                    "docs/architecture.md"
                ],
                ["M01-014"] =
                [
                    "src/OS.Kernel/Memory/X64PageMap.cs",
                    "src/OS.Kernel/Memory/X64TlbInvalidationPlan.cs",
                    "src/OS.Kernel/Memory/X64InvlpgStubPlan.cs",
                    "src/OS.Kernel/Memory/X64ProtectionProbeStubPlan.cs",
                    "tests/OS.HostTests/X64PageMapTests.cs",
                    "tests/OS.HostTests/X64InvlpgStubPlanTests.cs",
                    "tests/OS.HostTests/X64ProtectionProbeStubPlanTests.cs",
                    "docs/boot.md",
                    "docs/architecture.md"
                ],
                ["M01-015"] =
                [
                    "src/OS.Kernel/Memory/PageZeroMappingPolicy.cs",
                    "src/OS.Kernel/Diagnostics/NullPageFaultProbePlan.cs",
                    "tests/OS.HostTests/PageZeroMappingPolicyTests.cs",
                    "docs/boot.md",
                    "docs/architecture.md",
                    "docs/diagnostics.md"
                ]
            };
        string[] staleTemplates =
        [
            "Complete the valid packet or socket transition",
            "retransmission timeout, reset, half-close",
            "Read or validate the exact well-formed storage object",
            "missing file/tool, duplicate key",
            "repository/evidence behavior required by",
            "TestsTests.cs",
            "PDPTPDPT",
            "LiveN4KiBPage",
            "src/OS/EfiEntry.cs"
        ];

        for (int number = 1; number <= 15; number++)
        {
            string key = "M01-" + number.ToString("000");
            string marker = "CLROS_M01_" + number.ToString("000") + "_READY";
            string expectedType = number == 10
                ? "type:test"
                : "type:implementation";
            string[] expectedMarkers =
            [
                "TEST_START m01." + number.ToString("000"),
                "TEST_PASS m01." + number.ToString("000"),
                "TEST_FAIL m01." + number.ToString("000") + " <reason>",
                marker
            ];
            JsonElement issue = issueByKey[key];
            string combined = string.Join(
                "\n",
                issue.GetProperty("implementationNotes").EnumerateArray()
                    .Concat(issue.GetProperty("acceptanceCriteria").EnumerateArray())
                    .Select(value => value.GetString() ?? string.Empty));
            if (!string.Equals(
                    issue.GetProperty("title").GetString(),
                    expectedTitles[key],
                    StringComparison.Ordinal) ||
                !JsonElementContains(issue.GetProperty("labels"), "evidence:host") ||
                JsonElementContains(issue.GetProperty("labels"), "evidence:qemu") ||
                CountPrefix(issue.GetProperty("labels"), "type:") != 1 ||
                !JsonArrayContainsExact(issue.GetProperty("labels"), expectedType) ||
                issue.GetProperty("collisionLane").ValueKind != JsonValueKind.Null ||
                !JsonStringArrayEquals(
                    issue.GetProperty("requiredMarkers"),
                    expectedMarkers) ||
                JsonElementContains(issue.GetProperty("requiredCommands"), " -Qemu \"") ||
                !(issue.GetProperty("evidenceBoundary").GetString() ?? string.Empty).Contains(
                    "not linked into or executed by the canonical EFI artifact",
                    StringComparison.Ordinal) ||
                combined.Contains("#if NOSTDLIB ", StringComparison.Ordinal) ||
                combined.Contains("when NOSTDLIB is defined", StringComparison.Ordinal) ||
                combined.Contains("non-NOSTDLIB", StringComparison.Ordinal) ||
                staleTemplates.Any(template =>
                    combined.Contains(template, StringComparison.Ordinal)) ||
                !combined.Contains(
                    marker + " is a host-harness readiness marker",
                    StringComparison.Ordinal) ||
                (number != 10 &&
                    !combined.Contains("NOSTDLIB_EFI", StringComparison.Ordinal)))
            {
                return HostCheck.Result(false);
            }

            if (!OwnedPathsEqual(issue, expectedLeafPaths[key]))
            {
                return HostCheck.Result(false);
            }
        }

        if (!ValidateM01ReferenceLabels(issueByKey) ||
            !ReferencePathExists(
                issueByKey["M01-EPIC"],
                "references/RoseOS-master/Loader/src/MemoryMap.cs") ||
            !ReferencePathExists(
                issueByKey["M01-001"],
                "references/RoseOS-master/Loader/src/MemoryMap.cs") ||
            !ReferencePathExists(
                issueByKey["M01-020"],
                "references/zerosharp-master/efi-no-runtime/efinoruntime.csproj"))
        {
            return HostCheck.Result(false);
        }

        string leaf001Evidence =
            issueByKey["M01-001"].GetProperty("currentEvidence").GetString() ?? string.Empty;
        string leaf002 = JsonArrayText(issueByKey["M01-002"].GetProperty("implementationNotes"));
        string leaf006 = JsonArrayText(issueByKey["M01-006"].GetProperty("implementationNotes"));
        string leaf007 = JsonArrayText(issueByKey["M01-007"].GetProperty("implementationNotes"));
        string leaf011 = JsonArrayText(issueByKey["M01-011"].GetProperty("implementationNotes"));
        string leaf013 = JsonArrayText(issueByKey["M01-013"].GetProperty("implementationNotes"));
        string leaf014 = JsonArrayText(issueByKey["M01-014"].GetProperty("implementationNotes"));
        if (!leaf001Evidence.Contains(
                "04f2bc09c07f23b9e9c2a4cbc5be2a80823d4e34",
                StringComparison.Ordinal) ||
            !leaf001Evidence.Contains(
                "not linked into or executed by the canonical EFI artifact",
                StringComparison.Ordinal) ||
            !leaf002.Contains("PageProtectionProbe=14", StringComparison.Ordinal) ||
            !leaf002.Contains("future values append only", StringComparison.Ordinal) ||
            !leaf006.Contains("BootPageAllocatorCoreContracts", StringComparison.Ordinal) ||
            !leaf006.Contains("BootPageAllocatorStateMaterializer", StringComparison.Ordinal) ||
            !leaf007.Contains("BootReservationSubtractor", StringComparison.Ordinal) ||
            !leaf011.Contains(
                "plain explicit-valued enum",
                StringComparison.Ordinal) ||
            !leaf011.Contains("do not use System.FlagsAttribute", StringComparison.Ordinal) ||
            !leaf011.Contains(
                "None=0, Read=1, Write=2, Execute=4",
                StringComparison.Ordinal) ||
            !leaf011.Contains(
                "Uninitialized=0, WriteBack=1, Uncached=2, WriteCombining=3",
                StringComparison.Ordinal) ||
            !leaf013.Contains("exact raw firmware CR3 and CR4", StringComparison.Ordinal) ||
            !leaf013.Contains("clear CR4.PGE", StringComparison.Ordinal) ||
            !leaf013.Contains("CR4.PCIDE=0", StringComparison.Ordinal) ||
            !leaf013.Contains("CR4.LA57=0", StringComparison.Ordinal) ||
            !leaf014.Contains(
                "BootReservationKind.PageProtectionProbe",
                StringComparison.Ordinal) ||
            !leaf014.Contains("separate nonoverlapping EfiLoaderCode", StringComparison.Ordinal) ||
            !JsonElementContains(
                issueByKey["M01-015"].GetProperty("implementationNotes"),
                "M02-014"))
        {
            return HostCheck.Result(false);
        }

        return HostCheck.Result(ValidateM01IntegrationContractsV2(issueByKey));
    }

    private static bool ValidateM01ReferenceLabels(
        IReadOnlyDictionary<string, JsonElement> issueByKey)
    {
        for (int number = 1; number <= 36; number++)
        {
            string key = "M01-" + number.ToString("000");
            JsonElement issue = issueByKey[key];
            JsonElement[] external = issue.GetProperty("referenceInfluences")
                .EnumerateArray()
                .Where(reference =>
                    (reference.GetProperty("path").GetString() ?? string.Empty).StartsWith(
                        "references/",
                        StringComparison.Ordinal))
                .ToArray();
            bool hasAdapt = external.Any(reference =>
                string.Equals(
                    reference.GetProperty("use").GetString(),
                    "adapt",
                    StringComparison.Ordinal));
            bool hasTestOnly = external.Any(reference =>
                string.Equals(
                    reference.GetProperty("use").GetString(),
                    "test-only",
                    StringComparison.Ordinal));
            string? expected = hasAdapt
                ? "reference:adapt"
                : hasTestOnly
                    ? "reference:test-only"
                    : null;
            JsonElement labels = issue.GetProperty("labels");
            if (CountPrefix(labels, "reference:") != (expected is null ? 0 : 1) ||
                (expected is not null && !JsonArrayContainsExact(labels, expected)) ||
                external.Any(reference =>
                    string.Equals(
                        reference.GetProperty("use").GetString(),
                        "license-blocked",
                        StringComparison.Ordinal)))
            {
                return HostCheck.Result(false);
            }
        }

        return true;
    }

    private static bool ValidateM01IntegrationContractsV2(
        IReadOnlyDictionary<string, JsonElement> issueByKey)
    {
        IReadOnlyDictionary<string, string> lanes =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["M01-016"] = "lane:probes",
                ["M01-017"] = "lane:boot-sequence",
                ["M01-018"] = "lane:boot-sequence",
                ["M01-019"] = "lane:boot-sequence",
                ["M01-027"] = "lane:probes",
                ["M01-020"] = "lane:os-project",
                ["M01-021"] = "lane:efi-entry",
                ["M01-022"] = "lane:central-docs",
                ["M01-023"] = "lane:efi-entry",
                ["M01-024"] = "lane:central-docs",
                ["M01-025"] = "lane:efi-entry",
                ["M01-026"] = "lane:central-docs"
            };
        IReadOnlyDictionary<string, string> types =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["M01-016"] = "type:tooling",
                ["M01-017"] = "type:implementation",
                ["M01-018"] = "type:implementation",
                ["M01-019"] = "type:implementation",
                ["M01-027"] = "type:implementation",
                ["M01-020"] = "type:tooling",
                ["M01-021"] = "type:implementation",
                ["M01-022"] = "type:test",
                ["M01-023"] = "type:implementation",
                ["M01-024"] = "type:test",
                ["M01-025"] = "type:implementation",
                ["M01-026"] = "type:test"
            };
        IReadOnlyDictionary<string, string[]> ownedPaths =
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["M01-016"] =
                [
                    "src/OS.Kernel/Diagnostics/Probes.cs",
                    "tests/OS.HostTests/DiagnosticsTests.cs",
                    "docs/diagnostics.md"
                ],
                ["M01-017"] =
                [
                    "src/OS.Kernel/Boot/M01LoadedImageStackIntegration.cs",
                    "src/OS.Kernel/Boot/BootSequence.cs",
                    "src/OS.Kernel/Boot/BootSequencePlan.cs",
                    "tests/OS.HostTests/M01LoadedImageStackIntegrationTests.cs",
                    "tests/OS.HostTests/BootSequencePlanTests.cs"
                ],
                ["M01-018"] =
                [
                    "src/OS.Kernel/Boot/M01LiveAllocatorIntegration.cs",
                    "src/OS.Kernel/Boot/BootSequence.cs",
                    "src/OS.Kernel/Boot/BootSequencePlan.cs",
                    "tests/OS.HostTests/M01LiveAllocatorIntegrationTests.cs",
                    "tests/OS.HostTests/BootSequencePlanTests.cs"
                ],
                ["M01-019"] =
                [
                    "src/OS.Kernel/Boot/M01LivePagingIntegration.cs",
                    "src/OS.Kernel/Boot/BootSequence.cs",
                    "src/OS.Kernel/Boot/BootSequencePlan.cs",
                    "tests/OS.HostTests/M01LivePagingIntegrationTests.cs",
                    "tests/OS.HostTests/BootSequencePlanTests.cs"
                ],
                ["M01-027"] =
                [
                    "src/OS.Kernel/Boot/M01OwnedCr3CompositeTransition.cs",
                    "tests/OS.HostTests/M01OwnedCr3CompositeTransitionTests.cs",
                    "docs/boot.md",
                    "docs/architecture.md"
                ],
                ["M01-020"] =
                [
                    "src/OS/OS.csproj",
                    "tools/OS.Tooling/EfiBuild.cs",
                    "tests/OS.HostTests/BuildEfiShellPreflightTests.cs",
                    "tests/OS.HostTests/BootPageAllocatorFreeCoalescingTests.cs",
                    "tests/OS.HostTests/BootPageAllocatorOnePageAllocationTests.cs",
                    "tests/OS.HostTests/BootPageAllocatorStateMaterializerTests.cs",
                    "tests/OS.HostTests/BootReservationSubtractorTests.cs",
                    "tests/OS.HostTests/BootSequencePlanTests.cs",
                    "tests/OS.HostTests/M01LoadedImageStackIntegrationTests.cs",
                    "tests/OS.HostTests/M01LiveAllocatorIntegrationTests.cs",
                    "tests/OS.HostTests/OwnedBootStackLayoutTests.cs",
                    "tests/OS.HostTests/OwnedBootStackSwitchPlanTests.cs",
                    "tests/OS.HostTests/PageZeroMappingPolicyTests.cs",
                    "tests/OS.HostTests/X64BootIdentityMappingPlanTests.cs",
                    "tests/OS.HostTests/X64PageTableEntryTests.cs",
                    "tests/OS.HostTests/GiteaBacklogManifestTests.cs",
                    ".gitea/backlog/clros-issues.json",
                    "docs/efi-build.md"
                ],
                ["M01-021"] =
                [
                    "src/OS/Boot/EfiEntry.cs",
                    "src/OS/Runtime/MinimalRuntime.cs",
                    "src/OS.Kernel/Boot/M01LoadedImageStackIntegration.cs",
                    "src/OS.Kernel/Runtime/NativeAotHelperLedger.cs",
                    "tests/OS.HostTests/M01LoadedImageStackEfiEntryTests.cs",
                    "tests/OS.HostTests/M01LoadedImageStackIntegrationTests.cs",
                    "tests/OS.HostTests/BootSequencePlanTests.cs",
                    "tests/OS.HostTests/NativeAotHelperLedgerTests.cs",
                    "tests/OS.HostTests/GiteaBacklogManifestTests.cs",
                    ".gitea/backlog/clros-issues.json",
                    "docs/boot.md",
                    "docs/efi-build.md",
                    "docs/nativeaot-nostdlib-limits.md",
                    "done/step869.md",
                    "done/step879.md"
                ],
                ["M01-022"] =
                [
                    "tools/OS.Tooling/QemuSmoke.cs",
                    "tools/OS.QemuRunner/Program.cs",
                    "tools/OS.QemuRunner/SerialMarkerStateMachine.cs",
                    "tests/OS.HostTests/QemuRunnerTests.cs",
                    "docs/qemu.md",
                    "docs/boot.md",
                    "docs/architecture.md"
                ],
                ["M01-023"] =
                [
                    "src/OS/Boot/EfiEntry.cs",
                    "tests/OS.HostTests/M01LiveAllocatorEfiEntryTests.cs"
                ],
                ["M01-024"] =
                [
                    "tools/OS.Tooling/QemuSmoke.cs",
                    "tests/OS.HostTests/QemuRunnerTests.cs",
                    "docs/qemu.md",
                    "docs/boot.md",
                    "docs/architecture.md"
                ],
                ["M01-025"] =
                [
                    "src/OS/Boot/EfiEntry.cs",
                    "tests/OS.HostTests/M01LivePagingEfiEntryTests.cs"
                ],
                ["M01-026"] =
                [
                    "tools/OS.Tooling/QemuSmoke.cs",
                    "tests/OS.HostTests/QemuRunnerTests.cs",
                    "docs/qemu.md",
                    "docs/boot.md",
                    "docs/architecture.md",
                    "docs/diagnostics.md",
                    "docs/completion-gates.md"
                ]
            };
        string[] qemuIssues = ["M01-022", "M01-024", "M01-026"];
        string[] efiIssues = ["M01-021", "M01-023", "M01-025"];
        string[] hostOnlyIssues =
        [
            "M01-016", "M01-017", "M01-018", "M01-019", "M01-020",
            "M01-021", "M01-023", "M01-025", "M01-027", "M01-028",
            "M01-029", "M01-030", "M01-031", "M01-032", "M01-033",
            "M01-034", "M01-035"
        ];

        for (int number = 16; number <= 26; number++)
        {
            string key = "M01-" + number.ToString("000");
            string dependency = number == 16
                ? "M01-015"
                : number == 20
                    ? "M01-027"
                    : "M01-" + (number - 1).ToString("000");
            JsonElement issue = issueByKey[key];
            string lane = lanes[key];
            bool qemu = qemuIssues.Contains(key, StringComparer.Ordinal);
            bool dependencyContractSatisfied =
                key == "M01-025"
                    ? DependenciesEqual(
                        issue,
                        "M01-024",
                        "M01-027",
                        "M01-029",
                        "M01-030",
                        "M01-031",
                        "M01-032")
                    : key == "M01-026"
                        ? DependenciesEqual(issue, "M01-035")
                    : DependenciesEqual(issue, dependency);
            if (!dependencyContractSatisfied ||
                !string.Equals(
                    issue.GetProperty("collisionLane").GetString(),
                    lane,
                    StringComparison.Ordinal) ||
                !JsonArrayContainsExact(issue.GetProperty("labels"), lane) ||
                !JsonArrayContainsExact(issue.GetProperty("labels"), types[key]) ||
                CountPrefix(issue.GetProperty("labels"), "lane:") != 1 ||
                CountPrefix(issue.GetProperty("labels"), "type:") != 1 ||
                !OwnedPathsEqual(issue, ownedPaths[key]) ||
                (qemu && !JsonArrayContainsExact(
                    issue.GetProperty("labels"),
                    "evidence:qemu")) ||
                (!qemu && !JsonArrayContainsExact(
                    issue.GetProperty("labels"),
                    "evidence:host")))
            {
                return HostCheck.Result(false);
            }
        }

        JsonElement composite = issueByKey["M01-027"];
        if (!string.Equals(
                issueByKey["M01-019"].GetProperty("desiredState").GetString(),
                "closed",
                StringComparison.Ordinal) ||
            !string.Equals(
                composite.GetProperty("desiredState").GetString(),
                "closed",
                StringComparison.Ordinal) ||
            !string.Equals(
                issueByKey["M01-020"].GetProperty("desiredState").GetString(),
                "closed",
                StringComparison.Ordinal) ||
            !DependenciesEqual(composite, "M01-019") ||
            !string.Equals(
                composite.GetProperty("collisionLane").GetString(),
                lanes["M01-027"],
                StringComparison.Ordinal) ||
            !JsonArrayContainsExact(
                composite.GetProperty("labels"),
                lanes["M01-027"]) ||
            !JsonArrayContainsExact(
                composite.GetProperty("labels"),
                types["M01-027"]) ||
            CountPrefix(composite.GetProperty("labels"), "lane:") != 1 ||
            CountPrefix(composite.GetProperty("labels"), "type:") != 1 ||
            !OwnedPathsEqual(composite, ownedPaths["M01-027"]) ||
            !JsonArrayContainsExact(
                composite.GetProperty("labels"),
                "evidence:host"))
        {
            return HostCheck.Result(false);
        }

        foreach (string key in hostOnlyIssues)
        {
            string suffix = key[4..];
            string[] expected =
            [
                "TEST_START m01." + suffix,
                "CLROS_M01_" + suffix + "_READY",
                "TEST_PASS m01." + suffix,
                "TEST_FAIL m01." + suffix + " <reason>"
            ];
            if (!JsonStringArrayEquals(
                    issueByKey[key].GetProperty("requiredMarkers"),
                    expected) ||
                JsonElementContains(
                    issueByKey[key].GetProperty("requiredCommands"),
                    " -Qemu \""))
            {
                return HostCheck.Result(false);
            }
        }

        string[] stage1Segment =
        [
            "TEST_START m01.stage1",
            "M01_LOADED_IMAGE_CAPTURED",
            "M01_STACK_SWITCH_STUB_READY",
            "M01_STACK_ARENA_ALLOCATED",
            "M01_STACK_GUARD_RESERVED",
            "M01_STAGE1_MAP_RECAPTURED",
            "M01_STAGE1_RESERVATIONS_VALIDATED",
            "M01_OWNED_STACK_READY",
            "M01_OWNED_STACK_SWITCHED",
            "M01_RSP_CONTAINED",
            "TEST_PASS m01.stage1"
        ];
        string[] stage2Segment =
        [
            "TEST_START m01.stage2",
            "M01_ALLOCATOR_ARENA_ALLOCATED",
            "M01_STAGE2_MAP_RECAPTURED",
            "M01_RESERVATIONS_APPLIED",
            "M01_LIVE_ALLOCATED_4K",
            "M01_LIVE_PAGE_WITHIN_ARENA",
            "M01_LIVE_FREED_4K",
            "M01_DOUBLE_FREE_REJECTED",
            "TEST_PASS m01.stage2"
        ];
        string[] stage3Segment =
        [
            "TEST_START m01.stage3",
            "M01_CR3_STUB_PAGE_ALLOCATED",
            "M01_PROTECTION_PROBE_PAGE_ALLOCATED",
            "M01_PAGE_TABLE_STATE_ARENA_ALLOCATED",
            "M01_STAGE3_MAP_RECAPTURED",
            "M01_CR3_STUB_MATERIALIZED",
            "M01_PAGE_TABLES_BUILT",
            "M01_PAGING_MODE_VALIDATED",
            "M01_PRE_CR3",
            "M01_FIRMWARE_CR3_RESTORED",
            "M01_RFLAGS_RESTORED",
            "M01_PGE_CYCLE_VERIFIED",
            "M01_OWNED_CR3_CANARY_VERIFIED",
            "M01_INSTRUCTION_PUBLICATION_EPOCHS_VERIFIED",
            "M01_POST_CR3",
            "M01_RW_TO_RX_VERIFIED",
            "M01_TLB_INVALIDATION_COMPLETE",
            "M01_RX_PROBE_EXECUTED",
            "M01_PAGE_ZERO_ABSENT",
            "TEST_PASS m01.stage3",
            "CLROS_M01_LIVE_BOOT_MEMORY_READY"
        ];
        IReadOnlyDictionary<string, string[]> guestMarkers =
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["M01-022"] = stage1Segment
                    .Append("OS_SMOKE_PASS")
                    .ToArray(),
                ["M01-024"] = stage1Segment
                    .Concat(stage2Segment)
                    .Append("OS_SMOKE_PASS")
                    .ToArray(),
                ["M01-026"] = stage1Segment
                    .Concat(stage2Segment)
                    .Concat(stage3Segment)
                    .Append("OS_SMOKE_PASS")
                    .ToArray()
            };
        IReadOnlyDictionary<string, string> guestFailureMarkers =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["M01-022"] = "TEST_FAIL m01.stage1 <reason>",
                ["M01-024"] = "TEST_FAIL m01.stage2 <reason>",
                ["M01-026"] = "TEST_FAIL m01.stage3 <reason>"
            };

        foreach (string key in qemuIssues)
        {
            JsonElement issue = issueByKey[key];
            string commands = JsonArrayText(issue.GetProperty("requiredCommands"));
            string notes = JsonArrayText(issue.GetProperty("implementationNotes"));
            string suffix = key[4..];
            if (!commands.Contains(" -BuildEfi", StringComparison.Ordinal) ||
                !commands.Contains(" -Qemu \"", StringComparison.Ordinal) ||
                !commands.Contains("-RequireMarker", StringComparison.Ordinal) ||
                !commands.Contains("-RequireOrderedMarker", StringComparison.Ordinal) ||
                !commands.Contains("-RequireUniqueMarker", StringComparison.Ordinal) ||
                commands.Contains("TEST_PASS m01." + suffix, StringComparison.Ordinal) ||
                JsonArrayContainsExact(
                    issue.GetProperty("requiredMarkers"),
                    "TEST_START m01." + suffix) ||
                JsonArrayContainsExact(
                    issue.GetProperty("requiredMarkers"),
                    "TEST_PASS m01." + suffix) ||
                JsonArrayContainsExact(
                    issue.GetProperty("requiredMarkers"),
                    "TEST_FAIL m01." + suffix + " <reason>") ||
                !notes.Contains(
                    "host-harness markers",
                    StringComparison.Ordinal) ||
                !notes.Contains(
                    "runner-process markers",
                    StringComparison.Ordinal) ||
                !notes.Contains(
                    "guest serial output",
                    StringComparison.Ordinal) ||
                !ValidateQemuMarkerContract(
                    issue,
                    guestMarkers[key],
                    guestFailureMarkers[key]))
            {
                return HostCheck.Result(false);
            }
        }

        string stage1Commands =
            JsonArrayText(issueByKey["M01-022"].GetProperty("requiredCommands"));
        string stage2Commands =
            JsonArrayText(issueByKey["M01-024"].GetProperty("requiredCommands"));
        string stage3Commands =
            JsonArrayText(issueByKey["M01-026"].GetProperty("requiredCommands"));
        if (stage1Commands.Contains(
                "LIVE_ALLOCATOR_NOT_INSTALLED",
                StringComparison.Ordinal) ||
            stage1Commands.Contains(
                "BOOT_PAGE_ALLOCATOR_NOT_READY",
                StringComparison.Ordinal) ||
            !stage2Commands.Contains(
                "LIVE_ALLOCATOR_NOT_INSTALLED",
                StringComparison.Ordinal) ||
            !stage2Commands.Contains(
                "BOOT_PAGE_ALLOCATOR_NOT_READY",
                StringComparison.Ordinal) ||
            !stage3Commands.Contains(
                "M01_PROTECTION_PROBE_PAGE_ALLOCATED",
                StringComparison.Ordinal) ||
            !stage3Commands.Contains(
                "M01_PAGING_MODE_VALIDATED",
                StringComparison.Ordinal) ||
            !stage3Commands.Contains(
                "M01_FIRMWARE_CR3_RESTORED,M01_RFLAGS_RESTORED,M01_PGE_CYCLE_VERIFIED",
                StringComparison.Ordinal) ||
            !stage3Commands.Contains(
                "M01_RW_TO_RX_VERIFIED,M01_TLB_INVALIDATION_COMPLETE,M01_RX_PROBE_EXECUTED",
                StringComparison.Ordinal) ||
            !stage3Commands.Contains("M01_PGE_CLEAR_FAILED", StringComparison.Ordinal) ||
            !stage3Commands.Contains("M01_PGE_RESTORE_FAILED", StringComparison.Ordinal) ||
            !stage3Commands.Contains("M01_RX_PROBE_FAILED", StringComparison.Ordinal) ||
            stage3Commands.Contains("M01_OWNED_CR3_ENTERED", StringComparison.Ordinal))
        {
            return HostCheck.Result(false);
        }

        string linkNotes =
            JsonArrayText(issueByKey["M01-020"].GetProperty("implementationNotes"));
        string[] requiredCompileNames =
        [
            "BootPageAllocatorCoreContracts.cs",
            "UefiMemoryType.cs",
            "PageProtection.cs",
            "PageCachePolicy.cs",
            "X64PagingModePreflightStubPlan.cs",
            "X64ReversibleCr3TransitionStubPlan.cs",
            "X64InvlpgStubPlan.cs",
            "X64ProtectionProbeStubPlan.cs",
            "M01LoadedImageStackIntegration.cs",
            "M01LiveAllocatorIntegration.cs",
            "M01LivePagingIntegration.cs",
            "M01OwnedCr3CompositeTransition.cs",
            "BootSequencePlan.cs"
        ];
        string[] excludedCompileNames =
        [
            "M01-010",
            "NullPageFaultProbePlan.cs",
            "BootSequence.cs",
            "BootPageAllocatorState.cs",
            "BootPageFreeListPlan.cs",
            "BootPageAllocatorPlan.cs",
            "BootMemoryRegionPlan.cs"
        ];
        if (requiredCompileNames.Any(name =>
                !linkNotes.Contains(name, StringComparison.Ordinal)) ||
            excludedCompileNames.Any(name =>
                !linkNotes.Contains(name, StringComparison.Ordinal)) ||
            !linkNotes.Contains("NOSTDLIB_EFI", StringComparison.Ordinal) ||
            !linkNotes.Contains("System.FlagsAttribute", StringComparison.Ordinal) ||
            linkNotes.Contains("only if its audited POD surface", StringComparison.Ordinal) ||
            !JsonElementContains(
                issueByKey["M01-020"].GetProperty("requiredCommands"),
                "-EnableM01LoadedImageStackProbe -EnableM01LiveAllocatorProbe -EnableM01OwnedPagingProbe"))
        {
            return HostCheck.Result(false);
        }

        return HostCheck.Result(ValidateM01PrerequisiteRepairContracts(issueByKey)) &&
            HostCheck.Result(ValidateM01InstructionPublicationContracts(issueByKey)) &&
            HostCheck.Result(ValidateM01DurabilityRepairContracts(issueByKey)) &&
            HostCheck.Result(efiIssues.All(key =>
                JsonElementContains(
                    issueByKey[key].GetProperty("requiredCommands"),
                    "efi build") &&
                !JsonElementContains(
                    issueByKey[key].GetProperty("requiredCommands"),
                    "qemu smoke"))) &&
            HostCheck.Result(HasOwnedPath(issueByKey["M01-026"], "docs/completion-gates.md")) &&
            HostCheck.Result(JsonElementContains(
                issueByKey["M01-026"].GetProperty("acceptanceCriteria"),
                "M02-014")) &&
            HostCheck.Result(DependenciesEqual(issueByKey["M02-EPIC"], "M01-036")) &&
            HostCheck.Result(DependenciesEqual(issueByKey["M02-001"], "M01-036")) &&
            HostCheck.Result(DependenciesEqual(issueByKey["M04-EPIC"], "M01-036")) &&
            HostCheck.Result(DependenciesEqual(issueByKey["M04-001"], "M01-036"));
    }

    private static bool ValidateM01PrerequisiteRepairContracts(
        IReadOnlyDictionary<string, JsonElement> issueByKey)
    {
        string[] repairKeys =
        [
            "M01-028",
            "M01-029",
            "M01-030",
            "M01-031",
            "M01-032"
        ];
        if (repairKeys.Any(key => !issueByKey.ContainsKey(key)) ||
            !DependenciesEqual(
                issueByKey["M01-029"],
                "M01-028",
                "M01-027") ||
            !DependenciesEqual(issueByKey["M01-030"], "M01-029") ||
            !DependenciesEqual(issueByKey["M01-031"], "M01-030") ||
            !DependenciesEqual(issueByKey["M01-032"], "M01-031") ||
            !DependenciesEqual(
                issueByKey["M01-025"],
                "M01-024",
                "M01-027",
                "M01-029",
                "M01-030",
                "M01-031",
                "M01-032") ||
            !DependenciesEqual(issueByKey["M01-026"], "M01-035"))
        {
            return HostCheck.Result(false);
        }

        foreach (string key in repairKeys)
        {
            JsonElement issue = issueByKey[key];
            string lane = issue.GetProperty("collisionLane").GetString()
                ?? string.Empty;
            string commands = JsonArrayText(
                issue.GetProperty("requiredCommands"));
            if (string.IsNullOrWhiteSpace(lane) ||
                !lane.StartsWith("lane:", StringComparison.Ordinal) ||
                CountPrefix(issue.GetProperty("labels"), "lane:") != 1 ||
                !JsonArrayContainsExact(issue.GetProperty("labels"), lane) ||
                !JsonArrayContainsExact(
                    issue.GetProperty("labels"),
                    "evidence:host") ||
                commands.Contains(" -Qemu \"", StringComparison.Ordinal) ||
                commands.Contains(
                    " -OvmfCode \"",
                    StringComparison.Ordinal))
            {
                return HostCheck.Result(false);
            }
        }

        if (!JsonArrayContainsExact(
                issueByKey["M01-028"].GetProperty("labels"),
                "type:research") ||
            !HasOwnedPath(
                issueByKey["M01-028"],
                ".gitea/backlog/clros-issues.json") ||
            !HasOwnedPath(
                issueByKey["M01-028"],
                "tools/OS.Tooling/BacklogSync.cs") ||
            !HasOwnedPath(
                issueByKey["M01-028"],
                "tests/OS.HostTests/GiteaBacklogManifestTests.cs") ||
            !HasOwnedPath(
                issueByKey["M01-028"],
                "tests/OS.HostTests/M01LivePagingPrerequisiteManifestTests.cs") ||
            !HasOwnedPath(
                issueByKey["M01-028"],
                "tests/OS.HostTests/RepositoryRecoveryTests.cs") ||
            !HasOwnedPath(issueByKey["M01-028"], "docs/boot.md") ||
            !HasOwnedPath(issueByKey["M01-028"], "docs/architecture.md") ||
            !HasOwnedPath(issueByKey["M01-028"], "docs/repo-audit.md") ||
            !HasOwnedPath(issueByKey["M01-028"], "docs/issue-labels.md") ||
            !HasOwnedPath(
                issueByKey["M01-029"],
                "src/OS.Kernel/Boot/M01LivePagingIntegration.cs") ||
            !HasOwnedPath(
                issueByKey["M01-029"],
                "tests/OS.HostTests/M01LivePagingIntegrationTests.cs") ||
            !HasOwnedPath(
                issueByKey["M01-030"],
                "src/OS.Kernel/Boot/M01LivePagingIntegration.cs") ||
            !HasOwnedPath(
                issueByKey["M01-030"],
                "src/OS.Kernel/Boot/X64BootIdentityMappingPlan.cs") ||
            !HasOwnedPath(
                issueByKey["M01-030"],
                "tests/OS.HostTests/M01LivePagingIntegrationTests.cs") ||
            !HasOwnedPath(
                issueByKey["M01-030"],
                "tests/OS.HostTests/X64BootIdentityMappingPlanTests.cs") ||
            !HasOwnedPath(
                issueByKey["M01-031"],
                "src/OS.Kernel/Boot/M01LivePagingIntegration.cs") ||
            !HasOwnedPath(
                issueByKey["M01-031"],
                "tests/OS.HostTests/M01LivePagingIntegrationTests.cs") ||
            !HasOwnedPath(
                issueByKey["M01-032"],
                "src/OS.Kernel/Boot/M01OwnedCr3CompositeTransition.cs") ||
            !HasOwnedPath(
                issueByKey["M01-032"],
                "tests/OS.HostTests/M01OwnedCr3CompositeTransitionTests.cs"))
        {
            return HostCheck.Result(false);
        }

        for (int left = 0; left < repairKeys.Length; left++)
        {
            JsonElement leftIssue = issueByKey[repairKeys[left]];
            HashSet<string> leftPaths = leftIssue.GetProperty("ownedPaths")
                .EnumerateArray()
                .Select(path => path.GetString() ?? string.Empty)
                .ToHashSet(StringComparer.Ordinal);
            string leftLane =
                leftIssue.GetProperty("collisionLane").GetString()
                ?? string.Empty;
            for (int right = left + 1;
                right < repairKeys.Length;
                right++)
            {
                JsonElement rightIssue = issueByKey[repairKeys[right]];
                string rightLane =
                    rightIssue.GetProperty("collisionLane").GetString()
                    ?? string.Empty;
                bool overlaps =
                    rightIssue.GetProperty("ownedPaths")
                        .EnumerateArray()
                        .Select(path => path.GetString() ?? string.Empty)
                        .Any(leftPaths.Contains) ||
                    string.Equals(
                        leftLane,
                        rightLane,
                        StringComparison.Ordinal);
                if (overlaps &&
                    !DependsTransitively(
                        issueByKey,
                        repairKeys[left],
                        repairKeys[right]) &&
                    !DependsTransitively(
                        issueByKey,
                        repairKeys[right],
                        repairKeys[left]))
                {
                    return HostCheck.Result(false);
                }
            }
        }

        return true;
    }

    private static bool ValidateM01InstructionPublicationContracts(
        IReadOnlyDictionary<string, JsonElement> issueByKey)
    {
        string[] keys = ["M01-033", "M01-034", "M01-035"];
        if (keys.Any(key => !issueByKey.ContainsKey(key)) ||
            !DependenciesEqual(issueByKey["M01-033"], "M01-025") ||
            !DependenciesEqual(issueByKey["M01-034"], "M01-033") ||
            !DependenciesEqual(issueByKey["M01-035"], "M01-034") ||
            !DependenciesEqual(issueByKey["M01-026"], "M01-035"))
        {
            return HostCheck.Result(false);
        }

        IReadOnlyDictionary<string, string?> lanes =
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["M01-033"] = null,
                ["M01-034"] = "lane:os-project",
                ["M01-035"] = "lane:efi-entry"
            };
        IReadOnlyDictionary<string, string> types =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["M01-033"] = "type:tooling",
                ["M01-034"] = "type:tooling",
                ["M01-035"] = "type:implementation"
            };
        IReadOnlyDictionary<string, string> desiredStates =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["M01-033"] = "closed",
                ["M01-034"] = "closed",
                ["M01-035"] = "closed"
            };
        IReadOnlyDictionary<string, string> agentLabels =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["M01-033"] = "agent:done",
                ["M01-034"] = "agent:done",
                ["M01-035"] = "agent:done"
            };

        foreach (string key in keys)
        {
            JsonElement issue = issueByKey[key];
            JsonElement labels = issue.GetProperty("labels");
            string commands = JsonArrayText(issue.GetProperty("requiredCommands"));
            string? expectedLane = lanes[key];
            bool laneContractSatisfied = expectedLane is null
                ? issue.GetProperty("collisionLane").ValueKind ==
                    JsonValueKind.Null &&
                    CountPrefix(labels, "lane:") == 0
                : string.Equals(
                        issue.GetProperty("collisionLane").GetString(),
                        expectedLane,
                        StringComparison.Ordinal) &&
                    JsonArrayContainsExact(labels, expectedLane) &&
                    CountPrefix(labels, "lane:") == 1;
            if (!string.Equals(
                    issue.GetProperty("desiredState").GetString(),
                    desiredStates[key],
                    StringComparison.Ordinal) ||
                !JsonArrayContainsExact(labels, agentLabels[key]) ||
                CountPrefix(labels, "agent:") != 1 ||
                !JsonArrayContainsExact(labels, "priority:p0") ||
                !JsonArrayContainsExact(labels, "evidence:host") ||
                !JsonArrayContainsExact(labels, "phase:m01-memory") ||
                !JsonArrayContainsExact(labels, types[key]) ||
                CountPrefix(labels, "type:") != 1 ||
                !laneContractSatisfied ||
                commands.Contains("qemu smoke", StringComparison.Ordinal) ||
                commands.Contains(" -Qemu \"", StringComparison.Ordinal) ||
                commands.Contains(" -OvmfCode \"", StringComparison.Ordinal))
            {
                return HostCheck.Result(false);
            }
        }

        JsonElement generator = issueByKey["M01-033"];
        JsonElement integration = issueByKey["M01-034"];
        JsonElement publication = issueByKey["M01-035"];
        string generatorEvidence =
            generator.GetProperty("currentEvidence").GetString() ?? string.Empty;
        string integrationEvidence =
            integration.GetProperty("currentEvidence").GetString() ?? string.Empty;
        string publicationEvidence =
            publication.GetProperty("currentEvidence").GetString() ?? string.Empty;
        string[] requiredGeneratorEvidenceTerms =
        [
            "PR #416",
            "b3b1dc892205509bef56bde6c4f9c25cb8f02726",
            "DA97E50C95E479541B65EB3F96D9C6DED163735857650BEE9416324086D55C7B",
            "done/step884.md",
            "comment #6336",
            "clros-evidence-reviewed:M01-033",
            "deterministic object-only host evidence"
        ];
        string[] requiredIntegrationEvidenceTerms =
        [
            "eb06fda1c9146436a3025b4b4da093195551579d",
            "PR #418",
            "5226b2a33809a487bfc1ebfc3b142fc43bfb2ee8",
            "DA97E50C95E479541B65EB3F96D9C6DED163735857650BEE9416324086D55C7B",
            "9A886C748EE8F6FFB5FF87005F5CD7EE1B6C19911186F52A93FEA433B427C37B",
            "D40FCD847855EC59971FA9E1395A23B0E3804D2079973E3D1DFEC6388BC2A302",
            "done/step886.md",
            "comment #6366",
            "clros-evidence-reviewed:M01-034",
            "static host/artifact evidence only"
        ];
        string[] requiredPublicationEvidenceTerms =
        [
            "6d43176fa6602dd4a3bdd87179c66e3982d43967",
            "PR #420",
            "a008e84fb30e8a88471fdfa499d8b82cbce98751",
            "a32b4cbe695b704919dfd9dcdfd0ba49dcee9470",
            "done/step887.md",
            "comment #6388",
            "clros-evidence-reviewed:M01-035",
            "6E8508D43357A05A3391A8A80578D114D71B6C6521B109AF03EA3E460B6F0742",
            "76F2B404E91EB5E62CAD80B797B936BA3D3D39D5B0577889BB8459AF1B55C47C",
            "DF76C8E39E0768D26EA27206FF102D7EADCC35D2C9BDF547B52B1171FD47BDAF",
            "0 -> 1 -> 3 -> 7 -> 15",
            "static source-order, host, and fresh-build evidence only"
        ];
        if (!OwnedPathsEqual(
                generator,
                "tools/OS.X64TextSerializerGenerator/OS.X64TextSerializerGenerator.csproj",
                "tools/OS.X64TextSerializerGenerator/Program.cs",
                "tools/OS.X64TextSerializerGenerator/Amd64CoffWriter.cs",
                "tools/OS.X64TextSerializerGenerator/InstructionPublicationSerializerGenerator.cs",
                "tools/AGENTS.override.md",
                "tests/OS.HostTests/M01InstructionPublicationSerializerGeneratorTests.cs",
                "tests/OS.HostTests/HostToolProjectBoundaryTests.cs",
                "docs/security.md",
                "docs/qemu.md",
                "docs/libvirt.md") ||
            !OwnedPathsEqual(
                integration,
                "CLROS.slnx",
                "src/OS/OS.csproj",
                "src/OS/Boot/X64InstructionPublication.cs",
                "src/OS/Runtime/RuntimeImportAttribute.cs",
                "src/OS/Runtime/MethodImplAttribute.cs",
                "tools/OS.X64TextSerializerGenerator/OS.X64TextSerializerGenerator.targets",
                "tools/AGENTS.override.md",
                "tools/OS.Tooling/EfiBuild.cs",
                "tools/OS.Tooling/EfiVerify.cs",
                "tests/OS.HostTests/M01InstructionPublicationBuildIntegrationTests.cs",
                "tests/OS.HostTests/BuildEfiShellPreflightTests.cs",
                "tests/OS.HostTests/EfiArtifactVerificationScriptTests.cs",
                "docs/efi-build.md",
                "docs/nativeaot-nostdlib-limits.md",
                "docs/boot.md",
                "docs/security.md") ||
            !OwnedPathsEqual(
                publication,
                "src/OS/Boot/EfiEntry.cs",
                "tests/OS.HostTests/M01InstructionPublicationEfiEntryTests.cs",
                "docs/boot.md",
                "docs/architecture.md",
                "docs/security.md") ||
            requiredGeneratorEvidenceTerms.Any(term =>
                !generatorEvidence.Contains(term, StringComparison.Ordinal)) ||
            requiredIntegrationEvidenceTerms.Any(term =>
                !integrationEvidence.Contains(term, StringComparison.Ordinal)) ||
            requiredPublicationEvidenceTerms.Any(term =>
                !publicationEvidence.Contains(term, StringComparison.Ordinal)))
        {
            return HostCheck.Result(false);
        }

        if (!JsonElementContains(
                generator.GetProperty("forbiddenPaths"),
                "CLROS.slnx") ||
            !JsonElementContains(
                generator.GetProperty("forbiddenPaths"),
                "src/OS/OS.csproj") ||
            !JsonElementContains(
                generator.GetProperty("forbiddenPaths"),
                "src/OS/Boot/EfiEntry.cs") ||
            !JsonElementContains(
                generator.GetProperty("forbiddenPaths"),
                "src/OS/Runtime/MethodImplAttribute.cs") ||
            !JsonElementContains(
                integration.GetProperty("forbiddenPaths"),
                "src/OS/Boot/EfiEntry.cs") ||
            !JsonElementContains(
                integration.GetProperty("forbiddenPaths"),
                "InstructionPublicationSerializerGenerator.cs") ||
            JsonElementContains(
                integration.GetProperty("forbiddenPaths"),
                "tools/OS.Tooling/EfiBuild.cs") ||
            JsonElementContains(
                integration.GetProperty("forbiddenPaths"),
                "docs/security.md") ||
            JsonElementContains(
                integration.GetProperty("forbiddenPaths"),
                "docs/boot.md") ||
            !JsonElementContains(
                publication.GetProperty("forbiddenPaths"),
                "src/OS/OS.csproj") ||
            !JsonElementContains(
                publication.GetProperty("forbiddenPaths"),
                "src/OS/Runtime/MethodImplAttribute.cs") ||
            !JsonElementContains(
                publication.GetProperty("forbiddenPaths"),
                "tools/OS.X64TextSerializerGenerator/**") ||
            !JsonElementContains(
                publication.GetProperty("forbiddenPaths"),
                "tools/OS.Tooling/EfiVerify.cs"))
        {
            return HostCheck.Result(false);
        }

        string generatorContract =
            generator.GetProperty("summary").GetString() + "\n" +
            generator.GetProperty("currentEvidence").GetString() + "\n" +
            JsonArrayText(generator.GetProperty("implementationNotes")) + "\n" +
            JsonArrayText(generator.GetProperty("acceptanceCriteria")) + "\n" +
            generator.GetProperty("evidenceBoundary").GetString();
        string integrationContract =
            integration.GetProperty("summary").GetString() + "\n" +
            integration.GetProperty("currentEvidence").GetString() + "\n" +
            JsonArrayText(integration.GetProperty("implementationNotes")) + "\n" +
            JsonArrayText(integration.GetProperty("acceptanceCriteria")) + "\n" +
            integration.GetProperty("evidenceBoundary").GetString();
        string publicationContract =
            publication.GetProperty("summary").GetString() + "\n" +
            publication.GetProperty("currentEvidence").GetString() + "\n" +
            JsonArrayText(publication.GetProperty("implementationNotes")) + "\n" +
            JsonArrayText(publication.GetProperty("acceptanceCriteria")) + "\n" +
            publication.GetProperty("evidenceBoundary").GetString();
        string exactBytes =
            "53 B8 00 00 00 00 B9 00 00 00 00 0F A2 B8 43 4C 52 53 5B C3";
        string[] generatorTerms =
        [
            "AMD64 COFF machine 0x8664",
            ".text$clrossm",
            "CLROS_X64_SerializeInstructionPublication",
            exactBytes,
            "timestamp zero",
            "IMAGE_SCN_MEM_WRITE clear",
            "IMAGE_SCN_ALIGN_16BYTES",
            "raw size 20",
            ".xdata$clrossm",
            ".pdata$clrossm",
            "raw size 8",
            "raw size 12",
            "IMAGE_SCN_CNT_INITIALIZED_DATA",
            "IMAGE_SCN_ALIGN_4BYTES",
            "COFF function type 0x20",
            "static storage class",
            "IMAGE_REL_AMD64_ADDR32NB",
            "UWOP_PUSH_NONVOL RBX",
            "01 01 01 00 01 30 00 00",
            ".pdata offsets 0 and 4",
            "at offset 8",
            "static value-zero .xdata$clrossm base symbol",
            "0x53524C43",
            "RFLAGS",
            "integer overflow",
            "generator timeout",
            "No .obj"
        ];
        string[] integrationTerms =
        [
            "CLROS.slnx",
            "NativeLibrary",
            "[RuntimeImport(\"*\", \"CLROS_X64_SerializeInstructionPublication\")]",
            "[MethodImpl(MethodImplOptions.InternalCall)]",
            "internal static extern uint Serialize()",
            "InternalCall = 0x1000",
            "0x53524C43",
            "/INCLUDE:CLROS_X64_SerializeInstructionPublication",
            "same-build map",
            "data-directory index 3",
            "NumberOfRvaAndSizes",
            "DWORD-aligned exception-directory RVA",
            "file-backed",
            "RX/nonwritable",
            "RUNTIME_FUNCTION",
            "UNWIND_INFO",
            "exactly divisible by the 12-byte RUNTIME_FUNCTION",
            "strictly ascending BeginAddress values",
            "BeginAddress equal the same-build symbol RVA",
            "EndAddress equal BeginAddress + 20",
            "UnwindInfoAddress",
            "within SizeOfImage",
            "no other record beginning in, ending in, enclosing, duplicating",
            "MethodImplAttributes.InternalCall set",
            "PInvokeImpl clear",
            "no IL body",
            exactBytes,
            "absent import",
            "must not invoke",
            "build timeout",
            "exact 44 ordered compile items",
            "exactly one allowlisted serializer-targets import",
            "reject every other import",
            "no serializer-execution"
        ];
        string[] publicationTerms =
        [
            "exactly four checked fully qualified serializer calls",
            "Epoch 1",
            "Epoch 2",
            "Epoch 3",
            "Epoch 4",
            "CaptureArchitecturalPreflight",
            "After the PAT-reader readback loop",
            "both entry bindings",
            "preflight(observation)",
            "readPat()",
            "ordinary caller local is forbidden",
            "one-way stage-one stack switch",
            "0 -> 1 -> 3 -> 7 -> 15",
            "M01_PRE_CR3",
            "0x53524C43",
            "mask is exactly 0xF",
            "M01_INSTRUCTION_PUBLICATION_COOKIE_FAILED",
            "M01_INSTRUCTION_PUBLICATION_EPOCHS_VERIFIED",
            "No executable-page write",
            "LFENCE",
            "MFENCE",
            "firmware callback",
            "zero/default",
            "overflow",
            "timeout",
            "qemu-system-x86_64 --version",
            "qemu-img --version",
            "no QEMU smoke, guest boot, or QEMU dry-run smoke workflow",
            "cross-core"
        ];
        if (generatorTerms.Any(term =>
                !generatorContract.Contains(term, StringComparison.Ordinal)) ||
            integrationTerms.Any(term =>
                !integrationContract.Contains(term, StringComparison.Ordinal)) ||
            publicationTerms.Any(term =>
                !publicationContract.Contains(term, StringComparison.Ordinal)))
        {
            return HostCheck.Result(false);
        }

        if (!ReferencePathExists(
                generator,
                "references/SharpOS-main/bootasm/CoffStub.Generator/CoffWriter.cs") ||
            !ReferencePathExists(
                generator,
                "references/SharpOS-main/bootasm/CoffStub.Generator/EmitCoffStubsTask.cs") ||
            !ReferencePathExists(
                integration,
                "references/SharpOS-main/bootasm/CoffStub.Generator/CoffStub.Generator.targets") ||
            !ReferencePathExists(
                integration,
                "references/SharpOS-main/apps_native/sdk/MinimalRuntime.cs") ||
            !ReferencePathExists(
                publication,
                "references/nativeaot-patcher-main/src/Cosmos.Kernel.Native.X64/CPU/CpuOps.s") ||
            !JsonArrayContainsExact(
                generator.GetProperty("labels"),
                "reference:adapt") ||
            !JsonArrayContainsExact(
                integration.GetProperty("labels"),
                "reference:adapt") ||
            !JsonArrayContainsExact(
                publication.GetProperty("labels"),
                "reference:test-only"))
        {
            return HostCheck.Result(false);
        }

        string epicContract =
            JsonArrayText(
                issueByKey["M01-EPIC"].GetProperty("implementationNotes")) +
            "\n" +
            JsonArrayText(
                issueByKey["M01-EPIC"].GetProperty("acceptanceCriteria"));
        string qemuContract =
            issueByKey["M01-026"].GetProperty("currentEvidence").GetString() +
            "\n" +
            JsonArrayText(
                issueByKey["M01-026"].GetProperty("implementationNotes")) +
            "\n" +
            JsonArrayText(
                issueByKey["M01-026"].GetProperty("acceptanceCriteria")) +
            "\n" +
            issueByKey["M01-026"].GetProperty("evidenceBoundary").GetString();
        return HostCheck.Result(epicContract.Contains(
                "M01-033 generates one deterministic C# AMD64 COFF serializer",
                StringComparison.Ordinal)) &&
            HostCheck.Result(epicContract.Contains(
                "All 36 keyed child issues",
                StringComparison.Ordinal)) &&
            HostCheck.Result(qemuContract.Contains(
                "Accepted implementation commit 8f1edea17f2de425520e239af58d37aa3b182146",
                StringComparison.Ordinal)) &&
            HostCheck.Result(qemuContract.Contains(
                "exactly four checked publication-epoch calls",
                StringComparison.Ordinal)) &&
            HostCheck.Result(qemuContract.Contains(
                "runtime-observable aggregate success marker",
                StringComparison.Ordinal)) &&
            HostCheck.Result(qemuContract.Contains(
                "M01_INSTRUCTION_PUBLICATION_COOKIE_FAILED",
                StringComparison.Ordinal)) &&
            HostCheck.Result(qemuContract.Contains(
                "M01_INSTRUCTION_PUBLICATION_EPOCHS_VERIFIED",
                StringComparison.Ordinal)) &&
            HostCheck.Result(qemuContract.Contains(
                "QEMU/TCG",
                StringComparison.Ordinal)) &&
            HostCheck.Result(qemuContract.Contains(
                "does not empirically prove absence of speculative code-store bypass",
                StringComparison.Ordinal)) &&
            HostCheck.Result(qemuContract.Contains(
                "does not prove cross-core instruction publication",
                StringComparison.Ordinal)) &&
            HostCheck.Result(JsonElementContains(
                integration.GetProperty("requiredCommands"),
                "efi build")) &&
            HostCheck.Result(JsonElementContains(
                publication.GetProperty("requiredCommands"),
                "efi build")) &&
            HostCheck.Result(JsonElementContains(
                integration.GetProperty("requiredCommands"),
                "efi verify")) &&
            HostCheck.Result(JsonElementContains(
                integration.GetProperty("requiredCommands"),
                "-EfiArtifact .\\artifacts\\efi\\BOOTX64.EFI")) &&
            HostCheck.Result(JsonElementContains(
                publication.GetProperty("requiredCommands"),
                "efi verify")) &&
            HostCheck.Result(JsonElementContains(
                publication.GetProperty("requiredCommands"),
                "-EfiArtifact .\\artifacts\\efi\\BOOTX64.EFI")) &&
            HostCheck.Result(JsonElementContains(
                issueByKey["M01-026"].GetProperty("requiredCommands"),
                "M01_INSTRUCTION_PUBLICATION_COOKIE_FAILED")) &&
            HostCheck.Result(JsonElementContains(
                issueByKey["M01-026"].GetProperty("requiredCommands"),
                "M01_INSTRUCTION_PUBLICATION_EPOCHS_VERIFIED"));
    }

    private static bool ValidateM01DurabilityRepairContracts(
        IReadOnlyDictionary<string, JsonElement> issueByKey)
    {
        const string M02001Marker =
            "CLROS_M02_001_READY strategy=ExistingAuditedBytePlans families=1 generatorThreshold=2 generatorEligible=false out8Bytes=7 in8Bytes=5 writeThenExecute=true permanentRwx=false";
        const string M02002Marker =
            "CLROS_M02_002_READY pageBytes=4096 safeOffset=0 out8Offset=16 in8Offset=32 fnv1a64=F85393074A792B1E sha256=F8DAF2F093F6FCD3CB6CA764DCC294EC9B4DBF2B392D222946F11250BFCE353D initial=RW+NX final=RO+X liveProtection=false liveExecution=false";
        const string M02017Marker =
            "CLROS_M02_017_READY pageBytes=4096 safeOffset=0 out8Offset=16 in8Offset=32 fnv1a64=F85393074A792B1E temporaryOwnedRootRx=true portIoExecuted=false";
        const string M04001Marker =
            "CLROS_M04_001_READY declarations=40 runtimeExports=17 linkOnlyNoOp=4 failFast=12 auditedStackProbe=1 gate4=Fail canonicalSystemNamespace=false";

        JsonElement epic = issueByKey["M01-EPIC"];
        JsonElement exit = issueByKey["M01-026"];
        JsonElement repair = issueByKey["M01-036"];
        JsonElement strategy = issueByKey["M02-001"];
        JsonElement materializer = issueByKey["M02-002"];
        JsonElement safeIntegration = issueByKey["M02-017"];
        JsonElement runtimeLedger = issueByKey["M04-001"];

        string exitEvidence =
            exit.GetProperty("currentEvidence").GetString() ?? string.Empty;
        string[] exitAnchors =
        [
            "8f1edea17f2de425520e239af58d37aa3b182146",
            "6b5359e36293d770e3dc0d074b1baabf8d4fdbf8",
            "26219cc5327e9b32935f4e5b06fe5eb014cca93c",
            "6aaf046362d0a3187f551ee06f1456d05ce5021f",
            "PR #422",
            "Issue #333",
            "comment #6472",
            "comment #6477",
            "94A28FE683FC068C88F42AB806B3E9A06DB5467FD35488B6ADF397AC8F8A8188",
            "B686F7C9C69CE8A9FB8731A4C8C2DF3807006A9993AB2349B763CE556E05DAA1",
            "996E343DC72C34B6F2521C1E703A58BB98362242A3E894D48647057F139F13F3",
            "CC51100244A8A20865DB9D16CE5D73B0930F43EA98835DFA57027A00B126C661",
            "B8451020C2355B061E83DB090EFFD62A965A95AD73D11D7F9864477753582339",
            "6FFEDB0B5998A930E614E3EC0EE68E2B444439AE686A9412F3206760D257F933",
            "76F2B404E91EB5E62CAD80B797B936BA3D3D39D5B0577889BB8459AF1B55C47C",
            "done/step890.md"
        ];
        if (!string.Equals(
                epic.GetProperty("desiredState").GetString(),
                "closed",
                StringComparison.Ordinal) ||
            !JsonArrayContainsExact(epic.GetProperty("labels"), "agent:done") ||
            CountPrefix(epic.GetProperty("labels"), "agent:") != 1 ||
            !JsonElementContains(
                epic.GetProperty("acceptanceCriteria"),
                "All 36 keyed child issues") ||
            !string.Equals(
                exit.GetProperty("desiredState").GetString(),
                "closed",
                StringComparison.Ordinal) ||
            !JsonArrayContainsExact(exit.GetProperty("labels"), "agent:done") ||
            CountPrefix(exit.GetProperty("labels"), "agent:") != 1 ||
            !DependenciesEqual(exit, "M01-035") ||
            exitAnchors.Any(anchor =>
                !exitEvidence.Contains(anchor, StringComparison.Ordinal)) ||
            !DependenciesEqual(repair, "M01-026") ||
            !string.Equals(
                repair.GetProperty("desiredState").GetString(),
                "closed",
                StringComparison.Ordinal) ||
            !JsonArrayContainsExact(repair.GetProperty("labels"), "agent:done") ||
            !JsonArrayContainsExact(repair.GetProperty("labels"), "type:tooling") ||
            !JsonArrayContainsExact(repair.GetProperty("labels"), "evidence:host") ||
            !JsonArrayContainsExact(repair.GetProperty("labels"), "component:repo") ||
            !JsonArrayContainsExact(repair.GetProperty("labels"), "phase:m01-memory") ||
            !JsonArrayContainsExact(repair.GetProperty("labels"), "lane:central-docs") ||
            CountPrefix(repair.GetProperty("labels"), "agent:") != 1 ||
            CountPrefix(repair.GetProperty("labels"), "type:") != 1 ||
            CountPrefix(repair.GetProperty("labels"), "evidence:") != 1 ||
            CountPrefix(repair.GetProperty("labels"), "lane:") != 1 ||
            !string.Equals(
                repair.GetProperty("collisionLane").GetString(),
                "lane:central-docs",
                StringComparison.Ordinal) ||
            !OwnedPathsEqual(
                repair,
                ".gitea/backlog/clros-issues.json",
                "tools/OS.Tooling/BacklogSync.cs",
                "tests/OS.HostTests/GiteaBacklogManifestTests.cs",
                "tests/OS.HostTests/RepositoryRecoveryTests.cs",
                "tests/OS.HostTests/M01LivePagingPrerequisiteManifestTests.cs",
                "docs/issue-labels.md",
                "docs/repo-audit.md") ||
            !JsonStringArrayEquals(
                repair.GetProperty("forbiddenPaths"),
                "src/**",
                "std/**",
                "apps/**",
                "src/OS/Boot/EfiEntry.cs",
                "src/OS/OS.csproj",
                "src/OS.Kernel/Boot/BootSequence.cs",
                "src/OS.Kernel/Boot/BootSequencePlan.cs",
                "src/OS.Kernel/Diagnostics/Probes.cs",
                "tools/OS.Tooling/EfiBuild.cs",
                "tools/OS.Tooling/QemuSmoke.cs",
                "tests/OS.HostTests/QemuRunnerTests.cs",
                "README.md",
                "references/** (read-only)",
                "done/** (reserve one new step only after implementation evidence exists)") ||
            !JsonStringArrayEquals(
                repair.GetProperty("requiredMarkers"),
                "TEST_START m01.036",
                M01DurabilityRepairMarker,
                "TEST_PASS m01.036",
                "TEST_FAIL m01.036 <reason>") ||
            !JsonElementContains(
                repair.GetProperty("implementationNotes"),
                "issue #425") ||
            !JsonElementContains(
                repair.GetProperty("implementationNotes"),
                "create only M02-017"))
        {
            return HostCheck.Result(false);
        }

        string repairCommands = JsonArrayText(
            repair.GetProperty("requiredCommands"));
        if (repairCommands.Contains(" -Qemu \"", StringComparison.Ordinal) ||
            repairCommands.Contains(" -OvmfCode \"", StringComparison.Ordinal) ||
            !repairCommands.Contains("backlog sync -SelfTest", StringComparison.Ordinal) ||
            !repairCommands.Contains("backlog sync -Offline", StringComparison.Ordinal) ||
            !repairCommands.Contains("qemu smoke -DryRun", StringComparison.Ordinal) ||
            !repairCommands.Contains("efi build", StringComparison.Ordinal))
        {
            return HostCheck.Result(false);
        }

        if (!ValidateHostProductionLeaf(
                strategy,
                "M01-036",
                M02001Marker,
                [
                    "src/OS.Kernel/Diagnostics/ManagedX64StubStrategyADRUseExistingAudited.cs",
                    "tests/OS.HostTests/ManagedX64StubStrategyADRUseExistingAuditedTests.cs",
                    "docs/diagnostics.md",
                    "docs/boot-order.md"
                ],
                "agent:done",
                "closed") ||
            !ValidateHostProductionLeaf(
                materializer,
                "M02-018",
                M02002Marker,
                [
                    "src/OS.Kernel/Diagnostics/PortIoOperationPlan.cs",
                    "src/OS.Kernel/Diagnostics/PortIoStubPlan.cs",
                    "src/OS.Kernel/Diagnostics/ExistingIn8Out8StubBytesRWPagesFinalize.cs",
                    "tests/OS.HostTests/ExistingIn8Out8StubBytesRWPagesFinalizeTests.cs",
                    "docs/diagnostics.md",
                    "docs/boot-order.md"
                ],
                "agent:done",
                "closed") ||
            !ValidateHostProductionLeaf(
                runtimeLedger,
                "M01-036",
                M04001Marker,
                [
                    "std/no-runtime/shared/DotNetN10SystemExportCompatibilityLedgerImplementedStubbed.cs",
                    "tests/OS.HostTests/DotNetN10SystemExportCompatibilityLedgerImplementedStubbedTests.cs",
                    "docs/nativeaot-nostdlib-limits.md",
                    "docs/completion-gates.md"
                ],
                "agent:done",
                "closed"))
        {
            return HostCheck.Result(false);
        }

        string strategyContract =
            JsonArrayText(strategy.GetProperty("implementationNotes")) +
            "\n" +
            JsonArrayText(strategy.GetProperty("acceptanceCriteria")) +
            "\n" +
            strategy.GetProperty("evidenceBoundary").GetString();
        string materializerContract =
            JsonArrayText(materializer.GetProperty("implementationNotes")) +
            "\n" +
            JsonArrayText(materializer.GetProperty("acceptanceCriteria")) +
            "\n" +
            materializer.GetProperty("evidenceBoundary").GetString();
        string ledgerContract =
            JsonArrayText(runtimeLedger.GetProperty("implementationNotes")) +
            "\n" +
            JsonArrayText(runtimeLedger.GetProperty("acceptanceCriteria")) +
            "\n" +
            runtimeLedger.GetProperty("evidenceBoundary").GetString();
        if (!strategyContract.Contains("66 89 CA 88 D0 EE C3", StringComparison.Ordinal) ||
            !strategyContract.Contains("66 89 CA EC C3", StringComparison.Ordinal) ||
            !strategyContract.Contains("generatorThreshold=2", StringComparison.Ordinal) ||
            !strategyContract.Contains("permanentRwx=false", StringComparison.Ordinal) ||
            !materializerContract.Contains("MissingDestination = 3", StringComparison.Ordinal) ||
            !materializerContract.Contains("F85393074A792B1E", StringComparison.Ordinal) ||
            !materializerContract.Contains(
                "F8DAF2F093F6FCD3CB6CA764DCC294EC9B4DBF2B392D222946F11250BFCE353D",
                StringComparison.Ordinal) ||
            !materializerContract.Contains("liveExecution=false", StringComparison.Ordinal) ||
            !ledgerContract.Contains("OS.Std.*", StringComparison.Ordinal) ||
            !ledgerContract.Contains("40 MinimalRuntime declarations", StringComparison.Ordinal) ||
            !ledgerContract.Contains("17 RuntimeExports", StringComparison.Ordinal) ||
            !ledgerContract.Contains("four link-only no-op", StringComparison.Ordinal) ||
            !ledgerContract.Contains("twelve fail-fast", StringComparison.Ordinal) ||
            !ledgerContract.Contains("one audited stack-probe", StringComparison.Ordinal) ||
            !ledgerContract.Contains("Gate 4 failing", StringComparison.Ordinal))
        {
            return HostCheck.Result(false);
        }

        string safeContract =
            JsonArrayText(safeIntegration.GetProperty("implementationNotes")) +
            "\n" +
            JsonArrayText(safeIntegration.GetProperty("acceptanceCriteria")) +
            "\n" +
            safeIntegration.GetProperty("evidenceBoundary").GetString();
        string safeCommands = JsonArrayText(
            safeIntegration.GetProperty("requiredCommands"));
        if (!DependenciesEqual(safeIntegration, "M02-019") ||
            !JsonArrayContainsExact(
                safeIntegration.GetProperty("labels"),
                "type:implementation") ||
            !JsonArrayContainsExact(
                safeIntegration.GetProperty("labels"),
                "evidence:qemu") ||
            !JsonArrayContainsExact(
                safeIntegration.GetProperty("labels"),
                "lane:efi-entry") ||
            !string.Equals(
                safeIntegration.GetProperty("collisionLane").GetString(),
                "lane:efi-entry",
                StringComparison.Ordinal) ||
            !OwnedPathsEqual(
                safeIntegration,
                "src/OS/OS.csproj",
                "src/OS/Boot/EfiEntry.cs",
                "src/OS/Boot/M02SafeStubPageIntegration.cs",
                "tools/OS.Tooling/EfiBuild.cs",
                "tools/OS.Tooling/QemuSmoke.cs",
                "tests/OS.HostTests/M02SafeStubPageEfiEntryTests.cs",
                "tests/OS.HostTests/BuildEfiShellPreflightTests.cs",
                "tests/OS.HostTests/QemuRunnerTests.cs",
                "docs/diagnostics.md",
                "docs/boot.md",
                "docs/boot-order.md",
                "docs/efi-build.md",
                "docs/qemu.md",
                "docs/completion-gates.md") ||
            !JsonElementContains(
                safeIntegration.GetProperty("requiredMarkers"),
                M02017Marker) ||
            !JsonElementContains(
                safeIntegration.GetProperty("requiredMarkers"),
                "M02_PRIVILEGED_STUB_TARGETED") ||
            !safeContract.Contains("Never call, jump to, or otherwise target offsets 16 or 32", StringComparison.Ordinal) ||
            !safeContract.Contains("Execute only entry offset 0", StringComparison.Ordinal) ||
            !safeContract.Contains("0x4F534F4B", StringComparison.Ordinal) ||
            !safeContract.Contains("all 42 accepted M01", StringComparison.OrdinalIgnoreCase) ||
            !safeContract.Contains("all 25 accepted M01 forbidden", StringComparison.OrdinalIgnoreCase) ||
            !safeCommands.Contains("-EnableM02SafeStubProbe", StringComparison.Ordinal) ||
            !safeCommands.Contains("-EnableM01LoadedImageStackProbe", StringComparison.Ordinal) ||
            !safeCommands.Contains("-EnableM01LiveAllocatorProbe", StringComparison.Ordinal) ||
            !safeCommands.Contains("-EnableM01OwnedPagingProbe", StringComparison.Ordinal) ||
            CountExact(
                safeIntegration.GetProperty("requiredCommands"),
                "dotnet run --project tools/OS.Tooling -- qemu smoke -BuildEfi -EnableM01LoadedImageStackProbe -EnableM01LiveAllocatorProbe -EnableM01OwnedPagingProbe -EnableM02SafeStubProbe -Qemu \"C:\\Program Files\\qemu\\qemu-system-x86_64.exe\" -OvmfCode \"C:\\Program Files\\qemu\\share\\edk2-x86_64-code.fd\" -TimeoutSeconds 30") != 1 ||
            !DependenciesEqual(issueByKey["M02-003"], "M02-020") ||
            !DependenciesEqual(issueByKey["M02-EPIC"], "M01-036") ||
            !DependenciesEqual(issueByKey["M04-EPIC"], "M01-036") ||
            !DependenciesEqual(
                issueByKey["M03-007"],
                "M01-026",
                "M02-016",
                "M03-006"))
        {
            return HostCheck.Result(false);
        }

        return true;

        static bool ValidateHostProductionLeaf(
            JsonElement issue,
            string dependency,
            string readyMarker,
            string[] ownedPaths,
            string agentState = "agent:blocked",
            string desiredState = "open")
        {
            JsonElement labels = issue.GetProperty("labels");
            string commands = JsonArrayText(issue.GetProperty("requiredCommands"));
            return DependenciesEqual(issue, dependency) &&
                JsonArrayContainsExact(labels, agentState) &&
                JsonArrayContainsExact(labels, "type:implementation") &&
                JsonArrayContainsExact(labels, "evidence:host") &&
                JsonArrayContainsExact(labels, "lane:central-docs") &&
                !JsonArrayContainsExact(labels, "evidence:qemu") &&
                CountPrefix(labels, "agent:") == 1 &&
                CountPrefix(labels, "type:") == 1 &&
                CountPrefix(labels, "evidence:") == 1 &&
                CountPrefix(labels, "lane:") == 1 &&
                string.Equals(
                    issue.GetProperty("collisionLane").GetString(),
                    "lane:central-docs",
                    StringComparison.Ordinal) &&
                string.Equals(
                    issue.GetProperty("desiredState").GetString(),
                    desiredState,
                    StringComparison.Ordinal) &&
                OwnedPathsEqual(issue, ownedPaths) &&
                JsonElementContains(
                    issue.GetProperty("requiredMarkers"),
                    readyMarker) &&
                commands.Contains("qemu smoke -DryRun", StringComparison.Ordinal) &&
                commands.Contains("efi build", StringComparison.Ordinal) &&
                !commands.Contains(" -Qemu \"", StringComparison.Ordinal) &&
                !commands.Contains(" -OvmfCode \"", StringComparison.Ordinal);
        }
    }

    private static bool ValidateM02DurableClosureContracts(
        IReadOnlyDictionary<string, JsonElement> issueByKey)
    {
        string[] requiredKeys =
        [
            "M02-EPIC",
            "M02-001",
            "M02-002",
            "M02-003",
            "M02-017",
            "M02-018"
        ];
        if (requiredKeys.Any(key => !issueByKey.ContainsKey(key)))
        {
            return HostCheck.Result(false);
        }

        JsonElement epic = issueByKey["M02-EPIC"];
        JsonElement strategy = issueByKey["M02-001"];
        JsonElement closure = issueByKey["M02-018"];
        JsonElement materializer = issueByKey["M02-002"];
        JsonElement safeIntegration = issueByKey["M02-017"];
        JsonElement livePortIo = issueByKey["M02-003"];
        JsonElement strategyLabels = strategy.GetProperty("labels");
        JsonElement closureLabels = closure.GetProperty("labels");
        string strategyEvidence =
            strategy.GetProperty("currentEvidence").GetString()
            ?? string.Empty;
        string closureEvidence =
            closure.GetProperty("currentEvidence").GetString()
            ?? string.Empty;
        string closureNotes =
            JsonArrayText(closure.GetProperty("implementationNotes"));
        string closureAcceptance =
            JsonArrayText(closure.GetProperty("acceptanceCriteria"));
        string closureCommands =
            JsonArrayText(closure.GetProperty("requiredCommands"));
        string closureBoundary =
            closure.GetProperty("evidenceBoundary").GetString()
            ?? string.Empty;
        string[] strategyEvidenceAnchors =
        [
            "acec50b47056e7c538ca5225d32898ff2c0bc93f",
            "ac236ce5acb9a88485b15e12f309ec2457d2e758",
            "a97d25107d9cd573b140a6a524a192c8fd77b68b",
            "d5bc7a406484cd8aa08b9cdfd7ed3cbc5dbef0da",
            "5a4234a5b6e0fadcd1a4db9a01374340723ba4e8",
            "85528a5513f1cf24396ff29acc6787bc015ec9c5",
            "PR #428",
            "issue #41",
            "comment #6578",
            "comment #6584",
            "done/step892.md",
            "CDBDF0A7F980F554854931291165AF4073FED1F6C3A2B30C949C6702C10BF5C4",
            "native HostTests exit 0",
            "3,336 TEST_START",
            "3,337 TEST_PASS",
            "zero TEST_FAIL",
            "realQemuRun=false"
        ];

        return HostCheck.Result(DependenciesEqual(strategy, "M01-036")) &&
            HostCheck.Result(string.Equals(
                strategy.GetProperty("desiredState").GetString(),
                "closed",
                StringComparison.Ordinal)) &&
            HostCheck.Result(JsonArrayContainsExact(strategyLabels, "agent:done")) &&
            HostCheck.Result(!JsonArrayContainsExact(strategyLabels, "agent:blocked")) &&
            HostCheck.Result(CountPrefix(strategyLabels, "agent:") == 1) &&
            HostCheck.Result(strategyEvidenceAnchors.All(anchor =>
                strategyEvidence.Contains(anchor, StringComparison.Ordinal))) &&
            HostCheck.Result(DependenciesEqual(closure, "M02-001")) &&
            HostCheck.Result(string.Equals(
                closure.GetProperty("desiredState").GetString(),
                "closed",
                StringComparison.Ordinal)) &&
            HostCheck.Result(string.Equals(
                closure.GetProperty("collisionLane").GetString(),
                "lane:solution-baseline",
                StringComparison.Ordinal)) &&
            HostCheck.Result(JsonArrayContainsExact(closureLabels, "agent:done")) &&
            HostCheck.Result(JsonArrayContainsExact(closureLabels, "priority:p0")) &&
            HostCheck.Result(JsonArrayContainsExact(closureLabels, "type:tooling")) &&
            HostCheck.Result(JsonArrayContainsExact(closureLabels, "evidence:host")) &&
            HostCheck.Result(JsonArrayContainsExact(closureLabels, "component:repo")) &&
            HostCheck.Result(JsonArrayContainsExact(closureLabels, "phase:m02-diagnostics")) &&
            HostCheck.Result(JsonArrayContainsExact(
                closureLabels,
                "lane:solution-baseline")) &&
            HostCheck.Result(CountPrefix(closureLabels, "agent:") == 1) &&
            HostCheck.Result(CountPrefix(closureLabels, "type:") == 1) &&
            HostCheck.Result(CountPrefix(closureLabels, "evidence:") == 1) &&
            HostCheck.Result(CountPrefix(closureLabels, "lane:") == 1) &&
            HostCheck.Result(OwnedPathsEqual(
                closure,
                ".gitea/backlog/clros-issues.json",
                "tools/OS.Tooling/BacklogSync.cs",
                "tests/OS.HostTests/GiteaBacklogManifestTests.cs",
                "tests/OS.HostTests/RepositoryRecoveryTests.cs",
                "docs/issue-labels.md",
                "docs/repo-audit.md")) &&
            HostCheck.Result(JsonStringArrayEquals(
                closure.GetProperty("forbiddenPaths"),
                "src/**",
                "std/**",
                "apps/**",
                "src/OS/Boot/EfiEntry.cs",
                "src/OS/OS.csproj",
                "src/OS.Kernel/Boot/BootSequence.cs",
                "src/OS.Kernel/Diagnostics/Probes.cs",
                "src/OS.Kernel/Diagnostics/ManagedX64StubStrategyADRUseExistingAudited.cs",
                "tests/OS.HostTests/ManagedX64StubStrategyADRUseExistingAuditedTests.cs",
                "docs/diagnostics.md",
                "docs/boot-order.md",
                "tools/OS.Tooling/EfiBuild.cs",
                "tools/OS.Tooling/QemuSmoke.cs",
                "tools/**",
                "README.md",
                "docs/completion-gates.md",
                "references/** (read-only)",
                "done/** (reserve one new step only after implementation evidence exists)")) &&
            HostCheck.Result(JsonStringArrayEquals(
                closure.GetProperty("requiredMarkers"),
                "TEST_START m02.018",
                M02DurableClosureMarker,
                "TEST_PASS m02.018",
                "TEST_FAIL m02.018 <reason>")) &&
            HostCheck.Result(JsonStringArrayEquals(
                closure.GetProperty("documentationPaths"),
                "docs/issue-labels.md",
                "docs/repo-audit.md")) &&
            HostCheck.Result(closureEvidence.Contains("issue #429", StringComparison.Ordinal)) &&
            HostCheck.Result(closureEvidence.Contains(
                "85528a5513f1cf24396ff29acc6787bc015ec9c5",
                StringComparison.Ordinal)) &&
            HostCheck.Result(closureNotes.Contains(
                "The ownedPaths inventory is the exact scope for this slice.",
                StringComparison.Ordinal)) &&
            HostCheck.Result(closureNotes.Contains(M02DurableClosureMarker, StringComparison.Ordinal)) &&
            HostCheck.Result(closureNotes.Contains(
                "<!-- clros-evidence-reviewed:M02-018 -->",
                StringComparison.Ordinal)) &&
            HostCheck.Result(closureAcceptance.Contains(
                "all 427 objects unchanged/skipped",
                StringComparison.Ordinal)) &&
            HostCheck.Result(closureCommands.Contains(
                "dotnet build .\\CLROS.slnx --nologo -m:1",
                StringComparison.Ordinal)) &&
            HostCheck.Result(closureCommands.Contains(
                "dotnet build .\\tests\\OS.HostTests\\OS.HostTests.csproj --nologo",
                StringComparison.Ordinal)) &&
            HostCheck.Result(closureCommands.Contains(
                "backlog sync -Offline",
                StringComparison.Ordinal)) &&
            HostCheck.Result(closureCommands.Contains(
                "backlog sync -SelfTest",
                StringComparison.Ordinal)) &&
            HostCheck.Result(closureCommands.Contains(
                "qemu smoke -DryRun",
                StringComparison.Ordinal)) &&
            HostCheck.Result(closureCommands.Contains(
                "efi build",
                StringComparison.Ordinal)) &&
            HostCheck.Result(!closureCommands.Contains(" -Qemu \"", StringComparison.Ordinal)) &&
            HostCheck.Result(!closureCommands.Contains(" -OvmfCode \"", StringComparison.Ordinal)) &&
            HostCheck.Result(DependenciesEqual(materializer, "M02-018")) &&
            HostCheck.Result(DependenciesEqual(safeIntegration, "M02-019")) &&
            HostCheck.Result(DependenciesEqual(livePortIo, "M02-020")) &&
            HostCheck.Result(JsonElementContains(
                epic.GetProperty("acceptanceCriteria"),
                "All 20 keyed child issues")) &&
            HostCheck.Result(closure.GetProperty("ownedPaths")
                .EnumerateArray()
                .Select(path => path.GetString() ?? string.Empty)
                .All(path =>
                    !path.StartsWith("src/", StringComparison.Ordinal) &&
                    !path.StartsWith("std/", StringComparison.Ordinal) &&
                    !path.StartsWith("apps/", StringComparison.Ordinal))) &&
            HostCheck.Result(closureBoundary.Contains(
                "changes no production C#",
                StringComparison.Ordinal)) &&
            HostCheck.Result(closureBoundary.Contains(
                "does not prove live page materialization",
                StringComparison.Ordinal)) &&
            HostCheck.Result(closureBoundary.Contains(
                "CloudReady status",
                StringComparison.Ordinal)) &&
            HostCheck.Result(closureBoundary.Contains(
                "production readiness",
                StringComparison.Ordinal));
    }

    private static bool ValidateM02MaterializerDurableClosureContracts(
        IReadOnlyDictionary<string, JsonElement> issueByKey)
    {
        string[] requiredKeys =
        [
            "M02-EPIC",
            "M02-002",
            "M02-003",
            "M02-017",
            "M02-019"
        ];
        if (requiredKeys.Any(key => !issueByKey.ContainsKey(key)))
        {
            return HostCheck.Result(false);
        }

        JsonElement epic = issueByKey["M02-EPIC"];
        JsonElement materializer = issueByKey["M02-002"];
        JsonElement closure = issueByKey["M02-019"];
        JsonElement safeIntegration = issueByKey["M02-017"];
        JsonElement livePortIo = issueByKey["M02-003"];
        JsonElement materializerLabels = materializer.GetProperty("labels");
        JsonElement closureLabels = closure.GetProperty("labels");
        string materializerEvidence =
            materializer.GetProperty("currentEvidence").GetString()
            ?? string.Empty;
        string closureEvidence =
            closure.GetProperty("currentEvidence").GetString()
            ?? string.Empty;
        string closureNotes =
            JsonArrayText(closure.GetProperty("implementationNotes"));
        string closureAcceptance =
            JsonArrayText(closure.GetProperty("acceptanceCriteria"));
        string closureCommands =
            JsonArrayText(closure.GetProperty("requiredCommands"));
        string closureBoundary =
            closure.GetProperty("evidenceBoundary").GetString()
            ?? string.Empty;
        string[] materializerEvidenceAnchors =
        [
            "8566f44cf493d7138d500b0cdad17b75d34b3405",
            "06a38803b8bf4d5bc35e29948e626dbd8fb164bd",
            "09bc5e263a3ab624ea3232655957d9ae79b2d042",
            "88df315bcca0bf3302d25ca88451b1d51f1ce31b",
            "c272b6991a68309a7e348bd20d85819977b7a264",
            "da856db657575d8f8d67713c7afe96e113854e54",
            "PR #431",
            "issue #42",
            "comment #6644",
            "comment #6651",
            "done/step894.md",
            "artifacts/logs/m02-002-final/06a38803-20260730T220052Z-r1",
            "7AF214CCF75F8D05F6A7E1E667B56698633DD5EB57B22544A450B0FF1F584935",
            "44D9E3F31EA3E3AFA9F66C9F3766B0A38BD84C32934145FCCD8809CFC0A9B8C7",
            "native exit 0",
            "3,338 TEST_START",
            "3,339 TEST_PASS",
            "zero TEST_FAIL",
            "realQemuRun=false"
        ];

        return HostCheck.Result(DependenciesEqual(materializer, "M02-018")) &&
            HostCheck.Result(string.Equals(
                materializer.GetProperty("desiredState").GetString(),
                "closed",
                StringComparison.Ordinal)) &&
            HostCheck.Result(JsonArrayContainsExact(materializerLabels, "agent:done")) &&
            HostCheck.Result(!JsonArrayContainsExact(materializerLabels, "agent:blocked")) &&
            HostCheck.Result(CountPrefix(materializerLabels, "agent:") == 1) &&
            HostCheck.Result(materializerEvidenceAnchors.All(anchor =>
                materializerEvidence.Contains(anchor, StringComparison.Ordinal))) &&
            HostCheck.Result(string.Equals(
                closure.GetProperty("title").GetString(),
                "[M02-019] Persist reviewed M02-002 closure intent and converge the tracker before safe EFI integration.",
                StringComparison.Ordinal)) &&
            HostCheck.Result(DependenciesEqual(closure, "M02-002")) &&
            HostCheck.Result(string.Equals(
                closure.GetProperty("desiredState").GetString(),
                "closed",
                StringComparison.Ordinal)) &&
            HostCheck.Result(string.Equals(
                closure.GetProperty("collisionLane").GetString(),
                "lane:solution-baseline",
                StringComparison.Ordinal)) &&
            HostCheck.Result(JsonArrayContainsExact(closureLabels, "agent:done")) &&
            HostCheck.Result(JsonArrayContainsExact(closureLabels, "priority:p0")) &&
            HostCheck.Result(JsonArrayContainsExact(closureLabels, "type:tooling")) &&
            HostCheck.Result(JsonArrayContainsExact(closureLabels, "evidence:host")) &&
            HostCheck.Result(JsonArrayContainsExact(closureLabels, "component:repo")) &&
            HostCheck.Result(JsonArrayContainsExact(closureLabels, "phase:m02-diagnostics")) &&
            HostCheck.Result(JsonArrayContainsExact(
                closureLabels,
                "lane:solution-baseline")) &&
            HostCheck.Result(CountPrefix(closureLabels, "agent:") == 1) &&
            HostCheck.Result(CountPrefix(closureLabels, "type:") == 1) &&
            HostCheck.Result(CountPrefix(closureLabels, "evidence:") == 1) &&
            HostCheck.Result(CountPrefix(closureLabels, "lane:") == 1) &&
            HostCheck.Result(OwnedPathsEqual(
                closure,
                ".gitea/backlog/clros-issues.json",
                "tools/OS.Tooling/BacklogSync.cs",
                "tests/OS.HostTests/GiteaBacklogManifestTests.cs",
                "tests/OS.HostTests/RepositoryRecoveryTests.cs",
                "docs/issue-labels.md",
                "docs/repo-audit.md")) &&
            HostCheck.Result(JsonStringArrayEquals(
                closure.GetProperty("forbiddenPaths"),
                "src/**",
                "std/**",
                "apps/**",
                "src/OS/Boot/EfiEntry.cs",
                "src/OS/OS.csproj",
                "src/OS.Kernel/Boot/BootSequence.cs",
                "src/OS.Kernel/Diagnostics/Probes.cs",
                "src/OS.Kernel/Diagnostics/PortIoOperationPlan.cs",
                "src/OS.Kernel/Diagnostics/PortIoStubPlan.cs",
                "src/OS.Kernel/Diagnostics/ExistingIn8Out8StubBytesRWPagesFinalize.cs",
                "tests/OS.HostTests/ExistingIn8Out8StubBytesRWPagesFinalizeTests.cs",
                "docs/diagnostics.md",
                "docs/boot-order.md",
                "tools/OS.Tooling/EfiBuild.cs",
                "tools/OS.Tooling/QemuSmoke.cs",
                "tools/**",
                "README.md",
                "docs/completion-gates.md",
                "references/** (read-only)",
                "done/** (reserve one new step only after implementation evidence exists)")) &&
            HostCheck.Result(JsonStringArrayEquals(
                closure.GetProperty("requiredMarkers"),
                "TEST_START m02.019",
                M02MaterializerDurableClosureMarker,
                "TEST_PASS m02.019",
                "TEST_FAIL m02.019 <reason>")) &&
            HostCheck.Result(JsonStringArrayEquals(
                closure.GetProperty("documentationPaths"),
                "docs/issue-labels.md",
                "docs/repo-audit.md")) &&
            HostCheck.Result(closureEvidence.Contains("issue #432", StringComparison.Ordinal)) &&
            HostCheck.Result(closureEvidence.Contains("claim comment #6661", StringComparison.Ordinal)) &&
            HostCheck.Result(closureEvidence.Contains("PR #431", StringComparison.Ordinal)) &&
            HostCheck.Result(closureEvidence.Contains(
                "da856db657575d8f8d67713c7afe96e113854e54",
                StringComparison.Ordinal)) &&
            HostCheck.Result(closureNotes.Contains(
                "The ownedPaths inventory is the exact scope for this slice.",
                StringComparison.Ordinal)) &&
            HostCheck.Result(closureNotes.Contains(
                M02MaterializerDurableClosureMarker,
                StringComparison.Ordinal)) &&
            HostCheck.Result(closureNotes.Contains(
                "<!-- clros-evidence-reviewed:M02-019 -->",
                StringComparison.Ordinal)) &&
            HostCheck.Result(closureAcceptance.Contains(
                "all 428 objects unchanged/skipped",
                StringComparison.Ordinal)) &&
            HostCheck.Result(closureAcceptance.Contains(
                "M02-017 the smallest collision-safe ready issue",
                StringComparison.Ordinal)) &&
            HostCheck.Result(closureCommands.Contains(
                "dotnet build .\\CLROS.slnx --nologo -m:1",
                StringComparison.Ordinal)) &&
            HostCheck.Result(closureCommands.Contains(
                "dotnet build .\\tests\\OS.HostTests\\OS.HostTests.csproj --nologo",
                StringComparison.Ordinal)) &&
            HostCheck.Result(closureCommands.Contains(
                "backlog sync -Offline",
                StringComparison.Ordinal)) &&
            HostCheck.Result(closureCommands.Contains(
                "backlog sync -SelfTest",
                StringComparison.Ordinal)) &&
            HostCheck.Result(closureCommands.Contains(
                "qemu smoke -DryRun",
                StringComparison.Ordinal)) &&
            HostCheck.Result(closureCommands.Contains(
                "efi build",
                StringComparison.Ordinal)) &&
            HostCheck.Result(!closureCommands.Contains(" -Qemu \"", StringComparison.Ordinal)) &&
            HostCheck.Result(!closureCommands.Contains(" -OvmfCode \"", StringComparison.Ordinal)) &&
            HostCheck.Result(DependenciesEqual(safeIntegration, "M02-019")) &&
            HostCheck.Result(DependenciesEqual(livePortIo, "M02-020")) &&
            HostCheck.Result(JsonElementContains(
                epic.GetProperty("acceptanceCriteria"),
                "All 20 keyed child issues")) &&
            HostCheck.Result(closure.GetProperty("ownedPaths")
                .EnumerateArray()
                .Select(path => path.GetString() ?? string.Empty)
                .All(path =>
                    !path.StartsWith("src/", StringComparison.Ordinal) &&
                    !path.StartsWith("std/", StringComparison.Ordinal) &&
                    !path.StartsWith("apps/", StringComparison.Ordinal))) &&
            HostCheck.Result(closureBoundary.Contains(
                "changes no production C#",
                StringComparison.Ordinal)) &&
            HostCheck.Result(closureBoundary.Contains(
                "does not prove live page materialization",
                StringComparison.Ordinal)) &&
            HostCheck.Result(closureBoundary.Contains(
                "CloudReady status",
                StringComparison.Ordinal)) &&
            HostCheck.Result(closureBoundary.Contains(
                "production readiness",
                StringComparison.Ordinal));
    }

    private static bool ValidateM02SafeStubDurableClosureContracts(
        IReadOnlyDictionary<string, JsonElement> issueByKey)
    {
        string[] requiredKeys =
        [
            "M02-EPIC",
            "M02-003",
            "M02-017",
            "M02-019",
            "M02-020"
        ];
        if (requiredKeys.Any(key => !issueByKey.ContainsKey(key)))
        {
            return HostCheck.Result(false);
        }

        JsonElement epic = issueByKey["M02-EPIC"];
        JsonElement livePortIo = issueByKey["M02-003"];
        JsonElement safeIntegration = issueByKey["M02-017"];
        JsonElement materializerClosure = issueByKey["M02-019"];
        JsonElement closure = issueByKey["M02-020"];
        JsonElement safeLabels = safeIntegration.GetProperty("labels");
        JsonElement closureLabels = closure.GetProperty("labels");
        string safeEvidence =
            safeIntegration.GetProperty("currentEvidence").GetString()
            ?? string.Empty;
        string closureEvidence =
            closure.GetProperty("currentEvidence").GetString()
            ?? string.Empty;
        string closureNotes =
            JsonArrayText(closure.GetProperty("implementationNotes"));
        string closureAcceptance =
            JsonArrayText(closure.GetProperty("acceptanceCriteria"));
        string closureCommands =
            JsonArrayText(closure.GetProperty("requiredCommands"));
        string closureBoundary =
            closure.GetProperty("evidenceBoundary").GetString()
            ?? string.Empty;
        string[] safeEvidenceAnchors =
        [
            "cf8ce63c37a9c62a0f8f736adda14b6bd3e19a9a",
            "d18f520cee19aa0c89dfb41f326c92afd0284c02",
            "abb82e4cd5ccb8751fabbf0d3095796f626bb6e7",
            "910d19ece794d5f2a7d3f3df63b4d1dac749dba5",
            "d2872746059a7200431b6d3bc89c6e02cb42fa8b",
            "issue #427",
            "PR #435",
            "PR review #1",
            "comment #6742",
            "done/step896.md",
            "done/step897.md",
            "artifacts/logs/m02-017-final/cf8ce63c-20260803T211323Z-r1",
            "native exit 0",
            "3,346 TEST_START",
            "3,347 TEST_PASS",
            "zero TEST_FAIL",
            "C293C89AA5A48675E78380B571935B8F3EAC3E4F6A0E4F29D019F77B8E2911BB",
            "C8E8F0DE57D7E7AB1A4A606882CB05A77DD92915FEB99B7C0D35A5D37D377823",
            "D30B508AC06F592756C36ABD2B5CFAE2469ED31DF234BFDCD37134A7DBEB4EA5",
            "6F223E27E4180855DA64F4785C13B9B44649F0C8EF1590AA9B65A89CEF508AD0",
            "B934A1E5C2E2EEB095BDE1EE374A0B129DE99CE203AAA7F992E1F0CAC8137B0C",
            "D764684446C21497EE3816FF170F46E2B73F8228B429242B11BDAB47BCA16EF5",
            "9C2C8EBC64BCA601CE6CDB2D231DA784EE9555B546FB7426A46A433B71353965"
        ];

        return HostCheck.Result(DependenciesEqual(safeIntegration, "M02-019")) &&
            HostCheck.Result(string.Equals(
                safeIntegration.GetProperty("desiredState").GetString(),
                "closed",
                StringComparison.Ordinal)) &&
            HostCheck.Result(JsonStringArrayEquals(
                safeLabels,
                "agent:done",
                "priority:p1",
                "type:implementation",
                "evidence:qemu",
                "component:boot",
                "component:diagnostics",
                "component:qemu",
                "phase:m02-diagnostics",
                "lane:efi-entry")) &&
            HostCheck.Result(OwnedPathsEqual(
                safeIntegration,
                "src/OS/OS.csproj",
                "src/OS/Boot/EfiEntry.cs",
                "src/OS/Boot/M02SafeStubPageIntegration.cs",
                "tools/OS.Tooling/EfiBuild.cs",
                "tools/OS.Tooling/QemuSmoke.cs",
                "tests/OS.HostTests/M02SafeStubPageEfiEntryTests.cs",
                "tests/OS.HostTests/BuildEfiShellPreflightTests.cs",
                "tests/OS.HostTests/QemuRunnerTests.cs",
                "docs/diagnostics.md",
                "docs/boot.md",
                "docs/boot-order.md",
                "docs/efi-build.md",
                "docs/qemu.md",
                "docs/completion-gates.md")) &&
            HostCheck.Result(JsonStringArrayEquals(
                safeIntegration.GetProperty("forbiddenPaths"),
                ".gitea/backlog/**",
                "src/OS/Boot/X64InstructionPublication.cs",
                "src/OS/Runtime/**",
                "src/OS.Kernel/Diagnostics/PortIoOperationPlan.cs (read-only)",
                "src/OS.Kernel/Diagnostics/PortIoStubPlan.cs (read-only)",
                "src/OS.Kernel/Diagnostics/ExistingIn8Out8StubBytesRWPagesFinalize.cs (read-only)",
                "src/OS.Kernel/Diagnostics/X64ProtectionProbeStubPlan.cs (read-only)",
                "src/OS.Kernel/Boot/BootSequence.cs",
                "src/OS.Kernel/Boot/BootSequencePlan.cs",
                "src/OS.Kernel/Diagnostics/Probes.cs",
                "tests/OS.HostTests/ManagedX64StubStrategyADRUseExistingAuditedTests.cs",
                "tests/OS.HostTests/ExistingIn8Out8StubBytesRWPagesFinalizeTests.cs",
                "tools/**",
                "README.md",
                "references/** (read-only)",
                "done/** (reserve one new step only after fresh real-QEMU evidence exists)")) &&
            HostCheck.Result(JsonStringArrayEquals(
                safeIntegration.GetProperty("requiredMarkers"),
                "TEST_START m02.017",
                "M02_STUB_PAGE_MATERIALIZED pageBytes=4096 safeOffset=0 out8Offset=16 in8Offset=32 fnv1a64=F85393074A792B1E",
                "M02_STUB_PAGE_READBACK_VERIFIED fnv1a64=F85393074A792B1E",
                "M02_SAFE_PROBE_EXECUTED offset=0 return=0x4F534F4B",
                "CLROS_M02_017_READY pageBytes=4096 safeOffset=0 out8Offset=16 in8Offset=32 fnv1a64=F85393074A792B1E temporaryOwnedRootRx=true portIoExecuted=false",
                "TEST_PASS m02.017",
                "M02_STUB_PAGE_MATERIALIZATION_FAILED",
                "M02_STUB_PAGE_READBACK_FAILED",
                "M02_STUB_PAGE_HASH_MISMATCH",
                "M02_SAFE_PROBE_FAILED",
                "M02_OUT8_EXECUTED",
                "M02_IN8_EXECUTED",
                "M02_PORT_IO_ATTEMPTED",
                "M02_PRIVILEGED_STUB_TARGETED",
                "TEST_FAIL m02.017 <reason>")) &&
            HostCheck.Result(safeEvidenceAnchors.All(anchor =>
                safeEvidence.Contains(anchor, StringComparison.Ordinal))) &&
            HostCheck.Result(string.Equals(
                closure.GetProperty("title").GetString(),
                "[M02-020] Persist reviewed M02-017 safe-stub closure and converge the tracker before privileged I/O.",
                StringComparison.Ordinal)) &&
            HostCheck.Result(DependenciesEqual(closure, "M02-017")) &&
            HostCheck.Result(string.Equals(
                closure.GetProperty("desiredState").GetString(),
                "closed",
                StringComparison.Ordinal)) &&
            HostCheck.Result(string.Equals(
                closure.GetProperty("collisionLane").GetString(),
                "lane:solution-baseline",
                StringComparison.Ordinal)) &&
            HostCheck.Result(JsonStringArrayEquals(
                closureLabels,
                "agent:done",
                "priority:p0",
                "type:tooling",
                "evidence:host",
                "component:repo",
                "phase:m02-diagnostics",
                "lane:solution-baseline")) &&
            HostCheck.Result(OwnedPathsEqual(
                closure,
                ".gitea/backlog/clros-issues.json",
                "tools/OS.Tooling/BacklogSync.cs",
                "tests/OS.HostTests/GiteaBacklogManifestTests.cs",
                "tests/OS.HostTests/RepositoryRecoveryTests.cs",
                "docs/issue-labels.md",
                "docs/repo-audit.md")) &&
            HostCheck.Result(JsonStringArrayEquals(
                closure.GetProperty("forbiddenPaths"),
                "src/**",
                "std/**",
                "apps/**",
                "tools/OS.Tooling/EfiBuild.cs",
                "tools/OS.Tooling/QemuSmoke.cs",
                "tools/OS.Tooling/EfiVerify.cs",
                "tests/OS.HostTests/M02SafeStubPageEfiEntryTests.cs",
                "tests/OS.HostTests/BuildEfiShellPreflightTests.cs",
                "tests/OS.HostTests/QemuRunnerTests.cs",
                "tests/OS.HostTests/EfiArtifactVerificationScriptTests.cs",
                "docs/diagnostics.md",
                "docs/boot.md",
                "docs/boot-order.md",
                "docs/efi-build.md",
                "docs/qemu.md",
                "docs/completion-gates.md",
                "tools/**",
                "README.md",
                "references/** (read-only)",
                "done/** (reserve one new step only after implementation evidence exists)")) &&
            HostCheck.Result(JsonStringArrayEquals(
                closure.GetProperty("requiredMarkers"),
                "TEST_START m02.020",
                M02SafeStubDurableClosureMarker,
                "TEST_PASS m02.020",
                "TEST_FAIL m02.020 <reason>")) &&
            HostCheck.Result(JsonStringArrayEquals(
                closure.GetProperty("documentationPaths"),
                "docs/issue-labels.md",
                "docs/repo-audit.md")) &&
            HostCheck.Result(closureEvidence.Contains("live issue #436", StringComparison.Ordinal)) &&
            HostCheck.Result(closureEvidence.Contains("PR #435", StringComparison.Ordinal)) &&
            HostCheck.Result(closureEvidence.Contains(
                "d2872746059a7200431b6d3bc89c6e02cb42fa8b",
                StringComparison.Ordinal)) &&
            HostCheck.Result(closureNotes.Contains(
                "The ownedPaths inventory is the exact scope for this slice.",
                StringComparison.Ordinal)) &&
            HostCheck.Result(closureNotes.Contains(
                M02SafeStubDurableClosureMarker,
                StringComparison.Ordinal)) &&
            HostCheck.Result(closureNotes.Contains(
                "<!-- clros-evidence-reviewed:M02-020 -->",
                StringComparison.Ordinal)) &&
            HostCheck.Result(closureAcceptance.Contains(
                "all 429 objects unchanged/skipped",
                StringComparison.Ordinal)) &&
            HostCheck.Result(closureAcceptance.Contains(
                "M02-003 the smallest collision-safe ready issue",
                StringComparison.Ordinal)) &&
            HostCheck.Result(closureCommands.Contains(
                "dotnet build .\\CLROS.slnx --nologo -m:1",
                StringComparison.Ordinal)) &&
            HostCheck.Result(closureCommands.Contains(
                "dotnet build .\\tests\\OS.HostTests\\OS.HostTests.csproj --nologo",
                StringComparison.Ordinal)) &&
            HostCheck.Result(closureCommands.Contains(
                "backlog sync -Offline",
                StringComparison.Ordinal)) &&
            HostCheck.Result(closureCommands.Contains(
                "backlog sync -SelfTest",
                StringComparison.Ordinal)) &&
            HostCheck.Result(closureCommands.Contains(
                "qemu smoke -DryRun",
                StringComparison.Ordinal)) &&
            HostCheck.Result(closureCommands.Contains(
                "efi build",
                StringComparison.Ordinal)) &&
            HostCheck.Result(!closureCommands.Contains(" -Qemu \"", StringComparison.Ordinal)) &&
            HostCheck.Result(!closureCommands.Contains(" -OvmfCode \"", StringComparison.Ordinal)) &&
            HostCheck.Result(DependenciesEqual(materializerClosure, "M02-002")) &&
            HostCheck.Result(DependenciesEqual(safeIntegration, "M02-019")) &&
            HostCheck.Result(DependenciesEqual(closure, "M02-017")) &&
            HostCheck.Result(DependenciesEqual(livePortIo, "M02-020")) &&
            HostCheck.Result(JsonElementContains(
                epic.GetProperty("acceptanceCriteria"),
                "All 20 keyed child issues")) &&
            HostCheck.Result(closure.GetProperty("ownedPaths")
                .EnumerateArray()
                .Select(path => path.GetString() ?? string.Empty)
                .All(path =>
                    !path.StartsWith("src/", StringComparison.Ordinal) &&
                    !path.StartsWith("std/", StringComparison.Ordinal) &&
                    !path.StartsWith("apps/", StringComparison.Ordinal))) &&
            HostCheck.Result(closureBoundary.Contains(
                "changes no production C#",
                StringComparison.Ordinal)) &&
            HostCheck.Result(closureBoundary.Contains(
                "does not prove new live page behavior",
                StringComparison.Ordinal)) &&
            HostCheck.Result(closureBoundary.Contains(
                "CloudReady status",
                StringComparison.Ordinal)) &&
            HostCheck.Result(closureBoundary.Contains(
                "production readiness",
                StringComparison.Ordinal));
    }

    private static bool ValidateM03PermanentCr3Handoff(
        IReadOnlyDictionary<string, JsonElement> issueByKey)
    {
        JsonElement issue = issueByKey["M03-007"];
        string notes = JsonArrayText(issue.GetProperty("implementationNotes"));
        string acceptance = JsonArrayText(issue.GetProperty("acceptanceCriteria"));
        string commands = JsonArrayText(issue.GetProperty("requiredCommands"));
        string evidence = issue.GetProperty("evidenceBoundary").GetString() ?? string.Empty;
        return HostCheck.Result(DependenciesEqual(issue, "M01-026", "M02-016", "M03-006")) &&
            HostCheck.Result(string.Equals(
                issue.GetProperty("collisionLane").GetString(),
                "lane:efi-entry",
                StringComparison.Ordinal)) &&
            HostCheck.Result(JsonArrayContainsExact(issue.GetProperty("labels"), "lane:efi-entry")) &&
            HostCheck.Result(HasOwnedPath(issue, "src/OS/Boot/EfiEntry.cs")) &&
            HostCheck.Result(HasOwnedPath(
                issue,
                "tests/OS.HostTests/M03PermanentCr3AfterExitEfiEntryTests.cs")) &&
            HostCheck.Result(notes.Contains(
                "Permanent owned-CR3 adoption before a successful ExitBootServices return is forbidden",
                StringComparison.Ordinal)) &&
            HostCheck.Result(notes.Contains(
                "make no firmware call ever again",
                StringComparison.Ordinal)) &&
            HostCheck.Result(notes.Contains(
                "M03_PERMANENT_CR3_ADOPTED",
                StringComparison.Ordinal)) &&
            HostCheck.Result(acceptance.Contains(
                "no permanent adoption occurs before successful EBS",
                StringComparison.Ordinal)) &&
            HostCheck.Result(commands.Contains(
                "M03_EBS_RETURNED_SUCCESS,M03_PERMANENT_CR3_ADOPTED,M03_POST_EBS_OWNED_DIAGNOSTIC",
                StringComparison.Ordinal)) &&
            HostCheck.Result(commands.Contains(
                "M03_FIRMWARE_CALL_AFTER_EBS",
                StringComparison.Ordinal)) &&
            HostCheck.Result(evidence.Contains(
                "adopted permanently before owned post-EBS diagnostics",
                StringComparison.Ordinal)) &&
            HostCheck.Result(evidence.Contains("M03-008", StringComparison.Ordinal));
    }

    private static bool ValidateSyncVerifyRequiresApply(
        IReadOnlyDictionary<string, JsonElement> issueByKey)
    {
        foreach (JsonElement issue in issueByKey.Values)
        {
            if (JsonElementContains(
                    issue.GetProperty("requiredMarkers"),
                    "SYNC_VERIFY_PASS") &&
                !JsonElementContains(
                    issue.GetProperty("requiredCommands"),
                    "backlog sync -Apply"))
            {
                return HostCheck.Result(false);
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
                    return HostCheck.Result(false);
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

    private static int CountExact(JsonElement array, string expected)
    {
        return array.EnumerateArray().Count(value =>
            string.Equals(value.GetString(), expected, StringComparison.Ordinal));
    }

    private static bool HasOwnedPath(JsonElement issue, string path)
    {
        return issue.GetProperty("ownedPaths")
            .EnumerateArray()
            .Any(value => string.Equals(value.GetString(), path, StringComparison.Ordinal));
    }

    private static bool OwnedPathsEqual(JsonElement issue, params string[] expected)
    {
        string[] actual = issue.GetProperty("ownedPaths")
            .EnumerateArray()
            .Select(value => value.GetString() ?? string.Empty)
            .ToArray();
        return actual.SequenceEqual(expected, StringComparer.Ordinal);
    }

    private static string JsonArrayText(JsonElement array)
    {
        return string.Join(
            "\n",
            array.EnumerateArray().Select(value => value.GetString() ?? string.Empty));
    }

    private static bool ReferencePathExists(JsonElement issue, string expected)
    {
        return issue.GetProperty("referenceInfluences")
            .EnumerateArray()
            .Any(reference =>
                string.Equals(
                    reference.GetProperty("path").GetString(),
                    expected,
                    StringComparison.Ordinal));
    }

    private static bool JsonElementContains(JsonElement array, string value)
    {
        return array.EnumerateArray().Any(element =>
            (element.GetString() ?? string.Empty).Contains(value, StringComparison.Ordinal));
    }

    private static bool JsonArrayContainsExact(JsonElement array, string value)
    {
        return array.EnumerateArray().Any(element =>
            string.Equals(element.GetString(), value, StringComparison.Ordinal));
    }

    private static bool JsonStringArrayEquals(JsonElement array, params string[] expected)
    {
        string[] actual = array.EnumerateArray()
            .Select(element => element.GetString() ?? string.Empty)
            .ToArray();
        return actual.SequenceEqual(expected, StringComparer.Ordinal);
    }

    private static bool ValidateQemuMarkerContract(
        JsonElement issue,
        string[] guestSuccessMarkers,
        string guestFailureMarker)
    {
        string[] expectedRequiredMarkers =
            ["TEST_START qemu.smoke", .. guestSuccessMarkers,
             "TEST_PASS qemu.smoke", guestFailureMarker,
             "TEST_FAIL qemu.smoke <reason>"];
        if (!JsonStringArrayEquals(
                issue.GetProperty("requiredMarkers"),
                expectedRequiredMarkers))
        {
            return HostCheck.Result(false);
        }

        string commands = JsonArrayText(issue.GetProperty("requiredCommands"));
        string expectedGuestList = string.Join(",", guestSuccessMarkers);
        foreach (string option in
            new[] { "-RequireMarker", "-RequireOrderedMarker", "-RequireUniqueMarker" })
        {
            System.Text.RegularExpressions.MatchCollection matches =
                System.Text.RegularExpressions.Regex.Matches(
                    commands,
                    System.Text.RegularExpressions.Regex.Escape(option) +
                        " \"([^\"]*)\"",
                    System.Text.RegularExpressions.RegexOptions.CultureInvariant);
            if (matches.Count != 2 ||
                matches.Cast<System.Text.RegularExpressions.Match>().Any(match =>
                    !string.Equals(
                        match.Groups[1].Value,
                        expectedGuestList,
                        StringComparison.Ordinal)))
            {
                return HostCheck.Result(false);
            }
        }

        return true;
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
        var inventory = BacklogManifest.Read(manifestPath);
        int totalManaged = inventory.Issues.Count + inventory.Labels.Count + inventory.Milestones.Count;
        byte[] before = SHA256.HashData(File.ReadAllBytes(manifestPath));
        DateTime beforeWriteTime = File.GetLastWriteTimeUtc(manifestPath);

        ProcessResult result = RunScript(scriptPath, "-Offline");

        byte[] after = SHA256.HashData(File.ReadAllBytes(manifestPath));
        DateTime afterWriteTime = File.GetLastWriteTimeUtc(manifestPath);
        return HostCheck.Result(result.ExitCode == 0) &&
            HostCheck.Result(result.Output.Contains($"SYNC_MANIFEST_VALID milestones={inventory.Milestones.Count} labels={inventory.Labels.Count} epics={inventory.Milestones.Count} children={BacklogContracts.ChildCount} issues={inventory.Issues.Count}", StringComparison.Ordinal)) &&
            HostCheck.Result(result.Output.Contains("SYNC_MODE preview", StringComparison.Ordinal)) &&
            HostCheck.Result(result.Output.Contains("SYNC_OFFLINE manifest_only", StringComparison.Ordinal)) &&
            HostCheck.Result(result.Output.Contains($"SYNC_KEYS action=skipped count={totalManaged}", StringComparison.Ordinal)) &&
            HostCheck.Result(result.Output.Contains($"SYNC_SUMMARY created=0 updated=0 unchanged=0 skipped={totalManaged} failed=0 wouldCreate=0 wouldUpdate=0", StringComparison.Ordinal)) &&
            HostCheck.Result(result.Output.Contains("TEST_PASS gitea.backlog.sync", StringComparison.Ordinal)) &&
            HostCheck.Result(before.AsSpan().SequenceEqual(after)) &&
            HostCheck.Result(beforeWriteTime == afterWriteTime);
    }

    private static bool TestManagedBodySelfTest(string scriptPath)
    {
        ProcessResult result = RunScript(scriptPath, "-SelfTest");
        var milestoneCases = BacklogSelfTest.MilestoneCaseNames;
        string[] actualMilestoneCases = result.Output.Split('\n')
            .Select(line => line.TrimEnd('\r'))
            .Where(line => line.StartsWith("SYNC_SELF_TEST_MILESTONE_CASE_PASS ", StringComparison.Ordinal))
            .ToArray();
        return HostCheck.Result(result.ExitCode == 0) &&
            HostCheck.Result(actualMilestoneCases.SequenceEqual(milestoneCases.Select(name =>
                "SYNC_SELF_TEST_MILESTONE_CASE_PASS name=" + name + " modes=preview,apply"), StringComparer.Ordinal)) &&
            HostCheck.Result(result.Output.Contains($"SYNC_SELF_TEST_MILESTONE_POLICY_PASS cases={milestoneCases.Count} modes=preview,apply actualApiCalls=0", StringComparison.Ordinal)) &&
            HostCheck.Result(result.Output.Contains("SYNC_SELF_TEST_MANAGED_BODY_PASS", StringComparison.Ordinal)) &&
            HostCheck.Result(result.Output.Contains("SYNC_SELF_TEST_HIDDEN_KEY_ADOPTION_PASS key=M02-020 markers=1 wouldCreate=0 duplicateMarkersRejected=true", StringComparison.Ordinal)) &&
            HostCheck.Result(result.Output.Contains("SYNC_SELF_TEST_STATE_POLICY_PASS", StringComparison.Ordinal)) &&
            HostCheck.Result(result.Output.Contains("TEST_PASS gitea.backlog.sync", StringComparison.Ordinal));
    }

    private static bool TestRequiredFieldsAndMilestoneIntent(
        string scriptPath, string manifestPath, string tempRoot)
    {
        if (!TestInvalidManifest(scriptPath, manifestPath, tempRoot, "missing",
                RemoveRequiredField, "missing required field 'summary'"))
        {
            return HostCheck.Result(false);
        }

        string?[] invalidStates = [null, "null", "0", "true", "[]", "[\"open\"]", "[\"closed\",\"reopen\"]", "{}", "\"\"",
            "\"Closed\"", "\"OPEN\"", "\" closed\"", "\"closed \"", "\"done\"",
            "\"cl\\u0000osed\"", "\"o\\u00ADpen\"", "\"re\\u200Bopen\""];
        for (int index = 0; index < invalidStates.Length; index++)
        {
            string? value = invalidStates[index];
            if (!TestInvalidManifest(scriptPath, manifestPath, tempRoot, "milestone-state-" + index,
                    root =>
                    {
                        JsonObject milestone = root["milestones"]![0]!.AsObject();
                        if (value is null) { milestone.Remove("state"); }
                        else { milestone["state"] = JsonNode.Parse(value); }
                    }, "state must be exactly open, closed or reopen"))
            {
                return HostCheck.Result(false);
            }
        }

        foreach (string state in new[] { "open", "closed", "reopen" })
        {
            JsonObject root = JsonNode.Parse(File.ReadAllText(manifestPath))!.AsObject();
            root["milestones"]![0]!["state"] = state;
            string path = Path.Combine(tempRoot, "milestone-valid-" + state + ".json");
            File.WriteAllText(path, root.ToJsonString(), new UTF8Encoding(false));
            ProcessResult result = RunScript(scriptPath, "-Offline", "-ManifestPath", path);
            if (result.ExitCode != 0 || !result.Output.Contains("SYNC_OFFLINE manifest_only", StringComparison.Ordinal) ||
                !result.Output.Contains("TEST_PASS gitea.backlog.sync", StringComparison.Ordinal))
            {
                return HostCheck.Result(false);
            }
        }
        return true;
    }

    private static bool TestDedicatedIssueLabelEndpoint(string scriptPath)
    {
        int calls = 0;
        Task<JsonNode?> Api(string method, string path, JsonObject? body, CancellationToken cancellation)
        {
            calls++;
            HostCheck.Equal("PUT", method);
            HostCheck.Equal("/repos/admin/CLROS/issues/17/labels", path);
            HostCheck.Equal(1, body!.Count);
            HostCheck.True(body["labels"]!.AsArray().Select(x => (int)x!).SequenceEqual(new[] { 3, 7 }));
            return Task.FromResult<JsonNode?>(new JsonArray(new JsonObject { ["id"] = 3 }, new JsonObject { ["id"] = 7 }));
        }
        var response = BacklogSync.ReplaceLabelsAsync(Api, 17, new JsonArray(3, 7)).GetAwaiter().GetResult();
        return HostCheck.Result(calls == 1) && HostCheck.Result(response.Count == 2);
    }
    private static bool TestCanonicalTargetRefusal(string scriptPath)
    {
        ProcessResult result = RunScript(
            scriptPath,
            "-Offline",
            "-BaseUrl",
            "http://localhost:3001");

        return HostCheck.Result(result.ExitCode == 1) &&
            HostCheck.Result(result.Output.Contains("TEST_FAIL gitea.backlog.sync", StringComparison.Ordinal)) &&
            HostCheck.Result(result.Output.Contains("Refusing non-canonical target", StringComparison.Ordinal));
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
        return HostCheck.Result(result.ExitCode == 1) &&
            HostCheck.Result(result.Output.Contains("TEST_FAIL gitea.backlog.sync", StringComparison.Ordinal)) &&
            HostCheck.Result(result.Output.Contains(expectedFailure, StringComparison.Ordinal));
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

    private static void ReplaceLastM01ChildWithExtra(JsonObject root)
    {
        JsonObject child = FindIssue(root, "M01-036");
        child["key"] = "M01-999";

        int replacements = 0;
        foreach (string dependentKey in
            new[] { "M02-EPIC", "M02-001", "M04-EPIC", "M04-001" })
        {
            JsonObject dependent = FindIssue(root, dependentKey);
            JsonArray dependencyKeys = dependent["dependencyKeys"]?.AsArray()
                ?? throw new InvalidOperationException(
                    dependentKey + " dependencyKeys missing");
            for (int index = 0; index < dependencyKeys.Count; index++)
            {
                if (dependencyKeys[index]?.GetValue<string>() == "M01-036")
                {
                    dependencyKeys[index] = "M01-999";
                    replacements++;
                }
            }
        }

        if (replacements != 4)
        {
            throw new InvalidOperationException(
                "M01-036 dependent rewrites missing");
        }
    }

    private static void AddDependencyCycle(JsonObject root)
    {
        JsonObject target = FindIssue(root, "M00-002");
        target["dependencyKeys"] = new JsonArray("M00-003");
    }

    private static void AddUnserializedM00Overlap(JsonObject root)
    {
        JsonObject target = FindIssue(root, "M00-010");
        target["desiredState"] = "reopen";
        foreach (JsonObject other in root["issues"]!.AsArray().Select(x => x!.AsObject()))
            if (other["ownedPaths"]!.AsArray().Any(x => x!.GetValue<string>() == "docs/reference-map.md"))
                other["desiredState"] = "reopen";
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
        using StringWriter output = new();
        int exit = BacklogSync.RunAsync(HostToolProcess.FindRepositoryRoot(), arguments, output).GetAwaiter().GetResult();
        return new ProcessResult(exit, output.ToString());
    }
    private readonly struct ProcessResult(int exitCode, string output)
    {
        internal int ExitCode { get; } = exitCode;

        internal string Output { get; } = output;
    }
}
