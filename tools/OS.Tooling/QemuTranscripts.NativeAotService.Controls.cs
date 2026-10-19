using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using OS.Tools.QemuRunner;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Independent complete-capture controls and ordinary packet view.
    /// The view is an internal partition of borrowed bytes, never a replacement
    /// artifact. Process, source, serial and native ownership remain separate.</summary>
    public sealed record ServiceControlInspection(byte[] OrdinaryCapture, IReadOnlyList<int> Ports,
        ServiceHttpPacketInspection Http, int ControlledRecords, int PendingResets);

    private sealed record ServiceControlPacket(long Ordinal, ulong Microseconds, byte[] Raw, bool Outgoing,
        uint Port, uint Sequence, uint Ack, byte Flags, ushort Window, byte[] Payload);

    private static T ServiceControlJson<T>(string text) where T : class
    {
        Need(text is not null && text.Length is > 1 and <= 2 * 1024 * 1024, "service_control_json_extent");
        try
        {
            using var parsed = JsonDocument.Parse(text!, new() { MaxDepth = 12 });
            void Unique(JsonElement value)
            {
                if (value.ValueKind == JsonValueKind.Object)
                {
                    HashSet<string> names = new(StringComparer.Ordinal);
                    foreach (var property in value.EnumerateObject())
                    { Need(names.Add(property.Name), "service_control_json_duplicate"); Unique(property.Value); }
                }
                else if (value.ValueKind == JsonValueKind.Array) foreach (var item in value.EnumerateArray()) Unique(item);
            }
            Unique(parsed.RootElement);
            return JsonSerializer.Deserialize<T>(text!, new JsonSerializerOptions
            { MaxDepth = 12, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, RespectRequiredConstructorParameters = true })
                ?? throw new ToolFailure("service_control_json_null");
        }
        catch (JsonException) { throw new ToolFailure("service_control_json"); }
    }

    /// <summary>Synchronously check full original PCAP against literal control
    /// and frame-writer JSON. Check every controlled packet including any after
    /// the driver completed, two pending resets, exactly one lost reopen,
    /// independent healthy progress and byte-exact resumed HTTP/active closes.
    /// UTC windows must lie within the supplied process window. These borrowed
    /// values authenticate no process or guest; production supplies owned ones.
    /// Invalid framing, ledgers, packet checksums or substitutions throw.</summary>
    public static ServiceControlInspection InspectNativeAotServiceControlsForTests(byte[] bytes, uint anchor,
        QemuArpReceiveReceipt arp, string controlsJson, string framesJson, DateTimeOffset started, DateTimeOffset ended)
    {
        var control = ServiceControlJson<ServiceSocketControls.Report>(controlsJson);
        var frames = ServiceControlJson<WebFrameControl.ServiceReport>(framesJson);
        Need(control.Schema == "clros.nativeaot.service.controls.v1" && control.Persist is not null && control.Healthy is not null &&
            control.Observations is not null && control.Observations.Count is > 20 and <= 512 && frames.Session is not null &&
            frames.ControlledPorts is not null && frames.Injections is not null, "service_control_inputs");
        int[] ports = [control.PendingPort, control.Persist!.Port, control.Healthy!.Port];
        Need(ports.All(port => port is >= 1024 and <= 65535) && ports.Distinct().Count() == 3 && frames.ControlledPorts!.SequenceEqual(ports) &&
            started < ended && control.FirstClientSequence == 0xfffffff0 && control.SecondClientSequence == 0xfffff800, "service_control_selection");
        var session = frames.Session!;
        Need(session.Stopped && session.Healthy && session.GuestExitExpected && session.VlanFrames == 0 && session.ForwardedFrames >= 0 &&
            session.QemuOutPort is >= 1024 and <= 65535 && session.QemuInPort is >= 1024 and <= 65535 && session.QemuOutPort != session.QemuInPort &&
            session.DroppedFrames is not null && session.DroppedFrames.Count <= 128, "service_control_writer");
        string[] arguments = ["-chardev", "socket,id=clroswebout,host=127.0.0.1,port=" + session.QemuOutPort.ToString(CultureInfo.InvariantCulture),
            "-chardev", "socket,id=clroswebin,host=127.0.0.1,port=" + session.QemuInPort.ToString(CultureInfo.InvariantCulture),
            "-object", "filter-redirector,id=clroswebfilter,netdev=n0,queue=tx,indev=clroswebin,outdev=clroswebout"];
        Need(session.Arguments is not null && session.Arguments.SequenceEqual(arguments), "service_control_arguments");
        _ = QemuEthernetCapture.Inspect(bytes, anchor);
        using MemoryStream ordinary = new(), http = new(); ordinary.Write(bytes.AsSpan(0, 24)); http.Write(bytes.AsSpan(0, 24));
        List<ServiceControlPacket> packets = []; long ordinal = 0; ulong clock = 0;
        for (int offset = 24; offset < bytes.Length;)
        {
            int record = offset, length = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset + 8)));
            ulong now = (ulong)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset)) * 1_000_000 + BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset + 4));
            Need(now >= clock, "service_control_capture_clock"); clock = now; ordinal++; offset += 16;
            byte[] raw = bytes.AsSpan(offset, length).ToArray(); offset += length;
            var packet = ServiceSelectedPacket(raw, arp.PeerMac, ports, ordinal, now);
            if (packet is null) { ordinary.Write(bytes.AsSpan(record, offset - record)); http.Write(bytes.AsSpan(record, offset - record)); }
            else
            {
                Need(packets.Count < 512, "service_control_packet_bound"); packets.Add(packet);
                if (packet.Port != control.PendingPort) http.Write(bytes.AsSpan(record, offset - record));
            }
        }
        Need(packets.Count == control.Observations!.Count && packets.Zip(control.Observations).All(pair =>
            pair.First.Ordinal == pair.Second.Ordinal && pair.First.Microseconds == pair.Second.Microseconds &&
            Convert.ToHexString(pair.First.Raw) == pair.Second.Hex), "service_control_full_observations");
        long backend = checked(session.ForwardedFrames + session.DroppedFrames!.Count), previous = 0;
        foreach (var dropped in session.DroppedFrames)
        {
            Need(dropped.Ordinal > previous && dropped.Ordinal <= backend, "service_control_drop_order"); previous = dropped.Ordinal;
            var packet = ServiceSelectedPacket(ServiceHex(dropped.Hex), arp.PeerMac, ports, 0, 0);
            Need(packet is not null && !packet.Outgoing && packet.Flags is 4 or 0x14 && packet.Payload.Length == 0, "service_control_drop_frame");
        }
        var injections = frames.Injections!;
        Need(injections.Count is > 10 and <= 64 && injections.Select(row => row.Tag).Distinct(StringComparer.Ordinal).Count() == injections.Count &&
            injections.Count(row => !row.Forwarded) == 1 && injections.Single(row => !row.Forwarded).Tag == "persist.reopen.lost", "service_control_injection_set");
        previous = 0; List<ServiceControlPacket> sent = [];
        foreach (var injection in injections)
        {
            Need(injection.AfterBackendOrdinal >= previous && injection.AfterBackendOrdinal <= backend && injection.Tag is not null &&
                injection.Tag.Length is > 0 and <= 48 && injection.Tag.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_'), "service_control_injection_order");
            previous = injection.AfterBackendOrdinal;
            var packet = ServiceSelectedPacket(ServiceHex(injection.Hex), arp.PeerMac, ports, 0, 0);
            Need(packet is not null && !packet.Outgoing, "service_control_injection_frame"); if (injection.Forwarded) sent.Add(packet!);
        }
        var incoming = packets.Where(packet => !packet.Outgoing).ToArray();
        Need(sent.Count == incoming.Length && sent.Zip(incoming).All(pair => pair.First.Raw.AsSpan().SequenceEqual(pair.Second.Raw)), "service_control_injection_capture");
        var pending = packets.Where(packet => packet.Port == control.PendingPort).ToArray();
        Need(pending.Length == 6, "service_control_pending_complete");
        for (int i = 0; i < 2; i++)
        {
            var syn = pending[i * 3]; var synack = pending[i * 3 + 1]; var reset = pending[i * 3 + 2];
            uint client = i == 0 ? control.FirstClientSequence : control.SecondClientSequence;
            Need(!syn.Outgoing && syn.Flags == 2 && syn.Sequence == client && syn.Ack == 0 && syn.Payload.Length == 0 && syn.Window == 8192 &&
                synack.Outgoing && synack.Flags == 0x12 && synack.Ack == unchecked(client + 1) && synack.Payload.Length == 0 &&
                !reset.Outgoing && reset.Flags == 4 && reset.Sequence == unchecked(client + 1) && reset.Ack == 0 && reset.Payload.Length == 0 &&
                synack.Ordinal == (i == 0 ? control.FirstSynAckOrdinal : control.SecondSynAckOrdinal), "service_control_pending_reset");
        }
        Need(pending[1].Sequence != pending[4].Sequence, "service_control_pending_isn");
        var persist = packets.Where(packet => packet.Port == control.Persist.Port).ToArray();
        var healthy = packets.Where(packet => packet.Port == control.Healthy.Port).ToArray();
        Need(persist.Length > 8 && healthy.Length > 8 && !persist[0].Outgoing && persist[0].Flags == 2 && persist[0].Sequence == 0xffffff00 &&
            !healthy[0].Outgoing && healthy[0].Flags == 2 && healthy[0].Sequence == 0xfffffe00, "service_control_http_syn");
        var firstProbe = persist.SingleOrDefault(packet => packet.Ordinal == control.FirstProbeOrdinal);
        var secondProbe = persist.SingleOrDefault(packet => packet.Ordinal == control.SecondProbeOrdinal);
        Need(firstProbe is not null && secondProbe is not null && firstProbe.Outgoing && secondProbe.Outgoing &&
            firstProbe.Payload.Length == 1 && secondProbe.Payload.Length == 1 && firstProbe.Flags is 0x10 or 0x18 && secondProbe.Flags is 0x10 or 0x18 &&
            secondProbe.Sequence == unchecked(firstProbe.Sequence + 1) && secondProbe.Ordinal > firstProbe.Ordinal, "service_control_probes");
        var zero = injections.SingleOrDefault(row => row.Tag == "persist.zero.ack");
        var lost = injections.Single(row => !row.Forwarded); var fresh = injections.SingleOrDefault(row => row.Tag == "persist.reopen.fresh");
        int zeroIndex=Array.FindIndex(injections.ToArray(),row=>row.Tag=="persist.zero.ack"),
            lostIndex=Array.FindIndex(injections.ToArray(),row=>!row.Forwarded),
            freshIndex=Array.FindIndex(injections.ToArray(),row=>row.Tag=="persist.reopen.fresh"),
            healthyIndex=Array.FindIndex(injections.ToArray(),row=>row.Tag=="healthy.1");
        Need(zeroIndex>=0 && zeroIndex<lostIndex && lostIndex<healthyIndex && healthyIndex<freshIndex,"service_control_loss_order");
        var zeroPacket = ServiceSelectedPacket(ServiceHex(zero.Hex), arp.PeerMac, ports, 0, 0)!;
        var lostPacket = ServiceSelectedPacket(ServiceHex(lost.Hex), arp.PeerMac, ports, 0, 0)!;
        var freshPacket = ServiceSelectedPacket(ServiceHex(fresh.Hex), arp.PeerMac, ports, 0, 0)!;
        Need(zeroPacket is not null && lostPacket is not null && freshPacket is not null &&
            zeroPacket.Port == control.Persist.Port && zeroPacket.Flags == 0x10 && zeroPacket.Window == 0 && zeroPacket.Payload.Length==0 &&
            lostPacket.Port == zeroPacket.Port && lostPacket.Flags == 0x10 && lostPacket.Window == 8192 && lostPacket.Payload.Length == 0 &&
            lostPacket.Sequence == zeroPacket.Sequence && lostPacket.Ack == zeroPacket.Ack &&
            freshPacket.Port == zeroPacket.Port && freshPacket.Flags == 0x10 && freshPacket.Window == 8192 && freshPacket.Payload.Length == 0 &&
            freshPacket.Sequence == lostPacket.Sequence && freshPacket.Ack == unchecked(lostPacket.Ack + 1), "service_control_reopen");
        long zeroAt = incoming.Single(packet => packet.Raw.AsSpan().SequenceEqual(zeroPacket!.Raw)).Ordinal;
        long freshAt = incoming.Single(packet => packet.Raw.AsSpan().SequenceEqual(freshPacket!.Raw)).Ordinal;
        Need(firstProbe!.Ordinal < zeroAt && zeroAt < secondProbe!.Ordinal && secondProbe.Ordinal < freshAt &&
            healthy[^1].Ordinal < freshAt && persist.Where(packet => !packet.Outgoing && packet.Ordinal < freshAt).All(packet => packet.Window == 0) &&
            !persist.Any(packet => packet.Outgoing && packet.Ordinal < firstProbe.Ordinal && packet.Payload.Length != 0) &&
            persist.Where(packet => packet.Outgoing && packet.Ordinal > firstProbe.Ordinal && packet.Ordinal < freshAt && packet.Payload.Length != 0)
                .All(packet => packet.Ordinal == secondProbe.Ordinal), "service_control_isolation");
        Need(zeroPacket!.Ack == unchecked(firstProbe.Sequence + 1) && freshPacket!.Ack == unchecked(secondProbe!.Sequence + 1) &&
            firstProbe.Sequence==unchecked(persist[1].Sequence+1), "service_control_probe_ack");
        var exchanges = new[] { control.Persist, control.Healthy }; List<JsonElement> expected = [];
        foreach (var exchange in exchanges)
        {
            Need(exchange.StartedUtc >= started && exchange.CompletedUtc >= exchange.StartedUtc && exchange.CompletedUtc <= ended,
                "service_control_http_window");
            byte[] response = ServiceHex(exchange.ResponseHex); string responseText = Encoding.ASCII.GetString(response);
            string[] dates = responseText.Split("\r\n", StringSplitOptions.None).Where(row => row.StartsWith("Date: ", StringComparison.Ordinal)).ToArray();
            Need(dates.Length == 1 && DateTimeOffset.TryParseExact(dates[0][6..], "r", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out _), "service_control_http_date");
            var date = DateTimeOffset.ParseExact(dates[0][6..], "r", CultureInfo.InvariantCulture);
            Need(date >= exchange.StartedUtc.AddSeconds(-5) && date <= exchange.CompletedUtc.AddSeconds(5), "service_control_http_utc");
            expected.Add(JsonSerializer.SerializeToElement(new { method = "GET", path = "/healthz", status = 200, dateUtc = date.ToString("O", CultureInfo.InvariantCulture),
                bodyBase64 = Convert.ToBase64String("CLROS_SERVICE_HEALTH_OK"u8) }));
        }
        Need(control.Persist.StartedUtc <= control.Healthy.StartedUtc && control.Healthy.CompletedUtc <= control.Persist.CompletedUtc, "service_control_http_order");
        // Exclude ordinary flows from this internal view, preserving the parent
        // Ethernet/ARP prefix. Their complete independent inspection follows.
        byte[] controlHttp = ServiceFilterCapture(http.ToArray(), ports, includeSelected: true);
        var checkedHttp = ServiceHttpPackets(controlHttp, anchor, arp, [(uint)ports[1], (uint)ports[2]], expected.ToArray(), true,
            (i, request, response) => Need(Convert.ToHexString(request) == exchanges[i].RequestHex && Convert.ToHexString(response) == exchanges[i].ResponseHex,
                "service_control_http_literal"));
        return new(ordinary.ToArray(), Array.AsReadOnly(ports), checkedHttp, packets.Count, 2);
    }

    internal static int InspectServiceRun(string root, QemuSmokeOptions options, QemuSmokePlan plan, ProcessResult runner,
        TextWriter output, NativeAotServiceProgram program, NativeAotServicePeer.Completion peer, ServiceRunnerContext context)
    {
        Need(ReferenceEquals(peer.Runner, runner) && peer.Port == context.ForwardPort, "service_runner_peer_owner");
        byte[] raw = File.ReadAllBytes(Path.GetFullPath(options.SerialLog, root));
        byte[] package = File.ReadAllBytes(Path.GetFullPath(options.LinuxElfPackage, root));
        var process = InspectNativeAotServiceRunnerForTests(options, plan, runner, raw,
            File.ReadAllBytes(Path.GetFullPath(options.SummaryLog, root)), context);
        var capture = ValidateNativeAotServiceCapture(root, package, program, raw);
        byte[] bytes = File.ReadAllBytes(Path.GetFullPath(options.SerialLog + ".pcap", root));
        var controls = InspectNativeAotServiceControlsForTests(bytes, capture.Parent.FrameAnchor, capture.Parent.Arp,
            process.ControlsJson, process.FramesJson, process.StartUtc, process.EndUtc);
        string report = File.ReadAllText(peer.Path);
        var stimulus=NativeAotServicePeer.InspectOutput(report);
        using(var peerJson=JsonDocument.Parse(report))
            foreach(var exchange in peerJson.RootElement.GetProperty("exchanges").EnumerateArray())
            {
                var date=DateTimeOffset.ParseExact(exchange.GetProperty("dateUtc").GetString()!,"O",CultureInfo.InvariantCulture);
                Need(date>=process.StartUtc.AddMilliseconds(exchange.GetProperty("startedMilliseconds").GetInt64()).AddSeconds(-5) &&
                    date<=process.EndUtc.AddMilliseconds(exchange.GetProperty("completedMilliseconds").GetInt64()-stimulus.DurationMilliseconds).AddSeconds(5),
                    "service_peer_owned_utc");
            }
        var observed = NativeAotThreadingOutput.ReadService(raw[raw.AsSpan().IndexOf("TEST_START m16-011\r\n"u8)..]);
        string[] rows = observed.NormalizedTail.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        var fresh = rows.Where(row => row.StartsWith("M16_011_SLOT event=0 ", StringComparison.Ordinal)).Select(SocketTransport).ToArray();
        uint[] ports = fresh.Where(row => !controls.Ports.Contains(checked((int)row["remotePort"]))).Select(row => checked((uint)row["remotePort"])).ToArray();
        var http = InspectNativeAotServiceHttpPacketsForTests(controls.OrdinaryCapture, capture.Parent.FrameAnchor, capture.Parent.Arp, ports, report);
        int connections = http.Connections + controls.Http.Connections;
        Need(fresh.Length == connections + 2 && fresh.Count(row => row["remotePort"] == (ulong)controls.Ports[0]) == 2 &&
            fresh.Count(row => row["remotePort"] == (ulong)controls.Ports[1]) == 1 && fresh.Count(row => row["remotePort"] == (ulong)controls.Ports[2]) == 1 &&
            capture.Leaf.SocketAccepts == connections && capture.Leaf.SocketSent == (ulong)(http.ResponseBytes + controls.Http.ResponseBytes) &&
            capture.Leaf.SocketReceived == (ulong)(http.RequestBytes + controls.Http.RequestBytes) &&
            capture.Leaf.Transport.Transfers == connections && capture.Leaf.Transport.Retired == connections &&
            capture.Leaf.Transport.CompactHighWater > 8 && capture.Envelope.Fresh == (ulong)fresh.Length, "service_packet_owner_join");
        ServiceWireTotals(bytes, capture.Parent, capture.Envelope, raw);
        output.WriteLine($"SERVICE_CAPTURE calls={capture.Leaf.Protocol.Calls} clones={capture.Leaf.Protocol.Clones} reaped={capture.Leaf.Protocol.Reaped} connections={connections} pendingResets=2 transfers={capture.Leaf.Transport.Transfers} compactHighWater={capture.Leaf.Transport.CompactHighWater} randomRequests={capture.Leaf.Random.Requests} randomBytes={capture.Leaf.Random.GeneratedBytes} acceptance=pending");
        output.WriteLine($"SERVICE_PACKETS ordinary={http.Connections} controlled={controls.ControlledRecords} lostReopen=1 probes=2 activeCloses={connections} processId={process.ProcessId} lifetimeMilliseconds={process.LifetimeMilliseconds} acceptance=pending");
        return 0;
    }

    private static void ServiceWireTotals(byte[] bytes, WebParentReport parent, ServiceEnvelopeInspection envelope, byte[] raw)
    {
        int stage = 0; bool resolved = false; ulong peer = 0, received = 0, arp = 0, arpCopied = 0, tcpCopied = 0, ignored = 0;
        Dictionary<uint, ulong> closes = [];
        Dictionary<(uint Port,uint Sequence),Stack<int>> syns=[];
        HashSet<(uint Port,uint Ack,uint Sequence)> synAcks=[];
        int ordinal=0,firstSynAck=0;
        for (int offset = 24; offset < bytes.Length;)
        {
            ordinal++;
            ulong time = (ulong)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset)) * 1_000_000 + BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset + 4));
            int size = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset + 8))); offset += 16;
            var frame = bytes.AsSpan(offset, size); offset += size;
            if (stage < 3)
            { if (stage == 0) { if (size == 60 && OS.Std.Hash.Crc32.Compute(frame) == parent.FrameAnchor) stage++; } else stage++; continue; }
            if (SocketIgnoredRouterAdvertisement(frame)) { if (resolved) { received++; ignored++; } else peer++; continue; }
            bool outgoing = MatchNetworkMac(frame[6..], 0x020000000001); ushort type = BinaryPrimitives.ReadUInt16BigEndian(frame[12..]);
            if (!resolved)
            { if (!outgoing) { peer++; if (type == 0x0806 && BinaryPrimitives.ReadUInt16BigEndian(frame[20..]) == 2) resolved = true; } continue; }
            if (!outgoing) received++;
            if (type == 0x0806) { if (outgoing) arpCopied++; else arp++; continue; }
            if (!ReadSocketTcpFrame(frame, outgoing, parent.Arp.PeerMac, out var packet)) { ignored++; continue; }
            if(!outgoing && packet.Flags==2)
            {
                var key=(packet.Remote,packet.Sequence);
                if(!syns.TryGetValue(key,out var pending))syns.Add(key,pending=new());
                pending.Push(ordinal);
            }
            if(outgoing && packet.Flags==0x12)
            {
                var signature=(packet.Remote,packet.Ack,packet.Sequence);
                if(syns.TryGetValue((packet.Remote,unchecked(packet.Ack-1)),out var pending) && pending.Count!=0)pending.Pop();
                else Need(synAcks.Contains(signature),"service_packet_syn_ack_source");
                synAcks.Add(signature);if(firstSynAck==0)firstSynAck=ordinal;
            }
            if (outgoing)
            { tcpCopied++; if ((packet.Flags & 1) != 0) closes.TryAdd(packet.Remote, time); }
        }
        // The complete flow/control models bind every answered handshake.
        // Only unanswered SYNs before the first answer belong to startup quiet
        // time; they are valid TCP packets whose receive result is Ignored.
        int[] quietSyns=syns.Values.SelectMany(pending=>pending).ToArray();
        Need(firstSynAck>0 && quietSyns.Length<=1600 && quietSyns.All(at=>at<firstSynAck),"service_packet_quiet_syn_lifetime");
        ignored+=(uint)quietSyns.Length;
        string[] rows = ReadThreadingSerialLines(DecodeUtf8(raw));
        var peerRow = ReadRecord(rows.Single(row => row.StartsWith("M16_011_PEER ", StringComparison.Ordinal)), "M16_011_PEER",
            "mac:dec", "sent:dec", "received:dec", "start:dec", "end:dec");
        if(!(resolved && peer == peerRow["received"] && received == envelope.Received && arp == envelope.Arp && arpCopied == envelope.ArpCopied &&
            tcpCopied == envelope.TcpCopied && ignored == envelope.Ignored && received - arp - ignored == envelope.Accepted))
            throw new ToolFailure("service_packet_nic_join",$"peer={peer}/{peerRow["received"]} received={received}/{envelope.Received} arp={arp}/{envelope.Arp} arpCopied={arpCopied}/{envelope.ArpCopied} tcpCopied={tcpCopied}/{envelope.TcpCopied} ignored={ignored}/{envelope.Ignored}");
        ulong[] times = closes.Values.Order().ToArray();
        Need(times.Length > 8 && times[8] - times[0] < 240_000_000, "service_packet_compact_reuse_before_expiry");
    }

    private static byte[] ServiceHex(string? hex)
    {
        Need(hex is not null && hex.Length is >= 108 and <= 32768 && hex.Length % 2 == 0 && hex.All(char.IsAsciiHexDigit), "service_control_hex");
        return Convert.FromHexString(hex!);
    }

    private static ServiceControlPacket? ServiceSelectedPacket(byte[] raw, ulong peer, IReadOnlyList<int> ports, long ordinal, ulong time)
    {
        Need(raw.Length is >= 14 and <= 1514, "service_control_frame_extent");
        if (BinaryPrimitives.ReadUInt16BigEndian(raw.AsSpan(12)) != 0x0800 || raw.Length < 54 || raw[23] != 6) return null;
        int ihl = (raw[14] & 15) * 4; Need(ihl >= 20 && raw.Length >= 14 + ihl + 20, "service_control_ip_extent");
        bool outgoing = BinaryPrimitives.ReadUInt32BigEndian(raw.AsSpan(26)) == 0x0a00020f;
        int port = BinaryPrimitives.ReadUInt16BigEndian(raw.AsSpan(14 + ihl + (outgoing ? 2 : 0)));
        if (!ports.Contains(port)) return null;
        Need(MatchNetworkMac(raw.AsSpan(6), outgoing ? 0x020000000001 : peer) && ReadSocketTcpFrame(raw, outgoing, peer, out _), "service_control_packet");
        _ = ReadSocketTcpFrame(raw, outgoing, peer, out var packet);
        return new(ordinal, time, raw, outgoing, packet.Remote, packet.Sequence, packet.Ack, packet.Flags,
            BinaryPrimitives.ReadUInt16BigEndian(raw.AsSpan(14 + ihl + 14)), packet.Payload.ToArray());
    }

    private static byte[] ServiceFilterCapture(byte[] bytes, IReadOnlyList<int> ports, bool includeSelected)
    {
        using MemoryStream view = new(); view.Write(bytes.AsSpan(0, 24));
        for (int offset = 24; offset < bytes.Length;)
        {
            int start = offset, size = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset + 8))); offset += 16;
            var frame = bytes.AsSpan(offset, size); offset += size; bool tcp = size >= 54 && BinaryPrimitives.ReadUInt16BigEndian(frame[12..]) == 0x0800 && frame[23] == 6;
            int ihl = tcp ? (frame[14] & 15) * 4 : 0;
            Need(!tcp || ihl >= 20 && size >= 14 + ihl + 20, "service_control_filter_extent");
            bool selected = tcp && ports.Contains(BinaryPrimitives.ReadUInt16BigEndian(frame[(14 + ihl +
                (BinaryPrimitives.ReadUInt32BigEndian(frame[26..]) == 0x0a00020f ? 2 : 0))..]));
            if (!tcp || selected == includeSelected) view.Write(bytes.AsSpan(start, offset - start));
        }
        return view.ToArray();
    }
}
