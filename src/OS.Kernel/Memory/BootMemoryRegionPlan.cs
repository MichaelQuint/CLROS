using OS.Boot;

namespace OS.Kernel.Memory;

public enum BootMemoryRegionPlanStatus
{
    Success = 0,
    InvalidRegionCount = 1,
    InvalidPageCount = 2,
    PageByteCountOverflow = 3,
    AddressRangeOverflow = 4,
    ByteLengthMismatch = 5,
    PhysicalEndMismatch = 6,
    UnsortedRange = 7,
    OverlappingRange = 8,
    TotalPageCountOverflow = 9,
    TotalByteCountOverflow = 10,
    UsableDestinationTooSmall = 11,
    ReservedDestinationTooSmall = 12
}

public readonly struct BootMemoryUsableRange
{
    public BootMemoryUsableRange(
        int sourceRegionIndex,
        ulong physicalStart,
        ulong physicalEndExclusive,
        ulong pageCount,
        ulong byteLength,
        ulong attribute)
    {
        SourceRegionIndex = sourceRegionIndex;
        PhysicalStart = physicalStart;
        PhysicalEndExclusive = physicalEndExclusive;
        PageCount = pageCount;
        ByteLength = byteLength;
        Attribute = attribute;
    }

    public int SourceRegionIndex { get; }

    public ulong PhysicalStart { get; }

    public ulong PhysicalEndExclusive { get; }

    public ulong PageCount { get; }

    public ulong ByteLength { get; }

    public ulong Attribute { get; }
}

public readonly struct BootMemoryReservedRange
{
    public BootMemoryReservedRange(
        int sourceRegionIndex,
        uint uefiType,
        ulong physicalStart,
        ulong physicalEndExclusive,
        ulong pageCount,
        ulong byteLength,
        ulong attribute)
    {
        SourceRegionIndex = sourceRegionIndex;
        UefiType = uefiType;
        PhysicalStart = physicalStart;
        PhysicalEndExclusive = physicalEndExclusive;
        PageCount = pageCount;
        ByteLength = byteLength;
        Attribute = attribute;
    }

    public int SourceRegionIndex { get; }

    public uint UefiType { get; }

    public ulong PhysicalStart { get; }

    public ulong PhysicalEndExclusive { get; }

    public ulong PageCount { get; }

    public ulong ByteLength { get; }

    public ulong Attribute { get; }
}

