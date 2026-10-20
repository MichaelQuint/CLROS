using System.Text;
using System.Text.Json.Nodes;
using OS.Tooling;
using OS.Tools.QemuRunner;

internal static class NativeAotServiceQuotaRunnerTests
{
    [HostTest("quota runner value capture recomputes bytes preserves inherited boot and startup pass and remains pending")]
    private static void CaptureOnly()
    {
        var f = Fixture(); var value = Inspect(f); // Synthetic values, no owned process or guest authority.
        HostCheck.Equal("captured", value.Outcome); HostCheck.Equal("pending", value.Acceptance);
        HostCheck.Equal(1234, value.ProcessId); HostCheck.Equal((long)f.Raw.Length, value.TerminalBytes);
        HostCheck.Equal(800L, value.QuietMilliseconds); HostCheck.Equal(258_000L, value.LifetimeMilliseconds);
        HostCheck.True(value.LifetimeMilliseconds < 300_000); // No positive service duration criterion.
        HostCheck.Equal(2L, value.ForwardedFrames); HostCheck.Equal(3U, value.Pcap.Records);
        HostCheck.Equal(0xd24c49ddU, value.Pcap.FrameCrc32); HostCheck.Equal(Frames, value.FramesJson);
        HostCheck.Equal(750L, Inspect(Rewrite(f, "quietMilliseconds=800", "quietMilliseconds=750")).QuietMilliseconds);
        f.Pcap[0] ^= 1; HostCheck.Equal(3U, value.Pcap.Records); HostCheck.Equal(Frames, value.FramesJson);
    }

    [HostTest("quota runner rejects noncapture process results and mismatched original selection UTC and lifetime")]
    private static void ProcessAndSelection()
    {
        var f = Fixture();
        foreach (var result in new[] { f.Runner with { ExitCode = 0 }, f.Runner with { ExitCode = 99 },
            f.Runner with { StandardError = "error" }, f.Runner with { TimedOut = true }, f.Runner with { Cancelled = true },
            f.Runner with { OutputLimitExceeded = true }, f.Runner with { TerminationConfirmed = false },
            f.Runner with { FailureReason = "cleanup" }, f.Runner with { Duration = TimeSpan.Zero },
            f.Runner with { Duration = TimeSpan.FromSeconds(961) }, f.Runner with { Duration = TimeSpan.FromSeconds(200) } })
            Reject(() => Inspect(f with { Runner = result }));
        foreach (var context in new[] { f.Context with { ForwardPort = 20809 }, f.Context with { Qemu = "relative" },
            f.Context with { Ovmf = "relative" }, f.Context with { StartedUtc = default },
            f.Context with { StartedUtc = f.Context.StartedUtc.AddSeconds(2) },
            f.Context with { CompletedUtc = f.Context.CompletedUtc.AddSeconds(-2) },
            f.Context with { CompletedUtc = f.Context.StartedUtc },
            f.Context with { CompletedUtc = f.Context.CompletedUtc.ToOffset(TimeSpan.FromHours(1)) } })
            Reject(() => Inspect(f with { Context = context }));
        foreach (Action<QemuSmokeOptions> change in new Action<QemuSmokeOptions>[] {
            o => o.ServiceQuotaFailure = false, o => o.ServiceLifecycle = true, o => o.SignalEpollRace = true,
            o => o.DryRun = true, o => o.TimeoutSeconds = 779, o => o.TimeoutSeconds = 901, o => o.BootPolicy = "VerifiedExit",
            o => o.WorkloadProfile = "other", o => o.FirmwareRngSource = "none", o => o.DiskFormat = "qcow2",
            o => o.LinuxElfImageVariant = "Damaged", o => o.LinuxThreadingApplication = "MinimalApiSmoke" })
        { var changed = Fixture(); change(changed.Options); Reject(() => Inspect(changed)); }
        Reject(() => Inspect(f with { Plan = f.Plan with { Presets = ["baseline"] } }));
        Reject(() => Inspect(f with { Plan = f.Plan with { ReplaceDefaultRequiredMarkers = false } }));
        Reject(() => Inspect(f with { Plan = f.Plan with { Forbidden = f.Plan.Forbidden.Where(marker => marker != "TEST_FAIL").ToArray() } }));
    }

