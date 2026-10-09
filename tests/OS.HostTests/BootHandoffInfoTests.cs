using System.Runtime.CompilerServices;
using OS.Boot;
using OS.Kernel.Platform;

internal static class BootHandoffInfoTests
{

    [HostTest("boothandoff validates UEFI memory-map metadata", HostLane.Fast, 0)]
    private static bool TestValidMetadata()
    {
        BootHandoffInfo handoff = CreateValidHandoff(FirmwareServicesMode.BootServicesAvailable);
        BootHandoffValidationResult result = handoff.Validate();
        FirmwareMemoryMapMetadata memoryMap = handoff.MemoryMap;

        return HostCheck.Result(result.Succeeded
            && result.Status == BootHandoffStatus.Success
            && result.Message.Length == 0
            && handoff.BootMode == BootMode.Uefi
            && handoff.Capabilities == ValidCapabilities
            && handoff.FirmwareServicesMode == FirmwareServicesMode.BootServicesAvailable
            && handoff.FirmwareContext.BootServicesAvailable
            && memoryMap.BufferAddress == ValidBufferAddress
            && memoryMap.BufferSize == ValidBufferSize
            && memoryMap.MapSize == ValidMapSize
            && memoryMap.MapKey == ValidMapKey
            && memoryMap.DescriptorSize == ValidDescriptorSize
            && memoryMap.DescriptorVersion == ValidDescriptorVersion
            && memoryMap.DescriptorCount == ValidDescriptorCount
            && memoryMap.HasMap);
    }

    [HostTest("boothandoff rejects missing memory map", HostLane.Fast, 1)]
    private static bool TestMissingMemoryMap()
    {
        FirmwareMemoryMapMetadata memoryMap = new FirmwareMemoryMapMetadata(
            0,
            ValidBufferSize,
            ValidMapSize,
            ValidMapKey,
            ValidDescriptorSize,
            ValidDescriptorVersion,
            ValidDescriptorCount);

        BootHandoffValidationResult result = CreateHandoff(memoryMap).Validate();

        return HostCheck.Result(!result.Succeeded
            && result.Status == BootHandoffStatus.MissingMemoryMap
            && result.Message.Contains("memory map", StringComparison.Ordinal));
    }

    [HostTest("boothandoff rejects invalid descriptor size", HostLane.Fast, 2)]
    private static bool TestInvalidDescriptorSize()
    {
        FirmwareMemoryMapMetadata memoryMap = new FirmwareMemoryMapMetadata(
            ValidBufferAddress,
            ValidBufferSize,
            ValidMapSize,
            ValidMapKey,
            0,
            ValidDescriptorVersion,
            ValidDescriptorCount);

        BootHandoffValidationResult result = CreateHandoff(memoryMap).Validate();

        return HostCheck.Result(!result.Succeeded
            && result.Status == BootHandoffStatus.InvalidDescriptorSize
            && result.Message.Contains("descriptor size", StringComparison.Ordinal));
    }

    [HostTest("boothandoff rejects invalid descriptor version", HostLane.Fast, 3)]
    private static bool TestInvalidDescriptorVersion()
    {
        FirmwareMemoryMapMetadata memoryMap = new FirmwareMemoryMapMetadata(
            ValidBufferAddress,
            ValidBufferSize,
            ValidMapSize,
            ValidMapKey,
            ValidDescriptorSize,
            0,
            ValidDescriptorCount);

        BootHandoffValidationResult result = CreateHandoff(memoryMap).Validate();

        return HostCheck.Result(!result.Succeeded
            && result.Status == BootHandoffStatus.InvalidDescriptorVersion
            && result.Message.Contains("descriptor version", StringComparison.Ordinal));
    }

