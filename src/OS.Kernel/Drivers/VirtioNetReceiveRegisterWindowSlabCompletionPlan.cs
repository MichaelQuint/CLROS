namespace OS.Drivers;

using OS.Net;
using OS.Std.Binary;

public enum VirtioNetReceiveRegisterWindowSlabCompletionStatus
{
    Success = 0,
    ReadinessFailed = 1,
    QueueMemoryTooSmall = 2,
    NoCompletion = 3,
    UnexpectedUsedIndex = 4,
    UnexpectedDescriptorHead = 5,
    ReceiveSlabTooSmall = 6,
    CompletionFailed = 7,
    ClassificationFailed = 8
}

public enum VirtioNetReceiveBufferSlabSelectionStatus
{
    Success = 0,
    ReadinessFailed = 1,
    QueueMemoryTooSmall = 2,
    NoCompletion = 3,
    UnexpectedUsedIndex = 4,
    UnexpectedDescriptorHead = 5,
    ReceiveBufferSlabTooSmall = 6
}

public readonly struct VirtioNetReceiveRegisterWindowSlabCompletionResult
{
    private VirtioNetReceiveRegisterWindowSlabCompletionResult(
        VirtioNetReceiveRegisterWindowSlabCompletionStatus status,
        VirtioNetReceiveReadinessStatus readinessStatus,
        VirtioNetReceiveBufferSlabSelectionStatus slabSelectionStatus,
        VirtioNetReceiveRegisterWindowCompletionStatus completionPlanStatus,
        VirtioNetReceiveLifecycleCompletionStatus completionStatus,
        VirtioNetReceiveFrameClassifierStatus classifierStatus,
        PacketParseStatus packetStatus,
        bool slabSelectionAttempted,
        bool completionAttempted,
        ushort descriptorHead,
        ushort bufferIndex,
        int receiveBufferSlabLength,
        uint receiveBufferStrideBytes,
        uint selectedBufferOffset,
        uint selectedBufferLength,
        ushort previousUsedIndex,
        ushort currentUsedIndex,
        ushort nextUsedIndex,
        ushort usedRingSlot,
        uint usedRingElementOffset,
        uint usedByteCount,
        EthernetPayloadKind payloadKind,
        int payloadOffset,
        int payloadLength,
        VirtioNetReceiveRegisterWindowCompletionResult completion,
        bool isInitialized)
    {
        Status = status;
        ReadinessStatus = readinessStatus;
        SlabSelectionStatus = slabSelectionStatus;
        CompletionPlanStatus = completionPlanStatus;
        CompletionStatus = completionStatus;
        ClassifierStatus = classifierStatus;
        PacketStatus = packetStatus;
        SlabSelectionAttempted = slabSelectionAttempted;
        CompletionAttempted = completionAttempted;
        DescriptorHead = descriptorHead;
        BufferIndex = bufferIndex;
        ReceiveBufferSlabLength = receiveBufferSlabLength;
        ReceiveBufferStrideBytes = receiveBufferStrideBytes;
        SelectedBufferOffset = selectedBufferOffset;
        SelectedBufferLength = selectedBufferLength;
        PreviousUsedIndex = previousUsedIndex;
        CurrentUsedIndex = currentUsedIndex;
        NextUsedIndex = nextUsedIndex;
        UsedRingSlot = usedRingSlot;
        UsedRingElementOffset = usedRingElementOffset;
        UsedByteCount = usedByteCount;
        PayloadKind = payloadKind;
        PayloadOffset = payloadOffset;
        PayloadLength = payloadLength;
        Completion = completion;
        IsInitialized = isInitialized;
    }

    public VirtioNetReceiveRegisterWindowSlabCompletionStatus Status { get; }

    public VirtioNetReceiveReadinessStatus ReadinessStatus { get; }

    public VirtioNetReceiveBufferSlabSelectionStatus SlabSelectionStatus { get; }

    public VirtioNetReceiveRegisterWindowCompletionStatus CompletionPlanStatus { get; }

    public VirtioNetReceiveLifecycleCompletionStatus CompletionStatus { get; }

    public VirtioNetReceiveFrameClassifierStatus ClassifierStatus { get; }

    public PacketParseStatus PacketStatus { get; }

    public bool SlabSelectionAttempted { get; }

    public bool CompletionAttempted { get; }

    public ushort DescriptorHead { get; }

    public ushort BufferIndex { get; }

    public int ReceiveBufferSlabLength { get; }

    public uint ReceiveBufferStrideBytes { get; }

    public uint SelectedBufferOffset { get; }

    public uint SelectedBufferLength { get; }

    public ushort PreviousUsedIndex { get; }

    public ushort CurrentUsedIndex { get; }

    public ushort NextUsedIndex { get; }

    public ushort UsedRingSlot { get; }

    public uint UsedRingElementOffset { get; }

    public uint UsedByteCount { get; }

    public EthernetPayloadKind PayloadKind { get; }

    public int PayloadOffset { get; }

    public int PayloadLength { get; }

    public VirtioNetReceiveRegisterWindowCompletionResult Completion { get; }

    public bool IsInitialized { get; }

    public bool Succeeded => IsInitialized &&
        Status == VirtioNetReceiveRegisterWindowSlabCompletionStatus.Success;

    internal static VirtioNetReceiveRegisterWindowSlabCompletionResult FromReadinessFailure(
        VirtioNetReceiveReadinessResult readiness,
        int receiveBufferSlabLength)
    {
        return new VirtioNetReceiveRegisterWindowSlabCompletionResult(
            VirtioNetReceiveRegisterWindowSlabCompletionStatus.ReadinessFailed,
            readiness.Status,
            VirtioNetReceiveBufferSlabSelectionStatus.ReadinessFailed,
            VirtioNetReceiveRegisterWindowCompletionStatus.ReadinessFailed,
            default,
            default,
            default,
            slabSelectionAttempted: false,
            completionAttempted: false,
            descriptorHead: 0,
            bufferIndex: 0,
            receiveBufferSlabLength,
            readiness.ReceiveBufferStrideBytes,
            selectedBufferOffset: 0,
            selectedBufferLength: 0,
            readiness.PreviousUsedIndex,
            readiness.PreviousUsedIndex,
            readiness.PreviousUsedIndex,
            usedRingSlot: 0,
            usedRingElementOffset: 0,
            usedByteCount: 0,
            EthernetPayloadKind.Unsupported,
            payloadOffset: 0,
            payloadLength: 0,
            default,
            isInitialized: true);
    }

    internal static VirtioNetReceiveRegisterWindowSlabCompletionResult FromSelectionFailure(
        VirtioNetReceiveRegisterWindowSlabCompletionStatus status,
        VirtioNetReceiveReadinessResult readiness,
        VirtioNetReceiveLifecycleCompletionStatus completionStatus,
        int receiveBufferSlabLength,
        ushort descriptorHead,
        ushort bufferIndex,
        uint selectedBufferOffset,
        uint selectedBufferLength,
        ushort currentUsedIndex,
        ushort usedRingSlot,
        uint usedRingElementOffset,
        uint usedByteCount)
    {
        return new VirtioNetReceiveRegisterWindowSlabCompletionResult(
            status,
            readiness.Status,
            MapSelectionStatus(status),
            VirtioNetReceiveRegisterWindowCompletionStatus.CompletionFailed,
            completionStatus,
            default,
            default,
            slabSelectionAttempted: true,
            completionAttempted: false,
            descriptorHead,
            bufferIndex,
            receiveBufferSlabLength,
            readiness.ReceiveBufferStrideBytes,
            selectedBufferOffset,
            selectedBufferLength,
            readiness.PreviousUsedIndex,
            currentUsedIndex,
            currentUsedIndex,
            usedRingSlot,
            usedRingElementOffset,
            usedByteCount,
            EthernetPayloadKind.Unsupported,
            payloadOffset: 0,
            payloadLength: 0,
            default,
            isInitialized: true);
    }

    internal static VirtioNetReceiveRegisterWindowSlabCompletionResult FromCompletion(
        VirtioNetReceiveReadinessResult readiness,
        int receiveBufferSlabLength,
        uint selectedBufferOffset,
        uint selectedBufferLength,
        VirtioNetReceiveRegisterWindowCompletionResult completion)
    {
        VirtioNetReceiveRegisterWindowSlabCompletionStatus status =
            completion.Succeeded
                ? VirtioNetReceiveRegisterWindowSlabCompletionStatus.Success
                : completion.Status == VirtioNetReceiveRegisterWindowCompletionStatus.ClassificationFailed
                    ? VirtioNetReceiveRegisterWindowSlabCompletionStatus.ClassificationFailed
                    : VirtioNetReceiveRegisterWindowSlabCompletionStatus.CompletionFailed;

        return new VirtioNetReceiveRegisterWindowSlabCompletionResult(
            status,
            readiness.Status,
            VirtioNetReceiveBufferSlabSelectionStatus.Success,
            completion.Status,
            completion.CompletionStatus,
            completion.ClassifierStatus,
            completion.PacketStatus,
            slabSelectionAttempted: true,
            completionAttempted: true,
            completion.DescriptorHead,
            completion.BufferIndex,
            receiveBufferSlabLength,
            readiness.ReceiveBufferStrideBytes,
            selectedBufferOffset,
            selectedBufferLength,
            completion.PreviousUsedIndex,
            completion.CurrentUsedIndex,
            completion.NextUsedIndex,
            completion.UsedRingSlot,
            completion.UsedRingElementOffset,
            completion.UsedByteCount,
            completion.PayloadKind,
            completion.PayloadOffset,
            completion.PayloadLength,
            completion,
            isInitialized: true);
    }

    private static VirtioNetReceiveBufferSlabSelectionStatus MapSelectionStatus(
        VirtioNetReceiveRegisterWindowSlabCompletionStatus status)
    {
        return status switch
        {
            VirtioNetReceiveRegisterWindowSlabCompletionStatus.QueueMemoryTooSmall =>
                VirtioNetReceiveBufferSlabSelectionStatus.QueueMemoryTooSmall,
            VirtioNetReceiveRegisterWindowSlabCompletionStatus.NoCompletion =>
                VirtioNetReceiveBufferSlabSelectionStatus.NoCompletion,
            VirtioNetReceiveRegisterWindowSlabCompletionStatus.UnexpectedUsedIndex =>
                VirtioNetReceiveBufferSlabSelectionStatus.UnexpectedUsedIndex,
            VirtioNetReceiveRegisterWindowSlabCompletionStatus.UnexpectedDescriptorHead =>
                VirtioNetReceiveBufferSlabSelectionStatus.UnexpectedDescriptorHead,
            VirtioNetReceiveRegisterWindowSlabCompletionStatus.ReceiveSlabTooSmall =>
                VirtioNetReceiveBufferSlabSelectionStatus.ReceiveBufferSlabTooSmall,
            _ => VirtioNetReceiveBufferSlabSelectionStatus.Success
        };
    }
}

