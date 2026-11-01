using OS.Boot;
using OS.Kernel.Diagnostics;

namespace OS.Kernel.Memory;
public enum BootPageAllocatorReadinessComposeStatus
{
    Success = 0,
    MemoryRegionPlanFailed = 1,
    AllocatorPlanFailed = 2,
    FreeListPlanFailed = 3,
    StateInitializationFailed = 4,
    BootReadinessFailed = 5
}
