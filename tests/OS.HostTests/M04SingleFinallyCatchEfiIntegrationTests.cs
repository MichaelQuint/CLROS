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
        Run("measured throwing and parent frames", Frames);
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
    private static void Frames()
    {
        Need(SingleFinallyAdmission.ParentFrameMatches(0x10000, 0x10030, 0x10070), "40-byte thrower plus return; parent RBP +64");
        foreach (ulong delta in new ulong[] { 0, 8, 16, 40, 56 })
            Need(!SingleFinallyAdmission.ParentFrameMatches(0x10000, 0x10000 + delta, 0x10040 + delta), "wrong throwing-frame extent");
        Need(!SingleFinallyAdmission.ParentFrameMatches(0x10000, 0x10030, 0x10060), "wrong parent RBP");
        Need(!SingleFinallyAdmission.ParentFrameMatches(0, 48, 112), "null capture");
        Need(!SingleFinallyAdmission.ParentFrameMatches(ulong.MaxValue - 47, 0, 64), "wrapped caller");
        Need(!SingleFinallyAdmission.ParentFrameMatches(ulong.MaxValue - 111, ulong.MaxValue - 63, 0), "wrapped parent RBP");
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

    [HostTest("m04.021.finally.efi default authority", HostLane.Fast, 0)]
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
        string[] expectedDefaults = OS.Tooling.EfiContracts.ProjectDefaultProbes.Order(StringComparer.Ordinal).ToArray();
        Need(vectorCount == OS.Tooling.EfiContracts.DiagnosticProbes.Count && literalDefaults.Select(x => x.Name.LocalName).Order(StringComparer.Ordinal).SequenceEqual(expectedDefaults) && literalDefaults.All(x => x.Value == "false"),
            "complete expanded profile vector with all dangerous literal defaults false");
        var probe = Probes.GetM04SingleFinallyCatchProbe();
        Need(probe.Name == Flag && !probe.EnabledByDefault && probe.Risk == ProbeRisk.Destructive,
            "explicit destructive diagnostic");
        var result = BootSequence.RunSingleFinallyCatchUnwind(default, default,
            null, 0, 0, null, 0, null, 0, default);
        Need(!result.Succeeded && result.Caller.Rip == 0 && result.Caller.Rsp == 0,
            "disabled boot authority cannot reach unwind with invalid inputs");
    }

    [HostTest("m04.021.finally.efi project switch admission", HostLane.Native, 1)]
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

    [HostTest("m04.021.finally.efi explicit profile dry run", HostLane.Fast, 2)]
    private static void Profile()
    {
        var result = HostToolProcess.RunTool(["qemu", "smoke", "--build-efi", "--dry-run", "--" + Flag, ..Ancestry.Select(x => "--" + x)]);
        Need(result.ExitCode == 0 && result.Error.Length == 0, result.Output + result.Error);
        string output = result.Output;
        string[] Read(string key) => output.Split('\n').Single(x => x.StartsWith(key, StringComparison.Ordinal))[key.Length..]
            .TrimEnd('\r').Split(';', StringSplitOptions.RemoveEmptyEntries);
        string[] ordered = Read("ORDERED_MARKERS "), required = Read("REQUIRED_MARKERS "), unique = Read("UNIQUE_MARKERS ");
        string[] expected = new[] { "OS_BOOT_OK", "TEST_PASS boot.sequence", "OS_SMOKE_PASS" }.Concat(ordered).Distinct(StringComparer.Ordinal).ToArray();
        Need(ordered.Length == OS.Tooling.QemuContracts.M04SingleFinallyRequiredMarkers.Count && required.SequenceEqual(expected, StringComparer.Ordinal) && unique.SequenceEqual(ordered, StringComparer.Ordinal),
            "complete ancestor and fourteen child markers");
        Need(ordered[51] == "TEST_START m04.021.single-finally" && ordered[64] == "CLROS_M04_SINGLE_FINALLY_READY" &&
            ordered[65] == "TEST_START boot.sequence", "child follows collector and precedes boot");
        Need(Read("FORBIDDEN_MARKERS ").Contains("TEST_FAIL", StringComparer.Ordinal), "failure markers forbidden");
    }

    private static void Check(string name, string[] properties, int expectedExit, string? expectedReason)
        => RunCommand(name, "dotnet", ["msbuild", "src/OS/OS.csproj", "-nologo", "-t:ValidateM01ProbeSwitches", ..properties],
            expectedExit, expectedReason);

    [HostTest("m04.021.finally.efi actual admission guard core", HostLane.Native, 3)]
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
        RunCommand("guard-core-build", "dotnet", ["build", project, "--nologo", "-m:1", "--no-dependencies"], 0, null);
        string output = RunCommand("guard-core-run", "dotnet", ["run", "--project", project, "--no-build"], 0, "FINALLY_ADMISSION_CORE_PASS");
        Need(!output.Contains("TEST_FAIL ", StringComparison.Ordinal), "actual guard rejection cases pass");
    }

    [HostTest("m04.021.finally.efi strict complete finally transcript", HostLane.Fast, 4)]
