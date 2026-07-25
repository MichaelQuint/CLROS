using OS.Drivers;

namespace OS.Storage;

public enum VirtioBlockDescriptorOwnershipState
{
    Free = 0,
    InFlight = 1
}

public enum VirtioBlockDescriptorOwnershipStatus
{
    Success = 0,
    PublishFailed = 1,
    CompletionFailed = 2,
    StateTableTooSmall = 3,
    DescriptorAlreadyInFlight = 4,
    DescriptorNotInFlight = 5,
    DuplicateCompletion = 6
}

public enum VirtioBlockDescriptorFreeListStatus
{
    Success = 0,
    QueuePlanFailed = 1,
    StateTableTooSmall = 2,
    InvalidDescriptorState = 3,
    NoFreeDescriptorTriple = 4
}

public readonly struct VirtioBlockDescriptorFreeListResult
{
    private VirtioBlockDescriptorFreeListResult(
        VirtioBlockDescriptorFreeListStatus status,
        VirtqueueSplitRingPlanStatus queuePlanStatus,
        ushort requestHeaderDescriptorIndex,
        ushort dataBufferDescriptorIndex,
        ushort statusByteDescriptorIndex,
        int stateTableLength,
        int requiredStateTableLength,
        ushort descriptorsRequired,
        ushort descriptorsScanned,
        ushort invalidDescriptorIndex,
        bool isInitialized)
    {
        Status = status;
        QueuePlanStatus = queuePlanStatus;
        RequestHeaderDescriptorIndex = requestHeaderDescriptorIndex;
        DataBufferDescriptorIndex = dataBufferDescriptorIndex;
        StatusByteDescriptorIndex = statusByteDescriptorIndex;
        FirstDescriptorIndex = requestHeaderDescriptorIndex;
        StateTableLength = stateTableLength;
        RequiredStateTableLength = requiredStateTableLength;
        DescriptorsRequired = descriptorsRequired;
        DescriptorsScanned = descriptorsScanned;
        InvalidDescriptorIndex = invalidDescriptorIndex;
        IsInitialized = isInitialized;
    }

    public VirtioBlockDescriptorFreeListStatus Status { get; }

    public VirtqueueSplitRingPlanStatus QueuePlanStatus { get; }

    public ushort RequestHeaderDescriptorIndex { get; }

    public ushort DataBufferDescriptorIndex { get; }

    public ushort StatusByteDescriptorIndex { get; }

    public ushort FirstDescriptorIndex { get; }

    public int StateTableLength { get; }

    public int RequiredStateTableLength { get; }

    public ushort DescriptorsRequired { get; }

    public ushort DescriptorsScanned { get; }

    public ushort InvalidDescriptorIndex { get; }

    public bool IsInitialized { get; }

    public bool Succeeded => IsInitialized && Status == VirtioBlockDescriptorFreeListStatus.Success;

    internal static VirtioBlockDescriptorFreeListResult Create(
        VirtioBlockDescriptorFreeListStatus status,
        VirtqueueSplitRingPlanStatus queuePlanStatus,
        ushort requestHeaderDescriptorIndex,
        ushort dataBufferDescriptorIndex,
        ushort statusByteDescriptorIndex,
        int stateTableLength,
        int requiredStateTableLength,
        ushort descriptorsRequired,
        ushort descriptorsScanned,
        ushort invalidDescriptorIndex)
    {
        return new VirtioBlockDescriptorFreeListResult(
            status,
            queuePlanStatus,
            requestHeaderDescriptorIndex,
            dataBufferDescriptorIndex,
            statusByteDescriptorIndex,
            stateTableLength,
            requiredStateTableLength,
            descriptorsRequired,
            descriptorsScanned,
            invalidDescriptorIndex,
            isInitialized: true);
    }
}

