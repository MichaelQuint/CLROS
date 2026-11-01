namespace OS.Kernel.Memory;
/// <summary>
/// Reports construction or structural validation of one boot reservation.
/// The zero-valued default is intentionally not success.
/// </summary>

public enum BootReservedRangeStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    UninitializedKind = 2,
    UnknownKind = 3,
    ReservedBitsSet = 4,
    InvalidPhysicalStart = 5,
    MisalignedPhysicalStart = 6,
    InvalidPageCount = 7,
    PageByteCountOverflow = 8,
    ByteLengthMismatch = 9,
    InvalidPhysicalEndExclusive = 10,
    MisalignedPhysicalEndExclusive = 11,
    AddressRangeOverflow = 12,
    PhysicalEndMismatch = 13
}
