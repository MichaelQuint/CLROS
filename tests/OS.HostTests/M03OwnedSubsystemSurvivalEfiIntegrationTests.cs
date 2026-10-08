using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using OS.Boot;
using OS.Kernel.Diagnostics;

internal static unsafe class M03OwnedSubsystemSurvivalEfiIntegrationTests
{
    private static string root = null!, scratch = null!;
    [ModuleInitializer]
    internal static void Run()
    {
        root = Environment.GetEnvironmentVariable("CLROS_TEST_ROOT") ?? Directory.GetCurrentDirectory();
        while (!File.Exists(Path.Combine(root, "CLROS.slnx")))
            root = Directory.GetParent(root)?.FullName ?? throw new InvalidOperationException("CLROS root missing");
        scratch = Path.Combine(root, "artifacts", "host-tests", "m03-owned-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratch);
        Group("profile", Profile);
        Group("panic_capture", PanicCapture);
        Group("whole_transcript", WholeTranscript);
    }
    private static void Group(string name, Action test)
    {
        string marker = "m03.010.integration." + name;
        Console.WriteLine("TEST_START " + marker);
        try { test(); Console.WriteLine("TEST_PASS " + marker); }
        catch (Exception error) { Console.WriteLine("TEST_FAIL " + marker + " " + error.Message); throw; }
    }
    private static void Need(bool value, string message)
    { if (!value) throw new InvalidOperationException(message); }
    private static string Read(string path) => File.ReadAllText(Path.Combine(root, path));

    private static void Profile()
    {
        Need(!Probes.EnableM03OwnedSubsystemSurvivalProbe && !Probes.EnableM03OwnedSubsystemPanicProbe,
            "both dangerous children default off");
        OwnedAllocatorRoundTripState state = default;
        FirstExitDiagnosticState diagnostic = default;
        FirstExitOwnedArenaState arena = default;
        Need(BootSequence.RunOwnedSubsystemAllocatorRoundTrip(ref state, ref diagnostic, ref arena, default,
            (M01LiveAllocatorStaticState*)1) == OwnedAllocatorRoundTripStatus.AdmissionFailed &&
            state.Status == default && diagnostic.Status == default, "disabled boundary never accesses invalid owner");
        XDocument project = XDocument.Parse(Read("src/OS/OS.csproj"));
        var flags = project.Descendants().Where(e => e.Name.LocalName.StartsWith("Enable", StringComparison.Ordinal) &&
            e.Name.LocalName.EndsWith("Probe", StringComparison.Ordinal) && e.Attribute("Condition") != null).ToArray();
        int vectorCount = System.Text.RegularExpressions.Regex.Matches(project.ToString(), @"\$\((Enable\w+Probe)\)")
            .Select(m => m.Groups[1].Value).Distinct(StringComparer.Ordinal).Count();
        Need(vectorCount == 52 && flags.Length == 49 && flags.All(e => e.Value == "false"), "actual 52-property / 49-literal-default profile");
        Need(project.Descendants("Compile").Count() == 121, "actual explicit 121-file compile closure");
        foreach (string flag in new[] { "EnableM03OwnedSubsystemSurvivalProbe", "EnableM03OwnedSubsystemPanicProbe" })
        {
            Need(flags.Single(e => e.Name.LocalName == flag).Value == "false", "literal child default " + flag);
            foreach (string path in new[] { "scripts/build-efi-shell.ps1", "scripts/test-efi-artifact.ps1", "scripts/run-qemu-smoke.ps1" })
                Need(System.Text.RegularExpressions.Regex.IsMatch(Read(path), @"\[switch\]\s*\$" + flag + @"\b") &&
                    (Read(path).Contains("-" + flag, StringComparison.Ordinal) || Read(path).Contains("-p:" + flag + "=", StringComparison.Ordinal)),
                    "explicit propagated child " + path);
        }
        Need(Read("scripts/test-efi-artifact.ps1").Contains("$expectedSourceClosureInputCount = 137", StringComparison.Ordinal), "raw source count includes allocator leaf");
        string publisher = Read("scripts/build-efi-shell.ps1");
        Need(publisher.Contains("$sourceSnapshotBeforePublish.InputCount -ne 137", StringComparison.Ordinal) &&
            publisher.Contains("$sourceSnapshotAfterLock.InputCount -ne 137", StringComparison.Ordinal) &&
            publisher.Contains("EFI_PUBLISH_SOURCE_AUTHORITY status=ok inputs=137", StringComparison.Ordinal),
            "original unlocked/locked publisher authority admits the actual 137 inputs");
        string entry = Read("src/OS/Boot/EfiEntry.cs");
        int start = entry.IndexOf("private static bool ReportOwnedSubsystems", StringComparison.Ordinal);
        int end = entry.IndexOf("private static bool OwnedFailure", start, StringComparison.Ordinal);
        string operation = entry[start..end];
        Need(!operation.Contains("FirstExitPostExitDiagnostics.Report", StringComparison.Ordinal) &&
            operation.Split("BootSequence.RunOwnedSubsystemAllocatorRoundTrip", StringSplitOptions.None).Length == 2,
            "allocator wrapper is the sole original diagnostic invocation");
        Need(operation.Contains("ReadBackAcpiBootstrap(workspace, copied->Bytes", StringComparison.Ordinal) &&
            operation.Contains("ReadOwnedGuard((ulong*)state->PanicGuard)", StringComparison.Ordinal), "original readback and actual C# guard load");
        Need(Read("tools/OS.QemuRunner/Program.cs").Contains("--expect-owned-subsystem-panic", StringComparison.Ordinal), "explicit terminal-control mode");
    }

    private const string Firmware = "G1syShtbMDE7MDFIG1s9M2gbWzJKG1swMTswMUgbWzJKG1swMTswMUgbWz0zaBtbMkobWzAxOzAxSBtbMkobWzAxOzAxSBtbPTNoG1syShtbMDE7MDFIQmRzRHhlOiBsb2FkaW5nIEJvb3QwMDAxICJVRUZJIFFFTVUgSEFSRERJU0sgUU0wMDAxMyAiIGZyb20gUGNpUm9vdCgweDApL1BjaSgweDIsMHgwKS9TYXRhKDB4MCwweEZGRkYsMHgwKQ0KQmRzRHhlOiBzdGFydGluZyBCb290MDAwMSAiVUVGSSBRRU1VIEhBUkRESVNLIFFNMDAwMTMgIiBmcm9tIFBjaVJvb3QoMHgwKS9QY2koMHgyLDB4MCkvU2F0YSgweDAsMHhGRkZGLDB4MCkNCg==";
    private static string Frame(string[] lines) => Encoding.ASCII.GetString(Convert.FromBase64String(Firmware)) + string.Join("\r\n", lines) + "\r\n";
    private static string Row(int stage, params ulong[] values) => "M03_OWNED " + stage + string.Concat(values.Select(v => " 0x" + v.ToString("X16")));
    private static string[] Capture(bool panic)
    {
        // Independent synthetic original-parent transcript, with distinct static,
        // copied, allocator, table, code and arena owners. This is not guest proof.
        List<string> lines = [..Parent.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(s => s.TrimEnd('\r'))];
        ulong[] physical = [805306368, 805310464, 805314560, 805318656, 805322752];
        ulong[] lengths = [36, 60, 60, 44, 56];
        ulong[] signatures = [2329016660419433298, 1413763928, 1195787085, 1128878145, 1413828680];
        ulong[] indices = [ulong.MaxValue, ulong.MaxValue, 2, 0, 1];
        lines.Add("TEST_START m03.010");
        lines.Add(Row(0, 0x41000000, 0x50000000, 0x50000000, 2, 2, 8192, 0, 0));
        ulong offset = 0;
        for (int i = 0; i < 5; i++)
        {
            lines.Add(Row(i + 1, 0x42000000, 0x40000010, physical[i], offset, lengths[i], signatures[i], indices[i], lengths[i]));
            offset += lengths[i];
        }
        const ulong arena = 0x60000000, tables = 0x61000000, code = 0x62000000;
        lines.Add(Row(6, arena, tables, code, arena + 4096, arena + 8192, arena + 12288, 24, arena + 300008));
        if (!panic) { lines.Add("CLROS_M03_010_READY"); lines.Add("TEST_PASS m03.010"); return [..lines]; }
        lines.Add(Row(7, arena + 86016, tables, arena + 1024, code, arena, arena + 90120, arena + 352240, arena + 401392));
        lines.Add(Row(8, arena + 86016, tables, arena + 1024, arena + 401000, arena + 385032, arena + 401392, 14, 0));
        string fault = "OS_SMOKE_FAIL subsystem=boot reason=exit-fault vector=14 errorCodePresent=true errorCode=0x0000000000000000";
        string[] names = ["rip", "rsp", "rbp", "rflags", "cr2", "cr3", "rax", "rbx", "rcx", "rdx", "rsi", "rdi", "r8", "r9", "r10", "r11", "r12", "r13", "r14", "r15"];
        ulong[] values = [0x70001000, arena + 300000, 0, 2, arena + 86016, tables, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14];
        for (int i = 0; i < names.Length; i++) fault += " " + names[i] + "=0x" + values[i].ToString("X16");
        lines.Add(fault);
        return [..lines];
    }
    private static void PanicCapture()
    {
        string[] lines = Capture(true); string text = Frame(lines);
        Need(lines.Length == 77 && text.Count(c => c == '\n') == 79, "77 application rows / 79 newline observations include firmware preamble");
        Need(OwnedSubsystemPanicCapture.Evaluate(text, out string prefix) == 1 && prefix.Split("\r\n").Length == 77 &&
            !prefix.Contains("OS_SMOKE_FAIL", StringComparison.Ordinal), "finite prerequisite prefix plus exact terminal control");
        Need(OwnedSubsystemPanicCapture.Evaluate(text[..^2], out _) == 0, "incomplete final record cannot complete");
        Need(OwnedSubsystemPanicCapture.Evaluate(text + "TEST_PASS m03.010\r\n", out _) == -1, "no post-panic success");
        foreach (var change in new[] {
            (67, "0x0000000000002000", "0x0000000000001000"),
            (68, "0x0000000030000000", "0x0000000030000001"),
            (69, "0x0000000000000024", "0x0000000000000025"),
            (73, "0x0000000060003000", "0x0000000060004000"),
            (75, "0x0000000060061E68", "0x0000000060063000"),
            (76, "cr2=0x0000000060015000", "cr2=0x0000000060015008"),
            (76, "cr3=0x0000000061000000", "cr3=0x0000000061001000"),
            (76, "rflags=0x0000000000000002", "rflags=0x0000000000000202") })
        {
            string[] bad = (string[])lines.Clone(); Need(bad[change.Item1].Contains(change.Item2), "mutation target exists");
            bad[change.Item1] = bad[change.Item1].Replace(change.Item2, change.Item3);
            Need(OwnedSubsystemPanicCapture.Evaluate(Frame(bad), out _) == -1, "reject association mutation row " + change.Item1);
        }
        Need(OwnedSubsystemPanicCapture.Evaluate("X" + text[1..], out _) == -1, "exact original firmware framing");
        Need(OwnedSubsystemPanicCapture.Evaluate(text.Replace("0x0000000041000000", "0x41000000"), out _) == -1, "fixed-width fields");
        File.WriteAllText(Path.Combine(scratch, "panic.log"), text, new UTF8Encoding(false));
        string[] positive = Capture(false);
        Need(positive.Length == 76 && Frame(positive).Count(c => c == '\n') == 78, "positive application/stream counts");
        File.WriteAllText(Path.Combine(scratch, "positive.log"), Frame(positive), new UTF8Encoding(false));
    }
    private static void WholeTranscript()
    {
        string script = Path.Combine(scratch, "controls.ps1");
        File.WriteAllText(script, Script, new UTF8Encoding(false));
        var start = new ProcessStartInfo("powershell.exe") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string arg in new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", script, "-Source", Path.Combine(root, "scripts/run-qemu-smoke.ps1"), "-Directory", scratch }) start.ArgumentList.Add(arg);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30000)) { process.Kill(true); process.WaitForExit(); throw new InvalidOperationException("bounded parser helper timeout"); }
        string stdout = output.GetAwaiter().GetResult(), stderr = error.GetAwaiter().GetResult();
        File.WriteAllText(Path.Combine(scratch, "stdout.log"), stdout); File.WriteAllText(Path.Combine(scratch, "stderr.log"), stderr);
        File.WriteAllText(Path.Combine(scratch, "process.json"), JsonSerializer.Serialize(new { start.FileName, Args = start.ArgumentList, process.ExitCode }));
        Need(process.ExitCode == 0 && stderr.Length == 0 && stdout.Contains("TEST_PASS m03.010.parser controls=12", StringComparison.Ordinal), "whole transcript controls: " + stdout + stderr);
    }
    private const string Script = """
        param([string]$Source,[string]$Directory)
        $ErrorActionPreference='Stop';$root=$Directory;$tokens=$null;$errors=$null
        $ast=[Management.Automation.Language.Parser]::ParseFile($Source,[ref]$tokens,[ref]$errors)
        if($errors.Count){throw 'syntax'}
        foreach($name in @('Read-M03MemoryMapCaptureRecord','Assert-M03OwnedSubsystemRecords','Assert-M03AcpiBootstrapTranscript')){
            $f=@($ast.FindAll({param($n)$n -is [Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -ceq $name},$true));if($f.Count -ne 1){throw 'function inventory'}
            Invoke-Expression $f[0].Extent.Text
        }
        foreach($name in @('M03FirstExitStageMarkers','M03FirstExitTerminalMarkers')){
            $a=@($ast.FindAll({param($n)$n -is [Management.Automation.Language.AssignmentStatementAst] -and $n.Left.Extent.Text -ceq ('$script:'+$name)},$true));if($a.Count -ne 1){throw 'marker inventory'}
            Invoke-Expression $a[0].Extent.Text
        }
        $positive=[IO.File]::ReadAllText((Join-Path $Directory 'positive.log'));$panic=[IO.File]::ReadAllText((Join-Path $Directory 'panic.log'));$script:cases=0
        function Check([string]$text,[bool]$control,[bool]$expected){
            $path=Join-Path $Directory ('case-'+$script:cases+'.log');[IO.File]::WriteAllText($path,$text,(New-Object Text.UTF8Encoding($false)))
            $accepted=$true;try{Assert-M03AcpiBootstrapTranscript $path -FirstRealExit -OwnedSurvival -OwnedPanic:$control}catch{$accepted=$false;$reason=$_.Exception.Message}
            if($accepted -ne $expected){throw ('case '+$script:cases+' '+$reason)};$script:cases++
        }
        Check $positive $false $true
        Check $panic $true $true
        Check ($positive.Replace('0x0000000041000000','0x0000000040000000')) $false $false
        Check ($positive.Replace('0x0000000030000000','0x0000000030000001')) $false $false
        Check ($positive.Replace('BOOT_MEMORY_REGION_COUNT 4','BOOT_MEMORY_REGION_COUNT 5')) $false $false
        Check ($positive.Replace('mismatches=0','mismatches=1')) $false $false
        Check ($positive.Replace('CLROS_M03_010_READY','CLROS_M03_010_READY extra=1')) $false $false
        Check ($panic.Replace('M03_OWNED 8','M03_OWNED 7')) $true $false
        Check ($panic.Replace('cr2=0x0000000060015000','cr2=0x0000000060015008')) $true $false
        Check ($panic+"TEST_PASS m03.010`r`n") $true $false
        Check ($panic.Replace("`r`n","`n")) $true $false
        Check ('X'+$panic.Substring(1)) $true $false
        Write-Output ('TEST_PASS m03.010.parser controls='+$script:cases)
        """;

    private const string Parent = """
        TEST_START efi.main
        EFI_CONSOLE_READY
        UEFI_MEMORY_MAP_READY
        UEFI_MEMORY_MAP_DESCRIPTOR_COUNT 4
        UEFI_MEMORY_MAP_OWNED
        BOOT_HANDOFF_READY
        UEFI_MEMORY_MAP_NORMALIZED
        BOOT_MEMORY_MAP_ACCEPTED
        BOOT_MEMORY_REGIONS_READY
        BOOT_MEMORY_REGION_COUNT 4
        BOOT_PAGE_ALLOCATOR_READY status=Ready regions=4 conventionalPages=20 conventionalBytes=81920 freeNodes=2 freePages=20 freeBytes=81920 pageSize=4096 statusCode=0
        BOOT_ACPI_RSDP_HANDOFF_READY
        UEFI_ACPI_RSDP_READY
        ACPI_RSDP_SNAPSHOT_READY
        ACPI_RSDP_VALIDATED
        ACPI_ROOT_SDT_SNAPSHOT_READY
        ACPI_ROOT_SDT_VALIDATED
        ACPI_MCFG_DISCOVERY_READY
        TEST_START m03.002
        CLROS_M03_002_TABLE role=rsdp physical=805306368 owned=1073741840 offset=0 length=36 signature=2329016660419433298 rootIndex=18446744073709551615 compared=36 mismatches=0
        CLROS_M03_002_TABLE role=root physical=805310464 owned=1073741876 offset=36 length=60 signature=1413763928 rootIndex=18446744073709551615 compared=60 mismatches=0
        CLROS_M03_002_TABLE role=mcfg physical=805314560 owned=1073741936 offset=96 length=60 signature=1195787085 rootIndex=2 compared=60 mismatches=0
        CLROS_M03_002_TABLE role=apic physical=805318656 owned=1073741996 offset=156 length=44 signature=1128878145 rootIndex=0 compared=44 mismatches=0
        CLROS_M03_002_TABLE role=hpet physical=805322752 owned=1073742040 offset=200 length=56 signature=1413828680 rootIndex=1 compared=56 mismatches=0
        CLROS_M03_002_RETAIN total=256 descriptors=5 guards=32 tail=265984 mismatches=0
        CLROS_M03_002_READY firmwareAvailable=true exitBootServices=false
        TEST_PASS m03.002
        TEST_START m01.stage1
        M01_LOADED_IMAGE_CAPTURED
        M01_STACK_SWITCH_STUB_READY
        M01_STACK_ARENA_ALLOCATED
        M01_STACK_GUARD_RESERVED
        M01_STAGE1_MAP_RECAPTURED
        M01_STAGE1_RESERVATIONS_VALIDATED
        M01_OWNED_STACK_READY
        M01_OWNED_STACK_SWITCHED
        M01_RSP_CONTAINED
        TEST_PASS m01.stage1
        TEST_START m01.stage2
        M01_ALLOCATOR_ARENA_ALLOCATED
        M01_STAGE2_MAP_RECAPTURED
        M01_RESERVATIONS_APPLIED
        M01_LIVE_ALLOCATED_4K
        M01_LIVE_PAGE_WITHIN_ARENA
        M01_LIVE_FREED_4K
        M01_DOUBLE_FREE_REJECTED
        TEST_PASS m01.stage2
        TEST_START boot.sequence
        BOOT_PHASE PhaseA.Audit START
        BOOT_PHASE PhaseA.Audit PASS
        BOOT_PHASE PhaseA.Diagnostics START
        BOOT_PHASE PhaseA.Diagnostics PASS
        OS_BOOT_OK
        TEST_PASS boot.sequence
        OS_SMOKE_PASS
        TEST_START m03.007
        M03_EBS_RETURNED_SUCCESS
        M03_PERMANENT_CR3_ADOPTED
        M03_POST_EBS_OWNED_DIAGNOSTIC
        CLROS_M03_007_READY
        TEST_PASS m03.007
        TEST_START m03.009
        UEFI_EBS_SUCCESS
        POST_FIRMWARE_DIAGNOSTICS_READY
        CLROS_M03_009_READY
        TEST_PASS m03.009
        """;
}
