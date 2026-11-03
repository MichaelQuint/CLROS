using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OS.Tools.QemuRunner;

namespace OS.Tooling;
public static partial class QemuTranscripts
{
    /// <summary>Inspect a borrowed raw serial, runner result and exact summary
    /// against explicit selection/window expectations. Recomputes streaming
    /// metrics, observed rows and tail; inconsistent process/HTTP/summary state
    /// throws. Caller data grants no native, source, packet or guest authority.
    /// Controls are mandatory by default. Explicit requireControls=false is a
    /// historical framing-only seam returning Controls=null; production never
    /// selects it. Inputs are not executed, mutated or retained.</summary>

    public static WebRunnerReport InspectNativeAotWebRunnerForTests(QemuSmokeOptions options, QemuSmokePlan plan, ProcessResult runner, byte[] raw, byte[] summaryBytes, WebRunnerContext context, string body, string configuration, bool logQuota = false, bool requireControls = true) => ReadWebRunner(options, plan, runner, raw, summaryBytes, context, body, configuration, logQuota, requireControls);
    private static WebRunnerReport ReadWebRunner(QemuSmokeOptions options, QemuSmokePlan plan, ProcessResult runner, byte[] raw, byte[] summaryBytes, WebRunnerContext context, string body, string configuration, bool logQuota, bool requireControls, WebRaceRendezvous.Report? raceGate = null, WebUtcObservation? producerWindow = null, bool eventOwned = false)
    {
        Need(raceGate is null ? producerWindow is null && !eventOwned : producerWindow is not null && requireControls && !logQuota, "web_race_runner_mode");
        RequireCompleteProcess(runner, "web_runner_incomplete");
        Need(runner.ExitCode == 0 && runner.StandardError.Length == 0 && raw.Length is> 0 and <= 8 * 1024 * 1024 && summaryBytes.Length is> 3 and <= 16 * 1024 * 1024 && summaryBytes.AsSpan(0, 3).SequenceEqual(Encoding.UTF8.GetPreamble()), "web_runner_inputs");
        Need(context.ForwardPort is> 0 and <= 65535 && Path.IsPathFullyQualified(context.Qemu) && Path.IsPathFullyQualified(context.Ovmf) && !Controls.IsMatch(context.Qemu) && !Controls.IsMatch(context.Ovmf) && options.WorkloadProfile == "SingleMinimalApi" && options.FirmwareRngSource == "virtio" && options.TimeoutSeconds is> 0 and <= 285 && context.StartedUtc.Offset == TimeSpan.Zero && context.CompletedUtc.Offset == TimeSpan.Zero && context.StartedUtc > DateTimeOffset.UnixEpoch && context.CompletedUtc >= context.StartedUtc, "web_runner_selection");
        string summaryText = DecodeUtf8(summaryBytes[3..]);
        Need(summaryText.EndsWith("\r\n", StringComparison.Ordinal), "web_runner_summary_end");
        string[] summary = summaryText[..^2].Split("\r\n", StringSplitOptions.None);
        string Value(string key)
        {
            string prefix = key + " ";
            string[] found = summary.Where(r => r.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
            Need(found.Length == 1, "web_runner_summary_field_" + key);
            return found[0][prefix.Length..];
        }

        ulong Decimal(string key)
        {
            string value = Value(key);
            Need(value.Length != 0 && (value.Length == 1 || value[0] != '0') && value.All(c => c is >= '0' and <= '9') && ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out _), "web_runner_number_" + key);
            return ulong.Parse(value, CultureInfo.InvariantCulture);
        }

        ulong pid = Decimal("WEB_PROCESS_ID"), life = Decimal("WEB_PROCESS_LIFETIME_MS"), health = Decimal("HTTP_PROBE_ELAPSED_MS"), config = Decimal("WEB_CONFIG_PROBE_ELAPSED_MS");
        ulong logging = Decimal("WEB_LOG_PROBE_ELAPSED_MS"), repeat = Decimal("WEB_LOG_REPEAT_PROBE_ELAPSED_MS"), after = Decimal("WEB_HEALTH_AFTER_PROBE_ELAPSED_MS");
        ulong vector = logQuota ? 0 : Decimal("WEB_VECTOR_PROBE_ELAPSED_MS");
        string first = Value("WEB_PROCESS_START_UTC"), last = Value("WEB_PROCESS_END_UTC"), exit = Value("WEB_PROCESS_EXIT_CODE");
        Need(DateTime.TryParseExact(first, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var started) && started.Kind == DateTimeKind.Utc && DateTime.TryParseExact(last, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var ended) && ended.Kind == DateTimeKind.Utc, "web_runner_child_timestamps");
        // Reparse separately so both values are definitely assigned after Need.
        started = DateTime.ParseExact(first, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        ended = DateTime.ParseExact(last, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        Need(pid is> 0 and <= int.MaxValue && ended >= started && life > 0 && life <= (ulong)(options.TimeoutSeconds * 1000L + 5000) && life == (ulong)((ended - started).Ticks / TimeSpan.TicksPerMillisecond) && started >= context.StartedUtc.UtcDateTime && ended <= context.CompletedUtc.UtcDateTime && runner.Duration.TotalMilliseconds >= life && runner.Duration.TotalSeconds <= options.TimeoutSeconds + 60 && Math.Abs((context.CompletedUtc - context.StartedUtc).TotalMilliseconds - runner.Duration.TotalMilliseconds) <= 5000 && health <= 20000 && config <= 20000 && logging <= 60000 && repeat <= 20000 && vector <= 20000 && after <= 20000 && health + config + logging + repeat + vector + after <= life && int.TryParse(exit, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int exitCode) && exitCode.ToString(CultureInfo.InvariantCulture) == exit, "web_runner_child_lifetime");
        string? socketsJson = requireControls ? Value("WEB_SOCKET_CONTROLS") : null, framesJson = requireControls ? Value("WEB_FRAME_CONTROL") : null;
        var controls = requireControls ? ReadWebControls(socketsJson!, framesJson!, context.ForwardPort) : null;
        int ordinary = logQuota ? 5 : 6, count = ordinary + (requireControls ? 3 : 0);
        Need(Decimal("WEB_HTTP_UTC_TOLERANCE_SECONDS") == WebUtcToleranceSeconds && Decimal("WEB_HTTP_UTC_WINDOWS") == (ulong)count, "web_runner_utc_policy");
        List<WebUtcObservation> utc = [];
        List<string> utcRows = ["WEB_HTTP_UTC_TOLERANCE_SECONDS " + WebUtcToleranceSeconds, "WEB_HTTP_UTC_WINDOWS " + count];
        List<ulong> durations = logQuota ? [health, config, logging, repeat, after] : [health, config, logging, repeat, vector, after];
        WebSocketControls.Exchange[] exchanges = controls is null ? [] : [controls.Sockets.Deadline, controls.Sockets.Before, controls.Sockets.After];
        durations.AddRange(exchanges.Select(x => (ulong)x.ElapsedMilliseconds));
        Need(durations.Aggregate(0UL, (total, value) => total + value) <= life, "web_runner_probe_lifetime");
        for (int i = 0; i < count; i++)
        {
            string prefix = "WEB_HTTP_UTC_" + i.ToString(CultureInfo.InvariantCulture);
            string from = Value(prefix + "_STARTED"), to = Value(prefix + "_COMPLETED");
            Need(DateTime.TryParseExact(from, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var begin) && begin.Kind == DateTimeKind.Utc, "web_runner_utc_timestamp");
            Need(DateTime.TryParseExact(to, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var end) && end.Kind == DateTimeKind.Utc, "web_runner_utc_timestamp");
            Need(begin >= started && end <= ended && begin <= end && (i == 0 || begin >= utc[^1].CompletedUtc.UtcDateTime) && Math.Abs((end - begin).TotalMilliseconds - durations[i]) <= 5000, "web_runner_utc_interval");
            utc.Add(new(begin, end));
            utcRows.AddRange([prefix + "_STARTED " + from, prefix + "_COMPLETED " + to]);
            if (i >= ordinary)
                Need(utc[^1].StartedUtc == exchanges[i - ordinary].StartedUtc && utc[^1].CompletedUtc == exchanges[i - ordinary].CompletedUtc, "web_runner_control_utc_join");
        }

        if (raceGate is not null)
            ValidateWebRaceRunnerWindow(raceGate, producerWindow!.Value, context, pid, started, ended, utc, eventOwned);
        string serial = DecodeUtf8(raw);
        var stream = new SerialMarkerStateMachine(plan.Required, plan.Forbidden, successTerminalMarker: "TEST_PASS m10.004", orderedMarkers: plan.Ordered, uniqueMarkers: plan.Unique);
        stream.Feed(raw);
        var completed = stream.Complete();
        Need(completed.Passed && completed.SuccessTerminalCount == 1 && completed.FailureTerminalCount == 0, "web_runner_serial_completion");
        string metrics = $"SERIAL_MARKER_STREAM status={completed.Status} terminal={completed.TerminalState} bytes={completed.TotalBytes} lines={completed.LinesProcessed} invalidBytes={completed.InvalidByteCount} invalidLines={completed.InvalidNoiseLineCount} orderedSeen={completed.OrderedSeenCount} outOfOrder={completed.OutOfOrderOccurrenceCount} uniqueSeen={completed.UniqueSeenCount} duplicateUnique={completed.DuplicateUniqueOccurrenceCount}";
        string endpoint = "http://127.0.0.1:" + context.ForwardPort.ToString(CultureInfo.InvariantCulture);
        string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
        int bodyBytes = Encoding.UTF8.GetByteCount(body), configBytes = Encoding.UTF8.GetByteCount(configuration);
        Need(bodyBytes is> 0 and <= 1024 && configBytes is> 0 and <= 4096, "web_runner_http_expectations");
        Need(runner.StandardOutput.EndsWith("\r\n", StringComparison.Ordinal), "web_runner_stdout_end");
        string[] stdout = RunnerBody(options, runner.StandardOutput[..^2].Split("\r\n", StringSplitOptions.None));
        Need(stdout.Length == (logQuota ? 29 : 31), "web_runner_stdout_count");
        var healthStart = ReadRecord(stdout[14], $"HTTP_PROBE_START endpoint={endpoint} expected_status=200 expected_body_bytes={bodyBytes}", "timeout_seconds:dec");
        var configStart = ReadRecord(stdout[16], $"HTTP_PROBE_START endpoint={endpoint} expected_status=200 expected_body_bytes={configBytes}", "timeout_seconds:dec");
        ulong healthTimeout = healthStart["timeout_seconds"], configTimeout = configStart["timeout_seconds"];
        Need(healthTimeout is> 0 and <= 20 && configTimeout is> 0 and <= 20 && health <= healthTimeout * 1000 && config <= configTimeout * 1000, "web_runner_http_deadline");
        string healthHash = Hash(body), configHash = Hash(configuration);
        string logBody = logQuota ? "" : "CLROS_LOG_OK workers=8 records=64 writev=32 invalid_errno=9";
        List<(string Prefix, string Path, string Method, int Status, string Body, ulong Milliseconds)> extra = [("WEB_LOG_PROBE", "/__clros/logexercise", "POST", logQuota ? 500 : 200, logBody, logging), ("WEB_LOG_REPEAT_PROBE", "/__clros/logexercise", "POST", 409, "", repeat), ("WEB_HEALTH_AFTER_PROBE", "/healthz", "GET", 200, body, after)];
        if (!logQuota)
            extra.Insert(2, ("WEB_VECTOR_PROBE", "/__clros/vectors", "GET", 200, "", vector));
        List<string> extraStdout = [], extraSummary = [];
        for (int i = 0; i < extra.Count; i++)
        {
            var p = extra[i];
            bool binary = p.Prefix == "WEB_VECTOR_PROBE";
            int length = binary ? WebVectorResponse.Length : Encoding.UTF8.GetByteCount(p.Body);
            string hash = binary ? WebVectorResponse.Sha256 : Hash(p.Body);
            string start = $"HTTP_PROBE_START endpoint={endpoint} expected_status={p.Status} expected_body_bytes={length}";
            ulong timeout = ReadRecord(stdout[18 + i * 2], start, "timeout_seconds:dec")["timeout_seconds"];
            Need(timeout > 0 && timeout <= (p.Prefix == "WEB_LOG_PROBE" ? 60UL : 20UL) && p.Milliseconds <= timeout * 1000, "web_runner_logging_deadline");
            extraStdout.Add(start + " timeout_seconds=" + timeout);
            extraStdout.Add($"HTTP_PROBE_PASS endpoint={endpoint} status={p.Status} body_bytes={length} body_sha256={hash} elapsed_ms={p.Milliseconds}");
            extraSummary.AddRange([p.Prefix + "_URL " + endpoint + p.Path, p.Prefix + "_METHOD " + p.Method, p.Prefix + "_EXPECTED_STATUS " + p.Status, p.Prefix + "_EXPECTED_BODY_BYTES " + length, p.Prefix + "_EXPECTED_BODY_SHA256 " + hash, p.Prefix + "_RESULT pass", p.Prefix + "_REASON none", p.Prefix + "_STATUS " + p.Status, p.Prefix + "_BODY_BYTES " + length, p.Prefix + "_BODY_SHA256 " + hash, p.Prefix + "_ELAPSED_MS " + p.Milliseconds]);
        }

        string Format(IReadOnlyList<string> values) => values.Count == 0 ? "none" : string.Join(';', values);
        string[] selected = ["IMAGE_ARTIFACT " + options.Image, "SERIAL_LOG " + options.SerialLog, "WORKLOAD_PROFILE SingleMinimalApi", "FIRMWARE_RNG_SOURCE virtio", "SOCKET_FORWARD_PORT " + context.ForwardPort, "MARKER_PRESETS " + Format(plan.Presets), "REQUIRED_MARKERS " + Format(plan.Required), "ORDERED_MARKERS " + Format(plan.Ordered), "UNIQUE_MARKERS " + Format(plan.Unique), "FORBIDDEN_MARKERS " + Format(plan.Forbidden)];
        List<string> expected = [..RunnerBackend(options), "QEMU_DISCOVERY result=" + context.Qemu + " checked=" + context.Qemu, "OVMF_DISCOVERY result=" + context.Ovmf + " checked=" + context.Ovmf, "TEST_START qemu.smoke", selected[0], selected[1], "OVMF_CODE " + context.Ovmf, ..selected.Skip(2), $"HTTP_PROBE_START endpoint={endpoint} expected_status=200 expected_body_bytes={bodyBytes} timeout_seconds={healthTimeout}", $"HTTP_PROBE_PASS endpoint={endpoint} status=200 body_bytes={bodyBytes} body_sha256={healthHash} elapsed_ms={health}", $"HTTP_PROBE_START endpoint={endpoint} expected_status=200 expected_body_bytes={configBytes} timeout_seconds={configTimeout}", $"HTTP_PROBE_PASS endpoint={endpoint} status=200 body_bytes={configBytes} body_sha256={configHash} elapsed_ms={config}", ..extraStdout, metrics, "SERIAL_MARKER_DRAIN milliseconds=750", "QEMU_PROCESS_RESULT disposition=runner-terminated exitCode=" + exit, "SUMMARY_LOG " + options.SummaryLog, "TEST_PASS qemu.smoke"];
        Need(runner.StandardOutput == string.Join("\r\n", expected) + "\r\n", "web_runner_stdout");
        List<string> expectedSummary = ["QEMU_SMOKE_STATUS pass", ..selected, "HTTP_PROBE_ENDPOINT " + endpoint, "HTTP_PROBE_EXPECTED_STATUS 200", "HTTP_PROBE_EXPECTED_BODY_BYTES " + bodyBytes, "HTTP_PROBE_TIMEOUT_SECONDS 20", "HTTP_PROBE_RESULT pass", "HTTP_PROBE_REASON none", "HTTP_PROBE_STATUS 200", "HTTP_PROBE_BODY_BYTES " + bodyBytes, "HTTP_PROBE_BODY_SHA256 " + healthHash, "HTTP_PROBE_ELAPSED_MS " + health, "CONTINUING_WEB true", "WEB_LOG_CASE " + (logQuota ? "records64" : "normal"), "WEB_CONFIG_PROBE_URL " + endpoint + "/config", "WEB_CONFIG_PROBE_EXPECTED_STATUS 200", "WEB_CONFIG_PROBE_EXPECTED_BODY_BYTES " + configBytes, "WEB_CONFIG_PROBE_EXPECTED_BODY_SHA256 " + configHash, "WEB_CONFIG_PROBE_RESULT pass", "WEB_CONFIG_PROBE_REASON none", "WEB_CONFIG_PROBE_STATUS 200", "WEB_CONFIG_PROBE_BODY_BYTES " + configBytes, "WEB_CONFIG_PROBE_BODY_SHA256 " + configHash, "WEB_CONFIG_PROBE_ELAPSED_MS " + config, ..extraSummary, ..requireControls ? new[]
        {
            "WEB_SOCKET_CONTROLS " + socketsJson,
            "WEB_FRAME_CONTROL " + framesJson,
            "WEB_PROCESS_ALIVE_AFTER_CONTROLS true"
        }

        : Array.Empty<string>(), ..raceGate is null ? Array.Empty<string>() : new[]
        {
            "WEB_RACE_GATE " + JsonSerializer.Serialize(raceGate),
            "WEB_PROCESS_ALIVE_AFTER_RACE_GATE true"
        }, "WEB_ENDPOINT_PRELAUNCH_AVAILABLE true", "WEB_ENDPOINT_STOPPED_REFUSED true", "WEB_PROCESS_ID " + pid, "WEB_PROCESS_START_UTC " + first, "WEB_PROCESS_END_UTC " + last, "WEB_PROCESS_LIFETIME_MS " + life, ..utcRows, "WEB_PROCESS_ALIVE_BEFORE_HEALTH true", "WEB_PROCESS_ALIVE_AFTER_HEALTH true", "WEB_PROCESS_ALIVE_AFTER_CONFIG true", "WEB_PROCESS_ALIVE_AFTER_LOG true", "WEB_PROCESS_ALIVE_AFTER_LOG_REPEAT true", ..logQuota ? Array.Empty<string>() : new[]
        {
            "WEB_PROCESS_ALIVE_AFTER_VECTORS true"
        }, "WEB_PROCESS_ALIVE_AFTER_FINAL_HEALTH true", "WEB_PROCESS_TERMINATION_REQUESTED true", "WEB_PROCESS_DEATH_CONFIRMED true", "WEB_PROCESS_SPONTANEOUS_EXIT false", "WEB_PROCESS_EXIT_CODE " + exit, "WEB_PROCESS_FAILURE none"];
        expectedSummary.InsertRange(4, RunnerBackend(options));
        string[] rawLines = ReadThreadingSerialLines(serial);
        if (controls is not null)
        {
            string[] events = rawLines.Where(l => l.StartsWith("M07_027_TIMEOUT", StringComparison.Ordinal) || l.StartsWith("M07_028_DROP", StringComparison.Ordinal)).ToArray();
            Need(events.SequenceEqual(new[] { controls.Sockets.Observations.DropRow, controls.Sockets.Observations.TimeoutRow }), "web_runner_control_serial_join");
        }

        expectedSummary.AddRange(rawLines.Where(l => IsThreadingObservedLine(l, plan)).Select(l => "OBSERVED_MARKER " + l));
        expectedSummary.AddRange(["APP_EXIT_RESULT none", "APP_EXIT_MALFORMED none", "MISSING_MARKERS none", "OUT_OF_ORDER_MARKERS none", "DUPLICATE_UNIQUE_MARKERS none", "FORBIDDEN_MARKER_HIT none", "FAIL_REASON none", "SERIAL_TAIL_LIMIT lines=8 chars=240"]);
        expectedSummary.AddRange(rawLines.TakeLast(8).Select(l => "SERIAL_TAIL_LINE " + (l.Length > 240 ? l[..237] + "..." : l)));
        byte[] encoded = [..Encoding.UTF8.GetPreamble(), ..Encoding.UTF8.GetBytes(string.Join("\r\n", expectedSummary) + "\r\n")];
        Need(summaryBytes.AsSpan().SequenceEqual(encoded), "web_runner_summary_bytes");
        return new((int)pid, (long)life, (int)health, (int)config, utc.AsReadOnly(), controls);
    }
}
