namespace OS.Kernel.Memory;

public enum BootPageAllocatorPlanStatus
{
    Success = 0,
    InvalidPageSize = 1,
    InvalidPageCount = 2,
    PageByteCountOverflow = 3,
    AddressRangeOverflow = 4,
    ByteLengthMismatch = 5,
    PhysicalEndMismatch = 6,
    UnalignedPhysicalStart = 7,
    UnsortedUsableRange = 8,
    OverlappingUsableRange = 9,
    UnsortedReservedRange = 10,
    OverlappingReservedRange = 11,
    FreeSegmentDestinationTooSmall = 12,
    TotalFreePageCountOverflow = 13,
    TotalFreeByteCountOverflow = 14
}

public readonly struct BootPageFreeSegment
{
    public BootPageFreeSegment(
        int sourceUsableRangeIndex,
        ulong physicalStart,
        ulong physicalEndExclusive,
        ulong pageCount,
        ulong byteLength)
    {
        SourceUsableRangeIndex = sourceUsableRangeIndex;
        PhysicalStart = physicalStart;
        PhysicalEndExclusive = physicalEndExclusive;
        PageCount = pageCount;
        ByteLength = byteLength;
    }

    public int SourceUsableRangeIndex { get; }

    public ulong PhysicalStart { get; }

    public ulong PhysicalEndExclusive { get; }

    public ulong PageCount { get; }

    public ulong ByteLength { get; }
}

public readonly struct BootPageAllocatorPlanResult
{
    private BootPageAllocatorPlanResult(
        BootPageAllocatorPlanStatus status,
        int usableRangeCount,
        int reservedRangeCount,
        int freeSegmentCount,
        int requiredFreeSegmentCapacity,
        int availableFreeSegmentCapacity,
        int invalidUsableRangeIndex,
        int invalidReservedRangeIndex,
        ulong pageSize,
        ulong totalFreePages,
        ulong totalFreeBytes)
    {
        Status = status;
        UsableRangeCount = usableRangeCount;
        ReservedRangeCount = reservedRangeCount;
        FreeSegmentCount = freeSegmentCount;
        RequiredFreeSegmentCapacity = requiredFreeSegmentCapacity;
        AvailableFreeSegmentCapacity = availableFreeSegmentCapacity;
        InvalidUsableRangeIndex = invalidUsableRangeIndex;
        InvalidReservedRangeIndex = invalidReservedRangeIndex;
        PageSize = pageSize;
        TotalFreePages = totalFreePages;
        TotalFreeBytes = totalFreeBytes;
    }

    public BootPageAllocatorPlanStatus Status { get; }

    public bool Succeeded => Status == BootPageAllocatorPlanStatus.Success;

    public int UsableRangeCount { get; }

    public int ReservedRangeCount { get; }

    public int FreeSegmentCount { get; }

    public int RequiredFreeSegmentCapacity { get; }

    public int AvailableFreeSegmentCapacity { get; }

    public int InvalidUsableRangeIndex { get; }

    public int InvalidReservedRangeIndex { get; }

    public ulong PageSize { get; }

    public ulong TotalFreePages { get; }

    public ulong TotalFreeBytes { get; }

    internal static BootPageAllocatorPlanResult Success(
        int usableRangeCount,
        int reservedRangeCount,
        int availableFreeSegmentCapacity,
        ulong pageSize,
        ulong totalFreePages,
        ulong totalFreeBytes)
    {
        return new BootPageAllocatorPlanResult(
            BootPageAllocatorPlanStatus.Success,
            usableRangeCount,
            reservedRangeCount,
            freeSegmentCount: usableRangeCount,
            requiredFreeSegmentCapacity: usableRangeCount,
            availableFreeSegmentCapacity,
            invalidUsableRangeIndex: -1,
            invalidReservedRangeIndex: -1,
            pageSize,
            totalFreePages,
            totalFreeBytes);
    }

    internal static BootPageAllocatorPlanResult Fail(
        BootPageAllocatorPlanStatus status,
        int usableRangeCount,
        int reservedRangeCount,
        int requiredFreeSegmentCapacity,
        int availableFreeSegmentCapacity,
        int invalidUsableRangeIndex,
        int invalidReservedRangeIndex,
        ulong pageSize,
        ulong totalFreePages,
        ulong totalFreeBytes)
    {
        return new BootPageAllocatorPlanResult(
            status,
            usableRangeCount,
            reservedRangeCount,
            freeSegmentCount: 0,
            requiredFreeSegmentCapacity,
            availableFreeSegmentCapacity,
            invalidUsableRangeIndex,
            invalidReservedRangeIndex,
            pageSize,
            totalFreePages,
            totalFreeBytes);
    }
}

