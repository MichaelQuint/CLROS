using OS.Kernel.Platform;
using OS.Std.Binary;

namespace OS.Boot;

public enum AcpiRsdpSnapshotStatus
{
    Success = 0,
    InvalidPhysicalAddress = 1,
    ScratchTooSmall = 2,
    PhysicalReadFailed = 3,
    BaseValidationFailed = 4,
    ExtendedReadFailed = 5,
    ExtendedValidationFailed = 6,
    PhysicalMemoryUnavailable = 7
}

public readonly struct AcpiRsdpSnapshotResult
{
    private AcpiRsdpSnapshotResult(
        AcpiRsdpSnapshotStatus status,
        ulong physicalAddress,
        int bytesRead,
        int requiredBytes,
        bool extendedReadAttempted,
        PlatformPhysicalMemoryStatus physicalReadStatus,
        AcpiRsdpValidationStatus validationStatus,
        AcpiRsdpRevisionKind revisionKind,
        byte revision,
        uint declaredLength,
        ulong rsdtAddress,
        ulong xsdtAddress)
    {
        Status = status;
        PhysicalAddress = physicalAddress;
        BytesRead = bytesRead;
        RequiredBytes = requiredBytes;
        ExtendedReadAttempted = extendedReadAttempted;
        PhysicalReadStatus = physicalReadStatus;
        ValidationStatus = validationStatus;
        RevisionKind = revisionKind;
        Revision = revision;
        DeclaredLength = declaredLength;
        RsdtAddress = rsdtAddress;
        XsdtAddress = xsdtAddress;
    }

    public AcpiRsdpSnapshotStatus Status { get; }

    public bool Succeeded => Status == AcpiRsdpSnapshotStatus.Success;

    public ulong PhysicalAddress { get; }

    public int BytesRead { get; }

    public int RequiredBytes { get; }

    public bool ExtendedReadAttempted { get; }

    public PlatformPhysicalMemoryStatus PhysicalReadStatus { get; }

    public AcpiRsdpValidationStatus ValidationStatus { get; }

    public AcpiRsdpRevisionKind RevisionKind { get; }

    public byte Revision { get; }

    public uint DeclaredLength { get; }

    public ulong RsdtAddress { get; }

    public ulong XsdtAddress { get; }

    public static AcpiRsdpSnapshotResult Success(
        ulong physicalAddress,
        int bytesRead,
        bool extendedReadAttempted,
        AcpiRsdpValidationResult validation,
        ulong rsdtAddress,
        ulong xsdtAddress)
    {
        return new AcpiRsdpSnapshotResult(
            AcpiRsdpSnapshotStatus.Success,
            physicalAddress,
            bytesRead,
            validation.RequiredLength > 0 ? ToInt32Length(validation.RequiredLength) : bytesRead,
            extendedReadAttempted,
            PlatformPhysicalMemoryStatus.Success,
            validation.Status,
            validation.RevisionKind,
            validation.Revision,
            validation.Length,
            rsdtAddress,
            xsdtAddress);
    }

    public static AcpiRsdpSnapshotResult InvalidPhysicalAddress()
    {
        return Fail(AcpiRsdpSnapshotStatus.InvalidPhysicalAddress);
    }

    public static AcpiRsdpSnapshotResult PhysicalMemoryUnavailable(
        ulong physicalAddress)
    {
        return new AcpiRsdpSnapshotResult(
            AcpiRsdpSnapshotStatus.PhysicalMemoryUnavailable,
            physicalAddress,
            0,
            0,
            extendedReadAttempted: false,
            PlatformPhysicalMemoryStatus.SourceUnavailable,
            AcpiRsdpValidationStatus.Success,
            AcpiRsdpRevisionKind.None,
            revision: 0,
            declaredLength: 0,
            rsdtAddress: 0,
            xsdtAddress: 0);
    }

    public static AcpiRsdpSnapshotResult ScratchTooSmall(
        ulong physicalAddress,
        int requiredBytes)
    {
        return new AcpiRsdpSnapshotResult(
            AcpiRsdpSnapshotStatus.ScratchTooSmall,
            physicalAddress,
            0,
            requiredBytes,
            extendedReadAttempted: false,
            PlatformPhysicalMemoryStatus.Unknown,
            AcpiRsdpValidationStatus.Success,
            AcpiRsdpRevisionKind.None,
            revision: 0,
            declaredLength: 0,
            rsdtAddress: 0,
            xsdtAddress: 0);
    }

    public static AcpiRsdpSnapshotResult PhysicalReadFailed(
        ulong physicalAddress,
        PlatformPhysicalMemoryResult readResult,
        bool extendedReadAttempted)
    {
        AcpiRsdpSnapshotStatus status = extendedReadAttempted
            ? AcpiRsdpSnapshotStatus.ExtendedReadFailed
            : AcpiRsdpSnapshotStatus.PhysicalReadFailed;

        return new AcpiRsdpSnapshotResult(
            status,
            physicalAddress,
            readResult.BytesRead,
            readResult.RequiredBytes,
            extendedReadAttempted,
            readResult.Status,
            AcpiRsdpValidationStatus.Success,
            AcpiRsdpRevisionKind.None,
            revision: 0,
            declaredLength: 0,
            rsdtAddress: 0,
            xsdtAddress: 0);
    }

    public static AcpiRsdpSnapshotResult ValidationFailed(
        ulong physicalAddress,
        AcpiRsdpValidationResult validation,
        bool extendedReadAttempted)
    {
        AcpiRsdpSnapshotStatus status = extendedReadAttempted
            ? AcpiRsdpSnapshotStatus.ExtendedValidationFailed
            : AcpiRsdpSnapshotStatus.BaseValidationFailed;

        return new AcpiRsdpSnapshotResult(
            status,
            physicalAddress,
            0,
            ToInt32Length(validation.RequiredLength),
            extendedReadAttempted,
            PlatformPhysicalMemoryStatus.Success,
            validation.Status,
            validation.RevisionKind,
            validation.Revision,
            validation.Length,
            rsdtAddress: 0,
            xsdtAddress: 0);
    }

    private static AcpiRsdpSnapshotResult Fail(AcpiRsdpSnapshotStatus status)
    {
        return new AcpiRsdpSnapshotResult(
            status,
            0,
            0,
            0,
            extendedReadAttempted: false,
            PlatformPhysicalMemoryStatus.Unknown,
            AcpiRsdpValidationStatus.Success,
            AcpiRsdpRevisionKind.None,
            revision: 0,
            declaredLength: 0,
            rsdtAddress: 0,
            xsdtAddress: 0);
    }

    private static int ToInt32Length(uint length)
    {
        return length > int.MaxValue ? int.MaxValue : (int)length;
    }
}

