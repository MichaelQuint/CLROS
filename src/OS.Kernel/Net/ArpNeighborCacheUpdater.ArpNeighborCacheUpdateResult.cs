namespace OS.Net;

public readonly struct ArpNeighborCacheUpdateResult
{
    private ArpNeighborCacheUpdateResult(ArpNeighborCacheUpdateStatus status, ArpNeighborResolutionStatus arpStatus, ArpNeighborCacheStatus cacheStatus, PacketParseStatus packetStatus, ArpNeighborResolutionState arpState, MacAddress resolvedMac, int cacheCount, int cacheCapacity, bool updatedExisting)
    {
        IsInitialized = true;
        Status = status;
        ArpStatus = arpStatus;
        CacheStatus = cacheStatus;
        PacketStatus = packetStatus;
        ArpState = arpState;
        ResolvedMac = resolvedMac;
        CacheCount = cacheCount;
        CacheCapacity = cacheCapacity;
        UpdatedExisting = updatedExisting;
    }

    public bool IsInitialized
    {
        get;
    }
    public ArpNeighborCacheUpdateStatus Status
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == ArpNeighborCacheUpdateStatus.Success;
    public ArpNeighborResolutionStatus ArpStatus
    {
        get;
    }
    public ArpNeighborCacheStatus CacheStatus
    {
        get;
    }
    public PacketParseStatus PacketStatus
    {
        get;
    }
    public ArpNeighborResolutionState ArpState
    {
        get;
    }
    public MacAddress ResolvedMac
    {
        get;
    }
    public int CacheCount
    {
        get;
    }
    public int CacheCapacity
    {
        get;
    }
    public bool UpdatedExisting
    {
        get;
    }

    public static ArpNeighborCacheUpdateResult Success(ArpNeighborResolutionStatus arpStatus, ArpNeighborCacheStatus cacheStatus, PacketParseStatus packetStatus, ArpNeighborResolutionState arpState, MacAddress resolvedMac, int cacheCount, int cacheCapacity, bool updatedExisting)
    {
        return new ArpNeighborCacheUpdateResult(ArpNeighborCacheUpdateStatus.Success, arpStatus, cacheStatus, packetStatus, arpState, resolvedMac, cacheCount, cacheCapacity, updatedExisting);
    }

    public static ArpNeighborCacheUpdateResult Fail(ArpNeighborCacheUpdateStatus status, ArpNeighborResolutionStatus arpStatus, ArpNeighborCacheStatus cacheStatus, PacketParseStatus packetStatus, ArpNeighborResolutionState arpState, MacAddress resolvedMac, int cacheCount, int cacheCapacity)
    {
        return new ArpNeighborCacheUpdateResult(status, arpStatus, cacheStatus, packetStatus, arpState, resolvedMac, cacheCount, cacheCapacity, updatedExisting: false);
    }
}
