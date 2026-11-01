namespace OS.Kernel.Memory;
public static class BootPageFreeListBuilder
{
    public static BootPageFreeListPlanResult Build(BootPageAllocatorPlanResult allocatorPlan, ReadOnlySpan<BootPageFreeSegment> freeSegments, Span<BootPageFreeListNode> destination)
    {
        BootPageFreeListPlanResult summary = Validate(allocatorPlan, freeSegments, destination.Length);
        if (!summary.Succeeded)
        {
            return summary;
        }

        WriteNodesUnchecked(allocatorPlan.FreeSegmentCount, freeSegments, destination);
        return summary;
    }

    private static BootPageFreeListPlanResult Validate(BootPageAllocatorPlanResult allocatorPlan, ReadOnlySpan<BootPageFreeSegment> freeSegments, int availableNodeCapacity)
    {
        int requiredNodeCapacity = allocatorPlan.FreeSegmentCount;
        if (!allocatorPlan.Succeeded)
        {
            return BootPageFreeListPlanResult.Fail(BootPageFreeListPlanStatus.AllocatorPlanFailed, allocatorPlan.Status, allocatorPlan.FreeSegmentCount, requiredNodeCapacity, availableNodeCapacity, freeSegments.Length, invalidSegmentIndex: -1, allocatorPlan.PageSize, allocatorPlan.TotalFreePages, allocatorPlan.TotalFreeBytes);
        }

        if (!IsValidPageSize(allocatorPlan.PageSize))
        {
            return BootPageFreeListPlanResult.Fail(BootPageFreeListPlanStatus.InvalidPageSize, allocatorPlan.Status, allocatorPlan.FreeSegmentCount, requiredNodeCapacity, availableNodeCapacity, freeSegments.Length, invalidSegmentIndex: -1, allocatorPlan.PageSize, allocatorPlan.TotalFreePages, allocatorPlan.TotalFreeBytes);
        }

        if (freeSegments.Length < allocatorPlan.FreeSegmentCount)
        {
            return BootPageFreeListPlanResult.Fail(BootPageFreeListPlanStatus.SourceSegmentCapacityTooSmall, allocatorPlan.Status, allocatorPlan.FreeSegmentCount, requiredNodeCapacity, availableNodeCapacity, freeSegments.Length, invalidSegmentIndex: -1, allocatorPlan.PageSize, allocatorPlan.TotalFreePages, allocatorPlan.TotalFreeBytes);
        }

        if (availableNodeCapacity < allocatorPlan.FreeSegmentCount)
        {
            return BootPageFreeListPlanResult.Fail(BootPageFreeListPlanStatus.DestinationTooSmall, allocatorPlan.Status, allocatorPlan.FreeSegmentCount, requiredNodeCapacity, availableNodeCapacity, freeSegments.Length, invalidSegmentIndex: -1, allocatorPlan.PageSize, allocatorPlan.TotalFreePages, allocatorPlan.TotalFreeBytes);
        }

        ulong totalPages = 0;
        ulong totalBytes = 0;
        ulong previousStart = 0;
        ulong previousEndExclusive = 0;
        for (int segmentIndex = 0; segmentIndex < allocatorPlan.FreeSegmentCount; segmentIndex++)
        {
            BootPageFreeSegment segment = freeSegments[segmentIndex];
            BootPageFreeListPlanStatus status = ValidateSegment(allocatorPlan.PageSize, segment, segmentIndex, previousStart, previousEndExclusive, out ulong byteLength, out ulong physicalEndExclusive);
            if (status != BootPageFreeListPlanStatus.Success)
            {
                return BootPageFreeListPlanResult.Fail(status, allocatorPlan.Status, allocatorPlan.FreeSegmentCount, requiredNodeCapacity, availableNodeCapacity, freeSegments.Length, segmentIndex, allocatorPlan.PageSize, totalPages, totalBytes);
            }

            if (!TryAdd(totalPages, segment.PageCount, out ulong nextTotalPages))
            {
                return BootPageFreeListPlanResult.Fail(BootPageFreeListPlanStatus.TotalPageCountMismatch, allocatorPlan.Status, allocatorPlan.FreeSegmentCount, requiredNodeCapacity, availableNodeCapacity, freeSegments.Length, segmentIndex, allocatorPlan.PageSize, totalPages, totalBytes);
            }

            if (!TryAdd(totalBytes, byteLength, out ulong nextTotalBytes))
            {
                return BootPageFreeListPlanResult.Fail(BootPageFreeListPlanStatus.TotalByteCountMismatch, allocatorPlan.Status, allocatorPlan.FreeSegmentCount, requiredNodeCapacity, availableNodeCapacity, freeSegments.Length, segmentIndex, allocatorPlan.PageSize, totalPages, totalBytes);
            }

            totalPages = nextTotalPages;
            totalBytes = nextTotalBytes;
            previousStart = segment.PhysicalStart;
            previousEndExclusive = physicalEndExclusive;
        }

        if (totalPages != allocatorPlan.TotalFreePages)
        {
            return BootPageFreeListPlanResult.Fail(BootPageFreeListPlanStatus.TotalPageCountMismatch, allocatorPlan.Status, allocatorPlan.FreeSegmentCount, requiredNodeCapacity, availableNodeCapacity, freeSegments.Length, invalidSegmentIndex: -1, allocatorPlan.PageSize, totalPages, totalBytes);
        }

        if (totalBytes != allocatorPlan.TotalFreeBytes)
        {
            return BootPageFreeListPlanResult.Fail(BootPageFreeListPlanStatus.TotalByteCountMismatch, allocatorPlan.Status, allocatorPlan.FreeSegmentCount, requiredNodeCapacity, availableNodeCapacity, freeSegments.Length, invalidSegmentIndex: -1, allocatorPlan.PageSize, totalPages, totalBytes);
        }

        return BootPageFreeListPlanResult.Success(allocatorPlan.Status, allocatorPlan.FreeSegmentCount, availableNodeCapacity, freeSegments.Length, allocatorPlan.PageSize, totalPages, totalBytes);
    }

