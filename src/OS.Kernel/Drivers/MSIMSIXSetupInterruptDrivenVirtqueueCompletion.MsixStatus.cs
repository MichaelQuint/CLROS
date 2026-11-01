using OS.Std.Binary;

namespace OS.Drivers;
public enum MsixStatus : byte
{
    Uninitialized,
    Success,
    InvalidStorage,
    AbsentDevice,
    UnsupportedHeader,
    InvalidChain,
    MissingCapability,
    DuplicateCapability,
    TruncatedCapability,
    InvalidCapability,
    AlreadyEnabled,
    Overlap,
    InvalidBar,
    OutsideBar,
    InvalidAddress,
    InvalidAccess,
    ReadFailed,
    WriteFailed,
    ReadBackFailed,
    PendingInterrupt
}
