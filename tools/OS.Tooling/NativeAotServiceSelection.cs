namespace OS.Tooling;

/// <summary>Explicit diagnostic service/application pairing only. Selection
/// starts nothing and grants no package, native, lifecycle or guest authority.</summary>
public static class NativeAotServiceSelection
{
    /// <summary>Return the dedicated service name only for M16-014. Mismatched
    /// explicit names and missing/conflicting prerequisites reject.</summary>
    public static string Application(IReadOnlyDictionary<string, bool> flags, string? selected = null)
    {
        bool On(string key) => flags.TryGetValue("Enable" + key + "Probe", out bool value) && value;
        if (!On("M16014")) return "";
        if (!On("M10004") || !On("M16012") || !On("M16009")) throw new ToolFailure("service_profile_prerequisites");
        if (new[] { "M09011", "M09012", "M16010", "M16013", "M07027", "M07028", "M16019Unavailable", "M16019Invalid" }.Any(On))
            throw new ToolFailure("service_profile_conflict");
        if (selected is not null && selected != NativeAotServicePackage.Application) throw new ToolFailure("service_profile_application");
        return NativeAotServicePackage.Application;
    }

    // Kept separate from the finite web selector. The runner guard is retained
    // until the complete service peer/packet/lifecycle consumer is installed.
    internal static bool Bind(QemuSmokeOptions options, IReadOnlyDictionary<string, bool> flags)
    {
        string app = Application(flags);
        if (options.ServiceLifecycle && app.Length == 0) throw new ToolFailure("service_lifecycle_requires_service_profile");
        if (app.Length == 0) return false;
        if (options.LinuxThreadingApplication.Length == 0 && !options.Explicit.Contains("LinuxThreadingApplication"))
            options.LinuxThreadingApplication = app;
        _ = Application(flags, options.LinuxThreadingApplication);
        if (options.WorkloadProfile != "SingleMinimalApi") throw new ToolFailure("service_single_workload_required");
        if (options.DiskFormat != "raw") throw new ToolFailure("service_raw_image_required");
        if (options.LinuxThreadingDisassembly.Length == 0) throw new ToolFailure("service_disassembly_required");
        if (options.LinuxElfPackage.Length == 0 || options.LinuxElfImageVariant != "Intact") throw new ToolFailure("service_original_package_required");
        if (options.LinuxHelloDisassembly.Length != 0) throw new ToolFailure("service_hello_conflict");
        return true;
    }
}
