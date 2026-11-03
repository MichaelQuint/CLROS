namespace OS.Kernel.Diagnostics;

public enum OwnedCom1ReadyWriterStatus : byte
{
    Success = 0,
    InvalidTransmitReadyPollLimit = 1,
    BackendNotReady = 2,
    ConfigurationFailed = 3,
    LineStatusReadFailed = 4,
    TransmitReadyTimeout = 5,
    MarkerWriteFailed = 6,
    InvalidPayloadIndex = 7
}
