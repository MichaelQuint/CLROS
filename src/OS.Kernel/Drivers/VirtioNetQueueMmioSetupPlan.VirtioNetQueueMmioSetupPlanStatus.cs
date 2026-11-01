using OS.Std.Binary;

namespace OS.Drivers;
public enum VirtioNetQueueMmioSetupPlanStatus
{
    Success = 0,
    BringUpPlanFailed = 1,
    QueueUnavailable = 2,
    QueueSizeRejected = 3,
    InvalidPhysicalAddress = 4,
    PhysicalAddressOverflow = 5,
    MissingNotifyOffset = 6,
    CommandDestinationTooSmall = 7,
    UnsupportedNotificationDataFeature = 8
}
