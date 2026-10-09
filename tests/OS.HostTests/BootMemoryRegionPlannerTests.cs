using System.Runtime.CompilerServices;
using OS.Boot;
using OS.Kernel.Memory;

internal static class BootMemoryRegionPlannerTests
{

    [HostTest("memory.boot regions selects conventional ranges", HostLane.Fast, 0)]
    private static bool TestSelectsConventionalRanges()
    {
        Span<BootMemoryRegionRecord> source = stackalloc BootMemoryRegionRecord[4];
        source[0] = Region(UefiMemoryType.Reserved, 0, 1, attribute: 0x1);
        source[1] = Region(UefiMemoryType.ConventionalMemory, 0x100000, 4, attribute: 0x10);
        source[2] = Region(UefiMemoryType.BootServicesData, 0x200000, 2, attribute: 0x20);
        source[3] = Region(UefiMemoryType.ConventionalMemory, 0x300000, 6, attribute: 0x30);

        Span<BootMemoryUsableRange> usable = stackalloc BootMemoryUsableRange[2];
        Span<BootMemoryReservedRange> reserved = stackalloc BootMemoryReservedRange[2];
        BootMemoryRegionPlanResult result = BootMemoryRegionPlanner.Plan(source, usable, reserved);

        return HostCheck.Result(result.Succeeded
            && result.Status == BootMemoryRegionPlanStatus.Success
            && result.SourceRegionCount == 4
            && result.UsableRangeCount == 2
            && result.ReservedRangeCount == 2
            && result.RequiredUsableRangeCapacity == 2
            && result.AvailableUsableRangeCapacity == 2
            && result.RequiredReservedRangeCapacity == 2
            && result.AvailableReservedRangeCapacity == 2
            && result.InvalidRegionIndex == -1
            && result.TotalPages == 13
            && result.TotalBytes == 13 * BootMemoryRegionRecord.PageSize
            && result.UsablePages == 10
            && result.UsableBytes == 10 * BootMemoryRegionRecord.PageSize
            && result.ReservedPages == 3
            && result.ReservedBytes == 3 * BootMemoryRegionRecord.PageSize
            && usable[0].SourceRegionIndex == 1
            && usable[0].PhysicalStart == 0x100000
            && usable[0].PhysicalEndExclusive == 0x100000 + (4 * BootMemoryRegionRecord.PageSize)
            && usable[0].PageCount == 4
            && usable[0].ByteLength == 4 * BootMemoryRegionRecord.PageSize
            && usable[0].Attribute == 0x10
            && usable[1].SourceRegionIndex == 3
            && usable[1].PhysicalStart == 0x300000
            && usable[1].PageCount == 6
            && usable[1].Attribute == 0x30);
    }

    [HostTest("memory.boot regions records reserved ranges", HostLane.Fast, 1)]
    private static bool TestRecordsReservedRanges()
    {
        const uint unknownType = 0x80000000;
        Span<BootMemoryRegionRecord> source = stackalloc BootMemoryRegionRecord[4];
        source[0] = Region(UefiMemoryType.LoaderCode, 0x100000, 1, attribute: 0x11);
        source[1] = Region(UefiMemoryType.RuntimeServicesCode, 0x200000, 2, attribute: 0x22);
        source[2] = Region(UefiMemoryType.AcpiMemoryNvs, 0x300000, 3, attribute: 0x33);
        source[3] = Region(unknownType, 0x400000, 4, attribute: 0x44);

        Span<BootMemoryUsableRange> usable = stackalloc BootMemoryUsableRange[1];
        Span<BootMemoryReservedRange> reserved = stackalloc BootMemoryReservedRange[4];
        BootMemoryRegionPlanResult result = BootMemoryRegionPlanner.Plan(source, usable, reserved);

        return HostCheck.Result(result.Succeeded
            && result.UsableRangeCount == 0
            && result.ReservedRangeCount == 4
            && result.UsablePages == 0
            && result.UsableBytes == 0
            && result.ReservedPages == 10
            && result.ReservedBytes == 10 * BootMemoryRegionRecord.PageSize
            && reserved[0].SourceRegionIndex == 0
            && reserved[0].UefiType == (uint)UefiMemoryType.LoaderCode
            && reserved[0].PhysicalStart == 0x100000
            && reserved[0].PageCount == 1
            && reserved[0].Attribute == 0x11
            && reserved[1].UefiType == (uint)UefiMemoryType.RuntimeServicesCode
            && reserved[1].PhysicalEndExclusive == 0x200000 + (2 * BootMemoryRegionRecord.PageSize)
            && reserved[2].UefiType == (uint)UefiMemoryType.AcpiMemoryNvs
            && reserved[2].ByteLength == 3 * BootMemoryRegionRecord.PageSize
            && reserved[3].SourceRegionIndex == 3
            && reserved[3].UefiType == unknownType
            && reserved[3].Attribute == 0x44);
    }

