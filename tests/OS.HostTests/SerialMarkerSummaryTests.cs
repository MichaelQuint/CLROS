using System.Runtime.CompilerServices;
using OS.Tools.QemuRunner;

internal static class SerialMarkerSummaryTests
{
    [HostFixture(HostLane.Fast)]
    public static void Initialize()
    {
        int failures = 0;

        Run("qemu.serial summary passes required markers", TestPassesRequiredMarkers, ref failures);
        Run("qemu.serial summary reports missing markers", TestReportsMissingMarkers, ref failures);
        Run("qemu.serial summary requires marker line boundaries", TestRequiresMarkerLineBoundaries, ref failures);
        Run("qemu.serial summary reports forbidden markers", TestReportsForbiddenMarkers, ref failures);
        Run("qemu.serial summary preserves marker order", TestPreservesMarkerOrder, ref failures);
        Run("qemu.serial summary reports ordered marker failures", TestReportsOrderedMarkerFailures, ref failures);
        Run("qemu.serial summary preserves ACPI marker lines", TestPreservesAcpiMarkerLines, ref failures);
        Run("qemu.summary app exit marker parsed", TestParsesAppExitMarker, ref failures);
        Run("qemu.summary app exit absent reported", TestReportsAppExitAbsent, ref failures);
        Run("qemu.summary app exit malformed reported", TestReportsMalformedAppExitMarker, ref failures);
        Run("qemu.serial summary keeps required marker behavior unchanged", TestKeepsRequiredMarkerBehaviorUnchanged, ref failures);

        if (failures != 0)
        {
            Console.WriteLine("TEST_FAIL qemu.serial.summary.tests failures=" + failures);
            Environment.Exit(1);
        }
    }

    private static void Run(string name, Func<bool> test, ref int failures)
    {

        HostCheck.Run("" + name, test, ref failures);
    }

    private static bool TestPassesRequiredMarkers()
    {
        const string serial =
            "TEST_START efi.main\r\n"
            + "EFI_CONSOLE_READY\r\n"
            + "OS_SMOKE_PASS\r\n";

        SerialMarkerSummary summary = SerialMarkerSummary.Parse(
            serial,
            ["TEST_START efi.main", "OS_SMOKE_PASS"],
            ["OS_SMOKE_FAIL"]);

        return summary.Passed
            && summary.HasAllRequiredMarkers
            && !summary.HasForbiddenMarkers
            && summary.MissingMarkers.Count == 0
            && summary.ForbiddenMarkerHits.Count == 0;
    }

    private static bool TestReportsMissingMarkers()
    {
        const string serial =
            "TEST_START efi.main\n"
            + "OS_BOOT_OK\n";

        SerialMarkerSummary summary = SerialMarkerSummary.Parse(
            serial,
            ["OS_SMOKE_PASS", "UEFI_MEMORY_MAP_OWNED"],
            []);

        return !summary.Passed
            && !summary.HasAllRequiredMarkers
            && summary.MissingMarkers.Count == 2
            && summary.MissingMarkers[0] == "OS_SMOKE_PASS"
            && summary.MissingMarkers[1] == "UEFI_MEMORY_MAP_OWNED"
            && summary.ForbiddenMarkerHits.Count == 0;
    }

    private static bool TestRequiresMarkerLineBoundaries()
    {
        const string serial =
            "ACPI_MCFG_DISCOVERY_READY_BOGUS\n"
            + "BOOT_MEMORY_REGION_COUNT 102\n"
            + "OS_SMOKE_PASS_EXTRA\n";

        SerialMarkerSummary summary = SerialMarkerSummary.Parse(
            serial,
            ["ACPI_MCFG_DISCOVERY_READY", "BOOT_MEMORY_REGION_COUNT"],
            ["OS_SMOKE_PASS"]);

        return !summary.Passed
            && !summary.HasAllRequiredMarkers
            && summary.MissingMarkers.Count == 1
            && summary.MissingMarkers[0] == "ACPI_MCFG_DISCOVERY_READY"
            && summary.ForbiddenMarkerHits.Count == 0;
    }

