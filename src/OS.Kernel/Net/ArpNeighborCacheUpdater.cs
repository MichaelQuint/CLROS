namespace OS.Net;

public enum ArpNeighborCacheUpdateStatus
{
    Success = 0,
    ArpValidationFailed = 1,
    CacheUpdateFailed = 2
}

public readonly struct ArpNeighborCacheUpdateResult
{
    private ArpNeighborCacheUpdateResult(
        ArpNeighborCacheUpdateStatus status,
        ArpNeighborResolutionStatus arpStatus,
        ArpNeighborCacheStatus cacheStatus,
        PacketParseStatus packetStatus,
        ArpNeighborResolutionState arpState,
        MacAddress resolvedMac,
        int cacheCount,
        int cacheCapacity,
        bool updatedExisting)
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

    public bool IsInitialized { get; }

    public ArpNeighborCacheUpdateStatus Status { get; }

    public bool Succeeded => IsInitialized && Status == ArpNeighborCacheUpdateStatus.Success;

    public ArpNeighborResolutionStatus ArpStatus { get; }

    public ArpNeighborCacheStatus CacheStatus { get; }

    public PacketParseStatus PacketStatus { get; }

    public ArpNeighborResolutionState ArpState { get; }

    public MacAddress ResolvedMac { get; }

    public int CacheCount { get; }

    public int CacheCapacity { get; }

    public bool UpdatedExisting { get; }

    public static ArpNeighborCacheUpdateResult Success(
        ArpNeighborResolutionStatus arpStatus,
        ArpNeighborCacheStatus cacheStatus,
        PacketParseStatus packetStatus,
        ArpNeighborResolutionState arpState,
        MacAddress resolvedMac,
        int cacheCount,
        int cacheCapacity,
        bool updatedExisting)
    {
        return new ArpNeighborCacheUpdateResult(
            ArpNeighborCacheUpdateStatus.Success,
            arpStatus,
            cacheStatus,
            packetStatus,
            arpState,
            resolvedMac,
            cacheCount,
            cacheCapacity,
            updatedExisting);
    }

    public static ArpNeighborCacheUpdateResult Fail(
        ArpNeighborCacheUpdateStatus status,
        ArpNeighborResolutionStatus arpStatus,
        ArpNeighborCacheStatus cacheStatus,
        PacketParseStatus packetStatus,
        ArpNeighborResolutionState arpState,
        MacAddress resolvedMac,
        int cacheCount,
        int cacheCapacity)
    {
        return new ArpNeighborCacheUpdateResult(
            status,
            arpStatus,
            cacheStatus,
            packetStatus,
            arpState,
            resolvedMac,
            cacheCount,
            cacheCapacity,
            updatedExisting: false);
    }
}

public static class ArpNeighborCacheUpdater
{
    public static ArpNeighborCacheUpdateStatus TryUpdateFromReply(
        ref ArpNeighborCache cache,
        MacAddress localMac,
        Ipv4Address localAddress,
        Ipv4Address targetAddress,
        ReadOnlySpan<byte> replyFrame,
        ulong currentTick,
        out ArpNeighborCacheUpdateResult result)
    {
        ArpNeighborResolutionTransaction transaction =
            ArpNeighborResolutionTransaction.Create(localMac, localAddress, targetAddress);
        Span<byte> requestScratch = stackalloc byte[ArpNeighborResolutionTransaction.RequestFrameLength];
        ArpNeighborResolutionStatus requestStatus = transaction.TryWriteRequest(
            requestScratch,
            out ArpNeighborResolutionResult requestResult);
        if (requestStatus != ArpNeighborResolutionStatus.Success)
        {
            result = ArpNeighborCacheUpdateResult.Fail(
                ArpNeighborCacheUpdateStatus.ArpValidationFailed,
                requestStatus,
                ArpNeighborCacheStatus.Success,
                requestResult.PacketStatus,
                requestResult.State,
                default,
                cache.Count,
                cache.Capacity);
            return result.Status;
        }

        ArpNeighborResolutionStatus arpStatus = transaction.ReceiveReply(
            replyFrame,
            out ArpNeighborResolutionResult arpResult);
        if (arpStatus != ArpNeighborResolutionStatus.Success)
        {
            result = ArpNeighborCacheUpdateResult.Fail(
                ArpNeighborCacheUpdateStatus.ArpValidationFailed,
                arpStatus,
                ArpNeighborCacheStatus.Success,
                arpResult.PacketStatus,
                arpResult.State,
                default,
                cache.Count,
                cache.Capacity);
            return result.Status;
        }

        ArpNeighborCacheStatus cacheStatus = cache.InsertOrUpdate(
            targetAddress,
            arpResult.ResolvedMac,
            currentTick,
            out ArpNeighborCacheMutationResult cacheResult);
        if (cacheStatus != ArpNeighborCacheStatus.Success)
        {
            result = ArpNeighborCacheUpdateResult.Fail(
                ArpNeighborCacheUpdateStatus.CacheUpdateFailed,
                arpStatus,
                cacheStatus,
                arpResult.PacketStatus,
                arpResult.State,
                arpResult.ResolvedMac,
                cacheResult.Count,
                cacheResult.Capacity);
            return result.Status;
        }

        result = ArpNeighborCacheUpdateResult.Success(
            arpStatus,
            cacheStatus,
            arpResult.PacketStatus,
            arpResult.State,
            arpResult.ResolvedMac,
            cacheResult.Count,
            cacheResult.Capacity,
            cacheResult.UpdatedExisting);
        return result.Status;
    }
}
