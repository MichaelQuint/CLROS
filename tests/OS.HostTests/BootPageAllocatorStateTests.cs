using System.Runtime.CompilerServices;
using OS.Boot;
using OS.Kernel.Memory;

internal static class BootPageAllocatorStateTests
{
    private const ulong PageSize = BootMemoryRegionRecord.PageSize;

    [HostTest("memory.boot allocator state initializes", HostLane.Fast, 0)]
    private static bool TestInitializes()
    {
        Span<BootPageFreeListNode> nodes = stackalloc BootPageFreeListNode[2];
        BootPageFreeListPlanResult freeListPlan = CreateReadyFreeListPlan(nodes);

        BootPageAllocatorStateInitResult result = BootPageAllocatorStateInitializer.Initialize(
            freeListPlan,
            nodes);

        return HostCheck.Result(result.Succeeded
            && result.Status == BootPageAllocatorStateStatus.Success
            && result.FreeListPlanStatus == BootPageFreeListPlanStatus.Success
            && result.RequiredNodeCapacity == 2
            && result.AvailableNodeCapacity == 2
            && result.VisitedNodeCount == 2
            && result.InvalidNodeIndex == -1
            && result.PageSize == PageSize
            && result.TotalFreePages == 10
            && result.TotalFreeBytes == 10 * PageSize
            && result.State.IsInitialized
            && result.State.PageSize == PageSize
            && result.State.HeadNodeIndex == 0
            && result.State.FreeNodeCount == 2
            && result.State.FreePageCount == 10
            && result.State.FreeByteCount == 10 * PageSize
            && result.State.AllocatedPageCount == 0
            && result.State.AllocatedByteCount == 0);
    }

    [HostTest("memory.boot allocator state rejects failed free-list plan", HostLane.Fast, 1)]
    private static bool TestRejectsFailedFreeListPlan()
    {
        Span<BootMemoryUsableRange> usable = stackalloc BootMemoryUsableRange[1];
        usable[0] = Usable(0, 0x100000, 1);
        Span<BootMemoryReservedRange> reserved = stackalloc BootMemoryReservedRange[0];
        Span<BootPageFreeSegment> freeSegments = stackalloc BootPageFreeSegment[1];

        BootPageAllocatorPlanResult allocatorPlan = BootPageAllocatorPlanner.Plan(
            pageSize: 0,
            usable,
            reserved,
            freeSegments);

        Span<BootPageFreeListNode> nodes = stackalloc BootPageFreeListNode[1];
        BootPageFreeListPlanResult freeListPlan = BootPageFreeListBuilder.Build(
            allocatorPlan,
            freeSegments,
            nodes);

        BootPageAllocatorStateInitResult result = BootPageAllocatorStateInitializer.Initialize(
            freeListPlan,
            nodes);

        return HostCheck.Result(!result.Succeeded
            && result.Status == BootPageAllocatorStateStatus.FreeListPlanFailed
            && result.FreeListPlanStatus == BootPageFreeListPlanStatus.AllocatorPlanFailed
            && result.RequiredNodeCapacity == 0
            && result.AvailableNodeCapacity == 1
            && result.VisitedNodeCount == 0
            && result.InvalidNodeIndex == -1
            && !result.State.IsInitialized);
    }

    [HostTest("memory.boot allocator state rejects empty free-list", HostLane.Fast, 2)]
    private static bool TestRejectsEmptyFreeList()
    {
        Span<BootMemoryUsableRange> usable = stackalloc BootMemoryUsableRange[0];
        Span<BootMemoryReservedRange> reserved = stackalloc BootMemoryReservedRange[0];
        Span<BootPageFreeSegment> freeSegments = stackalloc BootPageFreeSegment[0];
        Span<BootPageFreeListNode> nodes = stackalloc BootPageFreeListNode[0];

        BootPageAllocatorPlanResult allocatorPlan = BootPageAllocatorPlanner.Plan(
            PageSize,
            usable,
            reserved,
            freeSegments);
        BootPageFreeListPlanResult freeListPlan = BootPageFreeListBuilder.Build(
            allocatorPlan,
            freeSegments,
            nodes);

        BootPageAllocatorStateInitResult result = BootPageAllocatorStateInitializer.Initialize(
            freeListPlan,
            nodes);

        return HostCheck.Result(allocatorPlan.Succeeded
            && freeListPlan.Succeeded
            && !result.Succeeded
            && result.Status == BootPageAllocatorStateStatus.NoFreeNodes
            && result.FreeListPlanStatus == BootPageFreeListPlanStatus.Success
            && result.RequiredNodeCapacity == 0
            && result.AvailableNodeCapacity == 0
            && result.State.IsInitialized == false);
    }

