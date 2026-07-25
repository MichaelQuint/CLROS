namespace OS.Storage;

public enum OsPackageLoadStatus
{
    Success = 0,
    GptFailed = 1,
    PartitionDeviceFailed = 2,
    Fat32ReadFailed = 3,
    PackageParseFailed = 4
}

public enum GptPartitionTableSource
{
    Unknown = 0,
    Primary = 1,
    Backup = 2
}

public readonly struct OsPackageLoadResult
{
    private OsPackageLoadResult(
        OsPackageLoadStatus status,
        GptStatus gptStatus,
        GptPartitionTableStatus gptTableStatus,
        GptPartitionTableCrcStatus gptCrcStatus,
        BlockDeviceStatus blockStatus,
        Fat32Status fat32Status,
        OsPackageStatus packageStatus,
        uint gptPartitionEntryIndex,
        ulong gptPartitionEntryBytesRequired,
        ulong gptPartitionEntrySectorBytesRequired,
        uint expectedGptHeaderCrc32,
        uint computedGptHeaderCrc32,
        uint expectedGptPartitionEntryArrayCrc32,
        uint computedGptPartitionEntryArrayCrc32,
        OsPackageHeader packageHeader,
        uint bytesRead,
        uint packageBytesRequired,
        uint packageBufferBytesAvailable,
        ulong gptHeaderBytesRequired = 0,
        ulong gptHeaderBufferBytesAvailable = 0,
        ulong gptPartitionEntryBufferBytesAvailable = 0,
        GptPartitionTableSource gptTableSource = GptPartitionTableSource.Unknown)
    {
        IsInitialized = true;
        Status = status;
        GptStatus = gptStatus;
        GptTableStatus = gptTableStatus;
        GptCrcStatus = gptCrcStatus;
        GptTableSource = gptTableSource;
        BlockStatus = blockStatus;
        Fat32Status = fat32Status;
        PackageStatus = packageStatus;
        GptPartitionEntryIndex = gptPartitionEntryIndex;
        GptPartitionEntryBytesRequired = gptPartitionEntryBytesRequired;
        GptPartitionEntrySectorBytesRequired = gptPartitionEntrySectorBytesRequired;
        GptHeaderBytesRequired = gptHeaderBytesRequired;
        GptHeaderBufferBytesAvailable = gptHeaderBufferBytesAvailable;
        GptPartitionEntryBufferBytesAvailable = gptPartitionEntryBufferBytesAvailable;
        ExpectedGptHeaderCrc32 = expectedGptHeaderCrc32;
        ComputedGptHeaderCrc32 = computedGptHeaderCrc32;
        ExpectedGptPartitionEntryArrayCrc32 = expectedGptPartitionEntryArrayCrc32;
        ComputedGptPartitionEntryArrayCrc32 = computedGptPartitionEntryArrayCrc32;
        PackageHeader = packageHeader;
        BytesRead = bytesRead;
        PackageBytesRequired = packageBytesRequired;
        PackageBufferBytesAvailable = packageBufferBytesAvailable;
    }

    public bool IsInitialized { get; }

    public OsPackageLoadStatus Status { get; }

    public GptStatus GptStatus { get; }

    public GptPartitionTableStatus GptTableStatus { get; }

    public GptPartitionTableCrcStatus GptCrcStatus { get; }

    public GptPartitionTableSource GptTableSource { get; }

    public BlockDeviceStatus BlockStatus { get; }

    public Fat32Status Fat32Status { get; }

    public OsPackageStatus PackageStatus { get; }

    public uint GptPartitionEntryIndex { get; }

    public ulong GptPartitionEntryBytesRequired { get; }

    public ulong GptPartitionEntrySectorBytesRequired { get; }

    public ulong GptHeaderBytesRequired { get; }

    public ulong GptHeaderBufferBytesAvailable { get; }

    public ulong GptPartitionEntryBufferBytesAvailable { get; }

    public uint ExpectedGptHeaderCrc32 { get; }

    public uint ComputedGptHeaderCrc32 { get; }

    public uint ExpectedGptPartitionEntryArrayCrc32 { get; }

    public uint ComputedGptPartitionEntryArrayCrc32 { get; }

    public OsPackageHeader PackageHeader { get; }

    public uint BytesRead { get; }

    public uint PackageBytesRequired { get; }

    public uint PackageBufferBytesAvailable { get; }

    public bool Succeeded => IsInitialized && Status == OsPackageLoadStatus.Success;

    public static OsPackageLoadResult Success(OsPackageHeader packageHeader, uint bytesRead)
    {
        return new OsPackageLoadResult(
            OsPackageLoadStatus.Success,
            GptStatus.Success,
            GptPartitionTableStatus.Success,
            GptPartitionTableCrcStatus.NotValidated,
            BlockDeviceStatus.Success,
            Fat32Status.Success,
            OsPackageStatus.Success,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            packageHeader,
            bytesRead,
            bytesRead,
            bytesRead);
    }

    public static OsPackageLoadResult Success(
        OsPackageHeader packageHeader,
        uint bytesRead,
        uint packageBytesRequired,
        uint packageBufferBytesAvailable)
    {
        return new OsPackageLoadResult(
            OsPackageLoadStatus.Success,
            GptStatus.Success,
            GptPartitionTableStatus.Success,
            GptPartitionTableCrcStatus.NotValidated,
            BlockDeviceStatus.Success,
            Fat32Status.Success,
            OsPackageStatus.Success,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            packageHeader,
            bytesRead,
            packageBytesRequired,
            packageBufferBytesAvailable);
    }

    public static OsPackageLoadResult Success(
        OsPackageHeader packageHeader,
        uint bytesRead,
        GptEspLocationReadinessResult gpt)
    {
        return new OsPackageLoadResult(
            OsPackageLoadStatus.Success,
            GptStatus.Success,
            gpt.GptTableStatus,
            gpt.GptCrcStatus,
            BlockDeviceStatus.Success,
            Fat32Status.Success,
            OsPackageStatus.Success,
            gpt.PartitionEntryIndex,
            gpt.PartitionEntryBytesRequired,
            gpt.PartitionEntrySectorBytesRequired,
            gpt.ExpectedHeaderCrc32,
            gpt.ComputedHeaderCrc32,
            gpt.ExpectedPartitionEntryArrayCrc32,
            gpt.ComputedPartitionEntryArrayCrc32,
            packageHeader,
            bytesRead,
            bytesRead,
            bytesRead,
            gptHeaderBytesRequired: gpt.HeaderBytesRequired,
            gptHeaderBufferBytesAvailable: gpt.HeaderBufferBytesAvailable,
            gptPartitionEntryBufferBytesAvailable: gpt.PartitionEntryBufferBytesAvailable,
            gptTableSource: gpt.TableSource);
    }

    public static OsPackageLoadResult Success(
        OsPackageHeader packageHeader,
        uint bytesRead,
        uint packageBytesRequired,
        uint packageBufferBytesAvailable,
        GptEspLocationReadinessResult gpt)
    {
        return new OsPackageLoadResult(
            OsPackageLoadStatus.Success,
            GptStatus.Success,
            gpt.GptTableStatus,
            gpt.GptCrcStatus,
            BlockDeviceStatus.Success,
            Fat32Status.Success,
            OsPackageStatus.Success,
            gpt.PartitionEntryIndex,
            gpt.PartitionEntryBytesRequired,
            gpt.PartitionEntrySectorBytesRequired,
            gpt.ExpectedHeaderCrc32,
            gpt.ComputedHeaderCrc32,
            gpt.ExpectedPartitionEntryArrayCrc32,
            gpt.ComputedPartitionEntryArrayCrc32,
            packageHeader,
            bytesRead,
            packageBytesRequired,
            packageBufferBytesAvailable,
            gptHeaderBytesRequired: gpt.HeaderBytesRequired,
            gptHeaderBufferBytesAvailable: gpt.HeaderBufferBytesAvailable,
            gptPartitionEntryBufferBytesAvailable: gpt.PartitionEntryBufferBytesAvailable,
            gptTableSource: gpt.TableSource);
    }

    public static OsPackageLoadResult GptFail(
        GptStatus gptStatus,
        BlockDeviceStatus blockStatus)
    {
        return new OsPackageLoadResult(
            OsPackageLoadStatus.GptFailed,
            gptStatus,
            GptPartitionTableStatus.Success,
            GptPartitionTableCrcStatus.NotValidated,
            blockStatus,
            Fat32Status.Success,
            OsPackageStatus.Success,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            default,
            0,
            0,
            0);
    }

    public static OsPackageLoadResult GptFail(GptEspLocationReadinessResult gpt)
    {
        return new OsPackageLoadResult(
            OsPackageLoadStatus.GptFailed,
            gpt.GptStatus,
            gpt.GptTableStatus,
            gpt.GptCrcStatus,
            gpt.BlockStatus,
            Fat32Status.Success,
            OsPackageStatus.Success,
            gpt.PartitionEntryIndex,
            gpt.PartitionEntryBytesRequired,
            gpt.PartitionEntrySectorBytesRequired,
            gpt.ExpectedHeaderCrc32,
            gpt.ComputedHeaderCrc32,
            gpt.ExpectedPartitionEntryArrayCrc32,
            gpt.ComputedPartitionEntryArrayCrc32,
            default,
            0,
            0,
            0,
            gptHeaderBytesRequired: gpt.HeaderBytesRequired,
            gptHeaderBufferBytesAvailable: gpt.HeaderBufferBytesAvailable,
            gptPartitionEntryBufferBytesAvailable: gpt.PartitionEntryBufferBytesAvailable,
            gptTableSource: gpt.TableSource);
    }

    public static OsPackageLoadResult PartitionFail(BlockDeviceStatus blockStatus)
    {
        return new OsPackageLoadResult(
            OsPackageLoadStatus.PartitionDeviceFailed,
            GptStatus.Success,
            GptPartitionTableStatus.Success,
            GptPartitionTableCrcStatus.NotValidated,
            blockStatus,
            Fat32Status.Success,
            OsPackageStatus.Success,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            default,
            0,
            0,
            0);
    }

    public static OsPackageLoadResult PartitionFail(
        BlockDeviceStatus blockStatus,
        GptEspLocationReadinessResult gpt)
    {
        return new OsPackageLoadResult(
            OsPackageLoadStatus.PartitionDeviceFailed,
            GptStatus.Success,
            gpt.GptTableStatus,
            gpt.GptCrcStatus,
            blockStatus,
            Fat32Status.Success,
            OsPackageStatus.Success,
            gpt.PartitionEntryIndex,
            gpt.PartitionEntryBytesRequired,
            gpt.PartitionEntrySectorBytesRequired,
            gpt.ExpectedHeaderCrc32,
            gpt.ComputedHeaderCrc32,
            gpt.ExpectedPartitionEntryArrayCrc32,
            gpt.ComputedPartitionEntryArrayCrc32,
            default,
            0,
            0,
            0,
            gptTableSource: gpt.TableSource);
    }

    public static OsPackageLoadResult Fat32Fail(
        Fat32Status fat32Status,
        BlockDeviceStatus blockStatus)
    {
        return new OsPackageLoadResult(
            OsPackageLoadStatus.Fat32ReadFailed,
            GptStatus.Success,
            GptPartitionTableStatus.Success,
            GptPartitionTableCrcStatus.NotValidated,
            blockStatus,
            fat32Status,
            OsPackageStatus.Success,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            default,
            0,
            0,
            0);
    }

    public static OsPackageLoadResult Fat32Fail(
        Fat32FileReadResult file,
        uint packageBufferBytesAvailable)
    {
        return new OsPackageLoadResult(
            OsPackageLoadStatus.Fat32ReadFailed,
            GptStatus.Success,
            GptPartitionTableStatus.Success,
            GptPartitionTableCrcStatus.NotValidated,
            file.BlockStatus,
            file.Status,
            OsPackageStatus.Success,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            default,
            file.BytesRead,
            file.FileSize,
            packageBufferBytesAvailable);
    }

    public static OsPackageLoadResult Fat32Fail(
        Fat32Status fat32Status,
        BlockDeviceStatus blockStatus,
        GptEspLocationReadinessResult gpt)
    {
        return new OsPackageLoadResult(
            OsPackageLoadStatus.Fat32ReadFailed,
            GptStatus.Success,
            gpt.GptTableStatus,
            gpt.GptCrcStatus,
            blockStatus,
            fat32Status,
            OsPackageStatus.Success,
            gpt.PartitionEntryIndex,
            gpt.PartitionEntryBytesRequired,
            gpt.PartitionEntrySectorBytesRequired,
            gpt.ExpectedHeaderCrc32,
            gpt.ComputedHeaderCrc32,
            gpt.ExpectedPartitionEntryArrayCrc32,
            gpt.ComputedPartitionEntryArrayCrc32,
            default,
            0,
            0,
            0,
            gptHeaderBytesRequired: gpt.HeaderBytesRequired,
            gptHeaderBufferBytesAvailable: gpt.HeaderBufferBytesAvailable,
            gptPartitionEntryBufferBytesAvailable: gpt.PartitionEntryBufferBytesAvailable,
            gptTableSource: gpt.TableSource);
    }

    public static OsPackageLoadResult Fat32Fail(
        Fat32FileReadResult file,
        uint packageBufferBytesAvailable,
        GptEspLocationReadinessResult gpt)
    {
        return new OsPackageLoadResult(
            OsPackageLoadStatus.Fat32ReadFailed,
            GptStatus.Success,
            gpt.GptTableStatus,
            gpt.GptCrcStatus,
            file.BlockStatus,
            file.Status,
            OsPackageStatus.Success,
            gpt.PartitionEntryIndex,
            gpt.PartitionEntryBytesRequired,
            gpt.PartitionEntrySectorBytesRequired,
            gpt.ExpectedHeaderCrc32,
            gpt.ComputedHeaderCrc32,
            gpt.ExpectedPartitionEntryArrayCrc32,
            gpt.ComputedPartitionEntryArrayCrc32,
            default,
            file.BytesRead,
            file.FileSize,
            packageBufferBytesAvailable,
            gptHeaderBytesRequired: gpt.HeaderBytesRequired,
            gptHeaderBufferBytesAvailable: gpt.HeaderBufferBytesAvailable,
            gptPartitionEntryBufferBytesAvailable: gpt.PartitionEntryBufferBytesAvailable,
            gptTableSource: gpt.TableSource);
    }

    public static OsPackageLoadResult PackageFail(
        OsPackageStatus packageStatus,
        uint bytesRead)
    {
        return new OsPackageLoadResult(
            OsPackageLoadStatus.PackageParseFailed,
            GptStatus.Success,
            GptPartitionTableStatus.Success,
            GptPartitionTableCrcStatus.NotValidated,
            BlockDeviceStatus.Success,
            Fat32Status.Success,
            packageStatus,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            default,
            bytesRead,
            bytesRead,
            bytesRead);
    }

    public static OsPackageLoadResult PackageFail(
        OsPackageStatus packageStatus,
        uint bytesRead,
        uint packageBytesRequired,
        uint packageBufferBytesAvailable)
    {
        return new OsPackageLoadResult(
            OsPackageLoadStatus.PackageParseFailed,
            GptStatus.Success,
            GptPartitionTableStatus.Success,
            GptPartitionTableCrcStatus.NotValidated,
            BlockDeviceStatus.Success,
            Fat32Status.Success,
            packageStatus,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            default,
            bytesRead,
            packageBytesRequired,
            packageBufferBytesAvailable);
    }

    public static OsPackageLoadResult PackageFail(
        OsPackageStatus packageStatus,
        uint bytesRead,
        GptEspLocationReadinessResult gpt)
    {
        return new OsPackageLoadResult(
            OsPackageLoadStatus.PackageParseFailed,
            GptStatus.Success,
            gpt.GptTableStatus,
            gpt.GptCrcStatus,
            BlockDeviceStatus.Success,
            Fat32Status.Success,
            packageStatus,
            gpt.PartitionEntryIndex,
            gpt.PartitionEntryBytesRequired,
            gpt.PartitionEntrySectorBytesRequired,
            gpt.ExpectedHeaderCrc32,
            gpt.ComputedHeaderCrc32,
            gpt.ExpectedPartitionEntryArrayCrc32,
            gpt.ComputedPartitionEntryArrayCrc32,
            default,
            bytesRead,
            bytesRead,
            bytesRead,
            gptHeaderBytesRequired: gpt.HeaderBytesRequired,
            gptHeaderBufferBytesAvailable: gpt.HeaderBufferBytesAvailable,
            gptPartitionEntryBufferBytesAvailable: gpt.PartitionEntryBufferBytesAvailable,
            gptTableSource: gpt.TableSource);
    }

    public static OsPackageLoadResult PackageFail(
        OsPackageStatus packageStatus,
        uint bytesRead,
        uint packageBytesRequired,
        uint packageBufferBytesAvailable,
        GptEspLocationReadinessResult gpt)
    {
        return new OsPackageLoadResult(
            OsPackageLoadStatus.PackageParseFailed,
            GptStatus.Success,
            gpt.GptTableStatus,
            gpt.GptCrcStatus,
            BlockDeviceStatus.Success,
            Fat32Status.Success,
            packageStatus,
            gpt.PartitionEntryIndex,
            gpt.PartitionEntryBytesRequired,
            gpt.PartitionEntrySectorBytesRequired,
            gpt.ExpectedHeaderCrc32,
            gpt.ComputedHeaderCrc32,
            gpt.ExpectedPartitionEntryArrayCrc32,
            gpt.ComputedPartitionEntryArrayCrc32,
            default,
            bytesRead,
            packageBytesRequired,
            packageBufferBytesAvailable,
            gptHeaderBytesRequired: gpt.HeaderBytesRequired,
            gptHeaderBufferBytesAvailable: gpt.HeaderBufferBytesAvailable,
            gptPartitionEntryBufferBytesAvailable: gpt.PartitionEntryBufferBytesAvailable,
            gptTableSource: gpt.TableSource);
    }
}

