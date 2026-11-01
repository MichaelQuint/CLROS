namespace OS.Kernel.Platform;
public enum PlatformPhysicalMemoryStatus
{
    Unknown = 0,
    Success = 1,
    FirmwareServicesUnavailable = 2,
    InvalidRange = 3,
    InvalidDestination = 4,
    DestinationTooSmall = 5,
    SourceUnavailable = 6,
    SourceFailed = 7,
    ShortRead = 8
}
