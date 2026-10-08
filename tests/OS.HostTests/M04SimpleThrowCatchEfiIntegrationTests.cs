using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml.Linq;
using OS.Kernel.Diagnostics;
using OS.Kernel.Runtime;
using OS.Tools.QemuRunner;

internal static class M04SimpleThrowCatchEfiIntegrationTests
{
    private const string Flag="EnableM04SimpleThrowCatchProbe";
    private static string Root=>HostToolProcess.FindRepositoryRoot();
    private static void Need(bool ok,string why){if(!ok)throw new InvalidOperationException(why);}
    [ModuleInitializer]
    internal static void RunAll()
    {
        Run("source",Source);Run("transcript",Transcript);Run("profile",Profile);
    }
    private static void Run(string name,Action action)
    {
        Console.WriteLine("TEST_START m04.021.efi "+name);
        try{action();Console.WriteLine("TEST_PASS m04.021.efi "+name);}
        catch(Exception ex){Console.WriteLine("TEST_FAIL m04.021.efi "+name+" "+ex);throw;}
    }
    private static void Source()
    {
        var project=XDocument.Load(Path.Combine(Root,"src/OS/OS.csproj"));
        var defaults=project.Descendants(Flag).ToArray();
        Need(defaults.Length==1&&defaults[0].Value=="false"&&!Probes.EnableM04SimpleThrowCatchProbe&&!OS.Boot.BootSequence.IsSimpleThrowCatchEnabled(),"default-off child boundary");
        var definition=Probes.GetM04SimpleThrowCatchProbe();Need(!definition.EnabledByDefault&&definition.Risk==ProbeRisk.Destructive,"explicit dangerous probe");
        string[] errors=project.Descendants("Error").Select(x=>(string)x.Attribute("Condition")!).ToArray();
        foreach(string condition in new[]{
            "'$(EnableM04SimpleThrowCatchProbe)' != '' and '$(EnableM04SimpleThrowCatchProbe)' != 'true' and '$(EnableM04SimpleThrowCatchProbe)' != 'false'",
            "'$(EnableM04SimpleThrowCatchProbe)' == 'true' and '$(EnableM04GcStaticRootMaterializationProbe)' != 'true'",
            "'$(EnableM04SimpleThrowCatchProbe)' == 'true' and ('$(EnableM04SeededCollectorChurnProbe)' == 'true' or '$(EnableM04ClassConstructorExecutionProbe)' == 'true' or '$(EnableM04InterfaceDispatchProbe)' == 'true' or '$(EnableM04X64UnwindMetadataProbe)' == 'true')"})
            Need(errors.Count(x=>x==condition)==1,"exact child relationship");
        string[] compile=project.Descendants("Compile").Select(x=>(string)x.Attribute("Include")!).ToArray();
        foreach(string file in new[]{"..\\OS.Kernel\\Runtime\\ExceptionTypeMatch.cs","..\\OS.Kernel\\Runtime\\X64FrameUnwind.cs","..\\OS.Kernel\\Runtime\\NativeAotEhClauseMetadata.cs","Runtime\\ExceptionCatchProbe.cs"})Need(compile.Count(x=>x==file)==1,"one explicit admitted source "+file);
        Need(Marshal.SizeOf<X64UnwindContext>()==240,"actual context size");
        foreach(var offset in new[]{("Rip",0),("Rsp",8),("Rbx",16),("Rbp",24),("Rsi",32),("Rdi",40),("R12",48),("R13",56),("R14",64),("R15",72),("Xmm6",80),("Xmm15",224)})
            Need(Marshal.OffsetOf<X64UnwindContext>(offset.Item1).ToInt32()==offset.Item2,"actual capture field "+offset.Item1);
        Need(Marshal.SizeOf<X64UnwindVector>()==16&&Marshal.OffsetOf<X64UnwindVector>("High").ToInt32()==8,"whole 128-bit nonvolatile vector");
        string probe=File.ReadAllText(Path.Combine(Root,"src/OS/Runtime/ExceptionCatchProbe.cs"));
        Need(probe.StartsWith("#if NOSTDLIB_EFI && (M04_SIMPLE_THROW_CATCH_PROBE || M04_SINGLE_FINALLY_CATCH_PROBE)",StringComparison.Ordinal)&&
            probe.Contains("throw value;",StringComparison.Ordinal)&&probe.Contains("catch (SimpleCatchBase caught)",StringComparison.Ordinal)&&
            probe.Contains("CollectGcGraph(&root, 1)",StringComparison.Ordinal)&&probe.Contains("post->Rbp != s_ehCaller.Rbp",StringComparison.Ordinal)&&
            probe.Contains("unwound.Caller.Rip - 1 - catcher",StringComparison.Ordinal),"actual throw/catch/root/body contract");
        Need(probe.Contains("[MethodImpl(MethodImplOptions.NoInlining)]\n    private static bool CaptureAdmitted(",StringComparison.Ordinal)&&
            probe.Contains("rip == thrower + EhThrowReturnOffset && rsp == capturedAddress + 816 &&",StringComparison.Ordinal)&&
            probe.Contains("rip != 0 && rsp >= s_gcStack.Start + 16400 && rsp <= s_gcStack.InitialRsp;",StringComparison.Ordinal),"same bounded non-inlined capture predicate");
        int original=probe.IndexOf("if (!CaptureAdmitted((ulong)captured, originalRip, originalRsp, thrower)) return RejectEh(14);",StringComparison.Ordinal);
        int invalid=probe.IndexOf("bool invalidAdmitted = CaptureAdmitted((ulong)captured, invalidRip, originalRsp, thrower);",StringComparison.Ordinal);
        int rejection=probe.IndexOf("EhLine(2, originalRip, invalidRip, originalRsp, invalidAdmitted ? 1UL : 0UL);",StringComparison.Ordinal);
        Need(original>=0&&invalid>original&&rejection>invalid&&probe.IndexOf("BootSequence.RunSimpleThrowCatchUnwind(",StringComparison.Ordinal)>rejection&&
            probe.Contains("ulong invalidRip = originalRip ^ 1UL;",StringComparison.Ordinal)&&
            probe.Contains("invalidAdmitted || captured->Rip != originalRip || captured->Rsp != originalRsp",StringComparison.Ordinal)&&
            probe.Contains("s_ehPhase != 3 || s_ehSelected != 0 || s_ehTransferAddress != 0",StringComparison.Ordinal)&&
            probe.Contains("s_ehCaller.Rip != 0 || s_ehCaller.Rsp != 0 || s_ehCatchEntries != 0 || s_ehContinuations != 0",StringComparison.Ordinal)&&
            probe.Contains("s_ehReceipt.ObjectReference != exception || s_ehReceipt.MethodTableToken != originalType",StringComparison.Ordinal)&&
            probe.Contains("s_ehReceipt.AllocationCount != 9 || !EhObjectIntact()) return RejectEh(21);",StringComparison.Ordinal),"local rejection precedes selection and preserves original capture/root/phase");
    }
    private static string PowerShell(string file,params string[] args)
    {
        var start=new ProcessStartInfo("powershell.exe"){WorkingDirectory=Root,UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
        foreach(string arg in new[]{"-NoProfile","-NonInteractive","-ExecutionPolicy","Bypass","-File",file}.Concat(args))start.ArgumentList.Add(arg);
        start.Environment["PSModulePath"]=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"System32/WindowsPowerShell/v1.0/Modules");
        using var process=Process.Start(start)!;var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();
        bool ended=process.WaitForExit(120000);if(!ended){process.Kill(true);process.WaitForExit();}
        string output=stdout.GetAwaiter().GetResult(),error=stderr.GetAwaiter().GetResult();
        string evidence=Path.Combine(Root,"artifacts/simple-catch-process",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(evidence);
        File.WriteAllText(Path.Combine(evidence,"stdout.log"),output);File.WriteAllText(Path.Combine(evidence,"stderr.log"),error);
        File.WriteAllLines(Path.Combine(evidence,"arguments.txt"),start.ArgumentList);
        Console.WriteLine("SIMPLE_CATCH_PROCESS_EVIDENCE "+evidence);
        Need(ended&&process.ExitCode==0&&error.Length==0,output+error);return output;
    }
    private static void Transcript()
    {
        string folder=Path.Combine(Root,"artifacts/simple-catch-transcript",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
        // Reuse the existing fixed ancestor data without running that fixture's
        // initializer or duplicating its seventy records and assertions.
        string source=File.ReadAllText(Path.Combine(Root,"tests/OS.HostTests/M04GCStaticRootMaterializationEfiIntegrationTests.cs")).Replace("\r\n","\n",StringComparison.Ordinal);
        string start="private const string Protocol = \"\"\"\n";int at=source.IndexOf(start,StringComparison.Ordinal);
        Need(at>=0&&source.IndexOf(start,at+1,StringComparison.Ordinal)<0,"unique ancestor transcript literal");
        at+=start.Length;int end=source.IndexOf("        \"\"\";",at,StringComparison.Ordinal);Need(end>at,"complete ancestor transcript literal");
        string[] lines=source[at..end].TrimEnd('\n').Split('\n').Select(x=>x[8..]).ToArray();Need(lines.Length==70,"unchanged complete ancestor count");
        string ancestor=Path.Combine(folder,"ancestor.serial");File.WriteAllText(ancestor,string.Join("\r\n",lines)+"\r\n",new UTF8Encoding(false));
        string script=Path.Combine(folder,"controls.ps1");File.WriteAllText(script,Controls,new UTF8Encoding(false));
        Need(PowerShell(script,"-Source",Path.Combine(Root,"scripts/run-qemu-smoke.ps1"),"-Ancestor",ancestor)
            .Contains("TEST_PASS simple.catch.transcript",StringComparison.Ordinal),"complete raw positive/negative controls");
    }
    private static void Profile()
    {
        string[] chain=["EnableM01LoadedImageStackProbe","EnableM01LiveAllocatorProbe","EnableM04IndependentEndianConformanceProbe","EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe","EnableM04ArithmeticConformanceProbe","EnableM04LayoutContractProbe","EnableM04ManagedHeapPageRangeProbe","EnableM04RhpNewFastProbe","EnableM04GcStaticRootMaterializationProbe",Flag];
        string output=PowerShell(Path.Combine(Root,"scripts/run-qemu-smoke.ps1"),["-DryRun","-BuildEfi",..chain.Select(x=>"-"+x)]);
        string[] Read(string key)=>output.Split('\n').Single(x=>x.StartsWith(key,StringComparison.Ordinal))[key.Length..].TrimEnd('\r').Split(';',StringSplitOptions.RemoveEmptyEntries);
        string[] required=Read("REQUIRED_MARKERS "),ordered=Read("ORDERED_MARKERS "),unique=Read("UNIQUE_MARKERS "),forbidden=Read("FORBIDDEN_MARKERS ");
        string[] expectedRequired=new[]{"OS_BOOT_OK","TEST_PASS boot.sequence","OS_SMOKE_PASS"}.Concat(ordered).Distinct(StringComparer.Ordinal).ToArray();
        Need(ordered.Length==71&&required.Length==71&&required.SequenceEqual(expectedRequired,StringComparer.Ordinal)&&unique.SequenceEqual(ordered,StringComparer.Ordinal),"complete ancestor plus twelve child markers and original default-required order");
        int first=Array.IndexOf(ordered,"TEST_START m04.021.simple-catch");
        Need(first==51&&ordered[first-1]=="CLROS_M04_013_READY"&&ordered[first+12]=="TEST_START boot.sequence","after actual collector, before ordinary boot");
        bool Accept(string[] records){var state=new SerialMarkerStateMachine(required,forbidden,orderedMarkers:ordered,uniqueMarkers:unique);return state.Feed(Encoding.ASCII.GetBytes(string.Join("\r\n",records)+"\r\n")).Succeeded&&state.Complete().Passed;}
        Need(Accept(ordered),"complete marker stream");
        for(int i=first;i<first+12;i++)Need(!Accept(ordered.Where((_,index)=>index!=i).ToArray()),"missing child marker");
        Need(!Accept(ordered.Append("TEST_FAIL m04.021.simple-catch 1").ToArray()),"late failure");
    }
    private const string Controls="""
        param([string]$Source,[string]$Ancestor)
        $ErrorActionPreference='Stop'
        $tokens=$null;$errors=$null
        $ast=[Management.Automation.Language.Parser]::ParseFile($Source,[ref]$tokens,[ref]$errors)
        if($errors.Count){throw 'parser syntax'}
        foreach($statement in $ast.EndBlock.Statements){
            if($statement.Extent.Text -cmatch '^\$script:M04(?:IndependentEndianConformance|SpanMemoryConformance|ArithmeticConformance|LayoutContract)\w+ ='){. ([scriptblock]::Create($statement.Extent.Text))}
        }
        foreach($name in @('Assert-M04ManagedHeapPageRangeTranscript','Assert-M04GcStaticRootRecords','Assert-M04SimpleCatchRecords','Assert-M04SimpleCatchSelection')){
            $f=@($ast.FindAll({param($n)$n -is [Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -ceq $name},$true))
            if($f.Count -ne 1){throw 'one original function'};. ([scriptblock]::Create($f[0].Extent.Text))
        }
        $base=[IO.File]::ReadAllText($Ancestor).Split(@("`r`n"),[StringSplitOptions]::RemoveEmptyEntries)
        function Fields($line){$p=$line.Split(' ');,@((2..5|ForEach-Object{[Convert]::ToUInt64($p[$_].Substring(2),16)}))}
        function Row($stage,$v){'M04_CATCH '+$stage+' '+(($v|ForEach-Object{'0x'+([UInt64]$_).ToString('X16')})-join ' ')}
        $image=Fields $base[44];$module=Fields $base[45];$stack=Fields $base[46];$old=Fields $base[48];$checked=Fields $base[51]
        $heap=[UInt64]$old[0]-32;$seed=([UInt64]$module[3] -bxor $heap)-band 0xFFFF;$sp=[UInt64]$stack[1]-1024
        $rip=[UInt64]$image[0]+4105
        $rows=@(@($module[3],($image[0]+65536),512,3),@($old[0],($old[1]+64),9,3),@($rip,($rip -bxor [UInt64]1),$sp,0),@($rip,$sp,($image[0]+8218),($sp+48)),@(($old[1]+32),($image[0]+8243),($sp+96),($sp+40)),@($old[0],$seed,($image[0]+8243),1),@(($image[0]+8219),($sp+48),($sp+96),1),@($old[0],$seed,($seed+1),1),@(6,($sp-4096),$checked[1],$checked[2]))
        $child=@('TEST_START m04.021.simple-catch';for($i=0;$i -lt 9;$i++){Row $i $rows[$i]};'TEST_PASS m04.021.simple-catch';'CLROS_M04_SIMPLE_CATCH_READY')
        $lines=@($base[0..61])+$child+@($base[62..69]);$good=($lines -join "`r`n")+"`r`n"
        $serial=Join-Path (Split-Path -Parent $Ancestor) 'case.serial'
        function Check([string]$text,[bool]$accept){
            [IO.File]::WriteAllText($serial,$text,[Text.UTF8Encoding]::new($false));$ok=$true
            try{Assert-M04ManagedHeapPageRangeTranscript -SerialLogPath $serial -IncludeGcStatics -IncludeSimpleCatch}catch{if($accept){throw};$ok=$false}
            if($ok -ne $accept){throw ('unexpected admission '+$accept)}
        }
        Check $good $true
        foreach($i in 62..73){
            Check ((@($lines[0..($i-1)])+@($lines[($i+1)..81])-join "`r`n")+"`r`n") $false
            Check ((@($lines[0..$i])+@($lines[$i..81])-join "`r`n")+"`r`n") $false
            $bad=$lines.Clone();$bad[$i]+=' ';Check (($bad-join "`r`n")+"`r`n") $false
        }
        foreach($pair in @(@(0,0),@(0,1),@(0,2),@(0,3),@(1,0),@(1,1),@(1,2),@(1,3),@(2,0),@(2,1),@(2,2),@(2,3),@(3,1),@(3,3),@(4,1),@(4,2),@(4,3),@(5,0),@(5,1),@(5,2),@(5,3),@(6,0),@(6,1),@(6,2),@(6,3),@(7,0),@(7,1),@(7,2),@(7,3),@(8,0),@(8,1),@(8,2),@(8,3))){
            $bad=$lines.Clone();$values=$rows[$pair[0]].Clone();$values[$pair[1]]=[UInt64]::MaxValue;$bad[63+$pair[0]]=Row $pair[0] $values;Check (($bad-join "`r`n")+"`r`n") $false
        }
        foreach($wrongRip in @($rip,($rip -bxor [UInt64]2))){
            $bad=$lines.Clone();$values=$rows[2].Clone();$values[1]=$wrongRip;$bad[65]=Row 2 $values;Check (($bad-join "`r`n")+"`r`n") $false
        }
        $bad=$lines.Clone();$values=$rows[2].Clone();$values[3]=1;$bad[65]=Row 2 $values;Check (($bad-join "`r`n")+"`r`n") $false
        Check ($good+"`r`n") $false;Check ($good+"TEST_FAIL m04.021.simple-catch 1`r`n") $false
        Check ($good.Replace("`r`n","`n")) $false;Check ($good.TrimEnd([char]10)) $false
        Assert-M04SimpleCatchSelection -Text $good -Enabled $true
        foreach($text in @('M04_CATCH','CLROS_M04_SIMPLE_CATCH_READY',"m04.`t021")){
            $ok=$true;try{Assert-M04SimpleCatchSelection -Text $text -Enabled $false}catch{$ok=$false};if($ok){throw 'unselected claim accepted'}
        }
        Write-Output 'TEST_PASS simple.catch.transcript'
        """;
}
