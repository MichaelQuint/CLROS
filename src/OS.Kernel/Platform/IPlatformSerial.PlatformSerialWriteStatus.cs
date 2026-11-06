namespace OS.Kernel.Platform;

public enum PlatformSerialWriteStatus
{
    Unknown = 0,
    Success = 1,
    InvalidPayload = 2,
    NoOwnedTransport = 3,
    UefiGoneRequiresOwnedSerial = 4,
    OwnedSerialUnavailable = 5,
    BackendFailure = 6,
    ShortWrite = 7
}
