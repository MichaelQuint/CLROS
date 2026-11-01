using System.Text;
using OS.Tooling;
using OS.Tools.QemuRunner;

internal static class NativeAotServiceRunnerTests
{
    [HostTest("service runner recomputes original serial and exact diagnostic summary with confirmed cleanup")]
    private static void Complete()
    {
        var f = Fixture(); var result = Inspect(f); // Synthetic framing only; no parent/native/packet authority.
        HostCheck.Equal("{}", result.ControlsJson); HostCheck.Equal("{}", result.FramesJson); HostCheck.Equal(1234, result.ProcessId);
        HostCheck.Equal(f.Context.StartedUtc.AddSeconds(1), result.StartUtc); HostCheck.Equal(f.Context.CompletedUtc.AddSeconds(-1), result.EndUtc);
        HostCheck.Equal(798_000L, result.LifetimeMilliseconds);
        HostCheck.Equal(798_100L, Inspect(Rewrite(f, "SERVICE_PROCESS_LIFETIME_MS 798000", "SERVICE_PROCESS_LIFETIME_MS 798100")).LifetimeMilliseconds);
        HostCheck.Equal(797_900L, Inspect(Rewrite(f, "SERVICE_PROCESS_LIFETIME_MS 798000", "SERVICE_PROCESS_LIFETIME_MS 797900")).LifetimeMilliseconds);
    }

    [HostTest("service runner admits lifecycle capture margin but rejects its next second and ordinary widening")]
    private static void CaptureBounds()
    {
        var f = Fixture(); f.Options.ServiceLifecycle = true; f.Options.TimeoutSeconds = 1080;
        _ = Inspect(f);
        f.Options.TimeoutSeconds = 1081; Reject(() => Inspect(f));
        f.Options.ServiceLifecycle = false; f.Options.TimeoutSeconds = 901; Reject(() => Inspect(f));
    }

    [HostTest("service runner rejects timeout cancellation truncation and unconfirmed or spontaneous process exit")]
    private static void ProcessFailures()
    {
        var f = Fixture();
        foreach (var result in new[] { f.Runner with { ExitCode = 1 }, f.Runner with { StandardError = "error" },
            f.Runner with { TimedOut = true }, f.Runner with { Cancelled = true }, f.Runner with { OutputLimitExceeded = true },
            f.Runner with { TerminationConfirmed = false }, f.Runner with { FailureReason = "cleanup" },
            f.Runner with { Duration = TimeSpan.FromSeconds(300) }, f.Runner with { Duration = TimeSpan.FromSeconds(961) },
            f.Runner with { StandardOutput = f.Runner.StandardOutput.Replace("disposition=runner-terminated", "disposition=exited", StringComparison.Ordinal) } })
            Reject(() => Inspect(f with { Runner = result }));
    }

    [HostTest("service runner binds selection forward markers raw byte counts and summary tails without prefix acceptance")]
    private static void Substitutions()
    {
        var f = Fixture();
        foreach (string text in new[] { "SOCKET_FORWARD_PORT 20808", "bytes=" + f.Raw.Length, "invalidLines=0", "milliseconds=750", "TEST_PASS qemu.smoke" })
            Reject(() => Inspect(f with { Runner = f.Runner with { StandardOutput = f.Runner.StandardOutput.Replace(text, text + "x", StringComparison.Ordinal) } }));
        Reject(() => Inspect(f with { Raw = Encoding.ASCII.GetBytes("TEST_START service\r\nOS_BOOT_OK\r\n") }));
        Reject(() => Inspect(f with { Raw = [..f.Raw, ..Encoding.ASCII.GetBytes("OS_SMOKE_FAIL\r\n")] }));
        Reject(() => Inspect(f with { Summary = f.Summary[3..] }));
        Reject(() => Inspect(f with { Summary = [..f.Summary, (byte)'x'] }));
        Reject(() => Inspect(f with { Context = f.Context with { ForwardPort = 20809 } }));
        Reject(() => Inspect(f with { Context = f.Context with { Qemu = "relative" } }));
        Reject(() => Inspect(f with { Context = f.Context with { StartedUtc = default } }));
        Reject(() => Inspect(f with { Context = f.Context with { StartedUtc = f.Context.StartedUtc.AddSeconds(2) } }));
        Reject(() => Inspect(f with { Context = f.Context with { CompletedUtc = f.Context.CompletedUtc.AddSeconds(-2) } }));
        Reject(() => Inspect(f with { Context = f.Context with { CompletedUtc = f.Context.StartedUtc } }));
        Reject(() => Inspect(f with { Runner = f.Runner with { Duration = TimeSpan.FromSeconds(797) } }));
        foreach (Action<QemuSmokeOptions> alter in new Action<QemuSmokeOptions>[] { o => o.TimeoutSeconds = 779, o => o.TimeoutSeconds = 901,
            o => o.FirmwareRngSource = "none", o => o.WorkloadProfile = "other", o => o.LinuxThreadingApplication = "MinimalApiSmoke" })
        { var changed = Fixture(); alter(changed.Options); Reject(() => Inspect(changed)); }
    }

