namespace OS.Tooling;

public readonly record struct QemuArpReceiveReceipt(uint ReceiveCrc32, uint ReceiveBytes,
    uint RequestCrc32, ulong PeerMac);

public static partial class QemuTranscripts
{
    public static string[] NormalizeM07ArpReceiveRecords(IReadOnlyList<string> lines)
    {
        Need(lines.Count == QemuContracts.M07ArpReceiveMarkers.Count, "m07_arp_record_count");
        return lines.Take(QemuContracts.M07VirtioNetMarkers.Count).ToArray();
    }

    public static QemuArpReceiveReceipt AssertM07ArpReceiveRecords(IReadOnlyList<string> lines)
    {
        string[] parent = NormalizeM07ArpReceiveRecords(lines);
        _ = AssertM07VirtioNetRecords(parent);
        for (int i = parent.Length; i < lines.Count; i++)
        {
            string expected = QemuContracts.M07ArpReceiveMarkers[i];
            if (expected.StartsWith("M07_ARP ", StringComparison.Ordinal)) _ = ReadHexRecord(lines[i], expected, 4);
            else Need(lines[i] == expected, "m07_arp_marker_order");
        }
        ulong[] Row(int stage) => PrefixRow(lines, "M07_ARP " + stage.ToString("X", System.Globalization.CultureInfo.InvariantCulture), 4);
        Need(Row(0).SequenceEqual(PrefixRow(parent, "M07_NET 0", 4)), "m07_arp_same_binding");
        Need(Row(1).SequenceEqual(PrefixRow(parent, "M07_NET 1", 4)), "m07_arp_same_features");
        Need(Row(2).SequenceEqual(PrefixRow(parent, "M07_NET 4", 4)), "m07_arp_fresh_restored_dma");
        Need(Row(3).SequenceEqual(PrefixRow(parent, "M07_NET 5", 4)) &&
            Row(4).SequenceEqual(PrefixRow(parent, "M07_NET 6", 4)), "m07_arp_ring_layout");
        ulong[] used = Row(5), cache = Row(7), crc = Row(8), waits = Row(11);
        Need(used[0] == 1 && used[1] == 0 && used[2] >= 54 && used[2] <= 72 && used[3] == 1, "m07_arp_receive_used");
        Need(Row(6).SequenceEqual(new ulong[] { 12, 1, 0, 0x100000000 }), "m07_arp_modern_no_offload");
        Need(cache[0] == 1 && cache[1] == 2 && cache[2] > 0 && cache[2] < 0xffffffffffff &&
            (cache[2] & 0x010000000000) == 0 && cache[3] == 100, "m07_arp_bounded_cache");
        Need(crc[0] <= uint.MaxValue && crc[1] == used[2] - 12 && crc[2] <= uint.MaxValue && crc[3] == 60, "m07_arp_frame_crc");
        Need(Row(9).SequenceEqual(new ulong[] { 0x7f, 2, 42, 1 }), "m07_arp_explicit_tick_controls");
        Need(Row(10).SequenceEqual(PrefixRow(parent, "M07_NET C", 4)), "m07_arp_allocator_restored");
        Need(waits[0] >= 1 && waits[0] <= 1000000 && waits[1] == 8 && waits[2] >= 1 &&
            waits[2] <= 1024 && waits[3] == 3, "m07_arp_bounded_release");
        Need(Row(12).SequenceEqual(PrefixRow(parent, "M07_NET B", 4)), "m07_arp_quiescent_cleanup");
        Need(Row(13).SequenceEqual(new ulong[] { 15, 0x100000000, 0x0a000202, 0x0a00020f }), "m07_arp_policy_controls");
        return new((uint)crc[0], (uint)crc[1], (uint)crc[2], cache[2]);
    }

    public static QemuArpCaptureReceipt AssertM07ArpReceiveCapture(string serialPath, string capturePath)
    {
        string[] lines = FirmwareProtocol(serialPath, "TEST_PASS m07.009\r\n", true, false, "m07_arp");
        return QemuEthernetCapture.AssertArp(capturePath, AssertM07ArpReceiveRecords(lines));
    }
}
