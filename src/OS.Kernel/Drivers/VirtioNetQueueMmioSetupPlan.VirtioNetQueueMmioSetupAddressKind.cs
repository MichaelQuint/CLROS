using OS.Std.Binary;

namespace OS.Drivers;

public enum VirtioNetQueueMmioSetupAddressKind
{
    None = 0,
    DescriptorTable = 1,
    DriverRing = 2,
    DeviceRing = 3
}
