using System.Runtime.CompilerServices;
using OS.Std.Binary;
using OS.Storage;

internal static partial class StorageGptParserTests
{
    private const uint SectorSize = 512;
    private const ulong BlockCount = 128;
    private const ulong PartitionEntryLba = 2;
    private const uint PartitionEntryCount = 128;
    private const uint PartitionEntrySize = 128;
    private const ulong FirstUsableLba = 34;
    private const ulong LastUsableLba = 94;
    private const ulong FirstPartitionLba = 40;
    private const ulong LastPartitionLba = 60;
    private const ulong PartitionAttributes = 0x4000000000000001;

    private static readonly Guid DiskGuid = new Guid("11111111-2222-3333-4444-555555555555");
    private static readonly Guid EfiSystemPartitionGuid = new Guid("c12a7328-f81f-11d2-ba4b-00a0c93ec93b");
    private static readonly Guid BasicDataPartitionGuid = new Guid("ebd0a0a2-b9e5-4433-87c0-68b6b72699c7");
    private static readonly Guid UniquePartitionGuid = new Guid("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
    private static readonly Guid SecondUniquePartitionGuid = new Guid("bbbbbbbb-cccc-dddd-eeee-ffffffffffff");

    [HostTest("storage gpt parses valid primary first partition", HostLane.Fast, 0)]
    private static bool TestValidPrimaryFirstPartition()
    {
        byte[] image = CreateValidGptImage();
        GptParseResult result = GptParser.ReadPrimaryFirstPartition(MemoryBlockDevice.FromImage(image, SectorSize));

        return HostCheck.Result(result.Succeeded
            && result.Status == GptStatus.Success
            && result.BlockStatus == BlockDeviceStatus.Success
            && result.Header.CurrentLba == 1
            && result.Header.BackupLba == BlockCount - 1
            && result.Header.FirstUsableLba == FirstUsableLba
            && result.Header.LastUsableLba == LastUsableLba
            && result.Header.DiskGuid == DiskGuid
            && result.Header.PartitionEntryLba == PartitionEntryLba
            && result.Header.PartitionEntryCount == PartitionEntryCount
            && result.Header.PartitionEntrySize == PartitionEntrySize
            && result.FirstPartition.PartitionTypeGuid == EfiSystemPartitionGuid
            && result.FirstPartition.UniquePartitionGuid == UniquePartitionGuid
            && result.FirstPartition.FirstLba == FirstPartitionLba
            && result.FirstPartition.LastLba == LastPartitionLba
            && result.FirstPartition.Attributes == PartitionAttributes
            && result.FirstPartition.Name == "OSDATA");
    }

    [HostTest("storage gpt rejects bad primary signature", HostLane.Fast, 1)]
    private static bool TestBadSignature()
    {
        byte[] image = CreateValidGptImage();
        image[SectorSize] = 0;

        GptParseResult result = GptParser.ReadPrimaryFirstPartition(MemoryBlockDevice.FromImage(image, SectorSize));

        return HostCheck.Result(!result.Succeeded
            && result.Status == GptStatus.InvalidHeaderSignature);
    }

    [HostTest("storage gpt rejects invalid entry size", HostLane.Fast, 2)]
    private static bool TestInvalidEntrySize()
    {
        byte[] image = CreateValidGptImage();
        WriteUInt32(image, checked((int)SectorSize) + 84, 64);

        GptParseResult result = GptParser.ReadPrimaryFirstPartition(MemoryBlockDevice.FromImage(image, SectorSize));

        return HostCheck.Result(!result.Succeeded
            && result.Status == GptStatus.InvalidPartitionEntrySize);
    }

    [HostTest("storage gpt reports primary header short read", HostLane.Fast, 3)]
    private static bool TestPrimaryHeaderShortRead()
    {
        byte[] image = CreateValidGptImage();
        GptParseResult result = GptParser.ReadPrimaryFirstPartition(new ShortReadBlockDevice(
            MemoryBlockDevice.FromImage(image, SectorSize),
            shortReadLba: 1));

        return HostCheck.Result(!result.Succeeded
            && result.Status == GptStatus.PrimaryHeaderShortRead);
    }

    [HostTest("storage gpt reports partition entry short read", HostLane.Fast, 4)]
    private static bool TestPartitionEntryShortRead()
    {
        byte[] image = CreateValidGptImage();
        GptParseResult result = GptParser.ReadPrimaryFirstPartition(new ShortReadBlockDevice(
            MemoryBlockDevice.FromImage(image, SectorSize),
            shortReadLba: PartitionEntryLba));

        return HostCheck.Result(!result.Succeeded
            && result.Status == GptStatus.PartitionEntryShortRead);
    }

    [HostTest("storage gpt rejects unsupported geometry", HostLane.Fast, 5)]
    private static bool TestUnsupportedGeometry()
    {
        GptParseResult result = GptParser.ReadPrimaryFirstPartition(new GeometryOnlyBlockDevice(8192, 4));

        return HostCheck.Result(!result.Succeeded
            && result.Status == GptStatus.UnsupportedGeometry);
    }

    [HostTest("storage gpt rejects partition entry array out of range", HostLane.Fast, 6)]
    private static bool TestPartitionEntryArrayOutOfRange()
    {
        byte[] image = CreateValidGptImage();
        WriteUInt32(image, checked((int)SectorSize) + 80, 4096);

        GptParseResult result = GptParser.ReadPrimaryFirstPartition(MemoryBlockDevice.FromImage(image, SectorSize));

        return HostCheck.Result(!result.Succeeded
            && result.Status == GptStatus.InvalidPartitionEntryArrayRange);
    }

    [HostTest("storage gpt result defaults are not success", HostLane.Fast, 7)]
    private static bool TestDefaultResultsAreNotSuccess()
    {
        GptParseResult parse = default;
        GptPartitionSelectionResult selection = default;
        GptEfiSystemPartitionLocationResult location = default;
        GptParseResult parseFailure = GptParseResult.Fail(GptStatus.InvalidBlockDevice);
        GptPartitionSelectionResult selectionFailure =
            GptPartitionSelectionResult.Fail(GptStatus.EfiSystemPartitionNotFound);
        GptEfiSystemPartitionLocationResult locationFailure =
            GptEfiSystemPartitionLocationResult.Fail(GptStatus.InvalidPartitionEntryRange);

        return HostCheck.Result(!parse.IsInitialized
            && !parse.Succeeded
            && parse.Status == GptStatus.Success
            && parse.BlockStatus == BlockDeviceStatus.Success
            && !selection.IsInitialized
            && !selection.Succeeded
            && selection.Status == GptStatus.Success
            && selection.BlockStatus == BlockDeviceStatus.Success
            && selection.PartitionEntryIndex == 0
            && !location.IsInitialized
            && !location.Succeeded
            && location.Status == GptStatus.Success
            && location.BlockStatus == BlockDeviceStatus.Success
            && location.PartitionEntryIndex == 0
            && location.BlockSizeBytes == 0
            && location.PartitionBlockCount == 0
            && location.PartitionByteOffset == 0
            && location.PartitionByteLength == 0
            && parseFailure.IsInitialized
            && !parseFailure.Succeeded
            && parseFailure.Status == GptStatus.InvalidBlockDevice
            && parseFailure.BlockStatus == BlockDeviceStatus.Success
            && selectionFailure.IsInitialized
            && !selectionFailure.Succeeded
            && selectionFailure.Status == GptStatus.EfiSystemPartitionNotFound
            && selectionFailure.BlockStatus == BlockDeviceStatus.Success
            && locationFailure.IsInitialized
            && !locationFailure.Succeeded
            && locationFailure.Status == GptStatus.InvalidPartitionEntryRange
            && locationFailure.BlockStatus == BlockDeviceStatus.Success);
    }

    [HostTest("storage gpt locates esp partition", HostLane.Fast, 8)]
    private static bool TestLocateEfiSystemPartition()
    {
        byte[] image = CreateValidGptImage();
        GptEfiSystemPartitionLocationResult result = GptParser.LocatePrimaryEfiSystemPartition(
            MemoryBlockDevice.FromImage(image, SectorSize));

        return HostCheck.Result(result.Succeeded
            && result.Status == GptStatus.Success
            && result.BlockStatus == BlockDeviceStatus.Success
            && result.Header.PartitionEntryCount == PartitionEntryCount
            && result.PartitionEntryIndex == 0
            && result.Partition.PartitionTypeGuid == GptParser.EfiSystemPartitionTypeGuid
            && result.Partition.UniquePartitionGuid == UniquePartitionGuid
            && result.PartitionStartLba == FirstPartitionLba
            && result.PartitionLastLba == LastPartitionLba
            && result.PartitionBlockCount == LastPartitionLba - FirstPartitionLba + 1
            && result.BlockSizeBytes == SectorSize
            && result.PartitionByteOffset == FirstPartitionLba * SectorSize
            && result.PartitionByteLength == (LastPartitionLba - FirstPartitionLba + 1) * SectorSize
            && result.Partition.Name == "OSDATA");
    }

    [HostTest("storage gpt selects non-first efi system partition", HostLane.Fast, 9)]
    private static bool TestSelectEfiSystemPartitionNonFirst()
    {
        byte[] image = CreateGptImageWithoutEntries();
        WritePartitionEntry(
            image,
            0,
            BasicDataPartitionGuid,
            UniquePartitionGuid,
            FirstPartitionLba,
            LastPartitionLba,
            attributes: 0,
            name: "DATA");
        WritePartitionEntry(
            image,
            1,
            EfiSystemPartitionGuid,
            SecondUniquePartitionGuid,
            firstLba: 62,
            lastLba: 80,
            attributes: PartitionAttributes,
            name: "EFI System");

        GptPartitionSelectionResult result = GptParser.SelectPrimaryEfiSystemPartition(
            MemoryBlockDevice.FromImage(image, SectorSize));

        return HostCheck.Result(result.Succeeded
            && result.PartitionEntryIndex == 1
            && result.Partition.PartitionTypeGuid == EfiSystemPartitionGuid
            && result.Partition.UniquePartitionGuid == SecondUniquePartitionGuid
            && result.FirstLba == 62
            && result.LastLba == 80
            && result.Partition.Attributes == PartitionAttributes
            && result.Partition.Name == "EFI System");
    }

    [HostTest("storage gpt rejects missing esp partition", HostLane.Fast, 10)]
    private static bool TestMissingEfiSystemPartition()
    {
        byte[] image = CreateGptImageWithoutEntries();
        WritePartitionEntry(
            image,
            0,
            BasicDataPartitionGuid,
            UniquePartitionGuid,
            FirstPartitionLba,
            LastPartitionLba,
            attributes: 0,
            name: "DATA");

        GptEfiSystemPartitionLocationResult result = GptParser.LocatePrimaryEfiSystemPartition(
            MemoryBlockDevice.FromImage(image, SectorSize));

        return HostCheck.Result(!result.Succeeded
            && result.Status == GptStatus.EfiSystemPartitionNotFound);
    }

    [HostTest("storage gpt skips empty entries while selecting esp", HostLane.Fast, 11)]
    private static bool TestEmptyEntriesSkipped()
    {
        byte[] image = CreateGptImageWithoutEntries();
        WritePartitionEntry(
            image,
            2,
            EfiSystemPartitionGuid,
            SecondUniquePartitionGuid,
            firstLba: 62,
            lastLba: 80,
            attributes: PartitionAttributes,
            name: "ESP2");

        GptPartitionSelectionResult result = GptParser.SelectPrimaryEfiSystemPartition(
            MemoryBlockDevice.FromImage(image, SectorSize));

        return HostCheck.Result(result.Succeeded
            && result.PartitionEntryIndex == 2
            && result.FirstLba == 62
            && result.LastLba == 80
            && result.Partition.Name == "ESP2");
    }

    [HostTest("storage gpt reports esp entry array short read", HostLane.Fast, 12)]
    private static bool TestEfiSystemPartitionEntryShortRead()
    {
        byte[] image = CreateGptImageWithoutEntries();
        WritePartitionEntry(
            image,
            4,
            EfiSystemPartitionGuid,
            SecondUniquePartitionGuid,
            firstLba: 62,
            lastLba: 80,
            attributes: PartitionAttributes,
            name: "ESP4");

        GptPartitionSelectionResult result = GptParser.SelectPrimaryEfiSystemPartition(new ShortReadBlockDevice(
            MemoryBlockDevice.FromImage(image, SectorSize),
            shortReadLba: PartitionEntryLba + 1));

        return HostCheck.Result(!result.Succeeded
            && result.Status == GptStatus.PartitionEntryShortRead);
    }

    [HostTest("storage gpt rejects invalid esp lba range", HostLane.Fast, 13)]
    private static bool TestEfiSystemPartitionInvalidLbaRange()
    {
        byte[] image = CreateGptImageWithoutEntries();
        WritePartitionEntry(
            image,
            0,
            EfiSystemPartitionGuid,
            UniquePartitionGuid,
            firstLba: 80,
            lastLba: 79,
            attributes: PartitionAttributes,
            name: "BADESP");

        GptEfiSystemPartitionLocationResult result = GptParser.LocatePrimaryEfiSystemPartition(
            MemoryBlockDevice.FromImage(image, SectorSize));

        return HostCheck.Result(!result.Succeeded
            && result.Status == GptStatus.InvalidPartitionEntryRange);
    }

    [HostTest("storage gpt parser uses shared memory helpers", HostLane.Fast, 14)]
    private static bool TestParserUsesSharedMemoryHelpers()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(
            Path.Combine(repositoryRoot, "src", "OS.Kernel", "Storage", "GptParser.cs"));

        return HostCheck.Result(source.Contains(
                "OS.Std.MemoryExtensions.SequenceEqual(bytes[..8], \"EFI PART\"u8)",
                StringComparison.Ordinal)
            && source.Contains(
                "ReadOnlySpan<byte> zeroGuid = stackalloc byte[16]",
                StringComparison.Ordinal)
            && source.Contains(
                "OS.Std.MemoryExtensions.SequenceEqual(bytes, zeroGuid)",
                StringComparison.Ordinal)
            && !source.Contains(
                "bytes[0] == (byte)'E'",
                StringComparison.Ordinal)
            && !source.Contains(
                "bytes[7] == (byte)'T'",
                StringComparison.Ordinal)
            && !source.Contains(
                "for (int i = 0; i < 16; i++)",
                StringComparison.Ordinal)
            && !source.Contains(
                "bytes[i] != 0",
                StringComparison.Ordinal));
    }

