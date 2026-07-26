using System.Buffers.Binary;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using OS.Boot;
using OS.Kernel.Memory;

internal static class BootPageAllocatorStressTests
{
    private const string IssueTestName = "m01.010";
    private const string ReadyMarker = "CLROS_M01_010_READY";
    private const ulong PageSize = 4096;
    private const ulong CanonicalSeed = 0x6A09_E667_F3BC_C909;
    private const int CanonicalChurnSteps = 1024;
    private const int CanonicalTranscriptRecordCount = 1278;
    private const int CanonicalSuccessfulAllocations = 549;
    private const int CanonicalSuccessfulFrees = 549;
    private const int CanonicalExpectedAllocationFailures = 178;
    private const string CanonicalTranscriptHash =
        "12F1D263FC34D423E628574705A22F426BF43BACBAA38F53BEF886F5AB5D8D96";
    private const int SeedMatrixChurnSteps = 384;
    private const int MaximumReplayDepth = 24;
    private const int AllocationWeight = 65;
    private const int FreeWeight = 35;

    private static ReplaySummary s_canonicalSummary;

    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Console.WriteLine("TEST_START " + IssueTestName);

        Run("m01.010 01 fixed seed replay hashes are identical", TestFixedSeedReplay, ref failures);
        Run("m01.010 02 seed matrix churn matches shadow state", TestSeedMatrixChurn, ref failures);
        Run("m01.010 03 scripted restoration shapes are complete", TestScriptedRestorationShapes, ref failures);
        Run("m01.010 04 exhaustion and boundary pages are exact", TestExhaustionAndBoundaryPages, ref failures);
        Run("m01.010 05 default and empty setup fail atomically", TestDefaultAndEmptySetup, ref failures);
        Run("m01.010 06 request and high boundary failures are atomic", TestRequestAndBoundaryFailures, ref failures);
        Run("m01.010 07 stale ownership and replay fail atomically", TestOwnershipAndReplayFailures, ref failures);
        Run("m01.010 08 metadata capacity and corruption fail atomically", TestMetadataCapacityAndCorruption, ref failures);
        Run("m01.010 09 shadow ownership and content corruption are detected", TestShadowCorruptionDetection, ref failures);
        Run("m01.010 10 reservations tails and exact snapshots survive churn", TestStoragePreservation, ref failures);
        Run("m01.010 11 stress source remains host only", TestHostOnlyBoundary, ref failures);
        Run("m01.010 12 readiness markers are unique ordered and host only", TestMarkerBoundary, ref failures);

        if (failures != 0)
        {
            Console.WriteLine("TEST_FAIL " + IssueTestName + " failures=" + failures);
            throw new InvalidOperationException(
                "M01-010 deterministic allocator stress tests failed: "
                + failures);
        }