public enum GptEspLocationReadinessStatus
{
    Success = 0,
    InvalidBlockDevice = 1,
    HeaderBufferTooSmall = 2,
    PrimaryHeaderReadFailed = 3,
    PrimaryHeaderShortRead = 4,
    ParserPlanningFailed = 5,
    PartitionEntryBufferTooSmall = 6,
    PartitionEntryReadFailed = 7,
    PartitionEntryShortRead = 8,
    ParserFailed = 9,
    EfiSystemPartitionNotFound = 10
}

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

public static class OsPackageLoader
{
    public const string PackagePath = "OS/APP.PKG";

    public static OsPackageLoadResult LoadFromPrimaryGptEsp(
        IReadOnlyBlockDevice? disk,
        Span<byte> packageBuffer)
    {
        GptEfiSystemPartitionLocationResult esp = GptParser.LocatePrimaryEfiSystemPartition(disk);
        if (!esp.Succeeded)
        {
            return OsPackageLoadResult.GptFail(esp.Status, esp.BlockStatus);
        }

        PartitionBlockDeviceCreateResult partition = PartitionBlockDevice.Create(
            disk,
            esp.PartitionStartLba,
            esp.PartitionBlockCount);
        if (!partition.Succeeded || partition.Device is null)
        {
            return OsPackageLoadResult.PartitionFail(partition.Status);
        }

        Fat32FileReadResult file = Fat32Parser.ReadFirstLevelFile(
            partition.Device,
            partitionStartLba: 0,
            PackagePath,
            packageBuffer);
        if (!file.Succeeded)
        {
            return OsPackageLoadResult.Fat32Fail(file, checked((uint)packageBuffer.Length));
        }

        OsPackageParseResult parsed = OsPackageParser.Parse(
            packageBuffer.Slice(0, checked((int)file.BytesRead)));
        if (!parsed.Succeeded)
        {
            return OsPackageLoadResult.PackageFail(
                parsed.Status,
                file.BytesRead,
                file.FileSize,
                checked((uint)packageBuffer.Length));
        }

        return OsPackageLoadResult.Success(
            parsed.Header,
            file.BytesRead,
            file.FileSize,
            checked((uint)packageBuffer.Length));
    }

