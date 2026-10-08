using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using OS.Tools.QemuRunner;

internal static class M04GCStaticRootMaterializationEfiIntegrationTests
{
    private const string Flag = "EnableM04GcStaticRootMaterializationProbe";
    private static readonly string[] Chain =
    [
        "EnableM01LoadedImageStackProbe", "EnableM01LiveAllocatorProbe",
        "EnableM04IndependentEndianConformanceProbe", "EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe",
        "EnableM04ArithmeticConformanceProbe", "EnableM04LayoutContractProbe",
        "EnableM04ManagedHeapPageRangeProbe", "EnableM04RhpNewFastProbe"
    ];

    [ModuleInitializer]
    internal static void RunAll()
    {
        Run("default-off source and complete profile admission", Source);
        Run("strict transcript and GC numeric rejection controls", Transcript);
        Run("actual profile marker stream and isolation", Profile);
    }

    private static void Run(string name, Action action)
    {
        Console.WriteLine("TEST_START m04.012.efi " + name);
        try { action(); Console.WriteLine("TEST_PASS m04.012.efi " + name); }
        catch (Exception e) { Console.WriteLine("TEST_FAIL m04.012.efi " + name + " " + e); throw; }
    }
    private static void Need(bool value, string reason)
    { if (!value) throw new InvalidOperationException(reason); }
    private static string Root => HostToolProcess.FindRepositoryRoot();
    private static string Scratch(string name)
    {
        string path = Path.Combine(Root, "artifacts/issue548-tests", name + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void Source()
    {
        var project = XDocument.Load(Path.Combine(Root, "src/OS/OS.csproj"));
        var defaults = project.Descendants(Flag).ToArray();
        Need(defaults.Length == 1 && defaults[0].Value == "false", "one disabled profile default");
        string authority = File.ReadAllText(Path.Combine(Root, "src/OS.Kernel/Diagnostics/GCStaticRootMaterializationProbeAuthority.cs"));
        Need(authority.Contains("EnableGCStaticRootMaterializationProbe = false;", StringComparison.Ordinal), "disabled compile-time authority");
        var conditions = project.Descendants("Error").Select(x => (string)x.Attribute("Condition")!).ToArray();
        foreach (string prerequisite in Chain)
            Need(conditions.Count(x => x == "'$(" + Flag + ")' == 'true' and '$(" + prerequisite + ")' != 'true'") == 1, "explicit prerequisite " + prerequisite);
        string[] flags = Regex.Matches(project.ToString(), @"\$\((Enable\w+Probe)\)")
            .Select(x => x.Groups[1].Value).Distinct(StringComparer.Ordinal).ToArray();
        Need(flags.Length == 52, "complete actual profile vector");
        Need(conditions.Contains("'$(EnableM04SeededCollectorChurnProbe)' == 'true' and '$(EnableM04GcStaticRootMaterializationProbe)' != 'true'"),"churn inherits GC prerequisites and conflicts");
        Need(conditions.Count(x => x == "'$(EnableM04ClassConstructorExecutionProbe)' == 'true' and '$(EnableM04GcStaticRootMaterializationProbe)' != 'true'") == 1, "constructors inherit GC prerequisites and conflicts");
        Need(conditions.Count(x => x == "'$(EnableM04InterfaceDispatchProbe)' == 'true' and '$(EnableM04ClassConstructorExecutionProbe)' != 'true'") == 1, "dispatch inherits constructor and GC prerequisites and conflicts");
        Need(conditions.Count(x => x == "'$(EnableM04ClassConstructorExecutionProbe)' == 'true' and '$(EnableM04SeededCollectorChurnProbe)' == 'true'") == 1, "constructor and churn children are exclusive");
        Need(conditions.Count(x => x == "'$(EnableM03FirmwareAdapterRevocationProbe)' == 'true' and '$(EnableM03FirstRealExitBootServicesProbe)' != 'true'") == 1, "revocation inherits first-exit prerequisites and conflicts");
        Need(conditions.Count(x => x == "'$(EnableM04X64UnwindMetadataProbe)' == 'true' and '$(EnableM04TypeManagerModuleRegistrationProbe)' != 'true'") == 1, "unwind inherits original TypeManager prerequisites and conflicts");
        Need(conditions.Count(x => x == "'$(EnableM03OwnedSubsystemSurvivalProbe)' == 'true' and '$(EnableM03FirstRealExitBootServicesProbe)' != 'true'") == 1, "exact owned inherited prerequisite EnableM03OwnedSubsystemSurvivalProbe");
        Need(conditions.Count(x => x == "'$(EnableM03OwnedSubsystemPanicProbe)' == 'true' and '$(EnableM03OwnedSubsystemSurvivalProbe)' != 'true'") == 1, "exact owned inherited prerequisite EnableM03OwnedSubsystemPanicProbe");
        Need(project.Descendants("Error").Count(x => x.Attribute("Condition")?.Value == "'$(EnableM04SimpleThrowCatchProbe)' == 'true' and '$(EnableM04GcStaticRootMaterializationProbe)' != 'true'") == 1, "simple catch inherits exact GC prerequisites and conflicts");
        Need(project.Descendants("Error").Count(x => x.Attribute("Condition")?.Value == "'$(EnableM04SingleFinallyCatchProbe)' == 'true' and '$(EnableM04GcStaticRootMaterializationProbe)' != 'true'") == 1, "single finally inherits exact GC prerequisites and conflicts");
        Need(conditions.Count(x => x == "'$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true' and '$(" + Flag + ")' == 'true'") == 1, "incoming original TypeManager conflict");
        foreach (string other in flags.Except(Chain.Append(Flag).Append("EnableM04SeededCollectorChurnProbe").Append("EnableM04ClassConstructorExecutionProbe").Append("EnableM04InterfaceDispatchProbe").Append("EnableM04SimpleThrowCatchProbe").Append("EnableM04SingleFinallyCatchProbe"), StringComparer.Ordinal))
        {
            string profile = other is "EnableM03FirmwareAdapterRevocationProbe" or "EnableM03OwnedSubsystemSurvivalProbe" or "EnableM03OwnedSubsystemPanicProbe" ? "EnableM03FirstRealExitBootServicesProbe"
                : other == "EnableM04X64UnwindMetadataProbe" ? "EnableM04TypeManagerModuleRegistrationProbe" : other;
            Need(conditions.Count(x => x == (profile == "EnableM03FirstRealExitBootServicesProbe"
                ? "'$(" + profile + ")' == 'true' and '$(" + Flag + ")' == 'true'"
                : "'$(" + Flag + ")' == 'true' and '$(" + profile + ")' == 'true'")) == 1, "conflict " + other);
        }
        string[] compile = project.Descendants("Compile").Select(x => (string)x.Attribute("Include")!).ToArray();
        Need(compile.Length == 122 && compile.Distinct(StringComparer.Ordinal).Count() == 122, "unique complete closure");
        foreach (string file in new[] { @"..\OS.Kernel\Runtime\GCStaticRootsMalformedMetadata.cs", @"Runtime\GCStaticRootMaterializationProbe.cs", @"..\OS.Kernel\Runtime\BoundedNonMovingCollectorExplicitRootEnumeration.cs", @"..\OS.Kernel\Diagnostics\GCStaticRootMaterializationProbeAuthority.cs" })
            Need(compile.Contains(file, StringComparer.Ordinal), "compiled source " + file);
    }

    private static (int Exit, string Output, string Error) PowerShell(string directory, params string[] args)
    {
        Directory.CreateDirectory(directory);
        var start = new ProcessStartInfo("powershell.exe") { WorkingDirectory = Root, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string arg in new[] { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File" }.Concat(args)) start.ArgumentList.Add(arg);
        start.Environment["PSModulePath"] = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32/WindowsPowerShell/v1.0/Modules");
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        bool ended = process.WaitForExit(120_000);
        if (!ended) { process.Kill(true); process.WaitForExit(); }
        string output = stdout.GetAwaiter().GetResult(), error = stderr.GetAwaiter().GetResult();
        File.WriteAllText(Path.Combine(directory, "stdout.log"), output);
        File.WriteAllText(Path.Combine(directory, "stderr.log"), error);
        Need(ended, "PowerShell timed out");
        return (process.ExitCode, output, error);
    }

    private static void Transcript()
    {
        string directory = Scratch("transcript"), script = Path.Combine(directory, "controls.ps1"), serial = Path.Combine(directory, "serial.log");
        File.WriteAllText(script, Controls, new UTF8Encoding(false));
        File.WriteAllText(serial, string.Join("\r\n", Lines) + "\r\n", new UTF8Encoding(false));
        var result = PowerShell(directory, script, "-Source", Path.Combine(Root, "scripts/run-qemu-smoke.ps1"), "-Serial", serial);
        Need(result.Exit == 0 && result.Error.Length == 0 && result.Output.Contains("TEST_PASS gc.static.transcript", StringComparison.Ordinal), result.Output + result.Error);
    }

    private static void Profile()
    {
        string directory = Scratch("profile");
        var enabled = PowerShell(directory, [Path.Combine(Root, "scripts/run-qemu-smoke.ps1"), "-BootPolicy", "Diagnostic", "-DryRun", "-BuildEfi", ..Chain.Append(Flag).Select(x => "-" + x)]);
        Need(enabled.Exit == 0 && enabled.Error.Length == 0, enabled.Output + enabled.Error);
        string[] Markers(string key) => enabled.Output.Split('\n').Single(x => x.StartsWith(key, StringComparison.Ordinal))[key.Length..].TrimEnd('\r').Split(';', StringSplitOptions.RemoveEmptyEntries);
        string[] required = Markers("REQUIRED_MARKERS "), forbidden = Markers("FORBIDDEN_MARKERS "), ordered = Markers("ORDERED_MARKERS "), unique = Markers("UNIQUE_MARKERS ");
        Need(ordered.Length == 59 && unique.SequenceEqual(ordered, StringComparer.Ordinal), "complete ordered unique vector");
        SerialMarkerCompletionResult Stream(string[] lines)
        {
            var state = new SerialMarkerStateMachine(required, forbidden, orderedMarkers: ordered, uniqueMarkers: unique);
            Need(state.Feed(Encoding.ASCII.GetBytes(string.Join("\r\n", lines) + "\r\n")).Succeeded, "stream feed");
            return state.Complete();
        }
        Need(Stream(Lines).Passed, "full GC profile stream");
        foreach (int index in Enumerable.Range(44, 18))
        {
            Need(!Stream(Lines.Where((_, i) => i != index).ToArray()).Passed, "missing GC stage");
            Need(Stream(Lines.Take(index + 1).Concat(Lines.Skip(index)).ToArray()).DuplicateUniqueOccurrenceCount == 1, "duplicate GC stage");
            string[] swapped = [..Lines]; (swapped[index], swapped[index - 1]) = (swapped[index - 1], swapped[index]);
            Need(Stream(swapped).OutOfOrderOccurrenceCount > 0, "early GC stage");
        }
        var baseline = PowerShell(Path.Combine(directory, "default"), Path.Combine(Root, "scripts/run-qemu-smoke.ps1"), "-BootPolicy", "Diagnostic", "-DryRun");
        Need(baseline.Exit == 0 && !baseline.Output.Split('\n').Where(x => x.StartsWith("ORDERED_MARKERS ", StringComparison.Ordinal)).Any(x => x.Contains("M04_GC_ROOT", StringComparison.Ordinal)), "default marker isolation");
        var missing = PowerShell(Path.Combine(directory, "missing"), [Path.Combine(Root, "scripts/run-qemu-smoke.ps1"), "-BootPolicy", "Diagnostic", "-DryRun", "-BuildEfi", "-" + Flag, ..Chain.SkipLast(1).Select(x => "-" + x)]);
        Need(missing.Exit != 0 && missing.Output.Contains("m04_gc_static_profile_invalid property=EnableM04RhpNewFastProbe", StringComparison.Ordinal), "incomplete chain refused");
        var collision = PowerShell(Path.Combine(directory, "conflict"), [Path.Combine(Root, "scripts/run-qemu-smoke.ps1"), "-BootPolicy", "Diagnostic", "-DryRun", "-BuildEfi", "-EnableM04CheckedReferenceBarrierProbe", ..Chain.Append(Flag).Select(x => "-" + x)]);
        Need(collision.Exit != 0 && collision.Output.Contains("m04_gc_static_profile_conflict property=EnableM04CheckedReferenceBarrierProbe", StringComparison.Ordinal), "overlapping allocator role refused");
    }

    private static string[] Lines => Protocol.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
    // Fixed accepted guest-shaped input; these are parser controls, not guest proof.
    private const string Protocol = """
        TEST_START efi.main
        EFI_CONSOLE_READY
        CLROS_M04_002_READY readVectors=25 reverseVectors=7 byteSeries=5 uint16Series=7 fingerprint=E81E4451E8D999F6 referenceFilesRequired=false hostConformance=false guestIntegrated=true gate4=Fail
        CLROS_M04_003_READY vectors=16 spanVectors=4 overlapVectors=5 memoryCastVectors=3 unsafeWidthVectors=4 fingerprint=F03FF59D36F39ECD pointerWidth=8 referenceFilesRequired=false hostConformance=false guestIntegrated=true gate4=Fail
        CLROS_M04_004_READY vectors=57 fingerprint=45FDFA24DD72EA28 referenceFilesRequired=false hostConformance=false guestIntegrated=true gate4=Fail
        CLROS_M04_005_READY profile=ilc10.0.12-win-x64-le checks=19 shapes=3 prefixBytes=24 referenceFilesRequired=false hostConformance=false guestIntegrated=true gate4=Fail
        UEFI_MEMORY_MAP_READY
        UEFI_MEMORY_MAP_DESCRIPTOR_COUNT 102
        UEFI_MEMORY_MAP_OWNED
        BOOT_HANDOFF_READY
        UEFI_MEMORY_MAP_NORMALIZED
        BOOT_MEMORY_MAP_ACCEPTED
        BOOT_MEMORY_REGIONS_READY
        BOOT_MEMORY_REGION_COUNT 102
        BOOT_PAGE_ALLOCATOR_READY status=Ready regions=102 conventionalPages=52140 conventionalBytes=213565440 freeNodes=9 freePages=52140 freeBytes=213565440 pageSize=4096 statusCode=0
        BOOT_ACPI_RSDP_HANDOFF_READY
        UEFI_ACPI_RSDP_READY
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
        TEST_START m04.006
        M04_HEAP_PAGE_BOUND allocatorState=0x000000000DC13528 nodes=0x000000000DC134F8 heapState=0x000000000DC13580 ledger=0x000000000DC13380 parentIndex=1 parentStart=0x000000000DC44000 parentEnd=0x000000000DC46000 backing=0x000000000DC44000 retainedStart=0x000000000DC44000 retainedEnd=0x000000000DC45000
        M04_HEAP_PAGE_INITIALIZED pages=1 bytes=4096 freePagesBefore=2 freeBytesBefore=8192 allocatedPagesBefore=0 allocatedBytesBefore=0 freePagesAfter=1 freeBytesAfter=4096 allocatedPagesAfter=1 allocatedBytesAfter=4096 readBytes=4096 contentsZero=true
        M04_HEAP_PAGE_RETAINED checkpoint=beforeBootPlan allocatorState=0x000000000DC13528 nodes=0x000000000DC134F8 heapState=0x000000000DC13580 ledger=0x000000000DC13380 parentIndex=1 parentStart=0x000000000DC44000 parentEnd=0x000000000DC46000 backing=0x000000000DC44000 retainedStart=0x000000000DC44000 retainedEnd=0x000000000DC45000
        M04_HEAP_PAGE_CURRENT freePages=1 freeBytes=4096 allocatedPages=1 allocatedBytes=4096 readBytes=4096 contentsZero=true
        CLROS_M04_006_READY pages=1 bytes=4096 retained=true allocatorMutated=true zeroReadback=true checkpoint=beforeBootPlan hostConformance=false guestIntegrated=true objectAllocation=false gc=false hardwareNxProved=false postExitBootServices=false gate4=Fail
        TEST_PASS m04.006
        M04_GC_ROOT 0 0x000000000DB0D000 0x000000000010B000 0x0000000000000000 0x0000000000000000
        M04_GC_ROOT 1 0x0000000000000001 0x000000000DB5F000 0x0000000000000004 0x000000000DB60C70
        M04_GC_ROOT 7 0x000000000DE00000 0x000000000DE21000 0x000000000DE20FD8 0x000000000DE20000
        M04_GC_ROOT 3 0x000000000DB5F028 0x000000000DC44008 0x000000000DC44010 0x0000000000000018
        M04_GC_ROOT 4 0x000000000DC44020 0x000000000DB59300 0x0000000000000018 0x0000000000000030
        M04_GC_ROOT 5 0x0000000000000001 0x0000000000000001 0x000000000DC44020 0x7143B6A952D08E2F
        M04_GC_ROOT 6 0x0000000000000001 0x0000000000000002 0x0000000000000002 0x0000000000000030
        M04_GC_ROOT 8 0x0000000000000005 0x195A86B4C327D0EF 0x62C79E1035AB48D6 0x000000000DE1FF00
        M04_COLLECT 0 0x000000000DB59330 0x0000000000000020 0x0000000000000008 0x0000000000000008
        M04_COLLECT 1 0x000000000DC44038 0x000000000DC44058 0x000000000DC44078 0x000000000DC44098
        M04_COLLECT 2 0x0000000000000001 0x0000000000000004 0x0000000000000002 0x0000000000000040
        M04_COLLECT 3 0x000000000DC44078 0x000000000DC44078 0x0000000000000005 0x0000000000000007
        M04_COLLECT 4 0x0000000000000001 0x0000000000000002 0x0000000000000003 0x0000000000000058
        M04_COLLECT 5 0x0000000000000001 0x0000000000000001 0x0000000000000001 0x0000000000000020
        M04_COLLECT 6 0x0000000000000001 0x0000000000000002 0x0000000000000000 0x0000000000000000
        M04_COLLECT 7 0x000000000DC44020 0x00000000000000B0 0x0000000000000008 0x0000000000000007
        M04_COLLECT 8 0x000000000000000A 0x195A86B4C327D0EF 0x62C79E1035AB48D6 0x000000000DE1FE00
        CLROS_M04_013_READY
        TEST_START boot.sequence
        BOOT_PHASE PhaseA.Audit START
        BOOT_PHASE PhaseA.Audit PASS
        BOOT_PHASE PhaseA.Diagnostics START
        BOOT_PHASE PhaseA.Diagnostics PASS
        OS_BOOT_OK
        TEST_PASS boot.sequence
        OS_SMOKE_PASS
        """;

    private const string Controls = """
        param([string]$Source,[string]$Serial)
        $ErrorActionPreference='Stop'
        $tokens=$null;$errors=$null
        $ast=[Management.Automation.Language.Parser]::ParseFile($Source,[ref]$tokens,[ref]$errors)
        if($errors.Count){throw 'parser syntax'}
        foreach($statement in $ast.EndBlock.Statements) {
            if($statement.Extent.Text -cmatch '^\$script:M04(?:IndependentEndianConformance|SpanMemoryConformance|ArithmeticConformance|LayoutContract)\w+ =') {
                . ([scriptblock]::Create($statement.Extent.Text))
            }
        }
        foreach($f in $ast.FindAll({param($n) $n -is [Management.Automation.Language.FunctionDefinitionAst]},$true)) { . ([scriptblock]::Create($f.Extent.Text)) }
        $good=[IO.File]::ReadAllText($Serial)
        function Check([string]$text,[bool]$accept) {
            [IO.File]::WriteAllText($Serial,$text,[Text.UTF8Encoding]::new($false))
            $passed=$true
            try { Assert-M04ManagedHeapPageRangeTranscript -SerialLogPath $Serial -IncludeGcStatics } catch { if($accept){throw}; $passed=$false }
            if($passed -ne $accept){throw ('unexpected transcript admission accept='+$accept)}
        }
        Check $good $true
        $lines=$good.Split(@("`r`n"),[StringSplitOptions]::RemoveEmptyEntries)
        foreach($i in 44..60) {
            Check ((@($lines[0..($i-1)])+@($lines[($i+1)..69]) -join "`r`n")+"`r`n") $false
            Check ((@($lines[0..$i])+@($lines[$i..69]) -join "`r`n")+"`r`n") $false
            $bad=$lines.Clone();$bad[$i]+=' ';Check (($bad -join "`r`n")+"`r`n") $false
        }
        foreach($case in @(@(44,2),@(44,4),@(45,2),@(45,3),@(45,4)) + @(foreach($i in 46..60){foreach($j in 2..5){,@($i,$j)}})) {
            $bad=$lines.Clone();$words=$bad[$case[0]].Split(' ')
            $words[$case[1]]='0xFFFFFFFFFFFFFFFF';$bad[$case[0]]=$words -join ' '
            Check (($bad -join "`r`n")+"`r`n") $false
        }
        Check ($good.Replace('CLROS_M04_013_READY','CLROS_M04_013_NOT_READY')) $false
        Check ($good.Replace('M04_GC_ROOT 6','M04_GC_ROOT 255')) $false
        Check ($good.Replace('M04_GC_ROOT 5','M04_GC_ROOT 2')) $false
        Check ($good+"TEST_FAIL hidden`r`n") $false
        Check ($good+"suffix`r`n") $false
        Check ($good.Replace("`r`n","`n")) $false
        Write-Output 'TEST_PASS gc.static.transcript'
        """;
}
