namespace OS.Boot;
/// <summary>
/// Reports structural validation of the single loader-data arena record.
/// </summary>

public enum M01LiveAllocatorArenaStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    InvalidMemoryType = 2,
    InvalidPhysicalStart = 3,
    MisalignedPhysicalStart = 4,
    InvalidVirtualStart = 5,
    MisalignedVirtualStart = 6,
    NonIdentityVirtualStart = 7,
    TooFewPages = 8,
    TooManyPages = 9,
    PageByteCountOverflow = 10,
    ByteLengthMismatch = 11,
    PhysicalEndOverflow = 12,
    PhysicalEndMismatch = 13,
    VirtualEndOverflow = 14
}
