using OS.Boot;

namespace OS.Kernel.Platform;
public enum PlatformMemoryMapStatus
{
    Unknown = 0,
    Success = 1,
    FirmwareServicesUnavailable = 2,
    InvalidSnapshot = 3
}
