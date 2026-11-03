namespace OS.Net;

public readonly struct ArpNeighborCacheMutationResult
{
    private ArpNeighborCacheMutationResult(ArpNeighborCacheStatus status, int count, int capacity, int removedCount, bool updatedExisting)
    {
        IsInitialized = true;
        Status = status;
        Count = count;
        Capacity = capacity;
        RemovedCount = removedCount;
        UpdatedExisting = updatedExisting;
    }

    public bool IsInitialized
    {
        get;
    }
    public ArpNeighborCacheStatus Status
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == ArpNeighborCacheStatus.Success;
    public int Count
    {
        get;
    }
    public int Capacity
    {
        get;
    }
    public int RemovedCount
    {
        get;
    }
    public bool UpdatedExisting
    {
        get;
    }

    public static ArpNeighborCacheMutationResult Success(int count, int capacity, int removedCount, bool updatedExisting)
    {
        return new ArpNeighborCacheMutationResult(ArpNeighborCacheStatus.Success, count, capacity, removedCount, updatedExisting);
    }

    public static ArpNeighborCacheMutationResult Fail(ArpNeighborCacheStatus status, int count, int capacity)
    {
        return new ArpNeighborCacheMutationResult(status, count, capacity, removedCount: 0, updatedExisting: false);
    }
}
