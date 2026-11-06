using OS.Tooling;

internal static class NativeAotDiskStaticProfileTests
{
    [HostTest("actual project XML uses the same bounded reader and source admission as native publication")]
    private static void ProjectAdmission()
    {
        var result = EfiProjectAdmission.InspectStatic(HostToolProcess.FindRepositoryRoot());
        HostCheck.True(result.Succeeded, result.Reason + " " + result.Detail);
    }
    [HostTest("short timing diagnostic is explicit default off and retains full parents with bounded capture")]
    private static void Timing()
    {
        HostCheck.True(!OS.Kernel.Diagnostics.Probes.EnableM10018TimingProbe);
        var plan = QemuProfiles.Create(QemuSmokeOptions.Parse([.. Arguments, "-EnableM10018TimingProbe"]));
        HostCheck.True(NativeAotDiskSelection.TimingSelected(plan.Flags) && plan.IsEnabled("EnableM10018FullProbe"));
        HostCheck.True(plan.Flags.Where(p => p.Key.StartsWith("EnableM04", StringComparison.Ordinal)).All(p => !p.Value));
        HostCheck.Equal(2048U, OS.Kernel.Runtime.X64PERuntimeFunctionUnwindMetadataHostileInput.MaximumFunctions);
        foreach (string[] suffix in new[]{new[]{"-EnableM10018FullProbe:false"},new[]{"-EnableM10018Probe:false"},
            new[]{"-TimeoutSeconds","239"},new[]{"-TimeoutSeconds","601"},new[]{"-EnableM10017Probe"},
            new[]{"-EnableM04X64UnwindMetadataProbe"}})
        {
            bool rejected = false;
            try
            {
                _ = QemuProfiles.Create(QemuSmokeOptions.Parse([.. Arguments, "-EnableM10018TimingProbe", .. suffix]));
            }
            catch (ToolFailure)
            {
                rejected = true;
            }
            HostCheck.True(rejected, string.Join(' ', suffix));
        }
    }
    private static string[] Arguments => ["-BuildEfi", "-EnableM10018Probe", "-LinuxElfPackage", "artifacts/disk-static.pkg",
        "-LinuxThreadingDisassembly", "artifacts/disk-static.dump", "-WebRootFixture", "DiskA", "-WebRootAssets", "artifacts/webroot"];

    [HostTest("disk static profile binds original indexed server with distinct disk and app owners")]
    private static void Plan()
    {
        var options = QemuSmokeOptions.Parse(Arguments);
        var plan = QemuProfiles.Create(options);
        HostCheck.True(NativeAotDiskSelection.StaticSelected(plan.Flags));
        HostCheck.Equal("MinimalApiStaticFiles", options.LinuxThreadingApplication);
        HostCheck.Equal(825, options.TimeoutSeconds);
        foreach (string key in new[] { "M10004", "M16025", "M16024", "M16023", "M16012", "M16008" })
            HostCheck.True(plan.IsEnabled("Enable" + key + "Probe"));
        HostCheck.True(!plan.IsEnabled("EnableM10017Probe"));
        HostCheck.Equal("", NativeAotWebSelection.Application(plan.Flags));
        foreach (string marker in new[] { "M10_018_LISTENING", "M10_018_DISK_READY", "TEST_PASS m10.018", "TEST_PASS m16.025" })
            HostCheck.True(plan.Required.Contains(marker));
        HostCheck.True(!plan.Required.Contains("CLROS_M10_017_READY"));
        string[] inputs = EfiProjectAdmission.ExpectedCompileItems(EfiProfile.Create("", new Dictionary<string, bool> { ["EnableM10018Probe"] = true }));
        HostCheck.True(inputs.Contains(@"Boot\EfiEntry.StaticFiles.cs|") && inputs.Contains(@"Boot\EfiEntry.DiskNative.cs|"));
    }

    [HostTest("disk static profile rejects missing index wrong server controls and capture budgets")]
    private static void Rejections()
    {
        foreach (string[] suffix in new[]
        {
            new[] { "-LinuxThreadingApplication", "MinimalApiSmoke" }, new[] { "-TimeoutSeconds", "599" },
            new[] { "-TimeoutSeconds", "826" }, new[] { "-WebRootFixture", "Staged" },
            new[] { "-EnableM10017Probe" }, new[] { "-EnableM16024Probe:false" }, new[] { "-WebSignalRace" }
        })
        {
            bool rejected = false;
            try
            {
                _ = QemuProfiles.Create(QemuSmokeOptions.Parse([.. Arguments, .. suffix]));
            }
            catch (ToolFailure)
            {
                rejected = true;
            }
            HostCheck.True(rejected, string.Join(' ', suffix));
        }
        var missing = QemuSmokeOptions.Parse(Arguments);
        missing.LinuxThreadingDisassembly = "";
        bool denied = false;
        try
        {
            _ = QemuProfiles.Create(missing);
        }
        catch (ToolFailure)
        {
            denied = true;
        }
        HostCheck.True(denied);
    }

