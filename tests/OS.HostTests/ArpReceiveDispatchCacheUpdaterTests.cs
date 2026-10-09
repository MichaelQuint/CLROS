using System.Runtime.CompilerServices;
using OS.Drivers;
using OS.Net;

internal static class ArpReceiveDispatchCacheUpdaterTests
{

    [HostTest("net.arp.rxdispatch updates cache from dispatched reply", HostLane.Fast, 0)]
    private static bool TestUpdatesCacheFromDispatchedReply()
    {
        Span<ArpNeighborCacheEntry> storage = stackalloc ArpNeighborCacheEntry[2];
        ArpNeighborCache cache = ArpNeighborCache.Create(storage);
        byte[] buffer = CreateArpReplyReceiveBuffer(
            ethernetSource: RemoteMac(),
            ethernetDestination: LocalMac(),
            arpSenderMac: RemoteMac(),
            arpSenderAddress: TargetAddress(),
            arpTargetMac: LocalMac(),
            arpTargetAddress: LocalAddress());
        NetworkReceiveDispatchResult dispatch = NetworkReceiveDispatcher.Dispatch(buffer);

        ArpReceiveDispatchCacheUpdateStatus status =
            ArpReceiveDispatchCacheUpdater.TryUpdateFromDispatch(
                ref cache,
                LocalMac(),
                LocalAddress(),
                TargetAddress(),
                buffer,
                dispatch,
                currentTick: 100,
                out ArpReceiveDispatchCacheUpdateResult result);
        ArpNeighborCacheStatus lookupStatus = cache.TryLookup(
            TargetAddress(),
            out ArpNeighborCacheLookupResult lookup);

        return HostCheck.Result(status == ArpReceiveDispatchCacheUpdateStatus.Success
            && result.Succeeded
            && result.DispatchStatus == NetworkReceiveDispatchStatus.Success
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
            && lookup.Entry.LastUpdatedTick == 100);
    }

    [HostTest("net.arp.rxdispatch refreshes existing cache entry", HostLane.Fast, 1)]
    private static bool TestRefreshesExistingCacheEntry()
    {
        Span<ArpNeighborCacheEntry> storage = stackalloc ArpNeighborCacheEntry[1];
        ArpNeighborCache cache = ArpNeighborCache.Create(storage);
        cache.InsertOrUpdate(TargetAddress(), OldRemoteMac(), currentTick: 10, out _);
        byte[] buffer = CreateArpReplyReceiveBuffer(
            ethernetSource: RemoteMac(),
            ethernetDestination: LocalMac(),
            arpSenderMac: RemoteMac(),
            arpSenderAddress: TargetAddress(),
            arpTargetMac: LocalMac(),
            arpTargetAddress: LocalAddress());
        NetworkReceiveDispatchResult dispatch = NetworkReceiveDispatcher.Dispatch(buffer);

        ArpReceiveDispatchCacheUpdateStatus status =
            ArpReceiveDispatchCacheUpdater.TryUpdateFromDispatch(
                ref cache,
                LocalMac(),
                LocalAddress(),
                TargetAddress(),
                buffer,
                dispatch,
                currentTick: 200,
                out ArpReceiveDispatchCacheUpdateResult result);
        ArpNeighborCacheStatus lookupStatus = cache.TryLookup(
            TargetAddress(),
            out ArpNeighborCacheLookupResult lookup);

        return HostCheck.Result(status == ArpReceiveDispatchCacheUpdateStatus.Success
            && result.UpdatedExisting
            && result.CacheCount == 1
            && result.CacheCapacity == 1
            && lookupStatus == ArpNeighborCacheStatus.Success
            && lookup.Entry.MacAddress == RemoteMac()
            && lookup.Entry.LastUpdatedTick == 200
            && cache.Count == 1);
    }

    [HostTest("net.arp.rxdispatch rejects non-arp dispatch", HostLane.Fast, 2)]
    private static bool TestRejectsNonArpDispatch()
    {
        Span<ArpNeighborCacheEntry> storage = stackalloc ArpNeighborCacheEntry[1];
        ArpNeighborCache cache = ArpNeighborCache.Create(storage);
        cache.InsertOrUpdate(TargetAddress(), OldRemoteMac(), currentTick: 10, out _);
        byte[] buffer = CreateIpv4ReceiveBuffer();
        NetworkReceiveDispatchResult dispatch = NetworkReceiveDispatcher.Dispatch(buffer);

        ArpReceiveDispatchCacheUpdateStatus status =
            ArpReceiveDispatchCacheUpdater.TryUpdateFromDispatch(
                ref cache,
                LocalMac(),
                LocalAddress(),
                TargetAddress(),
                buffer,
                dispatch,
                currentTick: 200,
                out ArpReceiveDispatchCacheUpdateResult result);
        ArpNeighborCacheStatus lookupStatus = cache.TryLookup(
            TargetAddress(),
            out ArpNeighborCacheLookupResult lookup);

        return HostCheck.Result(status == ArpReceiveDispatchCacheUpdateStatus.NonArpDispatch
            && !result.Succeeded
            && result.DispatchStatus == NetworkReceiveDispatchStatus.Success
            && result.ArpStatus == ArpNeighborResolutionStatus.UnsupportedEtherType
            && result.CacheStatus == ArpNeighborCacheStatus.Success
            && result.PacketStatus == PacketParseStatus.UnsupportedEtherType
            && lookupStatus == ArpNeighborCacheStatus.Success
            && lookup.Entry.MacAddress == OldRemoteMac()
            && lookup.Entry.LastUpdatedTick == 10
            && cache.Count == 1);
    }

