namespace OS.Drivers;

public readonly struct EnaAdminQueueLayoutPlanResult
{
    private EnaAdminQueueLayoutPlanResult(bool isInitialized, EnaAdminQueueLayoutPlanStatus status, uint adminQueueDepth, uint adminCompletionQueueDepth, uint adminQueueEntrySize, uint adminCompletionQueueEntrySize, uint baseAlignment, uint memoryBudgetBytes, bool requiresContiguousMemory, uint totalRequiredBytes, EnaAdminQueueLayoutPlan plan)
    {
        IsInitialized = isInitialized;
        Status = status;
        AdminQueueDepth = adminQueueDepth;
        AdminCompletionQueueDepth = adminCompletionQueueDepth;
        AdminQueueEntrySize = adminQueueEntrySize;
        AdminCompletionQueueEntrySize = adminCompletionQueueEntrySize;
        BaseAlignment = baseAlignment;
        MemoryBudgetBytes = memoryBudgetBytes;
        RequiresContiguousMemory = requiresContiguousMemory;
        TotalRequiredBytes = totalRequiredBytes;
        Plan = plan;
    }

    public EnaAdminQueueLayoutPlanStatus Status
    {
        get;
    }
    public uint AdminQueueDepth
    {
        get;
    }
    public uint AdminCompletionQueueDepth
    {
        get;
    }
    public uint AdminQueueEntrySize
    {
        get;
    }
    public uint AdminCompletionQueueEntrySize
    {
        get;
    }
    public uint BaseAlignment
    {
        get;
    }
    public uint MemoryBudgetBytes
    {
        get;
    }
    public bool RequiresContiguousMemory
    {
        get;
    }
    public uint TotalRequiredBytes
    {
        get;
    }
    public EnaAdminQueueLayoutPlan Plan
    {
        get;
    }
    public bool IsInitialized
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == EnaAdminQueueLayoutPlanStatus.Success;

    internal static EnaAdminQueueLayoutPlanResult Success(EnaAdminQueueLayoutPlan plan)
    {
        return new EnaAdminQueueLayoutPlanResult(isInitialized: true, EnaAdminQueueLayoutPlanStatus.Success, plan.AdminQueue.Depth, plan.AdminCompletionQueue.Depth, plan.AdminQueue.EntrySize, plan.AdminCompletionQueue.EntrySize, plan.BaseAlignment, plan.MemoryBudgetBytes, plan.RequiresContiguousMemory, plan.TotalRequiredBytes, plan);
    }

    internal static EnaAdminQueueLayoutPlanResult Fail(EnaAdminQueueLayoutPlanStatus status, uint adminQueueDepth, uint adminCompletionQueueDepth, uint adminQueueEntrySize, uint adminCompletionQueueEntrySize, uint baseAlignment, uint memoryBudgetBytes, bool requiresContiguousMemory, uint totalRequiredBytes = 0)
    {
        return new EnaAdminQueueLayoutPlanResult(isInitialized: true, status, adminQueueDepth, adminCompletionQueueDepth, adminQueueEntrySize, adminCompletionQueueEntrySize, baseAlignment, memoryBudgetBytes, requiresContiguousMemory, totalRequiredBytes, default);
    }
}
