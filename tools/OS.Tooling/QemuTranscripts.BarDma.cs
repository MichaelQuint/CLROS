namespace OS.Tooling;

public static partial class QemuTranscripts
{
    public static void AssertM05BarDmaRecords(IReadOnlyList<string> lines)
    {
        Need(lines.Count == QemuContracts.M05BarDmaMarkers.Count, "m05_bar_dma_record_count");
        int start = QemuContracts.M05PciEnumerationMarkers.Count;
        var rows = new List<ulong[]>();
        for (int i = start; i < lines.Count; i++)
        {
            string expected = QemuContracts.M05BarDmaMarkers[i];
            if (expected.StartsWith("M05_BAR_DMA ", StringComparison.Ordinal))
                rows.Add(ReadHexRecord(lines[i], expected, 4));
            else
                Need(lines[i] == expected, "m05_bar_dma_marker_order");
        }
        ulong[] window = ReadHexRecord(lines[77], "M05_PCI 0", 4), owner = ReadHexRecord(lines[67], "M03_OWNED 0", 8);
        Need(window[0] == window[1] && window[2] == 0x100000 && window[3] == 0, "m05_bar_dma_bus");
        ulong[] device = rows[0], narrow = rows[1], wide = rows[2], restored = rows[3];
        Need(device[0] == 0x600 && device[1] == 0x00051b36 && device[2] <= 65535 &&
            device[3] == window[1] + 0x30000, "m05_bar_dma_test_device");
        Need(narrow[0] != 0 && narrow[0] % 4096 == 0 && narrow[0] <= 0xfffff000 &&
            narrow[1] == 4096 && narrow[2] == 2 && narrow[3] == 0, "m05_bar_dma_memory32");
        Need(wide[0] != 0 && wide[0] % 0x200000 == 0 && wide[0] <= ulong.MaxValue - 0x200000 &&
            wide[1] == 0x200000 && wide[2] == 3 && wide[3] == 1 &&
            ((decimal)narrow[0] + narrow[1] <= wide[0] || (decimal)wide[0] + wide[1] <= narrow[0]), "m05_bar_dma_memory64");
        ulong ownedCommand = device[2] & 0xfffb;
        Need(restored.SequenceEqual(new ulong[] { ownedCommand, ownedCommand, 1, 1 }), "m05_bar_dma_restoration");
        Need(rows[4].SequenceEqual(new ulong[] { 0, 1, 2, 3 }) && rows[5].SequenceEqual(new ulong[] { 7, 7, 8, 9 }), "m05_bar_dma_type_controls");
        ulong[] mapping = rows[6], allocation = rows[9], final = rows[12];
        Need(mapping[0] >= owner[2] && mapping[0] % 4096 == 0 && mapping[0] < 0x100000000 &&
            (decimal)mapping[0] + 4096 <= (decimal)owner[2] + owner[3] * 4096 &&
            mapping[1] == mapping[0] && mapping[2] == mapping[0] && mapping[3] == 4096, "m05_bar_dma_identity_adapter");
        Need(rows[7].SequenceEqual(new ulong[] { 0x101000, 0x201000, 0x301000, 4096 }) &&
            rows[8].SequenceEqual(new ulong[] { 0, 7, 4, 0 }), "m05_bar_dma_address_controls");
        Need(allocation[0] == mapping[0] && allocation[1] == owner[4] && allocation[1] > 0 &&
            allocation[2] == allocation[1] - 1 && allocation[3] == 1, "m05_bar_dma_backing_allocation");
        Need(rows[10].SequenceEqual(new ulong[] { 1, 1, 13, 1 }) && rows[11].SequenceEqual(new ulong[] { 12, 12, 9, 1 }), "m05_bar_dma_lifetime_controls");
        Need(final.SequenceEqual(new ulong[] { allocation[1], allocation[1], owner[6], 1 }), "m05_bar_dma_allocator_restored");
    }
}
