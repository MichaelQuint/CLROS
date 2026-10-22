using OS.Kernel.Diagnostics;
using OS.Tooling;

internal static class ContinuingServiceProfileTests
{
    [HostTest("service lifecycle option remains default off and parses explicit true false without enabling a guest")]
    private static void LifecycleOption()
    {
        HostCheck.True(!new QemuSmokeOptions().ServiceLifecycle && !QemuSmokeOptions.Parse().ServiceLifecycle);
        var enabled = QemuSmokeOptions.Parse("--service-lifecycle");
        HostCheck.True(enabled.ServiceLifecycle && enabled.Explicit.Contains("ServiceLifecycle"));
        HostCheck.True(!QemuSmokeOptions.Parse("-ServiceLifecycle:false").ServiceLifecycle);
        bool rejected = false;
        try { _ = QemuSmokeOptions.Parse("--service-lifecycle=invalid"); } catch (ArgumentException) { rejected = true; }
        HostCheck.True(rejected);
    }

    [HostTest("service lifecycle selection requires the explicit continuing service parent and preserves inherited policy")]
    private static void LifecycleSelection()
    {
        var options = new QemuSmokeOptions { ServiceLifecycle = true };
        Reject(() => QemuProfiles.Create(options), "service_lifecycle_requires_service_profile");
        options.BuildEfi = true; options.FirmwareRngSource = "virtio";
        options.LinuxElfPackage = "service-v2.pkg"; options.LinuxThreadingDisassembly = "service-v2.dump";
        options.Flags["EnableM16014Probe"] = true;
        var plan = QemuProfiles.Create(options);
        HostCheck.True(options.ServiceLifecycle && plan.IsEnabled("EnableM16014Probe") && plan.IsEnabled("EnableM10004Probe"));
        HostCheck.Equal(1080, options.TimeoutSeconds);
    }

    [HostTest("service lifecycle launch requires original package admission before starting children")]
    private static async Task LifecycleInputAdmission()
    {
        string prefix = Path.Combine(Path.GetTempPath(), "clros-service-lifecycle-" + Guid.NewGuid().ToString("N"));
        var options = new QemuSmokeOptions { ServiceLifecycle = true, BuildEfi = true, FirmwareRngSource = "virtio",
            LinuxElfPackage = prefix + ".pkg", LinuxThreadingDisassembly = prefix + ".dump",
            SerialLog = prefix + ".serial", SummaryLog = prefix + ".summary" };
        options.Flags["EnableM16014Probe"] = true; int children = 0; using var output = new StringWriter();
        int result = await QemuSmoke.RunAsync(HostToolProcess.FindRepositoryRoot(), options, output, (_, _) => { children++; throw new InvalidOperationException("Unexpected lifecycle child."); });
        HostCheck.Equal(1, result); HostCheck.Equal(0, children);
        HostCheck.True(output.ToString().Contains("TEST_FAIL qemu.smoke", StringComparison.Ordinal) &&
            output.ToString().Contains(Path.GetFileName(options.LinuxElfPackage), StringComparison.Ordinal));
    }

    [HostTest("continuing service remains default off and explicitly inherits the owned web stack")]
    private static void Selection()
    {
        HostCheck.True(!Probes.EnableM16014Probe);
        HostCheck.True(!EfiProfile.Create(null, new Dictionary<string, bool>()).IsEnabled("M16014"));
        var profile = EfiProfile.Create(null, Flags());
        HostCheck.Equal("Diagnostic", profile.BootPolicy);
        foreach (string parent in new[] { "M16014", "M10004", "M16012", "M16011", "M16009", "M16008", "M16007", "M16004", "M07026", "M05ApicSetup" })
            HostCheck.True(profile.IsEnabled(parent));
        HostCheck.Equal("MinimalApiService", NativeAotServiceSelection.Application(profile.Flags));
        HostCheck.Equal("", NativeAotWebSelection.Application(profile.Flags));
        HostCheck.True(!profile.IsEnabled("M16013") && !profile.IsEnabled("M07027"));
    }

