namespace OS.Kernel.Memory;
/// <summary>
/// Reports validation of a caller-owned canonical boot-reservation table.
/// The zero-valued default is intentionally not success.
/// </summary>

public enum BootReservationTableStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    InvalidReservationCount = 2,
    TooManyReservations = 3,
    ReservationCapacityTooSmall = 4,
    MissingReservationTable = 5,
    MisalignedReservationTable = 6,
    InvalidReservation = 7,
    AddressComparisonFailed = 8,
    UnsortedRange = 9,
    DuplicateRange = 10,
    DuplicateLoadedImageCoverage = 11,
    NestedRange = 12,
    OverlappingRange = 13,
    MultipleBootStackGuards = 14,
    MultipleBootStacks = 15,
    MissingBootStackGuard = 16,
    MissingBootStack = 17,
    BootStackOrderInvalid = 18,
    BootStackArenaGap = 19,
    BootStackArenaAccountingOverflow = 20,
    BootStackArenaAccountingMismatch = 21,
    TotalPageCountOverflow = 22,
    TotalByteCountOverflow = 23,
    ReservationTableAddressRangeOverflow = 24
}
