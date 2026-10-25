using OS.Tooling;

internal static class NativeAotWritableDiskSelectionTests
{
    private static string[] Arguments(string fixture) => ["-BuildEfi", "-EnableM16027Probe", "-WritableDiskFixture", fixture,
        "-LinuxElfPackage", "artifacts/writable.pkg", "-WebRootFixture", "DiskA", "-WebRootAssets", "artifacts/published-webroot"];

    [HostTest("writable profile binds explicit canonical30 and40 packages", HostLane.Fast, 0)]
    private static void CanonicalPackages()
    {
        foreach (string fixture in new[] { "Mechanical30", "LogUpload40" })
        {
            var options = QemuSmokeOptions.Parse(Arguments(fixture));
            var plan = QemuProfiles.Create(options);
            HostCheck.True(NativeAotDiskSelection.WritableSelected(plan.Flags));
            HostCheck.Equal(180, options.TimeoutSeconds);
            HostCheck.True(plan.Required.Contains("CLROS_M16_027_READY") && plan.Required.Contains("M16_027_STOP"));
            HostCheck.True(!plan.Required.Contains("CLROS_M16_025_READY") && !plan.Required.Contains("CLROS_M10_018_READY"));
            foreach (string parent in new[] { "M16025", "M16024", "M16023", "M16012", "M16011", "M16008", "M07026" })
                HostCheck.True(plan.IsEnabled("Enable" + parent + "Probe"));
            byte[] package = fixture == "Mechanical30" ? LinuxWritableDiskProofFixture.CreatePackage() : LinuxWritableDiskProofFixture.CreateLogUploadPackage();
            NativeAotDiskSelection.RequireWritablePackage(package, fixture);
            Reject(() => NativeAotDiskSelection.RequireWritablePackage(package, fixture == "Mechanical30" ? "LogUpload40" : "Mechanical30"));
            package[^1] ^= 1;
            Reject(() => NativeAotDiskSelection.RequireWritablePackage(package, fixture));
        }
        Reject(() => NativeAotDiskSelection.RequireWritablePackage(LinuxDiskFileProofFixture.CreatePackage(), "Mechanical30"));
    }

    [HostTest("writable selection rejects missing explicit fixtures and incompatible profiles", HostLane.Fast, 1)]
    private static void Rejections()
    {
        Reject(() => QemuProfiles.Create(QemuSmokeOptions.Parse(["-BuildEfi", "-EnableM16027Probe"])));
        Reject(() => QemuProfiles.Create(QemuSmokeOptions.Parse(["-BuildEfi", "-WritableDiskFixture", "Mechanical30"])));
        foreach (string parent in new[] { "M16025", "M16024", "M16023", "M16012", "M16011", "M16008" })
            Reject(() => QemuProfiles.Create(QemuSmokeOptions.Parse([..Arguments("Mechanical30"), "-Enable" + parent + "Probe:false"])));
        foreach (string conflict in new[] { "M10018", "M10017", "M10004", "M16014", "M16013" })
            Reject(() => QemuProfiles.Create(QemuSmokeOptions.Parse([..Arguments("Mechanical30"), "-Enable" + conflict + "Probe"])));
        foreach (string[] extra in new[] {
            new[] { "-TimeoutSeconds", "119" }, new[] { "-TimeoutSeconds", "221" },
            new[] { "-LinuxDiskRaceVariant", "ExitPending" }, new[] { "-LinuxThreadingApplication", "NativeAotSocketSmoke" },
            new[] { "-GptImageVariant", "PrimaryHeaderCrcFault" }, new[] { "-DiskFormat", "qcow2" },
            new[] { "-SkipImageBuild" }, new[] { "-MarkerPreset", "unexpected-vector-panic" },
            new[] { "-RequireMarker", "CLROS_M16_025_READY" }, new[] { "-WritableDiskFixture", "LogUpload40" } })
            Reject(() => QemuProfiles.Create(QemuSmokeOptions.Parse([..Arguments("Mechanical30"), ..extra])));
        Reject(() => QemuSmokeOptions.Parse([..Arguments("Mechanical30"), "-WritableDiskFixture", "unknown"]));
    }

