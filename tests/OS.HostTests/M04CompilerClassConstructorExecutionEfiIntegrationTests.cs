using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Xml.Linq;
using OS.Tools.QemuRunner;

internal static class M04CompilerClassConstructorExecutionEfiIntegrationTests
{
    private const string Flag = "EnableM04ClassConstructorExecutionProbe";
    private static string Root => HostToolProcess.FindRepositoryRoot();

    [ModuleInitializer]
    internal static void RunAll()
    {
        Run("compiler helper ABI and retained bootstrap order", Source);
        Run("strict constructor records reject false once and reentry proof", Transcript);
        Run("explicit constructor profile marker isolation", Profile);
    }
    private static void Run(string name, Action action)
    {
        Console.WriteLine("TEST_START m04.024.efi " + name);
        try { action(); Console.WriteLine("TEST_PASS m04.024.efi " + name); }
        catch (Exception error) { Console.WriteLine("TEST_FAIL m04.024.efi " + name + " " + error); throw; }
    }
    private static void Need(bool value, string reason)
    { if (!value) throw new InvalidOperationException(reason); }

    private static void Source()
    {
        var project = XDocument.Load(Path.Combine(Root, "src/OS/OS.csproj"));
        Need(project.Descendants(Flag).Single().Value == "false", "disabled default");
        foreach (string edge in new[] {
            "'$(EnableM04ClassConstructorExecutionProbe)' == 'true' and '$(EnableM04GcStaticRootMaterializationProbe)' != 'true'",
            "'$(EnableM04ClassConstructorExecutionProbe)' == 'true' and '$(EnableM04SeededCollectorChurnProbe)' == 'true'" })
            Need(project.Descendants("Error").Count(x => (string?)x.Attribute("Condition") == edge) == 1, "exact profile edge");
        string helper = File.ReadAllText(Path.Combine(Root, "src/OS/Runtime/ClassConstructorRunner.cs"));
        Need(helper.StartsWith("#if NOSTDLIB_EFI && M04_CLASS_CONSTRUCTOR_EXECUTION_PROBE", StringComparison.Ordinal) &&
            helper.Contains("public volatile nint cctorMethodAddress;", StringComparison.Ordinal), "profile-only exact pointer context");
        foreach (string name in new[] { "CheckStaticClassConstructionReturnNonGCStaticBase", "CheckStaticClassConstructionReturnGCStaticBase", "EnsureClassConstructorRun" })
            Need(helper.Contains(name + "(", StringComparison.Ordinal), "pinned managed helper " + name);
        Need(!helper.Contains("ThreadStatic", StringComparison.Ordinal) && !helper.Contains("RuntimeExport", StringComparison.Ordinal) &&
            !helper.Contains("UnmanagedCallersOnly", StringComparison.Ordinal), "no unsupported ABI placeholder");
        string gc = File.ReadAllText(Path.Combine(Root, "src/OS/Runtime/GCStaticRootMaterializationProbe.cs"));
        int bind = gc.IndexOf("BindAndRunCompilerClassConstructors(materialized.ObjectReference);", StringComparison.Ordinal);
        Need(bind > gc.IndexOf("s_gcHandle = registered.Handle;", StringComparison.Ordinal) &&
            bind < gc.IndexOf("GcStaticFixture.Get()", bind, StringComparison.Ordinal), "registration and materialization before first constructor-bearing accessor");
        string probe = File.ReadAllText(Path.Combine(Root, "src/OS/Runtime/CompilerClassConstructorExecutionProbe.cs"));
        Need(probe.Contains("BootSequence.RunCompilerClassConstructors(", StringComparison.Ordinal) &&
            probe.Contains("CctorDirectRead() => CctorDirectFixture.Read();", StringComparison.Ordinal) &&
            probe.Contains("s_cctorDepthOneReentries != 5 || s_cctorDepthTwoReentries != 3", StringComparison.Ordinal) &&
            probe.Contains("if (*(ulong*)context == 0) RejectCctor(20);", StringComparison.Ordinal), "actual active-context reentry and callable preservation");
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
        string directory = Path.Combine(Root, "artifacts/issue570-cctor-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string script = Path.Combine(directory, "controls.ps1");
        File.WriteAllText(script, Controls, new UTF8Encoding(false));
        Need(PowerShell(script, "-Source", Path.Combine(Root, "scripts/run-qemu-smoke.ps1")).Contains("TEST_PASS cctor.transcript", StringComparison.Ordinal), "controlled transcript checks");
    }

    private static void Profile()
    {
        string[] chain = ["EnableM01LoadedImageStackProbe", "EnableM01LiveAllocatorProbe", "EnableM04IndependentEndianConformanceProbe",
            "EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe", "EnableM04ArithmeticConformanceProbe", "EnableM04LayoutContractProbe",
            "EnableM04ManagedHeapPageRangeProbe", "EnableM04RhpNewFastProbe", "EnableM04GcStaticRootMaterializationProbe", Flag];
        string output = PowerShell(Path.Combine(Root, "scripts/run-qemu-smoke.ps1"), ["-DryRun", "-BuildEfi", ..chain.Select(x => "-" + x)]);
        string[] Read(string key) => output.Split('\n').Single(x => x.StartsWith(key, StringComparison.Ordinal))[key.Length..].TrimEnd('\r').Split(';', StringSplitOptions.RemoveEmptyEntries);
        string[] required=Read("REQUIRED_MARKERS "), ordered=Read("ORDERED_MARKERS "), unique=Read("UNIQUE_MARKERS "), forbidden=Read("FORBIDDEN_MARKERS ");
        Need(ordered.Length == 71 && unique.SequenceEqual(ordered, StringComparer.Ordinal), "complete parent plus 12 constructor markers");
        int start = Array.IndexOf(ordered, "TEST_START m04.024");
        Need(start > 0 && ordered[start-1] == "M04_GC_ROOT 7" && ordered[start+12] == "M04_GC_ROOT 3", "constructor bootstrap insertion");
        bool Accept(string[] records)
        {
            var state = new SerialMarkerStateMachine(required, forbidden, orderedMarkers: ordered, uniqueMarkers: unique);
            return state.Feed(Encoding.ASCII.GetBytes(string.Join("\r\n", records) + "\r\n")).Succeeded && state.Complete().Passed;
        }
        Need(Accept(ordered), "controlled complete profile stream");
        for (int i=start; i < start+12; i++) Need(!Accept(ordered.Where((_, index) => index != i).ToArray()), "missing constructor record " + i);
        Need(!Accept(ordered.Append("M04_CCTOR 255 0 0 0 0").ToArray()), "failure after readiness is rejected");
    }

    private const string Controls = """
        param([string]$Source)
        $ErrorActionPreference='Stop'
        $tokens=$null;$errors=$null
        $ast=[Management.Automation.Language.Parser]::ParseFile($Source,[ref]$tokens,[ref]$errors)
        if($errors.Count){throw 'parser syntax'}
        $f=@($ast.FindAll({param($n) $n -is [Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -ceq 'Assert-M04ClassConstructorRecords'},$true))
        if($f.Count -ne 1){throw 'one production validator'}
        . ([scriptblock]::Create($f[0].Extent.Text))
        function Row($prefix,$values){$prefix+' '+(($values | ForEach-Object {'0x'+([UInt64]$_).ToString('X16')}) -join ' ')}
        $gc=@((Row 'M04_GC_ROOT 0' @(4096,65536,0,0)),(Row 'M04_GC_ROOT 1' @(1,16384,4,3)), '',(Row 'M04_GC_ROOT 3' @(20480,131080,131088,24)))
        $seed=[UInt64]3 -bxor [UInt64]131072
        $values=@(@(3,32768,8192,$seed),@(1,1,($seed+11),($seed+11)),@(($seed+21),($seed+21),1,1),@(($seed+31),($seed+32),($seed+31),1),@(($seed+41),($seed+42),($seed+43),2),@(($seed+51),($seed+52),($seed+51),2),@(9,1,6144,6152),@(1,7,0,8),@(5,3,0,131080))
        $good=@('TEST_START m04.024';for($i=0;$i -lt 9;$i++){Row ('M04_CCTOR '+$i) $values[$i]};'CLROS_M04_024_READY';'TEST_PASS m04.024')
        function Check($lines,$accept){
            $passed=$true
            try {Assert-M04ClassConstructorRecords -Lines $lines -GcLines $gc -Heap @{retainedStart=131072;backing=131072}} catch {if($accept){throw};$passed=$false}
            if($passed -ne $accept){throw ('unexpected admission '+$accept)}
        }
        Check $good $true
        # Every derived constructor value remains coherent, but the seed is no
        # longer bound to the original admitted module and allocator backing.
        $bad=$good.Clone();$shifted=@()
        foreach($v in $values){$shifted+=,@($v.Clone())}
        $shifted[0][3]++
        foreach($field in @(@(1,2),@(1,3),@(2,0),@(2,1),@(3,0),@(3,1),@(3,2),@(4,0),@(4,1),@(4,2),@(5,0),@(5,1),@(5,2))){$shifted[$field[0]][$field[1]]++}
        for($i=0;$i -lt 9;$i++){$bad[$i+1]=Row ('M04_CCTOR '+$i) $shifted[$i]}
        Check $bad $false
        foreach($i in 0..11){
            Check @($good | Select-Object -Index @(0..11 | Where-Object {$_ -ne $i})) $false
            $bad=$good.Clone();$bad[$i]+=' ';Check $bad $false
        }
        foreach($case in @(@(1,0,2),@(3,0,130),@(4,0,140),@(6,3,6160),@(7,1,6),@(8,0,0),@(8,1,0),@(8,2,1),@(8,3,131088))){
            $bad=$good.Clone();$v=$values[$case[0]].Clone();$v[$case[1]]=$case[2]
            $bad[$case[0]+1]=Row ('M04_CCTOR '+$case[0]) $v;Check $bad $false
        }
        Write-Output 'TEST_PASS cctor.transcript'
        """;
}
