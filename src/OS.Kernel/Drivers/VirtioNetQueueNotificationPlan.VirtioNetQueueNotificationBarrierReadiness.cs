namespace OS.Drivers;

public readonly struct VirtioNetQueueNotificationBarrierReadiness
{
    public VirtioNetQueueNotificationBarrierReadiness(bool descriptorWritesVisible, bool availableRingWritesVisible, bool orderedBeforeNotify, uint barrierSequence)
    {
        DescriptorWritesVisible = descriptorWritesVisible;
        AvailableRingWritesVisible = availableRingWritesVisible;
        OrderedBeforeNotify = orderedBeforeNotify;
        BarrierSequence = barrierSequence;
    }

    public bool DescriptorWritesVisible
    {
        get;
    }
    public bool AvailableRingWritesVisible
    {
        get;
    }
    public bool OrderedBeforeNotify
    {
        get;
    }
    public uint BarrierSequence
    {
        get;
    }
    public bool Succeeded => DescriptorWritesVisible && AvailableRingWritesVisible && OrderedBeforeNotify;

    public static VirtioNetQueueNotificationBarrierReadiness Ready(uint barrierSequence)
    {
        return new VirtioNetQueueNotificationBarrierReadiness(descriptorWritesVisible: true, availableRingWritesVisible: true, orderedBeforeNotify: true, barrierSequence);
    }
}
