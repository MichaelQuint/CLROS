using OS.Drivers;

namespace OS.Storage;

public readonly struct VirtioBlockQueueCursorReclaimResult
{
    private VirtioBlockQueueCursorReclaimResult(
        VirtioBlockQueueCursorReclaimStatus status,
        VirtioBlockDescriptorReclaimPlanStatus reclaimPlanStatus,
        VirtioBlockDescriptorPublishPlanStatus publishPlanStatus,
        VirtioBlockReadLifecycleCompletionStatus completionStatus,
        VirtioBlockUsedRingCompletionReaderStatus completionReaderStatus,
        VirtioBlockDescriptorOwnershipStatus ownershipStatus,
        bool completionAttempted,
        bool ownershipReclaimAttempted,
        ushort previousAvailableRingIndex,
        ushort currentAvailableRingIndex,
        ushort previousUsedRingIndex,
        ushort currentUsedRingIndex,
        VirtioBlockDescriptorReclaimPlanResult reclaim,
        bool isInitialized)
    {
        Status = status;
        ReclaimPlanStatus = reclaimPlanStatus;
        PublishPlanStatus = publishPlanStatus;
        CompletionStatus = completionStatus;
        CompletionReaderStatus = completionReaderStatus;
        OwnershipStatus = ownershipStatus;
        CompletionAttempted = completionAttempted;
        OwnershipReclaimAttempted = ownershipReclaimAttempted;
        PreviousAvailableRingIndex = previousAvailableRingIndex;
        CurrentAvailableRingIndex = currentAvailableRingIndex;
        PreviousUsedRingIndex = previousUsedRingIndex;
        CurrentUsedRingIndex = currentUsedRingIndex;
        Reclaim = reclaim;
        IsInitialized = isInitialized;
    }

    public VirtioBlockQueueCursorReclaimStatus Status { get; }

    public VirtioBlockDescriptorReclaimPlanStatus ReclaimPlanStatus { get; }

    public VirtioBlockDescriptorPublishPlanStatus PublishPlanStatus { get; }

    public VirtioBlockReadLifecycleCompletionStatus CompletionStatus { get; }

    public VirtioBlockUsedRingCompletionReaderStatus CompletionReaderStatus { get; }

    public VirtioBlockDescriptorOwnershipStatus OwnershipStatus { get; }

    public bool CompletionAttempted { get; }

    public bool OwnershipReclaimAttempted { get; }

    public ushort PreviousAvailableRingIndex { get; }

    public ushort CurrentAvailableRingIndex { get; }

    public ushort PreviousUsedRingIndex { get; }

    public ushort CurrentUsedRingIndex { get; }

    public VirtioBlockDescriptorReclaimPlanResult Reclaim { get; }

    public bool IsInitialized { get; }

    public bool Succeeded => IsInitialized && Status == VirtioBlockQueueCursorReclaimStatus.Success;

    internal static VirtioBlockQueueCursorReclaimResult Create(
        VirtioBlockQueueCursorReclaimStatus status,
        VirtioBlockDescriptorReclaimPlanResult reclaim,
        ushort previousAvailableRingIndex,
        ushort currentAvailableRingIndex,
        ushort previousUsedRingIndex,
        ushort currentUsedRingIndex)
    {
        return new VirtioBlockQueueCursorReclaimResult(
            status,
            reclaim.Status,
            reclaim.PublishPlanStatus,
            reclaim.CompletionStatus,
            reclaim.CompletionReaderStatus,
            reclaim.OwnershipStatus,
            reclaim.CompletionAttempted,
            reclaim.OwnershipReclaimAttempted,
            previousAvailableRingIndex,
            currentAvailableRingIndex,
            previousUsedRingIndex,
            currentUsedRingIndex,
            reclaim,
            isInitialized: true);
    }
}
