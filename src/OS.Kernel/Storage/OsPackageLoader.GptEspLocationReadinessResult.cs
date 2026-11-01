namespace OS.Storage;

public readonly struct GptEspLocationReadinessResult
{
    private GptEspLocationReadinessResult(
        GptEspLocationReadinessStatus status,
        GptStatus gptStatus,
        GptPartitionTableStatus gptTableStatus,
        GptPartitionTableCrcStatus gptCrcStatus,
        BlockDeviceStatus blockStatus,
        GptHeader header,
        GptPartitionEntry partition,
        uint partitionEntryIndex,
        uint blockSizeBytes,
        ulong partitionEntryBytesRequired,
        ulong partitionEntrySectorBytesRequired,
        uint expectedHeaderCrc32,
        uint computedHeaderCrc32,
        uint expectedPartitionEntryArrayCrc32,
        uint computedPartitionEntryArrayCrc32,
        ulong headerBytesRequired = 0,
        ulong headerBufferBytesAvailable = 0,
        ulong partitionEntryBufferBytesAvailable = 0,
        GptPartitionTableSource tableSource = GptPartitionTableSource.Unknown)
    {
        IsInitialized = true;
        Status = status;
        GptStatus = gptStatus;
        GptTableStatus = gptTableStatus;
        GptCrcStatus = gptCrcStatus;
        BlockStatus = blockStatus;
        Header = header;
        Partition = partition;
        PartitionEntryIndex = partitionEntryIndex;
        BlockSizeBytes = blockSizeBytes;
        PartitionEntryBytesRequired = partitionEntryBytesRequired;
        PartitionEntrySectorBytesRequired = partitionEntrySectorBytesRequired;
        HeaderBytesRequired = headerBytesRequired;
        HeaderBufferBytesAvailable = headerBufferBytesAvailable;
        PartitionEntryBufferBytesAvailable = partitionEntryBufferBytesAvailable;
        TableSource = tableSource;
        ExpectedHeaderCrc32 = expectedHeaderCrc32;
        ComputedHeaderCrc32 = computedHeaderCrc32;
        ExpectedPartitionEntryArrayCrc32 = expectedPartitionEntryArrayCrc32;
        ComputedPartitionEntryArrayCrc32 = computedPartitionEntryArrayCrc32;
    }

    public bool IsInitialized { get; }

    public GptEspLocationReadinessStatus Status { get; }

    public GptStatus GptStatus { get; }

    public GptPartitionTableStatus GptTableStatus { get; }

    public GptPartitionTableCrcStatus GptCrcStatus { get; }

    public BlockDeviceStatus BlockStatus { get; }

    public GptHeader Header { get; }

    public GptPartitionEntry Partition { get; }

    public uint PartitionEntryIndex { get; }

    public uint BlockSizeBytes { get; }

    public ulong PartitionStartLba => Partition.FirstLba;

    public ulong PartitionLastLba => Partition.LastLba;

    public ulong PartitionBlockCount => Succeeded
        ? Partition.LastLba - Partition.FirstLba + 1
        : 0;

    public ulong PartitionEntryBytesRequired { get; }

    public ulong PartitionEntrySectorBytesRequired { get; }

    public ulong HeaderBytesRequired { get; }

    public ulong HeaderBufferBytesAvailable { get; }

    public ulong PartitionEntryBufferBytesAvailable { get; }

    public GptPartitionTableSource TableSource { get; }

    public uint ExpectedHeaderCrc32 { get; }

    public uint ComputedHeaderCrc32 { get; }

    public uint ExpectedPartitionEntryArrayCrc32 { get; }

    public uint ComputedPartitionEntryArrayCrc32 { get; }

    public bool Succeeded => IsInitialized && Status == GptEspLocationReadinessStatus.Success;

    public static GptEspLocationReadinessResult Success(
        GptPartitionTableParseResult parse,
        GptPartitionDescriptor partition,
        uint blockSizeBytes,
        GptPartitionTableSource tableSource)
    {
        return new GptEspLocationReadinessResult(
            GptEspLocationReadinessStatus.Success,
            GptStatus.Success,
            parse.Status,
            parse.CrcStatus,
            BlockDeviceStatus.Success,
            parse.Header,
            new GptPartitionEntry(
                partition.PartitionTypeGuid,
                partition.UniquePartitionGuid,
                partition.FirstLba,
                partition.LastLba,
                partition.Attributes,
                string.Empty),
            partition.EntryIndex,
            blockSizeBytes,
            parse.PartitionEntryBytesRequired,
            parse.PartitionEntrySectorsRequired * blockSizeBytes,
            parse.ExpectedHeaderCrc32,
            parse.ComputedHeaderCrc32,
            parse.ExpectedPartitionEntryArrayCrc32,
            parse.ComputedPartitionEntryArrayCrc32,
            headerBytesRequired: blockSizeBytes,
            headerBufferBytesAvailable: blockSizeBytes,
            partitionEntryBufferBytesAvailable: parse.PartitionEntrySectorsRequired * blockSizeBytes,
            tableSource: tableSource);
    }

    public static GptEspLocationReadinessResult Fail(
        GptEspLocationReadinessStatus status,
        GptStatus gptStatus,
        BlockDeviceStatus blockStatus = BlockDeviceStatus.Success,
        ulong headerBytesRequired = 0,
        ulong headerBufferBytesAvailable = 0,
        ulong partitionEntryBufferBytesAvailable = 0,
        GptPartitionTableSource tableSource = GptPartitionTableSource.Unknown)
    {
        return new GptEspLocationReadinessResult(
            status,
            gptStatus,
            GptPartitionTableStatus.Success,
            GptPartitionTableCrcStatus.NotValidated,
            blockStatus,
            default,
            default,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            headerBytesRequired: headerBytesRequired,
            headerBufferBytesAvailable: headerBufferBytesAvailable,
            partitionEntryBufferBytesAvailable: partitionEntryBufferBytesAvailable,
            tableSource: tableSource);
    }

    public static GptEspLocationReadinessResult Fail(
        GptEspLocationReadinessStatus status,
        GptPartitionTableParseResult parse,
        BlockDeviceStatus blockStatus = BlockDeviceStatus.Success,
        GptPartitionTableSource tableSource = GptPartitionTableSource.Unknown)
    {
        return new GptEspLocationReadinessResult(
            status,
            MapGptTableStatus(parse.Status),
            parse.Status,
            parse.CrcStatus,
            blockStatus,
            parse.Header,
            default,
            parse.FailingEntryIndex,
            0,
            parse.PartitionEntryBytesRequired,
            0,
            parse.ExpectedHeaderCrc32,
            parse.ComputedHeaderCrc32,
            parse.ExpectedPartitionEntryArrayCrc32,
            parse.ComputedPartitionEntryArrayCrc32,
            headerBytesRequired: parse.Header.HeaderSize,
            tableSource: tableSource);
    }

    public static GptEspLocationReadinessResult FailMissingEsp(
        GptPartitionTableParseResult parse,
        ulong partitionEntrySectorBytesRequired,
        GptPartitionTableSource tableSource)
    {
        return new GptEspLocationReadinessResult(
            GptEspLocationReadinessStatus.EfiSystemPartitionNotFound,
            GptStatus.EfiSystemPartitionNotFound,
            parse.Status,
            parse.CrcStatus,
            BlockDeviceStatus.Success,
            parse.Header,
            default,
            0,
            0,
            parse.PartitionEntryBytesRequired,
            partitionEntrySectorBytesRequired,
            parse.ExpectedHeaderCrc32,
            parse.ComputedHeaderCrc32,
            parse.ExpectedPartitionEntryArrayCrc32,
            parse.ComputedPartitionEntryArrayCrc32,
            headerBytesRequired: parse.Header.HeaderSize,
            headerBufferBytesAvailable: parse.Header.HeaderSize,
            partitionEntryBufferBytesAvailable: partitionEntrySectorBytesRequired,
            tableSource: tableSource);
    }

    public static GptEspLocationReadinessResult FailPartitionEntryBufferTooSmall(
        GptPartitionTableParseResult planning,
        ulong partitionEntrySectorBytesRequired,
        ulong partitionEntryBufferBytesAvailable,
        ulong headerBytesRequired,
        ulong headerBufferBytesAvailable,
        GptPartitionTableSource tableSource)
    {
        return new GptEspLocationReadinessResult(
            GptEspLocationReadinessStatus.PartitionEntryBufferTooSmall,
            GptStatus.PartitionEntryShortRead,
            GptPartitionTableStatus.PartitionEntryBufferTooSmall,
            planning.CrcStatus,
            BlockDeviceStatus.DestinationTooSmall,
            planning.Header,
            default,
            0,
            0,
            planning.PartitionEntryBytesRequired,
            partitionEntrySectorBytesRequired,
            planning.ExpectedHeaderCrc32,
            planning.ComputedHeaderCrc32,
            planning.ExpectedPartitionEntryArrayCrc32,
            planning.ComputedPartitionEntryArrayCrc32,
            headerBytesRequired: headerBytesRequired,
            headerBufferBytesAvailable: headerBufferBytesAvailable,
            partitionEntryBufferBytesAvailable: partitionEntryBufferBytesAvailable,
            tableSource: tableSource);
    }

    private static GptStatus MapGptTableStatus(GptPartitionTableStatus status)
    {
        return status switch
        {
            GptPartitionTableStatus.Success => GptStatus.Success,
            GptPartitionTableStatus.UnsupportedSectorSize => GptStatus.UnsupportedGeometry,
            GptPartitionTableStatus.InvalidBlockCount => GptStatus.UnsupportedGeometry,
            GptPartitionTableStatus.HeaderSectorTooSmall => GptStatus.PrimaryHeaderShortRead,
            GptPartitionTableStatus.InvalidHeaderSignature => GptStatus.InvalidHeaderSignature,
            GptPartitionTableStatus.UnsupportedRevision => GptStatus.InvalidHeaderSize,
            GptPartitionTableStatus.InvalidHeaderSize => GptStatus.InvalidHeaderSize,
            GptPartitionTableStatus.InvalidCurrentLba => GptStatus.InvalidHeaderLba,
            GptPartitionTableStatus.InvalidBackupLba => GptStatus.InvalidHeaderLba,
            GptPartitionTableStatus.InvalidUsableLbaRange => GptStatus.InvalidUsableLbaRange,
            GptPartitionTableStatus.InvalidPartitionEntryCount => GptStatus.InvalidPartitionEntryCount,
            GptPartitionTableStatus.InvalidPartitionEntrySize => GptStatus.InvalidPartitionEntrySize,
            GptPartitionTableStatus.InvalidPartitionEntryArrayRange => GptStatus.InvalidPartitionEntryArrayRange,
            GptPartitionTableStatus.PartitionEntryArrayTooLarge => GptStatus.UnsupportedGeometry,
            GptPartitionTableStatus.PartitionEntryBufferTooSmall => GptStatus.PartitionEntryShortRead,
            GptPartitionTableStatus.DestinationTooSmall => GptStatus.PartitionEntryShortRead,
            GptPartitionTableStatus.InvalidPartitionEntryRange => GptStatus.InvalidPartitionEntryRange,
            GptPartitionTableStatus.HeaderCrcMismatch => GptStatus.InvalidHeaderSignature,
            GptPartitionTableStatus.PartitionEntryArrayCrcMismatch => GptStatus.InvalidPartitionEntryArrayRange,
            _ => GptStatus.UnsupportedGeometry
        };
    }
}
