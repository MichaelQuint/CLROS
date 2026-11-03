using OS.Drivers;

namespace OS.Storage;

public readonly struct VirtioBlockQueueCursorPublishResult
{
    private VirtioBlockQueueCursorPublishResult(
        VirtioBlockQueueCursorPublishStatus status,
        VirtioBlockDescriptorPublishPlanStatus publishPlanStatus,
        VirtioBlockDescriptorFreeListStatus freeListStatus,
        VirtioBlockReadLifecyclePublishStatus lifecyclePublishStatus,
        VirtioBlockDescriptorOwnershipStatus ownershipStatus,
        ushort previousAvailableRingIndex,
        ushort currentAvailableRingIndex,
        ushort previousUsedRingIndex,
        ushort currentUsedRingIndex,
        VirtioBlockDescriptorPublishPlanResult publish,
        bool isInitialized)
    {
        Status = status;
        PublishPlanStatus = publishPlanStatus;
        FreeListStatus = freeListStatus;
        LifecyclePublishStatus = lifecyclePublishStatus;
        OwnershipStatus = ownershipStatus;
        PreviousAvailableRingIndex = previousAvailableRingIndex;
        CurrentAvailableRingIndex = currentAvailableRingIndex;
        PreviousUsedRingIndex = previousUsedRingIndex;
        CurrentUsedRingIndex = currentUsedRingIndex;
        Publish = publish;
        IsInitialized = isInitialized;
    }

    public VirtioBlockQueueCursorPublishStatus Status
    {
        get;
    }

    public VirtioBlockDescriptorPublishPlanStatus PublishPlanStatus
    {
        get;
    }

    public VirtioBlockDescriptorFreeListStatus FreeListStatus
    {
        get;
    }

    public VirtioBlockReadLifecyclePublishStatus LifecyclePublishStatus
    {
        get;
    }

    public VirtioBlockDescriptorOwnershipStatus OwnershipStatus
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

    public VirtioBlockDescriptorPublishPlanResult Publish
    {
        get;
    }

    public bool IsInitialized
    {
        get;
    }

    public bool Succeeded => IsInitialized && Status == VirtioBlockQueueCursorPublishStatus.Success;

    internal static VirtioBlockQueueCursorPublishResult Create(
        VirtioBlockQueueCursorPublishStatus status,
        VirtioBlockDescriptorPublishPlanResult publish,
        ushort previousAvailableRingIndex,
        ushort currentAvailableRingIndex,
        ushort previousUsedRingIndex,
        ushort currentUsedRingIndex)
    {
        return new VirtioBlockQueueCursorPublishResult(
            status,
            publish.Status,
            publish.FreeListStatus,
            publish.LifecyclePublishStatus,
            publish.OwnershipStatus,
            previousAvailableRingIndex,
            currentAvailableRingIndex,
            previousUsedRingIndex,
            currentUsedRingIndex,
            publish,
            isInitialized: true);
    }
}
