namespace OS.Tooling;

/// <summary>Exact default-off runtime profile/application pairing for host
/// orchestration. These selections confer no package, source or guest authority;
/// the sealed program factory and complete transcript checks remain required.</summary>
public static class NativeAotRuntimeSelection
{
    /// <summary>Return the selected application, or empty for an older profile.
    /// Without an explicit application, M16-010 defaults to RuntimeMatrix.
    /// Its original RuntimeSmoke regression must be selected explicitly.
    /// Conflicting probes and mismatched names fail before child execution.</summary>
    public static string Application(IReadOnlyDictionary<string, bool> flags, string? selected = null)
    {
        string result = "";
        foreach (string app in new[] { "NativeAotSuspensionStress", "NativeAotThreadPoolStress", "NativeAotRuntimeMatrix" })
            if (flags.TryGetValue(Flag(app), out bool enabled) && enabled)
            {
                if (result.Length != 0) throw new ToolFailure("runtime_profile_conflict");
                result = app;
            }
        if (result.Length == 0 || selected is null || selected == result) return result;
        if (result == "NativeAotRuntimeMatrix" && selected == "NativeAotRuntimeSmoke") return selected;
        throw new ToolFailure("runtime_profile_application");
    }

    /// <summary>Return the one owning probe; unknown application names throw.</summary>
    public static string Flag(string app) => app switch
    {
        "NativeAotSuspensionStress" => "EnableM09011Probe",
        "NativeAotThreadPoolStress" => "EnableM09012Probe",
        "NativeAotRuntimeMatrix" or "NativeAotRuntimeSmoke" => "EnableM16010Probe",
        _ => throw new ToolFailure("runtime_application")
    };

    /// <summary>Return the three exact outer records, in execution order.</summary>
    public static (string Start, string Ready, string Pass) Markers(string app) => app switch
    {
        "NativeAotSuspensionStress" => ("TEST_START m09.011", "CLROS_M09_011_READY", "TEST_PASS m09.011"),
        "NativeAotThreadPoolStress" => ("TEST_START m09.012", "CLROS_M09_012_READY", "TEST_PASS m09.012"),
        "NativeAotRuntimeMatrix" or "NativeAotRuntimeSmoke" => ("TEST_START m16-010", "CLROS_M16_010_READY", "TEST_PASS m16-010"),
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
            _ = Application(flags, options.LinuxThreadingApplication);
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
