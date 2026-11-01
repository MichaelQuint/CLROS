namespace OS.Kernel.Memory;
/// <summary>
/// Reports construction or materialization of one caller-owned x64
/// page-granular TLB invalidation plan. Values are append-only.
/// </summary>

public enum X64TlbInvalidationStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    InvalidRange = 2,
    InvalidVirtualStart = 3,
    PageZeroRejected = 4,
    MisalignedVirtualStart = 5,
    InvalidByteLength = 6,
    ByteLengthNotPageAligned = 7,
    RangeOverflow = 8,
    NonCanonicalRange = 9,
    TooManyPages = 10,
    InvalidPlanState = 11,
    DestinationCapacityTooSmall = 12,
    CapacityExceedsLimit = 13,
    MissingDestination = 14,
    MisalignedDestination = 15,
    DestinationSpanOverflow = 16,
    InvalidRecord = 17
}
