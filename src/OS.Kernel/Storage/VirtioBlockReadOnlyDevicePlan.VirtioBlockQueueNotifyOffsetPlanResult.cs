using OS.Drivers;

namespace OS.Storage;

public readonly struct VirtioBlockQueueNotifyOffsetPlanResult
{
    private VirtioBlockQueueNotifyOffsetPlanResult(
        VirtioBlockQueueNotifyOffsetPlanStatus status,
        VirtioBlockQueueNotificationMetadataStatus notificationMetadataStatus,
        ushort queueIndex,
        ushort queueNotifyOffset,
        uint notifyOffsetMultiplier,
        uint notifyRelativeOffset,
        ushort publishedDescriptorCount,
        ushort previousAvailableRingIndex,
        ushort nextAvailableRingIndex,
        ushort descriptorHead,
        ushort writeValue,
        uint writeWidthBytes,
        bool isInitialized)
    {
        Status = status;
        NotificationMetadataStatus = notificationMetadataStatus;
        QueueIndex = queueIndex;
        QueueNotifyOffset = queueNotifyOffset;
        NotifyOffsetMultiplier = notifyOffsetMultiplier;
        NotifyRelativeOffset = notifyRelativeOffset;
        PublishedDescriptorCount = publishedDescriptorCount;
        PreviousAvailableRingIndex = previousAvailableRingIndex;
        NextAvailableRingIndex = nextAvailableRingIndex;
        DescriptorHead = descriptorHead;
        WriteValue = writeValue;
        WriteWidthBytes = writeWidthBytes;
        IsInitialized = isInitialized;
    }

    public VirtioBlockQueueNotifyOffsetPlanStatus Status
    {
        get;
    }

    public VirtioBlockQueueNotificationMetadataStatus NotificationMetadataStatus
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

    public uint NotifyRelativeOffset
    {
        get;
    }

    public ushort PublishedDescriptorCount
    {
        get;
    }

    public ushort PreviousAvailableRingIndex
    {
        get;
    }

    public ushort NextAvailableRingIndex
    {
        get;
    }

    public ushort DescriptorHead
    {
        get;
    }

    public ushort WriteValue
    {
        get;
    }

    public uint WriteWidthBytes
    {
        get;
    }

    public bool IsInitialized
    {
        get;
    }

    public bool Succeeded => IsInitialized && Status == VirtioBlockQueueNotifyOffsetPlanStatus.Success;

    internal static VirtioBlockQueueNotifyOffsetPlanResult Create(
        VirtioBlockQueueNotifyOffsetPlanStatus status,
        VirtioBlockQueueNotificationMetadataResult notification,
        uint notifyOffsetMultiplier,
        uint notifyRelativeOffset)
    {
        return new VirtioBlockQueueNotifyOffsetPlanResult(
            status,
            notification.Status,
            notification.QueueIndex,
            notification.QueueNotifyOffset,
            notifyOffsetMultiplier,
            notifyRelativeOffset,
            notification.PublishedDescriptorCount,
            notification.PreviousAvailableRingIndex,
            notification.NextAvailableRingIndex,
            notification.DescriptorHead,
            notification.WriteValue,
            notification.WriteWidthBytes,
            isInitialized: true);
    }
}
