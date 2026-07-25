namespace OS.Drivers;

public enum VirtioNetTransmitDescriptorOwnershipState
{
    Free = 0,
    InFlight = 1
}

public enum VirtioNetTransmitDescriptorOwnershipStatus
{
    Success = 0,
    TransmitPublishFailed = 1,
    CompletionPlanFailed = 2,
    StateTableTooSmall = 3,
    DescriptorAlreadyInFlight = 4,
    DescriptorNotInFlight = 5,
    DuplicateCompletion = 6
}

public enum VirtioNetTransmitDescriptorFreeListStatus
{
    Success = 0,
    QueuePlanFailed = 1,
    StateTableTooSmall = 2,
    InvalidDescriptorState = 3,
    NoFreeDescriptorPair = 4
}

public readonly struct VirtioNetTransmitDescriptorFreeListResult
{
    private VirtioNetTransmitDescriptorFreeListResult(
        VirtioNetTransmitDescriptorFreeListStatus status,
        VirtioNetQueuePlanStatus queuePlanStatus,
        ushort headerDescriptorIndex,
        ushort frameDescriptorIndex,
        int stateTableLength,
        int requiredStateTableLength,
        ushort descriptorsRequired,
        ushort descriptorsScanned,
        ushort invalidDescriptorIndex,
        bool isInitialized)
    {
        Status = status;
        QueuePlanStatus = queuePlanStatus;
        HeaderDescriptorIndex = headerDescriptorIndex;
        FrameDescriptorIndex = frameDescriptorIndex;
        FirstDescriptorIndex = headerDescriptorIndex;
        StateTableLength = stateTableLength;
        RequiredStateTableLength = requiredStateTableLength;
        DescriptorsRequired = descriptorsRequired;
        DescriptorsScanned = descriptorsScanned;
        InvalidDescriptorIndex = invalidDescriptorIndex;
        IsInitialized = isInitialized;
    }

    public VirtioNetTransmitDescriptorFreeListStatus Status { get; }

    public VirtioNetQueuePlanStatus QueuePlanStatus { get; }

    public ushort HeaderDescriptorIndex { get; }

    public ushort FrameDescriptorIndex { get; }

    public ushort FirstDescriptorIndex { get; }

    public int StateTableLength { get; }

    public int RequiredStateTableLength { get; }

    public ushort DescriptorsRequired { get; }

    public ushort DescriptorsScanned { get; }

    public ushort InvalidDescriptorIndex { get; }

    public bool IsInitialized { get; }

    public bool Succeeded =>
        IsInitialized && Status == VirtioNetTransmitDescriptorFreeListStatus.Success;

    internal static VirtioNetTransmitDescriptorFreeListResult Create(
        VirtioNetTransmitDescriptorFreeListStatus status,
        VirtioNetQueuePlanStatus queuePlanStatus,
        ushort headerDescriptorIndex,
        ushort frameDescriptorIndex,
        int stateTableLength,
        int requiredStateTableLength,
        ushort descriptorsRequired,
        ushort descriptorsScanned,
        ushort invalidDescriptorIndex)
    {
        return new VirtioNetTransmitDescriptorFreeListResult(
            status,
            queuePlanStatus,
            headerDescriptorIndex,
            frameDescriptorIndex,
            stateTableLength,
            requiredStateTableLength,
            descriptorsRequired,
            descriptorsScanned,
            invalidDescriptorIndex,
            isInitialized: true);
    }
}

