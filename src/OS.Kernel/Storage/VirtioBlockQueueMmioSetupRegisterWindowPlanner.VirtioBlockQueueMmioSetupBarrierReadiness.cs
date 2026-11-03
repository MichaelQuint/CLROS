using OS.Drivers;

namespace OS.Storage;

public readonly struct VirtioBlockQueueMmioSetupBarrierReadiness
{
    public VirtioBlockQueueMmioSetupBarrierReadiness(
        bool queueSelectVisible,
        bool queueSizeVisible,
        bool queueAddressWritesVisible,
        bool orderedBeforeQueueEnable,
        uint barrierSequence)
    {
        QueueSelectVisible = queueSelectVisible;
        QueueSizeVisible = queueSizeVisible;
        QueueAddressWritesVisible = queueAddressWritesVisible;
        OrderedBeforeQueueEnable = orderedBeforeQueueEnable;
        BarrierSequence = barrierSequence;
    }

    public bool QueueSelectVisible
    {
        get;
    }

    public bool QueueSizeVisible
    {
        get;
    }

    public bool QueueAddressWritesVisible
    {
        get;
    }

    public bool OrderedBeforeQueueEnable
    {
        get;
    }

    public uint BarrierSequence
    {
        get;
    }

    public bool Succeeded =>
        QueueSelectVisible &&
        QueueSizeVisible &&
        QueueAddressWritesVisible &&
        OrderedBeforeQueueEnable;

    public static VirtioBlockQueueMmioSetupBarrierReadiness Ready(uint barrierSequence)
    {
        return new VirtioBlockQueueMmioSetupBarrierReadiness(
            queueSelectVisible: true,
            queueSizeVisible: true,
            queueAddressWritesVisible: true,
            orderedBeforeQueueEnable: true,
            barrierSequence);
    }
}
