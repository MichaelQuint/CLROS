using OS.Std.Binary;
using OS.Std.Hash;
using OS.Std.Memory;

namespace OS.Storage;

public enum GptPartitionTableStatus
{
    Success = 0,
    UnsupportedSectorSize = 1,
    InvalidBlockCount = 2,
    HeaderSectorTooSmall = 3,
    InvalidHeaderSignature = 4,
    UnsupportedRevision = 5,
    InvalidHeaderSize = 6,
    InvalidCurrentLba = 7,
    InvalidBackupLba = 8,
    InvalidUsableLbaRange = 9,
    InvalidPartitionEntryCount = 10,
    InvalidPartitionEntrySize = 11,
    InvalidPartitionEntryArrayRange = 12,
    PartitionEntryArrayTooLarge = 13,
    PartitionEntryBufferTooSmall = 14,
    DestinationTooSmall = 15,
    InvalidPartitionEntryRange = 16,
    HeaderCrcMismatch = 17,
    PartitionEntryArrayCrcMismatch = 18
}

public enum GptPartitionTableCrcStatus
{
    NotValidated = 0,
    Validated = 1,
    HeaderMismatch = 2,
    PartitionEntryArrayMismatch = 3
}

public readonly struct GptPartitionDescriptor
{
    public const int NameOffsetWithinEntryBytes = 56;
    public const int NameFieldLengthBytes = 72;
    public const int MaximumNameUtf16CodeUnits = NameFieldLengthBytes / sizeof(ushort);

    public GptPartitionDescriptor(
        uint entryIndex,
        int entryByteOffset,
        Guid partitionTypeGuid,
        Guid uniquePartitionGuid,
        ulong firstLba,
        ulong lastLba,
        ulong attributes,
        int nameByteOffset,
        int nameUtf16CodeUnitLength,
        bool nameIsNullTerminated)
    {
        EntryIndex = entryIndex;
        EntryByteOffset = entryByteOffset;
        PartitionTypeGuid = partitionTypeGuid;
        UniquePartitionGuid = uniquePartitionGuid;
        FirstLba = firstLba;
        LastLba = lastLba;
        Attributes = attributes;
        NameByteOffset = nameByteOffset;
        NameUtf16CodeUnitLength = nameUtf16CodeUnitLength;
        NameIsNullTerminated = nameIsNullTerminated;
    }

    public uint EntryIndex { get; }

    public int EntryByteOffset { get; }

    public Guid PartitionTypeGuid { get; }

    public Guid UniquePartitionGuid { get; }

    public ulong FirstLba { get; }

    public ulong LastLba { get; }

    public ulong Attributes { get; }

    public int NameByteOffset { get; }

    public int NameByteLength => NameFieldLengthBytes;

    public int NameUtf16CodeUnitLength { get; }

    public bool NameIsNullTerminated { get; }
}

