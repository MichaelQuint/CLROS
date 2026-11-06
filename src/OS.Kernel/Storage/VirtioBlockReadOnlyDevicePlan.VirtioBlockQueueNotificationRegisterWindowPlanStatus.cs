using OS.Drivers;

namespace OS.Storage;

public enum VirtioBlockQueueNotificationRegisterWindowPlanStatus
{
    Success = 0,
    NotifyOffsetPlanFailed = 1,
    UnsupportedWriteWidth = 2,
    RegisterWindowPlanFailed = 3
}