public readonly struct BootMemoryRegionPlanResult
{
    private BootMemoryRegionPlanResult(
        BootMemoryRegionPlanStatus status,
        int sourceRegionCount,
        int usableRangeCount,
        int reservedRangeCount,
        int requiredUsableRangeCapacity,
        int availableUsableRangeCapacity,
        int requiredReservedRangeCapacity,
        int availableReservedRangeCapacity,
        int invalidRegionIndex,
        ulong totalPages,
        ulong totalBytes,
        ulong usablePages,
        ulong usableBytes,
        ulong reservedPages,
        ulong reservedBytes)
    {
        Status = status;
        SourceRegionCount = sourceRegionCount;
        UsableRangeCount = usableRangeCount;
        ReservedRangeCount = reservedRangeCount;
        RequiredUsableRangeCapacity = requiredUsableRangeCapacity;
        AvailableUsableRangeCapacity = availableUsableRangeCapacity;
        RequiredReservedRangeCapacity = requiredReservedRangeCapacity;
        AvailableReservedRangeCapacity = availableReservedRangeCapacity;
        InvalidRegionIndex = invalidRegionIndex;
        TotalPages = totalPages;
        TotalBytes = totalBytes;
        UsablePages = usablePages;
        UsableBytes = usableBytes;
        ReservedPages = reservedPages;
        ReservedBytes = reservedBytes;
    }

    public BootMemoryRegionPlanStatus Status { get; }

    public bool Succeeded => Status == BootMemoryRegionPlanStatus.Success;

    public int SourceRegionCount { get; }

    public int UsableRangeCount { get; }

    public int ReservedRangeCount { get; }

    public int RequiredUsableRangeCapacity { get; }

    public int AvailableUsableRangeCapacity { get; }

    public int RequiredReservedRangeCapacity { get; }

    public int AvailableReservedRangeCapacity { get; }

    public int InvalidRegionIndex { get; }

    public ulong TotalPages { get; }

    public ulong TotalBytes { get; }

    public ulong UsablePages { get; }

    public ulong UsableBytes { get; }

    public ulong ReservedPages { get; }

    public ulong ReservedBytes { get; }

    internal static BootMemoryRegionPlanResult Success(
        int sourceRegionCount,
        int usableRangeCount,
        int reservedRangeCount,
        int availableUsableRangeCapacity,
        int availableReservedRangeCapacity,
        ulong totalPages,
        ulong totalBytes,
        ulong usablePages,
        ulong usableBytes,
        ulong reservedPages,
        ulong reservedBytes)
    {
        return new BootMemoryRegionPlanResult(
            BootMemoryRegionPlanStatus.Success,
            sourceRegionCount,
            usableRangeCount,
            reservedRangeCount,
            usableRangeCount,
            availableUsableRangeCapacity,
            reservedRangeCount,
            availableReservedRangeCapacity,
            invalidRegionIndex: -1,
            totalPages,
            totalBytes,
            usablePages,
            usableBytes,
            reservedPages,
            reservedBytes);
    }

    internal static BootMemoryRegionPlanResult Fail(
        BootMemoryRegionPlanStatus status,
        int sourceRegionCount,
        int usableRangeCount,
        int reservedRangeCount,
        int availableUsableRangeCapacity,
        int availableReservedRangeCapacity,
        int invalidRegionIndex,
        ulong totalPages,
        ulong totalBytes,
        ulong usablePages,
        ulong usableBytes,
        ulong reservedPages,
        ulong reservedBytes)
    {
        return new BootMemoryRegionPlanResult(
            status,
            sourceRegionCount,
            usableRangeCount: 0,
            reservedRangeCount: 0,
            requiredUsableRangeCapacity: usableRangeCount,
            availableUsableRangeCapacity,
            requiredReservedRangeCapacity: reservedRangeCount,
            availableReservedRangeCapacity,
            invalidRegionIndex,
            totalPages,
            totalBytes,
            usablePages,
            usableBytes,
            reservedPages,
            reservedBytes);
    }
}

public static class BootMemoryRegionPlanner
{
    private const uint UefiConventionalMemoryType = (uint)UefiMemoryType.ConventionalMemory;

    public static BootMemoryRegionPlanResult Plan(
        ReadOnlySpan<BootMemoryRegionRecord> sourceRegions,
        Span<BootMemoryUsableRange> usableDestination,
        Span<BootMemoryReservedRange> reservedDestination)
    {
        BootMemoryRegionPlanResult summary = Summarize(
            sourceRegions,
            usableDestination.Length,
            reservedDestination.Length);
        if (!summary.Succeeded)
        {
            return summary;
        }

        WriteRangesUnchecked(sourceRegions, usableDestination, reservedDestination);
        return summary;
    }

