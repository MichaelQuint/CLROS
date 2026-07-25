namespace OS.Storage;

public enum VirtioBlockPackageReadPreflightStatus
{
    Success = 0,
    IncompleteReadSession = 1,
    InvalidBlockDevice = 2,
    HeaderScratchTooSmall = 3,
    PartitionEntryScratchTooSmall = 4,
    PartitionDescriptorTableEmpty = 5,
    PackageBufferTooSmall = 6,
    PackageLoadFailed = 7
}

/// <summary>
/// Host-tested bridge that validates a caller-owned virtio-blk read-session and the
/// GPT/FAT/package scratch and package-loader buffers before delegating to the existing
/// <see cref="OsPackageLoader"/> contract. The preflight performs no live device I/O: it
/// neither notifies a device, waits for hardware, maps DMA, nor reads a real disk. The
/// caller supplies an already-validated in-memory block device for the package-loader
/// contract to read.
/// </summary>
public readonly struct VirtioBlockPackageReadPreflightResult
{
    private VirtioBlockPackageReadPreflightResult(
        VirtioBlockPackageReadPreflightStatus status,
        bool readSessionComplete,
        VirtioBlockReadOnlyReadSessionStatus readSessionStatus,
        VirtioBlockReadQueuePipelineStatus readSessionPipelineStatus,
        VirtioBlockQueueCursorState cursorBefore,
        VirtioBlockQueueCursorState cursorAfter,
        ulong readSessionByteCount,
        ulong headerScratchBytesRequired,
        ulong headerScratchBytesAvailable,
        ulong partitionEntryScratchBytesAvailable,
        int partitionDescriptorCapacity,
        ulong packageBufferBytesRequired,
        ulong packageBufferBytesAvailable,
        bool packageLoadAttempted,
        OsPackageLoadResult packageLoad)
    {
        IsInitialized = true;
        Status = status;
        ReadSessionComplete = readSessionComplete;
        ReadSessionStatus = readSessionStatus;
        ReadSessionPipelineStatus = readSessionPipelineStatus;
        CursorBefore = cursorBefore;
        CursorAfter = cursorAfter;
        ReadSessionByteCount = readSessionByteCount;
        HeaderScratchBytesRequired = headerScratchBytesRequired;
        HeaderScratchBytesAvailable = headerScratchBytesAvailable;
        PartitionEntryScratchBytesAvailable = partitionEntryScratchBytesAvailable;
        PartitionDescriptorCapacity = partitionDescriptorCapacity;
        PackageBufferBytesRequired = packageBufferBytesRequired;
        PackageBufferBytesAvailable = packageBufferBytesAvailable;
        PackageLoadAttempted = packageLoadAttempted;
        PackageLoad = packageLoad;
    }

    public VirtioBlockPackageReadPreflightStatus Status { get; }

    public bool IsInitialized { get; }

    public bool ReadSessionComplete { get; }

    public VirtioBlockReadOnlyReadSessionStatus ReadSessionStatus { get; }

    public VirtioBlockReadQueuePipelineStatus ReadSessionPipelineStatus { get; }

    public VirtioBlockQueueCursorState CursorBefore { get; }

    public VirtioBlockQueueCursorState CursorAfter { get; }

    public ulong ReadSessionByteCount { get; }

    public ulong HeaderScratchBytesRequired { get; }

    public ulong HeaderScratchBytesAvailable { get; }

    public ulong PartitionEntryScratchBytesAvailable { get; }

    public int PartitionDescriptorCapacity { get; }

    public ulong PackageBufferBytesRequired { get; }

    public ulong PackageBufferBytesAvailable { get; }

    public bool PackageLoadAttempted { get; }

    public OsPackageLoadResult PackageLoad { get; }

    public bool Succeeded => IsInitialized &&
        Status == VirtioBlockPackageReadPreflightStatus.Success;

    internal static VirtioBlockPackageReadPreflightResult Reject(
        VirtioBlockPackageReadPreflightStatus status,
        in VirtioBlockReadOnlyReadSessionResult session,
        ulong headerScratchBytesRequired,
        ulong headerScratchBytesAvailable,
        ulong partitionEntryScratchBytesAvailable,
        int partitionDescriptorCapacity,
        ulong packageBufferBytesRequired,
        ulong packageBufferBytesAvailable)
    {
        return new VirtioBlockPackageReadPreflightResult(
            status,
            session.Succeeded,
            session.Status,
            session.Adapter.PipelineStatus,
            session.CursorBefore,
            session.CursorAfter,
            session.BlockRead.BytesRead,
            headerScratchBytesRequired,
            headerScratchBytesAvailable,
            partitionEntryScratchBytesAvailable,
            partitionDescriptorCapacity,
            packageBufferBytesRequired,
            packageBufferBytesAvailable,
            packageLoadAttempted: false,
            default);
    }

    internal static VirtioBlockPackageReadPreflightResult FromPackageLoad(
        in VirtioBlockReadOnlyReadSessionResult session,
        ulong headerScratchBytesRequired,
        ulong headerScratchBytesAvailable,
        ulong partitionEntryScratchBytesAvailable,
        int partitionDescriptorCapacity,
        ulong packageBufferBytesRequired,
        ulong packageBufferBytesAvailable,
        OsPackageLoadResult packageLoad)
    {
        VirtioBlockPackageReadPreflightStatus status = packageLoad.Succeeded
            ? VirtioBlockPackageReadPreflightStatus.Success
            : VirtioBlockPackageReadPreflightStatus.PackageLoadFailed;

        return new VirtioBlockPackageReadPreflightResult(
            status,
            session.Succeeded,
            session.Status,
            session.Adapter.PipelineStatus,
            session.CursorBefore,
            session.CursorAfter,
            session.BlockRead.BytesRead,
            headerScratchBytesRequired,
            headerScratchBytesAvailable,
            partitionEntryScratchBytesAvailable,
            partitionDescriptorCapacity,
            packageBufferBytesRequired,
            packageBufferBytesAvailable,
            packageLoadAttempted: true,
            packageLoad);
    }
}