    [HostTest("memory.boot regions rejects overlapping ranges", HostLane.Fast, 2)]
    private static bool TestRejectsOverlappingRanges()
    {
        Span<BootMemoryRegionRecord> source = stackalloc BootMemoryRegionRecord[2];
        source[0] = Region(UefiMemoryType.ConventionalMemory, 0x100000, 4);
        source[1] = Region(UefiMemoryType.BootServicesData, 0x103000, 2);

        BootMemoryUsableRange usableSentinel = new BootMemoryUsableRange(
            sourceRegionIndex: 99,
            physicalStart: 0xDEAD0000,
            physicalEndExclusive: 0xDEAD1000,
            pageCount: 1,
            byteLength: BootMemoryRegionRecord.PageSize,
            attribute: 0xAA);
        BootMemoryReservedRange reservedSentinel = new BootMemoryReservedRange(
            sourceRegionIndex: 98,
            uefiType: (uint)UefiMemoryType.PersistentMemory,
            physicalStart: 0xBEEF0000,
            physicalEndExclusive: 0xBEEF1000,
            pageCount: 1,
            byteLength: BootMemoryRegionRecord.PageSize,
            attribute: 0xBB);
        Span<BootMemoryUsableRange> usable = stackalloc BootMemoryUsableRange[1];
        Span<BootMemoryReservedRange> reserved = stackalloc BootMemoryReservedRange[1];
        usable[0] = usableSentinel;
        reserved[0] = reservedSentinel;

        BootMemoryRegionPlanResult result = BootMemoryRegionPlanner.Plan(source, usable, reserved);

        return HostCheck.Result(!result.Succeeded
            && result.Status == BootMemoryRegionPlanStatus.OverlappingRange
            && result.InvalidRegionIndex == 1
            && result.UsableRangeCount == 0
            && result.ReservedRangeCount == 0
            && result.RequiredUsableRangeCapacity == 1
            && result.RequiredReservedRangeCapacity == 0
            && result.TotalPages == 4
            && result.TotalBytes == 4 * BootMemoryRegionRecord.PageSize
            && result.UsablePages == 4
            && result.UsableBytes == 4 * BootMemoryRegionRecord.PageSize
            && usable[0].SourceRegionIndex == usableSentinel.SourceRegionIndex
            && usable[0].PhysicalStart == usableSentinel.PhysicalStart
            && reserved[0].SourceRegionIndex == reservedSentinel.SourceRegionIndex
            && reserved[0].PhysicalStart == reservedSentinel.PhysicalStart);
    }

