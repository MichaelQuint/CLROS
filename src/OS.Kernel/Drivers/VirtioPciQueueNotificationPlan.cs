namespace OS.Drivers;

public readonly struct VirtioPciQueueNotificationPlan
{
    internal VirtioPciQueueNotificationPlan(VirtioPciBarAperture aperture, ushort queueIndex, ushort queueNotifyOffset, uint notifyOffsetMultiplier, uint relativeOffset)
    {
        Aperture = aperture;
        QueueIndex = queueIndex;
        QueueNotifyOffset = queueNotifyOffset;
        NotifyOffsetMultiplier = notifyOffsetMultiplier;
        RelativeOffset = relativeOffset;
    }

    public VirtioPciBarAperture Aperture
    {
        get;
    }
    public ushort QueueIndex
    {
        get;
    }
    public ushort QueueNotifyOffset
    {
        get;
    }
    public uint NotifyOffsetMultiplier
    {
        get;
    }
    public uint RelativeOffset
    {
        get;
    }
    public ulong BarRelativeOffset => (ulong)Aperture.Offset + RelativeOffset;
    public ulong Address => Aperture.BarBaseAddressBits + BarRelativeOffset;
    public ushort WriteValue => QueueIndex;
    public uint WriteWidthBits => VirtioPciModernRegisterMap.Notification.NoNotificationDataWriteWidthBits;
    public uint WriteWidthBytes => VirtioPciModernRegisterMap.Notification.NoNotificationDataWriteWidthBytes;
    public bool UsesNotificationData => false;
    public bool UsesNotificationConfigData => false;
    public bool PerformsMmio => false;
}
