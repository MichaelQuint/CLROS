using OS.Drivers;
using OS.Std.Binary;

namespace OS.Storage;

public enum VirtioBlockDeviceConfigStatus
{
    Success = 0,
    ConfigTooSmall = 1,
    InvalidCapacity = 2,
    CapacityOverflow = 3,
    BlockDeviceInfoRejected = 4
}
