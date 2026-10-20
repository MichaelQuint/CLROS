using System.Globalization;
using System.Text;
using System.Text.Json;
using OS.Tools.QemuRunner;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Borrowed selection of the actual executable, firmware and
    /// loopback forward and actual owned invocation UTC bounds. Production must
    /// supply both bounds; values alone confer no process or guest authority.</summary>
    public readonly record struct ServiceRunnerContext(int ForwardPort, string Qemu, string Ovmf, DateTimeOffset StartedUtc, DateTimeOffset CompletedUtc);

    /// <summary>Value-only copied control JSON and checked process observations.
    /// JSON is only structurally bounded here; its detailed controls, packets,
    /// source/native identity and guest cleanup require independent joins.</summary>
    public readonly record struct ServiceRunnerInspection(string ControlsJson, string FramesJson, int ProcessId,
        DateTimeOffset StartUtc, DateTimeOffset EndUtc, long LifetimeMilliseconds);

    /// <summary>Check a complete ordinary diagnostic runner against its exact
    /// serial, summary and selected service inputs. Recompute streaming counts,
    /// require owned process termination after all terminal markers, and reject
    /// prefix-only web results, timeout, truncation and substituted summaries.
    /// This synchronous value-only inspection starts no process; native, source,
    /// parent, HTTP, packet and guest cleanup joins remain mandatory.</summary>
    public static ServiceRunnerInspection InspectNativeAotServiceRunnerForTests(QemuSmokeOptions options, QemuSmokePlan plan,
        ProcessResult runner, byte[] raw, byte[] summaryBytes, ServiceRunnerContext context)
    {
        RequireCompleteProcess(runner, "service_runner_incomplete");
        Need(runner.ExitCode == 0 && runner.StandardError.Length == 0 &&
            runner.Duration > TimeSpan.FromSeconds(300) && runner.Duration <= TimeSpan.FromSeconds(options.TimeoutSeconds + 60) &&
            runner.StandardOutput.Length <= 8 * 1024 * 1024 &&
            raw.Length > 0 && raw.Length <= SerialMarkerStateMachine.ContinuingServiceByteCapacity &&
            summaryBytes.Length > 3 && summaryBytes.Length <= SerialMarkerStateMachine.ContinuingServiceByteCapacity, "service_runner_inputs");
        Need(NativeAotServiceSelection.Application(plan.Flags, options.LinuxThreadingApplication) == NativeAotServicePackage.Application &&
            options.WorkloadProfile == "SingleMinimalApi" && options.FirmwareRngSource == "virtio" && options.TimeoutSeconds is >= 780 and <= 900 &&
            context.ForwardPort is >= 1 and <= 65535 && Path.IsPathFullyQualified(context.Qemu) && Path.IsPathFullyQualified(context.Ovmf) &&
            !Controls.IsMatch(context.Qemu) && !Controls.IsMatch(context.Ovmf) && plan.Required.Contains("TEST_PASS m16-012") &&
            context.StartedUtc != default && context.StartedUtc.Offset == TimeSpan.Zero && context.CompletedUtc.Offset == TimeSpan.Zero &&
            context.CompletedUtc > context.StartedUtc && context.CompletedUtc - context.StartedUtc >= runner.Duration - TimeSpan.FromMilliseconds(100), "service_runner_selection");
        var stream = new SerialMarkerStateMachine(plan.Required, plan.Forbidden, successTerminalMarker: "TEST_PASS m16-012",
            byteCapacity: SerialMarkerStateMachine.ContinuingServiceByteCapacity, orderedMarkers: plan.Ordered, uniqueMarkers: plan.Unique);
        Need(stream.Feed(raw).Succeeded, "service_runner_serial_feed");
        var done = stream.Complete();
        Need(done.Passed && done.SuccessTerminalCount == 1 && done.FailureTerminalCount == 0, "service_runner_serial_completion");
        string metrics = $"SERIAL_MARKER_STREAM status={done.Status} terminal={done.TerminalState} bytes={done.TotalBytes} lines={done.LinesProcessed} invalidBytes={done.InvalidByteCount} invalidLines={done.InvalidNoiseLineCount} orderedSeen={done.OrderedSeenCount} outOfOrder={done.OutOfOrderOccurrenceCount} uniqueSeen={done.UniqueSeenCount} duplicateUnique={done.DuplicateUniqueOccurrenceCount}";
        string Format(IReadOnlyList<string> values) => values.Count == 0 ? "none" : string.Join(';', values);
        string[] selected = ["IMAGE_ARTIFACT " + options.Image, "SERIAL_LOG " + options.SerialLog,
            "WORKLOAD_PROFILE SingleMinimalApi", "FIRMWARE_RNG_SOURCE virtio", "SOCKET_FORWARD_PORT " + context.ForwardPort.ToString(CultureInfo.InvariantCulture), "CONTINUING_SERVICE true",
            "MARKER_PRESETS " + Format(plan.Presets), "REQUIRED_MARKERS " + Format(plan.Required), "ORDERED_MARKERS " + Format(plan.Ordered),
            "UNIQUE_MARKERS " + Format(plan.Unique), "FORBIDDEN_MARKERS " + Format(plan.Forbidden)];
        Need(runner.StandardOutput.EndsWith("\r\n", StringComparison.Ordinal), "service_runner_stdout_end");
        string[] stdout = runner.StandardOutput[..^2].Split("\r\n", StringSplitOptions.None);
        AssertRunnerTerminated(stdout, "service");
        string[] prefixes = ["SERVICE_SOCKET_CONTROLS ", "SERVICE_FRAME_CONTROL ", "SERVICE_PROCESS_ID ", "SERVICE_PROCESS_START_UTC ",
            "SERVICE_PROCESS_END_UTC ", "SERVICE_PROCESS_LIFETIME_MS ", "SERVICE_ENDPOINT_PRELAUNCH_AVAILABLE ", "SERVICE_ENDPOINT_STOPPED_REFUSED ", "SERVICE_PROCESS_FAILURE "];
        string[] block = stdout.Where(row => row.StartsWith("SERVICE_", StringComparison.Ordinal)).ToArray();
        Need(block.Length == prefixes.Length && block.Select((row, i) => row.StartsWith(prefixes[i], StringComparison.Ordinal)).All(value => value), "service_runner_block");
        string Field(int i) => block[i][prefixes[i].Length..];
        string controls = Field(0), frames = Field(1);
        foreach (string json in new[] { controls, frames })
        {
            Need(json.Length is > 1 and <= 2 * 1024 * 1024 && Encoding.UTF8.GetByteCount(json) <= 2 * 1024 * 1024, "service_runner_control_extent");
            try
            {
                using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 16 });
                Need(document.RootElement.ValueKind == JsonValueKind.Object, "service_runner_control_object");
            }
            catch (JsonException) { throw new ToolFailure("service_runner_control_json"); }
        }
        Need(int.TryParse(Field(2), NumberStyles.None, CultureInfo.InvariantCulture, out int processId) && processId > 0 &&
            Field(2) == processId.ToString(CultureInfo.InvariantCulture), "service_runner_process_id");
        DateTimeOffset Utc(string text)
        {
            Need(DateTime.TryParseExact(text, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var value) &&
                value.Kind == DateTimeKind.Utc && text == value.ToString("O", CultureInfo.InvariantCulture), "service_runner_process_utc");
            return new(value);
        }
        var start = Utc(Field(3)); var end = Utc(Field(4));
        Need(long.TryParse(Field(5), NumberStyles.None, CultureInfo.InvariantCulture, out long lifetime) &&
            Field(5) == lifetime.ToString(CultureInfo.InvariantCulture) && lifetime > 300_000 && end - start > TimeSpan.FromSeconds(300) &&
            Math.Abs((end - start).TotalMilliseconds - lifetime) <= 100 && end - start <= runner.Duration + TimeSpan.FromMilliseconds(100) &&
            lifetime <= runner.Duration.TotalMilliseconds + 100 && start >= context.StartedUtc && end <= context.CompletedUtc, "service_runner_process_lifetime");
        Need(Field(6) == "true" && Field(7) == "true" && Field(8) == "none", "service_runner_process_cleanup");
        string disposition = stdout.Single(row => row.StartsWith("QEMU_PROCESS_RESULT ", StringComparison.Ordinal));
        string[] expectedRows = ["QEMU_DISCOVERY result=" + context.Qemu + " checked=" + context.Qemu,
            "OVMF_DISCOVERY result=" + context.Ovmf + " checked=" + context.Ovmf, "TEST_START qemu.smoke", selected[0], selected[1],
            "OVMF_CODE " + context.Ovmf, ..selected.Skip(2), metrics, "SERIAL_MARKER_DRAIN milliseconds=750", disposition,
            ..block, "SUMMARY_LOG " + options.SummaryLog, "TEST_PASS qemu.smoke"];
        string expected = string.Join("\r\n", expectedRows) + "\r\n";
        Need(runner.StandardOutput == expected, "service_runner_stdout");
        string[] serial = ReadThreadingSerialLines(DecodeUtf8(raw));
        List<string> summary = ["QEMU_SMOKE_STATUS pass", ..selected];
        summary.AddRange(serial.Where(row => IsThreadingObservedLine(row, plan)).Select(row => "OBSERVED_MARKER " + row));
        summary.AddRange(["APP_EXIT_RESULT none", "APP_EXIT_MALFORMED none", "MISSING_MARKERS none", "OUT_OF_ORDER_MARKERS none",
            "DUPLICATE_UNIQUE_MARKERS none", "FORBIDDEN_MARKER_HIT none", "FAIL_REASON none", "SERIAL_TAIL_LIMIT lines=8 chars=240"]);
        foreach (string row in serial.TakeLast(8)) summary.Add("SERIAL_TAIL_LINE " + (row.Length > 240 ? row[..237] + "..." : row));
        summary.AddRange(block);
        byte[] expectedSummary = [..Encoding.UTF8.GetPreamble(), ..Encoding.UTF8.GetBytes(string.Join("\r\n", summary) + "\r\n")];
        Need(summaryBytes.AsSpan().SequenceEqual(expectedSummary), "service_runner_summary_bytes");
        return new(controls, frames, processId, start, end, lifetime);
    }

    internal static AuthorityFiles HoldServiceEvidence(string root, QemuSmokeOptions options, NativeAotServicePeer.Completion peer)
    {
        AuthorityFiles files = new();
        try
        {
            Need(string.Equals(Path.GetFullPath(root), peer.Root, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) &&
                options.ServiceLifecycle == peer.Lifecycle &&
                string.Equals(Path.GetFullPath(options.SerialLog, root), peer.SerialPath, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) &&
                string.Equals(Path.GetFullPath(options.SerialLog + ".peer.log", root), peer.Path, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal),
                "service_peer_owner");
            var serial = files.Open(root, Path.GetFullPath(options.SerialLog, root), "service serial", "service_evidence_serial", SerialMarkerStateMachine.ContinuingServiceByteCapacity, singleLink: true);
            var summary = files.Open(root, Path.GetFullPath(options.SummaryLog, root), "service summary", "service_evidence_summary", SerialMarkerStateMachine.ContinuingServiceByteCapacity, singleLink: true);
            var packets = files.Open(root, Path.GetFullPath(options.SerialLog + ".pcap", root), "service PCAP", "service_evidence_packets", 16 * 1024 * 1024, singleLink: true);
            var report = files.Open(root, peer.Path, "service peer", "service_evidence_peer", 65536, singleLink: true);
            AuthorityFiles.RequireDistinct(serial, summary, packets, report); peer.Verify(files); files.VerifyUnchanged(); return files;
        }
        catch { files.Dispose(); throw; }
    }
}
