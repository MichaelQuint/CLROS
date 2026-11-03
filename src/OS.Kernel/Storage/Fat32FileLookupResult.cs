namespace OS.Storage;

public readonly struct Fat32FileLookupResult
{
    private Fat32FileLookupResult(
        Fat32Status status,
        BlockDeviceStatus blockStatus,
        Fat32Bpb bpb,
        Fat32DirectoryEntry entry)
    {
        IsInitialized = true;
        Status = status;
        BlockStatus = blockStatus;
        Bpb = bpb;
        Entry = entry;
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

    public Fat32DirectoryEntry Entry
    {
        get;
    }

    public bool Succeeded => IsInitialized && Status == Fat32Status.Success;

    public static Fat32FileLookupResult Success(Fat32Bpb bpb, Fat32DirectoryEntry entry)
    {
        return new Fat32FileLookupResult(Fat32Status.Success, BlockDeviceStatus.Success, bpb, entry);
    }

    public static Fat32FileLookupResult Fail(Fat32Status status)
    {
        return new Fat32FileLookupResult(status, BlockDeviceStatus.Success, default, default);
    }

    public static Fat32FileLookupResult Fail(Fat32Status status, BlockDeviceStatus blockStatus)
    {
        return new Fat32FileLookupResult(status, blockStatus, default, default);
    }

    public static Fat32FileLookupResult Fail(Fat32Status status, Fat32Bpb bpb)
    {
        return new Fat32FileLookupResult(status, BlockDeviceStatus.Success, bpb, default);
    }

    public static Fat32FileLookupResult Fail(
        Fat32Status status,
        BlockDeviceStatus blockStatus,
        Fat32Bpb bpb)
    {
        return new Fat32FileLookupResult(status, blockStatus, bpb, default);
    }
}
