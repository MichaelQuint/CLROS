namespace OS.Drivers;

using OS.Net;
using OS.Std.Binary;

public readonly struct VirtioNetReceiveLifecycleCompletionResult
{
    private VirtioNetReceiveLifecycleCompletionResult(VirtioNetReceiveLifecycleCompletionStatus status, VirtioNetReceiveLifecyclePublishStatus publishStatus, VirtqueueSplitRingPlanStatus queuePlanStatus, VirtioNetReceiveFrameClassifierStatus classifierStatus, PacketParseStatus packetStatus, ushort descriptorHead, ushort bufferIndex, ushort previousUsedIndex, ushort currentUsedIndex, ushort nextUsedIndex, ushort usedRingSlot, uint usedRingElementOffset, uint usedByteCount, uint frameBufferBytes, VirtioNetReceiveFrameClassificationResult classification)
    {
        IsInitialized = true;
        Status = status;
        PublishStatus = publishStatus;
        QueuePlanStatus = queuePlanStatus;
        ClassifierStatus = classifierStatus;
        PacketStatus = packetStatus;
        DescriptorHead = descriptorHead;
        BufferIndex = bufferIndex;
        PreviousUsedIndex = previousUsedIndex;
        CurrentUsedIndex = currentUsedIndex;
        NextUsedIndex = nextUsedIndex;
        UsedRingSlot = usedRingSlot;
        UsedRingElementOffset = usedRingElementOffset;
        UsedByteCount = usedByteCount;
        FrameBufferBytes = frameBufferBytes;
        Classification = classification;
    }

    public VirtioNetReceiveLifecycleCompletionStatus Status
    {
        get;
    }
    public bool IsInitialized
    {
        get;
    }
    public VirtioNetReceiveLifecyclePublishStatus PublishStatus
    {
        get;
    }
    public VirtqueueSplitRingPlanStatus QueuePlanStatus
    {
        get;
    }
    public VirtioNetReceiveFrameClassifierStatus ClassifierStatus
    {
        get;
    }
    public PacketParseStatus PacketStatus
    {
        get;
    }
    public ushort DescriptorHead
    {
        get;
    }
    public ushort BufferIndex
    {
        get;
    }
    public ushort PreviousUsedIndex
    {
        get;
    }
    public ushort CurrentUsedIndex
    {
        get;
    }
    public ushort NextUsedIndex
    {
        get;
    }
    public ushort UsedRingSlot
    {
        get;
    }
    public uint UsedRingElementOffset
    {
        get;
    }
    public uint UsedByteCount
    {
        get;
    }
    public uint FrameBufferBytes
    {
        get;
    }
    public VirtioNetReceiveFrameClassificationResult Classification
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == VirtioNetReceiveLifecycleCompletionStatus.Success;

    internal static VirtioNetReceiveLifecycleCompletionResult Create(VirtioNetReceiveLifecycleCompletionStatus status, VirtioNetReceiveLifecyclePublishResult publish, VirtioNetReceiveFrameClassifierStatus classifierStatus, PacketParseStatus packetStatus, ushort descriptorHead, ushort bufferIndex, ushort currentUsedIndex, ushort usedRingSlot, uint usedRingElementOffset, uint usedByteCount, VirtioNetReceiveFrameClassificationResult classification)
    {
        return new VirtioNetReceiveLifecycleCompletionResult(status, publish.Status, publish.QueuePlanStatus, classifierStatus, packetStatus, descriptorHead, bufferIndex, publish.PreviousUsedIndex, currentUsedIndex, currentUsedIndex, usedRingSlot, usedRingElementOffset, usedByteCount, publish.FrameBufferBytes, classification);
    }
}
