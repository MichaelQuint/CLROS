namespace OS.Drivers;
public static class VirtioNetQueueNotificationPlanner
{
    public const bool PerformsMmio = false;
    public const bool RequiresRegisterWindowBinding = true;
    public const bool PlansMappedRegisterAddresses = true;
    public const bool MapsBars = false;
    public const bool PerformsLiveMmio = false;
    public const bool ContainsVolatileAccess = false;
    public const bool ReadsDeviceRegisters = false;
    public const bool WritesDeviceRegisters = false;
    public const bool PublishesDescriptors = false;
    public const bool NotifiesDevice = false;
    public const bool HandlesInterrupts = false;
    public const bool AllocatesDmaMemory = false;
    public const bool PerformsDmaMapping = false;
    public const bool PerformsPacketIo = false;
    public const bool SupportsNotificationData = false;
    public static VirtioNetQueueNotificationRegisterWindowPlanResult PlanRegisterWindowAccess(VirtioNetQueueNotificationPlanResult notificationPlan, VirtioPciBarRegisterWindowBinding notifyConfiguration)
    {
        if (!notificationPlan.Succeeded)
        {
            return VirtioNetQueueNotificationRegisterWindowPlanResult.FailedNotification(notificationPlan);
        }

        VirtioNetQueueNotificationPlan plan = notificationPlan.Plan;
        if (plan.WriteWidthBytes != VirtioPciModernRegisterMap.Notification.NoNotificationDataWriteWidthBytes)
        {
            return VirtioNetQueueNotificationRegisterWindowPlanResult.FromPlan(VirtioNetQueueNotificationRegisterWindowPlanStatus.UnsupportedWriteWidth, notificationPlan, VirtioPciBarRegisterWindowPlanStatus.UnsupportedAccessWidth, default);
        }

        VirtioPciBarRegisterWindowAccessPlanResult accessPlan = VirtioPciBarRegisterWindowPlanner.PlanAccess(notifyConfiguration, plan.NotifyRelativeOffset, VirtioPciRegisterAccessWidth.Word);
        if (!accessPlan.Succeeded)
        {
            return VirtioNetQueueNotificationRegisterWindowPlanResult.FromPlan(VirtioNetQueueNotificationRegisterWindowPlanStatus.RegisterWindowPlanFailed, notificationPlan, accessPlan.Status, default);
        }

        return VirtioNetQueueNotificationRegisterWindowPlanResult.FromPlan(VirtioNetQueueNotificationRegisterWindowPlanStatus.Success, notificationPlan, accessPlan.Status, accessPlan.Plan);
    }

    public static VirtioNetQueueNotificationPlanResult CreateReceiveNotification(VirtioNetQueueMmioSetupPlanResult queueSetup, VirtioNetReceiveLifecyclePublishResult receivePublish, VirtioPciBarAperture notifyAperture, ulong negotiatedFeatures, bool queueEnabled = true)
    {
        if (!queueSetup.Succeeded)
        {
            return Fail(VirtioNetQueueNotificationPlanStatus.QueueSetupFailed, VirtioNetQueueNotificationKind.Receive, queueSetup.Status, receivePublish.Status, VirtioNetTransmitLifecycleStatus.Success, VirtioPciQueueNotificationPlanStatus.Success);
        }

        if (!receivePublish.Succeeded)
        {
            return Fail(VirtioNetQueueNotificationPlanStatus.ReceivePublishFailed, VirtioNetQueueNotificationKind.Receive, queueSetup.Status, receivePublish.Status, VirtioNetTransmitLifecycleStatus.Success, VirtioPciQueueNotificationPlanStatus.Success);
        }

        return CreateNotification(VirtioNetQueueNotificationKind.Receive, queueSetup, queueSetup.Plan.RxQueue, notifyAperture, negotiatedFeatures, queueEnabled, receivePublish.PublishedDescriptorCount, receivePublish.PreviousAvailableRingIndex, receivePublish.NextAvailableRingIndex, receivePublish.Status, VirtioNetTransmitLifecycleStatus.Success);
    }

