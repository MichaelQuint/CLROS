using OS.Std.Binary;

namespace OS.Boot;

public enum AcpiRsdpValidationStatus
{
    Success = 0,
    SourceTooShort = 1,
    InvalidSignature = 2,
    InvalidOemId = 3,
    InvalidBaseChecksum = 4,
    UnsupportedRevision = 5,
    ExtendedHeaderTooShort = 6,
    ExtendedLengthSmallerThanBase = 7,
    ExtendedLengthSmallerThanHeader = 8,
    SourceTooShortForExtendedLength = 9,
    InvalidExtendedChecksum = 10
}

public enum AcpiRsdpRevisionKind
{
    None = 0,
    Acpi10 = 1,
    Acpi20OrGreater = 2
}

public readonly struct AcpiRsdpValidationResult
{
    private AcpiRsdpValidationResult(
        bool succeeded,
        AcpiRsdpValidationStatus status,
        AcpiRsdpRevisionKind revisionKind,
        byte revision,
        uint length,
        uint requiredLength)
    {
        Succeeded = succeeded;
        Status = status;
        RevisionKind = revisionKind;
        Revision = revision;
        Length = length;
        RequiredLength = requiredLength;
    }

    public bool Succeeded { get; }

    public AcpiRsdpValidationStatus Status { get; }

    public AcpiRsdpRevisionKind RevisionKind { get; }

    public byte Revision { get; }

    public uint Length { get; }

    public uint RequiredLength { get; }

    public static AcpiRsdpValidationResult Success(
        AcpiRsdpRevisionKind revisionKind,
        byte revision,
        uint length)
    {
        return new AcpiRsdpValidationResult(
            true,
            AcpiRsdpValidationStatus.Success,
            revisionKind,
            revision,
            length,
            length);
    }

    public static AcpiRsdpValidationResult Fail(
        AcpiRsdpValidationStatus status,
        uint requiredLength = 0,
        byte revision = 0,
        uint length = 0)
    {
        return new AcpiRsdpValidationResult(
            false,
            status,
            AcpiRsdpRevisionKind.None,
            revision,
            length,
            requiredLength);
    }
}

public static class AcpiRsdpValidator
{
    public const int SignatureLength = 8;
    public const int BaseLength = 20;
    public const int ExtendedHeaderLength = 36;

    public const int ChecksumOffset = 8;
    public const int OemIdOffset = 9;
    public const int OemIdLength = 6;
    public const int RevisionOffset = 15;
    public const int RsdtAddressOffset = 16;
    public const int LengthOffset = 20;
    public const int XsdtAddressOffset = 24;
    public const int ExtendedChecksumOffset = 32;

    public static AcpiRsdpValidationResult Validate(ReadOnlySpan<byte> source)
    {
        if (source.Length < BaseLength)
        {
            return AcpiRsdpValidationResult.Fail(
                AcpiRsdpValidationStatus.SourceTooShort,
                BaseLength);
        }

        if (!HasSignature(source))
        {
            return AcpiRsdpValidationResult.Fail(AcpiRsdpValidationStatus.InvalidSignature);
        }

        if (HasZeroOemId(source))
        {
            return AcpiRsdpValidationResult.Fail(AcpiRsdpValidationStatus.InvalidOemId);
        }

        if (!IsChecksumValid(source.Slice(0, BaseLength)))
        {
            return AcpiRsdpValidationResult.Fail(AcpiRsdpValidationStatus.InvalidBaseChecksum);
        }

        byte revision = source[RevisionOffset];
        if (revision == 0)
        {
            return AcpiRsdpValidationResult.Success(
                AcpiRsdpRevisionKind.Acpi10,
                revision,
                BaseLength);
        }

        if (revision == 1)
        {
            return AcpiRsdpValidationResult.Fail(
                AcpiRsdpValidationStatus.UnsupportedRevision,
                ExtendedHeaderLength,
                revision);
        }

        if (source.Length < ExtendedHeaderLength)
        {
            return AcpiRsdpValidationResult.Fail(
                AcpiRsdpValidationStatus.ExtendedHeaderTooShort,
                ExtendedHeaderLength,
                revision);
        }

        uint declaredLength = EndianPrimitives.ReadUInt32LittleEndian(
            source.Slice(LengthOffset, sizeof(uint)));

        if (declaredLength < BaseLength)
        {
            return AcpiRsdpValidationResult.Fail(
                AcpiRsdpValidationStatus.ExtendedLengthSmallerThanBase,
                BaseLength,
                revision,
                declaredLength);
        }

        if (declaredLength < ExtendedHeaderLength)
        {
            return AcpiRsdpValidationResult.Fail(
                AcpiRsdpValidationStatus.ExtendedLengthSmallerThanHeader,
                ExtendedHeaderLength,
                revision,
                declaredLength);
        }

        if (source.Length < declaredLength)
        {
            return AcpiRsdpValidationResult.Fail(
                AcpiRsdpValidationStatus.SourceTooShortForExtendedLength,
                declaredLength,
                revision,
                declaredLength);
        }

        if (!IsChecksumValid(source.Slice(0, (int)declaredLength)))
        {
            return AcpiRsdpValidationResult.Fail(
                AcpiRsdpValidationStatus.InvalidExtendedChecksum,
                declaredLength,
                revision,
                declaredLength);
        }

        return AcpiRsdpValidationResult.Success(
            AcpiRsdpRevisionKind.Acpi20OrGreater,
            revision,
            declaredLength);
    }

    private static bool HasSignature(ReadOnlySpan<byte> source)
    {
        return source[0] == (byte)'R'
            && source[1] == (byte)'S'
            && source[2] == (byte)'D'
            && source[3] == (byte)' '
            && source[4] == (byte)'P'
            && source[5] == (byte)'T'
            && source[6] == (byte)'R'
            && source[7] == (byte)' ';
    }

    private static bool HasZeroOemId(ReadOnlySpan<byte> source)
    {
        for (int index = 0; index < OemIdLength; index++)
        {
            if (source[OemIdOffset + index] != 0)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsChecksumValid(ReadOnlySpan<byte> source)
    {
        uint sum = 0;

        for (int index = 0; index < source.Length; index++)
        {
            sum += source[index];
        }

        return (sum & 0xff) == 0;
    }
}
