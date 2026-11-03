using OS.Std.Binary;

namespace OS.Drivers;

public enum VirtioNetQueueMmioSetupRegisterWindowPlanStatus
{
    Success = 0,
    SetupPlanFailed = 1,
    CommandSourceTooSmall = 2,
    AccessPlanDestinationTooSmall = 3,
    UnsupportedCommandWidth = 4,
    RegisterWindowPlanFailed = 5
}
