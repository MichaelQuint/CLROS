namespace OS.Drivers;

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
    public static VirtioNetTransmitDescriptorOwnershipPlanResult MarkPublished(VirtioNetTransmitLifecycleResult transmitPublish, Span<VirtioNetTransmitDescriptorOwnershipState> descriptorStates)
    {
        if (!IsValidTransmitPublish(transmitPublish))
        {
            return CreateFailure(VirtioNetTransmitDescriptorOwnershipStatus.TransmitPublishFailed, transmitPublish.Status, VirtioNetTransmitCompletionPlanStatus.Success, headerDescriptorIndex: 0, frameDescriptorIndex: 0, descriptorStates.Length, requiredStateTableLength: 0);
        }

        ushort headerDescriptorIndex = transmitPublish.Plan.HeaderDescriptor.DescriptorIndex;
        ushort frameDescriptorIndex = transmitPublish.Plan.FrameDescriptor.DescriptorIndex;
        int requiredStateTableLength = GetRequiredStateTableLength(headerDescriptorIndex, frameDescriptorIndex);
        if (descriptorStates.Length < requiredStateTableLength)
        {
            return CreateFailure(VirtioNetTransmitDescriptorOwnershipStatus.StateTableTooSmall, transmitPublish.Status, VirtioNetTransmitCompletionPlanStatus.Success, headerDescriptorIndex, frameDescriptorIndex, descriptorStates.Length, requiredStateTableLength);
        }

        if (descriptorStates[headerDescriptorIndex] != VirtioNetTransmitDescriptorOwnershipState.Free || descriptorStates[frameDescriptorIndex] != VirtioNetTransmitDescriptorOwnershipState.Free)
        {
            return CreateFailure(VirtioNetTransmitDescriptorOwnershipStatus.DescriptorAlreadyInFlight, transmitPublish.Status, VirtioNetTransmitCompletionPlanStatus.Success, headerDescriptorIndex, frameDescriptorIndex, descriptorStates.Length, requiredStateTableLength);
        }

        descriptorStates[headerDescriptorIndex] = VirtioNetTransmitDescriptorOwnershipState.InFlight;
        descriptorStates[frameDescriptorIndex] = VirtioNetTransmitDescriptorOwnershipState.InFlight;
        return VirtioNetTransmitDescriptorOwnershipPlanResult.Create(VirtioNetTransmitDescriptorOwnershipStatus.Success, transmitPublish.Status, VirtioNetTransmitCompletionPlanStatus.Success, headerDescriptorIndex, frameDescriptorIndex, descriptorStates.Length, requiredStateTableLength, VirtioNetTransmitLifecycle.RequiredDescriptorCount, descriptorsFreed: 0);
    }

    public static VirtioNetTransmitDescriptorOwnershipPlanResult ApplyCompletion(VirtioNetTransmitCompletionPlanResult completion, Span<VirtioNetTransmitDescriptorOwnershipState> descriptorStates)
    {
        if (!IsValidCompletion(completion))
        {
            return CreateFailure(VirtioNetTransmitDescriptorOwnershipStatus.CompletionPlanFailed, VirtioNetTransmitLifecycleStatus.Success, completion.Status, headerDescriptorIndex: 0, frameDescriptorIndex: 0, descriptorStates.Length, requiredStateTableLength: 0);
        }

        ushort headerDescriptorIndex = completion.HeaderDescriptorIndex;
        ushort frameDescriptorIndex = completion.FrameDescriptorIndex;
        int requiredStateTableLength = GetRequiredStateTableLength(headerDescriptorIndex, frameDescriptorIndex);
        if (descriptorStates.Length < requiredStateTableLength)
        {
            return CreateFailure(VirtioNetTransmitDescriptorOwnershipStatus.StateTableTooSmall, VirtioNetTransmitLifecycleStatus.Success, completion.Status, headerDescriptorIndex, frameDescriptorIndex, descriptorStates.Length, requiredStateTableLength);
        }

        bool headerInFlight = descriptorStates[headerDescriptorIndex] == VirtioNetTransmitDescriptorOwnershipState.InFlight;
        bool frameInFlight = descriptorStates[frameDescriptorIndex] == VirtioNetTransmitDescriptorOwnershipState.InFlight;
        if (!headerInFlight || !frameInFlight)
        {
            VirtioNetTransmitDescriptorOwnershipStatus status = !headerInFlight && !frameInFlight && descriptorStates[headerDescriptorIndex] == VirtioNetTransmitDescriptorOwnershipState.Free && descriptorStates[frameDescriptorIndex] == VirtioNetTransmitDescriptorOwnershipState.Free ? VirtioNetTransmitDescriptorOwnershipStatus.DuplicateCompletion : VirtioNetTransmitDescriptorOwnershipStatus.DescriptorNotInFlight;
            return CreateFailure(status, VirtioNetTransmitLifecycleStatus.Success, completion.Status, headerDescriptorIndex, frameDescriptorIndex, descriptorStates.Length, requiredStateTableLength);
        }

        descriptorStates[headerDescriptorIndex] = VirtioNetTransmitDescriptorOwnershipState.Free;
        descriptorStates[frameDescriptorIndex] = VirtioNetTransmitDescriptorOwnershipState.Free;
        return VirtioNetTransmitDescriptorOwnershipPlanResult.Create(VirtioNetTransmitDescriptorOwnershipStatus.Success, VirtioNetTransmitLifecycleStatus.Success, completion.Status, headerDescriptorIndex, frameDescriptorIndex, descriptorStates.Length, requiredStateTableLength, descriptorsMarkedInFlight: 0, completion.ReclaimedDescriptorCount);
    }

    private static bool IsValidTransmitPublish(VirtioNetTransmitLifecycleResult transmitPublish)
    {
        return transmitPublish.Succeeded && transmitPublish.DescriptorsWritten == VirtioNetTransmitLifecycle.RequiredDescriptorCount && transmitPublish.Plan.HeaderDescriptor.DescriptorIndex != transmitPublish.Plan.FrameDescriptor.DescriptorIndex && VirtqueueSplitRingPlan.ValidateQueueSize(transmitPublish.Plan.TxQueue.QueueSize) == VirtqueueSplitRingPlanStatus.Success;
    }

    private static bool IsValidCompletion(VirtioNetTransmitCompletionPlanResult completion)
    {
        return completion.Succeeded && completion.ReclaimedDescriptorCount == VirtioNetTransmitLifecycle.RequiredDescriptorCount && completion.HeaderDescriptorIndex != completion.FrameDescriptorIndex;
    }

    private static int GetRequiredStateTableLength(ushort headerDescriptorIndex, ushort frameDescriptorIndex)
    {
        return (headerDescriptorIndex > frameDescriptorIndex ? headerDescriptorIndex : frameDescriptorIndex) + 1;
    }

    private static VirtioNetTransmitDescriptorOwnershipPlanResult CreateFailure(VirtioNetTransmitDescriptorOwnershipStatus status, VirtioNetTransmitLifecycleStatus transmitPublishStatus, VirtioNetTransmitCompletionPlanStatus completionPlanStatus, ushort headerDescriptorIndex, ushort frameDescriptorIndex, int stateTableLength, int requiredStateTableLength)
    {
        return VirtioNetTransmitDescriptorOwnershipPlanResult.Create(status, transmitPublishStatus, completionPlanStatus, headerDescriptorIndex, frameDescriptorIndex, stateTableLength, requiredStateTableLength, descriptorsMarkedInFlight: 0, descriptorsFreed: 0);
    }
}
