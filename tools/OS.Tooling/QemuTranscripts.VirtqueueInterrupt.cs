namespace OS.Tooling;

public static partial class QemuTranscripts
{
    public static void AssertM05VirtqueueInterruptRecords(IReadOnlyList<string> lines)
    {
        Need(lines.Count == QemuContracts.M05VirtqueueInterruptMarkers.Count, "m05_irq_record_count");
        int pci = QemuContracts.M05PciEnumerationMarkers.Count, apic = QemuContracts.M05ApicSetupMarkers.Count;
        int negotiation = QemuContracts.M05VirtioNegotiationMarkers.Count - pci;
        int queue = QemuContracts.M05VirtqueuePollingMarkers.Count - QemuContracts.M05VirtioNegotiationMarkers.Count;
        AssertM05ApicSetupRecords(lines.Take(apic).ToArray());
        string[] virtio = [.. lines.Take(pci), .. lines.Skip(apic).Take(negotiation)];
        AssertM05VirtioNegotiationRecords(virtio);
        Need(lines[apic + negotiation] == "TEST_START m05.021", "m05_irq_start_order");
        // Reuse the accepted queue/DMA record checks; the explicit IRQ records
        // below establish the additional wait/handler/EOI/IDTR requirements.
        AssertM05VirtqueuePollingRecords([.. virtio, .. lines.Skip(apic + negotiation + 1).Take(queue)]);
        var rows = new List<ulong[]>();
        for (int i = apic + negotiation + 1 + queue; i < lines.Count; i++)
        {
            string expected = QemuContracts.M05VirtqueueInterruptMarkers[i];
            if (expected.StartsWith("M05_IRQ ", StringComparison.Ordinal)) rows.Add(ReadHexRecord(lines[i], expected, 4));
            else Need(lines[i] == expected, "m05_irq_marker_order");
        }
        ulong table = rows[0][0], pba = rows[0][1], entries = rows[0][2], offset = rows[0][3];
        Need(entries is >= 1 and <= 2048 && offset is >= 0x40 and <= 0xf4 && offset % 4 == 0 &&
            table >= 0x100000 && pba >= 0x100000 && table % 8 == 0 && pba % 8 == 0 &&
            table < 0x800000000000 && pba < 0x800000000000 && (table & ~4095UL) == (pba & ~4095UL) &&
            (table & 4095) + entries * 16 <= 4096 && (pba & 4095) + (entries + 63) / 64 * 8 <= 4096 &&
            !((decimal)table < (decimal)pba + (entries + 63) / 64 * 8 && (decimal)pba < (decimal)table + entries * 16),
            "m05_irq_msix_extents");
        ulong[] topology = ReadHexRecord(lines.Single(s => s.StartsWith("M05_APIC 0 ", StringComparison.Ordinal)), "M05_APIC 0", 4);
        Need(rows[1].SequenceEqual(new[] { 0xfee00000UL | (topology[2] << 12), 0xe0UL, 0xc000UL | (entries - 1), 0xffffUL }),
            "m05_irq_physical_edge_message");
        ulong code = rows[2][0], original = rows[2][1], temporary = rows[2][2];
        Need(code >= 4096 && code <= 0x800000000000 - 192 && code % 16 == 0 &&
            original >= 4096 && original <= 0x800000000000 - 4096 && original % 4096 == 0 &&
            temporary >= 4096 && temporary <= 0x800000000000 - 4096 && temporary % 16 == 0 &&
            !(original < temporary + 4096 && temporary < original + 4096) &&
            !(code < temporary + 4096 && temporary < code + 192) &&
            rows[2][3] == topology[0] + 0xb0, "m05_irq_owned_context_addresses");
        Need(rows[3].SequenceEqual(new ulong[] { 1, 0, 1, 0 }), "m05_irq_single_handler_eoi");
        Need(rows[4][0] >= code + 54 && rows[4][0] <= code + 70 && (rows[4][1] & 0x202) == 0x202 &&
            (rows[4][2] & 0x202) == 2 && (rows[4][3] & 0x202) == 2, "m05_irq_bounded_if_window");
        Need(rows[5][0] is >= 1 and <= 1000000 && rows[5][1] == 1000000 && rows[5][2] == 1 && rows[5][3] == 5,
            "m05_irq_wait_then_acquire");
        Need(rows[6].SequenceEqual(new[] { 4095UL, original, 4095UL, original }), "m05_irq_idtr_restored");
        Need((rows[7][0] & ~0x4000UL) == entries - 1 && rows[7][1] == 1 && rows[7][2] == 1 && rows[7][3] == 1,
            "m05_irq_disabled_masked_cleanup");
    }
}
