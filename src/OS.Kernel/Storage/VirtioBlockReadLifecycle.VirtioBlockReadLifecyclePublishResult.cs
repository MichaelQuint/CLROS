using OS.Drivers;

namespace OS.Storage;

public readonly struct VirtioBlockReadLifecyclePublishResult
{
    private VirtioBlockReadLifecyclePublishResult(
        VirtioBlockReadLifecyclePublishStatus status,
        VirtioBlockReadTransactionPlanStatus readPlanStatus,
        VirtioBlockRequestPlanStatus requestPlanStatus,
        VirtqueueSplitRingPlanStatus queuePlanStatus,
        VirtioBlockDescriptorChainPlanStatus descriptorPlanStatus,
        VirtioBlockDescriptorChainWriterStatus descriptorWriterStatus,
        VirtqueueSplitRingPlan queuePlan,
        ulong firstSector,
        uint sectorCount,
        ulong lastSectorInclusive,
        uint dataByteCount,
        ushort queueSize,
        ushort descriptorHead,
        ushort previousAvailableRingIndex,
        ushort nextAvailableRingIndex,
        ushort previousUsedIndex,
        ushort expectedNextUsedIndex,
        ushort availableRingSlot,
        uint availableRingEntryOffset,
        ushort descriptorsWritten)
    {
        IsInitialized = true;
        Status = status;
        ReadPlanStatus = readPlanStatus;
        RequestPlanStatus = requestPlanStatus;
        QueuePlanStatus = queuePlanStatus;
        DescriptorPlanStatus = descriptorPlanStatus;
        DescriptorWriterStatus = descriptorWriterStatus;
        QueuePlan = queuePlan;
        FirstSector = firstSector;
        SectorCount = sectorCount;
        LastSectorInclusive = lastSectorInclusive;
        DataByteCount = dataByteCount;
        QueueSize = queueSize;
        DescriptorHead = descriptorHead;
        PreviousAvailableRingIndex = previousAvailableRingIndex;
        NextAvailableRingIndex = nextAvailableRingIndex;
        PreviousUsedIndex = previousUsedIndex;
        ExpectedNextUsedIndex = expectedNextUsedIndex;
        AvailableRingSlot = availableRingSlot;
        AvailableRingEntryOffset = availableRingEntryOffset;
        DescriptorsWritten = descriptorsWritten;
    }

    public bool IsInitialized { get; }

    public VirtioBlockReadLifecyclePublishStatus Status { get; }

    public VirtioBlockReadTransactionPlanStatus ReadPlanStatus { get; }

    public VirtioBlockRequestPlanStatus RequestPlanStatus { get; }

    public VirtqueueSplitRingPlanStatus QueuePlanStatus { get; }

    public VirtioBlockDescriptorChainPlanStatus DescriptorPlanStatus { get; }

    public VirtioBlockDescriptorChainWriterStatus DescriptorWriterStatus { get; }

    public VirtqueueSplitRingPlan QueuePlan { get; }

    public ulong FirstSector { get; }

    public uint SectorCount { get; }

    public ulong LastSectorInclusive { get; }

    public uint DataByteCount { get; }

    public ushort QueueSize { get; }

    public ushort DescriptorHead { get; }

    public ushort PreviousAvailableRingIndex { get; }

    public ushort NextAvailableRingIndex { get; }

    public ushort PreviousUsedIndex { get; }

    public ushort ExpectedNextUsedIndex { get; }

    public ushort AvailableRingSlot { get; }

    public uint AvailableRingEntryOffset { get; }

    public ushort DescriptorsWritten { get; }

    public bool Succeeded => IsInitialized &&
        Status == VirtioBlockReadLifecyclePublishStatus.Success;

    internal static VirtioBlockReadLifecyclePublishResult Success(
        VirtioBlockReadTransactionPlan transactionPlan,
        VirtioBlockDescriptorChainPlan descriptorPlan,
        VirtioBlockDescriptorChainWriterResult writer,
        ushort previousUsedIndex)
    {
        return new VirtioBlockReadLifecyclePublishResult(
            VirtioBlockReadLifecyclePublishStatus.Success,
            VirtioBlockReadTransactionPlanStatus.Success,
            VirtioBlockRequestPlanStatus.Success,
            VirtqueueSplitRingPlanStatus.Success,
            VirtioBlockDescriptorChainPlanStatus.Success,
            VirtioBlockDescriptorChainWriterStatus.Success,
            descriptorPlan.QueuePlan,
            transactionPlan.FirstSector,
            transactionPlan.SectorCount,
            transactionPlan.LastSectorInclusive,
            transactionPlan.DataByteCount,
            transactionPlan.QueueSize,
            writer.HeadDescriptorIndex,
            descriptorPlan.AvailableRingIndex,
            writer.NextAvailableRingIndex,
            previousUsedIndex,
            unchecked((ushort)(previousUsedIndex + 1)),
            descriptorPlan.AvailableRingSlot,
            descriptorPlan.AvailableRingEntryOffset,
            writer.DescriptorsWritten);
    }

    internal static VirtioBlockReadLifecyclePublishResult Fail(
        VirtioBlockReadLifecyclePublishStatus status,
        VirtioBlockReadTransactionPlanStatus readPlanStatus,
        VirtioBlockRequestPlanStatus requestPlanStatus,
        VirtqueueSplitRingPlanStatus queuePlanStatus,
        VirtioBlockDescriptorChainPlanStatus descriptorPlanStatus,
        VirtioBlockDescriptorChainWriterStatus descriptorWriterStatus,
        VirtqueueSplitRingPlan queuePlan,
        ulong firstSector,
        uint sectorCount,
        ushort queueSize,
        ushort previousAvailableRingIndex,
        ushort previousUsedIndex)
    {
        return new VirtioBlockReadLifecyclePublishResult(
            status,
            readPlanStatus,
            requestPlanStatus,
            queuePlanStatus,
            descriptorPlanStatus,
            descriptorWriterStatus,
            queuePlan,
            firstSector,
            sectorCount,
            lastSectorInclusive: 0,
            dataByteCount: 0,
            queueSize,
            descriptorHead: 0,
            previousAvailableRingIndex,
            nextAvailableRingIndex: previousAvailableRingIndex,
            previousUsedIndex,
            expectedNextUsedIndex: previousUsedIndex,
            availableRingSlot: 0,
            availableRingEntryOffset: 0,
            descriptorsWritten: 0);
    }
}
