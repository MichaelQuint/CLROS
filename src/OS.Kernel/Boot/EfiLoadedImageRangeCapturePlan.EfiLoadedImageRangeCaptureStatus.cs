namespace OS.Boot;
/// <summary>
/// Reports validation of one supplied EFI loaded-image observation.
/// Existing values are append-only and the zero-valued default is not success.
/// </summary>

public enum EfiLoadedImageRangeCaptureStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    InvalidObservationCount = 2,
    TooManyObservations = 3,
    ObservationCapacityTooSmall = 4,
    MissingObservationBuffer = 5,
    MisalignedObservationBuffer = 6,
    ObservationAddressRangeOverflow = 7,
    InvalidPageSize = 8,
    InvalidImageHandle = 9,
    FirmwareFailure = 10,
    MissingProtocolPointer = 11,
    MisalignedProtocolPointer = 12,
    InvalidImageBase = 13,
    InvalidImageSize = 14,
    ImageEndOverflow = 15,
    ReservationStartZero = 16,
    ReservationRoundingOverflow = 17,
    ReservationRangeInvalid = 18,
    ReservationRejected = 19,
    ReservationEndMismatch = 20
}
