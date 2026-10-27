using OS.Tooling;

internal static class ReadOnlyDiskProfileTests
{
    private static string[] Arguments => ["-BuildEfi", "-EnableM16025Probe", "-LinuxElfPackage", "artifacts/disk-files.pkg",
        "-WebRootFixture", "DiskA", "-WebRootAssets", "artifacts/published-webroot"];

    [HostTest("disk child closes block filesystem scheduler and socket prerequisites", HostLane.Fast, 0)]
    private static void Prerequisites()
    {
        var options = QemuSmokeOptions.Parse(Arguments);
        var plan = QemuProfiles.Create(options);
        foreach (string name in new[] { "M16023", "M16024", "M16025", "M16012", "M16011", "M16009", "M16008", "M16007", "M07026" })
            HostCheck.True(plan.IsEnabled("Enable" + name + "Probe"));
        HostCheck.True(!plan.IsEnabled("EnableM10017Probe") && !plan.IsEnabled("EnableM10018Probe"));
        HostCheck.Equal("", NativeAotSocketSelection.Application(plan.Flags));
        HostCheck.Equal("", NativeAotWebSelection.Application(plan.Flags));
        HostCheck.Equal(180, options.TimeoutSeconds);
        HostCheck.True(plan.Required.Contains("TEST_PASS m16.025"));
        HostCheck.True(plan.Required.Contains("CLROS_M16_025_READY"));
        HostCheck.True(!plan.Required.Contains("M10_017_LISTENING"));
    }

    [HostTest("disk child refuses missing denied and conflicting ownership prerequisites", HostLane.Fast, 1)]
    private static void Rejections()
    {
        foreach (string parent in new[] { "M16023", "M16024", "M16012", "M16011", "M16008" })
            Reject([..Arguments, "-Enable" + parent + "Probe:false"]);
        foreach (string conflict in new[] { "M10017", "M16014", "M16013", "M07027", "M09011" })
            Reject([..Arguments, "-Enable" + conflict + "Probe"]);
        Reject(["-EnableM16023Probe"]);
        Reject(["-EnableM16024Probe"]);
        Reject(["-EnableM16025Probe"]);
        Reject([..Arguments, "-TimeoutSeconds", "119"]);
        Reject([..Arguments, "-TimeoutSeconds", "286"]);
        Reject([..Arguments, "-LinuxThreadingApplication", "NativeAotSocketSmoke"]);
        Reject([..Arguments, "-LinuxThreadingDisassembly", "untrusted.txt"]);
        Reject(["-WebRootFixture", "DiskA", "-WebRootAssets", "artifacts/source"]);
    }

    [HostTest("disk profile compiles only its explicit conditional source additions", HostLane.Fast, 2)]
    private static void CompileInventory()
    {
        foreach (var (flag, symbol) in new[] { ("M16023", "M16_023_PROBE"), ("M16024", "M16_024_PROBE"),
            ("M16025", "M16_025_PROBE"), ("M10018", "M10_018_PROBE") })
            HostCheck.Equal(symbol, EfiContracts.ProbeSymbols["Enable" + flag + "Probe"]);
        var disk = EfiProfile.Create("", new Dictionary<string, bool> { ["EnableM16025Probe"] = true });
        string[] items = EfiProjectAdmission.ExpectedCompileItems(disk);
        HostCheck.True(items.Contains(@"Boot\EfiEntry.DiskNative.cs|"));
        HostCheck.True(items.Contains(@"..\OS.Kernel\Storage\ContinuingReadOnlyBlockService.cs|Storage\ContinuingReadOnlyBlockService.cs"));
        HostCheck.True(items.Contains(@"..\OS.Kernel\Compatibility\LinuxX64\LinuxThreadServices.Disk.cs|Compatibility\LinuxX64\LinuxThreadServices.Disk.cs"));
        HostCheck.True(!items.Contains(@"Boot\EfiEntry.StaticFiles.cs|"));
        var original = EfiProfile.Create("", new Dictionary<string, bool> { ["EnableM10017Probe"] = true });
        string[] oldItems = EfiProjectAdmission.ExpectedCompileItems(original);
        HostCheck.True(oldItems.Contains(@"Boot\EfiEntry.StaticFiles.cs|"));
        HostCheck.True(!oldItems.Contains(@"Boot\EfiEntry.DiskNative.cs|"));
    }

    [HostTest("disk runner keeps NIC slot6 and a distinct read-only slot7 image", HostLane.Fast, 3)]
    private static void RunnerDeviceArguments()
    {
        var run = HostToolProcess.RunTool(["qemu", "smoke", "-DryRun", "-Qemu", "qemu-test", "-OvmfCode", "ovmf-test", ..Arguments]);
        HostCheck.True(run.ExitCode == 0, run.Output);
        HostCheck.True(run.Output.Contains("virtio-net-pci,netdev=n0,addr=06.0,disable-legacy=on,romfile=", StringComparison.Ordinal));
        HostCheck.True(run.Output.Contains("virtio-blk-pci,drive=webroot,addr=07.0,disable-legacy=on,romfile=", StringComparison.Ordinal));
        HostCheck.True(run.Output.Contains("if=none,id=webroot,format=raw,readonly=on,file=", StringComparison.Ordinal));
        HostCheck.True(run.Output.Contains("if=none,id=esp,format=raw,snapshot=on,file=", StringComparison.Ordinal));
        HostCheck.True(!run.Output.Contains("drive=webroot,addr=06.0", StringComparison.Ordinal));
        HostCheck.True(!run.Output.Contains("--continuing-network-peer", StringComparison.Ordinal));
    }

    private static void Reject(string[] arguments)
    {
        bool rejected = false;
        try { _ = QemuProfiles.Create(QemuSmokeOptions.Parse(arguments)); }
        catch (ToolFailure) { rejected = true; }
        HostCheck.True(rejected);
    }

    [HostTest("disk static source admission requires every new reviewed conditional input and rejects mutations", HostLane.Fast, 4)]
    private static void SourceAdmission()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        var project = EfiProjectAdmission.InspectStatic(root);
        HostCheck.True(project.Succeeded, project.Reason + ":" + project.Detail);
        string[] sources = [@"Boot\EfiEntry.DiskNative.cs", @"Boot\EfiEntry.DiskBootstrap.cs", @"Boot\EfiEntry.DiskEvidence.cs",
            @"..\OS.Kernel\Storage\ContinuingReadOnlyBlockService.cs", @"..\OS.Kernel\Storage\ReadOnlyWebRootFileSystem.cs",
            @"..\OS.Kernel\Compatibility\LinuxX64\LinuxDescriptorServices.Disk.cs", @"..\OS.Kernel\Compatibility\LinuxX64\LinuxThreadServices.Disk.cs"];
        foreach (string source in sources)
        {
            HostCheck.True(EfiContracts.SourceAdmissionReasons.ContainsKey(source) && EfiContracts.SourceIdentities.ContainsKey(source));
            string text = File.ReadAllText(Path.GetFullPath(Path.Combine(root, "src", "OS", source.Replace('\\', Path.DirectorySeparatorChar))));
            HostCheck.True(EfiSourceAdmission.Inspect(text, source).Succeeded);
            var changed = EfiSourceAdmission.Inspect(text + "\n// unreviewed source mutation\n", source);
            HostCheck.True(!changed.Succeeded && changed.Reason == "forbidden_active_source");
        }
    }
}
