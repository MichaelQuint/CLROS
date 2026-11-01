namespace OS.Net;
public readonly struct ArpNeighborCacheLookupResult
{
    private ArpNeighborCacheLookupResult(ArpNeighborCacheStatus status, ArpNeighborCacheEntry entry, int count, int capacity)
    {
        IsInitialized = true;
        Status = status;
        Entry = entry;
        Count = count;
        Capacity = capacity;
    }

    public bool IsInitialized { get; }
    public ArpNeighborCacheStatus Status { get; }
    public bool Succeeded => IsInitialized && Status == ArpNeighborCacheStatus.Success;
    public ArpNeighborCacheEntry Entry { get; }
    public int Count { get; }
    public int Capacity { get; }

    public static ArpNeighborCacheLookupResult Success(ArpNeighborCacheEntry entry, int count, int capacity)
    {
        return new ArpNeighborCacheLookupResult(ArpNeighborCacheStatus.Success, entry, count, capacity);
    }

    public static ArpNeighborCacheLookupResult Fail(ArpNeighborCacheStatus status, int count, int capacity)
    {
        return new ArpNeighborCacheLookupResult(status, default, count, capacity);
    }
}