public readonly struct VirtioNetTransmitDescriptorOwnershipPlanResult
{
    private VirtioNetTransmitDescriptorOwnershipPlanResult(
        VirtioNetTransmitDescriptorOwnershipStatus status,
        VirtioNetTransmitLifecycleStatus transmitPublishStatus,
        VirtioNetTransmitCompletionPlanStatus completionPlanStatus,
        ushort headerDescriptorIndex,
        ushort frameDescriptorIndex,
        int stateTableLength,
        int requiredStateTableLength,
        ushort descriptorsMarkedInFlight,
        ushort descriptorsFreed,
        bool isInitialized)
    {
        Status = status;
        TransmitPublishStatus = transmitPublishStatus;
        CompletionPlanStatus = completionPlanStatus;
        HeaderDescriptorIndex = headerDescriptorIndex;
        FrameDescriptorIndex = frameDescriptorIndex;
        StateTableLength = stateTableLength;
        RequiredStateTableLength = requiredStateTableLength;
        DescriptorsMarkedInFlight = descriptorsMarkedInFlight;
        DescriptorsFreed = descriptorsFreed;
        IsInitialized = isInitialized;
    }

    public VirtioNetTransmitDescriptorOwnershipStatus Status { get; }

    public VirtioNetTransmitLifecycleStatus TransmitPublishStatus { get; }

    public VirtioNetTransmitCompletionPlanStatus CompletionPlanStatus { get; }

    public ushort HeaderDescriptorIndex { get; }

    public ushort FrameDescriptorIndex { get; }

    public int StateTableLength { get; }

    public int RequiredStateTableLength { get; }

    public ushort DescriptorsMarkedInFlight { get; }

    public ushort DescriptorsFreed { get; }

    public bool IsInitialized { get; }

    public bool Succeeded =>
        IsInitialized && Status == VirtioNetTransmitDescriptorOwnershipStatus.Success;

    internal static VirtioNetTransmitDescriptorOwnershipPlanResult Create(
        VirtioNetTransmitDescriptorOwnershipStatus status,
        VirtioNetTransmitLifecycleStatus transmitPublishStatus,
        VirtioNetTransmitCompletionPlanStatus completionPlanStatus,
        ushort headerDescriptorIndex,
        ushort frameDescriptorIndex,
        int stateTableLength,
        int requiredStateTableLength,
        ushort descriptorsMarkedInFlight,
        ushort descriptorsFreed)
    {
        return new VirtioNetTransmitDescriptorOwnershipPlanResult(
            status,
            transmitPublishStatus,
            completionPlanStatus,
            headerDescriptorIndex,
            frameDescriptorIndex,
            stateTableLength,
            requiredStateTableLength,
            descriptorsMarkedInFlight,
            descriptorsFreed,
            isInitialized: true);
    }
}

public static class VirtioNetTransmitDescriptorFreeListPlanner
{
    public const bool PerformsMmio = false;
    public const bool NotifiesDevice = false;
    public const bool PollsDevice = false;
    public const bool HandlesInterrupts = false;
    public const bool PerformsDmaMapping = false;
    public const bool PerformsPacketIo = false;
    public const bool ReadsQueueMemory = false;
    public const bool MutatesCallerOwnedStateTable = false;

