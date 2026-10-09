using System.Globalization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    public static void AssertExpectedPanicRunner(string root, QemuSmokeOptions options, QemuSmokePlan plan,
        ProcessResult runner, IReadOnlyList<string> original, bool ownedSubsystem)
    {
        Need(runner.ExitCode == 19 && runner.StandardError.Length == 0 && original.Count == 18 && plan.Presets.Count == 0 && original.All(l => !Controls.IsMatch(l)), "qemu_panic_runner_inventory");
        Match q = Regex.Match(original[0], @"\AQEMU_DISCOVERY result=(.+) checked=(.+)\z"), o = Regex.Match(original[1], @"\AOVMF_DISCOVERY result=(.+) checked=(.+)\z");
        Need(q.Success && o.Success && q.Groups[1].Value != "not_found" && o.Groups[1].Value != "not_found", "qemu_panic_discovery");
        string[] lines = original.Skip(2).ToArray();
        string[] required = (ownedSubsystem ? plan.Required : new[] { "OS_BOOT_OK", "TEST_PASS boot.sequence", "OS_SMOKE_PASS" }.Concat(plan.Required)).Distinct(StringComparer.Ordinal).ToArray();
        string[] forbidden = new[] { "OS_SMOKE_FAIL", "TEST_FAIL" }.Concat(plan.Forbidden).Distinct(StringComparer.Ordinal).ToArray();
        string profile = ownedSubsystem ? "m03.owned-subsystem" : "m02.unexpected-vector", reason = ownedSubsystem ? "expected_owned_subsystem_panic" : "expected_unexpected_vector_panic";
        string Format(IReadOnlyList<string> values) => values.Count == 0 ? "none" : string.Join(';', values);
        string[] expected = ["TEST_START qemu.smoke", "IMAGE_ARTIFACT " + options.Image, "SERIAL_LOG " + options.SerialLog, "OVMF_CODE " + o.Groups[1].Value,
            "WORKLOAD_PROFILE " + options.WorkloadProfile, "MARKER_PRESETS none", "REQUIRED_MARKERS " + string.Join(';', required),
            "ORDERED_MARKERS " + Format(plan.Ordered), "UNIQUE_MARKERS " + Format(plan.Unique), "FORBIDDEN_MARKERS " + string.Join(';', forbidden),
            "EXPECTED_PANIC_PROFILE " + profile, lines[11], lines[12], lines[13], "SUMMARY_LOG " + options.SummaryLog,
            "QEMU_EXPECTED_PANIC outcome=expected-panic reason=" + reason];
        Need(lines.SequenceEqual(expected, StringComparer.Ordinal), "qemu_panic_runner_schema");
        Match native = Regex.Match(lines[13], @"\AQEMU_PROCESS_RESULT disposition=runner-terminated exitCode=(0|-?[1-9][0-9]*)\z");
        Need(native.Success && int.TryParse(native.Groups[1].Value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _), "qemu_panic_native_exit");
        Match drain = Regex.Match(lines[12], @"\ASERIAL_MARKER_DRAIN milliseconds=750 observedMilliseconds=(0|[1-9][0-9]*)\z");
        Need(drain.Success && long.TryParse(drain.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out _), "qemu_panic_quiet_shape");
        long quiet = long.Parse(drain.Groups[1].Value, CultureInfo.InvariantCulture);
        Need(quiet >= 750, "qemu_panic_quiet");
        byte[] raw = File.ReadAllBytes(Path.GetFullPath(options.SerialLog, root));
        string serial = DecodeUtf8(raw);
        int invalid = ownedSubsystem ? raw.Count(b => b is not (9 or 10 or 13) && (b < 32 || b > 126)) : 0;
        Need(lines[11] == $"SERIAL_MARKER_STREAM status=ExpectedPanic terminal=ExpectedPanic bytes={raw.Length} lines={(ownedSubsystem ? 79 : QemuContracts.M02UnexpectedCaptureRecordCount)} invalidBytes={invalid} invalidLines={(ownedSubsystem ? 1 : 0)} orderedSeen={plan.Ordered.Count} outOfOrder=0 uniqueSeen={plan.Unique.Count} duplicateUnique=0", "qemu_panic_stream_accounting");
        byte[] summaryBytes = File.ReadAllBytes(Path.GetFullPath(options.SummaryLog, root));
        string summary = DecodeUtf8(summaryBytes);
        Need(summaryBytes.Length <= 1048576 && !Regex.IsMatch(summary, @"[^\x20-\x7e\r\n]|(?<!\r)\n|\r(?!\n)"), "qemu_panic_summary_delimiter");
        List<string> wanted = ["QEMU_SMOKE_STATUS expected-panic"];
        foreach (int i in new[] { 1, 2, 4, 5, 6, 7, 8, 9 }) wanted.Add(expected[i]);
        string[] allMarkers = [.. required, .. plan.Ordered, .. plan.Unique, .. forbidden];
        string[] serialLines = serial.Split("\r\n", StringSplitOptions.None);
        foreach (string line in serialLines)
        {
            bool observed = Regex.IsMatch(line, @"\A(?:TEST_|BOOT_|OS_|UEFI_|ACPI_)") || line == "APP_EXIT" || line.StartsWith("APP_EXIT ", StringComparison.Ordinal) ||
                allMarkers.Any(marker => line == marker || line.StartsWith(marker + " ", StringComparison.Ordinal));
            if (observed) wanted.Add("OBSERVED_MARKER " + line);
        }
        wanted.AddRange(["APP_EXIT_RESULT none", "APP_EXIT_MALFORMED none", "MISSING_MARKERS none", "OUT_OF_ORDER_MARKERS none", "DUPLICATE_UNIQUE_MARKERS none", "FORBIDDEN_MARKER_HIT none", "FAIL_REASON " + reason,
            "EXPECTED_PANIC_DRAIN milliseconds=750 observedMilliseconds=" + quiet, lines[13], "EXPECTED_PANIC_CAPTURE bytes=" + raw.Length + " sha256=" + Convert.ToHexString(SHA256.HashData(raw)), "SERIAL_TAIL_LIMIT lines=8 chars=240"]);
        foreach (string line in serialLines.Where(l => l.Length > 0).TakeLast(8))
        { string value = line.Trim(); wanted.Add("SERIAL_TAIL_LINE " + (value.Length > 240 ? value[..237] + "..." : value)); }
        Need(summary == string.Join("\r\n", wanted) + "\r\n", "qemu_panic_summary_exact");
    }
}
