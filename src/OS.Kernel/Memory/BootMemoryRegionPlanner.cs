using OS.Boot;

namespace OS.Kernel.Memory;

public static class BootMemoryRegionPlanner
{
    private const uint UefiConventionalMemoryType = (uint)UefiMemoryType.ConventionalMemory;
    public static BootMemoryRegionPlanResult Plan(ReadOnlySpan<BootMemoryRegionRecord> sourceRegions, Span<BootMemoryUsableRange> usableDestination, Span<BootMemoryReservedRange> reservedDestination)
    {
        BootMemoryRegionPlanResult summary = Summarize(sourceRegions, usableDestination.Length, reservedDestination.Length);
        if (!summary.Succeeded)
        {
            return summary;
        }

        WriteRangesUnchecked(sourceRegions, usableDestination, reservedDestination);
        return summary;
    }

    private static BootMemoryRegionPlanResult Summarize(ReadOnlySpan<BootMemoryRegionRecord> sourceRegions, int availableUsableRangeCapacity, int availableReservedRangeCapacity)
    {
        if (sourceRegions.Length == 0)
        {
            return BootMemoryRegionPlanResult.Fail(BootMemoryRegionPlanStatus.InvalidRegionCount, sourceRegionCount: 0, usableRangeCount: 0, reservedRangeCount: 0, availableUsableRangeCapacity, availableReservedRangeCapacity, invalidRegionIndex: -1, totalPages: 0, totalBytes: 0, usablePages: 0, usableBytes: 0, reservedPages: 0, reservedBytes: 0);
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
            BootMemoryRegionPlanStatus status = ValidateRegion(region, regionIndex, previousStart, previousEndExclusive, out ulong byteLength, out ulong physicalEndExclusive);
            if (status != BootMemoryRegionPlanStatus.Success)
            {
                return BootMemoryRegionPlanResult.Fail(status, sourceRegions.Length, usableRangeCount, reservedRangeCount, availableUsableRangeCapacity, availableReservedRangeCapacity, regionIndex, totalPages, totalBytes, usablePages, usableBytes, reservedPages, reservedBytes);
            }

            if (!TryAdd(totalPages, region.PageCount, out ulong nextTotalPages))
            {
                return BootMemoryRegionPlanResult.Fail(BootMemoryRegionPlanStatus.TotalPageCountOverflow, sourceRegions.Length, usableRangeCount, reservedRangeCount, availableUsableRangeCapacity, availableReservedRangeCapacity, regionIndex, totalPages, totalBytes, usablePages, usableBytes, reservedPages, reservedBytes);
            }

            if (!TryAdd(totalBytes, byteLength, out ulong nextTotalBytes))
            {
                return BootMemoryRegionPlanResult.Fail(BootMemoryRegionPlanStatus.TotalByteCountOverflow, sourceRegions.Length, usableRangeCount, reservedRangeCount, availableUsableRangeCapacity, availableReservedRangeCapacity, regionIndex, totalPages, totalBytes, usablePages, usableBytes, reservedPages, reservedBytes);
            }

            totalPages = nextTotalPages;
            totalBytes = nextTotalBytes;
            if (region.UefiType == UefiConventionalMemoryType)
            {
                if (!TryAdd(usablePages, region.PageCount, out ulong nextUsablePages))
                {
                    return BootMemoryRegionPlanResult.Fail(BootMemoryRegionPlanStatus.TotalPageCountOverflow, sourceRegions.Length, usableRangeCount, reservedRangeCount, availableUsableRangeCapacity, availableReservedRangeCapacity, regionIndex, totalPages, totalBytes, usablePages, usableBytes, reservedPages, reservedBytes);
                }

                if (!TryAdd(usableBytes, byteLength, out ulong nextUsableBytes))
                {
                    return BootMemoryRegionPlanResult.Fail(BootMemoryRegionPlanStatus.TotalByteCountOverflow, sourceRegions.Length, usableRangeCount, reservedRangeCount, availableUsableRangeCapacity, availableReservedRangeCapacity, regionIndex, totalPages, totalBytes, usablePages, usableBytes, reservedPages, reservedBytes);
                }

                usableRangeCount++;
                usablePages = nextUsablePages;
                usableBytes = nextUsableBytes;
            }
            else
            {
                if (!TryAdd(reservedPages, region.PageCount, out ulong nextReservedPages))
                {
                    return BootMemoryRegionPlanResult.Fail(BootMemoryRegionPlanStatus.TotalPageCountOverflow, sourceRegions.Length, usableRangeCount, reservedRangeCount, availableUsableRangeCapacity, availableReservedRangeCapacity, regionIndex, totalPages, totalBytes, usablePages, usableBytes, reservedPages, reservedBytes);
                }

                if (!TryAdd(reservedBytes, byteLength, out ulong nextReservedBytes))
                {
                    return BootMemoryRegionPlanResult.Fail(BootMemoryRegionPlanStatus.TotalByteCountOverflow, sourceRegions.Length, usableRangeCount, reservedRangeCount, availableUsableRangeCapacity, availableReservedRangeCapacity, regionIndex, totalPages, totalBytes, usablePages, usableBytes, reservedPages, reservedBytes);
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
            return BootMemoryRegionPlanResult.Fail(BootMemoryRegionPlanStatus.UsableDestinationTooSmall, sourceRegions.Length, usableRangeCount, reservedRangeCount, availableUsableRangeCapacity, availableReservedRangeCapacity, invalidRegionIndex: -1, totalPages, totalBytes, usablePages, usableBytes, reservedPages, reservedBytes);
        }

        if (availableReservedRangeCapacity < reservedRangeCount)
        {
            return BootMemoryRegionPlanResult.Fail(BootMemoryRegionPlanStatus.ReservedDestinationTooSmall, sourceRegions.Length, usableRangeCount, reservedRangeCount, availableUsableRangeCapacity, availableReservedRangeCapacity, invalidRegionIndex: -1, totalPages, totalBytes, usablePages, usableBytes, reservedPages, reservedBytes);
        }

        return BootMemoryRegionPlanResult.Success(sourceRegions.Length, usableRangeCount, reservedRangeCount, availableUsableRangeCapacity, availableReservedRangeCapacity, totalPages, totalBytes, usablePages, usableBytes, reservedPages, reservedBytes);
    }

    private static BootMemoryRegionPlanStatus ValidateRegion(BootMemoryRegionRecord region, int regionIndex, ulong previousStart, ulong previousEndExclusive, out ulong byteLength, out ulong physicalEndExclusive)
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

    private static void WriteRangesUnchecked(ReadOnlySpan<BootMemoryRegionRecord> sourceRegions, Span<BootMemoryUsableRange> usableDestination, Span<BootMemoryReservedRange> reservedDestination)
    {
        int usableIndex = 0;
        int reservedIndex = 0;
        for (int regionIndex = 0; regionIndex < sourceRegions.Length; regionIndex++)
        {
            BootMemoryRegionRecord region = sourceRegions[regionIndex];
            if (region.UefiType == UefiConventionalMemoryType)
            {
                usableDestination[usableIndex] = new BootMemoryUsableRange(regionIndex, region.PhysicalStart, region.PhysicalEndExclusive, region.PageCount, region.ByteLength, region.Attribute);
                usableIndex++;
            }
            else
            {
                reservedDestination[reservedIndex] = new BootMemoryReservedRange(regionIndex, region.UefiType, region.PhysicalStart, region.PhysicalEndExclusive, region.PageCount, region.ByteLength, region.Attribute);
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