    private static BootMemoryRegionPlanResult Summarize(
        ReadOnlySpan<BootMemoryRegionRecord> sourceRegions,
        int availableUsableRangeCapacity,
        int availableReservedRangeCapacity)
    {
        if (sourceRegions.Length == 0)
        {
            return BootMemoryRegionPlanResult.Fail(
                BootMemoryRegionPlanStatus.InvalidRegionCount,
                sourceRegionCount: 0,
                usableRangeCount: 0,
                reservedRangeCount: 0,
                availableUsableRangeCapacity,
                availableReservedRangeCapacity,
                invalidRegionIndex: -1,
                totalPages: 0,
                totalBytes: 0,
                usablePages: 0,
                usableBytes: 0,
                reservedPages: 0,
                reservedBytes: 0);
        }

        int usableRangeCount = 0;
        int reservedRangeCount = 0;
        ulong totalPages = 0;
        ulong totalBytes = 0;
        ulong usablePages = 0;
        ulong usableBytes = 0;
        ulong reservedPages = 0;
        ulong reservedBytes = 0;
        ulong previousStart = 0;
        ulong previousEndExclusive = 0;

        for (int regionIndex = 0; regionIndex < sourceRegions.Length; regionIndex++)
        {
            BootMemoryRegionRecord region = sourceRegions[regionIndex];
            BootMemoryRegionPlanStatus status = ValidateRegion(
                region,
                regionIndex,
                previousStart,
                previousEndExclusive,
                out ulong byteLength,
                out ulong physicalEndExclusive);
            if (status != BootMemoryRegionPlanStatus.Success)
            {
                return BootMemoryRegionPlanResult.Fail(
                    status,
                    sourceRegions.Length,
                    usableRangeCount,
                    reservedRangeCount,
                    availableUsableRangeCapacity,
                    availableReservedRangeCapacity,
                    regionIndex,
                    totalPages,
                    totalBytes,
                    usablePages,
                    usableBytes,
                    reservedPages,
                    reservedBytes);
            }

            if (!TryAdd(totalPages, region.PageCount, out ulong nextTotalPages))
            {
                return BootMemoryRegionPlanResult.Fail(
                    BootMemoryRegionPlanStatus.TotalPageCountOverflow,
                    sourceRegions.Length,
                    usableRangeCount,
                    reservedRangeCount,
                    availableUsableRangeCapacity,
                    availableReservedRangeCapacity,
                    regionIndex,
                    totalPages,
                    totalBytes,
                    usablePages,
                    usableBytes,
                    reservedPages,
                    reservedBytes);
            }

            if (!TryAdd(totalBytes, byteLength, out ulong nextTotalBytes))
            {
                return BootMemoryRegionPlanResult.Fail(
                    BootMemoryRegionPlanStatus.TotalByteCountOverflow,
                    sourceRegions.Length,
                    usableRangeCount,
                    reservedRangeCount,
                    availableUsableRangeCapacity,
                    availableReservedRangeCapacity,
                    regionIndex,
                    totalPages,
                    totalBytes,
                    usablePages,
                    usableBytes,
                    reservedPages,
                    reservedBytes);
            }

            totalPages = nextTotalPages;
            totalBytes = nextTotalBytes;

            if (region.UefiType == UefiConventionalMemoryType)
            {
                if (!TryAdd(usablePages, region.PageCount, out ulong nextUsablePages))
                {
                    return BootMemoryRegionPlanResult.Fail(
                        BootMemoryRegionPlanStatus.TotalPageCountOverflow,
                        sourceRegions.Length,
                        usableRangeCount,
                        reservedRangeCount,
                        availableUsableRangeCapacity,
                        availableReservedRangeCapacity,
                        regionIndex,
                        totalPages,
                        totalBytes,
                        usablePages,
                        usableBytes,
                        reservedPages,
                        reservedBytes);
                }

                if (!TryAdd(usableBytes, byteLength, out ulong nextUsableBytes))
                {
                    return BootMemoryRegionPlanResult.Fail(
                        BootMemoryRegionPlanStatus.TotalByteCountOverflow,
                        sourceRegions.Length,
                        usableRangeCount,
                        reservedRangeCount,
                        availableUsableRangeCapacity,
                        availableReservedRangeCapacity,
                        regionIndex,
                        totalPages,
                        totalBytes,
                        usablePages,
                        usableBytes,
                        reservedPages,
                        reservedBytes);
                }

                usableRangeCount++;
                usablePages = nextUsablePages;
                usableBytes = nextUsableBytes;
            }
            else
            {
                if (!TryAdd(reservedPages, region.PageCount, out ulong nextReservedPages))
                {
                    return BootMemoryRegionPlanResult.Fail(
                        BootMemoryRegionPlanStatus.TotalPageCountOverflow,
                        sourceRegions.Length,
                        usableRangeCount,
                        reservedRangeCount,
                        availableUsableRangeCapacity,
                        availableReservedRangeCapacity,
                        regionIndex,
                        totalPages,
                        totalBytes,
                        usablePages,
                        usableBytes,
                        reservedPages,
                        reservedBytes);
                }

                if (!TryAdd(reservedBytes, byteLength, out ulong nextReservedBytes))
                {
                    return BootMemoryRegionPlanResult.Fail(
                        BootMemoryRegionPlanStatus.TotalByteCountOverflow,
                        sourceRegions.Length,
                        usableRangeCount,
                        reservedRangeCount,
                        availableUsableRangeCapacity,
                        availableReservedRangeCapacity,
                        regionIndex,
                        totalPages,
                        totalBytes,
                        usablePages,
                        usableBytes,
                        reservedPages,
                        reservedBytes);
                }

                reservedRangeCount++;
                reservedPages = nextReservedPages;
                reservedBytes = nextReservedBytes;
            }

            previousStart = region.PhysicalStart;
            previousEndExclusive = physicalEndExclusive;
        }

        if (availableUsableRangeCapacity < usableRangeCount)
        {
            return BootMemoryRegionPlanResult.Fail(
                BootMemoryRegionPlanStatus.UsableDestinationTooSmall,
                sourceRegions.Length,
                usableRangeCount,
                reservedRangeCount,
                availableUsableRangeCapacity,
                availableReservedRangeCapacity,
                invalidRegionIndex: -1,
                totalPages,
                totalBytes,
                usablePages,
                usableBytes,
                reservedPages,
                reservedBytes);
        }

        if (availableReservedRangeCapacity < reservedRangeCount)
        {
            return BootMemoryRegionPlanResult.Fail(
                BootMemoryRegionPlanStatus.ReservedDestinationTooSmall,
                sourceRegions.Length,
                usableRangeCount,
                reservedRangeCount,
                availableUsableRangeCapacity,
                availableReservedRangeCapacity,
                invalidRegionIndex: -1,
                totalPages,
                totalBytes,
                usablePages,
                usableBytes,
                reservedPages,
                reservedBytes);
        }

        return BootMemoryRegionPlanResult.Success(
            sourceRegions.Length,
            usableRangeCount,
            reservedRangeCount,
            availableUsableRangeCapacity,
            availableReservedRangeCapacity,
            totalPages,
            totalBytes,
            usablePages,
            usableBytes,
            reservedPages,
            reservedBytes);
    }