public static class BootPageAllocatorPlanner
{
    public static BootPageAllocatorPlanResult Plan(
        ulong pageSize,
        ReadOnlySpan<BootMemoryUsableRange> usableRanges,
        ReadOnlySpan<BootMemoryReservedRange> reservedRanges,
        Span<BootPageFreeSegment> freeSegmentDestination)
    {
        BootPageAllocatorPlanResult summary = Summarize(
            pageSize,
            usableRanges,
            reservedRanges,
            freeSegmentDestination.Length);
        if (!summary.Succeeded)
        {
            return summary;
        }

        WriteFreeSegmentsUnchecked(usableRanges, freeSegmentDestination);
        return summary;
    }

    private static BootPageAllocatorPlanResult Summarize(
        ulong pageSize,
        ReadOnlySpan<BootMemoryUsableRange> usableRanges,
        ReadOnlySpan<BootMemoryReservedRange> reservedRanges,
        int availableFreeSegmentCapacity)
    {
        if (!IsValidPageSize(pageSize))
        {
            return BootPageAllocatorPlanResult.Fail(
                BootPageAllocatorPlanStatus.InvalidPageSize,
                usableRanges.Length,
                reservedRanges.Length,
                requiredFreeSegmentCapacity: 0,
                availableFreeSegmentCapacity,
                invalidUsableRangeIndex: -1,
                invalidReservedRangeIndex: -1,
                pageSize,
                totalFreePages: 0,
                totalFreeBytes: 0);
        }

        ulong totalFreePages = 0;
        ulong totalFreeBytes = 0;
        ulong previousUsableStart = 0;
        ulong previousUsableEndExclusive = 0;

        for (int usableIndex = 0; usableIndex < usableRanges.Length; usableIndex++)
        {
            BootMemoryUsableRange usable = usableRanges[usableIndex];
            BootPageAllocatorPlanStatus status = ValidateUsableRange(
                pageSize,
                usable,
                usableIndex,
                previousUsableStart,
                previousUsableEndExclusive,
                out ulong byteLength,
                out ulong physicalEndExclusive);
            if (status != BootPageAllocatorPlanStatus.Success)
            {
                return BootPageAllocatorPlanResult.Fail(
                    status,
                    usableRanges.Length,
                    reservedRanges.Length,
                    usableRanges.Length,
                    availableFreeSegmentCapacity,
                    usableIndex,
                    invalidReservedRangeIndex: -1,
                    pageSize,
                    totalFreePages,
                    totalFreeBytes);
            }

            if (!TryAdd(totalFreePages, usable.PageCount, out ulong nextTotalFreePages))
            {
                return BootPageAllocatorPlanResult.Fail(
                    BootPageAllocatorPlanStatus.TotalFreePageCountOverflow,
                    usableRanges.Length,
                    reservedRanges.Length,
                    usableRanges.Length,
                    availableFreeSegmentCapacity,
                    usableIndex,
                    invalidReservedRangeIndex: -1,
                    pageSize,
                    totalFreePages,
                    totalFreeBytes);
            }

            if (!TryAdd(totalFreeBytes, byteLength, out ulong nextTotalFreeBytes))
            {
                return BootPageAllocatorPlanResult.Fail(
                    BootPageAllocatorPlanStatus.TotalFreeByteCountOverflow,
                    usableRanges.Length,
                    reservedRanges.Length,
                    usableRanges.Length,
                    availableFreeSegmentCapacity,
                    usableIndex,
                    invalidReservedRangeIndex: -1,
                    pageSize,
                    totalFreePages,
                    totalFreeBytes);
            }

            totalFreePages = nextTotalFreePages;
            totalFreeBytes = nextTotalFreeBytes;
            previousUsableStart = usable.PhysicalStart;
            previousUsableEndExclusive = physicalEndExclusive;
        }

        BootPageAllocatorPlanStatus reservedStatus = ValidateReservedRanges(
            pageSize,
            usableRanges,
            reservedRanges,
            out int invalidReservedRangeIndex);
        if (reservedStatus != BootPageAllocatorPlanStatus.Success)
        {
            return BootPageAllocatorPlanResult.Fail(
                reservedStatus,
                usableRanges.Length,
                reservedRanges.Length,
                usableRanges.Length,
                availableFreeSegmentCapacity,
                invalidUsableRangeIndex: -1,
                invalidReservedRangeIndex,
                pageSize,
                totalFreePages,
                totalFreeBytes);
        }

        if (availableFreeSegmentCapacity < usableRanges.Length)
        {
            return BootPageAllocatorPlanResult.Fail(
                BootPageAllocatorPlanStatus.FreeSegmentDestinationTooSmall,
                usableRanges.Length,
                reservedRanges.Length,
                usableRanges.Length,
                availableFreeSegmentCapacity,
                invalidUsableRangeIndex: -1,
                invalidReservedRangeIndex: -1,
                pageSize,
                totalFreePages,
                totalFreeBytes);
        }

        return BootPageAllocatorPlanResult.Success(
            usableRanges.Length,
            reservedRanges.Length,
            availableFreeSegmentCapacity,
            pageSize,
            totalFreePages,
            totalFreeBytes);
    }

