namespace OS.Kernel.Diagnostics;
public enum PanicSafeOwnedCom1ReportStatus : byte
{
    Success = 0,
    InvalidTransmitReadyPollLimit = 1,
    BackendNotReady = 2,
    InvalidSubsystem = 3,
    InvalidReason = 4,
    InvalidVector = 5,
    DestinationTooSmall = 6,
    ConfigurationFailed = 7,
    LineStatusReadFailed = 8,
    TransmitReadyTimeout = 9,
    SerialWriteFailed = 10,
    InvalidDestination = 11,
    InvalidPayloadIndex = 12
}
#if !NOSTDLIB_EFI
#endif
