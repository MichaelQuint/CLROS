using System.Runtime.CompilerServices;
using System.Text;
using OS.Net;

internal static class Ipv4NeighborRoutePlannerTests
{

    [HostTest("net.ipv4.neighbor returns cached mac", HostLane.Fast, 0)]
    private static bool TestReturnsCachedMac()
    {
        Span<ArpNeighborCacheEntry> storage = stackalloc ArpNeighborCacheEntry[1];
        ArpNeighborCache cache = ArpNeighborCache.Create(storage);
        cache.InsertOrUpdate(TargetAddress(), TargetMac(), currentTick: 10, out _);

        Ipv4NeighborRoutePlanStatus status = Ipv4NeighborRoutePlanner.TryPlan(
            ref cache,
            LocalMac(),
            LocalAddress(),
            TargetAddress(),
            Span<byte>.Empty,
            out Ipv4NeighborRoutePlanResult result);

        return HostCheck.Result(status == Ipv4NeighborRoutePlanStatus.Success
            && result.Succeeded
            && result.Action == Ipv4NeighborRoutePlanAction.UseCachedMac
            && result.TargetMac == TargetMac()
            && result.BytesWritten == 0
            && result.CacheStatus == ArpNeighborCacheStatus.Success
            && result.ArpStatus == ArpNeighborResolutionStatus.Success
            && result.PacketStatus == PacketParseStatus.Success
            && result.CacheCount == 1
            && result.CacheCapacity == 1
            && cache.Count == 1);
    }

    [HostTest("net.ipv4.neighbor writes arp request on cache miss", HostLane.Fast, 1)]
    private static bool TestWritesArpRequestOnCacheMiss()
    {
        Span<ArpNeighborCacheEntry> storage = stackalloc ArpNeighborCacheEntry[1];
        ArpNeighborCache cache = ArpNeighborCache.Create(storage);
        Span<byte> frame = stackalloc byte[ArpNeighborResolutionTransaction.RequestFrameLength];

        Ipv4NeighborRoutePlanStatus status = Ipv4NeighborRoutePlanner.TryPlan(
            ref cache,
            LocalMac(),
            LocalAddress(),
            TargetAddress(),
            frame,
            out Ipv4NeighborRoutePlanResult result);
        if (status != Ipv4NeighborRoutePlanStatus.Success
            || !result.Succeeded
            || result.Action != Ipv4NeighborRoutePlanAction.SendArpRequest
            || result.BytesWritten != ArpNeighborResolutionTransaction.RequestFrameLength
            || result.CacheStatus != ArpNeighborCacheStatus.NotFound
            || result.ArpStatus != ArpNeighborResolutionStatus.Success
            || result.PacketStatus != PacketParseStatus.Success
            || result.CacheCount != 0
            || result.CacheCapacity != 1
            || cache.Count != 0)
        {
            return HostCheck.Result(false);
        }

        return HostCheck.Result(IsExpectedArpRequest(frame));
    }

    [HostTest("net.ipv4.neighbor preserves cache on request write", HostLane.Fast, 2)]
    private static bool TestPreservesCacheOnRequestWrite()
    {
        Span<ArpNeighborCacheEntry> storage = stackalloc ArpNeighborCacheEntry[2];
        ArpNeighborCache cache = ArpNeighborCache.Create(storage);
        cache.InsertOrUpdate(CachedAddress(), CachedMac(), currentTick: 100, out _);
        Span<byte> frame = stackalloc byte[ArpNeighborResolutionTransaction.RequestFrameLength];

        Ipv4NeighborRoutePlanStatus status = Ipv4NeighborRoutePlanner.TryPlan(
            ref cache,
            LocalMac(),
            LocalAddress(),
            TargetAddress(),
            frame,
            out Ipv4NeighborRoutePlanResult result);
        ArpNeighborCacheStatus cachedStatus = cache.TryLookup(
            CachedAddress(),
            out ArpNeighborCacheLookupResult cached);
        ArpNeighborCacheStatus targetStatus = cache.TryLookup(
            TargetAddress(),
            out ArpNeighborCacheLookupResult target);

        return HostCheck.Result(status == Ipv4NeighborRoutePlanStatus.Success
            && result.Action == Ipv4NeighborRoutePlanAction.SendArpRequest
            && result.CacheCount == 1
            && result.CacheCapacity == 2
            && cache.Count == 1
            && cachedStatus == ArpNeighborCacheStatus.Success
            && cached.Entry.MacAddress == CachedMac()
            && cached.Entry.LastUpdatedTick == 100
            && targetStatus == ArpNeighborCacheStatus.NotFound
            && !target.Succeeded
            && IsExpectedArpRequest(frame));
    }

