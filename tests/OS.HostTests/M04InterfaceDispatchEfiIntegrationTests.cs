using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Xml.Linq;
using OS.Tools.QemuRunner;

internal static class M04InterfaceDispatchEfiIntegrationTests
{
    private const string Flag = "EnableM04InterfaceDispatchProbe";
    private static string Root => HostToolProcess.FindRepositoryRoot();

    [ModuleInitializer]
    internal static void RunAll()
    {
        Run("compiler callsites and explicit child profile", Source);
        Run("complete raw dispatch oracle rejects malformed evidence", Transcript);
        Run("bounded envelope markers retain final boot EOF", Profile);
    }

    private static void Run(string name, Action action)
    {
        Console.WriteLine("TEST_START m04.016.efi " + name);
        try { action(); Console.WriteLine("TEST_PASS m04.016.efi " + name); }
        catch (Exception error) { Console.WriteLine("TEST_FAIL m04.016.efi " + name + " " + error); throw; }
    }

    private static void Need(bool value, string reason)
    { if (!value) throw new InvalidOperationException(reason); }

    private static void Source()
    {
        var project = XDocument.Load(Path.Combine(Root, "src/OS/OS.csproj"));
        Need(project.Descendants(Flag).Single().Value == "false", "disabled default");
        Need(OS.Kernel.Runtime.TypeManagerModuleMetadataLiveNativeAOTImage.StateBytes == 272, "raw original-owner handle offset matches current POD");
        const string edge = "'$(EnableM04InterfaceDispatchProbe)' == 'true' and '$(EnableM04ClassConstructorExecutionProbe)' != 'true'";
        Need(project.Descendants("Error").Count(x => (string?)x.Attribute("Condition") == edge) == 1, "exact constructor prerequisite");
        string source = File.ReadAllText(Path.Combine(Root, "src/OS/Runtime/InterfaceDispatchProbe.cs"));
        foreach (string consumer in new[] { "DispatchPlain", "DispatchVirtual", "DispatchProducer", "DispatchConsumer", "DispatchNoise" })
            Need(source.Contains("private static ulong " + consumer + "(", StringComparison.Ordinal), "actual opaque interface consumer " + consumer);
        Need(source.Contains("BootSequence.RunInterfaceDispatch(", StringComparison.Ordinal) &&
            source.Contains("CLROS_InterfaceDispatch_Resolve", StringComparison.Ordinal) &&
            source.Contains("resolved.CacheHit != (index % 3 == 2)", StringComparison.Ordinal), "real callback and original cache receipts");
        string gc = File.ReadAllText(Path.Combine(Root, "src/OS/Runtime/GCStaticRootMaterializationProbe.cs"));
        Need(gc.IndexOf("RunCompilerInterfaceDispatch();", StringComparison.Ordinal) > gc.IndexOf("RunCollectorGraph();", StringComparison.Ordinal), "dispatch follows retained original collector");
    }

