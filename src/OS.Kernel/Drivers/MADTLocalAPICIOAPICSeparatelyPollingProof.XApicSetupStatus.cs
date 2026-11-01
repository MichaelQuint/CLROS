using OS.Std.Binary;

namespace OS.Drivers;
public enum XApicSetupStatus : byte
{
    Uninitialized,
    Success,
    InvalidAccess,
    InvalidCpu,
    InvalidTopology,
    ReadFailed,
    WriteFailed,
    UnsupportedHardware,
    ReadBackFailed,
    PendingInterrupt
}
