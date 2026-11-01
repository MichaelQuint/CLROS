using OS.Kernel.Platform;

namespace OS.Boot;
public readonly struct BootHandoffInfo
{
    public BootHandoffInfo(FirmwareBootContext firmwareContext)
    {
        FirmwareContext = firmwareContext;
    }

    public FirmwareBootContext FirmwareContext { get; }
    public BootMode BootMode => FirmwareContext.BootMode;
    public BootCapabilities Capabilities => FirmwareContext.Capabilities;
    public FirmwareServicesMode FirmwareServicesMode => FirmwareContext.FirmwareServicesMode;
    public FirmwareMemoryMapMetadata MemoryMap => FirmwareContext.MemoryMap;
    public FirmwareAcpiRsdpMetadata AcpiRsdp => FirmwareContext.AcpiRsdp;

    public static BootHandoffInfo FromRecord(BootHandoffRecord record)
    {
        FirmwareMemoryMapRecord memoryMap = record.MemoryMap;
        FirmwareMemoryMapMetadata metadata = new FirmwareMemoryMapMetadata(memoryMap.BufferAddress, memoryMap.BufferSize, memoryMap.MapSize, memoryMap.MapKey, memoryMap.DescriptorSize, memoryMap.DescriptorVersion, memoryMap.DescriptorCount);
        FirmwareAcpiRsdpRecord acpiRsdp = record.AcpiRsdp;
        FirmwareAcpiRsdpMetadata acpiMetadata = new FirmwareAcpiRsdpMetadata(acpiRsdp.RsdpAddress, MapAcpiTableKind(acpiRsdp.TableKind), MapConfigurationTableScanStatus(acpiRsdp.ScanStatus), acpiRsdp.HasRsdp());
        FirmwareBootContext context = new FirmwareBootContext(MapBootMode(record.BootMode), (BootCapabilities)record.Capabilities, MapFirmwareServicesMode(record.FirmwareServicesMode), metadata, acpiMetadata);
        return new BootHandoffInfo(context);
    }

    public BootHandoffValidationResult Validate()
    {
        FirmwareMemoryMapMetadata memoryMap = MemoryMap;
        if (!memoryMap.HasMap)
        {
            return BootHandoffValidationResult.Invalid(BootHandoffStatus.MissingMemoryMap, "Firmware handoff memory map metadata is required.");
        }

        if (memoryMap.MapSize > memoryMap.BufferSize)
        {
            return BootHandoffValidationResult.Invalid(BootHandoffStatus.InvalidMapSize, "Firmware handoff memory map size exceeds buffer size.");
        }

        if (memoryMap.DescriptorSize == 0)
        {
            return BootHandoffValidationResult.Invalid(BootHandoffStatus.InvalidDescriptorSize, "Firmware handoff memory map descriptor size is required.");
        }

        if (memoryMap.DescriptorVersion == 0)
        {
            return BootHandoffValidationResult.Invalid(BootHandoffStatus.InvalidDescriptorVersion, "Firmware handoff memory map descriptor version is required.");
        }

        if (memoryMap.DescriptorCount == 0 || memoryMap.MapSize % memoryMap.DescriptorSize != 0 || memoryMap.MapSize / memoryMap.DescriptorSize != memoryMap.DescriptorCount)
        {
            return BootHandoffValidationResult.Invalid(BootHandoffStatus.InvalidDescriptorCount, "Firmware handoff memory map descriptor count does not match map size.");
        }

        bool acpiCapabilitySet = (Capabilities & BootCapabilities.AcpiRsdp) != 0;
        if (acpiCapabilitySet && (!AcpiRsdp.HasRsdp || AcpiRsdp.ScanStatus != EfiConfigurationTableScanStatus.Ok || !IsKnownAcpiTableKind(AcpiRsdp.TableKind)))
        {
            return BootHandoffValidationResult.Invalid(BootHandoffStatus.InvalidAcpiRsdp, "Firmware handoff ACPI RSDP metadata is inconsistent.");
        }

        if (!acpiCapabilitySet && (AcpiRsdp.IsPresent || AcpiRsdp.RsdpAddress != 0))
        {
            return BootHandoffValidationResult.Invalid(BootHandoffStatus.InvalidAcpiRsdp, "Firmware handoff ACPI RSDP metadata is inconsistent.");
        }

        return BootHandoffValidationResult.Success();
    }

    private static BootMode MapBootMode(uint mode)
    {
        return mode switch
        {
            BootHandoffRecord.BootModeHost => BootMode.Host,
            BootHandoffRecord.BootModeUefi => BootMode.Uefi,
            _ => BootMode.Unknown
        };
    }

    private static FirmwareServicesMode MapFirmwareServicesMode(uint mode)
    {
        return mode switch
        {
            BootHandoffRecord.FirmwareServicesAvailable => FirmwareServicesMode.BootServicesAvailable,
            BootHandoffRecord.FirmwareServicesUefiGoneSimulated => FirmwareServicesMode.UefiGoneSimulated,
            BootHandoffRecord.FirmwareServicesExited => FirmwareServicesMode.BootServicesExited,
            _ => FirmwareServicesMode.BootServicesExited
        };
    }

    private static EfiAcpiTableKind MapAcpiTableKind(uint tableKind)
    {
        return tableKind switch
        {
            FirmwareAcpiRsdpRecord.TableKindAcpi10 => EfiAcpiTableKind.Acpi10,
            FirmwareAcpiRsdpRecord.TableKindAcpi20 => EfiAcpiTableKind.Acpi20,
            _ => EfiAcpiTableKind.None
        };
    }

    private static EfiConfigurationTableScanStatus MapConfigurationTableScanStatus(uint status)
    {
        return status switch
        {
            (uint)EfiConfigurationTableScanStatus.Ok => EfiConfigurationTableScanStatus.Ok,
            (uint)EfiConfigurationTableScanStatus.NotFound => EfiConfigurationTableScanStatus.NotFound,
            (uint)EfiConfigurationTableScanStatus.InvalidPointer => EfiConfigurationTableScanStatus.InvalidPointer,
            (uint)EfiConfigurationTableScanStatus.InvalidConfigurationTableCount => EfiConfigurationTableScanStatus.InvalidConfigurationTableCount,
            (uint)EfiConfigurationTableScanStatus.InvalidConfigurationTableAddress => EfiConfigurationTableScanStatus.InvalidConfigurationTableAddress,
            _ => EfiConfigurationTableScanStatus.InvalidPointer
        };
    }

    private static bool IsKnownAcpiTableKind(EfiAcpiTableKind tableKind)
    {
        return tableKind == EfiAcpiTableKind.Acpi10 || tableKind == EfiAcpiTableKind.Acpi20;
    }
}