    public static VirtioNetTransmitDescriptorFreeListResult SelectFreeDescriptorPair(
        VirtioNetQueuePlanResult queuePlan,
        ReadOnlySpan<VirtioNetTransmitDescriptorOwnershipState> descriptorStates)
    {
        if (!queuePlan.Succeeded ||
            VirtqueueSplitRingPlan.ValidateQueueSize(queuePlan.Plan.TxQueue.QueueSize) !=
                VirtqueueSplitRingPlanStatus.Success)
        {
            return CreateFailure(
                VirtioNetTransmitDescriptorFreeListStatus.QueuePlanFailed,
                queuePlan.Status,
                descriptorStates.Length,
                requiredStateTableLength: 0);
        }

        ushort queueSize = queuePlan.Plan.TxQueue.QueueSize;
        if (descriptorStates.Length < queueSize)
        {
            return CreateFailure(
                VirtioNetTransmitDescriptorFreeListStatus.StateTableTooSmall,
                queuePlan.Status,
                descriptorStates.Length,
                queueSize);
        }

        for (ushort index = 0; index < queueSize; index++)
        {
            VirtioNetTransmitDescriptorOwnershipState state = descriptorStates[index];
            if (!IsValidState(state))
            {
                return VirtioNetTransmitDescriptorFreeListResult.Create(
                    VirtioNetTransmitDescriptorFreeListStatus.InvalidDescriptorState,
                    queuePlan.Status,
                    headerDescriptorIndex: 0,
                    frameDescriptorIndex: 0,
                    descriptorStates.Length,
                    queueSize,
                    VirtioNetTransmitLifecycle.RequiredDescriptorCount,
                    descriptorsScanned: index,
                    invalidDescriptorIndex: index);
            }
        }

        ushort lastFirstDescriptor = (ushort)(queueSize - VirtioNetTransmitLifecycle.RequiredDescriptorCount);
        for (ushort index = 0; index <= lastFirstDescriptor; index++)
        {
            ushort frameDescriptorIndex = (ushort)(index + 1);
            if (descriptorStates[index] == VirtioNetTransmitDescriptorOwnershipState.Free &&
                descriptorStates[frameDescriptorIndex] == VirtioNetTransmitDescriptorOwnershipState.Free)
            {
                return VirtioNetTransmitDescriptorFreeListResult.Create(
                    VirtioNetTransmitDescriptorFreeListStatus.Success,
                    queuePlan.Status,
                    index,
                    frameDescriptorIndex,
                    descriptorStates.Length,
                    queueSize,
                    VirtioNetTransmitLifecycle.RequiredDescriptorCount,
                    descriptorsScanned: (ushort)(frameDescriptorIndex + 1),
                    invalidDescriptorIndex: 0);
            }
        }

        return VirtioNetTransmitDescriptorFreeListResult.Create(
            VirtioNetTransmitDescriptorFreeListStatus.NoFreeDescriptorPair,
            queuePlan.Status,
            headerDescriptorIndex: 0,
            frameDescriptorIndex: 0,
            descriptorStates.Length,
            queueSize,
            VirtioNetTransmitLifecycle.RequiredDescriptorCount,
            queueSize,
            invalidDescriptorIndex: 0);
    }

    private static bool IsValidState(VirtioNetTransmitDescriptorOwnershipState state)
    {
        return state == VirtioNetTransmitDescriptorOwnershipState.Free ||
            state == VirtioNetTransmitDescriptorOwnershipState.InFlight;
    }

    private static VirtioNetTransmitDescriptorFreeListResult CreateFailure(
        VirtioNetTransmitDescriptorFreeListStatus status,
        VirtioNetQueuePlanStatus queuePlanStatus,
        int stateTableLength,
        int requiredStateTableLength)
    {
        return VirtioNetTransmitDescriptorFreeListResult.Create(
            status,
            queuePlanStatus,
            headerDescriptorIndex: 0,
            frameDescriptorIndex: 0,
            stateTableLength,
            requiredStateTableLength,
            VirtioNetTransmitLifecycle.RequiredDescriptorCount,
            descriptorsScanned: 0,
            invalidDescriptorIndex: 0);
    }
}

public enum VirtioNetTransmitDescriptorPublishPlanStatus
{
    Success = 0,
    FreeListFailed = 1,
    TransmitPublishFailed = 2,
    OwnershipMarkFailed = 3
}

