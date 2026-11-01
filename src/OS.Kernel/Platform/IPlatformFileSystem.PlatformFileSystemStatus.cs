namespace OS.Kernel.Platform;
public enum PlatformFileSystemStatus
{
    Unknown = 0,
    Success = 1,
    FirmwareServicesUnavailable = 2,
    InvalidPath = 3,
    DestinationTooSmall = 4,
    SourceUnavailable = 5,
    SourceFailed = 6,
    NotFound = 7
}