    [HostTest("boothandoff rejects invalid descriptor count", HostLane.Fast, 4)]
    private static bool TestInvalidDescriptorCount()
    {
        FirmwareMemoryMapMetadata zeroCount = new FirmwareMemoryMapMetadata(
            ValidBufferAddress,
            ValidBufferSize,
            ValidMapSize,
            ValidMapKey,
            ValidDescriptorSize,
            ValidDescriptorVersion,
            0);

        FirmwareMemoryMapMetadata mismatchedCount = new FirmwareMemoryMapMetadata(
            ValidBufferAddress,
            ValidBufferSize,
            ValidMapSize,
            ValidMapKey,
            ValidDescriptorSize,
            ValidDescriptorVersion,
            ValidDescriptorCount + 1);

        BootHandoffValidationResult zeroCountResult = CreateHandoff(zeroCount).Validate();
        BootHandoffValidationResult mismatchedCountResult = CreateHandoff(mismatchedCount).Validate();

        return HostCheck.Result(!zeroCountResult.Succeeded
            && zeroCountResult.Status == BootHandoffStatus.InvalidDescriptorCount
            && zeroCountResult.Message.Contains("descriptor count", StringComparison.Ordinal)
            && !mismatchedCountResult.Succeeded
            && mismatchedCountResult.Status == BootHandoffStatus.InvalidDescriptorCount);
    }

    [HostTest("boothandoff preserves UEFI-gone simulation mode", HostLane.Fast, 5)]
    private static bool TestUefiGoneSimulationMode()
    {
        BootHandoffInfo handoff = CreateValidHandoff(FirmwareServicesMode.UefiGoneSimulated);
        BootHandoffValidationResult result = handoff.Validate();

        return HostCheck.Result(result.Succeeded
            && result.Status == BootHandoffStatus.Success
            && handoff.FirmwareServicesMode == FirmwareServicesMode.UefiGoneSimulated
            && !handoff.FirmwareContext.BootServicesAvailable
            && handoff.MemoryMap.DescriptorCount == ValidDescriptorCount);
    }

    [HostTest("boothandoff imports no-stdlib EFI handoff record", HostLane.Fast, 6)]
    private static bool TestImportsNoStdlibRecord()
    {
        BootHandoffRecord record = BootHandoffRecord.CreateUefiWithMemoryMap(
            ValidBufferAddress,
            ValidBufferSize,
            ValidMapSize,
            ValidMapKey,
            ValidDescriptorSize,
            ValidDescriptorVersion,
            ValidDescriptorCount);

        BootHandoffInfo handoff = BootHandoffInfo.FromRecord(record);
        BootHandoffValidationResult result = handoff.Validate();

        return HostCheck.Result(record.BootMode == BootHandoffRecord.BootModeUefi
            && record.Capabilities == (BootHandoffRecord.CapabilityTextOutput | BootHandoffRecord.CapabilityMemoryMap)
            && record.FirmwareServicesMode == BootHandoffRecord.FirmwareServicesAvailable
            && record.MemoryMap.HasMap()
            && record.MemoryMap.SourceBufferAddress == record.MemoryMap.BufferAddress
            && record.MemoryMap.SourceBufferSize == record.MemoryMap.BufferSize
            && !record.MemoryMap.HasOwnedCopy()
            && result.Succeeded
            && handoff.BootMode == BootMode.Uefi
            && handoff.Capabilities == (BootCapabilities.TextOutput | BootCapabilities.MemoryMap)
            && handoff.FirmwareServicesMode == FirmwareServicesMode.BootServicesAvailable
            && handoff.MemoryMap.BufferAddress == ValidBufferAddress
            && handoff.MemoryMap.BufferSize == ValidBufferSize
            && handoff.MemoryMap.MapSize == ValidMapSize
            && handoff.MemoryMap.MapKey == ValidMapKey
            && handoff.MemoryMap.DescriptorSize == ValidDescriptorSize
            && handoff.MemoryMap.DescriptorVersion == ValidDescriptorVersion
            && handoff.MemoryMap.DescriptorCount == ValidDescriptorCount);
    }