public readonly struct GptPartitionTableParseResult
{
    private GptPartitionTableParseResult(
        GptPartitionTableStatus status,
        GptPartitionTableCrcStatus crcStatus,
        GptHeader header,
        int partitionsWritten,
        uint emptyEntriesSkipped,
        ulong partitionEntryBytesRequired,
        ulong partitionEntrySectorsRequired,
        uint failingEntryIndex,
        uint expectedHeaderCrc32,
        uint computedHeaderCrc32,
        uint expectedPartitionEntryArrayCrc32,
        uint computedPartitionEntryArrayCrc32)
    {
        IsInitialized = true;
        Status = status;
        CrcStatus = crcStatus;
        Header = header;
        PartitionsWritten = partitionsWritten;
        EmptyEntriesSkipped = emptyEntriesSkipped;
        PartitionEntryBytesRequired = partitionEntryBytesRequired;
        PartitionEntrySectorsRequired = partitionEntrySectorsRequired;
        FailingEntryIndex = failingEntryIndex;
        ExpectedHeaderCrc32 = expectedHeaderCrc32;
        ComputedHeaderCrc32 = computedHeaderCrc32;
        ExpectedPartitionEntryArrayCrc32 = expectedPartitionEntryArrayCrc32;
        ComputedPartitionEntryArrayCrc32 = computedPartitionEntryArrayCrc32;
    }

    public bool IsInitialized { get; }

    public GptPartitionTableStatus Status { get; }

    public GptPartitionTableCrcStatus CrcStatus { get; }

    public GptHeader Header { get; }

    public int PartitionsWritten { get; }

    public uint EmptyEntriesSkipped { get; }

    public ulong PartitionEntryBytesRequired { get; }

    public ulong PartitionEntrySectorsRequired { get; }

    public uint FailingEntryIndex { get; }

    public uint ExpectedHeaderCrc32 { get; }

    public uint ComputedHeaderCrc32 { get; }

    public uint ExpectedPartitionEntryArrayCrc32 { get; }

    public uint ComputedPartitionEntryArrayCrc32 { get; }

    public bool Succeeded => IsInitialized && Status == GptPartitionTableStatus.Success;

    public static GptPartitionTableParseResult Success(
        GptHeader header,
        int partitionsWritten,
        uint emptyEntriesSkipped,
        ulong partitionEntryBytesRequired,
        ulong partitionEntrySectorsRequired,
        uint expectedHeaderCrc32,
        uint computedHeaderCrc32,
        uint expectedPartitionEntryArrayCrc32,
        uint computedPartitionEntryArrayCrc32)
    {
        return new GptPartitionTableParseResult(
            GptPartitionTableStatus.Success,
            GptPartitionTableCrcStatus.Validated,
            header,
            partitionsWritten,
            emptyEntriesSkipped,
            partitionEntryBytesRequired,
            partitionEntrySectorsRequired,
            0,
            expectedHeaderCrc32,
            computedHeaderCrc32,
            expectedPartitionEntryArrayCrc32,
            computedPartitionEntryArrayCrc32);
    }

    public static GptPartitionTableParseResult Fail(
        GptPartitionTableStatus status,
        GptHeader header = default,
        int partitionsWritten = 0,
        uint emptyEntriesSkipped = 0,
        ulong partitionEntryBytesRequired = 0,
        ulong partitionEntrySectorsRequired = 0,
        uint failingEntryIndex = 0,
        GptPartitionTableCrcStatus crcStatus = GptPartitionTableCrcStatus.NotValidated,
        uint expectedHeaderCrc32 = 0,
        uint computedHeaderCrc32 = 0,
        uint expectedPartitionEntryArrayCrc32 = 0,
        uint computedPartitionEntryArrayCrc32 = 0)
    {
        return new GptPartitionTableParseResult(
            status,
            crcStatus,
            header,
            partitionsWritten,
            emptyEntriesSkipped,
            partitionEntryBytesRequired,
            partitionEntrySectorsRequired,
            failingEntryIndex,
            expectedHeaderCrc32,
            computedHeaderCrc32,
            expectedPartitionEntryArrayCrc32,
            computedPartitionEntryArrayCrc32);
    }
}

public static class GptPartitionTableParser
{
    public const uint PrimaryHeaderLba = 1;
    public const uint MinimumHeaderSize = GptParser.MinimumHeaderSize;
    public const uint MinimumPartitionEntrySize = GptParser.MinimumPartitionEntrySize;
    public const uint MinimumSectorSizeBytes = BlockDeviceInfo.MinimumBlockSizeBytes;
    public const uint MaximumSupportedSectorSizeBytes = GptParser.MaximumSupportedBlockSize;
    public const uint SupportedRevision = 0x00010000;
    public const bool ValidatesHeaderCrc32 = true;
    public const bool ValidatesPartitionEntryArrayCrc32 = true;

    public static GptPartitionTableParseResult ParsePrimary(
        ReadOnlySpan<byte> primaryHeaderSector,
        ReadOnlySpan<byte> partitionEntrySectors,
        ulong blockCount,
        uint sectorSizeBytes,
        Span<GptPartitionDescriptor> destination)
    {
        return Parse(
            primaryHeaderSector,
            partitionEntrySectors,
            blockCount,
            sectorSizeBytes,
            destination,
            GptPartitionTableKind.Primary);
    }

    public static GptPartitionTableParseResult ParseBackup(
        ReadOnlySpan<byte> backupHeaderSector,
        ReadOnlySpan<byte> partitionEntrySectors,
        ulong blockCount,
        uint sectorSizeBytes,
        Span<GptPartitionDescriptor> destination)
    {
        return Parse(
            backupHeaderSector,
            partitionEntrySectors,
            blockCount,
            sectorSizeBytes,
            destination,
            GptPartitionTableKind.Backup);
    }

    private enum GptPartitionTableKind
    {
        Primary = 0,
        Backup = 1
    }

