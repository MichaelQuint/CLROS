namespace OS.Boot;

public static class EfiBootHandoffValidator
{
    public const ulong DescriptorStride40 = 40;
    public const ulong DescriptorStride48 = 48;
    public const ulong MaximumDescriptorCount = 4096;
    public const ulong MaximumUnsignedLong = 0xffffffffffffffff;
    private const uint RequiredCapabilities = BootHandoffRecord.CapabilityTextOutput | BootHandoffRecord.CapabilityMemoryMap;
    private const uint KnownCapabilities = BootHandoffRecord.CapabilityTextOutput | BootHandoffRecord.CapabilityShutdown | BootHandoffRecord.CapabilityMemoryMap | BootHandoffRecord.CapabilityAcpiRsdp;
    public static EfiBootHandoffStatus Validate(BootHandoffRecord record)
    {
        if (record.BootMode != BootHandoffRecord.BootModeUefi)
        {
            return EfiBootHandoffStatus.InvalidBootMode;
        }

        uint unknownCapabilities = record.Capabilities & ~KnownCapabilities;
        if (unknownCapabilities != 0)
        {
            return EfiBootHandoffStatus.UnknownCapability;
        }

        uint missingCapabilities = RequiredCapabilities & ~record.Capabilities;
        if (missingCapabilities != 0)
        {
            return EfiBootHandoffStatus.MissingRequiredCapability;
        }

        if (!IsKnownFirmwareServicesMode(record.FirmwareServicesMode))
        {
            return EfiBootHandoffStatus.InvalidFirmwareServicesMode;
        }

        FirmwareMemoryMapRecord memoryMap = record.MemoryMap;
        if (!memoryMap.HasMap())
        {
            return EfiBootHandoffStatus.MissingMemoryMap;
        }

        if (!memoryMap.HasOwnedCopy())
        {
            return EfiBootHandoffStatus.MemoryMapNotOwned;
        }

        if (memoryMap.SourceBufferAddress == 0 || memoryMap.SourceBufferSize == 0 || memoryMap.SourceBufferSize < memoryMap.MapSize)
        {
            return EfiBootHandoffStatus.MissingSourceMemoryMap;
        }

        if (memoryMap.SourceBufferAddress == memoryMap.BufferAddress)
        {
            return EfiBootHandoffStatus.SourceMatchesOwnedBuffer;
        }

        if (memoryMap.MapSize == 0 || memoryMap.BufferSize < memoryMap.MapSize)
        {
            return EfiBootHandoffStatus.InvalidMapSize;
        }

        if (memoryMap.DescriptorSize != DescriptorStride40 && memoryMap.DescriptorSize != DescriptorStride48)
        {
            return EfiBootHandoffStatus.InvalidDescriptorSize;
        }

        if (memoryMap.DescriptorVersion == 0)
        {
            return EfiBootHandoffStatus.InvalidDescriptorVersion;
        }

        if (memoryMap.DescriptorCount == 0 || memoryMap.DescriptorCount > MaximumDescriptorCount)
        {
            return EfiBootHandoffStatus.InvalidDescriptorCount;
        }

        if (memoryMap.DescriptorCount > MaximumUnsignedLong / memoryMap.DescriptorSize)
        {
            return EfiBootHandoffStatus.DescriptorSizeOverflow;
        }

        ulong requiredMapBytes = memoryMap.DescriptorCount * memoryMap.DescriptorSize;
        if (requiredMapBytes != memoryMap.MapSize)
        {
            return EfiBootHandoffStatus.InvalidDescriptorCount;
        }

        if (!ValidateAcpiRsdp(record))
        {
            return EfiBootHandoffStatus.InvalidAcpiRsdp;
        }

        return EfiBootHandoffStatus.Success;
    }

    private static bool IsKnownFirmwareServicesMode(uint mode)
    {
        return mode == BootHandoffRecord.FirmwareServicesAvailable || mode == BootHandoffRecord.FirmwareServicesUefiGoneSimulated || mode == BootHandoffRecord.FirmwareServicesExited;
    }

    private static bool ValidateAcpiRsdp(BootHandoffRecord record)
    {
        bool capabilitySet = (record.Capabilities & BootHandoffRecord.CapabilityAcpiRsdp) != 0;
        FirmwareAcpiRsdpRecord acpiRsdp = record.AcpiRsdp;
        uint unknownFlags = acpiRsdp.Flags & ~FirmwareAcpiRsdpRecord.FlagPresent;
        if (unknownFlags != 0)
        {
            return false;
        }

        if (!capabilitySet)
        {
            return acpiRsdp.RsdpAddress == 0 && acpiRsdp.TableKind == 0 && acpiRsdp.Flags == 0;
        }

        if (!acpiRsdp.HasRsdp() || acpiRsdp.ScanStatus != FirmwareAcpiRsdpRecord.ScanStatusOk)
        {
            return false;
        }

        return acpiRsdp.TableKind == FirmwareAcpiRsdpRecord.TableKindAcpi10 || acpiRsdp.TableKind == FirmwareAcpiRsdpRecord.TableKindAcpi20;
    }
}
