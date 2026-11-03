using OS.Drivers;

namespace OS.Storage;

public readonly struct VirtioBlockDescriptorReclaimPlanResult
{
    private VirtioBlockDescriptorReclaimPlanResult(
        VirtioBlockDescriptorReclaimPlanStatus status,
        VirtioBlockDescriptorPublishPlanStatus publishPlanStatus,
        VirtioBlockDescriptorFreeListStatus freeListStatus,
        VirtioBlockReadLifecyclePublishStatus lifecyclePublishStatus,
        VirtioBlockReadLifecycleCompletionStatus completionStatus,
        VirtioBlockUsedRingCompletionReaderStatus completionReaderStatus,
        VirtioBlockDescriptorOwnershipStatus ownershipStatus,
        bool completionAttempted,
        bool ownershipReclaimAttempted,
        ushort requestHeaderDescriptorIndex,
        ushort dataBufferDescriptorIndex,
        ushort statusByteDescriptorIndex,
        ushort previousAvailableRingIndex,
        ushort nextAvailableRingIndex,
        ushort previousUsedIndex,
        ushort currentUsedIndex,
        ushort nextUsedIndex,
        ushort expectedNextUsedIndex,
        ushort usedRingSlot,
        uint usedRingElementOffset,
        uint completedDescriptorHead,
        uint completedByteCount,
        byte statusByte,
        int stateTableLength,
        int requiredStateTableLength,
        ushort descriptorsMarkedInFlight,
        ushort descriptorsFreed,
        VirtioBlockDescriptorPublishPlanResult publish,
        VirtioBlockReadLifecycleCompletionResult completion,
        VirtioBlockDescriptorOwnershipPlanResult ownershipReclaim,
        bool isInitialized)
    {
        Status = status;
        PublishPlanStatus = publishPlanStatus;
        FreeListStatus = freeListStatus;
        LifecyclePublishStatus = lifecyclePublishStatus;
        CompletionStatus = completionStatus;
        CompletionReaderStatus = completionReaderStatus;
        OwnershipStatus = ownershipStatus;
        CompletionAttempted = completionAttempted;
        OwnershipReclaimAttempted = ownershipReclaimAttempted;
        RequestHeaderDescriptorIndex = requestHeaderDescriptorIndex;
        DataBufferDescriptorIndex = dataBufferDescriptorIndex;
        StatusByteDescriptorIndex = statusByteDescriptorIndex;
        FirstDescriptorIndex = requestHeaderDescriptorIndex;
        PreviousAvailableRingIndex = previousAvailableRingIndex;
        NextAvailableRingIndex = nextAvailableRingIndex;
        PreviousUsedIndex = previousUsedIndex;
        CurrentUsedIndex = currentUsedIndex;
        NextUsedIndex = nextUsedIndex;
        ExpectedNextUsedIndex = expectedNextUsedIndex;
        UsedRingSlot = usedRingSlot;
        UsedRingElementOffset = usedRingElementOffset;
        CompletedDescriptorHead = completedDescriptorHead;
        CompletedByteCount = completedByteCount;
        StatusByte = statusByte;
        StateTableLength = stateTableLength;
        RequiredStateTableLength = requiredStateTableLength;
        DescriptorsMarkedInFlight = descriptorsMarkedInFlight;
        DescriptorsFreed = descriptorsFreed;
        Publish = publish;
        Completion = completion;
        OwnershipReclaim = ownershipReclaim;
        IsInitialized = isInitialized;
    }

    public VirtioBlockDescriptorReclaimPlanStatus Status
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

    public VirtioBlockReadLifecycleCompletionStatus CompletionStatus
    {
        get;
    }

    public VirtioBlockUsedRingCompletionReaderStatus CompletionReaderStatus
    {
        get;
    }

    public VirtioBlockDescriptorOwnershipStatus OwnershipStatus
    {
        get;
    }

    public bool CompletionAttempted
    {
        get;
    }

    public bool OwnershipReclaimAttempted
    {
        get;
    }

    public ushort RequestHeaderDescriptorIndex
    {
        get;
    }

    public ushort DataBufferDescriptorIndex
    {
        get;
    }

    public ushort StatusByteDescriptorIndex
    {
        get;
    }

    public ushort FirstDescriptorIndex
    {
        get;
    }

    public ushort PreviousAvailableRingIndex
    {
        get;
    }

    public ushort NextAvailableRingIndex
    {
        get;
    }

    public ushort PreviousUsedIndex
    {
        get;
    }

    public ushort CurrentUsedIndex
    {
        get;
    }

    public ushort NextUsedIndex
    {
        get;
    }

    public ushort ExpectedNextUsedIndex
    {
        get;
    }

    public ushort UsedRingSlot
    {
        get;
    }

    public uint UsedRingElementOffset
    {
        get;
    }

    public uint CompletedDescriptorHead
    {
        get;
    }

    public uint CompletedByteCount
    {
        get;
    }

    public byte StatusByte
    {
        get;
    }

    public int StateTableLength
    {
        get;
    }

    public int RequiredStateTableLength
    {
        get;
    }

    public ushort DescriptorsMarkedInFlight
    {
        get;
    }

    public ushort DescriptorsFreed
    {
        get;
    }

    public VirtioBlockDescriptorPublishPlanResult Publish
    {
        get;
    }

    public VirtioBlockReadLifecycleCompletionResult Completion
    {
        get;
    }

    public VirtioBlockDescriptorOwnershipPlanResult OwnershipReclaim
    {
        get;
    }

    public bool IsInitialized
    {
        get;
    }

    public bool Succeeded => IsInitialized && Status == VirtioBlockDescriptorReclaimPlanStatus.Success;

    internal static VirtioBlockDescriptorReclaimPlanResult Create(
        VirtioBlockDescriptorReclaimPlanStatus status,
        VirtioBlockDescriptorPublishPlanResult publish,
        VirtioBlockReadLifecycleCompletionResult completion,
        VirtioBlockDescriptorOwnershipPlanResult ownershipReclaim,
        bool completionAttempted,
        bool ownershipReclaimAttempted,
        int stateTableLength)
    {
        return new VirtioBlockDescriptorReclaimPlanResult(
            status,
            publish.Status,
            publish.FreeListStatus,
            publish.LifecyclePublishStatus,
            completionAttempted ? completion.Status : VirtioBlockReadLifecycleCompletionStatus.Success,
            completionAttempted
                ? completion.CompletionReaderStatus
                : VirtioBlockUsedRingCompletionReaderStatus.Success,
            ownershipReclaimAttempted ? ownershipReclaim.Status : VirtioBlockDescriptorOwnershipStatus.Success,
            completionAttempted,
            ownershipReclaimAttempted,
            publish.RequestHeaderDescriptorIndex,
            publish.DataBufferDescriptorIndex,
            publish.StatusByteDescriptorIndex,
            publish.PreviousAvailableRingIndex,
            publish.NextAvailableRingIndex,
            completionAttempted ? completion.PreviousUsedIndex : publish.PreviousUsedIndex,
            completionAttempted ? completion.CurrentUsedIndex : publish.PreviousUsedIndex,
            completionAttempted ? completion.NextUsedIndex : publish.PreviousUsedIndex,
            publish.ExpectedNextUsedIndex,
            completionAttempted ? completion.UsedRingSlot : (ushort)0,
            completionAttempted ? completion.UsedRingElementOffset : 0,
            completionAttempted ? completion.CompletedDescriptorHead : 0,
            completionAttempted ? completion.CompletedByteCount : 0,
            completionAttempted ? completion.StatusByte : (byte)0,
            ownershipReclaimAttempted ? ownershipReclaim.StateTableLength : stateTableLength,
            ownershipReclaimAttempted
                ? ownershipReclaim.RequiredStateTableLength
                : publish.RequiredStateTableLength,
            publish.DescriptorsMarkedInFlight,
            ownershipReclaim.DescriptorsFreed,
            publish,
            completion,
            ownershipReclaim,
            isInitialized: true);
    }
}
