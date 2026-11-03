using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using OS.Tools.QemuRunner;

namespace OS.Tooling;
public static partial class QemuTranscripts
{
    internal static AuthorityFiles HoldWebEvidence(string root, QemuSmokeOptions options)
    {
        AuthorityFiles files = new();
        try
        {
            var serial = files.Open(root, Path.GetFullPath(options.SerialLog, root), "web serial", "web_evidence_serial", 8 * 1024 * 1024, singleLink: true);
            var summary = files.Open(root, Path.GetFullPath(options.SummaryLog, root), "web summary", "web_evidence_summary", 16 * 1024 * 1024, singleLink: true);
            var packets = files.Open(root, Path.GetFullPath(options.SerialLog + ".pcap", root), "web PCAP", "web_evidence_packets", 4 * 1024 * 1024, singleLink: true);
            AuthorityFiles.RequireDistinct(serial, summary, packets);
            return files;
        }
        catch
        {
            files.Dispose();
            throw;
        }
    }

    private static WebPacketInspection ValidateWebPackets(byte[] package, byte[] raw, byte[] packets, WebParentReport parent, WebReplayReport leaf, int forward, IReadOnlyList<WebUtcObservation> utc, WebControlInspection controls)
    {
        int at = raw.AsSpan().IndexOf("TEST_START m10.004\r\n"u8);
        Need(at >= 0, "web_packet_leaf");
        var framed = NativeAotThreadingOutput.ReadWeb(raw.AsSpan(at));
        string complete = framed.NormalizedTail[..(framed.NormalizedTail.Length - leaf.Observations.TrailingFragment.Length)];
        var configuration = NativeAotWebProgram.DescribeConfiguration(package);
        string[] rows = complete[..^2].Split("\r\n", StringSplitOptions.None);
        Need(leaf.TransportControlsChecked, "web_packet_transport_controls");
        var lifetimes = SocketLifetimes(rows, continuingWeb: true, webConnections: configuration.LogQuota ? 7 : 8, controls: controls.Sockets);
        if (!configuration.LogQuota)
            _ = WebVectorCalls(rows, transportControls: true);
        var report = InspectNativeAotWebPacketsForTests(packets, parent.FrameAnchor, parent.Arp, lifetimes.Select(l => l.RemotePort).ToArray(), forward, configuration.Body, configuration.Json, configuration.LogQuota, utc, controls);
        Need(report.ControlsChecked, "web_packet_controls_required");
        Need(report.Connections == leaf.SocketAccepts && (ulong)report.Received == leaf.Received && (ulong)report.Sent == leaf.Sent, "web_packet_leaf_join");
        Need(report.TimeWaitPorts.All(port => lifetimes.Any(l => l.RemotePort == port && l.Waits > 0)), "web_packet_time_wait_join");
        return report;
    }

    public const int WebUtcToleranceSeconds = 5;
    /// <summary>Inspect bounded original QEMU PCAP bytes against borrowed
    /// anchor/ARP, ordered remote ports and exact selected HTTP expectations.
    /// Checks headers, checksums, sequence/ACK bounds, retransmission equality,
    /// complete request/response bytes and EOF-delimited config completion.
    /// Caller values grant no execution, parent, native or runner authority.
    /// Optional UTC windows additionally check each Date within5 seconds of its
    /// host interval, nonregression and normal-run advance. Omitting them is a
    /// framing-only inspection, as used by historical synthetic host controls.
    /// Optional controls require UTC windows, two further connections and three
    /// health responses, exact raw exchanges, one ordered VLAN and checked
    /// withheld response ACKs. ControlsChecked describes only these wire joins;
    /// live kernel lifetime and scheduler joins remain separate requirements.
    /// ForwardedFrames is ancillary host-writer accounting. Mixed-direction
    /// PCAP records contain no authenticated backend-direction identifier, and
    /// a successful host write alone proves no destination capture/delivery.
    /// No exact forwarded-count versus capture-count join is claimed here.
    /// Malformed, unsupported or incomplete traffic throws; inputs are not kept.</summary>

