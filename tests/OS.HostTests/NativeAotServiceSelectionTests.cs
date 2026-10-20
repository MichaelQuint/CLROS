using OS.Tooling;

internal static class NativeAotServiceSelectionTests
{
    [HostTest("service selector distinguishes dedicated source from ordinary web and rejects substitute names")]
    private static void Application()
    {
        var empty = new Dictionary<string, bool>(); HostCheck.Equal("", NativeAotServiceSelection.Application(empty));
        var web = EfiProfile.Create(null, new Dictionary<string, bool> { ["EnableM10004Probe"] = true });
        HostCheck.Equal("MinimalApiSmoke", NativeAotWebSelection.Application(web.Flags));
        HostCheck.Equal("", NativeAotServiceSelection.Application(web.Flags));
        var service = EfiProfile.Create(null, new Dictionary<string, bool> { ["EnableM16014Probe"] = true });
        HostCheck.Equal("", NativeAotWebSelection.Application(service.Flags));
        HostCheck.Equal("MinimalApiService", NativeAotServiceSelection.Application(service.Flags));
        HostCheck.Equal("MinimalApiService", NativeAotServiceSelection.Application(service.Flags, "MinimalApiService"));
        foreach (string name in new[] { "", "MinimalApiSmoke", "NativeAotRuntimeMatrix", "minimalapiservice" })
            Reject(() => NativeAotServiceSelection.Application(service.Flags, name), "service_profile_application");
    }

    [HostTest("service selector requires the owned stack and rejects diagnostic unions before execution")]
    private static void Conflicts()
    {
        var service = EfiProfile.Create(null, new Dictionary<string, bool> { ["EnableM16014Probe"] = true });
        foreach (string parent in new[] { "M10004", "M16012", "M16009" })
        {
            var flags = service.Flags.ToDictionary(); flags["Enable" + parent + "Probe"] = false;
            Reject(() => NativeAotServiceSelection.Application(flags), "service_profile_prerequisites");
        }
        foreach (string conflict in new[] { "M09011", "M09012", "M16010", "M16013", "M07027", "M07028", "M16019Unavailable", "M16019Invalid" })
        {
            var flags = service.Flags.ToDictionary(); flags["Enable" + conflict + "Probe"] = true;
            Reject(() => NativeAotServiceSelection.Application(flags), "service_profile_conflict");
        }
    }

    [HostTest("service quota option stays default off and parses only explicit boolean values")]
    private static void QuotaOption()
    {
        HostCheck.True(!new QemuSmokeOptions().ServiceQuotaFailure && !QemuSmokeOptions.Parse().ServiceQuotaFailure);
        foreach (string argument in new[] { "--service-quota-failure", "-ServiceQuotaFailure:true", "--ServiceQuotaFailure=$true" })
        {
            var options = QemuSmokeOptions.Parse(argument);
            HostCheck.True(options.ServiceQuotaFailure && options.Explicit.Contains("ServiceQuotaFailure") && !options.ServiceLifecycle);
        }
        var off = QemuSmokeOptions.Parse("--service-quota-failure=false", "--service-lifecycle");
        HostCheck.True(!off.ServiceQuotaFailure && off.ServiceLifecycle);
        foreach (string value in new[] { "", "invalid", "1", "0", "v3-quota", "true:false" })
        {
            string? actual = null;
            try { _ = QemuSmokeOptions.Parse("--service-quota-failure=" + value); }
            catch (ArgumentException error) { actual = error.Message; }
            HostCheck.Equal("qemu_invalid_boolean ServiceQuotaFailure", actual ?? "accepted");
        }
    }

