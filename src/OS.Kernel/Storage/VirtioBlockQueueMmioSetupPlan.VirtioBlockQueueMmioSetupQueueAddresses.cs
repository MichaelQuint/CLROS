using OS.Drivers;

namespace OS.Storage;

public readonly struct VirtioBlockQueueMmioSetupQueueAddresses
{
    public VirtioBlockQueueMmioSetupQueueAddresses(
        ulong descriptorTablePhysicalAddress,
        ulong driverRingPhysicalAddress,
        ulong deviceRingPhysicalAddress)
    {
        DescriptorTablePhysicalAddress = descriptorTablePhysicalAddress;
        DriverRingPhysicalAddress = driverRingPhysicalAddress;
        DeviceRingPhysicalAddress = deviceRingPhysicalAddress;
    }

    public ulong DescriptorTablePhysicalAddress { get; }

    public ulong DriverRingPhysicalAddress { get; }

    public ulong DeviceRingPhysicalAddress { get; }
}