public readonly struct VirtioBlockDescriptorOwnershipPlanResult
{
    private VirtioBlockDescriptorOwnershipPlanResult(
        VirtioBlockDescriptorOwnershipStatus status,
        VirtioBlockReadLifecyclePublishStatus publishStatus,
        VirtioBlockReadLifecycleCompletionStatus completionStatus,
        VirtioBlockUsedRingCompletionReaderStatus completionReaderStatus,
        ushort requestHeaderDescriptorIndex,
        ushort dataBufferDescriptorIndex,
        ushort statusByteDescriptorIndex,
        int stateTableLength,
        int requiredStateTableLength,
        ushort descriptorsMarkedInFlight,
        ushort descriptorsFreed,
        bool isInitialized)
    {
        Status = status;
        PublishStatus = publishStatus;
        CompletionStatus = completionStatus;
        CompletionReaderStatus = completionReaderStatus;
        RequestHeaderDescriptorIndex = requestHeaderDescriptorIndex;
        DataBufferDescriptorIndex = dataBufferDescriptorIndex;
        StatusByteDescriptorIndex = statusByteDescriptorIndex;
        FirstDescriptorIndex = requestHeaderDescriptorIndex;
        StateTableLength = stateTableLength;
        RequiredStateTableLength = requiredStateTableLength;
        DescriptorsMarkedInFlight = descriptorsMarkedInFlight;
        DescriptorsFreed = descriptorsFreed;
        IsInitialized = isInitialized;
    }

    public VirtioBlockDescriptorOwnershipStatus Status { get; }

    public VirtioBlockReadLifecyclePublishStatus PublishStatus { get; }

    public VirtioBlockReadLifecycleCompletionStatus CompletionStatus { get; }

    public VirtioBlockUsedRingCompletionReaderStatus CompletionReaderStatus { get; }

    public ushort RequestHeaderDescriptorIndex { get; }

    public ushort DataBufferDescriptorIndex { get; }

    public ushort StatusByteDescriptorIndex { get; }

    public ushort FirstDescriptorIndex { get; }

    public int StateTableLength { get; }

    public int RequiredStateTableLength { get; }

    public ushort DescriptorsMarkedInFlight { get; }

    public ushort DescriptorsFreed { get; }

    public bool IsInitialized { get; }

    public bool Succeeded => IsInitialized && Status == VirtioBlockDescriptorOwnershipStatus.Success;

    internal static VirtioBlockDescriptorOwnershipPlanResult Create(
        VirtioBlockDescriptorOwnershipStatus status,
        VirtioBlockReadLifecyclePublishStatus publishStatus,
        VirtioBlockReadLifecycleCompletionStatus completionStatus,
        VirtioBlockUsedRingCompletionReaderStatus completionReaderStatus,
        ushort requestHeaderDescriptorIndex,
        ushort dataBufferDescriptorIndex,
        ushort statusByteDescriptorIndex,
        int stateTableLength,
        int requiredStateTableLength,
        ushort descriptorsMarkedInFlight,
        ushort descriptorsFreed)
    {
        return new VirtioBlockDescriptorOwnershipPlanResult(
            status,
            publishStatus,
            completionStatus,
            completionReaderStatus,
            requestHeaderDescriptorIndex,
            dataBufferDescriptorIndex,
            statusByteDescriptorIndex,
            stateTableLength,
            requiredStateTableLength,
            descriptorsMarkedInFlight,
            descriptorsFreed,
            isInitialized: true);
    }
}

public static class VirtioBlockDescriptorFreeListPlanner
{
    public const bool PerformsMmio = false;
    public const bool NotifiesDevice = false;
    public const bool PollsDevice = false;
    public const bool HandlesInterrupts = false;
    public const bool PerformsDmaMapping = false;
    public const bool AllocatesDmaMemory = false;
    public const bool PerformsBlockIo = false;
    public const bool PerformsDiskRead = false;
    public const bool ReadsQueueMemory = false;
    public const bool MutatesCallerOwnedStateTable = false;

    public static VirtioBlockDescriptorFreeListResult SelectFreeDescriptorTriple(
        VirtqueueSplitRingPlanResult queuePlan,
        ReadOnlySpan<VirtioBlockDescriptorOwnershipState> descriptorStates)
    {
        if (!queuePlan.Succeeded ||
            VirtqueueSplitRingPlan.ValidateQueueSize(queuePlan.Plan.QueueSize) !=
                VirtqueueSplitRingPlanStatus.Success)
        {
            return CreateFailure(
                VirtioBlockDescriptorFreeListStatus.QueuePlanFailed,
                queuePlan.Status,
                descriptorStates.Length,
                requiredStateTableLength: 0);
        }

        ushort queueSize = queuePlan.Plan.QueueSize;
        if (descriptorStates.Length < queueSize)
        {
            return CreateFailure(
                VirtioBlockDescriptorFreeListStatus.StateTableTooSmall,
                queuePlan.Status,
                descriptorStates.Length,
                queueSize);
        }

        for (ushort index = 0; index < queueSize; index++)
        {
            VirtioBlockDescriptorOwnershipState state = descriptorStates[index];
            if (!IsValidState(state))
            {
                return VirtioBlockDescriptorFreeListResult.Create(
                    VirtioBlockDescriptorFreeListStatus.InvalidDescriptorState,
                    queuePlan.Status,
                    requestHeaderDescriptorIndex: 0,
                    dataBufferDescriptorIndex: 0,
                    statusByteDescriptorIndex: 0,
                    descriptorStates.Length,
                    queueSize,
                    VirtioBlockDescriptorChainPlan.RequiredDescriptorCount,
                    descriptorsScanned: index,
                    invalidDescriptorIndex: index);
            }
        }

        if (queueSize < VirtioBlockDescriptorChainPlan.RequiredDescriptorCount)
        {
            return VirtioBlockDescriptorFreeListResult.Create(
                VirtioBlockDescriptorFreeListStatus.NoFreeDescriptorTriple,
                queuePlan.Status,
                requestHeaderDescriptorIndex: 0,
                dataBufferDescriptorIndex: 0,
                statusByteDescriptorIndex: 0,
                descriptorStates.Length,
                queueSize,
                VirtioBlockDescriptorChainPlan.RequiredDescriptorCount,
                descriptorsScanned: queueSize,
                invalidDescriptorIndex: 0);
        }

        ushort lastFirstDescriptor =
            (ushort)(queueSize - VirtioBlockDescriptorChainPlan.RequiredDescriptorCount);
        for (ushort index = 0; index <= lastFirstDescriptor; index++)
        {
            ushort dataDescriptorIndex = (ushort)(index + 1);
            ushort statusDescriptorIndex = (ushort)(index + 2);
            if (descriptorStates[index] == VirtioBlockDescriptorOwnershipState.Free &&
                descriptorStates[dataDescriptorIndex] == VirtioBlockDescriptorOwnershipState.Free &&
                descriptorStates[statusDescriptorIndex] == VirtioBlockDescriptorOwnershipState.Free)
            {
                return VirtioBlockDescriptorFreeListResult.Create(
                    VirtioBlockDescriptorFreeListStatus.Success,
                    queuePlan.Status,
                    index,
                    dataDescriptorIndex,
                    statusDescriptorIndex,
                    descriptorStates.Length,
                    queueSize,
                    VirtioBlockDescriptorChainPlan.RequiredDescriptorCount,
                    descriptorsScanned: (ushort)(statusDescriptorIndex + 1),
                    invalidDescriptorIndex: 0);
            }
        }

        return VirtioBlockDescriptorFreeListResult.Create(
            VirtioBlockDescriptorFreeListStatus.NoFreeDescriptorTriple,
            queuePlan.Status,
            requestHeaderDescriptorIndex: 0,
            dataBufferDescriptorIndex: 0,
            statusByteDescriptorIndex: 0,
            descriptorStates.Length,
            queueSize,
            VirtioBlockDescriptorChainPlan.RequiredDescriptorCount,
            queueSize,
            invalidDescriptorIndex: 0);
    }

