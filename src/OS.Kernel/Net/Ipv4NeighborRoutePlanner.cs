namespace OS.Net;

public enum Ipv4NeighborRoutePlanStatus
{
    Success = 0,
    InvalidTargetAddress = 1,
    InvalidLocalAddress = 2,
    InvalidLocalMacAddress = 3,
    RequestBufferTooSmall = 4,
    RequestWriteFailed = 5
}

public enum Ipv4NeighborRoutePlanAction
{
    None = 0,
    UseCachedMac = 1,
    SendArpRequest = 2
}

public readonly struct Ipv4NeighborRoutePlanResult
{
    private Ipv4NeighborRoutePlanResult(
        Ipv4NeighborRoutePlanStatus status,
        Ipv4NeighborRoutePlanAction action,
        MacAddress targetMac,
        int bytesWritten,
        ArpNeighborCacheStatus cacheStatus,
        ArpNeighborResolutionStatus arpStatus,
        PacketParseStatus packetStatus,
        int cacheCount,
        int cacheCapacity)
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

    public Ipv4NeighborRoutePlanStatus Status { get; }

    public bool IsInitialized { get; }

    public bool Succeeded => IsInitialized && Status == Ipv4NeighborRoutePlanStatus.Success;

    public Ipv4NeighborRoutePlanAction Action { get; }

    public MacAddress TargetMac { get; }

    public int BytesWritten { get; }

    public ArpNeighborCacheStatus CacheStatus { get; }

    public ArpNeighborResolutionStatus ArpStatus { get; }

    public PacketParseStatus PacketStatus { get; }

    public int CacheCount { get; }

    public int CacheCapacity { get; }

    public static Ipv4NeighborRoutePlanResult Cached(
        MacAddress targetMac,
        ArpNeighborCacheStatus cacheStatus,
        int cacheCount,
        int cacheCapacity)
    {
        return new Ipv4NeighborRoutePlanResult(
            Ipv4NeighborRoutePlanStatus.Success,
            Ipv4NeighborRoutePlanAction.UseCachedMac,
            targetMac,
            bytesWritten: 0,
            cacheStatus,
            ArpNeighborResolutionStatus.Success,
            PacketParseStatus.Success,
            cacheCount,
            cacheCapacity);
    }

    public static Ipv4NeighborRoutePlanResult RequestWritten(
        int bytesWritten,
        ArpNeighborCacheStatus cacheStatus,
        ArpNeighborResolutionStatus arpStatus,
        PacketParseStatus packetStatus,
        int cacheCount,
        int cacheCapacity)
    {
        return new Ipv4NeighborRoutePlanResult(
            Ipv4NeighborRoutePlanStatus.Success,
            Ipv4NeighborRoutePlanAction.SendArpRequest,
            default,
            bytesWritten,
            cacheStatus,
            arpStatus,
            packetStatus,
            cacheCount,
            cacheCapacity);
    }

    public static Ipv4NeighborRoutePlanResult Fail(
        Ipv4NeighborRoutePlanStatus status,
        ArpNeighborCacheStatus cacheStatus,
        ArpNeighborResolutionStatus arpStatus,
        PacketParseStatus packetStatus,
        int cacheCount,
        int cacheCapacity)
    {
        return new Ipv4NeighborRoutePlanResult(
            status,
            Ipv4NeighborRoutePlanAction.None,
            default,
            bytesWritten: 0,
            cacheStatus,
            arpStatus,
            packetStatus,
            cacheCount,
            cacheCapacity);
    }
}

public static class Ipv4NeighborRoutePlanner
{
    public static Ipv4NeighborRoutePlanStatus TryPlan(
        ref ArpNeighborCache cache,
        MacAddress localMac,
        Ipv4Address localAddress,
        Ipv4Address targetAddress,
        Span<byte> arpRequestFrame,
        out Ipv4NeighborRoutePlanResult result)
    {
        ArpNeighborCacheStatus cacheStatus = cache.TryLookup(
            targetAddress,
            out ArpNeighborCacheLookupResult lookup);
        if (cacheStatus == ArpNeighborCacheStatus.InvalidAddress)
        {
            result = Ipv4NeighborRoutePlanResult.Fail(
                Ipv4NeighborRoutePlanStatus.InvalidTargetAddress,
                cacheStatus,
                ArpNeighborResolutionStatus.Success,
                PacketParseStatus.Success,
                lookup.Count,
                lookup.Capacity);
            return result.Status;
        }

        if (cacheStatus == ArpNeighborCacheStatus.Success)
        {
            result = Ipv4NeighborRoutePlanResult.Cached(
                lookup.Entry.MacAddress,
                cacheStatus,
                lookup.Count,
                lookup.Capacity);
            return result.Status;
        }

        if (localAddress == default)
        {
            result = Ipv4NeighborRoutePlanResult.Fail(
                Ipv4NeighborRoutePlanStatus.InvalidLocalAddress,
                cacheStatus,
                ArpNeighborResolutionStatus.Success,
                PacketParseStatus.Success,
                lookup.Count,
                lookup.Capacity);
            return result.Status;
        }

        if (localMac.IsZero || localMac.IsBroadcast)
        {
            result = Ipv4NeighborRoutePlanResult.Fail(
                Ipv4NeighborRoutePlanStatus.InvalidLocalMacAddress,
                cacheStatus,
                ArpNeighborResolutionStatus.Success,
                PacketParseStatus.Success,
                lookup.Count,
                lookup.Capacity);
            return result.Status;
        }

        ArpNeighborResolutionTransaction transaction =
            ArpNeighborResolutionTransaction.Create(localMac, localAddress, targetAddress);
        ArpNeighborResolutionStatus arpStatus = transaction.TryWriteRequest(
            arpRequestFrame,
            out ArpNeighborResolutionResult arpResult);
        if (arpStatus != ArpNeighborResolutionStatus.Success)
        {
            Ipv4NeighborRoutePlanStatus status =
                arpStatus == ArpNeighborResolutionStatus.BufferTooSmall
                    ? Ipv4NeighborRoutePlanStatus.RequestBufferTooSmall
                    : Ipv4NeighborRoutePlanStatus.RequestWriteFailed;

            result = Ipv4NeighborRoutePlanResult.Fail(
                status,
                cacheStatus,
                arpStatus,
                arpResult.PacketStatus,
                lookup.Count,
                lookup.Capacity);
            return result.Status;
        }

        result = Ipv4NeighborRoutePlanResult.RequestWritten(
            arpResult.BytesWritten,
            cacheStatus,
            arpStatus,
            arpResult.PacketStatus,
            lookup.Count,
            lookup.Capacity);
        return result.Status;
    }
}