    [HostTest("quota runner binds exact stdout summary streaming metrics terminal bytes measured quiet and native disposition")]
    private static void CaptureMetrics()
    {
        var f = Fixture();
        foreach (var edit in new[] {
            ("SERVICE_QUOTA_FAILURE true", "SERVICE_QUOTA_FAILURE false"),
            ("bytes=" + f.Raw.Length, "bytes=" + (f.Raw.Length + 1)), ("invalidLines=0", "invalidLines=1"),
            ("orderedSeen=6", "orderedSeen=5"), ("uniqueSeen=6", "uniqueSeen=5"),
            ("terminalBytes=" + f.Raw.Length, "terminalBytes=" + (f.Raw.Length - 1)),
            ("quietMilliseconds=800", "quietMilliseconds=749"), ("quietMilliseconds=800", "quietMilliseconds=258101"),
            ("quietMilliseconds=800", "quietMilliseconds=0800"),
            ("SERIAL_MARKER_DRAIN milliseconds=750", "SERIAL_MARKER_DRAIN milliseconds=749"),
            ("controls=disabled acceptance=pending", "controls=disabled acceptance=passed"),
            ("terminationRequested=true", "terminationRequested=false"), ("deathConfirmed=true", "deathConfirmed=false"),
            ("spontaneous=false", "spontaneous=true"), ("cleanupFailed=false", "cleanupFailed=true"),
            ("disposition=runner-terminated", "disposition=exited"),
            ("cleanupFailed=false exitCode=-1", "cleanupFailed=false exitCode=99"),
            ("QEMU_SMOKE_STATUS expected-quota", "QEMU_SMOKE_STATUS pass"),
            ("QEMU_EXPECTED_SERVICE_QUOTA outcome=captured acceptance=pending", "TEST_PASS qemu.smoke") })
            Reject(() => Inspect(Rewrite(f, edit.Item1, edit.Item2)));
        Reject(() => Inspect(f with { Summary = f.Summary[3..] }));
        Reject(() => Inspect(f with { Summary = [..f.Summary, (byte)'x'] }));
        Reject(() => Inspect(f with { Runner = f.Runner with { StandardOutput = f.Runner.StandardOutput + "TEST_PASS qemu.smoke.service\r\n" } }));
        Reject(() => Inspect(f with { Runner = f.Runner with { StandardOutput = f.Runner.StandardOutput.Replace("\r\n", "\n", StringComparison.Ordinal) } }));
    }

    [HostTest("quota runner rejects coherent trailer substitutions ordinary success and invalid inherited boot phase")]
    private static void NegativeTerminal()
    {
        string[] rows = Rows();
        foreach (string[] changed in new string[][] {
            [..rows, "OS_SMOKE_PASS"], [..rows, "TEST_PASS m16-012"], [..rows, "trailing"],
            [rows[0], rows[1], rows[1], ..rows.Skip(2)],
            [rows[0], rows[2], rows[1], ..rows.Skip(3)],
            [rows[0], rows[1] + " suffix", ..rows.Skip(2)],
            [..rows.Take(4), "M16_016_QUOTA_PROCESS_FAILURE exit=98 cause=physical-backing", rows[^1]],
            [..rows.Take(4), rows[^1]], [..rows.Take(5), rows[^1] + " suffix"],
            [rows[0], rows[1], rows[2], rows[2], ..rows.Skip(3)] })
            Reject(() => Inspect(Fixture(changed)));
        var f = Fixture(); Reject(() => Inspect(f with { Raw = f.Raw[..^1] }));
    }

