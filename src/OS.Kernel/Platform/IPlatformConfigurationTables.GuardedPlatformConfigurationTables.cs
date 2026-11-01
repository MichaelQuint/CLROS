using OS.Boot;

namespace OS.Kernel.Platform;
public sealed class GuardedPlatformConfigurationTables : IPlatformConfigurationTables
{
    private readonly IPlatformFirmware _firmware;
    private readonly IPlatformConfigurationTableSource _source;
    public GuardedPlatformConfigurationTables(IPlatformFirmware firmware, IPlatformConfigurationTableSource source)
    {
        _firmware = firmware;
        _source = source;
    }

    public PlatformConfigurationTableScanResult ScanAcpiRsdp()
    {
        FirmwareCallResult guardResult = _firmware.RequireBootServices(PlatformConfigurationTableOperations.ScanAcpiRsdp);
        if (!guardResult.Succeeded)
        {
            return PlatformConfigurationTableScanResult.ScanAcpiRsdp(guardResult, default);
        }

        return PlatformConfigurationTableScanResult.ScanAcpiRsdp(guardResult, _source.GetMetadata());
    }
}
