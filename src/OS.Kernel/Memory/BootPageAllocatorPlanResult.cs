namespace OS.Kernel.Memory;

public readonly struct BootPageAllocatorPlanResult
{
    private BootPageAllocatorPlanResult(BootPageAllocatorPlanStatus status, int usableRangeCount, int reservedRangeCount, int freeSegmentCount, int requiredFreeSegmentCapacity, int availableFreeSegmentCapacity, int invalidUsableRangeIndex, int invalidReservedRangeIndex, ulong pageSize, ulong totalFreePages, ulong totalFreeBytes)
    {
        Status = status;
        UsableRangeCount = usableRangeCount;
        ReservedRangeCount = reservedRangeCount;
        FreeSegmentCount = freeSegmentCount;
        RequiredFreeSegmentCapacity = requiredFreeSegmentCapacity;
        AvailableFreeSegmentCapacity = availableFreeSegmentCapacity;
        InvalidUsableRangeIndex = invalidUsableRangeIndex;
        InvalidReservedRangeIndex = invalidReservedRangeIndex;
        PageSize = pageSize;
        TotalFreePages = totalFreePages;
        TotalFreeBytes = totalFreeBytes;
    }

    public BootPageAllocatorPlanStatus Status
    {
        get;
    }
    public bool Succeeded => Status == BootPageAllocatorPlanStatus.Success;
    public int UsableRangeCount
    {
        get;
    }
    public int ReservedRangeCount
    {
        get;
    }
    public int FreeSegmentCount
    {
        get;
    }
    public int RequiredFreeSegmentCapacity
    {
        get;
    }
    public int AvailableFreeSegmentCapacity
    {
        get;
    }
    public int InvalidUsableRangeIndex
    {
        get;
    }
    public int InvalidReservedRangeIndex
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

    internal static BootPageAllocatorPlanResult Success(int usableRangeCount, int reservedRangeCount, int availableFreeSegmentCapacity, ulong pageSize, ulong totalFreePages, ulong totalFreeBytes)
    {
        return new BootPageAllocatorPlanResult(BootPageAllocatorPlanStatus.Success, usableRangeCount, reservedRangeCount, freeSegmentCount: usableRangeCount, requiredFreeSegmentCapacity: usableRangeCount, availableFreeSegmentCapacity, invalidUsableRangeIndex: -1, invalidReservedRangeIndex: -1, pageSize, totalFreePages, totalFreeBytes);
    }

    internal static BootPageAllocatorPlanResult Fail(BootPageAllocatorPlanStatus status, int usableRangeCount, int reservedRangeCount, int requiredFreeSegmentCapacity, int availableFreeSegmentCapacity, int invalidUsableRangeIndex, int invalidReservedRangeIndex, ulong pageSize, ulong totalFreePages, ulong totalFreeBytes)
    {
        return new BootPageAllocatorPlanResult(status, usableRangeCount, reservedRangeCount, freeSegmentCount: 0, requiredFreeSegmentCapacity, availableFreeSegmentCapacity, invalidUsableRangeIndex, invalidReservedRangeIndex, pageSize, totalFreePages, totalFreeBytes);
    }
}
