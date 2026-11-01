using OS.Std.Binary;

namespace OS.Drivers;
public readonly struct VirtioNetQueueMmioSetupQueueDeviceMetadata
{
    public VirtioNetQueueMmioSetupQueueDeviceMetadata(ushort deviceMaximumQueueSize, bool hasNotifyOffset, ushort notifyOffset)
    {
        DeviceMaximumQueueSize = deviceMaximumQueueSize;
        HasNotifyOffset = hasNotifyOffset;
        NotifyOffset = notifyOffset;
    }

    public ushort DeviceMaximumQueueSize { get; }
    public bool HasNotifyOffset { get; }
    public ushort NotifyOffset { get; }
}