    [HostTest("disk static dry run keeps read-only slot7 forwarded NIC6 and distinct terminal")]
    private static void Runner()
    {
        var run = HostToolProcess.RunTool(["qemu", "smoke", "-DryRun", "-Qemu", "qemu-test", "-OvmfCode", "ovmf-test", .. Arguments]);
        HostCheck.True(run.ExitCode == 0, run.Output);
        foreach (string value in new[] { "virtio-blk-pci,drive=webroot,addr=07.0", "readonly=on", "hostfwd=tcp:127.0.0.1:20080-:8080", "CLROS_M10_018_READY" })
            HostCheck.True(run.Output.Contains(value, StringComparison.Ordinal), value);
        string forbidden = run.Output.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n')
            .Single(line => line.StartsWith("FORBIDDEN_MARKERS ", StringComparison.Ordinal));
        HostCheck.True(forbidden["FORBIDDEN_MARKERS ".Length..].Split(';').Contains("CLROS_M10_017_READY"));
    }

    [HostTest("timing dry run reaches actual runner at both deadline boundaries without creating outputs")]
    private static void TimingRunner()
    {
        foreach (string timeout in new[] { "240", "600" })
        {
            string path = Path.Combine(Path.GetTempPath(), "clros-disk-timing-" + Guid.NewGuid().ToString("N"));
            var run = HostToolProcess.RunTool(["qemu","smoke","-DryRun","-Qemu","qemu-test","-OvmfCode","ovmf-test",
                ..Arguments,"-EnableM10018TimingProbe","-TimeoutSeconds",timeout,
                "-Image",path+".raw","-SerialLog",path+".serial","-SummaryLog",path+".summary"]);
            HostCheck.True(run.ExitCode == 0, run.Output + run.Error);
            foreach (string value in new[]{"TEST_PASS qemu.runner.dryrun","DISK_READ_TIMING true",
                "virtio-blk-pci,drive=webroot,addr=07.0","readonly=on","addr=06.0",
                "hostfwd=tcp:127.0.0.1:20080-:8080","-rtc base=utc,clock=host",
                "filter-dump,id=clroscapture","CLROS_M10_018_READY","TEST_PASS m16-012"})
                HostCheck.True(run.Output.Contains(value, StringComparison.Ordinal), value);
            foreach (string suffix in new[] { ".raw", ".serial", ".summary", ".serial.pcap" })
                HostCheck.True(!Path.Exists(path + suffix), suffix);
        }
        foreach (string[] args in new[] { Arguments.Where(value => value != "-BuildEfi").ToArray(), [.. Arguments, "-SkipImageBuild"] })
        {
            var denied = HostToolProcess.RunTool(["qemu", "smoke", "-DryRun", .. args, "-EnableM10018TimingProbe"]);
            HostCheck.True(denied.ExitCode != 0 && !denied.Output.Contains("TEST_PASS qemu.runner.dryrun", StringComparison.Ordinal), denied.Output + denied.Error);
        }
    }

    [HostTest("actual runner rejects timing parent deadline and duplicate errors while preserving full deadlines")]
    private static async Task TimingRunnerRejections()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string path = Path.Combine(Path.GetTempPath(), "clros-disk-timing-cli-" + Guid.NewGuid().ToString("N"));
        string[] parent = ["--dry-run","--qemu","qemu-test","--image",path+".raw","--serial-log",path+".serial",
            "--summary-log",path+".summary","--virtio-negotiation-device","--arp-receive-fixture",
            "--ethernet-capture",path+".pcap","--socket-forward-port","20880","--workload-profile","SingleMinimalApi",
            "--firmware-rng-source","virtio","--replace-default-required-markers","--finite-static-files",
            "--readonly-webroot-disk","--full-disk-static-files"];
        foreach (string missing in new[] { "--full-disk-static-files", "--readonly-webroot-disk", "--finite-static-files" })
            await Reject([.. parent.Where(value => value != missing), "--disk-read-timing", "--timeout", "600"]);
        await Reject([.. parent, "--disk-read-timing", "--disk-read-timing", "--timeout", "600"]);
        foreach (string timeout in new[] { "239", "601" })
            await Reject([.. parent, "--disk-read-timing", "--timeout", timeout]);
        await Reject([.. parent, "--timeout", "600"]);
        foreach (string timeout in new[] { "6600", "7200" })
        {
            var run = await Run([.. parent, "--timeout", timeout]);
            HostCheck.True(run.ExitCode == 0 && run.StandardOutput.Contains("TEST_PASS qemu.runner.dryrun", StringComparison.Ordinal), run.StandardOutput + run.StandardError);
            HostCheck.True(!run.StandardOutput.Contains("DISK_READ_TIMING", StringComparison.Ordinal));
        }
        foreach (string suffix in new[] { ".raw", ".serial", ".summary", ".pcap" })
            HostCheck.True(!Path.Exists(path + suffix));

        Task<ProcessResult> Run(string[] args) => ProcessRunner.RunAsync(new("dotnet", [HostTools.ManagedTool(root, "OS.QemuRunner"), .. args], root)
        {
            Timeout = TimeSpan.FromSeconds(20)
        });
        async Task Reject(string[] args)
        {
            var run = await Run(args);
            HostCheck.True(run.ExitCode == 1 && run.StandardError.Contains("TEST_FAIL qemu.runner.invalid_args", StringComparison.Ordinal), run.StandardOutput + run.StandardError);
            HostCheck.True(!run.StandardOutput.Contains("TEST_PASS qemu.runner.dryrun", StringComparison.Ordinal));
        }
    }
}
