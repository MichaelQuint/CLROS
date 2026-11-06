using OS.Drivers;

namespace OS.Storage;

public enum VirtioBlockQueueMmioSetupAddressKind
{
    None = 0,
    DescriptorTable = 1,
    DriverRing = 2,
    DeviceRing = 3
}
