using OS.Drivers;

namespace OS.Storage;

public enum VirtioBlockDescriptorReclaimPlanStatus
{
    Success = 0,
    PublishPlanFailed = 1,
    CompletionFailed = 2,
    OwnershipReclaimFailed = 3
}