    [HostTest("net.arp.rxdispatch rejects wrong local target", HostLane.Fast, 3)]
    private static bool TestRejectsWrongLocalTarget()
    {
        Span<ArpNeighborCacheEntry> storage = stackalloc ArpNeighborCacheEntry[1];
        ArpNeighborCache cache = ArpNeighborCache.Create(storage);
        byte[] buffer = CreateArpReplyReceiveBuffer(
            ethernetSource: RemoteMac(),
            ethernetDestination: LocalMac(),
            arpSenderMac: RemoteMac(),
            arpSenderAddress: TargetAddress(),
            arpTargetMac: LocalMac(),
            arpTargetAddress: WrongLocalAddress());
        NetworkReceiveDispatchResult dispatch = NetworkReceiveDispatcher.Dispatch(buffer);

        ArpReceiveDispatchCacheUpdateStatus status =
            ArpReceiveDispatchCacheUpdater.TryUpdateFromDispatch(
                ref cache,
                LocalMac(),
                LocalAddress(),
                TargetAddress(),
                buffer,
                dispatch,
                currentTick: 100,
                out ArpReceiveDispatchCacheUpdateResult result);

        return HostCheck.Result(status == ArpReceiveDispatchCacheUpdateStatus.ArpValidationFailed
            && !result.Succeeded
            && result.ArpStatus == ArpNeighborResolutionStatus.TargetAddressMismatch
            && result.CacheStatus == ArpNeighborCacheStatus.Success
            && result.CacheCount == 0
            && result.CacheCapacity == 1
            && cache.Count == 0);
    }

    [HostTest("net.arp.rxdispatch preserves cache on validation failure", HostLane.Fast, 4)]
    private static bool TestPreservesCacheOnValidationFailure()
    {
        Span<ArpNeighborCacheEntry> storage = stackalloc ArpNeighborCacheEntry[1];
        ArpNeighborCache cache = ArpNeighborCache.Create(storage);
        cache.InsertOrUpdate(TargetAddress(), OldRemoteMac(), currentTick: 10, out _);
        byte[] buffer = CreateArpReplyReceiveBuffer(
            ethernetSource: WrongRemoteMac(),
            ethernetDestination: LocalMac(),
            arpSenderMac: RemoteMac(),
            arpSenderAddress: TargetAddress(),
            arpTargetMac: LocalMac(),
            arpTargetAddress: LocalAddress());
        NetworkReceiveDispatchResult dispatch = NetworkReceiveDispatcher.Dispatch(buffer);

        ArpReceiveDispatchCacheUpdateStatus status =
            ArpReceiveDispatchCacheUpdater.TryUpdateFromDispatch(
                ref cache,
                LocalMac(),
                LocalAddress(),
                TargetAddress(),
                buffer,
                dispatch,
                currentTick: 200,
                out ArpReceiveDispatchCacheUpdateResult result);
        ArpNeighborCacheStatus lookupStatus = cache.TryLookup(
            TargetAddress(),
            out ArpNeighborCacheLookupResult lookup);

        return HostCheck.Result(status == ArpReceiveDispatchCacheUpdateStatus.ArpValidationFailed
            && !result.Succeeded
            && result.ArpStatus == ArpNeighborResolutionStatus.EthernetSourceMismatch
            && result.CacheCount == 1
            && result.CacheCapacity == 1
            && lookupStatus == ArpNeighborCacheStatus.Success
            && lookup.Entry.MacAddress == OldRemoteMac()
            && lookup.Entry.LastUpdatedTick == 10
            && cache.Count == 1);
    }