public static class AcpiRsdpSnapshotReader
{
    public static AcpiRsdpSnapshotResult ReadAndValidate(
        IPlatformPhysicalMemory physicalMemory,
        ulong physicalAddress,
        Span<byte> scratch)
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
            return AcpiRsdpSnapshotResult.ScratchTooSmall(
                physicalAddress,
                AcpiRsdpValidator.BaseLength);
        }

        PlatformPhysicalMemoryResult baseRead = physicalMemory.ReadBytes(
            physicalAddress,
            AcpiRsdpValidator.BaseLength,
            scratch);

        if (!baseRead.Succeeded)
        {
            return AcpiRsdpSnapshotResult.PhysicalReadFailed(
                physicalAddress,
                baseRead,
                extendedReadAttempted: false);
        }

        AcpiRsdpValidationResult baseValidation = AcpiRsdpValidator.Validate(
            scratch.Slice(0, AcpiRsdpValidator.BaseLength));

        if (baseValidation.Succeeded)
        {
            return AcpiRsdpSnapshotResult.Success(
                physicalAddress,
                AcpiRsdpValidator.BaseLength,
                extendedReadAttempted: false,
                baseValidation,
                ReadRsdtAddress(scratch),
                xsdtAddress: 0);
        }

        if (baseValidation.Status != AcpiRsdpValidationStatus.ExtendedHeaderTooShort)
        {
            return AcpiRsdpSnapshotResult.ValidationFailed(
                physicalAddress,
                baseValidation,
                extendedReadAttempted: false);
        }

        if (scratch.Length < AcpiRsdpValidator.ExtendedHeaderLength)
        {
            return AcpiRsdpSnapshotResult.ScratchTooSmall(
                physicalAddress,
                AcpiRsdpValidator.ExtendedHeaderLength);
        }

        PlatformPhysicalMemoryResult headerRead = physicalMemory.ReadBytes(
            physicalAddress,
            AcpiRsdpValidator.ExtendedHeaderLength,
            scratch);

        if (!headerRead.Succeeded)
        {
            return AcpiRsdpSnapshotResult.PhysicalReadFailed(
                physicalAddress,
                headerRead,
                extendedReadAttempted: true);
        }

        AcpiRsdpValidationResult headerValidation = AcpiRsdpValidator.Validate(
            scratch.Slice(0, AcpiRsdpValidator.ExtendedHeaderLength));

        if (headerValidation.Succeeded)
        {
            return AcpiRsdpSnapshotResult.Success(
                physicalAddress,
                AcpiRsdpValidator.ExtendedHeaderLength,
                extendedReadAttempted: true,
                headerValidation,
                ReadRsdtAddress(scratch),
                ReadXsdtAddress(scratch));
        }

        if (headerValidation.Status != AcpiRsdpValidationStatus.SourceTooShortForExtendedLength)
        {
            return AcpiRsdpSnapshotResult.ValidationFailed(
                physicalAddress,
                headerValidation,
                extendedReadAttempted: true);
        }

        if (headerValidation.RequiredLength > int.MaxValue)
        {
            return AcpiRsdpSnapshotResult.ScratchTooSmall(
                physicalAddress,
                int.MaxValue);
        }

        int requiredLength = (int)headerValidation.RequiredLength;
        if (scratch.Length < requiredLength)
        {
            return AcpiRsdpSnapshotResult.ScratchTooSmall(
                physicalAddress,
                requiredLength);
        }

        PlatformPhysicalMemoryResult extendedRead = physicalMemory.ReadBytes(
            physicalAddress,
            requiredLength,
            scratch);

        if (!extendedRead.Succeeded)
        {
            return AcpiRsdpSnapshotResult.PhysicalReadFailed(
                physicalAddress,
                extendedRead,
                extendedReadAttempted: true);
        }

        AcpiRsdpValidationResult extendedValidation = AcpiRsdpValidator.Validate(
            scratch.Slice(0, requiredLength));

        if (!extendedValidation.Succeeded)
        {
            return AcpiRsdpSnapshotResult.ValidationFailed(
                physicalAddress,
                extendedValidation,
                extendedReadAttempted: true);
        }

        return AcpiRsdpSnapshotResult.Success(
            physicalAddress,
            requiredLength,
            extendedReadAttempted: true,
            extendedValidation,
            ReadRsdtAddress(scratch),
            ReadXsdtAddress(scratch));
    }

    private static ulong ReadRsdtAddress(ReadOnlySpan<byte> source)
    {
        return EndianPrimitives.ReadUInt32LittleEndian(
            source.Slice(AcpiRsdpValidator.RsdtAddressOffset, sizeof(uint)));
    }

    private static ulong ReadXsdtAddress(ReadOnlySpan<byte> source)
    {
        return EndianPrimitives.ReadUInt64LittleEndian(
            source.Slice(AcpiRsdpValidator.XsdtAddressOffset, sizeof(ulong)));
    }
}