    [HostTest("writable tooling dryrun constructs independent immutable boot and writable slot7", HostLane.Fast, 2)]
    private static void RunnerArguments()
    {
        var run = HostToolProcess.RunTool(["qemu", "smoke", "-DryRun", "-Qemu", "qemu-test", "-OvmfCode", "ovmf-test", ..Arguments("Mechanical30")]);
        HostCheck.True(run.ExitCode == 0, run.Output);
        HostCheck.True(run.Output.Contains("if=none,id=esp,format=raw,snapshot=on,file=", StringComparison.Ordinal));
        HostCheck.True(run.Output.Contains("if=none,id=writabledata,format=raw,cache=writeback,readonly=off,file=", StringComparison.Ordinal));
        HostCheck.True(run.Output.Contains("virtio-blk-pci,drive=writabledata,addr=07.0,disable-legacy=on,romfile=", StringComparison.Ordinal));
        HostCheck.True(run.Output.Contains("virtio-net-pci,netdev=n0,addr=06.0,disable-legacy=on,romfile=", StringComparison.Ordinal));
        HostCheck.True(!run.Output.Contains("--readonly-webroot-disk", StringComparison.Ordinal));
    }

    [HostTest("writable tooling rejects evidence aliases before invoking any builder", HostLane.Fast, 3)]
    private static async Task OutputAliases()
    {
        string root = OS.Tooling.Repository.FindRoot();
        var options = QemuSmokeOptions.Parse(Arguments("Mechanical30"));
        options.SerialLog = options.Image;
        using StringWriter log = new(); int invoked = 0;
        int result = await QemuSmoke.RunAsync(root, options, log, (_, _) =>
        {
            invoked++;
            return Task.FromResult(new ProcessResult(0, "", "", false, false, false, TimeSpan.Zero));
        });
        HostCheck.Equal(1, result); HostCheck.Equal(0, invoked);
        HostCheck.True(log.ToString().Contains("writable_output_alias", StringComparison.Ordinal));
    }

    [HostTest("writable startup preserves exact two-load65-range admission and old modes", HostLane.Fast, 4)]
    private static void StartupGeometry()
    {
        QemuTranscripts.InspectLinuxStartupRangeValues(65,2,diskFiles:true,writableDisk:true);
        foreach(uint loads in new uint[]{0,1,3,4})Reject(()=>QemuTranscripts.InspectLinuxStartupRangeValues(65,loads,diskFiles:true,writableDisk:true));
        foreach(ulong ranges in new ulong[]{0,64,66,67,135})Reject(()=>QemuTranscripts.InspectLinuxStartupRangeValues(ranges,2,diskFiles:true,writableDisk:true));
        Reject(()=>QemuTranscripts.InspectLinuxStartupRangeValues(65,2,writableDisk:true));
        Reject(()=>QemuTranscripts.InspectLinuxStartupRangeValues(65,2,diskFiles:true,diskStatic:true,writableDisk:true));
        QemuTranscripts.InspectLinuxStartupRangeValues(64,2);
        QemuTranscripts.InspectLinuxStartupRangeValues(65,2,diskFiles:true);
        QemuTranscripts.InspectLinuxStartupRangeValues(67,4,diskStatic:true);
        foreach(bool log in new[]{false,true})
        {
            byte[] package=log?LinuxWritableDiskProofFixture.CreateLogUploadPackage():LinuxWritableDiskProofFixture.CreatePackage();
            Failure(()=>QemuTranscripts.ValidateLinuxStartupTail([],package,diskFiles:true,writableDisk:true,logUpload:log),"m16_004_record_count");
            Reject(()=>QemuTranscripts.ValidateLinuxStartupTail([],package,diskFiles:true,writableDisk:true,logUpload:!log));
            Reject(()=>QemuTranscripts.ValidateLinuxStartupTail([],package,diskFiles:true));
        }
    }