    private static BootPageAllocatorPlanStatus ValidateUsableRange(
        ulong pageSize,
        BootMemoryUsableRange range,
        int rangeIndex,
        ulong previousStart,
        ulong previousEndExclusive,
        out ulong byteLength,
        out ulong physicalEndExclusive)
    {
        BootPageAllocatorPlanStatus status = ValidateRangeShape(
            pageSize,
            range.PhysicalStart,
            range.PhysicalEndExclusive,
            range.PageCount,
            range.ByteLength,
            out byteLength,
            out physicalEndExclusive);
        if (status != BootPageAllocatorPlanStatus.Success)
        {
            return status;
        }

        if (rangeIndex != 0)
        {
            if (range.PhysicalStart < previousStart)
            {
                return BootPageAllocatorPlanStatus.UnsortedUsableRange;
            }

            if (range.PhysicalStart < previousEndExclusive)
            {
                return BootPageAllocatorPlanStatus.OverlappingUsableRange;
            }
        }

        return BootPageAllocatorPlanStatus.Success;
    }

    private static BootPageAllocatorPlanStatus ValidateReservedRanges(
        ulong pageSize,
        ReadOnlySpan<BootMemoryUsableRange> usableRanges,
        ReadOnlySpan<BootMemoryReservedRange> reservedRanges,
        out int invalidReservedRangeIndex)
    {
        invalidReservedRangeIndex = -1;
        ulong previousReservedStart = 0;
        ulong previousReservedEndExclusive = 0;

        for (int reservedIndex = 0; reservedIndex < reservedRanges.Length; reservedIndex++)
        {
            BootMemoryReservedRange reserved = reservedRanges[reservedIndex];
            BootPageAllocatorPlanStatus status = ValidateRangeShape(
                pageSize,
                reserved.PhysicalStart,
                reserved.PhysicalEndExclusive,
                reserved.PageCount,
                reserved.ByteLength,
                out _,
                out ulong reservedEndExclusive);
            if (status != BootPageAllocatorPlanStatus.Success)
            {
                invalidReservedRangeIndex = reservedIndex;
                return status;
            }

            if (reservedIndex != 0)
            {
                if (reserved.PhysicalStart < previousReservedStart)
                {
                    invalidReservedRangeIndex = reservedIndex;
                    return BootPageAllocatorPlanStatus.UnsortedReservedRange;
                }

                if (reserved.PhysicalStart < previousReservedEndExclusive)
                {
                    invalidReservedRangeIndex = reservedIndex;
                    return BootPageAllocatorPlanStatus.OverlappingReservedRange;
                }
            }

            if (OverlapsAnyUsableRange(reserved.PhysicalStart, reservedEndExclusive, usableRanges))
            {
                invalidReservedRangeIndex = reservedIndex;
                return BootPageAllocatorPlanStatus.OverlappingReservedRange;
            }

            previousReservedStart = reserved.PhysicalStart;
            previousReservedEndExclusive = reservedEndExclusive;
        }

        return BootPageAllocatorPlanStatus.Success;
    }