public readonly struct VirtioNetTransmitDescriptorPublishPlanResult
{
    private VirtioNetTransmitDescriptorPublishPlanResult(
        VirtioNetTransmitDescriptorPublishPlanStatus status,
        VirtioNetQueuePlanStatus queuePlanStatus,
        VirtioNetTransmitDescriptorFreeListStatus freeListStatus,
        VirtioNetTransmitLifecycleStatus transmitPublishStatus,
        VirtioNetHeaderStatus headerStatus,
        VirtioNetTransmitDescriptorOwnershipStatus ownershipStatus,
        bool transmitPublishAttempted,
        bool ownershipMarkAttempted,
        ushort headerDescriptorIndex,
        ushort frameDescriptorIndex,
        ushort nextAvailableRingIndex,
        int stateTableLength,
        int requiredStateTableLength,
        ushort descriptorsSelected,
        ushort descriptorsWritten,
        ushort descriptorsMarkedInFlight,
        VirtioNetTransmitDescriptorFreeListResult freeList,
        VirtioNetTransmitLifecycleResult transmitPublish,
        VirtioNetTransmitDescriptorOwnershipPlanResult ownershipMark,
        bool isInitialized)
    {
        Status = status;
        QueuePlanStatus = queuePlanStatus;
        FreeListStatus = freeListStatus;
        TransmitPublishStatus = transmitPublishStatus;
        HeaderStatus = headerStatus;
        OwnershipStatus = ownershipStatus;
        TransmitPublishAttempted = transmitPublishAttempted;
        OwnershipMarkAttempted = ownershipMarkAttempted;
        HeaderDescriptorIndex = headerDescriptorIndex;
        FrameDescriptorIndex = frameDescriptorIndex;
        NextAvailableRingIndex = nextAvailableRingIndex;
        StateTableLength = stateTableLength;
        RequiredStateTableLength = requiredStateTableLength;
        DescriptorsSelected = descriptorsSelected;
        DescriptorsWritten = descriptorsWritten;
        DescriptorsMarkedInFlight = descriptorsMarkedInFlight;
        FreeList = freeList;
        TransmitPublish = transmitPublish;
        OwnershipMark = ownershipMark;
        IsInitialized = isInitialized;
    }

    public VirtioNetTransmitDescriptorPublishPlanStatus Status { get; }

    public VirtioNetQueuePlanStatus QueuePlanStatus { get; }

    public VirtioNetTransmitDescriptorFreeListStatus FreeListStatus { get; }

    public VirtioNetTransmitLifecycleStatus TransmitPublishStatus { get; }

    public VirtioNetHeaderStatus HeaderStatus { get; }

    public VirtioNetTransmitDescriptorOwnershipStatus OwnershipStatus { get; }

    public bool TransmitPublishAttempted { get; }

    public bool OwnershipMarkAttempted { get; }

    public ushort HeaderDescriptorIndex { get; }

    public ushort FrameDescriptorIndex { get; }

    public ushort NextAvailableRingIndex { get; }

    public int StateTableLength { get; }

    public int RequiredStateTableLength { get; }

    public ushort DescriptorsSelected { get; }

    public ushort DescriptorsWritten { get; }

    public ushort DescriptorsMarkedInFlight { get; }

    public VirtioNetTransmitDescriptorFreeListResult FreeList { get; }

    public VirtioNetTransmitLifecycleResult TransmitPublish { get; }

    public VirtioNetTransmitDescriptorOwnershipPlanResult OwnershipMark { get; }

    public bool IsInitialized { get; }

    public bool Succeeded =>
        IsInitialized && Status == VirtioNetTransmitDescriptorPublishPlanStatus.Success;

    internal static VirtioNetTransmitDescriptorPublishPlanResult Create(
        VirtioNetTransmitDescriptorPublishPlanStatus status,
        VirtioNetQueuePlanStatus queuePlanStatus,
        VirtioNetTransmitDescriptorFreeListResult freeList,
        VirtioNetTransmitLifecycleResult transmitPublish,
        VirtioNetTransmitDescriptorOwnershipPlanResult ownershipMark,
        bool transmitPublishAttempted,
        bool ownershipMarkAttempted)
    {
        return new VirtioNetTransmitDescriptorPublishPlanResult(
            status,
            queuePlanStatus,
            freeList.Status,
            transmitPublish.Status,
            transmitPublish.HeaderStatus,
            ownershipMark.Status,
            transmitPublishAttempted,
            ownershipMarkAttempted,
            freeList.HeaderDescriptorIndex,
            freeList.FrameDescriptorIndex,
            transmitPublish.NextAvailableRingIndex,
            freeList.StateTableLength,
            freeList.RequiredStateTableLength,
            freeList.Succeeded ? freeList.DescriptorsRequired : (ushort)0,
            transmitPublish.DescriptorsWritten,
            ownershipMark.DescriptorsMarkedInFlight,
            freeList,
            transmitPublish,
            ownershipMark,
            isInitialized: true);
    }
}

