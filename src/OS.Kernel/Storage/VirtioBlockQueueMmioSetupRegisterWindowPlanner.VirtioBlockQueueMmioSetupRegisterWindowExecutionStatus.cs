using OS.Drivers;

namespace OS.Storage;

public enum VirtioBlockQueueMmioSetupRegisterWindowExecutionStatus
{
    Success = 0,
    SetupPlanFailed = 1,
    CommandSourceTooSmall = 2,
    UnsupportedCommandWidth = 3,
    RegisterWindowPlanFailed = 4,
    AccessExecutionFailed = 5,
    RegisterReadMismatch = 6
}
