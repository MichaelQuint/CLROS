using OS.Drivers;
using OS.Kernel.Memory;

namespace OS.Kernel.Platform;
public enum PhysicalMmioOwnershipPlanStatus
{
    Success = 0,
    InvalidSegmentCount = 1,
    InvalidBusRange = 2,
    InvalidAlignment = 3,
    AddressRangeOverflow = 4,
    OverlappingMmioRange = 5,
    UsableMemoryOverlap = 6,
    InvalidUsableRange = 7,
    DestinationTooSmall = 8,
    TotalByteCountOverflow = 9
}
