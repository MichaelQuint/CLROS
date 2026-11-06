namespace OS.Tooling;

public static partial class QemuTranscripts
{
    public static void AssertM06VirtioBlockQueueSetupRecords(IReadOnlyList<string> lines)
    {
        int start = QemuContracts.M05PciEnumerationMarkers.Count;
        Need(lines.Count == QemuContracts.M06VirtioBlockQueueSetupMarkers.Count, "m06_block_record_count");
        for (int i = start; i < lines.Count; i++)
        {
            string expected = QemuContracts.M06VirtioBlockQueueSetupMarkers[i];
            if (!expected.StartsWith("M06_BLK ", StringComparison.Ordinal))
                Need(lines[i] == expected, "m06_block_marker_order");
        }
        ulong[] Row(int offset, string stage) => ReadHexRecord(lines[start + offset], "M06_BLK " + stage, 4);
        ulong[] binding = Row(1, "0"), quiescence = Row(2, "1"), mapping = Row(3, "2");
        Need(binding[0] == 0x600 && binding[1] == 0x10421af4 && binding[2] >= 0x100000 &&
            (binding[2] & 0x3fff) == 0 && binding[2] < 0x0000800000000000UL - 0x4000 &&
            binding[3] <= ushort.MaxValue && (binding[3] & 2) != 0, "m06_block_binding");
        Need(quiescence[0] == ((binding[3] & 0xfffb) | 0x400) && (quiescence[0] & 0x406) == 0x402 &&
            quiescence[1] <= byte.MaxValue && quiescence[2] == 0 && quiescence[3] <= ushort.MaxValue &&
            (quiescence[3] & 0xb800) == 0, "m06_block_quiescence");
        Need(mapping.SequenceEqual(new ulong[] { 0x4000, 4, 56, 8 }), "m06_block_mapping");
        ulong[] features = Row(7, "3"), controls = Row(8, "4");
        Need(features[1] == 0x100000020 && (features[0] & features[1]) == features[1] &&
            features[2] == 11 && features[3] == 1, "m06_block_features");
        Need(controls.SequenceEqual(new ulong[] { 3, 5, 6, 0 }), "m06_block_feature_controls");
        ulong[] dma = Row(12, "5"), rings = Row(13, "6"), request = Row(14, "7");
        ulong[] owner = ReadHexRecord(lines.Single(s => s.StartsWith("M03_OWNED 0 ", StringComparison.Ordinal)), "M03_OWNED 0", 8);
        Need(dma[0] >= 0x100000 && dma[0] <= 0xfffff000 && (dma[0] & 4095) == 0 &&
            dma[0] == dma[1] && dma[1] == dma[2] && dma[3] == 4096 &&
            dma[0] >= owner[2] && (decimal)dma[0] + 4096 <= (decimal)owner[2] + owner[3] * 4096 &&
            (dma[0] >= binding[2] + 0x4000 || dma[0] + 4096 <= binding[2]), "m06_block_dma_domains");
        Need(rings.SequenceEqual(new ulong[] { 0, 128, 148, 8 }) &&
            request.SequenceEqual(new ulong[] { 256, 272, 512, 512 }), "m06_block_dma_layout");
        ulong[] queue = Row(18, "8"), addresses = Row(19, "9"), cleanup = Row(20, "A"), allocator = Row(21, "B"), final = Row(22, "C");
        Need(queue[0] == 0 && queue[1] is >= 8 and <= 32768 && (queue[1] & (queue[1] - 1)) == 0 &&
            queue[2] == 8 && queue[3] == 1, "m06_block_queue");
        Need(addresses.SequenceEqual(new ulong[] { dma[2], dma[2] + 128, dma[2] + 148, 0 }), "m06_block_queue_addresses");
        Need(cleanup.SequenceEqual(new ulong[] { 1, 1, 1, 2 }), "m06_block_cleanup");
        Need(allocator.SequenceEqual(new ulong[] { owner[4], owner[4], owner[6], 1 }), "m06_block_allocator_restore");
        Need(final.SequenceEqual(new ulong[] { quiescence[0], 0, quiescence[3], 1 }), "m06_block_final_state");
    }
}
