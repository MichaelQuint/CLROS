namespace OS.Drivers;

public static class VirtioNetTransmitReadinessPlanner
{
    public const bool RequiresQueueSetupExecution = true;
    public const bool RequiresBarrierReadiness = true;
    public const bool UsesInjectedRegisterAccessor = true;
    public const bool UsesFakeRegisterAccessorSink = true;
    public const bool PublishesCallerOwnedDescriptors = true;
    public const bool WritesQueueMemory = true;
    public const bool WritesTransmitHeader = true;
    public const bool MutatesCallerOwnedStateTable = true;
    public const bool PerformsLiveMmio = false;
    public const bool MapsBars = false;
    public const bool NotifiesLiveDevice = false;
    public const bool PollsDevice = false;
    public const bool HandlesInterrupts = false;
    public const bool PerformsDmaMapping = false;
    public const bool AllocatesDmaMemory = false;
    public const bool PerformsPacketIo = false;
    public const bool PerformsPacketTransmit = false;
    public const bool WaitsForUsedRingCompletion = false;
    public const bool ReadsQueueMemory = false;
    public static VirtioNetTransmitReadinessResult ComposeTransmit(VirtioNetQueueMmioSetupPlanResult queueSetup, VirtioNetQueueMmioSetupExecutionResult queueSetupExecution, VirtioNetQueuePlanResult queuePlan, Span<VirtioNetTransmitDescriptorOwnershipState> descriptorStates, ushort availableRingIndex, ulong txHeaderPhysicalAddress, ulong ethernetFramePhysicalAddress, ReadOnlySpan<byte> ethernetFrame, Span<byte> txHeader, Span<byte> queueMemory, VirtioNetQueueNotificationBarrierReadiness barrier, VirtioPciBarAperture notifyAperture, ulong negotiatedFeatures, IVirtioPciRegisterAccessor? registerAccessor)
    {
        if (!queueSetup.Succeeded)
        {
            return VirtioNetTransmitReadinessResult.Create(VirtioNetTransmitReadinessStatus.QueueSetupPlanFailed, queueSetup, queueSetupExecution, default, default, default, barrier, transmitPublishAttempted: false, notificationPlanAttempted: false, notificationExecutionAttempted: false);
        }

        if (!queueSetupExecution.Succeeded)
        {
            return VirtioNetTransmitReadinessResult.Create(VirtioNetTransmitReadinessStatus.QueueSetupExecutionFailed, queueSetup, queueSetupExecution, default, default, default, barrier, transmitPublishAttempted: false, notificationPlanAttempted: false, notificationExecutionAttempted: false);
        }

        if (queueSetupExecution.SetupPlanStatus != queueSetup.Status || queueSetupExecution.CommandsExecuted != queueSetup.CommandCount)
        {
            return VirtioNetTransmitReadinessResult.Create(VirtioNetTransmitReadinessStatus.QueueSetupExecutionMismatch, queueSetup, queueSetupExecution, default, default, default, barrier, transmitPublishAttempted: false, notificationPlanAttempted: false, notificationExecutionAttempted: false);
        }

        VirtioNetTransmitDescriptorPublishPlanResult transmitPublish = VirtioNetTransmitDescriptorPublishPlanner.PublishFromFreeList(queuePlan, descriptorStates, availableRingIndex, txHeaderPhysicalAddress, ethernetFramePhysicalAddress, ethernetFrame, txHeader, queueMemory);
        if (!transmitPublish.Succeeded)
        {
            return VirtioNetTransmitReadinessResult.Create(VirtioNetTransmitReadinessStatus.TransmitPublishFailed, queueSetup, queueSetupExecution, transmitPublish, default, default, barrier, transmitPublishAttempted: true, notificationPlanAttempted: false, notificationExecutionAttempted: false);
        }

        VirtioNetQueueNotificationPlanResult notificationPlan = VirtioNetQueueNotificationPlanner.CreateTransmitNotification(queueSetup, transmitPublish.TransmitPublish, notifyAperture, negotiatedFeatures);
        if (!notificationPlan.Succeeded)
        {
            return VirtioNetTransmitReadinessResult.Create(VirtioNetTransmitReadinessStatus.NotificationPlanFailed, queueSetup, queueSetupExecution, transmitPublish, notificationPlan, default, barrier, transmitPublishAttempted: true, notificationPlanAttempted: true, notificationExecutionAttempted: false);
        }

        VirtioNetQueueNotificationExecutionResult notificationExecution = VirtioNetQueueNotificationExecutor.Execute(notificationPlan, barrier, new VirtioPciNotifyRegisterAccessorSink(registerAccessor));
        if (!notificationExecution.Succeeded)
        {
            VirtioNetTransmitReadinessStatus status = notificationExecution.Status == VirtioNetQueueNotificationExecutionStatus.MemoryBarrierNotReady ? VirtioNetTransmitReadinessStatus.BarrierNotReady : VirtioNetTransmitReadinessStatus.NotificationExecutionFailed;
            return VirtioNetTransmitReadinessResult.Create(status, queueSetup, queueSetupExecution, transmitPublish, notificationPlan, notificationExecution, barrier, transmitPublishAttempted: true, notificationPlanAttempted: true, notificationExecutionAttempted: true);
        }

        return VirtioNetTransmitReadinessResult.Create(VirtioNetTransmitReadinessStatus.Success, queueSetup, queueSetupExecution, transmitPublish, notificationPlan, notificationExecution, barrier, transmitPublishAttempted: true, notificationPlanAttempted: true, notificationExecutionAttempted: true);
    }
}
