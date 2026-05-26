using System.Runtime.CompilerServices;
using OS.Boot;
using OS.Drivers;
using OS.Std.Binary;

internal static class AcpiMcfgDiscoveryTests
{
    private const ulong HpetAddress = 0x00000000FED00000;
    private const ulong McfgAddress = 0x00000000E0000000;

    private static readonly uint HpetSignature = AcpiTableDirectory.CreateSignature(
        (byte)'H',
        (byte)'P',
        (byte)'E',
        (byte)'T');

    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("acpi.mcfg.discovery selects and parses mcfg", TestSelectsAndParsesMcfg, ref failures);
        Run("acpi.mcfg.discovery preserves selected physical address", TestPreservesSelectedPhysicalAddress, ref failures);
        Run("acpi.mcfg.discovery reports directory miss", TestReportsDirectoryMiss, ref failures);
        Run("acpi.mcfg.discovery reports mcfg parse failure", TestReportsMcfgParseFailure, ref failures);
        Run("acpi.mcfg.discovery reports segment destination exhaustion", TestReportsSegmentDestinationExhaustion, ref failures);
        Run("acpi.mcfg.discovery supports direct pointer list input", TestSupportsDirectPointerListInput, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("ACPI MCFG discovery host tests failed: " + failures);
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

    private static bool TestSelectsAndParsesMcfg()
    {
        byte[] xsdt = CreateXsdt(HpetAddress, McfgAddress);
        byte[] hpet = CreateSdt(HpetSignature, payloadLength: 8);
        byte[] mcfg = CreateMcfg(
            new McfgEntrySpec(
                basePhysicalAddress: 0x00000000E0000000,
                segmentGroup: 0,
                startBus: 0,
                endBus: 0x3F),
            new McfgEntrySpec(
                basePhysicalAddress: 0x00000000E4000000,
                segmentGroup: 1,
                startBus: 0x40,
                endBus: 0x7F));
        AcpiTableSource[] sources =
        [
            new AcpiTableSource(HpetAddress, hpet),
            new AcpiTableSource(McfgAddress, mcfg)
        ];
        ulong[] pointerScratch = new ulong[2];
        PciEcamSegment[] segments = new PciEcamSegment[2];

        AcpiMcfgDiscoveryResult result = AcpiMcfgDiscovery.TryDiscover(
            xsdt,
            pointerScratch,
            sources,
            segments);

        return result.Succeeded
            && result.Status == AcpiMcfgDiscoveryStatus.Success
            && result.DirectoryStatus == AcpiTableDirectoryStatus.Success
            && result.McfgParseStatus == AcpiMcfgParseStatus.Success
            && result.McfgParseAttempted
            && result.SegmentCount == 2
            && result.RequiredSegmentCapacity == 2
            && result.PointerCount == 2
            && result.SelectedPointerIndex == 1
            && result.SelectedSourceIndex == 1
            && result.SelectedPhysicalAddress == McfgAddress
            && segments[0].BasePhysicalAddress == 0x00000000E0000000
            && segments[0].SegmentGroup == 0
            && segments[0].StartBus == 0
            && segments[0].EndBus == 0x3F
            && segments[1].BasePhysicalAddress == 0x00000000E4000000
            && segments[1].SegmentGroup == 1
            && segments[1].StartBus == 0x40
            && segments[1].EndBus == 0x7F;
    }

    private static bool TestPreservesSelectedPhysicalAddress()
    {
        byte[] xsdt = CreateXsdt(HpetAddress, McfgAddress);
        byte[] hpet = CreateSdt(HpetSignature, payloadLength: 8);
        byte[] mcfg = CreateMcfg(
            new McfgEntrySpec(
                basePhysicalAddress: 0x00000000E0000000,
                segmentGroup: 2,
                startBus: 0x20,
                endBus: 0x2F));
        AcpiTableSource[] sources =
        [
            new AcpiTableSource(HpetAddress, hpet),
            new AcpiTableSource(McfgAddress, mcfg)
        ];
        ulong[] pointerScratch = new ulong[2];
        PciEcamSegment[] segments = new PciEcamSegment[1];

        AcpiMcfgDiscoveryResult result = AcpiMcfgDiscovery.TryDiscover(
            xsdt,
            pointerScratch,
            sources,
            segments);

        return result.Succeeded
            && result.SelectedPhysicalAddress == McfgAddress
            && result.SelectedPointerIndex == 1
            && result.SelectedSourceIndex == 1
            && result.SelectedTableLength == mcfg.Length
            && result.DirectoryResult.PhysicalAddress == McfgAddress
            && result.DirectoryResult.PointerIndex == 1
            && result.DirectoryResult.SourceIndex == 1
            && result.McfgParseResult.DeclaredLength == mcfg.Length;
    }

    private static bool TestReportsDirectoryMiss()
    {
        byte[] xsdt = CreateXsdt(HpetAddress);
        AcpiTableSource[] sources =
        [
            new AcpiTableSource(HpetAddress, CreateSdt(HpetSignature, payloadLength: 8))
        ];
        ulong[] pointerScratch = new ulong[1];
        PciEcamSegment[] segments = new PciEcamSegment[1];

        AcpiMcfgDiscoveryResult result = AcpiMcfgDiscovery.TryDiscover(
            xsdt,
            pointerScratch,
            sources,
            segments);

        return !result.Succeeded
            && result.Status == AcpiMcfgDiscoveryStatus.DirectoryFailed
            && result.DirectoryStatus == AcpiTableDirectoryStatus.SignatureNotFound
            && result.DirectoryResult.RequestedSignature == AcpiTableDirectory.McfgSignature
            && result.PointerCount == 1
            && !result.McfgParseAttempted;
    }

    private static bool TestReportsMcfgParseFailure()
    {
        byte[] xsdt = CreateXsdt(McfgAddress);
        byte[] mcfg = CreateMcfg(
            new McfgEntrySpec(
                basePhysicalAddress: 0x00000000E0000000,
                segmentGroup: 0,
                startBus: 0,
                endBus: 0));
        mcfg[AcpiMcfgParser.HeaderReservedOffset] = 1;
        SetChecksum(mcfg);
        AcpiTableSource[] sources =
        [
            new AcpiTableSource(McfgAddress, mcfg)
        ];
        ulong[] pointerScratch = new ulong[1];
        PciEcamSegment[] segments = new PciEcamSegment[1];

        AcpiMcfgDiscoveryResult result = AcpiMcfgDiscovery.TryDiscover(
            xsdt,
            pointerScratch,
            sources,
            segments);

        return !result.Succeeded
            && result.Status == AcpiMcfgDiscoveryStatus.McfgParseFailed
            && result.DirectoryStatus == AcpiTableDirectoryStatus.Success
            && result.McfgParseStatus == AcpiMcfgParseStatus.InvalidReservedBytes
            && result.McfgParseAttempted
            && result.SelectedPhysicalAddress == McfgAddress
            && result.SelectedPointerIndex == 0
            && result.SelectedSourceIndex == 0;
    }

    private static bool TestReportsSegmentDestinationExhaustion()
    {
        byte[] xsdt = CreateXsdt(McfgAddress);
        byte[] mcfg = CreateMcfg(
            new McfgEntrySpec(
                basePhysicalAddress: 0x00000000E0000000,
                segmentGroup: 0,
                startBus: 0,
                endBus: 0),
            new McfgEntrySpec(
                basePhysicalAddress: 0x00000000E1000000,
                segmentGroup: 1,
                startBus: 1,
                endBus: 2));
        AcpiTableSource[] sources =
        [
            new AcpiTableSource(McfgAddress, mcfg)
        ];
        ulong[] pointerScratch = new ulong[1];
        PciEcamSegment[] segments = new PciEcamSegment[1];

        AcpiMcfgDiscoveryResult result = AcpiMcfgDiscovery.TryDiscover(
            xsdt,
            pointerScratch,
            sources,
            segments);

        return !result.Succeeded
            && result.Status == AcpiMcfgDiscoveryStatus.McfgParseFailed
            && result.DirectoryStatus == AcpiTableDirectoryStatus.Success
            && result.McfgParseStatus == AcpiMcfgParseStatus.DestinationTooSmall
            && result.RequiredSegmentCapacity == 2
            && result.SegmentCount == 0
            && result.SelectedPhysicalAddress == McfgAddress;
    }

    private static bool TestSupportsDirectPointerListInput()
    {
        ulong[] tablePointers = [McfgAddress];
        byte[] mcfg = CreateMcfg(
            new McfgEntrySpec(
                basePhysicalAddress: 0x00000000E0000000,
                segmentGroup: 3,
                startBus: 0x80,
                endBus: 0x8F));
        AcpiTableSource[] sources =
        [
            new AcpiTableSource(McfgAddress, mcfg)
        ];
        PciEcamSegment[] segments = new PciEcamSegment[1];

        AcpiMcfgDiscoveryResult result = AcpiMcfgDiscovery.TryDiscover(
            tablePointers,
            sources,
            segments);

        return result.Succeeded
            && result.PointerCount == 1
            && result.SelectedPointerIndex == 0
            && result.SelectedSourceIndex == 0
            && result.SelectedPhysicalAddress == McfgAddress
            && result.SegmentCount == 1
            && segments[0].SegmentGroup == 3
            && segments[0].StartBus == 0x80
            && segments[0].EndBus == 0x8F;
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

    private static byte[] CreateMcfg(params McfgEntrySpec[] entries)
    {
        byte[] mcfg = new byte[
            AcpiMcfgParser.AllocationEntryOffset + (entries.Length * AcpiMcfgParser.AllocationEntryLength)];

        WriteSdtHeader(mcfg, AcpiTableDirectory.McfgSignature);

        for (int index = 0; index < entries.Length; index++)
        {
            WriteEntry(
                mcfg.AsSpan(
                    AcpiMcfgParser.AllocationEntryOffset + (index * AcpiMcfgParser.AllocationEntryLength),
                    AcpiMcfgParser.AllocationEntryLength),
                entries[index]);
        }

        SetChecksum(mcfg);
        return mcfg;
    }

    private static void WriteEntry(
        Span<byte> destination,
        McfgEntrySpec entry)
    {
        EndianPrimitives.WriteUInt64LittleEndian(
            destination.Slice(AcpiMcfgParser.AllocationBaseAddressOffset, sizeof(ulong)),
            entry.BasePhysicalAddress);
        EndianPrimitives.WriteUInt16LittleEndian(
            destination.Slice(AcpiMcfgParser.AllocationSegmentGroupOffset, sizeof(ushort)),
            entry.SegmentGroup);
        destination[AcpiMcfgParser.AllocationStartBusOffset] = entry.StartBus;
        destination[AcpiMcfgParser.AllocationEndBusOffset] = entry.EndBus;
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
        WriteAscii(table, AcpiSdtParser.OemTableIdOffset, "MCFGDISC");
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

    private readonly struct McfgEntrySpec
    {
        public McfgEntrySpec(
            ulong basePhysicalAddress,
            ushort segmentGroup,
            byte startBus,
            byte endBus)
        {
            BasePhysicalAddress = basePhysicalAddress;
            SegmentGroup = segmentGroup;
            StartBus = startBus;
            EndBus = endBus;
        }

        public ulong BasePhysicalAddress { get; }

        public ushort SegmentGroup { get; }

        public byte StartBus { get; }

        public byte EndBus { get; }
    }
}