    private static GptPartitionTableParseResult Parse(
        ReadOnlySpan<byte> headerSector,
        ReadOnlySpan<byte> partitionEntrySectors,
        ulong blockCount,
        uint sectorSizeBytes,
        Span<GptPartitionDescriptor> destination,
        GptPartitionTableKind tableKind)
    {
        if (!IsSectorSizeSupported(sectorSizeBytes))
        {
            return GptPartitionTableParseResult.Fail(GptPartitionTableStatus.UnsupportedSectorSize);
        }

        if (blockCount < 3)
        {
            return GptPartitionTableParseResult.Fail(GptPartitionTableStatus.InvalidBlockCount);
        }

        if (headerSector.Length < sectorSizeBytes || headerSector.Length < MinimumHeaderSize)
        {
            return GptPartitionTableParseResult.Fail(GptPartitionTableStatus.HeaderSectorTooSmall);
        }

        if (!HasGptSignature(headerSector))
        {
            return GptPartitionTableParseResult.Fail(GptPartitionTableStatus.InvalidHeaderSignature);
        }

        if (!TryReadHeader(headerSector, out GptHeader header, out uint expectedHeaderCrc32))
        {
            return GptPartitionTableParseResult.Fail(GptPartitionTableStatus.HeaderSectorTooSmall);
        }

        if (header.Revision != SupportedRevision)
        {
            return GptPartitionTableParseResult.Fail(GptPartitionTableStatus.UnsupportedRevision, header);
        }

        if (header.HeaderSize < MinimumHeaderSize
            || header.HeaderSize > sectorSizeBytes
            || header.HeaderSize > headerSector.Length)
        {
            return GptPartitionTableParseResult.Fail(GptPartitionTableStatus.InvalidHeaderSize, header);
        }

        if (!IsCurrentLbaValid(header, blockCount, tableKind))
        {
            return GptPartitionTableParseResult.Fail(GptPartitionTableStatus.InvalidCurrentLba, header);
        }

        if (!IsBackupLbaValid(header, blockCount, tableKind))
        {
            return GptPartitionTableParseResult.Fail(GptPartitionTableStatus.InvalidBackupLba, header);
        }

        if (header.FirstUsableLba > header.LastUsableLba
            || header.FirstUsableLba <= PrimaryHeaderLba
            || header.LastUsableLba >= blockCount)
        {
            return GptPartitionTableParseResult.Fail(GptPartitionTableStatus.InvalidUsableLbaRange, header);
        }

        if (header.PartitionEntryCount == 0)
        {
            return GptPartitionTableParseResult.Fail(GptPartitionTableStatus.InvalidPartitionEntryCount, header);
        }

        if (header.PartitionEntrySize < MinimumPartitionEntrySize
            || (header.PartitionEntrySize % 8) != 0)
        {
            return GptPartitionTableParseResult.Fail(GptPartitionTableStatus.InvalidPartitionEntrySize, header);
        }

        if (!IsPartitionEntryArrayLbaValid(header, blockCount, tableKind))
        {
            return GptPartitionTableParseResult.Fail(GptPartitionTableStatus.InvalidPartitionEntryArrayRange, header);
        }

        if (!TryComputePartitionEntryArrayBounds(
            header,
            blockCount,
            sectorSizeBytes,
            out ulong requiredEntryBytes,
            out ulong requiredEntrySectors))
        {
            return GptPartitionTableParseResult.Fail(GptPartitionTableStatus.InvalidPartitionEntryArrayRange, header);
        }

        if (!IsPartitionEntryArrayPlacementValid(header, requiredEntrySectors, tableKind))
        {
            return GptPartitionTableParseResult.Fail(
                GptPartitionTableStatus.InvalidPartitionEntryArrayRange,
                header,
                partitionEntryBytesRequired: requiredEntryBytes,
                partitionEntrySectorsRequired: requiredEntrySectors);
        }

        if (requiredEntryBytes > int.MaxValue)
        {
            return GptPartitionTableParseResult.Fail(
                GptPartitionTableStatus.PartitionEntryArrayTooLarge,
                header,
                partitionEntryBytesRequired: requiredEntryBytes,
                partitionEntrySectorsRequired: requiredEntrySectors);
        }

        if ((ulong)partitionEntrySectors.Length < requiredEntryBytes)
        {
            return GptPartitionTableParseResult.Fail(
                GptPartitionTableStatus.PartitionEntryBufferTooSmall,
                header,
                partitionEntryBytesRequired: requiredEntryBytes,
                partitionEntrySectorsRequired: requiredEntrySectors);
        }

        if (!TryComputeHeaderCrc32(headerSector, header.HeaderSize, out uint computedHeaderCrc32))
        {
            return GptPartitionTableParseResult.Fail(
                GptPartitionTableStatus.HeaderSectorTooSmall,
                header: header,
                partitionEntryBytesRequired: requiredEntryBytes,
                partitionEntrySectorsRequired: requiredEntrySectors,
                expectedHeaderCrc32: expectedHeaderCrc32,
                expectedPartitionEntryArrayCrc32: header.PartitionEntryArrayCrc32);
        }

        if (computedHeaderCrc32 != expectedHeaderCrc32)
        {
            return GptPartitionTableParseResult.Fail(
                GptPartitionTableStatus.HeaderCrcMismatch,
                header: header,
                crcStatus: GptPartitionTableCrcStatus.HeaderMismatch,
                partitionEntryBytesRequired: requiredEntryBytes,
                partitionEntrySectorsRequired: requiredEntrySectors,
                expectedHeaderCrc32: expectedHeaderCrc32,
                computedHeaderCrc32: computedHeaderCrc32,
                expectedPartitionEntryArrayCrc32: header.PartitionEntryArrayCrc32);
        }

        uint computedPartitionEntryArrayCrc32 =
            Crc32.Compute(partitionEntrySectors.Slice(0, checked((int)requiredEntryBytes)));
        if (computedPartitionEntryArrayCrc32 != header.PartitionEntryArrayCrc32)
        {
            return GptPartitionTableParseResult.Fail(
                GptPartitionTableStatus.PartitionEntryArrayCrcMismatch,
                header: header,
                crcStatus: GptPartitionTableCrcStatus.PartitionEntryArrayMismatch,
                partitionEntryBytesRequired: requiredEntryBytes,
                partitionEntrySectorsRequired: requiredEntrySectors,
                expectedHeaderCrc32: expectedHeaderCrc32,
                computedHeaderCrc32: computedHeaderCrc32,
                expectedPartitionEntryArrayCrc32: header.PartitionEntryArrayCrc32,
                computedPartitionEntryArrayCrc32: computedPartitionEntryArrayCrc32);
        }

        int partitionsWritten = 0;
        uint emptyEntriesSkipped = 0;
        int entrySize = checked((int)header.PartitionEntrySize);
        for (uint entryIndex = 0; entryIndex < header.PartitionEntryCount; entryIndex++)
        {
            int entryByteOffset = checked((int)((ulong)entryIndex * header.PartitionEntrySize));
            ReadOnlySpan<byte> entryBytes = partitionEntrySectors.Slice(entryByteOffset, entrySize);
            if (IsZeroGuid(entryBytes[..16]))
            {
                emptyEntriesSkipped++;
                continue;
            }

            if (!TryReadPartitionDescriptor(
                entryBytes,
                entryIndex,
                entryByteOffset,
                header,
                blockCount,
                out GptPartitionDescriptor descriptor))
            {
                return GptPartitionTableParseResult.Fail(
                    GptPartitionTableStatus.InvalidPartitionEntryRange,
                    header: header,
                    partitionsWritten: partitionsWritten,
                    emptyEntriesSkipped: emptyEntriesSkipped,
                    partitionEntryBytesRequired: requiredEntryBytes,
                    partitionEntrySectorsRequired: requiredEntrySectors,
                    failingEntryIndex: entryIndex,
                    crcStatus: GptPartitionTableCrcStatus.Validated,
                    expectedHeaderCrc32: expectedHeaderCrc32,
                    computedHeaderCrc32: computedHeaderCrc32,
                    expectedPartitionEntryArrayCrc32: header.PartitionEntryArrayCrc32,
                    computedPartitionEntryArrayCrc32: computedPartitionEntryArrayCrc32);
            }

            if (partitionsWritten >= destination.Length)
            {
                return GptPartitionTableParseResult.Fail(
                    GptPartitionTableStatus.DestinationTooSmall,
                    header: header,
                    partitionsWritten: partitionsWritten,
                    emptyEntriesSkipped: emptyEntriesSkipped,
                    partitionEntryBytesRequired: requiredEntryBytes,
                    partitionEntrySectorsRequired: requiredEntrySectors,
                    failingEntryIndex: entryIndex,
                    crcStatus: GptPartitionTableCrcStatus.Validated,
                    expectedHeaderCrc32: expectedHeaderCrc32,
                    computedHeaderCrc32: computedHeaderCrc32,
                    expectedPartitionEntryArrayCrc32: header.PartitionEntryArrayCrc32,
                    computedPartitionEntryArrayCrc32: computedPartitionEntryArrayCrc32);
            }

            destination[partitionsWritten] = descriptor;
            partitionsWritten++;
        }

        return GptPartitionTableParseResult.Success(
            header,
            partitionsWritten,
            emptyEntriesSkipped,
            requiredEntryBytes,
            requiredEntrySectors,
            expectedHeaderCrc32,
            computedHeaderCrc32,
            header.PartitionEntryArrayCrc32,
            computedPartitionEntryArrayCrc32);
    }