    private static bool IsValidState(VirtioBlockDescriptorOwnershipState state)
    {
        return state == VirtioBlockDescriptorOwnershipState.Free ||
            state == VirtioBlockDescriptorOwnershipState.InFlight;
    }

    private static VirtioBlockDescriptorFreeListResult CreateFailure(
        VirtioBlockDescriptorFreeListStatus status,
        VirtqueueSplitRingPlanStatus queuePlanStatus,
        int stateTableLength,
        int requiredStateTableLength)
    {
        return VirtioBlockDescriptorFreeListResult.Create(
            status,
            queuePlanStatus,
            requestHeaderDescriptorIndex: 0,
            dataBufferDescriptorIndex: 0,
            statusByteDescriptorIndex: 0,
            stateTableLength,
            requiredStateTableLength,
            VirtioBlockDescriptorChainPlan.RequiredDescriptorCount,
            descriptorsScanned: 0,
            invalidDescriptorIndex: 0);
    }
}

public enum VirtioBlockDescriptorPublishPlanStatus
{
    Success = 0,
    FreeListFailed = 1,
    LifecyclePublishFailed = 2,
    OwnershipMarkFailed = 3
}

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

public static class VirtioBlockDescriptorPublishPlanner
{
    public const bool PerformsMmio = false;
    public const bool NotifiesDevice = false;
    public const bool PollsDevice = false;
    public const bool HandlesInterrupts = false;
    public const bool PerformsDmaMapping = false;
    public const bool AllocatesDmaMemory = false;
    public const bool PerformsBlockIo = false;
    public const bool PerformsDiskRead = false;
    public const bool ReadsQueueMemory = false;
    public const bool WritesQueueMemory = true;
    public const bool WritesRequestHeader = true;
    public const bool MutatesCallerOwnedStateTable = true;

