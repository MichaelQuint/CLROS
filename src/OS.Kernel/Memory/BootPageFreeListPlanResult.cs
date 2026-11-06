namespace OS.Kernel.Memory;

public readonly struct BootPageFreeListPlanResult
{
    private BootPageFreeListPlanResult(BootPageFreeListPlanStatus status, BootPageAllocatorPlanStatus allocatorPlanStatus, int freeSegmentCount, int requiredNodeCapacity, int availableNodeCapacity, int availableSegmentCapacity, int invalidSegmentIndex, int headNodeIndex, ulong pageSize, ulong totalFreePages, ulong totalFreeBytes)
    {
        Status = status;
        AllocatorPlanStatus = allocatorPlanStatus;
        FreeSegmentCount = freeSegmentCount;
        RequiredNodeCapacity = requiredNodeCapacity;
        AvailableNodeCapacity = availableNodeCapacity;
        AvailableSegmentCapacity = availableSegmentCapacity;
        InvalidSegmentIndex = invalidSegmentIndex;
        HeadNodeIndex = headNodeIndex;
        PageSize = pageSize;
        TotalFreePages = totalFreePages;
        TotalFreeBytes = totalFreeBytes;
    }

    public BootPageFreeListPlanStatus Status
    {
        get;
    }
    public bool Succeeded => Status == BootPageFreeListPlanStatus.Success;
    public BootPageAllocatorPlanStatus AllocatorPlanStatus
    {
        get;
    }
    public int FreeSegmentCount
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
    public int AvailableSegmentCapacity
    {
        get;
    }
    public int InvalidSegmentIndex
    {
        get;
    }
    public int HeadNodeIndex
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

    internal static BootPageFreeListPlanResult Success(BootPageAllocatorPlanStatus allocatorPlanStatus, int freeSegmentCount, int availableNodeCapacity, int availableSegmentCapacity, ulong pageSize, ulong totalFreePages, ulong totalFreeBytes)
    {
        return new BootPageFreeListPlanResult(BootPageFreeListPlanStatus.Success, allocatorPlanStatus, freeSegmentCount, requiredNodeCapacity: freeSegmentCount, availableNodeCapacity, availableSegmentCapacity, invalidSegmentIndex: -1, headNodeIndex: freeSegmentCount == 0 ? -1 : 0, pageSize, totalFreePages, totalFreeBytes);
    }

    internal static BootPageFreeListPlanResult Fail(BootPageFreeListPlanStatus status, BootPageAllocatorPlanStatus allocatorPlanStatus, int freeSegmentCount, int requiredNodeCapacity, int availableNodeCapacity, int availableSegmentCapacity, int invalidSegmentIndex, ulong pageSize, ulong totalFreePages, ulong totalFreeBytes)
    {
        return new BootPageFreeListPlanResult(status, allocatorPlanStatus, freeSegmentCount, requiredNodeCapacity, availableNodeCapacity, availableSegmentCapacity, invalidSegmentIndex, headNodeIndex: -1, pageSize, totalFreePages, totalFreeBytes);
    }
}
