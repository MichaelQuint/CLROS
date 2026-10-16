using OS.Kernel.Diagnostics;
using OS.Tooling;

internal static class NativeAotWebProfileTests
{
    [HostTest("nativeaot.web profile defaults off and selects original web separately from finite sockets")]
    private static void Selection()
    {
        var normal = EfiProfile.Create(null, new Dictionary<string, bool>());
        HostCheck.True(!normal.IsEnabled("M10004") && !Probes.EnableM10004Probe);
        var profile = EfiProfile.Create(null, new Dictionary<string, bool> { ["EnableM10004Probe"] = true });
        foreach (string required in new[] { "M16012", "M16011", "M16009", "M16008", "M16007", "M16004", "M07026", "M05ApicSetup" })
            HostCheck.True(profile.IsEnabled(required));
        HostCheck.Equal("MinimalApiSmoke", NativeAotWebSelection.Application(profile.Flags));
        HostCheck.Equal("", NativeAotSocketSelection.Application(profile.Flags));
        HostCheck.Equal("", NativeAotRuntimeSelection.Application(profile.Flags));
        var finite = EfiProfile.Create(null, new Dictionary<string, bool> { ["EnableM16012Probe"] = true });
        HostCheck.Equal("NativeAotSocketSmokeAsync", NativeAotSocketSelection.Application(finite.Flags));
        HostCheck.Equal("", NativeAotWebSelection.Application(finite.Flags));
    }

    [HostTest("nativeaot.web profile rejects false prerequisites runtime unions and explicit app mismatches")]
    private static void Rejection()
    {
        void Reject(Action action)
        { bool rejected = false; try { action(); } catch (ToolFailure) { rejected = true; } HostCheck.True(rejected); }
        foreach (string parent in new[] { "M16012", "M16011", "M16009", "M16007", "M16004", "M07026", "M05ApicSetup" })
            Reject(() => EfiProfile.Create(null, new Dictionary<string, bool> { ["EnableM10004Probe"] = true, ["Enable" + parent + "Probe"] = false }));
        foreach (string runtime in new[] { "M09011", "M09012", "M16010" })
            Reject(() => EfiProfile.Create(null, new Dictionary<string, bool> { ["EnableM10004Probe"] = true, ["Enable" + runtime + "Probe"] = true }));
        var web = EfiProfile.Create(null, new Dictionary<string, bool> { ["EnableM10004Probe"] = true });
        foreach (string app in new[] { "", "NativeAotSocketSmokeAsync", "NativeAotRuntimeSmoke", "minimalapismoke" })
            Reject(() => NativeAotWebSelection.Application(web.Flags, app));
        HostCheck.Equal("MinimalApiSmoke", NativeAotWebSelection.Application(web.Flags, "MinimalApiSmoke"));
    }

    [HostTest("nativeaot.web runner plan requires continuing startup without finite completion markers")]
    private static void Plan()
    {
        QemuSmokeOptions Options() { var o = new QemuSmokeOptions { BuildEfi = true, LinuxElfPackage = "web.pkg", LinuxThreadingDisassembly = "web.dump" }; o.Flags["EnableM10004Probe"] = true; return o; }
        var options = Options(); var plan = QemuProfiles.Create(options);
        HostCheck.Equal(240, options.TimeoutSeconds);
        HostCheck.Equal("MinimalApiSmoke", options.LinuxThreadingApplication);
        foreach (string marker in new[] { "M10_004_ENTER", "M10_004_CONFIG", "M10_004_LISTEN", "M10_004_LIVE", "CLROS_M10_004_READY", "TEST_PASS m10.004" })
            HostCheck.True(plan.Required.Contains(marker) && plan.Ordered.Contains(marker));
        // The inherited boot prefix already contains OS_SMOKE_PASS before real
        // firmware exit; it is never the continuing application's terminal.
        foreach (string marker in new[] { "M16_008_EXIT", "M16_008_OWNER_END", "M16_009_RESTORE", "M16_011_END ", "CLROS_M07_026_READY" })
            HostCheck.True(!plan.Required.Contains(marker));
        foreach (int timeout in new[] { 29, 286 })
        {
            var invalid = Options(); invalid.TimeoutSeconds = timeout; invalid.Explicit.Add("TimeoutSeconds");
            bool rejected = false; try { QemuProfiles.Create(invalid); } catch (ToolFailure) { rejected = true; } HostCheck.True(rejected);
        }
    }
}
