namespace OS.Net;

public static class ArpNeighborCacheUpdater
{
    public static ArpNeighborCacheUpdateStatus TryUpdateFromReply(ref ArpNeighborCache cache, MacAddress localMac, Ipv4Address localAddress, Ipv4Address targetAddress, ReadOnlySpan<byte> replyFrame, ulong currentTick, out ArpNeighborCacheUpdateResult result)
    {
        ArpNeighborResolutionTransaction transaction = ArpNeighborResolutionTransaction.Create(localMac, localAddress, targetAddress);
        Span<byte> requestScratch = stackalloc byte[ArpNeighborResolutionTransaction.RequestFrameLength];
        ArpNeighborResolutionStatus requestStatus = transaction.TryWriteRequest(requestScratch, out ArpNeighborResolutionResult requestResult);
        if (requestStatus != ArpNeighborResolutionStatus.Success)
        {
            result = ArpNeighborCacheUpdateResult.Fail(ArpNeighborCacheUpdateStatus.ArpValidationFailed, requestStatus, ArpNeighborCacheStatus.Success, requestResult.PacketStatus, requestResult.State, default, cache.Count, cache.Capacity);
            return result.Status;
        }

        ArpNeighborResolutionStatus arpStatus = transaction.ReceiveReply(replyFrame, out ArpNeighborResolutionResult arpResult);
        if (arpStatus != ArpNeighborResolutionStatus.Success)
        {
            result = ArpNeighborCacheUpdateResult.Fail(ArpNeighborCacheUpdateStatus.ArpValidationFailed, arpStatus, ArpNeighborCacheStatus.Success, arpResult.PacketStatus, arpResult.State, default, cache.Count, cache.Capacity);
            return result.Status;
        }

        ArpNeighborCacheStatus cacheStatus = cache.InsertOrUpdate(targetAddress, arpResult.ResolvedMac, currentTick, out ArpNeighborCacheMutationResult cacheResult);
        if (cacheStatus != ArpNeighborCacheStatus.Success)
        {
            result = ArpNeighborCacheUpdateResult.Fail(ArpNeighborCacheUpdateStatus.CacheUpdateFailed, arpStatus, cacheStatus, arpResult.PacketStatus, arpResult.State, arpResult.ResolvedMac, cacheResult.Count, cacheResult.Capacity);
            return result.Status;
        }

        result = ArpNeighborCacheUpdateResult.Success(arpStatus, cacheStatus, arpResult.PacketStatus, arpResult.State, arpResult.ResolvedMac, cacheResult.Count, cacheResult.Capacity, cacheResult.UpdatedExisting);
        return result.Status;
    }
}
