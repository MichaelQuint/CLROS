using OS.Drivers;

namespace OS.Storage;

public enum VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutionStatus
{
    Success = 0,
    SetupPlanFailed = 1,
    CommandSourceTooSmall = 2,
    UnsupportedCommandWidth = 3,
    RegisterWindowPlanFailed = 4,
    MemoryBarrierNotReady = 5,
    AccessExecutionFailed = 6,
    RegisterReadMismatch = 7
}
