using OS.Std.Binary;

namespace OS.Storage;

public static partial class GptParser
{
    public const uint MinimumHeaderSize = 92;
    public const uint MinimumPartitionEntrySize = 128;
    public const uint MaximumSupportedBlockSize = 4096;

    public static readonly Guid EfiSystemPartitionTypeGuid = new Guid("c12a7328-f81f-11d2-ba4b-00a0c93ec93b");

    private const ulong PrimaryHeaderLba = 1;

    public static GptParseResult ReadPrimaryFirstPartition(IReadOnlyBlockDevice? device)
    {
        if (device is null)
        {
            return GptParseResult.Fail(GptStatus.InvalidBlockDevice);
        }

        GptStatus headerStatus = TryReadPrimaryHeader(
            device,
            out BlockDeviceInfo info,
            out GptHeader header,
            out BlockDeviceStatus blockStatus);
        if (headerStatus != GptStatus.Success)
        {
            return GptParseResult.Fail(headerStatus, blockStatus);
        }

        int blockSize = checked((int)info.BlockSize);
        byte[] entryWindow = new byte[checked(blockSize * 2)];
        GptStatus entryStatus = TryReadPartitionEntry(
            device,
            info,
            header,
            0,
            entryWindow,
            out GptPartitionEntry entry,
            out bool isEmpty,
            out blockStatus);
        if (entryStatus != GptStatus.Success)
        {
            return GptParseResult.Fail(entryStatus, blockStatus);
        }

        if (isEmpty)
        {
            return GptParseResult.Fail(GptStatus.MissingPartitionEntry);
        }

        return GptParseResult.Success(header, entry);
    }

    public static GptPartitionSelectionResult SelectPrimaryEfiSystemPartition(IReadOnlyBlockDevice? device)
    {
        PrimaryEfiSystemPartitionSelection selection = SelectPrimaryEfiSystemPartitionCore(device);
        if (!selection.Succeeded)
        {
            return GptPartitionSelectionResult.Fail(selection.Status, selection.BlockStatus);
        }

        return GptPartitionSelectionResult.Success(
            selection.Header,
            selection.Partition,
            selection.PartitionEntryIndex);
    }

    public static GptEfiSystemPartitionLocationResult LocatePrimaryEfiSystemPartition(IReadOnlyBlockDevice? device)
    {
        PrimaryEfiSystemPartitionSelection selection = SelectPrimaryEfiSystemPartitionCore(device);
        if (!selection.Succeeded)
        {
            return GptEfiSystemPartitionLocationResult.Fail(selection.Status, selection.BlockStatus);
        }

        return GptEfiSystemPartitionLocationResult.Success(
            selection.Header,
            selection.Partition,
            selection.PartitionEntryIndex,
            selection.BlockSizeBytes);
    }

    private static PrimaryEfiSystemPartitionSelection SelectPrimaryEfiSystemPartitionCore(
        IReadOnlyBlockDevice? device)
    {
        if (device is null)
        {
            return PrimaryEfiSystemPartitionSelection.Fail(GptStatus.InvalidBlockDevice);
        }

        GptStatus headerStatus = TryReadPrimaryHeader(
            device,
            out BlockDeviceInfo info,
            out GptHeader header,
            out BlockDeviceStatus blockStatus);
        if (headerStatus != GptStatus.Success)
        {
            return PrimaryEfiSystemPartitionSelection.Fail(headerStatus, blockStatus);
        }

        int blockSize = checked((int)info.BlockSize);
        byte[] entryWindow = new byte[checked(blockSize * 2)];
        for (uint entryIndex = 0; entryIndex < header.PartitionEntryCount; entryIndex++)
        {
            GptStatus entryStatus = TryReadPartitionEntry(
                device,
                info,
                header,
                entryIndex,
                entryWindow,
                out GptPartitionEntry entry,
                out bool isEmpty,
                out blockStatus);
            if (entryStatus != GptStatus.Success)
            {
                return PrimaryEfiSystemPartitionSelection.Fail(entryStatus, blockStatus);
            }

            if (isEmpty)
            {
                continue;
            }

            if (entry.PartitionTypeGuid == EfiSystemPartitionTypeGuid)
            {
                return PrimaryEfiSystemPartitionSelection.Success(
                    header,
                    entry,
                    entryIndex,
                    info.BlockSize);
            }
        }

        return PrimaryEfiSystemPartitionSelection.Fail(GptStatus.EfiSystemPartitionNotFound);
    }