public static class VirtioNetReceiveRegisterWindowSlabCompletionPlanner
{
    public const bool RequiresSuccessfulRegisterWindowReadiness = true;
    public const bool SelectsCallerOwnedReceiveBufferFromSlab = true;
    public const bool ConsumesRegisterWindowNotificationProvenance = true;
    public const bool RequiresCallerOwnedQueueMemory = true;
    public const bool RequiresCallerOwnedReceiveBufferSlab = true;
    public const bool ReadsQueueMemory = true;
    public const bool ReadsCallerOwnedReceiveBuffer = true;
    public const bool ClassifiesCallerOwnedFrame = true;
    public const bool PerformsLiveMmio = false;
    public const bool MapsBars = false;
    public const bool ContainsVolatileAccess = false;
    public const bool AccessesPhysicalAddresses = false;
    public const bool ExecutesMemoryBarriers = false;
    public const bool ConfiguresLiveQueues = false;
    public const bool NotifiesLiveDevice = false;
    public const bool PollsDevice = false;
    public const bool HandlesInterrupts = false;
    public const bool PerformsDmaMapping = false;
    public const bool AllocatesDmaMemory = false;
    public const bool PerformsPacketIo = false;
    public const bool PerformsPacketReceive = false;
    public const bool CopiesPayload = false;
    public const bool WritesQueueMemory = false;