    [HostTest("net.arp.rxdispatch rejects truncated payload range", HostLane.Fast, 5)]
    private static bool TestRejectsTruncatedPayloadRange()
    {
        Span<ArpNeighborCacheEntry> storage = stackalloc ArpNeighborCacheEntry[1];
        ArpNeighborCache cache = ArpNeighborCache.Create(storage);
        cache.InsertOrUpdate(TargetAddress(), OldRemoteMac(), currentTick: 10, out _);
        byte[] buffer = new byte[ExpectedPayloadOffset + ArpPacket.PacketLength - 1];
        NetworkReceiveDispatchResult dispatch = CreateSyntheticArpDispatch(
            payloadOffset: ExpectedPayloadOffset,
            payloadLength: ArpPacket.PacketLength);

        ArpReceiveDispatchCacheUpdateStatus status =
            ArpReceiveDispatchCacheUpdater.TryUpdateFromDispatch(
                ref cache,
                LocalMac(),
                LocalAddress(),
                TargetAddress(),
                buffer,
                dispatch,
                currentTick: 200,
                out ArpReceiveDispatchCacheUpdateResult result);
        ArpNeighborCacheStatus lookupStatus = cache.TryLookup(
            TargetAddress(),
            out ArpNeighborCacheLookupResult lookup);

        return HostCheck.Result(status == ArpReceiveDispatchCacheUpdateStatus.PayloadRangeInvalid
            && !result.Succeeded
            && result.ArpStatus == ArpNeighborResolutionStatus.FrameParseFailed
            && result.PacketStatus == PacketParseStatus.BufferTooSmall
            && lookupStatus == ArpNeighborCacheStatus.Success
            && lookup.Entry.MacAddress == OldRemoteMac()
            && lookup.Entry.LastUpdatedTick == 10
            && cache.Count == 1);
    }

    [HostTest("net.arp.rxdispatch preserves no-nic-io boundary", HostLane.Fast, 6)]
    private static bool TestPreservesNoNicIoBoundary()
    {
        return HostCheck.Result(ArpReceiveDispatchCacheUpdater.RequiresCallerOwnedCacheStorage
            && !ArpReceiveDispatchCacheUpdater.ReadsNic
            && !ArpReceiveDispatchCacheUpdater.PollsNic
            && !ArpReceiveDispatchCacheUpdater.AllocatesCacheStorage
            && !ArpReceiveDispatchCacheUpdater.DispatchesSockets
            && !ArpReceiveDispatchCacheUpdater.RoutesPackets
            && !RuntimeHelpers.IsReferenceOrContainsReferences<ArpReceiveDispatchCacheUpdateResult>());
    }

    [HostTest("net.arp.rxdispatch default result is not success", HostLane.Fast, 7)]
    private static bool TestDefaultResultIsNotSuccess()
    {
        ArpReceiveDispatchCacheUpdateResult defaultResult = default;
        ArpReceiveDispatchCacheUpdateResult success = ArpReceiveDispatchCacheUpdateResult.Success(
            NetworkReceiveDispatchStatus.Success,
            ArpNeighborResolutionStatus.Success,
            ArpNeighborCacheStatus.Success,
            PacketParseStatus.Success,
            ArpNeighborResolutionState.Resolved,
            RemoteMac(),
            cacheCount: 1,
            cacheCapacity: 2,
            updatedExisting: true);
        ArpReceiveDispatchCacheUpdateResult failure = ArpReceiveDispatchCacheUpdateResult.Fail(
            ArpReceiveDispatchCacheUpdateStatus.ArpValidationFailed,
            NetworkReceiveDispatchStatus.Success,
            ArpNeighborResolutionStatus.TargetAddressMismatch,
            ArpNeighborCacheStatus.Success,
            PacketParseStatus.Success,
            ArpNeighborResolutionState.Failed,
            default,
            cacheCount: 3,
            cacheCapacity: 4);

        return HostCheck.Result(!defaultResult.IsInitialized
            && !defaultResult.Succeeded
            && defaultResult.Status == ArpReceiveDispatchCacheUpdateStatus.Success
            && defaultResult.DispatchStatus == NetworkReceiveDispatchStatus.Success
            && defaultResult.ArpStatus == ArpNeighborResolutionStatus.Success
            && defaultResult.CacheStatus == ArpNeighborCacheStatus.Success
            && defaultResult.PacketStatus == PacketParseStatus.Success
            && defaultResult.ArpState == ArpNeighborResolutionState.Initial
            && defaultResult.ResolvedMac == default
            && defaultResult.CacheCount == 0
            && defaultResult.CacheCapacity == 0
            && !defaultResult.UpdatedExisting
            && success.IsInitialized
            && success.Succeeded
            && success.Status == ArpReceiveDispatchCacheUpdateStatus.Success
            && success.ResolvedMac == RemoteMac()
            && success.CacheCount == 1
            && success.CacheCapacity == 2
            && success.UpdatedExisting
            && failure.IsInitialized
            && !failure.Succeeded
            && failure.Status == ArpReceiveDispatchCacheUpdateStatus.ArpValidationFailed
            && failure.DispatchStatus == NetworkReceiveDispatchStatus.Success
            && failure.ArpStatus == ArpNeighborResolutionStatus.TargetAddressMismatch
            && failure.CacheStatus == ArpNeighborCacheStatus.Success
            && failure.PacketStatus == PacketParseStatus.Success
            && failure.ArpState == ArpNeighborResolutionState.Failed
            && failure.CacheCount == 3
            && failure.CacheCapacity == 4
            && !failure.UpdatedExisting
            && !RuntimeHelpers.IsReferenceOrContainsReferences<ArpReceiveDispatchCacheUpdateResult>());
    }

