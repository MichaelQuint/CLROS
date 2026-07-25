namespace OS.Storage;

public enum VirtioBlockReadSessionBlockDeviceCreateStatus
{
    Success = 0,
    IncompleteReadSession = 1,
    InvalidBlockGeometry = 2,
    MismatchedCompletedByteCount = 3,
    BackingBytesTooSmall = 4
}

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

    public bool IsInitialized { get; }

    public VirtioBlockReadSessionBlockDeviceCreateStatus Status { get; }

    public BlockDeviceStatus BlockDeviceStatus { get; }

    public VirtioBlockReadOnlyReadSessionStatus ReadSessionStatus { get; }

    public VirtioBlockReadQueuePipelineStatus ReadSessionPipelineStatus { get; }

    public ulong CompletedByteCount { get; }

    public ulong ExpectedByteCount { get; }

    public ulong BackingByteCount { get; }

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

public sealed class VirtioBlockReadSessionBlockDevice : IReadOnlyBlockDevice
{
    private readonly ReadOnlyMemory<byte> completedDestination;

    internal VirtioBlockReadSessionBlockDevice(
        BlockDeviceInfo info,
        ReadOnlyMemory<byte> completedDestination)
    {
        Info = info;
        this.completedDestination = completedDestination;
    }

    public const bool RequiresCompletedReadSession = true;
    public const bool RequiresFullDiskReadSession = true;
    public const bool UsesCallerOwnedCompletedDestination = true;
    public const bool CopiesFromCompletedDestination = true;
    public const bool PerformsMmio = false;
    public const bool PerformsDmaMapping = false;
    public const bool AllocatesDmaMemory = false;
    public const bool MapsBars = false;
    public const bool AccessesPhysicalAddresses = false;
    public const bool ExecutesMemoryBarriers = false;
    public const bool ConfiguresLiveQueues = false;
    public const bool NotifiesLiveDevice = false;
    public const bool PollsDevice = false;
    public const bool HandlesInterrupts = false;
    public const bool CallsPal = false;
    public const bool PerformsDiskRead = false;
    public const bool ReadsRealDisk = false;

    public BlockDeviceInfo Info { get; }

    public static VirtioBlockReadSessionBlockDeviceCreateResult Create(
        in VirtioBlockReadOnlyReadSessionResult session,
        BlockDeviceInfo expectedInfo,
        ReadOnlyMemory<byte> completedDestination,
        out VirtioBlockReadSessionBlockDevice? device)
    {
        device = null;
        ulong backingByteCount = (ulong)completedDestination.Length;
        BlockDeviceStatus geometry = VirtioBlockReadOnlyDevicePlan.ValidateGeometry(expectedInfo);
        if (geometry != BlockDeviceStatus.Success)
        {
            return VirtioBlockReadSessionBlockDeviceCreateResult.Reject(
                VirtioBlockReadSessionBlockDeviceCreateStatus.InvalidBlockGeometry,
                geometry,
                session,
                expectedInfo.CapacityBytes,
                backingByteCount);
        }

        if (!session.Succeeded)
        {
            return VirtioBlockReadSessionBlockDeviceCreateResult.Reject(
                VirtioBlockReadSessionBlockDeviceCreateStatus.IncompleteReadSession,
                BlockDeviceStatus.IoError,
                session,
                expectedInfo.CapacityBytes,
                backingByteCount);
        }

        if (session.Adapter.Pipeline.Publish.FirstSector != 0 ||
            session.Adapter.Pipeline.Publish.SectorCount != expectedInfo.BlockCount ||
            session.BlockRead.BlocksRead != expectedInfo.BlockCount ||
            session.BlockRead.BytesRead != expectedInfo.CapacityBytes)
        {
            return VirtioBlockReadSessionBlockDeviceCreateResult.Reject(
                VirtioBlockReadSessionBlockDeviceCreateStatus.MismatchedCompletedByteCount,
                BlockDeviceStatus.InvalidBlockRange,
                session,
                expectedInfo.CapacityBytes,
                backingByteCount);
        }

        if (backingByteCount < expectedInfo.CapacityBytes)
        {
            return VirtioBlockReadSessionBlockDeviceCreateResult.Reject(
                VirtioBlockReadSessionBlockDeviceCreateStatus.BackingBytesTooSmall,
                BlockDeviceStatus.DestinationTooSmall,
                session,
                expectedInfo.CapacityBytes,
                backingByteCount);
        }

        device = new VirtioBlockReadSessionBlockDevice(expectedInfo, completedDestination);
        return VirtioBlockReadSessionBlockDeviceCreateResult.Success(
            session,
            expectedInfo,
            completedDestination);
    }

    public BlockReadResult ReadBlocks(
        ulong firstBlock,
        uint blockCount,
        Span<byte> destination)
    {
        BlockReadResult validation = BlockDeviceRead.Validate(
            Info,
            firstBlock,
            blockCount,
            destination);
        if (!validation.Succeeded)
        {
            return validation;
        }

        if (validation.BytesRead == 0)
        {
            return validation;
        }

        ulong sourceOffset = firstBlock * Info.BlockSize;
        if (sourceOffset > int.MaxValue ||
            validation.BytesRead > int.MaxValue)
        {
            return BlockReadResult.Fail(BlockDeviceStatus.Overflow);
        }

        completedDestination.Span
            .Slice((int)sourceOffset, (int)validation.BytesRead)
            .CopyTo(destination);
        return validation;
    }
}
