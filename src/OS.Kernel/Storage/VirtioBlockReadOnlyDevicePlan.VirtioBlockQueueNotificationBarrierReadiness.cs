using OS.Drivers;

namespace OS.Storage;

public readonly struct VirtioBlockQueueNotificationBarrierReadiness
{
    public VirtioBlockQueueNotificationBarrierReadiness(
        bool descriptorWritesVisible,
        bool availableRingWritesVisible,
        bool orderedBeforeNotify,
        uint barrierSequence)
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

    public bool Succeeded =>
        DescriptorWritesVisible &&
        AvailableRingWritesVisible &&
        OrderedBeforeNotify;

    public static VirtioBlockQueueNotificationBarrierReadiness Ready(uint barrierSequence)
    {
        return new VirtioBlockQueueNotificationBarrierReadiness(
            descriptorWritesVisible: true,
            availableRingWritesVisible: true,
            orderedBeforeNotify: true,
            barrierSequence);
    }
}
