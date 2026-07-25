using OS.Kernel.Platform;

namespace OS.Boot;

public enum BootHandoffStatus
{
    Unknown = 0,
    Success = 1,
    MissingMemoryMap = 2,
    InvalidMapSize = 3,
    InvalidDescriptorSize = 4,
    InvalidDescriptorVersion = 5,
    InvalidDescriptorCount = 6,
    InvalidAcpiRsdp = 7
}

public readonly struct FirmwareMemoryMapMetadata
{
    public FirmwareMemoryMapMetadata(
        ulong bufferAddress,
        ulong bufferSize,
        ulong mapSize,
        ulong mapKey,
        ulong descriptorSize,
        uint descriptorVersion,
        ulong descriptorCount)
    {
        BufferAddress = bufferAddress;
        BufferSize = bufferSize;
        MapSize = mapSize;
        MapKey = mapKey;
        DescriptorSize = descriptorSize;
        DescriptorVersion = descriptorVersion;
        DescriptorCount = descriptorCount;
    }

    public ulong BufferAddress { get; }

    public ulong BufferSize { get; }

    public ulong MapSize { get; }

    public ulong MapKey { get; }

    public ulong DescriptorSize { get; }

    public uint DescriptorVersion { get; }

    public ulong DescriptorCount { get; }

    public bool HasMap => BufferAddress != 0 && BufferSize != 0 && MapSize != 0;
}

public readonly struct FirmwareAcpiRsdpMetadata
{
    public FirmwareAcpiRsdpMetadata(
        ulong rsdpAddress,
        EfiAcpiTableKind tableKind,
        EfiConfigurationTableScanStatus scanStatus,
        bool isPresent)
    {
        RsdpAddress = rsdpAddress;
        TableKind = tableKind;
        ScanStatus = scanStatus;
        IsPresent = isPresent;
    }

    public ulong RsdpAddress { get; }

    public EfiAcpiTableKind TableKind { get; }

    public EfiConfigurationTableScanStatus ScanStatus { get; }

    public bool IsPresent { get; }

    public bool HasRsdp => IsPresent && RsdpAddress != 0;
}

