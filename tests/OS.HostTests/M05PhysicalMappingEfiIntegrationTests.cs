using System.Runtime.CompilerServices;
using System.Diagnostics;
using System.Text;
using OS.Boot;
using OS.Kernel.Diagnostics;

internal static unsafe class M05PhysicalMappingEfiIntegrationTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        const string marker = "m05.001.efi disabled boot gate";
        Console.WriteLine("TEST_START " + marker);
        int invocations = 0;
        if (Probes.EnableM05PhysicalMappingProbe ||
            BootSequence.RunPostFirmwarePhysicalMapping(true, &invocations, &Execute) ||
            BootSequence.RunPostFirmwarePhysicalMapping(false, &invocations, &Execute) ||
            BootSequence.RunPostFirmwarePhysicalMapping(true, null, &Execute) ||
            BootSequence.RunPostFirmwarePhysicalMapping(true, &invocations, null) ||
            invocations != 0)
        {
            Console.WriteLine("TEST_FAIL " + marker + " disabled profile invoked callback");
            throw new InvalidOperationException("disabled mapping profile");
        }
        Console.WriteLine("TEST_PASS " + marker);
        TranscriptControls();
    }

    private static bool Execute(void* context)
    {
        (*(int*)context)++;
        return true;
    }

    private static void TranscriptControls()
    {
        const string marker = "m05.001.efi transcript associations";
        Console.WriteLine("TEST_START " + marker);
        string root = Directory.GetCurrentDirectory();
        while (!File.Exists(Path.Combine(root, "CLROS.slnx")))
            root = Directory.GetParent(root)?.FullName ?? throw new InvalidOperationException("CLROS root missing");
        string directory = Path.Combine(root, "artifacts", "host-tests", "m05-mapping-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        const ulong table = 0x500000, scratch = 0x4000000, parent = 0x501100, pt = 0x504000;
        const ulong nx = 1UL << 63, oldPat = 0x0007040600070406, newPat = 0x0007040100070406;
        ulong large = nx | scratch | 0xe3, uc = nx | (scratch + 4096) | 0x7b;
        ulong wc = nx | (scratch + 4096) | 0xe3, wb = nx | (scratch + 4096) | 0x63;
        string Row(int stage, params ulong[] values) => "M05_MAP " + stage + string.Concat(values.Select(v => " 0x" + v.ToString("x16")));
        string[] lines = Enumerable.Repeat("fixture", 86).ToArray();
        lines[73] = "M03_OWNED 6" + string.Concat(new ulong[] { 0x1000000, table, 0x2000000, 0, 0, 0, 0, 0 }.Select(v => " 0x" + v.ToString("X16")));
        lines[76] = "TEST_START m05.001";
        lines[77] = Row(0, table, scratch, 0x508, 0x800);
        lines[78] = Row(1, oldPat, newPat, table, 0x11000);
        lines[79] = Row(2, parent, pt | 0x23, large, 4);
        lines[80] = Row(3, parent, large, pt | 0x23, uc);
        lines[81] = Row(4, pt + 8, uc, wc, newPat);
        lines[82] = Row(5, pt + 8, wc, wb, 0x5743000000000001);
        lines[83] = Row(6, table, scratch, oldPat, wb);
        lines[84] = "CLROS_M05_001_READY"; lines[85] = "TEST_PASS m05.001";
        File.WriteAllLines(Path.Combine(directory, "rows.txt"), lines, new UTF8Encoding(false));
        string script = Path.Combine(directory, "controls.ps1");
        File.WriteAllText(script, ParserControls, new UTF8Encoding(false));
        var start = new ProcessStartInfo("powershell.exe") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string argument in new[] { "-NoProfile", "-File", script, "-Source", Path.Combine(root, "scripts/run-qemu-smoke.ps1"), "-Directory", directory })
            start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30000)) { process.Kill(true); throw new InvalidOperationException("mapping parser timeout"); }
        string output = stdout.GetAwaiter().GetResult(), error = stderr.GetAwaiter().GetResult();
        File.WriteAllText(Path.Combine(directory, "stdout.log"), output); File.WriteAllText(Path.Combine(directory, "stderr.log"), error);
        if (process.ExitCode != 0 || error.Length != 0 || !output.Contains("TEST_PASS m05.mapping.rows controls=11", StringComparison.Ordinal))
            throw new InvalidOperationException("mapping parser: " + output + error);
        Console.WriteLine("TEST_PASS " + marker);
    }

    private const string ParserControls = """
        param([string]$Source,[string]$Directory)
        $ErrorActionPreference='Stop'; $tokens=$null; $errors=$null
        $ast=[Management.Automation.Language.Parser]::ParseFile($Source,[ref]$tokens,[ref]$errors)
        if($errors.Count){throw 'syntax'}
        $functions=@($ast.FindAll({param($n) $n -is [Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -ceq 'Assert-M05PhysicalMappingRecords'},$true))
        if($functions.Count -ne 1){throw 'function inventory'}
        Invoke-Expression $functions[0].Extent.Text
        $good=[IO.File]::ReadAllLines((Join-Path $Directory 'rows.txt')); $script:cases=0
        function Check([string[]]$lines,[bool]$expected){
            $accepted=$true; $reason=''; try {Assert-M05PhysicalMappingRecords -Lines $lines} catch {$accepted=$false;$reason=$_.Exception.Message}
            if($accepted -ne $expected){throw ('case '+$script:cases+' '+$reason)}; $script:cases++
        }
        Check $good $true
        foreach($change in @(
            @(77,'0x0000000000500000','0x0000000000501000'),
            @(77,'0x0000000004000000','0x0000000001000000'),
            @(77,'0x0000000000000508','0x0000000000000d08'),
            @(78,'0x0007040100070406','0x0007040100070006'),
            @(79,'0x0000000000000004','0x0000000000000037'),
            @(79,'0x0000000000504023','0x0000000000504063'),
            @(80,'0x800000000400107b','0x000000000400107b'),
            @(81,'0x80000000040010e3','0x8000000004001063'),
            @(83,'0x0007040600070406','0x0007040100070406')
        )){
            $copy=[string[]]$good.Clone(); $copy[$change[0]]=$copy[$change[0]].Replace($change[1],$change[2]);
            if($copy[$change[0]] -ceq $good[$change[0]]){throw 'mutation missed'}; Check $copy $false
        }
        Check @($good; 'unexpected trailing row') $false
        Write-Output ('TEST_PASS m05.mapping.rows controls='+$script:cases)
        """;
}
