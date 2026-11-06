using OS.Drivers;

namespace OS.Storage;

public enum VirtioBlockDescriptorPublishPlanStatus
{
    Success = 0,
    FreeListFailed = 1,
    LifecyclePublishFailed = 2,
    OwnershipMarkFailed = 3
}