public static class VirtioBlockPackageReadPreflight
{
    public const bool ValidatesCallerOwnedReadSession = true;
    public const bool PreservesReadSessionProvenance = true;
    public const bool RejectsIncompleteReadSessionBeforePackageParse = true;
    public const bool ValidatesGptHeaderScratch = true;
    public const bool ValidatesPartitionEntryScratch = true;
    public const bool ValidatesPackageBuffer = true;
    public const bool ReportsScratchCapacity = true;
    public const bool DelegatesPackageLoaderContracts = true;
    public const bool RequiresCallerOwnedBlockDevice = true;
    public const bool NotifiesLiveDevice = false;
    public const bool WaitsForHardware = false;
    public const bool PerformsDmaMapping = false;
    public const bool AllocatesDmaMemory = false;
    public const bool MapsBars = false;
    public const bool PerformsMmio = false;
    public const bool AccessesPhysicalAddresses = false;
    public const bool ExecutesMemoryBarriers = false;
    public const bool ConfiguresLiveQueues = false;
    public const bool PollsDevice = false;
    public const bool HandlesInterrupts = false;
    public const bool ReadsRealDisk = false;
    public const bool WaitsForUsedRingCompletion = false;

    public const uint MinimumPackageBufferBytes = OsPackageParser.HeaderSize;

