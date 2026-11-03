namespace OS.Tooling;
public static partial class QemuTranscripts
{
    /// <summary>Compare supplied values from already-inspected host/packet
    /// inventories. Copy and validate counts/ranges/canonical data first:4MiB
    /// aggregate host raw bytes,4MiB guest extents including holes,65536 original
    /// operations/ranges and64KiB per host operation. Malformed shapes throw.
    /// Guest request islands may match known writes or bytes of the separately
    /// unknown final write attempt; successful writes need not all be delivered.
    /// All known host reads must match the contiguous guest response prefix;
    /// extra unread response bytes are permitted. Input is borrowed only during
    /// this call; result retains no mutable buffers/lists. This value-only seam
    /// grants no original I/O, identity/unique assignment, port/clock, HTTP,
    /// FIN/RST, process/cleanup, packet ownership or guest acceptance authority.</summary>

    public static ServiceQuotaStreamPairInspection InspectNativeAotServiceQuotaStreamPairForTests(ServiceQuotaHostConnection host, ServiceQuotaPacketStream guest) => ReadServiceQuotaStreamPair(host, guest);
    // Production callers must supply entries from the strict inventories.
    private static ServiceQuotaStreamPairInspection ReadServiceQuotaStreamPair(ServiceQuotaHostConnection host, ServiceQuotaPacketStream guest) => CompareQuotaStreamPair(PrepareQuotaPairHost(host), PrepareQuotaPairGuest(guest));
    // Prepare once per original stream, rather than decoding for every graph edge.
    private static QuotaPairHostBytes PrepareQuotaPairHost(ServiceQuotaHostConnection host)
    {
        if (host is null)
            throw new ToolFailure("quota_pair_null");
        Need(host.ConnectionId is> 0 and <= NativeAotServiceQuotaPeer.WireMaximumConnections, "quota_pair_shape");
        if (host.Reads is null || host.Writes is null || host.Reads.Operations is null || host.Writes.Operations is null)
            throw new ToolFailure("quota_pair_null");
        int attempted = host.Writes.UnknownWrite?.Requested ?? 0;
        Need(host.Reads.KnownBytes is >= 0 and <= QuotaPacketMaximumExtent && host.Writes.KnownBytes is >= 0 and <= QuotaPacketMaximumExtent && attempted is >= 0 and <= NativeAotServiceQuotaPeer.WireMaximumOperationBytes && (long)host.Reads.KnownBytes + host.Writes.KnownBytes + attempted <= QuotaPacketMaximumExtent, "quota_pair_host_bound");
        Need((long)host.Reads.Operations.Count + host.Writes.Operations.Count <= NativeAotServiceQuotaPeer.WireMaximumOperations, "quota_pair_array_bound");
        var ids = new HashSet<int>();
        var reads = QuotaPairHost(host.Reads, false, ids);
        var writes = QuotaPairHost(host.Writes, true, ids);
        Need((long)reads.Known.Length + writes.Known.Length + writes.Unknown.Length <= QuotaPacketMaximumExtent, "quota_pair_host_bound");
        return new(reads.Known, writes.Known, writes.Unknown);
    }

    private static QuotaPairGuestBytes PrepareQuotaPairGuest(ServiceQuotaPacketStream guest)
    {
        if (guest is null)
            throw new ToolFailure("quota_pair_null");
        Need(guest.RemotePort is> 0 and <= 65535, "quota_pair_shape");
        if (guest.Requests is null || guest.Responses is null || guest.Requests.FirstRanges is null || guest.Responses.FirstRanges is null)
            throw new ToolFailure("quota_pair_null");
        Need((long)guest.Requests.FirstRanges.Count + guest.Responses.FirstRanges.Count <= MaximumServiceWireRanges, "quota_pair_array_bound");
        Need(guest.Requests.Extent is >= 0 and <= QuotaPacketMaximumExtent && guest.Responses.Extent is >= 0 and <= QuotaPacketMaximumExtent && (long)guest.Requests.Extent + guest.Responses.Extent <= QuotaPacketMaximumExtent, "quota_pair_packet_bound");
        var requests = QuotaPairPacket(guest.Requests, guest.RemotePort, false);
        var responses = QuotaPairPacket(guest.Responses, guest.RemotePort, true);
        return new(guest.Requests.Extent, requests.Prefix, requests.Ranges, responses.Prefix);
    }