    [HostTest("quota runner rejects missing duplicated unknown cleanup evidence and inconsistent process observations")]
    private static void CleanupEvidence()
    {
        var f = Fixture();
        foreach (var edit in new[] { ("SERVICE_SOCKET_CONTROLS none", "SERVICE_SOCKET_CONTROLS {}"),
            ("SERVICE_PROCESS_ID 1234", "SERVICE_PROCESS_ID 0"), ("SERVICE_PROCESS_ID 1234", "SERVICE_PROCESS_ID 01234"),
            ("SERVICE_PROCESS_START_UTC 2026-10-20T12:00:01.0000000Z", "SERVICE_PROCESS_START_UTC 2026-10-20T12:00:01.0000000+00:00"),
            ("SERVICE_PROCESS_END_UTC 2026-10-20T12:04:19.0000000Z", "SERVICE_PROCESS_END_UTC invalid"),
            ("SERVICE_PROCESS_LIFETIME_MS 258000", "SERVICE_PROCESS_LIFETIME_MS 258101"),
            ("SERVICE_ENDPOINT_PRELAUNCH_AVAILABLE true", "SERVICE_ENDPOINT_PRELAUNCH_AVAILABLE false"),
            ("SERVICE_ENDPOINT_STOPPED_REFUSED true", "SERVICE_ENDPOINT_STOPPED_REFUSED not_run"),
            ("SERVICE_PROCESS_FAILURE none", "SERVICE_PROCESS_FAILURE endpoint_live_after_stop"),
            ("SERVICE_PROCESS_FAILURE none", "SERVICE_PROCESS_FAILURE none\r\nSERVICE_UNKNOWN true") })
            Reject(() => Inspect(Rewrite(f, edit.Item1, edit.Item2)));
        foreach (string row in Block(f.Raw.Length))
        {
            Reject(() => Inspect(Rewrite(f, row + "\r\n", "")));
            Reject(() => Inspect(Rewrite(f, row + "\r\n", row + "\r\n" + row + "\r\n")));
        }
        Reject(() => Inspect(f with { Summary = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(f.Summary)
            .Replace("SERVICE_PROCESS_ID 1234", "SERVICE_PROCESS_ID 1235", StringComparison.Ordinal)) }));
    }

    [HostTest("quota runner checks strict frame owner shape exact redirector ports and borrowed PCAP without packet authority")]
    private static void FramesAndPcap()
    {
        var f = Fixture();
        foreach (Action<JsonObject> change in new Action<JsonObject>[] {
            r => r["Session"]!["Stopped"] = false, r => r["Session"]!["Healthy"] = false,
            r => r["Session"]!["GuestExitExpected"] = false, r => r["Session"]!["ForwardedFrames"] = 0,
            r => r["Session"]!["ForwardedFrames"] = 100001, r => r["Session"]!["VlanFrames"] = 1,
            r => r["Session"]!["QemuInPort"] = 20001, r => r["Session"]!["QemuInPort"] = 20808,
            r => r["Session"]!["Arguments"]![1] = "socket,id=clroswebout,host=127.0.0.1,port=20003",
            r => r["Session"]!["DroppedFrames"] = new JsonArray(new JsonObject { ["Ordinal"] = 1, ["Hex"] = "00" }),
            r => r["ControlledPorts"] = new JsonArray(20001), r => r["Injections"] = new JsonArray(new JsonObject()),
            r => r["Session"] = null, r => r["extra"] = 1 })
        { var json = JsonNode.Parse(Frames)!.AsObject(); change(json); Reject(() => Inspect(Rewrite(f, Frames, json.ToJsonString()))); }
        Reject(() => Inspect(Rewrite(f, Frames, Frames.Replace("\"Stopped\":true", "\"Stopped\":true,\"Stopped\":true", StringComparison.Ordinal))));
        foreach (byte[] bytes in new byte[][] { Array.Empty<byte>(), f.Pcap[..^1], new byte[4 * 1024 * 1024 + 1], [..f.Pcap, (byte)0] })
            Reject(() => Inspect(f with { Pcap = bytes }));
        Reject(() => Inspect(f with { Anchor = f.Anchor ^ 1 }));
        byte[] corrupt = f.Pcap.ToArray(); corrupt[0] ^= 1; Reject(() => Inspect(f with { Pcap = corrupt }));
    }

    private const string Frames = "{\"Session\":{\"QemuOutPort\":20001,\"QemuInPort\":20002,\"Arguments\":[\"-chardev\",\"socket,id=clroswebout,host=127.0.0.1,port=20001\",\"-chardev\",\"socket,id=clroswebin,host=127.0.0.1,port=20002\",\"-object\",\"filter-redirector,id=clroswebfilter,netdev=n0,queue=tx,indev=clroswebin,outdev=clroswebout\"],\"ForwardedFrames\":2,\"VlanFrames\":0,\"DroppedFrames\":[],\"GuestExitExpected\":true,\"Stopped\":true,\"Healthy\":true},\"ControlledPorts\":[],\"Injections\":[]}";
    private sealed record Capture(QemuSmokeOptions Options, QemuSmokePlan Plan, ProcessResult Runner, byte[] Raw, byte[] Summary,
        QemuTranscripts.ServiceRunnerContext Context, byte[] Pcap, uint Anchor);
    private static string[] Rows() => ["OS_BOOT_OK", "OS_SMOKE_PASS", "TEST_START m16-011", "TEST_PASS m16.014.startup",
        ServiceQuotaCapture.ProcessFailure, ServiceQuotaCapture.Terminal];

    private static Capture Fixture(string[]? rawRows = null)
    {
        var options = new QemuSmokeOptions { BootPolicy = "Diagnostic", ServiceQuotaFailure = true, TimeoutSeconds = 900,
            FirmwareRngSource = "virtio", LinuxThreadingApplication = "MinimalApiService" };
        var flags = EfiProfile.Create("Diagnostic", new Dictionary<string, bool> { ["EnableM16014Probe"] = true }).Flags;
        string[] required = Rows(), forbidden = ["OS_SMOKE_FAIL", "TEST_FAIL", ..ServiceQuotaCapture.ForbiddenSuccessMarkers.Where(marker => marker != "OS_SMOKE_PASS")];
        var plan = new QemuSmokePlan("Diagnostic", flags, required, required, required, forbidden, [], false, true);
        var started = new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);
        var context = new QemuTranscripts.ServiceRunnerContext(20808, Path.GetFullPath("value-qemu.exe"), Path.GetFullPath("value-ovmf.fd"), started, started.AddSeconds(260));
        rawRows ??= Rows(); byte[] raw = Encoding.ASCII.GetBytes(string.Join("\r\n", rawRows) + "\r\n");
        string[] selected = ["IMAGE_ARTIFACT " + options.Image, "SERIAL_LOG " + options.SerialLog, "WORKLOAD_PROFILE SingleMinimalApi",
            "FIRMWARE_RNG_SOURCE virtio", "SOCKET_FORWARD_PORT 20808", "CONTINUING_SERVICE true", "SERVICE_QUOTA_FAILURE true", "MARKER_PRESETS none",
            "REQUIRED_MARKERS " + string.Join(';', required), "ORDERED_MARKERS " + string.Join(';', required), "UNIQUE_MARKERS " + string.Join(';', required),
            "FORBIDDEN_MARKERS " + string.Join(';', forbidden)];
        string[] stdout = ["QEMU_DISCOVERY result=" + context.Qemu + " checked=" + context.Qemu,
            "OVMF_DISCOVERY result=" + context.Ovmf + " checked=" + context.Ovmf, "TEST_START qemu.smoke", selected[0], selected[1], "OVMF_CODE " + context.Ovmf,
            ..selected.Skip(2), $"SERIAL_MARKER_STREAM status=Passed terminal=Success bytes={raw.Length} lines={rawRows.Length} invalidBytes=0 invalidLines=0 orderedSeen=6 outOfOrder=0 uniqueSeen=6 duplicateUnique=0",
            "SERIAL_MARKER_DRAIN milliseconds=750", "QEMU_PROCESS_RESULT disposition=runner-terminated exitCode=-1", ..Block(raw.Length),
            "SUMMARY_LOG " + options.SummaryLog, "QEMU_EXPECTED_SERVICE_QUOTA outcome=captured acceptance=pending"];
        string[] summary = ["QEMU_SMOKE_STATUS expected-quota", ..selected, ..rawRows.Select(row => "OBSERVED_MARKER " + row), "APP_EXIT_RESULT none",
            "APP_EXIT_MALFORMED none", "MISSING_MARKERS none", "OUT_OF_ORDER_MARKERS none", "DUPLICATE_UNIQUE_MARKERS none", "FORBIDDEN_MARKER_HIT none",
            "FAIL_REASON none", "SERIAL_TAIL_LIMIT lines=8 chars=240", ..rawRows.TakeLast(8).Select(row => "SERIAL_TAIL_LINE " + row), ..Block(raw.Length)];
        return new(options, plan, new(22, string.Join("\r\n", stdout) + "\r\n", "", false, false, false, TimeSpan.FromSeconds(260)), raw,
            [..Encoding.UTF8.GetPreamble(), ..Encoding.UTF8.GetBytes(string.Join("\r\n", summary) + "\r\n")], context, Pcap(), 0xd24c49dd);
    }

    private static string[] Block(int bytes) => ["SERVICE_SOCKET_CONTROLS none", "SERVICE_FRAME_CONTROL " + Frames, "SERVICE_PROCESS_ID 1234",
        "SERVICE_PROCESS_START_UTC 2026-10-20T12:00:01.0000000Z", "SERVICE_PROCESS_END_UTC 2026-10-20T12:04:19.0000000Z",
        "SERVICE_PROCESS_LIFETIME_MS 258000", "SERVICE_ENDPOINT_PRELAUNCH_AVAILABLE true", "SERVICE_ENDPOINT_STOPPED_REFUSED true", "SERVICE_PROCESS_FAILURE none",
        "SERVICE_QUOTA_CAPTURE terminal=CLROS_M16_016_QUOTA_CLEANUP terminalBytes=" + bytes + " quietMilliseconds=800 controls=disabled acceptance=pending",
        "SERVICE_QUOTA_PROCESS terminationRequested=true deathConfirmed=true spontaneous=false cleanupFailed=false exitCode=-1"];
    private static byte[] Pcap()
    {
        byte[] anchor = new byte[60]; Convert.FromHexString("02000000000202000000000188B5").CopyTo(anchor, 0); Encoding.ASCII.GetBytes("CLROS-M07").CopyTo(anchor, 14);
        using var stream = new MemoryStream(); stream.Write(Convert.FromHexString("D4C3B2A10200040000000000000000000000010001000000"));
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        foreach (byte[] frame in new[] { new byte[60], anchor, new byte[60] })
        { writer.Write(1U); writer.Write(0U); writer.Write((uint)frame.Length); writer.Write((uint)frame.Length); writer.Write(frame); }
        writer.Flush(); return stream.ToArray();
    }
    private static Capture Rewrite(Capture f, string before, string after)
    {
        string summary = Encoding.UTF8.GetString(f.Summary);
        HostCheck.True(before != after && (f.Runner.StandardOutput.Contains(before, StringComparison.Ordinal) || summary.Contains(before, StringComparison.Ordinal)), "mutation must change a supplied observation");
        return f with { Runner = f.Runner with { StandardOutput = f.Runner.StandardOutput.Replace(before, after, StringComparison.Ordinal) },
            Summary = Encoding.UTF8.GetBytes(summary.Replace(before, after, StringComparison.Ordinal)) };
    }
    private static QemuTranscripts.ServiceQuotaRunnerInspection Inspect(Capture f) => QemuTranscripts.InspectNativeAotServiceQuotaRunnerForTests(
        f.Options, f.Plan, f.Runner, f.Raw, f.Summary, f.Context, f.Pcap, f.Anchor);
    private static void Reject(Action action)
    { bool rejected = false; try { action(); } catch (Exception error) when (error is ToolFailure or InvalidDataException or ArgumentException) { rejected = true; } HostCheck.True(rejected); }
}