    private static bool IsSectorSizeSupported(uint sectorSizeBytes)
    {
        return sectorSizeBytes >= MinimumSectorSizeBytes
            && sectorSizeBytes <= MaximumSupportedSectorSizeBytes
            && (sectorSizeBytes % MinimumSectorSizeBytes) == 0;
    }

    private static bool IsCurrentLbaValid(
        GptHeader header,
        ulong blockCount,
        GptPartitionTableKind tableKind)
    {
        return tableKind == GptPartitionTableKind.Primary
            ? header.CurrentLba == PrimaryHeaderLba
            : header.CurrentLba == blockCount - 1;
    }

    private static bool IsBackupLbaValid(
        GptHeader header,
        ulong blockCount,
        GptPartitionTableKind tableKind)
    {
        if (header.BackupLba >= blockCount || header.BackupLba == header.CurrentLba)
        {
            return false;
        }

        return tableKind == GptPartitionTableKind.Primary
            || header.BackupLba == PrimaryHeaderLba;
    }

    private static bool IsPartitionEntryArrayLbaValid(
        GptHeader header,
        ulong blockCount,
        GptPartitionTableKind tableKind)
    {
        if (header.PartitionEntryLba >= blockCount)
        {
            return false;
        }

        if (tableKind == GptPartitionTableKind.Primary)
        {
            return header.PartitionEntryLba > header.CurrentLba;
        }

        return header.PartitionEntryLba > header.BackupLba
            && header.PartitionEntryLba < header.CurrentLba;
    }

