namespace OS.Tooling;

public static partial class QemuTranscripts
{
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
