namespace OS.Kernel.Memory;

public readonly struct BootPageAllocatorStateInitResult
{
    private BootPageAllocatorStateInitResult(BootPageAllocatorStateStatus status, BootPageFreeListPlanStatus freeListPlanStatus, int requiredNodeCapacity, int availableNodeCapacity, int visitedNodeCount, int invalidNodeIndex, ulong pageSize, ulong totalFreePages, ulong totalFreeBytes, BootPageAllocatorState state)
    {
        Status = status;
        FreeListPlanStatus = freeListPlanStatus;
        RequiredNodeCapacity = requiredNodeCapacity;
        AvailableNodeCapacity = availableNodeCapacity;
        VisitedNodeCount = visitedNodeCount;
        InvalidNodeIndex = invalidNodeIndex;
        PageSize = pageSize;
        TotalFreePages = totalFreePages;
        TotalFreeBytes = totalFreeBytes;
        State = state;
    }

    public BootPageAllocatorStateStatus Status
    {
        get;
    }
    public bool Succeeded => Status == BootPageAllocatorStateStatus.Success && State.IsInitialized;
    public BootPageFreeListPlanStatus FreeListPlanStatus
    {
        get;
    }
    public int RequiredNodeCapacity
    {
        get;
    }
    public int AvailableNodeCapacity
    {
        get;
    }
    public int VisitedNodeCount
    {
        get;
    }
    public int InvalidNodeIndex
    {
        get;
    }
    public ulong PageSize
    {
        get;
    }
    public ulong TotalFreePages
    {
        get;
    }
    public ulong TotalFreeBytes
    {
        get;
    }
    public BootPageAllocatorState State
    {
        get;
    }

    internal static BootPageAllocatorStateInitResult Success(BootPageFreeListPlanResult freeListPlan, int requiredNodeCapacity, int availableNodeCapacity, int visitedNodeCount, ulong totalFreePages, ulong totalFreeBytes)
    {
        return new BootPageAllocatorStateInitResult(BootPageAllocatorStateStatus.Success, freeListPlan.Status, requiredNodeCapacity, availableNodeCapacity, visitedNodeCount, invalidNodeIndex: -1, freeListPlan.PageSize, totalFreePages, totalFreeBytes, new BootPageAllocatorState(isInitialized: true, freeListPlan.PageSize, freeListPlan.HeadNodeIndex, freeListPlan.FreeSegmentCount, totalFreePages, totalFreeBytes, allocatedPageCount: 0, allocatedByteCount: 0));
    }

    internal static BootPageAllocatorStateInitResult Fail(BootPageAllocatorStateStatus status, BootPageFreeListPlanResult freeListPlan, int requiredNodeCapacity, int availableNodeCapacity, int visitedNodeCount, int invalidNodeIndex, ulong totalFreePages, ulong totalFreeBytes)
    {
        return new BootPageAllocatorStateInitResult(status, freeListPlan.Status, requiredNodeCapacity, availableNodeCapacity, visitedNodeCount, invalidNodeIndex, freeListPlan.PageSize, totalFreePages, totalFreeBytes, default);
    }
}