    [HostTest("net.ipv4.neighbor rejects undersized request buffer", HostLane.Fast, 3)]
    private static bool TestRejectsUndersizedRequestBuffer()
    {
        Span<ArpNeighborCacheEntry> storage = stackalloc ArpNeighborCacheEntry[1];
        ArpNeighborCache cache = ArpNeighborCache.Create(storage);
        Span<byte> frame = stackalloc byte[ArpNeighborResolutionTransaction.RequestFrameLength - 1];

        Ipv4NeighborRoutePlanStatus status = Ipv4NeighborRoutePlanner.TryPlan(
            ref cache,
            LocalMac(),
            LocalAddress(),
            TargetAddress(),
            frame,
            out Ipv4NeighborRoutePlanResult result);

        return HostCheck.Result(status == Ipv4NeighborRoutePlanStatus.RequestBufferTooSmall
            && !result.Succeeded
            && result.Action == Ipv4NeighborRoutePlanAction.None
            && result.BytesWritten == 0
            && result.CacheStatus == ArpNeighborCacheStatus.NotFound
            && result.ArpStatus == ArpNeighborResolutionStatus.BufferTooSmall
            && result.PacketStatus == PacketParseStatus.BufferTooSmall
            && result.CacheCount == 0
            && result.CacheCapacity == 1
            && cache.Count == 0);
    }

    [HostTest("net.ipv4.neighbor handles cache capacity unchanged", HostLane.Fast, 4)]
    private static bool TestHandlesCacheCapacityUnchanged()
    {
        Span<ArpNeighborCacheEntry> storage = stackalloc ArpNeighborCacheEntry[1];
        ArpNeighborCache cache = ArpNeighborCache.Create(storage);
        cache.InsertOrUpdate(CachedAddress(), CachedMac(), currentTick: 200, out _);
        Span<byte> frame = stackalloc byte[ArpNeighborResolutionTransaction.RequestFrameLength];

        Ipv4NeighborRoutePlanStatus status = Ipv4NeighborRoutePlanner.TryPlan(
            ref cache,
            LocalMac(),
            LocalAddress(),
            TargetAddress(),
            frame,
            out Ipv4NeighborRoutePlanResult result);

        return HostCheck.Result(status == Ipv4NeighborRoutePlanStatus.Success
            && result.Action == Ipv4NeighborRoutePlanAction.SendArpRequest
            && result.CacheStatus == ArpNeighborCacheStatus.NotFound
            && result.CacheCount == 1
            && result.CacheCapacity == 1
            && cache.Count == 1
            && cache.Capacity == 1
            && IsExpectedArpRequest(frame));
    }

    [HostTest("net.ipv4.neighbor rejects invalid target input", HostLane.Fast, 5)]
    private static bool TestRejectsInvalidTargetInput()
    {
        Span<ArpNeighborCacheEntry> storage = stackalloc ArpNeighborCacheEntry[1];
        ArpNeighborCache cache = ArpNeighborCache.Create(storage);
        byte[] frame = new byte[ArpNeighborResolutionTransaction.RequestFrameLength];
        Array.Fill(frame, (byte)0xA5);

        Ipv4NeighborRoutePlanStatus status = Ipv4NeighborRoutePlanner.TryPlan(
            ref cache,
            LocalMac(),
            LocalAddress(),
            default,
            frame,
            out Ipv4NeighborRoutePlanResult result);

        return HostCheck.Result(status == Ipv4NeighborRoutePlanStatus.InvalidTargetAddress
            && !result.Succeeded
            && result.Action == Ipv4NeighborRoutePlanAction.None
            && result.BytesWritten == 0
            && result.CacheStatus == ArpNeighborCacheStatus.InvalidAddress
            && result.ArpStatus == ArpNeighborResolutionStatus.Success
            && result.PacketStatus == PacketParseStatus.Success
            && result.CacheCount == 0
            && result.CacheCapacity == 1
            && cache.Count == 0
            && frame[0] == 0xA5
            && frame[^1] == 0xA5);
    }

