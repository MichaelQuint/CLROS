using System.Reflection;
using OS.Tooling;

internal static class NativeAotSocketVectorsSelectionTests
{
    // Keep exact leaf selection controls separate from profile expansion.
    private static readonly Func<QemuSmokeOptions, IReadOnlyDictionary<string, bool>, bool> Bind =
        typeof(NativeAotSocketVectorsSelection).GetMethod("Bind", BindingFlags.NonPublic | BindingFlags.Static)!
            .CreateDelegate<Func<QemuSmokeOptions, IReadOnlyDictionary<string, bool>, bool>>();

    private static Dictionary<string, bool> Flags() => new(StringComparer.Ordinal)
    { ["EnableM16013Probe"] = true, ["EnableM16012Probe"] = true, ["EnableM16011Probe"] = true };
    private static QemuSmokeOptions Options() => new()
    { LinuxElfPackage = "original-vectors.pkg", LinuxThreadingDisassembly = "original-vectors.llvm.txt" };

    [HostTest("vectors selection stays off without its exact enabled flag and leaves other options alone")]
    private static void Unselected()
    {
        foreach (var flags in new Dictionary<string, bool>[] { [], new() { ["EnableM16013Probe"] = false },
            new() { ["EnableM16011Probe"] = true, ["EnableM16012Probe"] = true } })
        foreach (string application in new[] { "", "another-app" })
        {
            var options = new QemuSmokeOptions { LinuxThreadingApplication = application };
            HostCheck.True(!NativeAotSocketVectorsSelection.Selected(flags));
            HostCheck.True(!Bind(options, flags)); HostCheck.Equal(application, options.LinuxThreadingApplication);
        }
        HostCheck.True(NativeAotSocketVectorsSelection.Selected(new Dictionary<string, bool> { ["EnableM16013Probe"] = true }));
    }

    [HostTest("vectors selection defaults only an implicit empty app and accepts the exact explicit app")]
    private static void Application()
    {
        var options = Options(); HostCheck.True(Bind(options, Flags()));
        HostCheck.Equal("NativeAotSocketVectors", options.LinuxThreadingApplication);
        options = Options(); options.LinuxThreadingApplication = "NativeAotSocketVectors";
        options.Explicit.Add("LinuxThreadingApplication"); HostCheck.True(Bind(options, Flags()));
        options = Options(); options.Explicit.Add("LinuxThreadingApplication");
        Reject(() => Bind(options, Flags()), "socket_vectors_profile_application");
        HostCheck.Equal("", options.LinuxThreadingApplication);
        foreach (string application in new[] { "NativeAotSocketSmoke", "NativeAotSocketSmokeAsync", "NativeAotSocketContainment",
            "MinimalApiSmoke", "NativeAotRuntimeMatrix", "nativeaotsocketvectors" })
        {
            options = Options(); options.LinuxThreadingApplication = application;
            Reject(() => Bind(options, Flags()), "socket_vectors_profile_application");
            HostCheck.Equal(application, options.LinuxThreadingApplication);
        }
    }

    [HostTest("vectors selection requires both socket parents and rejects containment web and broader runtime modes")]
    private static void PrerequisitesAndConflicts()
    {
        foreach (string required in new[] { "EnableM16011Probe", "EnableM16012Probe" })
        foreach (bool remove in new[] { false, true })
        {
            var flags = Flags(); if (remove) flags.Remove(required); else flags[required] = false;
            var options = Options(); Reject(() => Bind(options, flags), "socket_vectors_profile_conflict");
            HostCheck.Equal("", options.LinuxThreadingApplication);
        }
        foreach (string forbidden in new[] { "M07027", "M07028", "M10004", "M09011", "M09012", "M16010" })
        {
            var flags = Flags(); flags["Enable" + forbidden + "Probe"] = true;
            var options = Options(); Reject(() => Bind(options, flags), "socket_vectors_profile_conflict");
            HostCheck.Equal("", options.LinuxThreadingApplication);
        }
    }

    [HostTest("vectors selection requires dedicated disassembly Intact original package and no Hello dump")]
    private static void OriginalInputs()
    {
        var options = Options(); options.LinuxThreadingDisassembly = "";
        Reject(() => Bind(options, Flags()), "socket_vectors_disassembly_required");
        options = Options(); options.LinuxElfPackage = "";
        Reject(() => Bind(options, Flags()), "socket_vectors_original_package_required");
        foreach (string variant in new[] { "DigestFault", "intact", "" })
        {
            options = Options(); options.LinuxElfImageVariant = variant;
            Reject(() => Bind(options, Flags()), "socket_vectors_original_package_required");
        }
        options = Options(); options.LinuxHelloDisassembly = "hello.llvm.txt";
        Reject(() => Bind(options, Flags()), "socket_vectors_hello_conflict");
    }

    private static void Reject(Action action, string reason)
    {
        string? observed = null; try { action(); } catch (ToolFailure error) { observed = error.Reason; }
        HostCheck.Equal(reason, observed ?? "accepted");
    }
}
