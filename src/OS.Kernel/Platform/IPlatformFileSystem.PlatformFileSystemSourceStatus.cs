namespace OS.Kernel.Platform;
public enum PlatformFileSystemSourceStatus
{
    Unknown = 0,
    Success = 1,
    DestinationTooSmall = 2,
    Unavailable = 3,
    FirmwareError = 4,
    NotFound = 5
}