    [HostTest("net.ipv4.neighbor default result is not success", HostLane.Fast, 6)]
    private static bool TestDefaultResultIsNotSuccess()
    {
        Ipv4NeighborRoutePlanResult defaultResult = default;
        Ipv4NeighborRoutePlanResult cached = Ipv4NeighborRoutePlanResult.Cached(
            TargetMac(),
            ArpNeighborCacheStatus.Success,
            cacheCount: 1,
            cacheCapacity: 2);
        Ipv4NeighborRoutePlanResult requested = Ipv4NeighborRoutePlanResult.RequestWritten(
            ArpNeighborResolutionTransaction.RequestFrameLength,
            ArpNeighborCacheStatus.NotFound,
            ArpNeighborResolutionStatus.Success,
            PacketParseStatus.Success,
            cacheCount: 0,
            cacheCapacity: 2);
        Ipv4NeighborRoutePlanResult failure = Ipv4NeighborRoutePlanResult.Fail(
            Ipv4NeighborRoutePlanStatus.RequestBufferTooSmall,
            ArpNeighborCacheStatus.NotFound,
            ArpNeighborResolutionStatus.BufferTooSmall,
            PacketParseStatus.BufferTooSmall,
            cacheCount: 1,
            cacheCapacity: 2);

        return HostCheck.Result(!defaultResult.IsInitialized
            && !defaultResult.Succeeded
            && defaultResult.Status == Ipv4NeighborRoutePlanStatus.Success
            && cached.IsInitialized
            && cached.Succeeded
            && cached.Action == Ipv4NeighborRoutePlanAction.UseCachedMac
            && cached.TargetMac == TargetMac()
            && cached.CacheStatus == ArpNeighborCacheStatus.Success
            && requested.IsInitialized
            && requested.Succeeded
            && requested.Action == Ipv4NeighborRoutePlanAction.SendArpRequest
            && requested.BytesWritten == ArpNeighborResolutionTransaction.RequestFrameLength
            && requested.CacheCapacity == 2
            && failure.IsInitialized
            && !failure.Succeeded
            && failure.Status == Ipv4NeighborRoutePlanStatus.RequestBufferTooSmall
            && failure.Action == Ipv4NeighborRoutePlanAction.None
            && failure.CacheStatus == ArpNeighborCacheStatus.NotFound
            && failure.ArpStatus == ArpNeighborResolutionStatus.BufferTooSmall
            && failure.PacketStatus == PacketParseStatus.BufferTooSmall
            && failure.CacheCount == 1
            && failure.CacheCapacity == 2
            && !RuntimeHelpers.IsReferenceOrContainsReferences<Ipv4NeighborRoutePlanResult>());
    }

    [HostTest("net.ipv4.neighbor.formatter formats cached route", HostLane.Fast, 7)]
    private static bool TestFormatterFormatsCachedRoute()
    {
        Ipv4NeighborRoutePlanResult result = CreateCachedRoutePlan();

        Span<byte> destination = stackalloc byte[256];
        destination.Fill(0);
        Ipv4NeighborRoutePlanDiagnosticFormatResult query =
            Ipv4NeighborRoutePlanDiagnosticFormatter.QueryRequiredBytes(result);
        Ipv4NeighborRoutePlanDiagnosticFormatResult format =
            Ipv4NeighborRoutePlanDiagnosticFormatter.FormatResult(result, destination);
        string text = Encoding.ASCII.GetString(destination[..format.BytesWritten]);
        string expected =
            "IPV4_NEIGHBOR_ROUTE status=Success action=UseCachedMac cache=Success arp=Success " +
            "packet=Success bytes=0 cacheCount=1 cacheCapacity=1 targetMac=02:00:00:00:00:20";

        return HostCheck.Result(query.IsInitialized
            && !query.Succeeded
            && query.Status == Ipv4NeighborRoutePlanDiagnosticFormatStatus.Success
            && query.RouteStatus == Ipv4NeighborRoutePlanStatus.Success
            && query.BytesWritten == 0
            && query.RequiredBytes == expected.Length
            && query.AvailableBytes == 0
            && format.Succeeded
            && format.RouteStatus == Ipv4NeighborRoutePlanStatus.Success
            && format.BytesWritten == expected.Length
            && format.RequiredBytes == expected.Length
            && format.AvailableBytes == destination.Length
            && text == expected);
    }

