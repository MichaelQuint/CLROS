using System.Buffers.Binary;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    public static IReadOnlyList<string> M07026Markers => Array.AsReadOnly<string>([
        .. QemuContracts.M03OwnedSubsystemMarkers,
        "TEST_START m16-002", "M16_002_STAGED", "M16_002_DIGEST", "M16_002_PACKAGE_DIGEST", "M16_002_OWNED",
        "CLROS_M16_002_READY", "TEST_PASS m16-002", .. M16ServiceLoopMarkers,
        .. QemuContracts.M07ArpReceiveMarkers.Skip(QemuContracts.M03OwnedSubsystemMarkers.Count),
        "TEST_START m07-026", .. Enumerable.Range(0, 7).Select(i => "M07_026_ROW stage=" + i + " "),
        "CLROS_M07_026_READY", "TEST_PASS m07-026"]);

    // Validate the finite child, preserving the original complete parents for
    // their existing package/service/PCI/net/ARP validators in M03.
    public static string[] ValidateNetworkServiceTail(string[] lines)
    {
        Need(lines.Length == M07026Markers.Count, "m07_026_record_count");
        int start = lines.Length - 10;
        Need(lines[start] == "TEST_START m07-026" && lines[^2] == "CLROS_M07_026_READY" &&
            lines[^1] == "TEST_PASS m07-026", "m07_026_tail_identity");
        Dictionary<string, ulong> Row(int stage)
        {
            var row = ReadRecord(lines[start + stage + 1], "M07_026_ROW", "stage:dec", "a:dec", "b:dec", "c:dec", "d:dec");
            Need(row["stage"] == (uint)stage, "m07_026_row_order"); return row;
        }
        var dma = Row(0); var full = Row(1); var arp = Row(2); var ip = Row(3); var stop = Row(4); var pool = Row(5); var restored = Row(6);
        Need(dma["a"] != 0 && (dma["a"] & 4095) == 0 && dma["a"] <= 0x100000000UL - 32768 &&
            dma["b"] == 32768 && dma["c"] == QemuContracts.M07ArpReceiveFeatures && dma["d"] == 8, "m07_026_dma");
        Need(full["a"] == 8 && full["b"] == 8 && full["c"] == 1 && full["d"] == 0, "m07_026_full");
        Need(arp["a"] == 20 && arp["b"] == 20 && arp["c"] >= 12 && arp["c"] <= 20 &&
            arp["d"] > 0 && arp["d"] < 0xffffffffffff && (arp["d"] & 0x010000000000) == 0, "m07_026_arp");
        Need(ip["a"] == 12 && ip["b"] == 32 && ip["c"] == 32 && ip["d"] >= 2000000000, "m07_026_ip");
        Need(stop["a"] == 32 && stop["b"] == 32 && stop["c"] is >= 1 and <= 1024 &&
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
        uint requests = 0, replies = 0, requestMask = 0, replyMask = 0; bool anchored = false;
        int offset = 24;
        while (offset < bytes.Length)
        {
            // Inspect already validated every record extent and header above.
            int size = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset + 8)));
            offset += 16; ReadOnlySpan<byte> frame = bytes.AsSpan(offset, size); offset += size;
            if (frame.Length < 14) continue;
            ushort type = BinaryPrimitives.ReadUInt16BigEndian(frame[12..]);
            if (type == 0x88b5 && frame.Length == 60 && OS.Std.Hash.Crc32.Compute(frame) == anchor[0])
            { anchored = true; continue; }
            if (!anchored) continue;
            if (type == 0x0806 && frame.Length >= 42 && frame[14..20].SequenceEqual(new byte[] { 0, 1, 8, 0, 6, 4 }))
            {
                ushort operation = BinaryPrimitives.ReadUInt16BigEndian(frame[20..]);
                if (operation == 1 && MatchNetworkMac(frame, 0xffffffffffff) && MatchNetworkMac(frame[6..], 0x020000000001) &&
                    MatchNetworkMac(frame[22..], 0x020000000001) && frame[28..32].SequenceEqual(new byte[] { 10, 0, 2, 15 }) &&
                    frame[38..42].SequenceEqual(new byte[] { 10, 0, 2, 2 })) requests++;
                if (operation == 2 && MatchNetworkMac(frame, 0x020000000001) && MatchNetworkMac(frame[6..], peerMac) &&
                    MatchNetworkMac(frame[22..], peerMac) && MatchNetworkMac(frame[32..], 0x020000000001) &&
                    frame[28..32].SequenceEqual(new byte[] { 10, 0, 2, 2 }) &&
                    frame[38..42].SequenceEqual(new byte[] { 10, 0, 2, 15 })) replies++;
                Need(replies <= requests, "m07_026_capture_arp_order");
            }
            if (type != 0x0800 || frame.Length < 42 || frame[14] != 0x45 || frame[23] != 1) continue;
            int total = BinaryPrimitives.ReadUInt16BigEndian(frame[16..]);
            if (total < 28 || total > frame.Length - 14 || (BinaryPrimitives.ReadUInt16BigEndian(frame[20..]) & 0xbfff) != 0 ||
                !NetworkChecksum(frame.Slice(14, 20)) ||
                !NetworkChecksum(frame.Slice(34, total - 20))) continue;
            ReadOnlySpan<byte> icmp = frame.Slice(34, total - 20);
            if (icmp[1] != 0 || BinaryPrimitives.ReadUInt16BigEndian(icmp[4..]) != 226 ||
                !icmp[8..].SequenceEqual("CLROS continuing network"u8)) continue;
            ushort sequence = BinaryPrimitives.ReadUInt16BigEndian(icmp[6..]);
            Need(sequence < 12, "m07_026_capture_sequence"); uint bit = 1U << sequence;
            if (icmp[0] == 8 && MatchNetworkMac(frame, peerMac) && MatchNetworkMac(frame[6..], 0x020000000001) &&
                frame[26..30].SequenceEqual(new byte[] { 10, 0, 2, 15 }) && frame[30..34].SequenceEqual(new byte[] { 10, 0, 2, 2 }))
            { Need((requestMask & bit) == 0 && replyMask == requestMask, "m07_026_capture_request_order"); requestMask |= bit; }
            else if (icmp[0] == 0 && MatchNetworkMac(frame, 0x020000000001) && MatchNetworkMac(frame[6..], peerMac) &&
                frame[26..30].SequenceEqual(new byte[] { 10, 0, 2, 2 }) && frame[30..34].SequenceEqual(new byte[] { 10, 0, 2, 15 }))
            { Need((requestMask & bit) != 0 && (replyMask & bit) == 0, "m07_026_capture_reply_order"); replyMask |= bit; }
        }
        // One original ARP parent exchange plus20 continuing exchanges.
        Need(requests == 21 && replies == 21 && requestMask == 4095 && replyMask == 4095, "m07_026_capture_exchange_counts");
    }

    private static bool NetworkChecksum(ReadOnlySpan<byte> bytes)
    {
        uint sum = 0;
        for (int i = 0; i < bytes.Length; i += 2) sum += (uint)(bytes[i] << 8) | (i + 1 < bytes.Length ? bytes[i + 1] : 0U);
        while ((sum >> 16) != 0) sum = (sum & 65535) + (sum >> 16);
        return sum == 65535;
    }
    private static bool MatchNetworkMac(ReadOnlySpan<byte> bytes, ulong mac)
    {
        if (bytes.Length < 6) return false;
        for (int i = 0; i < 6; i++) if (bytes[i] != (byte)(mac >> (40 - i * 8))) return false;
        return true;
    }
}
