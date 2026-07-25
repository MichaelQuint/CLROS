namespace OS.Drivers;

public enum VirtioPciQueueNotificationPlanStatus
{
    Success = 0,
    InvalidNotifyAperture = 1,
    InvalidQueueIndex = 2,
    UnsupportedNotificationDataFeature = 3,
    UnsupportedNotificationConfigDataFeature = 4,
    QueueNotEnabled = 5,
    NoNewlyPublishedDescriptors = 6,
    NotifyOffsetOverflow = 7,
    NotifyRangeOutsideAperture = 8
}

public static class VirtioPciQueueNotificationFeatureBits
{
    public const int NotificationDataBit = 38;
    public const int NotificationConfigDataBit = 39;
    public const ulong NotificationData = 1UL << NotificationDataBit;
    public const ulong NotificationConfigData = 1UL << NotificationConfigDataBit;
}

public readonly struct VirtioPciQueueNotificationPlan
{
    internal VirtioPciQueueNotificationPlan(
        VirtioPciBarAperture aperture,
        ushort queueIndex,
        ushort queueNotifyOffset,
        uint notifyOffsetMultiplier,
        uint relativeOffset)
    {
        Aperture = aperture;
        QueueIndex = queueIndex;
        QueueNotifyOffset = queueNotifyOffset;
        NotifyOffsetMultiplier = notifyOffsetMultiplier;
        RelativeOffset = relativeOffset;
    }

    public VirtioPciBarAperture Aperture { get; }

    public ushort QueueIndex { get; }

    public ushort QueueNotifyOffset { get; }

    public uint NotifyOffsetMultiplier { get; }

    public uint RelativeOffset { get; }

    public ulong BarRelativeOffset => (ulong)Aperture.Offset + RelativeOffset;

    public ulong Address => Aperture.BarBaseAddressBits + BarRelativeOffset;

    public ushort WriteValue => QueueIndex;

    public uint WriteWidthBits => VirtioPciModernRegisterMap.Notification.NoNotificationDataWriteWidthBits;

    public uint WriteWidthBytes => VirtioPciModernRegisterMap.Notification.NoNotificationDataWriteWidthBytes;

    public bool UsesNotificationData => false;

    public bool UsesNotificationConfigData => false;

    public bool PerformsMmio => false;
}

public readonly struct VirtioPciQueueNotificationPlanResult
{
    private VirtioPciQueueNotificationPlanResult(
        VirtioPciQueueNotificationPlanStatus status,
        VirtioPciBarAperture aperture,
        uint queueIndex,
        ushort availableQueueCount,
        ushort queueNotifyOffset,
        ulong negotiatedFeatures,
        VirtioPciQueueNotificationPlan plan,
        bool isInitialized)
    {
        Status = status;
        Aperture = aperture;
        QueueIndex = queueIndex;
        AvailableQueueCount = availableQueueCount;
        QueueNotifyOffset = queueNotifyOffset;
        NegotiatedFeatures = negotiatedFeatures;
        Plan = plan;
        IsInitialized = isInitialized;
    }

    public VirtioPciQueueNotificationPlanStatus Status { get; }

    public bool IsInitialized { get; }

    public VirtioPciBarAperture Aperture { get; }

    public uint QueueIndex { get; }

    public ushort AvailableQueueCount { get; }

    public ushort QueueNotifyOffset { get; }

    public ulong NegotiatedFeatures { get; }

    public VirtioPciQueueNotificationPlan Plan { get; }

    public bool Succeeded => IsInitialized && Status == VirtioPciQueueNotificationPlanStatus.Success;

    internal static VirtioPciQueueNotificationPlanResult Success(
        VirtioPciQueueNotificationPlan plan,
        ushort availableQueueCount,
        ulong negotiatedFeatures)
    {
        return new VirtioPciQueueNotificationPlanResult(
            VirtioPciQueueNotificationPlanStatus.Success,
            plan.Aperture,
            plan.QueueIndex,
            availableQueueCount,
            plan.QueueNotifyOffset,
            negotiatedFeatures,
            plan,
            isInitialized: true);
    }

    internal static VirtioPciQueueNotificationPlanResult Fail(
        VirtioPciQueueNotificationPlanStatus status,
        VirtioPciBarAperture aperture,
        uint queueIndex,
        ushort availableQueueCount,
        ushort queueNotifyOffset,
        ulong negotiatedFeatures)
    {
        return new VirtioPciQueueNotificationPlanResult(
            status,
            aperture,
            queueIndex,
            availableQueueCount,
            queueNotifyOffset,
            negotiatedFeatures,
            default,
            isInitialized: true);
    }
}

