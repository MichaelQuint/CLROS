using OS.Std.Binary;

namespace OS.Drivers;
public enum VirtioPciBarRegisterWindowLiveAccessorStatus
{
    Success = 0,
    MissingAccessPlan = 1,
    AccessorNotReady = 2,
    UnsupportedReadWidth = 3,
    UnsupportedWriteWidth = 4,
    RegisterReadFailed = 5,
    RegisterWriteFailed = 6
}
