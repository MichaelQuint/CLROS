using OS.Drivers;

namespace OS.Storage;

public readonly struct VirtioBlockQueueMmioSetupPlan
{
    internal VirtioBlockQueueMmioSetupPlan(
        ushort queueIndex,
        ushort plannedQueueSize,
        ushort deviceMaximumQueueSize,
        ushort notifyOffset,
        VirtioBlockQueueMmioSetupQueueAddresses addresses,
        uint descriptorTableBytes,
        uint driverRingBytes,
        uint deviceRingBytes,
        int commandCount)
    {
        QueueIndex = queueIndex;
        PlannedQueueSize = plannedQueueSize;
        DeviceMaximumQueueSize = deviceMaximumQueueSize;
        NotifyOffset = notifyOffset;
        Addresses = addresses;
        DescriptorTableBytes = descriptorTableBytes;
        DriverRingBytes = driverRingBytes;
        DeviceRingBytes = deviceRingBytes;
        CommandCount = commandCount;
    }

    public ushort QueueIndex { get; }

    public ushort PlannedQueueSize { get; }

    public ushort DeviceMaximumQueueSize { get; }

    public ushort NotifyOffset { get; }

    public VirtioBlockQueueMmioSetupQueueAddresses Addresses { get; }

    public uint DescriptorTableBytes { get; }

    public uint DriverRingBytes { get; }

    public uint DeviceRingBytes { get; }

    public int CommandCount { get; }
}
