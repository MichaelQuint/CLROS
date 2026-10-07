using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Xml.Linq;
using OS.Kernel.Diagnostics;
using OS.Kernel.Runtime;
using OS.Tools.QemuRunner;

internal static unsafe class M04X64UnwindMetadataEfiIntegrationTests
{
    private const string Flag = "EnableM04X64UnwindMetadataProbe";

    [ModuleInitializer]
    internal static void RunAll()
    {
        Run("original module and disabled boot boundary", Source);
        Run("bounded metadata transcript and unselected rejection", Transcript);
        Run("complete original module and unwind profile", Profile);
    }

    private static void Run(string name, Action action)
    {
        Console.WriteLine("TEST_START m04.020.efi " + name);
        try { action(); Console.WriteLine("TEST_PASS m04.020.efi " + name); }
        catch (Exception error)
        { Console.WriteLine("TEST_FAIL m04.020.efi " + name + " " + error); throw; }
    }

    private static void Need(bool value, string reason)
    { if (!value) throw new InvalidOperationException(reason); }

    private static void Source()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        var project = XDocument.Load(Path.Combine(root, "src/OS/OS.csproj"));
        var defaults = project.Descendants(Flag).ToArray();
        Need(defaults.Length == 1 && defaults[0].Value == "false" &&
            (string?)defaults[0].Attribute("Condition") == "'$(" + Flag + ")' == ''", "one disabled default");
        Need(project.Descendants("PropertyGroup").Single(x =>
            (string?)x.Attribute("Condition") == "'$(" + Flag + ")' == 'true'")
            .Element("DefineConstants")!.Value == "$(DefineConstants);M04_X64_UNWIND_METADATA_PROBE", "explicit child symbol");
        string[] errors = project.Descendants("Error").Select(x => (string)x.Attribute("Condition")!).ToArray();
        foreach (string condition in new[] {
            "'$(" + Flag + ")' != '' and '$(" + Flag + ")' != 'true' and '$(" + Flag + ")' != 'false'",
            "'$(" + Flag + ")' == 'true' and '$(EnableM04TypeManagerModuleRegistrationProbe)' != 'true'",
            "'$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true' and '$(EnableM01LoadedImageStackProbe)' != 'true'" })
            Need(errors.Count(x => x == condition) == 1, "exact original-module prerequisite or value guard");
        string[] compile = project.Descendants("Compile").Select(x => (string)x.Attribute("Include")!).ToArray();
        foreach (string source in new[] {
            "..\\OS.Kernel\\Runtime\\X64PERuntimeFunctionUnwindMetadataHostileInput.cs",
            "Runtime\\X64UnwindMetadataProbe.cs" })
            Need(compile.Count(x => x == source) == 1, "one explicit metadata source " + source);

