using OS.Kernel.Platform;
using OS.Std.Binary;

namespace OS.Boot;

public static class AcpiRsdpSnapshotReader
{
    public static AcpiRsdpSnapshotResult ReadAndValidate(IPlatformPhysicalMemory physicalMemory, ulong physicalAddress, Span<byte> scratch)
    {
        if (physicalAddress == 0)
        {
            return AcpiRsdpSnapshotResult.InvalidPhysicalAddress();
        }

        if (physicalMemory is null)
        {
            return AcpiRsdpSnapshotResult.PhysicalMemoryUnavailable(physicalAddress);
        }

        if (scratch.Length < AcpiRsdpValidator.BaseLength)
        {
            return AcpiRsdpSnapshotResult.ScratchTooSmall(physicalAddress, AcpiRsdpValidator.BaseLength);
        }

        PlatformPhysicalMemoryResult baseRead = physicalMemory.ReadBytes(physicalAddress, AcpiRsdpValidator.BaseLength, scratch);
        if (!baseRead.Succeeded)
        {
            return AcpiRsdpSnapshotResult.PhysicalReadFailed(physicalAddress, baseRead, extendedReadAttempted: false);
        }

        AcpiRsdpValidationResult baseValidation = AcpiRsdpValidator.Validate(scratch.Slice(0, AcpiRsdpValidator.BaseLength));
        if (baseValidation.Succeeded)
        {
            return AcpiRsdpSnapshotResult.Success(physicalAddress, AcpiRsdpValidator.BaseLength, extendedReadAttempted: false, baseValidation, ReadRsdtAddress(scratch), xsdtAddress: 0);
        }

        if (baseValidation.Status != AcpiRsdpValidationStatus.ExtendedHeaderTooShort)
        {
            return AcpiRsdpSnapshotResult.ValidationFailed(physicalAddress, baseValidation, extendedReadAttempted: false);
        }

        if (scratch.Length < AcpiRsdpValidator.ExtendedHeaderLength)
        {
            return AcpiRsdpSnapshotResult.ScratchTooSmall(physicalAddress, AcpiRsdpValidator.ExtendedHeaderLength);
        }

        PlatformPhysicalMemoryResult headerRead = physicalMemory.ReadBytes(physicalAddress, AcpiRsdpValidator.ExtendedHeaderLength, scratch);
        if (!headerRead.Succeeded)
        {
            return AcpiRsdpSnapshotResult.PhysicalReadFailed(physicalAddress, headerRead, extendedReadAttempted: true);
        }

        AcpiRsdpValidationResult headerValidation = AcpiRsdpValidator.Validate(scratch.Slice(0, AcpiRsdpValidator.ExtendedHeaderLength));
        if (headerValidation.Succeeded)
        {
            return AcpiRsdpSnapshotResult.Success(physicalAddress, AcpiRsdpValidator.ExtendedHeaderLength, extendedReadAttempted: true, headerValidation, ReadRsdtAddress(scratch), ReadXsdtAddress(scratch));
        }

        if (headerValidation.Status != AcpiRsdpValidationStatus.SourceTooShortForExtendedLength)
        {
            return AcpiRsdpSnapshotResult.ValidationFailed(physicalAddress, headerValidation, extendedReadAttempted: true);
        }

        if (headerValidation.RequiredLength > int.MaxValue)
        {
            return AcpiRsdpSnapshotResult.ScratchTooSmall(physicalAddress, int.MaxValue);
        }

        int requiredLength = (int)headerValidation.RequiredLength;
        if (scratch.Length < requiredLength)
        {
            return AcpiRsdpSnapshotResult.ScratchTooSmall(physicalAddress, requiredLength);
        }

        PlatformPhysicalMemoryResult extendedRead = physicalMemory.ReadBytes(physicalAddress, requiredLength, scratch);
        if (!extendedRead.Succeeded)
        {
            return AcpiRsdpSnapshotResult.PhysicalReadFailed(physicalAddress, extendedRead, extendedReadAttempted: true);
        }

        AcpiRsdpValidationResult extendedValidation = AcpiRsdpValidator.Validate(scratch.Slice(0, requiredLength));
        if (!extendedValidation.Succeeded)
        {
            return AcpiRsdpSnapshotResult.ValidationFailed(physicalAddress, extendedValidation, extendedReadAttempted: true);
        }

        return AcpiRsdpSnapshotResult.Success(physicalAddress, requiredLength, extendedReadAttempted: true, extendedValidation, ReadRsdtAddress(scratch), ReadXsdtAddress(scratch));
    }

    private static ulong ReadRsdtAddress(ReadOnlySpan<byte> source)
    {
        return EndianPrimitives.ReadUInt32LittleEndian(source.Slice(AcpiRsdpValidator.RsdtAddressOffset, sizeof(uint)));
    }

    private static ulong ReadXsdtAddress(ReadOnlySpan<byte> source)
    {
        return EndianPrimitives.ReadUInt64LittleEndian(source.Slice(AcpiRsdpValidator.XsdtAddressOffset, sizeof(ulong)));
    }
}
