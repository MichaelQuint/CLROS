namespace OS.Storage;

public readonly struct GptEfiSystemPartitionLocationResult
{
    private GptEfiSystemPartitionLocationResult(
        GptStatus status,
        BlockDeviceStatus blockStatus,
        GptHeader header,
        GptPartitionEntry partition,
        uint partitionEntryIndex,
        uint blockSizeBytes,
        ulong partitionBlockCount,
        ulong partitionByteOffset,
        ulong partitionByteLength)
    {
        IsInitialized = true;
        Status = status;
        BlockStatus = blockStatus;
        Header = header;
        Partition = partition;
        PartitionEntryIndex = partitionEntryIndex;
        BlockSizeBytes = blockSizeBytes;
        PartitionBlockCount = partitionBlockCount;
        PartitionByteOffset = partitionByteOffset;
        PartitionByteLength = partitionByteLength;
    }

    public bool IsInitialized
    {
        get;
    }

    public GptStatus Status
    {
        get;
    }

    public BlockDeviceStatus BlockStatus
    {
        get;
    }

    public GptHeader Header
    {
        get;
    }

    public GptPartitionEntry Partition
    {
        get;
    }

    public uint PartitionEntryIndex
    {
        get;
    }

    public uint BlockSizeBytes
    {
        get;
    }

    public ulong PartitionStartLba => Partition.FirstLba;

    public ulong PartitionLastLba => Partition.LastLba;

    public ulong PartitionBlockCount
    {
        get;
    }

    public ulong PartitionByteOffset
    {
        get;
    }

    public ulong PartitionByteLength
    {
        get;
    }

    public bool Succeeded => IsInitialized && Status == GptStatus.Success;

    public static GptEfiSystemPartitionLocationResult Success(
        GptHeader header,
        GptPartitionEntry partition,
        uint partitionEntryIndex,
        uint blockSizeBytes)
    {
        ulong partitionBlockCount = partition.LastLba - partition.FirstLba + 1;
        ulong partitionByteOffset = partition.FirstLba * blockSizeBytes;
        ulong partitionByteLength = partitionBlockCount * blockSizeBytes;

        return new GptEfiSystemPartitionLocationResult(
            GptStatus.Success,
            BlockDeviceStatus.Success,
            header,
            partition,
            partitionEntryIndex,
            blockSizeBytes,
            partitionBlockCount,
            partitionByteOffset,
            partitionByteLength);
    }

    public static GptEfiSystemPartitionLocationResult Fail(GptStatus status)
    {
        return new GptEfiSystemPartitionLocationResult(
            status,
            BlockDeviceStatus.Success,
            default,
            default,
            0,
            0,
            0,
            0,
            0);
    }

    public static GptEfiSystemPartitionLocationResult Fail(GptStatus status, BlockDeviceStatus blockStatus)
    {
        return new GptEfiSystemPartitionLocationResult(
            status,
            blockStatus,
            default,
            default,
            0,
            0,
            0,
            0,
            0);
    }
}