    [HostTest("boothandoff imports owned memory-map handoff record", HostLane.Fast, 7)]
    private static bool TestImportsOwnedMemoryMapRecord()
    {
        const ulong firmwareBufferAddress = ValidBufferAddress;
        const ulong firmwareBufferSize = ValidBufferSize;
        const ulong ownedBufferAddress = 0x900000;
        const ulong ownedBufferSize = 128 * 1024;

        BootHandoffRecord record = BootHandoffRecord.CreateUefiWithOwnedMemoryMap(
            firmwareBufferAddress,
            firmwareBufferSize,
            ownedBufferAddress,
            ownedBufferSize,
            ValidMapSize,
            ValidMapKey,
            ValidDescriptorSize,
            ValidDescriptorVersion,
            ValidDescriptorCount);

        BootHandoffInfo handoff = BootHandoffInfo.FromRecord(record);
        BootHandoffValidationResult result = handoff.Validate();

        return HostCheck.Result(record.MemoryMap.HasMap()
            && record.MemoryMap.HasOwnedCopy()
            && record.MemoryMap.BufferAddress == ownedBufferAddress
            && record.MemoryMap.BufferSize == ownedBufferSize
            && record.MemoryMap.SourceBufferAddress == firmwareBufferAddress
            && record.MemoryMap.SourceBufferSize == firmwareBufferSize
            && result.Succeeded
            && handoff.MemoryMap.BufferAddress == ownedBufferAddress
            && handoff.MemoryMap.BufferSize == ownedBufferSize
            && handoff.MemoryMap.MapSize == ValidMapSize
            && handoff.MemoryMap.DescriptorCount == ValidDescriptorCount);
    }

    [HostTest("boothandoff imports ACPI RSDP metadata", HostLane.Fast, 8)]
    private static bool TestImportsAcpiRsdpMetadata()
    {
        BootHandoffRecord record = CreateValidOwnedRecord();
        record.SetAcpiRsdp(
            ValidRsdpAddress,
            FirmwareAcpiRsdpRecord.TableKindAcpi20,
            FirmwareAcpiRsdpRecord.ScanStatusOk);

        BootHandoffInfo handoff = BootHandoffInfo.FromRecord(record);
        BootHandoffValidationResult result = handoff.Validate();

        return HostCheck.Result(record.AcpiRsdp.HasRsdp()
            && (record.Capabilities & BootHandoffRecord.CapabilityAcpiRsdp) != 0
            && result.Succeeded
            && handoff.Capabilities == (BootCapabilities.TextOutput | BootCapabilities.MemoryMap | BootCapabilities.AcpiRsdp)
            && handoff.AcpiRsdp.HasRsdp
            && handoff.AcpiRsdp.IsPresent
            && handoff.AcpiRsdp.RsdpAddress == ValidRsdpAddress
            && handoff.AcpiRsdp.TableKind == EfiAcpiTableKind.Acpi20
            && handoff.AcpiRsdp.ScanStatus == EfiConfigurationTableScanStatus.Ok);
    }

    [HostTest("boothandoff keeps ACPI RSDP optional", HostLane.Fast, 9)]
    private static bool TestAcpiRsdpOptional()
    {
        BootHandoffRecord record = CreateValidOwnedRecord();
        record.ClearAcpiRsdp((uint)EfiConfigurationTableScanStatus.NotFound);

        BootHandoffInfo handoff = BootHandoffInfo.FromRecord(record);
        BootHandoffValidationResult result = handoff.Validate();

        return HostCheck.Result(result.Succeeded
            && (record.Capabilities & BootHandoffRecord.CapabilityAcpiRsdp) == 0
            && !handoff.AcpiRsdp.HasRsdp
            && !handoff.AcpiRsdp.IsPresent
            && handoff.AcpiRsdp.RsdpAddress == 0
            && handoff.AcpiRsdp.ScanStatus == EfiConfigurationTableScanStatus.NotFound);
    }

