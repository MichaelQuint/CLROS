using System.Runtime.CompilerServices;
using OS.Boot;
using OS.Kernel.Memory;

internal static class BootPageFreeListPlanTests
{
    private const ulong PageSize = BootMemoryRegionRecord.PageSize;

    [HostTest("memory.boot freelist nodes constructs", HostLane.Fast, 0)]
    private static bool TestConstructsNodes()
    {
        Span<BootPageFreeSegment> freeSegments = stackalloc BootPageFreeSegment[2];
        BootPageAllocatorPlanResult allocatorPlan = CreateReadyAllocatorPlan(freeSegments);
        Span<BootPageFreeListNode> nodes = stackalloc BootPageFreeListNode[2];

        BootPageFreeListPlanResult result = BootPageFreeListBuilder.Build(
            allocatorPlan,
            freeSegments,
            nodes);

        return HostCheck.Result(result.Succeeded
            && result.Status == BootPageFreeListPlanStatus.Success
            && result.AllocatorPlanStatus == BootPageAllocatorPlanStatus.Success
            && result.FreeSegmentCount == 2
            && result.RequiredNodeCapacity == 2
            && result.AvailableNodeCapacity == 2
            && result.AvailableSegmentCapacity == 2
            && result.InvalidSegmentIndex == -1
            && result.HeadNodeIndex == 0
            && result.PageSize == PageSize
            && result.TotalFreePages == 10
            && result.TotalFreeBytes == 10 * PageSize
            && nodes[0].SourceFreeSegmentIndex == 0
            && nodes[0].SourceUsableRangeIndex == 0
            && nodes[0].PhysicalStart == 0x100000
            && nodes[0].PhysicalEndExclusive == 0x100000 + (4 * PageSize)
            && nodes[0].PageCount == 4
            && nodes[0].ByteLength == 4 * PageSize
            && nodes[0].NextNodeIndex == 1
            && nodes[1].SourceFreeSegmentIndex == 1
            && nodes[1].SourceUsableRangeIndex == 1
            && nodes[1].PhysicalStart == 0x300000
            && nodes[1].PhysicalEndExclusive == 0x300000 + (6 * PageSize)
            && nodes[1].PageCount == 6
            && nodes[1].ByteLength == 6 * PageSize
            && nodes[1].NextNodeIndex == -1);
    }

    [HostTest("memory.boot freelist nodes rejects failed planner", HostLane.Fast, 1)]
    private static bool TestRejectsFailedPlanner()
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

        BootPageFreeListNode sentinel = SentinelNode();
        Span<BootPageFreeListNode> nodes = stackalloc BootPageFreeListNode[1];
        nodes[0] = sentinel;

        BootPageFreeListPlanResult result = BootPageFreeListBuilder.Build(
            allocatorPlan,
            freeSegments,
            nodes);

