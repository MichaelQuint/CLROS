using OS.Std.Binary;

namespace OS.Storage;

public enum VirtioBlockDeviceStatus : byte
{
    Ok = 0,
    IoError = 1,
    Unsupported = 2
}
