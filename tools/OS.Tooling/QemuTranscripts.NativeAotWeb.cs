namespace OS.Tooling;

public static partial class QemuTranscripts
{
    internal static IReadOnlyList<string> WebMarkers() => [
        ..SocketMarkers(true).TakeWhile(x => x != "TEST_START m16-011"),
        "TEST_START m10.004",..M16LinuxSignalMarkers.Take(3),"M16_011_BOUND ","M16_005_ARMED","M16_008_BOUND","M16_008_BUDGET",
        "M10_004_ENTER","M10_004_CONFIG","M10_004_LISTEN","M10_004_LIVE","CLROS_M10_004_READY","TEST_PASS m10.004"];

    internal static int ValidateWebRun(string root,QemuSmokeOptions options,QemuSmokePlan plan,ProcessResult runner,
        TextWriter output,NativeAotWebProgram program)
    {
        RequireCompleteProcess(runner,"web_runner_incomplete");
        if(runner.ExitCode!=0)return runner.ExitCode;
        Need(plan.IsEnabled("EnableM10004Probe") && program is not null &&
            string.Equals(Path.GetFullPath(root),program.SourceRoot,OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal),
            "web_prepared_owner");
        // A live HTTP exchange alone cannot pass the full smoke gate. The finite
        // runtime parser requires guest exit/reap and must never be given invented
        // completion rows for this continuing profile. Keep acceptance closed
        // until the independent continuing transcript/native/packet join exists.
        output.WriteLine("TEST_FAIL qemu.smoke web_continuing_evidence_admission_pending");
        return 1;
    }
}