    private static BootPageFreeListPlanStatus ValidateSegment(ulong pageSize, BootPageFreeSegment segment, int segmentIndex, ulong previousStart, ulong previousEndExclusive, out ulong byteLength, out ulong physicalEndExclusive)
    {
        byteLength = 0;
        physicalEndExclusive = 0;
        if (segment.PageCount == 0)
        {
            return BootPageFreeListPlanStatus.InvalidSegmentPageCount;
        }

        if (!IsAligned(segment.PhysicalStart, pageSize))
        {
            return BootPageFreeListPlanStatus.UnalignedSegmentPhysicalStart;
        }

        if (segment.PageCount > ulong.MaxValue / pageSize)
        {
            return BootPageFreeListPlanStatus.SegmentByteCountOverflow;
        }

        byteLength = segment.PageCount * pageSize;
        if (segment.ByteLength != byteLength)
        {
            return BootPageFreeListPlanStatus.SegmentByteLengthMismatch;
        }

        if (!TryAdd(segment.PhysicalStart, byteLength, out physicalEndExclusive))
        {
            return BootPageFreeListPlanStatus.SegmentAddressRangeOverflow;
        }

        if (segment.PhysicalEndExclusive != physicalEndExclusive)
        {
            return BootPageFreeListPlanStatus.SegmentPhysicalEndMismatch;
        }

        if (segmentIndex != 0)
        {
            if (segment.PhysicalStart < previousStart)
            {
                return BootPageFreeListPlanStatus.UnsortedSegment;
            }

            if (segment.PhysicalStart < previousEndExclusive)
            {
                return BootPageFreeListPlanStatus.OverlappingSegment;
            }
        }

        return BootPageFreeListPlanStatus.Success;
    }

    private static void WriteNodesUnchecked(int freeSegmentCount, ReadOnlySpan<BootPageFreeSegment> freeSegments, Span<BootPageFreeListNode> destination)
    {
        for (int segmentIndex = 0; segmentIndex < freeSegmentCount; segmentIndex++)
        {
            BootPageFreeSegment segment = freeSegments[segmentIndex];
            destination[segmentIndex] = new BootPageFreeListNode(segmentIndex, segment.SourceUsableRangeIndex, segment.PhysicalStart, segment.PhysicalEndExclusive, segment.PageCount, segment.ByteLength, segmentIndex == freeSegmentCount - 1 ? -1 : segmentIndex + 1);
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