    private static ServiceQuotaStreamPairInspection CompareQuotaStreamPair(QuotaPairHostBytes host, QuotaPairGuestBytes guest)
    {
        bool compatible = true;
        int requestCompared = 0;
        int possible = host.Writes.Length + host.Unknown.Length, minimum = 0;
        foreach (var range in guest.Requests)
        {
            int at = range.Offset, available = Math.Min(range.Bytes.Length, Math.Max(0, possible - at));
            if (available != range.Bytes.Length)
                compatible = false;
            int known = Math.Min(available, Math.Max(0, host.Writes.Length - at));
            if (known > 0 && !range.Bytes.AsSpan(0, known).SequenceEqual(host.Writes.AsSpan(at, known)))
                compatible = false;
            int tail = available - known;
            if (tail > 0 && !range.Bytes.AsSpan(known, tail).SequenceEqual(host.Unknown.AsSpan(at + known - host.Writes.Length, tail)))
                compatible = false;
            requestCompared += available;
            if (tail > 0)
                minimum = Math.Max(minimum, at + available - host.Writes.Length);
        }

        int responseCompared = Math.Min(host.Reads.Length, guest.ResponsePrefix.Length);
        if (host.Reads.Length > guest.ResponsePrefix.Length || !host.Reads.AsSpan(0, responseCompared).SequenceEqual(guest.ResponsePrefix.AsSpan(0, responseCompared)))
            compatible = false;
        return new(compatible, requestCompared, responseCompared, compatible ? minimum : 0);
    }

    private static (byte[] Known, byte[] Unknown) QuotaPairHost(ServiceQuotaHostDirection direction, bool writing, HashSet<int> ids)
    {
        if (direction is null)
            throw new ToolFailure("quota_pair_null");
        Need(direction.KnownBytes is >= 0 and <= QuotaPacketMaximumExtent, "quota_pair_host_bound");
        var operations = QuotaPairCopy(direction.Operations, NativeAotServiceQuotaPeer.WireMaximumOperations);
        byte[] known = QuotaPairData(direction.KnownBase64, direction.KnownBytes);
        int offset = 0, previous = 0;
        bool failed = false;
        ServiceQuotaHostRange? last = null;
        foreach (var op in operations)
        {
            Need(!failed && op.OperationId > previous && op.OperationId <= NativeAotServiceQuotaPeer.WireMaximumOperations && ids.Add(op.OperationId) && ids.Count <= NativeAotServiceQuotaPeer.WireMaximumOperations && op.Offset == offset && op.Requested is >= 0 and <= NativeAotServiceQuotaPeer.WireMaximumOperationBytes, "quota_pair_host_range");
            previous = op.OperationId;
            failed = op.Outcome is "failed" or "canceled";
            Need(failed ? op.Transferred == -1 : op.Outcome == "complete" ? op.Transferred >= (writing ? 0 : 1) && op.Transferred <= op.Requested && (!writing || op.Transferred == op.Requested) : op.Outcome == "zero" ? !writing && op.Requested == 0 && op.Transferred == 0 : op.Outcome == "eof" && !writing && op.Requested > 0 && op.Transferred == 0, "quota_pair_host_result");
            if (failed)
                last = op;
            else
            {
                Need(op.Transferred <= direction.KnownBytes - offset, "quota_pair_host_range");
                offset += op.Transferred;
            }
        }

        Need(offset == direction.KnownBytes, "quota_pair_host_count");
        var attempt = direction.UnknownWrite;
        byte[] unknown = [];
        if (writing && last is { } final)
        {
            if (attempt is null)
                throw new ToolFailure("quota_pair_host_attempt");
            Need(attempt.OperationId == final.OperationId && attempt.Offset == final.Offset && attempt.Requested == final.Requested && attempt.Outcome == final.Outcome, "quota_pair_host_attempt");
            unknown = QuotaPairData(attempt.AttemptedBase64, attempt.Requested);
        }
        else
            Need(attempt is null, "quota_pair_host_attempt");
        return (known, unknown);
    }

