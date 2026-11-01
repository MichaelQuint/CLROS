namespace OS.Drivers;
public enum VirtioNetQueueNotificationRegisterWindowBarrierExecutionStatus
{
    Success = 0,
    NotificationPlanFailed = 1,
    MemoryBarrierNotReady = 2,
    UnsupportedWriteWidth = 3,
    RegisterWindowPlanFailed = 4,
    AccessExecutionFailed = 5
}
