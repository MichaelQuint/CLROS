using System.Runtime.CompilerServices;
using OS.Boot;
using OS.Std.Binary;

internal static class AcpiTableDirectoryTests
{
    private const ulong HpetAddress = 0x00000000FED00000;
    private const ulong McfgAddress = 0x00000000E0000000;
    private const ulong SecondMcfgAddress = 0x00000000E1000000;

    private static readonly uint HpetSignature = AcpiTableDirectory.CreateSignature(
        (byte)'H',
        (byte)'P',
        (byte)'E',
        (byte)'T');

    [HostTest("acpi.directory selects mcfg from xsdt pointer list", HostLane.Fast, 0)]
    private static bool TestSelectsMcfgFromXsdtPointerList()
    {
        byte[] xsdt = CreateXsdt(HpetAddress, McfgAddress);
        byte[] hpet = CreateSdt(HpetSignature, payloadLength: 8);
        byte[] mcfg = CreateMcfgTable(payloadLength: 24);
        ulong[] pointerScratch = new ulong[2];
        AcpiTableSnapshot[] destination = new AcpiTableSnapshot[1];
        AcpiTableSource[] sources =
        [
            new AcpiTableSource(HpetAddress, hpet),
            new AcpiTableSource(McfgAddress, mcfg)
        ];

        AcpiTableDirectoryResult result = AcpiTableDirectory.TrySelect(
            xsdt,
            pointerScratch,
            sources,
            AcpiTableDirectory.McfgSignature,
            destination);

        return HostCheck.Result(result.Succeeded
            && result.Status == AcpiTableDirectoryStatus.Success
            && result.SelectedCount == 1
            && result.PointerCount == 2
            && result.PointerIndex == 1
            && result.SourceIndex == 1
            && result.PhysicalAddress == McfgAddress
            && result.TableSignature == AcpiTableDirectory.McfgSignature
            && result.RequestedSignature == AcpiTableDirectory.McfgSignature
            && result.DeclaredLength == mcfg.Length
            && destination[0].PhysicalAddress == McfgAddress
            && destination[0].Signature == AcpiTableDirectory.McfgSignature
            && destination[0].Length == mcfg.Length
            && destination[0].PointerIndex == 1
            && destination[0].SourceIndex == 1);
    }

    [HostTest("acpi.directory selects first matching signature deterministically", HostLane.Fast, 1)]
    private static bool TestSelectsFirstMatchingSignatureDeterministically()
    {
        byte[] xsdt = CreateXsdt(McfgAddress, SecondMcfgAddress);
        byte[] firstMcfg = CreateMcfgTable(payloadLength: 16);
        byte[] secondMcfg = CreateMcfgTable(payloadLength: 32);
        ulong[] pointerScratch = new ulong[2];
        AcpiTableSnapshot[] destination = new AcpiTableSnapshot[1];
        AcpiTableSource[] sources =
        [
            new AcpiTableSource(SecondMcfgAddress, secondMcfg),
            new AcpiTableSource(McfgAddress, firstMcfg)
        ];

        AcpiTableDirectoryResult result = AcpiTableDirectory.TrySelect(
            xsdt,
            pointerScratch,
            sources,
            AcpiTableDirectory.McfgSignature,
            destination);

        return HostCheck.Result(result.Succeeded
            && result.PointerIndex == 0
            && result.SourceIndex == 1
            && result.PhysicalAddress == McfgAddress
            && result.DeclaredLength == firstMcfg.Length
            && destination[0].PhysicalAddress == McfgAddress
            && destination[0].Length == firstMcfg.Length);
    }

    [HostTest("acpi.directory rejects missing table source", HostLane.Fast, 2)]
    private static bool TestRejectsMissingTableSource()
    {
        byte[] xsdt = CreateXsdt(McfgAddress);
        ulong[] pointerScratch = new ulong[1];
        AcpiTableSnapshot[] destination = new AcpiTableSnapshot[1];
        AcpiTableSource[] sources =
        [
            new AcpiTableSource(HpetAddress, CreateSdt(HpetSignature, payloadLength: 8))
        ];

        AcpiTableDirectoryResult result = AcpiTableDirectory.TrySelect(
            xsdt,
            pointerScratch,
            sources,
            AcpiTableDirectory.McfgSignature,
            destination);

        return HostCheck.Result(IsFailure(
            result,
            AcpiTableDirectoryStatus.TableSourceMissing,
            pointerCount: 1,
            pointerIndex: 0,
            physicalAddress: McfgAddress));
    }

    [HostTest("acpi.directory rejects invalid table checksum", HostLane.Fast, 3)]
    private static bool TestRejectsInvalidTableChecksum()
    {
        byte[] xsdt = CreateXsdt(McfgAddress);
        byte[] mcfg = CreateMcfgTable(payloadLength: 16);
        mcfg[AcpiSdtParser.HeaderLength]++;
        ulong[] pointerScratch = new ulong[1];
        AcpiTableSnapshot[] destination = new AcpiTableSnapshot[1];
        AcpiTableSource[] sources =
        [
            new AcpiTableSource(McfgAddress, mcfg)
        ];

        AcpiTableDirectoryResult result = AcpiTableDirectory.TrySelect(
            xsdt,
            pointerScratch,
            sources,
            AcpiTableDirectory.McfgSignature,
            destination);

        return HostCheck.Result(IsFailure(
            result,
            AcpiTableDirectoryStatus.TableChecksumInvalid,
            pointerCount: 1,
            pointerIndex: 0,
            sourceIndex: 0,
            physicalAddress: McfgAddress,
            tableSignature: AcpiTableDirectory.McfgSignature,
            declaredLength: (uint)mcfg.Length,
            requiredLength: (uint)mcfg.Length));
    }