    [HostTest("net.ipv4.neighbor.formatter formats arp request and failure", HostLane.Fast, 8)]
    private static bool TestFormatterFormatsArpRequestAndFailure()
    {
        Ipv4NeighborRoutePlanResult request = CreateArpRequestPlan();
        Ipv4NeighborRoutePlanResult failure = CreateArpRequestFailurePlan();

        Span<byte> requestDestination = stackalloc byte[256];
        requestDestination.Fill(0);
        Ipv4NeighborRoutePlanDiagnosticFormatResult requestQuery =
            Ipv4NeighborRoutePlanDiagnosticFormatter.QueryRequiredBytes(request);
        Ipv4NeighborRoutePlanDiagnosticFormatResult requestFormat =
            Ipv4NeighborRoutePlanDiagnosticFormatter.FormatResult(request, requestDestination);
        string requestText = Encoding.ASCII.GetString(requestDestination[..requestFormat.BytesWritten]);
        string expectedRequest =
            "IPV4_NEIGHBOR_ROUTE status=Success action=SendArpRequest cache=NotFound " +
            "arp=Success packet=Success bytes=" +
            ArpNeighborResolutionTransaction.RequestFrameLength +
            " cacheCount=0 cacheCapacity=1";

        Span<byte> failureDestination = stackalloc byte[256];
        failureDestination.Fill(0);
        Ipv4NeighborRoutePlanDiagnosticFormatResult failureQuery =
            Ipv4NeighborRoutePlanDiagnosticFormatter.QueryRequiredBytes(failure);
        Ipv4NeighborRoutePlanDiagnosticFormatResult failureFormat =
            Ipv4NeighborRoutePlanDiagnosticFormatter.FormatResult(failure, failureDestination);
        string failureText = Encoding.ASCII.GetString(failureDestination[..failureFormat.BytesWritten]);
        string expectedFailure =
            "IPV4_NEIGHBOR_ROUTE status=RequestBufferTooSmall action=None cache=NotFound " +
            "arp=BufferTooSmall packet=BufferTooSmall bytes=0 cacheCount=0 cacheCapacity=1";

        return HostCheck.Result(requestQuery.IsInitialized
            && requestQuery.Status == Ipv4NeighborRoutePlanDiagnosticFormatStatus.Success
            && requestQuery.RequiredBytes == expectedRequest.Length
            && requestFormat.Succeeded
            && requestFormat.RouteStatus == Ipv4NeighborRoutePlanStatus.Success
            && requestFormat.BytesWritten == expectedRequest.Length
            && requestText == expectedRequest
            && failureQuery.IsInitialized
            && failureQuery.Status == Ipv4NeighborRoutePlanDiagnosticFormatStatus.Success
            && failureQuery.RequiredBytes == expectedFailure.Length
            && failureFormat.Succeeded
            && failureFormat.RouteStatus == Ipv4NeighborRoutePlanStatus.RequestBufferTooSmall
            && failureFormat.BytesWritten == expectedFailure.Length
            && failureText == expectedFailure);
    }

    [HostTest("net.ipv4.neighbor.formatter reports short destination", HostLane.Fast, 9)]
    private static bool TestFormatterReportsShortDestination()
    {
        Ipv4NeighborRoutePlanResult result = CreateCachedRoutePlan();
        Ipv4NeighborRoutePlanDiagnosticFormatResult query =
            Ipv4NeighborRoutePlanDiagnosticFormatter.QueryRequiredBytes(result);

        Span<byte> destination = stackalloc byte[24];
        destination.Fill(0xA5);
        Ipv4NeighborRoutePlanDiagnosticFormatResult shortResult =
            Ipv4NeighborRoutePlanDiagnosticFormatter.FormatResult(result, destination);

        return HostCheck.Result(shortResult.IsInitialized
            && !shortResult.Succeeded
            && shortResult.Status == Ipv4NeighborRoutePlanDiagnosticFormatStatus.DestinationTooSmall
            && shortResult.RouteStatus == Ipv4NeighborRoutePlanStatus.Success
            && shortResult.BytesWritten == 0
            && shortResult.RequiredBytes == query.RequiredBytes
            && shortResult.AvailableBytes == destination.Length
            && query.RequiredBytes > destination.Length
            && destination[0] == 0xA5
            && destination[23] == 0xA5);
    }

