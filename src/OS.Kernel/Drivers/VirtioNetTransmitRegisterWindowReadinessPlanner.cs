namespace OS.Drivers;
public static class VirtioNetTransmitRegisterWindowReadinessPlanner
{
    public const bool RequiresQueueSetupRegisterWindowExecution = true;
    public const bool RequiresRegisterWindowBinding = true;
    public const bool RequiresBarrierReadiness = true;
    public const bool UsesCallerOwnedQueueMemory = true;
    public const bool UsesCallerOwnedNotifyWindowBuffer = true;
    public const bool PublishesCallerOwnedTransmitDescriptors = true;
    public const bool WritesQueueMemory = true;
    public const bool WritesTransmitHeader = true;
    public const bool MutatesCallerOwnedStateTable = true;
    public const bool InvokesRegisterWindowAccessExecutor = true;
    public const bool WritesCallerOwnedNotifyWindowBuffer = true;
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
    public const bool ReadsQueueMemory = false;
    public static VirtioNetTransmitRegisterWindowReadinessResult ComposeTransmit(VirtioNetQueueMmioSetupPlanResult queueSetup, VirtioNetQueueMmioSetupRegisterWindowExecutionResult queueSetupExecution, VirtioNetQueuePlanResult queuePlan, Span<VirtioNetTransmitDescriptorOwnershipState> descriptorStates, ushort availableRingIndex, ulong txHeaderPhysicalAddress, ulong ethernetFramePhysicalAddress, ReadOnlySpan<byte> ethernetFrame, Span<byte> txHeader, Span<byte> queueMemory, VirtioNetQueueNotificationBarrierReadiness barrier, VirtioPciBarAperture notifyAperture, VirtioPciBarRegisterWindowBinding notifyConfiguration, Span<byte> notifyWindowBytes, ulong negotiatedFeatures, bool queueEnabled = true)
    {
        if (!queueSetup.Succeeded)
        {
            return VirtioNetTransmitRegisterWindowReadinessResult.Create(VirtioNetTransmitRegisterWindowReadinessStatus.QueueSetupPlanFailed, queueSetup, queueSetupExecution, default, default, default, barrier, transmitPublishAttempted: false, notificationPlanAttempted: false, notificationWindowExecutionAttempted: false);
        }

        if (!queueSetupExecution.Succeeded)
        {
            return VirtioNetTransmitRegisterWindowReadinessResult.Create(VirtioNetTransmitRegisterWindowReadinessStatus.QueueSetupWindowExecutionFailed, queueSetup, queueSetupExecution, default, default, default, barrier, transmitPublishAttempted: false, notificationPlanAttempted: false, notificationWindowExecutionAttempted: false);
        }

        if (queueSetupExecution.SetupPlanStatus != queueSetup.Status || queueSetupExecution.CommandsExecuted != queueSetup.CommandCount)
        {
            return VirtioNetTransmitRegisterWindowReadinessResult.Create(VirtioNetTransmitRegisterWindowReadinessStatus.QueueSetupWindowExecutionMismatch, queueSetup, queueSetupExecution, default, default, default, barrier, transmitPublishAttempted: false, notificationPlanAttempted: false, notificationWindowExecutionAttempted: false);
        }

        VirtioNetTransmitDescriptorPublishPlanResult transmitPublish = VirtioNetTransmitDescriptorPublishPlanner.PublishFromFreeList(queuePlan, descriptorStates, availableRingIndex, txHeaderPhysicalAddress, ethernetFramePhysicalAddress, ethernetFrame, txHeader, queueMemory);
        if (!transmitPublish.Succeeded)
        {
            return VirtioNetTransmitRegisterWindowReadinessResult.Create(VirtioNetTransmitRegisterWindowReadinessStatus.TransmitPublishFailed, queueSetup, queueSetupExecution, transmitPublish, default, default, barrier, transmitPublishAttempted: true, notificationPlanAttempted: false, notificationWindowExecutionAttempted: false);
        }

        VirtioNetQueueNotificationPlanResult notificationPlan = VirtioNetQueueNotificationPlanner.CreateTransmitNotification(queueSetup, transmitPublish.TransmitPublish, notifyAperture, negotiatedFeatures, queueEnabled);
        if (!notificationPlan.Succeeded)
        {
            return VirtioNetTransmitRegisterWindowReadinessResult.Create(VirtioNetTransmitRegisterWindowReadinessStatus.NotificationPlanFailed, queueSetup, queueSetupExecution, transmitPublish, notificationPlan, default, barrier, transmitPublishAttempted: true, notificationPlanAttempted: true, notificationWindowExecutionAttempted: false);
        }

        VirtioNetQueueNotificationRegisterWindowBarrierExecutionResult notificationExecution = VirtioNetQueueNotificationRegisterWindowBarrierExecutor.Execute(notificationPlan, barrier, notifyConfiguration, notifyWindowBytes);
        if (!notificationExecution.Succeeded)
        {
            VirtioNetTransmitRegisterWindowReadinessStatus status = notificationExecution.Status == VirtioNetQueueNotificationRegisterWindowBarrierExecutionStatus.MemoryBarrierNotReady ? VirtioNetTransmitRegisterWindowReadinessStatus.BarrierNotReady : VirtioNetTransmitRegisterWindowReadinessStatus.NotificationWindowExecutionFailed;
            return VirtioNetTransmitRegisterWindowReadinessResult.Create(status, queueSetup, queueSetupExecution, transmitPublish, notificationPlan, notificationExecution, barrier, transmitPublishAttempted: true, notificationPlanAttempted: true, notificationWindowExecutionAttempted: true);
        }

        return VirtioNetTransmitRegisterWindowReadinessResult.Create(VirtioNetTransmitRegisterWindowReadinessStatus.Success, queueSetup, queueSetupExecution, transmitPublish, notificationPlan, notificationExecution, barrier, transmitPublishAttempted: true, notificationPlanAttempted: true, notificationWindowExecutionAttempted: true);
    }
}