    [HostTest("memory.boot regions reports destination capacity", HostLane.Fast, 3)]
    private static bool TestReportsDestinationCapacity()
    {
        Span<BootMemoryRegionRecord> source = stackalloc BootMemoryRegionRecord[3];
        source[0] = Region(UefiMemoryType.ConventionalMemory, 0x100000, 1);
        source[1] = Region(UefiMemoryType.BootServicesData, 0x200000, 1);
        source[2] = Region(UefiMemoryType.ConventionalMemory, 0x300000, 1);

        BootMemoryUsableRange usableSentinel = new BootMemoryUsableRange(
            7,
            0x700000,
            0x701000,
            1,
            BootMemoryRegionRecord.PageSize,
            0x77);
        BootMemoryReservedRange reservedSentinel = new BootMemoryReservedRange(
            8,
            (uint)UefiMemoryType.LoaderData,
            0x800000,
            0x801000,
            1,
            BootMemoryRegionRecord.PageSize,
            0x88);

        Span<BootMemoryUsableRange> tooSmallUsable = stackalloc BootMemoryUsableRange[1];
        Span<BootMemoryReservedRange> enoughReserved = stackalloc BootMemoryReservedRange[1];
        tooSmallUsable[0] = usableSentinel;
        enoughReserved[0] = reservedSentinel;

        BootMemoryRegionPlanResult usableCapacity = BootMemoryRegionPlanner.Plan(
            source,
            tooSmallUsable,
            enoughReserved);

        Span<BootMemoryUsableRange> enoughUsable = stackalloc BootMemoryUsableRange[2];
        Span<BootMemoryReservedRange> tooSmallReserved = stackalloc BootMemoryReservedRange[0];
        BootMemoryRegionPlanResult reservedCapacity = BootMemoryRegionPlanner.Plan(
            source,
            enoughUsable,
            tooSmallReserved);

        return HostCheck.Result(!usableCapacity.Succeeded
            && usableCapacity.Status == BootMemoryRegionPlanStatus.UsableDestinationTooSmall
            && usableCapacity.RequiredUsableRangeCapacity == 2
            && usableCapacity.AvailableUsableRangeCapacity == 1
            && usableCapacity.RequiredReservedRangeCapacity == 1
            && usableCapacity.AvailableReservedRangeCapacity == 1
            && usableCapacity.TotalPages == 3
            && usableCapacity.UsablePages == 2
            && usableCapacity.ReservedPages == 1
            && tooSmallUsable[0].SourceRegionIndex == usableSentinel.SourceRegionIndex
            && tooSmallUsable[0].PhysicalStart == usableSentinel.PhysicalStart
            && enoughReserved[0].SourceRegionIndex == reservedSentinel.SourceRegionIndex
            && enoughReserved[0].PhysicalStart == reservedSentinel.PhysicalStart
            && !reservedCapacity.Succeeded
            && reservedCapacity.Status == BootMemoryRegionPlanStatus.ReservedDestinationTooSmall
            && reservedCapacity.RequiredUsableRangeCapacity == 2
            && reservedCapacity.AvailableUsableRangeCapacity == 2
            && reservedCapacity.RequiredReservedRangeCapacity == 1
            && reservedCapacity.AvailableReservedRangeCapacity == 0);
    }