    [HostTest("net.ipv4.neighbor.formatter rejects default result", HostLane.Fast, 10)]
    private static bool TestFormatterRejectsDefaultResult()
    {
        Span<byte> destination = stackalloc byte[64];
        destination.Fill(0xCC);

        Ipv4NeighborRoutePlanResult defaultResult = default;
        Ipv4NeighborRoutePlanDiagnosticFormatResult invalidFormat =
            Ipv4NeighborRoutePlanDiagnosticFormatter.FormatResult(defaultResult, destination);
        Ipv4NeighborRoutePlanDiagnosticFormatResult invalidQuery =
            Ipv4NeighborRoutePlanDiagnosticFormatter.QueryRequiredBytes(defaultResult);
        Ipv4NeighborRoutePlanDiagnosticFormatResult defaultFormat = default;

        Ipv4NeighborRoutePlanResult unknownRouteStatus = Ipv4NeighborRoutePlanResult.Fail(
            (Ipv4NeighborRoutePlanStatus)999,
            ArpNeighborCacheStatus.NotFound,
            ArpNeighborResolutionStatus.Success,
            PacketParseStatus.Success,
            cacheCount: 0,
            cacheCapacity: 1);
        Ipv4NeighborRoutePlanDiagnosticFormatResult unknownFormat =
            Ipv4NeighborRoutePlanDiagnosticFormatter.FormatResult(unknownRouteStatus, destination);
        Ipv4NeighborRoutePlanResult unknownCacheStatus = Ipv4NeighborRoutePlanResult.RequestWritten(
            ArpNeighborResolutionTransaction.RequestFrameLength,
            (ArpNeighborCacheStatus)999,
            ArpNeighborResolutionStatus.Success,
            PacketParseStatus.Success,
            cacheCount: 0,
            cacheCapacity: 1);
        Ipv4NeighborRoutePlanDiagnosticFormatResult unknownCacheFormat =
            Ipv4NeighborRoutePlanDiagnosticFormatter.FormatResult(unknownCacheStatus, destination);
        Ipv4NeighborRoutePlanResult unknownArpStatus = Ipv4NeighborRoutePlanResult.RequestWritten(
            ArpNeighborResolutionTransaction.RequestFrameLength,
            ArpNeighborCacheStatus.NotFound,
            (ArpNeighborResolutionStatus)999,
            PacketParseStatus.Success,
            cacheCount: 0,
            cacheCapacity: 1);
        Ipv4NeighborRoutePlanDiagnosticFormatResult unknownArpFormat =
            Ipv4NeighborRoutePlanDiagnosticFormatter.FormatResult(unknownArpStatus, destination);
        Ipv4NeighborRoutePlanResult unknownPacketStatus = Ipv4NeighborRoutePlanResult.RequestWritten(
            ArpNeighborResolutionTransaction.RequestFrameLength,
            ArpNeighborCacheStatus.NotFound,
            ArpNeighborResolutionStatus.Success,
            (PacketParseStatus)999,
            cacheCount: 0,
            cacheCapacity: 1);
        Ipv4NeighborRoutePlanDiagnosticFormatResult unknownPacketFormat =
            Ipv4NeighborRoutePlanDiagnosticFormatter.FormatResult(unknownPacketStatus, destination);

        return HostCheck.Result(invalidFormat.IsInitialized
            && !invalidFormat.Succeeded
            && invalidFormat.Status == Ipv4NeighborRoutePlanDiagnosticFormatStatus.InvalidResult
            && invalidFormat.RouteStatus == Ipv4NeighborRoutePlanStatus.Success
            && invalidFormat.BytesWritten == 0
            && invalidFormat.RequiredBytes == 0
            && invalidFormat.AvailableBytes == destination.Length
            && invalidQuery.IsInitialized
            && !invalidQuery.Succeeded
            && invalidQuery.Status == Ipv4NeighborRoutePlanDiagnosticFormatStatus.InvalidResult
            && invalidQuery.RequiredBytes == 0
            && unknownFormat.IsInitialized
            && !unknownFormat.Succeeded
            && unknownFormat.Status == Ipv4NeighborRoutePlanDiagnosticFormatStatus.InvalidResult
            && unknownFormat.RouteStatus == (Ipv4NeighborRoutePlanStatus)999
            && unknownFormat.BytesWritten == 0
            && unknownCacheFormat.IsInitialized
            && !unknownCacheFormat.Succeeded
            && unknownCacheFormat.Status == Ipv4NeighborRoutePlanDiagnosticFormatStatus.InvalidResult
            && unknownCacheFormat.BytesWritten == 0
            && unknownArpFormat.IsInitialized
            && !unknownArpFormat.Succeeded
            && unknownArpFormat.Status == Ipv4NeighborRoutePlanDiagnosticFormatStatus.InvalidResult
            && unknownArpFormat.BytesWritten == 0
            && unknownPacketFormat.IsInitialized
            && !unknownPacketFormat.Succeeded
            && unknownPacketFormat.Status == Ipv4NeighborRoutePlanDiagnosticFormatStatus.InvalidResult
            && unknownPacketFormat.BytesWritten == 0
            && !defaultFormat.IsInitialized
            && !defaultFormat.Succeeded
            && defaultFormat.Status == Ipv4NeighborRoutePlanDiagnosticFormatStatus.Success
            && defaultFormat.RouteStatus == Ipv4NeighborRoutePlanStatus.Success
            && destination[0] == 0xCC
            && destination[63] == 0xCC);
    }