    private static (byte[] Prefix, (int Offset, byte[] Bytes)[] Ranges) QuotaPairPacket(ServiceQuotaPacketDirection direction, uint port, bool response)
    {
        if (direction is null)
            throw new ToolFailure("quota_pair_null");
        Need(direction.Extent is >= 0 and <= QuotaPacketMaximumExtent && direction.KnownBytes >= 0 && direction.KnownBytes <= direction.Extent && direction.ContiguousBytes >= 0 && direction.ContiguousBytes <= direction.KnownBytes, "quota_pair_packet_bound");
        var ranges = QuotaPairCopy(direction.FirstRanges, MaximumServiceWireRanges);
        int known = 0, end = 0, previousRecord = 0;
        ulong previousTime = 0;
        foreach (var range in ranges)
        {
            if (range is null)
                throw new ToolFailure("quota_pair_null");
            var first = range.First;
            Need(first.RemotePort == port && first.ServerToClient == response && first.StreamOffset >= 0 && first.ByteCount > 0 && first.ByteCount <= direction.Extent - first.StreamOffset && first.Record >= 1 && first.Record >= previousRecord && first.Microseconds < 1_000_000 && first.TimestampMicroseconds >= previousTime, "quota_pair_packet_range");
            Need(first.ByteCount <= direction.KnownBytes - known, "quota_pair_packet_count");
            known += first.ByteCount;
            end = Math.Max(end, first.StreamOffset + first.ByteCount);
            previousRecord = first.Record;
            previousTime = first.TimestampMicroseconds;
        }

        Need(known == direction.KnownBytes && end == direction.Extent, "quota_pair_packet_count");
        var sorted = ranges.OrderBy(range => range.First.StreamOffset).ToArray();
        int priorEnd = 0, contiguous = 0;
        foreach (var range in sorted)
        {
            var first = range.First;
            Need(first.StreamOffset >= priorEnd, "quota_pair_packet_overlap");
            if (first.StreamOffset == contiguous)
                contiguous += first.ByteCount;
            priorEnd = first.StreamOffset + first.ByteCount;
        }

        Need(contiguous == direction.ContiguousBytes, "quota_pair_packet_prefix");
        byte[] prefix = QuotaPairData(direction.KnownPrefixBase64, contiguous);
        var copied = new (int Offset, byte[] Bytes)[ranges.Length];
        for (int i = 0; i < ranges.Length; i++)
        {
            var range = ranges[i];
            var first = range.First;
            byte[] data = QuotaPairData(range.DataBase64, first.ByteCount);
            if (first.StreamOffset < contiguous)
                Need(data.AsSpan().SequenceEqual(prefix.AsSpan(first.StreamOffset, data.Length)), "quota_pair_packet_prefix");
            copied[i] = (first.StreamOffset, data);
        }

        return (prefix, copied);
    }

    private static T[] QuotaPairCopy<T>(IReadOnlyList<T> values, int maximum)
    {
        if (values is null)
            throw new ToolFailure("quota_pair_null");
        int count = values.Count;
        Need(count >= 0 && count <= maximum, "quota_pair_array_bound");
        var copy = new T[count];
        for (int i = 0; i < count; i++)
            copy[i] = values[i];
        return copy;
    }

    private static byte[] QuotaPairData(string text, int count)
    {
        if (text is null)
            throw new ToolFailure("quota_pair_null");
        Need(count is >= 0 and <= QuotaPacketMaximumExtent && text.Length == ((count + 2) / 3) * 4, "quota_pair_data");
        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(text);
        }
        catch (FormatException)
        {
            throw new ToolFailure("quota_pair_data");
        }

        Need(bytes.Length == count && Convert.ToBase64String(bytes) == text, "quota_pair_data");
        return bytes;
    }
}