    private static BootMemoryRegionPlanStatus ValidateRegion(
        BootMemoryRegionRecord region,
        int regionIndex,
        ulong previousStart,
        ulong previousEndExclusive,
        out ulong byteLength,
        out ulong physicalEndExclusive)
    {
        byteLength = 0;
        physicalEndExclusive = 0;

        if (region.PageCount == 0)
        {
            return BootMemoryRegionPlanStatus.InvalidPageCount;
        }

        if (region.PageCount > ulong.MaxValue / BootMemoryRegionRecord.PageSize)
        {
            return BootMemoryRegionPlanStatus.PageByteCountOverflow;
        }

        byteLength = region.PageCount * BootMemoryRegionRecord.PageSize;
        if (region.ByteLength != byteLength)
        {
            return BootMemoryRegionPlanStatus.ByteLengthMismatch;
        }

        if (!TryAdd(region.PhysicalStart, byteLength, out physicalEndExclusive))
        {
            return BootMemoryRegionPlanStatus.AddressRangeOverflow;
        }

        if (region.PhysicalEndExclusive != physicalEndExclusive)
        {
            return BootMemoryRegionPlanStatus.PhysicalEndMismatch;
        }

        if (regionIndex != 0)
        {
            if (region.PhysicalStart < previousStart)
            {
                return BootMemoryRegionPlanStatus.UnsortedRange;
            }

            if (region.PhysicalStart < previousEndExclusive)
            {
                return BootMemoryRegionPlanStatus.OverlappingRange;
            }
        }

        return BootMemoryRegionPlanStatus.Success;
    }

    private static void WriteRangesUnchecked(
        ReadOnlySpan<BootMemoryRegionRecord> sourceRegions,
        Span<BootMemoryUsableRange> usableDestination,
        Span<BootMemoryReservedRange> reservedDestination)
    {
        int usableIndex = 0;
        int reservedIndex = 0;

        for (int regionIndex = 0; regionIndex < sourceRegions.Length; regionIndex++)
        {
            BootMemoryRegionRecord region = sourceRegions[regionIndex];
            if (region.UefiType == UefiConventionalMemoryType)
            {
                usableDestination[usableIndex] = new BootMemoryUsableRange(
                    regionIndex,
                    region.PhysicalStart,
                    region.PhysicalEndExclusive,
                    region.PageCount,
                    region.ByteLength,
                    region.Attribute);
                usableIndex++;
            }
            else
            {
                reservedDestination[reservedIndex] = new BootMemoryReservedRange(
                    regionIndex,
                    region.UefiType,
                    region.PhysicalStart,
                    region.PhysicalEndExclusive,
                    region.PageCount,
                    region.ByteLength,
                    region.Attribute);
                reservedIndex++;
            }
        }
    }

    private static bool TryAdd(ulong left, ulong right, out ulong value)
    {
        if (left > ulong.MaxValue - right)
        {
            value = 0;
            return false;
        }

        value = left + right;
        return true;
    }
}