    private static GptStatus TryReadPrimaryHeader(
        IReadOnlyBlockDevice device,
        out BlockDeviceInfo info,
        out GptHeader header,
        out BlockDeviceStatus blockStatus)
    {
        info = device.Info;
        header = default;
        blockStatus = BlockDeviceStatus.Success;

        if (!IsGeometrySupported(info))
        {
            return GptStatus.UnsupportedGeometry;
        }

        int blockSize = checked((int)info.BlockSize);
        byte[] headerBlock = new byte[blockSize];
        BlockReadResult headerRead = device.ReadBlocks(PrimaryHeaderLba, 1, headerBlock);
        if (!headerRead.Succeeded)
        {
            blockStatus = headerRead.Status;
            return GptStatus.PrimaryHeaderReadFailed;
        }

        if (headerRead.BlocksRead != 1 || headerRead.BytesRead != info.BlockSize)
        {
            blockStatus = headerRead.Status;
            return GptStatus.PrimaryHeaderShortRead;
        }

        ReadOnlySpan<byte> headerBytes = headerBlock;
        if (!HasGptSignature(headerBytes))
        {
            return GptStatus.InvalidHeaderSignature;
        }

        return TryParseHeader(headerBytes, info, out header);
    }

    private static bool HasGptSignature(ReadOnlySpan<byte> bytes)
    {
        return bytes.Length >= 8
            && OS.Std.MemoryExtensions.SequenceEqual(bytes[..8], "EFI PART"u8);
    }

    private static bool IsGeometrySupported(BlockDeviceInfo info)
    {
        return info.BlockSize >= BlockDeviceInfo.MinimumBlockSizeBytes
            && info.BlockSize <= MaximumSupportedBlockSize
            && (info.BlockSize % BlockDeviceInfo.MinimumBlockSizeBytes) == 0
            && info.BlockCount >= 3
            && info.CapacityBytes != 0;
    }

    private static GptStatus TryParseHeader(ReadOnlySpan<byte> headerBytes, BlockDeviceInfo info, out GptHeader header)
    {
        header = default;

        uint revision = EndianPrimitives.ReadUInt32LittleEndian(headerBytes.Slice(8, 4));
        uint headerSize = EndianPrimitives.ReadUInt32LittleEndian(headerBytes.Slice(12, 4));
        if (headerSize < MinimumHeaderSize || headerSize > info.BlockSize)
        {
            return GptStatus.InvalidHeaderSize;
        }

        ulong currentLba = EndianPrimitives.ReadUInt64LittleEndian(headerBytes.Slice(24, 8));
        ulong backupLba = EndianPrimitives.ReadUInt64LittleEndian(headerBytes.Slice(32, 8));
        ulong firstUsableLba = EndianPrimitives.ReadUInt64LittleEndian(headerBytes.Slice(40, 8));
        ulong lastUsableLba = EndianPrimitives.ReadUInt64LittleEndian(headerBytes.Slice(48, 8));
        Guid diskGuid = ReadGuid(headerBytes.Slice(56, 16));
        ulong partitionEntryLba = EndianPrimitives.ReadUInt64LittleEndian(headerBytes.Slice(72, 8));
        uint partitionEntryCount = EndianPrimitives.ReadUInt32LittleEndian(headerBytes.Slice(80, 4));
        uint partitionEntrySize = EndianPrimitives.ReadUInt32LittleEndian(headerBytes.Slice(84, 4));
        uint partitionEntryArrayCrc32 = EndianPrimitives.ReadUInt32LittleEndian(headerBytes.Slice(88, 4));

        if (currentLba != PrimaryHeaderLba || backupLba >= info.BlockCount)
        {
            return GptStatus.InvalidHeaderLba;
        }

        if (firstUsableLba > lastUsableLba || lastUsableLba >= info.BlockCount)
        {
            return GptStatus.InvalidUsableLbaRange;
        }

        if (partitionEntryCount == 0)
        {
            return GptStatus.InvalidPartitionEntryCount;
        }

        if (partitionEntrySize < MinimumPartitionEntrySize || (partitionEntrySize % 8) != 0)
        {
            return GptStatus.InvalidPartitionEntrySize;
        }

        if (partitionEntrySize > info.BlockSize)
        {
            return GptStatus.UnsupportedGeometry;
        }

        if (partitionEntryLba <= PrimaryHeaderLba || partitionEntryLba >= info.BlockCount)
        {
            return GptStatus.InvalidPartitionEntryArrayRange;
        }

        if (!IsPartitionArrayRangeValid(info, partitionEntryLba, partitionEntryCount, partitionEntrySize))
        {
            return GptStatus.InvalidPartitionEntryArrayRange;
        }

        header = new GptHeader(
            revision,
            headerSize,
            currentLba,
            backupLba,
            firstUsableLba,
            lastUsableLba,
            diskGuid,
            partitionEntryLba,
            partitionEntryCount,
            partitionEntrySize,
            partitionEntryArrayCrc32);
        return GptStatus.Success;
    }

