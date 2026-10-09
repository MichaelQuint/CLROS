using System.Runtime.CompilerServices;
using OS.Boot;
using OS.Kernel.Memory;

internal static class BootPageAllocatorPlanTests
{
    private const ulong PageSize = BootMemoryRegionRecord.PageSize;

    [HostTest("memory.boot freelist plans usable ranges", HostLane.Fast, 0)]
    private static bool TestPlansUsableRanges()
    {
        Span<BootMemoryUsableRange> usable = stackalloc BootMemoryUsableRange[2];
        usable[0] = Usable(sourceRegionIndex: 1, physicalStart: 0x100000, pageCount: 4);
        usable[1] = Usable(sourceRegionIndex: 3, physicalStart: 0x300000, pageCount: 6);

        Span<BootMemoryReservedRange> reserved = stackalloc BootMemoryReservedRange[2];
        reserved[0] = Reserved(sourceRegionIndex: 0, physicalStart: 0x0, pageCount: 1);
        reserved[1] = Reserved(sourceRegionIndex: 2, physicalStart: 0x200000, pageCount: 2);

        Span<BootPageFreeSegment> freeSegments = stackalloc BootPageFreeSegment[2];
        BootPageAllocatorPlanResult result = BootPageAllocatorPlanner.Plan(
            PageSize,
            usable,
            reserved,
            freeSegments);

        return HostCheck.Result(result.Succeeded
            && result.Status == BootPageAllocatorPlanStatus.Success
            && result.UsableRangeCount == 2
            && result.ReservedRangeCount == 2
            && result.FreeSegmentCount == 2
            && result.RequiredFreeSegmentCapacity == 2
            && result.AvailableFreeSegmentCapacity == 2
            && result.InvalidUsableRangeIndex == -1
            && result.InvalidReservedRangeIndex == -1
            && result.PageSize == PageSize
            && result.TotalFreePages == 10
            && result.TotalFreeBytes == 10 * PageSize
            && freeSegments[0].SourceUsableRangeIndex == 0
            && freeSegments[0].PhysicalStart == 0x100000
            && freeSegments[0].PhysicalEndExclusive == 0x100000 + (4 * PageSize)
            && freeSegments[0].PageCount == 4
            && freeSegments[0].ByteLength == 4 * PageSize
            && freeSegments[1].SourceUsableRangeIndex == 1
            && freeSegments[1].PhysicalStart == 0x300000
            && freeSegments[1].PhysicalEndExclusive == 0x300000 + (6 * PageSize)
            && freeSegments[1].PageCount == 6
            && freeSegments[1].ByteLength == 6 * PageSize);
    }

    [HostTest("memory.boot freelist rejects invalid page size", HostLane.Fast, 1)]
    private static bool TestRejectsInvalidPageSize()
    {
        Span<BootMemoryUsableRange> usable = stackalloc BootMemoryUsableRange[1];
        usable[0] = Usable(sourceRegionIndex: 0, physicalStart: 0x100000, pageCount: 1);
        Span<BootMemoryReservedRange> reserved = stackalloc BootMemoryReservedRange[0];
        Span<BootPageFreeSegment> destination = stackalloc BootPageFreeSegment[1];

        BootPageAllocatorPlanResult zero = BootPageAllocatorPlanner.Plan(
            pageSize: 0,
            usable,
            reserved,
            destination);
        BootPageAllocatorPlanResult notPowerOfTwo = BootPageAllocatorPlanner.Plan(
            pageSize: 6000,
            usable,
            reserved,
            destination);

        return HostCheck.Result(!zero.Succeeded
            && zero.Status == BootPageAllocatorPlanStatus.InvalidPageSize
            && zero.RequiredFreeSegmentCapacity == 0
            && zero.InvalidUsableRangeIndex == -1
            && zero.InvalidReservedRangeIndex == -1
            && !notPowerOfTwo.Succeeded
            && notPowerOfTwo.Status == BootPageAllocatorPlanStatus.InvalidPageSize);
    }