    [HostTest("service runner binds exact control blocks canonical process dates duration and endpoint cleanup")]
    private static void Evidence()
    {
        var f = Fixture();
        foreach (var edit in new[] { ("CONTINUING_SERVICE true", "CONTINUING_SERVICE false"),
            ("SERVICE_SOCKET_CONTROLS {}", "SERVICE_SOCKET_CONTROLS none"), ("SERVICE_SOCKET_CONTROLS {}", "SERVICE_SOCKET_CONTROLS []"),
            ("SERVICE_SOCKET_CONTROLS {}", "SERVICE_SOCKET_CONTROLS {"), ("SERVICE_FRAME_CONTROL {}", "SERVICE_FRAME_CONTROL null"),
            ("SERVICE_PROCESS_ID 1234", "SERVICE_PROCESS_ID 0"), ("SERVICE_PROCESS_ID 1234", "SERVICE_PROCESS_ID 01234"),
            ("SERVICE_PROCESS_ID 1234", "SERVICE_PROCESS_ID +1234"), ("SERVICE_PROCESS_ID 1234", "SERVICE_PROCESS_ID 2147483648"),
            ("SERVICE_PROCESS_START_UTC 2026-10-18T12:00:01.0000000Z", "SERVICE_PROCESS_START_UTC 2026-10-18T12:00:01Z"),
            ("SERVICE_PROCESS_START_UTC 2026-10-18T12:00:01.0000000Z", "SERVICE_PROCESS_START_UTC 2026-10-18T12:00:01.0000000+00:00"),
            ("SERVICE_PROCESS_END_UTC 2026-10-18T12:13:19.0000000Z", "SERVICE_PROCESS_END_UTC invalid"),
            ("SERVICE_PROCESS_LIFETIME_MS 798000", "SERVICE_PROCESS_LIFETIME_MS 300000"),
            ("SERVICE_PROCESS_LIFETIME_MS 798000", "SERVICE_PROCESS_LIFETIME_MS 798101"),
            ("SERVICE_PROCESS_LIFETIME_MS 798000", "SERVICE_PROCESS_LIFETIME_MS 0798000"),
            ("SERVICE_PROCESS_LIFETIME_MS 798000", "SERVICE_PROCESS_LIFETIME_MS 9223372036854775808"),
            ("SERVICE_ENDPOINT_PRELAUNCH_AVAILABLE true", "SERVICE_ENDPOINT_PRELAUNCH_AVAILABLE false"),
            ("SERVICE_ENDPOINT_STOPPED_REFUSED true", "SERVICE_ENDPOINT_STOPPED_REFUSED not_run"),
            ("SERVICE_PROCESS_FAILURE none", "SERVICE_PROCESS_FAILURE socket_controls_incomplete") })
            Reject(() => Inspect(Rewrite(f, edit.Item1, edit.Item2)));
        foreach (string row in Block())
        {
            Reject(() => Inspect(Rewrite(f, row + "\r\n", "")));
            Reject(() => Inspect(Rewrite(f, row + "\r\n", row + "\r\n" + row + "\r\n")));
        }
        Reject(() => Inspect(Rewrite(f, "SERVICE_PROCESS_FAILURE none", "SERVICE_PROCESS_FAILURE none\r\nSERVICE_UNKNOWN true")));
        Reject(() => Inspect(Rewrite(f, "SERVICE_SOCKET_CONTROLS {}\r\nSERVICE_FRAME_CONTROL {}", "SERVICE_FRAME_CONTROL {}\r\nSERVICE_SOCKET_CONTROLS {}")));
        Reject(() => Inspect(f with { Summary = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(f.Summary).Replace("SERVICE_PROCESS_ID 1234", "SERVICE_PROCESS_ID 1235", StringComparison.Ordinal)) }));
        Reject(() => Inspect(Rewrite(f, "SERVICE_SOCKET_CONTROLS {}", "SERVICE_SOCKET_CONTROLS {\"large\":\"" + new string('a', 2 * 1024 * 1024) + "\"}")));
        var exactBoundary = Rewrite(Rewrite(f, "SERVICE_PROCESS_END_UTC 2026-10-18T12:13:19.0000000Z", "SERVICE_PROCESS_END_UTC 2026-10-18T12:05:01.0000000Z"),
            "SERVICE_PROCESS_LIFETIME_MS 798000", "SERVICE_PROCESS_LIFETIME_MS 300001");
        Reject(() => Inspect(exactBoundary));
        Reject(() => Inspect(Rewrite(f, "TEST_PASS m16-012", "OS_SMOKE_PASS")));
    }

    [HostTest("service runner independently accepts complete serial above the unchanged ordinary16MiB limit")]
    private static void SerialCapacity()
    {
        var f = Fixture(paddingLines: 4096);
        HostCheck.True(f.Raw.Length > SerialMarkerStateMachine.DefaultByteCapacity);
        HostCheck.Equal(1234, Inspect(f).ProcessId);
        var ordinary = new SerialMarkerStateMachine(["TEST_PASS m16-012"], ["OS_SMOKE_FAIL"], successTerminalMarker: "TEST_PASS m16-012");
        HostCheck.Equal(SerialMarkerFeedStatus.TotalCapacityExceeded, ordinary.Feed(f.Raw).Status);
    }

    private sealed record Capture(QemuSmokeOptions Options, QemuSmokePlan Plan, ProcessResult Runner, byte[] Raw, byte[] Summary,
        QemuTranscripts.ServiceRunnerContext Context);

    private static Capture Fixture(int paddingLines = 0)
    {
        var options = new QemuSmokeOptions { TimeoutSeconds = 900, FirmwareRngSource = "virtio", LinuxThreadingApplication = "MinimalApiService" };
        var flags = EfiProfile.Create(null, new Dictionary<string, bool> { ["EnableM16014Probe"] = true }).Flags;
        var plan = new QemuSmokePlan("Diagnostic", flags, ["TEST_PASS m16-012"], ["TEST_PASS m16-012"], ["TEST_PASS m16-012"], ["OS_SMOKE_FAIL"], [], false, true);
        var started = new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);
        var context = new QemuTranscripts.ServiceRunnerContext(20808, Path.GetFullPath("mock-qemu.exe"), Path.GetFullPath("mock-ovmf.fd"), started, started.AddSeconds(800));
        string[] rawRows = ["TEST_START service", "OS_BOOT_OK", "TEST_PASS m16-012"];
        string[] capturedRows = [..Enumerable.Repeat(new string('x', 4094), paddingLines), ..rawRows];
        byte[] raw = Encoding.ASCII.GetBytes(string.Join("\r\n", capturedRows) + "\r\n");
        string[] selected = ["IMAGE_ARTIFACT " + options.Image, "SERIAL_LOG " + options.SerialLog, "WORKLOAD_PROFILE SingleMinimalApi",
            "FIRMWARE_RNG_SOURCE virtio", "SOCKET_FORWARD_PORT 20808", "CONTINUING_SERVICE true", "MARKER_PRESETS none", "REQUIRED_MARKERS TEST_PASS m16-012",
            "ORDERED_MARKERS TEST_PASS m16-012", "UNIQUE_MARKERS TEST_PASS m16-012", "FORBIDDEN_MARKERS OS_SMOKE_FAIL"];
        string[] stdout = ["QEMU_DISCOVERY result=" + context.Qemu + " checked=" + context.Qemu,
            "OVMF_DISCOVERY result=" + context.Ovmf + " checked=" + context.Ovmf, "TEST_START qemu.smoke", selected[0], selected[1], "OVMF_CODE " + context.Ovmf,
            ..selected.Skip(2), $"SERIAL_MARKER_STREAM status=Passed terminal=Success bytes={raw.Length} lines={capturedRows.Length} invalidBytes=0 invalidLines=0 orderedSeen=1 outOfOrder=0 uniqueSeen=1 duplicateUnique=0",
            "SERIAL_MARKER_DRAIN milliseconds=750", "QEMU_PROCESS_RESULT disposition=runner-terminated exitCode=-1", ..Block(), "SUMMARY_LOG " + options.SummaryLog, "TEST_PASS qemu.smoke"];
        string[] summary = ["QEMU_SMOKE_STATUS pass", ..selected, ..rawRows.Select(row => "OBSERVED_MARKER " + row), "APP_EXIT_RESULT none",
            "APP_EXIT_MALFORMED none", "MISSING_MARKERS none", "OUT_OF_ORDER_MARKERS none", "DUPLICATE_UNIQUE_MARKERS none", "FORBIDDEN_MARKER_HIT none",
            "FAIL_REASON none", "SERIAL_TAIL_LIMIT lines=8 chars=240", ..capturedRows.TakeLast(8).Select(row => "SERIAL_TAIL_LINE " + (row.Length > 240 ? row[..237] + "..." : row)), ..Block()];
        string[] backend = ["QEMU_ACCELERATOR " + options.Accelerator, "QEMU_MACHINE " + QemuContracts.MachineArgument(options.Accelerator)];
        stdout = [..backend, ..stdout];
        summary = [..summary.Take(4), ..backend, ..summary.Skip(4)];
        return new(options, plan, new(0, string.Join("\r\n", stdout) + "\r\n", "", false, false, false, TimeSpan.FromSeconds(800)), raw,
            [..Encoding.UTF8.GetPreamble(), ..Encoding.UTF8.GetBytes(string.Join("\r\n", summary) + "\r\n")], context);
    }

    private static string[] Block() => ["SERVICE_SOCKET_CONTROLS {}", "SERVICE_FRAME_CONTROL {}", "SERVICE_PROCESS_ID 1234",
        "SERVICE_PROCESS_START_UTC 2026-10-18T12:00:01.0000000Z", "SERVICE_PROCESS_END_UTC 2026-10-18T12:13:19.0000000Z",
        "SERVICE_PROCESS_LIFETIME_MS 798000", "SERVICE_ENDPOINT_PRELAUNCH_AVAILABLE true", "SERVICE_ENDPOINT_STOPPED_REFUSED true", "SERVICE_PROCESS_FAILURE none"];
    private static Capture Rewrite(Capture f, string before, string after) => f with {
        Runner = f.Runner with { StandardOutput = f.Runner.StandardOutput.Replace(before, after, StringComparison.Ordinal) },
        Summary = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(f.Summary).Replace(before, after, StringComparison.Ordinal)) };
    private static QemuTranscripts.ServiceRunnerInspection Inspect(Capture f) => QemuTranscripts.InspectNativeAotServiceRunnerForTests(f.Options, f.Plan, f.Runner, f.Raw, f.Summary, f.Context);
    private static void Reject(Action action)
    {
        bool refused = false; try { action(); } catch (ToolFailure) { refused = true; }
        HostCheck.True(refused);
    }
}
