namespace OS.Tooling;

public static partial class QemuTranscripts
{
    public static void AssertM05VirtioNegotiationRecords(IReadOnlyList<string> lines)
    {
        Need(lines.Count == QemuContracts.M05VirtioNegotiationMarkers.Count, "m05_virtio_record_count");
        var rows = new List<ulong[]>();
        for (int i = QemuContracts.M05PciEnumerationMarkers.Count; i < lines.Count; i++)
        {
            string expected = QemuContracts.M05VirtioNegotiationMarkers[i];
            if (expected.StartsWith("M05_VIRTIO ", StringComparison.Ordinal)) rows.Add(ReadHexRecord(lines[i], expected, 4));
            else Need(lines[i] == expected, "m05_virtio_marker_order");
        }
        ulong start = rows[0][2];
        Need(rows[0][0] == 0x600 && rows[0][1] == 0x10411af4 && rows[0][3] == 4 &&
            start >= 0x100000 && start % 0x4000 == 0 && start < 0x800000000000 - 0x4000, "m05_virtio_fixture");
        Need(rows[1].SequenceEqual(new ulong[] { start, start + 0x3000, start + 0x1000, start + 0x2000 }), "m05_virtio_capability_locations");
        ulong command = (rows[2][0] & 0xfffb) | 0x400;
        Need(rows[2][0] <= 65535 && (rows[2][0] & 2) != 0 && rows[2][1] == command &&
            rows[2][2] == 0 && rows[2][3] == 0x4000, "m05_virtio_quiescent_bar");
        uint[] needed = [56, 2, 1, 1];
        for (int i = 0; i < 4; i++)
            Need(rows[i + 3].SequenceEqual(new ulong[] { rows[1][i], rows[1][i], needed[i], 1 }), "m05_virtio_installed_region");
        Need((rows[7][0] & 0x100000000) != 0 && rows[7][1] == 0x100000000 && rows[7][2] == 11 && rows[7][3] == 1,
            "m05_virtio_version1_features_ok");
        // The negative statuses are local-input controls, not device rejection.
        Need(rows[8].SequenceEqual(new ulong[] { 2, 3, 5, 6 }), "m05_virtio_rejection_controls");
        Need(rows[9].SequenceEqual(new ulong[] { 0, command, 1, 0 }), "m05_virtio_terminal_reset");
    }
}
