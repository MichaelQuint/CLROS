using OS.Drivers;

namespace OS.Storage;

public readonly struct VirtioBlockReadLifecycleCompletionResult
{
    private VirtioBlockReadLifecycleCompletionResult(
        VirtioBlockReadLifecycleCompletionStatus status,
        VirtioBlockReadLifecyclePublishStatus publishStatus,
        VirtioBlockReadTransactionPlanStatus readPlanStatus,
        VirtioBlockRequestPlanStatus requestPlanStatus,
        VirtqueueSplitRingPlanStatus queuePlanStatus,
        VirtioBlockDescriptorChainPlanStatus descriptorPlanStatus,
        VirtioBlockDescriptorChainWriterStatus descriptorWriterStatus,
        VirtioBlockUsedRingCompletionReaderStatus completionReaderStatus,
        ulong firstSector,
        uint sectorCount,
        ulong lastSectorInclusive,
        uint dataByteCount,
        ushort descriptorHead,
        ushort previousAvailableRingIndex,
        ushort nextAvailableRingIndex,
        ushort previousUsedIndex,
        ushort currentUsedIndex,
        ushort nextUsedIndex,
        ushort usedRingSlot,
        uint usedRingElementOffset,
        uint completedDescriptorHead,
        uint completedByteCount,
        byte statusByte)
    {
        IsInitialized = true;
        Status = status;
        PublishStatus = publishStatus;
        ReadPlanStatus = readPlanStatus;
        RequestPlanStatus = requestPlanStatus;
        QueuePlanStatus = queuePlanStatus;
        DescriptorPlanStatus = descriptorPlanStatus;
        DescriptorWriterStatus = descriptorWriterStatus;
        CompletionReaderStatus = completionReaderStatus;
        FirstSector = firstSector;
        SectorCount = sectorCount;
        LastSectorInclusive = lastSectorInclusive;
        DataByteCount = dataByteCount;
        DescriptorHead = descriptorHead;
        PreviousAvailableRingIndex = previousAvailableRingIndex;
        NextAvailableRingIndex = nextAvailableRingIndex;
        PreviousUsedIndex = previousUsedIndex;
        CurrentUsedIndex = currentUsedIndex;
        NextUsedIndex = nextUsedIndex;
        UsedRingSlot = usedRingSlot;
        UsedRingElementOffset = usedRingElementOffset;
        CompletedDescriptorHead = completedDescriptorHead;
        CompletedByteCount = completedByteCount;
        StatusByte = statusByte;
    }

    public bool IsInitialized { get; }

    public VirtioBlockReadLifecycleCompletionStatus Status { get; }

    public VirtioBlockReadLifecyclePublishStatus PublishStatus { get; }

    public VirtioBlockReadTransactionPlanStatus ReadPlanStatus { get; }

    public VirtioBlockRequestPlanStatus RequestPlanStatus { get; }

    public VirtqueueSplitRingPlanStatus QueuePlanStatus { get; }

    public VirtioBlockDescriptorChainPlanStatus DescriptorPlanStatus { get; }

    public VirtioBlockDescriptorChainWriterStatus DescriptorWriterStatus { get; }

    public VirtioBlockUsedRingCompletionReaderStatus CompletionReaderStatus { get; }

    public ulong FirstSector { get; }

    public uint SectorCount { get; }

    public ulong LastSectorInclusive { get; }

    public uint DataByteCount { get; }

    public ushort DescriptorHead { get; }

    public ushort PreviousAvailableRingIndex { get; }

    public ushort NextAvailableRingIndex { get; }

    public ushort PreviousUsedIndex { get; }

    public ushort CurrentUsedIndex { get; }

    public ushort NextUsedIndex { get; }

    public ushort UsedRingSlot { get; }

    public uint UsedRingElementOffset { get; }

    public uint CompletedDescriptorHead { get; }

    public uint CompletedByteCount { get; }

    public byte StatusByte { get; }

    public bool Succeeded => IsInitialized &&
        Status == VirtioBlockReadLifecycleCompletionStatus.Success;

    internal static VirtioBlockReadLifecycleCompletionResult Success(
        VirtioBlockReadLifecyclePublishResult publish,
        VirtioBlockUsedRingCompletionReaderResult completion)
    {
        return Create(
            VirtioBlockReadLifecycleCompletionStatus.Success,
            publish,
            completion);
    }

    internal static VirtioBlockReadLifecycleCompletionResult Fail(
        VirtioBlockReadLifecycleCompletionStatus status,
        VirtioBlockReadLifecyclePublishResult publish,
        VirtioBlockUsedRingCompletionReaderResult completion)
    {
        return Create(status, publish, completion);
    }

    private static VirtioBlockReadLifecycleCompletionResult Create(
        VirtioBlockReadLifecycleCompletionStatus status,
        VirtioBlockReadLifecyclePublishResult publish,
        VirtioBlockUsedRingCompletionReaderResult completion)
    {
        return new VirtioBlockReadLifecycleCompletionResult(
            status,
            publish.Status,
            publish.ReadPlanStatus,
            publish.RequestPlanStatus,
            publish.QueuePlanStatus,
            publish.DescriptorPlanStatus,
            publish.DescriptorWriterStatus,
            completion.Status,
            publish.FirstSector,
            publish.SectorCount,
            publish.LastSectorInclusive,
            publish.DataByteCount,
            publish.DescriptorHead,
            publish.PreviousAvailableRingIndex,
            publish.NextAvailableRingIndex,
            completion.PreviousUsedIndex,
            completion.CurrentUsedIndex,
            completion.NextUsedIndex,
            completion.UsedRingSlot,
            completion.UsedRingElementOffset,
            completion.DescriptorHead,
            completion.UsedByteCount,
            completion.StatusByte);
    }
}
