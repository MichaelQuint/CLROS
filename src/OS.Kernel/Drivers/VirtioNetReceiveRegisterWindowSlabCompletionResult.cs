namespace OS.Drivers;
using OS.Net;
using OS.Std.Binary;

public readonly struct VirtioNetReceiveRegisterWindowSlabCompletionResult
{
    private VirtioNetReceiveRegisterWindowSlabCompletionResult(VirtioNetReceiveRegisterWindowSlabCompletionStatus status, VirtioNetReceiveReadinessStatus readinessStatus, VirtioNetReceiveBufferSlabSelectionStatus slabSelectionStatus, VirtioNetReceiveRegisterWindowCompletionStatus completionPlanStatus, VirtioNetReceiveLifecycleCompletionStatus completionStatus, VirtioNetReceiveFrameClassifierStatus classifierStatus, PacketParseStatus packetStatus, bool slabSelectionAttempted, bool completionAttempted, ushort descriptorHead, ushort bufferIndex, int receiveBufferSlabLength, uint receiveBufferStrideBytes, uint selectedBufferOffset, uint selectedBufferLength, ushort previousUsedIndex, ushort currentUsedIndex, ushort nextUsedIndex, ushort usedRingSlot, uint usedRingElementOffset, uint usedByteCount, EthernetPayloadKind payloadKind, int payloadOffset, int payloadLength, VirtioNetReceiveRegisterWindowCompletionResult completion, bool isInitialized)
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
    public bool Succeeded => IsInitialized && Status == VirtioNetReceiveRegisterWindowSlabCompletionStatus.Success;

    internal static VirtioNetReceiveRegisterWindowSlabCompletionResult FromReadinessFailure(VirtioNetReceiveReadinessResult readiness, int receiveBufferSlabLength)
    {
        return new VirtioNetReceiveRegisterWindowSlabCompletionResult(VirtioNetReceiveRegisterWindowSlabCompletionStatus.ReadinessFailed, readiness.Status, VirtioNetReceiveBufferSlabSelectionStatus.ReadinessFailed, VirtioNetReceiveRegisterWindowCompletionStatus.ReadinessFailed, default, default, default, slabSelectionAttempted: false, completionAttempted: false, descriptorHead: 0, bufferIndex: 0, receiveBufferSlabLength, readiness.ReceiveBufferStrideBytes, selectedBufferOffset: 0, selectedBufferLength: 0, readiness.PreviousUsedIndex, readiness.PreviousUsedIndex, readiness.PreviousUsedIndex, usedRingSlot: 0, usedRingElementOffset: 0, usedByteCount: 0, EthernetPayloadKind.Unsupported, payloadOffset: 0, payloadLength: 0, default, isInitialized: true);
    }

    internal static VirtioNetReceiveRegisterWindowSlabCompletionResult FromSelectionFailure(VirtioNetReceiveRegisterWindowSlabCompletionStatus status, VirtioNetReceiveReadinessResult readiness, VirtioNetReceiveLifecycleCompletionStatus completionStatus, int receiveBufferSlabLength, ushort descriptorHead, ushort bufferIndex, uint selectedBufferOffset, uint selectedBufferLength, ushort currentUsedIndex, ushort usedRingSlot, uint usedRingElementOffset, uint usedByteCount)
    {
        return new VirtioNetReceiveRegisterWindowSlabCompletionResult(status, readiness.Status, MapSelectionStatus(status), VirtioNetReceiveRegisterWindowCompletionStatus.CompletionFailed, completionStatus, default, default, slabSelectionAttempted: true, completionAttempted: false, descriptorHead, bufferIndex, receiveBufferSlabLength, readiness.ReceiveBufferStrideBytes, selectedBufferOffset, selectedBufferLength, readiness.PreviousUsedIndex, currentUsedIndex, currentUsedIndex, usedRingSlot, usedRingElementOffset, usedByteCount, EthernetPayloadKind.Unsupported, payloadOffset: 0, payloadLength: 0, default, isInitialized: true);
    }

    internal static VirtioNetReceiveRegisterWindowSlabCompletionResult FromCompletion(VirtioNetReceiveReadinessResult readiness, int receiveBufferSlabLength, uint selectedBufferOffset, uint selectedBufferLength, VirtioNetReceiveRegisterWindowCompletionResult completion)
    {
        VirtioNetReceiveRegisterWindowSlabCompletionStatus status = completion.Succeeded ? VirtioNetReceiveRegisterWindowSlabCompletionStatus.Success : completion.Status == VirtioNetReceiveRegisterWindowCompletionStatus.ClassificationFailed ? VirtioNetReceiveRegisterWindowSlabCompletionStatus.ClassificationFailed : VirtioNetReceiveRegisterWindowSlabCompletionStatus.CompletionFailed;
        return new VirtioNetReceiveRegisterWindowSlabCompletionResult(status, readiness.Status, VirtioNetReceiveBufferSlabSelectionStatus.Success, completion.Status, completion.CompletionStatus, completion.ClassifierStatus, completion.PacketStatus, slabSelectionAttempted: true, completionAttempted: true, completion.DescriptorHead, completion.BufferIndex, receiveBufferSlabLength, readiness.ReceiveBufferStrideBytes, selectedBufferOffset, selectedBufferLength, completion.PreviousUsedIndex, completion.CurrentUsedIndex, completion.NextUsedIndex, completion.UsedRingSlot, completion.UsedRingElementOffset, completion.UsedByteCount, completion.PayloadKind, completion.PayloadOffset, completion.PayloadLength, completion, isInitialized: true);
    }

    private static VirtioNetReceiveBufferSlabSelectionStatus MapSelectionStatus(VirtioNetReceiveRegisterWindowSlabCompletionStatus status)
    {
        return status switch
        {
            VirtioNetReceiveRegisterWindowSlabCompletionStatus.QueueMemoryTooSmall => VirtioNetReceiveBufferSlabSelectionStatus.QueueMemoryTooSmall,
            VirtioNetReceiveRegisterWindowSlabCompletionStatus.NoCompletion => VirtioNetReceiveBufferSlabSelectionStatus.NoCompletion,
            VirtioNetReceiveRegisterWindowSlabCompletionStatus.UnexpectedUsedIndex => VirtioNetReceiveBufferSlabSelectionStatus.UnexpectedUsedIndex,
            VirtioNetReceiveRegisterWindowSlabCompletionStatus.UnexpectedDescriptorHead => VirtioNetReceiveBufferSlabSelectionStatus.UnexpectedDescriptorHead,
            VirtioNetReceiveRegisterWindowSlabCompletionStatus.ReceiveSlabTooSmall => VirtioNetReceiveBufferSlabSelectionStatus.ReceiveBufferSlabTooSmall,
            _ => VirtioNetReceiveBufferSlabSelectionStatus.Success
        };
    }
}