    [HostTest("memory.boot allocator state rejects source capacity", HostLane.Fast, 3)]
    private static bool TestRejectsSourceCapacity()
    {
        Span<BootPageFreeListNode> nodes = stackalloc BootPageFreeListNode[2];
        BootPageFreeListPlanResult freeListPlan = CreateReadyFreeListPlan(nodes);

        Span<BootPageFreeListNode> tooSmall = stackalloc BootPageFreeListNode[1];
        tooSmall[0] = nodes[0];
        BootPageAllocatorStateInitResult result = BootPageAllocatorStateInitializer.Initialize(
            freeListPlan,
            tooSmall);

        return HostCheck.Result(!result.Succeeded
            && result.Status == BootPageAllocatorStateStatus.SourceNodeCapacityTooSmall
            && result.RequiredNodeCapacity == 2
            && result.AvailableNodeCapacity == 1
            && result.VisitedNodeCount == 0
            && result.InvalidNodeIndex == -1
            && result.TotalFreePages == 0
            && result.TotalFreeBytes == 0);
    }

    [HostTest("memory.boot allocator state rejects corrupted node shape", HostLane.Fast, 4)]
    private static bool TestRejectsCorruptedNodeShape()
    {
        BootPageAllocatorStateInitResult zeroPages = InitializeWithCorruptedFirstNode(
            new BootPageFreeListNode(
                sourceFreeSegmentIndex: 0,
                sourceUsableRangeIndex: 0,
                physicalStart: 0x100000,
                physicalEndExclusive: 0x100000,
                pageCount: 0,
                byteLength: 0,
                nextNodeIndex: 1));

        BootPageAllocatorStateInitResult unaligned = InitializeWithCorruptedFirstNode(
            new BootPageFreeListNode(
                sourceFreeSegmentIndex: 0,
                sourceUsableRangeIndex: 0,
                physicalStart: 0x100001,
                physicalEndExclusive: 0x104001,
                pageCount: 4,
                byteLength: 4 * PageSize,
                nextNodeIndex: 1));

        BootPageAllocatorStateInitResult byteOverflow = InitializeWithCorruptedFirstNode(
            new BootPageFreeListNode(
                sourceFreeSegmentIndex: 0,
                sourceUsableRangeIndex: 0,
                physicalStart: 0x100000,
                physicalEndExclusive: 0,
                pageCount: (ulong.MaxValue / PageSize) + 1,
                byteLength: 0,
                nextNodeIndex: 1));

        BootPageAllocatorStateInitResult byteMismatch = InitializeWithCorruptedFirstNode(
            new BootPageFreeListNode(
                sourceFreeSegmentIndex: 0,
                sourceUsableRangeIndex: 0,
                physicalStart: 0x100000,
                physicalEndExclusive: 0x104000,
                pageCount: 4,
                byteLength: PageSize,
                nextNodeIndex: 1));

        BootPageAllocatorStateInitResult addressOverflow = InitializeWithCorruptedFirstNode(
            new BootPageFreeListNode(
                sourceFreeSegmentIndex: 0,
                sourceUsableRangeIndex: 0,
                physicalStart: ulong.MaxValue - (PageSize - 1),
                physicalEndExclusive: 0,
                pageCount: 1,
                byteLength: PageSize,
                nextNodeIndex: 1));

        BootPageAllocatorStateInitResult endMismatch = InitializeWithCorruptedFirstNode(
            new BootPageFreeListNode(
                sourceFreeSegmentIndex: 0,
                sourceUsableRangeIndex: 0,
                physicalStart: 0x100000,
                physicalEndExclusive: 0x105000,
                pageCount: 4,
                byteLength: 4 * PageSize,
                nextNodeIndex: 1));

        return HostCheck.Result(!zeroPages.Succeeded
            && zeroPages.Status == BootPageAllocatorStateStatus.InvalidNodePageCount
            && zeroPages.InvalidNodeIndex == 0
            && !unaligned.Succeeded
            && unaligned.Status == BootPageAllocatorStateStatus.UnalignedNodePhysicalStart
            && !byteOverflow.Succeeded
            && byteOverflow.Status == BootPageAllocatorStateStatus.NodeByteCountOverflow
            && !byteMismatch.Succeeded
            && byteMismatch.Status == BootPageAllocatorStateStatus.NodeByteLengthMismatch
            && !addressOverflow.Succeeded
            && addressOverflow.Status == BootPageAllocatorStateStatus.NodeAddressRangeOverflow
            && !endMismatch.Succeeded
            && endMismatch.Status == BootPageAllocatorStateStatus.NodePhysicalEndMismatch);
    }

