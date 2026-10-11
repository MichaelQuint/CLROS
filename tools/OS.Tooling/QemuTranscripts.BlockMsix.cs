namespace OS.Tooling;

public static partial class QemuTranscripts
{
    public static string[] NormalizeM06BlockMsixRecords(IReadOnlyList<string> lines)
    {
        Need(lines.Count == QemuContracts.M06BlockMsixMarkers.Count, "m06_block_msix_record_count");
        int pci = QemuContracts.M05PciEnumerationMarkers.Count;
        int apic = QemuContracts.M05ApicSetupMarkers.Count;
        return [.. lines.Take(pci), .. lines.Skip(apic).Take(lines.Count - apic - 12)];
    }

    public static void AssertM06BlockMsixRecords(IReadOnlyList<string> lines, bool pollingRecovery = false)
    {
        var expected = QemuContracts.M06BlockMsixMarkers;
        Need(lines.Count == expected.Count, "m06_block_msix_record_count");
        for (int i = 0; i < lines.Count; i++)
        {
            string marker = expected[i];
            bool fields = i is 3 or 9 or 10 or >= 19 and <= 24 ||
                marker.StartsWith("M03_OWNED ", StringComparison.Ordinal) ||
                marker.StartsWith("M05_PCI ", StringComparison.Ordinal) ||
                marker.StartsWith("M05_APIC ", StringComparison.Ordinal) ||
                marker.StartsWith("M06_", StringComparison.Ordinal);
            Need(fields ? lines[i].StartsWith(marker + " ", StringComparison.Ordinal) : lines[i] == marker,
                "m06_block_msix_marker_order");
        }
        AssertM05ApicSetupRecords(lines.Take(QemuContracts.M05ApicSetupMarkers.Count).ToArray());
        string[] parent = NormalizeM06BlockMsixRecords(lines);
        AssertM06PackageReadRecords(parent);
        ulong[] Row(int stage) => PrefixRow(lines, "M06_IRQ " + stage, 4);
        ulong[] route = Row(0), request = Row(1), message = Row(2), handler = Row(3), flags = Row(4);
        ulong[] idtr = Row(5), wait = Row(6), cleanup = Row(7), completion = Row(8);
        ulong[] geometry = PrefixRow(parent, "M06_READ 0", 4), original = PrefixRow(parent, "M06_BLK 1", 4);
        Need(route.SequenceEqual(new[] { pollingRecovery ? 3UL : 2UL, original[3], 6UL, geometry[0] }) &&
            route[1] <= ushort.MaxValue && (route[1] & 0xb800) == 0 &&
            route[3] > 1 && route[3] <= ulong.MaxValue / 512, "m06_block_msix_route_capacity");
        Need(request.SequenceEqual(new ulong[] { 1, 512, 0, 513 }), "m06_block_msix_request_completion");
        ulong[] apic = PrefixRow(lines, "M05_APIC 0", 4);
        Need(message[0] == (0xfee00000UL | (apic[2] << 12)) && message[1] == 0xe0,
            "m06_block_msix_physical_edge_message");
        ulong table = message[2], pba = message[3], entries = (route[1] & 0x7ff) + 1;
        Need(table >= 0x100000 && pba >= 0x100000 && table < 0x800000000000 && pba < 0x800000000000 &&
            table % 8 == 0 && pba % 8 == 0 && (table & ~4095UL) == (pba & ~4095UL) &&
            (table & 4095) + entries * 16 <= 4096 && (pba & 4095) + (entries + 63) / 64 * 8 <= 4096 &&
            !(table < pba + (entries + 63) / 64 * 8 && pba < table + entries * 16), "m06_block_msix_extents");
        ulong[] dma = PrefixRow(parent, "M06_BLK 5", 4), binding = PrefixRow(parent, "M06_BLK 0", 4);
        ulong page = table & ~4095UL;
        Need(!((decimal)page < (decimal)dma[0] + dma[3] && dma[0] < page + 4096) &&
            !((decimal)page < (decimal)binding[2] + 0x4000 && binding[2] < page + 4096),
            "m06_block_msix_mapping_domains");
        Need(handler.SequenceEqual(pollingRecovery ? new ulong[] { 0, 0, 0, 0 } : new ulong[] { 1, 0, 1, 0 }),
            "m06_block_msix_handler_eoi");
        Need((flags[2] & 0x202) == 2 && (flags[3] & 0x202) == 2 &&
            (pollingRecovery ? flags[0] == 0 && flags[1] == 0 : flags[0] >= 4096 &&
                flags[0] < 0x800000000000 && (flags[1] & 0x202) == 0x202), "m06_block_msix_if_window");
        Need(idtr[0] == 4095 && idtr[2] == 4095 && idtr[1] >= 4096 &&
            idtr[1] <= 0x800000000000 - 4096 && idtr[1] % 4096 == 0 && idtr[3] == idtr[1],
            "m06_block_msix_idtr_restored");
        Need(wait[1] == 1000000 && wait[0] is >= 1 and <= 1000000 &&
            (pollingRecovery ? wait[0] == wait[1] && wait[2] is >= 1 and <= 1000000 : wait[2] == 1) &&
            wait[3] == 4, "m06_block_msix_wait_then_acquire");
        Need(cleanup.SequenceEqual(new ulong[] { 1, 1, 1, 1 }), "m06_block_msix_cleanup");
        ulong[] payload = PrefixRow(parent, "M06_READ 5", 4);
        Need(completion[0] <= uint.MaxValue && completion[0] == payload[0] && completion[1] == 1 &&
            completion[2] == 0 && completion[3] is >= 1 and <= 1024, "m06_block_msix_payload_reset");
    }

    public static void AssertM06BlockMsixImage(string serialPath, string imagePath, bool pollingRecovery = false)
    {
        string[] lines = FirmwareProtocol(serialPath, "TEST_PASS m06.019\r\n", true, false, "m06_block_msix", true);
        AssertM06BlockMsixRecords(lines, pollingRecovery);
        AssertM06PackageReadImage(NormalizeM06BlockMsixRecords(lines), imagePath, "Intact");
        AssertM06VirtioBlockReadImage(serialPath, imagePath);
    }
}
