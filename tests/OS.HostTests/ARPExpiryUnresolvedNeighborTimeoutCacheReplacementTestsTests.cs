using OS.Net;

internal static class ARPExpiryUnresolvedNeighborTimeoutCacheReplacementTestsTests
{
    [HostTest("net.arp.expiry controls are complete repeatable and allocation free", HostLane.Fast, 0)]
    private static bool Controls()
    {
        var first = ARPExpiryUnresolvedNeighborTimeoutCacheReplacementTests.Run();
        long before = GC.GetAllocatedBytesForCurrentThread();
        var r = ARPExpiryUnresolvedNeighborTimeoutCacheReplacementTests.Run();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        return HostCheck.Result(!default(ArpExpiryControlResult).Succeeded && first.Succeeded && r.Succeeded &&
            first.Checks == r.Checks && first.CacheCount == r.CacheCount && first.RequestBytes == r.RequestBytes &&
            r.Status == ArpExpiryControlStatus.Success && r.Checks == 0x7f && r.CacheCount == 2 &&
            r.RequestBytes == 42 && allocated == 0);
    }

    [HostTest("net.arp.expiry exact boundary tick zero and maximum do not wrap", HostLane.Fast, 1)]
    private static bool ExpiryBoundaries() => HostCheck.Result(Expiry(0, 1) && Expiry(100, 10) && Expiry(ulong.MaxValue - 10, 10));

    private static bool Expiry(ulong insertedTick, ulong ttl)
    {
        Span<ArpNeighborCacheEntry> entries = stackalloc ArpNeighborCacheEntry[1];
        var cache = ArpNeighborCache.Create(entries);
        if (cache.InsertOrUpdate(Address(2), Mac(2), insertedTick, out _) != ArpNeighborCacheStatus.Success) return false;
        ulong exactTick = insertedTick + ttl;
        return cache.PurgeStale(exactTick - 1, ttl, out var before) == ArpNeighborCacheStatus.Success &&
            before.Succeeded && before.RemovedCount == 0 && cache.Count == 1 &&
            cache.TryLookup(Address(2), out var alive) == ArpNeighborCacheStatus.Success &&
            alive.Entry.LastUpdatedTick == insertedTick && alive.Entry.MacAddress == Mac(2) &&
            cache.PurgeStale(exactTick, ttl, out var exact) == ArpNeighborCacheStatus.Success && exact.Succeeded &&
            exact.RemovedCount == 1 && cache.Count == 0 && !entries[0].IsActive &&
            cache.TryLookup(Address(2), out var missing) == ArpNeighborCacheStatus.NotFound && !missing.Succeeded;
    }

    [HostTest("net.arp.expiry invalid ttl and later retrograde entry preserve the entire cache", HostLane.Fast, 2)]
    private static bool InvalidPurgeAtomicity()
    {
        Span<ArpNeighborCacheEntry> entries = stackalloc ArpNeighborCacheEntry[3];
        var cache = ArpNeighborCache.Create(entries);
        cache.InsertOrUpdate(Address(2), Mac(2), 10, out _);
        cache.InsertOrUpdate(Address(3), Mac(3), 30, out _);
        cache.InsertOrUpdate(Address(4), Mac(4), 40, out _);
        ArpNeighborCacheEntry a = entries[0], b = entries[1], c = entries[2];
        bool invalidTtl = cache.PurgeStale(ulong.MaxValue, 0, out var ttl) == ArpNeighborCacheStatus.InvalidTtl &&
            Rejected(ttl, 3) && Same(entries[0], a) && Same(entries[1], b) && Same(entries[2], c) && cache.Count == 3;
        bool retrograde = cache.PurgeStale(20, 10, out var tick) == ArpNeighborCacheStatus.InvalidTick &&
            Rejected(tick, 3) && Same(entries[0], a) && Same(entries[1], b) && Same(entries[2], c) && cache.Count == 3;
        return HostCheck.Result(invalidTtl && retrograde &&
            cache.TryLookup(Address(2), out var first) == ArpNeighborCacheStatus.Success && first.Entry.LastUpdatedTick == 10);
    }

