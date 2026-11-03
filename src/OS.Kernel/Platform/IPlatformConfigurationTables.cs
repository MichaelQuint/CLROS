using OS.Boot;

namespace OS.Kernel.Platform;

public interface IPlatformConfigurationTables
{
    PlatformConfigurationTableScanResult ScanAcpiRsdp();
}
