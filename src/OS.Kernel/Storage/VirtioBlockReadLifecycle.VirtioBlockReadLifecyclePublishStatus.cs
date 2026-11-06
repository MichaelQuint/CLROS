using OS.Drivers;

namespace OS.Storage;

public enum VirtioBlockReadLifecyclePublishStatus
{
    Success = 0,
    ReadPlanFailed = 1,
    QueuePlanFailed = 2,
    DescriptorPlanFailed = 3,
    DescriptorWriteFailed = 4
}
