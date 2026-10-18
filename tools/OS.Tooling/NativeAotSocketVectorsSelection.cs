namespace OS.Tooling;

/// <summary>Exact default-off vector profile and original app selection.
/// These values grant no package, instruction-index or guest authority.</summary>
public static class NativeAotSocketVectorsSelection
{
    /// <summary>Return only the explicit vector leaf's enabled state. Socket
    /// prerequisites alone never select this dedicated fixture.</summary>
    public static bool Selected(IReadOnlyDictionary<string, bool> flags) =>
        flags.TryGetValue("EnableM16013Probe", out bool enabled) && enabled;

    /// <summary>Bind the dedicated app for the vector leaf with both socket
    /// prerequisites. Reject mixed containment/web/runtime profiles, explicit app
    /// substitutions, missing original inputs and Hello dumps before child start.
    /// Only an implicit empty app is defaulted; input bytes require later admission.</summary>
    internal static bool Bind(QemuSmokeOptions options, IReadOnlyDictionary<string, bool> flags)
    {
        if (!Selected(flags)) return false;
        bool On(string key) => flags.TryGetValue("Enable" + key + "Probe", out bool enabled) && enabled;
        if (!On("M16012") || !On("M16011") || On("M07027") || On("M07028") || On("M10004") ||
            On("M09011") || On("M09012") || On("M16010")) throw new ToolFailure("socket_vectors_profile_conflict");
        const string app = NativeAotSocketVectorsPublisher.Application;
        if (options.LinuxThreadingApplication.Length == 0 && !options.Explicit.Contains("LinuxThreadingApplication"))
            options.LinuxThreadingApplication = app;
        if (options.LinuxThreadingApplication != app) throw new ToolFailure("socket_vectors_profile_application");
        if (options.LinuxThreadingDisassembly.Length == 0) throw new ToolFailure("socket_vectors_disassembly_required");
        if (options.LinuxElfPackage.Length == 0 || options.LinuxElfImageVariant != "Intact")
            throw new ToolFailure("socket_vectors_original_package_required");
        if (options.LinuxHelloDisassembly.Length != 0) throw new ToolFailure("socket_vectors_hello_conflict");
        return true;
    }
}
