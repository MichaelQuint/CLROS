namespace OS.Net;

public enum ArpExpiryControlStatus : byte { Uninitialized, Success, CheckFailed }

public readonly struct ArpExpiryControlResult
{
    public readonly ArpExpiryControlStatus Status;
    public readonly uint Checks;
    public readonly ushort CacheCount, RequestBytes;

    internal ArpExpiryControlResult(ArpExpiryControlStatus status, uint checks, ushort count, ushort bytes)
    { Status = status; Checks = checks; CacheCount = count; RequestBytes = bytes; }

    public bool Succeeded => Status == ArpExpiryControlStatus.Success &&
        Checks == ARPExpiryUnresolvedNeighborTimeoutCacheReplacementTests.CompleteChecks;
}

/// <summary>Bounded caller-tick controls; no clock, automatic eviction or packet I/O.</summary>
public static class ARPExpiryUnresolvedNeighborTimeoutCacheReplacementTests
{
    public const uint BeforeExpiry = 1, ExactExpiry = 2, InvalidTtlAtomic = 4, RetrogradeTickAtomic = 8,
        FullCacheAtomic = 16, ExplicitReplacement = 32, TimeoutLateReply = 64;
    public const uint CompleteChecks = BeforeExpiry | ExactExpiry | InvalidTtlAtomic | RetrogradeTickAtomic |
        FullCacheAtomic | ExplicitReplacement | TimeoutLateReply;

    public static ArpExpiryControlResult Run()
    {
        Span<ArpNeighborCacheEntry> entries = stackalloc ArpNeighborCacheEntry[2];
        ArpNeighborCache cache = ArpNeighborCache.Create(entries);
        Ipv4Address first = new(10, 0, 0, 2), second = new(10, 0, 0, 3), replacement = new(10, 0, 0, 4);
        MacAddress firstMac = new(2, 0, 0, 0, 0, 2), secondMac = new(2, 0, 0, 0, 0, 3), replacementMac = new(2, 0, 0, 0, 0, 4);
        uint checks = 0;
        if (cache.InsertOrUpdate(first, firstMac, 100, out _) != ArpNeighborCacheStatus.Success ||
            cache.PurgeStale(109, 10, out var before) != ArpNeighborCacheStatus.Success ||
            !before.Succeeded || before.RemovedCount != 0 || cache.Count != 1 ||
            cache.TryLookup(first, out var lookup) != ArpNeighborCacheStatus.Success ||
            lookup.Entry.MacAddress != firstMac || lookup.Entry.LastUpdatedTick != 100)
            return Finish(checks, cache.Count, 0);
        checks |= BeforeExpiry;
        if (cache.PurgeStale(110, 10, out var exact) != ArpNeighborCacheStatus.Success ||
            !exact.Succeeded || exact.RemovedCount != 1 || cache.Count != 0 ||
            cache.TryLookup(first, out _) != ArpNeighborCacheStatus.NotFound || entries[0].IsActive || entries[1].IsActive)
            return Finish(checks, cache.Count, 0);
        checks |= ExactExpiry;

        if (cache.InsertOrUpdate(first, firstMac, 100, out _) != ArpNeighborCacheStatus.Success ||
            cache.InsertOrUpdate(second, secondMac, 120, out _) != ArpNeighborCacheStatus.Success)
            return Finish(checks, cache.Count, 0);
        ArpNeighborCacheEntry savedFirst = entries[0], savedSecond = entries[1];
        if (cache.PurgeStale(150, 0, out var invalidTtl) != ArpNeighborCacheStatus.InvalidTtl ||
            !Rejected(invalidTtl) || cache.Count != 2 || !Same(entries[0], savedFirst) || !Same(entries[1], savedSecond))
            return Finish(checks, cache.Count, 0);
        checks |= InvalidTtlAtomic;
        // The first entry would expire at 110; the later entry must reject the whole purge before any removal.
        if (cache.PurgeStale(110, 10, out var invalidTick) != ArpNeighborCacheStatus.InvalidTick ||
            !Rejected(invalidTick) || cache.Count != 2 || !Same(entries[0], savedFirst) || !Same(entries[1], savedSecond))
            return Finish(checks, cache.Count, 0);
        checks |= RetrogradeTickAtomic;
        if (cache.InsertOrUpdate(replacement, replacementMac, 130, out var full) != ArpNeighborCacheStatus.CapacityExceeded ||
            !Rejected(full) || cache.Count != 2 || !Same(entries[0], savedFirst) || !Same(entries[1], savedSecond) ||
            cache.TryLookup(replacement, out _) != ArpNeighborCacheStatus.NotFound)
            return Finish(checks, cache.Count, 0);
        checks |= FullCacheAtomic;
        if (cache.Remove(first, out var removed) != ArpNeighborCacheStatus.Success || !removed.Succeeded ||
            removed.RemovedCount != 1 || removed.Count != 1 || cache.Count != 1 || !Same(entries[0], savedSecond) || entries[1].IsActive ||
            cache.InsertOrUpdate(replacement, replacementMac, 130, out var inserted) != ArpNeighborCacheStatus.Success ||
            !inserted.Succeeded || inserted.UpdatedExisting || inserted.Count != 2 || cache.Count != 2 ||
            !Same(entries[0], savedSecond) || cache.TryLookup(first, out _) != ArpNeighborCacheStatus.NotFound ||
            cache.TryLookup(replacement, out lookup) != ArpNeighborCacheStatus.Success ||
            lookup.Entry.MacAddress != replacementMac || lookup.Entry.LastUpdatedTick != 130)
            return Finish(checks, cache.Count, 0);
        checks |= ExplicitReplacement;
        ushort requestBytes = 0;
        if (!TimeoutRejectsValidLateReply(out requestBytes)) return Finish(checks, cache.Count, requestBytes);
        checks |= TimeoutLateReply;
        return Finish(checks, cache.Count, requestBytes);
    }

