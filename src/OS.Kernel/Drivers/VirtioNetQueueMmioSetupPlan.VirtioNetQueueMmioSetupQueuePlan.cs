using OS.Std.Binary;

namespace OS.Drivers;
public readonly struct VirtioNetQueueMmioSetupQueuePlan
{
    internal VirtioNetQueueMmioSetupQueuePlan(ushort queueIndex, ushort plannedQueueSize, ushort deviceMaximumQueueSize, ushort notifyOffset, VirtioNetQueueMmioSetupQueueAddresses addresses, uint descriptorTableBytes, uint driverRingBytes, uint deviceRingBytes, int commandStartIndex, int commandCount)
    {
        QueueIndex = queueIndex;
        PlannedQueueSize = plannedQueueSize;
        DeviceMaximumQueueSize = deviceMaximumQueueSize;
        NotifyOffset = notifyOffset;
        Addresses = addresses;
        DescriptorTableBytes = descriptorTableBytes;
        DriverRingBytes = driverRingBytes;
        DeviceRingBytes = deviceRingBytes;
        CommandStartIndex = commandStartIndex;
        CommandCount = commandCount;
    }

    public ushort QueueIndex { get; }
    public ushort PlannedQueueSize { get; }
    public ushort DeviceMaximumQueueSize { get; }
    public ushort NotifyOffset { get; }
    public VirtioNetQueueMmioSetupQueueAddresses Addresses { get; }
    public uint DescriptorTableBytes { get; }
    public uint DriverRingBytes { get; }
    public uint DeviceRingBytes { get; }
    public int CommandStartIndex { get; }
    public int CommandCount { get; }
}
