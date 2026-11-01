namespace OS.Drivers;
public enum VirtioNetQueueNotificationExecutionStatus
{
    Success = 0,
    NotificationPlanFailed = 1,
    MemoryBarrierNotReady = 2,
    UnsupportedWriteWidth = 3,
    SinkNotReady = 4,
    SinkRangeMismatch = 5,
    SinkWriteFailed = 6
}
