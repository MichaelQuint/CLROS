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
