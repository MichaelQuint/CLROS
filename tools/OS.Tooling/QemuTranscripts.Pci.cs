namespace OS.Tooling;

public static partial class QemuTranscripts
{
    public static void AssertM05PciEnumerationRecords(IReadOnlyList<string> lines)
    {
        int start = QemuContracts.M03OwnedSubsystemMarkers.Count;
        Need(lines.Count == QemuContracts.M05PciEnumerationMarkers.Count, "m05_pci_record_count");
        for (int i = start; i < lines.Count; i++)
        {
            string expected = QemuContracts.M05PciEnumerationMarkers[i];
            if (!expected.StartsWith("M05_PCI ", StringComparison.Ordinal)) Need(lines[i] == expected, "m05_pci_marker_order");
        }
        ulong[] window = ReadHexRecord(lines[start + 1], "M05_PCI 0", 4);
        Need(window[0] >= 0x100000 && (window[0] & 0xfffff) == 0 && window[1] >= window[0] &&
            window[1] <= 0xfff00000 && (window[1] & 0xfffff) == 0 && window[1] - window[0] <= 0xff00000 &&
            window[2] == 0x100000 && window[3] <= 65535, "m05_pci_window");
        ulong bus = (window[1] - window[0]) >> 20;
        ulong[] rejected = ReadHexRecord(lines[start + 2], "M05_PCI 1", 4);
        Need(rejected.SequenceEqual(new ulong[] { 6, 4, 3, 1 }), "m05_pci_access_controls");
        ulong[] header = ReadHexRecord(lines[start + 6], "M05_PCI 2", 4);
        Need(header[0] == window[1] && header[1] <= uint.MaxValue && (header[1] & 65535) != 65535 &&
            header[2] <= 255 && (header[2] & 127) <= 1 && header[3] <= uint.MaxValue, "m05_pci_header");
        ulong[] mapping = ReadHexRecord(lines[start + 7], "M05_PCI 3", 4);
        Need(mapping.SequenceEqual(new ulong[] { 1, 1, 1, 4096 }), "m05_pci_mapping_controls");
        ulong[] scan = ReadHexRecord(lines[start + 11], "M05_PCI 4", 4);
        Need(scan[0] is >= 2 and <= 16 && scan[1] is >= 32 and <= 256 && scan[1] >= scan[0] &&
            (scan[1] - 32) % 7 == 0 && scan[2] >> 24 == window[3] && ((scan[2] >> 16) & 255) == bus &&
            ((scan[2] >> 8) & 255) == 31 && (scan[2] & 255) is 0 or 7 && scan[3] <= 1, "m05_pci_scan");
        ulong[] repeat = ReadHexRecord(lines[start + 12], "M05_PCI 5", 4);
        Need(repeat[0] == scan[0] && repeat[1] == scan[1] && repeat[2] == 1 && repeat[3] <= 1 &&
            (repeat[3] == 0 || scan[3] == 0), "m05_pci_repeat");
        ulong[] partial = ReadHexRecord(lines[start + 13], "M05_PCI 6", 4);
        Need(partial.SequenceEqual(new ulong[] { 10, 1, 2, 0 }), "m05_pci_truncation");
        ulong[] capabilities = ReadHexRecord(lines[start + 17], "M05_PCI 7", 4);
        Need(capabilities[0] <= uint.MaxValue && (capabilities[0] & 65535) != 65535 &&
            capabilities[1] >> 24 == window[3] && ((capabilities[1] >> 16) & 255) == bus &&
            ((capabilities[1] >> 8) & 255) <= 31 && (capabilities[1] & 255) <= 7 &&
            capabilities[2] is >= 1 and <= 48 && capabilities[3] == 2, "m05_pci_capabilities");
        ulong[] malformed = ReadHexRecord(lines[start + 18], "M05_PCI 8", 4);
        Need(malformed.SequenceEqual(new ulong[] { 9, 8, 7, 1 }), "m05_pci_capability_controls");
    }
}
