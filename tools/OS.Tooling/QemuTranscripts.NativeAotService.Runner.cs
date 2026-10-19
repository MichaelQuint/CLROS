using System.Globalization;
using System.Text;
using OS.Tools.QemuRunner;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Borrowed selection of the actual executable, firmware and
    /// loopback forward. Values alone confer no process or guest authority.</summary>
    public readonly record struct ServiceRunnerContext(int ForwardPort, string Qemu, string Ovmf);

    /// <summary>Check a complete ordinary diagnostic runner against its exact
    /// serial, summary and selected service inputs. Recompute streaming counts,
    /// require owned process termination after all terminal markers, and reject
    /// prefix-only web results, timeout, truncation and substituted summaries.
    /// This synchronous value-only inspection starts no process; native, source,
    /// parent, HTTP, packet and guest cleanup joins remain mandatory.</summary>
    public static void InspectNativeAotServiceRunnerForTests(QemuSmokeOptions options, QemuSmokePlan plan,
        ProcessResult runner, byte[] raw, byte[] summaryBytes, ServiceRunnerContext context)
    {
        RequireCompleteProcess(runner, "service_runner_incomplete");
        Need(runner.ExitCode == 0 && runner.StandardError.Length == 0 &&
            runner.Duration > TimeSpan.FromSeconds(300) && runner.Duration <= TimeSpan.FromSeconds(options.TimeoutSeconds + 60) &&
            raw.Length > 0 && raw.Length <= SerialMarkerStateMachine.DefaultByteCapacity &&
            summaryBytes.Length is > 3 and <= 16 * 1024 * 1024, "service_runner_inputs");
        Need(NativeAotServiceSelection.Application(plan.Flags, options.LinuxThreadingApplication) == NativeAotServicePackage.Application &&
            options.WorkloadProfile == "SingleMinimalApi" && options.FirmwareRngSource == "virtio" && options.TimeoutSeconds is >= 780 and <= 900 &&
            context.ForwardPort is >= 1 and <= 65535 && Path.IsPathFullyQualified(context.Qemu) && Path.IsPathFullyQualified(context.Ovmf) &&
            !Controls.IsMatch(context.Qemu) && !Controls.IsMatch(context.Ovmf), "service_runner_selection");
        var stream = new SerialMarkerStateMachine(plan.Required, plan.Forbidden, orderedMarkers: plan.Ordered, uniqueMarkers: plan.Unique);
        Need(stream.Feed(raw).Succeeded, "service_runner_serial_feed");
        var done = stream.Complete();
        Need(done.Passed && done.SuccessTerminalCount == 1 && done.FailureTerminalCount == 0, "service_runner_serial_completion");
        string metrics = $"SERIAL_MARKER_STREAM status={done.Status} terminal={done.TerminalState} bytes={done.TotalBytes} lines={done.LinesProcessed} invalidBytes={done.InvalidByteCount} invalidLines={done.InvalidNoiseLineCount} orderedSeen={done.OrderedSeenCount} outOfOrder={done.OutOfOrderOccurrenceCount} uniqueSeen={done.UniqueSeenCount} duplicateUnique={done.DuplicateUniqueOccurrenceCount}";
        string Format(IReadOnlyList<string> values) => values.Count == 0 ? "none" : string.Join(';', values);
        string[] selected = ["IMAGE_ARTIFACT " + options.Image, "SERIAL_LOG " + options.SerialLog,
            "WORKLOAD_PROFILE SingleMinimalApi", "FIRMWARE_RNG_SOURCE virtio", "SOCKET_FORWARD_PORT " + context.ForwardPort.ToString(CultureInfo.InvariantCulture),
            "MARKER_PRESETS " + Format(plan.Presets), "REQUIRED_MARKERS " + Format(plan.Required), "ORDERED_MARKERS " + Format(plan.Ordered),
            "UNIQUE_MARKERS " + Format(plan.Unique), "FORBIDDEN_MARKERS " + Format(plan.Forbidden)];
        Need(runner.StandardOutput.EndsWith("\r\n", StringComparison.Ordinal), "service_runner_stdout_end");
        string[] stdout = runner.StandardOutput[..^2].Split("\r\n", StringSplitOptions.None);
        AssertRunnerTerminated(stdout, "service");
        string disposition = stdout.Single(row => row.StartsWith("QEMU_PROCESS_RESULT ", StringComparison.Ordinal));
        string[] expectedRows = ["QEMU_DISCOVERY result=" + context.Qemu + " checked=" + context.Qemu,
            "OVMF_DISCOVERY result=" + context.Ovmf + " checked=" + context.Ovmf, "TEST_START qemu.smoke", selected[0], selected[1],
            "OVMF_CODE " + context.Ovmf, ..selected.Skip(2), metrics, "SERIAL_MARKER_DRAIN milliseconds=750", disposition,
            "SUMMARY_LOG " + options.SummaryLog, "TEST_PASS qemu.smoke"];
        string expected = string.Join("\r\n", expectedRows) + "\r\n";
        Need(runner.StandardOutput == expected, "service_runner_stdout");
        string[] serial = ReadThreadingSerialLines(DecodeUtf8(raw));
        List<string> summary = ["QEMU_SMOKE_STATUS pass", ..selected];
        summary.AddRange(serial.Where(row => IsThreadingObservedLine(row, plan)).Select(row => "OBSERVED_MARKER " + row));
        summary.AddRange(["APP_EXIT_RESULT none", "APP_EXIT_MALFORMED none", "MISSING_MARKERS none", "OUT_OF_ORDER_MARKERS none",
            "DUPLICATE_UNIQUE_MARKERS none", "FORBIDDEN_MARKER_HIT none", "FAIL_REASON none", "SERIAL_TAIL_LIMIT lines=8 chars=240"]);
        foreach (string row in serial.TakeLast(8)) summary.Add("SERIAL_TAIL_LINE " + (row.Length > 240 ? row[..237] + "..." : row));
        byte[] expectedSummary = [..Encoding.UTF8.GetPreamble(), ..Encoding.UTF8.GetBytes(string.Join("\r\n", summary) + "\r\n")];
        Need(summaryBytes.AsSpan().SequenceEqual(expectedSummary), "service_runner_summary_bytes");
    }

    internal static AuthorityFiles HoldServiceEvidence(string root, QemuSmokeOptions options, NativeAotServicePeer.Completion peer)
    {
        AuthorityFiles files = new();
        try
        {
            Need(string.Equals(Path.GetFullPath(root), peer.Root, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) &&
                string.Equals(Path.GetFullPath(options.SerialLog, root), peer.SerialPath, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) &&
                string.Equals(Path.GetFullPath(options.SerialLog + ".peer.log", root), peer.Path, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal),
                "service_peer_owner");
            var serial = files.Open(root, Path.GetFullPath(options.SerialLog, root), "service serial", "service_evidence_serial", 16 * 1024 * 1024, singleLink: true);
            var summary = files.Open(root, Path.GetFullPath(options.SummaryLog, root), "service summary", "service_evidence_summary", 16 * 1024 * 1024, singleLink: true);
            var packets = files.Open(root, Path.GetFullPath(options.SerialLog + ".pcap", root), "service PCAP", "service_evidence_packets", 16 * 1024 * 1024, singleLink: true);
            var report = files.Open(root, peer.Path, "service peer", "service_evidence_peer", 65536, singleLink: true);
            AuthorityFiles.RequireDistinct(serial, summary, packets, report); peer.Verify(files); files.VerifyUnchanged(); return files;
        }
        catch { files.Dispose(); throw; }
    }
}
