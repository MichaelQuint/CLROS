namespace OS.Drivers;

public static class VirtioNetQueueNotificationExecutor
{
    public const bool RequiresBarrierReadiness = true;
    public const bool UsesFakeSink = true;
    public const bool WritesNotifySink = true;
    public const bool PerformsLiveMmio = false;
    public const bool MapsBars = false;
    public const bool PublishesDescriptors = false;
    public const bool MutatesDescriptorState = false;
    public const bool ReadsQueueMemory = false;
    public const bool PollsDevice = false;
    public const bool HandlesInterrupts = false;
    public const bool PerformsDmaMapping = false;
    public const bool AllocatesDmaMemory = false;
    public const bool PerformsPacketIo = false;
    public const bool NotifiesLiveDevice = false;
    public static VirtioNetQueueNotificationExecutionResult Execute(VirtioNetQueueNotificationPlanResult notificationPlan, VirtioNetQueueNotificationBarrierReadiness barrier, IVirtioPciNotifyWriteSink? sink)
    {
        if (!notificationPlan.Succeeded)
        {
            return VirtioNetQueueNotificationExecutionResult.FailedPlan(notificationPlan, barrier);
        }

        if (!barrier.Succeeded)
        {
            return VirtioNetQueueNotificationExecutionResult.FromPlan(VirtioNetQueueNotificationExecutionStatus.MemoryBarrierNotReady, notificationPlan, barrier, VirtioPciNotifyWriteStatus.NotReady);
        }

        VirtioNetQueueNotificationPlan plan = notificationPlan.Plan;
        if (plan.WriteWidthBytes != VirtioPciModernRegisterMap.Notification.NoNotificationDataWriteWidthBytes)
        {
            return VirtioNetQueueNotificationExecutionResult.FromPlan(VirtioNetQueueNotificationExecutionStatus.UnsupportedWriteWidth, notificationPlan, barrier, VirtioPciNotifyWriteStatus.NotReady);
        }

        if (sink is null)
        {
            return VirtioNetQueueNotificationExecutionResult.FromPlan(VirtioNetQueueNotificationExecutionStatus.SinkNotReady, notificationPlan, barrier, VirtioPciNotifyWriteStatus.NotReady);
        }

        VirtioPciNotifyWriteStatus sinkStatus = sink.Write16(plan.NotificationPlan.Aperture, plan.NotifyRelativeOffset, plan.WriteValue);
        if (sinkStatus == VirtioPciNotifyWriteStatus.Success)
        {
            return VirtioNetQueueNotificationExecutionResult.FromPlan(VirtioNetQueueNotificationExecutionStatus.Success, notificationPlan, barrier, sinkStatus);
        }

        VirtioNetQueueNotificationExecutionStatus status = sinkStatus == VirtioPciNotifyWriteStatus.RangeOutsideAperture ? VirtioNetQueueNotificationExecutionStatus.SinkRangeMismatch : sinkStatus == VirtioPciNotifyWriteStatus.NotReady ? VirtioNetQueueNotificationExecutionStatus.SinkNotReady : VirtioNetQueueNotificationExecutionStatus.SinkWriteFailed;
        return VirtioNetQueueNotificationExecutionResult.FromPlan(status, notificationPlan, barrier, sinkStatus);
    }
}