    public static VirtioBlockDescriptorPublishPlanResult PublishReadFromFreeList(
        uint sectorSizeBytes,
        ulong deviceSectorCount,
        ulong firstSector,
        uint sectorCount,
        ushort queueSize,
        Span<VirtioBlockDescriptorOwnershipState> descriptorStates,
        ushort previousAvailableRingIndex,
        ushort previousUsedIndex,
        ulong requestHeaderPhysicalAddress,
        ulong dataBufferPhysicalAddress,
        ulong statusBytePhysicalAddress,
        Span<byte> requestHeader,
        Span<byte> destination,
        Span<byte> statusBuffer,
        Span<byte> queueMemory)
    {
        VirtqueueSplitRingPlanResult queuePlan = VirtqueueSplitRingPlan.Create(queueSize);
        VirtioBlockDescriptorFreeListResult freeList =
            VirtioBlockDescriptorFreeListPlanner.SelectFreeDescriptorTriple(
                queuePlan,
                descriptorStates);
        if (!freeList.Succeeded)
        {
            return VirtioBlockDescriptorPublishPlanResult.Create(
                VirtioBlockDescriptorPublishPlanStatus.FreeListFailed,
                queuePlan.Status,
                freeList,
                default,
                default,
                lifecyclePublishAttempted: false,
                ownershipMarkAttempted: false);
        }

        VirtioBlockReadLifecyclePublishResult lifecyclePublish =
            VirtioBlockReadLifecycle.PublishReadRequest(
                sectorSizeBytes,
                deviceSectorCount,
                firstSector,
                sectorCount,
                queueSize,
                freeList.FirstDescriptorIndex,
                freeList.DescriptorsRequired,
                previousAvailableRingIndex,
                previousUsedIndex,
                requestHeaderPhysicalAddress,
                dataBufferPhysicalAddress,
                statusBytePhysicalAddress,
                requestHeader,
                destination,
                statusBuffer,
                queueMemory);
        if (!lifecyclePublish.Succeeded)
        {
            return VirtioBlockDescriptorPublishPlanResult.Create(
                VirtioBlockDescriptorPublishPlanStatus.LifecyclePublishFailed,
                queuePlan.Status,
                freeList,
                lifecyclePublish,
                default,
                lifecyclePublishAttempted: true,
                ownershipMarkAttempted: false);
        }

        VirtioBlockDescriptorOwnershipPlanResult ownershipMark =
            VirtioBlockDescriptorOwnershipPlan.MarkPublished(
                lifecyclePublish,
                descriptorStates);
        if (!ownershipMark.Succeeded)
        {
            return VirtioBlockDescriptorPublishPlanResult.Create(
                VirtioBlockDescriptorPublishPlanStatus.OwnershipMarkFailed,
                queuePlan.Status,
                freeList,
                lifecyclePublish,
                ownershipMark,
                lifecyclePublishAttempted: true,
                ownershipMarkAttempted: true);
        }

        return VirtioBlockDescriptorPublishPlanResult.Create(
            VirtioBlockDescriptorPublishPlanStatus.Success,
            queuePlan.Status,
            freeList,
            lifecyclePublish,
            ownershipMark,
            lifecyclePublishAttempted: true,
            ownershipMarkAttempted: true);
    }
}

public enum VirtioBlockDescriptorReclaimPlanStatus
{
    Success = 0,
    PublishPlanFailed = 1,
    CompletionFailed = 2,
    OwnershipReclaimFailed = 3
}

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

    public VirtioBlockDescriptorReclaimPlanStatus Status { get; }

    public VirtioBlockDescriptorPublishPlanStatus PublishPlanStatus { get; }

    public VirtioBlockDescriptorFreeListStatus FreeListStatus { get; }

    public VirtioBlockReadLifecyclePublishStatus LifecyclePublishStatus { get; }

    public VirtioBlockReadLifecycleCompletionStatus CompletionStatus { get; }

    public VirtioBlockUsedRingCompletionReaderStatus CompletionReaderStatus { get; }

    public VirtioBlockDescriptorOwnershipStatus OwnershipStatus { get; }

    public bool CompletionAttempted { get; }

    public bool OwnershipReclaimAttempted { get; }

    public ushort RequestHeaderDescriptorIndex { get; }

    public ushort DataBufferDescriptorIndex { get; }

    public ushort StatusByteDescriptorIndex { get; }

    public ushort FirstDescriptorIndex { get; }

    public ushort PreviousAvailableRingIndex { get; }

    public ushort NextAvailableRingIndex { get; }

    public ushort PreviousUsedIndex { get; }

    public ushort CurrentUsedIndex { get; }

    public ushort NextUsedIndex { get; }

    public ushort ExpectedNextUsedIndex { get; }

    public ushort UsedRingSlot { get; }

    public uint UsedRingElementOffset { get; }

    public uint CompletedDescriptorHead { get; }

    public uint CompletedByteCount { get; }

    public byte StatusByte { get; }

    public int StateTableLength { get; }

    public int RequiredStateTableLength { get; }

    public ushort DescriptorsMarkedInFlight { get; }

    public ushort DescriptorsFreed { get; }

    public VirtioBlockDescriptorPublishPlanResult Publish { get; }

    public VirtioBlockReadLifecycleCompletionResult Completion { get; }

    public VirtioBlockDescriptorOwnershipPlanResult OwnershipReclaim { get; }

    public bool IsInitialized { get; }

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