public static class VirtioPciQueueNotificationPlanner
{
    public const bool PerformsMmio = false;
    public const bool PublishesDescriptors = false;
    public const bool HandlesInterrupts = false;
    public const bool PerformsPacketIo = false;
    public const bool SupportsNotificationData = false;
    public const bool SupportsNotificationConfigData = false;

    public static VirtioPciQueueNotificationPlanResult Create(
        VirtioPciBarAperture notifyAperture,
        uint queueIndex,
        ushort availableQueueCount,
        ushort queueNotifyOffset,
        ulong negotiatedFeatures,
        bool queueEnabled,
        bool hasNewlyPublishedDescriptors)
    {
        if (notifyAperture.CapabilityType != VirtioPciCapabilityType.NotifyConfiguration)
        {
            return Fail(
                VirtioPciQueueNotificationPlanStatus.InvalidNotifyAperture,
                notifyAperture,
                queueIndex,
                availableQueueCount,
                queueNotifyOffset,
                negotiatedFeatures);
        }

        if (queueIndex > ushort.MaxValue ||
            availableQueueCount == 0 ||
            queueIndex >= availableQueueCount)
        {
            return Fail(
                VirtioPciQueueNotificationPlanStatus.InvalidQueueIndex,
                notifyAperture,
                queueIndex,
                availableQueueCount,
                queueNotifyOffset,
                negotiatedFeatures);
        }

        if ((negotiatedFeatures & VirtioPciQueueNotificationFeatureBits.NotificationData) != 0)
        {
            return Fail(
                VirtioPciQueueNotificationPlanStatus.UnsupportedNotificationDataFeature,
                notifyAperture,
                queueIndex,
                availableQueueCount,
                queueNotifyOffset,
                negotiatedFeatures);
        }

        if ((negotiatedFeatures & VirtioPciQueueNotificationFeatureBits.NotificationConfigData) != 0)
        {
            return Fail(
                VirtioPciQueueNotificationPlanStatus.UnsupportedNotificationConfigDataFeature,
                notifyAperture,
                queueIndex,
                availableQueueCount,
                queueNotifyOffset,
                negotiatedFeatures);
        }

        if (!queueEnabled)
        {
            return Fail(
                VirtioPciQueueNotificationPlanStatus.QueueNotEnabled,
                notifyAperture,
                queueIndex,
                availableQueueCount,
                queueNotifyOffset,
                negotiatedFeatures);
        }

        if (!hasNewlyPublishedDescriptors)
        {
            return Fail(
                VirtioPciQueueNotificationPlanStatus.NoNewlyPublishedDescriptors,
                notifyAperture,
                queueIndex,
                availableQueueCount,
                queueNotifyOffset,
                negotiatedFeatures);
        }

        ulong relativeOffset = (ulong)queueNotifyOffset * notifyAperture.NotifyOffsetMultiplier;
        if (relativeOffset > uint.MaxValue)
        {
            return Fail(
                VirtioPciQueueNotificationPlanStatus.NotifyOffsetOverflow,
                notifyAperture,
                queueIndex,
                availableQueueCount,
                queueNotifyOffset,
                negotiatedFeatures);
        }

        if (!IsNotifyRangeInsideAperture((uint)relativeOffset, notifyAperture.Length))
        {
            return Fail(
                VirtioPciQueueNotificationPlanStatus.NotifyRangeOutsideAperture,
                notifyAperture,
                queueIndex,
                availableQueueCount,
                queueNotifyOffset,
                negotiatedFeatures);
        }

        VirtioPciQueueNotificationPlan plan =
            new VirtioPciQueueNotificationPlan(
                notifyAperture,
                (ushort)queueIndex,
                queueNotifyOffset,
                notifyAperture.NotifyOffsetMultiplier,
                (uint)relativeOffset);

        return VirtioPciQueueNotificationPlanResult.Success(
            plan,
            availableQueueCount,
            negotiatedFeatures);
    }

    private static bool IsNotifyRangeInsideAperture(uint relativeOffset, uint apertureLength)
    {
        ulong end = (ulong)relativeOffset +
            VirtioPciModernRegisterMap.Notification.NoNotificationDataWriteWidthBytes;
        return end <= apertureLength;
    }

    private static VirtioPciQueueNotificationPlanResult Fail(
        VirtioPciQueueNotificationPlanStatus status,
        VirtioPciBarAperture aperture,
        uint queueIndex,
        ushort availableQueueCount,
        ushort queueNotifyOffset,
        ulong negotiatedFeatures)
    {
        return VirtioPciQueueNotificationPlanResult.Fail(
            status,
            aperture,
            queueIndex,
            availableQueueCount,
            queueNotifyOffset,
            negotiatedFeatures);
    }
}
