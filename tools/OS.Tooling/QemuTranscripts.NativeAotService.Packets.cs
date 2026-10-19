using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Reconstructed ordinary HTTP exchanges and close directions.
    /// This report does not check injected persist/reset controls, guest owner
    /// retirement, native/source identity or process lifetime.</summary>
    public readonly record struct ServiceHttpPacketInspection(int Connections, int TcpRecords, int RequestBytes, int ResponseBytes,
        int ServerActiveCloses, int CompleteCloses, int QuietSyns);

    private sealed class ServiceWireFlow(uint port, uint client)
    {
        internal readonly uint Port = port, Client = unchecked(client + 1);
        internal readonly byte[] Request = new byte[4096], Response = new byte[16384];
        internal readonly bool[] RequestSeen = new bool[4096], ResponseSeen = new bool[16384];
        internal uint Server;
        internal int RequestExtent, ResponseExtent, ClientSeen, ServerSeen;
        internal bool SynAck, ClientReady, ClientFin, ServerFin, ClientFinAcked, ServerFinAcked, ServerFinFirst;
    }

    /// <summary>Inspect bounded original PCAP bytes with borrowed parent ARP,
    /// ordered admitted remote ports and a strictly checked service peer report.
    /// Reconstruct every unique request/response byte, check TCP sequence/ACK,
    /// retransmission equality, HTTP framing/status/entity/Date and complete
    /// server-active close. Unanswered startup SYNs may precede the first
    /// admitted connection during the independently checked ISN quiet period.
    /// No supplied value authenticates execution. Fault-injection controls and
    /// serial/native/runner/source joins are separate requirements.</summary>
    public static ServiceHttpPacketInspection InspectNativeAotServiceHttpPacketsForTests(byte[] bytes, uint anchor,
        QemuArpReceiveReceipt arp, IReadOnlyList<uint> ports, string peerReport)
    {
        var peer = NativeAotServicePeer.InspectOutput(peerReport);
        Need(ports.Count == peer.Exchanges && ports.Distinct().Count() == ports.Count && ports.All(port => port is > 0 and <= 65535),
            "service_wire_expectations");
        using var document = JsonDocument.Parse(peerReport);
        var exchanges = document.RootElement.GetProperty("exchanges").EnumerateArray().ToArray();
        return ServiceHttpPackets(bytes, anchor, arp, ports, exchanges, false);
    }

    private static ServiceHttpPacketInspection ServiceHttpPackets(byte[] bytes, uint anchor, QemuArpReceiveReceipt arp,
        IReadOnlyList<uint> ports, JsonElement[] exchanges, bool concurrent, Action<int, byte[], byte[]>? checkedStream = null)
    {
        _ = QemuEthernetCapture.Inspect(bytes, anchor);
        int offset = 24, stage = 0, prefixEnd = 0, resolution = 0, tcpRecords = 0, quietSyns = 0;
        ulong timestamp = 0; bool resolved = false; List<ServiceWireFlow> flows = [];
        while (offset < bytes.Length)
        {
            ulong now = (ulong)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset)) * 1_000_000 +
                BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset + 4));
            Need(now >= timestamp, "service_wire_clock"); timestamp = now;
            int size = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset + 8))); offset += 16;
            var frame = bytes.AsSpan(offset, size); offset += size;
            if (stage < 3)
            {
                if (stage == 0)
                {
                    if (frame.Length == 60 && frame.Slice(14, 9).SequenceEqual("CLROS-M07"u8) && OS.Std.Hash.Crc32.Compute(frame) == anchor) stage = 1;
                    continue;
                }
                Need(OS.Std.Hash.Crc32.Compute(frame) == (stage == 1 ? arp.RequestCrc32 : arp.ReceiveCrc32) &&
                    frame.Length == (stage == 1 ? 60 : arp.ReceiveBytes), "service_wire_parent_arp");
                stage++; prefixEnd = offset; continue;
            }
            Need(frame.Length is >= 14 and <= 1514, "service_wire_frame_extent");
            if (SocketIgnoredRouterAdvertisement(frame)) continue;
            bool outgoing = MatchNetworkMac(frame[6..], 0x020000000001), incoming = MatchNetworkMac(frame[6..], arp.PeerMac);
            Need(outgoing != incoming && (outgoing ? MatchNetworkMac(frame, arp.PeerMac) || frame[..6].IndexOfAnyExcept((byte)255) < 0 :
                MatchNetworkMac(frame, 0x020000000001) || frame[..6].IndexOfAnyExcept((byte)255) < 0), "service_wire_mac");
            ushort type = BinaryPrimitives.ReadUInt16BigEndian(frame[12..]);
            if (!resolved || type == 0x0806)
            {
                Need(type == 0x0806, "service_wire_resolution_type"); CheckSocketArp(frame, outgoing, arp.PeerMac, out ushort operation);
                if (!resolved)
                {
                    if (outgoing) Need(operation == 1 && ++resolution == 1, "service_wire_resolution_request");
                    else if (operation == 2) { Need(resolution == 1, "service_wire_resolution_order"); resolved = true; }
                }
                continue;
            }
            if (!ReadSocketTcpFrame(frame, outgoing, arp.PeerMac, out var packet)) continue;
            tcpRecords++; uint remote = packet.Remote, sequence = packet.Sequence, ack = packet.Ack; byte flags = packet.Flags;
            var payload = packet.Payload; var flow = flows.SingleOrDefault(value => value.Port == remote);
            if (incoming && flags == 2)
            {
                Need(payload.Length == 0 && ack == 0, "service_wire_syn");
                if (flow is null)
                {
                    if (!ports.Contains(remote))
                    { Need(flows.Count == 0 && ++quietSyns <= 1600, "service_wire_quiet_syn"); continue; }
                    Need(flows.Count < ports.Count && remote == ports[flows.Count] &&
                        (concurrent || flows.Count == 0 || flows[^1].ServerFin && flows[^1].ServerSeen == flows[^1].ResponseExtent), "service_wire_flow_order");
                    flow = new(remote, sequence); flows.Add(flow);
                }
                else Need(unchecked(sequence + 1) == flow.Client && !flow.ClientFin && !flow.ServerFin, "service_wire_syn_retry");
                continue;
            }
            Need(flow is not null, "service_wire_unowned_tuple");
            if (outgoing && flags == 0x12)
            {
                Need(payload.Length == 0 && ack == flow!.Client && (!flow.SynAck || flow.Server == unchecked(sequence + 1)), "service_wire_syn_ack");
                flow!.Server = unchecked(sequence + 1); flow.SynAck = true; continue;
            }
            Need(flow!.SynAck && flags is 0x10 or 0x18 or 0x11 or 0x19, "service_wire_established");
            uint relative = unchecked(sequence - (outgoing ? flow.Server : flow.Client));
            uint acknowledge = unchecked(ack - (outgoing ? flow.Client : flow.Server));
            Need(acknowledge <= (uint)(outgoing ? flow.ClientSeen : flow.ServerSeen) +
                (outgoing ? flow.ClientFin ? 1U : 0 : flow.ServerFin ? 1U : 0), "service_wire_ack");
            if (incoming) flow.ClientReady = true; else Need(flow.ClientReady, "service_wire_handshake_order");
            if (outgoing && flow.ClientFin && acknowledge == flow.RequestExtent + 1) flow.ClientFinAcked = true;
            if (incoming && flow.ServerFin && acknowledge == flow.ResponseExtent + 1) flow.ServerFinAcked = true;
            byte[] data = outgoing ? flow.Response : flow.Request; bool[] seen = outgoing ? flow.ResponseSeen : flow.RequestSeen;
            int contiguous = outgoing ? flow.ServerSeen : flow.ClientSeen, extent = outgoing ? flow.ResponseExtent : flow.RequestExtent;
            bool fin = outgoing ? flow.ServerFin : flow.ClientFin;
            Need((payload.Length == 0 ? relative <= (uint)contiguous + (fin ? 1U : 0) :
                relative < (uint)data.Length && (ulong)relative + (uint)payload.Length <= (uint)data.Length) &&
                (!fin || (ulong)relative + (uint)payload.Length <= (uint)extent + (payload.Length == 0 ? 1U : 0)), "service_wire_sequence");
            for (int i = 0; i < payload.Length; i++)
            {
                int at = (int)relative + i; Need(!seen[at] || data[at] == payload[i], "service_wire_retransmission");
                data[at] = payload[i]; seen[at] = true;
            }
            if (payload.Length != 0) extent = Math.Max(extent, checked((int)relative + payload.Length));
            while (contiguous < seen.Length && seen[contiguous]) contiguous++;
            if (outgoing) { flow.ResponseExtent = extent; flow.ServerSeen = contiguous; }
            else { flow.RequestExtent = extent; flow.ClientSeen = contiguous; }
            if ((flags & 1) != 0)
            {
                Need(extent > 0 && relative + payload.Length == extent && contiguous == extent, "service_wire_fin_sequence");
                if (outgoing) { if (!flow.ServerFin) flow.ServerFinFirst = !flow.ClientFin; flow.ServerFin = true; }
                else flow.ClientFin = true;
            }
        }
        Need(stage == 3 && resolved && resolution == 1 && flows.Count == ports.Count, "service_wire_complete");
        _ = QemuEthernetCapture.InspectArp(bytes.AsSpan(0, prefixEnd), arp);
        for (int i = 0; i < flows.Count; i++)
        {
            var flow = flows[i];
            Need(flow.ClientReady && flow.ClientSeen == flow.RequestExtent && flow.ServerSeen == flow.ResponseExtent &&
                flow.ClientFin && flow.ServerFin && flow.ClientFinAcked && flow.ServerFinAcked && flow.ServerFinFirst, "service_wire_close");
            ServiceHttpExchange(flow.Request.AsSpan(0, flow.RequestExtent), flow.Response.AsSpan(0, flow.ResponseExtent), exchanges[i]);
            checkedStream?.Invoke(i, flow.Request.AsSpan(0, flow.RequestExtent).ToArray(), flow.Response.AsSpan(0, flow.ResponseExtent).ToArray());
        }
        return new(flows.Count, tcpRecords, flows.Sum(flow => flow.RequestExtent), flows.Sum(flow => flow.ResponseExtent),
            flows.Count(flow => flow.ServerFinFirst), flows.Count(flow => flow.ClientFinAcked && flow.ServerFinAcked), quietSyns);
    }

    private static void ServiceHttpExchange(ReadOnlySpan<byte> request, ReadOnlySpan<byte> response, JsonElement exchange)
    {
        static (string First, Dictionary<string, string> Headers, int End) Head(ReadOnlySpan<byte> data)
        {
            int at = data.IndexOf("\r\n\r\n"u8); Need(at is > 0 and <= 4096, "service_http_header_extent");
            var bytes = data[..at]; foreach (byte value in bytes) Need(value is 10 or 13 or >= 32 and <= 126, "service_http_header_byte");
            string[] lines = Encoding.ASCII.GetString(bytes).Split("\r\n", StringSplitOptions.None);
            Need(lines.Length is >= 2 and <= 32 && lines.All(line => line.Length is > 0 and <= 1024 &&
                line.All(c => c is >= ' ' and <= '~')), "service_http_headers");
            Dictionary<string, string> headers = new(StringComparer.OrdinalIgnoreCase);
            foreach (string line in lines.Skip(1))
            {
                int colon = line.IndexOf(':'); Need(colon > 0 && colon + 2 < line.Length && line[colon + 1] == ' ' &&
                    line[..colon].All(c => char.IsAsciiLetterOrDigit(c) || c == '-') && headers.TryAdd(line[..colon], line[(colon + 2)..]), "service_http_header");
            }
            return (lines[0], headers, at + 4);
        }
        var sent = Head(request); string method = exchange.GetProperty("method").GetString()!, path = exchange.GetProperty("path").GetString()!;
        Need(sent.First == method + " " + path + " HTTP/1.1" && sent.End == request.Length && sent.Headers.GetValueOrDefault("Host") == "clros.service" &&
            sent.Headers.GetValueOrDefault("Connection") == "close" && sent.Headers.Count == (method == "POST" ? 3 : 2) &&
            (method != "POST" || sent.Headers.GetValueOrDefault("Content-Length") == "0"), "service_http_request");
        var received = Head(response); int status = exchange.GetProperty("status").GetInt32();
        Need(received.First == (status == 200 ? "HTTP/1.1 200 OK" : "HTTP/1.1 409 Conflict") &&
            received.Headers.GetValueOrDefault("Connection") == "close" && received.Headers.GetValueOrDefault("Server") == "Kestrel", "service_http_response");
        Need(received.Headers.TryGetValue("Date", out string? date) && DateTimeOffset.TryParseExact(date, "r", CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal, out var parsed) && parsed == DateTimeOffset.ParseExact(exchange.GetProperty("dateUtc").GetString()!, "O", CultureInfo.InvariantCulture), "service_http_date");
        byte[] expected = Convert.FromBase64String(exchange.GetProperty("bodyBase64").GetString()!);
        var entity = response[received.End..];
        if (received.Headers.TryGetValue("Transfer-Encoding", out string? encoding))
        {
            Need(encoding == "chunked" && !received.Headers.ContainsKey("Content-Length"), "service_http_transfer_encoding");
            int consumed = 0, decoded = 0;
            while (true)
            {
                int end = entity[consumed..].IndexOf("\r\n"u8); Need(end is > 0 and <= 8, "service_http_chunk_size");
                string hex = Encoding.ASCII.GetString(entity.Slice(consumed, end));
                Need(hex.All(char.IsAsciiHexDigit) && int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int length) && length >= 0, "service_http_chunk_number");
                int size = int.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture); consumed += end + 2;
                Need(size <= expected.Length - decoded && entity.Length - consumed >= size + 2 && entity.Slice(consumed + size, 2).SequenceEqual("\r\n"u8), "service_http_chunk_extent");
                Need(entity.Slice(consumed, size).SequenceEqual(expected.AsSpan(decoded, size)), "service_http_body");
                decoded += size; consumed += size + 2;
                if (size == 0) { Need(decoded == expected.Length && consumed == entity.Length, "service_http_chunk_end"); break; }
            }
        }
        else
        {
            if (received.Headers.TryGetValue("Content-Length", out string? length))
                Need(length == expected.Length.ToString(CultureInfo.InvariantCulture), "service_http_content_length");
            Need(entity.SequenceEqual(expected), "service_http_body");
        }
    }
}
