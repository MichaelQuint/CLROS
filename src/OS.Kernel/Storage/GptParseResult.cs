namespace OS.Storage;

public readonly struct GptParseResult
{
    private GptParseResult(
        GptStatus status,
        BlockDeviceStatus blockStatus,
        GptHeader header,
        GptPartitionEntry firstPartition)
    {
        IsInitialized = true;
        Status = status;
        BlockStatus = blockStatus;
        Header = header;
        FirstPartition = firstPartition;
    }

    public bool IsInitialized { get; }

    public GptStatus Status { get; }

    public BlockDeviceStatus BlockStatus { get; }

    public GptHeader Header { get; }

    public GptPartitionEntry FirstPartition { get; }

    public bool Succeeded => IsInitialized && Status == GptStatus.Success;

    public static GptParseResult Success(GptHeader header, GptPartitionEntry firstPartition)
    {
        return new GptParseResult(GptStatus.Success, BlockDeviceStatus.Success, header, firstPartition);
    }

    public static GptParseResult Fail(GptStatus status)
    {
        return new GptParseResult(status, BlockDeviceStatus.Success, default, default);
    }

    public static GptParseResult Fail(GptStatus status, BlockDeviceStatus blockStatus)
    {
        return new GptParseResult(status, blockStatus, default, default);
    }
}