    public static VirtioNetQueueNotificationPlanResult CreateTransmitNotification(VirtioNetQueueMmioSetupPlanResult queueSetup, VirtioNetTransmitLifecycleResult transmitPublish, VirtioPciBarAperture notifyAperture, ulong negotiatedFeatures, bool queueEnabled = true)
    {
        if (!queueSetup.Succeeded)
        {
            return Fail(VirtioNetQueueNotificationPlanStatus.QueueSetupFailed, VirtioNetQueueNotificationKind.Transmit, queueSetup.Status, VirtioNetReceiveLifecyclePublishStatus.Success, transmitPublish.Status, VirtioPciQueueNotificationPlanStatus.Success);
        }

        if (!transmitPublish.Succeeded)
        {
            return Fail(VirtioNetQueueNotificationPlanStatus.TransmitPublishFailed, VirtioNetQueueNotificationKind.Transmit, queueSetup.Status, VirtioNetReceiveLifecyclePublishStatus.Success, transmitPublish.Status, VirtioPciQueueNotificationPlanStatus.Success);
        }

        return CreateNotification(VirtioNetQueueNotificationKind.Transmit, queueSetup, queueSetup.Plan.TxQueue, notifyAperture, negotiatedFeatures, queueEnabled, transmitPublish.DescriptorsWritten, transmitPublish.Plan.AvailableRingIndex, transmitPublish.NextAvailableRingIndex, VirtioNetReceiveLifecyclePublishStatus.Success, transmitPublish.Status);
    }

    private static VirtioNetQueueNotificationPlanResult CreateNotification(VirtioNetQueueNotificationKind kind, VirtioNetQueueMmioSetupPlanResult queueSetup, VirtioNetQueueMmioSetupQueuePlan queue, VirtioPciBarAperture notifyAperture, ulong negotiatedFeatures, bool queueEnabled, ushort publishedDescriptorCount, ushort previousAvailableRingIndex, ushort nextAvailableRingIndex, VirtioNetReceiveLifecyclePublishStatus receivePublishStatus, VirtioNetTransmitLifecycleStatus transmitPublishStatus)
    {
        VirtioPciQueueNotificationPlanResult notification = VirtioPciQueueNotificationPlanner.Create(notifyAperture, queue.QueueIndex, VirtioNetQueuePlan.MinimumQueueCount, queue.NotifyOffset, negotiatedFeatures, queueEnabled, hasNewlyPublishedDescriptors: publishedDescriptorCount != 0);
        if (!notification.Succeeded)
        {
            return Fail(VirtioNetQueueNotificationPlanStatus.NotificationPlanFailed, kind, queueSetup.Status, receivePublishStatus, transmitPublishStatus, notification.Status);
        }

        VirtioNetQueueNotificationPlan plan = new VirtioNetQueueNotificationPlan(kind, publishedDescriptorCount, previousAvailableRingIndex, nextAvailableRingIndex, notification.Plan);
        return VirtioNetQueueNotificationPlanResult.Success(kind, queueSetup.Status, receivePublishStatus, transmitPublishStatus, notification.Status, plan);
    }

    private static VirtioNetQueueNotificationPlanResult Fail(VirtioNetQueueNotificationPlanStatus status, VirtioNetQueueNotificationKind kind, VirtioNetQueueMmioSetupPlanStatus queueSetupStatus, VirtioNetReceiveLifecyclePublishStatus receivePublishStatus, VirtioNetTransmitLifecycleStatus transmitPublishStatus, VirtioPciQueueNotificationPlanStatus notificationStatus)
    {
        return VirtioNetQueueNotificationPlanResult.Fail(status, kind, queueSetupStatus, receivePublishStatus, transmitPublishStatus, notificationStatus);
    }
}
