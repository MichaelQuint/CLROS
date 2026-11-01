namespace OS.Drivers;
public enum VirtioNetQueueNotificationPlanStatus
{
    Success = 0,
    QueueSetupFailed = 1,
    ReceivePublishFailed = 2,
    TransmitPublishFailed = 3,
    NotificationPlanFailed = 4
}