    [HostTest("boothandoff rejects inconsistent ACPI RSDP metadata", HostLane.Fast, 10)]
    private static bool TestRejectsInconsistentAcpiRsdpMetadata()
    {
        BootHandoffRecord zeroPointer = CreateValidOwnedRecord();
        zeroPointer.Capabilities |= BootHandoffRecord.CapabilityAcpiRsdp;
        zeroPointer.AcpiRsdp.Flags = FirmwareAcpiRsdpRecord.FlagPresent;
        zeroPointer.AcpiRsdp.TableKind = FirmwareAcpiRsdpRecord.TableKindAcpi20;
        zeroPointer.AcpiRsdp.ScanStatus = FirmwareAcpiRsdpRecord.ScanStatusOk;

        BootHandoffRecord invalidKind = CreateValidOwnedRecord();
        invalidKind.SetAcpiRsdp(
            ValidRsdpAddress,
            99,
            FirmwareAcpiRsdpRecord.ScanStatusOk);

        BootHandoffRecord pointerWithoutCapability = CreateValidOwnedRecord();
        pointerWithoutCapability.AcpiRsdp.RsdpAddress = ValidRsdpAddress;
        pointerWithoutCapability.AcpiRsdp.TableKind = FirmwareAcpiRsdpRecord.TableKindAcpi20;
        pointerWithoutCapability.AcpiRsdp.ScanStatus = FirmwareAcpiRsdpRecord.ScanStatusOk;
        pointerWithoutCapability.AcpiRsdp.Flags = FirmwareAcpiRsdpRecord.FlagPresent;

        BootHandoffValidationResult zeroPointerResult = BootHandoffInfo.FromRecord(zeroPointer).Validate();
        BootHandoffValidationResult invalidKindResult = BootHandoffInfo.FromRecord(invalidKind).Validate();
        BootHandoffValidationResult pointerWithoutCapabilityResult =
            BootHandoffInfo.FromRecord(pointerWithoutCapability).Validate();

        return HostCheck.Result(!zeroPointerResult.Succeeded
            && zeroPointerResult.Status == BootHandoffStatus.InvalidAcpiRsdp
            && !invalidKindResult.Succeeded
            && invalidKindResult.Status == BootHandoffStatus.InvalidAcpiRsdp
            && !pointerWithoutCapabilityResult.Succeeded
            && pointerWithoutCapabilityResult.Status == BootHandoffStatus.InvalidAcpiRsdp);
    }

    private static BootHandoffInfo CreateValidHandoff(FirmwareServicesMode firmwareServicesMode)
    {
        return CreateHandoff(
            new FirmwareMemoryMapMetadata(
                ValidBufferAddress,
                ValidBufferSize,
                ValidMapSize,
                ValidMapKey,
                ValidDescriptorSize,
                ValidDescriptorVersion,
                ValidDescriptorCount),
            firmwareServicesMode);
    }

    private static BootHandoffInfo CreateHandoff(FirmwareMemoryMapMetadata memoryMap)
    {
        return CreateHandoff(memoryMap, FirmwareServicesMode.BootServicesAvailable);
    }

    private static BootHandoffInfo CreateHandoff(
        FirmwareMemoryMapMetadata memoryMap,
        FirmwareServicesMode firmwareServicesMode)
    {
        return new BootHandoffInfo(
            FirmwareBootContext.CreateUefi(
                ValidCapabilities,
                firmwareServicesMode,
                memoryMap));
    }

    private static BootHandoffRecord CreateValidOwnedRecord()
    {
        return BootHandoffRecord.CreateUefiWithOwnedMemoryMap(
            firmwareMemoryMapBufferAddress: ValidBufferAddress,
            firmwareMemoryMapBufferSize: ValidBufferSize,
            ownedMemoryMapBufferAddress: 0x900000,
            ownedMemoryMapBufferSize: 128 * 1024,
            memoryMapSize: ValidMapSize,
            mapKey: ValidMapKey,
            descriptorSize: ValidDescriptorSize,
            descriptorVersion: ValidDescriptorVersion,
            descriptorCount: ValidDescriptorCount);
    }

    private const BootCapabilities ValidCapabilities =
        BootCapabilities.TextOutput | BootCapabilities.Shutdown | BootCapabilities.MemoryMap;

    private const ulong ValidBufferAddress = 0x100000;
    private const ulong ValidBufferSize = 4096;
    private const ulong ValidMapKey = 0x77;
    private const ulong ValidDescriptorSize = 48;
    private const uint ValidDescriptorVersion = 1;
    private const ulong ValidDescriptorCount = 3;
    private const ulong ValidMapSize = ValidDescriptorSize * ValidDescriptorCount;
    private const ulong ValidRsdpAddress = 0x7fe00000;
}
