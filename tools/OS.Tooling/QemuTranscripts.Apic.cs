namespace OS.Tooling;

public static partial class QemuTranscripts
{
    public static void AssertM05ApicSetupRecords(IReadOnlyList<string> lines)
    {
        Need(lines.Count == QemuContracts.M05ApicSetupMarkers.Count, "m05_apic_record_count");
        var rows = new List<ulong[]>();
        for (int i = QemuContracts.M05PciEnumerationMarkers.Count; i < lines.Count; i++)
        {
            string expected = QemuContracts.M05ApicSetupMarkers[i];
            if (expected.StartsWith("M05_APIC ", StringComparison.Ordinal)) rows.Add(ReadHexRecord(lines[i], expected, 4));
            else Need(lines[i] == expected, "m05_apic_marker_order");
        }
        ulong local = rows[0][0], io = rows[0][1], id = rows[0][2], flags = rows[0][3];
        Need(local is >= 0x100000 and <= 0xfffff000 && io is >= 0x100000 and <= 0xfffff000 &&
            local % 4096 == 0 && io % 4096 == 0 && local != io && id == 0 && flags <= 1, "m05_apic_topology");
        ulong version = rows[1][2], ioVersion = rows[1][3];
        ulong maxLvt = (version >> 16) & 255, maxIo = (ioVersion >> 16) & 255;
        Need(rows[1][0] == (local | 0x900) && rows[1][1] <= uint.MaxValue && (rows[1][1] & 0x220) == 0x220 &&
            version <= uint.MaxValue && (version & 255) is >= 0x10 and <= 0x15 && maxLvt is >= 3 and <= 6 &&
            (version & 0xfe00ff00) == 0 && ioVersion <= uint.MaxValue && (ioVersion & 0xff00ff00) == 0 &&
            (ioVersion & 255) is 0x11 or 0x20 && maxIo <= 23,
            "m05_apic_cpu_controller_identity");
        ulong gsi = rows[3][3];
        Need(rows[2][0] == maxLvt + 1 && rows[2][1] == maxIo + 1 && rows[2][2] == flags &&
            gsi <= uint.MaxValue - maxIo && (rows[2][3] == 0 || rows[2][3] >= gsi && rows[2][3] <= gsi + maxIo),
            "m05_apic_masked_sources");
        Need(rows[3][0] == 0xd0 && rows[3][1] == 0x1ff && rows[3][2] == 2, "m05_apic_priority_if_disabled");
    }
}