    private static NetworkReceiveDispatchResult CreateSyntheticArpDispatch(
        int payloadOffset,
        int payloadLength)
    {
        EthernetHeader ethernet = new EthernetHeader(
            LocalMac(),
            RemoteMac(),
            EthernetHeader.EtherTypeArp);
        VirtioNetReceiveFrameClassificationResult classifier =
            VirtioNetReceiveFrameClassificationResult.Create(
                VirtioNetReceiveFrameClassifierStatus.Success,
                PacketParseStatus.Success,
                default,
                ethernet,
                EthernetPayloadKind.Arp,
                payloadOffset,
                payloadLength);
        ArpPacket packet = new ArpPacket(
            ArpPacket.OperationReply,
            RemoteMac(),
            TargetAddress(),
            LocalMac(),
            LocalAddress());
        return NetworkReceiveDispatchResult.Arp(classifier, packet);
    }

    private static byte[] CreateArpReplyReceiveBuffer(
        MacAddress ethernetSource,
        MacAddress ethernetDestination,
        MacAddress arpSenderMac,
        Ipv4Address arpSenderAddress,
        MacAddress arpTargetMac,
        Ipv4Address arpTargetAddress)
    {
        byte[] buffer = new byte[ExpectedPayloadOffset + ArpPacket.PacketLength];
        EthernetHeader ethernet = new EthernetHeader(
            ethernetDestination,
            ethernetSource,
            EthernetHeader.EtherTypeArp);
        PacketParseStatus ethernetStatus = EthernetHeader.TryWrite(
            buffer.AsSpan(VirtioNetHeader.HeaderLengthWithoutMergeableBuffers),
            ethernet);
        PacketParseStatus arpStatus = ArpPacket.TryWrite(
            buffer.AsSpan(ExpectedPayloadOffset, ArpPacket.PacketLength),
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
            throw new InvalidOperationException("Failed to build ARP reply receive buffer.");
        }

        return buffer;
    }

    private static byte[] CreateIpv4ReceiveBuffer()
    {
        byte[] payload = { 0x70, 0x69, 0x6E, 0x67 };
        byte[] buffer = new byte[
            VirtioNetHeader.HeaderLengthWithoutMergeableBuffers +
            EthernetHeader.HeaderLength +
            Ipv4Header.MinHeaderLength +
            UdpHeader.HeaderLength +
            payload.Length];

        EthernetIpv4UdpPacketInfo info = new EthernetIpv4UdpPacketInfo(
            LocalMac(),
            RemoteMac(),
            LocalAddress(),
            TargetAddress(),
            sourcePort: 12345,
            destinationPort: 8080,
            timeToLive: 64,
            identification: 0x1234);

        PacketParseStatus status = EthernetIpv4UdpPacket.TryWrite(
            buffer.AsSpan(VirtioNetHeader.HeaderLengthWithoutMergeableBuffers),
            info,
            payload,
            out int bytesWritten);
        if (status != PacketParseStatus.Success
            || bytesWritten != buffer.Length - VirtioNetHeader.HeaderLengthWithoutMergeableBuffers)
        {
            throw new InvalidOperationException("Failed to build IPv4 receive buffer.");
        }

        return buffer;
    }

    private static int ExpectedPayloadOffset =>
        VirtioNetHeader.HeaderLengthWithoutMergeableBuffers + EthernetHeader.HeaderLength;

    private static MacAddress LocalMac()
    {
        return new MacAddress(0x02, 0x00, 0x00, 0x00, 0x00, 0x10);
    }

    private static MacAddress RemoteMac()
    {
        return new MacAddress(0x02, 0x00, 0x00, 0x00, 0x00, 0x20);
    }

    private static MacAddress OldRemoteMac()
    {
        return new MacAddress(0x02, 0x00, 0x00, 0x00, 0x00, 0x21);
    }

    private static MacAddress WrongRemoteMac()
    {
        return new MacAddress(0x02, 0x00, 0x00, 0x00, 0x00, 0x22);
    }

    private static Ipv4Address LocalAddress()
    {
        return new Ipv4Address(192, 0, 2, 10);
    }

    private static Ipv4Address WrongLocalAddress()
    {
        return new Ipv4Address(192, 0, 2, 11);
    }

    private static Ipv4Address TargetAddress()
    {
        return new Ipv4Address(192, 0, 2, 1);
    }
}
