namespace OS.Kernel.Platform;

public enum PlatformRandomStatus
{
    Unknown = 0,
    Success = 1,
    FirmwareServicesUnavailable = 2,
    InvalidDestination = 3,
    SourceUnavailable = 4,
    SourceFailed = 5,
    ShortRead = 6
}
