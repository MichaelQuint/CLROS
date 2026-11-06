namespace OS.Drivers;

public enum VirtioNetReceiveReadinessStatus
{
    Success = 0,
    QueueSetupPlanFailed = 1,
    QueueSetupWindowExecutionFailed = 2,
    QueueSetupWindowExecutionMismatch = 3,
    ReceivePublishFailed = 4,
    NotificationPlanFailed = 5,
    BarrierNotReady = 6,
    NotificationWindowExecutionFailed = 7
}
