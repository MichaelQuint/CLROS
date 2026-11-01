namespace OS.Drivers;
using OS.Std.Binary;

public readonly struct VirtioNetTransmitLifecyclePlan
{
    internal VirtioNetTransmitLifecyclePlan(VirtioNetQueuePlan queuePlan, ushort availableDescriptorCount, ushort availableRingIndex, ushort availableRingSlot, uint availableRingEntryOffset, uint virtioHeaderBytes, uint ethernetFrameBytes, VirtioNetTransmitPublishedDescriptor headerDescriptor, VirtioNetTransmitPublishedDescriptor frameDescriptor)
    {
        QueuePlan = queuePlan;
        AvailableDescriptorCount = availableDescriptorCount;
        AvailableRingIndex = availableRingIndex;
        AvailableRingSlot = availableRingSlot;
        AvailableRingEntryOffset = availableRingEntryOffset;
        VirtioHeaderBytes = virtioHeaderBytes;
        EthernetFrameBytes = ethernetFrameBytes;
        HeaderDescriptor = headerDescriptor;
        FrameDescriptor = frameDescriptor;
    }

    public VirtioNetQueuePlan QueuePlan { get; }
    public VirtqueueSplitRingPlan TxQueue => QueuePlan.TxQueue;
    public ushort AvailableDescriptorCount { get; }
    public ushort AvailableRingIndex { get; }
    public ushort AvailableRingSlot { get; }
    public uint AvailableRingEntryOffset { get; }
    public uint VirtioHeaderBytes { get; }
    public uint EthernetFrameBytes { get; }
    public ushort HeadDescriptorIndex => HeaderDescriptor.DescriptorIndex;
    public VirtioNetTransmitPublishedDescriptor HeaderDescriptor { get; }
    public VirtioNetTransmitPublishedDescriptor FrameDescriptor { get; }
}