public static class VirtioNetTransmitDescriptorPublishPlanner
{
    public const bool PerformsMmio = false;
    public const bool NotifiesDevice = false;
    public const bool PollsDevice = false;
    public const bool HandlesInterrupts = false;
    public const bool PerformsDmaMapping = false;
    public const bool AllocatesDmaMemory = false;
    public const bool PerformsPacketIo = false;
    public const bool PerformsPacketTransmit = false;
    public const bool WaitsForUsedRingCompletion = false;
    public const bool ReadsQueueMemory = false;
    public const bool WritesQueueMemory = true;
    public const bool WritesTransmitHeader = true;
    public const bool MutatesCallerOwnedStateTable = true;

    public static VirtioNetTransmitDescriptorPublishPlanResult PublishFromFreeList(
        VirtioNetQueuePlanResult queuePlan,
        Span<VirtioNetTransmitDescriptorOwnershipState> descriptorStates,
        ushort availableRingIndex,
        ulong txHeaderPhysicalAddress,
        ulong ethernetFramePhysicalAddress,
        ReadOnlySpan<byte> ethernetFrame,
        Span<byte> txHeader,
        Span<byte> queueMemory)
    {
        VirtioNetTransmitDescriptorFreeListResult freeList =
            VirtioNetTransmitDescriptorFreeListPlanner.SelectFreeDescriptorPair(
                queuePlan,
                descriptorStates);
        if (!freeList.Succeeded)
        {
            return VirtioNetTransmitDescriptorPublishPlanResult.Create(
                VirtioNetTransmitDescriptorPublishPlanStatus.FreeListFailed,
                queuePlan.Status,
                freeList,
                default,
                default,
                transmitPublishAttempted: false,
                ownershipMarkAttempted: false);
        }

        VirtioNetTransmitLifecycleResult transmitPublish =
            VirtioNetTransmitLifecycle.Publish(
                queuePlan,
                freeList.HeaderDescriptorIndex,
                freeList.DescriptorsRequired,
                availableRingIndex,
                txHeaderPhysicalAddress,
                ethernetFramePhysicalAddress,
                ethernetFrame,
                txHeader,
                queueMemory);
        if (!transmitPublish.Succeeded)
        {
            return VirtioNetTransmitDescriptorPublishPlanResult.Create(
                VirtioNetTransmitDescriptorPublishPlanStatus.TransmitPublishFailed,
                queuePlan.Status,
                freeList,
                transmitPublish,
                default,
                transmitPublishAttempted: true,
                ownershipMarkAttempted: false);
        }

        VirtioNetTransmitDescriptorOwnershipPlanResult ownershipMark =
            VirtioNetTransmitDescriptorOwnershipPlan.MarkPublished(
                transmitPublish,
                descriptorStates);
        if (!ownershipMark.Succeeded)
        {
            return VirtioNetTransmitDescriptorPublishPlanResult.Create(
                VirtioNetTransmitDescriptorPublishPlanStatus.OwnershipMarkFailed,
                queuePlan.Status,
                freeList,
                transmitPublish,
                ownershipMark,
                transmitPublishAttempted: true,
                ownershipMarkAttempted: true);
        }

        return VirtioNetTransmitDescriptorPublishPlanResult.Create(
            VirtioNetTransmitDescriptorPublishPlanStatus.Success,
            queuePlan.Status,
            freeList,
            transmitPublish,
            ownershipMark,
            transmitPublishAttempted: true,
            ownershipMarkAttempted: true);
    }
}

public static class VirtioNetTransmitDescriptorOwnershipPlan
{
    public const bool PerformsMmio = false;
    public const bool NotifiesDevice = false;
    public const bool PollsDevice = false;
    public const bool HandlesInterrupts = false;
    public const bool PerformsDmaMapping = false;
    public const bool PerformsPacketIo = false;
    public const bool ReadsQueueMemory = false;
    public const bool MutatesCallerOwnedStateTable = true;

