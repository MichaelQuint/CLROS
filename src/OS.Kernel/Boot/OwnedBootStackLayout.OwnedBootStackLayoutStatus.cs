namespace OS.Boot;
/// <summary>
/// Reports construction or lifetime transition of an owned boot-stack layout.
/// Existing values are append-only and the zero-valued default is not success.
/// </summary>

public enum OwnedBootStackLayoutStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    InvalidReservationCount = 2,
    TooManyReservations = 3,
    ReservationCapacityTooSmall = 4,
    MissingReservationTable = 5,
    MisalignedReservationTable = 6,
    ReservationTableAddressRangeOverflow = 7,
    InvalidBackingStart = 8,
    MisalignedBackingStart = 9,
    InvalidBackingPageCount = 10,
    BackingByteCountOverflow = 11,
    BackingEndOverflow = 12,
    ReservationTableRejected = 13,
    InvalidStackTop = 14,
    MisalignedStackTop = 15,
    MissingGuardReservation = 16,
    MissingStackReservation = 17,
    BackingArenaMismatch = 18,
    StackTopMismatch = 19,
    InvalidLayout = 20,
    InvalidLifetime = 21,
    RepeatedLifetimeTransition = 22,
    IllegalLifetimeTransition = 23
}
