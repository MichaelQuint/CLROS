namespace OS.Drivers;
public static class VirtioNetReceiveReadinessPlanner
{
    public const bool RequiresQueueSetupRegisterWindowExecution = true;
    public const bool RequiresRegisterWindowBinding = true;
    public const bool RequiresBarrierReadiness = true;
    public const bool UsesCallerOwnedQueueMemory = true;
    public const bool UsesCallerOwnedNotifyWindowBuffer = true;
    public const bool PublishesCallerOwnedReceiveDescriptors = true;
    public const bool WritesQueueMemory = true;
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
    public const bool PerformsPacketReceive = false;
    public const bool ReadsQueueMemory = false;
    public static VirtioNetReceiveReadinessResult ComposeReceive(VirtioNetQueueMmioSetupPlanResult queueSetup, VirtioNetQueueMmioSetupRegisterWindowExecutionResult queueSetupExecution, ushort firstDescriptorIndex, ushort availableDescriptorCount, ushort previousAvailableRingIndex, ushort previousUsedIndex, ulong firstReceiveBufferPhysicalAddress, uint receiveBufferStrideBytes, ushort receiveBufferCount, uint frameBufferBytes, Span<byte> queueMemory, VirtioNetQueueNotificationBarrierReadiness barrier, VirtioPciBarAperture notifyAperture, VirtioPciBarRegisterWindowBinding notifyConfiguration, Span<byte> notifyWindowBytes, ulong negotiatedFeatures, bool queueEnabled = true)
    {
        if (!queueSetup.Succeeded)
        {
            return VirtioNetReceiveReadinessResult.Create(VirtioNetReceiveReadinessStatus.QueueSetupPlanFailed, queueSetup, queueSetupExecution, default, default, default, barrier, receivePublishAttempted: false, notificationPlanAttempted: false, notificationWindowExecutionAttempted: false);
        }

        if (!queueSetupExecution.Succeeded)
        {
            return VirtioNetReceiveReadinessResult.Create(VirtioNetReceiveReadinessStatus.QueueSetupWindowExecutionFailed, queueSetup, queueSetupExecution, default, default, default, barrier, receivePublishAttempted: false, notificationPlanAttempted: false, notificationWindowExecutionAttempted: false);
        }

        if (queueSetupExecution.SetupPlanStatus != queueSetup.Status || queueSetupExecution.CommandsExecuted != queueSetup.CommandCount)
        {
            return VirtioNetReceiveReadinessResult.Create(VirtioNetReceiveReadinessStatus.QueueSetupWindowExecutionMismatch, queueSetup, queueSetupExecution, default, default, default, barrier, receivePublishAttempted: false, notificationPlanAttempted: false, notificationWindowExecutionAttempted: false);
        }

        VirtioNetReceiveLifecyclePublishResult receivePublish = VirtioNetReceiveLifecycle.PublishReceiveBuffers(queueSetup.Plan.RxQueue.PlannedQueueSize, firstDescriptorIndex, availableDescriptorCount, previousAvailableRingIndex, previousUsedIndex, firstReceiveBufferPhysicalAddress, receiveBufferStrideBytes, receiveBufferCount, frameBufferBytes, queueMemory);
        if (!receivePublish.Succeeded)
        {
            return VirtioNetReceiveReadinessResult.Create(VirtioNetReceiveReadinessStatus.ReceivePublishFailed, queueSetup, queueSetupExecution, receivePublish, default, default, barrier, receivePublishAttempted: true, notificationPlanAttempted: false, notificationWindowExecutionAttempted: false);
        }

        VirtioNetQueueNotificationPlanResult notificationPlan = VirtioNetQueueNotificationPlanner.CreateReceiveNotification(queueSetup, receivePublish, notifyAperture, negotiatedFeatures, queueEnabled);
        if (!notificationPlan.Succeeded)
        {
            return VirtioNetReceiveReadinessResult.Create(VirtioNetReceiveReadinessStatus.NotificationPlanFailed, queueSetup, queueSetupExecution, receivePublish, notificationPlan, default, barrier, receivePublishAttempted: true, notificationPlanAttempted: true, notificationWindowExecutionAttempted: false);
        }

        VirtioNetQueueNotificationRegisterWindowBarrierExecutionResult notificationExecution = VirtioNetQueueNotificationRegisterWindowBarrierExecutor.Execute(notificationPlan, barrier, notifyConfiguration, notifyWindowBytes);
        if (!notificationExecution.Succeeded)
        {
            VirtioNetReceiveReadinessStatus status = notificationExecution.Status == VirtioNetQueueNotificationRegisterWindowBarrierExecutionStatus.MemoryBarrierNotReady ? VirtioNetReceiveReadinessStatus.BarrierNotReady : VirtioNetReceiveReadinessStatus.NotificationWindowExecutionFailed;
            return VirtioNetReceiveReadinessResult.Create(status, queueSetup, queueSetupExecution, receivePublish, notificationPlan, notificationExecution, barrier, receivePublishAttempted: true, notificationPlanAttempted: true, notificationWindowExecutionAttempted: true);
        }

        return VirtioNetReceiveReadinessResult.Create(VirtioNetReceiveReadinessStatus.Success, queueSetup, queueSetupExecution, receivePublish, notificationPlan, notificationExecution, barrier, receivePublishAttempted: true, notificationPlanAttempted: true, notificationWindowExecutionAttempted: true);
    }
}