    public static OsPackageLoadResult LoadFromPrimaryGptEspWithCallerBuffers(
        IReadOnlyBlockDevice? disk,
        Span<byte> primaryHeaderSector,
        Span<byte> partitionEntrySectors,
        Span<GptPartitionDescriptor> partitions,
        Span<byte> packageBuffer)
    {
        GptEspLocationReadinessResult esp = LocatePrimaryGptEspWithCallerBuffers(
            disk,
            primaryHeaderSector,
            partitionEntrySectors,
            partitions);
        if (!esp.Succeeded)
        {
            return OsPackageLoadResult.GptFail(esp);
        }

        PartitionBlockDeviceCreateResult partition = PartitionBlockDevice.Create(
            disk,
            esp.PartitionStartLba,
            esp.PartitionBlockCount);
        if (!partition.Succeeded || partition.Device is null)
        {
            return OsPackageLoadResult.PartitionFail(partition.Status, esp);
        }

        Fat32FileReadResult file = Fat32Parser.ReadFirstLevelFile(
            partition.Device,
            partitionStartLba: 0,
            PackagePath,
            packageBuffer);
        if (!file.Succeeded)
        {
            return OsPackageLoadResult.Fat32Fail(file, checked((uint)packageBuffer.Length), esp);
        }

        OsPackageParseResult parsed = OsPackageParser.Parse(
            packageBuffer.Slice(0, checked((int)file.BytesRead)));
        if (!parsed.Succeeded)
        {
            return OsPackageLoadResult.PackageFail(
                parsed.Status,
                file.BytesRead,
                file.FileSize,
                checked((uint)packageBuffer.Length),
                esp);
        }

        return OsPackageLoadResult.Success(
            parsed.Header,
            file.BytesRead,
            file.FileSize,
            checked((uint)packageBuffer.Length),
            esp);
    }

