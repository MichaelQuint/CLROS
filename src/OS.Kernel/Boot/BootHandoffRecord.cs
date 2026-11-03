namespace OS.Boot;

public struct BootHandoffRecord
{
    public const uint BootModeUnknown = 0;
    public const uint BootModeHost = 1;
    public const uint BootModeUefi = 2;
    public const uint CapabilityTextOutput = 1;
    public const uint CapabilityShutdown = 2;
    public const uint CapabilityMemoryMap = 4;
    public const uint CapabilityAcpiRsdp = 8;
    public const uint FirmwareServicesAvailable = 0;
    public const uint FirmwareServicesUefiGoneSimulated = 1;
    public const uint FirmwareServicesExited = 2;
    public uint BootMode;
    public uint Capabilities;
    public uint FirmwareServicesMode;
    public FirmwareMemoryMapRecord MemoryMap;
    public FirmwareAcpiRsdpRecord AcpiRsdp;
    public static BootHandoffRecord CreateUefiWithMemoryMap(ulong memoryMapBufferAddress, ulong memoryMapBufferSize, ulong memoryMapSize, ulong mapKey, ulong descriptorSize, uint descriptorVersion, ulong descriptorCount)
    {
        BootHandoffRecord record = default;
        record.BootMode = BootModeUefi;
        record.Capabilities = CapabilityTextOutput | CapabilityMemoryMap;
        record.FirmwareServicesMode = FirmwareServicesAvailable;
        record.MemoryMap.BufferAddress = memoryMapBufferAddress;
        record.MemoryMap.BufferSize = memoryMapBufferSize;
        record.MemoryMap.SourceBufferAddress = memoryMapBufferAddress;
        record.MemoryMap.SourceBufferSize = memoryMapBufferSize;
        record.MemoryMap.MapSize = memoryMapSize;
        record.MemoryMap.MapKey = mapKey;
        record.MemoryMap.DescriptorSize = descriptorSize;
        record.MemoryMap.DescriptorVersion = descriptorVersion;
        record.MemoryMap.DescriptorCount = descriptorCount;
        record.MemoryMap.Flags = 0;
        return record;
    }

    public static BootHandoffRecord CreateUefiWithOwnedMemoryMap(ulong firmwareMemoryMapBufferAddress, ulong firmwareMemoryMapBufferSize, ulong ownedMemoryMapBufferAddress, ulong ownedMemoryMapBufferSize, ulong memoryMapSize, ulong mapKey, ulong descriptorSize, uint descriptorVersion, ulong descriptorCount)
    {
        BootHandoffRecord record = default;
        record.BootMode = BootModeUefi;
        record.Capabilities = CapabilityTextOutput | CapabilityMemoryMap;
        record.FirmwareServicesMode = FirmwareServicesAvailable;
        record.MemoryMap.BufferAddress = ownedMemoryMapBufferAddress;
        record.MemoryMap.BufferSize = ownedMemoryMapBufferSize;
        record.MemoryMap.SourceBufferAddress = firmwareMemoryMapBufferAddress;
        record.MemoryMap.SourceBufferSize = firmwareMemoryMapBufferSize;
        record.MemoryMap.MapSize = memoryMapSize;
        record.MemoryMap.MapKey = mapKey;
        record.MemoryMap.DescriptorSize = descriptorSize;
        record.MemoryMap.DescriptorVersion = descriptorVersion;
        record.MemoryMap.DescriptorCount = descriptorCount;
        record.MemoryMap.Flags = FirmwareMemoryMapRecord.FlagOwnedCopy;
        return record;
    }

    public void SetAcpiRsdp(ulong rsdpAddress, uint tableKind, uint scanStatus)
    {
        AcpiRsdp.RsdpAddress = rsdpAddress;
        AcpiRsdp.TableKind = tableKind;
        AcpiRsdp.ScanStatus = scanStatus;
        if (rsdpAddress != 0 && scanStatus == FirmwareAcpiRsdpRecord.ScanStatusOk)
        {
            AcpiRsdp.Flags = FirmwareAcpiRsdpRecord.FlagPresent;
            Capabilities |= CapabilityAcpiRsdp;
            return;
        }

        AcpiRsdp.Flags = 0;
        Capabilities &= ~CapabilityAcpiRsdp;
    }

    public void ClearAcpiRsdp(uint scanStatus)
    {
        AcpiRsdp.RsdpAddress = 0;
        AcpiRsdp.TableKind = 0;
        AcpiRsdp.ScanStatus = scanStatus;
        AcpiRsdp.Flags = 0;
        Capabilities &= ~CapabilityAcpiRsdp;
    }
}
