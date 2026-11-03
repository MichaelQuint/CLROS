using OS.Boot;
using OS.Kernel.Diagnostics;

namespace OS.Kernel.Memory;

public static class BootPageAllocatorReadinessComposer
{
    public static BootPageAllocatorReadinessComposeResult ComposeAndRun(ReadOnlySpan<BootMemoryRegionRecord> sourceRegions, ulong pageSize, Span<BootMemoryUsableRange> usableRanges, Span<BootMemoryReservedRange> reservedRanges, Span<BootPageFreeSegment> freeSegments, Span<BootPageFreeListNode> nodes, ISerialSink serial, BootEventRingBuffer events)
    {
        BootMemoryRegionPlanResult regionPlan = BootMemoryRegionPlanner.Plan(sourceRegions, usableRanges, reservedRanges);
        if (!regionPlan.Succeeded)
        {
            return BootPageAllocatorReadinessComposeResult.Create(BootPageAllocatorReadinessComposeStatus.MemoryRegionPlanFailed, regionPlan, default, default, default, bootReadinessInvoked: false, bootReadinessSucceeded: false);
        }

        BootPageAllocatorPlanResult allocatorPlan = BootPageAllocatorPlanner.Plan(pageSize, usableRanges.Slice(0, regionPlan.UsableRangeCount), reservedRanges.Slice(0, regionPlan.ReservedRangeCount), freeSegments);
        BootPageFreeListPlanResult freeListPlan = BootPageFreeListBuilder.Build(allocatorPlan, freeSegments, nodes);
        BootPageAllocatorStateInitResult allocatorState = BootPageAllocatorStateInitializer.Initialize(freeListPlan, nodes);
        BootResult bootReadiness = BootSequence.RunBootPageAllocatorReadiness(allocatorState, serial, events);
        BootPageAllocatorReadinessComposeStatus status = GetStatus(allocatorPlan, freeListPlan, allocatorState, bootReadiness);
        return BootPageAllocatorReadinessComposeResult.Create(status, regionPlan, allocatorPlan, freeListPlan, allocatorState, bootReadinessInvoked: true, bootReadinessSucceeded: bootReadiness.Succeeded);
    }

    private static BootPageAllocatorReadinessComposeStatus GetStatus(BootPageAllocatorPlanResult allocatorPlan, BootPageFreeListPlanResult freeListPlan, BootPageAllocatorStateInitResult allocatorState, BootResult bootReadiness)
    {
        if (!allocatorPlan.Succeeded)
        {
            return BootPageAllocatorReadinessComposeStatus.AllocatorPlanFailed;
        }

        if (!freeListPlan.Succeeded)
        {
            return BootPageAllocatorReadinessComposeStatus.FreeListPlanFailed;
        }

        if (!allocatorState.Succeeded)
        {
            return BootPageAllocatorReadinessComposeStatus.StateInitializationFailed;
        }

        return bootReadiness.Succeeded ? BootPageAllocatorReadinessComposeStatus.Success : BootPageAllocatorReadinessComposeStatus.BootReadinessFailed;
    }
}