    [HostTest("service typed contract preserves positive selectors and selects explicit negative quota capture")]
    private static void Contracts()
    {
        var flags = EfiProfile.Create(null, new Dictionary<string, bool> { ["EnableM16014Probe"] = true }).Flags;
        var v1 = ServiceOptions(); var v2 = ServiceOptions(); v2.ServiceLifecycle = true;
        HostCheck.Equal(NativeAotServiceContract.V1, NativeAotServiceSelection.Contract(v1, flags));
        HostCheck.Equal(NativeAotServiceContract.V2, NativeAotServiceSelection.Contract(v2, flags));
        var first = QemuProfiles.Create(v1); var second = QemuProfiles.Create(v2);
        HostCheck.True(first.Required.SequenceEqual(second.Required) && first.Forbidden.SequenceEqual(second.Forbidden));
        HostCheck.True(!v1.ServiceLifecycle && v2.ServiceLifecycle && !v1.ServiceQuotaFailure && !v2.ServiceQuotaFailure);
        var quota = ServiceOptions(); quota.ServiceQuotaFailure = true;
        HostCheck.Equal(NativeAotServiceContract.QuotaFailure, NativeAotServiceSelection.Contract(quota, flags));
        HostCheck.True(!quota.ServiceLifecycle && quota.LinuxThreadingApplication == NativeAotServicePackage.Application);
        var negative=QemuProfiles.Create(quota);
        HostCheck.True(negative.ReplaceDefaultRequiredMarkers && negative.Required.Contains(OS.Tools.QemuRunner.ServiceQuotaCapture.Terminal) &&
            negative.Unique.Contains(OS.Tools.QemuRunner.ServiceQuotaCapture.Terminal) && negative.Required.Contains(OS.Tools.QemuRunner.ServiceQuotaCapture.ProcessFailure) &&
            !negative.Required.Contains("M07_030_END") && !negative.Required.Contains("M16_014_PROGRESS") &&
            OS.Tools.QemuRunner.ServiceQuotaCapture.ForbiddenSuccessMarkers
                .Where(marker=>marker!=OS.Tools.QemuRunner.ServiceQuotaCapture.EarlyBootSmokePass)
                .All(marker=>negative.Forbidden.Contains(marker) && !negative.Required.Contains(marker)));
        string boot=OS.Tools.QemuRunner.ServiceQuotaCapture.EarlyBootSmokePass;
        HostCheck.True(negative.Required.Contains(boot) && negative.Ordered.Contains(boot) && negative.Unique.Contains(boot) && !negative.Forbidden.Contains(boot));
        HostCheck.True(Array.IndexOf(negative.Ordered.ToArray(),boot)<Array.IndexOf(negative.Ordered.ToArray(),"TEST_START m16-011"));
        HostCheck.True(negative.Forbidden.Contains("TEST_FAIL") && negative.Forbidden.Contains("OS_SMOKE_FAIL"));
    }

    [HostTest("service quota dry plan selects only negative runner arguments without executing publication")]
    private static async Task QuotaDryRun()
    {
        var quota=ServiceOptions();quota.ServiceQuotaFailure=true;quota.DryRun=true;int children=0;
        int result=await QemuSmoke.RunAsync(HostToolProcess.FindRepositoryRoot(),quota,TextWriter.Null,(name,args)=>{
            bool ordinaryRequired=Enumerable.Range(1,args.Count-1).Any(i=>
                args[i-1] is "--require-marker" or "--require-ordered-marker" or "--require-unique-marker" &&
                args[i]!=OS.Tools.QemuRunner.ServiceQuotaCapture.EarlyBootSmokePass &&
                OS.Tools.QemuRunner.ServiceQuotaCapture.ForbiddenSuccessMarkers.Contains(args[i]));
            string boot=OS.Tools.QemuRunner.ServiceQuotaCapture.EarlyBootSmokePass;
            bool bootRequired=new[]{"--require-ordered-marker","--require-unique-marker"}.All(flag=>
                Enumerable.Range(1,args.Count-1).Any(i=>args[i-1]==flag && args[i]==boot));
            bool bootForbidden=Enumerable.Range(1,args.Count-1).Any(i=>args[i-1]=="--forbid-marker" && args[i]==boot);
            children++;HostCheck.True(name=="OS.QemuRunner" && args.Contains("--dry-run") && args.Contains("--continuing-service") &&
                args.Contains("--service-quota-failure") && args.Contains("--socket-forward-port") && args.Contains("--replace-default-required-markers") &&
                args.Contains(OS.Tools.QemuRunner.ServiceQuotaCapture.Terminal) && args.Contains("TEST_FAIL") &&
                !args.Contains("--continuing-web") && !args.Contains("--http-probe-url") && !ordinaryRequired && bootRequired && !bootForbidden);
            return Task.FromResult(new ProcessResult(0,"","",false,false,false,TimeSpan.Zero));
        });
        HostCheck.True(result==0 && children==1);
    }

