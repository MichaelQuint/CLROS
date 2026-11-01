using OS.Kernel.Platform;

namespace OS.Drivers;
public enum PciEcamPhysicalHeaderReadStatus
{
    Success = 0,
    MissingPhysicalMemory = 1,
    AddressPlanningFailed = 2,
    DestinationTooSmall = 3,
    PhysicalReadFailed = 4
}
