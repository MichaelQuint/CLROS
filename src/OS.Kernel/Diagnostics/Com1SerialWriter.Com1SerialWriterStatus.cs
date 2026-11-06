namespace OS.Kernel.Diagnostics;

public enum Com1SerialWriterStatus
{
    Success = 0,
    InitializationFailed = 1,
    NotInitialized = 2,
    TransmitTimeout = 3,
    InvalidAscii = 4,
    BackendFailure = 5
}
