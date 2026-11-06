using OS.Drivers;

namespace OS.Storage;

public readonly struct VirtioBlockQueueNotificationRegisterWindowPlanResult
{
    private VirtioBlockQueueNotificationRegisterWindowPlanResult(
        VirtioBlockQueueNotificationRegisterWindowPlanStatus status,
        VirtioBlockQueueNotifyOffsetPlanStatus notifyOffsetPlanStatus,
        VirtioBlockQueueNotificationMetadataStatus notificationMetadataStatus,
        VirtioPciBarRegisterWindowPlanStatus registerWindowPlanStatus,
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
        VirtioPciBarRegisterWindowAccessPlan accessPlan,
        bool isInitialized)
    {
        Status = status;
        NotifyOffsetPlanStatus = notifyOffsetPlanStatus;
        NotificationMetadataStatus = notificationMetadataStatus;
        RegisterWindowPlanStatus = registerWindowPlanStatus;
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
        AccessPlan = accessPlan;
        IsInitialized = isInitialized;
    }

    public VirtioBlockQueueNotificationRegisterWindowPlanStatus Status
    {
        get;
    }

    public VirtioBlockQueueNotifyOffsetPlanStatus NotifyOffsetPlanStatus
    {
        get;
    }

    public VirtioBlockQueueNotificationMetadataStatus NotificationMetadataStatus
    {
        get;
    }

    public VirtioPciBarRegisterWindowPlanStatus RegisterWindowPlanStatus
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

    public VirtioPciBarRegisterWindowAccessPlan AccessPlan
    {
        get;
    }

    public bool IsInitialized
    {
        get;
    }

    public bool Succeeded =>
        IsInitialized && Status == VirtioBlockQueueNotificationRegisterWindowPlanStatus.Success;

    internal static VirtioBlockQueueNotificationRegisterWindowPlanResult Create(
        VirtioBlockQueueNotificationRegisterWindowPlanStatus status,
        VirtioBlockQueueNotifyOffsetPlanResult notifyOffset,
        VirtioPciBarRegisterWindowPlanStatus registerWindowPlanStatus,
        VirtioPciBarRegisterWindowAccessPlan accessPlan)
    {
        return new VirtioBlockQueueNotificationRegisterWindowPlanResult(
            status,
            notifyOffset.Status,
            notifyOffset.NotificationMetadataStatus,
            registerWindowPlanStatus,
            notifyOffset.QueueIndex,
            notifyOffset.QueueNotifyOffset,
            notifyOffset.NotifyOffsetMultiplier,
            notifyOffset.NotifyRelativeOffset,
            notifyOffset.PublishedDescriptorCount,
            notifyOffset.PreviousAvailableRingIndex,
            notifyOffset.NextAvailableRingIndex,
            notifyOffset.DescriptorHead,
            notifyOffset.WriteValue,
            notifyOffset.WriteWidthBytes,
            accessPlan,
            isInitialized: true);
    }
}
