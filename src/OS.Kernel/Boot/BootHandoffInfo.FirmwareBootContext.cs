using OS.Kernel.Platform;

namespace OS.Boot;

public readonly struct FirmwareBootContext
{
    public FirmwareBootContext(BootMode bootMode, BootCapabilities capabilities, FirmwareServicesMode firmwareServicesMode, FirmwareMemoryMapMetadata memoryMap, FirmwareAcpiRsdpMetadata acpiRsdp)
    {
        BootMode = bootMode;
        Capabilities = capabilities;
        FirmwareServicesMode = firmwareServicesMode;
        MemoryMap = memoryMap;
        AcpiRsdp = acpiRsdp;
    }

    public BootMode BootMode
    {
        get;
    }
    public BootCapabilities Capabilities
    {
        get;
    }
    public FirmwareServicesMode FirmwareServicesMode
    {
        get;
    }
    public FirmwareMemoryMapMetadata MemoryMap
    {
        get;
    }
    public FirmwareAcpiRsdpMetadata AcpiRsdp
    {
        get;
    }
    public bool BootServicesAvailable => FirmwareServicesMode == FirmwareServicesMode.BootServicesAvailable;

    public static FirmwareBootContext CreateUefi(BootCapabilities capabilities, FirmwareServicesMode firmwareServicesMode, FirmwareMemoryMapMetadata memoryMap)
    {
        return CreateUefi(capabilities, firmwareServicesMode, memoryMap, default);
    }

    public static FirmwareBootContext CreateUefi(BootCapabilities capabilities, FirmwareServicesMode firmwareServicesMode, FirmwareMemoryMapMetadata memoryMap, FirmwareAcpiRsdpMetadata acpiRsdp)
    {
        return new FirmwareBootContext(BootMode.Uefi, capabilities, firmwareServicesMode, memoryMap, acpiRsdp);
    }
}
