using OS.Tooling;

internal static class NativeAotDiskStaticProfileTests
{
    [HostTest("actual project XML uses the same bounded reader and source admission as native publication")]
    private static void ProjectAdmission()
    {
        var result=EfiProjectAdmission.InspectStatic(HostToolProcess.FindRepositoryRoot());
        HostCheck.True(result.Succeeded,result.Reason+" "+result.Detail);
    }
    [HostTest("short timing diagnostic is explicit default off and retains full parents with bounded capture")]
    private static void Timing()
    {
        HostCheck.True(!OS.Kernel.Diagnostics.Probes.EnableM10018TimingProbe);
        var plan=QemuProfiles.Create(QemuSmokeOptions.Parse([..Arguments,"-EnableM10018TimingProbe"]));
        HostCheck.True(NativeAotDiskSelection.TimingSelected(plan.Flags) && plan.IsEnabled("EnableM10018FullProbe"));
        foreach(string[] suffix in new[]{new[]{"-EnableM10018FullProbe:false"},new[]{"-EnableM10018Probe:false"},
            new[]{"-TimeoutSeconds","239"},new[]{"-TimeoutSeconds","601"},new[]{"-EnableM10017Probe"}})
        {
            bool rejected=false;
            try{_=QemuProfiles.Create(QemuSmokeOptions.Parse([..Arguments,"-EnableM10018TimingProbe",..suffix]));}
            catch(ToolFailure){rejected=true;}
            HostCheck.True(rejected,string.Join(' ',suffix));
        }
    }
    private static string[] Arguments => ["-BuildEfi", "-EnableM10018Probe", "-LinuxElfPackage", "artifacts/disk-static.pkg",
        "-LinuxThreadingDisassembly", "artifacts/disk-static.dump", "-WebRootFixture", "DiskA", "-WebRootAssets", "artifacts/webroot"];

    [HostTest("disk static profile binds original indexed server with distinct disk and app owners")]
    private static void Plan()
    {
        var options = QemuSmokeOptions.Parse(Arguments); var plan = QemuProfiles.Create(options);
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
            try { _ = QemuProfiles.Create(QemuSmokeOptions.Parse([..Arguments, ..suffix])); }
            catch (ToolFailure) { rejected = true; }
            HostCheck.True(rejected, string.Join(' ', suffix));
        }
        var missing = QemuSmokeOptions.Parse(Arguments); missing.LinuxThreadingDisassembly = "";
        bool denied = false; try { _ = QemuProfiles.Create(missing); } catch (ToolFailure) { denied = true; }
        HostCheck.True(denied);
    }

    [HostTest("disk static dry run keeps read-only slot7 forwarded NIC6 and distinct terminal")]
    private static void Runner()
    {
        var run = HostToolProcess.RunTool(["qemu", "smoke", "-DryRun", "-Qemu", "qemu-test", "-OvmfCode", "ovmf-test", ..Arguments]);
        HostCheck.True(run.ExitCode == 0, run.Output);
        foreach (string value in new[] { "virtio-blk-pci,drive=webroot,addr=07.0", "readonly=on", "hostfwd=tcp:127.0.0.1:20080-:8080", "CLROS_M10_018_READY" })
            HostCheck.True(run.Output.Contains(value, StringComparison.Ordinal), value);
        string forbidden = run.Output.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n')
            .Single(line => line.StartsWith("FORBIDDEN_MARKERS ", StringComparison.Ordinal));
        HostCheck.True(forbidden["FORBIDDEN_MARKERS ".Length..].Split(';').Contains("CLROS_M10_017_READY"));
    }
}
