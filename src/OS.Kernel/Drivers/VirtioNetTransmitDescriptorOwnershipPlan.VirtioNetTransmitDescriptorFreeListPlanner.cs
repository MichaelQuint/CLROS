namespace OS.Drivers;
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
    public static VirtioNetTransmitDescriptorFreeListResult SelectFreeDescriptorPair(VirtioNetQueuePlanResult queuePlan, ReadOnlySpan<VirtioNetTransmitDescriptorOwnershipState> descriptorStates)
    {
        if (!queuePlan.Succeeded || VirtqueueSplitRingPlan.ValidateQueueSize(queuePlan.Plan.TxQueue.QueueSize) != VirtqueueSplitRingPlanStatus.Success)
        {
            return CreateFailure(VirtioNetTransmitDescriptorFreeListStatus.QueuePlanFailed, queuePlan.Status, descriptorStates.Length, requiredStateTableLength: 0);
        }

        ushort queueSize = queuePlan.Plan.TxQueue.QueueSize;
        if (descriptorStates.Length < queueSize)
        {
            return CreateFailure(VirtioNetTransmitDescriptorFreeListStatus.StateTableTooSmall, queuePlan.Status, descriptorStates.Length, queueSize);
        }

        for (ushort index = 0; index < queueSize; index++)
        {
            VirtioNetTransmitDescriptorOwnershipState state = descriptorStates[index];
            if (!IsValidState(state))
            {
                return VirtioNetTransmitDescriptorFreeListResult.Create(VirtioNetTransmitDescriptorFreeListStatus.InvalidDescriptorState, queuePlan.Status, headerDescriptorIndex: 0, frameDescriptorIndex: 0, descriptorStates.Length, queueSize, VirtioNetTransmitLifecycle.RequiredDescriptorCount, descriptorsScanned: index, invalidDescriptorIndex: index);
            }
        }

        ushort lastFirstDescriptor = (ushort)(queueSize - VirtioNetTransmitLifecycle.RequiredDescriptorCount);
        for (ushort index = 0; index <= lastFirstDescriptor; index++)
        {
            ushort frameDescriptorIndex = (ushort)(index + 1);
            if (descriptorStates[index] == VirtioNetTransmitDescriptorOwnershipState.Free && descriptorStates[frameDescriptorIndex] == VirtioNetTransmitDescriptorOwnershipState.Free)
            {
                return VirtioNetTransmitDescriptorFreeListResult.Create(VirtioNetTransmitDescriptorFreeListStatus.Success, queuePlan.Status, index, frameDescriptorIndex, descriptorStates.Length, queueSize, VirtioNetTransmitLifecycle.RequiredDescriptorCount, descriptorsScanned: (ushort)(frameDescriptorIndex + 1), invalidDescriptorIndex: 0);
            }
        }

        return VirtioNetTransmitDescriptorFreeListResult.Create(VirtioNetTransmitDescriptorFreeListStatus.NoFreeDescriptorPair, queuePlan.Status, headerDescriptorIndex: 0, frameDescriptorIndex: 0, descriptorStates.Length, queueSize, VirtioNetTransmitLifecycle.RequiredDescriptorCount, queueSize, invalidDescriptorIndex: 0);
    }

    private static bool IsValidState(VirtioNetTransmitDescriptorOwnershipState state)
    {
        return state == VirtioNetTransmitDescriptorOwnershipState.Free || state == VirtioNetTransmitDescriptorOwnershipState.InFlight;
    }

    private static VirtioNetTransmitDescriptorFreeListResult CreateFailure(VirtioNetTransmitDescriptorFreeListStatus status, VirtioNetQueuePlanStatus queuePlanStatus, int stateTableLength, int requiredStateTableLength)
    {
        return VirtioNetTransmitDescriptorFreeListResult.Create(status, queuePlanStatus, headerDescriptorIndex: 0, frameDescriptorIndex: 0, stateTableLength, requiredStateTableLength, VirtioNetTransmitLifecycle.RequiredDescriptorCount, descriptorsScanned: 0, invalidDescriptorIndex: 0);
    }
}
