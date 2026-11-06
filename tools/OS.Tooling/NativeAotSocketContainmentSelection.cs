namespace OS.Tooling;

/// <summary>Exact paired timeout/drop profile and original containment app
/// selection. These values grant no package, native or guest authority.</summary>
public static class NativeAotSocketContainmentSelection
{
    public static bool Selected(IReadOnlyDictionary<string, bool> flags) =>
        flags.TryGetValue("EnableM07027Probe", out bool enabled) && enabled;

    /// <summary>Bind the dedicated app only for the paired default-off profile.
    /// Reject mixed original socket/runtime modes, missing original inputs and
    /// explicit application substitution before any child starts.</summary>
    internal static bool Bind(QemuSmokeOptions options, IReadOnlyDictionary<string, bool> flags)
    {
        if (!Selected(flags))
            return false;
        bool On(string key) => flags.TryGetValue("Enable" + key + "Probe", out bool enabled) && enabled;
        if (!On("M07028") || !On("M16011") || On("M16012") || On("M09011") || On("M09012") || On("M16010"))
            throw new ToolFailure("socket_containment_profile_conflict");
        const string app = NativeAotSocketContainmentPublisher.Application;
        if (options.LinuxThreadingApplication.Length == 0 && !options.Explicit.Contains("LinuxThreadingApplication"))
            options.LinuxThreadingApplication = app;
        if (options.LinuxThreadingApplication != app)
            throw new ToolFailure("socket_containment_profile_application");
        if (options.LinuxThreadingDisassembly.Length == 0)
            throw new ToolFailure("socket_containment_disassembly_required");
        if (options.LinuxElfPackage.Length == 0 || options.LinuxElfImageVariant != "Intact")
            throw new ToolFailure("socket_containment_original_package_required");
        if (options.LinuxHelloDisassembly.Length != 0)
            throw new ToolFailure("socket_containment_hello_conflict");
        return true;
    }
}
