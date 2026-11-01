using OS.Drivers;

namespace OS.Storage;

public readonly struct VirtioBlockReadQueueBlockReadProjectionResult
{
    private VirtioBlockReadQueueBlockReadProjectionResult(
        VirtioBlockReadQueueBlockReadProjectionStatus status,
        BlockReadResult blockRead,
        VirtioBlockReadQueueCompletionStatus readQueueCompletionStatus,
        VirtioBlockReadQueuePublishStatus readQueuePublishStatus,
        VirtioBlockQueueCursorReclaimStatus cursorReclaimStatus,
        VirtioBlockDescriptorReclaimPlanStatus reclaimPlanStatus,
        VirtioBlockUsedRingCompletionReaderStatus completionReaderStatus,
        VirtioBlockDescriptorOwnershipStatus ownershipStatus,
        uint requestedBlockCount,
        ulong requestedByteCount,
        uint completedByteCount,
        bool isInitialized)
    {
        Status = status;
        BlockRead = blockRead;
        ReadQueueCompletionStatus = readQueueCompletionStatus;
        ReadQueuePublishStatus = readQueuePublishStatus;
        CursorReclaimStatus = cursorReclaimStatus;
        ReclaimPlanStatus = reclaimPlanStatus;
        CompletionReaderStatus = completionReaderStatus;
        OwnershipStatus = ownershipStatus;
        RequestedBlockCount = requestedBlockCount;
        RequestedByteCount = requestedByteCount;
        CompletedByteCount = completedByteCount;
        IsInitialized = isInitialized;
    }

    public VirtioBlockReadQueueBlockReadProjectionStatus Status { get; }

    public BlockReadResult BlockRead { get; }

    public VirtioBlockReadQueueCompletionStatus ReadQueueCompletionStatus { get; }

    public VirtioBlockReadQueuePublishStatus ReadQueuePublishStatus { get; }

    public VirtioBlockQueueCursorReclaimStatus CursorReclaimStatus { get; }

    public VirtioBlockDescriptorReclaimPlanStatus ReclaimPlanStatus { get; }

    public VirtioBlockUsedRingCompletionReaderStatus CompletionReaderStatus { get; }

    public VirtioBlockDescriptorOwnershipStatus OwnershipStatus { get; }

    public uint RequestedBlockCount { get; }

    public ulong RequestedByteCount { get; }

    public uint CompletedByteCount { get; }

    public bool IsInitialized { get; }

    public bool Succeeded => IsInitialized && Status == VirtioBlockReadQueueBlockReadProjectionStatus.Success;

    internal static VirtioBlockReadQueueBlockReadProjectionResult Create(
        VirtioBlockReadQueueBlockReadProjectionStatus status,
        BlockReadResult blockRead,
        VirtioBlockReadQueueCompletionResult completion)
    {
        return new VirtioBlockReadQueueBlockReadProjectionResult(
            status,
            blockRead,
            completion.Status,
            completion.ReadQueuePublishStatus,
            completion.CursorReclaimStatus,
            completion.ReclaimPlanStatus,
            completion.CompletionReaderStatus,
            completion.OwnershipStatus,
            completion.ReadQueuePublish.SectorCount,
            completion.ReadQueuePublish.RequestedByteCount,
            completion.CursorReclaim.Reclaim.CompletedByteCount,
            isInitialized: true);
    }
}
