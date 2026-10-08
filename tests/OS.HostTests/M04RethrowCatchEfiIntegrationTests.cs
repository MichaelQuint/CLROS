#if RETHROW_CORE_TEST
using System.Reflection;
using OS.Kernel.Runtime;
using OS.Runtime;

internal static unsafe class RethrowAdmissionCoreProgram
{
    private static void Need(bool ok, string reason) { if (!ok) throw new InvalidOperationException(reason); }
    private static int Main()
    {
        Run("frames", Frames);
        Run("active-state", ActiveState);
        Run("clauses", Clauses);
        Run("continuation", Continuation);
        Console.WriteLine("RETHROW_ADMISSION_CORE_PASS"); return 0;
    }
    private static void Run(string name, Action test)
    {
        Console.WriteLine("TEST_START rethrow.admission." + name);
        try { test(); Console.WriteLine("TEST_PASS rethrow.admission." + name); }
        catch (Exception ex) { Console.WriteLine("TEST_FAIL rethrow.admission." + name + " " + ex); throw; }
    }
    private static X64UnwindContext Context()
    {
        X64UnwindContext value = default;
        Need(sizeof(X64UnwindContext) == 240, "complete 30-word context ABI");
        for (int i = 0; i < 30; i++) ((ulong*)&value)[i] = 0x10000UL + (ulong)(i * 17);
        return value;
    }
    private static void Frames()
    {
        Need(RethrowAdmission.InitialFramesMatch(0x10000, 0x10030, 0x10060), "thrower 40+8, inner RBP +48");
        Need(RethrowAdmission.OuterFramesMatch(0x10030, 0x10070, 0x100A0), "inner 48+8+8, outer RBP +48");
        foreach (ulong delta in new ulong[] { 0, 8, 16, 32, 40, 48, 56, 64, 72 })
        {
            Need(RethrowAdmission.InitialFramesMatch(0x10000, 0x10000 + delta, 0x10030 + delta) == (delta == 48), "exact first unwind");
            Need(RethrowAdmission.OuterFramesMatch(0x10000, 0x10000 + delta, 0x10030 + delta) == (delta == 64), "exact second unwind");
        }
        Need(!RethrowAdmission.InitialFramesMatch(0, 48, 96) &&
            !RethrowAdmission.InitialFramesMatch(ulong.MaxValue - 47, 0, 48) &&
            !RethrowAdmission.InitialFramesMatch(ulong.MaxValue - 95, ulong.MaxValue - 47, 0), "null/wrapped first frame");
        Need(!RethrowAdmission.OuterFramesMatch(0, 64, 112) &&
            !RethrowAdmission.OuterFramesMatch(ulong.MaxValue - 63, 0, 48) &&
            !RethrowAdmission.OuterFramesMatch(ulong.MaxValue - 111, ulong.MaxValue - 47, 0), "null/wrapped outer frame");

        const ulong transfer = 0x20110, method = 0x400000, capture = 0x1FCA0;
        X64UnwindContext inner = Context();
        inner.Rip = method + 25; inner.Rsp = 0x20380; inner.Rbp = 0x203B0;
        X64UnwindContext current = inner; current.Rip = method + 87; current.Rsp = 0x1FFD0;
        Need(RethrowAdmission.RethrowFrameMatches(transfer, capture, method, &current, &inner), "two bridges plus inner funclet");
        byte[] before = new ReadOnlySpan<byte>(&inner, 240).ToArray();
        for (int word = 0; word < 30; word++)
        {
            X64UnwindContext changed = current; ((ulong*)&changed)[word] ^= 1;
            Need(!RethrowAdmission.RethrowFrameMatches(transfer, capture, method, &changed, &inner), "corrupt capture word " + word);
            changed = inner; ((ulong*)&changed)[word] ^= 1;
            Need(!RethrowAdmission.RethrowFrameMatches(transfer, capture, method, &current, &changed), "corrupt active context word " + word);
        }
        foreach (ulong bad in new ulong[] { 0, 1167, transfer - 8, transfer + 16, ulong.MaxValue - 671 })
            Need(!RethrowAdmission.RethrowFrameMatches(bad, capture, method, &current, &inner), "invalid original bridge");
        Need(!RethrowAdmission.RethrowFrameMatches(transfer, capture + 16, method, &current, &inner) &&
            !RethrowAdmission.RethrowFrameMatches(transfer, capture, method + 1, &current, &inner) &&
            !RethrowAdmission.RethrowFrameMatches(transfer, capture, 0, &current, &inner) &&
            !RethrowAdmission.RethrowFrameMatches(transfer, capture, ulong.MaxValue - 87, &current, &inner) &&
            !RethrowAdmission.RethrowFrameMatches(transfer, capture, method, null, &inner) &&
            !RethrowAdmission.RethrowFrameMatches(transfer, capture, method, &current, null), "wrong location, method or missing capture");
        Need(before.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(&inner, 240)), "rejection never changes retained context");
    }
    private static void ActiveState()
    {
        ulong[] state = [7, 0x700000, 0x700000, 1, 1, 0, 0, 0];
        bool Accept(ulong[] s) => RethrowAdmission.ActiveStateMatches(s[0], s[1], s[2], s[3], s[4], s[5], s[6], s[7]);
        Need(Accept(state), "original exception after one cleanup and inner catch");
        foreach (ulong phase in Enumerable.Range(0, 14).Select(x => (ulong)x).Append(ulong.MaxValue))
        {
            ulong[] bad = (ulong[])state.Clone(); bad[0] = phase;
            Need(Accept(bad) == (phase == 7), "inactive, stale, reentered or finished phase");
        }
        for (int field = 1; field < state.Length; field++)
        foreach (ulong replacement in new ulong[] { 0, 1, 2, 0x700001, ulong.MaxValue })
        {
            ulong[] bad = (ulong[])state.Clone(); bad[field] = replacement;
            Need(Accept(bad) == (replacement == state[field]), "identity, count or incoming-argument change " + field);
        }
        Need(!RethrowAdmission.ActiveStateMatches(7, 0, 0, 1, 1, 0, 0, 0), "matching null identities cannot create an active exception");
    }
    private static readonly ConstructorInfo ClauseConstructor = typeof(NativeAotEhClauseResult).GetConstructor(
        BindingFlags.Instance | BindingFlags.NonPublic, null,
        [typeof(uint), typeof(uint), typeof(uint), typeof(uint), typeof(NativeAotEhClauseKind),
         typeof(ulong), typeof(ulong), typeof(ulong), typeof(bool)], null)!;
    private static NativeAotEhClauseResult Clause(object[] row) => (NativeAotEhClauseResult)ClauseConstructor.Invoke(row);
    private static void Clauses()
    {
        // Parser-result policy for the actual step1096 contribution. These
        // synthetic results do not authenticate a module or exercise decoding.
        object[] inner = [1U, 2U, 20U, 40U, NativeAotEhClauseKind.Typed, 0x400045UL, 0UL, 0x700000UL, false];
        object[] cleanup = [0U, 2U, 20U, 26U, NativeAotEhClauseKind.FaultOrFinally, 0x40002EUL, 0UL, 0UL, false];
        object[] outer = [0U, 1U, 21U, 27U, NativeAotEhClauseKind.Typed, 0x500033UL, 0UL, 0x700000UL, false];
        bool Inner(NativeAotEhClauseResult row, ulong method = 0x400000, ulong type = 0x700000, uint pc = 24) =>
            RethrowAdmission.AcceptInnerCatch(row, method, type, pc);
        bool Cleanup(NativeAotEhClauseResult row, NativeAotEhClauseResult caught) =>
            RethrowAdmission.AcceptInnerCleanup(row, caught, 0x400000, 0x700000, 24);
        bool Outer(NativeAotEhClauseResult row, ulong method = 0x500000, ulong type = 0x700000, uint pc = 25) =>
            RethrowAdmission.AcceptOuterCatch(row, method, type, pc);
        Need(Inner(Clause(inner)) && Cleanup(Clause(cleanup), Clause(inner)) && Outer(Clause(outer)), "exact nested tables");
        for (int field = 0; field < 9; field++)
        foreach (object[] original in new[] { inner, cleanup, outer })
        {
            object[] changed = (object[])original.Clone();
            changed[field] = field < 4 ? (object)((uint)changed[field] + 1) : field == 4 ? NativeAotEhClauseKind.Filter :
                field < 8 ? (ulong)changed[field] + 1 : true;
            NativeAotEhClauseResult bad = Clause(changed);
            Need(original == inner ? !Inner(bad) && !Cleanup(Clause(cleanup), bad) :
                original == cleanup ? !Cleanup(bad, Clause(inner)) : !Outer(bad), "changed clause field " + field);
        }
        Need(!Inner(default) && !Outer(default) && !Cleanup(default, Clause(inner)) &&
            !Cleanup(Clause(cleanup), default), "failed parser cannot authorize any target");
        foreach (uint pc in new uint[] { 0, 20, 23, 24, 25, 26, 27, 39, 40, 86, uint.MaxValue })
        {
            Need(Inner(Clause(inner), pc: pc) == (pc == 24), "inner exact protected call site");
            Need(Outer(Clause(outer), pc: pc) == (pc == 25), "outer exact protected call site");
        }
        Need(!Inner(Clause(inner), method: 0) && !Inner(Clause(inner), method: ulong.MaxValue - 87) &&
            !Inner(Clause(inner), type: 0) && !Outer(Clause(outer), method: 0) &&
            !Outer(Clause(outer), method: ulong.MaxValue - 88) && !Outer(Clause(outer), type: 0), "null/overflowed contributions and types");
    }
    private static void Continuation()
    {
        const ulong method = 0x500000, exception = 0x700000;
        X64UnwindContext selected = Context(); selected.Rip = method + 26;
        X64UnwindContext actual = selected; actual.Rip = method + 27; actual.Rbx = exception;
        Need(RethrowAdmission.OuterContinuationMatches(method, exception, &actual, &selected), "real catch RIP/RBX, all other words retained");
        byte[] original = new ReadOnlySpan<byte>(&actual, 240).ToArray();
        for (int word = 0; word < 30; word++)
        {
            X64UnwindContext changed = actual; ((ulong*)&changed)[word] ^= 1;
            Need(!RethrowAdmission.OuterContinuationMatches(method, exception, &changed, &selected), "corrupt continuation word " + word);
        }
        Need(!RethrowAdmission.OuterContinuationMatches(method, exception + 1, &actual, &selected) &&
            !RethrowAdmission.OuterContinuationMatches(method, 0, &actual, &selected) &&
            !RethrowAdmission.OuterContinuationMatches(0, exception, &actual, &selected) &&
            !RethrowAdmission.OuterContinuationMatches(ulong.MaxValue - 88, exception, &actual, &selected) &&
            !RethrowAdmission.OuterContinuationMatches(method, exception, null, &selected) &&
            !RethrowAdmission.OuterContinuationMatches(method, exception, &actual, null), "missing or changed final authority");
        selected.Rip++;
        Need(!RethrowAdmission.OuterContinuationMatches(method, exception, &actual, &selected), "selected return site cannot become the continuation early");
        Need(original.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(&actual, 240)), "rejected continuation remains unchanged");
    }
}
#else
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Xml.Linq;

internal static class M04RethrowCatchEfiIntegrationTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        const string name = "m04.021.rethrow.admission";
        Console.WriteLine("TEST_START " + name);
        try
        {
            string root = HostToolProcess.FindRepositoryRoot();
            string folder = Path.Combine(root, "artifacts/rethrow-admission-core", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            string project = Path.Combine(folder, "RethrowAdmission.Core.csproj");
            new XDocument(new XElement("Project", new XAttribute("Sdk", "Microsoft.NET.Sdk"),
                new XElement("PropertyGroup", new XElement("OutputType", "Exe"), new XElement("TargetFramework", "net10.0"),
                    new XElement("LangVersion", "14.0"), new XElement("Nullable", "enable"), new XElement("ImplicitUsings", "enable"),
                    new XElement("AllowUnsafeBlocks", "true"), new XElement("EnableDefaultCompileItems", "false"),
                    new XElement("DefineConstants", "RETHROW_CORE_TEST")),
                new XElement("ItemGroup",
                    new XElement("Compile", new XAttribute("Include", Path.Combine(root, "src/OS/Runtime/ExceptionRethrowProbe.cs"))),
                    new XElement("Compile", new XAttribute("Include", Path.Combine(root, "tests/OS.HostTests/M04RethrowCatchEfiIntegrationTests.cs"))),
                    new XElement("ProjectReference", new XAttribute("Include", Path.Combine(root, "src/OS.Kernel/OS.Kernel.csproj")))))).Save(project);
            Invoke(root, folder, "build", ["build", project, "--nologo", "-m:1"]);
            string output = Invoke(root, folder, "run", ["run", "--project", project, "--no-build"]);
            if (!output.Contains("RETHROW_ADMISSION_CORE_PASS", StringComparison.Ordinal) || output.Contains("TEST_FAIL ", StringComparison.Ordinal))
                throw new InvalidOperationException("Missing complete admission-core result: " + output);
            Console.WriteLine("RETHROW_ADMISSION_EVIDENCE " + folder);
            Console.WriteLine("TEST_PASS " + name);
        }
        catch (Exception ex) { Console.WriteLine("TEST_FAIL " + name + " " + ex); throw; }
    }
    private static string Invoke(string root, string folder, string name, string[] arguments)
    {
        var start = new ProcessStartInfo("dotnet") { WorkingDirectory = root, UseShellExecute = false,
            CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string argument in arguments) start.ArgumentList.Add(argument);
        File.WriteAllText(Path.Combine(folder, name + ".arguments.json"), JsonSerializer.Serialize(arguments));
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        bool ended = process.WaitForExit(60000);
        if (!ended) { process.Kill(true); process.WaitForExit(); }
        string output = stdout.GetAwaiter().GetResult(), error = stderr.GetAwaiter().GetResult();
        File.WriteAllText(Path.Combine(folder, name + ".out"), output);
        File.WriteAllText(Path.Combine(folder, name + ".err"), error);
        if (!ended || process.ExitCode != 0 || error.Length != 0)
            throw new InvalidOperationException(name + " exit=" + process.ExitCode + " " + output + error);
        return output;
    }
}
#endif
