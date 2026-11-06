namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Synchronously borrow an already strict StreamJoinInspection and
    /// ReadServiceQuotaSocketIo result. Recheck bounded shapes/counters and copy
    /// up to1,000,000 observations,16,384 required intervals and2,000,000 slices;
    /// original packet directional/aggregate extents stay<=4MiB/65,536 ranges.
    /// Cover every request/header/partial-echo/pressure interval with consuming
    /// receives and every response/header interval with accepted sends. Require
    /// request consumption before its first response send and each completed
    /// response send before the next request consumption on that connection.
    /// All consumed/viewed request fronts must fit captured contiguous bytes;
    /// captured response extent must fit accepted sends. Unconsumed request
    /// suffixes and accepted unsent response tails remain legal. Inputs grant
    /// no independent framing, flow identity, original files/processes, native
    /// execution, cross-clock, quota-overlap, termination or guest authority.</summary>

    public static ServiceQuotaTemporalInspection InspectNativeAotServiceQuotaTemporalForTests(ServiceQuotaStreamJoinInspection joined, IReadOnlyList<ServiceQuotaSocketIo> socketIo) => ReadServiceQuotaTemporal(joined, socketIo);
    private static ServiceQuotaTemporalInspection ReadServiceQuotaTemporal(ServiceQuotaStreamJoinInspection joined, IReadOnlyList<ServiceQuotaSocketIo> socketIo)
    {
        if (joined is null || joined.Guest is null)
            throw new ToolFailure("quota_temporal_input");
        var streams = QuotaPairCopy(joined.Guest.Streams, 512);
        var bindings = QuotaPairCopy(joined.Bindings, 512);
        var intervals = QuotaPairCopy(joined.Intervals, 16_384);
        var calls = QuotaPairCopy(socketIo, 1_000_000);
        Dictionary<uint, ServiceQuotaPacketStream> packets = [];
        int extent = 0, ranges = 0;
        foreach (var stream in streams)
        {
            if (stream is null || stream.Requests is null || stream.Responses is null)
                throw new ToolFailure("quota_temporal_input");
            Need(packets.TryAdd(stream.RemotePort, stream) && stream.Requests.Extent >= 0 && stream.Responses.Extent >= 0 && (long)stream.Requests.Extent + stream.Responses.Extent <= QuotaPacketMaximumExtent - extent, "quota_temporal_packet_bound");
            extent += stream.Requests.Extent + stream.Responses.Extent;
            if (stream.Requests.FirstRanges is null || stream.Responses.FirstRanges is null)
                throw new ToolFailure("quota_temporal_input");
            Need((long)stream.Requests.FirstRanges.Count + stream.Responses.FirstRanges.Count <= MaximumServiceWireRanges - ranges, "quota_temporal_packet_bound");
            ranges += stream.Requests.FirstRanges.Count + stream.Responses.FirstRanges.Count;
            _ = PrepareQuotaPairGuest(stream); // Existing exact bytes/islands/prefix validation.
        }

        Need(extent == joined.Guest.AggregateExtent, "quota_temporal_packet_bound");
        Dictionary<int, uint> ports = [];
        HashSet<uint> assigned = [];
        foreach (var binding in bindings)
            Need(binding.ConnectionId is > 0 and <= 8192 && packets.ContainsKey(binding.RemotePort) && binding.Compared.Compatible && binding.Compared.HasComparedBytes && ports.TryAdd(binding.ConnectionId, binding.RemotePort) && assigned.Add(binding.RemotePort), "quota_temporal_binding");
        Dictionary<uint, QuotaTemporalConnection> native = [];
        int previous = -1;
        ulong consumed = 0, accepted = 0;
        foreach (var call in calls)
        {
            var identity = call.Connection;
            Need(call.RecordIndex > previous && call.RecordIndex < 1_000_000 && call.CallRecordIndex >= 0 && call.CallRecordIndex == call.RecordIndex - 1 && call.Identity > 0 && call.Ordinal > 0 && call.Descriptor is >= 0 and < 16 && call.DescriptorGeneration > 0 && call.Description < 16 && call.Inode > 0 && identity.Owner > 0 && identity.Epoch > 0 && identity.Generation > 0 && identity.Index < 8 && identity.Slot < 8 && identity.Local == 0x0a00020f && identity.LocalPort == 8080 && identity.Remote == 0x0a000202 && packets.ContainsKey(identity.RemotePort) && call.Bytes <= 8192 && call.Offset <= ulong.MaxValue - call.Bytes, "quota_temporal_socket_shape");
            previous = call.RecordIndex;
            if (call.Accept)
            {
                Need(call.Bytes == 0 && call.Offset == 0 && !call.Peek && call.Sent == 0 && call.Received == 0 && native.TryAdd(identity.RemotePort, new(identity, call.Description, call.Inode)), "quota_temporal_accept");
                continue;
            }

            bool known = native.TryGetValue(identity.RemotePort, out var owner);
            if (!known || owner is null)
                throw new ToolFailure("quota_temporal_socket_owner");
            Need(call.Number is 0 or 1 or 20 or 44 or 45 or 46 or 47 && call.Bytes > 0 && owner.Identity == identity && owner.Description == call.Description && owner.Inode == call.Inode && (!call.Response || !call.Peek), "quota_temporal_socket_owner");
            Need(call.Offset == (call.Response ? owner.Sent : owner.Received), "quota_temporal_socket_offset");
            if (call.Response)
            {
                owner.Sent += call.Bytes;
                owner.Writes.Add(call);
                accepted += call.Bytes;
            }
            else
            {
                Need(call.Offset + call.Bytes <= (ulong)packets[identity.RemotePort].Requests.ContiguousBytes, "quota_temporal_request_front");
                if (!call.Peek)
                {
                    owner.Received += call.Bytes;
                    owner.Reads.Add(call);
                    consumed += call.Bytes;
                }
            }

            Need(call.Sent == owner.Sent && call.Received == owner.Received, "quota_temporal_socket_counters");
        }

        foreach (var stream in streams)
            Need((ulong)stream.Responses.Extent <= (native.TryGetValue(stream.RemotePort, out var owner) ? owner.Sent : 0), "quota_temporal_response_front");
        List<ServiceQuotaHttpSyscallInterval> result = [];
        int slices = 0;
        foreach (var interval in intervals)
        {
            if (interval is null)
                throw new ToolFailure("quota_temporal_input");
            bool bound = ports.TryGetValue(interval.ConnectionId, out uint port);
            bool known = native.TryGetValue(port, out var owner);
            if (!bound || !known || owner is null)
                throw new ToolFailure("quota_temporal_interval");
            Need(interval.ExchangeId is > 0 and <= 8192 && bound && port == interval.RemotePort && interval.Kind is "complete-request" or "complete-response" or "cancel-header" or "echo-prefix" or "pressure-request" && interval.Response == (interval.Kind is "complete-response" or "cancel-header") && interval.Start >= 0 && interval.End > interval.Start && interval.End <= (interval.Response ? packets[port].Responses.ContiguousBytes : packets[port].Requests.ContiguousBytes), "quota_temporal_interval");
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
            List<ServiceQuotaHttpSyscallSlice> clipped = [];
            for (int i = first; i < source.Count && source[i].Offset < (ulong)interval.End; i++)
            {
                var call = source[i];
                ulong start = Math.Max((ulong)interval.Start, call.Offset), end = Math.Min((ulong)interval.End, call.Offset + call.Bytes);
                Need(start == cursor && end > start && ++slices <= 2_000_000, "quota_temporal_coverage");
                clipped.Add(new(call, start, (uint)(end - start)));
                cursor = end;
            }

            Need(cursor == (ulong)interval.End, "quota_temporal_coverage");
            result.Add(new(interval.ExchangeId, interval.ConnectionId, owner.Identity, interval.Response, interval.Kind, interval.Start, interval.End, Array.AsReadOnly(clipped.ToArray())));
        }

        foreach (var connection in result.GroupBy(i => i.ConnectionId))
        {
            var requests = connection.Where(i => !i.Response).OrderBy(i => i.Start).ToArray();
            var responses = connection.Where(i => i.Response).OrderBy(i => i.Start).ToArray();
            Need(requests.Select(i => i.ExchangeId).Distinct().Count() == requests.Length && responses.Select(i => i.ExchangeId).Distinct().Count() == responses.Length, "quota_temporal_message");
            int priorRequestEnd = 0, priorResponseEnd = 0, priorCompletedSend = -1;
            foreach (var request in requests)
            {
                Need(request.Start >= priorRequestEnd && request.Slices[0].Original.CallRecordIndex > priorCompletedSend, "quota_temporal_fifo");
                priorRequestEnd = request.End;
                var response = responses.SingleOrDefault(i => i.ExchangeId == request.ExchangeId);
                if (response is null)
                {
                    Need(request.Kind != "complete-request", "quota_temporal_message");
                    continue;
                }

                Need(request.Kind == "complete-request" && response.Start >= priorResponseEnd && request.Slices[^1].Original.CallRecordIndex < response.Slices[0].Original.CallRecordIndex, "quota_temporal_request_before_response");
                priorResponseEnd = response.End;
                if (response.Kind == "complete-response")
                    priorCompletedSend = response.Slices[^1].Original.CallRecordIndex;
            }

            Need(responses.All(response => requests.Any(request => request.ExchangeId == response.ExchangeId)), "quota_temporal_message");
        }

        return new(calls.Length, consumed, accepted, Array.AsReadOnly(result.ToArray()));
    }
}
