namespace OS.Storage;

public readonly struct Fat32DirectoryReadResult
{
    private Fat32DirectoryReadResult(
        Fat32Status status,
        BlockDeviceStatus blockStatus,
        Fat32Bpb bpb,
        int entriesRead,
        bool reachedEndMarker)
    {
        IsInitialized = true;
        Status = status;
        BlockStatus = blockStatus;
        Bpb = bpb;
        EntriesRead = entriesRead;
        ReachedEndMarker = reachedEndMarker;
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

    public int EntriesRead
    {
        get;
    }

    public bool ReachedEndMarker
    {
        get;
    }

    public bool Succeeded => IsInitialized && Status == Fat32Status.Success;

    public static Fat32DirectoryReadResult Success(
        Fat32Bpb bpb,
        int entriesRead,
        bool reachedEndMarker)
    {
        return new Fat32DirectoryReadResult(
            Fat32Status.Success,
            BlockDeviceStatus.Success,
            bpb,
            entriesRead,
            reachedEndMarker);
    }

    public static Fat32DirectoryReadResult Fail(Fat32Status status)
    {
        return new Fat32DirectoryReadResult(status, BlockDeviceStatus.Success, default, 0, false);
    }

    public static Fat32DirectoryReadResult Fail(Fat32Status status, BlockDeviceStatus blockStatus)
    {
        return new Fat32DirectoryReadResult(status, blockStatus, default, 0, false);
    }

    public static Fat32DirectoryReadResult Fail(Fat32Status status, Fat32Bpb bpb)
    {
        return new Fat32DirectoryReadResult(status, BlockDeviceStatus.Success, bpb, 0, false);
    }

    public static Fat32DirectoryReadResult Fail(
        Fat32Status status,
        BlockDeviceStatus blockStatus,
        Fat32Bpb bpb)
    {
        return new Fat32DirectoryReadResult(status, blockStatus, bpb, 0, false);
    }
}