    private static bool TimeoutRejectsValidLateReply(out ushort requestBytes)
    {
        requestBytes = 0;
        MacAddress localMac = new(2, 0, 0, 0, 0, 1), peerMac = new(2, 0, 0, 0, 0, 2);
        Ipv4Address local = new(10, 0, 0, 1), peer = new(10, 0, 0, 2);
        Span<byte> request = stackalloc byte[ArpNeighborResolutionTransaction.RequestFrameLength];
        Span<byte> reply = stackalloc byte[ArpNeighborResolutionTransaction.RequestFrameLength];
        var pending = ArpNeighborResolutionTransaction.Create(localMac, local, peer);
        if (pending.TryWriteRequest(request, out var written) != ArpNeighborResolutionStatus.Success ||
            !written.Succeeded || written.BytesWritten != request.Length || pending.State != ArpNeighborResolutionState.RequestSent)
            return false;
        requestBytes = (ushort)written.BytesWritten;
        if (EthernetHeader.TryWrite(reply, new EthernetHeader(localMac, peerMac, EthernetHeader.EtherTypeArp)) != PacketParseStatus.Success ||
            ArpPacket.TryWrite(reply.Slice(EthernetHeader.HeaderLength, ArpPacket.PacketLength),
                new ArpPacket(ArpPacket.OperationReply, peerMac, peer, localMac, local), out int bytes) != PacketParseStatus.Success ||
            bytes != ArpPacket.PacketLength) return false;
        var accepting = ArpNeighborResolutionTransaction.Create(localMac, local, peer);
        if (accepting.TryWriteRequest(request, out _) != ArpNeighborResolutionStatus.Success ||
            accepting.ReceiveReply(reply, out var accepted) != ArpNeighborResolutionStatus.Success ||
            !accepted.Succeeded || accepted.ResolvedMac != peerMac) return false;
        // Timeout is an explicit caller decision; this control neither schedules nor measures it.
        return pending.Timeout(out var timedOut) == ArpNeighborResolutionStatus.Timeout && !timedOut.Succeeded &&
            timedOut.State == ArpNeighborResolutionState.Failed && pending.State == ArpNeighborResolutionState.Failed &&
            pending.ReceiveReply(reply, out var late) == ArpNeighborResolutionStatus.InvalidState && !late.Succeeded &&
            late.State == ArpNeighborResolutionState.Failed && pending.State == ArpNeighborResolutionState.Failed &&
            late.BytesWritten == 0 && late.ResolvedMac == default && pending.ResolvedMac == default;
    }

    private static bool Rejected(ArpNeighborCacheMutationResult r) => r.IsInitialized && !r.Succeeded &&
        r.Count == 2 && r.Capacity == 2 && r.RemovedCount == 0 && !r.UpdatedExisting;

    private static bool Same(ArpNeighborCacheEntry a, ArpNeighborCacheEntry b) => a.Address == b.Address &&
        a.MacAddress == b.MacAddress && a.State == b.State && a.LastUpdatedTick == b.LastUpdatedTick;

    private static ArpExpiryControlResult Finish(uint checks, int count, ushort bytes) =>
        new(checks == CompleteChecks ? ArpExpiryControlStatus.Success : ArpExpiryControlStatus.CheckFailed, checks, (ushort)count, bytes);
}