    private static string PowerShell(string file, params string[] arguments)
    {
        var start = new ProcessStartInfo("powershell.exe") { WorkingDirectory = Root, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string value in new[] { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", file }.Concat(arguments)) start.ArgumentList.Add(value);
        start.Environment["PSModulePath"] = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32/WindowsPowerShell/v1.0/Modules");
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
        bool ended = process.WaitForExit(120_000);
        if (!ended) { process.Kill(true); process.WaitForExit(); }
        string stdout = output.GetAwaiter().GetResult(), stderr = error.GetAwaiter().GetResult();
        Need(ended && process.ExitCode == 0 && stderr.Length == 0, stdout + stderr);
        return stdout;
    }

    private static void Transcript()
    {
        string directory = Path.Combine(Root, "artifacts/issue572-dispatch-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string script = Path.Combine(directory, "controls.ps1"), baseline = Path.Combine(directory, "gc-parent.log");
        // Reuse the existing complete synthetic GC/collector protocol, without
        // invoking that fixture or duplicating its prerequisite test cases.
        string protocol = (string)typeof(M04GCStaticRootMaterializationEfiIntegrationTests)
            .GetField("Protocol", BindingFlags.NonPublic | BindingFlags.Static)!.GetRawConstantValue()!;
        File.WriteAllText(baseline, protocol.Replace("\r\n", "\n").TrimEnd('\n') + "\n", new UTF8Encoding(false));
        File.WriteAllText(script, Controls, new UTF8Encoding(false));
        Need(PowerShell(script, "-Source", Path.Combine(Root, "scripts/run-qemu-smoke.ps1"), "-Baseline", baseline)
            .Contains("TEST_PASS dispatch.transcript", StringComparison.Ordinal), "controlled raw transcript checks");
    }

    private static void Profile()
    {
        string[] chain = ["EnableM01LoadedImageStackProbe", "EnableM01LiveAllocatorProbe", "EnableM04IndependentEndianConformanceProbe",
            "EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe", "EnableM04ArithmeticConformanceProbe", "EnableM04LayoutContractProbe",
            "EnableM04ManagedHeapPageRangeProbe", "EnableM04RhpNewFastProbe", "EnableM04GcStaticRootMaterializationProbe",
            "EnableM04ClassConstructorExecutionProbe", Flag];
        string output = PowerShell(Path.Combine(Root, "scripts/run-qemu-smoke.ps1"), ["-DryRun", "-BuildEfi", ..chain.Select(x => "-" + x)]);
        string[] Read(string key) => output.Split('\n').Single(x => x.StartsWith(key, StringComparison.Ordinal))[key.Length..].TrimEnd('\r').Split(';', StringSplitOptions.RemoveEmptyEntries);
        string[] required = Read("REQUIRED_MARKERS "), ordered = Read("ORDERED_MARKERS "), unique = Read("UNIQUE_MARKERS "), forbidden = Read("FORBIDDEN_MARKERS ");
        Need(ordered.Length == 74 && ordered[^1] == "OS_SMOKE_PASS" && unique.SequenceEqual(ordered, StringComparer.Ordinal), "71 parent plus 3 envelope markers; raw parser owns 73 numeric stages");
        int index = Array.IndexOf(ordered, "TEST_START m04.016");
        Need(index == 63 && ordered[index - 1] == "CLROS_M04_013_READY" && ordered[index + 3] == "TEST_START boot.sequence", "dispatch precedes final eight boot records");
        bool Accept(string[] records)
        {
            var state = new SerialMarkerStateMachine(required, forbidden, orderedMarkers: ordered, uniqueMarkers: unique);
            return state.Feed(Encoding.ASCII.GetBytes(string.Join("\r\n", records) + "\r\n")).Succeeded && state.Complete().Passed;
        }
        Need(Accept(ordered), "controlled envelope only; raw proof tested separately");
        for (int i = index; i < index + 3; i++) Need(!Accept(ordered.Where((_, n) => n != i).ToArray()), "missing dispatch envelope " + i);
        Need(!Accept(ordered.Append("M04_DISPATCH 255 0 0 0 0").ToArray()), "failure after readiness");
    }

    private const string Controls = """
        param([string]$Source,[string]$Baseline)
        $ErrorActionPreference='Stop'
        $tokens=$null;$errors=$null
        $ast=[Management.Automation.Language.Parser]::ParseFile($Source,[ref]$tokens,[ref]$errors)
        if($errors.Count){throw 'parser syntax'}
        foreach($statement in $ast.EndBlock.Statements) {
            if($statement.Extent.Text -cmatch '^\$script:M04(?:IndependentEndianConformance|SpanMemoryConformance|ArithmeticConformance|LayoutContract)\w+ =') {
                . ([scriptblock]::Create($statement.Extent.Text))
            }
        }
        foreach($name in @('Assert-M04ManagedHeapPageRangeTranscript','Assert-M04GcStaticRootRecords','Assert-M04ClassConstructorRecords','Assert-M04InterfaceDispatchRecords','Assert-M04InterfaceDispatchSelection')) {
            $f=@($ast.FindAll({param($n) $n -is [Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -ceq $name},$true))
            if($f.Count -ne 1){throw ('one production validator '+$name)}
            . ([scriptblock]::Create($f[0].Extent.Text))
        }
        function Row($prefix,$values){$prefix+' '+(($values | ForEach-Object {'0x'+([UInt64]$_).ToString('X16')}) -join ' ')}
        function Numbers($line){ ,@(($line.Split(' ')[2..5] | ForEach-Object {[Convert]::ToUInt64($_.Substring(2),16)})) }
        $base=@([IO.File]::ReadAllLines($Baseline));if($base.Count -ne 70){throw 'original GC parent count'}
        $gcImage=Numbers $base[44];$module=Numbers $base[45];$stack=Numbers $base[46];$gcRoot=Numbers $base[47]
        $image=$gcImage[0];$heap=$gcRoot[1]-8;$handle=$module[3];$seed=($handle -bxor $heap -bxor $stack[2]) -band [UInt64]65535;$seed++
        $cctorSeed=$handle -bxor $heap
        $cv=@(@($handle,($image+0x90000),8192,$cctorSeed),@(1,1,($cctorSeed+11),($cctorSeed+11)),@(($cctorSeed+21),($cctorSeed+21),1,1),@(($cctorSeed+31),($cctorSeed+32),($cctorSeed+31),1),@(($cctorSeed+41),($cctorSeed+42),($cctorSeed+43),2),@(($cctorSeed+51),($cctorSeed+52),($cctorSeed+51),2),@(9,1,($image+0x4000),($image+0x4008)),@(1,7,0,8),@(5,3,0,$gcRoot[1]))
        $cctor=@('TEST_START m04.024';for($i=0;$i -lt 9;$i++){Row ('M04_CCTOR '+$i) $cv[$i]};'CLROS_M04_024_READY';'TEST_PASS m04.024')
        $parent=@($base[0..46];$cctor;$base[47..69])
        $rows=New-Object 'object[]' 73
        $rows[0]=@($handle,($image+0x70000),32768,$seed)
        $rows[1]=@(($image+0x10000),112,($image+0x18000),5)
        $rows[2]=@(($heap+64),($heap+88),($image+0x5100),($image+0x5200))
        $rows[3]=@(9,10,24,24)
        $tag=[Convert]::ToUInt64('C011EC7000000006',16)
        $rows[4]=@($gcRoot[1],($heap+32),$tag,($handle-272))
        $rows[5]=@($stack[0],$stack[1],$stack[2],($stack[3]-512))
        $rows[6]=@(($image+0x5000),($image+0x6000),4,1)
        $rows[7]=@($rows[2][2],0,0,4)
        $rows[8]=@($rows[2][3],0,0,4)
        $rows[9]=@(7,2,10,176)
        for($i=0;$i -lt 15;$i++) {
            $op=[int][Math]::Floor($i/3);$second=[int]($i%3 -eq 1);$r=10+4*$i
            $requested=$image+0xA000+$op*128;$implemented=if(($op -eq 2 -or $op -eq 3) -and $second -eq 0){$image+0xB000+$op*128}else{$requested}
            $owner=if($op -le 1){$rows[6][0]}else{$rows[2][2+$second]}
            $target=$image+0x20000+$op*256+$(if($op -eq 0){0}else{$second*16})
            $rows[$r]=@($rows[2][$second],($rows[1][0]+@(0,16,64,80,32)[$op]),$target,(9+$second))
            $rows[$r+1]=@($rows[2][2+$second],$requested,$implemented,$owner)
            $slot=@(1,0,1,2,3)[$op];$interfaceSlot=if($op -eq 2 -or $op -eq 3){1}else{0}
            $rows[$r+2]=@($interfaceSlot,$slot,[int]($i%3 -eq 2),($i+1))
            $a=$seed+$i+1;$d=$a+3;$offset=if($op -eq 0){100}else{$op*200+100*$second}
            $expected=$seed+11+11*$second+$offset+$a+3*($a+1)+5*($a+2)+7*$d
            $rows[$r+3]=@($expected,$expected,$op,$d)
        }
        $rows[70]=@(15,10,5,128);$canary=Numbers $base[60]
        $rows[71]=@(7,7,$gcRoot[1],$tag);$rows[72]=@(17,$canary[1],$canary[2],($stack[3]-768))
        $block=@('TEST_START m04.016';for($i=0;$i -lt 73;$i++){Row ('M04_DISPATCH '+$i) $rows[$i]};'CLROS_M04_016_READY';'TEST_PASS m04.016')
        $good=@($parent[0..73];$block;$parent[74..81])
        $serial=Join-Path (Split-Path -Parent $Baseline) 'controlled.log';$script:checks=0
        function Check($lines,$accept,$suffix="`r`n") {
            [IO.File]::WriteAllText($serial,($lines -join "`r`n")+$suffix,(New-Object Text.UTF8Encoding($false)))
            $passed=$true
            try {Assert-M04ManagedHeapPageRangeTranscript -SerialLogPath $serial -IncludeGcStatics -IncludeClassConstructors -IncludeInterfaceDispatch} catch {if($accept){throw};$passed=$false}
            if($passed -ne $accept){throw ('unexpected admission case '+$script:checks)}
            $script:checks++
        }
        Check $good $true
        # Independently valid phase minima need not have the same relative depth
        # after compiler frame changes. Original stack bounds/canaries still apply.
        $shallower=$canary[3]+8
        if($shallower -gt $stack[3]){throw 'control requires distinct phase minima'}
        $differentFrames=$good.Clone();$start=$rows[5].Clone();$final=$rows[72].Clone()
        $start[3]=$shallower;$final[3]=$shallower
        $differentFrames[80]=Row 'M04_DISPATCH 5' $start
        $differentFrames[147]=Row 'M04_DISPATCH 72' $final
        Check $differentFrames $true
        # Entire dispatch block: omitted, duplicated, reordered and truncated.
        foreach($i in 74..149) {
            Check @($good | Select-Object -Index @(0..157 | Where-Object {$_ -ne $i})) $false
            Check @($good[0..$i];$good[$i];$good[($i+1)..157]) $false
            $bad=$good.Clone();$bad[$i]=$good[$i+1];$bad[$i+1]=$good[$i];Check $bad $false
            Check @($good[0..$i]) $false
        }
        # Shape and actual identity/oracle controls, not merely marker presence.
        foreach($i in 75..147){$bad=$good.Clone();$bad[$i]+=' ';Check $bad $false}
        foreach($case in @(@(0,0,($handle+8)),@(0,3,($seed+1)),@(1,0,$heap),@(2,0,$gcRoot[1]),@(3,0,8),@(4,1,($heap+56)),@(4,3,$image),@(5,0,($stack[0]+8)),@(6,2,0),@(7,0,$rows[6][0]),@(9,0,8),@(10,0,$rows[2][1]),@(10,1,($rows[1][0]+1)),@(10,2,0),@(10,3,10),@(11,2,($image+0xB000)),@(11,3,$rows[2][2]),@(12,0,1),@(12,1,0),@(12,2,1),@(12,3,2),@(13,3,0),@(18,1,($rows[1][0]+16)),@(70,0,14),@(70,1,9),@(70,2,4),@(70,3,1025),@(71,1,6),@(72,0,16),@(72,1,0),@(72,3,$stack[2]))) {
            $bad=$good.Clone();$v=$rows[$case[0]].Clone();$v[$case[1]]=$case[2]
            $bad[75+$case[0]]=Row ('M04_DISPATCH '+$case[0]) $v;Check $bad $false
        }
        # Coherently wrong actual+expected results cannot certify each other.
        foreach($case in @(@(1,1,96),@(1,3,6),@(7,1,($image+0x6100)),@(8,2,4),@(34,1,($rows[1][0]+48)),@(36,0,0),@(48,0,0),@(60,1,4),@(14,2,($rows[10][2]+16)))) {
            $bad=$good.Clone();$v=$rows[$case[0]].Clone();$v[$case[1]]=$case[2]
            $bad[75+$case[0]]=Row ('M04_DISPATCH '+$case[0]) $v;Check $bad $false
        }
        $bad=$good.Clone();$v=$rows[4].Clone();$v[3]+=8;$bad[79]=Row 'M04_DISPATCH 4' $v;Check $bad $false
        $bad=$good.Clone();$v=$rows[13].Clone();$v[0]++;$v[1]++;$bad[88]=Row 'M04_DISPATCH 13' $v;Check $bad $false
        Check $good $false ''
        Check $good $false "`r`n`r`n"
        Check @($good;'M04_DISPATCH 255 0 0 0 0') $false
        Check @($good;'noise') $false
        # The final eight parent records remain required after dispatch readiness.
        foreach($i in 150..157){Check @($good[0..($i-1)]) $false}
        foreach($claim in @('M04_DISPATCH 0','CLROS_M04_016_READY','TEST_START m04.016','TEST_PASS m04.016',"M04_DIS`0PATCH 0",'test_pass M04.016')) {
            $rejected=$false;try {Assert-M04InterfaceDispatchSelection -Text $claim -Enabled $false} catch {$rejected=$true}
            if(-not $rejected){throw 'unselected dispatch claim accepted'}
            Assert-M04InterfaceDispatchSelection -Text $claim -Enabled $true
            $script:checks++
        }
        Assert-M04InterfaceDispatchSelection -Text 'POST_FIRMWARE_DIAGNOSTICS_READY' -Enabled $false
        Write-Output ('TEST_PASS dispatch.transcript controls='+$script:checks+' raw=158 ordered=74 dispatchStages=73')
        """;
}
