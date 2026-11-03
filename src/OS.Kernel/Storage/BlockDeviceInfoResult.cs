namespace OS.Storage;

public readonly struct BlockDeviceInfoResult
{
    private BlockDeviceInfoResult(BlockDeviceStatus status, BlockDeviceInfo info)
    {
        IsInitialized = true;
        Status = status;
        Info = info;
    }

    public bool IsInitialized
    {
        get;
    }

    public BlockDeviceStatus Status
    {
        get;
    }

    public BlockDeviceInfo Info
    {
        get;
    }

    public bool Succeeded => IsInitialized && Status == BlockDeviceStatus.Success;

    public static BlockDeviceInfoResult Success(BlockDeviceInfo info)
    {
        return new BlockDeviceInfoResult(BlockDeviceStatus.Success, info);
    }

    public static BlockDeviceInfoResult Fail(BlockDeviceStatus status)
    {
        return new BlockDeviceInfoResult(status, default);
    }
}