    private const uint UsedRingIndexOffset = 2;
    private const uint UsedRingIndexLength = sizeof(ushort);
    private const uint UsedRingElementIdOffset = 0;
    private const uint UsedRingElementLengthOffset = 4;
    private const uint UsedRingElementLength = VirtqueueSplitRingPlan.UsedRingElementLength;

    public static VirtioNetReceiveRegisterWindowSlabCompletionResult CompleteReceiveFromSlab(
        VirtioNetReceiveReadinessResult readiness,
        ReadOnlySpan<byte> queueMemory,
        ReadOnlySpan<byte> receiveBufferSlab)
    {
        if (!readiness.Succeeded)
        {
            return VirtioNetReceiveRegisterWindowSlabCompletionResult.FromReadinessFailure(
                readiness,
                receiveBufferSlab.Length);
        }

        VirtioNetReceiveLifecyclePublishResult publish = readiness.ReceivePublish;
        ushort usedRingSlot = (ushort)(publish.PreviousUsedIndex & (publish.QueueSize - 1));
        uint usedRingIndexOffset = publish.QueuePlan.UsedRing.Offset + UsedRingIndexOffset;
        if (!TryValidateRange(usedRingIndexOffset, UsedRingIndexLength, queueMemory.Length) ||
            !TryGetUsedRingElementOffset(publish.QueuePlan, usedRingSlot, out uint usedRingElementOffset) ||
            !TryValidateRange(usedRingElementOffset, UsedRingElementLength, queueMemory.Length))
        {
            return VirtioNetReceiveRegisterWindowSlabCompletionResult.FromSelectionFailure(
                VirtioNetReceiveRegisterWindowSlabCompletionStatus.QueueMemoryTooSmall,
                readiness,
                VirtioNetReceiveLifecycleCompletionStatus.QueueMemoryTooSmall,
                receiveBufferSlab.Length,
                descriptorHead: 0,
                bufferIndex: 0,
                selectedBufferOffset: 0,
                selectedBufferLength: 0,
                publish.PreviousUsedIndex,
                usedRingSlot,
                usedRingElementOffset: 0,
                usedByteCount: 0);
        }

        ushort currentUsedIndex = ReadUInt16(queueMemory, usedRingIndexOffset);
        if (currentUsedIndex == publish.PreviousUsedIndex)
        {
            return VirtioNetReceiveRegisterWindowSlabCompletionResult.FromSelectionFailure(
                VirtioNetReceiveRegisterWindowSlabCompletionStatus.NoCompletion,
                readiness,
                VirtioNetReceiveLifecycleCompletionStatus.NoCompletion,
                receiveBufferSlab.Length,
                descriptorHead: 0,
                bufferIndex: 0,
                selectedBufferOffset: 0,
                selectedBufferLength: 0,
                currentUsedIndex,
                usedRingSlot,
                usedRingElementOffset,
                usedByteCount: 0);
        }

        if (currentUsedIndex != publish.ExpectedNextUsedIndex)
        {
            return VirtioNetReceiveRegisterWindowSlabCompletionResult.FromSelectionFailure(
                VirtioNetReceiveRegisterWindowSlabCompletionStatus.UnexpectedUsedIndex,
                readiness,
                VirtioNetReceiveLifecycleCompletionStatus.UnexpectedUsedIndex,
                receiveBufferSlab.Length,
                descriptorHead: 0,
                bufferIndex: 0,
                selectedBufferOffset: 0,
                selectedBufferLength: 0,
                currentUsedIndex,
                usedRingSlot,
                usedRingElementOffset,
                usedByteCount: 0);
        }

        uint descriptorHead = ReadUInt32(queueMemory, usedRingElementOffset + UsedRingElementIdOffset);
        uint usedByteCount = ReadUInt32(queueMemory, usedRingElementOffset + UsedRingElementLengthOffset);
        if (!TryMapDescriptorHead(publish, descriptorHead, out ushort descriptorIndex, out ushort bufferIndex))
        {
            return VirtioNetReceiveRegisterWindowSlabCompletionResult.FromSelectionFailure(
                VirtioNetReceiveRegisterWindowSlabCompletionStatus.UnexpectedDescriptorHead,
                readiness,
                VirtioNetReceiveLifecycleCompletionStatus.UnexpectedDescriptorHead,
                receiveBufferSlab.Length,
                descriptorIndex,
                bufferIndex,
                selectedBufferOffset: 0,
                selectedBufferLength: 0,
                currentUsedIndex,
                usedRingSlot,
                usedRingElementOffset,
                usedByteCount);
        }

        if (!TryGetSelectedBufferRange(
                publish,
                bufferIndex,
                receiveBufferSlab.Length,
                out uint selectedBufferOffset,
                out uint selectedBufferLength))
        {
            return VirtioNetReceiveRegisterWindowSlabCompletionResult.FromSelectionFailure(
                VirtioNetReceiveRegisterWindowSlabCompletionStatus.ReceiveSlabTooSmall,
                readiness,
                VirtioNetReceiveLifecycleCompletionStatus.ReceiveBufferTooSmall,
                receiveBufferSlab.Length,
                descriptorIndex,
                bufferIndex,
                selectedBufferOffset,
                selectedBufferLength,
                currentUsedIndex,
                usedRingSlot,
                usedRingElementOffset,
                usedByteCount);
        }

        VirtioNetReceiveRegisterWindowCompletionResult completion =
            VirtioNetReceiveRegisterWindowCompletionPlanner.CompleteReceive(
                readiness,
                queueMemory,
                receiveBufferSlab.Slice(
                    (int)selectedBufferOffset,
                    (int)selectedBufferLength));

        return VirtioNetReceiveRegisterWindowSlabCompletionResult.FromCompletion(
            readiness,
            receiveBufferSlab.Length,
            selectedBufferOffset,
            selectedBufferLength,
            completion);
    }