    public static VirtioNetTransmitDescriptorOwnershipPlanResult MarkPublished(
        VirtioNetTransmitLifecycleResult transmitPublish,
        Span<VirtioNetTransmitDescriptorOwnershipState> descriptorStates)
    {
        if (!IsValidTransmitPublish(transmitPublish))
        {
            return CreateFailure(
                VirtioNetTransmitDescriptorOwnershipStatus.TransmitPublishFailed,
                transmitPublish.Status,
                VirtioNetTransmitCompletionPlanStatus.Success,
                headerDescriptorIndex: 0,
                frameDescriptorIndex: 0,
                descriptorStates.Length,
                requiredStateTableLength: 0);
        }

        ushort headerDescriptorIndex = transmitPublish.Plan.HeaderDescriptor.DescriptorIndex;
        ushort frameDescriptorIndex = transmitPublish.Plan.FrameDescriptor.DescriptorIndex;
        int requiredStateTableLength = GetRequiredStateTableLength(
            headerDescriptorIndex,
            frameDescriptorIndex);
        if (descriptorStates.Length < requiredStateTableLength)
        {
            return CreateFailure(
                VirtioNetTransmitDescriptorOwnershipStatus.StateTableTooSmall,
                transmitPublish.Status,
                VirtioNetTransmitCompletionPlanStatus.Success,
                headerDescriptorIndex,
                frameDescriptorIndex,
                descriptorStates.Length,
                requiredStateTableLength);
        }

        if (descriptorStates[headerDescriptorIndex] != VirtioNetTransmitDescriptorOwnershipState.Free ||
            descriptorStates[frameDescriptorIndex] != VirtioNetTransmitDescriptorOwnershipState.Free)
        {
            return CreateFailure(
                VirtioNetTransmitDescriptorOwnershipStatus.DescriptorAlreadyInFlight,
                transmitPublish.Status,
                VirtioNetTransmitCompletionPlanStatus.Success,
                headerDescriptorIndex,
                frameDescriptorIndex,
                descriptorStates.Length,
                requiredStateTableLength);
        }

        descriptorStates[headerDescriptorIndex] = VirtioNetTransmitDescriptorOwnershipState.InFlight;
        descriptorStates[frameDescriptorIndex] = VirtioNetTransmitDescriptorOwnershipState.InFlight;

        return VirtioNetTransmitDescriptorOwnershipPlanResult.Create(
            VirtioNetTransmitDescriptorOwnershipStatus.Success,
            transmitPublish.Status,
            VirtioNetTransmitCompletionPlanStatus.Success,
            headerDescriptorIndex,
            frameDescriptorIndex,
            descriptorStates.Length,
            requiredStateTableLength,
            VirtioNetTransmitLifecycle.RequiredDescriptorCount,
            descriptorsFreed: 0);
    }

