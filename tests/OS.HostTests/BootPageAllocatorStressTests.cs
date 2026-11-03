using OS.Tooling;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using OS.Boot;
using OS.Kernel.Memory;

internal static partial class BootPageAllocatorStressTests
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
    private const string CanonicalTranscriptHash = "12F1D263FC34D423E628574705A22F426BF43BACBAA38F53BEF886F5AB5D8D96";
    private const int SeedMatrixChurnSteps = 384;
    private const int MaximumReplayDepth = 24;
    private const int AllocationWeight = 65;
    private const int FreeWeight = 35;
    private static ReplaySummary s_canonicalSummary;
    [HostFixture(HostLane.Native)]
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
            throw new InvalidOperationException("M01-010 deterministic allocator stress tests failed: " + failures);
        }

        Console.WriteLine("M01_010_REPLAY seed=0x" + CanonicalSeed.ToString("X16", CultureInfo.InvariantCulture) + " churn=" + CanonicalChurnSteps.ToString(CultureInfo.InvariantCulture) + " records=" + s_canonicalSummary.OperationCount.ToString(CultureInfo.InvariantCulture) + " allocations=" + s_canonicalSummary.SuccessfulAllocations.ToString(CultureInfo.InvariantCulture) + " frees=" + s_canonicalSummary.SuccessfulFrees.ToString(CultureInfo.InvariantCulture) + " expected_failures=" + s_canonicalSummary.ExpectedAllocationFailures.ToString(CultureInfo.InvariantCulture) + " hash=" + s_canonicalSummary.TranscriptHash);
        Console.WriteLine(ReadyMarker);
        Console.WriteLine("TEST_PASS " + IssueTestName);
    }

    private static void Run(string name, Func<bool> test, ref int failures)
    {
        HostCheck.Run("" + name, test, ref failures);
    }

    private static bool TestFixedSeedReplay()
    {
        ReplaySummary first = RunReplay(CanonicalSeed, CanonicalChurnSteps, streamId: 1);
        ReplaySummary second = RunReplay(CanonicalSeed, CanonicalChurnSteps, streamId: 1);
        ReplaySummary different = RunReplay(0xBB67_AE85_84CA_A73B, CanonicalChurnSteps, streamId: 1);
        s_canonicalSummary = first;
        return first.Equals(second) && first.TranscriptHash.Length == 64 && string.Equals(first.TranscriptHash, CanonicalTranscriptHash, StringComparison.Ordinal) && different.TranscriptHash.Length == 64 && !string.Equals(first.TranscriptHash, different.TranscriptHash, StringComparison.Ordinal) && first.OperationCount == CanonicalTranscriptRecordCount && first.SuccessfulAllocations == CanonicalSuccessfulAllocations && first.SuccessfulFrees == CanonicalSuccessfulFrees && first.ExpectedAllocationFailures == CanonicalExpectedAllocationFailures && first.ReturnedToBaseline && first.ExactRestorationCount == first.SuccessfulFrees && first.SuccessfulAllocations > CanonicalChurnSteps / 2 && first.SuccessfulFrees > CanonicalChurnSteps / 3;
    }

    private static bool TestSeedMatrixChurn()
    {
        ulong[] seeds = [0x3C6E_F372_FE94_F82B, 0xA54F_F53A_5F1D_36F1, 0x510E_527F_ADE6_82D1];
        string[] expectedHashes = ["A74D2B2234A669DE8FDD7A478A3D12BF6601378E3557961B4EE1ADAAD20DB4D7", "83AD88944AC8DB83A235E41A981F99EE02B652BC8DB2D596A0CF8AF585FC88B9", "767149D6F2EEC88CDC524E96C5C2F2E2024B92B90E1D92A200C353E1EB1231C8"];
        HashSet<string> hashes = new(StringComparer.Ordinal);
        int allocations = 0;
        int frees = 0;
        int allocationFailures = 0;
        for (int index = 0; index < seeds.Length; index++)
        {
            ReplaySummary summary = RunReplay(seeds[index], SeedMatrixChurnSteps, streamId: index + 2);
            if (!summary.ReturnedToBaseline || !summary.FirstBoundaryPageSeen || !summary.LastBoundaryPageSeen || !summary.CapacityTailPreserved || !summary.ReservationsPreserved || summary.ExactRestorationCount != summary.SuccessfulFrees || !string.Equals(summary.TranscriptHash, expectedHashes[index], StringComparison.Ordinal))
            {
                return false;
            }

            hashes.Add(summary.TranscriptHash);
            allocations += summary.SuccessfulAllocations;
            frees += summary.SuccessfulFrees;
            allocationFailures += summary.ExpectedAllocationFailures;
        }

        return hashes.Count == seeds.Length && allocations > seeds.Length * SeedMatrixChurnSteps / 2 && frees > seeds.Length * SeedMatrixChurnSteps / 3 && allocationFailures >= seeds.Length;
    }

    private static bool TestScriptedRestorationShapes()
    {
        return RunShape(baseAddress: 0x0040_0000, totalPages: 1, regionStartPage: 0, regionPageCount: 1, alignment: PageSize, BootPageAllocatorOnePageCoalescingKind.None) && RunShape(baseAddress: 0x0050_0000, totalPages: 4, regionStartPage: 0, regionPageCount: 4, alignment: PageSize, BootPageAllocatorOnePageCoalescingKind.Next) && RunShape(baseAddress: 0x0060_0000, totalPages: 5, regionStartPage: 1, regionPageCount: 4, alignment: 4 * PageSize, BootPageAllocatorOnePageCoalescingKind.Previous) && RunShape(baseAddress: 0x0070_0000, totalPages: 8, regionStartPage: 1, regionPageCount: 7, alignment: 4 * PageSize, BootPageAllocatorOnePageCoalescingKind.PreviousAndNext);
    }

    private static bool TestExhaustionAndBoundaryPages()
    {
        ReplaySummary summary = RunReplay(0x9B05_688C_2B3E_6C1F, SeedMatrixChurnSteps, streamId: 9);
        return summary.FirstBoundaryPageSeen && summary.LastBoundaryPageSeen && summary.ExpectedAllocationFailures >= 1 && summary.MaximumDepth >= 100 && summary.ReturnedToBaseline;
    }

    private static unsafe bool TestDefaultAndEmptySetup()
    {
        BootPageAllocatorOnePageResult defaultStateResult = BootPageAllocatorMutator.AllocateOnePage(default, null, 0, 0, null, 0, 0, 1, PageSize);
        BootPageAllocatorState emptyState = new(isInitialized: true, PageSize, headNodeIndex: -1, freeNodeCount: 0, freePageCount: 0, freeByteCount: 0, allocatedPageCount: 0, allocatedByteCount: 0);
        BootPageAllocatorOnePageResult emptyResult = BootPageAllocatorMutator.AllocateOnePage(emptyState, null, 0, 0, null, 0, 0, 1, PageSize);
        BootPageAllocatorOnePageFreeResult defaultFree = BootPageAllocatorMutator.FreeOnePage(emptyState, null, 0, 0, null, 0, 0, default);
        return IsAllocationFailure(defaultStateResult, default, BootPageAllocatorMutationStatus.StateNotInitialized) && IsAllocationFailure(emptyResult, emptyState, BootPageAllocatorMutationStatus.NoSuitableRange) && IsFreeFailure(defaultFree, emptyState, default, BootPageAllocatorMutationStatus.FreeRangeNotAllocated) && !default(BootPageAllocatorOnePageResult).Succeeded && !default(BootPageAllocatorOnePageFreeResult).Succeeded;
    }

    private static bool TestRequestAndBoundaryFailures()
    {
        BootPageFreeListNode[] nodes = [Node(0, 0, 0x0010_0000, 4, -1), default, CapacityTailCanary(90)];
        BootPageAllocatorState state = State(0, 1, 4, 0);
        if (!AllocationFailurePreserves(state, nodes, nodeCount: 2, nodeCapacity: 3, [], 0, 0, pageCount: 0, PageSize, BootPageAllocatorMutationStatus.InvalidPageCount) || !AllocationFailurePreserves(state, nodes, 2, 3, [], 0, 0, pageCount: 2, PageSize, BootPageAllocatorMutationStatus.InvalidPageCount) || !AllocationFailurePreserves(state, nodes, 2, 3, [], 0, 0, pageCount: 1, alignment: 0, BootPageAllocatorMutationStatus.InvalidAlignment) || !AllocationFailurePreserves(state, nodes, 2, 3, [], 0, 0, 1, PageSize / 2, BootPageAllocatorMutationStatus.InvalidAlignment) || !AllocationFailurePreserves(state, nodes, 2, 3, [], 0, 0, 1, 3 * PageSize, BootPageAllocatorMutationStatus.InvalidAlignment))
        {
            return false;
        }

        BootPageFreeListNode[] highNodes = [Node(0, 0, 0xFFFF_FFFF_FFFF_E000, 1, -1), default];
        return HighBoundaryRoundTrip() && AllocationFailurePreserves(State(0, 1, 1, 0), highNodes, nodeCount: 1, nodeCapacity: 2, [], 0, 0, 1, 4 * PageSize, BootPageAllocatorMutationStatus.AllocationAddressOverflow, AddressValueStatus.AddressOverflow);
    }

    private static bool TestOwnershipAndReplayFailures()
    {
        StressArena arena = StressArena.CreateSingleRegion(baseAddress: 0x0080_0000, totalPages: 8, regionStartPage: 0, regionPageCount: 8, logicalNodeCount: 8, nodeCapacity: 10);
        OperationOutcome first = arena.Allocate(PageSize, contentTag: 0x1111, step: 1);
        OperationOutcome second = arena.Allocate(PageSize, contentTag: 0x2222, step: 2);
        BootPageAllocatorOnePageResult firstReceipt = arena.GetAllocationResultFromBottom(0);
        BootPageAllocatorOnePageResult secondReceipt = arena.GetAllocationResultFromBottom(1);
        if (!first.Succeeded || !second.Succeeded || !arena.ExpectFreeFailure(firstReceipt, BootPageAllocatorMutationStatus.FreeRangeNotAllocated, step: 3))
        {
            return false;
        }

        StressArena foreignArena = StressArena.CreateSingleRegion(baseAddress: 0x00A0_0000, totalPages: 8, regionStartPage: 0, regionPageCount: 8, logicalNodeCount: 8, nodeCapacity: 10);
        OperationOutcome foreignFirst = foreignArena.Allocate(PageSize, contentTag: 0x3333, step: 4);
        OperationOutcome foreignSecond = foreignArena.Allocate(PageSize, contentTag: 0x4444, step: 5);
        BootPageAllocatorOnePageResult foreignReceipt = foreignArena.GetAllocationResultFromBottom(1);
        if (!foreignFirst.Succeeded || !foreignSecond.Succeeded || !StatesEqual(secondReceipt.AfterState, foreignReceipt.AfterState) || secondReceipt.NodeCount != foreignReceipt.NodeCount || secondReceipt.NodeCapacity != foreignReceipt.NodeCapacity || secondReceipt.ReservationCount != foreignReceipt.ReservationCount || secondReceipt.ReservationCapacity != foreignReceipt.ReservationCapacity || !arena.ExpectFreeFailure(foreignReceipt, BootPageAllocatorMutationStatus.FreeRangeNotAllocated, step: 6))
        {
            return false;
        }

        OperationOutcome freeSecond = arena.FreeTop(step: 7);
        OperationOutcome freeFirst = arena.FreeTop(step: 8);
        OperationOutcome freeForeignSecond = foreignArena.FreeTop(step: 9);
        OperationOutcome freeForeignFirst = foreignArena.FreeTop(step: 10);
        return freeSecond.Succeeded && freeFirst.Succeeded && freeForeignSecond.Succeeded && freeForeignFirst.Succeeded && arena.ExpectFreeFailure(firstReceipt, BootPageAllocatorMutationStatus.FreeRangeNotAllocated, step: 11) && arena.ExpectFreeFailure(secondReceipt, BootPageAllocatorMutationStatus.FreeRangeNotAllocated, step: 12) && arena.IsAtBaseline && foreignArena.IsAtBaseline;
    }

    private static bool TestMetadataCapacityAndCorruption()
    {
        BootPageFreeListNode[] noLogicalSplitSlot = [Node(0, 0, 0x0010_1000, 8, -1), default, CapacityTailCanary(77)];
        if (!AllocationFailurePreserves(State(0, 1, 8, 0), noLogicalSplitSlot, nodeCount: 1, nodeCapacity: 3, [], 0, 0, 1, 4 * PageSize, BootPageAllocatorMutationStatus.MetadataCapacityTooSmall))
        {
            return false;
        }

        BootPageFreeListNode[] valid = [Node(0, 0, 0x0020_0000, 4, -1), default];
        BootPageAllocatorState mismatchedTotals = new(isInitialized: true, PageSize, headNodeIndex: 0, freeNodeCount: 1, freePageCount: 5, freeByteCount: 5 * PageSize, allocatedPageCount: 0, allocatedByteCount: 0);
        if (!AllocationFailurePreserves(mismatchedTotals, valid, 2, 2, [], 0, 0, 1, PageSize, BootPageAllocatorMutationStatus.TotalPageCountMismatch))
        {
            return false;
        }

        BootPageFreeListNode[] malformed = [new BootPageFreeListNode(0, 0, 0x0030_0000, 0x0030_4000, pageCount: 3, byteLength: 4 * PageSize, nextNodeIndex: -1), default];
        return AllocationFailurePreserves(State(0, 1, 4, 0), malformed, 2, 2, [], 0, 0, 1, PageSize, BootPageAllocatorMutationStatus.InvalidNodeShape);
    }

    private static bool TestShadowCorruptionDetection()
    {
        StressArena arena = StressArena.CreateSingleRegion(baseAddress: 0x0090_0000, totalPages: 12, regionStartPage: 0, regionPageCount: 12, logicalNodeCount: 8, nodeCapacity: 10);
        arena.Allocate(PageSize, 0xA5A5_A5A5_A5A5_A5A5, step: 1);
        arena.Allocate(2 * PageSize, 0x5A5A_5A5A_5A5A_5A5A, step: 2);
        if (!arena.Validate(out _) || !arena.CorruptActualContentAndDetect(out string contentReason) || !contentReason.StartsWith("content_mismatch:", StringComparison.Ordinal) || !arena.CorruptShadowOwnershipAndDetect(out string ownershipReason) || !ownershipReason.StartsWith("ownership_mismatch:", StringComparison.Ordinal) || !arena.Validate(out _))
        {
            return false;
        }

        arena.FreeTop(step: 3);
        arena.FreeTop(step: 4);
        return arena.IsAtBaseline;
    }

    private static bool TestStoragePreservation()
    {
        ReplaySummary summary = RunReplay(0x1F83_D9AB_FB41_BD6B, SeedMatrixChurnSteps, streamId: 10);
        return summary.CapacityTailPreserved && summary.ReservationsPreserved && summary.ExactRestorationCount == summary.SuccessfulFrees && summary.ReturnedToBaseline;
    }

    private static bool TestHostOnlyBoundary()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        var host = HostSourceCatalog.Project("tests/OS.HostTests/OS.HostTests.csproj");
        var kernel = HostSourceCatalog.Project("src/OS.Kernel/OS.Kernel.csproj");
        var paths = host.TypeSources(StressSourceOwner).Select(document => document.Path).ToArray();
        if (!StressHostBoundary(root, host, kernel))
            return false;
        foreach (string workload in new[]
        {
            "SingleMinimalApi",
            "MultiMinimalApi"
        })
        {
            var evaluation = HostSourceCatalog.EfiEvaluation(OS.Tooling.EfiOptions.Parse([], true).Profile(), "Release", workload);
            if (!StressEfiBoundary(root, evaluation, paths, workload))
                return EfiClosureFailure(workload, "evaluated_source_boundary");
        }

        return !BootPageAllocatorMutator.PerformsLiveAllocation && !BootPageAllocatorMutator.PerformsLiveFree;
    }

    private static bool EfiClosureFailure(string workloadProfile, string reason, int exitCode = int.MinValue)
    {
        Console.WriteLine("M01_010_EFI_CLOSURE_FAIL configuration=Release profile=" + workloadProfile + " reason=" + reason + (exitCode == int.MinValue ? string.Empty : " code=" + exitCode.ToString(CultureInfo.InvariantCulture)));
        return false;
    }

    private static bool TestMarkerBoundary()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        var catalog = HostSourceCatalog.Project("tests/OS.HostTests/OS.HostTests.csproj");
        if (!StressMarkerShape(catalog))
            return false;
        var familyPaths = catalog.TypeSources(StressSourceOwner).Select(document => Path.GetFullPath(document.Path)).ToHashSet(StringComparer.OrdinalIgnoreCase);
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

            foreach (string path in Directory.EnumerateFiles(scanRoot, "*", SearchOption.AllDirectories))
            {
                if (!familyPaths.Contains(Path.GetFullPath(path)) && !IsGeneratedScanPath(path) && HasTextExtension(path) && File.ReadAllText(path).Contains(ReadyMarker, StringComparison.Ordinal))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static ReplaySummary RunReplay(ulong seed, int churnSteps, int streamId)
    {
        StressArena arena = StressArena.CreateCanonical();
        DeterministicRandom random = new(seed);
        using CanonicalTranscript transcript = new();
        transcript.AddUInt64(seed);
        transcript.AddInt32(streamId);
        transcript.AddInt32(churnSteps);
        transcript.AddInt32(AllocationWeight);
        transcript.AddInt32(FreeWeight);
        arena.AppendTranscript(transcript, step: -1, operation: ReplayOperation.Initial, selectorWord: 0, inputWord: 0, tokenWord: 0, default);
        int step = 0;
        for (; step < churnSteps; step++)
        {
            int selection = random.NextBounded(100, out ulong selectorWord);
            int alignmentIndex = random.NextBounded(7, out ulong inputWord);
            ulong tokenWord = random.NextUInt64();
            bool allocateSelected = selection < AllocationWeight;
            ReplayOperation operation;
            OperationOutcome outcome;
            if (arena.AllocationDepth == 0)
            {
                operation = allocateSelected ? ReplayOperation.Allocate : ReplayOperation.AllocateEmptyFallback;
                outcome = arena.Allocate(PageSize << alignmentIndex, ContentTag(seed, step, tokenWord), step);
            }
            else if (arena.AllocationDepth >= MaximumReplayDepth)
            {
                operation = allocateSelected ? ReplayOperation.FreeDepthFallback : ReplayOperation.Free;
                outcome = arena.FreeTop(step);
            }
            else if (allocateSelected)
            {
                operation = ReplayOperation.Allocate;
                outcome = arena.Allocate(PageSize << alignmentIndex, ContentTag(seed, step, tokenWord), step);
            }
            else
            {
                operation = ReplayOperation.Free;
                outcome = arena.FreeTop(step);
            }

            arena.AppendTranscript(transcript, step, operation, selectorWord, inputWord, tokenWord, outcome);
        }

        while (arena.AllocationDepth != 0)
        {
            OperationOutcome outcome = arena.FreeTop(step);
            arena.AppendTranscript(transcript, step, ReplayOperation.DrainFree, 0, 0, 0, outcome);
            step++;
        }

        int exhaustionGuard = arena.UsablePageCount + 1;
        while (arena.HasFreePage && exhaustionGuard-- > 0)
        {
            ulong tokenWord = Mix64(seed ^ (ulong)step);
            OperationOutcome outcome = arena.Allocate(PageSize, ContentTag(seed, step, tokenWord), step);
            if (!outcome.Succeeded)
            {
                throw ReplayFailure(seed, streamId, step, ReplayOperation.ExhaustionAllocate, "unexpected_exhaustion_before_model_empty", outcome.Status, arena);
            }

            arena.AppendTranscript(transcript, step, ReplayOperation.ExhaustionAllocate, 0, 0, tokenWord, outcome);
            step++;
        }

        if (exhaustionGuard < 0 || arena.HasFreePage)
        {
            throw ReplayFailure(seed, streamId, step, ReplayOperation.ExhaustionAllocate, "exhaustion_guard", BootPageAllocatorMutationStatus.NoSuitableRange, arena);
        }

        OperationOutcome exhausted = arena.Allocate(PageSize, ContentTag(seed, step, Mix64((ulong)step)), step);
        if (exhausted.Succeeded || exhausted.Status != BootPageAllocatorMutationStatus.NoSuitableRange)
        {
            throw ReplayFailure(seed, streamId, step, ReplayOperation.ExhaustionProbe, "expected_no_suitable_range", exhausted.Status, arena);
        }

        arena.AppendTranscript(transcript, step, ReplayOperation.ExhaustionProbe, 0, 0, 0, exhausted);
        step++;
        while (arena.AllocationDepth != 0)
        {
            OperationOutcome outcome = arena.FreeTop(step);
            arena.AppendTranscript(transcript, step, ReplayOperation.ExhaustionDrainFree, 0, 0, 0, outcome);
            step++;
        }

        if (!arena.Validate(out string reason))
        {
            throw ReplayFailure(seed, streamId, step, ReplayOperation.Final, reason, BootPageAllocatorMutationStatus.InvalidStateAccounting, arena);
        }

        arena.AppendTranscript(transcript, step, ReplayOperation.Final, 0, 0, 0, default);
        return arena.CreateSummary(seed, churnSteps, step + 2, transcript.Finish());
    }

    private static bool RunShape(ulong baseAddress, int totalPages, int regionStartPage, int regionPageCount, ulong alignment, BootPageAllocatorOnePageCoalescingKind expectedKind)
    {
        StressArena arena = StressArena.CreateSingleRegion(baseAddress, totalPages, regionStartPage, regionPageCount, logicalNodeCount: 4, nodeCapacity: 6);
        OperationOutcome allocation = arena.Allocate(alignment, ContentTag(baseAddress, regionPageCount, alignment), step: 1);
        if (!allocation.Succeeded)
        {
            return false;
        }

        OperationOutcome free = arena.FreeTop(step: 2);
        return free.Succeeded && free.CoalescingKind == expectedKind && arena.IsAtBaseline;
    }

    private static Exception ReplayFailure(ulong seed, int streamId, int step, ReplayOperation operation, string reason, BootPageAllocatorMutationStatus status, StressArena arena)
    {
        return new InvalidOperationException("M01_010_REPRO seed=0x" + seed.ToString("X16", CultureInfo.InvariantCulture) + " stream=" + streamId.ToString(CultureInfo.InvariantCulture) + " step=" + step.ToString(CultureInfo.InvariantCulture) + " op=" + operation + " status=" + status + " depth=" + arena.AllocationDepth.ToString(CultureInfo.InvariantCulture) + " reason=" + reason);
    }

    private static unsafe bool HighBoundaryRoundTrip()
    {
        BootPageFreeListNode[] nodes = [Node(0, 0, 0xFFFF_FFFF_FFFF_E000, 1, -1), default];
        byte[] original = StructArrayBytes(nodes);
        BootPageAllocatorState state = State(0, 1, 1, 0);
        fixed (BootPageFreeListNode* nodePointer = nodes)
        {
            BootPageAllocatorOnePageResult allocation = BootPageAllocatorMutator.AllocateOnePage(state, nodePointer, nodeCount: 1, nodeCapacity: 2, reservations: null, reservationCount: 0, reservationCapacity: 0, pageCount: 1, alignment: PageSize);
            if (!allocation.Succeeded || !allocation.CanRestore || Raw(allocation.PhysicalStart) != 0xFFFF_FFFF_FFFF_E000 || Raw(allocation.PhysicalEndExclusive) != 0xFFFF_FFFF_FFFF_F000)
            {
                return false;
            }

            BootPageAllocatorOnePageFreeResult free = BootPageAllocatorMutator.FreeOnePage(allocation.AfterState, nodePointer, nodeCount: 1, nodeCapacity: 2, reservations: null, reservationCount: 0, reservationCapacity: 0, allocation);
            return free.Succeeded && free.CoalescingKind == BootPageAllocatorOnePageCoalescingKind.None && original.AsSpan().SequenceEqual(StructArrayBytes(nodes));
        }
    }

    private static unsafe bool AllocationFailurePreserves(BootPageAllocatorState state, BootPageFreeListNode[] nodes, ulong nodeCount, ulong nodeCapacity, BootReservedRange[] reservations, ulong reservationCount, ulong reservationCapacity, ulong pageCount, ulong alignment, BootPageAllocatorMutationStatus expectedStatus, AddressValueStatus expectedAddressStatus = AddressValueStatus.Success)
    {
        byte[] nodeBytes = StructArrayBytes(nodes);
        byte[] reservationBytes = StructArrayBytes(reservations);
        fixed (BootPageFreeListNode* nodePointer = nodes)
        fixed (BootReservedRange* reservationPointer = reservations)
        {
            BootPageAllocatorOnePageResult result = BootPageAllocatorMutator.AllocateOnePage(state, nodePointer, nodeCount, nodeCapacity, reservationPointer, reservationCount, reservationCapacity, pageCount, alignment);
            return IsAllocationFailure(result, state, expectedStatus, expectedAddressStatus) && nodeBytes.AsSpan().SequenceEqual(StructArrayBytes(nodes)) && reservationBytes.AsSpan().SequenceEqual(StructArrayBytes(reservations));
        }
    }

    private static bool IsAllocationFailure(BootPageAllocatorOnePageResult result, BootPageAllocatorState state, BootPageAllocatorMutationStatus expectedStatus, AddressValueStatus expectedAddressStatus = AddressValueStatus.Success)
    {
        return result.IsInitialized && !result.Succeeded && !result.CanRestore && result.Status == expectedStatus && result.AddressStatus == expectedAddressStatus && StatesEqual(result.BeforeState, state) && StatesEqual(result.AfterState, state);
    }

    private static bool IsFreeFailure(BootPageAllocatorOnePageFreeResult result, BootPageAllocatorState state, BootPageAllocatorOnePageResult allocation, BootPageAllocatorMutationStatus expectedStatus)
    {
        return result.IsInitialized && !result.Succeeded && result.Status == expectedStatus && StatesEqual(result.BeforeState, state) && StatesEqual(result.AfterState, state) && AllocationEvidenceEqual(result.Allocation, allocation);
    }

    private static BootPageAllocatorState State(int headNodeIndex, int freeNodeCount, ulong freePages, ulong allocatedPages)
    {
        return new BootPageAllocatorState(isInitialized: true, PageSize, headNodeIndex, freeNodeCount, freePages, checked(freePages * PageSize), allocatedPages, checked(allocatedPages * PageSize));
    }

    private static BootPageFreeListNode Node(int sourceFreeSegmentIndex, int sourceUsableRangeIndex, ulong physicalStart, ulong pageCount, int nextNodeIndex)
    {
        ulong byteLength = checked(pageCount * PageSize);
        return new BootPageFreeListNode(sourceFreeSegmentIndex, sourceUsableRangeIndex, physicalStart, checked(physicalStart + byteLength), pageCount, byteLength, nextNodeIndex);
    }

    private static BootPageFreeListNode CapacityTailCanary(int ordinal)
    {
        ulong start = checked(0x7000_0000UL + (ulong)ordinal * 0x20_000);
        return Node(5000 + ordinal, 6000 + ordinal, start, 2, -1);
    }

    private static BootReservedRange Reservation(BootReservationKind kind, ulong physicalStart, ulong pageCount)
    {
        BootReservedRangeStatus status = BootReservedRange.TryCreate(kind, Address(physicalStart), pageCount, out BootReservedRange reservation);
        if (status != BootReservedRangeStatus.Success)
        {
            throw new InvalidOperationException("reservation_create_failed:" + status);
        }

        return reservation;
    }

    private static PhysicalAddress Address(ulong raw)
    {
        AddressValueStatus status = PhysicalAddress.TryCreateFromUefiBoundary(raw, out PhysicalAddress address);
        if (status != AddressValueStatus.Success)
        {
            throw new InvalidOperationException("address_create_failed:" + status);
        }

        return address;
    }

    private static ulong Raw(PhysicalAddress address)
    {
        AddressValueStatus status = address.TryGetRawValueForUefiBoundary(out ulong raw);
        if (status != AddressValueStatus.Success)
        {
            throw new InvalidOperationException("address_read_failed:" + status);
        }

        return raw;
    }

    private static byte[] StructArrayBytes<T>(T[] values)
        where T : struct
    {
        return MemoryMarshal.AsBytes(values.AsSpan()).ToArray();
    }

    private static bool StatesEqual(BootPageAllocatorState left, BootPageAllocatorState right)
    {
        return left.IsInitialized == right.IsInitialized && left.PageSize == right.PageSize && left.HeadNodeIndex == right.HeadNodeIndex && left.FreeNodeCount == right.FreeNodeCount && left.FreePageCount == right.FreePageCount && left.FreeByteCount == right.FreeByteCount && left.AllocatedPageCount == right.AllocatedPageCount && left.AllocatedByteCount == right.AllocatedByteCount;
    }

    private static bool NodesEqual(BootPageFreeListNode left, BootPageFreeListNode right)
    {
        return left.SourceFreeSegmentIndex == right.SourceFreeSegmentIndex && left.SourceUsableRangeIndex == right.SourceUsableRangeIndex && left.PhysicalStart == right.PhysicalStart && left.PhysicalEndExclusive == right.PhysicalEndExclusive && left.PageCount == right.PageCount && left.ByteLength == right.ByteLength && left.NextNodeIndex == right.NextNodeIndex;
    }

    private static bool AllocationEvidenceEqual(BootPageAllocatorOnePageResult left, BootPageAllocatorOnePageResult right)
    {
        return left.IsInitialized == right.IsInitialized && left.Status == right.Status && left.ReservationStatus == right.ReservationStatus && left.AddressStatus == right.AddressStatus && StatesEqual(left.BeforeState, right.BeforeState) && StatesEqual(left.AfterState, right.AfterState) && left.PhysicalStart.Equals(right.PhysicalStart) && left.PhysicalEndExclusive.Equals(right.PhysicalEndExclusive) && left.PageCount == right.PageCount && left.ByteLength == right.ByteLength && left.NodeCount == right.NodeCount && left.NodeCapacity == right.NodeCapacity && left.ReservationCount == right.ReservationCount && left.ReservationCapacity == right.ReservationCapacity && left.NodeIndex == right.NodeIndex && left.InsertedNodeIndex == right.InsertedNodeIndex && left.VisitedNodeCount == right.VisitedNodeCount && left.InvalidReservationIndex == right.InvalidReservationIndex && left.ConflictingIndex == right.ConflictingIndex && left.RestoreReceipt.IsInitialized == right.RestoreReceipt.IsInitialized && NodesEqual(left.RestoreReceipt.SourceNodeBefore, right.RestoreReceipt.SourceNodeBefore) && left.RestoreReceipt.PreviousNodeIndexBefore == right.RestoreReceipt.PreviousNodeIndexBefore && NodesEqual(left.RestoreReceipt.PreviousNodeBefore, right.RestoreReceipt.PreviousNodeBefore);
    }

    private static ulong ContentTag(ulong seed, int step, ulong randomWord)
    {
        ulong tag = Mix64(seed ^ randomWord ^ unchecked((ulong)(uint)step * 0x9E37_79B9_7F4A_7C15));
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
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    private static bool HasTextExtension(string path)
    {
        string extension = Path.GetExtension(path);
        return extension.Equals(".cs", StringComparison.OrdinalIgnoreCase) || extension.Equals(".csproj", StringComparison.OrdinalIgnoreCase) || extension.Equals(".props", StringComparison.OrdinalIgnoreCase) || extension.Equals(".targets", StringComparison.OrdinalIgnoreCase) || extension.Equals(".md", StringComparison.OrdinalIgnoreCase) || extension.Equals(".json", StringComparison.OrdinalIgnoreCase) || extension.Equals(".ps1", StringComparison.OrdinalIgnoreCase) || extension.Equals(".yml", StringComparison.OrdinalIgnoreCase) || extension.Equals(".yaml", StringComparison.OrdinalIgnoreCase) || extension.Equals(".slnx", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsGeneratedScanPath(string path)
    {
        string fullPath = Path.GetFullPath(path);
        foreach (string segment in new[]
        {
            "bin",
            "obj",
            "artifacts"
        }

        )
        {
            string needle = Path.DirectorySeparatorChar + segment + Path.DirectorySeparatorChar;
            if (fullPath.Contains(needle, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