    private static bool IsPartitionEntryArrayPlacementValid(
        GptHeader header,
        ulong requiredEntrySectors,
        GptPartitionTableKind tableKind)
    {
        if (tableKind == GptPartitionTableKind.Primary)
        {
            return true;
        }

        if (requiredEntrySectors == 0 || header.CurrentLba < requiredEntrySectors)
        {
            return false;
        }

        return header.PartitionEntryLba == header.CurrentLba - requiredEntrySectors;
    }

    private static bool HasGptSignature(ReadOnlySpan<byte> bytes)
    {
        ReadOnlySpan<byte> signature = "EFI PART"u8;
        return OS.Std.MemoryExtensions.SequenceEqual(bytes.Slice(0, signature.Length), signature);
    }

    private static bool TryReadHeader(ReadOnlySpan<byte> bytes, out GptHeader header, out uint headerCrc32)
    {
        header = default;
        headerCrc32 = 0;

        if (!EndianPrimitives.TryReadUInt32LittleEndian(bytes.Slice(8, 4), out uint revision)
            || !EndianPrimitives.TryReadUInt32LittleEndian(bytes.Slice(12, 4), out uint headerSize)
            || !EndianPrimitives.TryReadUInt32LittleEndian(bytes.Slice(16, 4), out headerCrc32)
            || !EndianPrimitives.TryReadUInt64LittleEndian(bytes.Slice(24, 8), out ulong currentLba)
            || !EndianPrimitives.TryReadUInt64LittleEndian(bytes.Slice(32, 8), out ulong backupLba)
            || !EndianPrimitives.TryReadUInt64LittleEndian(bytes.Slice(40, 8), out ulong firstUsableLba)
            || !EndianPrimitives.TryReadUInt64LittleEndian(bytes.Slice(48, 8), out ulong lastUsableLba)
            || !EndianPrimitives.TryReadUInt64LittleEndian(bytes.Slice(72, 8), out ulong partitionEntryLba)
            || !EndianPrimitives.TryReadUInt32LittleEndian(bytes.Slice(80, 4), out uint partitionEntryCount)
            || !EndianPrimitives.TryReadUInt32LittleEndian(bytes.Slice(84, 4), out uint partitionEntrySize)
            || !EndianPrimitives.TryReadUInt32LittleEndian(bytes.Slice(88, 4), out uint partitionEntryArrayCrc32))
        {
            return false;
        }

        header = new GptHeader(
            revision,
            headerSize,
            currentLba,
            backupLba,
            firstUsableLba,
            lastUsableLba,
            new Guid(bytes.Slice(56, 16)),
            partitionEntryLba,
            partitionEntryCount,
            partitionEntrySize,
            partitionEntryArrayCrc32);
        return true;
    }

