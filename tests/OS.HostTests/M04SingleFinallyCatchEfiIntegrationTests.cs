using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Xml.Linq;
using OS.Boot;
using OS.Kernel.Diagnostics;

internal static class M04SingleFinallyCatchEfiIntegrationTests
{
    private const string Flag = "EnableM04SingleFinallyCatchProbe";
    private static readonly string[] Ancestry = ["EnableM01LoadedImageStackProbe", "EnableM01LiveAllocatorProbe",
        "EnableM04IndependentEndianConformanceProbe", "EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe",
        "EnableM04ArithmeticConformanceProbe", "EnableM04LayoutContractProbe",
        "EnableM04ManagedHeapPageRangeProbe", "EnableM04RhpNewFastProbe", "EnableM04GcStaticRootMaterializationProbe"];
    private static string Root => HostToolProcess.FindRepositoryRoot();
    private static void Need(bool condition, string reason)
    { if (!condition) throw new InvalidOperationException(reason); }

    [ModuleInitializer]
    internal static void RunAll()
    {
        Run("default authority", Defaults);
        Run("project switch admission", Switches);
        Run("unadmitted smoke refuses execution", UnadmittedSmoke);
    }

    private static void Run(string name, Action action)
    {
        Console.WriteLine("TEST_START m04.021.finally.efi " + name);
        try { action(); Console.WriteLine("TEST_PASS m04.021.finally.efi " + name); }
        catch (Exception ex) { Console.WriteLine("TEST_FAIL m04.021.finally.efi " + name + " " + ex); throw; }
    }

    private static unsafe void Defaults()
    {
        var project = XDocument.Load(Path.Combine(Root, "src/OS/OS.csproj"));
        var defaults = project.Descendants(Flag).ToArray();
        Need(defaults.Length == 1 && defaults[0].Value == "false" &&
            !Probes.EnableM04SingleFinallyCatchProbe && !BootSequence.IsSingleFinallyCatchEnabled(),
            "new profile defaults off independently of simple catch");
        int vectorCount = System.Text.RegularExpressions.Regex.Matches(project.ToString(), @"\$\((Enable\w+Probe)\)")
            .Select(x => x.Groups[1].Value).Distinct(StringComparer.Ordinal).Count();
        var literalDefaults = project.Descendants().Where(x => x.Name.LocalName.StartsWith("Enable", StringComparison.Ordinal) &&
            x.Attribute("Condition")?.Value == "'$(" + x.Name.LocalName + ")' == ''").ToArray();
        Need(vectorCount == 52 && literalDefaults.Length == 49 && literalDefaults.All(x => x.Value == "false"),
            "complete expanded profile vector with all dangerous literal defaults false");
        var probe = Probes.GetM04SingleFinallyCatchProbe();
        Need(probe.Name == Flag && !probe.EnabledByDefault && probe.Risk == ProbeRisk.Destructive,
            "explicit destructive diagnostic");
        var result = BootSequence.RunSingleFinallyCatchUnwind(default, default,
            null, 0, 0, null, 0, null, 0, default);
        Need(!result.Succeeded && result.Caller.Rip == 0 && result.Caller.Rsp == 0,
            "disabled boot authority cannot reach unwind with invalid inputs");
    }

    private static void Switches()
    {
        string[] chain = Ancestry.Select(x => "-p:" + x + "=true").ToArray();
        Check("default", [], 0, null);
        Check("enabled", [..chain, "-p:" + Flag + "=true"], 0, null);
        Check("invalid", [..chain, "-p:" + Flag + "=invalid"], 1, "m04_single_finally_switch_invalid");
        Check("missing-gc", ["-p:" + Flag + "=true"], 1, "m04_single_finally_requires_gc_profile");
        foreach (string collision in new[] { "EnableM04SimpleThrowCatchProbe", "EnableM04SeededCollectorChurnProbe",
            "EnableM04ClassConstructorExecutionProbe", "EnableM04InterfaceDispatchProbe", "EnableM04X64UnwindMetadataProbe" })
        {
            string[] dependencies = collision == "EnableM04X64UnwindMetadataProbe"
                ? ["-p:EnableM04TypeManagerModuleRegistrationProbe=true"] : [];
            // The unwind profile requires the separate module-registration
            // profile, which the collecting GC ancestor already excludes.
            string reason = collision == "EnableM04X64UnwindMetadataProbe"
                ? "m04_gc_static_profile_conflict EnableM04TypeManagerModuleRegistrationProbe"
                : "m04_single_finally_profile_conflict";
            Check(collision, [..chain, ..dependencies, "-p:" + Flag + "=true", "-p:" + collision + "=true"],
                1, reason);
        }
    }

    private static void UnadmittedSmoke()
    {
        // Both command construction and real launch must remain closed before
        // native contribution/ABI review. The sentinel path cannot launch QEMU.
        foreach (bool dry in new[] { true, false })
        {
            string[] mode = dry ? ["-DryRun"] : [];
            string output = RunCommand("smoke-" + dry, "powershell.exe",
                ["-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", "scripts/run-qemu-smoke.ps1",
                 "-BuildEfi", "-" + Flag, ..Ancestry.Select(x => "-" + x), ..mode,
                 "-Qemu", "artifacts/unadmitted-finally-must-not-launch.exe"],
                1, "m04_single_finally_native_admission_pending");
            Need(output.Trim() == "TEST_FAIL qemu.smoke m04_single_finally_native_admission_pending",
                "reject before build, process launch or smoke acceptance");
        }
    }

    private static void Check(string name, string[] properties, int expectedExit, string? expectedReason)
        => RunCommand(name, "dotnet", ["msbuild", "src/OS/OS.csproj", "-nologo", "-t:ValidateM01ProbeSwitches", ..properties],
            expectedExit, expectedReason);

    private static string RunCommand(string name, string executable, string[] arguments, int expectedExit, string? expectedReason)
    {
        string folder = Path.Combine(Root, "artifacts/finally-project-admission", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var start = new ProcessStartInfo(executable) { WorkingDirectory = Root, UseShellExecute = false,
            CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string argument in arguments)
            start.ArgumentList.Add(argument);
        File.WriteAllText(Path.Combine(folder, "arguments.json"), JsonSerializer.Serialize(start.ArgumentList));
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        bool ended = process.WaitForExit(60000);
        if (!ended) { process.Kill(true); process.WaitForExit(); }
        string output = stdout.GetAwaiter().GetResult(), error = stderr.GetAwaiter().GetResult();
        File.WriteAllText(Path.Combine(folder, "stdout.log"), output);
        File.WriteAllText(Path.Combine(folder, "stderr.log"), error);
        Console.WriteLine("FINALLY_PROJECT_ADMISSION case=" + name + " exit=" + process.ExitCode + " evidence=" + folder);
        Need(ended && process.ExitCode == expectedExit && error.Length == 0 &&
            (expectedReason is null || output.Contains(expectedReason, StringComparison.Ordinal)), name + ": " + output + error);
        return output;
    }
}
