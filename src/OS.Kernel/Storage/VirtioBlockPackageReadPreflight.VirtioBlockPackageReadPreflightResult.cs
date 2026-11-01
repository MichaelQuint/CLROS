namespace OS.Storage;

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