    private static bool TryMapDescriptorHead(
        VirtioNetReceiveLifecyclePublishResult publish,
        uint descriptorHead,
        out ushort descriptorIndex,
        out ushort bufferIndex)
    {
        descriptorIndex = 0;
        bufferIndex = 0;
        if (descriptorHead > ushort.MaxValue ||
            descriptorHead < publish.FirstDescriptorIndex)
        {
            return false;
        }

        uint relativeIndex = descriptorHead - publish.FirstDescriptorIndex;
        if (relativeIndex >= publish.PublishedDescriptorCount ||
            relativeIndex > ushort.MaxValue)
        {
            return false;
        }

        descriptorIndex = (ushort)descriptorHead;
        bufferIndex = (ushort)relativeIndex;
        return true;
    }

    private static bool TryGetSelectedBufferRange(
        VirtioNetReceiveLifecyclePublishResult publish,
        ushort bufferIndex,
        int slabLength,
        out uint selectedBufferOffset,
        out uint selectedBufferLength)
    {
        selectedBufferOffset = 0;
        selectedBufferLength = publish.FrameBufferBytes;
        ulong offset = (ulong)bufferIndex * publish.ReceiveBufferStrideBytes;
        ulong end = offset + publish.FrameBufferBytes;
        if (offset > uint.MaxValue ||
            end > uint.MaxValue ||
            end > (ulong)slabLength)
        {
            selectedBufferOffset = offset <= uint.MaxValue ? (uint)offset : 0;
            return false;
        }

        selectedBufferOffset = (uint)offset;
        return true;
    }

    private static bool TryGetUsedRingElementOffset(
        VirtqueueSplitRingPlan queue,
        ushort slot,
        out uint offset)
    {
        ulong value = (ulong)queue.UsedRing.Offset +
            VirtqueueSplitRingPlan.UsedRingHeaderLength +
            ((ulong)slot * VirtqueueSplitRingPlan.UsedRingElementLength);
        if (value > uint.MaxValue)
        {
            offset = 0;
            return false;
        }

        offset = (uint)value;
        return true;
    }

    private static bool TryValidateRange(
        uint offset,
        uint length,
        int sourceLength)
    {
        ulong end = (ulong)offset + length;
        return end <= int.MaxValue && end <= (ulong)sourceLength;
    }

    private static ushort ReadUInt16(
        ReadOnlySpan<byte> source,
        uint offset)
    {
        return EndianPrimitives.ReadUInt16LittleEndian(
            source.Slice((int)offset, sizeof(ushort)));
    }

    private static uint ReadUInt32(
        ReadOnlySpan<byte> source,
        uint offset)
    {
        return EndianPrimitives.ReadUInt32LittleEndian(
            source.Slice((int)offset, sizeof(uint)));
    }
}
