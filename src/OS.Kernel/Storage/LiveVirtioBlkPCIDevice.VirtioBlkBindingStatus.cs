using OS.Drivers;
using OS.Std.Binary;

namespace OS.Storage;

public enum VirtioBlkBindingStatus : byte
{
    Uninitialized, Success, InvalidSnapshot, InvalidBdf, ReadFailed, AbsentDevice,
    WrongIdentity, UnsupportedHeader, InvalidCapabilities, MissingCapabilities,
    DeviceConfigurationTooShort
}
