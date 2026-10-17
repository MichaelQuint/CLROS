namespace OS.Tooling;

/// <summary>Explicit continuing web profile/application pairing. Selection
/// grants no source, package, instruction, runtime or HTTP authority. The sealed
/// web program and complete current guest/external evidence remain mandatory.</summary>
public static class NativeAotWebSelection
{
    /// <summary>Return the original MinimalApiSmoke name for M10-004, or empty
    /// for another profile. Runtime diagnostic unions and explicit mismatched
    /// names throw; this does not start or validate an application.</summary>
    public static string Application(IReadOnlyDictionary<string, bool> flags, string? selected = null)
    {
        bool On(string key) => flags.TryGetValue("Enable" + key + "Probe", out bool value) && value;
        if (!On("M10004")) return "";
        if (On("M09011") || On("M09012") || On("M16010")) throw new ToolFailure("web_runtime_conflict");
        if (selected is not null && selected != "MinimalApiSmoke") throw new ToolFailure("web_profile_application");
        return "MinimalApiSmoke";
    }

    internal static bool Bind(QemuSmokeOptions options, IReadOnlyDictionary<string, bool> flags)
    {
        string app = Application(flags); if (app.Length == 0) return false;
        if (options.LinuxThreadingApplication.Length == 0 && !options.Explicit.Contains("LinuxThreadingApplication"))
            options.LinuxThreadingApplication = app;
        _ = Application(flags, options.LinuxThreadingApplication);
        if (options.WorkloadProfile != "SingleMinimalApi") throw new ToolFailure("web_single_workload_required");
        if (options.DiskFormat != "raw") throw new ToolFailure("web_raw_image_required");
        if (options.LinuxThreadingDisassembly.Length == 0) throw new ToolFailure("web_disassembly_required");
        if (options.LinuxElfPackage.Length == 0 || options.LinuxElfImageVariant != "Intact") throw new ToolFailure("web_original_package_required");
        if (options.LinuxHelloDisassembly.Length != 0) throw new ToolFailure("web_hello_conflict");
        return true;
    }
}