    [HostTest("net.ipv4.neighbor.formatter metadata contains no managed references", HostLane.Fast, 11)]
    private static bool TestFormatterMetadataContainsNoManagedReferences()
    {
        Ipv4NeighborRoutePlanResult result = CreateArpRequestPlan();
        Ipv4NeighborRoutePlanDiagnosticFormatResult query =
            Ipv4NeighborRoutePlanDiagnosticFormatter.QueryRequiredBytes(result);

        return HostCheck.Result(query.IsInitialized
            && query.Status == Ipv4NeighborRoutePlanDiagnosticFormatStatus.Success
            && query.RouteStatus == Ipv4NeighborRoutePlanStatus.Success
            && !query.Succeeded
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                Ipv4NeighborRoutePlanDiagnosticFormatResult>());
    }

    [HostTest("net.ipv4.neighbor.formatter uses shared ascii text builder", HostLane.Fast, 12)]
    private static bool TestFormatterUsesSharedAsciiTextBuilder()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "OS.Kernel",
            "Net",
            "Ipv4NeighborRoutePlanDiagnosticFormatter.cs"));

        return HostCheck.Result(source.Contains("AsciiTextBuilder.AppendBytes(destination, offset, value)", StringComparison.Ordinal)
            && source.Contains("AsciiTextBuilder.AppendInt64Decimal(destination, offset, value)", StringComparison.Ordinal)
            && source.Contains("AsciiTextBuilder.AppendByte(destination, offset", StringComparison.Ordinal)
            && source.Contains("AsciiDecimalFormatter.CountInt32Digits", StringComparison.Ordinal)
            && !source.Contains("value.CopyTo(destination[offset..])", StringComparison.Ordinal)
            && !source.Contains("AsciiDecimalFormatter.FormatInt32(destination[offset..], value)", StringComparison.Ordinal)
            && !source.Contains("private static void AppendInt32", StringComparison.Ordinal)
            && !source.Contains("private static void Append(ReadOnlySpan<byte>", StringComparison.Ordinal));
    }

    private static Ipv4NeighborRoutePlanResult CreateCachedRoutePlan()
    {
        Span<ArpNeighborCacheEntry> storage = stackalloc ArpNeighborCacheEntry[1];
        ArpNeighborCache cache = ArpNeighborCache.Create(storage);
        cache.InsertOrUpdate(TargetAddress(), TargetMac(), currentTick: 10, out _);

        Ipv4NeighborRoutePlanStatus status = Ipv4NeighborRoutePlanner.TryPlan(
            ref cache,
            LocalMac(),
            LocalAddress(),
            TargetAddress(),
            Span<byte>.Empty,
            out Ipv4NeighborRoutePlanResult result);
        if (status != Ipv4NeighborRoutePlanStatus.Success || !result.Succeeded)
        {
            throw new InvalidOperationException("Failed to compose cached route fixture.");
        }

        return result;
    }

    private static Ipv4NeighborRoutePlanResult CreateArpRequestPlan()
    {
        Span<ArpNeighborCacheEntry> storage = stackalloc ArpNeighborCacheEntry[1];
        ArpNeighborCache cache = ArpNeighborCache.Create(storage);
        Span<byte> frame = stackalloc byte[ArpNeighborResolutionTransaction.RequestFrameLength];

        Ipv4NeighborRoutePlanStatus status = Ipv4NeighborRoutePlanner.TryPlan(
            ref cache,
            LocalMac(),
            LocalAddress(),
            TargetAddress(),
            frame,
            out Ipv4NeighborRoutePlanResult result);
        if (status != Ipv4NeighborRoutePlanStatus.Success || !result.Succeeded)
        {
            throw new InvalidOperationException("Failed to compose ARP request fixture.");
        }

        return result;
    }

    private static Ipv4NeighborRoutePlanResult CreateArpRequestFailurePlan()
    {
        Span<ArpNeighborCacheEntry> storage = stackalloc ArpNeighborCacheEntry[1];
        ArpNeighborCache cache = ArpNeighborCache.Create(storage);
        Span<byte> frame = stackalloc byte[ArpNeighborResolutionTransaction.RequestFrameLength - 1];

        Ipv4NeighborRoutePlanStatus status = Ipv4NeighborRoutePlanner.TryPlan(
            ref cache,
            LocalMac(),
            LocalAddress(),
            TargetAddress(),
            frame,
            out Ipv4NeighborRoutePlanResult result);
        if (status != Ipv4NeighborRoutePlanStatus.RequestBufferTooSmall || result.Succeeded)
        {
            throw new InvalidOperationException("Failed to compose ARP request failure fixture.");
        }

        return result;
    }

    private static bool IsExpectedArpRequest(ReadOnlySpan<byte> frame)
    {
        PacketParseStatus ethernetStatus = EthernetHeader.TryDispatch(
            frame,
            out EthernetHeader ethernet,
            out EthernetPayloadKind payloadKind,
            out ReadOnlySpan<byte> payload);
        PacketParseStatus arpStatus = ArpPacket.TryParse(payload, out ArpPacket arp);

        return ethernetStatus == PacketParseStatus.Success
            && payloadKind == EthernetPayloadKind.Arp
            && arpStatus == PacketParseStatus.Success
            && ethernet.Destination == BroadcastMac()
            && ethernet.Source == LocalMac()
            && ethernet.EtherType == EthernetHeader.EtherTypeArp
            && arp.Operation == ArpPacket.OperationRequest
            && arp.SenderMac == LocalMac()
            && arp.SenderAddress == LocalAddress()
            && arp.TargetMac == default
            && arp.TargetAddress == TargetAddress();
    }

    private static MacAddress LocalMac()
    {
        return new MacAddress(0x02, 0x00, 0x00, 0x00, 0x00, 0x10);
    }

    private static MacAddress TargetMac()
    {
        return new MacAddress(0x02, 0x00, 0x00, 0x00, 0x00, 0x20);
    }

    private static MacAddress CachedMac()
    {
        return new MacAddress(0x02, 0x00, 0x00, 0x00, 0x00, 0x30);
    }

    private static MacAddress BroadcastMac()
    {
        return new MacAddress(0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF);
    }

    private static Ipv4Address LocalAddress()
    {
        return new Ipv4Address(192, 0, 2, 10);
    }

    private static Ipv4Address TargetAddress()
    {
        return new Ipv4Address(192, 0, 2, 1);
    }

    private static Ipv4Address CachedAddress()
    {
        return new Ipv4Address(192, 0, 2, 77);
    }
}
