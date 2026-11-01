namespace OS.Drivers;
public enum VirtioNetTransmitReadinessStatus
{
    Success = 0,
    QueueSetupPlanFailed = 1,
    QueueSetupExecutionFailed = 2,
    QueueSetupExecutionMismatch = 3,
    TransmitPublishFailed = 4,
    NotificationPlanFailed = 5,
    BarrierNotReady = 6,
    NotificationExecutionFailed = 7
}