    public static VirtioBlockPackageReadPreflightResult LoadPackage(
        in VirtioBlockReadOnlyReadSessionResult session,
        IReadOnlyBlockDevice? disk,
        Span<byte> headerScratch,
        Span<byte> partitionEntryScratch,
        Span<GptPartitionDescriptor> partitions,
        Span<byte> packageBuffer)
    {
        ulong headerScratchBytesAvailable = (ulong)headerScratch.Length;
        ulong partitionEntryScratchBytesAvailable = (ulong)partitionEntryScratch.Length;
        ulong packageBufferBytesAvailable = (ulong)packageBuffer.Length;

        if (!session.Succeeded)
        {
            return VirtioBlockPackageReadPreflightResult.Reject(
                VirtioBlockPackageReadPreflightStatus.IncompleteReadSession,
                session,
                headerScratchBytesRequired: 0,
                headerScratchBytesAvailable,
                partitionEntryScratchBytesAvailable,
                partitions.Length,
                packageBufferBytesRequired: MinimumPackageBufferBytes,
                packageBufferBytesAvailable);
        }

        if (disk is null ||
            !BlockDeviceInfo.Create(disk.Info.BlockSize, disk.Info.BlockCount).Succeeded)
        {
            return VirtioBlockPackageReadPreflightResult.Reject(
                VirtioBlockPackageReadPreflightStatus.InvalidBlockDevice,
                session,
                headerScratchBytesRequired: 0,
                headerScratchBytesAvailable,
                partitionEntryScratchBytesAvailable,
                partitions.Length,
                packageBufferBytesRequired: MinimumPackageBufferBytes,
                packageBufferBytesAvailable);
        }

        uint blockSize = disk.Info.BlockSize;
        ulong headerScratchBytesRequired = blockSize;

        if (headerScratchBytesAvailable < headerScratchBytesRequired)
        {
            return VirtioBlockPackageReadPreflightResult.Reject(
                VirtioBlockPackageReadPreflightStatus.HeaderScratchTooSmall,
                session,
                headerScratchBytesRequired,
                headerScratchBytesAvailable,
                partitionEntryScratchBytesAvailable,
                partitions.Length,
                packageBufferBytesRequired: MinimumPackageBufferBytes,
                packageBufferBytesAvailable);
        }

        if (partitionEntryScratchBytesAvailable < blockSize)
        {
            return VirtioBlockPackageReadPreflightResult.Reject(
                VirtioBlockPackageReadPreflightStatus.PartitionEntryScratchTooSmall,
                session,
                headerScratchBytesRequired,
                headerScratchBytesAvailable,
                partitionEntryScratchBytesAvailable,
                partitions.Length,
                packageBufferBytesRequired: MinimumPackageBufferBytes,
                packageBufferBytesAvailable);
        }

        if (partitions.Length < 1)
        {
            return VirtioBlockPackageReadPreflightResult.Reject(
                VirtioBlockPackageReadPreflightStatus.PartitionDescriptorTableEmpty,
                session,
                headerScratchBytesRequired,
                headerScratchBytesAvailable,
                partitionEntryScratchBytesAvailable,
                partitions.Length,
                packageBufferBytesRequired: MinimumPackageBufferBytes,
                packageBufferBytesAvailable);
        }

        if (packageBufferBytesAvailable < MinimumPackageBufferBytes)
        {
            return VirtioBlockPackageReadPreflightResult.Reject(
                VirtioBlockPackageReadPreflightStatus.PackageBufferTooSmall,
                session,
                headerScratchBytesRequired,
                headerScratchBytesAvailable,
                partitionEntryScratchBytesAvailable,
                partitions.Length,
                packageBufferBytesRequired: MinimumPackageBufferBytes,
                packageBufferBytesAvailable);
        }

        OsPackageLoadResult packageLoad = OsPackageLoader.LoadFromPrimaryGptEspWithCallerBuffers(
            disk,
            headerScratch,
            partitionEntryScratch,
            partitions,
            packageBuffer);

        return VirtioBlockPackageReadPreflightResult.FromPackageLoad(
            session,
            headerScratchBytesRequired,
            headerScratchBytesAvailable,
            partitionEntryScratchBytesAvailable,
            partitions.Length,
            packageBufferBytesRequired: MinimumPackageBufferBytes,
            packageBufferBytesAvailable,
            packageLoad);
    }
}
