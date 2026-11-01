namespace OS.Drivers;
public readonly struct VirtioNetQueueNotificationPlan
{
    internal VirtioNetQueueNotificationPlan(VirtioNetQueueNotificationKind kind, ushort publishedDescriptorCount, ushort previousAvailableRingIndex, ushort nextAvailableRingIndex, VirtioPciQueueNotificationPlan notificationPlan)
    {
        Kind = kind;
        PublishedDescriptorCount = publishedDescriptorCount;
        PreviousAvailableRingIndex = previousAvailableRingIndex;
        NextAvailableRingIndex = nextAvailableRingIndex;
        NotificationPlan = notificationPlan;
    }

    public VirtioNetQueueNotificationKind Kind { get; }
    public ushort PublishedDescriptorCount { get; }
    public ushort PreviousAvailableRingIndex { get; }
    public ushort NextAvailableRingIndex { get; }
    public VirtioPciQueueNotificationPlan NotificationPlan { get; }
    public ushort QueueIndex => NotificationPlan.QueueIndex;
    public ushort QueueNotifyOffset => NotificationPlan.QueueNotifyOffset;
    public uint NotifyRelativeOffset => NotificationPlan.RelativeOffset;
    public ulong NotifyAddress => NotificationPlan.Address;
    public ushort WriteValue => NotificationPlan.WriteValue;
    public uint WriteWidthBytes => NotificationPlan.WriteWidthBytes;
    public bool PerformsMmio => false;
}
