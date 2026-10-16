using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Tooling;

internal static unsafe class LinuxSocketEfiProfileTests
{
    [HostTest("socket EFI leaf resolves the NIC and native runtime union without relaxing other profiles")]
    private static void Profile()
    {
        foreach (bool build in new[] { false, true })
        foreach (string leaf in new[] { "EnableM16011Probe", "EnableM16012Probe" })
        {
            var profile = EfiOptions.Parse(["--" + leaf], build).Profile();
            HostCheck.Equal("Diagnostic", profile.BootPolicy);
            HostCheck.Equal(leaf == "EnableM16012Probe" ? 22 : 21, profile.Flags.Count(x => x.Value));
            foreach (string parent in new[] { "M07026", "M16009", "M16008", "M16007", "M16006", "M16005", "M16004",
                "M16002", "M16003", "M05ApicSetup", "M05VirtqueuePolling", "M07ArpReceive" })
                HostCheck.True(profile.IsEnabled(parent), parent);
            foreach (string parent in profile.Flags.Where(x => x.Value && x.Key != leaf).Select(x => x.Key))
                Reject(() => EfiOptions.Parse(["--" + leaf, "--" + parent, "false"], build).Profile());
            foreach (string extra in new[] { "EnableM05VirtqueueInterruptProbe", "EnableM05BarDmaProbe", "EnableM09006Probe" })
                Reject(() => EfiOptions.Parse(["--" + leaf, "--" + extra], build).Profile());
            Reject(() => EfiOptions.Parse(["--" + leaf, "--BootPolicy", "VerifiedExit"], build).Profile());
        }
        Reject(() => EfiOptions.Parse(["--EnableM07026Probe", "--EnableM16009Probe"], true).Profile());
        Reject(() => EfiOptions.Parse(["--EnableM05ApicSetupProbe", "--EnableM05VirtqueuePollingProbe"], true).Profile());
    }

    [HostTest("socket EFI default-off boot gate never invokes a callback")]
    private static void DefaultGate()
    {
        int calls = 0;
        HostCheck.True(!Probes.EnableM16011Probe && !Probes.EnableM16012Probe);
        HostCheck.True(!BootSequence.RunOwnedLinuxSockets(true, true, true, true, true, &calls, &Execute) && calls == 0);
    }

    [HostTest("socket EFI cannot inherit the older scripted TCP guest package or transcript")]
    private static void GuestRunnerBoundary()
    {
        foreach (string leaf in new[] { "EnableM16011Probe", "EnableM16012Probe" })
        {
            string? reason = null;
            try { _ = QemuProfiles.Create(QemuSmokeOptions.Parse(["--BuildEfi", "--" + leaf])); }
            catch (ToolFailure failure) { reason = failure.Reason; }
            HostCheck.Equal("socket_guest_orchestration_pending", reason);
        }
    }

    [HostTest("socket EFI sources retain canonical no-stdlib project admission")]
    private static void SourceAdmission()
    {
        var result = EfiProjectAdmission.InspectStatic(HostToolProcess.FindRepositoryRoot());
        HostCheck.True(result.Succeeded, result.Reason + " " + result.Detail);
    }

    private static bool Execute(void* context) { (*(int*)context)++; return true; }
    private static void Reject(Action action)
    {
        try { action(); } catch (ToolFailure) { return; }
        throw new InvalidOperationException("socket_efi_invalid_profile_accepted");
    }
}
