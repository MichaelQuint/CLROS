namespace OS.Tooling;

public static partial class QemuTranscripts
{
    public static string[] NormalizeM07VirtioNetRecords(IReadOnlyList<string> lines)
    {
        Need(lines.Count == QemuContracts.M07VirtioNetMarkers.Count, "m07_net_record_count");
        return lines.Take(QemuContracts.M05VirtqueuePollingMarkers.Count).ToArray();
    }

    public static uint AssertM07VirtioNetRecords(IReadOnlyList<string> lines)
    {
        Need(lines.Count == QemuContracts.M07VirtioNetMarkers.Count, "m07_net_record_count");
        int parentCount = QemuContracts.M05VirtqueuePollingMarkers.Count;
        for (int i = parentCount; i < lines.Count; i++)
        {
            string expected = QemuContracts.M07VirtioNetMarkers[i];
            if (expected.StartsWith("M07_NET ", StringComparison.Ordinal))
                _ = ReadHexRecord(lines[i], expected, 4);
            else
                Need(lines[i] == expected, "m07_net_marker_order");
        }
        string[] parent = NormalizeM07VirtioNetRecords(lines);
        AssertM05VirtqueuePollingRecords(parent);
        AssertM05VirtioNegotiationRecords(parent.Take(QemuContracts.M05VirtioNegotiationMarkers.Count).ToArray());
        ulong[] Row(int stage) => PrefixRow(lines, "M07_NET " + stage.ToString("X", System.Globalization.CultureInfo.InvariantCulture), 4);
        ulong[] binding = Row(0), features = Row(1), rx = Row(2), tx = Row(3), dma = Row(4);
        ulong[] original = PrefixRow(parent, "M05_VIRTIO 0", 4), terminal = PrefixRow(parent, "M05_VIRTIO 9", 4);
        ulong[] offered = PrefixRow(parent, "M05_VIRTIO 7", 4), regions = PrefixRow(parent, "M05_VIRTIO 1", 4);
        Need(binding.SequenceEqual(new[] { 0x10411af4UL, 0x600UL, terminal[1], original[2] }), "m07_net_binding");
        Need(features.SequenceEqual(new[] { offered[0], 0x100000000UL, 11UL, 1UL }), "m07_net_features");
        Need(rx[0] == 0 && tx[0] == 1 && rx[2] == 8 && tx[2] == 8 &&
            new[] { rx[1], tx[1] }.All(n => n >= 8 && n <= 32768 && (n & (n - 1)) == 0) &&
            rx[3] <= ushort.MaxValue && tx[3] <= ushort.MaxValue, "m07_net_queue_sizes");
        ulong[] oldDma = PrefixRow(parent, "M05_QUEUE 2", 4);
        ulong page = dma[0];
        Need(dma.SequenceEqual(new[] { page, page, page, 4096UL }) && page == oldDma[0] &&
            (page + 4096 <= binding[3] || binding[3] + 0x4000 <= page), "m07_net_owned_disjoint_dma");
        Need(Row(5).SequenceEqual(new[] { page, page + 128, page + 148, page + 256 }) &&
            Row(6).SequenceEqual(new[] { page + 2048, page + 2176, page + 2196, page + 2304 }), "m07_net_ring_layout");
        Need(Row(7).SequenceEqual(new ulong[] { 1526, 2, 1, 1 }), "m07_net_receive_priming");
        ulong[] notify = Row(8), oldNotify = PrefixRow(parent, "M05_QUEUE 3", 4);
        ulong multiplier = notify[2];
        Need(multiplier == oldNotify[2] && rx[3] * multiplier <= 4094 && tx[3] * multiplier <= 4094 &&
            notify[0] == regions[1] + rx[3] * multiplier && notify[1] == regions[1] + tx[3] * multiplier &&
            notify[3] == 72, "m07_net_notifications");
        Need(Row(9).SequenceEqual(new ulong[] { 1, 0, 0, 1 }), "m07_net_used_receipt");
        ulong[] waits = Row(10);
        Need(waits[0] >= 1 && waits[0] <= 1000000 && waits[1] == 7 &&
            waits[2] >= 1 && waits[2] <= 1024 && waits[3] == 3, "m07_net_bounded_release");
        Need(Row(11).SequenceEqual(new[] { 1UL, 1UL, 1UL, terminal[1] }), "m07_net_quiescent_cleanup");
        Need(Row(12).SequenceEqual(PrefixRow(parent, "M05_QUEUE 6", 4)), "m07_net_allocator_restored");
        Need(Row(13).SequenceEqual(new ulong[] { 0xd24c49dd, 60, 0, 12 }), "m07_net_fixed_frame_crc");
        return 0xd24c49dd;
    }

    public static QemuEthernetCaptureReceipt AssertM07VirtioNetCapture(string serialPath, string capturePath)
    {
        string[] lines = FirmwareProtocol(serialPath, "TEST_PASS m07.005\r\n", true, false, "m07_net");
        return QemuEthernetCapture.Assert(capturePath, AssertM07VirtioNetRecords(lines));
    }
}
