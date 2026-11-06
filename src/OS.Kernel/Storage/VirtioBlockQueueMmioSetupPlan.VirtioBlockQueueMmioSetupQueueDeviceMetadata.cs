using OS.Drivers;

namespace OS.Storage;

public readonly struct VirtioBlockQueueMmioSetupQueueDeviceMetadata
{
    public VirtioBlockQueueMmioSetupQueueDeviceMetadata(
        ushort deviceMaximumQueueSize,
        bool hasNotifyOffset,
        ushort notifyOffset)
    {
        DeviceMaximumQueueSize = deviceMaximumQueueSize;
        HasNotifyOffset = hasNotifyOffset;
        NotifyOffset = notifyOffset;
    }

    public ushort DeviceMaximumQueueSize
    {
        get;
    }

    public bool HasNotifyOffset
    {
        get;
    }

    public ushort NotifyOffset
    {
        get;
    }
}
