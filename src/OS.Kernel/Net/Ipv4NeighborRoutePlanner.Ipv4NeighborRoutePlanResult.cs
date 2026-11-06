namespace OS.Net;

public readonly struct Ipv4NeighborRoutePlanResult
{
    private Ipv4NeighborRoutePlanResult(Ipv4NeighborRoutePlanStatus status, Ipv4NeighborRoutePlanAction action, MacAddress targetMac, int bytesWritten, ArpNeighborCacheStatus cacheStatus, ArpNeighborResolutionStatus arpStatus, PacketParseStatus packetStatus, int cacheCount, int cacheCapacity)
    {
        Status = status;
        Action = action;
        TargetMac = targetMac;
        BytesWritten = bytesWritten;
        CacheStatus = cacheStatus;
        ArpStatus = arpStatus;
        PacketStatus = packetStatus;
        CacheCount = cacheCount;
        CacheCapacity = cacheCapacity;
        IsInitialized = true;
    }

    public Ipv4NeighborRoutePlanStatus Status
    {
        get;
    }
    public bool IsInitialized
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == Ipv4NeighborRoutePlanStatus.Success;
    public Ipv4NeighborRoutePlanAction Action
    {
        get;
    }
    public MacAddress TargetMac
    {
        get;
    }
    public int BytesWritten
    {
        get;
    }
    public ArpNeighborCacheStatus CacheStatus
    {
        get;
    }
    public ArpNeighborResolutionStatus ArpStatus
    {
        get;
    }
    public PacketParseStatus PacketStatus
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

    public static Ipv4NeighborRoutePlanResult Cached(MacAddress targetMac, ArpNeighborCacheStatus cacheStatus, int cacheCount, int cacheCapacity)
    {
        return new Ipv4NeighborRoutePlanResult(Ipv4NeighborRoutePlanStatus.Success, Ipv4NeighborRoutePlanAction.UseCachedMac, targetMac, bytesWritten: 0, cacheStatus, ArpNeighborResolutionStatus.Success, PacketParseStatus.Success, cacheCount, cacheCapacity);
    }

    public static Ipv4NeighborRoutePlanResult RequestWritten(int bytesWritten, ArpNeighborCacheStatus cacheStatus, ArpNeighborResolutionStatus arpStatus, PacketParseStatus packetStatus, int cacheCount, int cacheCapacity)
    {
        return new Ipv4NeighborRoutePlanResult(Ipv4NeighborRoutePlanStatus.Success, Ipv4NeighborRoutePlanAction.SendArpRequest, default, bytesWritten, cacheStatus, arpStatus, packetStatus, cacheCount, cacheCapacity);
    }

    public static Ipv4NeighborRoutePlanResult Fail(Ipv4NeighborRoutePlanStatus status, ArpNeighborCacheStatus cacheStatus, ArpNeighborResolutionStatus arpStatus, PacketParseStatus packetStatus, int cacheCount, int cacheCapacity)
    {
        return new Ipv4NeighborRoutePlanResult(status, Ipv4NeighborRoutePlanAction.None, default, bytesWritten: 0, cacheStatus, arpStatus, packetStatus, cacheCount, cacheCapacity);
    }
}