    private static bool IsPartitionArrayRangeValid(
        BlockDeviceInfo info,
        ulong partitionEntryLba,
        uint partitionEntryCount,
        uint partitionEntrySize)
    {
        ulong entryCount = partitionEntryCount;
        ulong entrySize = partitionEntrySize;
        if (entryCount > ulong.MaxValue / entrySize)
        {
            return false;
        }

        ulong byteCount = entryCount * entrySize;
        ulong blockCount = (byteCount + info.BlockSize - 1) / info.BlockSize;
        return blockCount != 0
            && partitionEntryLba <= info.BlockCount
            && blockCount <= info.BlockCount - partitionEntryLba;
    }

    private static GptStatus TryReadPartitionEntry(
        IReadOnlyBlockDevice device,
        BlockDeviceInfo info,
        GptHeader header,
        uint entryIndex,
        Span<byte> entryWindow,
        out GptPartitionEntry entry,
        out bool isEmpty,
        out BlockDeviceStatus blockStatus)
    {
        entry = default;
        isEmpty = false;
        blockStatus = BlockDeviceStatus.Success;

        ulong entryByteOffset = (ulong)entryIndex * header.PartitionEntrySize;
        ulong blockOffset = entryByteOffset / info.BlockSize;
        ulong offsetInBlock = entryByteOffset % info.BlockSize;
        ulong requiredBytes = offsetInBlock + header.PartitionEntrySize;
        uint blocksToRead = requiredBytes > info.BlockSize ? 2U : 1U;
        int blockSize = checked((int)info.BlockSize);
        Span<byte> readWindow = entryWindow.Slice(0, checked(blockSize * (int)blocksToRead));

        BlockReadResult entryRead = device.ReadBlocks(
            header.PartitionEntryLba + blockOffset,
            blocksToRead,
            readWindow);
        if (!entryRead.Succeeded)
        {
            blockStatus = entryRead.Status;
            return GptStatus.PartitionEntryReadFailed;
        }

        if (entryRead.BlocksRead != blocksToRead || entryRead.BytesRead != (ulong)readWindow.Length)
        {
            blockStatus = entryRead.Status;
            return GptStatus.PartitionEntryShortRead;
        }

        ReadOnlySpan<byte> entryBytes = readWindow.Slice(
            checked((int)offsetInBlock),
            checked((int)header.PartitionEntrySize));
        return TryParsePartitionEntry(entryBytes, header, info, out entry, out isEmpty);
    }

    private static GptStatus TryParsePartitionEntry(
        ReadOnlySpan<byte> entryBytes,
        GptHeader header,
        BlockDeviceInfo info,
        out GptPartitionEntry entry,
        out bool isEmpty)
    {
        entry = default;
        isEmpty = false;

        ReadOnlySpan<byte> partitionTypeGuidBytes = entryBytes[..16];
        if (IsZeroGuid(partitionTypeGuidBytes))
        {
            isEmpty = true;
            return GptStatus.Success;
        }

        Guid partitionTypeGuid = ReadGuid(partitionTypeGuidBytes);
        Guid uniquePartitionGuid = ReadGuid(entryBytes.Slice(16, 16));
        ulong firstLba = EndianPrimitives.ReadUInt64LittleEndian(entryBytes.Slice(32, 8));
        ulong lastLba = EndianPrimitives.ReadUInt64LittleEndian(entryBytes.Slice(40, 8));
        ulong attributes = EndianPrimitives.ReadUInt64LittleEndian(entryBytes.Slice(48, 8));

        if (firstLba > lastLba
            || firstLba < header.FirstUsableLba
            || lastLba > header.LastUsableLba
            || lastLba >= info.BlockCount)
        {
            return GptStatus.InvalidPartitionEntryRange;
        }

        entry = new GptPartitionEntry(
            partitionTypeGuid,
            uniquePartitionGuid,
            firstLba,
            lastLba,
            attributes,
            ReadPartitionName(entryBytes.Slice(56, 72)));
        return GptStatus.Success;
    }

    private static bool IsZeroGuid(ReadOnlySpan<byte> bytes)
    {
        ReadOnlySpan<byte> zeroGuid = stackalloc byte[16]
        {
            0, 0, 0, 0,
            0, 0, 0, 0,
            0, 0, 0, 0,
            0, 0, 0, 0
        };
        return OS.Std.MemoryExtensions.SequenceEqual(bytes, zeroGuid);
    }

    private static Guid ReadGuid(ReadOnlySpan<byte> bytes)
    {
        return new Guid(bytes);
    }

    private static string ReadPartitionName(ReadOnlySpan<byte> bytes)
    {
        int length = 0;
        Span<char> chars = stackalloc char[36];
        for (int offset = 0; offset < bytes.Length; offset += 2)
        {
            ushort value = EndianPrimitives.ReadUInt16LittleEndian(bytes.Slice(offset, 2));
            if (value == 0)
            {
                break;
            }

            chars[length] = (char)value;
            length++;
        }

        return new string(chars[..length]);
    }

}
