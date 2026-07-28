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
            ["M00"] = 23,
            ["M01"] = 35,
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
            RunTest("gitea backlog M01 instruction-publication chain is collision-safe", () => TestM01InstructionPublicationManifest(manifestPath), ref failures);
            RunTest("gitea backlog validation requires every M01 child through 035", () => TestInvalidManifest(scriptPath, manifestPath, tempRoot, "missing-m01-035", ReplaceLastM01ChildWithExtra, "Manifest is missing child 'M01-035'"), ref failures);
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

        if (json.Contains('\uFFFD') || json.Contains('\u00E2'))
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
        if (milestones.GetArrayLength() != 16 || labels.GetArrayLength() != 68 || issues.GetArrayLength() != 340)
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

            if (!key.EndsWith("-EPIC", StringComparison.Ordinal) &&
                !key.StartsWith("M01-", StringComparison.Ordinal) &&
                !string.Equals(key, "M03-007", StringComparison.Ordinal))
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
            ValidateSyncVerifyRequiresApply(issueByKey) &&
            ValidateM01ContractsV2(issues, issueByKey) &&
            ValidateM03PermanentCr3Handoff(issueByKey) &&
            DependenciesEqual(issueByKey["M06-001"], "M05-019") &&
            DependenciesEqual(issueByKey["M06-019"], "M06-018", "M05-021") &&
            DependenciesEqual(issueByKey["M07-001"], "M05-019") &&
            DependenciesEqual(issueByKey["M07-024"], "M07-023", "M05-021") &&
            DependenciesEqual(issueByKey["M09-001"], "M04-023", "M06-019", "M07-025", "M08-019") &&
            DependenciesEqual(issueByKey["M12-018"], "M12-016") &&
            DependenciesEqual(issueByKey["M13-018"], "M13-016") &&
            DependenciesEqual(issueByKey["M15-014"], "M15-009");
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
        return ValidateM01InstructionPublicationContracts(issueByKey);
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

        JsonElement originalBacklog = issueByKey["M00-001"];
        JsonElement repair = issueByKey["M00-023"];
        return JsonElementContains(
                originalBacklog.GetProperty("requiredMarkers"),
                "children=303 issues=319") &&
            JsonElementContains(
                originalBacklog.GetProperty("acceptanceCriteria"),
                "16-milestone/319-issue manifest") &&
            !JsonElementContains(
                originalBacklog.GetProperty("requiredMarkers"),
                "issues=331") &&
            DependenciesEqual(repair, "M00-022") &&
            JsonElementContains(
                repair.GetProperty("requiredMarkers"),
                "children=315 issues=331") &&
            JsonElementContains(
                repair.GetProperty("requiredMarkers"),
                "SYNC_VERIFY_PASS milestones=16 labels=68 issues=331") &&
            JsonElementContains(
                repair.GetProperty("requiredCommands"),
                "sync-gitea-backlog.ps1 -Apply") &&
            CountExact(
                repair.GetProperty("requiredCommands"),
                "powershell -NoProfile -ExecutionPolicy Bypass -File .\\scripts\\sync-gitea-backlog.ps1") == 2 &&
            JsonElementContains(
                repair.GetProperty("acceptanceCriteria"),
                "final no-write preview reports all 415 managed objects unchanged") &&
            JsonElementContains(
                repair.GetProperty("acceptanceCriteria"),
                "guarded pre-apply no-write preview reports exactly wouldCreate=11 and wouldUpdate=24") &&
            JsonElementContains(
                repair.GetProperty("implementationNotes"),
                "M03-007 gains the post-ExitBootServices permanent-CR3 handoff contract") &&
            JsonElementContains(
                repair.GetProperty("implementationNotes"),
                "M03-EPIC is consequently updated") &&
            JsonElementContains(
                repair.GetProperty("acceptanceCriteria"),
                "creates exactly 11 issues, updates exactly 24 existing issues") &&
            string.Equals(
                repair.GetProperty("desiredState").GetString(),
                "closed",
                StringComparison.Ordinal) &&
            string.Equals(
                issueByKey["M00-EPIC"].GetProperty("desiredState").GetString(),
                "closed",
                StringComparison.Ordinal) &&
            HasOwnedPath(
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
        for (int number = 1; number <= 23; number++)
        {
            string requiredKey = "M00-" + number.ToString("000");
            if (!DependsTransitively(issueByKey, "M01-001", requiredKey))
            {
                return false;
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
            .ToArray();
        string[] actualOrder = issues.EnumerateArray()
            .Select(issue => issue.GetProperty("key").GetString() ?? string.Empty)
            .Where(key =>
                key.StartsWith("M01-", StringComparison.Ordinal) &&
                !key.EndsWith("-EPIC", StringComparison.Ordinal))
            .ToArray();
        if (!actualOrder.SequenceEqual(expectedOrder, StringComparer.Ordinal))
        {
            return false;
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
                return false;
            }

            if (!OwnedPathsEqual(issue, expectedLeafPaths[key]))
            {
                return false;
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
            return false;
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
            return false;
        }

        return ValidateM01IntegrationContractsV2(issueByKey);
    }

    private static bool ValidateM01ReferenceLabels(
        IReadOnlyDictionary<string, JsonElement> issueByKey)
    {
        for (int number = 1; number <= 35; number++)
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
                return false;
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
                    "scripts/build-efi-shell.ps1",
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
                    "scripts/run-qemu-smoke.ps1",
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
                    "scripts/run-qemu-smoke.ps1",
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
                    "scripts/run-qemu-smoke.ps1",
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
                return false;
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
            return false;
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
                return false;
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
                return false;
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
            return false;
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
            return false;
        }

        return ValidateM01PrerequisiteRepairContracts(issueByKey) &&
            ValidateM01InstructionPublicationContracts(issueByKey) &&
            efiIssues.All(key =>
                JsonElementContains(
                    issueByKey[key].GetProperty("requiredCommands"),
                    "build-efi-shell.ps1") &&
                !JsonElementContains(
                    issueByKey[key].GetProperty("requiredCommands"),
                    "run-qemu-smoke.ps1")) &&
            HasOwnedPath(issueByKey["M01-026"], "docs/completion-gates.md") &&
            JsonElementContains(
                issueByKey["M01-026"].GetProperty("acceptanceCriteria"),
                "M02-014") &&
            DependenciesEqual(issueByKey["M02-EPIC"], "M01-026") &&
            DependenciesEqual(issueByKey["M02-001"], "M01-026") &&
            DependenciesEqual(issueByKey["M04-EPIC"], "M01-026") &&
            DependenciesEqual(issueByKey["M04-001"], "M01-026");
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
            return false;
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
                return false;
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
                "scripts/sync-gitea-backlog.ps1") ||
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
            return false;
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
                    return false;
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
            return false;
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
                    "open",
                    StringComparison.Ordinal) ||
                !JsonArrayContainsExact(labels, "agent:blocked") ||
                CountPrefix(labels, "agent:") != 1 ||
                !JsonArrayContainsExact(labels, "priority:p0") ||
                !JsonArrayContainsExact(labels, "evidence:host") ||
                !JsonArrayContainsExact(labels, "phase:m01-memory") ||
                !JsonArrayContainsExact(labels, types[key]) ||
                CountPrefix(labels, "type:") != 1 ||
                !laneContractSatisfied ||
                commands.Contains("run-qemu-smoke.ps1", StringComparison.Ordinal) ||
                commands.Contains(" -Qemu \"", StringComparison.Ordinal) ||
                commands.Contains(" -OvmfCode \"", StringComparison.Ordinal))
            {
                return false;
            }
        }

        JsonElement generator = issueByKey["M01-033"];
        JsonElement integration = issueByKey["M01-034"];
        JsonElement publication = issueByKey["M01-035"];
        if (!OwnedPathsEqual(
                generator,
                "tools/OS.X64TextSerializerGenerator/OS.X64TextSerializerGenerator.csproj",
                "tools/OS.X64TextSerializerGenerator/Program.cs",
                "tools/OS.X64TextSerializerGenerator/Amd64CoffWriter.cs",
                "tools/OS.X64TextSerializerGenerator/InstructionPublicationSerializerGenerator.cs",
                "tests/OS.HostTests/M01InstructionPublicationSerializerGeneratorTests.cs",
                "docs/security.md") ||
            !OwnedPathsEqual(
                integration,
                "CLROS.slnx",
                "src/OS/OS.csproj",
                "src/OS/Boot/X64InstructionPublication.cs",
                "src/OS/Runtime/RuntimeImportAttribute.cs",
                "tools/OS.X64TextSerializerGenerator/OS.X64TextSerializerGenerator.targets",
                "scripts/test-efi-artifact.ps1",
                "tests/OS.HostTests/M01InstructionPublicationBuildIntegrationTests.cs",
                "tests/OS.HostTests/EfiArtifactVerificationScriptTests.cs",
                "docs/efi-build.md",
                "docs/nativeaot-nostdlib-limits.md") ||
            !OwnedPathsEqual(
                publication,
                "src/OS/Boot/EfiEntry.cs",
                "tests/OS.HostTests/M01InstructionPublicationEfiEntryTests.cs",
                "docs/boot.md",
                "docs/architecture.md",
                "docs/security.md"))
        {
            return false;
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
                integration.GetProperty("forbiddenPaths"),
                "src/OS/Boot/EfiEntry.cs") ||
            !JsonElementContains(
                integration.GetProperty("forbiddenPaths"),
                "InstructionPublicationSerializerGenerator.cs") ||
            !JsonElementContains(
                publication.GetProperty("forbiddenPaths"),
                "src/OS/OS.csproj") ||
            !JsonElementContains(
                publication.GetProperty("forbiddenPaths"),
                "tools/OS.X64TextSerializerGenerator/**") ||
            !JsonElementContains(
                publication.GetProperty("forbiddenPaths"),
                "scripts/test-efi-artifact.ps1"))
        {
            return false;
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
            "9C 53 B8 00 00 00 00 B9 00 00 00 00 0F A2 5B 9D C3";
        string[] generatorTerms =
        [
            "AMD64 COFF machine 0x8664",
            ".text$clrossm",
            "CLROS_X64_SerializeInstructionPublication",
            exactBytes,
            "timestamp zero",
            "IMAGE_SCN_MEM_WRITE clear",
            "no relocations",
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
            "/INCLUDE:CLROS_X64_SerializeInstructionPublication",
            "same-build map",
            "file-backed",
            "RX/nonwritable",
            exactBytes,
            "absent import",
            "must not invoke",
            "build timeout",
            "no serializer-execution"
        ];
        string[] publicationTerms =
        [
            "four relevant epochs",
            "Epoch 1",
            "Epoch 2",
            "Epoch 3",
            "Epoch 4",
            "M01_PRE_CR3",
            "No executable-page write",
            "LFENCE",
            "MFENCE",
            "firmware callback",
            "zero/default",
            "overflow",
            "timeout",
            "do not run QEMU",
            "cross-core"
        ];
        if (generatorTerms.Any(term =>
                !generatorContract.Contains(term, StringComparison.Ordinal)) ||
            integrationTerms.Any(term =>
                !integrationContract.Contains(term, StringComparison.Ordinal)) ||
            publicationTerms.Any(term =>
                !publicationContract.Contains(term, StringComparison.Ordinal)))
        {
            return false;
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
            return false;
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
        return epicContract.Contains(
                "M01-033 generates one deterministic C# AMD64 COFF serializer",
                StringComparison.Ordinal) &&
            epicContract.Contains(
                "All 35 keyed child issues",
                StringComparison.Ordinal) &&
            qemuContract.Contains(
                "M01-025 and then M01-033, M01-034, and M01-035",
                StringComparison.Ordinal) &&
            qemuContract.Contains(
                "exactly four publication-epoch calls",
                StringComparison.Ordinal) &&
            qemuContract.Contains(
                "QEMU/TCG",
                StringComparison.Ordinal) &&
            qemuContract.Contains(
                "does not empirically prove absence of speculative code-store bypass",
                StringComparison.Ordinal) &&
            qemuContract.Contains(
                "does not prove cross-core instruction publication",
                StringComparison.Ordinal) &&
            JsonElementContains(
                integration.GetProperty("requiredCommands"),
                "build-efi-shell.ps1") &&
            JsonElementContains(
                publication.GetProperty("requiredCommands"),
                "build-efi-shell.ps1") &&
            JsonElementContains(
                integration.GetProperty("requiredCommands"),
                "test-efi-artifact.ps1") &&
            JsonElementContains(
                publication.GetProperty("requiredCommands"),
                "test-efi-artifact.ps1");
    }

    private static bool ValidateM03PermanentCr3Handoff(
        IReadOnlyDictionary<string, JsonElement> issueByKey)
    {
        JsonElement issue = issueByKey["M03-007"];
        string notes = JsonArrayText(issue.GetProperty("implementationNotes"));
        string acceptance = JsonArrayText(issue.GetProperty("acceptanceCriteria"));
        string commands = JsonArrayText(issue.GetProperty("requiredCommands"));
        string evidence = issue.GetProperty("evidenceBoundary").GetString() ?? string.Empty;
        return DependenciesEqual(issue, "M01-026", "M02-016", "M03-006") &&
            string.Equals(
                issue.GetProperty("collisionLane").GetString(),
                "lane:efi-entry",
                StringComparison.Ordinal) &&
            JsonArrayContainsExact(issue.GetProperty("labels"), "lane:efi-entry") &&
            HasOwnedPath(issue, "src/OS/Boot/EfiEntry.cs") &&
            HasOwnedPath(
                issue,
                "tests/OS.HostTests/M03PermanentCr3AfterExitEfiEntryTests.cs") &&
            notes.Contains(
                "Permanent owned-CR3 adoption before a successful ExitBootServices return is forbidden",
                StringComparison.Ordinal) &&
            notes.Contains(
                "make no firmware call ever again",
                StringComparison.Ordinal) &&
            notes.Contains(
                "M03_PERMANENT_CR3_ADOPTED",
                StringComparison.Ordinal) &&
            acceptance.Contains(
                "no permanent adoption occurs before successful EBS",
                StringComparison.Ordinal) &&
            commands.Contains(
                "M03_EBS_RETURNED_SUCCESS,M03_PERMANENT_CR3_ADOPTED,M03_POST_EBS_OWNED_DIAGNOSTIC",
                StringComparison.Ordinal) &&
            commands.Contains(
                "M03_FIRMWARE_CALL_AFTER_EBS",
                StringComparison.Ordinal) &&
            evidence.Contains(
                "adopted permanently before owned post-EBS diagnostics",
                StringComparison.Ordinal) &&
            evidence.Contains("M03-008", StringComparison.Ordinal);
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
                    "sync-gitea-backlog.ps1 -Apply"))
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
            return false;
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
                return false;
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
        byte[] before = SHA256.HashData(File.ReadAllBytes(manifestPath));
        DateTime beforeWriteTime = File.GetLastWriteTimeUtc(manifestPath);

        ProcessResult result = RunScript(scriptPath, "-Offline");

        byte[] after = SHA256.HashData(File.ReadAllBytes(manifestPath));
        DateTime afterWriteTime = File.GetLastWriteTimeUtc(manifestPath);
        return result.ExitCode == 0 &&
            result.Output.Contains("SYNC_MANIFEST_VALID milestones=16 labels=68 epics=16 children=324 issues=340", StringComparison.Ordinal) &&
            result.Output.Contains("SYNC_MODE preview", StringComparison.Ordinal) &&
            result.Output.Contains("SYNC_OFFLINE manifest_only", StringComparison.Ordinal) &&
            result.Output.Contains("SYNC_KEYS action=skipped count=424", StringComparison.Ordinal) &&
            result.Output.Contains("SYNC_SUMMARY created=0 updated=0 unchanged=0 skipped=424 failed=0 wouldCreate=0 wouldUpdate=0", StringComparison.Ordinal) &&
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

    private static void ReplaceLastM01ChildWithExtra(JsonObject root)
    {
        JsonObject child = FindIssue(root, "M01-035");
        child["key"] = "M01-999";

        JsonObject dependent = FindIssue(root, "M01-026");
        JsonArray dependencyKeys = dependent["dependencyKeys"]?.AsArray()
            ?? throw new InvalidOperationException("M01-026 dependencyKeys missing");
        for (int index = 0; index < dependencyKeys.Count; index++)
        {
            if (dependencyKeys[index]?.GetValue<string>() == "M01-035")
            {
                dependencyKeys[index] = "M01-999";
                return;
            }
        }

        throw new InvalidOperationException("M01-026 dependency on M01-035 missing");
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
