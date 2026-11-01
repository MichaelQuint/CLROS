namespace OS.Boot;
public static class BootSequenceHandoffValidator
{
    public const string ReadyMarker = "BOOT_HANDOFF_READY";
    public const string FailMarkerPrefix = "BOOT_HANDOFF_FAIL";
    public const ulong DescriptorStride40 = EfiBootHandoffValidator.DescriptorStride40;
    public const ulong DescriptorStride48 = EfiBootHandoffValidator.DescriptorStride48;
    public const ulong MaximumDescriptorCount = EfiBootHandoffValidator.MaximumDescriptorCount;
    private const BootCapabilities RequiredCapabilities = BootCapabilities.TextOutput | BootCapabilities.MemoryMap;
    private const BootCapabilities KnownCapabilities = BootCapabilities.TextOutput | BootCapabilities.Shutdown | BootCapabilities.MemoryMap | BootCapabilities.AcpiRsdp;
    public static BootSequenceHandoffValidationResult Validate(BootHandoffRecord record)
    {
        EfiBootHandoffStatus coreStatus = EfiBootHandoffValidator.Validate(record);
        if (coreStatus == EfiBootHandoffStatus.Success)
        {
            return BootSequenceHandoffValidationResult.Success(record.MemoryMap.DescriptorCount * record.MemoryMap.DescriptorSize);
        }

        if (record.BootMode != BootHandoffRecord.BootModeUefi)
        {
            return BootSequenceHandoffValidationResult.Fail(BootSequenceHandoffStatus.InvalidBootMode);
        }

        BootCapabilities capabilities = (BootCapabilities)record.Capabilities;
        BootCapabilities unknownCapabilities = capabilities & ~KnownCapabilities;
        if (unknownCapabilities != BootCapabilities.None)
        {
            return BootSequenceHandoffValidationResult.Fail(BootSequenceHandoffStatus.UnknownCapability, unknownCapabilities: unknownCapabilities);
        }

        BootCapabilities missingCapabilities = RequiredCapabilities & ~capabilities;
        if (missingCapabilities != BootCapabilities.None)
        {
            return BootSequenceHandoffValidationResult.Fail(BootSequenceHandoffStatus.MissingRequiredCapability, missingCapabilities: missingCapabilities);
        }

        if (!IsKnownFirmwareServicesMode(record.FirmwareServicesMode))
        {
            return BootSequenceHandoffValidationResult.Fail(BootSequenceHandoffStatus.InvalidFirmwareServicesMode);
        }

        FirmwareMemoryMapRecord memoryMap = record.MemoryMap;
        if (!memoryMap.HasMap())
        {
            return BootSequenceHandoffValidationResult.Fail(BootSequenceHandoffStatus.MissingMemoryMap);
        }

        if (!memoryMap.HasOwnedCopy())
        {
            return BootSequenceHandoffValidationResult.Fail(BootSequenceHandoffStatus.MemoryMapNotOwned);
        }

        if (memoryMap.SourceBufferAddress == 0 || memoryMap.SourceBufferSize == 0 || memoryMap.SourceBufferSize < memoryMap.MapSize)
        {
            return BootSequenceHandoffValidationResult.Fail(BootSequenceHandoffStatus.MissingSourceMemoryMap);
        }

        if (memoryMap.SourceBufferAddress == memoryMap.BufferAddress)
        {
            return BootSequenceHandoffValidationResult.Fail(BootSequenceHandoffStatus.SourceMatchesOwnedBuffer);
        }

        if (memoryMap.MapSize == 0 || memoryMap.BufferSize < memoryMap.MapSize)
        {
            return BootSequenceHandoffValidationResult.Fail(BootSequenceHandoffStatus.InvalidMapSize);
        }

        if (memoryMap.DescriptorSize != DescriptorStride40 && memoryMap.DescriptorSize != DescriptorStride48)
        {
            return BootSequenceHandoffValidationResult.Fail(BootSequenceHandoffStatus.InvalidDescriptorSize);
        }

        if (memoryMap.DescriptorVersion == 0)
        {
            return BootSequenceHandoffValidationResult.Fail(BootSequenceHandoffStatus.InvalidDescriptorVersion);
        }

        if (memoryMap.DescriptorCount == 0 || memoryMap.DescriptorCount > MaximumDescriptorCount)
        {
            return BootSequenceHandoffValidationResult.Fail(BootSequenceHandoffStatus.InvalidDescriptorCount);
        }

        if (memoryMap.DescriptorCount > ulong.MaxValue / memoryMap.DescriptorSize)
        {
            return BootSequenceHandoffValidationResult.Fail(BootSequenceHandoffStatus.DescriptorSizeOverflow);
        }

        ulong requiredMapBytes = memoryMap.DescriptorCount * memoryMap.DescriptorSize;
        if (requiredMapBytes != memoryMap.MapSize)
        {
            return BootSequenceHandoffValidationResult.Fail(BootSequenceHandoffStatus.InvalidDescriptorCount, requiredMapBytes: requiredMapBytes);
        }

        if (!ValidateAcpiRsdp(record))
        {
            return BootSequenceHandoffValidationResult.Fail(BootSequenceHandoffStatus.InvalidAcpiRsdp);
        }

        return BootSequenceHandoffValidationResult.Fail(MapCoreStatus(coreStatus));
    }

