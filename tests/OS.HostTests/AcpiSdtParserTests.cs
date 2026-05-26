using System.Runtime.CompilerServices;
using OS.Boot;
using OS.Std.Binary;

internal static class AcpiSdtParserTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("acpi.sdt parses valid rsdt", TestParsesValidRsdt, ref failures);
        Run("acpi.sdt parses valid xsdt", TestParsesValidXsdt, ref failures);
        Run("acpi.sdt rejects checksum failure", TestRejectsChecksumFailure, ref failures);
        Run("acpi.sdt rejects declared source length mismatch", TestRejectsDeclaredSourceLengthMismatch, ref failures);
        Run("acpi.sdt rejects invalid pointer array length", TestRejectsInvalidPointerArrayLength, ref failures);
        Run("acpi.sdt rejects zero table pointer", TestRejectsZeroTablePointer, ref failures);
        Run("acpi.sdt rejects destination capacity exhaustion", TestRejectsDestinationCapacityExhaustion, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("ACPI SDT parser host tests failed: " + failures);
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

    private static bool TestParsesValidRsdt()
    {
        byte[] rsdt = CreateRsdt(0x00100000, 0x00102000);
        ulong[] pointers = new ulong[2];

        AcpiSdtParseResult result = AcpiSdtParser.TryParsePointerList(rsdt, pointers);

        return result.Succeeded
            && result.Status == AcpiSdtParseStatus.Success
            && result.TableKind == AcpiSdtTableKind.Rsdt
            && result.PointerCount == 2
            && result.DeclaredLength == rsdt.Length
            && result.RequiredLength == rsdt.Length
            && result.RequiredPointerCapacity == 2
            && HeaderMatches(
                result.Header,
                AcpiSdtParser.RsdtSignature,
                (uint)rsdt.Length,
                rsdt[AcpiSdtParser.ChecksumOffset])
            && pointers[0] == 0x00100000
            && pointers[1] == 0x00102000;
    }

    private static bool TestParsesValidXsdt()
    {
        byte[] xsdt = CreateXsdt(0x0000000100000000, 0x00000000E0000000);
        ulong[] pointers = new ulong[2];

        AcpiSdtParseResult result = AcpiSdtParser.TryParsePointerList(xsdt, pointers);

        return result.Succeeded
            && result.Status == AcpiSdtParseStatus.Success
            && result.TableKind == AcpiSdtTableKind.Xsdt
            && result.PointerCount == 2
            && result.DeclaredLength == xsdt.Length
            && result.RequiredLength == xsdt.Length
            && result.RequiredPointerCapacity == 2
            && HeaderMatches(
                result.Header,
                AcpiSdtParser.XsdtSignature,
                (uint)xsdt.Length,
                xsdt[AcpiSdtParser.ChecksumOffset])
            && pointers[0] == 0x0000000100000000
            && pointers[1] == 0x00000000E0000000;
    }

    private static bool TestRejectsChecksumFailure()
    {
        byte[] rsdt = CreateRsdt(0x00100000);
        rsdt[AcpiSdtParser.HeaderLength]++;

        AcpiSdtParseResult result = AcpiSdtParser.TryParsePointerList(
            rsdt,
            new ulong[1]);

        return IsFailure(
                result,
                AcpiSdtParseStatus.InvalidChecksum,
                declaredLength: (uint)rsdt.Length,
                requiredLength: (uint)rsdt.Length)
            && result.TableKind == AcpiSdtTableKind.Rsdt;
    }

    private static bool TestRejectsDeclaredSourceLengthMismatch()
    {
        byte[] tooShort = new byte[AcpiSdtParser.HeaderLength - 1];

        byte[] declaredTooSmall = CreateRsdt(0x00100000);
        WriteLength(declaredTooSmall, AcpiSdtParser.HeaderLength - 1);

        byte[] sourceTooShort = CreateRsdt(0x00100000, 0x00102000);
        uint declaredLength = (uint)(sourceTooShort.Length + AcpiSdtParser.RsdtPointerSize);
        WriteLength(sourceTooShort, declaredLength);

        AcpiSdtParseResult tooShortResult = AcpiSdtParser.TryParsePointerList(
            tooShort,
            new ulong[1]);
        AcpiSdtParseResult declaredTooSmallResult = AcpiSdtParser.TryParsePointerList(
            declaredTooSmall,
            new ulong[1]);
        AcpiSdtParseResult sourceTooShortResult = AcpiSdtParser.TryParsePointerList(
            sourceTooShort,
            new ulong[2]);

        return IsFailure(
                tooShortResult,
                AcpiSdtParseStatus.SourceTooShort,
                requiredLength: AcpiSdtParser.HeaderLength)
            && IsFailure(
                declaredTooSmallResult,
                AcpiSdtParseStatus.DeclaredLengthTooSmall,
                declaredLength: AcpiSdtParser.HeaderLength - 1,
                requiredLength: AcpiSdtParser.HeaderLength)
            && IsFailure(
                sourceTooShortResult,
                AcpiSdtParseStatus.SourceTooShortForDeclaredLength,
                declaredLength: declaredLength,
                requiredLength: declaredLength);
    }

    private static bool TestRejectsInvalidPointerArrayLength()
    {
        byte[] rsdt = CreateTableWithPayload(
            AcpiSdtParser.RsdtSignature,
            new byte[AcpiSdtParser.RsdtPointerSize - 1]);

        byte[] xsdt = CreateTableWithPayload(
            AcpiSdtParser.XsdtSignature,
            new byte[AcpiSdtParser.XsdtPointerSize + 4]);
        EndianPrimitives.WriteUInt64LittleEndian(
            xsdt.AsSpan(AcpiSdtParser.HeaderLength, sizeof(ulong)),
            0x0000000100000000);
        SetChecksum(xsdt);

        AcpiSdtParseResult rsdtResult = AcpiSdtParser.TryParsePointerList(
            rsdt,
            new ulong[1]);
        AcpiSdtParseResult xsdtResult = AcpiSdtParser.TryParsePointerList(
            xsdt,
            new ulong[2]);

        return IsFailure(
                rsdtResult,
                AcpiSdtParseStatus.InvalidPointerArrayLength,
                declaredLength: (uint)rsdt.Length,
                requiredLength: AcpiSdtParser.HeaderLength + AcpiSdtParser.RsdtPointerSize,
                entryIndex: 0)
            && IsFailure(
                xsdtResult,
                AcpiSdtParseStatus.InvalidPointerArrayLength,
                declaredLength: (uint)xsdt.Length,
                requiredLength: AcpiSdtParser.HeaderLength + (2 * AcpiSdtParser.XsdtPointerSize),
                entryIndex: 1);
    }

    private static bool TestRejectsZeroTablePointer()
    {
        byte[] rsdt = CreateRsdt(0);
        byte[] xsdt = CreateXsdt(0x0000000100000000, 0);

        AcpiSdtParseResult rsdtResult = AcpiSdtParser.TryParsePointerList(
            rsdt,
            new ulong[1]);
        AcpiSdtParseResult xsdtResult = AcpiSdtParser.TryParsePointerList(
            xsdt,
            new ulong[2]);

        return IsFailure(
                rsdtResult,
                AcpiSdtParseStatus.ZeroTablePointer,
                declaredLength: (uint)rsdt.Length,
                requiredLength: (uint)rsdt.Length,
                entryIndex: 0)
            && IsFailure(
                xsdtResult,
                AcpiSdtParseStatus.ZeroTablePointer,
                declaredLength: (uint)xsdt.Length,
                requiredLength: (uint)xsdt.Length,
                entryIndex: 1);
    }

    private static bool TestRejectsDestinationCapacityExhaustion()
    {
        byte[] xsdt = CreateXsdt(0x0000000100000000, 0x00000000E0000000);
        ulong[] pointers = new ulong[1];

        AcpiSdtParseResult result = AcpiSdtParser.TryParsePointerList(xsdt, pointers);

        return IsFailure(
            result,
            AcpiSdtParseStatus.DestinationTooSmall,
            declaredLength: (uint)xsdt.Length,
            requiredLength: (uint)xsdt.Length,
            requiredPointerCapacity: 2);
    }

    private static bool HeaderMatches(
        AcpiSdtHeader header,
        uint signature,
        uint length,
        byte checksum)
    {
        return header.Signature == signature
            && header.Length == length
            && header.Revision == 1
            && header.Checksum == checksum
            && header.OemId == PackAscii("OSTEST")
            && header.OemTableId == PackAscii("SDTTEST ")
            && header.OemRevision == 2
            && header.CreatorId == PackAscii("OS  ")
            && header.CreatorRevision == 3;
    }

    private static bool IsFailure(
        AcpiSdtParseResult result,
        AcpiSdtParseStatus status,
        uint declaredLength = 0,
        uint requiredLength = 0,
        int requiredPointerCapacity = 0,
        int entryIndex = -1)
    {
        return !result.Succeeded
            && result.Status == status
            && result.PointerCount == 0
            && result.DeclaredLength == declaredLength
            && result.RequiredLength == requiredLength
            && result.RequiredPointerCapacity == requiredPointerCapacity
            && result.EntryIndex == entryIndex;
    }

    private static byte[] CreateRsdt(params uint[] pointers)
    {
        byte[] table = new byte[
            AcpiSdtParser.HeaderLength + (pointers.Length * AcpiSdtParser.RsdtPointerSize)];
        WriteHeader(table, AcpiSdtParser.RsdtSignature);

        for (int index = 0; index < pointers.Length; index++)
        {
            EndianPrimitives.WriteUInt32LittleEndian(
                table.AsSpan(
                    AcpiSdtParser.HeaderLength + (index * AcpiSdtParser.RsdtPointerSize),
                    sizeof(uint)),
                pointers[index]);
        }

        SetChecksum(table);
        return table;
    }

    private static byte[] CreateXsdt(params ulong[] pointers)
    {
        byte[] table = new byte[
            AcpiSdtParser.HeaderLength + (pointers.Length * AcpiSdtParser.XsdtPointerSize)];
        WriteHeader(table, AcpiSdtParser.XsdtSignature);

        for (int index = 0; index < pointers.Length; index++)
        {
            EndianPrimitives.WriteUInt64LittleEndian(
                table.AsSpan(
                    AcpiSdtParser.HeaderLength + (index * AcpiSdtParser.XsdtPointerSize),
                    sizeof(ulong)),
                pointers[index]);
        }

        SetChecksum(table);
        return table;
    }

    private static byte[] CreateTableWithPayload(
        uint signature,
        byte[] payload)
    {
        byte[] table = new byte[AcpiSdtParser.HeaderLength + payload.Length];
        WriteHeader(table, signature);
        payload.CopyTo(table.AsSpan(AcpiSdtParser.HeaderLength));
        SetChecksum(table);
        return table;
    }

    private static void WriteHeader(byte[] table, uint signature)
    {
        EndianPrimitives.WriteUInt32LittleEndian(
            table.AsSpan(AcpiSdtParser.SignatureOffset, sizeof(uint)),
            signature);
        WriteLength(table, (uint)table.Length);
        table[AcpiSdtParser.RevisionOffset] = 1;
        WriteAscii(table, AcpiSdtParser.OemIdOffset, "OSTEST");
        WriteAscii(table, AcpiSdtParser.OemTableIdOffset, "SDTTEST ");
        EndianPrimitives.WriteUInt32LittleEndian(
            table.AsSpan(AcpiSdtParser.OemRevisionOffset, sizeof(uint)),
            2);
        WriteAscii(table, AcpiSdtParser.CreatorIdOffset, "OS  ");
        EndianPrimitives.WriteUInt32LittleEndian(
            table.AsSpan(AcpiSdtParser.CreatorRevisionOffset, sizeof(uint)),
            3);
    }

    private static void WriteLength(byte[] table, uint length)
    {
        EndianPrimitives.WriteUInt32LittleEndian(
            table.AsSpan(AcpiSdtParser.LengthOffset, sizeof(uint)),
            length);
    }

    private static void WriteAscii(
        byte[] destination,
        int offset,
        string value)
    {
        for (int index = 0; index < value.Length; index++)
        {
            destination[offset + index] = (byte)value[index];
        }
    }

    private static ulong PackAscii(string value)
    {
        ulong result = 0;
        for (int index = 0; index < value.Length; index++)
        {
            result |= (ulong)(byte)value[index] << (index * 8);
        }

        return result;
    }

    private static void SetChecksum(byte[] table)
    {
        table[AcpiSdtParser.ChecksumOffset] = 0;
        uint sum = 0;
        for (int index = 0; index < table.Length; index++)
        {
            sum += table[index];
        }

        table[AcpiSdtParser.ChecksumOffset] = unchecked((byte)(0 - sum));
    }
}
