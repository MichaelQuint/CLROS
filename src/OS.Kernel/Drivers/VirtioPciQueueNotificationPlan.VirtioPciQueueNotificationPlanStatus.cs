namespace OS.Drivers;

public enum VirtioPciQueueNotificationPlanStatus
{
    Success = 0,
    InvalidNotifyAperture = 1,
    InvalidQueueIndex = 2,
    UnsupportedNotificationDataFeature = 3,
    UnsupportedNotificationConfigDataFeature = 4,
    QueueNotEnabled = 5,
    NoNewlyPublishedDescriptors = 6,
    NotifyOffsetOverflow = 7,
    NotifyRangeOutsideAperture = 8
}