    internal static GptEspLocationReadinessResult LocatePrimaryGptEspWithCallerBuffers(
        IReadOnlyBlockDevice? disk,
        Span<byte> primaryHeaderSector,
        Span<byte> partitionEntrySectors,
        Span<GptPartitionDescriptor> partitions)
    {
        if (disk is null)
        {
            return GptEspLocationReadinessResult.Fail(
                GptEspLocationReadinessStatus.InvalidBlockDevice,
                GptStatus.InvalidBlockDevice);
        }

        uint blockSize = disk.Info.BlockSize;
        if ((ulong)primaryHeaderSector.Length < blockSize)
        {
            return GptEspLocationReadinessResult.Fail(
                GptEspLocationReadinessStatus.HeaderBufferTooSmall,
                GptStatus.PrimaryHeaderShortRead,
                BlockDeviceStatus.DestinationTooSmall,
                headerBytesRequired: blockSize,
                headerBufferBytesAvailable: checked((ulong)primaryHeaderSector.Length),
                partitionEntryBufferBytesAvailable: checked((ulong)partitionEntrySectors.Length));
        }

        GptEspLocationReadinessResult primary = LocateGptEspWithCallerBuffers(
            disk,
            primaryHeaderSector,
            partitionEntrySectors,
            partitions,
            GptPartitionTableReadKind.Primary);
        if (primary.Succeeded)
        {
            return primary;
        }

        GptEspLocationReadinessResult backup = LocateGptEspWithCallerBuffers(
            disk,
            primaryHeaderSector,
            partitionEntrySectors,
            partitions,
            GptPartitionTableReadKind.Backup);
        return backup.Succeeded ? backup : primary;
    }