    public static WebPacketInspection InspectNativeAotWebPacketsForTests(byte[] bytes, uint anchor, QemuArpReceiveReceipt arp, IReadOnlyList<uint> ports, int forward, string body, string configuration, bool logQuota = false, IReadOnlyList<WebUtcObservation>? utcWindows = null, WebControlInspection? controls = null) => ReadWebPackets(bytes, anchor, arp, ports, forward, body, configuration, logQuota, utcWindows, controls);
    private static WebPacketInspection ReadWebPackets(byte[] bytes, uint anchor, QemuArpReceiveReceipt arp, IReadOnlyList<uint> ports, int forward, string body, string configuration, bool logQuota, IReadOnlyList<WebUtcObservation>? utcWindows, WebControlInspection? controls, WebRacePacketPartition? race = null)
    {
        int ordinary = logQuota ? 5 : 6, connections = ordinary + (controls is null ? 0 : 2);
        Need(ports.Count == connections && ports.Distinct().Count() == connections && ports.All(p => p is> 0 and <= 65535) && forward is> 0 and <= 65535 && body.Length > 0 && Encoding.UTF8.GetByteCount(body) <= 1024 && configuration.Length > 0 && Encoding.UTF8.GetByteCount(configuration) <= 4096, "web_wire_expectations");
        Need(controls is null || utcWindows is not null && ports[ordinary] == controls.Sockets.Deadline.SourcePort && ports[ordinary + 1] == controls.Sockets.Before.SourcePort && controls.Sockets.After.SourcePort == controls.Sockets.Before.SourcePort, "web_wire_control_ports");
        _ = QemuEthernetCapture.Inspect(bytes, anchor);
        int offset = 24, prefixStage = 0, prefixEnd = 0, resolutionSent = 0, tcpRecords = 0, record = 0;
        ulong timestamp = 0;
        bool resolved = false;
        List<WebWireFlow> flows = [];
        int vlan = 0;
        byte[] beforeRequest = controls is null ? [] : Convert.FromHexString(controls.Sockets.Before.RequestHex);
        byte[] beforeResponse = controls is null ? [] : Convert.FromHexString(controls.Sockets.Before.ResponseHex);
        while (offset < bytes.Length)
        {
            record++;
            ulong now = (ulong)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset)) * 1_000_000 + BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset + 4));
            Need(now >= timestamp, "web_wire_clock");
            timestamp = now;
            int size = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset + 8)));
            offset += 16;
            var frame = bytes.AsSpan(offset, size);
            offset += size;
            if (prefixStage < 3)
            {
                if (prefixStage == 0)
                {
                    if (frame.Length == 60 && frame.Slice(14, 9).SequenceEqual("CLROS-M07"u8) && OS.Std.Hash.Crc32.Compute(frame) == anchor)
                        prefixStage = 1;
                    continue;
                }

                Need(OS.Std.Hash.Crc32.Compute(frame) == (prefixStage == 1 ? arp.RequestCrc32 : arp.ReceiveCrc32) && frame.Length == (prefixStage == 1 ? 60 : arp.ReceiveBytes), "web_wire_parent_arp");
                prefixStage++;
                prefixEnd = offset;
                continue;
            }

            Need(frame.Length >= 14, "web_wire_ethernet");
            if (BinaryPrimitives.ReadUInt16BigEndian(frame[12..]) == 0x8100)
            {
                Need(controls is not null && ++vlan == 1 && frame.Length == 1518 && frame[..18].SequenceEqual(Convert.FromHexString("020000000001020000000002810000010800")) && frame[18..].IndexOfAnyExcept((byte)0) < 0 && flows.Count == connections, "web_wire_control_vlan");
                var healthy = flows[^1];
                Need(healthy.ClientSeen == beforeRequest.Length && healthy.ServerSeen == beforeResponse.Length && healthy.ResponseExtent == beforeResponse.Length && healthy.Response.AsSpan(0, beforeResponse.Length).SequenceEqual(beforeResponse) && !healthy.ServerFin && !healthy.ClientFin, "web_wire_control_vlan_order");
                race?.OrdinaryRecords.Add(record);
                continue;
            }

            Need(frame.Length <= 1514, "web_wire_frame_extent");
            if (SocketIgnoredRouterAdvertisement(frame))
                continue;
            bool outgoing = MatchNetworkMac(frame[6..], 0x020000000001), incoming = MatchNetworkMac(frame[6..], arp.PeerMac);
            Need(outgoing != incoming && (outgoing ? MatchNetworkMac(frame, arp.PeerMac) || frame[..6].IndexOfAnyExcept((byte)255) < 0 : MatchNetworkMac(frame, 0x020000000001) || frame[..6].IndexOfAnyExcept((byte)255) < 0), "web_wire_mac");
            ushort type = BinaryPrimitives.ReadUInt16BigEndian(frame[12..]);
            if (!resolved || type == 0x0806)
            {
                Need(type == 0x0806, "web_wire_resolution_type");
                CheckSocketArp(frame, outgoing, arp.PeerMac, out ushort operation);
                if (!resolved)
                {
                    if (outgoing)
                        Need(operation == 1 && ++resolutionSent == 1, "web_wire_resolution_request");
                    else if (operation == 2)
                    {
                        Need(resolutionSent == 1, "web_wire_resolution_order");
                        resolved = true;
                    }
                }

                continue;
            }

            if (!ReadSocketTcpFrame(frame, outgoing, arp.PeerMac, out var packet))
                continue;
            uint remote = packet.Remote, sequence = packet.Sequence, ack = packet.Ack;
            byte flags = packet.Flags;
            var payload = packet.Payload;
            if (race is not null && race.Ports.Contains(remote))
            {
                race.Record(record, flows.Count, vlan, flows.Count == connections ? flows[^1] : null, incoming, flags);
                continue;
            }

            tcpRecords++;
            race?.OrdinaryRecords.Add(record);
            var flow = flows.SingleOrDefault(f => f.Port == remote);
            if (incoming && flags == 2)
            {
                Need(payload.Length == 0 && ack == 0, "web_wire_syn");
                if (flow is null)
                {
                    Need(flows.Count < connections && remote == ports[flows.Count], "web_wire_original_tuple");
                    if (flows.Count != 0)
                    {
                        var previous = flows[^1];
                        // The raw reader waits for response EOF before the next
                        // probe. Remaining teardown ACKs may still overlap.
                        Need((controls is not null && flows.Count == ordinary + 1 || previous.ServerFin) && previous.ResponseExtent > 0 && previous.ServerSeen == previous.ResponseExtent && (controls is null || flows.Count != ordinary + 1 || previous.Response.AsSpan(0, previous.ResponseExtent).SequenceEqual(Convert.FromHexString(controls.Sockets.Deadline.ResponseHex))), "web_wire_probe_order");
                    }

                    string path = flows.Count switch
                    {
                        1 => "/config",
                        2 or 3 => "/__clros/logexercise",
                        4 when !logQuota => "/__clros/vectors",
                        _ => "/healthz"
                    };
                    bool post = flows.Count is 2 or 3;
                    byte[] request = Encoding.ASCII.GetBytes($"{(post ? "POST" : "GET")} {path} HTTP/1.0\r\nHost: 127.0.0.1:{forward}\r\nConnection: close\r\nAccept: */*\r\n" + (post ? "Content-Length: 0\r\n" : "") + "\r\n");
                    if (controls is not null && flows.Count == ordinary + 1)
                        request = [..Encoding.ASCII.GetBytes($"GET /healthz HTTP/1.1\r\nHost: 127.0.0.1:{forward}\r\nConnection: keep-alive\r\nAccept: */*\r\n\r\n"), ..request];
                    flow = new(remote, sequence, request, flows.Count == 4 && !logQuota ? WebVectorResponse.Length + 2048 : 8192);
                    flows.Add(flow);
                }
                else
                    Need(unchecked(sequence + 1) == flow.Client && !flow.ClientFin && !flow.ServerFin, "web_wire_syn_retry");
                continue;
            }

            Need(flow is not null, "web_wire_unowned_tuple");
            if (outgoing && flags == 0x12)
            {
                Need(payload.Length == 0 && ack == flow!.Client && (!flow.SynAck || flow.Server == unchecked(sequence + 1)), "web_wire_syn_ack");
                flow!.SynAck = true;
                flow.Server = unchecked(sequence + 1);
                continue;
            }

            Need(flow!.SynAck && flags is 0x10 or 0x18 or 0x11 or 0x19, "web_wire_established");
            uint relative = unchecked(sequence - (outgoing ? flow.Server : flow.Client));
            uint acknowledge = unchecked(ack - (outgoing ? flow.Client : flow.Server));
            Need(acknowledge <= (uint)(outgoing ? flow.ClientSeen : flow.ServerSeen) + (outgoing ? flow.ClientFin ? 1U : 0 : flow.ServerFin ? 1U : 0), "web_wire_acknowledgement");
            if (controls is not null && flow.Port == ports[ordinary])
                Need(!incoming || payload.Length != 0 || flow.ClientSeen == 0, "web_wire_withheld_leak");
            if (controls is not null && flow.Port == ports[^1])
            {
                if (incoming && payload.Length != 0 && (ulong)relative + (uint)payload.Length > (uint)beforeRequest.Length)
                    Need(vlan == 1 && flow.ServerSeen >= beforeResponse.Length && flow.Response.AsSpan(0, beforeResponse.Length).SequenceEqual(beforeResponse), "web_wire_control_after_order");
                if (outgoing && payload.Length != 0 && (ulong)relative + (uint)payload.Length > (uint)beforeResponse.Length)
                    Need(flow.ClientSeen == flow.Request.Length, "web_wire_control_response_order");
            }

            if (incoming)
                flow.ClientReady = true;
            else
                Need(flow.ClientReady, "web_wire_handshake_order");
            if (outgoing && flow.ClientFin && acknowledge == flow.Request.Length + 1)
                flow.ClientFinAcked = true;
            if (incoming && flow.ServerFin && acknowledge == flow.ResponseExtent + 1)
                flow.ServerFinAcked = true;
            byte[] data = outgoing ? flow.Response : flow.Request;
            bool[] seen = outgoing ? flow.ResponseSeen : flow.RequestSeen;
            int contiguous = outgoing ? flow.ServerSeen : flow.ClientSeen;
            bool fin = outgoing ? flow.ServerFin : flow.ClientFin;
            int extent = outgoing ? flow.ResponseExtent : flow.Request.Length;
            Need((payload.Length == 0 ? relative <= (uint)contiguous + (fin ? 1U : 0) : relative < (uint)data.Length && (ulong)relative + (uint)payload.Length <= (uint)data.Length) && (!fin || (ulong)relative + (uint)payload.Length <= (uint)extent + (payload.Length == 0 ? 1U : 0)), "web_wire_sequence");
            for (int i = 0; i < payload.Length; i++)
            {
                int at = (int)relative + i;
                Need(outgoing ? !seen[at] || data[at] == payload[i] : data[at] == payload[i], "web_wire_literal_payload");
                if (outgoing)
                    data[at] = payload[i];
                seen[at] = true;
            }

            if (outgoing && payload.Length != 0)
                flow.ResponseExtent = Math.Max(flow.ResponseExtent, checked((int)relative + payload.Length));
            while (contiguous < seen.Length && seen[contiguous])
                contiguous++;
            if (outgoing)
                flow.ServerSeen = contiguous;
            else
                flow.ClientSeen = contiguous;
            if ((flags & 1) != 0)
            {
                int end = outgoing ? flow.ResponseExtent : flow.Request.Length;
                Need(end > 0 && relative + payload.Length == end && contiguous == end, "web_wire_fin_sequence");
                if (outgoing)
                {
                    if (!flow.ServerFin)
                        flow.ServerFinFirst = !flow.ClientFin;
                    flow.ServerFin = true;
                }
                else
                    flow.ClientFin = true;
            }
        }

        Need(prefixStage == 3 && resolved && resolutionSent == 1 && flows.Count == connections, "web_wire_complete");
        _ = QemuEthernetCapture.InspectArp(bytes.AsSpan(0, prefixEnd), arp);
        List<WebResponseObservation> responses = [];
        for (int i = 0; i < flows.Count; i++)
        {
            var flow = flows[i];
            Need(flow.ClientReady && flow.ClientSeen == flow.Request.Length && flow.ServerSeen == flow.ResponseExtent && flow.ResponseExtent > 0 && (i == 0 || controls is not null && i == ordinary || flow.ServerFin), "web_wire_stream_complete");
            if (controls is not null && i >= ordinary)
            {
                if (i == ordinary)
                {
                    Need(Convert.FromHexString(controls.Sockets.Deadline.RequestHex).AsSpan().SequenceEqual(flow.Request) && Convert.FromHexString(controls.Sockets.Deadline.ResponseHex).AsSpan().SequenceEqual(flow.Response.AsSpan(0, flow.ResponseExtent)), "web_wire_control_bad_join");
                    responses.Add(WebHttpResponse(flow.Response.AsSpan(0, flow.ResponseExtent), "OS_MINIMAL_API_OK", -1, false));
                }
                else
                {
                    Need(vlan == 1 && flow.Request.AsSpan().SequenceEqual([..beforeRequest, ..Convert.FromHexString(controls.Sockets.After.RequestHex)]) && flow.Response.AsSpan(0, flow.ResponseExtent).SequenceEqual([..beforeResponse, ..Convert.FromHexString(controls.Sockets.After.ResponseHex)]), "web_wire_control_healthy_join");
                    responses.Add(WebHttpResponse(beforeResponse, "OS_MINIMAL_API_OK", -1, false, keepAlive: true));
                    responses.Add(WebHttpResponse(flow.Response.AsSpan(beforeResponse.Length, flow.ResponseExtent - beforeResponse.Length), "OS_MINIMAL_API_OK", -1, false));
                    Need(responses[^1].DateUtc > responses[^2].DateUtc, "web_wire_control_date_advance");
                }

                continue;
            }

            responses.Add(WebHttpResponse(flow.Response.AsSpan(0, flow.ResponseExtent), i switch
            {
                1 => configuration,
                2 => logQuota ? "" : "CLROS_LOG_OK workers=8 records=64 writev=32 invalid_errno=9",
                3 => "",
                _ => body
            }, i, logQuota));
        }

        if (utcWindows is not null)
            ValidateWebUtc(responses, utcWindows, requireAdvance: !logQuota);
        if (controls is not null)
        {
            WebSocketControls.Exchange[] exchanges = [controls.Sockets.Deadline, controls.Sockets.Before, controls.Sockets.After];
            for (int i = 0; i < exchanges.Length; i++)
                Need(responses[ordinary + i].DateUtc == exchanges[i].DateUtc && utcWindows![ordinary + i].StartedUtc == exchanges[i].StartedUtc && utcWindows[ordinary + i].CompletedUtc == exchanges[i].CompletedUtc, "web_wire_control_utc_join");
            ValidateWebWithheld(controls, flows[ordinary], arp.PeerMac);
        }

        return new(connections, tcpRecords, flows.Sum(f => f.Request.Length), flows.Sum(f => f.ResponseExtent), flows.Count(f => f.ClientFin), flows.Count(f => f.ServerFin), flows.Count(f => f.ClientFinAcked), flows.Count(f => f.ServerFinAcked), Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(body))), Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(configuration))), Array.AsReadOnly(flows.Where(f => f.ServerFinFirst && f.ClientFinAcked && f.ServerFinAcked).Select(f => f.Port).ToArray()), responses.AsReadOnly(), utcWindows is not null, controls is not null);
    }

    private static void ValidateWebWithheld(WebControlInspection controls, WebWireFlow bad, ulong peer)
    {
        Need(controls.Frames.VlanFrames == 1 && controls.Frames.DroppedFrames.Count is> 0 and <= 128, "web_wire_withheld_extent");
        bool completeAck = false;
        long ordinal = 0;
        foreach (var dropped in controls.Frames.DroppedFrames)
        {
            byte[] frame = Convert.FromHexString(dropped.Hex);
            Need(dropped.Ordinal > ordinal && frame.Length is >= 54 and <= 1514 && MatchNetworkMac(frame.AsSpan(6), peer), "web_wire_withheld_identity");
            ordinal = dropped.Ordinal;
            Need(ReadSocketTcpFrame(frame, false, peer, out var packet) && packet.Remote == bad.Port && packet.Payload.Length == 0 && packet.Flags is 0x10 or 0x11 or 0x14 or 0x04, "web_wire_withheld_tcp");
            uint sequence = unchecked(packet.Sequence - bad.Client), ack = unchecked(packet.Ack - bad.Server);
            Need(sequence >= (uint)bad.Request.Length && sequence <= (uint)bad.Request.Length + 1 && (packet.Flags == 4 && packet.Ack == 0 || ack <= (uint)bad.ResponseExtent + (bad.ServerFin ? 1U : 0)), "web_wire_withheld_sequence");
            completeAck |= (packet.Flags & 0x10) != 0 && ack >= (uint)bad.ResponseExtent;
        }

        Need(completeAck, "web_wire_withheld_response_ack");
    }

    private static void ValidateWebUtc(IReadOnlyList<WebResponseObservation> responses, IReadOnlyList<WebUtcObservation> windows, bool requireAdvance)
    {
        Need(windows.Count == responses.Count && windows.Count > 1, "web_utc_count");
        const long tolerance = WebUtcToleranceSeconds * TimeSpan.TicksPerSecond;
        for (int i = 0; i < windows.Count; i++)
        {
            var window = windows[i];
            long start = window.StartedUtc.UtcTicks, end = window.CompletedUtc.UtcTicks;
            Need(window.StartedUtc.Offset == TimeSpan.Zero && window.CompletedUtc.Offset == TimeSpan.Zero && start >= tolerance && end <= DateTimeOffset.MaxValue.UtcTicks - tolerance && start <= end && (i == 0 || window.StartedUtc >= windows[i - 1].CompletedUtc), "web_utc_window");
            long date = responses[i].DateUtc.UtcTicks;
            Need(date >= start - tolerance && date <= end + tolerance, "web_utc_date");
            Need(i == 0 || responses[i].DateUtc >= responses[i - 1].DateUtc, "web_utc_regression");
        }

        // Normal logging already spans multiple seconds. The quota control may
        // complete within one cached Date second and claims no separate advance.
        Need(!requireAdvance || responses[^1].DateUtc > responses[0].DateUtc, "web_utc_advance");
    }

    private static WebResponseObservation WebHttpResponse(ReadOnlySpan<byte> bytes, string expected, int phase, bool logQuota, bool keepAlive = false)
    {
        int end = bytes.IndexOf("\r\n\r\n"u8);
        Need(end is> 0 and <= 2048, "web_wire_http_header");
        string[] lines = DecodeUtf8(bytes[..end].ToArray()).Split("\r\n", StringSplitOptions.None);
        bool quotaFailure = logQuota && phase == 2, vector = !logQuota && phase == 4;
        Need(lines[0] == (phase == 3 ? "HTTP/1.1 409 Conflict" : quotaFailure ? "HTTP/1.1 500 Internal Server Error" : "HTTP/1.1 200 OK"), "web_wire_http_status");
        Dictionary<string, string> headers = new(StringComparer.OrdinalIgnoreCase);
        foreach (string line in lines.Skip(1))
        {
            int colon = line.IndexOf(": ", StringComparison.Ordinal);
            Need(colon > 0 && !Controls.IsMatch(line) && headers.TryAdd(line[..colon], line[(colon + 2)..]), "web_wire_http_headers");
        }

        bool length = phase != 1, contentType = phase != 3 && !quotaFailure;
        Need(headers.Count == (phase == 1 || !contentType ? 4 : keepAlive ? 4 : 5) && headers.Keys.All(k => k is "Date" or "Server" || !keepAlive && k == "Connection" || contentType && k == "Content-Type" || length && k == "Content-Length") && headers.GetValueOrDefault("Connection") == (keepAlive ? null : "close") && headers.GetValueOrDefault("Server") == "Kestrel" && headers.GetValueOrDefault("Content-Type") == (phase == 1 ? "application/json; charset=utf-8" : vector ? "application/octet-stream" : contentType ? "text/plain" : null), "web_wire_http_headers");
        Need(DateTimeOffset.TryParseExact(headers.GetValueOrDefault("Date"), "r", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date), "web_wire_http_date");
        Need(headers["Date"] == date.ToString("r", CultureInfo.InvariantCulture), "web_wire_http_date");
        byte[] body = Encoding.UTF8.GetBytes(expected);
        Need((!length || headers.GetValueOrDefault("Content-Length") == (vector ? WebVectorResponse.Length : body.Length).ToString(CultureInfo.InvariantCulture)) && (vector ? WebVectorResponse.Matches(bytes[(end + 4)..]) : bytes[(end + 4)..].SequenceEqual(body)), "web_wire_http_body");
        byte[] normalized = bytes.ToArray();
        int dateAt = bytes[..end].IndexOf("\r\nDate: "u8);
        Need(dateAt >= 0 && headers["Date"].Length == 29, "web_wire_http_date");
        "Thu, 01 Jan 1970 00:00:00 GMT"u8.CopyTo(normalized.AsSpan(dateAt + 8, 29));
        return new(date, Convert.ToHexString(SHA256.HashData(bytes)), Convert.ToHexString(SHA256.HashData(normalized)));
    }
}
