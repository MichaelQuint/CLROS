namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Synchronously borrow a strict race stream join and added socket
    /// CALL projections from complete web replay. Recheck copied shapes, original
    /// indices, transport ownership, replay description IDs and continuous byte
    /// fronts. Bound calls to100000, intervals16384, packet ranges65536, copied
    /// range references and syscall slices2000000 each. Reuse packet bounds504
    /// streams/4MiB aggregate extents. Every complete request/response interval
    /// must retain exact original packet ranges and consuming receive/accepted
    /// send coverage, with request-before-response and persistent FIFO order.
    /// Peeks consume nothing; unconsumed request and accepted-unsent response
    /// suffixes remain legal. No quota post-CALL row, pointer data, cross-clock,
    /// independent source/file/process/native or guest authority is invented.
    /// Returns immutable copied values or ToolFailure; inputs are not retained.</summary>

    public static WebSignalRaceTemporalInspection InspectNativeAotWebSignalRaceTemporalForTests(WebSignalRaceStreamJoinInspection joined, IReadOnlyList<WebSignalRaceSocketCall> socketCalls)
    {
        var (guest, hosts, indexes) = CopyWebRaceBoundPackets(joined);
        var packets = guest.Streams.ToDictionary(s => s.RemotePort);
        var ports = hosts.ToDictionary(p => p.Value, p => p.Key);
        var calls = QuotaPairCopy(socketCalls, 100000);
        var intervals = QuotaPairCopy(joined.Intervals, 16384);
        Dictionary<uint, WebRaceTemporalConnection> owners = [];
        Dictionary<ulong, ulong> ordinals = [];
        int previous = -1;
        ulong consumed = 0, accepted = 0;
        foreach (var call in calls)
        {
            var identity = call.Connection;
            Need(call.CallRecordIndex > previous && call.CallRecordIndex < 100000 && call.Identity > 0 && call.Ordinal > 0 && call.Ordinal > ordinals.GetValueOrDefault(call.Identity) && call.Descriptor is >= 3 and < 16 && call.DescriptorGeneration > 0 && call.OpenDescription is >= 0 and < 100000 && identity.Owner > 0 && identity.Epoch > 0 && identity.Generation > 0 && identity.Index < 16 && identity.Slot < 16 && identity.Local == 0x0a00020f && identity.LocalPort == 8080 && identity.Remote == 0x0a000202 && packets.ContainsKey(identity.RemotePort) && call.Bytes <= 8192 && call.Offset <= ulong.MaxValue - call.Bytes, "web_race_temporal_call_shape");
            previous = call.CallRecordIndex;
            ordinals[call.Identity] = call.Ordinal;
            if (call.Accept)
            {
                Need(call.Bytes == 0 && call.Offset == 0 && !call.Peek && call.Sent == 0 && call.Received == 0 && owners.TryAdd(identity.RemotePort, new(identity, call.OpenDescription)), "web_race_temporal_accept");
                continue;
            }

            bool known = owners.TryGetValue(identity.RemotePort, out var owner);
            if (!known || owner is null)
                throw new ToolFailure("web_race_temporal_owner");
            Need(call.Number is 44 or 45 or 46 or 47 && call.Bytes > 0 && owner.Identity == identity && owner.Description == call.OpenDescription && (!call.Response || !call.Peek), "web_race_temporal_owner");
            Need(call.Offset == (call.Response ? owner.Sent : owner.Received), "web_race_temporal_offset");
            if (call.Response)
            {
                owner.Sent += call.Bytes;
                owner.Writes.Add(call);
                accepted += call.Bytes;
            }
            else
            {
                Need(call.Offset + call.Bytes <= (ulong)packets[identity.RemotePort].Requests.ContiguousBytes, "web_race_temporal_request_front");
                if (!call.Peek)
                {
                    owner.Received += call.Bytes;
                    owner.Reads.Add(call);
                    consumed += call.Bytes;
                }
            }

            Need(call.Sent == owner.Sent && call.Received == owner.Received, "web_race_temporal_counters");
        }

        foreach (var stream in guest.Streams)
            Need((ulong)stream.Responses.Extent <= (owners.TryGetValue(stream.RemotePort, out var owner) ? owner.Sent : 0), "web_race_temporal_response_front");
        List<WebSignalRaceHttpSyscallInterval> result = [];
        int slices = 0, rangeReferences = 0;
        foreach (var interval in intervals)
        {
            if (interval is null)
                throw new ToolFailure("web_race_temporal_interval");
            Need(ports.TryGetValue(interval.ConnectionId, out uint port) && port == interval.RemotePort, "web_race_temporal_interval");
            bool known = owners.TryGetValue(port, out var owner);
            if (!known || owner is null)
                throw new ToolFailure("web_race_temporal_interval");
            Need(interval.ExchangeId is > 0 and <= 8192 && interval.Kind is "complete-request" or "complete-response" && interval.Response == (interval.Kind == "complete-response"), "web_race_temporal_interval");
            var ranges = QuotaPairCopy(interval.Ranges, MaximumServiceWireRanges);
            Need(ranges.Length <= 2000000 - rangeReferences, "web_race_temporal_interval_bound");
            rangeReferences += ranges.Length;
            var expected = WebRaceHttpRanges(packets[port], interval.Response, interval.Start, interval.End, indexes[(port, interval.Response)]);
            Need(ranges.SequenceEqual(expected), "web_race_temporal_packet_ranges");
            var source = interval.Response ? owner.Writes : owner.Reads;
            int first = 0, last = source.Count;
            while (first < last)
            {
                int mid = first + (last - first) / 2;
                if (source[mid].Offset + source[mid].Bytes <= (ulong)interval.Start)
                    first = mid + 1;
                else
                    last = mid;
            }

            ulong cursor = (ulong)interval.Start;
            List<WebSignalRaceHttpSyscallSlice> clipped = [];
            for (int i = first; i < source.Count && source[i].Offset < (ulong)interval.End; i++)
            {
                var call = source[i];
                ulong start = Math.Max((ulong)interval.Start, call.Offset), end = Math.Min((ulong)interval.End, call.Offset + call.Bytes);
                Need(start == cursor && end > start && ++slices <= 2000000, "web_race_temporal_coverage");
                clipped.Add(new(call, start, (uint)(end - start)));
                cursor = end;
            }

            Need(cursor == (ulong)interval.End, "web_race_temporal_coverage");
            result.Add(new(interval with
            {
                Ranges = Array.AsReadOnly(ranges)
            }, owner.Identity, Array.AsReadOnly(clipped.ToArray())));
        }

        foreach (var connection in result.GroupBy(i => i.Packet.ConnectionId))
        {
            var requests = connection.Where(i => !i.Packet.Response).OrderBy(i => i.Packet.Start).ToArray();
            var responses = connection.Where(i => i.Packet.Response).OrderBy(i => i.Packet.Start).ToArray();
            Need(requests.Select(i => i.Packet.ExchangeId).Distinct().Count() == requests.Length && responses.Select(i => i.Packet.ExchangeId).Distinct().Count() == responses.Length, "web_race_temporal_message");
            var byExchange = responses.ToDictionary(i => i.Packet.ExchangeId);
            int requestEnd = 0, responseEnd = 0, priorSend = -1;
            foreach (var request in requests)
            {
                var packet = request.Packet;
                Need(packet.Start >= requestEnd && request.Slices[0].Original.CallRecordIndex > priorSend, "web_race_temporal_fifo");
                requestEnd = packet.End;
                Need(byExchange.TryGetValue(packet.ExchangeId, out _), "web_race_temporal_message");
                var response = byExchange[packet.ExchangeId];
                Need(response.Packet.Start >= responseEnd && request.Slices[^1].Original.CallRecordIndex < response.Slices[0].Original.CallRecordIndex, "web_race_temporal_request_before_response");
                responseEnd = response.Packet.End;
                priorSend = response.Slices[^1].Original.CallRecordIndex;
            }

            Need(responses.Length == requests.Length, "web_race_temporal_message");
        }

        return new(calls.Length, consumed, accepted, Array.AsReadOnly(result.ToArray()));
    }
}
