using OS.Std.Binary;

namespace OS.Drivers;

public enum VirtioNetQueueMmioSetupExecutionStatus
{
    Success = 0,
    SetupPlanFailed = 1,
    CommandSourceTooSmall = 2,
    AccessorNotReady = 3,
    UnsupportedCommandWidth = 4,
    RegisterReadFailed = 5,
    RegisterReadMismatch = 6,
    RegisterWriteFailed = 7
}