public static class VirtioBlockDescriptorReclaimPlanner
{
    public const bool RequiresSuccessfulPublishPlan = true;
    public const bool RequiresCallerOwnedQueueMemory = true;
    public const bool RequiresCallerOwnedStatusBuffer = true;
    public const bool RequiresCallerOwnedDescriptorStateTable = true;
    public const bool ReadsQueueMemory = true;
    public const bool ReadsStatusBuffer = true;
    public const bool MutatesCallerOwnedStateTable = true;
    public const bool PerformsMmio = false;
    public const bool MapsBars = false;
    public const bool ContainsVolatileAccess = false;
    public const bool AccessesPhysicalAddresses = false;
    public const bool ExecutesMemoryBarriers = false;
    public const bool ConfiguresLiveQueues = false;
    public const bool NotifiesLiveDevice = false;
    public const bool PollsDevice = false;
    public const bool HandlesInterrupts = false;
    public const bool PerformsDmaMapping = false;
    public const bool AllocatesDmaMemory = false;
    public const bool PerformsBlockIo = false;
    public const bool PerformsDiskRead = false;
    public const bool WaitsForUsedRingCompletion = false;
    public const bool WritesQueueMemory = false;
    public const bool WritesRequestHeader = false;

    public static VirtioBlockDescriptorReclaimPlanResult CompletePublishedRead(
        VirtioBlockDescriptorPublishPlanResult publish,
        ReadOnlySpan<byte> queueMemory,
        ReadOnlySpan<byte> statusBuffer,
        Span<VirtioBlockDescriptorOwnershipState> descriptorStates)
    {
        if (!publish.Succeeded)
        {
            return VirtioBlockDescriptorReclaimPlanResult.Create(
                VirtioBlockDescriptorReclaimPlanStatus.PublishPlanFailed,
                publish,
                default,
                default,
                completionAttempted: false,
                ownershipReclaimAttempted: false,
                descriptorStates.Length);
        }

        VirtioBlockReadLifecycleCompletionResult completion =
            VirtioBlockReadLifecycle.CompleteReadRequest(
                publish.LifecyclePublish,
                queueMemory,
                statusBuffer);
        if (!completion.Succeeded)
        {
            return VirtioBlockDescriptorReclaimPlanResult.Create(
                VirtioBlockDescriptorReclaimPlanStatus.CompletionFailed,
                publish,
                completion,
                default,
                completionAttempted: true,
                ownershipReclaimAttempted: false,
                descriptorStates.Length);
        }

        VirtioBlockDescriptorOwnershipPlanResult ownershipReclaim =
            VirtioBlockDescriptorOwnershipPlan.ApplyCompletion(
                completion,
                descriptorStates);
        if (!ownershipReclaim.Succeeded)
        {
            return VirtioBlockDescriptorReclaimPlanResult.Create(
                VirtioBlockDescriptorReclaimPlanStatus.OwnershipReclaimFailed,
                publish,
                completion,
                ownershipReclaim,
                completionAttempted: true,
                ownershipReclaimAttempted: true,
                descriptorStates.Length);
        }

        return VirtioBlockDescriptorReclaimPlanResult.Create(
            VirtioBlockDescriptorReclaimPlanStatus.Success,
            publish,
            completion,
            ownershipReclaim,
            completionAttempted: true,
            ownershipReclaimAttempted: true,
            descriptorStates.Length);
    }
}

public struct VirtioBlockQueueCursorState
{
    public VirtioBlockQueueCursorState(
        ushort availableRingIndex,
        ushort usedRingIndex)
    {
        AvailableRingIndex = availableRingIndex;
        UsedRingIndex = usedRingIndex;
    }

    public ushort AvailableRingIndex { get; set; }

    public ushort UsedRingIndex { get; set; }
}

public enum VirtioBlockQueueCursorPublishStatus
{
    Success = 0,
    PublishPlanFailed = 1
}

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

    public VirtioBlockQueueCursorPublishStatus Status { get; }

    public VirtioBlockDescriptorPublishPlanStatus PublishPlanStatus { get; }

    public VirtioBlockDescriptorFreeListStatus FreeListStatus { get; }

    public VirtioBlockReadLifecyclePublishStatus LifecyclePublishStatus { get; }

    public VirtioBlockDescriptorOwnershipStatus OwnershipStatus { get; }

    public ushort PreviousAvailableRingIndex { get; }

    public ushort CurrentAvailableRingIndex { get; }

    public ushort PreviousUsedRingIndex { get; }

    public ushort CurrentUsedRingIndex { get; }

    public VirtioBlockDescriptorPublishPlanResult Publish { get; }

    public bool IsInitialized { get; }

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

public enum VirtioBlockQueueCursorReclaimStatus
{
    Success = 0,
    ReclaimPlanFailed = 1
}

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

public static class VirtioBlockQueueCursorPlanner
{
    public const bool RequiresCallerOwnedCursorState = true;
    public const bool RequiresCallerOwnedQueueMemory = true;
    public const bool RequiresCallerOwnedStatusBuffer = true;
    public const bool RequiresCallerOwnedDescriptorStateTable = true;
    public const bool ReadsQueueMemory = true;
    public const bool ReadsStatusBuffer = true;
    public const bool WritesQueueMemory = true;
    public const bool WritesRequestHeader = true;
    public const bool MutatesCallerOwnedCursorState = true;
    public const bool MutatesCallerOwnedDescriptorStateTable = true;
    public const bool PerformsMmio = false;
    public const bool MapsBars = false;
    public const bool ContainsVolatileAccess = false;
    public const bool AccessesPhysicalAddresses = false;
    public const bool ExecutesMemoryBarriers = false;
    public const bool ConfiguresLiveQueues = false;
    public const bool NotifiesLiveDevice = false;
    public const bool PollsDevice = false;
    public const bool HandlesInterrupts = false;
    public const bool PerformsDmaMapping = false;
    public const bool AllocatesDmaMemory = false;
    public const bool PerformsBlockIo = false;
    public const bool PerformsDiskRead = false;
    public const bool WaitsForUsedRingCompletion = false;

