using OS.Std.Binary;

namespace OS.Drivers;
public enum VirtioNetBindingStatus : byte
{
    Uninitialized,
    Success,
    InvalidSnapshot,
    InvalidBdf,
    ReadFailed,
    AbsentDevice,
    WrongIdentity,
    UnsupportedHeader,
    InvalidCapabilities,
    MissingCapabilities
}
