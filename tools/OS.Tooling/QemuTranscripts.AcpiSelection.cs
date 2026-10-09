namespace OS.Tooling;

public static partial class QemuTranscripts
{
    public static void AssertM05MonotonicClockRecords(IReadOnlyList<string> lines)
    {
        int start = QemuContracts.M03OwnedSubsystemMarkers.Count;
        Need(lines.Count == QemuContracts.M05MonotonicClockMarkers.Count &&
            lines[start] == "TEST_START m05.003" && lines[^2] == "CLROS_M05_003_READY" &&
            lines[^1] == "TEST_PASS m05.003", "m05_clock_record_count");
        ulong[] hpet = ReadHexRecord(lines[start + 1], "M05_CLOCK 0", 4);
        ulong period = hpet[1] >> 32;
        Need(hpet[0] is >= 0x100000 and <= 0xfffff000 && hpet[0] % 4096 == 0 &&
            period is >= 1 and <= 100000000 && hpet[2] == period &&
            hpet[3] == ((hpet[1] & (1UL << 13)) != 0 ? 64UL : 32UL), "m05_clock_hpet_metadata");
        ulong[] source = ReadHexRecord(lines[start + 2], "M05_CLOCK 1", 4);
        Need(source[0] == 1 && source[1] == 1000000000000000UL / period &&
            source[2] <= 1 && source[3] == 1, "m05_clock_source");
        ulong[] ticks = ReadHexRecord(lines[start + 3], "M05_CLOCK 2", 4);
        Need(ticks[0] > 0 && ticks[1] > ticks[0] && ticks[2] <= 1 &&
            (ticks[2] == 0 ? ticks[3] == 0 : ticks[3] is >= 1000000 and <= 10000000000UL), "m05_clock_progress");
        ulong[] controls = ReadHexRecord(lines[start + 4], "M05_CLOCK 3", 4);
        Need(controls[0] == 2 && controls[1] == 5 && controls[2] == 1 && controls[3] == 1,
            "m05_clock_rejection_controls");
    }

    public static void AssertM05OwnedAcpiSelectionRecords(IReadOnlyList<string> lines)
    {
        int start = QemuContracts.M03OwnedSubsystemMarkers.Count;
        Need(lines.Count == QemuContracts.M05OwnedAcpiSelectionMarkers.Count &&
            lines[start] == "TEST_START m05.002" && lines[^2] == "CLROS_M05_002_READY" &&
            lines[^1] == "TEST_PASS m05.002", "m05_acpi_record_count");
        for (int role = 0; role < 3; role++)
        {
            ulong[] selected = ReadHexRecord(lines[start + 1 + role], "M05_ACPI " + role, 4);
            ulong[] original = ReadHexRecord(lines[70 + role], "M03_OWNED " + (3 + role), 8);
            Need(selected[0] == original[2] && selected[1] == original[3] &&
                selected[2] == original[4] && selected[3] == original[6], "m05_acpi_original_selection");
        }
        var retained = ReadRecord(lines[24], "CLROS_M03_002_RETAIN", "total:dec", "descriptors:dec", "guards:dec", "tail:dec", "mismatches:dec");
        ulong[] rejected = ReadHexRecord(lines[start + 4], "M05_ACPI 3", 4);
        Need(rejected[0] == 3 && rejected[1] == retained["total"] - 1 &&
            rejected[2] == retained["total"] && rejected[3] == 1, "m05_acpi_rejection_control");
    }
}
