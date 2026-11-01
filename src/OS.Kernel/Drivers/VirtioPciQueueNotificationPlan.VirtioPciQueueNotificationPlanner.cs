namespace OS.Drivers;
public static class VirtioPciQueueNotificationPlanner
{
    public const bool PerformsMmio = false;
    public const bool PublishesDescriptors = false;
    public const bool HandlesInterrupts = false;
    public const bool PerformsPacketIo = false;
    public const bool SupportsNotificationData = false;
    public const bool SupportsNotificationConfigData = false;
    public static VirtioPciQueueNotificationPlanResult Create(VirtioPciBarAperture notifyAperture, uint queueIndex, ushort availableQueueCount, ushort queueNotifyOffset, ulong negotiatedFeatures, bool queueEnabled, bool hasNewlyPublishedDescriptors)
    {
        if (notifyAperture.CapabilityType != VirtioPciCapabilityType.NotifyConfiguration)
        {
            return Fail(VirtioPciQueueNotificationPlanStatus.InvalidNotifyAperture, notifyAperture, queueIndex, availableQueueCount, queueNotifyOffset, negotiatedFeatures);
        }

        if (queueIndex > ushort.MaxValue || availableQueueCount == 0 || queueIndex >= availableQueueCount)
        {
            return Fail(VirtioPciQueueNotificationPlanStatus.InvalidQueueIndex, notifyAperture, queueIndex, availableQueueCount, queueNotifyOffset, negotiatedFeatures);
        }

        if ((negotiatedFeatures & VirtioPciQueueNotificationFeatureBits.NotificationData) != 0)
        {
            return Fail(VirtioPciQueueNotificationPlanStatus.UnsupportedNotificationDataFeature, notifyAperture, queueIndex, availableQueueCount, queueNotifyOffset, negotiatedFeatures);
        }

        if ((negotiatedFeatures & VirtioPciQueueNotificationFeatureBits.NotificationConfigData) != 0)
        {
            return Fail(VirtioPciQueueNotificationPlanStatus.UnsupportedNotificationConfigDataFeature, notifyAperture, queueIndex, availableQueueCount, queueNotifyOffset, negotiatedFeatures);
        }

        if (!queueEnabled)
        {
            return Fail(VirtioPciQueueNotificationPlanStatus.QueueNotEnabled, notifyAperture, queueIndex, availableQueueCount, queueNotifyOffset, negotiatedFeatures);
        }

        if (!hasNewlyPublishedDescriptors)
        {
            return Fail(VirtioPciQueueNotificationPlanStatus.NoNewlyPublishedDescriptors, notifyAperture, queueIndex, availableQueueCount, queueNotifyOffset, negotiatedFeatures);
        }

        ulong relativeOffset = (ulong)queueNotifyOffset * notifyAperture.NotifyOffsetMultiplier;
        if (relativeOffset > uint.MaxValue)
        {
            return Fail(VirtioPciQueueNotificationPlanStatus.NotifyOffsetOverflow, notifyAperture, queueIndex, availableQueueCount, queueNotifyOffset, negotiatedFeatures);
        }

        if (!IsNotifyRangeInsideAperture((uint)relativeOffset, notifyAperture.Length))
        {
            return Fail(VirtioPciQueueNotificationPlanStatus.NotifyRangeOutsideAperture, notifyAperture, queueIndex, availableQueueCount, queueNotifyOffset, negotiatedFeatures);
        }

        VirtioPciQueueNotificationPlan plan = new VirtioPciQueueNotificationPlan(notifyAperture, (ushort)queueIndex, queueNotifyOffset, notifyAperture.NotifyOffsetMultiplier, (uint)relativeOffset);
        return VirtioPciQueueNotificationPlanResult.Success(plan, availableQueueCount, negotiatedFeatures);
    }

    private static bool IsNotifyRangeInsideAperture(uint relativeOffset, uint apertureLength)
    {
        ulong end = (ulong)relativeOffset + VirtioPciModernRegisterMap.Notification.NoNotificationDataWriteWidthBytes;
        return end <= apertureLength;
    }

    private static VirtioPciQueueNotificationPlanResult Fail(VirtioPciQueueNotificationPlanStatus status, VirtioPciBarAperture aperture, uint queueIndex, ushort availableQueueCount, ushort queueNotifyOffset, ulong negotiatedFeatures)
    {
        return VirtioPciQueueNotificationPlanResult.Fail(status, aperture, queueIndex, availableQueueCount, queueNotifyOffset, negotiatedFeatures);
    }
}
