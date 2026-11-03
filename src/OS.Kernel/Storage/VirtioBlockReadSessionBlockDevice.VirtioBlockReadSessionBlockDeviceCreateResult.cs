namespace OS.Storage;

public readonly struct VirtioBlockReadSessionBlockDeviceCreateResult
{
    private VirtioBlockReadSessionBlockDeviceCreateResult(
        VirtioBlockReadSessionBlockDeviceCreateStatus status,
        BlockDeviceStatus blockDeviceStatus,
        VirtioBlockReadOnlyReadSessionStatus readSessionStatus,
        VirtioBlockReadQueuePipelineStatus readSessionPipelineStatus,
        ulong completedByteCount,
        ulong expectedByteCount,
        ulong backingByteCount)
    {
        IsInitialized = true;
        Status = status;
        BlockDeviceStatus = blockDeviceStatus;
        ReadSessionStatus = readSessionStatus;
        ReadSessionPipelineStatus = readSessionPipelineStatus;
        CompletedByteCount = completedByteCount;
        ExpectedByteCount = expectedByteCount;
        BackingByteCount = backingByteCount;
    }

    public bool IsInitialized
    {
        get;
    }

    public VirtioBlockReadSessionBlockDeviceCreateStatus Status
    {
        get;
    }

    public BlockDeviceStatus BlockDeviceStatus
    {
        get;
    }

    public VirtioBlockReadOnlyReadSessionStatus ReadSessionStatus
    {
        get;
    }

    public VirtioBlockReadQueuePipelineStatus ReadSessionPipelineStatus
    {
        get;
    }

    public ulong CompletedByteCount
    {
        get;
    }

    public ulong ExpectedByteCount
    {
        get;
    }

    public ulong BackingByteCount
    {
        get;
    }

    public bool Succeeded =>
        IsInitialized &&
        Status == VirtioBlockReadSessionBlockDeviceCreateStatus.Success;

    internal static VirtioBlockReadSessionBlockDeviceCreateResult Reject(
        VirtioBlockReadSessionBlockDeviceCreateStatus status,
        BlockDeviceStatus blockDeviceStatus,
        in VirtioBlockReadOnlyReadSessionResult session,
        ulong expectedByteCount,
        ulong backingByteCount)
    {
        return new VirtioBlockReadSessionBlockDeviceCreateResult(
            status,
            blockDeviceStatus,
            session.Status,
            session.Adapter.PipelineStatus,
            session.BlockRead.BytesRead,
            expectedByteCount,
            backingByteCount);
    }

    internal static VirtioBlockReadSessionBlockDeviceCreateResult Success(
        in VirtioBlockReadOnlyReadSessionResult session,
        BlockDeviceInfo info,
        ReadOnlyMemory<byte> completedDestination)
    {
        return new VirtioBlockReadSessionBlockDeviceCreateResult(
            VirtioBlockReadSessionBlockDeviceCreateStatus.Success,
            BlockDeviceStatus.Success,
            session.Status,
            session.Adapter.PipelineStatus,
            session.BlockRead.BytesRead,
            info.CapacityBytes,
            (ulong)completedDestination.Length);
    }
}
