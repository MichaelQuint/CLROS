using System.Buffers.Binary;
using OS.Tools.QemuRunner;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    public const int M07026TcpStages = 11;
    public const int M07026SchedulerStages = 3;
    public static IReadOnlyList<string> M07026TcpStarts { get; } = Array.AsReadOnly(Enumerable.Range(13, 6).Select(i => $"TEST_START m07.{i:000}").ToArray());
    public static IReadOnlyList<string> M07026TcpEnds { get; } = Array.AsReadOnly(Enumerable.Range(13, 6).SelectMany(i => new[] { $"CLROS_M07_{i:000}_READY", $"TEST_PASS m07.{i:000}" }).ToArray());
    public static int M07026TcpRecords => M07026TcpStarts.Count + M07026TcpStages + M07026TcpEnds.Count;
    public static IReadOnlyList<string> M07026Markers => Array.AsReadOnly<string>([
        .. QemuContracts.M03OwnedSubsystemMarkers,
        "TEST_START m16-002", "M16_002_STAGED", "M16_002_DIGEST", "M16_002_PACKAGE_DIGEST", "M16_002_OWNED",
        "CLROS_M16_002_READY", "TEST_PASS m16-002", .. M16ServiceLoopMarkers,
        .. QemuContracts.M07ArpReceiveMarkers.Skip(QemuContracts.M03OwnedSubsystemMarkers.Count),
        "TEST_START m07-026", "M07_026_SCHED stage=0 ", .. Enumerable.Range(0, 4).Select(i => "M07_026_ROW stage=" + i + " "),
        .. M07026TcpStarts, .. Enumerable.Range(0, M07026TcpStages).Select(i => "M07_026_TCP stage=" + i + " "), .. M07026TcpEnds,
        .. Enumerable.Range(4, 3).Select(i => "M07_026_ROW stage=" + i + " "),
        "M07_026_SCHED stage=1 ", "M07_026_SCHED stage=2 ",
        "CLROS_M07_026_READY", "TEST_PASS m07-026"]);

    // Validate the finite child, preserving the original complete parents for
    // their existing package/service/PCI/net/ARP validators in M03.
    public static string[] ValidateNetworkServiceTail(string[] lines)
    {
        Need(lines.Length == M07026Markers.Count, "m07_026_record_count");
        int start = lines.Length - 10 - M07026TcpRecords - M07026SchedulerStages;
        Need(lines[start] == "TEST_START m07-026" && lines[^2] == "CLROS_M07_026_READY" &&
            lines[^1] == "TEST_PASS m07-026", "m07_026_tail_identity");
        Dictionary<string, ulong> Row(int stage)
        {
            var row = ReadRecord(lines[start + stage + 2 + (stage >= 4 ? M07026TcpRecords : 0)], "M07_026_ROW", "stage:dec", "a:dec", "b:dec", "c:dec", "d:dec");
            Need(row["stage"] == (uint)stage, "m07_026_row_order");
            return row;
        }
        var dma = Row(0);
        var full = Row(1);
        var arp = Row(2);
        var ip = Row(3);
        var stop = Row(4);
        var pool = Row(5);
        var restored = Row(6);
        Need(dma["a"] != 0 && (dma["a"] & 4095) == 0 && dma["a"] <= 0x100000000UL - 32768 &&
            dma["b"] == 32768 && dma["c"] == QemuContracts.M07ArpReceiveFeatures && dma["d"] == 8, "m07_026_dma");
        Need(full["a"] == 8 && full["b"] == 8 && full["c"] == 1 && full["d"] == 0, "m07_026_full");
        Need(arp["a"] == 20 && arp["b"] == 20 && arp["c"] >= 12 && arp["c"] <= 20 &&
            arp["d"] > 0 && arp["d"] < 0xffffffffffff && (arp["d"] & 0x010000000000) == 0, "m07_026_arp");
        Need(ip["a"] == 12 && ip["b"] == 32 && ip["c"] == 32 && ip["d"] >= 2000000000, "m07_026_ip");
        var tcp = ValidateNetworkTcpRows(lines, start + 6, out ulong timeWaitInterval);
        var scheduled = ReadRecord(lines[start + 1], "M07_026_SCHED", "stage:dec", "a:dec", "b:dec", "c:dec", "d:dec");
        var progress = ReadRecord(lines[^4], "M07_026_SCHED", "stage:dec", "a:dec", "b:dec", "c:dec", "d:dec");
        var reaped = ReadRecord(lines[^3], "M07_026_SCHED", "stage:dec", "a:dec", "b:dec", "c:dec", "d:dec");
        Need(scheduled["stage"] == 0 && scheduled["a"] >= 4096 && (scheduled["a"] & 4095) == 0 &&
            scheduled["a"] <= 0x100000000UL - 131072 && scheduled["b"] == 32 && scheduled["c"] == 1 && scheduled["d"] == 0,
            "m07_scheduler_owned_ready");
        Need(progress["stage"] == 1 && progress["a"] is >= 4 and <= 4096 && progress["b"] >= 2 && progress["c"] >= 2 &&
            progress["b"] <= progress["a"] && progress["c"] <= progress["a"] && progress["a"] == progress["b"] + progress["c"] &&
            progress["d"] is > 0 and <= 1000000000 && (progress["c"] + 1) * progress["d"] >= timeWaitInterval,
            "m07_scheduler_runnable_progress");
        Need(reaped["stage"] == 2 && reaped["a"] == 2 * (progress["a"] + 1) && reaped["b"] == 1 && reaped["c"] == 0 && reaped["d"] == 32,
            "m07_scheduler_off_stack_reap_restore");
        Need(scheduled["a"] + 131072 <= dma["a"] || dma["a"] + 32768 <= scheduled["a"], "m07_scheduler_dma_disjoint");
        Need(stop["a"] == tcp["c"] && stop["b"] == tcp["d"] && stop["c"] is >= 1 and <= 1024 &&
            stop["d"] == 2, "m07_026_quiescence");
        Need(pool["a"] == 8 && pool["b"] == 0 && pool["c"] == 8 && pool["d"] == 1, "m07_026_pool_release_retained_reservation");
        Need(restored["a"] == 2 && restored["b"] == 2 && restored["c"] == 0 &&
            restored["d"] <= ushort.MaxValue && (restored["d"] & 0x406) == 0x402, "m07_026_original_allocator");
        int serviceEnd = QemuContracts.M03OwnedSubsystemMarkers.Count + 7 + M16ServiceLoopMarkers.Count;
        string[] net = [.. lines.Take(QemuContracts.M03OwnedSubsystemMarkers.Count), .. lines[serviceEnd..start]];
        var parent = AssertM07ArpReceiveRecords(net);
        Need(parent.PeerMac == arp["d"], "m07_026_peer_association");
        ulong[] parentAllocator = PrefixRow(net, "M07_ARP A", 4);
        Need(restored["a"] == parentAllocator[0] && restored["b"] == parentAllocator[1] &&
            restored["c"] == parentAllocator[2], "m07_026_allocator_association");
        ulong originalPage = PrefixRow(net, "M07_ARP 2", 4)[0], bar = PrefixRow(net, "M07_ARP 0", 4)[3];
        Need(restored["d"] == PrefixRow(net, "M07_ARP 0", 4)[2], "m07_026_original_command_association");
        Need((dma["a"] + 32768 <= originalPage || originalPage + 8192 <= dma["a"]) &&
            (dma["a"] + 32768 <= bar || bar + 16384 <= dma["a"]), "m07_026_disjoint_arena");
        return lines[..start];
    }

    private static Dictionary<string, ulong> ValidateNetworkTcpRows(string[] lines, int start, out ulong timeWaitInterval)
    {
        foreach (string marker in M07026TcpStarts)
            Need(lines[start++] == marker, "m07_tcp_start_marker");
        Dictionary<string, ulong>[] rows = new Dictionary<string, ulong>[M07026TcpStages];
        for (int stage = 0; stage < rows.Length; stage++)
        {
            rows[stage] = ReadRecord(lines[start++], "M07_026_TCP", "stage:dec", "a:dec", "b:dec", "c:dec", "d:dec");
            Need(rows[stage]["stage"] == (uint)stage, "m07_tcp_row_order");
        }
        foreach (string marker in M07026TcpEnds)
            Need(lines[start++] == marker, "m07_tcp_end_marker");
        bool Exact(int stage, ulong a, ulong b, ulong c, ulong d) => rows[stage]["a"] == a && rows[stage]["b"] == b && rows[stage]["c"] == c && rows[stage]["d"] == d;
        Need(Exact(0, 2, 1, 4, 2) && Exact(1, 2, 1, 1, 6), "m07_tcp_handshake_pressure");
        var wait = rows[2];
        Need(wait["d"] == 240000000000 && wait["b"] >= wait["a"] && wait["b"] >= wait["c"] &&
            wait["b"] - wait["c"] >= wait["d"] && wait["b"] - wait["c"] <= 248000000000, "m07_tcp_time_wait_start");
        ulong beforeFin = (ulong)ContinuingTcpScenario.Packets.TakeWhile(x => !(x.FromGuest && x.Port == 40000 && x.Sequence == 1021 && x.Flags == 0x11)).Count(x => x.FromGuest);
        Need(Exact(3, 11, 4, 10017, 4) && rows[4]["a"] == 20 && rows[4]["b"] == 1021 && rows[4]["c"] == 1 &&
            rows[4]["d"] == beforeFin && Exact(5, 20, 15, 1, 1) && Exact(6, 2, 3, 4, 8), "m07_tcp_stream_close_reset");
        var timeout = rows[7];
        var pending = rows[8];
        var closed = rows[9];
        var final = rows[10];
        Need(timeout["a"] == 2 && timeout["c"] >= timeout["b"] && timeout["c"] - timeout["b"] >= 7000000000 && timeout["d"] == 1 &&
            pending["a"] == 3 && pending["c"] >= pending["b"] && pending["d"] == 1, "m07_tcp_bounded_timeout");
        Need(closed["a"] == wait["b"] && closed["b"] >= closed["a"] && closed["b"] >= pending["c"] &&
            closed["c"] == 0 && closed["d"] == 5, "m07_tcp_time_wait_reap");
        timeWaitInterval = closed["b"] - pending["c"];
        ulong sent = (ulong)ContinuingTcpScenario.Packets.Count(x => x.FromGuest), received = (ulong)ContinuingTcpScenario.Packets.Count(x => !x.FromGuest);
        Need(final["a"] == 31 && final["b"] == sent && final["c"] == 33 + received && final["d"] == 33 + sent && rows[4]["d"] < sent,
            "m07_tcp_packet_accounting");
        return final;
    }

    public static void AssertM07026Capture(string serialPath, string capturePath)
    {
        string[] lines = File.ReadAllLines(serialPath).Where(x => !x.StartsWith("UEFI ", StringComparison.Ordinal)).ToArray();
        // FirmwareProtocol and all serial parents are checked by the caller;
        // capture admission independently requires the original fixed-frame anchor.
        Need(File.Exists(capturePath) && new FileInfo(capturePath).Length is > 24 and <= 1048576, "m07_026_capture_extent");
        byte[] bytes = File.ReadAllBytes(capturePath);
        int row = Array.FindIndex(lines, x => x.StartsWith("M07_NET D ", StringComparison.Ordinal));
        Need(row >= 0, "m07_026_capture_parent_crc");
        ulong[] anchor = ReadHexRecord(lines[row], "M07_NET D", 4);
        Need(anchor[0] <= uint.MaxValue, "m07_026_capture_crc_width");
        int peerRow = Array.FindIndex(lines, x => x.StartsWith("M07_026_ROW stage=2 ", StringComparison.Ordinal));
        Need(peerRow >= 0, "m07_026_capture_peer");
        ulong peerMac = ReadRecord(lines[peerRow], "M07_026_ROW", "stage:dec", "a:dec", "b:dec", "c:dec", "d:dec")["d"];
        _ = QemuEthernetCapture.Inspect(bytes, checked((uint)anchor[0]));
        uint requests = 0, replies = 0, requestMask = 0, replyMask = 0;
        bool anchored = false;
        int tcpIndex = 0, finalEchoes = 0, capturedFrames = 0, warmupFrames = 0;
        ulong lastTimestamp = 0, closedTimestamp = 0;
        Dictionary<string, ulong> times = new(StringComparer.Ordinal);
        int offset = 24;
        while (offset < bytes.Length)
        {
            // Inspect already validated every record extent and header above.
            ulong timestamp = (ulong)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset)) * 1000000 +
                BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset + 4));
            Need(BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset + 4)) < 1000000 && timestamp >= lastTimestamp, "m07_tcp_capture_clock");
            lastTimestamp = timestamp;
            capturedFrames++;
            int size = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset + 8)));
            offset += 16;
            ReadOnlySpan<byte> frame = bytes.AsSpan(offset, size);
            offset += size;
            if (frame.Length < 14)
                continue;
            ushort type = BinaryPrimitives.ReadUInt16BigEndian(frame[12..]);
            if (!anchored && frame.Length == 60 && frame[..14].SequenceEqual(new byte[] { 2, 0, 0, 0, 0, 2, 2, 0, 0, 0, 0, 1, 0x88, 0xb5 }) &&
                frame[14..].IndexOfAnyExcept((byte)0) < 0)
            {
                Need(warmupFrames++ == 0 && capturedFrames == 1, "m07_tcp_original_queue_warmup");
                continue;
            }
            if (type == 0x88b5 && frame.Length == 60 && OS.Std.Hash.Crc32.Compute(frame) == anchor[0])
            {
                Need(!anchored && warmupFrames == 1 && capturedFrames == 2, "m07_tcp_original_anchor_order");
                anchored = true;
                continue;
            }
            Need(anchored, "m07_tcp_unexpected_original_prefix");
            if (type == 0x0800 && frame.Length >= 34 && frame[23] == 6)
            {
                Need(requestMask == 4095 && replyMask == 4095 && finalEchoes == 0 && tcpIndex < ContinuingTcpScenario.Packets.Count, "m07_tcp_capture_phase");
                var expected = ContinuingTcpScenario.Packets[tcpIndex++];
                try
                {
                    ContinuingTcpScenario.Validate(frame, expected, peerMac);
                }
                catch (InvalidDataException) { Need(false, "m07_tcp_capture_wire"); }
                if (expected.Tag.Length != 0)
                    times.Add(expected.Tag, timestamp);
                continue;
            }
            if (type == 0x0806 && frame.Length >= 42 && frame[14..20].SequenceEqual(new byte[] { 0, 1, 8, 0, 6, 4 }))
            {
                ushort operation = BinaryPrimitives.ReadUInt16BigEndian(frame[20..]);
                if (operation == 1 && MatchNetworkMac(frame, 0xffffffffffff) && MatchNetworkMac(frame[6..], 0x020000000001) &&
                    MatchNetworkMac(frame[22..], 0x020000000001) && frame[28..32].SequenceEqual(new byte[] { 10, 0, 2, 15 }) &&
                    frame[38..42].SequenceEqual(new byte[] { 10, 0, 2, 2 }))
                    requests++;
                if (operation == 2 && MatchNetworkMac(frame, 0x020000000001) && MatchNetworkMac(frame[6..], peerMac) &&
                    MatchNetworkMac(frame[22..], peerMac) && MatchNetworkMac(frame[32..], 0x020000000001) &&
                    frame[28..32].SequenceEqual(new byte[] { 10, 0, 2, 2 }) &&
                    frame[38..42].SequenceEqual(new byte[] { 10, 0, 2, 15 }))
                    replies++;
                Need(replies <= requests, "m07_026_capture_arp_order");
            }
            if (type != 0x0800 || frame.Length < 42 || frame[14] != 0x45 || frame[23] != 1)
                continue;
            int total = BinaryPrimitives.ReadUInt16BigEndian(frame[16..]);
            if (total < 28 || total > frame.Length - 14 || (BinaryPrimitives.ReadUInt16BigEndian(frame[20..]) & 0xbfff) != 0 ||
                !NetworkChecksum(frame.Slice(14, 20)) ||
                !NetworkChecksum(frame.Slice(34, total - 20)))
                continue;
            ReadOnlySpan<byte> icmp = frame.Slice(34, total - 20);
            if (icmp[1] == 0 && BinaryPrimitives.ReadUInt16BigEndian(icmp[4..]) == 227 && icmp[8..].SequenceEqual("CLROS TCP closed"u8))
            {
                Need(tcpIndex == ContinuingTcpScenario.Packets.Count && BinaryPrimitives.ReadUInt16BigEndian(icmp[6..]) == 0 && finalEchoes < 2, "m07_tcp_capture_final_phase");
                bool outgoing = finalEchoes == 0;
                Need(icmp[0] == (outgoing ? 8 : 0) && MatchNetworkMac(frame, outgoing ? peerMac : 0x020000000001) &&
                    MatchNetworkMac(frame[6..], outgoing ? 0x020000000001 : peerMac) &&
                    frame[26..30].SequenceEqual(outgoing ? new byte[] { 10, 0, 2, 15 } : new byte[] { 10, 0, 2, 2 }) &&
                    frame[30..34].SequenceEqual(outgoing ? new byte[] { 10, 0, 2, 2 } : new byte[] { 10, 0, 2, 15 }), "m07_tcp_capture_final_echo");
                if (outgoing)
                    closedTimestamp = timestamp;
                finalEchoes++;
                continue;
            }
            if (icmp[1] != 0 || BinaryPrimitives.ReadUInt16BigEndian(icmp[4..]) != 226 ||
                !icmp[8..].SequenceEqual("CLROS continuing network"u8))
                continue;
            ushort sequence = BinaryPrimitives.ReadUInt16BigEndian(icmp[6..]);
            Need(sequence < 12, "m07_026_capture_sequence");
            uint bit = 1U << sequence;
            if (icmp[0] == 8 && MatchNetworkMac(frame, peerMac) && MatchNetworkMac(frame[6..], 0x020000000001) &&
                frame[26..30].SequenceEqual(new byte[] { 10, 0, 2, 15 }) && frame[30..34].SequenceEqual(new byte[] { 10, 0, 2, 2 }))
            {
                Need((requestMask & bit) == 0 && replyMask == requestMask, "m07_026_capture_request_order");
                requestMask |= bit;
            }
            else if (icmp[0] == 0 && MatchNetworkMac(frame, 0x020000000001) && MatchNetworkMac(frame[6..], peerMac) &&
                frame[26..30].SequenceEqual(new byte[] { 10, 0, 2, 2 }) && frame[30..34].SequenceEqual(new byte[] { 10, 0, 2, 15 }))
            {
                Need((requestMask & bit) != 0 && (replyMask & bit) == 0, "m07_026_capture_reply_order");
                replyMask |= bit;
            }
        }
        // One original ARP parent exchange plus20 continuing exchanges.
        Need(requests == 21 && replies == 21 && requestMask == 4095 && replyMask == 4095, "m07_026_capture_exchange_counts");
        Need(warmupFrames == 1 && tcpIndex == ContinuingTcpScenario.Packets.Count && finalEchoes == 2 && capturedFrames == 70 + tcpIndex, "m07_tcp_capture_counts");
        void Interval(string first, string second, ulong minimum)
        {
            Need(times.TryGetValue(first, out ulong a) && times.TryGetValue(second, out ulong b) && b >= a && b - a + 2000 >= minimum,
                "m07_tcp_capture_retry_interval"); // Two milliseconds bounds independent wire/callback observation skew.
        }
        Interval("syn.first", "syn.retry", 1000000);
        Interval("data.first", "data.retry", 3000000);
        Interval("loss.first", "loss.retry1", 1000000);
        Interval("loss.retry1", "loss.retry2", 2000000);
        Interval("pending.first", "pending.retry1", 1000000);
        Interval("pending.retry1", "pending.retry2", 1000000);
        Need(times.TryGetValue("wait.fin", out ulong waitFin) && closedTimestamp >= waitFin && closedTimestamp - waitFin >= 240000000 &&
            times.TryGetValue("syn.first", out ulong began) && closedTimestamp >= began && closedTimestamp - began <= 295000000,
            "m07_tcp_capture_real_time_wait");
    }

    private static bool NetworkChecksum(ReadOnlySpan<byte> bytes)
    {
        uint sum = 0;
        for (int i = 0; i < bytes.Length; i += 2)
            sum += (uint)(bytes[i] << 8) | (i + 1 < bytes.Length ? bytes[i + 1] : 0U);
        while ((sum >> 16) != 0)
            sum = (sum & 65535) + (sum >> 16);
        return sum == 65535;
    }
    private static bool MatchNetworkMac(ReadOnlySpan<byte> bytes, ulong mac)
    {
        if (bytes.Length < 6)
            return false;
        for (int i = 0; i < 6; i++)
            if (bytes[i] != (byte)(mac >> (40 - i * 8)))
                return false;
        return true;
    }
}