    private enum GptPartitionTableReadKind
    {
        Primary = 0,
        Backup = 1
    }

    private static GptEspLocationReadinessResult LocateGptEspWithCallerBuffers(
        IReadOnlyBlockDevice disk,
        Span<byte> headerSector,
        Span<byte> partitionEntrySectors,
        Span<GptPartitionDescriptor> partitions,
        GptPartitionTableReadKind tableKind)
    {
        GptPartitionTableSource tableSource = ToGptPartitionTableSource(tableKind);
        uint blockSize = disk.Info.BlockSize;
        if (tableKind == GptPartitionTableReadKind.Backup && disk.Info.BlockCount == 0)
        {
            return GptEspLocationReadinessResult.Fail(
                GptEspLocationReadinessStatus.ParserPlanningFailed,
                GptStatus.UnsupportedGeometry,
                BlockDeviceStatus.InvalidBlockCount,
                tableSource: tableSource);
        }

        Span<byte> headerReadBuffer = headerSector.Slice(0, checked((int)blockSize));
        BlockReadResult headerRead = disk.ReadBlocks(
            tableKind == GptPartitionTableReadKind.Primary
                ? GptPartitionTableParser.PrimaryHeaderLba
                : disk.Info.BlockCount - 1,
            1,
            headerReadBuffer);
        if (!headerRead.Succeeded)
        {
            return GptEspLocationReadinessResult.Fail(
                GptEspLocationReadinessStatus.PrimaryHeaderReadFailed,
                GptStatus.PrimaryHeaderReadFailed,
                headerRead.Status,
                tableSource: tableSource);
        }

        if (headerRead.BlocksRead != 1 || headerRead.BytesRead != blockSize)
        {
            return GptEspLocationReadinessResult.Fail(
                GptEspLocationReadinessStatus.PrimaryHeaderShortRead,
                GptStatus.PrimaryHeaderShortRead,
                headerRead.Status,
                tableSource: tableSource);
        }

        GptPartitionTableParseResult planning = ParseGptPartitionTable(
            headerReadBuffer,
            ReadOnlySpan<byte>.Empty,
            disk.Info.BlockCount,
            blockSize,
            Span<GptPartitionDescriptor>.Empty,
            tableKind);
        if (planning.Status != GptPartitionTableStatus.PartitionEntryBufferTooSmall)
        {
            return GptEspLocationReadinessResult.Fail(
                planning.Succeeded
                    ? GptEspLocationReadinessStatus.EfiSystemPartitionNotFound
                    : GptEspLocationReadinessStatus.ParserPlanningFailed,
                planning,
                tableSource: tableSource);
        }

        ulong partitionEntrySectorBytesRequired = planning.PartitionEntrySectorsRequired * blockSize;
        if (partitionEntrySectorBytesRequired > int.MaxValue
            || (ulong)partitionEntrySectors.Length < partitionEntrySectorBytesRequired)
        {
            return FailPartitionEntryBufferTooSmall(
                planning,
                partitionEntrySectorBytesRequired,
                checked((ulong)partitionEntrySectors.Length),
                blockSize,
                blockSize,
                tableSource);
        }

        if (planning.PartitionEntrySectorsRequired > uint.MaxValue)
        {
            return FailPartitionEntryBufferTooSmall(
                planning,
                partitionEntrySectorBytesRequired,
                checked((ulong)partitionEntrySectors.Length),
                blockSize,
                blockSize,
                tableSource);
        }

        Span<byte> partitionEntryReadBuffer = partitionEntrySectors.Slice(
            0,
            checked((int)partitionEntrySectorBytesRequired));
        BlockReadResult partitionEntryRead = disk.ReadBlocks(
            planning.Header.PartitionEntryLba,
            checked((uint)planning.PartitionEntrySectorsRequired),
            partitionEntryReadBuffer);
        if (!partitionEntryRead.Succeeded)
        {
            return GptEspLocationReadinessResult.Fail(
                GptEspLocationReadinessStatus.PartitionEntryReadFailed,
                GptStatus.PartitionEntryReadFailed,
                partitionEntryRead.Status,
                tableSource: tableSource);
        }

        if (partitionEntryRead.BlocksRead != planning.PartitionEntrySectorsRequired
            || partitionEntryRead.BytesRead != partitionEntrySectorBytesRequired)
        {
            return GptEspLocationReadinessResult.Fail(
                GptEspLocationReadinessStatus.PartitionEntryShortRead,
                GptStatus.PartitionEntryShortRead,
                partitionEntryRead.Status,
                tableSource: tableSource);
        }

        GptPartitionTableParseResult parsed = ParseGptPartitionTable(
            headerReadBuffer,
            partitionEntryReadBuffer,
            disk.Info.BlockCount,
            blockSize,
            partitions,
            tableKind);
        if (!parsed.Succeeded)
        {
            return GptEspLocationReadinessResult.Fail(
                GptEspLocationReadinessStatus.ParserFailed,
                parsed,
                tableSource: tableSource);
        }

        GptPartitionSelectorStatus selectorStatus =
            GptPartitionSelector.SelectByPartitionTypeGuid(
                parsed,
                partitions.Slice(0, parsed.PartitionsWritten),
                GptParser.EfiSystemPartitionTypeGuid,
                out GptPartitionSelectorResult selectedPartition);
        if (selectorStatus == GptPartitionSelectorStatus.Success && selectedPartition.Succeeded)
        {
            return GptEspLocationReadinessResult.Success(
                parsed,
                selectedPartition.Descriptor,
                blockSize,
                tableSource);
        }

        if (selectorStatus == GptPartitionSelectorStatus.NotFound)
        {
            return GptEspLocationReadinessResult.FailMissingEsp(
                parsed,
                partitionEntrySectorBytesRequired,
                tableSource);
        }

        return GptEspLocationReadinessResult.Fail(
            GptEspLocationReadinessStatus.ParserFailed,
            parsed,
            tableSource: tableSource);
    }

