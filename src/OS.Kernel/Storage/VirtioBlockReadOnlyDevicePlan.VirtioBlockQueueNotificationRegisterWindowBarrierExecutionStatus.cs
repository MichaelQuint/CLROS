using OS.Drivers;

namespace OS.Storage;

public enum VirtioBlockQueueNotificationRegisterWindowBarrierExecutionStatus
{
    Success = 0,
    PlanFailed = 1,
    MemoryBarrierNotReady = 2,
    AccessExecutionFailed = 3
}
