namespace OS.Storage;

public readonly struct Fat32FileReadResult
{
    private Fat32FileReadResult(
        Fat32Status status,
        BlockDeviceStatus blockStatus,
        Fat32Bpb bpb,
        Fat32DirectoryEntry entry,
        uint fileSize,
        uint bytesRead,
        bool assumedContiguousClusters)
    {
        IsInitialized = true;
        Status = status;
        BlockStatus = blockStatus;
        Bpb = bpb;
        Entry = entry;
        FileSize = fileSize;
        BytesRead = bytesRead;
        AssumedContiguousClusters = assumedContiguousClusters;
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

    public uint FileSize
    {
        get;
    }

    public uint BytesRead
    {
        get;
    }

    public bool AssumedContiguousClusters
    {
        get;
    }

    public bool Succeeded => IsInitialized && Status == Fat32Status.Success;

    public static Fat32FileReadResult Success(
        Fat32Bpb bpb,
        Fat32DirectoryEntry entry,
        uint bytesRead)
    {
        return Success(bpb, entry, bytesRead, assumedContiguousClusters: true);
    }

    public static Fat32FileReadResult Success(
        Fat32Bpb bpb,
        Fat32DirectoryEntry entry,
        uint bytesRead,
        bool assumedContiguousClusters)
    {
        return new Fat32FileReadResult(
            Fat32Status.Success,
            BlockDeviceStatus.Success,
            bpb,
            entry,
            entry.FileSize,
            bytesRead,
            assumedContiguousClusters);
    }

    public static Fat32FileReadResult Fail(Fat32Status status)
    {
        return new Fat32FileReadResult(
            status,
            BlockDeviceStatus.Success,
            default,
            default,
            0,
            0,
            assumedContiguousClusters: false);
    }

    public static Fat32FileReadResult Fail(Fat32Status status, BlockDeviceStatus blockStatus)
    {
        return new Fat32FileReadResult(
            status,
            blockStatus,
            default,
            default,
            0,
            0,
            assumedContiguousClusters: false);
    }

    public static Fat32FileReadResult Fail(Fat32Status status, Fat32Bpb bpb)
    {
        return new Fat32FileReadResult(
            status,
            BlockDeviceStatus.Success,
            bpb,
            default,
            0,
            0,
            assumedContiguousClusters: false);
    }

    public static Fat32FileReadResult Fail(
        Fat32Status status,
        BlockDeviceStatus blockStatus,
        Fat32Bpb bpb)
    {
        return new Fat32FileReadResult(
            status,
            blockStatus,
            bpb,
            default,
            0,
            0,
            assumedContiguousClusters: false);
    }

    public static Fat32FileReadResult Fail(
        Fat32Status status,
        Fat32Bpb bpb,
        Fat32DirectoryEntry entry)
    {
        return new Fat32FileReadResult(
            status,
            BlockDeviceStatus.Success,
            bpb,
            entry,
            entry.FileSize,
            0,
            assumedContiguousClusters: false);
    }

    public static Fat32FileReadResult Fail(
        Fat32Status status,
        BlockDeviceStatus blockStatus,
        Fat32Bpb bpb,
        Fat32DirectoryEntry entry,
        uint bytesRead,
        bool assumedContiguousClusters)
    {
        return new Fat32FileReadResult(
            status,
            blockStatus,
            bpb,
            entry,
            entry.FileSize,
            bytesRead,
            assumedContiguousClusters);
    }
}