        Console.WriteLine(
            "M01_010_REPLAY seed=0x"
            + CanonicalSeed.ToString("X16", CultureInfo.InvariantCulture)
            + " churn="
            + CanonicalChurnSteps.ToString(CultureInfo.InvariantCulture)
            + " records="
            + s_canonicalSummary.OperationCount.ToString(
                CultureInfo.InvariantCulture)
            + " allocations="
            + s_canonicalSummary.SuccessfulAllocations.ToString(
                CultureInfo.InvariantCulture)
            + " frees="
            + s_canonicalSummary.SuccessfulFrees.ToString(
                CultureInfo.InvariantCulture)
            + " expected_failures="
            + s_canonicalSummary.ExpectedAllocationFailures.ToString(
                CultureInfo.InvariantCulture)
            + " hash="
            + s_canonicalSummary.TranscriptHash);
        Console.WriteLine(ReadyMarker);
        Console.WriteLine("TEST_PASS " + IssueTestName);
    }

    private static void Run(string name, Func<bool> test, ref int failures)
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
            Console.WriteLine("TEST_FAIL " + name);
        }
        catch (Exception ex)
        {
            failures++;
            Console.WriteLine(
                "TEST_FAIL "
                + name
                + " "
                + ex.GetType().Name
                + ": "
                + ex.Message);
        }
    }

    private static bool TestFixedSeedReplay()
    {
        ReplaySummary first = RunReplay(
            CanonicalSeed,
            CanonicalChurnSteps,
            streamId: 1);
        ReplaySummary second = RunReplay(
            CanonicalSeed,
            CanonicalChurnSteps,
            streamId: 1);
        ReplaySummary different = RunReplay(
            0xBB67_AE85_84CA_A73B,
            CanonicalChurnSteps,
            streamId: 1);

        s_canonicalSummary = first;
        return first.Equals(second)
            && first.TranscriptHash.Length == 64
            && string.Equals(
                first.TranscriptHash,
                CanonicalTranscriptHash,
                StringComparison.Ordinal)
            && different.TranscriptHash.Length == 64
            && !string.Equals(
                first.TranscriptHash,
                different.TranscriptHash,
                StringComparison.Ordinal)
            && first.OperationCount
                == CanonicalTranscriptRecordCount
            && first.SuccessfulAllocations
                == CanonicalSuccessfulAllocations
            && first.SuccessfulFrees
                == CanonicalSuccessfulFrees
            && first.ExpectedAllocationFailures
                == CanonicalExpectedAllocationFailures
            && first.ReturnedToBaseline
            && first.ExactRestorationCount == first.SuccessfulFrees
            && first.SuccessfulAllocations > CanonicalChurnSteps / 2
            && first.SuccessfulFrees > CanonicalChurnSteps / 3;
    }

    private static bool TestSeedMatrixChurn()
    {
        ulong[] seeds =
        [
            0x3C6E_F372_FE94_F82B,
            0xA54F_F53A_5F1D_36F1,
            0x510E_527F_ADE6_82D1
        ];
        string[] expectedHashes =
        [
            "A74D2B2234A669DE8FDD7A478A3D12BF6601378E3557961B4EE1ADAAD20DB4D7",
            "83AD88944AC8DB83A235E41A981F99EE02B652BC8DB2D596A0CF8AF585FC88B9",
            "767149D6F2EEC88CDC524E96C5C2F2E2024B92B90E1D92A200C353E1EB1231C8"
        ];
        HashSet<string> hashes = new(StringComparer.Ordinal);
        int allocations = 0;
        int frees = 0;
        int allocationFailures = 0;

        for (int index = 0; index < seeds.Length; index++)
        {
            ReplaySummary summary = RunReplay(
                seeds[index],
                SeedMatrixChurnSteps,
                streamId: index + 2);
            if (!summary.ReturnedToBaseline
                || !summary.FirstBoundaryPageSeen
                || !summary.LastBoundaryPageSeen
                || !summary.CapacityTailPreserved
                || !summary.ReservationsPreserved
                || summary.ExactRestorationCount
                    != summary.SuccessfulFrees
                || !string.Equals(
                    summary.TranscriptHash,
                    expectedHashes[index],
                    StringComparison.Ordinal))
            {
                return false;
            }

            hashes.Add(summary.TranscriptHash);
            allocations += summary.SuccessfulAllocations;
            frees += summary.SuccessfulFrees;
            allocationFailures += summary.ExpectedAllocationFailures;
        }

        return hashes.Count == seeds.Length
            && allocations > seeds.Length * SeedMatrixChurnSteps / 2
            && frees > seeds.Length * SeedMatrixChurnSteps / 3
            && allocationFailures >= seeds.Length;
    }

    private static bool TestScriptedRestorationShapes()
    {
        return RunShape(
                baseAddress: 0x0040_0000,
                totalPages: 1,
                regionStartPage: 0,
                regionPageCount: 1,
                alignment: PageSize,
                BootPageAllocatorOnePageCoalescingKind.None)
            && RunShape(
                baseAddress: 0x0050_0000,
                totalPages: 4,
                regionStartPage: 0,
                regionPageCount: 4,
                alignment: PageSize,
                BootPageAllocatorOnePageCoalescingKind.Next)
            && RunShape(
                baseAddress: 0x0060_0000,
                totalPages: 5,
                regionStartPage: 1,
                regionPageCount: 4,
                alignment: 4 * PageSize,
                BootPageAllocatorOnePageCoalescingKind.Previous)
            && RunShape(
                baseAddress: 0x0070_0000,
                totalPages: 8,
                regionStartPage: 1,
                regionPageCount: 7,
                alignment: 4 * PageSize,
                BootPageAllocatorOnePageCoalescingKind
                    .PreviousAndNext);
    }

    private static bool TestExhaustionAndBoundaryPages()
    {
        ReplaySummary summary = RunReplay(
            0x9B05_688C_2B3E_6C1F,
            SeedMatrixChurnSteps,
            streamId: 9);
        return summary.FirstBoundaryPageSeen
            && summary.LastBoundaryPageSeen
            && summary.ExpectedAllocationFailures >= 1
            && summary.MaximumDepth >= 100
            && summary.ReturnedToBaseline;
    }

    private static unsafe bool TestDefaultAndEmptySetup()
    {
        BootPageAllocatorOnePageResult defaultStateResult =
            BootPageAllocatorMutator.AllocateOnePage(
                default,
                null,
                0,
                0,
                null,
                0,
                0,
                1,
                PageSize);
        BootPageAllocatorState emptyState = new(
            isInitialized: true,
            PageSize,
            headNodeIndex: -1,
            freeNodeCount: 0,
            freePageCount: 0,
            freeByteCount: 0,
            allocatedPageCount: 0,
            allocatedByteCount: 0);
        BootPageAllocatorOnePageResult emptyResult =
            BootPageAllocatorMutator.AllocateOnePage(
                emptyState,
                null,
                0,
                0,
                null,
                0,
                0,
                1,
                PageSize);
        BootPageAllocatorOnePageFreeResult defaultFree =
            BootPageAllocatorMutator.FreeOnePage(
                emptyState,
                null,
                0,
                0,
                null,
                0,
                0,
                default);

        return IsAllocationFailure(
                defaultStateResult,
                default,
                BootPageAllocatorMutationStatus.StateNotInitialized)
            && IsAllocationFailure(
                emptyResult,
                emptyState,
                BootPageAllocatorMutationStatus.NoSuitableRange)
            && IsFreeFailure(
                defaultFree,
                emptyState,
                default,
                BootPageAllocatorMutationStatus.FreeRangeNotAllocated)
            && !default(BootPageAllocatorOnePageResult).Succeeded
            && !default(BootPageAllocatorOnePageFreeResult).Succeeded;
    }

    private static bool TestRequestAndBoundaryFailures()
    {
        BootPageFreeListNode[] nodes =
        [
            Node(0, 0, 0x0010_0000, 4, -1),
            default,
            CapacityTailCanary(90)
        ];
        BootPageAllocatorState state = State(0, 1, 4, 0);

        if (!AllocationFailurePreserves(
                state,
                nodes,
                nodeCount: 2,
                nodeCapacity: 3,
                [],
                0,
                0,
                pageCount: 0,
                PageSize,
                BootPageAllocatorMutationStatus.InvalidPageCount)
            || !AllocationFailurePreserves(
                state,
                nodes,
                2,
                3,
                [],
                0,
                0,
                pageCount: 2,
                PageSize,
                BootPageAllocatorMutationStatus.InvalidPageCount)
            || !AllocationFailurePreserves(
                state,
                nodes,
                2,
                3,
                [],
                0,
                0,
                pageCount: 1,
                alignment: 0,
                BootPageAllocatorMutationStatus.InvalidAlignment)
            || !AllocationFailurePreserves(
                state,
                nodes,
                2,
                3,
                [],
                0,
                0,
                1,
                PageSize / 2,
                BootPageAllocatorMutationStatus.InvalidAlignment)
            || !AllocationFailurePreserves(
                state,
                nodes,
                2,
                3,
                [],
                0,
                0,
                1,
                3 * PageSize,
                BootPageAllocatorMutationStatus.InvalidAlignment))
        {
            return false;
        }

        BootPageFreeListNode[] highNodes =
        [
            Node(
                0,
                0,
                0xFFFF_FFFF_FFFF_E000,
                1,
                -1),
            default
        ];
        return HighBoundaryRoundTrip()
            && AllocationFailurePreserves(
            State(0, 1, 1, 0),
            highNodes,
            nodeCount: 1,
            nodeCapacity: 2,
            [],
            0,
            0,
            1,
            4 * PageSize,
            BootPageAllocatorMutationStatus.AllocationAddressOverflow,
            AddressValueStatus.AddressOverflow);
    }

    private static bool TestOwnershipAndReplayFailures()
    {
        StressArena arena = StressArena.CreateSingleRegion(
            baseAddress: 0x0080_0000,
            totalPages: 8,
            regionStartPage: 0,
            regionPageCount: 8,
            logicalNodeCount: 8,
            nodeCapacity: 10);
        OperationOutcome first = arena.Allocate(
            PageSize,
            contentTag: 0x1111,
            step: 1);
        OperationOutcome second = arena.Allocate(
            PageSize,
            contentTag: 0x2222,
            step: 2);
        BootPageAllocatorOnePageResult firstReceipt =
            arena.GetAllocationResultFromBottom(0);
        BootPageAllocatorOnePageResult secondReceipt =
            arena.GetAllocationResultFromBottom(1);

        if (!first.Succeeded
            || !second.Succeeded
            || !arena.ExpectFreeFailure(
                firstReceipt,
                BootPageAllocatorMutationStatus.FreeRangeNotAllocated,
                step: 3))
        {
            return false;
        }

        StressArena foreignArena = StressArena.CreateSingleRegion(
            baseAddress: 0x00A0_0000,
            totalPages: 8,
            regionStartPage: 0,
            regionPageCount: 8,
            logicalNodeCount: 8,
            nodeCapacity: 10);
        OperationOutcome foreignFirst = foreignArena.Allocate(
            PageSize,
            contentTag: 0x3333,
            step: 4);
        OperationOutcome foreignSecond = foreignArena.Allocate(
            PageSize,
            contentTag: 0x4444,
            step: 5);
        BootPageAllocatorOnePageResult foreignReceipt =
            foreignArena.GetAllocationResultFromBottom(1);
        if (!foreignFirst.Succeeded
            || !foreignSecond.Succeeded
            || !StatesEqual(
                secondReceipt.AfterState,
                foreignReceipt.AfterState)
            || secondReceipt.NodeCount != foreignReceipt.NodeCount
            || secondReceipt.NodeCapacity
                != foreignReceipt.NodeCapacity
            || secondReceipt.ReservationCount
                != foreignReceipt.ReservationCount
            || secondReceipt.ReservationCapacity
                != foreignReceipt.ReservationCapacity
            || !arena.ExpectFreeFailure(
                foreignReceipt,
                BootPageAllocatorMutationStatus.FreeRangeNotAllocated,
                step: 6))
        {
            return false;
        }

        OperationOutcome freeSecond = arena.FreeTop(step: 7);
        OperationOutcome freeFirst = arena.FreeTop(step: 8);
        OperationOutcome freeForeignSecond =
            foreignArena.FreeTop(step: 9);
        OperationOutcome freeForeignFirst =
            foreignArena.FreeTop(step: 10);
        return freeSecond.Succeeded
            && freeFirst.Succeeded
            && freeForeignSecond.Succeeded
            && freeForeignFirst.Succeeded
            && arena.ExpectFreeFailure(
                firstReceipt,
                BootPageAllocatorMutationStatus.FreeRangeNotAllocated,
                step: 11)
            && arena.ExpectFreeFailure(
                secondReceipt,
                BootPageAllocatorMutationStatus.FreeRangeNotAllocated,
                step: 12)
            && arena.IsAtBaseline
            && foreignArena.IsAtBaseline;
    }

    private static bool TestMetadataCapacityAndCorruption()
    {
        BootPageFreeListNode[] noLogicalSplitSlot =
        [
            Node(0, 0, 0x0010_1000, 8, -1),
            default,
            CapacityTailCanary(77)
        ];
        if (!AllocationFailurePreserves(
                State(0, 1, 8, 0),
                noLogicalSplitSlot,
                nodeCount: 1,
                nodeCapacity: 3,
                [],
                0,
                0,
                1,
                4 * PageSize,
                BootPageAllocatorMutationStatus.MetadataCapacityTooSmall))
        {
            return false;
        }

        BootPageFreeListNode[] valid =
        [
            Node(0, 0, 0x0020_0000, 4, -1),
            default
        ];
        BootPageAllocatorState mismatchedTotals = new(
            isInitialized: true,
            PageSize,
            headNodeIndex: 0,
            freeNodeCount: 1,
            freePageCount: 5,
            freeByteCount: 5 * PageSize,
            allocatedPageCount: 0,
            allocatedByteCount: 0);
        if (!AllocationFailurePreserves(
                mismatchedTotals,
                valid,
                2,
                2,
                [],
                0,
                0,
                1,
                PageSize,
                BootPageAllocatorMutationStatus.TotalPageCountMismatch))
        {
            return false;
        }

        BootPageFreeListNode[] malformed =
        [
            new BootPageFreeListNode(
                0,
                0,
                0x0030_0000,
                0x0030_4000,
                pageCount: 3,
                byteLength: 4 * PageSize,
                nextNodeIndex: -1),
            default
        ];
        return AllocationFailurePreserves(
            State(0, 1, 4, 0),
            malformed,
            2,
            2,
            [],
            0,
            0,
            1,
            PageSize,
            BootPageAllocatorMutationStatus.InvalidNodeShape);
    }

    private static bool TestShadowCorruptionDetection()
    {
        StressArena arena = StressArena.CreateSingleRegion(
            baseAddress: 0x0090_0000,
            totalPages: 12,
            regionStartPage: 0,
            regionPageCount: 12,
            logicalNodeCount: 8,
            nodeCapacity: 10);
        arena.Allocate(PageSize, 0xA5A5_A5A5_A5A5_A5A5, step: 1);
        arena.Allocate(2 * PageSize, 0x5A5A_5A5A_5A5A_5A5A, step: 2);

        if (!arena.Validate(out _)
            || !arena.CorruptActualContentAndDetect(out string contentReason)
            || !contentReason.StartsWith(
                "content_mismatch:",
                StringComparison.Ordinal)
            || !arena.CorruptShadowOwnershipAndDetect(
                out string ownershipReason)
            || !ownershipReason.StartsWith(
                "ownership_mismatch:",
                StringComparison.Ordinal)
            || !arena.Validate(out _))
        {
            return false;
        }

        arena.FreeTop(step: 3);
        arena.FreeTop(step: 4);
        return arena.IsAtBaseline;
    }

    private static bool TestStoragePreservation()
    {
        ReplaySummary summary = RunReplay(
            0x1F83_D9AB_FB41_BD6B,
            SeedMatrixChurnSteps,
            streamId: 10);
        return summary.CapacityTailPreserved
            && summary.ReservationsPreserved
            && summary.ExactRestorationCount
                == summary.SuccessfulFrees
            && summary.ReturnedToBaseline;
    }

    private static bool TestHostOnlyBoundary()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string testPath = Path.Combine(
            root,
            "tests",
            "OS.HostTests",
            "BootPageAllocatorStressTests.cs");
        string efiProjectPath = Path.Combine(
            root,
            "src",
            "OS",
            "OS.csproj");
        string kernelProjectPath = Path.Combine(
            root,
            "src",
            "OS.Kernel",
            "OS.Kernel.csproj");
        string hostProjectPath = Path.Combine(
            root,
            "tests",
            "OS.HostTests",
            "OS.HostTests.csproj");
        if (!File.Exists(testPath)
            || !File.Exists(efiProjectPath)
            || !File.Exists(kernelProjectPath)
            || !File.Exists(hostProjectPath))
        {
            return false;
        }

        string efiProject = File.ReadAllText(efiProjectPath);
        string kernelProject = File.ReadAllText(kernelProjectPath);
        string hostProject = File.ReadAllText(hostProjectPath);
        string testFileName = Path.GetFileName(testPath);
        return Path.GetFullPath(testPath).StartsWith(
                Path.GetFullPath(
                    Path.Combine(root, "tests", "OS.HostTests"))
                    + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase)
            && efiProject.Contains(
                "<EnableDefaultCompileItems>false</EnableDefaultCompileItems>",
                StringComparison.Ordinal)
            && !efiProject.Contains(
                testFileName,
                StringComparison.OrdinalIgnoreCase)
            && !efiProject.Contains(
                "OS.HostTests",
                StringComparison.OrdinalIgnoreCase)
            && !kernelProject.Contains(
                testFileName,
                StringComparison.OrdinalIgnoreCase)
            && !kernelProject.Contains(
                "OS.HostTests",
                StringComparison.OrdinalIgnoreCase)
            && hostProject.Contains(
                @"..\..\src\OS.Kernel\OS.Kernel.csproj",
                StringComparison.OrdinalIgnoreCase)
            && EvaluatedEfiClosureExcludesStressSource(
                root,
                efiProjectPath,
                testPath,
                "SingleMinimalApi")
            && EvaluatedEfiClosureExcludesStressSource(
                root,
                efiProjectPath,
                testPath,
                "MultiMinimalApi")
            && !BootPageAllocatorMutator.PerformsLiveAllocation
            && !BootPageAllocatorMutator.PerformsLiveFree;
    }

    private static bool EvaluatedEfiClosureExcludesStressSource(
        string repositoryRoot,
        string projectPath,
        string stressSourcePath,
        string workloadProfile)
    {
        ProcessStartInfo start = new("dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = repositoryRoot
        };
        start.ArgumentList.Add("msbuild");
        start.ArgumentList.Add(projectPath);
        start.ArgumentList.Add("-nologo");
        start.ArgumentList.Add(
            "-getItem:Compile,ProjectReference,Reference");
        start.ArgumentList.Add("-p:Configuration=Release");
        start.ArgumentList.Add(
            "-p:OSWorkloadProfile=" + workloadProfile);
        start.ArgumentList.Add("-p:RestoreIgnoreFailedSources=true");
        start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        start.Environment["DOTNET_NOLOGO"] = "1";
        start.Environment["DOTNET_CLI_UI_LANGUAGE"] = "en-US";
        start.Environment["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1";
        start.Environment["NUGET_XMLDOC_MODE"] = "skip";
        start.Environment["MSBUILDDISABLENODEREUSE"] = "1";

        using Process process = new() { StartInfo = start };
        try
        {
            if (!process.Start())
            {
                return EfiClosureFailure(
                    workloadProfile,
                    "process_start");
            }
        }
        catch
        {
            return EfiClosureFailure(
                workloadProfile,
                "process_start");
        }

        Task<string> standardOutput;
        Task<string> standardError;
        try
        {
            standardOutput =
                process.StandardOutput.ReadToEndAsync();
            standardError =
                process.StandardError.ReadToEndAsync();
        }
        catch
        {
            if (!TryTerminateProcess(process))
            {
                return EfiClosureFailure(
                    workloadProfile,
                    "output_start_kill_failed");
            }

            return EfiClosureFailure(
                workloadProfile,
                "output_start");
        }

        bool exited;
        try
        {
            exited = process.WaitForExit(60_000);
        }
        catch
        {
            if (!TryTerminateProcess(process))
            {
                return EfiClosureFailure(
                    workloadProfile,
                    "process_wait_kill_failed");
            }

            return EfiClosureFailure(
                workloadProfile,
                "process_wait");
        }

        if (!exited)
        {
            if (!TryTerminateProcess(process))
            {
                return EfiClosureFailure(
                    workloadProfile,
                    "timeout_kill_failed");
            }

            if (!TryReadProcessOutput(
                    standardOutput,
                    standardError,
                    out _))
            {
                return EfiClosureFailure(
                    workloadProfile,
                    "timeout_output_drain");
            }

            return EfiClosureFailure(
                workloadProfile,
                "timeout");
        }

        if (!TryReadProcessOutput(
                standardOutput,
                standardError,
                out string output))
        {
            return EfiClosureFailure(
                workloadProfile,
                "output_drain");
        }

        int exitCode;
        try
        {
            exitCode = process.ExitCode;
        }
        catch
        {
            return EfiClosureFailure(
                workloadProfile,
                "exit_status");
        }

        if (exitCode != 0)
        {
            return EfiClosureFailure(
                workloadProfile,
                "exit_nonzero",
                exitCode);
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(output);
            if (document.RootElement.ValueKind
                    != JsonValueKind.Object
                || !document.RootElement.TryGetProperty(
                    "Items",
                    out JsonElement items)
                || items.ValueKind != JsonValueKind.Object
                || !items.TryGetProperty(
                    "Compile",
                    out JsonElement compileItems)
                || compileItems.ValueKind != JsonValueKind.Array
                || compileItems.GetArrayLength() == 0
                || !items.TryGetProperty(
                    "ProjectReference",
                    out JsonElement projectReferences)
                || projectReferences.ValueKind != JsonValueKind.Array
                || !items.TryGetProperty(
                    "Reference",
                    out JsonElement references)
                || references.ValueKind != JsonValueKind.Array
                || projectReferences.GetArrayLength() != 0
                || references.GetArrayLength() != 0)
            {
                return EfiClosureFailure(
                    workloadProfile,
                    "json_shape");
            }

            string canonicalStressSource =
                Path.GetFullPath(stressSourcePath);
            string hostTestDirectory = Path.GetFullPath(
                    Path.Combine(
                        repositoryRoot,
                        "tests",
                        "OS.HostTests"))
                .TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            string expectedEfiEntry = Path.GetFullPath(
                Path.Combine(
                    repositoryRoot,
                    "src",
                    "OS",
                    "Boot",
                    "EfiEntry.cs"));
            string expectedMinimalRuntime = Path.GetFullPath(
                Path.Combine(
                    repositoryRoot,
                    "src",
                    "OS",
                    "Runtime",
                    "MinimalRuntime.cs"));
            bool foundEfiEntry = false;
            bool foundMinimalRuntime = false;

            foreach (JsonElement item in compileItems.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object
                    || !item.TryGetProperty(
                        "Identity",
                        out JsonElement identityElement)
                    || identityElement.ValueKind
                        != JsonValueKind.String
                    || !item.TryGetProperty(
                        "FullPath",
                        out JsonElement pathElement)
                    || pathElement.ValueKind
                        != JsonValueKind.String)
                {
                    return EfiClosureFailure(
                        workloadProfile,
                        "compile_item_shape");
                }

                string identity =
                    identityElement.GetString() ?? string.Empty;
                string fullPath =
                    pathElement.GetString() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(identity)
                    || string.IsNullOrWhiteSpace(fullPath)
                    || !Path.IsPathFullyQualified(fullPath))
                {
                    return EfiClosureFailure(
                        workloadProfile,
                        "compile_item_shape");
                }

                string canonicalPath = Path.GetFullPath(fullPath);
                if (canonicalPath.Equals(
                    canonicalStressSource,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return EfiClosureFailure(
                        workloadProfile,
                        "stress_source_compile_import");
                }

                if (canonicalPath.StartsWith(
                    hostTestDirectory,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return EfiClosureFailure(
                        workloadProfile,
                        "host_test_compile_import");
                }

                foundEfiEntry |= canonicalPath.Equals(
                    expectedEfiEntry,
                    StringComparison.OrdinalIgnoreCase);
                foundMinimalRuntime |= canonicalPath.Equals(
                    expectedMinimalRuntime,
                    StringComparison.OrdinalIgnoreCase);
            }

            if (!foundEfiEntry || !foundMinimalRuntime)
            {
                return EfiClosureFailure(
                    workloadProfile,
                    "closure_anchor_missing");
            }

            return true;
        }
        catch
        {
            return EfiClosureFailure(
                workloadProfile,
                "json_parse");
        }
    }

    private static bool TryTerminateProcess(Process process)
    {
        try
        {
            if (process.HasExited)
            {
                return true;
            }

            process.Kill(entireProcessTree: true);
            return process.WaitForExit(5_000);
        }
        catch
        {
            try
            {
                return process.HasExited;
            }
            catch
            {
                return false;
            }
        }
    }

    private static bool TryReadProcessOutput(
        Task<string> standardOutput,
        Task<string> standardError,
        out string output)
    {
        output = string.Empty;
        try
        {
            Task combined = Task.WhenAll(
                standardOutput,
                standardError);
            if (!combined.Wait(5_000))
            {
                return false;
            }

            output = standardOutput.GetAwaiter().GetResult();
            _ = standardError.GetAwaiter().GetResult();
            return true;
        }
        catch
        {
            output = string.Empty;
            return false;
        }
    }

    private static bool EfiClosureFailure(
        string workloadProfile,
        string reason,
        int exitCode = int.MinValue)
    {
        Console.WriteLine(
            "M01_010_EFI_CLOSURE_FAIL configuration=Release profile="
            + workloadProfile
            + " reason="
            + reason
            + (exitCode == int.MinValue
                ? string.Empty
                : " code="
                    + exitCode.ToString(
                        CultureInfo.InvariantCulture)));
        return false;
    }

    private static bool TestMarkerBoundary()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string testPath = Path.Combine(
            root,
            "tests",
            "OS.HostTests",
            "BootPageAllocatorStressTests.cs");
        string source = File.ReadAllText(testPath);
        string startWrite = string.Concat(
            "Console.WriteLine(\"TEST_START \" + ",
            "IssueTestName);");
        string failureWrite = string.Concat(
            "Console.WriteLine(\"TEST_FAIL \" + ",
            "IssueTestName + \" failures=\" + failures);");
        string readyWrite = string.Concat(
            "Console.WriteLine(",
            "ReadyMarker);");
        string passWrite = string.Concat(
            "Console.WriteLine(\"TEST_PASS \" + ",
            "IssueTestName);");
        int runAllStart = source.IndexOf(
            "internal static void RunAll()",
            StringComparison.Ordinal);
        int runHelperStart = source.IndexOf(
            "private static void Run(",
            runAllStart < 0 ? 0 : runAllStart,
            StringComparison.Ordinal);
        if (runAllStart < 0 || runHelperStart <= runAllStart)
        {
            return false;
        }

        string runAll = source.Substring(
            runAllStart,
            runHelperStart - runAllStart);
        int startIndex = runAll.IndexOf(
            startWrite,
            StringComparison.Ordinal);
        int failureGate = runAll.IndexOf(
            "if (failures != 0)",
            StringComparison.Ordinal);
        int failureIndex = runAll.IndexOf(
            failureWrite,
            StringComparison.Ordinal);
        int throwIndex = runAll.IndexOf(
            "throw new InvalidOperationException(",
            failureIndex < 0 ? 0 : failureIndex,
            StringComparison.Ordinal);
        int readyIndex = runAll.IndexOf(
            readyWrite,
            StringComparison.Ordinal);
        int passIndex = runAll.IndexOf(
            passWrite,
            StringComparison.Ordinal);
        if (CountOccurrences(source, "\"" + ReadyMarker + "\"") != 1
            || CountOccurrences(source, startWrite) != 1
            || CountOccurrences(source, failureWrite) != 1
            || CountOccurrences(source, readyWrite) != 1
            || CountOccurrences(source, passWrite) != 1
            || startIndex < 0
            || failureGate <= startIndex
            || failureIndex <= failureGate
            || throwIndex <= failureIndex
            || readyIndex <= throwIndex
            || passIndex <= readyIndex)
        {
            return false;
        }

        foreach (string scanRoot in new[]
        {
            Path.Combine(root, "src"),
            Path.Combine(root, "std"),
            Path.Combine(root, "apps"),
            Path.Combine(root, "tools"),
            Path.Combine(root, "scripts"),
            Path.Combine(root, ".gitea", "workflows"),
            Path.Combine(root, "tests", "OS.HostTests")
        })
        {
            if (!Directory.Exists(scanRoot))
            {
                return false;
            }

            foreach (string path in Directory.EnumerateFiles(
                scanRoot,
                "*",
                SearchOption.AllDirectories))
            {
                if (!Path.GetFullPath(path).Equals(
                        Path.GetFullPath(testPath),
                        StringComparison.OrdinalIgnoreCase)
                    && !IsGeneratedScanPath(path)
                    && HasTextExtension(path)
                    && File.ReadAllText(path).Contains(
                        ReadyMarker,
                        StringComparison.Ordinal))
                {
                    return false;
                }
            }
        }

        string bootMarker = string.Concat("OS_BOOT_", "OK");
        string smokeMarker = string.Concat("OS_SMOKE_", "PASS");
        return !source.Contains(bootMarker, StringComparison.Ordinal)
            && !source.Contains(smokeMarker, StringComparison.Ordinal);
    }

    private static ReplaySummary RunReplay(
        ulong seed,
        int churnSteps,
        int streamId)
    {
        StressArena arena = StressArena.CreateCanonical();
        DeterministicRandom random = new(seed);
        using CanonicalTranscript transcript = new();
        transcript.AddUInt64(seed);
        transcript.AddInt32(streamId);
        transcript.AddInt32(churnSteps);
        transcript.AddInt32(AllocationWeight);
        transcript.AddInt32(FreeWeight);
        arena.AppendTranscript(
            transcript,
            step: -1,
            operation: ReplayOperation.Initial,
            selectorWord: 0,
            inputWord: 0,
            tokenWord: 0,
            default);

        int step = 0;
        for (; step < churnSteps; step++)
        {
            int selection = random.NextBounded(
                100,
                out ulong selectorWord);
            int alignmentIndex = random.NextBounded(
                7,
                out ulong inputWord);
            ulong tokenWord = random.NextUInt64();
            bool allocateSelected = selection < AllocationWeight;
            ReplayOperation operation;
            OperationOutcome outcome;

            if (arena.AllocationDepth == 0)
            {
                operation = allocateSelected
                    ? ReplayOperation.Allocate
                    : ReplayOperation.AllocateEmptyFallback;
                outcome = arena.Allocate(
                    PageSize << alignmentIndex,
                    ContentTag(seed, step, tokenWord),
                    step);
            }
            else if (arena.AllocationDepth >= MaximumReplayDepth)
            {
                operation = allocateSelected
                    ? ReplayOperation.FreeDepthFallback
                    : ReplayOperation.Free;
                outcome = arena.FreeTop(step);
            }
            else if (allocateSelected)
            {
                operation = ReplayOperation.Allocate;
                outcome = arena.Allocate(
                    PageSize << alignmentIndex,
                    ContentTag(seed, step, tokenWord),
                    step);
            }
            else
            {
                operation = ReplayOperation.Free;
                outcome = arena.FreeTop(step);
            }

            arena.AppendTranscript(
                transcript,
                step,
                operation,
                selectorWord,
                inputWord,
                tokenWord,
                outcome);
        }

        while (arena.AllocationDepth != 0)
        {
            OperationOutcome outcome = arena.FreeTop(step);
            arena.AppendTranscript(
                transcript,
                step,
                ReplayOperation.DrainFree,
                0,
                0,
                0,
                outcome);
            step++;
        }

        int exhaustionGuard = arena.UsablePageCount + 1;
        while (arena.HasFreePage && exhaustionGuard-- > 0)
        {
            ulong tokenWord = Mix64(seed ^ (ulong)step);
            OperationOutcome outcome = arena.Allocate(
                PageSize,
                ContentTag(seed, step, tokenWord),
                step);
            if (!outcome.Succeeded)
            {
                throw ReplayFailure(
                    seed,
                    streamId,
                    step,
                    ReplayOperation.ExhaustionAllocate,
                    "unexpected_exhaustion_before_model_empty",
                    outcome.Status,
                    arena);
            }

            arena.AppendTranscript(
                transcript,
                step,
                ReplayOperation.ExhaustionAllocate,
                0,
                0,
                tokenWord,
                outcome);
            step++;
        }

        if (exhaustionGuard < 0 || arena.HasFreePage)
        {
            throw ReplayFailure(
                seed,
                streamId,
                step,
                ReplayOperation.ExhaustionAllocate,
                "exhaustion_guard",
                BootPageAllocatorMutationStatus.NoSuitableRange,
                arena);
        }

        OperationOutcome exhausted = arena.Allocate(
            PageSize,
            ContentTag(seed, step, Mix64((ulong)step)),
            step);
        if (exhausted.Succeeded
            || exhausted.Status
                != BootPageAllocatorMutationStatus.NoSuitableRange)
        {
            throw ReplayFailure(
                seed,
                streamId,
                step,
                ReplayOperation.ExhaustionProbe,
                "expected_no_suitable_range",
                exhausted.Status,
                arena);
        }

        arena.AppendTranscript(
            transcript,
            step,
            ReplayOperation.ExhaustionProbe,
            0,
            0,
            0,
            exhausted);
        step++;

        while (arena.AllocationDepth != 0)
        {
            OperationOutcome outcome = arena.FreeTop(step);
            arena.AppendTranscript(
                transcript,
                step,
                ReplayOperation.ExhaustionDrainFree,
                0,
                0,
                0,
                outcome);
            step++;
        }

        if (!arena.Validate(out string reason))
        {
            throw ReplayFailure(
                seed,
                streamId,
                step,
                ReplayOperation.Final,
                reason,
                BootPageAllocatorMutationStatus.InvalidStateAccounting,
                arena);
        }

        arena.AppendTranscript(
            transcript,
            step,
            ReplayOperation.Final,
            0,
            0,
            0,
            default);
        return arena.CreateSummary(
            seed,
            churnSteps,
            step + 2,
            transcript.Finish());
    }

    private static bool RunShape(
        ulong baseAddress,
        int totalPages,
        int regionStartPage,
        int regionPageCount,
        ulong alignment,
        BootPageAllocatorOnePageCoalescingKind expectedKind)
    {
        StressArena arena = StressArena.CreateSingleRegion(
            baseAddress,
            totalPages,
            regionStartPage,
            regionPageCount,
            logicalNodeCount: 4,
            nodeCapacity: 6);
        OperationOutcome allocation = arena.Allocate(
            alignment,
            ContentTag(baseAddress, regionPageCount, alignment),
            step: 1);
        if (!allocation.Succeeded)
        {
            return false;
        }

        OperationOutcome free = arena.FreeTop(step: 2);
        return free.Succeeded
            && free.CoalescingKind == expectedKind
            && arena.IsAtBaseline;
    }

    private static Exception ReplayFailure(
        ulong seed,
        int streamId,
        int step,
        ReplayOperation operation,
        string reason,
        BootPageAllocatorMutationStatus status,
        StressArena arena)
    {
        return new InvalidOperationException(
            "M01_010_REPRO seed=0x"
            + seed.ToString("X16", CultureInfo.InvariantCulture)
            + " stream="
            + streamId.ToString(CultureInfo.InvariantCulture)
            + " step="
            + step.ToString(CultureInfo.InvariantCulture)
            + " op="
            + operation
            + " status="
            + status
            + " depth="
            + arena.AllocationDepth.ToString(
                CultureInfo.InvariantCulture)
            + " reason="
            + reason);
    }

    private static unsafe bool HighBoundaryRoundTrip()
    {
        BootPageFreeListNode[] nodes =
        [
            Node(
                0,
                0,
                0xFFFF_FFFF_FFFF_E000,
                1,
                -1),
            default
        ];
        byte[] original = StructArrayBytes(nodes);
        BootPageAllocatorState state = State(0, 1, 1, 0);
        fixed (BootPageFreeListNode* nodePointer = nodes)
        {
            BootPageAllocatorOnePageResult allocation =
                BootPageAllocatorMutator.AllocateOnePage(
                    state,
                    nodePointer,
                    nodeCount: 1,
                    nodeCapacity: 2,
                    reservations: null,
                    reservationCount: 0,
                    reservationCapacity: 0,
                    pageCount: 1,
                    alignment: PageSize);
            if (!allocation.Succeeded
                || !allocation.CanRestore
                || Raw(allocation.PhysicalStart)
                    != 0xFFFF_FFFF_FFFF_E000
                || Raw(allocation.PhysicalEndExclusive)
                    != 0xFFFF_FFFF_FFFF_F000)
            {
                return false;
            }

            BootPageAllocatorOnePageFreeResult free =
                BootPageAllocatorMutator.FreeOnePage(
                    allocation.AfterState,
                    nodePointer,
                    nodeCount: 1,
                    nodeCapacity: 2,
                    reservations: null,
                    reservationCount: 0,
                    reservationCapacity: 0,
                    allocation);
            return free.Succeeded
                && free.CoalescingKind
                    == BootPageAllocatorOnePageCoalescingKind.None
                && original.AsSpan().SequenceEqual(
                    StructArrayBytes(nodes));
        }
    }

    private static unsafe bool AllocationFailurePreserves(
        BootPageAllocatorState state,
        BootPageFreeListNode[] nodes,
        ulong nodeCount,
        ulong nodeCapacity,
        BootReservedRange[] reservations,
        ulong reservationCount,
        ulong reservationCapacity,
        ulong pageCount,
        ulong alignment,
        BootPageAllocatorMutationStatus expectedStatus,
        AddressValueStatus expectedAddressStatus =
            AddressValueStatus.Success)
    {
        byte[] nodeBytes = StructArrayBytes(nodes);
        byte[] reservationBytes = StructArrayBytes(reservations);
        fixed (BootPageFreeListNode* nodePointer = nodes)
        fixed (BootReservedRange* reservationPointer = reservations)
        {
            BootPageAllocatorOnePageResult result =
                BootPageAllocatorMutator.AllocateOnePage(
                    state,
                    nodePointer,
                    nodeCount,
                    nodeCapacity,
                    reservationPointer,
                    reservationCount,
                    reservationCapacity,
                    pageCount,
                    alignment);
            return IsAllocationFailure(
                    result,
                    state,
                    expectedStatus,
                    expectedAddressStatus)
                && nodeBytes.AsSpan().SequenceEqual(
                    StructArrayBytes(nodes))
                && reservationBytes.AsSpan().SequenceEqual(
                    StructArrayBytes(reservations));
        }
    }

    private static bool IsAllocationFailure(
        BootPageAllocatorOnePageResult result,
        BootPageAllocatorState state,
        BootPageAllocatorMutationStatus expectedStatus,
        AddressValueStatus expectedAddressStatus =
            AddressValueStatus.Success)
    {
        return result.IsInitialized
            && !result.Succeeded
            && !result.CanRestore
            && result.Status == expectedStatus
            && result.AddressStatus == expectedAddressStatus
            && StatesEqual(result.BeforeState, state)
            && StatesEqual(result.AfterState, state);
    }

    private static bool IsFreeFailure(
        BootPageAllocatorOnePageFreeResult result,
        BootPageAllocatorState state,
        BootPageAllocatorOnePageResult allocation,
        BootPageAllocatorMutationStatus expectedStatus)
    {
        return result.IsInitialized
            && !result.Succeeded
            && result.Status == expectedStatus
            && StatesEqual(result.BeforeState, state)
            && StatesEqual(result.AfterState, state)
            && AllocationEvidenceEqual(result.Allocation, allocation);
    }

    private static BootPageAllocatorState State(
        int headNodeIndex,
        int freeNodeCount,
        ulong freePages,
        ulong allocatedPages)
    {
        return new BootPageAllocatorState(
            isInitialized: true,
            PageSize,
            headNodeIndex,
            freeNodeCount,
            freePages,
            checked(freePages * PageSize),
            allocatedPages,
            checked(allocatedPages * PageSize));
    }

    private static BootPageFreeListNode Node(
        int sourceFreeSegmentIndex,
        int sourceUsableRangeIndex,
        ulong physicalStart,
        ulong pageCount,
        int nextNodeIndex)
    {
        ulong byteLength = checked(pageCount * PageSize);
        return new BootPageFreeListNode(
            sourceFreeSegmentIndex,
            sourceUsableRangeIndex,
            physicalStart,
            checked(physicalStart + byteLength),
            pageCount,
            byteLength,
            nextNodeIndex);
    }

    private static BootPageFreeListNode CapacityTailCanary(int ordinal)
    {
        ulong start = checked(0x7000_0000UL + (ulong)ordinal * 0x20_000);
        return Node(
            5000 + ordinal,
            6000 + ordinal,
            start,
            2,
            -1);
    }

    private static BootReservedRange Reservation(
        BootReservationKind kind,
        ulong physicalStart,
        ulong pageCount)
    {
        BootReservedRangeStatus status =
            BootReservedRange.TryCreate(
                kind,
                Address(physicalStart),
                pageCount,
                out BootReservedRange reservation);
        if (status != BootReservedRangeStatus.Success)
        {
            throw new InvalidOperationException(
                "reservation_create_failed:" + status);
        }

        return reservation;
    }

    private static PhysicalAddress Address(ulong raw)
    {
        AddressValueStatus status =
            PhysicalAddress.TryCreateFromUefiBoundary(
                raw,
                out PhysicalAddress address);
        if (status != AddressValueStatus.Success)
        {
            throw new InvalidOperationException(
                "address_create_failed:" + status);
        }

        return address;
    }

    private static ulong Raw(PhysicalAddress address)
    {
        AddressValueStatus status =
            address.TryGetRawValueForUefiBoundary(out ulong raw);
        if (status != AddressValueStatus.Success)
        {
            throw new InvalidOperationException(
                "address_read_failed:" + status);
        }

        return raw;
    }

    private static byte[] StructArrayBytes<T>(T[] values)
        where T : struct
    {
        return MemoryMarshal.AsBytes(values.AsSpan()).ToArray();
    }

    private static bool StatesEqual(
        BootPageAllocatorState left,
        BootPageAllocatorState right)
    {
        return left.IsInitialized == right.IsInitialized
            && left.PageSize == right.PageSize
            && left.HeadNodeIndex == right.HeadNodeIndex
            && left.FreeNodeCount == right.FreeNodeCount
            && left.FreePageCount == right.FreePageCount
            && left.FreeByteCount == right.FreeByteCount
            && left.AllocatedPageCount == right.AllocatedPageCount
            && left.AllocatedByteCount == right.AllocatedByteCount;
    }

    private static bool NodesEqual(
        BootPageFreeListNode left,
        BootPageFreeListNode right)
    {
        return left.SourceFreeSegmentIndex
                == right.SourceFreeSegmentIndex
            && left.SourceUsableRangeIndex
                == right.SourceUsableRangeIndex
            && left.PhysicalStart == right.PhysicalStart
            && left.PhysicalEndExclusive
                == right.PhysicalEndExclusive
            && left.PageCount == right.PageCount
            && left.ByteLength == right.ByteLength
            && left.NextNodeIndex == right.NextNodeIndex;
    }

    private static bool AllocationEvidenceEqual(
        BootPageAllocatorOnePageResult left,
        BootPageAllocatorOnePageResult right)
    {
        return left.IsInitialized == right.IsInitialized
            && left.Status == right.Status
            && left.ReservationStatus == right.ReservationStatus
            && left.AddressStatus == right.AddressStatus
            && StatesEqual(left.BeforeState, right.BeforeState)
            && StatesEqual(left.AfterState, right.AfterState)
            && left.PhysicalStart.Equals(right.PhysicalStart)
            && left.PhysicalEndExclusive.Equals(
                right.PhysicalEndExclusive)
            && left.PageCount == right.PageCount
            && left.ByteLength == right.ByteLength
            && left.NodeCount == right.NodeCount
            && left.NodeCapacity == right.NodeCapacity
            && left.ReservationCount == right.ReservationCount
            && left.ReservationCapacity == right.ReservationCapacity
            && left.NodeIndex == right.NodeIndex
            && left.InsertedNodeIndex == right.InsertedNodeIndex
            && left.VisitedNodeCount == right.VisitedNodeCount
            && left.InvalidReservationIndex
                == right.InvalidReservationIndex
            && left.ConflictingIndex == right.ConflictingIndex
            && left.RestoreReceipt.IsInitialized
                == right.RestoreReceipt.IsInitialized
            && NodesEqual(
                left.RestoreReceipt.SourceNodeBefore,
                right.RestoreReceipt.SourceNodeBefore)
            && left.RestoreReceipt.PreviousNodeIndexBefore
                == right.RestoreReceipt.PreviousNodeIndexBefore
            && NodesEqual(
                left.RestoreReceipt.PreviousNodeBefore,
                right.RestoreReceipt.PreviousNodeBefore);
    }

    private static ulong ContentTag(
        ulong seed,
        int step,
        ulong randomWord)
    {
        ulong tag = Mix64(
            seed
            ^ randomWord
            ^ unchecked((ulong)(uint)step * 0x9E37_79B9_7F4A_7C15));
        return tag == 0 ? 0xD1B5_4A32_D192_ED03 : tag;
    }

    private static ulong Mix64(ulong value)
    {
        unchecked
        {
            value ^= value >> 30;
            value *= 0xBF58_476D_1CE4_E5B9;
            value ^= value >> 27;
            value *= 0x94D0_49BB_1331_11EB;
            return value ^ (value >> 31);
        }
    }

    private static int CountOccurrences(string text, string value)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(
            value,
            index,
            StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    private static bool HasTextExtension(string path)
    {
        string extension = Path.GetExtension(path);
        return extension.Equals(".cs", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".csproj", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".props", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".targets", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".md", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".json", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".ps1", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".yml", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".yaml", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".slnx", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsGeneratedScanPath(string path)
    {
        string fullPath = Path.GetFullPath(path);
        foreach (string segment in new[]
        {
            "bin",
            "obj",
            "artifacts"
        })
        {
            string needle =
                Path.DirectorySeparatorChar
                + segment
                + Path.DirectorySeparatorChar;
            if (fullPath.Contains(
                    needle,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private enum ReplayOperation : byte
    {
        Initial = 0,
        Allocate = 1,
        AllocateEmptyFallback = 2,
        Free = 3,
        FreeDepthFallback = 4,
        DrainFree = 5,
        ExhaustionAllocate = 6,
        ExhaustionProbe = 7,
        ExhaustionDrainFree = 8,
        Final = 9
    }

    private readonly struct OperationOutcome
    {
        public OperationOutcome(
            bool succeeded,
            BootPageAllocatorMutationStatus status,
            ulong physicalStart,
            BootPageAllocatorOnePageCoalescingKind coalescingKind)
        {
            Succeeded = succeeded;
            Status = status;
            PhysicalStart = physicalStart;
            CoalescingKind = coalescingKind;
        }

        public bool Succeeded { get; }

        public BootPageAllocatorMutationStatus Status { get; }

        public ulong PhysicalStart { get; }

        public BootPageAllocatorOnePageCoalescingKind CoalescingKind
        {
            get;
        }
    }

    private readonly struct ArenaRegion
    {
        public ArenaRegion(
            int startPage,
            int pageCount,
            int sourceFreeSegmentIndex,
            int sourceUsableRangeIndex)
        {
            StartPage = startPage;
            PageCount = pageCount;
            SourceFreeSegmentIndex = sourceFreeSegmentIndex;
            SourceUsableRangeIndex = sourceUsableRangeIndex;
        }

        public int StartPage { get; }

        public int PageCount { get; }

        public int SourceFreeSegmentIndex { get; }

        public int SourceUsableRangeIndex { get; }
    }

    private readonly struct ExpectedExtent
    {
        public ExpectedExtent(
            int startPage,
            int pageCount,
            int sourceFreeSegmentIndex,
            int sourceUsableRangeIndex)
        {
            StartPage = startPage;
            PageCount = pageCount;
            SourceFreeSegmentIndex = sourceFreeSegmentIndex;
            SourceUsableRangeIndex = sourceUsableRangeIndex;
        }

        public int StartPage { get; }

        public int PageCount { get; }

        public int SourceFreeSegmentIndex { get; }

        public int SourceUsableRangeIndex { get; }
    }

    private readonly struct ReplaySummary
    {
        public ReplaySummary(
            ulong seed,
            int churnSteps,
            int operationCount,
            int successfulAllocations,
            int successfulFrees,
            int expectedAllocationFailures,
            int exactRestorationCount,
            int maximumDepth,
            bool firstBoundaryPageSeen,
            bool lastBoundaryPageSeen,
            bool capacityTailPreserved,
            bool reservationsPreserved,
            bool returnedToBaseline,
            string transcriptHash)
        {
            Seed = seed;
            ChurnSteps = churnSteps;
            OperationCount = operationCount;
            SuccessfulAllocations = successfulAllocations;
            SuccessfulFrees = successfulFrees;
            ExpectedAllocationFailures = expectedAllocationFailures;
            ExactRestorationCount = exactRestorationCount;
            MaximumDepth = maximumDepth;
            FirstBoundaryPageSeen = firstBoundaryPageSeen;
            LastBoundaryPageSeen = lastBoundaryPageSeen;
            CapacityTailPreserved = capacityTailPreserved;
            ReservationsPreserved = reservationsPreserved;
            ReturnedToBaseline = returnedToBaseline;
            TranscriptHash = transcriptHash;
        }

        public ulong Seed { get; }

        public int ChurnSteps { get; }

        public int OperationCount { get; }

        public int SuccessfulAllocations { get; }

        public int SuccessfulFrees { get; }

        public int ExpectedAllocationFailures { get; }

        public int ExactRestorationCount { get; }

        public int MaximumDepth { get; }

        public bool FirstBoundaryPageSeen { get; }

        public bool LastBoundaryPageSeen { get; }

        public bool CapacityTailPreserved { get; }

        public bool ReservationsPreserved { get; }

        public bool ReturnedToBaseline { get; }

        public string TranscriptHash { get; }

        public bool Equals(ReplaySummary other)
        {
            return Seed == other.Seed
                && ChurnSteps == other.ChurnSteps
                && OperationCount == other.OperationCount
                && SuccessfulAllocations
                    == other.SuccessfulAllocations
                && SuccessfulFrees == other.SuccessfulFrees
                && ExpectedAllocationFailures
                    == other.ExpectedAllocationFailures
                && ExactRestorationCount
                    == other.ExactRestorationCount
                && MaximumDepth == other.MaximumDepth
                && FirstBoundaryPageSeen
                    == other.FirstBoundaryPageSeen
                && LastBoundaryPageSeen
                    == other.LastBoundaryPageSeen
                && CapacityTailPreserved
                    == other.CapacityTailPreserved
                && ReservationsPreserved
                    == other.ReservationsPreserved
                && ReturnedToBaseline == other.ReturnedToBaseline
                && string.Equals(
                    TranscriptHash,
                    other.TranscriptHash,
                    StringComparison.Ordinal);
        }
    }

    private struct DeterministicRandom
    {
        private ulong _state;

        public DeterministicRandom(ulong seed)
        {
            _state = seed == 0
                ? 0xD1B5_4A32_D192_ED03
                : seed;
        }

        public ulong NextUInt64()
        {
            unchecked
            {
                ulong value = _state;
                value ^= value >> 12;
                value ^= value << 25;
                value ^= value >> 27;
                _state = value;
                return value * 0x2545_F491_4F6C_DD1D;
            }
        }

        public int NextBounded(int exclusiveUpperBound, out ulong word)
        {
            if (exclusiveUpperBound <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(exclusiveUpperBound));
            }

            ulong bound = (ulong)exclusiveUpperBound;
            ulong limit = ulong.MaxValue
                - (ulong.MaxValue % bound);
            do
            {
                word = NextUInt64();
            }
            while (word >= limit);

            return (int)(word % bound);
        }
    }

    private sealed class CanonicalTranscript : IDisposable
    {
        private readonly MemoryStream _stream = new();
        private bool _finished;

        public void AddByte(byte value)
        {
            EnsureOpen();
            _stream.WriteByte(value);
        }

        public void AddBool(bool value)
        {
            AddByte(value ? (byte)1 : (byte)0);
        }

        public void AddInt32(int value)
        {
            Span<byte> bytes = stackalloc byte[sizeof(int)];
            BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
            AddBytes(bytes);
        }

        public void AddUInt64(ulong value)
        {
            Span<byte> bytes = stackalloc byte[sizeof(ulong)];
            BinaryPrimitives.WriteUInt64LittleEndian(bytes, value);
            AddBytes(bytes);
        }

        public string Finish()
        {
            EnsureOpen();
            _finished = true;
            _stream.Position = 0;
            return Convert.ToHexString(SHA256.HashData(_stream));
        }

        public void Dispose()
        {
            _stream.Dispose();
        }

        private void AddBytes(ReadOnlySpan<byte> bytes)
        {
            EnsureOpen();
            _stream.Write(bytes);
        }

        private void EnsureOpen()
        {
            if (_finished)
            {
                throw new InvalidOperationException(
                    "transcript_already_finished");
            }
        }
    }

    private sealed class AllocationFrame
    {
        public AllocationFrame(
            BootPageAllocatorOnePageResult allocation,
            int pageIndex,
            ulong contentTag,
            byte[] nodeBytesBefore,
            byte[] reservationBytesBefore)
        {
            Allocation = allocation;
            PageIndex = pageIndex;
            ContentTag = contentTag;
            NodeBytesBefore = nodeBytesBefore;
            ReservationBytesBefore = reservationBytesBefore;
        }

        public BootPageAllocatorOnePageResult Allocation { get; }

        public int PageIndex { get; }

        public ulong ContentTag { get; }

        public byte[] NodeBytesBefore { get; }

        public byte[] ReservationBytesBefore { get; }
    }

    private sealed class StressArena
    {
        private readonly ulong _baseAddress;
        private readonly int _totalPages;
        private readonly int _logicalNodeCount;
        private readonly int _nodeCapacity;
        private readonly BootPageFreeListNode[] _nodes;
        private readonly BootReservedRange[] _reservations;
        private readonly int _reservationCount;
        private readonly bool[] _usable;
        private readonly bool[] _allocated;
        private readonly bool[] _ownershipMirror;
        private readonly int[] _sourceFreeSegment;
        private readonly int[] _sourceUsableRange;
        private readonly ulong[] _shadowContent;
        private readonly ulong[] _actualContent;
        private readonly List<AllocationFrame> _frames = [];
        private readonly BootPageAllocatorState _baselineState;
        private readonly byte[] _baselineNodeBytes;
        private readonly byte[] _baselineReservationBytes;
        private readonly byte[] _baselineCapacityTailBytes;
        private BootPageAllocatorState _state;
        private int _successfulAllocations;
        private int _successfulFrees;
        private int _expectedAllocationFailures;
        private int _exactRestorationCount;
        private int _maximumDepth;
        private bool _firstBoundaryPageSeen;
        private bool _lastBoundaryPageSeen;
        private bool _capacityTailPreserved = true;
        private bool _reservationsPreserved = true;

        private StressArena(
            ulong baseAddress,
            int totalPages,
            ArenaRegion[] regions,
            int[] activeNodeIndexes,
            int logicalNodeCount,
            int nodeCapacity)
        {
            if (baseAddress == 0
                || baseAddress % PageSize != 0
                || totalPages <= 0
                || regions.Length == 0
                || activeNodeIndexes.Length != regions.Length
                || logicalNodeCount < regions.Length
                || nodeCapacity < logicalNodeCount)
            {
                throw new ArgumentException("invalid_stress_arena");
            }

            _baseAddress = baseAddress;
            _totalPages = totalPages;
            _logicalNodeCount = logicalNodeCount;
            _nodeCapacity = nodeCapacity;
            _nodes = new BootPageFreeListNode[nodeCapacity];
            _usable = new bool[totalPages];
            _allocated = new bool[totalPages];
            _ownershipMirror = new bool[totalPages];
            _sourceFreeSegment = new int[totalPages];
            _sourceUsableRange = new int[totalPages];
            _shadowContent = new ulong[totalPages];
            _actualContent = new ulong[totalPages];
            Array.Fill(_sourceFreeSegment, -1);
            Array.Fill(_sourceUsableRange, -1);

            int freePages = 0;
            for (int regionIndex = 0;
                regionIndex < regions.Length;
                regionIndex++)
            {
                ArenaRegion region = regions[regionIndex];
                if (region.StartPage < 0
                    || region.PageCount <= 0
                    || region.StartPage + region.PageCount > totalPages
                    || activeNodeIndexes[regionIndex] < 0
                    || activeNodeIndexes[regionIndex] >= logicalNodeCount)
                {
                    throw new ArgumentException(
                        "invalid_stress_region");
                }

                for (int page = region.StartPage;
                    page < region.StartPage + region.PageCount;
                    page++)
                {
                    if (_usable[page])
                    {
                        throw new ArgumentException(
                            "overlapping_stress_regions");
                    }

                    _usable[page] = true;
                    _sourceFreeSegment[page] =
                        region.SourceFreeSegmentIndex;
                    _sourceUsableRange[page] =
                        region.SourceUsableRangeIndex;
                }

                int nodeIndex = activeNodeIndexes[regionIndex];
                int nextNodeIndex = regionIndex + 1 < regions.Length
                    ? activeNodeIndexes[regionIndex + 1]
                    : -1;
                _nodes[nodeIndex] = Node(
                    region.SourceFreeSegmentIndex,
                    region.SourceUsableRangeIndex,
                    checked(
                        baseAddress
                        + (ulong)region.StartPage * PageSize),
                    (ulong)region.PageCount,
                    nextNodeIndex);
                freePages += region.PageCount;
            }

            for (int index = logicalNodeCount;
                index < nodeCapacity;
                index++)
            {
                _nodes[index] = CapacityTailCanary(index);
            }

            List<BootReservedRange> reservations = [];
            int cursor = 0;
            foreach (ArenaRegion region in regions)
            {
                if (region.StartPage > cursor)
                {
                    reservations.Add(
                        Reservation(
                            ReservationKindForIndex(
                                reservations.Count),
                            checked(
                                baseAddress
                                + (ulong)cursor * PageSize),
                            (ulong)(region.StartPage - cursor)));
                }

                cursor = region.StartPage + region.PageCount;
            }

            if (cursor < totalPages)
            {
                reservations.Add(
                    Reservation(
                        ReservationKindForIndex(reservations.Count),
                        checked(
                            baseAddress + (ulong)cursor * PageSize),
                        (ulong)(totalPages - cursor)));
            }

            _reservationCount = reservations.Count;
            _reservations = new BootReservedRange[
                _reservationCount + 1];
            reservations.CopyTo(_reservations);
            _reservations[^1] = Reservation(
                BootReservationKind.PageTableArena,
                checked(
                    baseAddress
                    + (ulong)(totalPages + 512) * PageSize),
                1);

            _state = new BootPageAllocatorState(
                isInitialized: true,
                PageSize,
                activeNodeIndexes[0],
                regions.Length,
                (ulong)freePages,
                checked((ulong)freePages * PageSize),
                allocatedPageCount: 0,
                allocatedByteCount: 0);
            _baselineState = _state;
            _baselineNodeBytes = StructArrayBytes(_nodes);
            _baselineReservationBytes =
                StructArrayBytes(_reservations);
            _baselineCapacityTailBytes =
                _baselineNodeBytes.AsSpan(
                    checked(
                        logicalNodeCount
                        * (int)BootPageFreeListNode.StructureSize))
                    .ToArray();

            if (!Validate(out string reason))
            {
                throw new InvalidOperationException(
                    "invalid_stress_baseline:" + reason);
            }
        }

        public int AllocationDepth => _frames.Count;

        public int UsablePageCount => _usable.Count(value => value);

        public bool HasFreePage
        {
            get
            {
                for (int page = 0; page < _totalPages; page++)
                {
                    if (_usable[page] && !_allocated[page])
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public bool IsAtBaseline =>
            _frames.Count == 0
            && StatesEqual(_state, _baselineState)
            && _baselineNodeBytes.AsSpan().SequenceEqual(
                StructArrayBytes(_nodes))
            && _baselineReservationBytes.AsSpan().SequenceEqual(
                StructArrayBytes(_reservations))
            && Validate(out _);

        public static StressArena CreateCanonical()
        {
            return new StressArena(
                baseAddress: 0x0020_0000,
                totalPages: 128,
                regions:
                [
                    new ArenaRegion(0, 10, 10, 100),
                    new ArenaRegion(16, 32, 20, 200),
                    new ArenaRegion(52, 40, 30, 300),
                    new ArenaRegion(96, 32, 40, 400)
                ],
                activeNodeIndexes: [0, 7, 19, 31],
                logicalNodeCount: 40,
                nodeCapacity: 44);
        }

        public static StressArena CreateSingleRegion(
            ulong baseAddress,
            int totalPages,
            int regionStartPage,
            int regionPageCount,
            int logicalNodeCount,
            int nodeCapacity)
        {
            return new StressArena(
                baseAddress,
                totalPages,
                [
                    new ArenaRegion(
                        regionStartPage,
                        regionPageCount,
                        sourceFreeSegmentIndex: 10,
                        sourceUsableRangeIndex: 100)
                ],
                activeNodeIndexes: [0],
                logicalNodeCount,
                nodeCapacity);
        }

        public BootPageAllocatorOnePageResult
            GetAllocationResultFromBottom(int index)
        {
            if (index < 0 || index >= _frames.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return _frames[index].Allocation;
        }

        public unsafe OperationOutcome Allocate(
            ulong alignment,
            ulong contentTag,
            int step)
        {
            bool hasExpectedPage = TryFindFirstFit(
                alignment,
                out int expectedPage);
            byte[] nodesBefore = StructArrayBytes(_nodes);
            byte[] reservationsBefore =
                StructArrayBytes(_reservations);
            ulong[] actualContentBefore =
                (ulong[])_actualContent.Clone();
            ulong[] shadowContentBefore =
                (ulong[])_shadowContent.Clone();
            bool[] allocatedBefore = (bool[])_allocated.Clone();
            bool[] ownershipBefore =
                (bool[])_ownershipMirror.Clone();
            BootPageAllocatorState stateBefore = _state;
            int depthBefore = _frames.Count;

            BootPageAllocatorOnePageResult result;
            fixed (BootPageFreeListNode* nodePointer = _nodes)
            fixed (BootReservedRange* reservationPointer =
                _reservations)
            {
                result = BootPageAllocatorMutator.AllocateOnePage(
                    _state,
                    nodePointer,
                    (ulong)_logicalNodeCount,
                    (ulong)_nodeCapacity,
                    reservationPointer,
                    (ulong)_reservationCount,
                    (ulong)_reservations.Length,
                    pageCount: 1,
                    alignment);
            }

            if (!actualContentBefore.AsSpan().SequenceEqual(
                    _actualContent)
                || !shadowContentBefore.AsSpan().SequenceEqual(
                    _shadowContent)
                || !reservationsBefore.AsSpan().SequenceEqual(
                    StructArrayBytes(_reservations)))
            {
                throw new InvalidOperationException(
                    "allocator_touched_host_content_or_reservations:"
                    + step.ToString(CultureInfo.InvariantCulture));
            }

            if (!hasExpectedPage)
            {
                if (!IsAllocationFailure(
                        result,
                        stateBefore,
                        BootPageAllocatorMutationStatus
                            .NoSuitableRange)
                    || !nodesBefore.AsSpan().SequenceEqual(
                        StructArrayBytes(_nodes))
                    || !allocatedBefore.AsSpan().SequenceEqual(
                        _allocated)
                    || !ownershipBefore.AsSpan().SequenceEqual(
                        _ownershipMirror)
                    || _frames.Count != depthBefore)
                {
                    throw new InvalidOperationException(
                        "allocation_failure_not_atomic:"
                        + step.ToString(
                            CultureInfo.InvariantCulture));
                }

                _expectedAllocationFailures++;
                RequireValid(step, "allocate_failure");
                return new OperationOutcome(
                    succeeded: false,
                    result.Status,
                    physicalStart: 0,
                    BootPageAllocatorOnePageCoalescingKind
                        .Uninitialized);
            }

            ulong expectedAddress = checked(
                _baseAddress + (ulong)expectedPage * PageSize);
            if (!result.Succeeded
                || !result.CanRestore
                || Raw(result.PhysicalStart) != expectedAddress
                || Raw(result.PhysicalEndExclusive)
                    != expectedAddress + PageSize
                || !StatesEqual(result.BeforeState, stateBefore)
                || result.NodeCount != (ulong)_logicalNodeCount
                || result.NodeCapacity != (ulong)_nodeCapacity
                || result.ReservationCount
                    != (ulong)_reservationCount
                || result.ReservationCapacity
                    != (ulong)_reservations.Length)
            {
                throw new InvalidOperationException(
                    "allocation_model_mismatch:"
                    + step.ToString(CultureInfo.InvariantCulture)
                    + ":expected=0x"
                    + expectedAddress.ToString(
                        "X16",
                        CultureInfo.InvariantCulture)
                    + ":status="
                    + result.Status);
            }

            _frames.Add(
                new AllocationFrame(
                    result,
                    expectedPage,
                    contentTag,
                    nodesBefore,
                    reservationsBefore));
            _state = result.AfterState;
            _allocated[expectedPage] = true;
            _ownershipMirror[expectedPage] = true;
            _shadowContent[expectedPage] = contentTag;
            _actualContent[expectedPage] = contentTag;
            _successfulAllocations++;
            if (_frames.Count > _maximumDepth)
            {
                _maximumDepth = _frames.Count;
            }

            int firstUsable = FirstUsablePage();
            int lastUsable = LastUsablePage();
            _firstBoundaryPageSeen |= expectedPage == firstUsable;
            _lastBoundaryPageSeen |= expectedPage == lastUsable;
            RequireValid(step, "allocate_success");
            return new OperationOutcome(
                succeeded: true,
                result.Status,
                expectedAddress,
                BootPageAllocatorOnePageCoalescingKind
                    .Uninitialized);
        }

        public unsafe OperationOutcome FreeTop(int step)
        {
            if (_frames.Count == 0)
            {
                throw new InvalidOperationException(
                    "free_empty_stack:"
                    + step.ToString(CultureInfo.InvariantCulture));
            }

            AllocationFrame frame = _frames[^1];
            BootPageAllocatorOnePageCoalescingKind expectedKind =
                ExpectedCoalescingKind(frame.PageIndex);
            byte[] reservationsBefore =
                StructArrayBytes(_reservations);
            ulong[] actualContentBefore =
                (ulong[])_actualContent.Clone();
            ulong[] shadowContentBefore =
                (ulong[])_shadowContent.Clone();
            BootPageAllocatorState stateBefore = _state;

            BootPageAllocatorOnePageFreeResult result;
            fixed (BootPageFreeListNode* nodePointer = _nodes)
            fixed (BootReservedRange* reservationPointer =
                _reservations)
            {
                result = BootPageAllocatorMutator.FreeOnePage(
                    _state,
                    nodePointer,
                    (ulong)_logicalNodeCount,
                    (ulong)_nodeCapacity,
                    reservationPointer,
                    (ulong)_reservationCount,
                    (ulong)_reservations.Length,
                    frame.Allocation);
            }

            if (!actualContentBefore.AsSpan().SequenceEqual(
                    _actualContent)
                || !shadowContentBefore.AsSpan().SequenceEqual(
                    _shadowContent)
                || !reservationsBefore.AsSpan().SequenceEqual(
                    StructArrayBytes(_reservations))
                || !result.Succeeded
                || result.CoalescingKind != expectedKind
                || !StatesEqual(result.BeforeState, stateBefore)
                || !StatesEqual(
                    result.AfterState,
                    frame.Allocation.BeforeState)
                || Raw(result.PhysicalStart)
                    != checked(
                        _baseAddress
                        + (ulong)frame.PageIndex * PageSize)
                || _actualContent[frame.PageIndex]
                    != frame.ContentTag
                || _shadowContent[frame.PageIndex]
                    != frame.ContentTag)
            {
                throw new InvalidOperationException(
                    "free_model_mismatch:"
                    + step.ToString(CultureInfo.InvariantCulture)
                    + ":status="
                    + result.Status
                    + ":kind="
                    + result.CoalescingKind
                    + ":expected="
                    + expectedKind);
            }

            _state = result.AfterState;
            _allocated[frame.PageIndex] = false;
            _ownershipMirror[frame.PageIndex] = false;
            _shadowContent[frame.PageIndex] = 0;
            _actualContent[frame.PageIndex] = 0;
            _frames.RemoveAt(_frames.Count - 1);
            _successfulFrees++;

            if (!frame.NodeBytesBefore.AsSpan().SequenceEqual(
                    StructArrayBytes(_nodes))
                || !frame.ReservationBytesBefore.AsSpan()
                    .SequenceEqual(
                        StructArrayBytes(_reservations)))
            {
                throw new InvalidOperationException(
                    "exact_inverse_bytes_mismatch:"
                    + step.ToString(CultureInfo.InvariantCulture));
            }

            _exactRestorationCount++;
            RequireValid(step, "free_success");
            return new OperationOutcome(
                succeeded: true,
                result.Status,
                Raw(result.PhysicalStart),
                result.CoalescingKind);
        }

        public unsafe bool ExpectFreeFailure(
            BootPageAllocatorOnePageResult allocation,
            BootPageAllocatorMutationStatus expectedStatus,
            int step)
        {
            byte[] nodesBefore = StructArrayBytes(_nodes);
            byte[] reservationsBefore =
                StructArrayBytes(_reservations);
            ulong[] actualContentBefore =
                (ulong[])_actualContent.Clone();
            ulong[] shadowContentBefore =
                (ulong[])_shadowContent.Clone();
            bool[] allocatedBefore = (bool[])_allocated.Clone();
            bool[] ownershipBefore =
                (bool[])_ownershipMirror.Clone();
            BootPageAllocatorState stateBefore = _state;
            int depthBefore = _frames.Count;

            BootPageAllocatorOnePageFreeResult result;
            fixed (BootPageFreeListNode* nodePointer = _nodes)
            fixed (BootReservedRange* reservationPointer =
                _reservations)
            {
                result = BootPageAllocatorMutator.FreeOnePage(
                    _state,
                    nodePointer,
                    (ulong)_logicalNodeCount,
                    (ulong)_nodeCapacity,
                    reservationPointer,
                    (ulong)_reservationCount,
                    (ulong)_reservations.Length,
                    allocation);
            }

            return IsFreeFailure(
                    result,
                    stateBefore,
                    allocation,
                    expectedStatus)
                && nodesBefore.AsSpan().SequenceEqual(
                    StructArrayBytes(_nodes))
                && reservationsBefore.AsSpan().SequenceEqual(
                    StructArrayBytes(_reservations))
                && actualContentBefore.AsSpan().SequenceEqual(
                    _actualContent)
                && shadowContentBefore.AsSpan().SequenceEqual(
                    _shadowContent)
                && allocatedBefore.AsSpan().SequenceEqual(
                    _allocated)
                && ownershipBefore.AsSpan().SequenceEqual(
                    _ownershipMirror)
                && _frames.Count == depthBefore
                && StatesEqual(_state, stateBefore)
                && Validate(out _);
        }

        public bool CorruptActualContentAndDetect(
            out string detectedReason)
        {
            for (int page = 0; page < _totalPages; page++)
            {
                if (!_allocated[page])
                {
                    continue;
                }

                ulong original = _actualContent[page];
                _actualContent[page] ^= 1;
                bool detected = !Validate(out detectedReason);
                _actualContent[page] = original;
                return detected;
            }

            detectedReason = "no_allocated_page";
            return false;
        }

        public bool CorruptShadowOwnershipAndDetect(
            out string detectedReason)
        {
            for (int page = 0; page < _totalPages; page++)
            {
                if (!_usable[page])
                {
                    continue;
                }

                _ownershipMirror[page] =
                    !_ownershipMirror[page];
                bool detected = !Validate(out detectedReason);
                _ownershipMirror[page] =
                    !_ownershipMirror[page];
                return detected;
            }

            detectedReason = "no_usable_page";
            return false;
        }

        public void AppendTranscript(
            CanonicalTranscript transcript,
            int step,
            ReplayOperation operation,
            ulong selectorWord,
            ulong inputWord,
            ulong tokenWord,
            OperationOutcome outcome)
        {
            transcript.AddInt32(step);
            transcript.AddByte((byte)operation);
            transcript.AddUInt64(selectorWord);
            transcript.AddUInt64(inputWord);
            transcript.AddUInt64(tokenWord);
            transcript.AddBool(outcome.Succeeded);
            transcript.AddInt32((int)outcome.Status);
            transcript.AddUInt64(outcome.PhysicalStart);
            transcript.AddByte((byte)outcome.CoalescingKind);
            transcript.AddBool(_state.IsInitialized);
            transcript.AddUInt64(_state.PageSize);
            transcript.AddInt32(_state.HeadNodeIndex);
            transcript.AddInt32(_state.FreeNodeCount);
            transcript.AddUInt64(_state.FreePageCount);
            transcript.AddUInt64(_state.FreeByteCount);
            transcript.AddUInt64(_state.AllocatedPageCount);
            transcript.AddUInt64(_state.AllocatedByteCount);
            transcript.AddInt32(_frames.Count);
            transcript.AddInt32(_successfulAllocations);
            transcript.AddInt32(_successfulFrees);
            transcript.AddInt32(_expectedAllocationFailures);
            transcript.AddInt32(_exactRestorationCount);
            transcript.AddInt32(_maximumDepth);

            for (int index = 0;
                index < _logicalNodeCount;
                index++)
            {
                BootPageFreeListNode node = _nodes[index];
                transcript.AddInt32(node.SourceFreeSegmentIndex);
                transcript.AddInt32(node.SourceUsableRangeIndex);
                transcript.AddUInt64(node.PhysicalStart);
                transcript.AddUInt64(node.PhysicalEndExclusive);
                transcript.AddUInt64(node.PageCount);
                transcript.AddUInt64(node.ByteLength);
                transcript.AddInt32(node.NextNodeIndex);
            }

            transcript.AddInt32(_reservationCount);
            transcript.AddInt32(_reservations.Length);
            for (int index = 0;
                index < _reservations.Length;
                index++)
            {
                BootReservedRange reservation =
                    _reservations[index];
                transcript.AddByte((byte)reservation.Kind);
                transcript.AddUInt64(
                    reservation.PhysicalStart.IsValid
                        ? Raw(reservation.PhysicalStart)
                        : 0);
                transcript.AddUInt64(
                    reservation.PhysicalEndExclusive.IsValid
                        ? Raw(reservation.PhysicalEndExclusive)
                        : 0);
                transcript.AddUInt64(reservation.PageCount);
                transcript.AddUInt64(reservation.ByteLength);
            }

            transcript.AddInt32(_totalPages);
            for (int page = 0; page < _totalPages; page++)
            {
                transcript.AddBool(_usable[page]);
                transcript.AddBool(_allocated[page]);
                transcript.AddBool(_ownershipMirror[page]);
                transcript.AddInt32(_sourceFreeSegment[page]);
                transcript.AddInt32(_sourceUsableRange[page]);
                transcript.AddUInt64(_shadowContent[page]);
                transcript.AddUInt64(_actualContent[page]);
            }
        }

        public ReplaySummary CreateSummary(
            ulong seed,
            int churnSteps,
            int operationCount,
            string transcriptHash)
        {
            return new ReplaySummary(
                seed,
                churnSteps,
                operationCount,
                _successfulAllocations,
                _successfulFrees,
                _expectedAllocationFailures,
                _exactRestorationCount,
                _maximumDepth,
                _firstBoundaryPageSeen,
                _lastBoundaryPageSeen,
                _capacityTailPreserved,
                _reservationsPreserved,
                IsAtBaseline,
                transcriptHash);
        }

        public bool Validate(out string reason)
        {
            for (int page = 0; page < _totalPages; page++)
            {
                if (_allocated[page] != _ownershipMirror[page])
                {
                    reason = "ownership_mismatch:"
                        + page.ToString(
                            CultureInfo.InvariantCulture);
                    return false;
                }

                if (_actualContent[page]
                    != _shadowContent[page])
                {
                    reason = "content_mismatch:"
                        + page.ToString(
                            CultureInfo.InvariantCulture);
                    return false;
                }

                if (!_usable[page])
                {
                    if (_allocated[page]
                        || _shadowContent[page] != 0
                        || _sourceFreeSegment[page] != -1
                        || _sourceUsableRange[page] != -1)
                    {
                        reason = "reserved_page_state:"
                            + page.ToString(
                                CultureInfo.InvariantCulture);
                        return false;
                    }
                }
                else if (_allocated[page]
                    != (_shadowContent[page] != 0))
                {
                    reason = "content_ownership_mismatch:"
                        + page.ToString(
                            CultureInfo.InvariantCulture);
                    return false;
                }
            }

            List<ExpectedExtent> expected = BuildExpectedExtents();
            if (_state.FreeNodeCount != expected.Count)
            {
                reason = "free_node_count:"
                    + _state.FreeNodeCount.ToString(
                        CultureInfo.InvariantCulture)
                    + ":"
                    + expected.Count.ToString(
                        CultureInfo.InvariantCulture);
                return false;
            }

            if (expected.Count == 0)
            {
                if (_state.HeadNodeIndex != -1)
                {
                    reason = "nonempty_head_for_empty_model";
                    return false;
                }
            }
            else if (_state.HeadNodeIndex < 0
                || _state.HeadNodeIndex >= _logicalNodeCount)
            {
                reason = "invalid_head";
                return false;
            }

            bool[] visited = new bool[_logicalNodeCount];
            int nodeIndex = _state.HeadNodeIndex;
            ulong freePages = 0;
            for (int extentIndex = 0;
                extentIndex < expected.Count;
                extentIndex++)
            {
                if (nodeIndex < 0
                    || nodeIndex >= _logicalNodeCount
                    || visited[nodeIndex])
                {
                    reason = "chain_index:"
                        + extentIndex.ToString(
                            CultureInfo.InvariantCulture);
                    return false;
                }

                visited[nodeIndex] = true;
                ExpectedExtent extent = expected[extentIndex];
                BootPageFreeListNode node = _nodes[nodeIndex];
                ulong expectedStart = checked(
                    _baseAddress
                    + (ulong)extent.StartPage * PageSize);
                ulong expectedBytes = checked(
                    (ulong)extent.PageCount * PageSize);
                if (node.SourceFreeSegmentIndex
                        != extent.SourceFreeSegmentIndex
                    || node.SourceUsableRangeIndex
                        != extent.SourceUsableRangeIndex
                    || node.PhysicalStart != expectedStart
                    || node.PhysicalEndExclusive
                        != expectedStart + expectedBytes
                    || node.PageCount != (ulong)extent.PageCount
                    || node.ByteLength != expectedBytes)
                {
                    reason = "extent_mismatch:"
                        + extentIndex.ToString(
                            CultureInfo.InvariantCulture);
                    return false;
                }

                freePages += node.PageCount;
                nodeIndex = node.NextNodeIndex;
            }

            if (nodeIndex != -1)
            {
                reason = "chain_not_terminated";
                return false;
            }

            for (int index = 0;
                index < _logicalNodeCount;
                index++)
            {
                if (!visited[index] && !IsRawZeroNode(_nodes[index]))
                {
                    reason = "inactive_slot_not_zero:"
                        + index.ToString(
                            CultureInfo.InvariantCulture);
                    return false;
                }
            }

            ulong allocatedPages = 0;
            for (int page = 0; page < _totalPages; page++)
            {
                if (_allocated[page])
                {
                    allocatedPages++;
                }
            }

            if (!_state.IsInitialized
                || _state.PageSize != PageSize
                || _state.FreePageCount != freePages
                || _state.FreeByteCount != freePages * PageSize
                || _state.AllocatedPageCount != allocatedPages
                || _state.AllocatedByteCount
                    != allocatedPages * PageSize
                || freePages + allocatedPages
                    != (ulong)UsablePageCount)
            {
                reason = "state_accounting";
                return false;
            }

            byte[] nodeBytes = StructArrayBytes(_nodes);
            byte[] currentTail = nodeBytes.AsSpan(
                checked(
                    _logicalNodeCount
                    * (int)BootPageFreeListNode.StructureSize))
                .ToArray();
            if (!_baselineCapacityTailBytes.AsSpan().SequenceEqual(
                    currentTail))
            {
                _capacityTailPreserved = false;
                reason = "capacity_tail_changed";
                return false;
            }

            if (!_baselineReservationBytes.AsSpan().SequenceEqual(
                    StructArrayBytes(_reservations)))
            {
                _reservationsPreserved = false;
                reason = "reservations_changed";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private void RequireValid(int step, string operation)
        {
            if (!Validate(out string reason))
            {
                throw new InvalidOperationException(
                    operation
                    + ":"
                    + step.ToString(CultureInfo.InvariantCulture)
                    + ":"
                    + reason);
            }
        }

        private bool TryFindFirstFit(
            ulong alignment,
            out int pageIndex)
        {
            pageIndex = -1;
            if (alignment < PageSize
                || (alignment & (alignment - 1)) != 0
                || alignment % PageSize != 0)
            {
                return false;
            }

            foreach (ExpectedExtent extent in BuildExpectedExtents())
            {
                ulong start = checked(
                    _baseAddress
                    + (ulong)extent.StartPage * PageSize);
                ulong end = checked(
                    start + (ulong)extent.PageCount * PageSize);
                ulong mask = alignment - 1;
                if (start > ulong.MaxValue - mask)
                {
                    continue;
                }

                ulong aligned = (start + mask) & ~mask;
                if (aligned > ulong.MaxValue - PageSize
                    || aligned + PageSize > end)
                {
                    continue;
                }

                ulong offset = aligned - _baseAddress;
                if (offset % PageSize != 0)
                {
                    throw new InvalidOperationException(
                        "model_alignment_not_page_multiple");
                }

                pageIndex = checked((int)(offset / PageSize));
                return true;
            }

            return false;
        }

        private List<ExpectedExtent> BuildExpectedExtents()
        {
            List<ExpectedExtent> extents = [];
            int page = 0;
            while (page < _totalPages)
            {
                if (!_usable[page] || _allocated[page])
                {
                    page++;
                    continue;
                }

                int start = page;
                int sourceFreeSegment =
                    _sourceFreeSegment[page];
                int sourceUsableRange =
                    _sourceUsableRange[page];
                page++;
                while (page < _totalPages
                    && _usable[page]
                    && !_allocated[page]
                    && _sourceFreeSegment[page]
                        == sourceFreeSegment
                    && _sourceUsableRange[page]
                        == sourceUsableRange)
                {
                    page++;
                }

                extents.Add(
                    new ExpectedExtent(
                        start,
                        page - start,
                        sourceFreeSegment,
                        sourceUsableRange));
            }

            return extents;
        }

        private BootPageAllocatorOnePageCoalescingKind
            ExpectedCoalescingKind(int pageIndex)
        {
            int sourceSegment = _sourceFreeSegment[pageIndex];
            int sourceRange = _sourceUsableRange[pageIndex];
            bool hasPrevious =
                pageIndex > 0
                && _usable[pageIndex - 1]
                && !_allocated[pageIndex - 1]
                && _sourceFreeSegment[pageIndex - 1]
                    == sourceSegment
                && _sourceUsableRange[pageIndex - 1]
                    == sourceRange;
            bool hasNext =
                pageIndex + 1 < _totalPages
                && _usable[pageIndex + 1]
                && !_allocated[pageIndex + 1]
                && _sourceFreeSegment[pageIndex + 1]
                    == sourceSegment
                && _sourceUsableRange[pageIndex + 1]
                    == sourceRange;
            if (hasPrevious && hasNext)
            {
                return BootPageAllocatorOnePageCoalescingKind
                    .PreviousAndNext;
            }

            if (hasPrevious)
            {
                return BootPageAllocatorOnePageCoalescingKind
                    .Previous;
            }

            return hasNext
                ? BootPageAllocatorOnePageCoalescingKind.Next
                : BootPageAllocatorOnePageCoalescingKind.None;
        }

        private int FirstUsablePage()
        {
            for (int page = 0; page < _totalPages; page++)
            {
                if (_usable[page])
                {
                    return page;
                }
            }

            return -1;
        }

        private int LastUsablePage()
        {
            for (int page = _totalPages - 1; page >= 0; page--)
            {
                if (_usable[page])
                {
                    return page;
                }
            }

            return -1;
        }

        private static bool IsRawZeroNode(
            BootPageFreeListNode node)
        {
            ReadOnlySpan<BootPageFreeListNode> nodeSpan =
                MemoryMarshal.CreateReadOnlySpan(ref node, 1);
            foreach (byte value in MemoryMarshal.AsBytes(nodeSpan))
            {
                if (value != 0)
                {
                    return false;
                }
            }

            return true;
        }

        private static BootReservationKind
            ReservationKindForIndex(int index)
        {
            return (index % 4) switch
            {
                0 => BootReservationKind.LoadedImage,
                1 => BootReservationKind.BootStack,
                2 => BootReservationKind.OwnedMemoryMap,
                _ => BootReservationKind.AcpiSnapshot
            };
        }
    }
}
