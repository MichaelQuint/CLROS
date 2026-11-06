using OS.Drivers;

namespace OS.Storage;

public enum VirtioBlockQueueNotificationRegisterWindowExecutionStatus
{
    Success = 0,
    PlanFailed = 1,
    AccessExecutionFailed = 2
}
