namespace OS.Drivers;

public static class VirtioNetTransmitRegisterWindowReclaimPlanner
{
    public const bool RequiresSuccessfulRegisterWindowReadiness = true;
    public const bool ConsumesRegisterWindowNotificationProvenance = true;
    public const bool RequiresCallerOwnedQueueMemory = true;
    public const bool RequiresCallerOwnedDescriptorStateTable = true;
    public const bool ReadsQueueMemory = true;
    public const bool MutatesCallerOwnedStateTable = true;
    public const bool PerformsLiveMmio = false;
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
    public const bool PerformsPacketIo = false;
    public const bool PerformsPacketTransmit = false;
    public const bool WaitsForUsedRingCompletion = false;
    public const bool WritesQueueMemory = false;
    public const bool WritesTransmitHeader = false;
    public const bool InvokesRegisterWindowAccessExecutor = false;
    public const bool WritesCallerOwnedNotifyWindowBuffer = false;
    public static VirtioNetTransmitRegisterWindowReclaimResult ApplyCompletedDescriptor(VirtioNetTransmitRegisterWindowReadinessResult readiness, Span<VirtioNetTransmitDescriptorOwnershipState> descriptorStates, ushort previousUsedIndex, ReadOnlySpan<byte> queueMemory)
    {
        if (!readiness.Succeeded)
        {
            return VirtioNetTransmitRegisterWindowReclaimResult.Create(VirtioNetTransmitRegisterWindowReclaimStatus.ReadinessFailed, readiness, default, default, completionPlanAttempted: false, ownershipReclaimAttempted: false, previousUsedIndex);
        }

        VirtioNetTransmitCompletionPlanResult completion = VirtioNetTransmitCompletionPlanner.Create(readiness.TransmitPublish.TransmitPublish, readiness.NotificationExecution, previousUsedIndex, queueMemory);
        if (!completion.Succeeded)
        {
            return VirtioNetTransmitRegisterWindowReclaimResult.Create(VirtioNetTransmitRegisterWindowReclaimStatus.CompletionPlanFailed, readiness, completion, default, completionPlanAttempted: true, ownershipReclaimAttempted: false, previousUsedIndex);
        }

        VirtioNetTransmitDescriptorOwnershipPlanResult ownershipReclaim = VirtioNetTransmitDescriptorOwnershipPlan.ApplyCompletion(completion, descriptorStates);
        if (!ownershipReclaim.Succeeded)
        {
            return VirtioNetTransmitRegisterWindowReclaimResult.Create(VirtioNetTransmitRegisterWindowReclaimStatus.OwnershipReclaimFailed, readiness, completion, ownershipReclaim, completionPlanAttempted: true, ownershipReclaimAttempted: true, previousUsedIndex);
        }

        return VirtioNetTransmitRegisterWindowReclaimResult.Create(VirtioNetTransmitRegisterWindowReclaimStatus.Success, readiness, completion, ownershipReclaim, completionPlanAttempted: true, ownershipReclaimAttempted: true, previousUsedIndex);
    }
}
