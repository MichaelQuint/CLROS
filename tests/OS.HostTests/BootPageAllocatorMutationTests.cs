using System.Runtime.CompilerServices;
using OS.Boot;
using OS.Kernel.Memory;

internal static class BootPageAllocatorMutationTests
{
    private const ulong PageSize = BootMemoryRegionRecord.PageSize;

    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("memory.boot allocator mutates allocation", TestAllocatesFromHead, ref failures);
        Run("memory.boot allocator splits aligned range", TestSplitsAlignedRange, ref failures);
        Run("memory.boot allocator frees and coalesces", TestFreesAndCoalesces, ref failures);
        Run("memory.boot allocator frees empty list", TestFreesIntoEmptyList, ref failures);
        Run("memory.boot allocator rejects invalid mutation", TestRejectsInvalidMutation, ref failures);
        Run("memory.boot allocator mutation default result is not success", TestDefaultResultIsNotSuccess, ref failures);
        Run("memory.boot allocator mutation layouts contain no managed references", TestLayoutsContainNoManagedReferences, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("Boot page allocator mutation host tests failed: " + failures);
        }
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
            Console.WriteLine("TEST_FAIL " + name + " " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static bool TestAllocatesFromHead()
    {
        Span<BootPageFreeListNode> nodes = stackalloc BootPageFreeListNode[2];
        BootPageAllocatorState state = CreateReadyState(nodes);

        BootPageAllocatorMutationResult result =
            BootPageAllocatorMutator.Allocate(state, nodes, pageCount: 2, alignment: PageSize);

        return result.Succeeded
            && result.Status == BootPageAllocatorMutationStatus.Success
            && result.PhysicalStart == 0x100000
            && result.PhysicalEndExclusive == 0x102000
            && result.PageCount == 2
            && result.ByteLength == 2 * PageSize
            && result.NodeIndex == 0
            && result.InsertedNodeIndex == -1
            && result.State.HeadNodeIndex == 0
            && result.State.FreeNodeCount == 2
            && result.State.FreePageCount == 8
            && result.State.FreeByteCount == 8 * PageSize
            && result.State.AllocatedPageCount == 2
            && result.State.AllocatedByteCount == 2 * PageSize
            && nodes[0].PhysicalStart == 0x102000
            && nodes[0].PhysicalEndExclusive == 0x104000
            && nodes[0].PageCount == 2
            && nodes[0].NextNodeIndex == 1;
    }

    private static bool TestSplitsAlignedRange()
    {
        Span<BootPageFreeListNode> nodes = stackalloc BootPageFreeListNode[2];
        BootPageAllocatorState state = CreateSingleRangeState(
            nodes,
            physicalStart: 0x101000,
            pageCount: 8);

        BootPageAllocatorMutationResult result =
            BootPageAllocatorMutator.Allocate(state, nodes, pageCount: 1, alignment: 4 * PageSize);

        return result.Succeeded
            && result.PhysicalStart == 0x104000
            && result.PhysicalEndExclusive == 0x105000
            && result.InsertedNodeIndex == 1
            && result.State.FreeNodeCount == 2
            && result.State.FreePageCount == 7
            && nodes[0].PhysicalStart == 0x101000
            && nodes[0].PhysicalEndExclusive == 0x104000
            && nodes[0].PageCount == 3
            && nodes[0].NextNodeIndex == 1
            && nodes[1].PhysicalStart == 0x105000
            && nodes[1].PhysicalEndExclusive == 0x109000
            && nodes[1].PageCount == 4
            && nodes[1].NextNodeIndex == -1;
    }

    private static bool TestFreesAndCoalesces()
    {
        Span<BootPageFreeListNode> nodes = stackalloc BootPageFreeListNode[2];
        BootPageAllocatorState state = CreateReadyState(nodes);
        BootPageAllocatorMutationResult allocation =
            BootPageAllocatorMutator.Allocate(state, nodes, pageCount: 2, alignment: PageSize);

        BootPageAllocatorMutationResult free =
            BootPageAllocatorMutator.Free(allocation.State, nodes, allocation.PhysicalStart, allocation.PageCount);

        return allocation.Succeeded
            && free.Succeeded
            && free.State.FreeNodeCount == 2
            && free.State.FreePageCount == 10
            && free.State.AllocatedPageCount == 0
            && nodes[0].PhysicalStart == 0x100000
            && nodes[0].PhysicalEndExclusive == 0x104000
            && nodes[0].PageCount == 4
            && nodes[0].NextNodeIndex == 1;
    }

    private static bool TestFreesIntoEmptyList()
    {
        Span<BootPageFreeListNode> nodes = stackalloc BootPageFreeListNode[1];
        BootPageAllocatorState state = CreateSingleRangeState(
            nodes,
            physicalStart: 0x500000,
            pageCount: 1);

        BootPageAllocatorMutationResult allocation =
            BootPageAllocatorMutator.Allocate(state, nodes, pageCount: 1, alignment: PageSize);
        BootPageAllocatorMutationResult free =
            BootPageAllocatorMutator.Free(allocation.State, nodes, allocation.PhysicalStart, allocation.PageCount);

        return allocation.Succeeded
            && allocation.State.HeadNodeIndex == -1
            && allocation.State.FreeNodeCount == 0
            && free.Succeeded
            && free.InsertedNodeIndex == 0
            && free.State.HeadNodeIndex == 0
            && free.State.FreeNodeCount == 1
            && free.State.FreePageCount == 1
            && free.State.AllocatedPageCount == 0
            && nodes[0].PhysicalStart == 0x500000
            && nodes[0].PhysicalEndExclusive == 0x501000
            && nodes[0].NextNodeIndex == -1;
    }

    private static bool TestRejectsInvalidMutation()
    {
        Span<BootPageFreeListNode> nodes = stackalloc BootPageFreeListNode[2];
        BootPageAllocatorState state = CreateReadyState(nodes);

        BootPageAllocatorMutationResult zeroPages =
            BootPageAllocatorMutator.Allocate(state, nodes, pageCount: 0, alignment: PageSize);
        BootPageAllocatorMutationResult invalidAlignment =
            BootPageAllocatorMutator.Allocate(state, nodes, pageCount: 1, alignment: PageSize + 1);
        BootPageAllocatorMutationResult noRange =
            BootPageAllocatorMutator.Allocate(state, nodes, pageCount: 1024, alignment: PageSize);

        Span<BootPageFreeListNode> splitNodes = stackalloc BootPageFreeListNode[1];
        BootPageAllocatorState splitState = CreateSingleRangeState(
            splitNodes,
            physicalStart: 0x101000,
            pageCount: 8);
        BootPageAllocatorMutationResult noMetadata =
            BootPageAllocatorMutator.Allocate(splitState, splitNodes, pageCount: 1, alignment: 4 * PageSize);

        BootPageAllocatorMutationResult allocation =
            BootPageAllocatorMutator.Allocate(state, nodes, pageCount: 1, alignment: PageSize);
        BootPageAllocatorMutationResult overlap =
            BootPageAllocatorMutator.Free(
                allocation.State,
                nodes,
                nodes[0].PhysicalStart,
                pageCount: 1);
        BootPageAllocatorMutationResult notAllocated =
            BootPageAllocatorMutator.Free(state, nodes, physicalStart: 0x800000, pageCount: 1);

        return !zeroPages.Succeeded
            && zeroPages.Status == BootPageAllocatorMutationStatus.InvalidPageCount
            && !invalidAlignment.Succeeded
            && invalidAlignment.Status == BootPageAllocatorMutationStatus.InvalidAlignment
            && !noRange.Succeeded
            && noRange.Status == BootPageAllocatorMutationStatus.NoSuitableRange
            && !noMetadata.Succeeded
            && noMetadata.Status == BootPageAllocatorMutationStatus.MetadataCapacityTooSmall
            && !overlap.Succeeded
            && overlap.Status == BootPageAllocatorMutationStatus.FreeRangeOverlaps
            && !notAllocated.Succeeded
            && notAllocated.Status == BootPageAllocatorMutationStatus.FreeRangeNotAllocated;
    }

    private static bool TestDefaultResultIsNotSuccess()
    {
        BootPageAllocatorMutationResult result = default;

        return !result.Succeeded
            && !result.IsInitialized
            && result.Status == BootPageAllocatorMutationStatus.Success;
    }

    private static bool TestLayoutsContainNoManagedReferences()
    {
        Span<BootPageFreeListNode> nodes = stackalloc BootPageFreeListNode[2];
        BootPageAllocatorState state = CreateReadyState(nodes);
        BootPageAllocatorMutationResult result =
            BootPageAllocatorMutator.Allocate(state, nodes, pageCount: 1, alignment: PageSize);

        return result.Succeeded
            && BootPageAllocatorMutator.MutatesCallerOwnedFreeList
            && !BootPageAllocatorMutator.PerformsLiveAllocation
            && !BootPageAllocatorMutator.ConfiguresPaging
            && !BootPageAllocatorMutator.EnforcesNxOrWx
            && !RuntimeHelpers.IsReferenceOrContainsReferences<BootPageAllocatorMutationResult>();
    }

    private static BootPageAllocatorState CreateReadyState(Span<BootPageFreeListNode> nodes)
    {
        Span<BootPageFreeSegment> freeSegments = stackalloc BootPageFreeSegment[2];
        Span<BootMemoryUsableRange> usable = stackalloc BootMemoryUsableRange[2];
        usable[0] = Usable(0, 0x100000, 4);
        usable[1] = Usable(1, 0x300000, 6);

        Span<BootMemoryReservedRange> reserved = stackalloc BootMemoryReservedRange[0];
        BootPageAllocatorPlanResult allocatorPlan = BootPageAllocatorPlanner.Plan(
            PageSize,
            usable,
            reserved,
            freeSegments);
        BootPageFreeListPlanResult freeListPlan = BootPageFreeListBuilder.Build(
            allocatorPlan,
            freeSegments,
            nodes);
        BootPageAllocatorStateInitResult init =
            BootPageAllocatorStateInitializer.Initialize(freeListPlan, nodes);

        if (!init.Succeeded)
        {
            throw new InvalidOperationException("test allocator state failed: " + init.Status);
        }

        return init.State;
    }

    private static BootPageAllocatorState CreateSingleRangeState(
        Span<BootPageFreeListNode> nodes,
        ulong physicalStart,
        ulong pageCount)
    {
        Span<BootPageFreeSegment> freeSegments = stackalloc BootPageFreeSegment[1];
        Span<BootMemoryUsableRange> usable = stackalloc BootMemoryUsableRange[1];
        usable[0] = Usable(0, physicalStart, pageCount);
        Span<BootMemoryReservedRange> reserved = stackalloc BootMemoryReservedRange[0];

        BootPageAllocatorPlanResult allocatorPlan = BootPageAllocatorPlanner.Plan(
            PageSize,
            usable,
            reserved,
            freeSegments);
        BootPageFreeListPlanResult freeListPlan = BootPageFreeListBuilder.Build(
            allocatorPlan,
            freeSegments,
            nodes);
        BootPageAllocatorStateInitResult init =
            BootPageAllocatorStateInitializer.Initialize(freeListPlan, nodes);

        if (!init.Succeeded)
        {
            throw new InvalidOperationException("single range state failed: " + init.Status);
        }

        return init.State;
    }

    private static BootMemoryUsableRange Usable(
        int sourceRegionIndex,
        ulong physicalStart,
        ulong pageCount)
    {
        ulong byteLength = pageCount * PageSize;
        return new BootMemoryUsableRange(
            sourceRegionIndex,
            physicalStart,
            physicalStart + byteLength,
            pageCount,
            byteLength,
            attribute: 0);
    }
}