    [HostTest("net.arp.expiry full cache requires explicit removal before replacement", HostLane.Fast, 3)]
    private static bool ExplicitReplacement()
    {
        Span<ArpNeighborCacheEntry> entries = stackalloc ArpNeighborCacheEntry[2];
        var cache = ArpNeighborCache.Create(entries);
        cache.InsertOrUpdate(Address(2), Mac(2), 10, out _);
        cache.InsertOrUpdate(Address(3), Mac(3), 20, out _);
        ArpNeighborCacheEntry a = entries[0], b = entries[1];
        bool full = cache.InsertOrUpdate(Address(4), Mac(4), 1000, out var rejected) == ArpNeighborCacheStatus.CapacityExceeded &&
            Rejected(rejected, 2) && cache.Count == 2 && Same(entries[0], a) && Same(entries[1], b) &&
            cache.TryLookup(Address(4), out _) == ArpNeighborCacheStatus.NotFound;
        bool removed = cache.Remove(Address(2), out var remove) == ArpNeighborCacheStatus.Success && remove.Succeeded &&
            remove.RemovedCount == 1 && remove.Count == 1 && cache.Count == 1 && Same(entries[0], b) && !entries[1].IsActive;
        bool missing = cache.Remove(Address(2), out var repeated) == ArpNeighborCacheStatus.NotFound &&
            !repeated.Succeeded && repeated.RemovedCount == 0 && cache.Count == 1 && Same(entries[0], b);
        bool replacement = cache.InsertOrUpdate(Address(4), Mac(4), 1000, out var insert) == ArpNeighborCacheStatus.Success &&
            insert.Succeeded && !insert.UpdatedExisting && insert.Count == 2 && cache.Count == 2 && Same(entries[0], b) &&
            cache.TryLookup(Address(4), out var lookup) == ArpNeighborCacheStatus.Success &&
            lookup.Entry.MacAddress == Mac(4) && lookup.Entry.LastUpdatedTick == 1000 &&
            cache.TryLookup(Address(2), out _) == ArpNeighborCacheStatus.NotFound;
        return HostCheck.Result(full && removed && missing && replacement);
    }

    [HostTest("net.arp.expiry unresolved timeout rejects an independently valid late reply", HostLane.Fast, 4)]
    private static bool TimeoutLateReply()
    {
        // Independent Ethernet/ARP vector: 02...01 destination, 02...02 source, 10.0.0.2 -> 10.0.0.1.
        ReadOnlySpan<byte> reply = [2, 0, 0, 0, 0, 1, 2, 0, 0, 0, 0, 2, 8, 6,
            0, 1, 8, 0, 6, 4, 0, 2, 2, 0, 0, 0, 0, 2, 10, 0, 0, 2, 2, 0, 0, 0, 0, 1, 10, 0, 0, 1];
        Span<byte> request = stackalloc byte[42];
        var accepting = ArpNeighborResolutionTransaction.Create(Mac(1), Address(1), Address(2));
        bool validReply = accepting.TryWriteRequest(request, out _) == ArpNeighborResolutionStatus.Success &&
            accepting.ReceiveReply(reply, out var accepted) == ArpNeighborResolutionStatus.Success && accepted.Succeeded &&
            accepting.State == ArpNeighborResolutionState.Resolved && accepting.ResolvedMac == Mac(2);
        var pending = ArpNeighborResolutionTransaction.Create(Mac(1), Address(1), Address(2));
        bool initial = pending.Timeout(out var noRequest) == ArpNeighborResolutionStatus.InvalidState &&
            !noRequest.Succeeded && pending.State == ArpNeighborResolutionState.Initial;
        bool sent = pending.TryWriteRequest(request, out var written) == ArpNeighborResolutionStatus.Success &&
            written.BytesWritten == 42 && pending.State == ArpNeighborResolutionState.RequestSent && pending.ResolvedMac == default;
        bool timeout = pending.Timeout(out var expired) == ArpNeighborResolutionStatus.Timeout && !expired.Succeeded &&
            expired.State == ArpNeighborResolutionState.Failed && pending.State == ArpNeighborResolutionState.Failed;
        bool late = pending.ReceiveReply(reply, out var rejected) == ArpNeighborResolutionStatus.InvalidState &&
            !rejected.Succeeded && rejected.State == ArpNeighborResolutionState.Failed && rejected.ResolvedMac == default &&
            pending.State == ArpNeighborResolutionState.Failed && pending.ResolvedMac == default;
        request.Fill(0xa5);
        bool repeat = pending.Timeout(out _) == ArpNeighborResolutionStatus.InvalidState &&
            pending.TryWriteRequest(request, out var rewrite) == ArpNeighborResolutionStatus.InvalidState && rewrite.BytesWritten == 0;
        for (int i = 0; i < request.Length; i++) repeat &= request[i] == 0xa5;
        return HostCheck.Result(validReply && initial && sent && timeout && late && repeat);
    }

    private static Ipv4Address Address(byte last) => new(10, 0, 0, last);
    private static MacAddress Mac(byte last) => new(2, 0, 0, 0, 0, last);
    private static bool Rejected(ArpNeighborCacheMutationResult r, int capacity) => r.IsInitialized && !r.Succeeded &&
        r.Count == capacity && r.Capacity == capacity && r.RemovedCount == 0 && !r.UpdatedExisting;
    private static bool Same(ArpNeighborCacheEntry a, ArpNeighborCacheEntry b) => a.Address == b.Address &&
        a.MacAddress == b.MacAddress && a.State == b.State && a.LastUpdatedTick == b.LastUpdatedTick;
}