    private static BootPageAllocatorPlanStatus ValidateRangeShape(
        ulong pageSize,
        ulong physicalStart,
        ulong physicalEndExclusive,
        ulong pageCount,
        ulong declaredByteLength,
        out ulong byteLength,
        out ulong calculatedPhysicalEndExclusive)
    {
        byteLength = 0;
        calculatedPhysicalEndExclusive = 0;

        if (pageCount == 0)
        {
            return BootPageAllocatorPlanStatus.InvalidPageCount;
        }

        if (!IsAligned(physicalStart, pageSize))
        {
            return BootPageAllocatorPlanStatus.UnalignedPhysicalStart;
        }

        if (pageCount > ulong.MaxValue / pageSize)
        {
            return BootPageAllocatorPlanStatus.PageByteCountOverflow;
        }

        byteLength = pageCount * pageSize;
        if (declaredByteLength != byteLength)
        {
            return BootPageAllocatorPlanStatus.ByteLengthMismatch;
        }

        if (!TryAdd(physicalStart, byteLength, out calculatedPhysicalEndExclusive))
        {
            return BootPageAllocatorPlanStatus.AddressRangeOverflow;
        }

        if (physicalEndExclusive != calculatedPhysicalEndExclusive)
        {
            return BootPageAllocatorPlanStatus.PhysicalEndMismatch;
        }

        return BootPageAllocatorPlanStatus.Success;
    }

    private static bool OverlapsAnyUsableRange(
        ulong reservedStart,
        ulong reservedEndExclusive,
        ReadOnlySpan<BootMemoryUsableRange> usableRanges)
    {
        for (int usableIndex = 0; usableIndex < usableRanges.Length; usableIndex++)
        {
            BootMemoryUsableRange usable = usableRanges[usableIndex];
            if (reservedStart < usable.PhysicalEndExclusive && usable.PhysicalStart < reservedEndExclusive)
            {
                return true;
            }
        }

        return false;
    }

    private static void WriteFreeSegmentsUnchecked(
        ReadOnlySpan<BootMemoryUsableRange> usableRanges,
        Span<BootPageFreeSegment> freeSegmentDestination)
    {
        for (int usableIndex = 0; usableIndex < usableRanges.Length; usableIndex++)
        {
            BootMemoryUsableRange usable = usableRanges[usableIndex];
            freeSegmentDestination[usableIndex] = new BootPageFreeSegment(
                usableIndex,
                usable.PhysicalStart,
                usable.PhysicalEndExclusive,
                usable.PageCount,
                usable.ByteLength);
        }
    }

    private static bool IsValidPageSize(ulong pageSize)
    {
        return pageSize != 0 && (pageSize & (pageSize - 1)) == 0;
    }

    private static bool IsAligned(ulong value, ulong alignment)
    {
        return (value & (alignment - 1)) == 0;
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