    [HostTest("memory.boot regions rejects invalid metadata", HostLane.Fast, 4)]
    private static bool TestRejectsInvalidMetadata()
    {
        BootMemoryRegionPlanResult zeroPages = PlanSingle(new BootMemoryRegionRecord(
            (uint)UefiMemoryType.ConventionalMemory,
            physicalStart: 0x100000,
            virtualStart: 0,
            pageCount: 0,
            attribute: 0,
            byteLength: 0,
            physicalEndExclusive: 0x100000));
        BootMemoryRegionPlanResult byteLengthMismatch = PlanSingle(new BootMemoryRegionRecord(
            (uint)UefiMemoryType.ConventionalMemory,
            physicalStart: 0x100000,
            virtualStart: 0,
            pageCount: 2,
            attribute: 0,
            byteLength: BootMemoryRegionRecord.PageSize,
            physicalEndExclusive: 0x102000));
        BootMemoryRegionPlanResult physicalEndMismatch = PlanSingle(new BootMemoryRegionRecord(
            (uint)UefiMemoryType.ConventionalMemory,
            physicalStart: 0x100000,
            virtualStart: 0,
            pageCount: 2,
            attribute: 0,
            byteLength: 2 * BootMemoryRegionRecord.PageSize,
            physicalEndExclusive: 0x103000));

        Span<BootMemoryRegionRecord> unsorted = stackalloc BootMemoryRegionRecord[2];
        unsorted[0] = Region(UefiMemoryType.ConventionalMemory, 0x300000, 1);
        unsorted[1] = Region(UefiMemoryType.ConventionalMemory, 0x100000, 1);
        Span<BootMemoryUsableRange> usable = stackalloc BootMemoryUsableRange[2];
        Span<BootMemoryReservedRange> reserved = stackalloc BootMemoryReservedRange[1];
        BootMemoryRegionPlanResult unsortedResult = BootMemoryRegionPlanner.Plan(
            unsorted,
            usable,
            reserved);

        return HostCheck.Result(!zeroPages.Succeeded
            && zeroPages.Status == BootMemoryRegionPlanStatus.InvalidPageCount
            && zeroPages.InvalidRegionIndex == 0
            && !byteLengthMismatch.Succeeded
            && byteLengthMismatch.Status == BootMemoryRegionPlanStatus.ByteLengthMismatch
            && byteLengthMismatch.InvalidRegionIndex == 0
            && !physicalEndMismatch.Succeeded
            && physicalEndMismatch.Status == BootMemoryRegionPlanStatus.PhysicalEndMismatch
            && physicalEndMismatch.InvalidRegionIndex == 0
            && !unsortedResult.Succeeded
            && unsortedResult.Status == BootMemoryRegionPlanStatus.UnsortedRange
            && unsortedResult.InvalidRegionIndex == 1);
    }

    [HostTest("memory.boot regions covers overflow failures", HostLane.Fast, 5)]
    private static bool TestCoversOverflowFailures()
    {
        BootMemoryRegionPlanResult pageByteOverflow = PlanSingle(new BootMemoryRegionRecord(
            (uint)UefiMemoryType.ConventionalMemory,
            physicalStart: 0x100000,
            virtualStart: 0,
            pageCount: (ulong.MaxValue / BootMemoryRegionRecord.PageSize) + 1,
            attribute: 0,
            byteLength: 0,
            physicalEndExclusive: 0));
        BootMemoryRegionPlanResult addressOverflow = PlanSingle(new BootMemoryRegionRecord(
            (uint)UefiMemoryType.ConventionalMemory,
            physicalStart: ulong.MaxValue - 10,
            virtualStart: 0,
            pageCount: 1,
            attribute: 0,
            byteLength: BootMemoryRegionRecord.PageSize,
            physicalEndExclusive: 0));

        return HostCheck.Result(!pageByteOverflow.Succeeded
            && pageByteOverflow.Status == BootMemoryRegionPlanStatus.PageByteCountOverflow
            && pageByteOverflow.InvalidRegionIndex == 0
            && !addressOverflow.Succeeded
            && addressOverflow.Status == BootMemoryRegionPlanStatus.AddressRangeOverflow
            && addressOverflow.InvalidRegionIndex == 0);
    }

    private static BootMemoryRegionPlanResult PlanSingle(BootMemoryRegionRecord region)
    {
        Span<BootMemoryRegionRecord> source = stackalloc BootMemoryRegionRecord[1];
        source[0] = region;
        Span<BootMemoryUsableRange> usable = stackalloc BootMemoryUsableRange[1];
        Span<BootMemoryReservedRange> reserved = stackalloc BootMemoryReservedRange[1];
        return BootMemoryRegionPlanner.Plan(source, usable, reserved);
    }

    private static BootMemoryRegionRecord Region(
        UefiMemoryType type,
        ulong physicalStart,
        ulong pageCount,
        ulong attribute = 0)
    {
        return Region((uint)type, physicalStart, pageCount, attribute);
    }

    private static BootMemoryRegionRecord Region(
        uint uefiType,
        ulong physicalStart,
        ulong pageCount,
        ulong attribute = 0)
    {
        ulong byteLength = pageCount * BootMemoryRegionRecord.PageSize;
        return new BootMemoryRegionRecord(
            uefiType,
            physicalStart,
            virtualStart: 0,
            pageCount,
            attribute,
            byteLength,
            physicalStart + byteLength);
    }
}
