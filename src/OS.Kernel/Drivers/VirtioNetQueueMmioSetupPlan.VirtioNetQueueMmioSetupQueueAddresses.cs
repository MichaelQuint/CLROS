using OS.Std.Binary;

namespace OS.Drivers;

public readonly struct VirtioNetQueueMmioSetupQueueAddresses
{
    public VirtioNetQueueMmioSetupQueueAddresses(ulong descriptorTablePhysicalAddress, ulong driverRingPhysicalAddress, ulong deviceRingPhysicalAddress)
    {
        DescriptorTablePhysicalAddress = descriptorTablePhysicalAddress;
        DriverRingPhysicalAddress = driverRingPhysicalAddress;
        DeviceRingPhysicalAddress = deviceRingPhysicalAddress;
    }

    public ulong DescriptorTablePhysicalAddress
    {
        get;
    }
    public ulong DriverRingPhysicalAddress
    {
        get;
    }
    public ulong DeviceRingPhysicalAddress
    {
        get;
    }
}
