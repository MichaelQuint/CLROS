using OS.Drivers;

namespace OS.Storage;

public readonly struct VirtioBlockReadOnlyDevicePlanResult
{
    private VirtioBlockReadOnlyDevicePlanResult(
        VirtioBlockReadOnlyDevicePlanStatus status,
        BlockDeviceStatus blockDeviceStatus,
        bool lifecycleInvoked,
        uint sectorSizeBytes,
        ulong deviceSectorCount,
        ulong firstSector,
        uint sectorCount,
        ulong requestedByteCount,
        ushort queueSize,
        VirtioBlockReadLifecyclePublishResult publish)
    {
        IsInitialized = true;
        Status = status;
        BlockDeviceStatus = blockDeviceStatus;
        LifecycleInvoked = lifecycleInvoked;
        SectorSizeBytes = sectorSizeBytes;
        DeviceSectorCount = deviceSectorCount;
        FirstSector = firstSector;
        SectorCount = sectorCount;
        RequestedByteCount = requestedByteCount;
        QueueSize = queueSize;
        Publish = publish;
    }

    public VirtioBlockReadOnlyDevicePlanStatus Status { get; }

    public bool IsInitialized { get; }

    public BlockDeviceStatus BlockDeviceStatus { get; }

    public bool LifecycleInvoked { get; }

    public uint SectorSizeBytes { get; }

    public ulong DeviceSectorCount { get; }

    public ulong FirstSector { get; }

    public uint SectorCount { get; }

    public ulong RequestedByteCount { get; }

    public ushort QueueSize { get; }

    public VirtioBlockReadLifecyclePublishResult Publish { get; }

    public VirtioBlockReadLifecyclePublishStatus LifecyclePublishStatus => Publish.Status;

    public VirtioBlockReadTransactionPlanStatus ReadPlanStatus => Publish.ReadPlanStatus;

    public VirtioBlockRequestPlanStatus RequestPlanStatus => Publish.RequestPlanStatus;

    public VirtqueueSplitRingPlanStatus QueuePlanStatus => Publish.QueuePlanStatus;

    public VirtioBlockDescriptorChainPlanStatus DescriptorPlanStatus => Publish.DescriptorPlanStatus;

    public VirtioBlockDescriptorChainWriterStatus DescriptorWriterStatus => Publish.DescriptorWriterStatus;

    public ulong LastSectorInclusive => Publish.LastSectorInclusive;

    public uint DataByteCount => Publish.DataByteCount;

    public ushort DescriptorHead => Publish.DescriptorHead;

    public ushort PreviousAvailableRingIndex => Publish.PreviousAvailableRingIndex;

    public ushort NextAvailableRingIndex => Publish.NextAvailableRingIndex;

    public ushort PreviousUsedIndex => Publish.PreviousUsedIndex;

    public ushort ExpectedNextUsedIndex => Publish.ExpectedNextUsedIndex;

    public ushort AvailableRingSlot => Publish.AvailableRingSlot;

    public uint AvailableRingEntryOffset => Publish.AvailableRingEntryOffset;

    public ushort DescriptorsWritten => Publish.DescriptorsWritten;

    public bool Succeeded => IsInitialized &&
        Status == VirtioBlockReadOnlyDevicePlanStatus.Success;

    internal static VirtioBlockReadOnlyDevicePlanResult Success(
        BlockDeviceInfo device,
        ulong requestedByteCount,
        VirtioBlockReadLifecyclePublishResult publish)
    {
        return new VirtioBlockReadOnlyDevicePlanResult(
            VirtioBlockReadOnlyDevicePlanStatus.Success,
            BlockDeviceStatus.Success,
            lifecycleInvoked: true,
            device.BlockSize,
            device.BlockCount,
            publish.FirstSector,
            publish.SectorCount,
            requestedByteCount,
            publish.QueueSize,
            publish);
    }

    internal static VirtioBlockReadOnlyDevicePlanResult FailBeforeLifecycle(
        VirtioBlockReadOnlyDevicePlanStatus status,
        BlockDeviceStatus blockDeviceStatus,
        BlockDeviceInfo device,
        ulong firstSector,
        uint sectorCount,
        ulong requestedByteCount,
        ushort queueSize)
    {
        return new VirtioBlockReadOnlyDevicePlanResult(
            status,
            blockDeviceStatus,
            lifecycleInvoked: false,
            device.BlockSize,
            device.BlockCount,
            firstSector,
            sectorCount,
            requestedByteCount,
            queueSize,
            default);
    }

    internal static VirtioBlockReadOnlyDevicePlanResult FailAfterLifecycle(
        BlockDeviceInfo device,
        ulong requestedByteCount,
        VirtioBlockReadLifecyclePublishResult publish)
    {
        return new VirtioBlockReadOnlyDevicePlanResult(
            VirtioBlockReadOnlyDevicePlanStatus.LifecyclePublishFailed,
            BlockDeviceStatus.Success,
            lifecycleInvoked: true,
            device.BlockSize,
            device.BlockCount,
            publish.FirstSector,
            publish.SectorCount,
            requestedByteCount,
            publish.QueueSize,
            publish);
    }
}
