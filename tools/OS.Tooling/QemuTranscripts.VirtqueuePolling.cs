namespace OS.Tooling;

public static partial class QemuTranscripts
{
    public static void AssertM05VirtqueuePollingRecords(IReadOnlyList<string> lines)
    {
        Need(lines.Count == QemuContracts.M05VirtqueuePollingMarkers.Count, "m05_queue_record_count");
        var rows = new List<ulong[]>();
        for (int i = QemuContracts.M05VirtioNegotiationMarkers.Count; i < lines.Count; i++)
        {
            string expected = QemuContracts.M05VirtqueuePollingMarkers[i];
            if (expected.StartsWith("M05_QUEUE ", StringComparison.Ordinal))
                rows.Add(ReadHexRecord(lines[i], expected, 4));
            else
                Need(lines[i] == expected, "m05_queue_marker_order");
        }
        Need(rows[0][0] == 1 && rows[0][1] >= 8 && rows[0][1] <= 32768 &&
            (rows[0][1] & (rows[0][1] - 1)) == 0 && rows[0][2] == 8 && rows[0][3] == 2, "m05_queue_size");
        Need(rows[1].SequenceEqual(new ulong[] { 0, 1, 8, 11 }), "m05_queue_descriptor_controls");
        ulong[] owner = ReadHexRecord(lines[67], "M03_OWNED 0", 8);
        ulong page = rows[2][0];
        Need(page >= owner[2] && page % 4096 == 0 && page < 0x100000000 &&
            (decimal)page + 4096 <= (decimal)owner[2] + owner[3] * 4096 &&
            rows[2].SequenceEqual(new ulong[] { page, page, page, 4096 }), "m05_queue_owned_identity_dma");
        ulong[] regions = ReadHexRecord(lines.Single(s => s.StartsWith("M05_VIRTIO 1 ", StringComparison.Ordinal)), "M05_VIRTIO 1", 4);
        ulong[] terminal = ReadHexRecord(lines.Single(s => s.StartsWith("M05_VIRTIO 9 ", StringComparison.Ordinal)), "M05_VIRTIO 9", 4);
        ulong offset = rows[3][1], multiplier = rows[3][2];
        Need(offset <= 65535 && multiplier <= uint.MaxValue &&
            (multiplier == 0 || multiplier >= 2 && (multiplier & (multiplier - 1)) == 0) &&
            offset * multiplier <= 4094 && rows[3][0] == regions[1] + offset * multiplier && rows[3][3] == 72,
            "m05_queue_notify_extent");
        Need(rows[4].SequenceEqual(new ulong[] { 1, 1, 0, 0 }), "m05_queue_matching_completion");
        Need(rows[5][0] >= 1 && rows[5][0] <= 1000000 && rows[5][1] == 5 && rows[5][2] == 1 && rows[5][3] == 1,
            "m05_queue_bounded_poll_reset_release");
        Need(rows[6].SequenceEqual(new ulong[] { owner[4], owner[4], owner[6], 1 }), "m05_queue_allocator_restored");
        Need(rows[7].SequenceEqual(new ulong[] { 5, 1, 1, terminal[1] }), "m05_queue_fence_and_command");
    }
}
