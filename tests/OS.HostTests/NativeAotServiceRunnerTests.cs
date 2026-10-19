using System.Text;
using OS.Tooling;

internal static class NativeAotServiceRunnerTests
{
    [HostTest("service runner recomputes original serial and exact diagnostic summary with confirmed cleanup")]
    private static void Complete()
    {
        var f = Fixture(); Inspect(f); // Synthetic framing only; no parent/native/packet authority.
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
        foreach (Action<QemuSmokeOptions> alter in new Action<QemuSmokeOptions>[] { o => o.TimeoutSeconds = 779, o => o.TimeoutSeconds = 901,
            o => o.FirmwareRngSource = "none", o => o.WorkloadProfile = "other", o => o.LinuxThreadingApplication = "MinimalApiSmoke" })
        { var changed = Fixture(); alter(changed.Options); Reject(() => Inspect(changed)); }
    }

    private sealed record Capture(QemuSmokeOptions Options, QemuSmokePlan Plan, ProcessResult Runner, byte[] Raw, byte[] Summary,
        QemuTranscripts.ServiceRunnerContext Context);

    private static Capture Fixture()
    {
        var options = new QemuSmokeOptions { TimeoutSeconds = 900, FirmwareRngSource = "virtio", LinuxThreadingApplication = "MinimalApiService" };
        var flags = EfiProfile.Create(null, new Dictionary<string, bool> { ["EnableM16014Probe"] = true }).Flags;
        var plan = new QemuSmokePlan("Diagnostic", flags, ["OS_SMOKE_PASS"], ["OS_SMOKE_PASS"], ["OS_SMOKE_PASS"], ["OS_SMOKE_FAIL"], [], false, true);
        var context = new QemuTranscripts.ServiceRunnerContext(20808, Path.GetFullPath("mock-qemu.exe"), Path.GetFullPath("mock-ovmf.fd"));
        string[] rawRows = ["TEST_START service", "OS_BOOT_OK", "OS_SMOKE_PASS"];
        byte[] raw = Encoding.ASCII.GetBytes(string.Join("\r\n", rawRows) + "\r\n");
        string[] selected = ["IMAGE_ARTIFACT " + options.Image, "SERIAL_LOG " + options.SerialLog, "WORKLOAD_PROFILE SingleMinimalApi",
            "FIRMWARE_RNG_SOURCE virtio", "SOCKET_FORWARD_PORT 20808", "MARKER_PRESETS none", "REQUIRED_MARKERS OS_SMOKE_PASS",
            "ORDERED_MARKERS OS_SMOKE_PASS", "UNIQUE_MARKERS OS_SMOKE_PASS", "FORBIDDEN_MARKERS OS_SMOKE_FAIL"];
        string[] stdout = ["QEMU_DISCOVERY result=" + context.Qemu + " checked=" + context.Qemu,
            "OVMF_DISCOVERY result=" + context.Ovmf + " checked=" + context.Ovmf, "TEST_START qemu.smoke", selected[0], selected[1], "OVMF_CODE " + context.Ovmf,
            ..selected.Skip(2), $"SERIAL_MARKER_STREAM status=Passed terminal=Success bytes={raw.Length} lines=3 invalidBytes=0 invalidLines=0 orderedSeen=1 outOfOrder=0 uniqueSeen=1 duplicateUnique=0",
            "SERIAL_MARKER_DRAIN milliseconds=750", "QEMU_PROCESS_RESULT disposition=runner-terminated exitCode=-1", "SUMMARY_LOG " + options.SummaryLog, "TEST_PASS qemu.smoke"];
        string[] summary = ["QEMU_SMOKE_STATUS pass", ..selected, ..rawRows.Select(row => "OBSERVED_MARKER " + row), "APP_EXIT_RESULT none",
            "APP_EXIT_MALFORMED none", "MISSING_MARKERS none", "OUT_OF_ORDER_MARKERS none", "DUPLICATE_UNIQUE_MARKERS none", "FORBIDDEN_MARKER_HIT none",
            "FAIL_REASON none", "SERIAL_TAIL_LIMIT lines=8 chars=240", ..rawRows.Select(row => "SERIAL_TAIL_LINE " + row)];
        return new(options, plan, new(0, string.Join("\r\n", stdout) + "\r\n", "", false, false, false, TimeSpan.FromSeconds(800)), raw,
            [..Encoding.UTF8.GetPreamble(), ..Encoding.UTF8.GetBytes(string.Join("\r\n", summary) + "\r\n")], context);
    }

    private static void Inspect(Capture f) => QemuTranscripts.InspectNativeAotServiceRunnerForTests(f.Options, f.Plan, f.Runner, f.Raw, f.Summary, f.Context);
    private static void Reject(Action action)
    {
        bool refused = false; try { action(); } catch (ToolFailure) { refused = true; }
        HostCheck.True(refused);
    }
}
