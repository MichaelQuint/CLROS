using System.Runtime.CompilerServices;
using OS.Boot;
using OS.Std.Binary;

internal static class AcpiRsdpValidatorTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("acpi.rsdp validates acpi1 checksum", TestValidAcpi10, ref failures);
        Run("acpi.rsdp validates acpi2 extended checksum", TestValidAcpi20, ref failures);
        Run("acpi.rsdp rejects signature metadata and revision", TestRejectsSignatureMetadataAndRevision, ref failures);
        Run("acpi.rsdp rejects checksum failures", TestRejectsChecksumFailures, ref failures);
        Run("acpi.rsdp rejects extended length failures", TestRejectsExtendedLengthFailures, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("ACPI RSDP validator host tests failed: " + failures);
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

    private static bool TestValidAcpi10()
    {
        byte[] rsdp = CreateValidAcpi10();

        AcpiRsdpValidationResult result = AcpiRsdpValidator.Validate(rsdp);

        return result.Succeeded
            && result.Status == AcpiRsdpValidationStatus.Success
            && result.RevisionKind == AcpiRsdpRevisionKind.Acpi10
            && result.Revision == 0
            && result.Length == AcpiRsdpValidator.BaseLength
            && result.RequiredLength == AcpiRsdpValidator.BaseLength;
    }

    private static bool TestValidAcpi20()
    {
        byte[] rsdp = CreateValidAcpi20(length: 40);

        AcpiRsdpValidationResult result = AcpiRsdpValidator.Validate(rsdp);

        return result.Succeeded
            && result.Status == AcpiRsdpValidationStatus.Success
            && result.RevisionKind == AcpiRsdpRevisionKind.Acpi20OrGreater
            && result.Revision == 2
            && result.Length == 40
            && result.RequiredLength == 40;
    }

    private static bool TestRejectsSignatureMetadataAndRevision()
    {
        byte[] badSignature = CreateValidAcpi10();
        badSignature[0] = (byte)'X';

        byte[] zeroOem = CreateValidAcpi10();
        zeroOem.AsSpan(AcpiRsdpValidator.OemIdOffset, AcpiRsdpValidator.OemIdLength).Clear();
        SetBaseChecksum(zeroOem);

        byte[] unsupportedRevision = CreateValidAcpi10();
        unsupportedRevision[AcpiRsdpValidator.RevisionOffset] = 1;
        SetBaseChecksum(unsupportedRevision);

        AcpiRsdpValidationResult tooShort = AcpiRsdpValidator.Validate(
            new byte[AcpiRsdpValidator.BaseLength - 1]);

        AcpiRsdpValidationResult badSignatureResult = AcpiRsdpValidator.Validate(badSignature);
        AcpiRsdpValidationResult zeroOemResult = AcpiRsdpValidator.Validate(zeroOem);
        AcpiRsdpValidationResult unsupportedRevisionResult = AcpiRsdpValidator.Validate(unsupportedRevision);

        return IsFailure(tooShort, AcpiRsdpValidationStatus.SourceTooShort, AcpiRsdpValidator.BaseLength)
            && IsFailure(badSignatureResult, AcpiRsdpValidationStatus.InvalidSignature)
            && IsFailure(zeroOemResult, AcpiRsdpValidationStatus.InvalidOemId)
            && IsFailure(
                unsupportedRevisionResult,
                AcpiRsdpValidationStatus.UnsupportedRevision,
                AcpiRsdpValidator.ExtendedHeaderLength,
                revision: 1);
    }

    private static bool TestRejectsChecksumFailures()
    {
        byte[] badBaseChecksum = CreateValidAcpi10();
        badBaseChecksum[AcpiRsdpValidator.RsdtAddressOffset]++;

        byte[] badExtendedChecksum = CreateValidAcpi20(length: AcpiRsdpValidator.ExtendedHeaderLength);
        badExtendedChecksum[AcpiRsdpValidator.XsdtAddressOffset]++;

        AcpiRsdpValidationResult baseResult = AcpiRsdpValidator.Validate(badBaseChecksum);
        AcpiRsdpValidationResult extendedResult = AcpiRsdpValidator.Validate(badExtendedChecksum);

        return IsFailure(baseResult, AcpiRsdpValidationStatus.InvalidBaseChecksum)
            && IsFailure(
                extendedResult,
                AcpiRsdpValidationStatus.InvalidExtendedChecksum,
                AcpiRsdpValidator.ExtendedHeaderLength,
                revision: 2,
                length: AcpiRsdpValidator.ExtendedHeaderLength);
    }

    private static bool TestRejectsExtendedLengthFailures()
    {
        byte[] missingExtendedHeader = CreateValidAcpi10();
        missingExtendedHeader[AcpiRsdpValidator.RevisionOffset] = 2;
        SetBaseChecksum(missingExtendedHeader);

        byte[] smallerThanBase = CreateValidAcpi20(length: AcpiRsdpValidator.ExtendedHeaderLength);
        EndianPrimitives.WriteUInt32LittleEndian(
            smallerThanBase.AsSpan(AcpiRsdpValidator.LengthOffset, sizeof(uint)),
            AcpiRsdpValidator.BaseLength - 1);

        byte[] smallerThanHeader = CreateValidAcpi20(length: AcpiRsdpValidator.ExtendedHeaderLength);
        EndianPrimitives.WriteUInt32LittleEndian(
            smallerThanHeader.AsSpan(AcpiRsdpValidator.LengthOffset, sizeof(uint)),
            AcpiRsdpValidator.ExtendedHeaderLength - 1);

        byte[] sourceTooShort = CreateValidAcpi20(length: AcpiRsdpValidator.ExtendedHeaderLength);
        EndianPrimitives.WriteUInt32LittleEndian(
            sourceTooShort.AsSpan(AcpiRsdpValidator.LengthOffset, sizeof(uint)),
            AcpiRsdpValidator.ExtendedHeaderLength + 4);

        AcpiRsdpValidationResult missingHeaderResult = AcpiRsdpValidator.Validate(missingExtendedHeader);
        AcpiRsdpValidationResult smallerThanBaseResult = AcpiRsdpValidator.Validate(smallerThanBase);
        AcpiRsdpValidationResult smallerThanHeaderResult = AcpiRsdpValidator.Validate(smallerThanHeader);
        AcpiRsdpValidationResult sourceTooShortResult = AcpiRsdpValidator.Validate(sourceTooShort);

        return IsFailure(
                missingHeaderResult,
                AcpiRsdpValidationStatus.ExtendedHeaderTooShort,
                AcpiRsdpValidator.ExtendedHeaderLength,
                revision: 2)
            && IsFailure(
                smallerThanBaseResult,
                AcpiRsdpValidationStatus.ExtendedLengthSmallerThanBase,
                AcpiRsdpValidator.BaseLength,
                revision: 2,
                length: AcpiRsdpValidator.BaseLength - 1)
            && IsFailure(
                smallerThanHeaderResult,
                AcpiRsdpValidationStatus.ExtendedLengthSmallerThanHeader,
                AcpiRsdpValidator.ExtendedHeaderLength,
                revision: 2,
                length: AcpiRsdpValidator.ExtendedHeaderLength - 1)
            && IsFailure(
                sourceTooShortResult,
                AcpiRsdpValidationStatus.SourceTooShortForExtendedLength,
                AcpiRsdpValidator.ExtendedHeaderLength + 4,
                revision: 2,
                length: AcpiRsdpValidator.ExtendedHeaderLength + 4);
    }

    private static bool IsFailure(
        AcpiRsdpValidationResult result,
        AcpiRsdpValidationStatus status,
        uint requiredLength = 0,
        byte revision = 0,
        uint length = 0)
    {
        return !result.Succeeded
            && result.Status == status
            && result.RevisionKind == AcpiRsdpRevisionKind.None
            && result.RequiredLength == requiredLength
            && result.Revision == revision
            && result.Length == length;
    }

    private static byte[] CreateValidAcpi10()
    {
        byte[] rsdp = new byte[AcpiRsdpValidator.BaseLength];
        WriteBaseFields(rsdp, revision: 0);
        SetBaseChecksum(rsdp);
        return rsdp;
    }

    private static byte[] CreateValidAcpi20(int length)
    {
        byte[] rsdp = new byte[length];
        WriteBaseFields(rsdp, revision: 2);
        EndianPrimitives.WriteUInt32LittleEndian(
            rsdp.AsSpan(AcpiRsdpValidator.LengthOffset, sizeof(uint)),
            (uint)length);
        EndianPrimitives.WriteUInt64LittleEndian(
            rsdp.AsSpan(AcpiRsdpValidator.XsdtAddressOffset, sizeof(ulong)),
            0x0000000000300000);

        for (int index = AcpiRsdpValidator.ExtendedHeaderLength; index < rsdp.Length; index++)
        {
            rsdp[index] = (byte)(index + 1);
        }

        SetBaseChecksum(rsdp);
        SetExtendedChecksum(rsdp, length);
        return rsdp;
    }

    private static void WriteBaseFields(byte[] rsdp, byte revision)
    {
        rsdp[0] = (byte)'R';
        rsdp[1] = (byte)'S';
        rsdp[2] = (byte)'D';
        rsdp[3] = (byte)' ';
        rsdp[4] = (byte)'P';
        rsdp[5] = (byte)'T';
        rsdp[6] = (byte)'R';
        rsdp[7] = (byte)' ';
        rsdp[AcpiRsdpValidator.OemIdOffset] = (byte)'O';
        rsdp[AcpiRsdpValidator.OemIdOffset + 1] = (byte)'S';
        rsdp[AcpiRsdpValidator.OemIdOffset + 2] = (byte)'T';
        rsdp[AcpiRsdpValidator.OemIdOffset + 3] = (byte)'E';
        rsdp[AcpiRsdpValidator.OemIdOffset + 4] = (byte)'S';
        rsdp[AcpiRsdpValidator.OemIdOffset + 5] = (byte)'T';
        rsdp[AcpiRsdpValidator.RevisionOffset] = revision;
        EndianPrimitives.WriteUInt32LittleEndian(
            rsdp.AsSpan(AcpiRsdpValidator.RsdtAddressOffset, sizeof(uint)),
            0x00100000);
    }

    private static void SetBaseChecksum(byte[] rsdp)
    {
        rsdp[AcpiRsdpValidator.ChecksumOffset] = 0;
        rsdp[AcpiRsdpValidator.ChecksumOffset] = CreateChecksum(
            rsdp.AsSpan(0, AcpiRsdpValidator.BaseLength));
    }

    private static void SetExtendedChecksum(byte[] rsdp, int length)
    {
        rsdp[AcpiRsdpValidator.ExtendedChecksumOffset] = 0;
        rsdp[AcpiRsdpValidator.ExtendedChecksumOffset] = CreateChecksum(
            rsdp.AsSpan(0, length));
    }

    private static byte CreateChecksum(ReadOnlySpan<byte> source)
    {
        uint sum = 0;
        for (int index = 0; index < source.Length; index++)
        {
            sum += source[index];
        }

        return unchecked((byte)(0 - sum));
    }
}
