using System.Globalization;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Inspect strict wire JSON once and original bounded PCAP streams;
    /// ports must retain the validated fresh guest census order without deduping
    /// generations. Assign every payload guest injectively and all required host
    /// controls/held roles; reject no solution or multiple global solutions.
    /// Empty/nonidentifying pairs cannot choose an identity. Bound graph to512
    /// guests/8192 hosts/65536 edges and64MiB compared-byte work; decode each stream
    /// once. Require complete HTTP/header intervals covered by original first
    /// ranges; preserve undelivered writes and unread responses. Full quota values
    /// additionally require the actual single pressure request/no response and
    /// held cancel/echo prefixes. Synchronous borrowed inputs yield copied values
    /// or ToolFailure; no port equality or clock comparison is used. This grants
    /// no held-file/process/source/native/syscall/termination or guest acceptance.</summary>

    public static ServiceQuotaStreamJoinInspection InspectNativeAotServiceQuotaStreamJoinForTests(ReadOnlySpan<byte> wireJson, byte[] pcap, uint anchor, QemuArpReceiveReceipt arp, IReadOnlyList<uint> ports) => ReadServiceQuotaStreamJoin(wireJson, pcap, anchor, arp, ports);
    private static ServiceQuotaStreamJoinInspection ReadServiceQuotaStreamJoin(ReadOnlySpan<byte> wireJson, byte[] pcap, uint anchor, QemuArpReceiveReceipt arp, IReadOnlyList<uint> ports)
    {
        var trace = NativeAotServiceQuotaPeer.InspectWireReport(wireJson);
        var host = ReadServiceQuotaHostHttp(trace);
        var guest = InspectNativeAotServiceQuotaPacketStreamsForTests(pcap, anchor, arp, ports);
        var hosts = host.Prefixes.Connections.Select(PrepareQuotaPairHost).ToArray();
        var guests = guest.Streams.Select(PrepareQuotaPairGuest).ToArray();
        var payload = Enumerable.Range(0, guests.Length).Where(i => guest.Streams[i].Requests.KnownBytes + guest.Streams[i].Responses.KnownBytes > 0).ToArray();
        var required = new Dictionary<int, (int Request, int Response)>();
        void Require(int id, int request, int response)
        {
            var prior = required.GetValueOrDefault(id);
            required[id] = (Math.Max(prior.Request, request), Math.Max(prior.Response, response));
        }

        var complete = host.Messages.Where(m => trace.Exchanges[m.ExchangeId - 1].Outcome == "complete" && m.Request.State == "complete" && m.Response?.State == "complete").ToArray();
        foreach (var message in complete)
            Require(message.ConnectionId, message.Request.End, message.Response!.End);
        foreach (var sample in host.Samples.Where(s => s.HeadersOnly))
        {
            var message = host.Messages[sample.MessageIndex];
            Require(message.ConnectionId, message.Request.End, message.Response!.HeaderEnd);
        }

        if (trace.Purpose == "quota-run")
            foreach (var exchange in trace.Exchanges.Where(e => e.Role != "control"))
            {
                Need(exchange.ConnectionIds.Count == 1, "quota_stream_join_held_retry");
                int id = exchange.ConnectionIds[0];
                Require(id, exchange.Role == "pressure" ? 0 : hosts[id - 1].Writes.Length, exchange.Role == "cancel" ? hosts[id - 1].Reads.Length : 0);
            }

        List<IReadOnlyList<int>> candidates = [];
        int edgeCount = 0;
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
                long charge = guest.Streams[index].Requests.KnownBytes + bytes.Reads.Length;
                Need(charge <= 64L * 1024 * 1024 - work, "quota_stream_join_work_bound");
                work += charge;
                var result = CompareQuotaStreamPair(bytes, packet);
                if (!result.Compatible || !result.HasComparedBytes)
                    continue;
                Need(++edgeCount <= MaximumServiceWireRanges, "quota_stream_join_graph");
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
            bindings.Add(new(h, guest.Streams[index].RemotePort, CompareQuotaStreamPair(hosts[h - 1], guests[index])));
        }

        List<ServiceQuotaHttpPacketInterval> intervals = [];
        ServiceQuotaHttpPacketInterval Interval(int exchange, int connection, bool response, string kind, int start, int end)
        {
            Need(byHost.TryGetValue(connection, out int index), "quota_stream_join_required");
            var stream = guest.Streams[index];
            var direction = response ? stream.Responses : stream.Requests;
            Need(start >= 0 && end > start && end <= direction.ContiguousBytes, "quota_stream_join_interval");
            var ranges = direction.FirstRanges.Where(r => r.First.StreamOffset < end && r.First.StreamOffset + r.First.ByteCount > start).Select(r => new ServiceQuotaHttpPacketRange(r.First, Math.Max(start, r.First.StreamOffset), Math.Min(end, r.First.StreamOffset + r.First.ByteCount) - Math.Max(start, r.First.StreamOffset))).ToArray();
            Need(ranges.Sum(r => r.ByteCount) == end - start, "quota_stream_join_interval");
            var interval = new ServiceQuotaHttpPacketInterval(exchange, connection, stream.RemotePort, response, kind, start, end, Array.AsReadOnly(ranges));
            intervals.Add(interval);
            return interval;
        }

        Dictionary<int, int> priorResponses = [];
        foreach (var message in complete)
        {
            var request = Interval(message.ExchangeId, message.ConnectionId, false, "complete-request", message.Request.Start, message.Request.End);
            var response = Interval(message.ExchangeId, message.ConnectionId, true, "complete-response", message.Response!.Start, message.Response.End);
            Need(request.Ranges.Min(r => r.Original.Record) > priorResponses.GetValueOrDefault(message.ConnectionId) && request.Ranges.Max(r => r.Original.Record) < response.Ranges.Min(r => r.Original.Record), "quota_stream_join_record_order");
            priorResponses[message.ConnectionId] = response.Ranges.Max(r => r.Original.Record);
        }

        foreach (var sample in host.Samples.Where(s => s.HeadersOnly))
        {
            var message = host.Messages[sample.MessageIndex];
            var request = Interval(message.ExchangeId, message.ConnectionId, false, "complete-request", message.Request.Start, message.Request.End);
            var response = Interval(message.ExchangeId, message.ConnectionId, true, "cancel-header", 0, message.Response!.HeaderEnd);
            Need(request.Ranges.Max(r => r.Original.Record) < response.Ranges.Min(r => r.Original.Record), "quota_stream_join_record_order");
        }

        if (trace.Purpose == "quota-run")
            foreach (var exchange in trace.Exchanges.Where(e => e.Role != "control"))
            {
                int h = exchange.ConnectionIds[0];
                var stream = guest.Streams[byHost[h]];
                var bytes = hosts[h - 1];
                if (exchange.Role == "pressure")
                {
                    var requests = ReadServiceQuotaHttpPrefixes(guests[byHost[h]].RequestPrefix, false).Messages;
                    Need(requests.Count == 1 && requests[0].State == "complete" && requests[0].First == "POST /__clros/service/quota HTTP/1.1" && requests[0].Framing == "length" && requests[0].DeclaredLength == 0 && requests[0].Headers.GetValueOrDefault("Host") == "127.0.0.1:" + trace.Port.ToString(CultureInfo.InvariantCulture) && requests[0].Headers.GetValueOrDefault("Connection") is null && requests[0].End == stream.Requests.Extent && stream.Responses.Extent == 0, "quota_stream_join_pressure");
                    Interval(exchange.Id, h, false, "pressure-request", 0, requests[0].End);
                }
                else
                {
                    Need(bytes.Unknown.Length == 0 && stream.Requests.Extent == bytes.Writes.Length && stream.Responses.Extent == (exchange.Role == "cancel" ? bytes.Reads.Length : 0), "quota_stream_join_held_prefix");
                    if (exchange.Role == "echo")
                        Interval(exchange.Id, h, false, "echo-prefix", 0, bytes.Writes.Length);
                }
            }

        return new(host, guest, Array.AsReadOnly(bindings.ToArray()), Array.AsReadOnly(Enumerable.Range(1, hosts.Length).Where(h => !byHost.ContainsKey(h)).ToArray()), Array.AsReadOnly(Enumerable.Range(0, guests.Length).Where(i => !payload.Contains(i)).Select(i => guest.Streams[i].RemotePort).ToArray()), Array.AsReadOnly(intervals.ToArray()));
    }

    /// <summary>Value-only graph control. Return the unique injective host-ID
    /// assignment covering every guest and every required host. Limits are512
    /// guests/8192 hosts/65536edges; malformed, impossible or ambiguous graphs
    /// throw. Copy inputs and return read-only IDs. This seam compares no bytes
    /// and authenticates no network, connection or execution identity.</summary>

    public static IReadOnlyList<int> InspectNativeAotServiceQuotaUniqueFlowAssignmentForTests(IReadOnlyList<IReadOnlyList<int>> candidates, int hostConnections, IReadOnlyList<int> requiredHostIds) => UniqueQuotaFlowAssignment(candidates, hostConnections, requiredHostIds);
    private static IReadOnlyList<int> UniqueQuotaFlowAssignment(IReadOnlyList<IReadOnlyList<int>> candidates, int hosts, IReadOnlyList<int> requiredHostIds)
    {
        Need(candidates is not null && requiredHostIds is not null && hosts is >= 0 and <= 8192 && candidates.Count is >= 0 and <= 512 && requiredHostIds.Count >= 0 && requiredHostIds.Count <= hosts, "quota_stream_join_graph");
        int[][] edges = new int[candidates!.Count][];
        List<int>[] reverse = Enumerable.Range(0, hosts).Select(_ => new List<int>()).ToArray();
        int total = 0;
        for (int g = 0; g < edges.Length; g++)
        {
            var source = candidates[g];
            Need(source is not null && source.Count >= 0 && source.Count <= hosts, "quota_stream_join_graph");
            Need(source!.Count <= MaximumServiceWireRanges - total, "quota_stream_join_graph");
            total += source.Count;
            int[] copy = new int[source.Count];
            for (int i = 0; i < copy.Length; i++)
                copy[i] = source[i];
            Need(copy.Distinct().Count() == copy.Length && copy.All(h => h > 0 && h <= hosts), "quota_stream_join_graph");
            edges[g] = copy.Select(h => h - 1).ToArray();
            foreach (int h in edges[g])
                reverse[h].Add(g);
        }

        int[] required = new int[requiredHostIds!.Count];
        for (int i = 0; i < required.Length; i++)
            required[i] = requiredHostIds[i];
        Need(required.Distinct().Count() == required.Length && required.All(h => h > 0 && h <= hosts), "quota_stream_join_graph");
        var mandatory = required.Select(h => h - 1).ToHashSet();
        int[] guestHost = Enumerable.Repeat(-1, edges.Length).ToArray(), hostGuest = Enumerable.Repeat(-1, hosts).ToArray();
        bool[] seenHosts = new bool[hosts], seenGuests = new bool[edges.Length];
        bool PlaceHost(int h)
        {
            if (seenHosts[h])
                return false;
            seenHosts[h] = true;
            foreach (int g in reverse[h])
            {
                if (seenGuests[g])
                    continue;
                seenGuests[g] = true;
                if (guestHost[g] >= 0 && !PlaceHost(guestHost[g]))
                    continue;
                guestHost[g] = h;
                hostGuest[h] = g;
                return true;
            }

            return false;
        }

        bool PlaceGuest(int g)
        {
            if (seenGuests[g])
                return false;
            seenGuests[g] = true;
            foreach (int h in edges[g])
            {
                if (seenHosts[h])
                    continue;
                seenHosts[h] = true;
                if (hostGuest[h] >= 0 && !PlaceGuest(hostGuest[h]))
                    continue;
                hostGuest[h] = g;
                guestHost[g] = h;
                return true;
            }

            return false;
        }

        foreach (int h in mandatory)
        {
            Array.Clear(seenHosts);
            Array.Clear(seenGuests);
            Need(PlaceHost(h), "quota_stream_join_unmatched");
        }

        for (int g = 0; g < edges.Length; g++)
            if (guestHost[g] < 0)
            {
                Array.Clear(seenHosts);
                Array.Clear(seenGuests);
                Need(PlaceGuest(g), "quota_stream_join_unmatched");
            }

        // Alternative full assignments differ by an alternating cycle, or a
        // path from an optional matched host to an unmatched host. Required
        // hosts cannot be the freed start of such a path.
        byte[] color = new byte[hosts];
        bool[] reachesUnmatched = new bool[hosts];
        bool Visit(int h)
        {
            Need(color[h] != 1, "quota_stream_join_ambiguous");
            if (color[h] == 2)
                return reachesUnmatched[h];
            color[h] = 1;
            bool reaches = hostGuest[h] < 0;
            if (!reaches)
                foreach (int next in edges[hostGuest[h]])
                    if (next != h)
                    {
                        bool child = Visit(next);
                        reaches |= child;
                    }

            color[h] = 2;
            reachesUnmatched[h] = reaches;
            return reaches;
        }

        for (int h = 0; h < hosts; h++)
            if (hostGuest[h] >= 0)
                _ = Visit(h);
        for (int h = 0; h < hosts; h++)
            Need(hostGuest[h] < 0 || mandatory.Contains(h) || !reachesUnmatched[h], "quota_stream_join_ambiguous");
        return Array.AsReadOnly(guestHost.Select(h => h + 1).ToArray());
    }
}