    public static string CreateFailMarker(BootSequenceHandoffStatus status)
    {
        return FailMarkerPrefix + " code=" + (int)status + " status=" + status;
    }

    private static bool IsKnownFirmwareServicesMode(uint mode)
    {
        return mode == BootHandoffRecord.FirmwareServicesAvailable || mode == BootHandoffRecord.FirmwareServicesUefiGoneSimulated || mode == BootHandoffRecord.FirmwareServicesExited;
    }

    private static BootSequenceHandoffStatus MapCoreStatus(EfiBootHandoffStatus status)
    {
        return status switch
        {
            EfiBootHandoffStatus.InvalidBootMode => BootSequenceHandoffStatus.InvalidBootMode,
            EfiBootHandoffStatus.UnknownCapability => BootSequenceHandoffStatus.UnknownCapability,
            EfiBootHandoffStatus.MissingRequiredCapability => BootSequenceHandoffStatus.MissingRequiredCapability,
            EfiBootHandoffStatus.InvalidFirmwareServicesMode => BootSequenceHandoffStatus.InvalidFirmwareServicesMode,
            EfiBootHandoffStatus.MissingMemoryMap => BootSequenceHandoffStatus.MissingMemoryMap,
            EfiBootHandoffStatus.MemoryMapNotOwned => BootSequenceHandoffStatus.MemoryMapNotOwned,
            EfiBootHandoffStatus.MissingSourceMemoryMap => BootSequenceHandoffStatus.MissingSourceMemoryMap,
            EfiBootHandoffStatus.SourceMatchesOwnedBuffer => BootSequenceHandoffStatus.SourceMatchesOwnedBuffer,
            EfiBootHandoffStatus.InvalidMapSize => BootSequenceHandoffStatus.InvalidMapSize,
            EfiBootHandoffStatus.InvalidDescriptorSize => BootSequenceHandoffStatus.InvalidDescriptorSize,
            EfiBootHandoffStatus.InvalidDescriptorVersion => BootSequenceHandoffStatus.InvalidDescriptorVersion,
            EfiBootHandoffStatus.InvalidDescriptorCount => BootSequenceHandoffStatus.InvalidDescriptorCount,
            EfiBootHandoffStatus.DescriptorSizeOverflow => BootSequenceHandoffStatus.DescriptorSizeOverflow,
            EfiBootHandoffStatus.InvalidAcpiRsdp => BootSequenceHandoffStatus.InvalidAcpiRsdp,
            _ => BootSequenceHandoffStatus.InvalidBootMode
        };
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
