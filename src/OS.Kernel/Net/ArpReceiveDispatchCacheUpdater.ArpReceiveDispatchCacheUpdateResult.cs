namespace OS.Net;
public readonly struct ArpReceiveDispatchCacheUpdateResult
{
    private ArpReceiveDispatchCacheUpdateResult(ArpReceiveDispatchCacheUpdateStatus status, NetworkReceiveDispatchStatus dispatchStatus, ArpNeighborResolutionStatus arpStatus, ArpNeighborCacheStatus cacheStatus, PacketParseStatus packetStatus, ArpNeighborResolutionState arpState, MacAddress resolvedMac, int cacheCount, int cacheCapacity, bool updatedExisting)
    {
        IsInitialized = true;
        Status = status;
        DispatchStatus = dispatchStatus;
        ArpStatus = arpStatus;
        CacheStatus = cacheStatus;
        PacketStatus = packetStatus;
        ArpState = arpState;
        ResolvedMac = resolvedMac;
        CacheCount = cacheCount;
        CacheCapacity = cacheCapacity;
        UpdatedExisting = updatedExisting;
    }

    public ArpReceiveDispatchCacheUpdateStatus Status { get; }
    public bool IsInitialized { get; }
    public bool Succeeded => IsInitialized && Status == ArpReceiveDispatchCacheUpdateStatus.Success;
    public NetworkReceiveDispatchStatus DispatchStatus { get; }
    public ArpNeighborResolutionStatus ArpStatus { get; }
    public ArpNeighborCacheStatus CacheStatus { get; }
    public PacketParseStatus PacketStatus { get; }
    public ArpNeighborResolutionState ArpState { get; }
    public MacAddress ResolvedMac { get; }
    public int CacheCount { get; }
    public int CacheCapacity { get; }
    public bool UpdatedExisting { get; }

    public static ArpReceiveDispatchCacheUpdateResult Success(NetworkReceiveDispatchStatus dispatchStatus, ArpNeighborResolutionStatus arpStatus, ArpNeighborCacheStatus cacheStatus, PacketParseStatus packetStatus, ArpNeighborResolutionState arpState, MacAddress resolvedMac, int cacheCount, int cacheCapacity, bool updatedExisting)
    {
        return new ArpReceiveDispatchCacheUpdateResult(ArpReceiveDispatchCacheUpdateStatus.Success, dispatchStatus, arpStatus, cacheStatus, packetStatus, arpState, resolvedMac, cacheCount, cacheCapacity, updatedExisting);
    }

    public static ArpReceiveDispatchCacheUpdateResult Fail(ArpReceiveDispatchCacheUpdateStatus status, NetworkReceiveDispatchStatus dispatchStatus, ArpNeighborResolutionStatus arpStatus, ArpNeighborCacheStatus cacheStatus, PacketParseStatus packetStatus, ArpNeighborResolutionState arpState, MacAddress resolvedMac, int cacheCount, int cacheCapacity)
    {
        return new ArpReceiveDispatchCacheUpdateResult(status, dispatchStatus, arpStatus, cacheStatus, packetStatus, arpState, resolvedMac, cacheCount, cacheCapacity, updatedExisting: false);
    }
}
