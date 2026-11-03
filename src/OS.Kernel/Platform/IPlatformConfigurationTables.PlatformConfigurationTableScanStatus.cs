using OS.Boot;

namespace OS.Kernel.Platform;

public enum PlatformConfigurationTableScanStatus
{
    Unknown = 0,
    Success = 1,
    FirmwareServicesUnavailable = 2,
    AcpiRsdpScanFailed = 3
}
