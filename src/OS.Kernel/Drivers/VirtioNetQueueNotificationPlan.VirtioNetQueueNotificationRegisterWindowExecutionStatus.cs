namespace OS.Drivers;

public enum VirtioNetQueueNotificationRegisterWindowExecutionStatus
{
    Success = 0,
    NotificationPlanFailed = 1,
    UnsupportedWriteWidth = 2,
    RegisterWindowPlanFailed = 3,
    AccessExecutionFailed = 4
}