private static void Transcript() => EfiExceptionTranscriptControls.Run(finallyProfile: true);



    [HostTest("m04.021.finally.efi exact native clause and unwind guards", HostLane.Fast, 5)]
private static void NativeMetadata()
{
    const ulong type = 0x12340, tableRva = 0x45670;
    byte[] table = [0x04, 0x36, 0x32, 0x8e, 0x36, 0xa0, 0xbc, ..BitConverter.GetBytes((uint)type)];
    byte[] rootData = [0x01, 0x06, 0x03, 0x00, 0x06, 0x62, 0x02, 0x30, 0x01, 0x50, 0x04, ..BitConverter.GetBytes((uint)tableRva)];
    byte[] funclet = [0x01, 0x04, 0x01, 0x00, 0x04, 0x42, 0x01];
    Need(OS.Tooling.EfiVerify.TestSingleFinallyEhTable(table, type) &&
        OS.Tooling.EfiVerify.TestSingleFinallyUnwindBytes(rootData, true, tableRva) &&
        OS.Tooling.EfiVerify.TestSingleFinallyUnwindBytes(funclet, false, tableRva), "exact measured positive bytes");
    for (int i = 0; i < table.Length; i++)
    {
        byte[] bad = [..table]; bad[i] ^= 1;
        Need(!OS.Tooling.EfiVerify.TestSingleFinallyEhTable(bad, type), "table byte mutation " + i);
    }
    foreach (bool root in new[] { true, false })
    {
        byte[] original = root ? rootData : funclet;
        for (int i = 0; i < original.Length; i++)
        {
            byte[] bad = [..original]; bad[i] ^= 1;
            Need(!OS.Tooling.EfiVerify.TestSingleFinallyUnwindBytes(bad, root, tableRva), "unwind byte mutation " + i);
        }
        for (int length = 0; length < original.Length; length++)
            Need(!OS.Tooling.EfiVerify.TestSingleFinallyUnwindBytes(original[..length], root, tableRva), "truncated unwind " + length);
        Need(!OS.Tooling.EfiVerify.TestSingleFinallyUnwindBytes([..original, 0], root, tableRva), "extended unwind");
    }
    for (int length = 0; length < table.Length; length++)
        Need(!OS.Tooling.EfiVerify.TestSingleFinallyEhTable(table[..length], type), "truncated table " + length);
    foreach (ulong badType in new[] { 0UL, type + 1, (ulong)uint.MaxValue + 1 })
        Need(!OS.Tooling.EfiVerify.TestSingleFinallyEhTable(table, badType), "wrong type RVA");
    Need(!OS.Tooling.EfiVerify.TestSingleFinallyEhTable([..table, 0], type) &&
        !OS.Tooling.EfiVerify.TestSingleFinallyEhTable(null, type) &&
        !OS.Tooling.EfiVerify.TestSingleFinallyUnwindBytes(rootData, true, tableRva + 1) &&
        !OS.Tooling.EfiVerify.TestSingleFinallyUnwindBytes(rootData, true, 0) &&
        !OS.Tooling.EfiVerify.TestSingleFinallyUnwindBytes(null, false, tableRva), "length null and table association");
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