        return HostCheck.Result(!result.Succeeded
            && result.Status == BootPageFreeListPlanStatus.AllocatorPlanFailed
            && result.AllocatorPlanStatus == BootPageAllocatorPlanStatus.InvalidPageSize
            && result.HeadNodeIndex == -1
            && nodes[0].PhysicalStart == sentinel.PhysicalStart
            && nodes[0].NextNodeIndex == sentinel.NextNodeIndex);
    }

    [HostTest("memory.boot freelist nodes reports destination capacity", HostLane.Fast, 2)]
    private static bool TestReportsDestinationCapacity()
    {
        Span<BootPageFreeSegment> freeSegments = stackalloc BootPageFreeSegment[2];
        BootPageAllocatorPlanResult allocatorPlan = CreateReadyAllocatorPlan(freeSegments);

        BootPageFreeListNode sentinel = SentinelNode();
        Span<BootPageFreeListNode> tooSmall = stackalloc BootPageFreeListNode[1];
        tooSmall[0] = sentinel;

        BootPageFreeListPlanResult destinationTooSmall = BootPageFreeListBuilder.Build(
            allocatorPlan,
            freeSegments,
            tooSmall);

        Span<BootPageFreeSegment> shortSource = stackalloc BootPageFreeSegment[1];
        shortSource[0] = freeSegments[0];
        Span<BootPageFreeListNode> enoughDestination = stackalloc BootPageFreeListNode[2];
        enoughDestination[0] = sentinel;
        BootPageFreeListPlanResult sourceTooSmall = BootPageFreeListBuilder.Build(
            allocatorPlan,
            shortSource,
            enoughDestination);

        return HostCheck.Result(!destinationTooSmall.Succeeded
            && destinationTooSmall.Status == BootPageFreeListPlanStatus.DestinationTooSmall
            && destinationTooSmall.RequiredNodeCapacity == 2
            && destinationTooSmall.AvailableNodeCapacity == 1
            && destinationTooSmall.AvailableSegmentCapacity == 2
            && tooSmall[0].PhysicalStart == sentinel.PhysicalStart
            && !sourceTooSmall.Succeeded
            && sourceTooSmall.Status == BootPageFreeListPlanStatus.SourceSegmentCapacityTooSmall
            && sourceTooSmall.RequiredNodeCapacity == 2
            && sourceTooSmall.AvailableNodeCapacity == 2
            && sourceTooSmall.AvailableSegmentCapacity == 1
            && enoughDestination[0].PhysicalStart == sentinel.PhysicalStart);
    }

    [HostTest("memory.boot freelist nodes rejects invalid segment shape", HostLane.Fast, 3)]
    private static bool TestRejectsInvalidSegmentShape()
    {
        Span<BootPageFreeSegment> freeSegments = stackalloc BootPageFreeSegment[2];
        BootPageAllocatorPlanResult allocatorPlan = CreateReadyAllocatorPlan(freeSegments);

        BootPageFreeListPlanResult zeroPages = BuildWithInvalidSegment(
            allocatorPlan,
            freeSegments,
            new BootPageFreeSegment(0, 0x100000, 0x100000, 0, 0),
            out bool zeroPreserved);

        BootPageFreeListPlanResult byteMismatch = BuildWithInvalidSegment(
            allocatorPlan,
            freeSegments,
            new BootPageFreeSegment(0, 0x100000, 0x104000, 4, PageSize),
            out bool byteMismatchPreserved);

        BootPageFreeListPlanResult endMismatch = BuildWithInvalidSegment(
            allocatorPlan,
            freeSegments,
            new BootPageFreeSegment(0, 0x100000, 0x105000, 4, 4 * PageSize),
            out bool endMismatchPreserved);

        BootPageFreeListPlanResult unalignedStart = BuildWithInvalidSegment(
            allocatorPlan,
            freeSegments,
            new BootPageFreeSegment(0, 0x100001, 0x104001, 4, 4 * PageSize),
            out bool unalignedStartPreserved);

        Span<BootPageFreeSegment> unsorted = stackalloc BootPageFreeSegment[2];
        unsorted[0] = freeSegments[1];
        unsorted[1] = freeSegments[0];
        BootPageFreeListNode sentinel = SentinelNode();
        Span<BootPageFreeListNode> nodes = stackalloc BootPageFreeListNode[2];
        nodes[0] = sentinel;
        BootPageFreeListPlanResult unsortedResult = BootPageFreeListBuilder.Build(
            allocatorPlan,
            unsorted,
            nodes);

        Span<BootPageFreeSegment> overlapping = stackalloc BootPageFreeSegment[2];
        overlapping[0] = freeSegments[0];
        overlapping[1] = new BootPageFreeSegment(
            1,
            freeSegments[0].PhysicalStart + PageSize,
            freeSegments[0].PhysicalStart + (3 * PageSize),
            2,
            2 * PageSize);
        BootPageFreeListPlanResult overlapResult = BootPageFreeListBuilder.Build(
            allocatorPlan,
            overlapping,
            nodes);

        return HostCheck.Result(!zeroPages.Succeeded
            && zeroPages.Status == BootPageFreeListPlanStatus.InvalidSegmentPageCount
            && zeroPages.InvalidSegmentIndex == 0
            && zeroPreserved
            && !byteMismatch.Succeeded
            && byteMismatch.Status == BootPageFreeListPlanStatus.SegmentByteLengthMismatch
            && byteMismatchPreserved
            && !endMismatch.Succeeded
            && endMismatch.Status == BootPageFreeListPlanStatus.SegmentPhysicalEndMismatch
            && endMismatchPreserved
            && !unalignedStart.Succeeded
            && unalignedStart.Status == BootPageFreeListPlanStatus.UnalignedSegmentPhysicalStart
            && unalignedStartPreserved
            && !unsortedResult.Succeeded
            && unsortedResult.Status == BootPageFreeListPlanStatus.UnsortedSegment
            && unsortedResult.InvalidSegmentIndex == 1
            && nodes[0].PhysicalStart == sentinel.PhysicalStart
            && !overlapResult.Succeeded
            && overlapResult.Status == BootPageFreeListPlanStatus.OverlappingSegment
            && overlapResult.InvalidSegmentIndex == 1);
    }

    [HostTest("memory.boot freelist nodes rejects arithmetic overflow", HostLane.Fast, 4)]
    private static bool TestRejectsArithmeticOverflow()
    {
        Span<BootPageFreeSegment> freeSegments = stackalloc BootPageFreeSegment[2];
        BootPageAllocatorPlanResult allocatorPlan = CreateReadyAllocatorPlan(freeSegments);

        BootPageFreeListPlanResult byteOverflow = BuildWithInvalidSegment(
            allocatorPlan,
            freeSegments,
            new BootPageFreeSegment(
                0,
                0x100000,
                0,
                (ulong.MaxValue / PageSize) + 1,
                0),
            out bool byteOverflowPreserved);

        BootPageFreeListPlanResult addressOverflow = BuildWithInvalidSegment(
            allocatorPlan,
            freeSegments,
            new BootPageFreeSegment(
                0,
                ulong.MaxValue - (PageSize - 1),
                0,
                1,
                PageSize),
            out bool addressOverflowPreserved);

        Span<BootPageFreeSegment> countMismatch = stackalloc BootPageFreeSegment[2];
        countMismatch[0] = freeSegments[0];
        countMismatch[1] = new BootPageFreeSegment(1, 0x300000, 0x301000, 1, PageSize);
        BootPageFreeListNode sentinel = SentinelNode();
        Span<BootPageFreeListNode> nodes = stackalloc BootPageFreeListNode[2];
        nodes[0] = sentinel;
        BootPageFreeListPlanResult totalMismatch = BootPageFreeListBuilder.Build(
            allocatorPlan,
            countMismatch,
            nodes);

        return HostCheck.Result(!byteOverflow.Succeeded
            && byteOverflow.Status == BootPageFreeListPlanStatus.SegmentByteCountOverflow
            && byteOverflowPreserved
            && !addressOverflow.Succeeded
            && addressOverflow.Status == BootPageFreeListPlanStatus.SegmentAddressRangeOverflow
            && addressOverflowPreserved
            && !totalMismatch.Succeeded
            && totalMismatch.Status == BootPageFreeListPlanStatus.TotalPageCountMismatch
            && totalMismatch.TotalFreePages == 5
            && totalMismatch.TotalFreeBytes == 5 * PageSize
            && nodes[0].PhysicalStart == sentinel.PhysicalStart);
    }

    private static BootPageFreeListPlanResult BuildWithInvalidSegment(
        BootPageAllocatorPlanResult allocatorPlan,
        ReadOnlySpan<BootPageFreeSegment> validSegments,
        BootPageFreeSegment invalidSegment,
        out bool destinationPreserved)
    {
        Span<BootPageFreeSegment> source = stackalloc BootPageFreeSegment[2];
        source[0] = invalidSegment;
        source[1] = validSegments[1];

        BootPageFreeListNode sentinel = SentinelNode();
        Span<BootPageFreeListNode> destination = stackalloc BootPageFreeListNode[2];
        destination[0] = sentinel;

        BootPageFreeListPlanResult result = BootPageFreeListBuilder.Build(
            allocatorPlan,
            source,
            destination);

        destinationPreserved = destination[0].PhysicalStart == sentinel.PhysicalStart
            && destination[0].NextNodeIndex == sentinel.NextNodeIndex;
        return result;
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

    private static BootPageFreeListNode SentinelNode()
    {
        return new BootPageFreeListNode(
            sourceFreeSegmentIndex: 99,
            sourceUsableRangeIndex: 98,
            physicalStart: 0xDEAD0000,
            physicalEndExclusive: 0xDEAD1000,
            pageCount: 1,
            byteLength: PageSize,
            nextNodeIndex: 77);
    }
}
