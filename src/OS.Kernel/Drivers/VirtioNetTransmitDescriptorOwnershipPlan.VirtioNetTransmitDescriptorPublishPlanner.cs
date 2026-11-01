namespace OS.Drivers;
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
    public static VirtioNetTransmitDescriptorPublishPlanResult PublishFromFreeList(VirtioNetQueuePlanResult queuePlan, Span<VirtioNetTransmitDescriptorOwnershipState> descriptorStates, ushort availableRingIndex, ulong txHeaderPhysicalAddress, ulong ethernetFramePhysicalAddress, ReadOnlySpan<byte> ethernetFrame, Span<byte> txHeader, Span<byte> queueMemory)
    {
        VirtioNetTransmitDescriptorFreeListResult freeList = VirtioNetTransmitDescriptorFreeListPlanner.SelectFreeDescriptorPair(queuePlan, descriptorStates);
        if (!freeList.Succeeded)
        {
            return VirtioNetTransmitDescriptorPublishPlanResult.Create(VirtioNetTransmitDescriptorPublishPlanStatus.FreeListFailed, queuePlan.Status, freeList, default, default, transmitPublishAttempted: false, ownershipMarkAttempted: false);
        }

        VirtioNetTransmitLifecycleResult transmitPublish = VirtioNetTransmitLifecycle.Publish(queuePlan, freeList.HeaderDescriptorIndex, freeList.DescriptorsRequired, availableRingIndex, txHeaderPhysicalAddress, ethernetFramePhysicalAddress, ethernetFrame, txHeader, queueMemory);
        if (!transmitPublish.Succeeded)
        {
            return VirtioNetTransmitDescriptorPublishPlanResult.Create(VirtioNetTransmitDescriptorPublishPlanStatus.TransmitPublishFailed, queuePlan.Status, freeList, transmitPublish, default, transmitPublishAttempted: true, ownershipMarkAttempted: false);
        }

        VirtioNetTransmitDescriptorOwnershipPlanResult ownershipMark = VirtioNetTransmitDescriptorOwnershipPlan.MarkPublished(transmitPublish, descriptorStates);
        if (!ownershipMark.Succeeded)
        {
            return VirtioNetTransmitDescriptorPublishPlanResult.Create(VirtioNetTransmitDescriptorPublishPlanStatus.OwnershipMarkFailed, queuePlan.Status, freeList, transmitPublish, ownershipMark, transmitPublishAttempted: true, ownershipMarkAttempted: true);
        }

        return VirtioNetTransmitDescriptorPublishPlanResult.Create(VirtioNetTransmitDescriptorPublishPlanStatus.Success, queuePlan.Status, freeList, transmitPublish, ownershipMark, transmitPublishAttempted: true, ownershipMarkAttempted: true);
    }
}
