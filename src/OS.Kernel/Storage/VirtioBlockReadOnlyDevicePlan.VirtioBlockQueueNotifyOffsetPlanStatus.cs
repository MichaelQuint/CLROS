using OS.Drivers;

namespace OS.Storage;

public enum VirtioBlockQueueNotifyOffsetPlanStatus
{
    Success = 0,
    NotificationMetadataFailed = 1,
    RelativeOffsetOverflow = 2
}