    private static GptPartitionTableParseResult ParseGptPartitionTable(
        ReadOnlySpan<byte> headerSector,
        ReadOnlySpan<byte> partitionEntrySectors,
        ulong blockCount,
        uint sectorSizeBytes,
        Span<GptPartitionDescriptor> partitions,
        GptPartitionTableReadKind tableKind)
    {
        return tableKind == GptPartitionTableReadKind.Primary
            ? GptPartitionTableParser.ParsePrimary(
                headerSector,
                partitionEntrySectors,
                blockCount,
                sectorSizeBytes,
                partitions)
            : GptPartitionTableParser.ParseBackup(
                headerSector,
                partitionEntrySectors,
                blockCount,
                sectorSizeBytes,
                partitions);
    }

    private static GptPartitionTableSource ToGptPartitionTableSource(GptPartitionTableReadKind tableKind)
    {
        return tableKind == GptPartitionTableReadKind.Primary
            ? GptPartitionTableSource.Primary
            : GptPartitionTableSource.Backup;
    }

    private static GptEspLocationReadinessResult FailPartitionEntryBufferTooSmall(
        GptPartitionTableParseResult planning,
        ulong partitionEntrySectorBytesRequired,
        ulong partitionEntryBufferBytesAvailable,
        ulong headerBytesRequired,
        ulong headerBufferBytesAvailable,
        GptPartitionTableSource tableSource)
    {
        return GptEspLocationReadinessResult.FailPartitionEntryBufferTooSmall(
            planning,
            partitionEntrySectorBytesRequired,
            partitionEntryBufferBytesAvailable,
            headerBytesRequired,
            headerBufferBytesAvailable,
            tableSource);
    }
}
