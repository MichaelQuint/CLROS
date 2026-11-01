namespace OS.Kernel.Memory;
public enum BootReservationSourceRangeStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    InvalidSourceRangeIndex = 2,
    ReservedBitsSet = 3,
    InvalidPhysicalStart = 4,
    MisalignedPhysicalStart = 5,
    InvalidPageCount = 6,
    PageByteCountOverflow = 7,
    ByteLengthMismatch = 8,
    InvalidPhysicalEndExclusive = 9,
    MisalignedPhysicalEndExclusive = 10,
    AddressRangeOverflow = 11,
    PhysicalEndMismatch = 12
}