    [HostTest("service quota live admission rejects missing or malformed original inputs before any child")]
    private static async Task QuotaAdmission()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string folder = Path.Combine(root, "artifacts", "quota-admission-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        string package = Path.Combine(folder, "invalid.elfpkg"), dump = Path.Combine(folder, "invalid.dump");
        File.WriteAllBytes(package, new byte[64]); File.WriteAllText(dump, "invalid original instruction dump\n");
        try
        {
            foreach (var input in new[] { (Package: package + ".missing", Dump: dump),
                (Package: package, Dump: dump + ".missing"), (Package: package, Dump: dump) })
            {
                var options = ServiceOptions(); options.ServiceQuotaFailure = true;
                options.LinuxElfPackage = input.Package; options.LinuxThreadingDisassembly = input.Dump;
                options.SerialLog = Path.Combine(folder, "serial.log"); options.SummaryLog = Path.Combine(folder, "summary.log");
                options.Image = Path.Combine(folder, "image.raw"); options.EfiArtifact = Path.Combine(folder, "BOOTX64.EFI");
                int children = 0; using var output = new StringWriter();
                int result = await QemuSmoke.RunAsync(root, options, output, (name, args) =>
                {
                    children++;
                    return Task.FromResult(new ProcessResult(0, "", "", false, false, false, TimeSpan.Zero));
                });
                HostCheck.True(result == 1 && children == 0 && output.ToString().Contains("TEST_FAIL qemu.smoke ", StringComparison.Ordinal) &&
                    !output.ToString().Contains("TEST_PASS qemu.smoke", StringComparison.Ordinal));
            }
        }
        finally { File.Delete(package); File.Delete(dump); Directory.Delete(folder); }
    }

    [HostTest("service quota selection rejects lifecycle normal other profiles and substitute applications")]
    private static void QuotaConflicts()
    {
        var service = EfiProfile.Create(null, new Dictionary<string, bool> { ["EnableM16014Probe"] = true }).Flags;
        var options = ServiceOptions(); options.ServiceQuotaFailure = true; options.ServiceLifecycle = true;
        Reject(() => NativeAotServiceSelection.Contract(options, service), "service_quota_lifecycle_conflict");
        options.ServiceLifecycle = false; options.SignalEpollRace = true;
        Reject(() => NativeAotServiceSelection.Contract(options, service), "service_quota_profile_conflict");
        options.SignalEpollRace = false; options.BootPolicy = "VerifiedExit";
        Reject(() => NativeAotServiceSelection.Contract(options, service), "service_quota_requires_diagnostic");
        foreach (string profile in new[] { "", "M10004", "M16011", "M16012", "M16013", "M07027", "M09011" })
        {
            var flags = new Dictionary<string, bool>(); if (profile.Length != 0) flags["Enable" + profile + "Probe"] = true;
            var resolved = EfiProfile.Create(null, flags).Flags;
            var other = ServiceOptions(); other.ServiceQuotaFailure = true;
            Reject(() => NativeAotServiceSelection.Contract(other, resolved), "service_quota_requires_service_profile");
        }
        options.BootPolicy = "Diagnostic"; options.LinuxThreadingApplication = "MinimalApiSmoke";
        Reject(() => NativeAotServiceSelection.Contract(options, service), "service_profile_application");
        foreach (string conflict in new[] { "M09011", "M09012", "M16010", "M16013", "M07027", "M07028", "M16019Unavailable", "M16019Invalid" })
        {
            var mixed = service.ToDictionary(); mixed["Enable" + conflict + "Probe"] = true;
            var quota = ServiceOptions(); quota.ServiceQuotaFailure = true;
            Reject(() => NativeAotServiceSelection.Contract(quota, mixed), "service_profile_conflict");
        }
    }

    [HostTest("service quota typed selection retains every existing bounded input prerequisite")]
    private static void QuotaInputs()
    {
        var flags = EfiProfile.Create(null, new Dictionary<string, bool> { ["EnableM16014Probe"] = true }).Flags;
        foreach (var test in new (Action<QemuSmokeOptions> Change, string Error)[] {
            (o => o.WorkloadProfile = "Other", "service_single_workload_required"),
            (o => o.DiskFormat = "qcow2", "service_raw_image_required"),
            (o => o.LinuxThreadingDisassembly = "", "service_disassembly_required"),
            (o => o.LinuxElfPackage = "", "service_original_package_required"),
            (o => o.LinuxElfImageVariant = "DigestFault", "service_original_package_required"),
            (o => o.LinuxHelloDisassembly = "other.dump", "service_hello_conflict") })
        {
            var options = ServiceOptions(); options.ServiceQuotaFailure = true; test.Change(options);
            Reject(() => NativeAotServiceSelection.Contract(options, flags), test.Error);
        }
    }

    private static QemuSmokeOptions ServiceOptions()
    {
        var options = new QemuSmokeOptions { BuildEfi = true, FirmwareRngSource = "virtio",
            LinuxElfPackage = "service.pkg", LinuxThreadingDisassembly = "service.dump" };
        options.Flags["EnableM16014Probe"] = true; return options;
    }

    private static void Reject(Action action, string expected)
    {
        string? actual = null; try { action(); } catch (ToolFailure error) { actual = error.Reason; }
        HostCheck.Equal(expected, actual ?? "accepted");
    }
}
