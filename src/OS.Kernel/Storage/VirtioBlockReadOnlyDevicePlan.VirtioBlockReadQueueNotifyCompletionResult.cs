using OS.Drivers;

namespace OS.Storage;

public readonly struct VirtioBlockReadQueueNotifyCompletionResult
{
    private VirtioBlockReadQueueNotifyCompletionResult(
        VirtioBlockReadQueueNotifyCompletionStatus status,
        VirtioBlockReadQueueNotifyReadinessResult notifyReadiness,
        VirtioBlockReadQueueCompletionResult completion,
        VirtioBlockReadQueuePublishStatus readQueuePublishStatus,
        bool completionAttempted,
        ushort previousAvailableRingIndex,
        ushort currentAvailableRingIndex,
        ushort previousUsedRingIndex,
        ushort currentUsedRingIndex,
        bool isInitialized)
    {
        Status = status;
        NotifyReadiness = notifyReadiness;
        Completion = completion;
        ReadQueuePublishStatus = readQueuePublishStatus;
        CompletionAttempted = completionAttempted;
        PreviousAvailableRingIndex = previousAvailableRingIndex;
        CurrentAvailableRingIndex = currentAvailableRingIndex;
        PreviousUsedRingIndex = previousUsedRingIndex;
        CurrentUsedRingIndex = currentUsedRingIndex;
        IsInitialized = isInitialized;
    }

    public VirtioBlockReadQueueNotifyCompletionStatus Status
    {
        get;
    }

    public VirtioBlockReadQueueNotifyReadinessResult NotifyReadiness
    {
        get;
    }

    public VirtioBlockReadQueueCompletionResult Completion
    {
        get;
    }

    public VirtioBlockReadQueuePublishStatus ReadQueuePublishStatus
    {
        get;
    }

    public bool CompletionAttempted
    {
        get;
    }

    public ushort PreviousAvailableRingIndex
    {
        get;
    }

    public ushort CurrentAvailableRingIndex
    {
        get;
    }

    public ushort PreviousUsedRingIndex
    {
        get;
    }

    public ushort CurrentUsedRingIndex
    {
        get;
    }

    public VirtioBlockReadQueueNotifyReadinessStatus NotifyReadinessStatus =>
        NotifyReadiness.Status;

    public VirtioBlockQueueNotificationMetadataStatus NotificationMetadataStatus =>
        NotifyReadiness.NotificationMetadataStatus;

    public VirtioBlockQueueNotifyOffsetPlanStatus NotifyOffsetPlanStatus =>
        NotifyReadiness.NotifyOffsetPlanStatus;

    public VirtioBlockQueueNotificationRegisterWindowBarrierExecutionStatus NotifyExecutionStatus =>
        NotifyReadiness.NotifyExecutionStatus;

    public VirtioBlockReadQueueCompletionStatus ReadQueueCompletionStatus =>
        CompletionAttempted ?
            Completion.Status :
            VirtioBlockReadQueueCompletionStatus.Success;

    public VirtioBlockQueueCursorReclaimStatus CursorReclaimStatus =>
        CompletionAttempted ?
            Completion.CursorReclaimStatus :
            VirtioBlockQueueCursorReclaimStatus.Success;

    public VirtioBlockDescriptorReclaimPlanStatus ReclaimPlanStatus =>
        CompletionAttempted ?
            Completion.ReclaimPlanStatus :
            VirtioBlockDescriptorReclaimPlanStatus.Success;

    public VirtioBlockReadLifecycleCompletionStatus CompletionStatus =>
        CompletionAttempted ?
            Completion.CompletionStatus :
            VirtioBlockReadLifecycleCompletionStatus.Success;

    public VirtioBlockUsedRingCompletionReaderStatus CompletionReaderStatus =>
        CompletionAttempted ?
            Completion.CompletionReaderStatus :
            VirtioBlockUsedRingCompletionReaderStatus.Success;

    public VirtioBlockDescriptorOwnershipStatus OwnershipStatus =>
        CompletionAttempted ?
            Completion.OwnershipStatus :
            VirtioBlockDescriptorOwnershipStatus.Success;

    public bool IsInitialized
    {
        get;
    }

    public bool Succeeded =>
        IsInitialized &&
        Status == VirtioBlockReadQueueNotifyCompletionStatus.Success;

    internal static VirtioBlockReadQueueNotifyCompletionResult Create(
        VirtioBlockReadQueueNotifyCompletionStatus status,
        VirtioBlockReadQueueNotifyReadinessResult notifyReadiness,
        VirtioBlockReadQueueCompletionResult completion,
        VirtioBlockReadQueuePublishStatus readQueuePublishStatus,
        bool completionAttempted,
        ushort previousAvailableRingIndex,
        ushort currentAvailableRingIndex,
        ushort previousUsedRingIndex,
        ushort currentUsedRingIndex)
    {
        return new VirtioBlockReadQueueNotifyCompletionResult(
            status,
            notifyReadiness,
            completion,
            readQueuePublishStatus,
            completionAttempted,
            previousAvailableRingIndex,
            currentAvailableRingIndex,
            previousUsedRingIndex,
            currentUsedRingIndex,
            isInitialized: true);
    }
}