        Need(!Probes.EnableM04X64UnwindMetadataProbe &&
            OS.Boot.BootSequence.RunX64UnwindMetadata(null, 0, 0).Status == X64UnwindMetadataStatus.Uninitialized,
            "disabled host boundary does not consume supplied pointers");
        var definition = Probes.GetM04X64UnwindMetadataProbe();
        Need(!definition.EnabledByDefault && definition.Risk == ProbeRisk.Destructive, "dangerous probe remains disabled");
        Need(X64PERuntimeFunctionUnwindMetadataHostileInput.StateBytes == 288 &&
            File.ReadAllText(Path.Combine(root, "scripts/run-qemu-smoke.ps1"))
                .Contains("$storage[3] -ne 288", StringComparison.Ordinal), "actual x64 retained-state geometry");
        string probe = File.ReadAllText(Path.Combine(root, "src/OS/Runtime/X64UnwindMetadataProbe.cs"));
        Need(probe.StartsWith("#if NOSTDLIB_EFI && M04_X64_UNWIND_METADATA_PROBE", StringComparison.Ordinal) &&
            probe.Contains("[System.Runtime.RuntimeExport(\"__security_cookie\")]", StringComparison.Ordinal) &&
            probe.Split("SecurityCookieStorageAnchor(", StringSplitOptions.None).Length == 2,
            "one profile-only cookie anchor with no calls");
        string owner = File.ReadAllText(Path.Combine(root, "src/OS/Runtime/NativeAotModuleRegistrationProbe.cs"));
        int child = owner.IndexOf("if (!X64UnwindMetadataProbe.Run(capture, mapping, (byte*)buffer->Words,", StringComparison.Ordinal);
        Need(child > owner.IndexOf("Token(console, 17); console.WriteCrLf(); Token(console, 18);", StringComparison.Ordinal) &&
            owner.IndexOf("if (!Preserved((byte*)buffer->Words, o.Required))", child, StringComparison.Ordinal) > child,
            "original registration transcript and retained canaries surround the child");
    }

    private static string PowerShell(string file, params string[] arguments)
    {
        var start = new ProcessStartInfo("powershell.exe") {
            WorkingDirectory = HostToolProcess.FindRepositoryRoot(), UseShellExecute = false,
            CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string value in new[] { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", file }.Concat(arguments))
            start.ArgumentList.Add(value);
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
        string root = HostToolProcess.FindRepositoryRoot();
        string directory = Path.Combine(root, "artifacts/issue574-unwind-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string script = Path.Combine(directory, "controls.ps1");
        File.WriteAllText(script, Controls, new UTF8Encoding(false));
        Need(PowerShell(script, "-Source", Path.Combine(root, "scripts/run-qemu-smoke.ps1"))
            .Contains("TEST_PASS unwind.transcript", StringComparison.Ordinal), "controlled transcript checks");
    }

    private static void Profile()
    {
        string output = PowerShell(Path.Combine(HostToolProcess.FindRepositoryRoot(), "scripts/run-qemu-smoke.ps1"),
            "-DryRun", "-BuildEfi", "-EnableM01LoadedImageStackProbe", "-EnableM04TypeManagerModuleRegistrationProbe", "-" + Flag);
        string[] Read(string key) => output.Split('\n').Single(x => x.StartsWith(key, StringComparison.Ordinal))
            [key.Length..].TrimEnd('\r').Split(';', StringSplitOptions.RemoveEmptyEntries);
        string[] required = Read("REQUIRED_MARKERS "), ordered = Read("ORDERED_MARKERS "),
            unique = Read("UNIQUE_MARKERS "), forbidden = Read("FORBIDDEN_MARKERS ");
        Need(ordered.Length == 67 && unique.SequenceEqual(ordered, StringComparer.Ordinal) &&
            required.SequenceEqual(ordered, StringComparer.Ordinal), "complete 45 ancestor plus 22 child markers, including required order");
        int first = Array.IndexOf(ordered, "TEST_START m04.020");
        Need(first == 37 && ordered[first - 1] == "TEST_PASS m04.011" && ordered[first + 22] == "TEST_START boot.sequence",
            "child follows original registration and precedes ordinary boot");
        bool Accept(string[] records)
        {
            var state = new SerialMarkerStateMachine(required, forbidden, orderedMarkers: ordered, uniqueMarkers: unique);
            return state.Feed(Encoding.ASCII.GetBytes(string.Join("\r\n", records) + "\r\n")).Succeeded && state.Complete().Passed;
        }
        Need(Accept(ordered), "controlled complete marker stream");
        for (int i = first; i < first + 22; i++)
            Need(!Accept(ordered.Where((_, index) => index != i).ToArray()), "missing child marker " + i);
        Need(!Accept(ordered.Append("TEST_FAIL m04.020 reason=1 status=8").ToArray()), "late child failure rejected");
    }

    // Synthetic rows exercise the production validator, not PE execution or guest proof.
    private const string Controls = """
        param([string]$Source)
        $ErrorActionPreference='Stop'
        $tokens=$null;$errors=$null
        $ast=[Management.Automation.Language.Parser]::ParseFile($Source,[ref]$tokens,[ref]$errors)
        if($errors.Count){throw 'parser syntax'}
        foreach($name in @('Read-M03MemoryMapCaptureRecord','Assert-M04X64UnwindMetadataTranscript','Assert-M04X64UnwindMetadataSelection')){
            $f=@($ast.FindAll({param($n) $n -is [Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -ceq $name},$true))
            if($f.Count -ne 1){throw ('one production validator '+$name)}
            . ([scriptblock]::Create($f[0].Extent.Text))
        }
        $image=@{base=[UInt64]1048576;bytes=[UInt64]262144}
        $module=@{storage=[UInt64]1056768}
        $registration=@{handle=[UInt64]1057040;slot=[UInt64]1060864}
        $seed=$image.base -bxor $registration.handle
        $word0=[Convert]::ToUInt64('3D947E21A6C850BF',16);$word1=[Convert]::ToUInt64('C2715AB809E634DF',16)
        $rows=@(
            @($image.base,$image.bytes,$module.storage,$registration.handle),
            @(16384,24,2,328),@(1114112,65536,65504,288),
            @(1064960,2097152,2232320,2232280),@(2232000,2231904,$word0,$word1),
            @(1081344,0,32768,32832),@(20480,20480,8,2),@(0,0,32,40),@(0,0,0,0),
            @(0,$seed,($seed -bxor [UInt64]255),1081407),
            @(1081600,1,33024,33088),@(20512,20512,8,2),@(0,0,32,40),@(0,0,0,0),
            @(0,$seed,($seed -bxor [UInt64]85),1081663),
            @(15,15,1,2),@(7,2231808,$word0,$word1),@(328,328,2,4),
            @($module.storage,$registration.handle,$registration.slot,$registration.handle))
        function Row($stage,$values){'CLROS_M04_020_DATA stage='+$stage+' a='+$values[0]+' b='+$values[1]+' c='+$values[2]+' d='+$values[3]}
        $good=@('TEST_START m04.020';for($i=0;$i -lt 19;$i++){Row $i $rows[$i]};
            'CLROS_M04_020_READY methods=2 lookups=4 version=1 hostConformance=false guestIntegrated=true unwindExecution=false';'TEST_PASS m04.020')
        function Check($lines,$accept){
            $passed=$true
            try {Assert-M04X64UnwindMetadataTranscript -Lines $lines -Image $image -ModuleStorage $module -Registration $registration}
            catch {if($accept){throw};$passed=$false}
            if($passed -ne $accept){throw ('unexpected admission '+$accept)}
        }
        Check $good $true
        foreach($i in 0..21){
            Check @($good | Select-Object -Index @(0..21 | Where-Object {$_ -ne $i})) $false
            $bad=$good.Clone();$bad[$i]+=' ';Check $bad $false
        }
        Check @($good+$good[0]) $false
        $bad=$good.Clone();$bad[6]=$good[11];$bad[11]=$good[6];Check $bad $false
        # Original ownership, private capacity/alias, stack, metadata range/frame,
        # method identity, actual result, end-exclusive lookup and retained owner.
        foreach($case in @(@(0,2,1056776),@(1,2,2049),@(1,3,65505),@(2,1,65535),@(2,0,1056768),@(2,3,8),
            @(3,3,2232288),@(4,1,2097160),@(4,2,0),@(5,1,2),@(5,0,1081408),
            @(6,0,16384),@(6,2,65),@(6,3,0),@(7,0,1),@(7,1,1),@(7,2,41),
            @(8,0,4),@(8,3,1),@(9,2,0),@(9,3,1081408),@(10,1,0),
            @(15,1,1),@(15,3,1),@(16,0,6),@(17,1,255),@(18,3,1057048))){
            $bad=$good.Clone();$values=$rows[$case[0]].Clone();$values[$case[1]]=$case[2]
            $bad[$case[0]+1]=Row $case[0] $values;Check $bad $false
        }
        foreach($value in @('-1','01','18446744073709551616')){
            $bad=$good.Clone();$bad[1]=$bad[1].Replace('a=1048576','a='+$value);Check $bad $false
        }
        Assert-M04X64UnwindMetadataSelection -Text 'OS_BOOT_OK' -Enabled $false
        Assert-M04X64UnwindMetadataSelection -Text ($good -join "`r`n") -Enabled $true
        foreach($claim in @('TEST_START m04.020','CLROS_M04_020_DATA stage=0',"clros_`tM04_020_READY",("TEST_PASS m04.`0"+'020'))){
            $rejected=$false
            try{Assert-M04X64UnwindMetadataSelection -Text $claim -Enabled $false}catch{$rejected=$true}
            if(-not $rejected){throw 'unselected child admitted'}
        }
        Write-Output 'TEST_PASS unwind.transcript'
        """;
}
