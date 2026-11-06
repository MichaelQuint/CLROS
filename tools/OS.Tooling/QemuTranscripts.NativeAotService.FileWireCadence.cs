using System.Globalization;
using System.Security.Cryptography;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Synchronously borrow classic-PCAP bytes, their parent ARP and
    /// all admitted ports in original SYN order. Return copied cadence values
    /// for one bodyless literal GET/fixed identity Content-Length response per
    /// target port. Reuse the strict service wire parser with its explicit full
    /// disk 64MiB capture/24MiB aggregate stream policy; ordinary defaults stay
    /// unchanged. At most32 ports/targets and262144 retained established packets
    /// and unique ranges are allowed, checked before list growth. Reject invalid
    /// wire bytes, an incomplete request/header, unsupported HTTP framing/coding,
    /// extra exchange bytes, a mismatched length, or a complete wrong body hash.
    /// A shorter/holey body returns IncompleteNoFullBodyHash and no full span.
    /// No close, replay, injection ledger or complete Full peer is required.
    /// No input is modified/retained; callers must not mutate inputs concurrently.
    /// No result is published if any strict parse or selected target fails.</summary>
    public static IReadOnlyList<FileWireCadence> InspectNativeAotFileWireCadenceValues(
        byte[] pcap, uint anchor, QemuArpReceiveReceipt arp,
        IReadOnlyList<uint> orderedGuestPorts, IReadOnlyList<FileWireTarget> targets)
    {
        const int maximumRows = 262_144;
        ArgumentNullException.ThrowIfNull(pcap);
        Need(orderedGuestPorts is { Count: > 0 and <= 32 } &&
            targets is { Count: > 0 and <= 32 }, "file_wire_input");
        uint[] ports = orderedGuestPorts.ToArray();
        FileWireTarget[] expected = targets.ToArray();
        Need(ports.Distinct().Count() == ports.Length &&
            ports.All(port => port is > 0 and <= 65535) &&
            expected.Select(target => target.RemotePort).Distinct().Count() == expected.Length,
            "file_wire_ports");
        foreach (var target in expected)
            Need(ports.Contains(target.RemotePort) && target.Path is { Length: > 0 and <= 1024 } &&
                target.Path[0] == '/' && target.Path.All(c => c is >= '!' and <= '~' && c != '#') &&
                target.BodyLength is >= 0 and <= DiskStaticCapturePolicy.StreamBytes &&
                target.Sha256 is { Length: 64 } && target.Sha256.All(char.IsAsciiHexDigit),
                "file_wire_target");

        List<ServiceWireByteRange> ranges = [];
        // Retain only scalar facts; decoder Raw/Payload arrays do not escape the callback.
        List<(long Record, ulong Time, bool Outgoing, uint Port, uint Sequence,
            uint Ack, byte Flags, int Bytes)> packets = [];
        var capture = ServiceWirePackets(pcap, anchor, arp, ports, concurrent: true,
            lifecycle: true, observe: range =>
            {
                Need(ranges.Count < maximumRows, "file_wire_range_extent");
                ranges.Add(range);
            }, quotaStreams: true, diskStaticFull: true, observeEstablished: packet =>
            {
                Need(packets.Count < maximumRows, "file_wire_packet_extent");
                packets.Add((packet.Ordinal, packet.Microseconds, packet.Outgoing,
                    packet.Port, packet.Sequence, packet.Ack, packet.Flags, packet.Payload.Length));
            });

        string captureSha = Convert.ToHexString(SHA256.HashData(pcap));
        string module = typeof(QemuTranscripts).Assembly.ManifestModule.ModuleVersionId.ToString("D");
        List<FileWireCadence> results = [];
        foreach (var supplied in expected)
        {
            var target = supplied with { Sha256 = supplied.Sha256.ToUpperInvariant() };
            var flow = capture.Flows.Single(value => value.Port == target.RemotePort);
            Need(flow.ClientReady && flow.ClientSeen == flow.RequestExtent,
                "file_wire_request_incomplete");
            var request = ServiceHttpHead(flow.Request.AsSpan(0, flow.RequestExtent));
            Need(request.First == "GET " + target.Path + " HTTP/1.1" &&
                request.End == flow.RequestExtent && request.Headers.ContainsKey("Host") &&
                !request.Headers.ContainsKey("Transfer-Encoding") &&
                !request.Headers.ContainsKey("Content-Encoding") &&
                !request.Headers.ContainsKey("Upgrade") &&
                (!request.Headers.TryGetValue("Content-Length", out string? requestLength) || requestLength == "0"),
                "file_wire_request");
            // Parse only the contiguous prefix; never expose zero-filled header holes.
            var response = ServiceHttpHead(flow.Response.AsSpan(0, flow.ServerSeen));
            Need(response.First == "HTTP/1.1 200 OK" &&
                response.Headers.GetValueOrDefault("Content-Length") == target.BodyLength.ToString(CultureInfo.InvariantCulture) &&
                !response.Headers.ContainsKey("Transfer-Encoding") &&
                !response.Headers.ContainsKey("Upgrade") &&
                !response.Headers.ContainsKey("Trailer") &&
                (!response.Headers.TryGetValue("Content-Encoding", out string? coding) || coding == "identity"),
                "file_wire_response");
            int bodyStart = response.End, bodyEnd = checked(bodyStart + target.BodyLength);
            Need(flow.ResponseExtent <= bodyEnd, "file_wire_extra_response_bytes");
            bool complete = flow.ServerSeen == bodyEnd && flow.ResponseExtent == bodyEnd;
            string? bodySha = complete
                ? Convert.ToHexString(SHA256.HashData(flow.Response.AsSpan(bodyStart, target.BodyLength)))
                : null;
            Need(!complete || bodySha == target.Sha256, "file_wire_body_sha256");

            var flowRanges = ranges.Where(range => range.RemotePort == target.RemotePort).ToArray();
            var requestRanges = flowRanges.Where(range => !range.ServerToClient).ToArray();
            Need(requestRanges.Length > 0 && requestRanges.Sum(range => range.ByteCount) == request.End,
                "file_wire_request_ranges");
            var requestFirst = requestRanges.MinBy(range => range.Record);
            var requestComplete = requestRanges.MaxBy(range => range.Record);
            ServiceWireByteRange? firstResponse = flowRanges.Where(range => range.ServerToClient)
                .Select(range => (ServiceWireByteRange?)range).MinBy(range => range!.Value.Record);
            int EntityBytes(int start, int count) => Math.Max(0,
                Math.Min(checked(start + count), bodyEnd) - Math.Max(start, bodyStart));
            var bodyRanges = flowRanges.Where(range => range.ServerToClient &&
                EntityBytes(range.StreamOffset, range.ByteCount) != 0).Select(range => range with
                {
                    StreamOffset = Math.Max(range.StreamOffset, bodyStart),
                    ByteCount = EntityBytes(range.StreamOffset, range.ByteCount)
                }).ToArray();
            ServiceWireByteRange? firstEntity = bodyRanges.Select(range => (ServiceWireByteRange?)range)
                .MinBy(range => range!.Value.Record);
            ServiceWireByteRange? lastEntity = bodyRanges.Select(range => (ServiceWireByteRange?)range)
                .MaxBy(range => range!.Value.Record);
            int observedEntity = bodyRanges.Sum(range => range.ByteCount);
            Need(!complete || observedEntity == target.BodyLength, "file_wire_body_ranges");
            var novel = flowRanges.Where(range => range.ServerToClient).GroupBy(range => (long)range.Record)
                .ToDictionary(group => group.Key, group => (Tcp: group.Sum(range => range.ByteCount),
                    Entity: group.Sum(range => EntityBytes(range.StreamOffset, range.ByteCount))));
            var selected = packets.Where(packet => packet.Port == target.RemotePort).ToArray();
            var requestLast = selected.Last(packet => !packet.Outgoing && packet.Bytes != 0);
            var outgoing = selected.Where(packet => packet.Outgoing && packet.Bytes != 0).ToArray();
            List<(uint End, long Record, ulong Time)> advancingAcks = [];
            uint watermark = 0;
            int incomingAcks = 0, nonAdvancingAcks = 0;
            foreach (var packet in selected)
            {
                if (packet.Outgoing || (packet.Flags & 0x10) == 0)
                    continue;
                incomingAcks++;
                uint end = unchecked(packet.Ack - flow.Server);
                if (end <= watermark)
                    nonAdvancingAcks++;
                else
                {
                    // selected.Length already obeys maximumRows; no unbounded ledger.
                    advancingAcks.Add((end, packet.Record, packet.Time));
                    watermark = end;
                }
            }

            // The literal next progress send is selected in capture order, before ACK matching.
            var nextProgress = new int[outgoing.Length];
            int next = -1;
            for (int i = outgoing.Length - 1; i >= 0; i--)
            {
                nextProgress[i] = next;
                if (novel.TryGetValue(outgoing[i].Record, out var added) && added.Entity != 0)
                    next = i;
            }
            List<FileWirePacketCadence> rows = [];
            for (int i = 0; i < outgoing.Length; i++)
            {
                var packet = outgoing[i];
                int start = checked((int)unchecked(packet.Sequence - flow.Server));
                uint end = checked((uint)(start + packet.Bytes));
                novel.TryGetValue(packet.Record, out var added);
                int low = 0, high = advancingAcks.Count;
                while (low < high)
                {
                    int middle = low + (high - low) / 2;
                    if (advancingAcks[middle].End < end)
                        low = middle + 1;
                    else
                        high = middle;
                }
                bool found = low != advancingAcks.Count;
                var ack = found ? advancingAcks[low] : default;
                bool precovered = found && ack.Record < packet.Record;
                long? ackRecord = found && !precovered ? ack.Record : null;
                ulong? ackTime = ackRecord is not null ? ack.Time : null;
                int nextAt = nextProgress[i];
                long? nextRecord = nextAt >= 0 ? outgoing[nextAt].Record : null;
                ulong? nextTime = nextAt >= 0 ? outgoing[nextAt].Time : null;
                rows.Add(new(packet.Record, packet.Time, packet.Sequence, start, packet.Bytes,
                    EntityBytes(start, packet.Bytes), added.Tcp, added.Entity, packet.Bytes - added.Tcp,
                    precovered, added.Tcp != packet.Bytes && !precovered, ackRecord, ackTime,
                    ackTime is { } a ? checked((long)a - (long)packet.Time) : null,
                    nextRecord, nextTime,
                    ackTime is { } a2 && nextTime is { } n ? checked((long)n - (long)a2) : null,
                    ackRecord is { } ar && nextRecord is { } nr && nr < ar));
            }

            results.Add(new(target, pcap.Length, captureSha, module, capture.Flows.Count,
                capture.QuietSyns, packets.Count, requestFirst, requestComplete, firstResponse,
                firstResponse is { } first ? checked((long)first.TimestampMicroseconds -
                    (long)requestComplete.TimestampMicroseconds) : null,
                requestLast.Record, requestLast.Time,
                firstResponse is { } dataFirst ? checked((long)dataFirst.TimestampMicroseconds -
                    (long)requestLast.Time) : null,
                bodyStart, observedEntity, complete,
                complete ? "CompleteBodyHashVerified" : "IncompleteNoFullBodyHash", bodySha,
                firstEntity, lastEntity, complete && firstEntity is { } begin && lastEntity is { } finish
                    ? checked((long)finish.TimestampMicroseconds - (long)begin.TimestampMicroseconds) : null,
                flow.ClientFinRecord == 0 ? null : flow.ClientFinRecord,
                flow.ServerFinRecord == 0 ? null : flow.ServerFinRecord,
                flow.ResetRecord == 0 ? null : flow.ResetRecord,
                flow.ClientFinAcked, flow.ServerFinAcked, true,
                flow.ResetRecord == 0 && !(flow.ClientFin && flow.ServerFin && flow.ClientFinAcked && flow.ServerFinAcked),
                incomingAcks, nonAdvancingAcks,
                Array.AsReadOnly(flowRanges), Array.AsReadOnly(rows.ToArray())));
        }

        return Array.AsReadOnly(results.ToArray());
    }
}
