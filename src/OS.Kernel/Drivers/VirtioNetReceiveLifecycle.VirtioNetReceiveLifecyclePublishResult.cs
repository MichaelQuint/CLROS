namespace OS.Drivers;
using OS.Net;
using OS.Std.Binary;

public readonly struct VirtioNetReceiveLifecyclePublishResult
{
    private VirtioNetReceiveLifecyclePublishResult(VirtioNetReceiveLifecyclePublishStatus status, VirtqueueSplitRingPlanStatus queuePlanStatus, VirtqueueSplitRingPlan queuePlan, ushort queueSize, ushort firstDescriptorIndex, ushort publishedDescriptorCount, ushort previousAvailableRingIndex, ushort nextAvailableRingIndex, ushort previousUsedIndex, ushort expectedNextUsedIndex, ushort firstAvailableRingSlot, ushort lastAvailableRingSlot, uint firstAvailableRingEntryOffset, uint frameBufferBytes, uint receiveBufferStrideBytes)
    {
        IsInitialized = true;
        Status = status;
        QueuePlanStatus = queuePlanStatus;
        QueuePlan = queuePlan;
        QueueSize = queueSize;
        FirstDescriptorIndex = firstDescriptorIndex;
        PublishedDescriptorCount = publishedDescriptorCount;
        PreviousAvailableRingIndex = previousAvailableRingIndex;
        NextAvailableRingIndex = nextAvailableRingIndex;
        PreviousUsedIndex = previousUsedIndex;
        ExpectedNextUsedIndex = expectedNextUsedIndex;
        FirstAvailableRingSlot = firstAvailableRingSlot;
        LastAvailableRingSlot = lastAvailableRingSlot;
        FirstAvailableRingEntryOffset = firstAvailableRingEntryOffset;
        FrameBufferBytes = frameBufferBytes;
        ReceiveBufferStrideBytes = receiveBufferStrideBytes;
    }

    public VirtioNetReceiveLifecyclePublishStatus Status { get; }
    public bool IsInitialized { get; }
    public VirtqueueSplitRingPlanStatus QueuePlanStatus { get; }
    public VirtqueueSplitRingPlan QueuePlan { get; }
    public ushort QueueSize { get; }
    public ushort FirstDescriptorIndex { get; }
    public ushort PublishedDescriptorCount { get; }
    public ushort PreviousAvailableRingIndex { get; }
    public ushort NextAvailableRingIndex { get; }
    public ushort PreviousUsedIndex { get; }
    public ushort ExpectedNextUsedIndex { get; }
    public ushort FirstAvailableRingSlot { get; }
    public ushort LastAvailableRingSlot { get; }
    public uint FirstAvailableRingEntryOffset { get; }
    public uint FrameBufferBytes { get; }
    public uint ReceiveBufferStrideBytes { get; }
    public bool Succeeded => IsInitialized && Status == VirtioNetReceiveLifecyclePublishStatus.Success;

    internal static VirtioNetReceiveLifecyclePublishResult Success(VirtqueueSplitRingPlan queuePlan, ushort firstDescriptorIndex, ushort publishedDescriptorCount, ushort previousAvailableRingIndex, ushort previousUsedIndex, ushort firstAvailableRingSlot, ushort lastAvailableRingSlot, uint firstAvailableRingEntryOffset, uint frameBufferBytes, uint receiveBufferStrideBytes)
    {
        return new VirtioNetReceiveLifecyclePublishResult(VirtioNetReceiveLifecyclePublishStatus.Success, VirtqueueSplitRingPlanStatus.Success, queuePlan, queuePlan.QueueSize, firstDescriptorIndex, publishedDescriptorCount, previousAvailableRingIndex, unchecked((ushort)(previousAvailableRingIndex + publishedDescriptorCount)), previousUsedIndex, unchecked((ushort)(previousUsedIndex + 1)), firstAvailableRingSlot, lastAvailableRingSlot, firstAvailableRingEntryOffset, frameBufferBytes, receiveBufferStrideBytes);
    }

    internal static VirtioNetReceiveLifecyclePublishResult Fail(VirtioNetReceiveLifecyclePublishStatus status, VirtqueueSplitRingPlanStatus queuePlanStatus, VirtqueueSplitRingPlan queuePlan, ushort queueSize, ushort firstDescriptorIndex, ushort publishedDescriptorCount, ushort previousAvailableRingIndex, ushort previousUsedIndex, uint frameBufferBytes, uint receiveBufferStrideBytes)
    {
        return new VirtioNetReceiveLifecyclePublishResult(status, queuePlanStatus, queuePlan, queueSize, firstDescriptorIndex, publishedDescriptorCount, previousAvailableRingIndex, previousAvailableRingIndex, previousUsedIndex, previousUsedIndex, firstAvailableRingSlot: 0, lastAvailableRingSlot: 0, firstAvailableRingEntryOffset: 0, frameBufferBytes, receiveBufferStrideBytes);
    }
}
