namespace OS.Storage;

public readonly struct GptPartitionSelectionResult
{
    private GptPartitionSelectionResult(
        GptStatus status,
        BlockDeviceStatus blockStatus,
        GptHeader header,
        GptPartitionEntry partition,
        uint partitionEntryIndex)
    {
        IsInitialized = true;
        Status = status;
        BlockStatus = blockStatus;
        Header = header;
        Partition = partition;
        PartitionEntryIndex = partitionEntryIndex;
    }

    public bool IsInitialized { get; }

    public GptStatus Status { get; }

    public BlockDeviceStatus BlockStatus { get; }

    public GptHeader Header { get; }

    public GptPartitionEntry Partition { get; }

    public uint PartitionEntryIndex { get; }

    public ulong FirstLba => Partition.FirstLba;

    public ulong LastLba => Partition.LastLba;

    public bool Succeeded => IsInitialized && Status == GptStatus.Success;

    public static GptPartitionSelectionResult Success(
        GptHeader header,
        GptPartitionEntry partition,
        uint partitionEntryIndex)
    {
        return new GptPartitionSelectionResult(
            GptStatus.Success,
            BlockDeviceStatus.Success,
            header,
            partition,
            partitionEntryIndex);
    }

    public static GptPartitionSelectionResult Fail(GptStatus status)
    {
        return new GptPartitionSelectionResult(status, BlockDeviceStatus.Success, default, default, 0);
    }

    public static GptPartitionSelectionResult Fail(GptStatus status, BlockDeviceStatus blockStatus)
    {
        return new GptPartitionSelectionResult(status, blockStatus, default, default, 0);
    }
}
