using System.Runtime.CompilerServices;
using OS.Net;

internal static class ArpNeighborCacheTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("net.arp.cache insert lookup", TestInsertLookup, ref failures);
        Run("net.arp.cache update existing", TestUpdateExisting, ref failures);
        Run("net.arp.cache remove clear", TestRemoveClear, ref failures);
        Run("net.arp.cache capacity exhaustion", TestCapacityExhaustion, ref failures);
        Run("net.arp.cache purge stale", TestPurgeStale, ref failures);
        Run("net.arp.cache rejects invalid inputs", TestRejectsInvalidInputs, ref failures);
        Run("net.arp.cache default results are not success", TestDefaultResultsAreNotSuccess, ref failures);
        Run("net.mac validation uses shared helpers", TestMacValidationUsesSharedHelpers, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("ARP neighbor cache host tests failed: " + failures);
        }
    }

    private static void Run(string name, Func<bool> test, ref int failures)
    {
        Console.WriteLine("TEST_START " + name);

        try
        {
            if (test())
            {
                Console.WriteLine("TEST_PASS " + name);
                return;
            }

            failures++;
            Console.WriteLine("TEST_FAIL " + name);
        }
        catch (Exception ex)
        {
            failures++;
            Console.WriteLine("TEST_FAIL " + name + " " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static bool TestInsertLookup()
    {
        Span<ArpNeighborCacheEntry> storage = stackalloc ArpNeighborCacheEntry[2];
        ArpNeighborCache cache = ArpNeighborCache.Create(storage);

        ArpNeighborCacheStatus insertStatus = cache.InsertOrUpdate(
            PeerAddress1(),
            PeerMac1(),
            currentTick: 100,
            out ArpNeighborCacheMutationResult insertResult);
        ArpNeighborCacheStatus lookupStatus = cache.TryLookup(
            PeerAddress1(),
            out ArpNeighborCacheLookupResult lookupResult);

        return insertStatus == ArpNeighborCacheStatus.Success
            && insertResult.Succeeded
            && insertResult.Count == 1
            && insertResult.Capacity == 2
            && !insertResult.UpdatedExisting
            && lookupStatus == ArpNeighborCacheStatus.Success
            && lookupResult.Succeeded
            && lookupResult.Entry.State == ArpNeighborCacheEntryState.Resolved
            && lookupResult.Entry.Address == PeerAddress1()
            && lookupResult.Entry.MacAddress == PeerMac1()
            && lookupResult.Entry.LastUpdatedTick == 100
            && cache.Count == 1
            && cache.Capacity == 2;
    }

    private static bool TestUpdateExisting()
    {
        Span<ArpNeighborCacheEntry> storage = stackalloc ArpNeighborCacheEntry[1];
        ArpNeighborCache cache = ArpNeighborCache.Create(storage);

        ArpNeighborCacheStatus firstStatus = cache.InsertOrUpdate(
            PeerAddress1(),
            PeerMac1(),
            currentTick: 10,
            out ArpNeighborCacheMutationResult firstResult);
        ArpNeighborCacheStatus updateStatus = cache.InsertOrUpdate(
            PeerAddress1(),
            PeerMac2(),
            currentTick: 20,
            out ArpNeighborCacheMutationResult updateResult);
        ArpNeighborCacheStatus lookupStatus = cache.TryLookup(
            PeerAddress1(),
            out ArpNeighborCacheLookupResult lookupResult);

        return firstStatus == ArpNeighborCacheStatus.Success
            && firstResult.Count == 1
            && updateStatus == ArpNeighborCacheStatus.Success
            && updateResult.Succeeded
            && updateResult.UpdatedExisting
            && updateResult.Count == 1
            && lookupStatus == ArpNeighborCacheStatus.Success
            && lookupResult.Entry.MacAddress == PeerMac2()
            && lookupResult.Entry.LastUpdatedTick == 20
            && cache.Count == 1;
    }

    private static bool TestRemoveClear()
    {
        Span<ArpNeighborCacheEntry> storage = stackalloc ArpNeighborCacheEntry[3];
        ArpNeighborCache cache = ArpNeighborCache.Create(storage);
        cache.InsertOrUpdate(PeerAddress1(), PeerMac1(), currentTick: 10, out _);
        cache.InsertOrUpdate(PeerAddress2(), PeerMac2(), currentTick: 20, out _);

        ArpNeighborCacheStatus removeStatus = cache.Remove(
            PeerAddress1(),
            out ArpNeighborCacheMutationResult removeResult);
        ArpNeighborCacheStatus missingStatus = cache.TryLookup(
            PeerAddress1(),
            out ArpNeighborCacheLookupResult missingResult);
        ArpNeighborCacheStatus remainingStatus = cache.TryLookup(
            PeerAddress2(),
            out ArpNeighborCacheLookupResult remainingResult);
        ArpNeighborCacheStatus repeatRemoveStatus = cache.Remove(
            PeerAddress1(),
            out ArpNeighborCacheMutationResult repeatRemoveResult);
        ArpNeighborCacheStatus clearStatus = cache.Clear(
            out ArpNeighborCacheMutationResult clearResult);
        ArpNeighborCacheStatus afterClearStatus = cache.TryLookup(
            PeerAddress2(),
            out ArpNeighborCacheLookupResult afterClearResult);

        return removeStatus == ArpNeighborCacheStatus.Success
            && removeResult.RemovedCount == 1
            && removeResult.Count == 1
            && missingStatus == ArpNeighborCacheStatus.NotFound
            && !missingResult.Succeeded
            && remainingStatus == ArpNeighborCacheStatus.Success
            && remainingResult.Entry.Address == PeerAddress2()
            && repeatRemoveStatus == ArpNeighborCacheStatus.NotFound
            && !repeatRemoveResult.Succeeded
            && repeatRemoveResult.Count == 1
            && clearStatus == ArpNeighborCacheStatus.Success
            && clearResult.RemovedCount == 1
            && clearResult.Count == 0
            && afterClearStatus == ArpNeighborCacheStatus.NotFound
            && !afterClearResult.Succeeded
            && cache.Count == 0
            && storage[0].State == ArpNeighborCacheEntryState.Empty;
    }

    private static bool TestCapacityExhaustion()
    {
        Span<ArpNeighborCacheEntry> storage = stackalloc ArpNeighborCacheEntry[1];
        ArpNeighborCache cache = ArpNeighborCache.Create(storage);

        ArpNeighborCacheStatus firstStatus = cache.InsertOrUpdate(
            PeerAddress1(),
            PeerMac1(),
            currentTick: 10,
            out ArpNeighborCacheMutationResult firstResult);
        ArpNeighborCacheStatus overflowStatus = cache.InsertOrUpdate(
            PeerAddress2(),
            PeerMac2(),
            currentTick: 20,
            out ArpNeighborCacheMutationResult overflowResult);
        ArpNeighborCacheStatus firstLookupStatus = cache.TryLookup(
            PeerAddress1(),
            out ArpNeighborCacheLookupResult firstLookup);
        ArpNeighborCacheStatus overflowLookupStatus = cache.TryLookup(
            PeerAddress2(),
            out ArpNeighborCacheLookupResult overflowLookup);

        Span<ArpNeighborCacheEntry> emptyStorage = Span<ArpNeighborCacheEntry>.Empty;
        ArpNeighborCache emptyCache = ArpNeighborCache.Create(emptyStorage);
        ArpNeighborCacheStatus emptyStatus = emptyCache.InsertOrUpdate(
            PeerAddress1(),
            PeerMac1(),
            currentTick: 30,
            out ArpNeighborCacheMutationResult emptyResult);

        return firstStatus == ArpNeighborCacheStatus.Success
            && firstResult.Count == 1
            && overflowStatus == ArpNeighborCacheStatus.CapacityExceeded
            && !overflowResult.Succeeded
            && overflowResult.Count == 1
            && firstLookupStatus == ArpNeighborCacheStatus.Success
            && firstLookup.Entry.MacAddress == PeerMac1()
            && overflowLookupStatus == ArpNeighborCacheStatus.NotFound
            && !overflowLookup.Succeeded
            && emptyStatus == ArpNeighborCacheStatus.CapacityExceeded
            && !emptyResult.Succeeded
            && emptyResult.Count == 0
            && emptyResult.Capacity == 0;
    }

    private static bool TestPurgeStale()
    {
        Span<ArpNeighborCacheEntry> storage = stackalloc ArpNeighborCacheEntry[3];
        ArpNeighborCache cache = ArpNeighborCache.Create(storage);
        cache.InsertOrUpdate(PeerAddress1(), PeerMac1(), currentTick: 10, out _);
        cache.InsertOrUpdate(PeerAddress2(), PeerMac2(), currentTick: 15, out _);
        cache.InsertOrUpdate(PeerAddress3(), PeerMac3(), currentTick: 17, out _);

        ArpNeighborCacheStatus purgeStatus = cache.PurgeStale(
            currentTick: 25,
            ttlTicks: 10,
            out ArpNeighborCacheMutationResult purgeResult);
        ArpNeighborCacheStatus firstLookupStatus = cache.TryLookup(
            PeerAddress1(),
            out ArpNeighborCacheLookupResult firstLookup);
        ArpNeighborCacheStatus secondLookupStatus = cache.TryLookup(
            PeerAddress2(),
            out ArpNeighborCacheLookupResult secondLookup);
        ArpNeighborCacheStatus thirdLookupStatus = cache.TryLookup(
            PeerAddress3(),
            out ArpNeighborCacheLookupResult thirdLookup);
        ArpNeighborCacheStatus finalPurgeStatus = cache.PurgeStale(
            currentTick: 30,
            ttlTicks: 10,
            out ArpNeighborCacheMutationResult finalPurgeResult);

        return purgeStatus == ArpNeighborCacheStatus.Success
            && purgeResult.RemovedCount == 2
            && purgeResult.Count == 1
            && firstLookupStatus == ArpNeighborCacheStatus.NotFound
            && !firstLookup.Succeeded
            && secondLookupStatus == ArpNeighborCacheStatus.NotFound
            && !secondLookup.Succeeded
            && thirdLookupStatus == ArpNeighborCacheStatus.Success
            && thirdLookup.Entry.Address == PeerAddress3()
            && thirdLookup.Entry.LastUpdatedTick == 17
            && finalPurgeStatus == ArpNeighborCacheStatus.Success
            && finalPurgeResult.RemovedCount == 1
            && finalPurgeResult.Count == 0
            && cache.Count == 0;
    }

    private static bool TestRejectsInvalidInputs()
    {
        Span<ArpNeighborCacheEntry> storage = stackalloc ArpNeighborCacheEntry[2];
        ArpNeighborCache cache = ArpNeighborCache.Create(storage);

        ArpNeighborCacheStatus invalidAddressInsert = cache.InsertOrUpdate(
            default,
            PeerMac1(),
            currentTick: 10,
            out ArpNeighborCacheMutationResult invalidAddressResult);
        ArpNeighborCacheStatus zeroMacInsert = cache.InsertOrUpdate(
            PeerAddress1(),
            default,
            currentTick: 10,
            out ArpNeighborCacheMutationResult zeroMacResult);
        ArpNeighborCacheStatus broadcastMacInsert = cache.InsertOrUpdate(
            PeerAddress1(),
            BroadcastMac(),
            currentTick: 10,
            out ArpNeighborCacheMutationResult broadcastMacResult);
        ArpNeighborCacheStatus invalidLookup = cache.TryLookup(
            default,
            out ArpNeighborCacheLookupResult invalidLookupResult);
        ArpNeighborCacheStatus invalidRemove = cache.Remove(
            default,
            out ArpNeighborCacheMutationResult invalidRemoveResult);
        ArpNeighborCacheStatus invalidTtl = cache.PurgeStale(
            currentTick: 20,
            ttlTicks: 0,
            out ArpNeighborCacheMutationResult invalidTtlResult);

        cache.InsertOrUpdate(PeerAddress1(), PeerMac1(), currentTick: 100, out _);
        ArpNeighborCacheStatus invalidTick = cache.PurgeStale(
            currentTick: 50,
            ttlTicks: 10,
            out ArpNeighborCacheMutationResult invalidTickResult);
        ArpNeighborCacheStatus lookupAfterInvalidTick = cache.TryLookup(
            PeerAddress1(),
            out ArpNeighborCacheLookupResult lookupAfterInvalidTickResult);

        return invalidAddressInsert == ArpNeighborCacheStatus.InvalidAddress
            && !invalidAddressResult.Succeeded
            && zeroMacInsert == ArpNeighborCacheStatus.InvalidMacAddress
            && !zeroMacResult.Succeeded
            && broadcastMacInsert == ArpNeighborCacheStatus.InvalidMacAddress
            && !broadcastMacResult.Succeeded
            && invalidLookup == ArpNeighborCacheStatus.InvalidAddress
            && !invalidLookupResult.Succeeded
            && invalidRemove == ArpNeighborCacheStatus.InvalidAddress
            && !invalidRemoveResult.Succeeded
            && invalidTtl == ArpNeighborCacheStatus.InvalidTtl
            && !invalidTtlResult.Succeeded
            && invalidTick == ArpNeighborCacheStatus.InvalidTick
            && !invalidTickResult.Succeeded
            && invalidTickResult.Count == 1
            && lookupAfterInvalidTick == ArpNeighborCacheStatus.Success
            && lookupAfterInvalidTickResult.Entry.LastUpdatedTick == 100
            && cache.Count == 1;
    }

    private static bool TestDefaultResultsAreNotSuccess()
    {
        ArpNeighborCacheLookupResult lookup = default;
        ArpNeighborCacheMutationResult mutation = default;
        ArpNeighborCacheLookupResult lookupFailure =
            ArpNeighborCacheLookupResult.Fail(ArpNeighborCacheStatus.NotFound, count: 2, capacity: 4);
        ArpNeighborCacheMutationResult mutationFailure =
            ArpNeighborCacheMutationResult.Fail(ArpNeighborCacheStatus.InvalidTtl, count: 1, capacity: 3);

        return !lookup.IsInitialized
            && !lookup.Succeeded
            && lookup.Status == ArpNeighborCacheStatus.Success
            && lookup.Count == 0
            && lookup.Capacity == 0
            && !mutation.IsInitialized
            && !mutation.Succeeded
            && mutation.Status == ArpNeighborCacheStatus.Success
            && mutation.Count == 0
            && mutation.Capacity == 0
            && mutation.RemovedCount == 0
            && !mutation.UpdatedExisting
            && lookupFailure.IsInitialized
            && !lookupFailure.Succeeded
            && lookupFailure.Status == ArpNeighborCacheStatus.NotFound
            && lookupFailure.Count == 2
            && lookupFailure.Capacity == 4
            && mutationFailure.IsInitialized
            && !mutationFailure.Succeeded
            && mutationFailure.Status == ArpNeighborCacheStatus.InvalidTtl
            && mutationFailure.Count == 1
            && mutationFailure.Capacity == 3
            && mutationFailure.RemovedCount == 0
            && !mutationFailure.UpdatedExisting
            && !RuntimeHelpers.IsReferenceOrContainsReferences<ArpNeighborCacheLookupResult>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<ArpNeighborCacheMutationResult>();
    }

    private static bool TestMacValidationUsesSharedHelpers()
    {
        return NetworkSourceUsesSharedMacHelpers("ArpNeighborCache.cs")
            && NetworkSourceUsesSharedMacHelpers("Ipv4NeighborRoutePlanner.cs")
            && NetworkSourceUsesSharedMacHelpers("UdpTransmitPlanner.cs")
            && NetworkSourceUsesSharedMacHelpers("DnsResolverTransactionPlanner.cs");
    }

    private static bool NetworkSourceUsesSharedMacHelpers(string fileName)
    {
        string source = File.ReadAllText(Path.Combine(
            HostToolProcess.FindRepositoryRoot(),
            "src",
            "OS.Kernel",
            "Net",
            fileName));

        return source.Contains(".IsZero", StringComparison.Ordinal)
            && source.Contains(".IsBroadcast", StringComparison.Ordinal)
            && !source.Contains("private static bool IsBroadcast(MacAddress", StringComparison.Ordinal);
    }

    private static Ipv4Address PeerAddress1()
    {
        return new Ipv4Address(192, 0, 2, 1);
    }

    private static Ipv4Address PeerAddress2()
    {
        return new Ipv4Address(192, 0, 2, 2);
    }

    private static Ipv4Address PeerAddress3()
    {
        return new Ipv4Address(192, 0, 2, 3);
    }

    private static MacAddress PeerMac1()
    {
        return new MacAddress(0x02, 0x00, 0x00, 0x00, 0x01, 0x01);
    }

    private static MacAddress PeerMac2()
    {
        return new MacAddress(0x02, 0x00, 0x00, 0x00, 0x01, 0x02);
    }

    private static MacAddress PeerMac3()
    {
        return new MacAddress(0x02, 0x00, 0x00, 0x00, 0x01, 0x03);
    }

    private static MacAddress BroadcastMac()
    {
        return new MacAddress(0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF);
    }
}
