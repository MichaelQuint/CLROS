using OS.Boot;
using OS.Kernel.Diagnostics;

namespace OS.Kernel.Memory;
public readonly struct BootPageAllocatorReadinessComposeResult
{
    private BootPageAllocatorReadinessComposeResult(BootPageAllocatorReadinessComposeStatus status, BootMemoryRegionPlanResult regionPlan, BootPageAllocatorPlanResult allocatorPlan, BootPageFreeListPlanResult freeListPlan, BootPageAllocatorStateInitResult allocatorState, bool bootReadinessInvoked, bool bootReadinessSucceeded)
    {
        Status = status;
        RegionPlan = regionPlan;
        AllocatorPlan = allocatorPlan;
        FreeListPlan = freeListPlan;
        AllocatorState = allocatorState;
        BootReadinessInvoked = bootReadinessInvoked;
        BootReadinessSucceeded = bootReadinessSucceeded;
    }

    public BootPageAllocatorReadinessComposeStatus Status { get; }
    public bool Succeeded => Status == BootPageAllocatorReadinessComposeStatus.Success;
    public BootMemoryRegionPlanResult RegionPlan { get; }
    public BootPageAllocatorPlanResult AllocatorPlan { get; }
    public BootPageFreeListPlanResult FreeListPlan { get; }
    public BootPageAllocatorStateInitResult AllocatorState { get; }
    public bool BootReadinessInvoked { get; }
    public bool BootReadinessSucceeded { get; }

    public static BootPageAllocatorReadinessComposeResult Create(BootPageAllocatorReadinessComposeStatus status, BootMemoryRegionPlanResult regionPlan, BootPageAllocatorPlanResult allocatorPlan, BootPageFreeListPlanResult freeListPlan, BootPageAllocatorStateInitResult allocatorState, bool bootReadinessInvoked, bool bootReadinessSucceeded)
    {
        return new BootPageAllocatorReadinessComposeResult(status, regionPlan, allocatorPlan, freeListPlan, allocatorState, bootReadinessInvoked, bootReadinessSucceeded);
    }
}