    private static bool TestReportsForbiddenMarkers()
    {
        const string serial =
            "TEST_START efi.main\n"
            + "UEFI_EBS_FRESH_MAP_PROBE_START\n"
            + "OS_SMOKE_PASS\n";

        SerialMarkerSummary summary = SerialMarkerSummary.Parse(
            serial,
            ["OS_SMOKE_PASS"],
            ["UEFI_EBS_FRESH_MAP_PROBE_START", "UEFI_EBS_CALL_SKIPPED"]);

        return !summary.Passed
            && summary.HasAllRequiredMarkers
            && summary.HasForbiddenMarkers
            && summary.MissingMarkers.Count == 0
            && summary.ForbiddenMarkerHits.Count == 1
            && summary.ForbiddenMarkerHits[0] == "UEFI_EBS_FRESH_MAP_PROBE_START";
    }

    private static bool TestPreservesMarkerOrder()
    {
        const string serial =
            "firmware noise\n"
            + "  UEFI_MEMORY_MAP_READY  \n"
            + "BOOT_HANDOFF_READY\n"
            + "INFO not a marker\n"
            + "TEST_PASS boot.sequence\n"
            + "OS_SMOKE_PASS\n";

        SerialMarkerSummary summary = SerialMarkerSummary.Parse(
            serial,
            ["OS_SMOKE_PASS"],
            []);

        return summary.ObservedMarkerLines.Count == 4
            && summary.ObservedMarkerLines[0] == "UEFI_MEMORY_MAP_READY"
            && summary.ObservedMarkerLines[1] == "BOOT_HANDOFF_READY"
            && summary.ObservedMarkerLines[2] == "TEST_PASS boot.sequence"
            && summary.ObservedMarkerLines[3] == "OS_SMOKE_PASS";
    }

    private static bool TestReportsOrderedMarkerFailures()
    {
        const string serial =
            "ACPI_RSDP_SNAPSHOT_READY\n"
            + "ACPI_ROOT_SDT_SNAPSHOT_READY\n"
            + "ACPI_RSDP_VALIDATED\n"
            + "ACPI_ROOT_SDT_VALIDATED\n"
            + "ACPI_MCFG_DISCOVERY_READY\n"
            + "OS_SMOKE_PASS\n";

        SerialMarkerSummary summary = SerialMarkerSummary.Parse(
            serial,
            [
                "ACPI_RSDP_SNAPSHOT_READY",
                "ACPI_RSDP_VALIDATED",
                "ACPI_ROOT_SDT_SNAPSHOT_READY",
                "ACPI_ROOT_SDT_VALIDATED",
                "ACPI_MCFG_DISCOVERY_READY",
                "OS_SMOKE_PASS"
            ],
            [],
            [
                "ACPI_RSDP_SNAPSHOT_READY",
                "ACPI_RSDP_VALIDATED",
                "ACPI_ROOT_SDT_SNAPSHOT_READY",
                "ACPI_ROOT_SDT_VALIDATED",
                "ACPI_MCFG_DISCOVERY_READY"
            ]);

        return !summary.Passed
            && !summary.HasAllRequiredMarkers
            && summary.MissingMarkers.Count == 0
            && summary.OutOfOrderMarkers.Count == 3
            && summary.OutOfOrderMarkers[0] == "ACPI_ROOT_SDT_SNAPSHOT_READY"
            && summary.OutOfOrderMarkers[1] == "ACPI_ROOT_SDT_VALIDATED"
            && summary.OutOfOrderMarkers[2] == "ACPI_MCFG_DISCOVERY_READY";
    }

