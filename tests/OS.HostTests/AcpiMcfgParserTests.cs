using System.Runtime.CompilerServices;
using OS.Boot;
using OS.Drivers;
using OS.Std.Binary;

internal static class AcpiMcfgParserTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("acpi.mcfg parses ecam segment", TestParsesEcamSegment, ref failures);
        Run("acpi.mcfg rejects bad signature", TestRejectsBadSignature, ref failures);
        Run("acpi.mcfg rejects truncated entry", TestRejectsTruncatedEntry, ref failures);
        Run("acpi.mcfg rejects invalid bus range", TestRejectsInvalidBusRange, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("ACPI MCFG parser host tests failed: " + failures);
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

    private static bool TestParsesEcamSegment()
    {
        byte[] mcfg = CreateMcfg(
            new McfgEntrySpec(
                basePhysicalAddress: 0x00000000E0000000,
                segmentGroup: 2,
                startBus: 0x20,
                endBus: 0x2F));
        PciEcamSegment[] segments = new PciEcamSegment[1];

        AcpiMcfgParseResult result = AcpiMcfgParser.TryParse(mcfg, segments);
        PciEcamConfigurationAddressResult address = PciEcamConfigurationSpace.CalculateAddress(
            segments[0],
            bus: 0x24,
            device: 0,
            function: 0,
            registerOffset: 0,
            accessWidth: 4);

        return result.Succeeded
            && result.Status == AcpiMcfgParseStatus.Success
            && result.SegmentCount == 1
            && result.DeclaredLength == mcfg.Length
            && result.RequiredSegmentCapacity == 1
            && segments[0].BasePhysicalAddress == 0x00000000E0000000
            && segments[0].SegmentGroup == 2
            && segments[0].StartBus == 0x20
            && segments[0].EndBus == 0x2F
            && address.Succeeded
            && address.PhysicalAddress == 0x00000000E0400000;
    }

    private static bool TestRejectsBadSignature()
    {
        byte[] badSignature = CreateMcfg(
            new McfgEntrySpec(
                basePhysicalAddress: 0xE0000000,
                segmentGroup: 0,
                startBus: 0,
                endBus: 0));
        badSignature[0] = (byte)'X';

        byte[] tooShort = new byte[AcpiMcfgParser.SystemDescriptionTableHeaderLength - 1];

        byte[] declaredTooSmall = CreateMcfg(
            new McfgEntrySpec(
                basePhysicalAddress: 0xE0000000,
                segmentGroup: 0,
                startBus: 0,
                endBus: 0));
        WriteLength(declaredTooSmall, AcpiMcfgParser.AllocationEntryOffset);

        byte[] invalidReservedBytes = CreateMcfg(
            new McfgEntrySpec(
                basePhysicalAddress: 0xE0000000,
                segmentGroup: 0,
                startBus: 0,
                endBus: 0));
        invalidReservedBytes[AcpiMcfgParser.HeaderReservedOffset] = 1;

        AcpiMcfgParseResult badSignatureResult = AcpiMcfgParser.TryParse(
            badSignature,
            new PciEcamSegment[1]);
        AcpiMcfgParseResult tooShortResult = AcpiMcfgParser.TryParse(
            tooShort,
            new PciEcamSegment[1]);
        AcpiMcfgParseResult declaredTooSmallResult = AcpiMcfgParser.TryParse(
            declaredTooSmall,
            new PciEcamSegment[1]);
        AcpiMcfgParseResult invalidReservedBytesResult = AcpiMcfgParser.TryParse(
            invalidReservedBytes,
            new PciEcamSegment[1]);

        return IsFailure(badSignatureResult, AcpiMcfgParseStatus.InvalidSignature)
            && IsFailure(
                tooShortResult,
                AcpiMcfgParseStatus.SourceTooShort,
                requiredLength: AcpiMcfgParser.SystemDescriptionTableHeaderLength)
            && IsFailure(
                declaredTooSmallResult,
                AcpiMcfgParseStatus.DeclaredLengthTooSmall,
                declaredLength: AcpiMcfgParser.AllocationEntryOffset,
                requiredLength: AcpiMcfgParser.MinimumLength)
            && IsFailure(
                invalidReservedBytesResult,
                AcpiMcfgParseStatus.InvalidReservedBytes,
                declaredLength: (uint)invalidReservedBytes.Length,
                requiredLength: (uint)invalidReservedBytes.Length);
    }

    private static bool TestRejectsTruncatedEntry()
    {
        byte[] truncatedEntry = CreateMcfg(
            new McfgEntrySpec(
                basePhysicalAddress: 0xE0000000,
                segmentGroup: 0,
                startBus: 0,
                endBus: 0));
        Array.Resize(ref truncatedEntry, truncatedEntry.Length + 1);
        WriteLength(truncatedEntry, truncatedEntry.Length);

        byte[] sourceTooShort = CreateMcfg(
            new McfgEntrySpec(
                basePhysicalAddress: 0xE0000000,
                segmentGroup: 0,
                startBus: 0,
                endBus: 0));
        WriteLength(sourceTooShort, sourceTooShort.Length + AcpiMcfgParser.AllocationEntryLength);

        AcpiMcfgParseResult truncatedEntryResult = AcpiMcfgParser.TryParse(
            truncatedEntry,
            new PciEcamSegment[2]);
        AcpiMcfgParseResult sourceTooShortResult = AcpiMcfgParser.TryParse(
            sourceTooShort,
            new PciEcamSegment[2]);

        return IsFailure(
                truncatedEntryResult,
                AcpiMcfgParseStatus.TruncatedAllocationEntry,
                declaredLength: (uint)truncatedEntry.Length,
                requiredLength: (uint)(truncatedEntry.Length + (AcpiMcfgParser.AllocationEntryLength - 1)),
                entryIndex: 1)
            && IsFailure(
                sourceTooShortResult,
                AcpiMcfgParseStatus.SourceTooShortForDeclaredLength,
                declaredLength: (uint)(sourceTooShort.Length + AcpiMcfgParser.AllocationEntryLength),
                requiredLength: (uint)(sourceTooShort.Length + AcpiMcfgParser.AllocationEntryLength));
    }

    private static bool TestRejectsInvalidBusRange()
    {
        byte[] invalidRange = CreateMcfg(
            new McfgEntrySpec(
                basePhysicalAddress: 0xE0000000,
                segmentGroup: 0,
                startBus: 4,
                endBus: 3));
        byte[] validTwoSegments = CreateMcfg(
            new McfgEntrySpec(
                basePhysicalAddress: 0xE0000000,
                segmentGroup: 0,
                startBus: 0,
                endBus: 0),
            new McfgEntrySpec(
                basePhysicalAddress: 0xE1000000,
                segmentGroup: 1,
                startBus: 1,
                endBus: 2));

        AcpiMcfgParseResult invalidRangeResult = AcpiMcfgParser.TryParse(
            invalidRange,
            new PciEcamSegment[1]);
        AcpiMcfgParseResult destinationTooSmallResult = AcpiMcfgParser.TryParse(
            validTwoSegments,
            new PciEcamSegment[1]);

        return IsFailure(
                invalidRangeResult,
                AcpiMcfgParseStatus.InvalidBusRange,
                declaredLength: (uint)invalidRange.Length,
                requiredLength: (uint)invalidRange.Length,
                entryIndex: 0)
            && IsFailure(
                destinationTooSmallResult,
                AcpiMcfgParseStatus.DestinationTooSmall,
                declaredLength: (uint)validTwoSegments.Length,
                requiredLength: (uint)validTwoSegments.Length,
                requiredSegmentCapacity: 2);
    }

    private static bool IsFailure(
        AcpiMcfgParseResult result,
        AcpiMcfgParseStatus status,
        uint declaredLength = 0,
        uint requiredLength = 0,
        int requiredSegmentCapacity = 0,
        int entryIndex = -1)
    {
        return !result.Succeeded
            && result.Status == status
            && result.SegmentCount == 0
            && result.DeclaredLength == declaredLength
            && result.RequiredLength == requiredLength
            && result.RequiredSegmentCapacity == requiredSegmentCapacity
            && result.EntryIndex == entryIndex;
    }

    private static byte[] CreateMcfg(params McfgEntrySpec[] entries)
    {
        byte[] mcfg = new byte[
            AcpiMcfgParser.AllocationEntryOffset + (entries.Length * AcpiMcfgParser.AllocationEntryLength)];

        mcfg[0] = (byte)'M';
        mcfg[1] = (byte)'C';
        mcfg[2] = (byte)'F';
        mcfg[3] = (byte)'G';
        WriteLength(mcfg, mcfg.Length);
        mcfg[8] = 1;
        WriteAscii(mcfg, 10, "OSTEST");
        WriteAscii(mcfg, 16, "MCFGTEST");
        EndianPrimitives.WriteUInt32LittleEndian(mcfg.AsSpan(24, sizeof(uint)), 1);
        WriteAscii(mcfg, 28, "OS  ");
        EndianPrimitives.WriteUInt32LittleEndian(mcfg.AsSpan(32, sizeof(uint)), 1);

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

    private static void WriteEntry(Span<byte> destination, McfgEntrySpec entry)
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

    private static void WriteLength(byte[] mcfg, int length)
    {
        EndianPrimitives.WriteUInt32LittleEndian(
            mcfg.AsSpan(AcpiMcfgParser.LengthOffset, sizeof(uint)),
            (uint)length);
        SetChecksum(mcfg);
    }

    private static void WriteAscii(byte[] destination, int offset, string value)
    {
        for (int index = 0; index < value.Length; index++)
        {
            destination[offset + index] = (byte)value[index];
        }
    }

    private static void SetChecksum(byte[] mcfg)
    {
        if (mcfg.Length <= 9)
        {
            return;
        }

        mcfg[9] = 0;
        uint sum = 0;
        for (int index = 0; index < mcfg.Length; index++)
        {
            sum += mcfg[index];
        }

        mcfg[9] = unchecked((byte)(0 - sum));
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
