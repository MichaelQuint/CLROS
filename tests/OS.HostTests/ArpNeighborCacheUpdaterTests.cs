using System.Runtime.CompilerServices;
using OS.Net;

internal static class ArpNeighborCacheUpdaterTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("net.arp.update inserts validated reply", TestInsertsValidatedReply, ref failures);
        Run("net.arp.update refreshes existing entry", TestRefreshesExistingEntry, ref failures);
        Run("net.arp.update rejects wrong sender address", TestRejectsWrongSenderAddress, ref failures);
        Run("net.arp.update rejects wrong target mac", TestRejectsWrongTargetMac, ref failures);
        Run("net.arp.update reports cache capacity failure", TestReportsCacheCapacityFailure, ref failures);
        Run("net.arp.update leaves cache unchanged on invalid reply", TestLeavesCacheUnchangedOnInvalidReply, ref failures);
        Run("net.arp.update default result is not success", TestDefaultResultIsNotSuccess, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("ARP neighbor cache updater host tests failed: " + failures);
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

    private static bool TestInsertsValidatedReply()
    {
        Span<ArpNeighborCacheEntry> storage = stackalloc ArpNeighborCacheEntry[2];
        ArpNeighborCache cache = ArpNeighborCache.Create(storage);
        byte[] reply = CreateReplyFrame(
            ethernetSource: RemoteMac(),
            ethernetDestination: LocalMac(),
            arpSenderMac: RemoteMac(),
            arpSenderAddress: TargetAddress(),
            arpTargetMac: LocalMac(),
            arpTargetAddress: LocalAddress());

        ArpNeighborCacheUpdateStatus status = ArpNeighborCacheUpdater.TryUpdateFromReply(
            ref cache,
            LocalMac(),
            LocalAddress(),
            TargetAddress(),
            reply,
            currentTick: 100,
            out ArpNeighborCacheUpdateResult result);
        ArpNeighborCacheStatus lookupStatus = cache.TryLookup(
            TargetAddress(),
            out ArpNeighborCacheLookupResult lookup);

        return status == ArpNeighborCacheUpdateStatus.Success
            && result.Succeeded
            && result.ArpStatus == ArpNeighborResolutionStatus.Success
            && result.CacheStatus == ArpNeighborCacheStatus.Success
            && result.PacketStatus == PacketParseStatus.Success
            && result.ArpState == ArpNeighborResolutionState.Resolved
            && result.ResolvedMac == RemoteMac()
            && result.CacheCount == 1
            && result.CacheCapacity == 2
            && !result.UpdatedExisting
            && lookupStatus == ArpNeighborCacheStatus.Success
            && lookup.Entry.Address == TargetAddress()
            && lookup.Entry.MacAddress == RemoteMac()
            && lookup.Entry.LastUpdatedTick == 100;
    }

    private static bool TestRefreshesExistingEntry()
    {
        Span<ArpNeighborCacheEntry> storage = stackalloc ArpNeighborCacheEntry[1];
        ArpNeighborCache cache = ArpNeighborCache.Create(storage);
        cache.InsertOrUpdate(TargetAddress(), OldRemoteMac(), currentTick: 10, out _);
        byte[] reply = CreateReplyFrame(
            ethernetSource: RemoteMac(),
            ethernetDestination: LocalMac(),
            arpSenderMac: RemoteMac(),
            arpSenderAddress: TargetAddress(),
            arpTargetMac: LocalMac(),
            arpTargetAddress: LocalAddress());

        ArpNeighborCacheUpdateStatus status = ArpNeighborCacheUpdater.TryUpdateFromReply(
            ref cache,
            LocalMac(),
            LocalAddress(),
            TargetAddress(),
            reply,
            currentTick: 200,
            out ArpNeighborCacheUpdateResult result);
        ArpNeighborCacheStatus lookupStatus = cache.TryLookup(
            TargetAddress(),
            out ArpNeighborCacheLookupResult lookup);

        return status == ArpNeighborCacheUpdateStatus.Success
            && result.Succeeded
            && result.UpdatedExisting
            && result.CacheCount == 1
            && result.CacheCapacity == 1
            && lookupStatus == ArpNeighborCacheStatus.Success
            && lookup.Entry.MacAddress == RemoteMac()
            && lookup.Entry.LastUpdatedTick == 200
            && cache.Count == 1;
    }

    private static bool TestRejectsWrongSenderAddress()
    {
        Span<ArpNeighborCacheEntry> storage = stackalloc ArpNeighborCacheEntry[1];
        ArpNeighborCache cache = ArpNeighborCache.Create(storage);
        byte[] reply = CreateReplyFrame(
            ethernetSource: RemoteMac(),
            ethernetDestination: LocalMac(),
            arpSenderMac: RemoteMac(),
            arpSenderAddress: WrongAddress(),
            arpTargetMac: LocalMac(),
            arpTargetAddress: LocalAddress());

        ArpNeighborCacheUpdateStatus status = ArpNeighborCacheUpdater.TryUpdateFromReply(
            ref cache,
            LocalMac(),
            LocalAddress(),
            TargetAddress(),
            reply,
            currentTick: 100,
            out ArpNeighborCacheUpdateResult result);

        return status == ArpNeighborCacheUpdateStatus.ArpValidationFailed
            && !result.Succeeded
            && result.ArpStatus == ArpNeighborResolutionStatus.SenderAddressMismatch
            && result.CacheStatus == ArpNeighborCacheStatus.Success
            && result.PacketStatus == PacketParseStatus.Success
            && result.ResolvedMac == default
            && result.CacheCount == 0
            && result.CacheCapacity == 1
            && cache.Count == 0;
    }

    private static bool TestRejectsWrongTargetMac()
    {
        Span<ArpNeighborCacheEntry> storage = stackalloc ArpNeighborCacheEntry[1];
        ArpNeighborCache cache = ArpNeighborCache.Create(storage);
        byte[] reply = CreateReplyFrame(
            ethernetSource: RemoteMac(),
            ethernetDestination: LocalMac(),
            arpSenderMac: RemoteMac(),
            arpSenderAddress: TargetAddress(),
            arpTargetMac: WrongLocalMac(),
            arpTargetAddress: LocalAddress());

        ArpNeighborCacheUpdateStatus status = ArpNeighborCacheUpdater.TryUpdateFromReply(
            ref cache,
            LocalMac(),
            LocalAddress(),
            TargetAddress(),
            reply,
            currentTick: 100,
            out ArpNeighborCacheUpdateResult result);

        return status == ArpNeighborCacheUpdateStatus.ArpValidationFailed
            && !result.Succeeded
            && result.ArpStatus == ArpNeighborResolutionStatus.TargetMacMismatch
            && result.CacheStatus == ArpNeighborCacheStatus.Success
            && result.PacketStatus == PacketParseStatus.Success
            && result.CacheCount == 0
            && result.CacheCapacity == 1
            && cache.Count == 0;
    }

    private static bool TestReportsCacheCapacityFailure()
    {
        Span<ArpNeighborCacheEntry> storage = stackalloc ArpNeighborCacheEntry[1];
        ArpNeighborCache cache = ArpNeighborCache.Create(storage);
        cache.InsertOrUpdate(OtherAddress(), OtherMac(), currentTick: 10, out _);
        byte[] reply = CreateReplyFrame(
            ethernetSource: RemoteMac(),
            ethernetDestination: LocalMac(),
            arpSenderMac: RemoteMac(),
            arpSenderAddress: TargetAddress(),
            arpTargetMac: LocalMac(),
            arpTargetAddress: LocalAddress());

        ArpNeighborCacheUpdateStatus status = ArpNeighborCacheUpdater.TryUpdateFromReply(
            ref cache,
            LocalMac(),
            LocalAddress(),
            TargetAddress(),
            reply,
            currentTick: 100,
            out ArpNeighborCacheUpdateResult result);
        ArpNeighborCacheStatus targetLookupStatus = cache.TryLookup(
            TargetAddress(),
            out ArpNeighborCacheLookupResult targetLookup);
        ArpNeighborCacheStatus otherLookupStatus = cache.TryLookup(
            OtherAddress(),
            out ArpNeighborCacheLookupResult otherLookup);

        return status == ArpNeighborCacheUpdateStatus.CacheUpdateFailed
            && !result.Succeeded
            && result.ArpStatus == ArpNeighborResolutionStatus.Success
            && result.CacheStatus == ArpNeighborCacheStatus.CapacityExceeded
            && result.PacketStatus == PacketParseStatus.Success
            && result.ResolvedMac == RemoteMac()
            && result.CacheCount == 1
            && result.CacheCapacity == 1
            && targetLookupStatus == ArpNeighborCacheStatus.NotFound
            && !targetLookup.Succeeded
            && otherLookupStatus == ArpNeighborCacheStatus.Success
            && otherLookup.Entry.MacAddress == OtherMac()
            && otherLookup.Entry.LastUpdatedTick == 10
            && cache.Count == 1;
    }

    private static bool TestLeavesCacheUnchangedOnInvalidReply()
    {
        Span<ArpNeighborCacheEntry> storage = stackalloc ArpNeighborCacheEntry[1];
        ArpNeighborCache cache = ArpNeighborCache.Create(storage);
        cache.InsertOrUpdate(TargetAddress(), OldRemoteMac(), currentTick: 10, out _);
        byte[] reply = CreateReplyFrame(
            ethernetSource: RemoteMac(),
            ethernetDestination: LocalMac(),
            arpSenderMac: RemoteMac(),
            arpSenderAddress: TargetAddress(),
            arpTargetMac: WrongLocalMac(),
            arpTargetAddress: LocalAddress());

        ArpNeighborCacheUpdateStatus status = ArpNeighborCacheUpdater.TryUpdateFromReply(
            ref cache,
            LocalMac(),
            LocalAddress(),
            TargetAddress(),
            reply,
            currentTick: 200,
            out ArpNeighborCacheUpdateResult result);
        ArpNeighborCacheStatus lookupStatus = cache.TryLookup(
            TargetAddress(),
            out ArpNeighborCacheLookupResult lookup);

        return status == ArpNeighborCacheUpdateStatus.ArpValidationFailed
            && !result.Succeeded
            && result.ArpStatus == ArpNeighborResolutionStatus.TargetMacMismatch
            && result.CacheCount == 1
            && result.CacheCapacity == 1
            && lookupStatus == ArpNeighborCacheStatus.Success
            && lookup.Entry.MacAddress == OldRemoteMac()
            && lookup.Entry.LastUpdatedTick == 10
            && cache.Count == 1;
    }

    private static bool TestDefaultResultIsNotSuccess()
    {
        ArpNeighborCacheUpdateResult result = default;
        ArpNeighborCacheUpdateResult failed = ArpNeighborCacheUpdateResult.Fail(
            ArpNeighborCacheUpdateStatus.ArpValidationFailed,
            ArpNeighborResolutionStatus.SenderAddressMismatch,
            ArpNeighborCacheStatus.Success,
            PacketParseStatus.Success,
            ArpNeighborResolutionState.RequestSent,
            default,
            cacheCount: 2,
            cacheCapacity: 4);

        return !result.IsInitialized
            && !result.Succeeded
            && result.Status == ArpNeighborCacheUpdateStatus.Success
            && result.ArpStatus == ArpNeighborResolutionStatus.Success
            && result.CacheStatus == ArpNeighborCacheStatus.Success
            && result.PacketStatus == PacketParseStatus.Success
            && result.ArpState == ArpNeighborResolutionState.Initial
            && result.ResolvedMac == default
            && result.CacheCount == 0
            && result.CacheCapacity == 0
            && !result.UpdatedExisting
            && failed.IsInitialized
            && !failed.Succeeded
            && failed.Status == ArpNeighborCacheUpdateStatus.ArpValidationFailed
            && failed.ArpStatus == ArpNeighborResolutionStatus.SenderAddressMismatch
            && failed.CacheStatus == ArpNeighborCacheStatus.Success
            && failed.PacketStatus == PacketParseStatus.Success
            && failed.ArpState == ArpNeighborResolutionState.RequestSent
            && failed.CacheCount == 2
            && failed.CacheCapacity == 4
            && !failed.UpdatedExisting
            && !RuntimeHelpers.IsReferenceOrContainsReferences<ArpNeighborCacheUpdateResult>();
    }

    private static byte[] CreateReplyFrame(
        MacAddress ethernetSource,
        MacAddress ethernetDestination,
        MacAddress arpSenderMac,
        Ipv4Address arpSenderAddress,
        MacAddress arpTargetMac,
        Ipv4Address arpTargetAddress)
    {
        byte[] frame = new byte[ArpNeighborResolutionTransaction.RequestFrameLength];
        EthernetHeader ethernet = new EthernetHeader(
            ethernetDestination,
            ethernetSource,
            EthernetHeader.EtherTypeArp);
        PacketParseStatus ethernetStatus = EthernetHeader.TryWrite(frame, ethernet);
        PacketParseStatus arpStatus = ArpPacket.TryWrite(
            frame.AsSpan(EthernetHeader.HeaderLength, ArpPacket.PacketLength),
            new ArpPacket(
                ArpPacket.OperationReply,
                arpSenderMac,
                arpSenderAddress,
                arpTargetMac,
                arpTargetAddress),
            out int arpBytesWritten);

        if (ethernetStatus != PacketParseStatus.Success
            || arpStatus != PacketParseStatus.Success
            || arpBytesWritten != ArpPacket.PacketLength)
        {
            throw new InvalidOperationException("Failed to build ARP reply frame.");
        }

        return frame;
    }

    private static MacAddress LocalMac()
    {
        return new MacAddress(0x02, 0x00, 0x00, 0x00, 0x00, 0x10);
    }

    private static MacAddress WrongLocalMac()
    {
        return new MacAddress(0x02, 0x00, 0x00, 0x00, 0x00, 0x11);
    }

    private static MacAddress RemoteMac()
    {
        return new MacAddress(0x02, 0x00, 0x00, 0x00, 0x00, 0x20);
    }

    private static MacAddress OldRemoteMac()
    {
        return new MacAddress(0x02, 0x00, 0x00, 0x00, 0x00, 0x21);
    }

    private static MacAddress OtherMac()
    {
        return new MacAddress(0x02, 0x00, 0x00, 0x00, 0x00, 0x30);
    }

    private static Ipv4Address LocalAddress()
    {
        return new Ipv4Address(192, 0, 2, 10);
    }

    private static Ipv4Address TargetAddress()
    {
        return new Ipv4Address(192, 0, 2, 1);
    }

    private static Ipv4Address WrongAddress()
    {
        return new Ipv4Address(192, 0, 2, 77);
    }

    private static Ipv4Address OtherAddress()
    {
        return new Ipv4Address(192, 0, 2, 99);
    }
}
