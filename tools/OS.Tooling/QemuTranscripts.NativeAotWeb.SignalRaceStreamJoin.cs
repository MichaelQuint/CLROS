namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Synchronously borrow strict wire/attempt/selected JSON and an
    /// already inspected added-stream inventory. Recheck and copy at most504
    /// streams,4MiB aggregate extents including holes and65536 original ranges;
    /// decode each direction once. Assign every payload guest injectively, require
    /// every successful HTTP host, and reject missing or ambiguous byte matches.
    /// Bound candidate work to64MiB and65536 edges. Complete logical response
    /// outcomes require full original request/response coverage and packet order;
    /// failed/cancelled attempts establish no HTTP completion or full delivery.
    /// Successful writes may have undelivered tails; unknown attempted writes
    /// may explain observed islands only, and unread response suffixes stay legal.
    /// Return immutable copied values or ToolFailure. No port equality or clock
    /// comparison establishes identity. This diagnostic performs no independent
    /// PCAP/TCP, Date/UTC, syscall, owned-file/process, native or guest admission.</summary>

    public static WebSignalRaceStreamJoinInspection InspectNativeAotWebSignalRaceStreamJoinForTests(ReadOnlySpan<byte> wireJson, string attemptsJson, string selectedJson, string endpoint, string expectedHealthBody, ServiceQuotaPacketStreams guest)
    {
        var trace = NativeAotWebPayload.InspectSignalRaceTransport(attemptsJson, selectedJson, wireJson, endpoint, expectedHealthBody);
        var host = InspectNativeAotWebSignalRaceHostHttp(wireJson, attemptsJson, selectedJson, endpoint, expectedHealthBody);
        var copied = CopyWebSignalRacePacketStreams(guest);
        var hosts = host.Prefixes.Connections.Select(PrepareQuotaPairHost).ToArray();
        var guests = copied.Streams.Select(PrepareQuotaPairGuest).ToArray();
        int[] payload = Enumerable.Range(0, guests.Length).Where(i => copied.Streams[i].Requests.KnownBytes + copied.Streams[i].Responses.KnownBytes > 0).ToArray();
        var complete = host.Messages.Where(m => trace.Exchanges[m.ExchangeId - 1].Outcome == "response" && m.Request.State == "complete" && m.Response?.State == "complete").ToArray();
        Dictionary<int, (int Request, int Response)> required = [];
        foreach (var message in complete)
        {
            var prior = required.GetValueOrDefault(message.ConnectionId);
            required[message.ConnectionId] = (Math.Max(prior.Request, message.Request.End), Math.Max(prior.Response, message.Response!.End));
        }

        List<IReadOnlyList<int>> candidates = [];
        int edges = 0;
        long work = 0;
        foreach (int index in payload)
        {
            List<int> compatible = [];
            var packet = guests[index];
            for (int h = 0; h < hosts.Length; h++)
            {
                var bytes = hosts[h];
                var coverage = required.GetValueOrDefault(h + 1);
                if (packet.RequestExtent > bytes.Writes.Length + bytes.Unknown.Length || bytes.Reads.Length > packet.ResponsePrefix.Length || packet.RequestPrefix.Length < coverage.Request || packet.ResponsePrefix.Length < coverage.Response)
                    continue;
                long charge = copied.Streams[index].Requests.KnownBytes + bytes.Reads.Length;
                Need(charge <= 64L * 1024 * 1024 - work, "web_race_stream_join_work_bound");
                work += charge;
                var result = CompareQuotaStreamPair(bytes, packet);
                if (!result.Compatible || !result.HasComparedBytes)
                    continue;
                Need(++edges <= MaximumServiceWireRanges, "web_race_stream_join_graph");
                compatible.Add(h + 1);
            }

            candidates.Add(compatible);
        }

        var assigned = UniqueQuotaFlowAssignment(candidates, hosts.Length, required.Keys.ToArray());
        Dictionary<int, int> byHost = [];
        List<ServiceQuotaFlowBinding> bindings = [];
        for (int i = 0; i < assigned.Count; i++)
        {
            int h = assigned[i], index = payload[i];
            byHost.Add(h, index);
            bindings.Add(new(h, copied.Streams[index].RemotePort, CompareQuotaStreamPair(hosts[h - 1], guests[index])));
        }

        List<ServiceQuotaHttpPacketInterval> intervals = [];
        ServiceQuotaHttpPacketInterval Interval(ServiceQuotaHostHttpMessage message, bool response)
        {
            Need(byHost.TryGetValue(message.ConnectionId, out int index), "web_race_stream_join_required");
            var stream = copied.Streams[index];
            var direction = response ? stream.Responses : stream.Requests;
            var prefix = response ? message.Response! : message.Request;
            int start = prefix.Start, end = prefix.End;
            Need(start >= 0 && end > start && end <= direction.ContiguousBytes, "web_race_stream_join_interval");
            var ranges = direction.FirstRanges.Where(r => r.First.StreamOffset < end && r.First.StreamOffset + r.First.ByteCount > start).Select(r => new ServiceQuotaHttpPacketRange(r.First, Math.Max(start, r.First.StreamOffset), Math.Min(end, r.First.StreamOffset + r.First.ByteCount) - Math.Max(start, r.First.StreamOffset))).ToArray();
            Need(ranges.Sum(r => r.ByteCount) == end - start, "web_race_stream_join_interval");
            var interval = new ServiceQuotaHttpPacketInterval(message.ExchangeId, message.ConnectionId, stream.RemotePort, response, response ? "complete-response" : "complete-request", start, end, Array.AsReadOnly(ranges));
            intervals.Add(interval);
            return interval;
        }

        Dictionary<int, int> priorResponses = [];
        foreach (var message in complete)
        {
            var request = Interval(message, false);
            var response = Interval(message, true);
            Need(request.Ranges.Min(r => r.Original.Record) > priorResponses.GetValueOrDefault(message.ConnectionId) && request.Ranges.Max(r => r.Original.Record) < response.Ranges.Min(r => r.Original.Record), "web_race_stream_join_record_order");
            priorResponses[message.ConnectionId] = response.Ranges.Max(r => r.Original.Record);
        }

        return new(host, copied, Array.AsReadOnly(bindings.ToArray()), Array.AsReadOnly(Enumerable.Range(1, hosts.Length).Where(h => !byHost.ContainsKey(h)).ToArray()), Array.AsReadOnly(Enumerable.Range(0, guests.Length).Where(i => !payload.Contains(i)).Select(i => copied.Streams[i].RemotePort).ToArray()), Array.AsReadOnly(intervals.ToArray()));
    }

    private static ServiceQuotaPacketStreams CopyWebSignalRacePacketStreams(ServiceQuotaPacketStreams source)
    {
        if (source is null)
            throw new ToolFailure("web_race_stream_join_input");
        Need(source.TcpRecords is >= 0 and <= MaximumServiceWireRanges && source.QuietSyns == 0 && source.AggregateExtent is >= 0 and <= QuotaPacketMaximumExtent, "web_race_stream_join_packet_bound");
        var streams = QuotaPairCopy(source.Streams, 504);
        HashSet<uint> ports = [];
        int extent = 0, ranges = 0;
        for (int i = 0; i < streams.Length; i++)
        {
            var stream = streams[i];
            if (stream is null || stream.Requests is null || stream.Responses is null || stream.Requests.FirstRanges is null || stream.Responses.FirstRanges is null)
                throw new ToolFailure("web_race_stream_join_input");
            Need(stream.RemotePort is > 0 and <= 65535 && ports.Add(stream.RemotePort), "web_race_stream_join_ports");
            Need(stream.Requests.Extent >= 0 && stream.Responses.Extent >= 0 && (long)stream.Requests.Extent + stream.Responses.Extent <= QuotaPacketMaximumExtent - extent && stream.Requests.FirstRanges.Count >= 0 && stream.Responses.FirstRanges.Count >= 0 && (long)stream.Requests.FirstRanges.Count + stream.Responses.FirstRanges.Count <= MaximumServiceWireRanges - ranges, "web_race_stream_join_packet_bound");
            extent += stream.Requests.Extent + stream.Responses.Extent;
            ranges += stream.Requests.FirstRanges.Count + stream.Responses.FirstRanges.Count;
            var facts = stream.Facts;
            Need(facts.SynRecord > 0 && facts.ClientFinRecord >= 0 && facts.ServerFinRecord >= 0 && facts.ResetRecord >= 0, "web_race_stream_join_packet_facts");
            ServiceQuotaPacketDirection Copy(ServiceQuotaPacketDirection direction)
            {
                var first = QuotaPairCopy(direction.FirstRanges, MaximumServiceWireRanges);
                for (int j = 0; j < first.Length; j++)
                {
                    var range = first[j];
                    if (range is null)
                        throw new ToolFailure("web_race_stream_join_input");
                    Need(range.First.Record > facts.SynRecord, "web_race_stream_join_packet_facts");
                    first[j] = range with
                    {
                    };
                }

                return direction with
                {
                    FirstRanges = Array.AsReadOnly(first)
                };
            }

            streams[i] = stream with
            {
                Requests = Copy(stream.Requests),
                Responses = Copy(stream.Responses)
            };
        }

        Need(extent == source.AggregateExtent, "web_race_stream_join_packet_bound");
        return source with
        {
            Streams = Array.AsReadOnly(streams)
        };
    }
}
