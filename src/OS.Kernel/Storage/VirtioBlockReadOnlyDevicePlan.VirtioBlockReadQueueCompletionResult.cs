using OS.Drivers;

namespace OS.Storage;

public readonly struct VirtioBlockReadQueueCompletionResult
{
    private VirtioBlockReadQueueCompletionResult(
        VirtioBlockReadQueueCompletionStatus status,
        VirtioBlockReadQueuePublishStatus readQueuePublishStatus,
        VirtioBlockQueueCursorReclaimStatus cursorReclaimStatus,
        VirtioBlockDescriptorReclaimPlanStatus reclaimPlanStatus,
        VirtioBlockReadLifecycleCompletionStatus completionStatus,
        VirtioBlockUsedRingCompletionReaderStatus completionReaderStatus,
        VirtioBlockDescriptorOwnershipStatus ownershipStatus,
        bool cursorReclaimAttempted,
        ushort previousAvailableRingIndex,
        ushort currentAvailableRingIndex,
        ushort previousUsedRingIndex,
        ushort currentUsedRingIndex,
        VirtioBlockReadQueuePublishResult readQueuePublish,
        VirtioBlockQueueCursorReclaimResult cursorReclaim,
        bool isInitialized)
    {
        Status = status;
        ReadQueuePublishStatus = readQueuePublishStatus;
        CursorReclaimStatus = cursorReclaimStatus;
        ReclaimPlanStatus = reclaimPlanStatus;
        CompletionStatus = completionStatus;
        CompletionReaderStatus = completionReaderStatus;
        OwnershipStatus = ownershipStatus;
        CursorReclaimAttempted = cursorReclaimAttempted;
        PreviousAvailableRingIndex = previousAvailableRingIndex;
        CurrentAvailableRingIndex = currentAvailableRingIndex;
        PreviousUsedRingIndex = previousUsedRingIndex;
        CurrentUsedRingIndex = currentUsedRingIndex;
        ReadQueuePublish = readQueuePublish;
        CursorReclaim = cursorReclaim;
        IsInitialized = isInitialized;
    }

    public VirtioBlockReadQueueCompletionStatus Status { get; }

    public VirtioBlockReadQueuePublishStatus ReadQueuePublishStatus { get; }

    public VirtioBlockQueueCursorReclaimStatus CursorReclaimStatus { get; }

    public VirtioBlockDescriptorReclaimPlanStatus ReclaimPlanStatus { get; }

    public VirtioBlockReadLifecycleCompletionStatus CompletionStatus { get; }

    public VirtioBlockUsedRingCompletionReaderStatus CompletionReaderStatus { get; }

    public VirtioBlockDescriptorOwnershipStatus OwnershipStatus { get; }

    public bool CursorReclaimAttempted { get; }

    public ushort PreviousAvailableRingIndex { get; }

    public ushort CurrentAvailableRingIndex { get; }

    public ushort PreviousUsedRingIndex { get; }

    public ushort CurrentUsedRingIndex { get; }

    public VirtioBlockReadQueuePublishResult ReadQueuePublish { get; }

    public VirtioBlockQueueCursorReclaimResult CursorReclaim { get; }

    public bool IsInitialized { get; }

    public bool Succeeded => IsInitialized && Status == VirtioBlockReadQueueCompletionStatus.Success;

    internal static VirtioBlockReadQueueCompletionResult Create(
        VirtioBlockReadQueueCompletionStatus status,
        VirtioBlockReadQueuePublishResult readQueuePublish,
        VirtioBlockQueueCursorReclaimResult cursorReclaim,
        bool cursorReclaimAttempted,
        ushort previousAvailableRingIndex,
        ushort currentAvailableRingIndex,
        ushort previousUsedRingIndex,
        ushort currentUsedRingIndex)
    {
        return new VirtioBlockReadQueueCompletionResult(
            status,
            readQueuePublish.Status,
            cursorReclaimAttempted
                ? cursorReclaim.Status
                : VirtioBlockQueueCursorReclaimStatus.Success,
            cursorReclaimAttempted
                ? cursorReclaim.ReclaimPlanStatus
                : VirtioBlockDescriptorReclaimPlanStatus.Success,
            cursorReclaimAttempted
                ? cursorReclaim.CompletionStatus
                : VirtioBlockReadLifecycleCompletionStatus.Success,
            cursorReclaimAttempted
                ? cursorReclaim.CompletionReaderStatus
                : VirtioBlockUsedRingCompletionReaderStatus.Success,
            cursorReclaimAttempted
                ? cursorReclaim.OwnershipStatus
                : VirtioBlockDescriptorOwnershipStatus.Success,
            cursorReclaimAttempted,
            previousAvailableRingIndex,
            currentAvailableRingIndex,
            previousUsedRingIndex,
            currentUsedRingIndex,
            readQueuePublish,
            cursorReclaim,
            isInitialized: true);
    }
}