    public static VirtioBlockQueueCursorPublishResult PublishReadFromFreeList(
        uint sectorSizeBytes,
        ulong deviceSectorCount,
        ulong firstSector,
        uint sectorCount,
        ushort queueSize,
        Span<VirtioBlockDescriptorOwnershipState> descriptorStates,
        ref VirtioBlockQueueCursorState cursor,
        ulong requestHeaderPhysicalAddress,
        ulong dataBufferPhysicalAddress,
        ulong statusBytePhysicalAddress,
        Span<byte> requestHeader,
        Span<byte> destination,
        Span<byte> statusBuffer,
        Span<byte> queueMemory)
    {
        ushort previousAvailableRingIndex = cursor.AvailableRingIndex;
        ushort previousUsedRingIndex = cursor.UsedRingIndex;
        VirtioBlockDescriptorPublishPlanResult publish =
            VirtioBlockDescriptorPublishPlanner.PublishReadFromFreeList(
                sectorSizeBytes,
                deviceSectorCount,
                firstSector,
                sectorCount,
                queueSize,
                descriptorStates,
                previousAvailableRingIndex,
                previousUsedRingIndex,
                requestHeaderPhysicalAddress,
                dataBufferPhysicalAddress,
                statusBytePhysicalAddress,
                requestHeader,
                destination,
                statusBuffer,
                queueMemory);
        if (!publish.Succeeded)
        {
            return VirtioBlockQueueCursorPublishResult.Create(
                VirtioBlockQueueCursorPublishStatus.PublishPlanFailed,
                publish,
                previousAvailableRingIndex,
                cursor.AvailableRingIndex,
                previousUsedRingIndex,
                cursor.UsedRingIndex);
        }

        cursor.AvailableRingIndex = publish.NextAvailableRingIndex;
        return VirtioBlockQueueCursorPublishResult.Create(
            VirtioBlockQueueCursorPublishStatus.Success,
            publish,
            previousAvailableRingIndex,
            cursor.AvailableRingIndex,
            previousUsedRingIndex,
            cursor.UsedRingIndex);
    }

    public static VirtioBlockQueueCursorReclaimResult CompletePublishedRead(
        VirtioBlockDescriptorPublishPlanResult publish,
        ReadOnlySpan<byte> queueMemory,
        ReadOnlySpan<byte> statusBuffer,
        Span<VirtioBlockDescriptorOwnershipState> descriptorStates,
        ref VirtioBlockQueueCursorState cursor)
    {
        ushort previousAvailableRingIndex = cursor.AvailableRingIndex;
        ushort previousUsedRingIndex = cursor.UsedRingIndex;
        VirtioBlockDescriptorReclaimPlanResult reclaim =
            VirtioBlockDescriptorReclaimPlanner.CompletePublishedRead(
                publish,
                queueMemory,
                statusBuffer,
                descriptorStates);
        if (!reclaim.Succeeded)
        {
            return VirtioBlockQueueCursorReclaimResult.Create(
                VirtioBlockQueueCursorReclaimStatus.ReclaimPlanFailed,
                reclaim,
                previousAvailableRingIndex,
                cursor.AvailableRingIndex,
                previousUsedRingIndex,
                cursor.UsedRingIndex);
        }

        cursor.UsedRingIndex = reclaim.NextUsedIndex;
        return VirtioBlockQueueCursorReclaimResult.Create(
            VirtioBlockQueueCursorReclaimStatus.Success,
            reclaim,
            previousAvailableRingIndex,
            cursor.AvailableRingIndex,
            previousUsedRingIndex,
            cursor.UsedRingIndex);
    }
}

public static class VirtioBlockDescriptorOwnershipPlan
{
    public const bool PerformsMmio = false;
    public const bool NotifiesDevice = false;
    public const bool PollsDevice = false;
    public const bool HandlesInterrupts = false;
    public const bool PerformsDmaMapping = false;
    public const bool AllocatesDmaMemory = false;
    public const bool PerformsBlockIo = false;
    public const bool PerformsDiskRead = false;
    public const bool ReadsQueueMemory = false;
    public const bool WritesQueueMemory = false;
    public const bool MutatesCallerOwnedStateTable = true;

