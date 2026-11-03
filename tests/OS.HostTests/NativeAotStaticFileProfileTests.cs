using OS.Kernel.Diagnostics;
using OS.Tooling;

internal static class NativeAotStaticFileProfileTests
{
    private static QemuSmokeOptions Options()
    {
        var value = new QemuSmokeOptions { BuildEfi = true, LinuxElfPackage = "static.pkg", LinuxThreadingDisassembly = "static.dump" };
        value.Flags["EnableM10017Probe"] = true;
        return value;
    }

    [HostTest("static profile inherits real web owners while retaining a separate app and finite cleanup")]
    private static void Plan()
    {
        HostCheck.True(!Probes.EnableM10017Probe);
        var options = Options();
        var plan = QemuProfiles.Create(options);
        HostCheck.Equal("MinimalApiStaticFiles", options.LinuxThreadingApplication);
        HostCheck.Equal(600, options.TimeoutSeconds);
        foreach (string parent in new[] { "M10004", "M16012", "M16011", "M16009", "M07026", "M05ApicSetup" })
            HostCheck.True(plan.IsEnabled("Enable" + parent + "Probe"));
        HostCheck.True(!plan.IsEnabled("EnableM16014Probe"));
        HostCheck.Equal("", NativeAotWebSelection.Application(plan.Flags));
        HostCheck.Equal("", NativeAotSocketSelection.Application(plan.Flags));
        foreach (string marker in new[] { "M16_008_OWNER_END", "M16_009_RESTORE", "M16_011_END ", "CLROS_M07_026_READY",
            "M10_017_LISTENING ", "M10_017_TERMINATE ", "M10_017_APP_EXIT ", "M10_017_OWNER_END ", "M10_017_CLEANUP ", "TEST_PASS m10.017" })
            HostCheck.True(plan.Required.Contains(marker.Trim()), marker);
        HostCheck.True(!plan.Required.Contains("CLROS_M10_004_READY"));
    }

    [HostTest("static profile rejects false inherited owners incompatible apps and truncated capture budgets")]
    private static void Rejections()
    {
        foreach (string parent in new[] { "M10004", "M16012", "M16011", "M16009", "M07026", "M05ApicSetup" })
        {
            var options = Options();
            options.Flags["Enable" + parent + "Probe"] = false;
            Reject(options);
        }
        foreach (string conflict in new[] { "M16014", "M16019Unavailable", "M16019Invalid", "M09011", "M09012", "M16010", "M16013", "M07027" })
        {
            var options = Options();
            options.Flags["Enable" + conflict + "Probe"] = true;
            Reject(options);
        }
        foreach (string app in new[] { "MinimalApiSmoke", "NativeAotRuntimeSmoke", "minimalapistaticfiles" })
        {
            var options = Options();
            options.LinuxThreadingApplication = app;
            Reject(options);
        }
        foreach (int timeout in new[] { 1, 599, 826, 901 })
        {
            var options = Options();
            options.TimeoutSeconds = timeout;
            options.Explicit.Add("TimeoutSeconds");
            Reject(options);
        }
        var converted = Options();
        converted.DiskFormat = "qcow2";
        Reject(converted);
        var racing = Options();
        racing.WebSignalRace = true;
        Reject(racing);
        var noIndex = Options();
        noIndex.LinuxThreadingDisassembly = "";
        Reject(noIndex);
    }

    private static void Reject(QemuSmokeOptions options)
    {
        bool rejected = false;
        try
        {
            QemuProfiles.Create(options);
        }
        catch (ToolFailure) { rejected = true; }
        HostCheck.True(rejected);
    }
}
