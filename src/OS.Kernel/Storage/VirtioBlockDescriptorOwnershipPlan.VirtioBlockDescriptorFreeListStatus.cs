using OS.Drivers;

namespace OS.Storage;

public enum VirtioBlockDescriptorFreeListStatus
{
    Success = 0,
    QueuePlanFailed = 1,
    StateTableTooSmall = 2,
    InvalidDescriptorState = 3,
    NoFreeDescriptorTriple = 4
}
