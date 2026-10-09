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

internal static unsafe class M04RethrowCatchEfiIntegrationTests
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
            ProjectAdmission(root, folder);
            VerifierContracts(root, folder);
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
    private static void ProjectAdmission(string root, string folder)
    {
        const string flag = "EnableM04RethrowProbe";
        var probe = OS.Kernel.Diagnostics.Probes.GetM04RethrowProbe();
        var disabled = OS.Boot.BootSequence.RunRethrowUnwind(default, default, null, 0, 0, null, 0, null, 0, default);
        if (OS.Boot.BootSequence.IsRethrowEnabled() || disabled.Succeeded || disabled.Caller.Rip != 0 ||
            disabled.Caller.Rsp != 0 || probe.Name != flag || probe.EnabledByDefault || probe.Risk != OS.Kernel.Diagnostics.ProbeRisk.Destructive)
            throw new InvalidOperationException("Default boot authority must not dispatch rethrow");
        string[] common = ["msbuild", Path.Combine(root, "src/OS/OS.csproj"), "-nologo",
            "-t:ValidateClrosBootPolicy;ValidateM01ProbeSwitches"];
        string[] ancestry = ["EnableM01LoadedImageStackProbe", "EnableM01LiveAllocatorProbe",
            "EnableM04IndependentEndianConformanceProbe", "EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe",
            "EnableM04ArithmeticConformanceProbe", "EnableM04LayoutContractProbe", "EnableM04ManagedHeapPageRangeProbe",
            "EnableM04RhpNewFastProbe", "EnableM04GcStaticRootMaterializationProbe"];
        string[] chain = ancestry.Select(x => "-p:" + x + "=true").ToArray();
        Invoke(root, folder, "profile-default", common);
        string enabled = Invoke(root, folder, "profile-enabled", [..common, ..chain, "-p:" + flag + "=true", "-getProperty:DefineConstants"]);
        if (!enabled.Contains("M04_RETHROW_PROBE", StringComparison.Ordinal)) throw new InvalidOperationException("Missing explicit rethrow compile authority");
        void Reject(string name, string[] arguments, string reason)
        {
            string output = Invoke(root, folder, name, [..common, ..arguments], 1);
            if (!output.Contains(reason, StringComparison.Ordinal)) throw new InvalidOperationException(name + ": " + output);
        }
        Reject("profile-invalid", [..chain, "-p:" + flag + "=invalid"], "m04_rethrow_switch_invalid");
        Reject("profile-missing-gc", ["-p:" + flag + "=true"], "m04_rethrow_requires_gc_profile");
        Reject("profile-policy-conflict", [..chain, "-p:" + flag + "=true", "-p:ClrosBootPolicy=VerifiedExit"], "clros_boot_policy_probe_conflict");
        foreach (string collision in new[] { "EnableM04SimpleThrowCatchProbe", "EnableM04SingleFinallyCatchProbe",
            "EnableM04SeededCollectorChurnProbe", "EnableM04ClassConstructorExecutionProbe" })
            Reject("profile-conflict-" + collision, [..chain, "-p:" + flag + "=true", "-p:" + collision + "=true"], "m04_rethrow_profile_conflict");
        Console.WriteLine("RETHROW_PROJECT_ADMISSION_PASS");
    }
    private static void VerifierContracts(string root, string folder)
    {
        string script = Path.Combine(folder, "verifier-contracts.ps1");
        File.WriteAllText(script, """
            param([string]$Root)
            $ErrorActionPreference='Stop'
            $tokens=$null; $errors=$null
            $ast=[Management.Automation.Language.Parser]::ParseFile(
                (Join-Path $Root 'scripts/test-efi-artifact.ps1'),[ref]$tokens,[ref]$errors)
            if($errors.Count){throw 'Verifier parse failure'}
            foreach($name in @('Test-RethrowEhTable','Test-RethrowUnwindBytes')) {
                $nodes=@($ast.FindAll({param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -ceq $name},$true))
                if($nodes.Count -ne 1){throw 'Missing exact predicate'}
                . ([scriptblock]::Create($nodes[0].Extent.Text))
            }
            function Need([bool]$Value,[string]$Reason){if(-not $Value){throw $Reason}}
            foreach($inner in @($true,$false)) {
                [byte[]]$table=if($inner){@(4,40,50,92,40,160,138,88,152,5,0)}else{@(2,42,48,102,88,152,5,0)}
                Need (Test-RethrowEhTable $table $inner 0x59858) 'measured clauses'
                foreach($badRva in @(0,0x59859,0x100000000L)) {
                    Need (-not (Test-RethrowEhTable $table $inner $badRva)) 'wrong type identity'
                }
                Need (-not (Test-RethrowEhTable $null $inner 0x59858)) 'missing table'
                Need (-not (Test-RethrowEhTable ($table+[byte]0) $inner 0x59858)) 'trailing table byte'
                for($length=0;$length -lt $table.Length;$length++) {
                    [byte[]]$short=New-Object byte[] $length; [Array]::Copy($table,$short,$length)
                    Need (-not (Test-RethrowEhTable $short $inner 0x59858)) 'truncated table'
                }
                for($at=0;$at -lt $table.Length;$at++) {
                    [byte[]]$bad=$table.Clone(); $bad[$at]=$bad[$at] -bxor 1
                    Need (-not (Test-RethrowEhTable $bad $inner 0x59858)) 'changed table byte'
                }
                foreach($rootRow in @($true,$false)) {
                    [byte[]]$data=if(-not $rootRow){@(1,4,1,0,4,66,1)}elseif($inner){@(1,5,2,0,5,82,1,80,4,0,176,5,0)}else{@(1,6,3,0,6,66,2,48,1,80,4,0,176,5,0)}
                    Need (Test-RethrowUnwindBytes $data $inner $rootRow 0x5B000) 'measured unwind'
                    Need (-not (Test-RethrowUnwindBytes $null $inner $rootRow 0x5B000)) 'missing unwind'
                    Need (-not (Test-RethrowUnwindBytes ($data+[byte]0) $inner $rootRow 0x5B000)) 'trailing unwind'
                    for($at=0;$at -lt $data.Length;$at++) {
                        [byte[]]$bad=$data.Clone(); $bad[$at]=$bad[$at] -bxor 1
                        Need (-not (Test-RethrowUnwindBytes $bad $inner $rootRow 0x5B000)) 'changed unwind byte'
                    }
                    for($length=0;$length -lt $data.Length;$length++) {
                        [byte[]]$short=New-Object byte[] $length; [Array]::Copy($data,$short,$length)
                        Need (-not (Test-RethrowUnwindBytes $short $inner $rootRow 0x5B000)) 'truncated unwind'
                    }
                    if($rootRow) {
                        foreach($badRva in @(0,0x5B001,0x100000000L)) {
                            Need (-not (Test-RethrowUnwindBytes $data $inner $rootRow $badRva)) 'wrong EH trailer'
                        }
                    }
                }
            }
            $ast=[Management.Automation.Language.Parser]::ParseFile(
                (Join-Path $Root 'scripts/run-qemu-smoke.ps1'),[ref]$tokens,[ref]$errors)
            if($errors.Count){throw 'Runner parse failure'}
            foreach($name in @('Assert-M04RethrowRecords','Assert-M04RethrowSelection','Assert-M04SimpleCatchSelection')) {
                $nodes=@($ast.FindAll({param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -ceq $name},$true))
                if($nodes.Count -ne 1){throw 'Missing runner predicate'}
                . ([scriptblock]::Create($nodes[0].Extent.Text))
            }
            $heap=@{retainedStart=0x300000;retainedEnd=0x301000;backing=0x300000}
            $parents=@(@(0x100000,0x100000,0,0),@(0,0,0,0x70007),@(0x800000,0x20000,0x81FFF0,0),@(0,0xCAFE,0xBEEF,0))
            $gc=New-Object string[] 17; $indices=@(0,1,2,7)
            for($i=0;$i -lt 4;$i++){$gc[$indices[$i]]='M04_GC_ROOT '+$indices[$i]+' '+(($parents[$i]|ForEach-Object{'0x{0:X16}' -f [UInt64]$_}) -join ' ')}
            $rows=@{
                0=@(0x70007,0x180000,400,5);1=@(0x300008,0x140000,9,3)
                2=@(0x111009,0x111008,0x818000,0);3=@(0x111009,0x818000,0x110019,0x818030)
                4=@(0x140040,0x110045,0x818060,0x818028);9=@(0x300008,0x818058,9,1)
                10=@(0x11002E,0x110045,0x818058,9);5=@(0x300008,7,9,1)
                14=@(0x110057,0x817C80,0x817DC0,0x300008);11=@(0x110019,0x818030,0x11201A,0x818070)
                12=@(0x140040,0x112033,0x8180A0,0x818068);13=@(0x300008,7,0x112033,1)
                6=@(0x11201B,0x818070,0x8180A0,1);7=@(0x300008,7,8,1);8=@(10,0x810000,0xCAFE,0xBEEF)
            }
            function Records {
                'TEST_START m04.021.rethrow'
                foreach($stage in @(0,1,2,3,4,9,10,5,14,11,12,13,6,7,8)) {
                    'M04_RETHROW '+$stage+' '+(($rows[$stage]|ForEach-Object{'0x{0:X16}' -f [UInt64]$_}) -join ' ')
                }
                'TEST_PASS m04.021.rethrow';'CLROS_M04_RETHROW_READY'
            }
            $valid=@(Records)
            Assert-M04RethrowRecords $valid $gc $heap
            foreach($stage in @(1,2,3,4,5,6,7,9,10,11,12,13,14)) {
                $saved=$rows[$stage][0];$rows[$stage][0]=$saved -bxor 1
                $rejected=$false;try{Assert-M04RethrowRecords @(Records) $gc $heap}catch{$rejected=$true}
                Need $rejected ('changed identity or native association '+$stage);$rows[$stage][0]=$saved
            }
            $reordered=$valid.Clone();$reordered[6]=$valid[8];$reordered[8]=$valid[6]
            $rejected=$false;try{Assert-M04RethrowRecords $reordered $gc $heap}catch{$rejected=$true}
            Need $rejected 'cleanup/catch order'
            foreach($claim in @('M04_RETHROW 0',"M04_`nRETHROW 0",'CLROS_M04_RETHROW_READY','TEST_PASS m04.021.rethrow')) {
                $rejected=$false;try{Assert-M04RethrowSelection $claim $false}catch{$rejected=$true}
                Need $rejected 'unselected rethrow claim'
            }
            Assert-M04SimpleCatchSelection 'TEST_PASS m04.021.rethrow' $false $false $true
            'RETHROW_VERIFIER_CONTRACT_PASS'
            """);
        string output = Invoke(root, folder, "verifier-contracts",
            ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", script, "-Root", root], executable: "powershell");
        if (!output.Contains("RETHROW_VERIFIER_CONTRACT_PASS", StringComparison.Ordinal))
            throw new InvalidOperationException("Missing verifier predicate result");
    }
    private static string Invoke(string root, string folder, string name, string[] arguments, int expectedExit = 0, string executable = "dotnet")
    {
        var start = new ProcessStartInfo(executable) { WorkingDirectory = root, UseShellExecute = false,
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
        if (!ended || process.ExitCode != expectedExit || error.Length != 0)
            throw new InvalidOperationException(name + " exit=" + process.ExitCode + " " + output + error);
        return output;
    }
}
#endif
