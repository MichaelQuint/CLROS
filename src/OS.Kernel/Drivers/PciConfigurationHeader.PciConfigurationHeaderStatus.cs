using OS.Std.Binary;

namespace OS.Drivers;
public enum PciConfigurationHeaderStatus
{
    Success = 0,
    BufferTooSmall = 1,
    InvalidVendor = 2,
    UnsupportedHeaderType = 3,
    InvalidCapabilityPointerAlignment = 4,
    InvalidCapabilityPointerRange = 5
}