    private static bool TestPreservesAcpiMarkerLines()
    {
        const string serial =
            "ACPI_RSDP_SNAPSHOT_READY\n"
            + "ACPI_RSDP_VALIDATED\n"
            + "ACPI_ROOT_SDT_SNAPSHOT_READY\n"
            + "ACPI_ROOT_SDT_VALIDATED\n"
            + "ACPI_MCFG_DISCOVERY_READY\n"
            + "OS_SMOKE_PASS\n";

        SerialMarkerSummary summary = SerialMarkerSummary.Parse(
            serial,
            ["OS_SMOKE_PASS", "ACPI_MCFG_DISCOVERY_READY"],
            []);

        return summary.Passed
            && summary.ObservedMarkerLines.Count == 6
            && summary.ObservedMarkerLines[0] == "ACPI_RSDP_SNAPSHOT_READY"
            && summary.ObservedMarkerLines[1] == "ACPI_RSDP_VALIDATED"
            && summary.ObservedMarkerLines[2] == "ACPI_ROOT_SDT_SNAPSHOT_READY"
            && summary.ObservedMarkerLines[3] == "ACPI_ROOT_SDT_VALIDATED"
            && summary.ObservedMarkerLines[4] == "ACPI_MCFG_DISCOVERY_READY"
            && summary.ObservedMarkerLines[5] == "OS_SMOKE_PASS";
    }

    private static bool TestParsesAppExitMarker()
    {
        const string serial =
            "TEST_START fake.qemu\n"
            + "APP_EXIT code=0 status=OSHOST_STATUS_OK\n"
            + "OS_SMOKE_PASS\n";

        SerialMarkerSummary summary = SerialMarkerSummary.Parse(
            serial,
            ["OS_SMOKE_PASS"],
            []);

        return summary.Passed
            && summary.ObservedMarkerLines.Count == 3
            && summary.ObservedMarkerLines[0] == "TEST_START fake.qemu"
            && summary.ObservedMarkerLines[1] == "APP_EXIT code=0 status=OSHOST_STATUS_OK"
            && summary.ObservedMarkerLines[2] == "OS_SMOKE_PASS"
            && summary.AppExitResults.Count == 1
            && summary.AppExitResults[0].ExitCode == 0
            && summary.AppExitResults[0].Status == "OSHOST_STATUS_OK"
            && summary.MalformedAppExitMarkers.Count == 0;
    }

    private static bool TestReportsAppExitAbsent()
    {
        const string serial =
            "TEST_START fake.qemu\n"
            + "OS_SMOKE_PASS\n";

        SerialMarkerSummary summary = SerialMarkerSummary.Parse(
            serial,
            ["OS_SMOKE_PASS"],
            []);

        return summary.Passed
            && summary.AppExitResults.Count == 0
            && summary.MalformedAppExitMarkers.Count == 0;
    }

    private static bool TestReportsMalformedAppExitMarker()
    {
        const string serial =
            "APP_EXIT status=OSHOST_STATUS_OK code=0\n"
            + "APP_EXIT code=abc status=OSHOST_STATUS_OK\n"
            + "APP_EXIT code=0 status=bad\n"
            + "OS_SMOKE_PASS\n";

        SerialMarkerSummary summary = SerialMarkerSummary.Parse(
            serial,
            ["OS_SMOKE_PASS"],
            []);

        return summary.Passed
            && summary.AppExitResults.Count == 0
            && summary.MalformedAppExitMarkers.Count == 3
            && summary.MalformedAppExitMarkers[0] == "reason=invalid_format line=APP_EXIT status=OSHOST_STATUS_OK code=0"
            && summary.MalformedAppExitMarkers[1] == "reason=invalid_code line=APP_EXIT code=abc status=OSHOST_STATUS_OK"
            && summary.MalformedAppExitMarkers[2] == "reason=invalid_status line=APP_EXIT code=0 status=bad";
    }

    private static bool TestKeepsRequiredMarkerBehaviorUnchanged()
    {
        const string serial =
            "APP_EXIT malformed\n"
            + "OS_SMOKE_PASS\n";

        SerialMarkerSummary summary = SerialMarkerSummary.Parse(
            serial,
            ["OS_SMOKE_PASS"],
            []);

        return summary.Passed
            && summary.HasAllRequiredMarkers
            && summary.MissingMarkers.Count == 0
            && !summary.HasForbiddenMarkers
            && summary.MalformedAppExitMarkers.Count == 1;
    }
}