    public static VirtioBlockDescriptorOwnershipPlanResult MarkPublished(
        VirtioBlockReadLifecyclePublishResult publish,
        Span<VirtioBlockDescriptorOwnershipState> descriptorStates)
    {
        if (!IsValidPublish(publish))
        {
            return CreateFailure(
                VirtioBlockDescriptorOwnershipStatus.PublishFailed,
                publish.Status,
                VirtioBlockReadLifecycleCompletionStatus.Success,
                VirtioBlockUsedRingCompletionReaderStatus.Success,
                requestHeaderDescriptorIndex: 0,
                dataBufferDescriptorIndex: 0,
                statusByteDescriptorIndex: 0,
                descriptorStates.Length,
                requiredStateTableLength: 0);
        }

        GetDescriptorIndexes(
            publish.DescriptorHead,
            out ushort requestHeaderDescriptorIndex,
            out ushort dataBufferDescriptorIndex,
            out ushort statusByteDescriptorIndex);
        int requiredStateTableLength = GetRequiredStateTableLength(statusByteDescriptorIndex);
        if (descriptorStates.Length < requiredStateTableLength)
        {
            return CreateFailure(
                VirtioBlockDescriptorOwnershipStatus.StateTableTooSmall,
                publish.Status,
                VirtioBlockReadLifecycleCompletionStatus.Success,
                VirtioBlockUsedRingCompletionReaderStatus.Success,
                requestHeaderDescriptorIndex,
                dataBufferDescriptorIndex,
                statusByteDescriptorIndex,
                descriptorStates.Length,
                requiredStateTableLength);
        }

        if (descriptorStates[requestHeaderDescriptorIndex] != VirtioBlockDescriptorOwnershipState.Free ||
            descriptorStates[dataBufferDescriptorIndex] != VirtioBlockDescriptorOwnershipState.Free ||
            descriptorStates[statusByteDescriptorIndex] != VirtioBlockDescriptorOwnershipState.Free)
        {
            return CreateFailure(
                VirtioBlockDescriptorOwnershipStatus.DescriptorAlreadyInFlight,
                publish.Status,
                VirtioBlockReadLifecycleCompletionStatus.Success,
                VirtioBlockUsedRingCompletionReaderStatus.Success,
                requestHeaderDescriptorIndex,
                dataBufferDescriptorIndex,
                statusByteDescriptorIndex,
                descriptorStates.Length,
                requiredStateTableLength);
        }

        descriptorStates[requestHeaderDescriptorIndex] = VirtioBlockDescriptorOwnershipState.InFlight;
        descriptorStates[dataBufferDescriptorIndex] = VirtioBlockDescriptorOwnershipState.InFlight;
        descriptorStates[statusByteDescriptorIndex] = VirtioBlockDescriptorOwnershipState.InFlight;

        return VirtioBlockDescriptorOwnershipPlanResult.Create(
            VirtioBlockDescriptorOwnershipStatus.Success,
            publish.Status,
            VirtioBlockReadLifecycleCompletionStatus.Success,
            VirtioBlockUsedRingCompletionReaderStatus.Success,
            requestHeaderDescriptorIndex,
            dataBufferDescriptorIndex,
            statusByteDescriptorIndex,
            descriptorStates.Length,
            requiredStateTableLength,
            VirtioBlockDescriptorChainPlan.RequiredDescriptorCount,
            descriptorsFreed: 0);
    }

