using System.Runtime.CompilerServices;
using OS.Std.Binary;
using OS.Std.Hash;
using OS.Storage;

internal static class GptPartitionTableParserTests
{
    private const uint SectorSize = 512;
    private const ulong BlockCount = 128;
    private const ulong PartitionEntryLba = 2;
    private const uint PartitionEntryCount = 4;
    private const uint PartitionEntrySize = 128;
    private const ulong FirstUsableLba = 34;
    private const ulong LastUsableLba = 94;

    private static readonly Guid DiskGuid = new("11111111-2222-3333-4444-555555555555");
    private static readonly Guid EfiSystemPartitionGuid = new("c12a7328-f81f-11d2-ba4b-00a0c93ec93b");
    private static readonly Guid BasicDataPartitionGuid = new("ebd0a0a2-b9e5-4433-87c0-68b6b72699c7");
    private static readonly Guid FirstUniquePartitionGuid = new("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
    private static readonly Guid SecondUniquePartitionGuid = new("bbbbbbbb-cccc-dddd-eeee-ffffffffffff");

    [HostTest("storage.gpt.parser parses caller buffers", HostLane.Fast, 0)]
    private static bool TestParsesCallerBuffers()
    {
        byte[] entries = CreateValidPartitionEntries();
        byte[] header = CreateValidHeaderSector(entries);
        Span<GptPartitionDescriptor> descriptors = stackalloc GptPartitionDescriptor[2];

        GptPartitionTableParseResult result = GptPartitionTableParser.ParsePrimary(
            header,
            entries,
            BlockCount,
            SectorSize,
            descriptors);

        return HostCheck.Result(result.Succeeded
            && result.Status == GptPartitionTableStatus.Success
            && result.Header.Revision == GptPartitionTableParser.SupportedRevision
            && result.Header.HeaderSize == GptPartitionTableParser.MinimumHeaderSize
            && result.Header.CurrentLba == GptPartitionTableParser.PrimaryHeaderLba
            && result.Header.BackupLba == BlockCount - 1
            && result.Header.FirstUsableLba == FirstUsableLba
            && result.Header.LastUsableLba == LastUsableLba
            && result.Header.DiskGuid == DiskGuid
            && result.Header.PartitionEntryLba == PartitionEntryLba
            && result.Header.PartitionEntryCount == PartitionEntryCount
            && result.Header.PartitionEntrySize == PartitionEntrySize
            && result.PartitionsWritten == 2
            && result.EmptyEntriesSkipped == 2
            && result.PartitionEntryBytesRequired == PartitionEntryCount * PartitionEntrySize
            && result.PartitionEntrySectorsRequired == 1
            && descriptors[0].EntryIndex == 0
            && descriptors[0].EntryByteOffset == 0
            && descriptors[0].PartitionTypeGuid == BasicDataPartitionGuid
            && descriptors[0].UniquePartitionGuid == FirstUniquePartitionGuid
            && descriptors[0].FirstLba == 40
            && descriptors[0].LastLba == 50
            && descriptors[0].Attributes == 0x4000000000000001
            && descriptors[0].NameByteOffset == GptPartitionDescriptor.NameOffsetWithinEntryBytes
            && descriptors[0].NameUtf16CodeUnitLength == 4
            && descriptors[0].NameIsNullTerminated
            && descriptors[1].EntryIndex == 2
            && descriptors[1].EntryByteOffset == 256
            && descriptors[1].PartitionTypeGuid == EfiSystemPartitionGuid
            && descriptors[1].UniquePartitionGuid == SecondUniquePartitionGuid
            && descriptors[1].FirstLba == 60
            && descriptors[1].LastLba == 80
            && descriptors[1].NameByteOffset == 256 + GptPartitionDescriptor.NameOffsetWithinEntryBytes
            && descriptors[1].NameUtf16CodeUnitLength == 3
            && descriptors[1].NameByteLength == GptPartitionDescriptor.NameFieldLengthBytes);
    }

    [HostTest("storage.gpt.parser.crc validates good buffers", HostLane.Fast, 2)]
    private static bool TestValidatesCrc32()
    {
        byte[] entries = CreateValidPartitionEntries();
        byte[] header = CreateValidHeaderSector(entries);
        Span<GptPartitionDescriptor> descriptors = stackalloc GptPartitionDescriptor[2];

        GptPartitionTableParseResult result = GptPartitionTableParser.ParsePrimary(
            header,
            entries,
            BlockCount,
            SectorSize,
            descriptors);

        return HostCheck.Result(result.Succeeded
            && result.CrcStatus == GptPartitionTableCrcStatus.Validated
            && GptPartitionTableParser.ValidatesHeaderCrc32
            && GptPartitionTableParser.ValidatesPartitionEntryArrayCrc32
            && result.ExpectedHeaderCrc32 == result.ComputedHeaderCrc32
            && result.ExpectedHeaderCrc32 != 0
            && result.ExpectedPartitionEntryArrayCrc32 == result.ComputedPartitionEntryArrayCrc32
            && result.ExpectedPartitionEntryArrayCrc32 == result.Header.PartitionEntryArrayCrc32
            && result.ExpectedPartitionEntryArrayCrc32 != 0);
    }

    [HostTest("storage.gpt.parser default result is not success", HostLane.Fast, 3)]
    private static bool TestDefaultResultIsNotSuccess()
    {
        GptPartitionTableParseResult result = default;
        GptPartitionTableParseResult failed = GptPartitionTableParseResult.Fail(
            GptPartitionTableStatus.HeaderCrcMismatch,
            partitionsWritten: 1,
            emptyEntriesSkipped: 2,
            partitionEntryBytesRequired: 512,
            partitionEntrySectorsRequired: 1,
            failingEntryIndex: 3,
            crcStatus: GptPartitionTableCrcStatus.HeaderMismatch,
            expectedHeaderCrc32: 4,
            computedHeaderCrc32: 5,
            expectedPartitionEntryArrayCrc32: 6,
            computedPartitionEntryArrayCrc32: 7);

        return HostCheck.Result(!result.IsInitialized
            && !result.Succeeded
            && result.Status == GptPartitionTableStatus.Success
            && result.CrcStatus == GptPartitionTableCrcStatus.NotValidated
            && result.PartitionsWritten == 0
            && result.EmptyEntriesSkipped == 0
            && result.PartitionEntryBytesRequired == 0
            && result.PartitionEntrySectorsRequired == 0
            && result.FailingEntryIndex == 0
            && result.ExpectedHeaderCrc32 == 0
            && result.ComputedHeaderCrc32 == 0
            && result.ExpectedPartitionEntryArrayCrc32 == 0
            && result.ComputedPartitionEntryArrayCrc32 == 0
            && failed.IsInitialized
            && !failed.Succeeded
            && failed.Status == GptPartitionTableStatus.HeaderCrcMismatch
            && failed.CrcStatus == GptPartitionTableCrcStatus.HeaderMismatch
            && failed.PartitionsWritten == 1
            && failed.EmptyEntriesSkipped == 2
            && failed.PartitionEntryBytesRequired == 512
            && failed.PartitionEntrySectorsRequired == 1
            && failed.FailingEntryIndex == 3
            && failed.ExpectedHeaderCrc32 == 4
            && failed.ComputedHeaderCrc32 == 5
            && failed.ExpectedPartitionEntryArrayCrc32 == 6
            && failed.ComputedPartitionEntryArrayCrc32 == 7
            && !RuntimeHelpers.IsReferenceOrContainsReferences<GptPartitionTableParseResult>());
    }

    [HostTest("storage.gpt.parser uses shared memory helpers", HostLane.Fast, 4)]
    private static bool TestUsesSharedMemoryHelpers()
    {
        var catalog = HostSourceCatalog.Project("src/OS.Kernel/OS.Kernel.csproj");
        string owner = typeof(GptPartitionTableParser).FullName!;
        return HostCheck.Result(StorageEqualitySourceGuard.Literal(catalog, owner, "HasGptSignature", "EFI PART", 8)
            && StorageEqualitySourceGuard.ZeroBuffer(catalog, owner, "IsZeroGuid", 16));
    }

    [HostTest("storage.gpt.partition parser header copy uses shared buffer operations", HostLane.Fast, 5)]
    private static bool TestHeaderCopyUsesSharedBufferOperations()
    {
        return HostCheck.Result(StorageGptHeaderSourceGuard.Check(
            HostSourceCatalog.Project("src/OS.Kernel/OS.Kernel.csproj"), typeof(GptPartitionTableParser).FullName!));
    }

    [HostTest("storage.gpt.parser parses backup caller buffers", HostLane.Fast, 1)]
    private static bool TestParsesBackupCallerBuffers()
    {
        byte[] entries = CreateValidPartitionEntries();
        byte[] header = CreateValidBackupHeaderSector(entries);
        Span<GptPartitionDescriptor> descriptors = stackalloc GptPartitionDescriptor[2];

        GptPartitionTableParseResult result = GptPartitionTableParser.ParseBackup(
            header,
            entries,
            BlockCount,
            SectorSize,
            descriptors);

        return HostCheck.Result(result.Succeeded
            && result.Status == GptPartitionTableStatus.Success
            && result.CrcStatus == GptPartitionTableCrcStatus.Validated
            && result.Header.CurrentLba == BlockCount - 1
            && result.Header.BackupLba == GptPartitionTableParser.PrimaryHeaderLba
            && result.Header.PartitionEntryLba == BlockCount - 2
            && result.Header.FirstUsableLba == FirstUsableLba
            && result.Header.LastUsableLba == LastUsableLba
            && result.PartitionsWritten == 2
            && result.EmptyEntriesSkipped == 2
            && result.PartitionEntryBytesRequired == PartitionEntryCount * PartitionEntrySize
            && result.PartitionEntrySectorsRequired == 1
            && result.ExpectedHeaderCrc32 == result.ComputedHeaderCrc32
            && result.ExpectedPartitionEntryArrayCrc32 == result.ComputedPartitionEntryArrayCrc32
            && descriptors[0].PartitionTypeGuid == BasicDataPartitionGuid
            && descriptors[1].PartitionTypeGuid == EfiSystemPartitionGuid);
    }

    [HostTest("storage.gpt.parser.crc rejects header mismatch", HostLane.Fast, 6)]
    private static bool TestRejectsHeaderCrcMismatch()
    {
        byte[] entries = CreateValidPartitionEntries();
        byte[] header = CreateValidHeaderSector(entries);
        header[20] = 0x5A;

        GptPartitionTableParseResult result = ParseWithOneDescriptor(header, entries);

        return HostCheck.Result(!result.Succeeded
            && result.Status == GptPartitionTableStatus.HeaderCrcMismatch
            && result.CrcStatus == GptPartitionTableCrcStatus.HeaderMismatch
            && result.ExpectedHeaderCrc32 != result.ComputedHeaderCrc32
            && result.ExpectedPartitionEntryArrayCrc32 == result.Header.PartitionEntryArrayCrc32);
    }

    [HostTest("storage.gpt.parser.crc rejects partition array mismatch", HostLane.Fast, 7)]
    private static bool TestRejectsPartitionEntryArrayCrcMismatch()
    {
        byte[] entries = CreateValidPartitionEntries();
        byte[] header = CreateValidHeaderSector(entries);
        entries[checked((int)(GptPartitionDescriptor.NameOffsetWithinEntryBytes + sizeof(ushort)))] ^= 0x40;

        GptPartitionTableParseResult result = ParseWithOneDescriptor(header, entries);

        return HostCheck.Result(!result.Succeeded
            && result.Status == GptPartitionTableStatus.PartitionEntryArrayCrcMismatch
            && result.CrcStatus == GptPartitionTableCrcStatus.PartitionEntryArrayMismatch
            && result.ExpectedHeaderCrc32 == result.ComputedHeaderCrc32
            && result.ExpectedPartitionEntryArrayCrc32 != result.ComputedPartitionEntryArrayCrc32);
    }

    [HostTest("storage.gpt.parser rejects bad signature", HostLane.Fast, 8)]
    private static bool TestRejectsBadSignature()
    {
        byte[] header = CreateValidHeaderSector();
        header[0] = 0;

        GptPartitionTableParseResult result = ParseWithOneDescriptor(header, CreateValidPartitionEntries());

        return HostCheck.Result(!result.Succeeded
            && result.Status == GptPartitionTableStatus.InvalidHeaderSignature);
    }

    [HostTest("storage.gpt.parser rejects unsupported revision", HostLane.Fast, 9)]
    private static bool TestRejectsUnsupportedRevision()
    {
        byte[] header = CreateValidHeaderSector();
        WriteUInt32(header, 8, 0x00020000);

        GptPartitionTableParseResult result = ParseWithOneDescriptor(header, CreateValidPartitionEntries());

        return HostCheck.Result(!result.Succeeded
            && result.Status == GptPartitionTableStatus.UnsupportedRevision
            && result.Header.Revision == 0x00020000);
    }

    [HostTest("storage.gpt.parser rejects invalid header size", HostLane.Fast, 10)]
    private static bool TestRejectsInvalidHeaderSize()
    {
        byte[] header = CreateValidHeaderSector();
        WriteUInt32(header, 12, GptPartitionTableParser.MinimumHeaderSize - 1);

        GptPartitionTableParseResult result = ParseWithOneDescriptor(header, CreateValidPartitionEntries());

        return HostCheck.Result(!result.Succeeded
            && result.Status == GptPartitionTableStatus.InvalidHeaderSize);
    }

    [HostTest("storage.gpt.parser rejects invalid current lba", HostLane.Fast, 11)]
    private static bool TestRejectsInvalidCurrentLba()
    {
        byte[] header = CreateValidHeaderSector();
        WriteUInt64(header, 24, 2);

        GptPartitionTableParseResult result = ParseWithOneDescriptor(header, CreateValidPartitionEntries());

        return HostCheck.Result(!result.Succeeded
            && result.Status == GptPartitionTableStatus.InvalidCurrentLba);
    }

    [HostTest("storage.gpt.parser rejects invalid usable bounds", HostLane.Fast, 12)]
    private static bool TestRejectsInvalidUsableBounds()
    {
        byte[] header = CreateValidHeaderSector();
        WriteUInt64(header, 40, LastUsableLba);
        WriteUInt64(header, 48, FirstUsableLba);

        GptPartitionTableParseResult result = ParseWithOneDescriptor(header, CreateValidPartitionEntries());

        return HostCheck.Result(!result.Succeeded
            && result.Status == GptPartitionTableStatus.InvalidUsableLbaRange);
    }

    [HostTest("storage.gpt.parser backup rejects invalid lba metadata", HostLane.Fast, 13)]
    private static bool TestBackupRejectsInvalidLbaMetadata()
    {
        byte[] entries = CreateValidPartitionEntries();
        Span<GptPartitionDescriptor> descriptors = stackalloc GptPartitionDescriptor[2];

        byte[] invalidCurrent = CreateValidBackupHeaderSector(entries);
        WriteUInt64(invalidCurrent, 24, BlockCount - 2);
        RefreshHeaderCrc32(invalidCurrent);
        GptPartitionTableParseResult currentResult = GptPartitionTableParser.ParseBackup(
            invalidCurrent,
            entries,
            BlockCount,
            SectorSize,
            descriptors);

        byte[] invalidBackup = CreateValidBackupHeaderSector(entries);
        WriteUInt64(invalidBackup, 32, 2);
        RefreshHeaderCrc32(invalidBackup);
        GptPartitionTableParseResult backupResult = GptPartitionTableParser.ParseBackup(
            invalidBackup,
            entries,
            BlockCount,
            SectorSize,
            descriptors);

        byte[] overlappingEntries = CreateValidBackupHeaderSector(entries);
        WriteUInt64(overlappingEntries, 72, BlockCount - 1);
        RefreshHeaderCrc32(overlappingEntries);
        GptPartitionTableParseResult entryResult = GptPartitionTableParser.ParseBackup(
            overlappingEntries,
            entries,
            BlockCount,
            SectorSize,
            descriptors);

        byte[] earlyEntries = CreateValidBackupHeaderSector(entries);
        WriteUInt64(earlyEntries, 72, 2);
        RefreshHeaderCrc32(earlyEntries);
        GptPartitionTableParseResult earlyEntryResult = GptPartitionTableParser.ParseBackup(
            earlyEntries,
            entries,
            BlockCount,
            SectorSize,
            descriptors);

        return HostCheck.Result(!currentResult.Succeeded
            && currentResult.Status == GptPartitionTableStatus.InvalidCurrentLba
            && !backupResult.Succeeded
            && backupResult.Status == GptPartitionTableStatus.InvalidBackupLba
            && !entryResult.Succeeded
            && entryResult.Status == GptPartitionTableStatus.InvalidPartitionEntryArrayRange
            && !earlyEntryResult.Succeeded
            && earlyEntryResult.Status == GptPartitionTableStatus.InvalidPartitionEntryArrayRange
            && earlyEntryResult.PartitionEntrySectorsRequired == 1);
    }

    [HostTest("storage.gpt.parser rejects invalid entry count", HostLane.Fast, 14)]
    private static bool TestRejectsInvalidEntryCount()
    {
        byte[] header = CreateValidHeaderSector();
        WriteUInt32(header, 80, 0);

        GptPartitionTableParseResult result = ParseWithOneDescriptor(header, CreateValidPartitionEntries());

        return HostCheck.Result(!result.Succeeded
            && result.Status == GptPartitionTableStatus.InvalidPartitionEntryCount);
    }

    [HostTest("storage.gpt.parser rejects invalid entry size", HostLane.Fast, 15)]
    private static bool TestRejectsInvalidEntrySize()
    {
        byte[] header = CreateValidHeaderSector();
        WriteUInt32(header, 84, 64);

        GptPartitionTableParseResult result = ParseWithOneDescriptor(header, CreateValidPartitionEntries());

        return HostCheck.Result(!result.Succeeded
            && result.Status == GptPartitionTableStatus.InvalidPartitionEntrySize);
    }

    [HostTest("storage.gpt.parser rejects entry array out of range", HostLane.Fast, 16)]
    private static bool TestRejectsEntryArrayOutOfRange()
    {
        byte[] header = CreateValidHeaderSector();
        WriteUInt64(header, 72, BlockCount - 1);
        WriteUInt32(header, 80, PartitionEntryCount * 2);

        GptPartitionTableParseResult result = ParseWithOneDescriptor(header, CreateValidPartitionEntries());

        return HostCheck.Result(!result.Succeeded
            && result.Status == GptPartitionTableStatus.InvalidPartitionEntryArrayRange);
    }

    [HostTest("storage.gpt.parser rejects short entry buffer", HostLane.Fast, 17)]
    private static bool TestRejectsShortEntryBuffer()
    {
        byte[] entries = CreateValidPartitionEntries();
        Span<GptPartitionDescriptor> descriptors = stackalloc GptPartitionDescriptor[2];

        GptPartitionTableParseResult result = GptPartitionTableParser.ParsePrimary(
            CreateValidHeaderSector(),
            entries.AsSpan(0, entries.Length - 1),
            BlockCount,
            SectorSize,
            descriptors);

        return HostCheck.Result(!result.Succeeded
            && result.Status == GptPartitionTableStatus.PartitionEntryBufferTooSmall
            && result.PartitionEntryBytesRequired == PartitionEntryCount * PartitionEntrySize
            && result.PartitionEntrySectorsRequired == 1);
    }

    [HostTest("storage.gpt.parser reports destination too small", HostLane.Fast, 18)]
    private static bool TestReportsDestinationTooSmall()
    {
        byte[] header = CreateValidHeaderSector();
        byte[] entries = CreateValidPartitionEntries();
        Span<GptPartitionDescriptor> descriptors = stackalloc GptPartitionDescriptor[1];

        GptPartitionTableParseResult result = GptPartitionTableParser.ParsePrimary(
            header,
            entries,
            BlockCount,
            SectorSize,
            descriptors);

        return HostCheck.Result(!result.Succeeded
            && result.Status == GptPartitionTableStatus.DestinationTooSmall
            && result.PartitionsWritten == 1
            && result.FailingEntryIndex == 2
            && descriptors[0].PartitionTypeGuid == BasicDataPartitionGuid);
    }

    [HostTest("storage.gpt.parser rejects invalid partition lba range", HostLane.Fast, 19)]
    private static bool TestRejectsInvalidPartitionLbaRange()
    {
        byte[] entries = CreateEmptyPartitionEntries();
        WritePartitionEntry(
            entries,
            entryIndex: 0,
            BasicDataPartitionGuid,
            FirstUniquePartitionGuid,
            firstLba: 80,
            lastLba: 79,
            attributes: 0,
            name: "BAD");
        byte[] header = CreateValidHeaderSector(entries);

        GptPartitionTableParseResult result = ParseWithOneDescriptor(header, entries);

        return HostCheck.Result(!result.Succeeded
            && result.Status == GptPartitionTableStatus.InvalidPartitionEntryRange
            && result.FailingEntryIndex == 0);
    }

    [HostTest("storage.gpt.parser records unterminated name metadata", HostLane.Fast, 20)]
    private static bool TestRecordsUnterminatedNameMetadata()
    {
        byte[] entries = CreateEmptyPartitionEntries();
        string maximumName = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        WritePartitionEntry(
            entries,
            entryIndex: 0,
            BasicDataPartitionGuid,
            FirstUniquePartitionGuid,
            firstLba: 40,
            lastLba: 50,
            attributes: 0,
            name: maximumName);
        byte[] header = CreateValidHeaderSector(entries);
        Span<GptPartitionDescriptor> descriptors = stackalloc GptPartitionDescriptor[1];

        GptPartitionTableParseResult result = GptPartitionTableParser.ParsePrimary(
            header,
            entries,
            BlockCount,
            SectorSize,
            descriptors);

        return HostCheck.Result(result.Succeeded
            && result.PartitionsWritten == 1
            && descriptors[0].NameUtf16CodeUnitLength == GptPartitionDescriptor.MaximumNameUtf16CodeUnits
            && !descriptors[0].NameIsNullTerminated);
    }

    private static GptPartitionTableParseResult ParseWithOneDescriptor(byte[] header, byte[] entries)
    {
        Span<GptPartitionDescriptor> descriptors = stackalloc GptPartitionDescriptor[1];
        return GptPartitionTableParser.ParsePrimary(
            header,
            entries,
            BlockCount,
            SectorSize,
            descriptors);
    }

    private static byte[] CreateValidHeaderSector()
    {
        return CreateValidHeaderSector(CreateValidPartitionEntries());
    }

    private static byte[] CreateValidHeaderSector(ReadOnlySpan<byte> partitionEntries)
    {
        return CreateHeaderSector(
            partitionEntries,
            GptPartitionTableParser.PrimaryHeaderLba,
            BlockCount - 1,
            PartitionEntryLba);
    }

    private static byte[] CreateValidBackupHeaderSector(ReadOnlySpan<byte> partitionEntries)
    {
        return CreateHeaderSector(
            partitionEntries,
            BlockCount - 1,
            GptPartitionTableParser.PrimaryHeaderLba,
            BlockCount - 2);
    }

    private static byte[] CreateHeaderSector(
        ReadOnlySpan<byte> partitionEntries,
        ulong currentLba,
        ulong backupLba,
        ulong partitionEntryLba)
    {
        byte[] header = new byte[SectorSize];
        header[0] = (byte)'E';
        header[1] = (byte)'F';
        header[2] = (byte)'I';
        header[3] = (byte)' ';
        header[4] = (byte)'P';
        header[5] = (byte)'A';
        header[6] = (byte)'R';
        header[7] = (byte)'T';

        WriteUInt32(header, 8, GptPartitionTableParser.SupportedRevision);
        WriteUInt32(header, 12, GptPartitionTableParser.MinimumHeaderSize);
        WriteUInt64(header, 24, currentLba);
        WriteUInt64(header, 32, backupLba);
        WriteUInt64(header, 40, FirstUsableLba);
        WriteUInt64(header, 48, LastUsableLba);
        WriteGuid(header, 56, DiskGuid);
        WriteUInt64(header, 72, partitionEntryLba);
        WriteUInt32(header, 80, PartitionEntryCount);
        WriteUInt32(header, 84, PartitionEntrySize);
        WriteUInt32(header, 88, Crc32.Compute(partitionEntries.Slice(0, checked((int)(PartitionEntryCount * PartitionEntrySize)))));
        RefreshHeaderCrc32(header);

        return header;
    }

    private static byte[] CreateValidPartitionEntries()
    {
        byte[] entries = CreateEmptyPartitionEntries();
        WritePartitionEntry(
            entries,
            entryIndex: 0,
            BasicDataPartitionGuid,
            FirstUniquePartitionGuid,
            firstLba: 40,
            lastLba: 50,
            attributes: 0x4000000000000001,
            name: "BOOT");
        WritePartitionEntry(
            entries,
            entryIndex: 2,
            EfiSystemPartitionGuid,
            SecondUniquePartitionGuid,
            firstLba: 60,
            lastLba: 80,
            attributes: 0,
            name: "ESP");
        return entries;
    }

    private static byte[] CreateEmptyPartitionEntries()
    {
        return new byte[PartitionEntryCount * PartitionEntrySize];
    }

    private static void WritePartitionEntry(
        byte[] entries,
        uint entryIndex,
        Guid partitionTypeGuid,
        Guid uniquePartitionGuid,
        ulong firstLba,
        ulong lastLba,
        ulong attributes,
        string name)
    {
        int entryOffset = checked((int)(entryIndex * PartitionEntrySize));
        WriteGuid(entries, entryOffset, partitionTypeGuid);
        WriteGuid(entries, entryOffset + 16, uniquePartitionGuid);
        WriteUInt64(entries, entryOffset + 32, firstLba);
        WriteUInt64(entries, entryOffset + 40, lastLba);
        WriteUInt64(entries, entryOffset + 48, attributes);
        WriteUtf16Name(entries, entryOffset + GptPartitionDescriptor.NameOffsetWithinEntryBytes, name);
    }

    private static void WriteGuid(byte[] destination, int offset, Guid value)
    {
        if (!value.TryWriteBytes(destination.AsSpan(offset, 16)))
        {
            throw new InvalidOperationException("Could not write test GUID.");
        }
    }

    private static void WriteUtf16Name(byte[] destination, int offset, string value)
    {
        for (int i = 0; i < value.Length; i++)
        {
            EndianPrimitives.WriteUInt16LittleEndian(
                destination.AsSpan(offset + (i * sizeof(ushort)), sizeof(ushort)),
                value[i]);
        }
    }

    private static void WriteUInt32(byte[] destination, int offset, uint value)
    {
        EndianPrimitives.WriteUInt32LittleEndian(destination.AsSpan(offset, 4), value);
    }

    private static void WriteUInt64(byte[] destination, int offset, ulong value)
    {
        EndianPrimitives.WriteUInt64LittleEndian(destination.AsSpan(offset, 8), value);
    }

    private static void RefreshHeaderCrc32(byte[] header)
    {
        WriteUInt32(header, 16, 0);
        WriteUInt32(
            header,
            16,
            Crc32.Compute(header.AsSpan(0, checked((int)GptPartitionTableParser.MinimumHeaderSize))));
    }
}
