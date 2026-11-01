using OS.Boot;
using OS.Kernel.Memory;

namespace OS.Kernel.Diagnostics;
public enum SeparatePageFaultPageTableStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    InvalidRequest = 2,
    InvalidPreconditions = 3,
    InvalidRangeCount = 4,
    InvalidRange = 5,
    RangeOverlap = 6,
    TableArenaOverlap = 7,
    InsufficientTablePages = 8,
    InvalidStorage = 9,
    StorageOverlap = 10,
    StorageNotZero = 11,
    InvalidImageMetadata = 12,
    MalformedEntry = 13,
    ForeignTableTarget = 14,
    RepeatedTableTarget = 15,
    UnexpectedMapping = 16,
    MappingMismatch = 17,
    MissingMapping = 18,
    UnusedTableNotZero = 19,
    PageZeroNotAbsent = 20
}
#if M16_025_PROBE



#else
#endif
#if M10_004_PROBE




#else
#endif