    [HostTest("memory.boot allocator state rejects invalid chain", HostLane.Fast, 5)]
    private static bool TestRejectsInvalidChain()
    {
        Span<BootPageFreeListNode> invalidNextNodes = stackalloc BootPageFreeListNode[2];
        BootPageFreeListPlanResult invalidNextPlan = CreateReadyFreeListPlan(invalidNextNodes);
        invalidNextNodes[0] = new BootPageFreeListNode(
            sourceFreeSegmentIndex: 0,
            sourceUsableRangeIndex: 0,
            physicalStart: 0x100000,
            physicalEndExclusive: 0x100000 + (4 * PageSize),
            pageCount: 4,
            byteLength: 4 * PageSize,
            nextNodeIndex: 2);
        BootPageAllocatorStateInitResult invalidNext = BootPageAllocatorStateInitializer.Initialize(
            invalidNextPlan,
            invalidNextNodes);

        Span<BootPageFreeListNode> cycleNodes = stackalloc BootPageFreeListNode[2];
        BootPageFreeListPlanResult cyclePlan = CreateReadyFreeListPlan(cycleNodes);
        cycleNodes[1] = new BootPageFreeListNode(
            sourceFreeSegmentIndex: 1,
            sourceUsableRangeIndex: 1,
            physicalStart: 0x300000,
            physicalEndExclusive: 0x300000 + (6 * PageSize),
            pageCount: 6,
            byteLength: 6 * PageSize,
            nextNodeIndex: 0);
        BootPageAllocatorStateInitResult cycle = BootPageAllocatorStateInitializer.Initialize(
            cyclePlan,
            cycleNodes);

        Span<BootPageFreeListNode> shortChainNodes = stackalloc BootPageFreeListNode[2];
        BootPageFreeListPlanResult shortChainPlan = CreateReadyFreeListPlan(shortChainNodes);
        shortChainNodes[0] = new BootPageFreeListNode(
            sourceFreeSegmentIndex: 0,
            sourceUsableRangeIndex: 0,
            physicalStart: 0x100000,
            physicalEndExclusive: 0x100000 + (4 * PageSize),
            pageCount: 4,
            byteLength: 4 * PageSize,
            nextNodeIndex: -1);
        BootPageAllocatorStateInitResult shortChain = BootPageAllocatorStateInitializer.Initialize(
            shortChainPlan,
            shortChainNodes);

        return HostCheck.Result(!invalidNext.Succeeded
            && invalidNext.Status == BootPageAllocatorStateStatus.InvalidNextNodeIndex
            && invalidNext.InvalidNodeIndex == 2
            && invalidNext.VisitedNodeCount == 1
            && !cycle.Succeeded
            && cycle.Status == BootPageAllocatorStateStatus.FreeListCycleDetected
            && cycle.InvalidNodeIndex == 0
            && cycle.VisitedNodeCount == 2
            && !shortChain.Succeeded
            && shortChain.Status == BootPageAllocatorStateStatus.FreeListNodeCountMismatch
            && shortChain.VisitedNodeCount == 1);
    }

    [HostTest("memory.boot allocator state default result is not success", HostLane.Fast, 6)]
    private static bool TestDefaultResultIsNotSuccess()
    {
        BootPageAllocatorStateInitResult result = default;

        return HostCheck.Result(!result.Succeeded
            && result.Status == BootPageAllocatorStateStatus.Success
            && result.State.IsInitialized == false);
    }

    [HostTest("memory.boot allocator state layouts contain no managed references", HostLane.Fast, 7)]
    private static bool TestLayoutsContainNoManagedReferences()
    {
        return HostCheck.Result(!RuntimeHelpers.IsReferenceOrContainsReferences<BootPageAllocatorState>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<BootPageAllocatorStateInitResult>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<BootPageFreeListNode>());
    }

    private static BootPageAllocatorStateInitResult InitializeWithCorruptedFirstNode(
        BootPageFreeListNode corruptedNode)
    {
        Span<BootPageFreeListNode> nodes = stackalloc BootPageFreeListNode[2];
        BootPageFreeListPlanResult freeListPlan = CreateReadyFreeListPlan(nodes);
        nodes[0] = corruptedNode;
        return BootPageAllocatorStateInitializer.Initialize(freeListPlan, nodes);
    }

    private static BootPageFreeListPlanResult CreateReadyFreeListPlan(Span<BootPageFreeListNode> nodes)
    {
        Span<BootPageFreeSegment> freeSegments = stackalloc BootPageFreeSegment[2];
        BootPageAllocatorPlanResult allocatorPlan = CreateReadyAllocatorPlan(freeSegments);
        return BootPageFreeListBuilder.Build(allocatorPlan, freeSegments, nodes);
    }

    private static BootPageAllocatorPlanResult CreateReadyAllocatorPlan(Span<BootPageFreeSegment> freeSegments)
    {
        Span<BootMemoryUsableRange> usable = stackalloc BootMemoryUsableRange[2];
        usable[0] = Usable(0, 0x100000, 4);
        usable[1] = Usable(1, 0x300000, 6);

        Span<BootMemoryReservedRange> reserved = stackalloc BootMemoryReservedRange[1];
        reserved[0] = Reserved(2, 0x200000, 1);

        return BootPageAllocatorPlanner.Plan(PageSize, usable, reserved, freeSegments);
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

    private static BootMemoryReservedRange Reserved(
        int sourceRegionIndex,
        ulong physicalStart,
        ulong pageCount)
    {
        ulong byteLength = pageCount * PageSize;
        return new BootMemoryReservedRange(
            sourceRegionIndex,
            (uint)UefiMemoryType.LoaderData,
            physicalStart,
            physicalStart + byteLength,
            pageCount,
            byteLength,
            attribute: 0);
    }
}
