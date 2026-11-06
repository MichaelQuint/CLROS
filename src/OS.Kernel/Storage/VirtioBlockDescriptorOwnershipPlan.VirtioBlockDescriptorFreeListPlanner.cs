using OS.Drivers;

namespace OS.Storage;

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
