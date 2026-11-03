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
            try
            {
                _ = QemuProfiles.Create(QemuSmokeOptions.Parse(["--BuildEfi", "--" + leaf]));
            }
            catch (ToolFailure failure) { reason = failure.Reason; }
            HostCheck.Equal("socket_disassembly_required", reason);
        }
    }

    [HostTest("socket runner binds exact mode entropy final teardown markers and complete runtime drain budget")]
    private static void GuestPlan()
    {
        foreach (bool asynchronous in new[] { false, true })
        {
            string leaf = asynchronous ? "M16012" : "M16011";
            string[] args = ["--BuildEfi", "--Enable" + leaf + "Probe", "--LinuxThreadingDisassembly", "original-llvm.txt", "--LinuxElfPackage", "original.pkg", "--DryRun"];
            var options = QemuSmokeOptions.Parse(args);
            var plan = QemuProfiles.Create(options);
            HostCheck.Equal(asynchronous ? "NativeAotSocketSmokeAsync" : "NativeAotSocketSmoke", options.LinuxThreadingApplication);
            HostCheck.True(options.TimeoutSeconds == 600 && options.FirmwareRngSource == "virtio");
            HostCheck.Equal("TEST_PASS m16-" + (asynchronous ? "012" : "011"), plan.Ordered[^1]);
            HostCheck.True(plan.Ordered.Contains("M16_011_BOUND") && plan.Unique.Contains("M16_011_PEER"));
            foreach (string[] extra in new string[][]{
                ["--LinuxThreadingApplication",asynchronous?"NativeAotSocketSmoke":"NativeAotSocketSmokeAsync"],
                ["--TimeoutSeconds","599"],["--LinuxElfImageVariant","DigestFault"],
                ["--LinuxHelloDisassembly","hello.txt"],["--RequireMarker","CLROS_M07_026_READY"]})
                Reject(() => QemuProfiles.Create(QemuSmokeOptions.Parse([.. args, .. extra])));
            List<string>? runner = null;
            int result = QemuSmoke.RunAsync(HostToolProcess.FindRepositoryRoot(), options, TextWriter.Null, (name, arguments) =>
            {
                HostCheck.Equal("OS.QemuRunner", name);
                runner = arguments.ToList();
                return Task.FromResult(new ProcessResult(0, "", "", false, false, false, TimeSpan.Zero));
            }).GetAwaiter().GetResult();
            HostCheck.True(result == 0 && runner is not null && !runner.Contains("--continuing-network-peer") && runner.Contains("--ethernet-capture"));
            int port = runner!.IndexOf("--socket-forward-port");
            HostCheck.True(port >= 0 && runner[port + 1] == "20080");
            HostCheck.True(runner.Sum(value => value.Length + 3) < 30000, "socket runner command line remains bounded");
            string root = HostToolProcess.FindRepositoryRoot();
            var direct = ProcessRunner.RunAsync(new("dotnet", [HostTools.ManagedTool(root, "OS.QemuRunner"), .. runner], root)
            {
                Timeout = TimeSpan.FromSeconds(30),
                OutputCharacterLimit = 128 * 1024
            }, CancellationToken.None).GetAwaiter().GetResult();
            HostCheck.True(direct.Succeeded, direct.FailureReason + direct.StandardOutput + direct.StandardError);
            HostCheck.True(direct.StandardOutput.Contains("hostfwd=tcp:127.0.0.1:20080-:8080", StringComparison.Ordinal));
            HostCheck.True(direct.StandardOutput.Contains("REQUIRED_MARKERS " + string.Join(';', plan.Required), StringComparison.Ordinal));
        }
    }

    [HostTest("socket EFI sources retain canonical no-stdlib project admission")]
    private static void SourceAdmission()
    {
        var result = EfiProjectAdmission.InspectStatic(HostToolProcess.FindRepositoryRoot());
        HostCheck.True(result.Succeeded, result.Reason + " " + result.Detail);
    }

    private static bool Execute(void* context)
    {
        (*(int*)context)++;
        return true;
    }
    private static void Reject(Action action)
    {
        try
        {
            action();
        }
        catch (ToolFailure) { return; }
        throw new InvalidOperationException("socket_efi_invalid_profile_accepted");
    }
}
