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

    [ModuleInitializer]
    internal static void RunAll()
    {
        Run("default authority", Defaults);
        Run("project switch admission", Switches);
        Run("unadmitted smoke refuses execution", UnadmittedSmoke);
        Run("actual admission guard core", GuardCore);
        Run("strict complete finally transcript", Transcript);
        Run("exact native clause and unwind guards", NativeMetadata);
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

    private static void Transcript()
    {
        string directory = Path.Combine(Root, "artifacts/finally-transcript", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string source = File.ReadAllText(Path.Combine(Root, "tests/OS.HostTests/M04GCStaticRootMaterializationEfiIntegrationTests.cs")).Replace("\r\n", "\n", StringComparison.Ordinal);
        const string marker = "private const string Protocol = \"\"\"\n";
        int at = source.IndexOf(marker, StringComparison.Ordinal);
        Need(at >= 0 && source.IndexOf(marker, at + 1, StringComparison.Ordinal) < 0, "unique ancestor transcript");
        at += marker.Length; int end = source.IndexOf("        \"\"\";", at, StringComparison.Ordinal);
        Need(end > at, "complete ancestor literal");
        string[] lines = source[at..end].TrimEnd('\n').Split('\n').Select(x => x[8..]).ToArray();
        Need(lines.Length == 70, "unchanged complete GC ancestor");
        string ancestor = Path.Combine(directory, "ancestor.serial");
        File.WriteAllText(ancestor, string.Join("\r\n", lines) + "\r\n", new System.Text.UTF8Encoding(false));
        string script = Path.Combine(directory, "controls.ps1");
        File.WriteAllText(script, TranscriptControls, new System.Text.UTF8Encoding(false));
        RunCommand("transcript", "powershell.exe", ["-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", script,
            "-Source", Path.Combine(Root, "scripts/run-qemu-smoke.ps1"), "-Ancestor", ancestor], 0, "TEST_PASS single.finally.transcript");
    }

    private const string TranscriptControls = """
        param([string]$Source,[string]$Ancestor)
        $ErrorActionPreference='Stop'
        $tokens=$null;$errors=$null
        $ast=[Management.Automation.Language.Parser]::ParseFile($Source,[ref]$tokens,[ref]$errors)
        if($errors.Count){throw 'parser syntax'}
        foreach($statement in $ast.EndBlock.Statements){
            if($statement.Extent.Text -cmatch '^\$script:M04(?:IndependentEndianConformance|SpanMemoryConformance|ArithmeticConformance|LayoutContract)\w+ =' -or
               $statement.Extent.Text -cmatch '^\$script:M04(?:ManagedHeapPageRange|GcStaticRoot|SingleFinally)RequiredMarkers ='){
                . ([scriptblock]::Create($statement.Extent.Text))
            }
        }
        foreach($name in @('Assert-M04ManagedHeapPageRangeTranscript','Assert-M04GcStaticRootRecords','Assert-M04SingleFinallyRecords','Assert-M04SingleFinallySelection','Assert-M04SimpleCatchSelection')){
            $f=@($ast.FindAll({param($n)$n -is [Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -ceq $name},$true))
            if($f.Count -ne 1){throw 'one actual function'};. ([scriptblock]::Create($f[0].Extent.Text))
        }
        $stages=@(0,1,2,3,4,9,10,5,6,7,8)
        $markers=@($script:M04SingleFinallyRequiredMarkers)
        if($markers.Count -ne 73 -or $markers[50] -cne 'CLROS_M04_013_READY' -or $markers[51] -cne 'TEST_START m04.021.single-finally' -or
            $markers[64] -cne 'CLROS_M04_SINGLE_FINALLY_READY' -or $markers[65] -cne 'TEST_START boot.sequence'){throw 'whole marker vector'}
        for($i=0;$i -lt $stages.Count;$i++){if($markers[52+$i] -cne ('M04_FINALLY '+$stages[$i])){throw 'ordered child marker'}}
        $base=[IO.File]::ReadAllText($Ancestor).Split(@("`r`n"),[StringSplitOptions]::RemoveEmptyEntries)
        function Fields($line){$p=$line.Split(' ');,@((2..5|ForEach-Object{[Convert]::ToUInt64($p[$_].Substring(2),16)}))}
        function Row($stage,$v){'M04_FINALLY '+$stage+' '+(($v|ForEach-Object{'0x'+([UInt64]$_).ToString('X16')})-join ' ')}
        $image=Fields $base[44];$module=Fields $base[45];$stack=Fields $base[46];$old=Fields $base[48];$checked=Fields $base[51]
        $heap=[UInt64]$old[0]-32;$seed=([UInt64]$module[3] -bxor $heap)-band 0xFFFF;$sp=[UInt64]$stack[1]-1024
        $rip=[UInt64]$image[0]+4105;$catcher=[UInt64]$image[0]+8192;$parent=$sp+48;$rbp=$parent+64;$stamp=$rbp-24
        $rows=@{
            0=@($module[3],($image[0]+65536),512,3);1=@($old[0],($old[1]+64),9,3)
            2=@($rip,($rip -bxor [UInt64]1),$sp,0);3=@($rip,$sp,($catcher+32),$parent)
            4=@(($old[1]+32),($catcher+94),$rbp,($sp+40));9=@($old[0],$stamp,($seed+2),1)
            10=@(($catcher+71),($catcher+94),$stamp,($seed+2));5=@($old[0],$seed,($seed+2),1)
            6=@(($catcher+47),$parent,$rbp,1);7=@($old[0],$seed,($seed+1),1);8=@(8,($sp-4096),$checked[1],$checked[2])
        }
        $child=@('TEST_START m04.021.single-finally';foreach($stage in $stages){Row $stage $rows[$stage]};'TEST_PASS m04.021.single-finally';'CLROS_M04_SINGLE_FINALLY_READY')
        $lines=@($base[0..61])+$child+@($base[62..69]);$good=($lines -join "`r`n")+"`r`n"
        $serial=Join-Path (Split-Path -Parent $Ancestor) 'case.serial';$script:cases=0
        function Check([string]$text,[bool]$accept){
            [IO.File]::WriteAllText($serial,$text,[Text.UTF8Encoding]::new($false));$ok=$true
            try{Assert-M04ManagedHeapPageRangeTranscript -SerialLogPath $serial -IncludeGcStatics -IncludeSingleFinally}catch{if($accept){throw};$ok=$false}
            if($ok -ne $accept){throw ('unexpected admission case='+$script:cases+' expected='+$accept)};$script:cases++
        }
        Check $good $true
        foreach($i in 62..75){
            Check ((@($lines[0..($i-1)])+@($lines[($i+1)..83])-join "`r`n")+"`r`n") $false
            Check ((@($lines[0..$i])+@($lines[$i..83])-join "`r`n")+"`r`n") $false
            $bad=$lines.Clone();$bad[$i]+=' ';Check (($bad-join "`r`n")+"`r`n") $false
        }
        for($i=0;$i -lt $stages.Count;$i++){
            for($field=0;$field -lt 4;$field++){
                $bad=$lines.Clone();$values=$rows[$stages[$i]].Clone();$values[$field]=[UInt64]::MaxValue
                $bad[63+$i]=Row $stages[$i] $values;Check (($bad-join "`r`n")+"`r`n") $false
            }
        }
        foreach($pair in @(@(67,68),@(68,69),@(69,70),@(70,71),@(71,72),@(72,73))){
            $bad=$lines.Clone();$tmp=$bad[$pair[0]];$bad[$pair[0]]=$bad[$pair[1]];$bad[$pair[1]]=$tmp
            Check (($bad-join "`r`n")+"`r`n") $false
        }
        foreach($value in @($rip,($rip -bxor [UInt64]2))){
            $bad=$lines.Clone();$v=$rows[2].Clone();$v[1]=$value;$bad[65]=Row 2 $v;Check (($bad-join "`r`n")+"`r`n") $false
        }
        foreach($tail in @("`r`n","noise`r`n","TEST_FAIL m04.021.single-finally 1`r`n","T E S T_`tFAIL bad`r`n")){Check ($good+$tail) $false}
        Check ($good.Replace("`r`n","`n")) $false;Check ($good.TrimEnd([char]10)) $false
        Check ("M04_`tFINALLY 9`r`n"+$good) $false;Check ($good.Replace('M04_FINALLY 9 ',"M04_FINALLY`t9 ")) $false
        Check ($good.Replace('TEST_START m04.021.single-finally','TEST_START m04.021.simple-catch')) $false
        Assert-M04SingleFinallySelection -Text $good -Enabled $true
        Assert-M04SimpleCatchSelection -Text $good -Enabled $false -FinallyEnabled $true
        foreach($text in @('M04_FINALLY','CLROS_M04_SINGLE_FINALLY_READY',"m04.`t021.single-finally")){
            $ok=$true;try{Assert-M04SingleFinallySelection -Text $text -Enabled $false}catch{$ok=$false};if($ok){throw 'unselected finally claim accepted'}
        }
        $ok=$true;try{Assert-M04SimpleCatchSelection -Text 'M04_CATCH' -Enabled $false -FinallyEnabled $true}catch{$ok=$false};if($ok){throw 'sibling collision'}
        Write-Output ('FINALLY_TRANSCRIPT_CASES '+$script:cases)
        Write-Output 'TEST_PASS single.finally.transcript'
        """;

    private static void NativeMetadata()
    {
        string directory = Path.Combine(Root, "artifacts/finally-native-metadata-guards", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string script = Path.Combine(directory, "controls.ps1");
        File.WriteAllText(script, """
            param([string]$Source)
            $ErrorActionPreference='Stop'
            $tokens=$null;$errors=$null
            $ast=[Management.Automation.Language.Parser]::ParseFile($Source,[ref]$tokens,[ref]$errors)
            if($errors.Count){throw 'parser syntax'}
            foreach($name in @('Test-SingleFinallyEhTable','Test-SingleFinallyUnwindBytes')){
                $f=@($ast.FindAll({param($n)$n -is [Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -ceq $name},$true))
                if($f.Count -ne 1){throw 'one actual guard'};. ([scriptblock]::Create($f[0].Extent.Text))
            }
            $type=[UInt64]0x12340;$tableRva=[UInt64]0x45670
            [byte[]]$table=@(0x04,0x36,0x32,0x8e,0x36,0xa0,0xbc)+[BitConverter]::GetBytes([UInt32]$type)
            [byte[]]$rootData=@(0x01,0x06,0x03,0x00,0x06,0x62,0x02,0x30,0x01,0x50,0x04)+[BitConverter]::GetBytes([UInt32]$tableRva)
            [byte[]]$funclet=@(0x01,0x04,0x01,0x00,0x04,0x42,0x01)
            if(-not (Test-SingleFinallyEhTable $table $type) -or -not (Test-SingleFinallyUnwindBytes $rootData $true $tableRva) -or
               -not (Test-SingleFinallyUnwindBytes $funclet $false $tableRva)){throw 'exact measured positives'}
            for($i=0;$i -lt $table.Length;$i++){
                [byte[]]$bad=$table.Clone();$bad[$i]=$bad[$i] -bxor 1
                if(Test-SingleFinallyEhTable $bad $type){throw ('table mutation '+$i)}
            }
            foreach($isRoot in @($true,$false)){
                [byte[]]$original=if($isRoot){$rootData}else{$funclet}
                for($i=0;$i -lt $original.Length;$i++){
                    [byte[]]$bad=$original.Clone();$bad[$i]=$bad[$i] -bxor 1
                    if(Test-SingleFinallyUnwindBytes $bad $isRoot $tableRva){throw ('unwind mutation '+$isRoot+' '+$i)}
                }
                for($length=0;$length -lt $original.Length;$length++){
                    [byte[]]$bad=New-Object byte[] $length;[Array]::Copy($original,$bad,$length)
                    if(Test-SingleFinallyUnwindBytes $bad $isRoot $tableRva){throw 'truncated unwind'}
                }
                if(Test-SingleFinallyUnwindBytes ([byte[]]($original+0)) $isRoot $tableRva){throw 'extended unwind'}
            }
            for($length=0;$length -lt $table.Length;$length++){
                [byte[]]$bad=New-Object byte[] $length;[Array]::Copy($table,$bad,$length)
                if(Test-SingleFinallyEhTable $bad $type){throw 'truncated table'}
            }
            foreach($badType in @([UInt64]0,($type+1),([UInt64][UInt32]::MaxValue+1))){
                if(Test-SingleFinallyEhTable $table $badType){throw 'wrong type RVA'}
            }
            if((Test-SingleFinallyEhTable ([byte[]]($table+0)) $type) -or (Test-SingleFinallyEhTable $null $type) -or
                (Test-SingleFinallyUnwindBytes $rootData $true ($tableRva+1)) -or (Test-SingleFinallyUnwindBytes $rootData $true 0) -or
                (Test-SingleFinallyUnwindBytes $null $false $tableRva)){throw 'length null or table association'}
            Write-Output 'TEST_PASS single.finally.native.metadata'
            """, new System.Text.UTF8Encoding(false));
        RunCommand("native-metadata", "powershell.exe", ["-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", script,
            "-Source", Path.Combine(Root, "scripts/test-efi-artifact.ps1")], 0, "TEST_PASS single.finally.native.metadata");
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