    public static VirtioBlockDescriptorOwnershipPlanResult ApplyCompletion(
        VirtioBlockReadLifecycleCompletionResult completion,
        Span<VirtioBlockDescriptorOwnershipState> descriptorStates)
    {
        if (!IsValidCompletion(completion))
        {
            return CreateFailure(
                VirtioBlockDescriptorOwnershipStatus.CompletionFailed,
                completion.PublishStatus,
                completion.Status,
                completion.CompletionReaderStatus,
                requestHeaderDescriptorIndex: 0,
                dataBufferDescriptorIndex: 0,
                statusByteDescriptorIndex: 0,
                descriptorStates.Length,
                requiredStateTableLength: 0);
        }

        GetDescriptorIndexes(
            completion.DescriptorHead,
            out ushort requestHeaderDescriptorIndex,
            out ushort dataBufferDescriptorIndex,
            out ushort statusByteDescriptorIndex);
        int requiredStateTableLength = GetRequiredStateTableLength(statusByteDescriptorIndex);
        if (descriptorStates.Length < requiredStateTableLength)
        {
            return CreateFailure(
                VirtioBlockDescriptorOwnershipStatus.StateTableTooSmall,
                completion.PublishStatus,
                completion.Status,
                completion.CompletionReaderStatus,
                requestHeaderDescriptorIndex,
                dataBufferDescriptorIndex,
                statusByteDescriptorIndex,
                descriptorStates.Length,
                requiredStateTableLength);
        }

        bool requestHeaderInFlight =
            descriptorStates[requestHeaderDescriptorIndex] == VirtioBlockDescriptorOwnershipState.InFlight;
        bool dataBufferInFlight =
            descriptorStates[dataBufferDescriptorIndex] == VirtioBlockDescriptorOwnershipState.InFlight;
        bool statusByteInFlight =
            descriptorStates[statusByteDescriptorIndex] == VirtioBlockDescriptorOwnershipState.InFlight;
        if (!requestHeaderInFlight || !dataBufferInFlight || !statusByteInFlight)
        {
            VirtioBlockDescriptorOwnershipStatus status =
                IsFree(descriptorStates[requestHeaderDescriptorIndex]) &&
                IsFree(descriptorStates[dataBufferDescriptorIndex]) &&
                IsFree(descriptorStates[statusByteDescriptorIndex])
                    ? VirtioBlockDescriptorOwnershipStatus.DuplicateCompletion
                    : VirtioBlockDescriptorOwnershipStatus.DescriptorNotInFlight;

            return CreateFailure(
                status,
                completion.PublishStatus,
                completion.Status,
                completion.CompletionReaderStatus,
                requestHeaderDescriptorIndex,
                dataBufferDescriptorIndex,
                statusByteDescriptorIndex,
                descriptorStates.Length,
                requiredStateTableLength);
        }

        descriptorStates[requestHeaderDescriptorIndex] = VirtioBlockDescriptorOwnershipState.Free;
        descriptorStates[dataBufferDescriptorIndex] = VirtioBlockDescriptorOwnershipState.Free;
        descriptorStates[statusByteDescriptorIndex] = VirtioBlockDescriptorOwnershipState.Free;

        return VirtioBlockDescriptorOwnershipPlanResult.Create(
            VirtioBlockDescriptorOwnershipStatus.Success,
            completion.PublishStatus,
            completion.Status,
            completion.CompletionReaderStatus,
            requestHeaderDescriptorIndex,
            dataBufferDescriptorIndex,
            statusByteDescriptorIndex,
            descriptorStates.Length,
            requiredStateTableLength,
            descriptorsMarkedInFlight: 0,
            VirtioBlockDescriptorChainPlan.RequiredDescriptorCount);
    }

    private static bool IsValidPublish(VirtioBlockReadLifecyclePublishResult publish)
    {
        return publish.Succeeded &&
            publish.DescriptorsWritten == VirtioBlockDescriptorChainPlan.RequiredDescriptorCount &&
            VirtqueueSplitRingPlan.ValidateQueueSize(publish.QueueSize) == VirtqueueSplitRingPlanStatus.Success &&
            publish.QueueSize >= VirtioBlockDescriptorChainPlan.RequiredDescriptorCount &&
            (int)publish.DescriptorHead + VirtioBlockDescriptorChainPlan.RequiredDescriptorCount <= publish.QueueSize;
    }

    private static bool IsValidCompletion(VirtioBlockReadLifecycleCompletionResult completion)
    {
        return completion.Succeeded &&
            completion.CompletedDescriptorHead == completion.DescriptorHead &&
            (int)completion.DescriptorHead + VirtioBlockDescriptorChainPlan.RequiredDescriptorCount <=
                ushort.MaxValue + 1;
    }

    private static void GetDescriptorIndexes(
        ushort firstDescriptorIndex,
        out ushort requestHeaderDescriptorIndex,
        out ushort dataBufferDescriptorIndex,
        out ushort statusByteDescriptorIndex)
    {
        requestHeaderDescriptorIndex = firstDescriptorIndex;
        dataBufferDescriptorIndex = (ushort)(firstDescriptorIndex + 1);
        statusByteDescriptorIndex = (ushort)(firstDescriptorIndex + 2);
    }

    private static int GetRequiredStateTableLength(ushort statusByteDescriptorIndex)
    {
        return statusByteDescriptorIndex + 1;
    }

    private static bool IsFree(VirtioBlockDescriptorOwnershipState state)
    {
        return state == VirtioBlockDescriptorOwnershipState.Free;
    }

    private static VirtioBlockDescriptorOwnershipPlanResult CreateFailure(
        VirtioBlockDescriptorOwnershipStatus status,
        VirtioBlockReadLifecyclePublishStatus publishStatus,
        VirtioBlockReadLifecycleCompletionStatus completionStatus,
        VirtioBlockUsedRingCompletionReaderStatus completionReaderStatus,
        ushort requestHeaderDescriptorIndex,
        ushort dataBufferDescriptorIndex,
        ushort statusByteDescriptorIndex,
        int stateTableLength,
        int requiredStateTableLength)
    {
        return VirtioBlockDescriptorOwnershipPlanResult.Create(
            status,
            publishStatus,
            completionStatus,
            completionReaderStatus,
            requestHeaderDescriptorIndex,
            dataBufferDescriptorIndex,
            statusByteDescriptorIndex,
            stateTableLength,
            requiredStateTableLength,
            descriptorsMarkedInFlight: 0,
            descriptorsFreed: 0);
    }
}
