using OS.Boot;

namespace OS.Kernel.Memory;

public readonly struct BootMemoryRegionPlanResult
{
    private BootMemoryRegionPlanResult(BootMemoryRegionPlanStatus status, int sourceRegionCount, int usableRangeCount, int reservedRangeCount, int requiredUsableRangeCapacity, int availableUsableRangeCapacity, int requiredReservedRangeCapacity, int availableReservedRangeCapacity, int invalidRegionIndex, ulong totalPages, ulong totalBytes, ulong usablePages, ulong usableBytes, ulong reservedPages, ulong reservedBytes)
    {
        Status = status;
        SourceRegionCount = sourceRegionCount;
        UsableRangeCount = usableRangeCount;
        ReservedRangeCount = reservedRangeCount;
        RequiredUsableRangeCapacity = requiredUsableRangeCapacity;
        AvailableUsableRangeCapacity = availableUsableRangeCapacity;
        RequiredReservedRangeCapacity = requiredReservedRangeCapacity;
        AvailableReservedRangeCapacity = availableReservedRangeCapacity;
        InvalidRegionIndex = invalidRegionIndex;
        TotalPages = totalPages;
        TotalBytes = totalBytes;
        UsablePages = usablePages;
        UsableBytes = usableBytes;
        ReservedPages = reservedPages;
        ReservedBytes = reservedBytes;
    }

    public BootMemoryRegionPlanStatus Status
    {
        get;
    }
    public bool Succeeded => Status == BootMemoryRegionPlanStatus.Success;
    public int SourceRegionCount
    {
        get;
    }
    public int UsableRangeCount
    {
        get;
    }
    public int ReservedRangeCount
    {
        get;
    }
    public int RequiredUsableRangeCapacity
    {
        get;
    }
    public int AvailableUsableRangeCapacity
    {
        get;
    }
    public int RequiredReservedRangeCapacity
    {
        get;
    }
    public int AvailableReservedRangeCapacity
    {
        get;
    }
    public int InvalidRegionIndex
    {
        get;
    }
    public ulong TotalPages
    {
        get;
    }
    public ulong TotalBytes
    {
        get;
    }
    public ulong UsablePages
    {
        get;
    }
    public ulong UsableBytes
    {
        get;
    }
    public ulong ReservedPages
    {
        get;
    }
    public ulong ReservedBytes
    {
        get;
    }

    internal static BootMemoryRegionPlanResult Success(int sourceRegionCount, int usableRangeCount, int reservedRangeCount, int availableUsableRangeCapacity, int availableReservedRangeCapacity, ulong totalPages, ulong totalBytes, ulong usablePages, ulong usableBytes, ulong reservedPages, ulong reservedBytes)
    {
        return new BootMemoryRegionPlanResult(BootMemoryRegionPlanStatus.Success, sourceRegionCount, usableRangeCount, reservedRangeCount, usableRangeCount, availableUsableRangeCapacity, reservedRangeCount, availableReservedRangeCapacity, invalidRegionIndex: -1, totalPages, totalBytes, usablePages, usableBytes, reservedPages, reservedBytes);
    }

    internal static BootMemoryRegionPlanResult Fail(BootMemoryRegionPlanStatus status, int sourceRegionCount, int usableRangeCount, int reservedRangeCount, int availableUsableRangeCapacity, int availableReservedRangeCapacity, int invalidRegionIndex, ulong totalPages, ulong totalBytes, ulong usablePages, ulong usableBytes, ulong reservedPages, ulong reservedBytes)
    {
        return new BootMemoryRegionPlanResult(status, sourceRegionCount, usableRangeCount: 0, reservedRangeCount: 0, requiredUsableRangeCapacity: usableRangeCount, availableUsableRangeCapacity, requiredReservedRangeCapacity: reservedRangeCount, availableReservedRangeCapacity, invalidRegionIndex, totalPages, totalBytes, usablePages, usableBytes, reservedPages, reservedBytes);
    }
}
