#if SINGLE_FINALLY_CORE_TEST
using System.Reflection;
using OS.Kernel.Runtime;
using OS.Runtime;

internal static unsafe class FinallyAdmissionCoreProgram
{
    private static void Need(bool ok, string reason) { if (!ok) throw new InvalidOperationException(reason); }
    private static int Main()
    {
        Run("complete saved contexts", Contexts);
        Run("control fields and allowed status flags", Controls);
        Run("measured nested clause pair", Clauses);
        Console.WriteLine("FINALLY_ADMISSION_CORE_PASS"); return 0;
    }
    private static void Run(string name, Action test)
    {
        Console.WriteLine("TEST_START finally.admission " + name);
        try { test(); Console.WriteLine("TEST_PASS finally.admission " + name); }
        catch (Exception ex) { Console.WriteLine("TEST_FAIL finally.admission " + name + " " + ex); throw; }
    }
    private static void Contexts()
    {
        Need(sizeof(X64UnwindContext) == 240, "actual complete context ABI");
        X64UnwindContext saved = default;
        for (int i = 0; i < 30; i++) ((ulong*)&saved)[i] = 0x1000UL + (ulong)(17 * i);
        X64UnwindContext actual = saved;
        Need(SingleFinallyAdmission.ContextsMatch(&actual, &saved, false), "exact transfer context");
        for (int i = 0; i < 30; i++)
        {
            actual = saved; ((ulong*)&actual)[i] ^= 1;
            Need(!SingleFinallyAdmission.ContextsMatch(&actual, &saved, false), "changed transfer word " + i);
            Need(SingleFinallyAdmission.ContextsMatch(&actual, &saved, true) == (i is 0 or 2), "post-catch word " + i);
        }
        Need(!SingleFinallyAdmission.ContextsMatch(null, &saved, false) &&
            !SingleFinallyAdmission.ContextsMatch(&actual, null, true), "null contexts rejected");
    }
    private static SingleFinallyAdmission.Control Control(ulong flags = 0x202, ulong cr4 = 0x620,
        uint mxcsr = 0x1F80, ushort x87 = 0x37F) => new(flags, cr4, mxcsr, x87);
    private static void Controls()
    {
        var entry = Control();
        Need(SingleFinallyAdmission.ControlsMatch(entry, entry, entry), "unchanged controls");
        Need(SingleFinallyAdmission.ControlsMatch(entry, Control(flags: 0x203, mxcsr: 0x1FBF), entry),
            "arithmetic flags and MXCSR status bits may differ");
        foreach (var bad in new[] { Control(flags: 0x602), Control(cr4: 0x800620), Control(cr4: 0x621),
            Control(mxcsr: 0x11F80), Control(mxcsr: 0x1FC0), Control(x87: 0x27F) })
            Need(!SingleFinallyAdmission.ControlsMatch(entry, bad, entry), "invalid or changed post controls");
        foreach (var changed in new[] { Control(flags: 0x203), Control(cr4: 0x621), Control(mxcsr: 0x1F81), Control(x87: 0x27F) })
            Need(!SingleFinallyAdmission.ControlsMatch(changed, changed, entry), "entry snapshot mutation");
        var df = Control(flags: 0x602); var cet = Control(cr4: 0x800620);
        Need(!SingleFinallyAdmission.ControlsMatch(df, df, df) && !SingleFinallyAdmission.ControlsMatch(cet, cet, cet),
            "forbidden entry state even when snapshots agree");
    }
    private static readonly ConstructorInfo ClauseConstructor = typeof(NativeAotEhClauseResult).GetConstructor(
        BindingFlags.Instance | BindingFlags.NonPublic, null,
        [typeof(uint), typeof(uint), typeof(uint), typeof(uint), typeof(NativeAotEhClauseKind),
         typeof(ulong), typeof(ulong), typeof(ulong), typeof(bool)], null)!;
    private static NativeAotEhClauseResult Clause(bool cleanup, uint? index = null, uint count = 2,
        uint start = 27, uint? end = null, NativeAotEhClauseKind? kind = null, ulong? handler = null,
        ulong filter = 0, ulong? type = null, bool boundary = false) =>
        (NativeAotEhClauseResult)ClauseConstructor.Invoke([index ?? (cleanup ? 0U : 1U), count, start,
            end ?? (cleanup ? 33U : 47U), kind ?? (cleanup ? NativeAotEhClauseKind.FaultOrFinally : NativeAotEhClauseKind.Typed),
            handler ?? (cleanup ? 0x100047UL : 0x10005EUL), filter, type ?? (cleanup ? 0UL : 0x200000UL), boundary]);
    private static void Clauses()
    {
        // Model actual parser results from the measured 11-byte table. This
        // exercises admission policy, not module authentication or the parser.
        var cleanup = Clause(true); var caught = Clause(false);
        bool Catch(NativeAotEhClauseResult c, ulong root = 0x100000, ulong type = 0x200000, uint pc = 31) =>
            SingleFinallyAdmission.AcceptCatch(c, root, type, pc);
        bool Pair(NativeAotEhClauseResult c, NativeAotEhClauseResult handler) =>
            SingleFinallyAdmission.AcceptCleanup(c, handler, 0x100000, 0x200000, 31);
        Need(Catch(caught) && Pair(cleanup, caught), "exact containing typed catch then inner cleanup");
        foreach (var bad in new[] { default, Clause(false, index: 0), Clause(false, count: 1),
            Clause(false, start: 26), Clause(false, end: 46), Clause(false, kind: NativeAotEhClauseKind.Filter),
            Clause(false, handler: 0x10005F), Clause(false, filter: 1), Clause(false, type: 0), Clause(false, boundary: true) })
            Need(!Catch(bad) && !Pair(cleanup, bad), "invalid outer record cannot admit cleanup");
        foreach (var bad in new[] { default, Clause(true, index: 1), Clause(true, count: 3), Clause(true, start: 28),
            Clause(true, end: 47), Clause(true, kind: NativeAotEhClauseKind.Typed), Clause(true, handler: 0x100048),
            Clause(true, filter: 1), Clause(true, type: 0x200000), Clause(true, boundary: true) })
            Need(!Pair(bad, caught), "invalid cleanup/order/extent/target");
        foreach (uint pc in new uint[] { 0, 27, 30, 32, 33, 47, uint.MaxValue }) Need(!Catch(caught, pc: pc), "wrong protected PC");
        Need(!Catch(caught, root: 0) && !Catch(caught, root: ulong.MaxValue - 50) && !Catch(caught, type: 0),
            "null authority or wrapped contribution rejected");
    }
}
#else
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
        Run("actual admission guard core", GuardCore);
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

    private static void GuardCore()
    {
        string directory = Path.Combine(Root, "artifacts/finally-admission-core", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string project = Path.Combine(directory, "FinallyAdmission.Core.csproj");
        new XDocument(new XElement("Project", new XAttribute("Sdk", "Microsoft.NET.Sdk"),
            new XElement("PropertyGroup", new XElement("OutputType", "Exe"), new XElement("TargetFramework", "net10.0"),
                new XElement("LangVersion", "14.0"), new XElement("Nullable", "enable"), new XElement("ImplicitUsings", "enable"),
                new XElement("AllowUnsafeBlocks", "true"), new XElement("EnableDefaultCompileItems", "false"),
                new XElement("DefineConstants", "SINGLE_FINALLY_CORE_TEST")),
            new XElement("ItemGroup",
                new XElement("Compile", new XAttribute("Include", Path.Combine(Root, "src/OS/Runtime/ExceptionCatchProbe.cs"))),
                new XElement("Compile", new XAttribute("Include", Path.Combine(Root, "tests/OS.HostTests/M04SingleFinallyCatchEfiIntegrationTests.cs"))),
                new XElement("ProjectReference", new XAttribute("Include", Path.Combine(Root, "src/OS.Kernel/OS.Kernel.csproj")))))).Save(project);
        RunCommand("guard-core-build", "dotnet", ["build", project, "--nologo", "-m:1"], 0, null);
        string output = RunCommand("guard-core-run", "dotnet", ["run", "--project", project, "--no-build"], 0, "FINALLY_ADMISSION_CORE_PASS");
        Need(!output.Contains("TEST_FAIL ", StringComparison.Ordinal), "actual guard rejection cases pass");
    }

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
#endif
