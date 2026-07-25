using OS.Drivers;

namespace OS.Storage;

public enum VirtioBlockReadLifecyclePublishStatus
{
    Success = 0,
    ReadPlanFailed = 1,
    QueuePlanFailed = 2,
    DescriptorPlanFailed = 3,
    DescriptorWriteFailed = 4
}

public enum VirtioBlockReadLifecycleCompletionStatus
{
    Success = 0,
    PublishFailed = 1,
    CompletionFailed = 2
}

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

public static class VirtioBlockReadLifecycle
{
    public const bool PerformsDmaMapping = false;
    public const bool PerformsMmio = false;
    public const bool NotifiesDevice = false;
    public const bool PollsDevice = false;
    public const bool HandlesInterrupts = false;
    public const bool CopiesPayload = false;
    public const bool PerformsDiskRead = false;

    public static VirtioBlockReadLifecyclePublishResult PublishReadRequest(
        uint sectorSizeBytes,
        ulong deviceSectorCount,
        ulong firstSector,
        uint sectorCount,
        ushort queueSize,
        ushort firstDescriptorIndex,
        ushort availableDescriptorCount,
        ushort previousAvailableRingIndex,
        ushort previousUsedIndex,
        ulong requestHeaderPhysicalAddress,
        ulong dataBufferPhysicalAddress,
        ulong statusBytePhysicalAddress,
        Span<byte> requestHeader,
        Span<byte> destination,
        Span<byte> statusBuffer,
        Span<byte> queueMemory)
    {
        VirtioBlockReadTransactionPlanResult transactionPlan =
            VirtioBlockReadTransactionPlan.Create(
                sectorSizeBytes,
                deviceSectorCount,
                firstSector,
                sectorCount,
                queueSize,
                requestHeader,
                destination,
                statusBuffer);
        if (!transactionPlan.Succeeded)
        {
            return VirtioBlockReadLifecyclePublishResult.Fail(
                VirtioBlockReadLifecyclePublishStatus.ReadPlanFailed,
                transactionPlan.Status,
                transactionPlan.RequestPlanStatus,
                VirtqueueSplitRingPlanStatus.Success,
                VirtioBlockDescriptorChainPlanStatus.Success,
                VirtioBlockDescriptorChainWriterStatus.Success,
                default,
                firstSector,
                sectorCount,
                queueSize,
                previousAvailableRingIndex,
                previousUsedIndex);
        }

        VirtqueueSplitRingPlanResult queuePlan = VirtqueueSplitRingPlan.Create(queueSize);
        if (!queuePlan.Succeeded)
        {
            return VirtioBlockReadLifecyclePublishResult.Fail(
                VirtioBlockReadLifecyclePublishStatus.QueuePlanFailed,
                transactionPlan.Status,
                transactionPlan.RequestPlanStatus,
                queuePlan.Status,
                VirtioBlockDescriptorChainPlanStatus.Success,
                VirtioBlockDescriptorChainWriterStatus.Success,
                default,
                firstSector,
                sectorCount,
                queueSize,
                previousAvailableRingIndex,
                previousUsedIndex);
        }

        VirtioBlockDescriptorChainPlanResult descriptorPlan =
            VirtioBlockDescriptorChainPlan.Create(
                transactionPlan,
                queuePlan,
                firstDescriptorIndex,
                availableDescriptorCount,
                previousAvailableRingIndex,
                requestHeaderPhysicalAddress,
                dataBufferPhysicalAddress,
                statusBytePhysicalAddress);
        if (!descriptorPlan.Succeeded)
        {
            return VirtioBlockReadLifecyclePublishResult.Fail(
                VirtioBlockReadLifecyclePublishStatus.DescriptorPlanFailed,
                descriptorPlan.TransactionPlanStatus,
                descriptorPlan.RequestPlanStatus,
                descriptorPlan.QueuePlanStatus,
                descriptorPlan.Status,
                VirtioBlockDescriptorChainWriterStatus.Success,
                queuePlan.Plan,
                firstSector,
                sectorCount,
                queueSize,
                previousAvailableRingIndex,
                previousUsedIndex);
        }

        VirtioBlockDescriptorChainWriterResult writer =
            VirtioBlockDescriptorChainWriter.Write(
                descriptorPlan,
                queuePlan,
                queueMemory);
        if (!writer.Succeeded)
        {
            return VirtioBlockReadLifecyclePublishResult.Fail(
                VirtioBlockReadLifecyclePublishStatus.DescriptorWriteFailed,
                descriptorPlan.TransactionPlanStatus,
                descriptorPlan.RequestPlanStatus,
                writer.QueuePlanStatus,
                writer.DescriptorPlanStatus,
                writer.Status,
                queuePlan.Plan,
                firstSector,
                sectorCount,
                queueSize,
                previousAvailableRingIndex,
                previousUsedIndex);
        }

        return VirtioBlockReadLifecyclePublishResult.Success(
            transactionPlan.Plan,
            descriptorPlan.Plan,
            writer,
            previousUsedIndex);
    }

    public static VirtioBlockReadLifecycleCompletionResult CompleteReadRequest(
        VirtioBlockReadLifecyclePublishResult publish,
        ReadOnlySpan<byte> queueMemory,
        ReadOnlySpan<byte> statusBuffer)
    {
        if (!publish.Succeeded)
        {
            VirtioBlockUsedRingCompletionReaderResult notRead =
                VirtioBlockUsedRingCompletionReaderResult.Fail(
                    VirtioBlockUsedRingCompletionReaderStatus.NoCompletion,
                    publish.QueuePlanStatus,
                    publish.PreviousUsedIndex);
            return VirtioBlockReadLifecycleCompletionResult.Fail(
                VirtioBlockReadLifecycleCompletionStatus.PublishFailed,
                publish,
                notRead);
        }

        ReadOnlySpan<byte> usedRing = GetUsedRingSlice(
            publish.QueuePlan,
            queueMemory);
        VirtioBlockUsedRingCompletionReaderResult completion =
            VirtioBlockUsedRingCompletionReader.Read(
                publish.QueueSize,
                publish.PreviousUsedIndex,
                publish.DescriptorHead,
                publish.DataByteCount,
                statusBuffer,
                usedRing);

        if (!completion.Succeeded)
        {
            return VirtioBlockReadLifecycleCompletionResult.Fail(
                VirtioBlockReadLifecycleCompletionStatus.CompletionFailed,
                publish,
                completion);
        }

        return VirtioBlockReadLifecycleCompletionResult.Success(
            publish,
            completion);
    }

    private static ReadOnlySpan<byte> GetUsedRingSlice(
        VirtqueueSplitRingPlan queuePlan,
        ReadOnlySpan<byte> queueMemory)
    {
        uint usedRingOffset = queuePlan.UsedRing.Offset;
        if (usedRingOffset > int.MaxValue ||
            usedRingOffset > (uint)queueMemory.Length)
        {
            return ReadOnlySpan<byte>.Empty;
        }

        return queueMemory.Slice((int)usedRingOffset);
    }
}
