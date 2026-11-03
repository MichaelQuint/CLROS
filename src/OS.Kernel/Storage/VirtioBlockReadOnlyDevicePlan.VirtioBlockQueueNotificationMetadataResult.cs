using OS.Drivers;

namespace OS.Storage;

public readonly struct VirtioBlockQueueNotificationMetadataResult
{
    private VirtioBlockQueueNotificationMetadataResult(
        VirtioBlockQueueNotificationMetadataStatus status,
        VirtioBlockReadQueuePublishStatus readQueuePublishStatus,
        VirtioBlockQueueCursorPublishStatus cursorPublishStatus,
        VirtioBlockDescriptorPublishPlanStatus descriptorPublishStatus,
        ushort queueIndex,
        ushort queueNotifyOffset,
        ushort publishedDescriptorCount,
        ushort previousAvailableRingIndex,
        ushort nextAvailableRingIndex,
        ushort descriptorHead,
        ushort writeValue,
        uint writeWidthBytes,
        bool isInitialized)
    {
        Status = status;
        ReadQueuePublishStatus = readQueuePublishStatus;
        CursorPublishStatus = cursorPublishStatus;
        DescriptorPublishStatus = descriptorPublishStatus;
        QueueIndex = queueIndex;
        QueueNotifyOffset = queueNotifyOffset;
        PublishedDescriptorCount = publishedDescriptorCount;
        PreviousAvailableRingIndex = previousAvailableRingIndex;
        NextAvailableRingIndex = nextAvailableRingIndex;
        DescriptorHead = descriptorHead;
        WriteValue = writeValue;
        WriteWidthBytes = writeWidthBytes;
        IsInitialized = isInitialized;
    }

    public VirtioBlockQueueNotificationMetadataStatus Status
    {
        get;
    }

    public VirtioBlockReadQueuePublishStatus ReadQueuePublishStatus
    {
        get;
    }

    public VirtioBlockQueueCursorPublishStatus CursorPublishStatus
    {
        get;
    }

    public VirtioBlockDescriptorPublishPlanStatus DescriptorPublishStatus
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

    public bool Succeeded => IsInitialized && Status == VirtioBlockQueueNotificationMetadataStatus.Success;

    internal static VirtioBlockQueueNotificationMetadataResult Create(
        VirtioBlockQueueNotificationMetadataStatus status,
        VirtioBlockReadQueuePublishResult publish,
        ushort queueIndex,
        ushort queueNotifyOffset)
    {
        bool succeeded = status == VirtioBlockQueueNotificationMetadataStatus.Success;
        return new VirtioBlockQueueNotificationMetadataResult(
            status,
            publish.Status,
            publish.CursorPublishStatus,
            publish.DescriptorPublishStatus,
            queueIndex,
            queueNotifyOffset,
            succeeded ? publish.CursorPublish.Publish.DescriptorsWritten : (ushort)0,
            publish.PreviousAvailableRingIndex,
            publish.CurrentAvailableRingIndex,
            succeeded ? publish.CursorPublish.Publish.FirstDescriptorIndex : (ushort)0,
            queueIndex,
            sizeof(ushort),
            isInitialized: true);
    }
}
