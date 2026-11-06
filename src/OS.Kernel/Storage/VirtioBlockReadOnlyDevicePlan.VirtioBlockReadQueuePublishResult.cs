using OS.Drivers;

namespace OS.Storage;

public readonly struct VirtioBlockReadQueuePublishResult
{
    private VirtioBlockReadQueuePublishResult(
        VirtioBlockReadQueuePublishStatus status,
        BlockDeviceStatus blockDeviceStatus,
        VirtioBlockQueueCursorPublishStatus cursorPublishStatus,
        VirtioBlockDescriptorPublishPlanStatus descriptorPublishStatus,
        VirtioBlockDescriptorFreeListStatus freeListStatus,
        VirtioBlockReadLifecyclePublishStatus lifecyclePublishStatus,
        VirtioBlockDescriptorOwnershipStatus ownershipStatus,
        bool cursorPublishAttempted,
        uint sectorSizeBytes,
        ulong deviceSectorCount,
        ulong firstSector,
        uint sectorCount,
        ulong requestedByteCount,
        ushort queueSize,
        ushort previousAvailableRingIndex,
        ushort currentAvailableRingIndex,
        ushort previousUsedRingIndex,
        ushort currentUsedRingIndex,
        VirtioBlockQueueCursorPublishResult cursorPublish,
        bool isInitialized)
    {
        Status = status;
        BlockDeviceStatus = blockDeviceStatus;
        CursorPublishStatus = cursorPublishStatus;
        DescriptorPublishStatus = descriptorPublishStatus;
        FreeListStatus = freeListStatus;
        LifecyclePublishStatus = lifecyclePublishStatus;
        OwnershipStatus = ownershipStatus;
        CursorPublishAttempted = cursorPublishAttempted;
        SectorSizeBytes = sectorSizeBytes;
        DeviceSectorCount = deviceSectorCount;
        FirstSector = firstSector;
        SectorCount = sectorCount;
        RequestedByteCount = requestedByteCount;
        QueueSize = queueSize;
        PreviousAvailableRingIndex = previousAvailableRingIndex;
        CurrentAvailableRingIndex = currentAvailableRingIndex;
        PreviousUsedRingIndex = previousUsedRingIndex;
        CurrentUsedRingIndex = currentUsedRingIndex;
        CursorPublish = cursorPublish;
        IsInitialized = isInitialized;
    }

    public VirtioBlockReadQueuePublishStatus Status
    {
        get;
    }

    public BlockDeviceStatus BlockDeviceStatus
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

    public VirtioBlockDescriptorFreeListStatus FreeListStatus
    {
        get;
    }

    public VirtioBlockReadLifecyclePublishStatus LifecyclePublishStatus
    {
        get;
    }

    public VirtioBlockDescriptorOwnershipStatus OwnershipStatus
    {
        get;
    }

    public bool CursorPublishAttempted
    {
        get;
    }

    public uint SectorSizeBytes
    {
        get;
    }

    public ulong DeviceSectorCount
    {
        get;
    }

    public ulong FirstSector
    {
        get;
    }

    public uint SectorCount
    {
        get;
    }

    public ulong RequestedByteCount
    {
        get;
    }

    public ushort QueueSize
    {
        get;
    }

    public ushort PreviousAvailableRingIndex
    {
        get;
    }

    public ushort CurrentAvailableRingIndex
    {
        get;
    }

    public ushort PreviousUsedRingIndex
    {
        get;
    }

    public ushort CurrentUsedRingIndex
    {
        get;
    }

    public VirtioBlockQueueCursorPublishResult CursorPublish
    {
        get;
    }

    public bool IsInitialized
    {
        get;
    }

    public bool Succeeded => IsInitialized && Status == VirtioBlockReadQueuePublishStatus.Success;

    internal static VirtioBlockReadQueuePublishResult FailBeforeCursorPublish(
        VirtioBlockReadQueuePublishStatus status,
        BlockDeviceStatus blockDeviceStatus,
        BlockDeviceInfo device,
        ulong firstSector,
        uint sectorCount,
        ulong requestedByteCount,
        ushort queueSize,
        VirtioBlockQueueCursorState cursor)
    {
        return new VirtioBlockReadQueuePublishResult(
            status,
            blockDeviceStatus,
            VirtioBlockQueueCursorPublishStatus.Success,
            VirtioBlockDescriptorPublishPlanStatus.Success,
            VirtioBlockDescriptorFreeListStatus.Success,
            VirtioBlockReadLifecyclePublishStatus.Success,
            VirtioBlockDescriptorOwnershipStatus.Success,
            cursorPublishAttempted: false,
            device.BlockSize,
            device.BlockCount,
            firstSector,
            sectorCount,
            requestedByteCount,
            queueSize,
            cursor.AvailableRingIndex,
            cursor.AvailableRingIndex,
            cursor.UsedRingIndex,
            cursor.UsedRingIndex,
            default,
            isInitialized: true);
    }

    internal static VirtioBlockReadQueuePublishResult Create(
        VirtioBlockReadQueuePublishStatus status,
        BlockDeviceInfo device,
        ulong requestedByteCount,
        VirtioBlockQueueCursorPublishResult cursorPublish)
    {
        return new VirtioBlockReadQueuePublishResult(
            status,
            BlockDeviceStatus.Success,
            cursorPublish.Status,
            cursorPublish.PublishPlanStatus,
            cursorPublish.FreeListStatus,
            cursorPublish.LifecyclePublishStatus,
            cursorPublish.OwnershipStatus,
            cursorPublishAttempted: true,
            device.BlockSize,
            device.BlockCount,
            cursorPublish.Publish.LifecyclePublish.FirstSector,
            cursorPublish.Publish.LifecyclePublish.SectorCount,
            requestedByteCount,
            cursorPublish.Publish.LifecyclePublish.QueueSize,
            cursorPublish.PreviousAvailableRingIndex,
            cursorPublish.CurrentAvailableRingIndex,
            cursorPublish.PreviousUsedRingIndex,
            cursorPublish.CurrentUsedRingIndex,
            cursorPublish,
            isInitialized: true);
    }
}
