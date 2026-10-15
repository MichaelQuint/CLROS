namespace OS.Tooling;

/// <summary>Exact default-off runtime profile/application pairing for host
/// orchestration. These selections confer no package, source or guest authority;
/// the sealed program factory and complete transcript checks remain required.</summary>
public static class NativeAotRuntimeSelection
{
    /// <summary>Return the selected application, or empty for an older profile.
    /// Multiple enabled runtime leaves fail before any child can execute.</summary>
    public static string Application(IReadOnlyDictionary<string, bool> flags)
    {
        string result = "";
        foreach (string app in new[] { "NativeAotSuspensionStress", "NativeAotThreadPoolStress", "NativeAotRuntimeMatrix" })
            if (flags.TryGetValue(Flag(app), out bool enabled) && enabled)
            {
                if (result.Length != 0) throw new ToolFailure("runtime_profile_conflict");
                result = app;
            }
        return result;
    }

    /// <summary>Return the one owning probe; unknown application names throw.</summary>
    public static string Flag(string app) => app switch
    {
        "NativeAotSuspensionStress" => "EnableM09011Probe",
        "NativeAotThreadPoolStress" => "EnableM09012Probe",
        "NativeAotRuntimeMatrix" => "EnableM16010Probe",
        _ => throw new ToolFailure("runtime_application")
    };

    /// <summary>Return the three exact outer records, in execution order.</summary>
    public static (string Start, string Ready, string Pass) Markers(string app) => app switch
    {
        "NativeAotSuspensionStress" => ("TEST_START m09.011", "CLROS_M09_011_READY", "TEST_PASS m09.011"),
        "NativeAotThreadPoolStress" => ("TEST_START m09.012", "CLROS_M09_012_READY", "TEST_PASS m09.012"),
        "NativeAotRuntimeMatrix" => ("TEST_START m16-010", "CLROS_M16_010_READY", "TEST_PASS m16-010"),
        _ => throw new ToolFailure("runtime_application")
    };

    // Only an omitted application may be inferred. Explicit empty/mismatched
    // input must not silently select a different payload.
    internal static void Bind(QemuSmokeOptions options, IReadOnlyDictionary<string, bool> flags)
    {
        string app = Application(flags);
        if (app.Length != 0)
        {
            if (options.LinuxThreadingApplication.Length == 0 && !options.Explicit.Contains("LinuxThreadingApplication"))
                options.LinuxThreadingApplication = app;
            if (options.LinuxThreadingApplication != app) throw new ToolFailure("runtime_profile_application");
        }
        if (!options.HasThreadingSelection) return;
        if (app.Length == 0 && options.LinuxThreadingApplication is not ("NativeAotSuspensionSmoke" or "NativeAotThreadPoolSmoke"))
            throw new ToolFailure("threading_application");
        if (options.LinuxThreadingDisassembly.Length == 0) throw new ToolFailure("threading_disassembly_required");
        if (options.LinuxHelloDisassembly.Length != 0) throw new ToolFailure("threading_hello_conflict");
        if (!flags.TryGetValue("EnableM16009Probe", out bool signal) || !signal)
            throw new ToolFailure("threading_profile_requires_m16_009");
    }
}