    [HostTest("memory.boot freelist rejects overlapping reservations", HostLane.Fast, 2)]
    private static bool TestRejectsOverlappingReservations()
    {
        Span<BootMemoryUsableRange> usable = stackalloc BootMemoryUsableRange[1];
        usable[0] = Usable(sourceRegionIndex: 0, physicalStart: 0x100000, pageCount: 8);

        Span<BootMemoryReservedRange> overlapsUsable = stackalloc BootMemoryReservedRange[1];
        overlapsUsable[0] = Reserved(sourceRegionIndex: 1, physicalStart: 0x102000, pageCount: 1);
        Span<BootPageFreeSegment> destination = stackalloc BootPageFreeSegment[1];
        destination[0] = new BootPageFreeSegment(99, 0xDEAD0000, 0xDEAD1000, 1, PageSize);

        BootPageAllocatorPlanResult usableOverlap = BootPageAllocatorPlanner.Plan(
            PageSize,
            usable,
            overlapsUsable,
            destination);

        Span<BootMemoryUsableRange> emptyUsable = stackalloc BootMemoryUsableRange[0];
        Span<BootMemoryReservedRange> overlapsReserved = stackalloc BootMemoryReservedRange[2];
        overlapsReserved[0] = Reserved(sourceRegionIndex: 1, physicalStart: 0x200000, pageCount: 4);
        overlapsReserved[1] = Reserved(sourceRegionIndex: 2, physicalStart: 0x202000, pageCount: 1);
        Span<BootPageFreeSegment> emptyDestination = stackalloc BootPageFreeSegment[0];
        BootPageAllocatorPlanResult reservedOverlap = BootPageAllocatorPlanner.Plan(
            PageSize,
            emptyUsable,
            overlapsReserved,
            emptyDestination);

        return HostCheck.Result(!usableOverlap.Succeeded
            && usableOverlap.Status == BootPageAllocatorPlanStatus.OverlappingReservedRange
            && usableOverlap.InvalidReservedRangeIndex == 0
            && usableOverlap.InvalidUsableRangeIndex == -1
            && usableOverlap.RequiredFreeSegmentCapacity == 1
            && usableOverlap.TotalFreePages == 8
            && usableOverlap.TotalFreeBytes == 8 * PageSize
            && destination[0].SourceUsableRangeIndex == 99
            && destination[0].PhysicalStart == 0xDEAD0000
            && !reservedOverlap.Succeeded
            && reservedOverlap.Status == BootPageAllocatorPlanStatus.OverlappingReservedRange
            && reservedOverlap.InvalidReservedRangeIndex == 1
            && reservedOverlap.RequiredFreeSegmentCapacity == 0);
    }

    [HostTest("memory.boot freelist reports destination capacity", HostLane.Fast, 3)]
    private static bool TestReportsDestinationCapacity()
    {
        Span<BootMemoryUsableRange> usable = stackalloc BootMemoryUsableRange[2];
        usable[0] = Usable(sourceRegionIndex: 0, physicalStart: 0x100000, pageCount: 1);
        usable[1] = Usable(sourceRegionIndex: 1, physicalStart: 0x200000, pageCount: 1);

        Span<BootMemoryReservedRange> reserved = stackalloc BootMemoryReservedRange[0];
        Span<BootPageFreeSegment> tooSmall = stackalloc BootPageFreeSegment[1];
        tooSmall[0] = new BootPageFreeSegment(77, 0x770000, 0x771000, 1, PageSize);

        BootPageAllocatorPlanResult result = BootPageAllocatorPlanner.Plan(
            PageSize,
            usable,
            reserved,
            tooSmall);

        return HostCheck.Result(!result.Succeeded
            && result.Status == BootPageAllocatorPlanStatus.FreeSegmentDestinationTooSmall
            && result.UsableRangeCount == 2
            && result.ReservedRangeCount == 0
            && result.FreeSegmentCount == 0
            && result.RequiredFreeSegmentCapacity == 2
            && result.AvailableFreeSegmentCapacity == 1
            && result.TotalFreePages == 2
            && result.TotalFreeBytes == 2 * PageSize
            && tooSmall[0].SourceUsableRangeIndex == 77
            && tooSmall[0].PhysicalStart == 0x770000);
    }

    [HostTest("memory.boot freelist rejects arithmetic overflow", HostLane.Fast, 4)]
    private static bool TestRejectsArithmeticOverflow()
    {
        Span<BootMemoryUsableRange> pageByteOverflow = stackalloc BootMemoryUsableRange[1];
        pageByteOverflow[0] = new BootMemoryUsableRange(
            sourceRegionIndex: 0,
            physicalStart: 0x100000,
            physicalEndExclusive: 0,
            pageCount: (ulong.MaxValue / PageSize) + 1,
            byteLength: 0,
            attribute: 0);

        Span<BootMemoryUsableRange> addressOverflow = stackalloc BootMemoryUsableRange[1];
        addressOverflow[0] = new BootMemoryUsableRange(
            sourceRegionIndex: 0,
            physicalStart: ulong.MaxValue - (PageSize - 1),
            physicalEndExclusive: 0,
            pageCount: 1,
            byteLength: PageSize,
            attribute: 0);

        Span<BootMemoryReservedRange> reserved = stackalloc BootMemoryReservedRange[0];
        Span<BootPageFreeSegment> destination = stackalloc BootPageFreeSegment[1];

        BootPageAllocatorPlanResult pageOverflowResult = BootPageAllocatorPlanner.Plan(
            PageSize,
            pageByteOverflow,
            reserved,
            destination);
        BootPageAllocatorPlanResult addressOverflowResult = BootPageAllocatorPlanner.Plan(
            PageSize,
            addressOverflow,
            reserved,
            destination);

        return HostCheck.Result(!pageOverflowResult.Succeeded
            && pageOverflowResult.Status == BootPageAllocatorPlanStatus.PageByteCountOverflow
            && pageOverflowResult.InvalidUsableRangeIndex == 0
            && !addressOverflowResult.Succeeded
            && addressOverflowResult.Status == BootPageAllocatorPlanStatus.AddressRangeOverflow);
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
            uefiType: (uint)UefiMemoryType.LoaderData,
            physicalStart,
            physicalStart + byteLength,
            pageCount,
            byteLength,
            attribute: 0);
    }
}