    [HostTest("acpi.directory reports destination capacity exhaustion", HostLane.Fast, 4)]
    private static bool TestReportsDestinationCapacityExhaustion()
    {
        byte[] xsdt = CreateXsdt(McfgAddress);
        byte[] mcfg = CreateMcfgTable(payloadLength: 16);
        ulong[] pointerScratch = new ulong[1];
        AcpiTableSource[] sources =
        [
            new AcpiTableSource(McfgAddress, mcfg)
        ];

        AcpiTableDirectoryResult result = AcpiTableDirectory.TrySelect(
            xsdt,
            pointerScratch,
            sources,
            AcpiTableDirectory.McfgSignature,
            Span<AcpiTableSnapshot>.Empty);

        return HostCheck.Result(IsFailure(
            result,
            AcpiTableDirectoryStatus.DestinationTooSmall,
            pointerCount: 1,
            pointerIndex: 0,
            sourceIndex: 0,
            physicalAddress: McfgAddress,
            requestedSignature: AcpiTableDirectory.McfgSignature,
            tableSignature: AcpiTableDirectory.McfgSignature,
            declaredLength: (uint)mcfg.Length,
            requiredLength: (uint)mcfg.Length,
            requiredDestinationCapacity: 1));
    }

    [HostTest("acpi.directory reports signature not found", HostLane.Fast, 5)]
    private static bool TestReportsSignatureNotFound()
    {
        byte[] xsdt = CreateXsdt(HpetAddress);
        ulong[] pointerScratch = new ulong[1];
        AcpiTableSnapshot[] destination = new AcpiTableSnapshot[1];
        AcpiTableSource[] sources =
        [
            new AcpiTableSource(HpetAddress, CreateSdt(HpetSignature, payloadLength: 8))
        ];

        AcpiTableDirectoryResult result = AcpiTableDirectory.TrySelect(
            xsdt,
            pointerScratch,
            sources,
            AcpiTableDirectory.McfgSignature,
            destination);

        return HostCheck.Result(IsFailure(
            result,
            AcpiTableDirectoryStatus.SignatureNotFound,
            pointerCount: 1,
            requestedSignature: AcpiTableDirectory.McfgSignature));
    }

    private static bool IsFailure(
        AcpiTableDirectoryResult result,
        AcpiTableDirectoryStatus status,
        int pointerCount = 0,
        int pointerIndex = -1,
        int sourceIndex = -1,
        ulong physicalAddress = 0,
        uint requestedSignature = 0,
        uint tableSignature = 0,
        uint declaredLength = 0,
        uint requiredLength = 0,
        int requiredDestinationCapacity = 0)
    {
        return !result.Succeeded
            && result.Status == status
            && result.SelectedCount == 0
            && result.PointerCount == pointerCount
            && result.PointerIndex == pointerIndex
            && result.SourceIndex == sourceIndex
            && result.PhysicalAddress == physicalAddress
            && result.RequestedSignature == requestedSignature
            && result.TableSignature == tableSignature
            && result.DeclaredLength == declaredLength
            && result.RequiredLength == requiredLength
            && result.RequiredDestinationCapacity == requiredDestinationCapacity;
    }

    private static byte[] CreateXsdt(params ulong[] pointers)
    {
        byte[] table = new byte[
            AcpiSdtParser.HeaderLength + (pointers.Length * AcpiSdtParser.XsdtPointerSize)];
        WriteSdtHeader(table, AcpiSdtParser.XsdtSignature);

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

    private static byte[] CreateMcfgTable(int payloadLength)
    {
        return CreateSdt(AcpiTableDirectory.McfgSignature, payloadLength);
    }

    private static byte[] CreateSdt(
        uint signature,
        int payloadLength)
    {
        byte[] table = new byte[AcpiSdtParser.HeaderLength + payloadLength];
        WriteSdtHeader(table, signature);
        for (int index = 0; index < payloadLength; index++)
        {
            table[AcpiSdtParser.HeaderLength + index] = (byte)(index + 1);
        }

        SetChecksum(table);
        return table;
    }

    private static void WriteSdtHeader(
        byte[] table,
        uint signature)
    {
        EndianPrimitives.WriteUInt32LittleEndian(
            table.AsSpan(AcpiSdtParser.SignatureOffset, sizeof(uint)),
            signature);
        EndianPrimitives.WriteUInt32LittleEndian(
            table.AsSpan(AcpiSdtParser.LengthOffset, sizeof(uint)),
            (uint)table.Length);
        table[AcpiSdtParser.RevisionOffset] = 1;
        WriteAscii(table, AcpiSdtParser.OemIdOffset, "OSTEST");
        WriteAscii(table, AcpiSdtParser.OemTableIdOffset, "DIRTEST ");
        EndianPrimitives.WriteUInt32LittleEndian(
            table.AsSpan(AcpiSdtParser.OemRevisionOffset, sizeof(uint)),
            1);
        WriteAscii(table, AcpiSdtParser.CreatorIdOffset, "OS  ");
        EndianPrimitives.WriteUInt32LittleEndian(
            table.AsSpan(AcpiSdtParser.CreatorRevisionOffset, sizeof(uint)),
            1);
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
