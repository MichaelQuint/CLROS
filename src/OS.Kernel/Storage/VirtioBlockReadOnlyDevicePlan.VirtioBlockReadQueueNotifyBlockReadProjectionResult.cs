using OS.Drivers;

namespace OS.Storage;

public readonly struct VirtioBlockReadQueueNotifyBlockReadProjectionResult
{
    private VirtioBlockReadQueueNotifyBlockReadProjectionResult(
        VirtioBlockReadQueueNotifyBlockReadProjectionStatus status,
        BlockReadResult blockRead,
        VirtioBlockReadQueueNotifyCompletionStatus notifyCompletionStatus,
        VirtioBlockReadQueueNotifyReadinessStatus notifyReadinessStatus,
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
        NotifyCompletionStatus = notifyCompletionStatus;
        NotifyReadinessStatus = notifyReadinessStatus;
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

    public VirtioBlockReadQueueNotifyBlockReadProjectionStatus Status { get; }

    public BlockReadResult BlockRead { get; }

    public VirtioBlockReadQueueNotifyCompletionStatus NotifyCompletionStatus { get; }

    public VirtioBlockReadQueueNotifyReadinessStatus NotifyReadinessStatus { get; }

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

    public bool Succeeded =>
        IsInitialized &&
        Status == VirtioBlockReadQueueNotifyBlockReadProjectionStatus.Success;

    internal static VirtioBlockReadQueueNotifyBlockReadProjectionResult Create(
        VirtioBlockReadQueueNotifyBlockReadProjectionStatus status,
        BlockReadResult blockRead,
        VirtioBlockReadQueueNotifyCompletionResult notifyCompletion,
        VirtioBlockReadQueueBlockReadProjectionResult readQueueProjection)
    {
        bool completionAttempted = notifyCompletion.CompletionAttempted;
        return new VirtioBlockReadQueueNotifyBlockReadProjectionResult(
            status,
            blockRead,
            notifyCompletion.Status,
            notifyCompletion.NotifyReadinessStatus,
            notifyCompletion.ReadQueueCompletionStatus,
            notifyCompletion.ReadQueuePublishStatus,
            notifyCompletion.CursorReclaimStatus,
            notifyCompletion.ReclaimPlanStatus,
            notifyCompletion.CompletionReaderStatus,
            notifyCompletion.OwnershipStatus,
            completionAttempted ? readQueueProjection.RequestedBlockCount : 0,
            completionAttempted ? readQueueProjection.RequestedByteCount : 0,
            completionAttempted ? readQueueProjection.CompletedByteCount : 0,
            isInitialized: true);
    }
}
