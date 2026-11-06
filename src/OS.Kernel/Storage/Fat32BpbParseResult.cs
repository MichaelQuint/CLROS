namespace OS.Storage;

public readonly struct Fat32BpbParseResult
{
    private Fat32BpbParseResult(Fat32Status status, BlockDeviceStatus blockStatus, Fat32Bpb bpb)
    {
        IsInitialized = true;
        Status = status;
        BlockStatus = blockStatus;
        Bpb = bpb;
    }

    public bool IsInitialized
    {
        get;
    }

    public Fat32Status Status
    {
        get;
    }

    public BlockDeviceStatus BlockStatus
    {
        get;
    }

    public Fat32Bpb Bpb
    {
        get;
    }

    public bool Succeeded => IsInitialized && Status == Fat32Status.Success;

    public static Fat32BpbParseResult Success(Fat32Bpb bpb)
    {
        return new Fat32BpbParseResult(Fat32Status.Success, BlockDeviceStatus.Success, bpb);
    }

    public static Fat32BpbParseResult Fail(Fat32Status status)
    {
        return new Fat32BpbParseResult(status, BlockDeviceStatus.Success, default);
    }

    public static Fat32BpbParseResult Fail(Fat32Status status, BlockDeviceStatus blockStatus)
    {
        return new Fat32BpbParseResult(status, blockStatus, default);
    }
}
