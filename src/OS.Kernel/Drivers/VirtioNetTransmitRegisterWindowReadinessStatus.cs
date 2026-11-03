namespace OS.Drivers;

public enum VirtioNetTransmitRegisterWindowReadinessStatus
{
    Success = 0,
    QueueSetupPlanFailed = 1,
    QueueSetupWindowExecutionFailed = 2,
    QueueSetupWindowExecutionMismatch = 3,
    TransmitPublishFailed = 4,
    NotificationPlanFailed = 5,
    BarrierNotReady = 6,
    NotificationWindowExecutionFailed = 7
}
