namespace OS.Drivers;

public readonly struct VirtqueueSplitRingPlanResult
{
    private VirtqueueSplitRingPlanResult(VirtqueueSplitRingPlanStatus status, ushort queueSize, VirtqueueSplitRingPlan plan)
    {
        IsInitialized = true;
        Status = status;
        QueueSize = queueSize;
        Plan = plan;
    }

    public bool IsInitialized
    {
        get;
    }
    public VirtqueueSplitRingPlanStatus Status
    {
        get;
    }
    public ushort QueueSize
    {
        get;
    }
    public VirtqueueSplitRingPlan Plan
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == VirtqueueSplitRingPlanStatus.Success;

    public static VirtqueueSplitRingPlanResult Success(VirtqueueSplitRingPlan plan)
    {
        return new VirtqueueSplitRingPlanResult(VirtqueueSplitRingPlanStatus.Success, plan.QueueSize, plan);
    }

    public static VirtqueueSplitRingPlanResult Fail(VirtqueueSplitRingPlanStatus status, ushort queueSize)
    {
        return new VirtqueueSplitRingPlanResult(status, queueSize, default);
    }
}