public readonly struct FirmwareBootContext
{
    public FirmwareBootContext(
        BootMode bootMode,
        BootCapabilities capabilities,
        FirmwareServicesMode firmwareServicesMode,
        FirmwareMemoryMapMetadata memoryMap,
        FirmwareAcpiRsdpMetadata acpiRsdp)
    {
        BootMode = bootMode;
        Capabilities = capabilities;
        FirmwareServicesMode = firmwareServicesMode;
        MemoryMap = memoryMap;
        AcpiRsdp = acpiRsdp;
    }

    public BootMode BootMode { get; }

    public BootCapabilities Capabilities { get; }

    public FirmwareServicesMode FirmwareServicesMode { get; }

    public FirmwareMemoryMapMetadata MemoryMap { get; }

    public FirmwareAcpiRsdpMetadata AcpiRsdp { get; }

    public bool BootServicesAvailable => FirmwareServicesMode == FirmwareServicesMode.BootServicesAvailable;

    public static FirmwareBootContext CreateUefi(
        BootCapabilities capabilities,
        FirmwareServicesMode firmwareServicesMode,
        FirmwareMemoryMapMetadata memoryMap)
    {
        return CreateUefi(
            capabilities,
            firmwareServicesMode,
            memoryMap,
            default);
    }

    public static FirmwareBootContext CreateUefi(
        BootCapabilities capabilities,
        FirmwareServicesMode firmwareServicesMode,
        FirmwareMemoryMapMetadata memoryMap,
        FirmwareAcpiRsdpMetadata acpiRsdp)
    {
        return new FirmwareBootContext(
            BootMode.Uefi,
            capabilities,
            firmwareServicesMode,
            memoryMap,
            acpiRsdp);
    }
}

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
        FirmwareMemoryMapMetadata metadata = new FirmwareMemoryMapMetadata(
            memoryMap.BufferAddress,
            memoryMap.BufferSize,
            memoryMap.MapSize,
            memoryMap.MapKey,
            memoryMap.DescriptorSize,
            memoryMap.DescriptorVersion,
            memoryMap.DescriptorCount);

        FirmwareAcpiRsdpRecord acpiRsdp = record.AcpiRsdp;
        FirmwareAcpiRsdpMetadata acpiMetadata = new FirmwareAcpiRsdpMetadata(
            acpiRsdp.RsdpAddress,
            MapAcpiTableKind(acpiRsdp.TableKind),
            MapConfigurationTableScanStatus(acpiRsdp.ScanStatus),
            acpiRsdp.HasRsdp());

        FirmwareBootContext context = new FirmwareBootContext(
            MapBootMode(record.BootMode),
            (BootCapabilities)record.Capabilities,
            MapFirmwareServicesMode(record.FirmwareServicesMode),
            metadata,
            acpiMetadata);

        return new BootHandoffInfo(context);
    }

    public BootHandoffValidationResult Validate()
    {
        FirmwareMemoryMapMetadata memoryMap = MemoryMap;

        if (!memoryMap.HasMap)
        {
            return BootHandoffValidationResult.Invalid(
                BootHandoffStatus.MissingMemoryMap,
                "Firmware handoff memory map metadata is required.");
        }

        if (memoryMap.MapSize > memoryMap.BufferSize)
        {
            return BootHandoffValidationResult.Invalid(
                BootHandoffStatus.InvalidMapSize,
                "Firmware handoff memory map size exceeds buffer size.");
        }

        if (memoryMap.DescriptorSize == 0)
        {
            return BootHandoffValidationResult.Invalid(
                BootHandoffStatus.InvalidDescriptorSize,
                "Firmware handoff memory map descriptor size is required.");
        }

        if (memoryMap.DescriptorVersion == 0)
        {
            return BootHandoffValidationResult.Invalid(
                BootHandoffStatus.InvalidDescriptorVersion,
                "Firmware handoff memory map descriptor version is required.");
        }

        if (memoryMap.DescriptorCount == 0
            || memoryMap.MapSize % memoryMap.DescriptorSize != 0
            || memoryMap.MapSize / memoryMap.DescriptorSize != memoryMap.DescriptorCount)
        {
            return BootHandoffValidationResult.Invalid(
                BootHandoffStatus.InvalidDescriptorCount,
                "Firmware handoff memory map descriptor count does not match map size.");
        }

        bool acpiCapabilitySet = (Capabilities & BootCapabilities.AcpiRsdp) != 0;
        if (acpiCapabilitySet
            && (!AcpiRsdp.HasRsdp
                || AcpiRsdp.ScanStatus != EfiConfigurationTableScanStatus.Ok
                || !IsKnownAcpiTableKind(AcpiRsdp.TableKind)))
        {
            return BootHandoffValidationResult.Invalid(
                BootHandoffStatus.InvalidAcpiRsdp,
                "Firmware handoff ACPI RSDP metadata is inconsistent.");
        }

        if (!acpiCapabilitySet && (AcpiRsdp.IsPresent || AcpiRsdp.RsdpAddress != 0))
        {
            return BootHandoffValidationResult.Invalid(
                BootHandoffStatus.InvalidAcpiRsdp,
                "Firmware handoff ACPI RSDP metadata is inconsistent.");
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
        return tableKind == EfiAcpiTableKind.Acpi10
            || tableKind == EfiAcpiTableKind.Acpi20;
    }
}

public readonly struct BootHandoffValidationResult
{
    private readonly string? _message;

    private BootHandoffValidationResult(
        bool succeeded,
        BootHandoffStatus status,
        string message)
    {
        Succeeded = succeeded;
        Status = status;
        _message = message;
    }

    public bool Succeeded { get; }

    public BootHandoffStatus Status { get; }

    public string Message => _message ?? string.Empty;

    public static BootHandoffValidationResult Success()
    {
        return new BootHandoffValidationResult(true, BootHandoffStatus.Success, string.Empty);
    }

    public static BootHandoffValidationResult Invalid(
        BootHandoffStatus status,
        string message)
    {
        return new BootHandoffValidationResult(false, status, message);
    }
}
