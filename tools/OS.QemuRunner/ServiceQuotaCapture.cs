namespace OS.Tools.QemuRunner;

/// <summary>Value-only shape check for the explicit quota capture trailer.
/// Borrows bounded serial text synchronously and returns a refusal reason or
/// null. It grants no syscall, physical-denial, process, cleanup or guest
/// authority; complete independent original evidence joins remain required.</summary>
public static class ServiceQuotaCapture
{
    public const string Terminal = "CLROS_M16_016_QUOTA_CLEANUP";
    public const string ProcessFailure = "M16_016_QUOTA_PROCESS_FAILURE exit=99 cause=physical-backing";
    public const string EarlyBootSmokePass = "OS_SMOKE_PASS";
    public static IReadOnlyList<string> ForbiddenSuccessMarkers { get; } = Array.AsReadOnly<string>([
        "OS_SMOKE_PASS", "TEST_PASS m07-026", "TEST_PASS m16.014", "TEST_PASS m16-009", "TEST_PASS m16-011", "TEST_PASS m16-012", "TEST_PASS m10.004",
        "CLROS_M07_026_READY", "CLROS_M16_014_READY", "CLROS_M16_009_READY", "CLROS_M16_011_READY", "CLROS_M16_012_READY", "CLROS_M10_004_READY"]);

    /// <summary>Require one exact physical process-failure row before one exact
    /// final CRLF-terminated cleanup marker, with no following bytes or ordinary
    /// leaf/OS success. One exact inherited boot smoke pass may precede one exact
    /// service-start gate. Prefixless trailers need neither; complete boot-prefix
    /// authority stays owned by the marker plan and full parent consumer.</summary>
    public static string? Failure(string? serial)
    {
        if (serial is null || serial.Length > SerialMarkerStateMachine.ContinuingServiceByteCapacity)
            return "quota_serial_input";
        ReadOnlySpan<char> remaining = serial.AsSpan(); bool terminal = false, bootPass = false, serviceStart = false; int failures = 0;
        while (!remaining.IsEmpty)
        {
            if (terminal) return "bytes_after_quota_terminal";
            int end = remaining.IndexOf('\n');
            if (end < 0) return "quota_terminal_incomplete";
            bool crlf = end != 0 && remaining[end - 1] == '\r';
            ReadOnlySpan<char> line = remaining[..(crlf ? end - 1 : end)]; remaining = remaining[(end + 1)..];
            if (line.StartsWith("TEST_START m16-011", StringComparison.Ordinal))
            {
                if (!crlf || !line.SequenceEqual("TEST_START m16-011") || serviceStart) return "quota_service_start_shape";
                serviceStart = true;
            }
            if (line.StartsWith(EarlyBootSmokePass, StringComparison.Ordinal))
            {
                if (!crlf || !line.SequenceEqual(EarlyBootSmokePass) || bootPass || serviceStart) return "quota_boot_success_shape";
                bootPass = true; continue;
            }
            foreach (string marker in ForbiddenSuccessMarkers)
                if (line.SequenceEqual(marker) || line.StartsWith(marker + " ", StringComparison.Ordinal))
                    return "ordinary_success_in_quota_capture";
            if (line.StartsWith(Terminal, StringComparison.Ordinal))
            {
                if (!crlf || !line.SequenceEqual(Terminal)) return "quota_terminal_shape";
                terminal = true;
            }
            if (line.StartsWith("M16_016_QUOTA_PROCESS_FAILURE", StringComparison.Ordinal))
            {
                if (!crlf || !line.SequenceEqual(ProcessFailure) || ++failures != 1) return "quota_process_failure_shape";
            }
        }
        return bootPass && !serviceStart ? "quota_boot_success_without_service_start" :
            !terminal ? "quota_terminal_missing" : failures != 1 ? "quota_process_failure_missing" : null;
    }
}
