using OS.Std.Binary;

namespace OS.Boot;

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
            return AcpiRsdpValidationResult.Fail(AcpiRsdpValidationStatus.SourceTooShort, BaseLength);
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
            return AcpiRsdpValidationResult.Success(AcpiRsdpRevisionKind.Acpi10, revision, BaseLength);
        }

        if (revision == 1)
        {
            return AcpiRsdpValidationResult.Fail(AcpiRsdpValidationStatus.UnsupportedRevision, ExtendedHeaderLength, revision);
        }

        if (source.Length < ExtendedHeaderLength)
        {
            return AcpiRsdpValidationResult.Fail(AcpiRsdpValidationStatus.ExtendedHeaderTooShort, ExtendedHeaderLength, revision);
        }

        uint declaredLength = EndianPrimitives.ReadUInt32LittleEndian(source.Slice(LengthOffset, sizeof(uint)));
        if (declaredLength < BaseLength)
        {
            return AcpiRsdpValidationResult.Fail(AcpiRsdpValidationStatus.ExtendedLengthSmallerThanBase, BaseLength, revision, declaredLength);
        }

        if (declaredLength < ExtendedHeaderLength)
        {
            return AcpiRsdpValidationResult.Fail(AcpiRsdpValidationStatus.ExtendedLengthSmallerThanHeader, ExtendedHeaderLength, revision, declaredLength);
        }

        if (source.Length < declaredLength)
        {
            return AcpiRsdpValidationResult.Fail(AcpiRsdpValidationStatus.SourceTooShortForExtendedLength, declaredLength, revision, declaredLength);
        }

        if (!IsChecksumValid(source.Slice(0, (int)declaredLength)))
        {
            return AcpiRsdpValidationResult.Fail(AcpiRsdpValidationStatus.InvalidExtendedChecksum, declaredLength, revision, declaredLength);
        }

        return AcpiRsdpValidationResult.Success(AcpiRsdpRevisionKind.Acpi20OrGreater, revision, declaredLength);
    }

    private static bool HasSignature(ReadOnlySpan<byte> source)
    {
        return source[0] == (byte)'R' && source[1] == (byte)'S' && source[2] == (byte)'D' && source[3] == (byte)' ' && source[4] == (byte)'P' && source[5] == (byte)'T' && source[6] == (byte)'R' && source[7] == (byte)' ';
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
