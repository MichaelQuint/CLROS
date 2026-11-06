using OS.Drivers;

namespace OS.Storage;

public enum VirtioBlockQueueMmioSetupPlanStatus
{
    Success = 0,
    DescriptorNotReady = 1,
    NotBlockDevice = 2,
    QueuePlanFailed = 3,
    QueueUnavailable = 4,
    QueueSizeRejected = 5,
    InvalidPhysicalAddress = 6,
    PhysicalAddressOverflow = 7,
    MissingNotifyOffset = 8,
    CommandDestinationTooSmall = 9
}
