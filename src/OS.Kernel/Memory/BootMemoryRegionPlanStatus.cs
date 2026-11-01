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