    [HostTest("continuing service cannot override an explicitly disabled web or socket prerequisite")]
    private static void Prerequisites()
    {
        var flags = Flags(); flags["EnableM10004Probe"] = false;
        Reject(() => EfiProfile.Create(null, flags), "m16_014_explicit_false_prerequisite");
        flags = Flags(); flags["EnableM16012Probe"] = false;
        Reject(() => EfiProfile.Create(null, flags), "web_explicit_false_prerequisite");
    }

    [HostTest("continuing service retains negative-control vector containment runtime and boot-policy exclusions")]
    private static void Conflicts()
    {
        foreach (string control in new[] { "M16019Unavailable", "M16019Invalid" })
        {
            var flags = Flags(); flags["Enable" + control + "Probe"] = true;
            Reject(() => EfiProfile.Create(null, flags), "continuing_service_profile_conflict");
        }
        foreach (string conflict in new[] { "M16013", "M07027", "M07028", "M09011", "M09012", "M16010" })
        {
            var flags = Flags(); flags["Enable" + conflict + "Probe"] = true;
            bool rejected = false;
            try { EfiProfile.Create(null, flags); } catch (ToolFailure) { rejected = true; }
            HostCheck.True(rejected);
        }
        bool bootRejected = false;
        try { EfiProfile.Create("VerifiedExit", Flags()); } catch (ToolFailure) { bootRejected = true; }
        HostCheck.True(bootRejected);
    }

    private static Dictionary<string, bool> Flags() => new() { ["EnableM16014Probe"] = true };

    [HostTest("continuing service selects its owned full capture runner with bounded real time and isolated terminal markers")]
    private static void RunnerBoundary()
    {
        var options = new QemuSmokeOptions { BuildEfi=true,FirmwareRngSource="virtio",LinuxElfPackage = "service.pkg", LinuxThreadingDisassembly = "service.dump" };
        options.Flags["EnableM16014Probe"] = true;
        var plan=QemuProfiles.Create(options);
        HostCheck.Equal(900,options.TimeoutSeconds);
        HostCheck.Equal("MinimalApiService",options.LinuxThreadingApplication);
        HostCheck.True(plan.Required.Contains("TEST_PASS m16-012") && plan.Required.Contains("M16_014_POLICY") &&
            !plan.Required.Contains("TEST_PASS m10.004") && !plan.IsEnabled("EnableM01OwnedPagingProbe"));
        options.Explicit.Add("TimeoutSeconds");options.TimeoutSeconds=779;
        Reject(() => QemuProfiles.Create(options), "service_timeout_must_cover_quiet_runtime_and_transport_drain");
        options.TimeoutSeconds=780;_=QemuProfiles.Create(options);
        options.FirmwareRngSource="none";
        Reject(() => QemuProfiles.Create(options), "service_virtio_rng_required");
    }

    [HostTest("service smoke dry run wires only the full service runner and starts no peer or package admission")]
    private static async Task DryRun()
    {
        var options=new QemuSmokeOptions { DryRun=true,BuildEfi=true,FirmwareRngSource="virtio",LinuxElfPackage="missing-service.pkg",
            LinuxThreadingDisassembly="missing-service.dump" };
        options.Flags["EnableM16014Probe"]=true;int calls=0;
        int result=await QemuSmoke.RunAsync(HostToolProcess.FindRepositoryRoot(),options,TextWriter.Null,(name,args)=>
        {
            calls++;HostCheck.True(name=="OS.QemuRunner" && args.Contains("--dry-run") && args.Contains("--continuing-service") &&
                args.Contains("--socket-forward-port") && args.Contains("900") && args.Contains("TEST_PASS m16-012") &&
                !args.Contains("--continuing-web") && !args.Contains("--continuing-network-peer") && !args.Contains("--http-probe-url"));
            return Task.FromResult(new ProcessResult(0,"","",false,false,false,TimeSpan.Zero));
        });
        HostCheck.True(result==0 && calls==1);
    }

    private static void Reject(Action action, string reason)
    {
        string? observed = null; try { action(); } catch (ToolFailure error) { observed = error.Reason; }
        HostCheck.Equal(reason, observed ?? "accepted");
    }
}