    private static byte[] CreateValidGptImage()
    {
        byte[] image = CreateGptImageWithoutEntries();
        WritePartitionEntry(
            image,
            0,
            EfiSystemPartitionGuid,
            UniquePartitionGuid,
            FirstPartitionLba,
            LastPartitionLba,
            PartitionAttributes,
            "OSDATA");
        return image;
    }

    private static byte[] CreateGptImageWithoutEntries()
    {
        byte[] image = new byte[checked((int)(SectorSize * BlockCount))];
        int headerOffset = checked((int)SectorSize);

        image[headerOffset] = (byte)'E';
        image[headerOffset + 1] = (byte)'F';
        image[headerOffset + 2] = (byte)'I';
        image[headerOffset + 3] = (byte)' ';
        image[headerOffset + 4] = (byte)'P';
        image[headerOffset + 5] = (byte)'A';
        image[headerOffset + 6] = (byte)'R';
        image[headerOffset + 7] = (byte)'T';

        WriteUInt32(image, headerOffset + 8, 0x00010000);
        WriteUInt32(image, headerOffset + 12, GptParser.MinimumHeaderSize);
        WriteUInt64(image, headerOffset + 24, 1);
        WriteUInt64(image, headerOffset + 32, BlockCount - 1);
        WriteUInt64(image, headerOffset + 40, FirstUsableLba);
        WriteUInt64(image, headerOffset + 48, LastUsableLba);
        WriteGuid(image, headerOffset + 56, DiskGuid);
        WriteUInt64(image, headerOffset + 72, PartitionEntryLba);
        WriteUInt32(image, headerOffset + 80, PartitionEntryCount);
        WriteUInt32(image, headerOffset + 84, PartitionEntrySize);

        return image;
    }

