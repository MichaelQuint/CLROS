using System.Globalization;
using System.Text;
using OS.Tools.QemuRunner;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Copied capture observations only. Quiet/lifetime use milliseconds;
    /// terminal extent uses bytes and frame/PCAP observations use counts. No file,
    /// process, packet, peer, syscall, native or guest authority is constructed.</summary>
    public readonly record struct ServiceQuotaRunnerInspection(string FramesJson, int ProcessId, DateTimeOffset StartUtc,
        DateTimeOffset EndUtc, long LifetimeMilliseconds, long TerminalBytes, long QuietMilliseconds,
        long ForwardedFrames, QemuEthernetCaptureReceipt Pcap)
    {
        public string Outcome => "captured";
        public string Acceptance => "pending";
    }

    /// <summary>Synchronously inspect borrowed original serial, summary and PCAP
    /// bytes beside supplied owned-invocation bounds. Require complete runner22,
    /// exact quota terminal, recomputed streaming metrics, byte extent, measured
    /// quiet>=750ms, terminated QEMU, stopped/refused endpoint and stopped healthy
    /// frame owner with no diagnostic injection/drop/VLAN. Reuse classic-PCAP
    /// framing and the borrowed parent anchor. Backend reinjection writes and
    /// bidirectional PCAP records have different semantics and are not equated.
    /// Production must later supply held original snapshots and an owned runner;
    /// these values alone establish no execution or capture ownership. Full
    /// source/native/parent/leaf/peer/HTTP/TCP/syscall joins remain mandatory.
    /// Invalid, incomplete or substituted values throw; nothing is run/modified.</summary>
    public static ServiceQuotaRunnerInspection InspectNativeAotServiceQuotaRunnerForTests(QemuSmokeOptions options, QemuSmokePlan plan,
        ProcessResult runner, byte[] raw, byte[] summaryBytes, ServiceRunnerContext context, byte[] pcap, uint frameAnchor)
    {
        RequireCompleteProcess(runner, "service_quota_runner_incomplete");
        Need(runner.ExitCode == 22 && runner.StandardError.Length == 0 && runner.Duration > TimeSpan.Zero &&
            runner.Duration <= TimeSpan.FromSeconds(options.TimeoutSeconds + 60) && runner.StandardOutput.Length <= 8 * 1024 * 1024 &&
            raw.Length is > 0 && raw.Length <= SerialMarkerStateMachine.ContinuingServiceByteCapacity &&
            summaryBytes.Length is > 3 && summaryBytes.Length <= SerialMarkerStateMachine.ContinuingServiceByteCapacity,
            "service_quota_runner_inputs");
        Need(options.ServiceQuotaFailure && !options.ServiceLifecycle && !options.SignalEpollRace && !options.DryRun &&
            NativeAotServiceSelection.Application(plan.Flags, options.LinuxThreadingApplication) == NativeAotServicePackage.Application &&
            plan.BootPolicy == "Diagnostic" && (options.BootPolicy.Length == 0 || options.BootPolicy.Equals("Diagnostic", StringComparison.OrdinalIgnoreCase)) &&
            plan.ReplaceDefaultRequiredMarkers && plan.Presets.Count == 0 && options.WorkloadProfile == "SingleMinimalApi" &&
            options.DiskFormat == "raw" && options.LinuxElfImageVariant == "Intact" && options.FirmwareRngSource == "virtio" &&
            options.TimeoutSeconds is >= 780 and <= 900 && context.ForwardPort is >= 1 and <= 65535 &&
            Path.IsPathFullyQualified(context.Qemu) && Path.IsPathFullyQualified(context.Ovmf) &&
            !Controls.IsMatch(context.Qemu) && !Controls.IsMatch(context.Ovmf) &&
            new[] { options.Image, options.SerialLog, options.SummaryLog }.All(path => path.Length > 0 && !Controls.IsMatch(path)) &&
            context.StartedUtc != default && context.StartedUtc.Offset == TimeSpan.Zero && context.CompletedUtc.Offset == TimeSpan.Zero &&
            context.CompletedUtc > context.StartedUtc && context.CompletedUtc - context.StartedUtc >= runner.Duration - TimeSpan.FromMilliseconds(100),
            "service_quota_runner_selection");
        string[] gates = [ServiceQuotaCapture.EarlyBootSmokePass, "TEST_START m16-011", ServiceQuotaCapture.ProcessFailure, ServiceQuotaCapture.Terminal];
        Need(gates.All(marker => plan.Required.Contains(marker) && plan.Ordered.Contains(marker) && plan.Unique.Contains(marker)) &&
            plan.Required.Contains("OS_BOOT_OK") && plan.Ordered.LastOrDefault() == ServiceQuotaCapture.Terminal &&
            plan.Forbidden.Contains("OS_SMOKE_FAIL") && plan.Forbidden.Contains("TEST_FAIL") &&
            ServiceQuotaCapture.ForbiddenSuccessMarkers.Where(marker => marker != ServiceQuotaCapture.EarlyBootSmokePass).All(plan.Forbidden.Contains),
            "service_quota_runner_markers");
        string serialText = DecodeUtf8(raw);
        Need(ServiceQuotaCapture.Failure(serialText) is null, "service_quota_runner_terminal");
        var stream = new SerialMarkerStateMachine(plan.Required, plan.Forbidden, successTerminalMarker: ServiceQuotaCapture.Terminal,
            byteCapacity: SerialMarkerStateMachine.ContinuingServiceByteCapacity, orderedMarkers: plan.Ordered, uniqueMarkers: plan.Unique);
        Need(stream.Feed(raw).Succeeded, "service_quota_runner_serial_feed"); var done = stream.Complete();
        Need(done.Passed && done.TotalBytes == raw.Length && done.SuccessTerminalCount == 1 && done.FailureTerminalCount == 0,
            "service_quota_runner_serial_completion");
        string metrics = $"SERIAL_MARKER_STREAM status={done.Status} terminal={done.TerminalState} bytes={done.TotalBytes} lines={done.LinesProcessed} invalidBytes={done.InvalidByteCount} invalidLines={done.InvalidNoiseLineCount} orderedSeen={done.OrderedSeenCount} outOfOrder={done.OutOfOrderOccurrenceCount} uniqueSeen={done.UniqueSeenCount} duplicateUnique={done.DuplicateUniqueOccurrenceCount}";
        string Format(IReadOnlyList<string> values) => values.Count == 0 ? "none" : string.Join(';', values);
        string[] selected = ["IMAGE_ARTIFACT " + options.Image, "SERIAL_LOG " + options.SerialLog, "WORKLOAD_PROFILE SingleMinimalApi",
            "FIRMWARE_RNG_SOURCE virtio", "SOCKET_FORWARD_PORT " + context.ForwardPort.ToString(CultureInfo.InvariantCulture),
            "CONTINUING_SERVICE true", "SERVICE_QUOTA_FAILURE true", "MARKER_PRESETS none", "REQUIRED_MARKERS " + Format(plan.Required),
            "ORDERED_MARKERS " + Format(plan.Ordered), "UNIQUE_MARKERS " + Format(plan.Unique), "FORBIDDEN_MARKERS " + Format(plan.Forbidden)];
        Need(runner.StandardOutput.EndsWith("\r\n", StringComparison.Ordinal), "service_quota_runner_stdout_end");
        string[] stdout = runner.StandardOutput[..^2].Split("\r\n", StringSplitOptions.None); AssertRunnerTerminated(stdout, "service_quota");
        string[] prefixes = ["SERVICE_SOCKET_CONTROLS ", "SERVICE_FRAME_CONTROL ", "SERVICE_PROCESS_ID ", "SERVICE_PROCESS_START_UTC ",
            "SERVICE_PROCESS_END_UTC ", "SERVICE_PROCESS_LIFETIME_MS ", "SERVICE_ENDPOINT_PRELAUNCH_AVAILABLE ", "SERVICE_ENDPOINT_STOPPED_REFUSED ",
            "SERVICE_PROCESS_FAILURE ", "SERVICE_QUOTA_CAPTURE ", "SERVICE_QUOTA_PROCESS "];
        string[] block = stdout.Where(row => row.StartsWith("SERVICE_", StringComparison.Ordinal) && !row.StartsWith("SERVICE_QUOTA_FAILURE ", StringComparison.Ordinal)).ToArray();
        Need(block.Length == prefixes.Length && block.Select((row, i) => row.StartsWith(prefixes[i], StringComparison.Ordinal)).All(value => value),
            "service_quota_runner_block");
        string Field(int i) => block[i][prefixes[i].Length..];
        Need(Field(0) == "none" && Field(6) == "true" && Field(7) == "true" && Field(8) == "none", "service_quota_runner_cleanup");
        Need(int.TryParse(Field(2), NumberStyles.None, CultureInfo.InvariantCulture, out int processId) && processId > 0 &&
            Field(2) == processId.ToString(CultureInfo.InvariantCulture), "service_quota_runner_process_id");
        DateTimeOffset Utc(string text)
        {
            Need(DateTime.TryParseExact(text, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var value) &&
                value.Kind == DateTimeKind.Utc && text == value.ToString("O", CultureInfo.InvariantCulture), "service_quota_runner_utc");
            return new(value);
        }
        var start = Utc(Field(3)); var end = Utc(Field(4));
        Need(long.TryParse(Field(5), NumberStyles.None, CultureInfo.InvariantCulture, out long lifetime) && lifetime > 0 &&
            Field(5) == lifetime.ToString(CultureInfo.InvariantCulture) && end > start &&
            Math.Abs((end - start).TotalMilliseconds - lifetime) <= 100 && end - start <= runner.Duration + TimeSpan.FromMilliseconds(100) &&
            lifetime <= runner.Duration.TotalMilliseconds + 100 && start >= context.StartedUtc && end <= context.CompletedUtc,
            "service_quota_runner_lifetime");
        const string suffix = " controls=disabled acceptance=pending";
        Need(block[9].EndsWith(suffix, StringComparison.Ordinal), "service_quota_runner_capture_shape");
        var capture = ReadRecord(block[9][..^suffix.Length], "SERVICE_QUOTA_CAPTURE terminal=" + ServiceQuotaCapture.Terminal,
            "terminalBytes:dec", "quietMilliseconds:dec");
        Need(capture["terminalBytes"] == (ulong)raw.Length && capture["quietMilliseconds"] is >= 750 and <= long.MaxValue &&
            capture["quietMilliseconds"] <= (decimal)lifetime + 100, "service_quota_runner_capture_metrics");
        string disposition = stdout.Single(row => row.StartsWith("QEMU_PROCESS_RESULT ", StringComparison.Ordinal));
        string nativeExit = disposition["QEMU_PROCESS_RESULT disposition=runner-terminated exitCode=".Length..];
        Need(block[10] == "SERVICE_QUOTA_PROCESS terminationRequested=true deathConfirmed=true spontaneous=false cleanupFailed=false exitCode=" + nativeExit,
            "service_quota_runner_process_disposition");
        string framesJson = Field(1);
        Need(Encoding.UTF8.GetByteCount(framesJson) <= 2 * 1024 * 1024, "service_quota_runner_frame_extent");
        var frames = ServiceControlJson<WebFrameControl.ServiceReport>(framesJson);
        Need(frames.Session is not null && frames.ControlledPorts is not null && frames.ControlledPorts.Count == 0 &&
            frames.Injections is not null && frames.Injections.Count == 0, "service_quota_runner_frame_shape");
        var session = frames.Session!;
        Need(session.Stopped && session.Healthy && session.GuestExitExpected && session.ForwardedFrames is > 0 and <= 100_000 &&
            session.VlanFrames == 0 && session.DroppedFrames is not null && session.DroppedFrames.Count == 0 &&
            session.QemuOutPort is >= 1024 and <= 65535 && session.QemuInPort is >= 1024 and <= 65535 &&
            new[] { session.QemuOutPort, session.QemuInPort, context.ForwardPort }.Distinct().Count() == 3, "service_quota_runner_frame_owner");
        string[] arguments = ["-chardev", "socket,id=clroswebout,host=127.0.0.1,port=" + session.QemuOutPort.ToString(CultureInfo.InvariantCulture),
            "-chardev", "socket,id=clroswebin,host=127.0.0.1,port=" + session.QemuInPort.ToString(CultureInfo.InvariantCulture),
            "-object", "filter-redirector,id=clroswebfilter,netdev=n0,queue=tx,indev=clroswebin,outdev=clroswebout"];
        Need(session.Arguments is not null && session.Arguments.SequenceEqual(arguments), "service_quota_runner_frame_arguments");
        var packets = QemuEthernetCapture.Inspect(pcap, frameAnchor); Need(packets.Records > 0, "service_quota_runner_pcap");
        string[] expectedRows = ["QEMU_DISCOVERY result=" + context.Qemu + " checked=" + context.Qemu,
            "OVMF_DISCOVERY result=" + context.Ovmf + " checked=" + context.Ovmf, "TEST_START qemu.smoke", selected[0], selected[1],
            "OVMF_CODE " + context.Ovmf, ..selected.Skip(2), metrics, "SERIAL_MARKER_DRAIN milliseconds=750", disposition, ..block,
            "SUMMARY_LOG " + options.SummaryLog, "QEMU_EXPECTED_SERVICE_QUOTA outcome=captured acceptance=pending"];
        Need(runner.StandardOutput == string.Join("\r\n", expectedRows) + "\r\n", "service_quota_runner_stdout");
        string[] serial = ReadThreadingSerialLines(serialText);
        List<string> summary = ["QEMU_SMOKE_STATUS expected-quota", ..selected];
        summary.AddRange(serial.Where(row => IsThreadingObservedLine(row, plan)).Select(row => "OBSERVED_MARKER " + row));
        summary.AddRange(["APP_EXIT_RESULT none", "APP_EXIT_MALFORMED none", "MISSING_MARKERS none", "OUT_OF_ORDER_MARKERS none",
            "DUPLICATE_UNIQUE_MARKERS none", "FORBIDDEN_MARKER_HIT none", "FAIL_REASON none", "SERIAL_TAIL_LIMIT lines=8 chars=240"]);
        summary.AddRange(serial.TakeLast(8).Select(row => "SERIAL_TAIL_LINE " + (row.Length > 240 ? row[..237] + "..." : row))); summary.AddRange(block);
        byte[] expectedSummary = [..Encoding.UTF8.GetPreamble(), ..Encoding.UTF8.GetBytes(string.Join("\r\n", summary) + "\r\n")];
        Need(summaryBytes.AsSpan().SequenceEqual(expectedSummary), "service_quota_runner_summary_bytes");
        return new(framesJson, processId, start, end, lifetime, raw.Length, (long)capture["quietMilliseconds"], session.ForwardedFrames, packets);
    }
}
