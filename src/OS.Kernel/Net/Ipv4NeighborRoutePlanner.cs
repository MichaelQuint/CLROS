namespace OS.Net;
public static class Ipv4NeighborRoutePlanner
{
    public static Ipv4NeighborRoutePlanStatus TryPlan(ref ArpNeighborCache cache, MacAddress localMac, Ipv4Address localAddress, Ipv4Address targetAddress, Span<byte> arpRequestFrame, out Ipv4NeighborRoutePlanResult result)
    {
        ArpNeighborCacheStatus cacheStatus = cache.TryLookup(targetAddress, out ArpNeighborCacheLookupResult lookup);
        if (cacheStatus == ArpNeighborCacheStatus.InvalidAddress)
        {
            result = Ipv4NeighborRoutePlanResult.Fail(Ipv4NeighborRoutePlanStatus.InvalidTargetAddress, cacheStatus, ArpNeighborResolutionStatus.Success, PacketParseStatus.Success, lookup.Count, lookup.Capacity);
            return result.Status;
        }

        if (cacheStatus == ArpNeighborCacheStatus.Success)
        {
            result = Ipv4NeighborRoutePlanResult.Cached(lookup.Entry.MacAddress, cacheStatus, lookup.Count, lookup.Capacity);
            return result.Status;
        }

        if (localAddress == default)
        {
            result = Ipv4NeighborRoutePlanResult.Fail(Ipv4NeighborRoutePlanStatus.InvalidLocalAddress, cacheStatus, ArpNeighborResolutionStatus.Success, PacketParseStatus.Success, lookup.Count, lookup.Capacity);
            return result.Status;
        }

        if (localMac.IsZero || localMac.IsBroadcast)
        {
            result = Ipv4NeighborRoutePlanResult.Fail(Ipv4NeighborRoutePlanStatus.InvalidLocalMacAddress, cacheStatus, ArpNeighborResolutionStatus.Success, PacketParseStatus.Success, lookup.Count, lookup.Capacity);
            return result.Status;
        }

        ArpNeighborResolutionTransaction transaction = ArpNeighborResolutionTransaction.Create(localMac, localAddress, targetAddress);
        ArpNeighborResolutionStatus arpStatus = transaction.TryWriteRequest(arpRequestFrame, out ArpNeighborResolutionResult arpResult);
        if (arpStatus != ArpNeighborResolutionStatus.Success)
        {
            Ipv4NeighborRoutePlanStatus status = arpStatus == ArpNeighborResolutionStatus.BufferTooSmall ? Ipv4NeighborRoutePlanStatus.RequestBufferTooSmall : Ipv4NeighborRoutePlanStatus.RequestWriteFailed;
            result = Ipv4NeighborRoutePlanResult.Fail(status, cacheStatus, arpStatus, arpResult.PacketStatus, lookup.Count, lookup.Capacity);
            return result.Status;
        }

        result = Ipv4NeighborRoutePlanResult.RequestWritten(arpResult.BytesWritten, cacheStatus, arpStatus, arpResult.PacketStatus, lookup.Count, lookup.Capacity);
        return result.Status;
    }
}