    public static VirtioNetTransmitDescriptorOwnershipPlanResult ApplyCompletion(
        VirtioNetTransmitCompletionPlanResult completion,
        Span<VirtioNetTransmitDescriptorOwnershipState> descriptorStates)
    {
        if (!IsValidCompletion(completion))
        {
            return CreateFailure(
                VirtioNetTransmitDescriptorOwnershipStatus.CompletionPlanFailed,
                VirtioNetTransmitLifecycleStatus.Success,
                completion.Status,
                headerDescriptorIndex: 0,
                frameDescriptorIndex: 0,
                descriptorStates.Length,
                requiredStateTableLength: 0);
        }

        ushort headerDescriptorIndex = completion.HeaderDescriptorIndex;
        ushort frameDescriptorIndex = completion.FrameDescriptorIndex;
        int requiredStateTableLength = GetRequiredStateTableLength(
            headerDescriptorIndex,
            frameDescriptorIndex);
        if (descriptorStates.Length < requiredStateTableLength)
        {
            return CreateFailure(
                VirtioNetTransmitDescriptorOwnershipStatus.StateTableTooSmall,
                VirtioNetTransmitLifecycleStatus.Success,
                completion.Status,
                headerDescriptorIndex,
                frameDescriptorIndex,
                descriptorStates.Length,
                requiredStateTableLength);
        }

        bool headerInFlight =
            descriptorStates[headerDescriptorIndex] == VirtioNetTransmitDescriptorOwnershipState.InFlight;
        bool frameInFlight =
            descriptorStates[frameDescriptorIndex] == VirtioNetTransmitDescriptorOwnershipState.InFlight;
        if (!headerInFlight || !frameInFlight)
        {
            VirtioNetTransmitDescriptorOwnershipStatus status =
                !headerInFlight &&
                !frameInFlight &&
                descriptorStates[headerDescriptorIndex] == VirtioNetTransmitDescriptorOwnershipState.Free &&
                descriptorStates[frameDescriptorIndex] == VirtioNetTransmitDescriptorOwnershipState.Free
                    ? VirtioNetTransmitDescriptorOwnershipStatus.DuplicateCompletion
                    : VirtioNetTransmitDescriptorOwnershipStatus.DescriptorNotInFlight;

            return CreateFailure(
                status,
                VirtioNetTransmitLifecycleStatus.Success,
                completion.Status,
                headerDescriptorIndex,
                frameDescriptorIndex,
                descriptorStates.Length,
                requiredStateTableLength);
        }

        descriptorStates[headerDescriptorIndex] = VirtioNetTransmitDescriptorOwnershipState.Free;
        descriptorStates[frameDescriptorIndex] = VirtioNetTransmitDescriptorOwnershipState.Free;

        return VirtioNetTransmitDescriptorOwnershipPlanResult.Create(
            VirtioNetTransmitDescriptorOwnershipStatus.Success,
            VirtioNetTransmitLifecycleStatus.Success,
            completion.Status,
            headerDescriptorIndex,
            frameDescriptorIndex,
            descriptorStates.Length,
            requiredStateTableLength,
            descriptorsMarkedInFlight: 0,
            completion.ReclaimedDescriptorCount);
    }

    private static bool IsValidTransmitPublish(VirtioNetTransmitLifecycleResult transmitPublish)
    {
        return transmitPublish.Succeeded &&
            transmitPublish.DescriptorsWritten == VirtioNetTransmitLifecycle.RequiredDescriptorCount &&
            transmitPublish.Plan.HeaderDescriptor.DescriptorIndex !=
                transmitPublish.Plan.FrameDescriptor.DescriptorIndex &&
            VirtqueueSplitRingPlan.ValidateQueueSize(transmitPublish.Plan.TxQueue.QueueSize) ==
                VirtqueueSplitRingPlanStatus.Success;
    }

    private static bool IsValidCompletion(VirtioNetTransmitCompletionPlanResult completion)
    {
        return completion.Succeeded &&
            completion.ReclaimedDescriptorCount == VirtioNetTransmitLifecycle.RequiredDescriptorCount &&
            completion.HeaderDescriptorIndex != completion.FrameDescriptorIndex;
    }

    private static int GetRequiredStateTableLength(
        ushort headerDescriptorIndex,
        ushort frameDescriptorIndex)
    {
        return (headerDescriptorIndex > frameDescriptorIndex
            ? headerDescriptorIndex
            : frameDescriptorIndex) + 1;
    }

    private static VirtioNetTransmitDescriptorOwnershipPlanResult CreateFailure(
        VirtioNetTransmitDescriptorOwnershipStatus status,
        VirtioNetTransmitLifecycleStatus transmitPublishStatus,
        VirtioNetTransmitCompletionPlanStatus completionPlanStatus,
        ushort headerDescriptorIndex,
        ushort frameDescriptorIndex,
        int stateTableLength,
        int requiredStateTableLength)
    {
        return VirtioNetTransmitDescriptorOwnershipPlanResult.Create(
            status,
            transmitPublishStatus,
            completionPlanStatus,
            headerDescriptorIndex,
            frameDescriptorIndex,
            stateTableLength,
            requiredStateTableLength,
            descriptorsMarkedInFlight: 0,
            descriptorsFreed: 0);
    }
}