    [HostTest("writable runner requires original identities and exact initial lease facts", HostLane.Fast, 5)]
    private static void RunnerIdentity()
    {
        var options=QemuSmokeOptions.Parse(Arguments("Mechanical30"));var plan=QemuProfiles.Create(options);
        Failure(()=>QemuTranscripts.AssertStandardRunnerResult(".",options,plan,[]),"writable_runner_original_values_required");
        var copied=new QemuTranscripts.WritableRunnerValues("work.raw","boot-id",128*1024*1024,"AA","work-id",128*1024*1024,"BB");
        string[] rows=Enumerable.Repeat("unused",22).ToArray();
        Failure(()=>QemuTranscripts.AssertStandardRunnerResult(".",options,plan,rows,writableData:copied),"writable_runner_initial_identity");
        var ordinary=QemuProfiles.Create(QemuSmokeOptions.Parse([]));
        Failure(()=>QemuTranscripts.AssertStandardRunnerResult(".",new(),ordinary,[],writableData:copied),"writable_runner_original_values_required");
    }

    [HostTest("writable NIC envelope requires exact retired027 tail and rejects old disk acceptance", HostLane.Fast, 6)]
    private static void NetworkEnvelope()
    {
        var rows=NativeAotSocketIntegrationTests.Envelope(true).ToList();
        int first=rows.IndexOf("TEST_START m16-009"),last=rows.IndexOf("TEST_PASS m16-009");
        rows.RemoveRange(first+1,last-first-1);
        void Set(string prefix,string value)=>rows[rows.FindIndex(row=>row.StartsWith(prefix,StringComparison.Ordinal))]=value;
        Set("M16_011_WIRE ","M16_011_WIRE received=0 arp=0 accepted=0 ignored=0 capacity=0 arpCopied=0 tcpCopied=0");
        Set("M16_011_END ","M16_011_END polls=1 received=0 copied=0 fresh=0 retired=0 start=0 end=1 maxDeadline=0 retained=0 buffers=scrubbed descriptors=closed workers=reaped");
        Set("M07_026_ROW stage=4 ","M07_026_ROW stage=4 a=1 b=1 c=1 d=2");
        int at=rows.IndexOf("CLROS_M07_026_READY");
        const string end="M16_027_END owner=16781312 epoch=272 reaped=1 transfers=60 irqs=0 submitted=100 completed=100 submittedBytes=512 completedBytes=512 flushAcknowledgedBytes=512 clock=2 process=drained timer=restored descriptors=closed mappings=restored pool=scrubbed nic=retired dma=zero reservation=retained";
        rows.InsertRange(at,[end,"CLROS_M16_027_READY","TEST_PASS m16.027"]);
        string[] original=rows.ToArray();QemuTranscripts.InspectWritableDiskNetworkValues(original);HostCheck.True(rows.SequenceEqual(original));
        Reject(()=>QemuTranscripts.InspectLinuxDiskNetworkForTests(original));
        foreach(string changed in new[]{end.Replace("clock=2","clock=0",StringComparison.Ordinal),end.Replace("completed=100","completed=99",StringComparison.Ordinal),
            end.Replace("flushAcknowledgedBytes=512","flushAcknowledgedBytes=0",StringComparison.Ordinal),end.Replace("nic=retired","nic=live",StringComparison.Ordinal)})
            Reject(()=>QemuTranscripts.InspectWritableDiskNetworkValues(rows.Select(row=>row==end?changed:row).ToArray()));
        for(int i=at;i<at+3;i++)
        { int removed=i;Reject(()=>QemuTranscripts.InspectWritableDiskNetworkValues(rows.Where((_,index)=>index!=removed).ToArray())); }
        Reject(()=>QemuTranscripts.InspectWritableDiskNetworkValues([..original,"unexpected tail"]));
    }

    private static void Failure(Action action,string reason)
    { string? found=null;try{action();}catch(ToolFailure error){found=error.Reason;}HostCheck.Equal(reason,found); }

    private static void Reject(Action action)
    {
        bool rejected = false;
        try { action(); }
        catch (Exception error) when (error is ToolFailure or ArgumentException) { rejected = true; }
        HostCheck.True(rejected);
    }
}
