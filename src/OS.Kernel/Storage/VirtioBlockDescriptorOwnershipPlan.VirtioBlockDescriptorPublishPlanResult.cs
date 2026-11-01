using OS.Drivers;

namespace OS.Storage;

public readonly struct VirtioBlockDescriptorPublishPlanResult
{
    private VirtioBlockDescriptorPublishPlanResult(
        VirtioBlockDescriptorPublishPlanStatus status,
        VirtqueueSplitRingPlanStatus queuePlanStatus,
        VirtioBlockDescriptorFreeListStatus freeListStatus,
        VirtioBlockReadLifecyclePublishStatus lifecyclePublishStatus,
        VirtioBlockReadTransactionPlanStatus readPlanStatus,
        VirtioBlockRequestPlanStatus requestPlanStatus,
        VirtioBlockDescriptorChainPlanStatus descriptorPlanStatus,
        VirtioBlockDescriptorChainWriterStatus descriptorWriterStatus,
        VirtioBlockDescriptorOwnershipStatus ownershipStatus,
        bool lifecyclePublishAttempted,
        bool ownershipMarkAttempted,
        ushort requestHeaderDescriptorIndex,
        ushort dataBufferDescriptorIndex,
        ushort statusByteDescriptorIndex,
        ushort previousAvailableRingIndex,
        ushort nextAvailableRingIndex,
        ushort previousUsedIndex,
        ushort expectedNextUsedIndex,
        ushort availableRingSlot,
        uint availableRingEntryOffset,
        int stateTableLength,
        int requiredStateTableLength,
        ushort descriptorsSelected,
        ushort descriptorsWritten,
        ushort descriptorsMarkedInFlight,
        VirtioBlockDescriptorFreeListResult freeList,
        VirtioBlockReadLifecyclePublishResult lifecyclePublish,
        VirtioBlockDescriptorOwnershipPlanResult ownershipMark,
        bool isInitialized)
    {
        Status = status;
        QueuePlanStatus = queuePlanStatus;
        FreeListStatus = freeListStatus;
        LifecyclePublishStatus = lifecyclePublishStatus;
        ReadPlanStatus = readPlanStatus;
        RequestPlanStatus = requestPlanStatus;
        DescriptorPlanStatus = descriptorPlanStatus;
        DescriptorWriterStatus = descriptorWriterStatus;
        OwnershipStatus = ownershipStatus;
        LifecyclePublishAttempted = lifecyclePublishAttempted;
        OwnershipMarkAttempted = ownershipMarkAttempted;
        RequestHeaderDescriptorIndex = requestHeaderDescriptorIndex;
        DataBufferDescriptorIndex = dataBufferDescriptorIndex;
        StatusByteDescriptorIndex = statusByteDescriptorIndex;
        FirstDescriptorIndex = requestHeaderDescriptorIndex;
        PreviousAvailableRingIndex = previousAvailableRingIndex;
        NextAvailableRingIndex = nextAvailableRingIndex;
        PreviousUsedIndex = previousUsedIndex;
        ExpectedNextUsedIndex = expectedNextUsedIndex;
        AvailableRingSlot = availableRingSlot;
        AvailableRingEntryOffset = availableRingEntryOffset;
        StateTableLength = stateTableLength;
        RequiredStateTableLength = requiredStateTableLength;
        DescriptorsSelected = descriptorsSelected;
        DescriptorsWritten = descriptorsWritten;
        DescriptorsMarkedInFlight = descriptorsMarkedInFlight;
        FreeList = freeList;
        LifecyclePublish = lifecyclePublish;
        OwnershipMark = ownershipMark;
        IsInitialized = isInitialized;
    }

    public VirtioBlockDescriptorPublishPlanStatus Status { get; }

    public VirtqueueSplitRingPlanStatus QueuePlanStatus { get; }

    public VirtioBlockDescriptorFreeListStatus FreeListStatus { get; }

    public VirtioBlockReadLifecyclePublishStatus LifecyclePublishStatus { get; }

    public VirtioBlockReadTransactionPlanStatus ReadPlanStatus { get; }

    public VirtioBlockRequestPlanStatus RequestPlanStatus { get; }

    public VirtioBlockDescriptorChainPlanStatus DescriptorPlanStatus { get; }

    public VirtioBlockDescriptorChainWriterStatus DescriptorWriterStatus { get; }

    public VirtioBlockDescriptorOwnershipStatus OwnershipStatus { get; }

    public bool LifecyclePublishAttempted { get; }

    public bool OwnershipMarkAttempted { get; }

    public ushort RequestHeaderDescriptorIndex { get; }

    public ushort DataBufferDescriptorIndex { get; }

    public ushort StatusByteDescriptorIndex { get; }

    public ushort FirstDescriptorIndex { get; }

    public ushort PreviousAvailableRingIndex { get; }

    public ushort NextAvailableRingIndex { get; }

    public ushort PreviousUsedIndex { get; }

    public ushort ExpectedNextUsedIndex { get; }

    public ushort AvailableRingSlot { get; }

    public uint AvailableRingEntryOffset { get; }

    public int StateTableLength { get; }

    public int RequiredStateTableLength { get; }

    public ushort DescriptorsSelected { get; }

    public ushort DescriptorsWritten { get; }

    public ushort DescriptorsMarkedInFlight { get; }

    public VirtioBlockDescriptorFreeListResult FreeList { get; }

    public VirtioBlockReadLifecyclePublishResult LifecyclePublish { get; }

    public VirtioBlockDescriptorOwnershipPlanResult OwnershipMark { get; }

    public bool IsInitialized { get; }

    public bool Succeeded => IsInitialized && Status == VirtioBlockDescriptorPublishPlanStatus.Success;

    internal static VirtioBlockDescriptorPublishPlanResult Create(
        VirtioBlockDescriptorPublishPlanStatus status,
        VirtqueueSplitRingPlanStatus queuePlanStatus,
        VirtioBlockDescriptorFreeListResult freeList,
        VirtioBlockReadLifecyclePublishResult lifecyclePublish,
        VirtioBlockDescriptorOwnershipPlanResult ownershipMark,
        bool lifecyclePublishAttempted,
        bool ownershipMarkAttempted)
    {
        return new VirtioBlockDescriptorPublishPlanResult(
            status,
            queuePlanStatus,
            freeList.Status,
            lifecyclePublish.Status,
            lifecyclePublish.ReadPlanStatus,
            lifecyclePublish.RequestPlanStatus,
            lifecyclePublish.DescriptorPlanStatus,
            lifecyclePublish.DescriptorWriterStatus,
            ownershipMark.Status,
            lifecyclePublishAttempted,
            ownershipMarkAttempted,
            freeList.RequestHeaderDescriptorIndex,
            freeList.DataBufferDescriptorIndex,
            freeList.StatusByteDescriptorIndex,
            lifecyclePublish.PreviousAvailableRingIndex,
            lifecyclePublish.NextAvailableRingIndex,
            lifecyclePublish.PreviousUsedIndex,
            lifecyclePublish.ExpectedNextUsedIndex,
            lifecyclePublish.AvailableRingSlot,
            lifecyclePublish.AvailableRingEntryOffset,
            freeList.StateTableLength,
            freeList.RequiredStateTableLength,
            freeList.Succeeded ? freeList.DescriptorsRequired : (ushort)0,
            lifecyclePublish.DescriptorsWritten,
            ownershipMark.DescriptorsMarkedInFlight,
            freeList,
            lifecyclePublish,
            ownershipMark,
            isInitialized: true);
    }
}