    private static bool TryComputeHeaderCrc32(
        ReadOnlySpan<byte> primaryHeaderSector,
        uint headerSize,
        out uint crc32)
    {
        crc32 = 0;

        // GPT requires the header CRC field itself to be zero while hashing.
        // Header size is bounded to the supported sector size before this path, so stack copy is bounded to 4096 bytes.
        Span<byte> headerBytes = stackalloc byte[checked((int)headerSize)];
        BufferOperationResult copy = BufferOperations.MemoryCopy(
            primaryHeaderSector.Slice(0, headerBytes.Length),
            headerBytes);
        if (!copy.Succeeded)
        {
            return false;
        }

        EndianPrimitives.WriteUInt32LittleEndian(headerBytes.Slice(16, 4), 0);
        crc32 = Crc32.Compute(headerBytes);
        return true;
    }

    private static bool TryComputePartitionEntryArrayBounds(
        GptHeader header,
        ulong blockCount,
        uint sectorSizeBytes,
        out ulong requiredEntryBytes,
        out ulong requiredEntrySectors)
    {
        requiredEntryBytes = 0;
        requiredEntrySectors = 0;

        ulong entryCount = header.PartitionEntryCount;
        ulong entrySize = header.PartitionEntrySize;
        if (entrySize != 0 && entryCount > ulong.MaxValue / entrySize)
        {
            return false;
        }

        requiredEntryBytes = entryCount * entrySize;
        requiredEntrySectors = (requiredEntryBytes + sectorSizeBytes - 1) / sectorSizeBytes;
        if (requiredEntrySectors == 0)
        {
            return false;
        }

        return header.PartitionEntryLba <= blockCount
            && requiredEntrySectors <= blockCount - header.PartitionEntryLba;
    }

    private static bool TryReadPartitionDescriptor(
        ReadOnlySpan<byte> entryBytes,
        uint entryIndex,
        int entryByteOffset,
        GptHeader header,
        ulong blockCount,
        out GptPartitionDescriptor descriptor)
    {
        descriptor = default;

        ulong firstLba = EndianPrimitives.ReadUInt64LittleEndian(entryBytes.Slice(32, 8));
        ulong lastLba = EndianPrimitives.ReadUInt64LittleEndian(entryBytes.Slice(40, 8));
        if (firstLba > lastLba
            || firstLba < header.FirstUsableLba
            || lastLba > header.LastUsableLba
            || lastLba >= blockCount)
        {
            return false;
        }

        int nameByteOffset = entryByteOffset + GptPartitionDescriptor.NameOffsetWithinEntryBytes;
        descriptor = new GptPartitionDescriptor(
            entryIndex,
            entryByteOffset,
            new Guid(entryBytes[..16]),
            new Guid(entryBytes.Slice(16, 16)),
            firstLba,
            lastLba,
            EndianPrimitives.ReadUInt64LittleEndian(entryBytes.Slice(48, 8)),
            nameByteOffset,
            GetUtf16NameCodeUnitLength(
                entryBytes.Slice(
                    GptPartitionDescriptor.NameOffsetWithinEntryBytes,
                    GptPartitionDescriptor.NameFieldLengthBytes),
                out bool nameIsNullTerminated),
            nameIsNullTerminated);
        return true;
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

    private static int GetUtf16NameCodeUnitLength(ReadOnlySpan<byte> nameBytes, out bool isNullTerminated)
    {
        for (int offset = 0; offset < nameBytes.Length; offset += sizeof(ushort))
        {
            ushort value = EndianPrimitives.ReadUInt16LittleEndian(nameBytes.Slice(offset, sizeof(ushort)));
            if (value == 0)
            {
                isNullTerminated = true;
                return offset / sizeof(ushort);
            }
        }

        isNullTerminated = false;
        return GptPartitionDescriptor.MaximumNameUtf16CodeUnits;
    }
}
