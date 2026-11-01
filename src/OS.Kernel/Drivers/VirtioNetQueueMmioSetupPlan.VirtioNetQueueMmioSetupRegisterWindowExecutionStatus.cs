using OS.Std.Binary;

namespace OS.Drivers;
public enum VirtioNetQueueMmioSetupRegisterWindowExecutionStatus
{
    Success = 0,
    SetupPlanFailed = 1,
    CommandSourceTooSmall = 2,
    UnsupportedCommandWidth = 3,
    RegisterWindowPlanFailed = 4,
    AccessExecutionFailed = 5,
    RegisterReadMismatch = 6
}
