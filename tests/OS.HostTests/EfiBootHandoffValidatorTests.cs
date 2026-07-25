using System.Runtime.CompilerServices;
using OS.Boot;

internal static class EfiBootHandoffValidatorTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("efi.handoff.validator accepts owned memory-map handoff", TestAcceptsOwnedMemoryMapHandoff, ref failures);
        Run("efi.handoff.validator rejects invalid boot source and capabilities", TestRejectsSourceAndCapabilities, ref failures);
        Run("efi.handoff.validator rejects memory-map ownership issues", TestRejectsMemoryMapOwnership, ref failures);
        Run("efi.handoff.validator rejects descriptor metadata issues", TestRejectsDescriptorMetadata, ref failures);
        Run("efi.handoff.validator validates ACPI RSDP metadata", TestValidatesAcpiRsdpMetadata, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("EFI boot handoff validator host tests failed: " + failures);
        }
    }

    private static void Run(string name, Func<bool> test, ref int failures)
    {
        Console.WriteLine("TEST_START " + name);

        try
        {
            if (test())
            {
                Console.WriteLine("TEST_PASS " + name);
                return;
            }

            failures++;
            Console.WriteLine("TEST_FAIL " + name);
        }
        catch (Exception ex)
        {
            failures++;
            Console.WriteLine("TEST_FAIL " + name + " " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static bool TestAcceptsOwnedMemoryMapHandoff()
    {
        BootHandoffRecord record = CreateValidOwnedRecord();

        return EfiBootHandoffValidator.Validate(record) == EfiBootHandoffStatus.Success;
    }

    private static bool TestRejectsSourceAndCapabilities()
    {
        BootHandoffRecord invalidBootMode = CreateValidOwnedRecord();
        invalidBootMode.BootMode = BootHandoffRecord.BootModeHost;

        BootHandoffRecord unknownCapability = CreateValidOwnedRecord();
        unknownCapability.Capabilities |= 0x80000000;

        BootHandoffRecord missingCapability = CreateValidOwnedRecord();
        missingCapability.Capabilities = BootHandoffRecord.CapabilityTextOutput;

        BootHandoffRecord invalidFirmwareMode = CreateValidOwnedRecord();
        invalidFirmwareMode.FirmwareServicesMode = 99;

        return EfiBootHandoffValidator.Validate(invalidBootMode) == EfiBootHandoffStatus.InvalidBootMode
            && EfiBootHandoffValidator.Validate(unknownCapability) == EfiBootHandoffStatus.UnknownCapability
            && EfiBootHandoffValidator.Validate(missingCapability) == EfiBootHandoffStatus.MissingRequiredCapability
            && EfiBootHandoffValidator.Validate(invalidFirmwareMode) == EfiBootHandoffStatus.InvalidFirmwareServicesMode;
    }

    private static bool TestRejectsMemoryMapOwnership()
    {
        BootHandoffRecord missingMap = CreateValidOwnedRecord();
        missingMap.MemoryMap.BufferAddress = 0;

        BootHandoffRecord notOwned = CreateValidOwnedRecord();
        notOwned.MemoryMap.Flags = 0;

        BootHandoffRecord missingSource = CreateValidOwnedRecord();
        missingSource.MemoryMap.SourceBufferAddress = 0;

        BootHandoffRecord sourceAlias = CreateValidOwnedRecord();
        sourceAlias.MemoryMap.SourceBufferAddress = sourceAlias.MemoryMap.BufferAddress;

        BootHandoffRecord invalidMapSize = CreateValidOwnedRecord();
        invalidMapSize.MemoryMap.BufferSize = invalidMapSize.MemoryMap.MapSize - 1;

        return EfiBootHandoffValidator.Validate(missingMap) == EfiBootHandoffStatus.MissingMemoryMap
            && EfiBootHandoffValidator.Validate(notOwned) == EfiBootHandoffStatus.MemoryMapNotOwned
            && EfiBootHandoffValidator.Validate(missingSource) == EfiBootHandoffStatus.MissingSourceMemoryMap
            && EfiBootHandoffValidator.Validate(sourceAlias) == EfiBootHandoffStatus.SourceMatchesOwnedBuffer
            && EfiBootHandoffValidator.Validate(invalidMapSize) == EfiBootHandoffStatus.InvalidMapSize;
    }

    private static bool TestRejectsDescriptorMetadata()
    {
        BootHandoffRecord invalidSize = CreateValidOwnedRecord();
        invalidSize.MemoryMap.DescriptorSize = 56;

        BootHandoffRecord invalidVersion = CreateValidOwnedRecord();
        invalidVersion.MemoryMap.DescriptorVersion = 0;

        BootHandoffRecord zeroCount = CreateValidOwnedRecord();
        zeroCount.MemoryMap.DescriptorCount = 0;

        BootHandoffRecord excessiveCount = CreateValidOwnedRecord();
        excessiveCount.MemoryMap.DescriptorCount = EfiBootHandoffValidator.MaximumDescriptorCount + 1;

        BootHandoffRecord overflow = CreateValidOwnedRecord();
        overflow.MemoryMap.DescriptorSize = EfiBootHandoffValidator.DescriptorStride40;
        overflow.MemoryMap.DescriptorCount =
            (EfiBootHandoffValidator.MaximumUnsignedLong / overflow.MemoryMap.DescriptorSize) + 1;

        BootHandoffRecord mismatchedCount = CreateValidOwnedRecord();
        mismatchedCount.MemoryMap.DescriptorCount = 2;

        return EfiBootHandoffValidator.Validate(invalidSize) == EfiBootHandoffStatus.InvalidDescriptorSize
            && EfiBootHandoffValidator.Validate(invalidVersion) == EfiBootHandoffStatus.InvalidDescriptorVersion
            && EfiBootHandoffValidator.Validate(zeroCount) == EfiBootHandoffStatus.InvalidDescriptorCount
            && EfiBootHandoffValidator.Validate(excessiveCount) == EfiBootHandoffStatus.InvalidDescriptorCount
            && EfiBootHandoffValidator.Validate(overflow) == EfiBootHandoffStatus.InvalidDescriptorCount
            && EfiBootHandoffValidator.Validate(mismatchedCount) == EfiBootHandoffStatus.InvalidDescriptorCount;
    }

    private static bool TestValidatesAcpiRsdpMetadata()
    {
        BootHandoffRecord validAcpi = CreateValidOwnedRecord();
        validAcpi.SetAcpiRsdp(
            0x7fe00000,
            FirmwareAcpiRsdpRecord.TableKindAcpi20,
            FirmwareAcpiRsdpRecord.ScanStatusOk);

        BootHandoffRecord zeroPointer = CreateValidOwnedRecord();
        zeroPointer.Capabilities |= BootHandoffRecord.CapabilityAcpiRsdp;
        zeroPointer.AcpiRsdp.Flags = FirmwareAcpiRsdpRecord.FlagPresent;
        zeroPointer.AcpiRsdp.TableKind = FirmwareAcpiRsdpRecord.TableKindAcpi20;
        zeroPointer.AcpiRsdp.ScanStatus = FirmwareAcpiRsdpRecord.ScanStatusOk;

        BootHandoffRecord invalidKind = CreateValidOwnedRecord();
        invalidKind.SetAcpiRsdp(
            0x7fe00000,
            99,
            FirmwareAcpiRsdpRecord.ScanStatusOk);

        BootHandoffRecord pointerWithoutCapability = CreateValidOwnedRecord();
        pointerWithoutCapability.AcpiRsdp.RsdpAddress = 0x7fe00000;
        pointerWithoutCapability.AcpiRsdp.TableKind = FirmwareAcpiRsdpRecord.TableKindAcpi20;
        pointerWithoutCapability.AcpiRsdp.ScanStatus = FirmwareAcpiRsdpRecord.ScanStatusOk;
        pointerWithoutCapability.AcpiRsdp.Flags = FirmwareAcpiRsdpRecord.FlagPresent;

        return EfiBootHandoffValidator.Validate(validAcpi) == EfiBootHandoffStatus.Success
            && EfiBootHandoffValidator.Validate(zeroPointer) == EfiBootHandoffStatus.InvalidAcpiRsdp
            && EfiBootHandoffValidator.Validate(invalidKind) == EfiBootHandoffStatus.InvalidAcpiRsdp
            && EfiBootHandoffValidator.Validate(pointerWithoutCapability) == EfiBootHandoffStatus.InvalidAcpiRsdp;
    }

    private static BootHandoffRecord CreateValidOwnedRecord()
    {
        return BootHandoffRecord.CreateUefiWithOwnedMemoryMap(
            firmwareMemoryMapBufferAddress: 0x100000,
            firmwareMemoryMapBufferSize: 4096,
            ownedMemoryMapBufferAddress: 0x900000,
            ownedMemoryMapBufferSize: 128 * 1024,
            memoryMapSize: 48 * 3,
            mapKey: 0x77,
            descriptorSize: 48,
            descriptorVersion: 1,
            descriptorCount: 3);
    }
}
