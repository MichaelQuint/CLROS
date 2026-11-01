namespace OS.Kernel.Memory;
public enum BootPageFreeListPlanStatus
{
    Success = 0,
    AllocatorPlanFailed = 1,
    InvalidPageSize = 2,
    SourceSegmentCapacityTooSmall = 3,
    DestinationTooSmall = 4,
    InvalidSegmentPageCount = 5,
    SegmentByteCountOverflow = 6,
    SegmentAddressRangeOverflow = 7,
    SegmentByteLengthMismatch = 8,
    SegmentPhysicalEndMismatch = 9,
    UnalignedSegmentPhysicalStart = 10,
    UnsortedSegment = 11,
    OverlappingSegment = 12,
    TotalPageCountMismatch = 13,
    TotalByteCountMismatch = 14
}