    private static void WritePartitionEntry(
        byte[] image,
        uint entryIndex,
        Guid partitionTypeGuid,
        Guid uniquePartitionGuid,
        ulong firstLba,
        ulong lastLba,
        ulong attributes,
        string name)
    {
        ulong byteOffset = (PartitionEntryLba * SectorSize) + ((ulong)entryIndex * PartitionEntrySize);
        int entryOffset = checked((int)byteOffset);
        WriteGuid(image, entryOffset, partitionTypeGuid);
        WriteGuid(image, entryOffset + 16, uniquePartitionGuid);
        WriteUInt64(image, entryOffset + 32, firstLba);
        WriteUInt64(image, entryOffset + 40, lastLba);
        WriteUInt64(image, entryOffset + 48, attributes);
        WriteUtf16Name(image, entryOffset + 56, name);
    }

    private static void WriteGuid(byte[] image, int offset, Guid value)
    {
        if (!value.TryWriteBytes(image.AsSpan(offset, 16)))
        {
            throw new InvalidOperationException("Could not write test GUID.");
        }
    }

    private static void WriteUtf16Name(byte[] image, int offset, string value)
    {
        for (int i = 0; i < value.Length; i++)
        {
            EndianPrimitives.WriteUInt16LittleEndian(image.AsSpan(offset + (i * 2), 2), (ushort)value[i]);
        }
    }

    private static void WriteUInt32(byte[] image, int offset, uint value)
    {
        EndianPrimitives.WriteUInt32LittleEndian(image.AsSpan(offset, 4), value);
    }

    private static void WriteUInt64(byte[] image, int offset, ulong value)
    {
        EndianPrimitives.WriteUInt64LittleEndian(image.AsSpan(offset, 8), value);
    }



}
