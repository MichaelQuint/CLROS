namespace OS.Tooling;

/// <summary>Explicit source-sealed static-file profile pairing. This selects
/// policy only; package/index/source, guest and HTTP evidence remain mandatory.</summary>
public static class NativeAotStaticFileSelection
{
    public static string Application(IReadOnlyDictionary<string, bool> flags, string? selected = null)
    {
        bool On(string name) => flags.TryGetValue("Enable" + name + "Probe", out bool enabled) && enabled;
        if (!On("M10017") && !On("M10018")) return "";
        if (On("M16014") || On("M09011") || On("M09012") || On("M16010") || On("M16019Unavailable") || On("M16019Invalid"))
            throw new ToolFailure("static_files_profile_conflict");
        if (selected is not null && selected != NativeAotStaticFilePublisher.Application)
            throw new ToolFailure("static_files_profile_application");
        return NativeAotStaticFilePublisher.Application;
    }

    internal static bool Bind(QemuSmokeOptions options, IReadOnlyDictionary<string, bool> flags)
    {
        string app = Application(flags); if (app.Length == 0) return false;
        if (options.LinuxThreadingApplication.Length == 0 && !options.Explicit.Contains("LinuxThreadingApplication")) options.LinuxThreadingApplication = app;
        _ = Application(flags, options.LinuxThreadingApplication);
        if (options.HasWebSignalRace || options.ServiceLifecycle || options.ServiceQuotaFailure) throw new ToolFailure("static_files_control_conflict");
        if (options.WorkloadProfile != "SingleMinimalApi" || options.DiskFormat != "raw") throw new ToolFailure("static_files_single_raw_required");
        if (options.LinuxThreadingDisassembly.Length == 0 || options.LinuxElfPackage.Length == 0 ||
            options.LinuxElfImageVariant != "Intact" || options.LinuxHelloDisassembly.Length != 0)
            throw new ToolFailure("static_files_original_package_and_index_required");
        return true;
    }
}
