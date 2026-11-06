using OS.Drivers;

namespace OS.Storage;

public enum VirtioBlockQueueMmioSetupRegisterWindowPlanStatus
{
    Success = 0,
    SetupPlanFailed = 1,
    CommandSourceTooSmall = 2,
    AccessPlanDestinationTooSmall = 3,
    UnsupportedCommandWidth = 4,
    RegisterWindowPlanFailed = 5
}
