using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Tooling;

internal static unsafe class M07026EfiIntegrationTests
{
    [HostTest("m07-026 profile defaults off and admits only the complete service and ARP chain", HostLane.Fast, 0)]
    private static void Profile()
    {
        int calls = 0;
        HostCheck.True(!BootSequence.PrepareNetworkService(true, &calls, &Execute) && calls == 0);
        HostCheck.True(!Probes.EnableM07026Probe && !BootSequence.RunOwnedNetworkService(true, true, true, &calls, &Execute) && calls == 0);
        foreach (bool build in new[] { false, true })
        {
            var profile = EfiOptions.Parse(["--EnableM07026Probe"], build).Profile();
            HostCheck.Equal("Diagnostic", profile.BootPolicy);
            HostCheck.Equal(13, profile.Flags.Count(x => x.Value));
            foreach (var parent in profile.Flags.Where(x => x.Value && x.Key != "EnableM07026Probe"))
                Reject(() => EfiOptions.Parse(["--EnableM07026Probe", "--" + parent.Key, "false"], build).Profile());
            foreach (string extra in new[] { "M05ApicSetup", "M16004", "M16008", "M09006", "M04SimpleThrowCatch" })
                Reject(() => EfiOptions.Parse(["--EnableM07026Probe", "--Enable" + extra + "Probe"], build).Profile());
            Reject(() => EfiOptions.Parse(["--EnableM07026Probe", "--BootPolicy", "VerifiedExit"], build).Profile());
        }
        var plan = QemuProfiles.Create(QemuSmokeOptions.Parse("--BuildEfi", "--EnableM07026Probe", "--RequireMarker", "CLROS_M07_026_READY"));
        HostCheck.True(plan.Required.Contains("CLROS_M07_026_READY"));
        HostCheck.True(plan.Ordered.SequenceEqual(QemuTranscripts.M07026Markers.Select(x => x.Trim())));
        Reject(() => QemuProfiles.Create(QemuSmokeOptions.Parse("--BuildEfi", "--EnableM07026Probe", "--RequireMarker", "CLROS_M16_003_READY")));
    }
    private static bool Execute(void* context) { (*(int*)context)++; return true; }
    private static void Reject(Action action)
    {
        try { action(); } catch (ToolFailure) { return; }
        throw new InvalidOperationException("m07_026_invalid_profile_accepted");
    }

    [HostTest("m07-026 runner emits the original MAC independent capture and complete protocol", HostLane.Fast, 1)]
    private static void Runner()
    {
        var result = HostToolProcess.RunTool(["qemu", "smoke", "--DryRun", "--BuildEfi", "--EnableM07026Probe"]);
        HostCheck.True(result.ExitCode == 0, result.Error + result.Output);
        string markers = string.Join(';', QemuTranscripts.M07026Markers.Select(x => x.Trim()));
        foreach (string prefix in new[] { "REQUIRED_MARKERS ", "ORDERED_MARKERS ", "UNIQUE_MARKERS " })
            HostCheck.True(result.Output.Contains(prefix + markers, StringComparison.Ordinal), prefix);
        HostCheck.True(result.Output.Contains(",mac=02:00:00:00:00:01", StringComparison.Ordinal) &&
            result.Output.Contains("filter-dump,id=clroscapture,netdev=n0,file=", StringComparison.Ordinal));
    }
}
