using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

internal static class M02OwnedGdtTssTerminalEfiIntegrationTests
{
    private const string TestName = "m02.009.efi.integration";
    private const string ReadyMarker =
        "CLROS_M02_009_READY gdtr=true tr=true gdtBytes=40 " +
        "tssBytes=104 gdtrLimit=39 trSelector=24 ownedCom1=true " +
        "terminalHalt=true postExitBootServices=false";
    private const string CompileClosureSha256 =
        "40f45634fc87d66d875d2859a5fe7ad6f5bc09d531af81d785ef2a497f5f6e33";
    private const string ProjectFileSha256 =
        "7f401ada2a652bd35827f87dc4d7eabbfa331164eab1e055c8782a338683f248";
    private const string CanonicalProjectShapeSha256 =
        "1e37233e70e923e8fb59716cc31fc26b7d748c4be26f5e5cf76fbd759b4a5cd9";

    [ModuleInitializer]
    internal static void RunAll()
    {
        Console.WriteLine("TEST_START " + TestName);
        int failures = 0;

        RunCase(
            "m02.009 compile closure flag dependency and tool propagation are exact",
            TestCompileClosureAndTooling,
            ref failures);
        RunCase(
            "m02.009 prepares two typed pages and cleans data before code",
            TestPreparationAndCleanup,
            ref failures);
        RunCase(
            "m02.009 starts after OS smoke and enters the terminal stub directly",
            TestTerminalEntryOrderAndMarkers,
            ref failures);
        RunCase(
            "m02.009 QEMU profile is cumulative terminal and fail closed",
            TestQemuProfileAndNegativeArguments,
            ref failures);
        RunCase(
            "m02.009 QEMU runner rejects hostile terminal transcripts",
            TestQemuRunnerTerminalTranscripts,
            ref failures);
        RunCase(
            "m02.009 documentation preserves the pre-exit and W-X evidence boundary",
            TestDocumentationBoundary,
            ref failures);

        if (failures != 0)
        {
            Console.WriteLine(
                "TEST_FAIL " + TestName + " host-contract failures=" + failures);
            throw new InvalidOperationException(
                "M02-009 EFI integration host tests failed: " + failures);
        }

        Console.WriteLine("TEST_PASS " + TestName);
    }

    private static void RunCase(
        string name,
        Func<bool> test,
        ref int failures)
    {
        Console.WriteLine("TEST_START " + name);
        try
        {
            if (test())
            {
                Console.WriteLine("TEST_PASS " + name);
                return;
            }

            failures++;
            Console.WriteLine("TEST_FAIL " + name);
        }
        catch (Exception exception)
        {
            failures++;
            Console.WriteLine(
                "TEST_FAIL " + name + " " + exception.GetType().Name + ": " +
                exception.Message);
        }
    }

    private static bool TestCompileClosureAndTooling()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string projectPath = Path.Combine(root, "src", "OS", "OS.csproj");
        XDocument project = XDocument.Load(projectPath);
        XElement[] compileItems = project.Descendants("Compile").ToArray();
        string[] compileKeys = compileItems
            .Select(item =>
                (item.Attribute("Include")?.Value ?? string.Empty) + "|" +
                (item.Attribute("Link")?.Value ?? string.Empty))
            .ToArray();
        string compileHash = Convert.ToHexString(
                SHA256.HashData(
                    Encoding.UTF8.GetBytes(string.Join("\n", compileKeys) + "\n")))
            .ToLowerInvariant();
        string projectHash = Convert.ToHexString(
                SHA256.HashData(File.ReadAllBytes(projectPath)))
            .ToLowerInvariant();

        const string GuardedIstKey =
            @"..\OS.Kernel\Diagnostics\GuardedISTStacksDoubleFaultPageFaultNMI.cs|Diagnostics\GuardedISTStacksDoubleFaultPageFaultNMI.cs";
        const string TerminalKey =
            @"..\OS.Kernel\Diagnostics\OwnedGDTTSSGeneratedAuditedCSharpStubBytes.cs|Diagnostics\OwnedGDTTSSGeneratedAuditedCSharpStubBytes.cs";
        const string EndianKey =
            @"..\..\std\no-runtime\shared\IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMOSA.cs|Std\IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMOSA.cs";
        int guardedIndex = Array.IndexOf(compileKeys, GuardedIstKey);
        int terminalIndex = Array.IndexOf(compileKeys, TerminalKey);
        int endianIndex = Array.IndexOf(compileKeys, EndianKey);

        XElement[] defaults = project
            .Descendants("EnableM02OwnedGdtTssTerminalProbe")
            .Where(element => element.Parent?.Name.LocalName == "PropertyGroup")
            .ToArray();
        XElement[] symbolGroups = project
            .Descendants("PropertyGroup")
            .Where(element =>
                element.Attribute("Condition")?.Value ==
                    "'$(EnableM02OwnedGdtTssTerminalProbe)' == 'true'")
            .ToArray();
        XElement[] dependencyErrors = project
            .Descendants("Error")
            .Where(element =>
                element.Attribute("Condition")?.Value ==
                    "'$(EnableM02OwnedGdtTssTerminalProbe)' == 'true' and " +
                    "'$(EnableM02GuardedIstStackAllocationProbe)' != 'true'")
            .ToArray();

        string buildScript = File.ReadAllText(
            Path.Combine(root, "scripts", "build-efi-shell.ps1"));
        string verifier = File.ReadAllText(
            Path.Combine(root, "scripts", "test-efi-artifact.ps1"));

        return compileItems.Length == 58
            && compileHash == CompileClosureSha256
            && projectHash == ProjectFileSha256
            && guardedIndex >= 0
            && terminalIndex == guardedIndex + 1
            && endianIndex == terminalIndex + 1
            && compileKeys.Count(key => key == TerminalKey) == 1
            && defaults.Length == 1
            && defaults[0].Value == "false"
            && defaults[0].Attribute("Condition")?.Value ==
                "'$(EnableM02OwnedGdtTssTerminalProbe)' == ''"
            && symbolGroups.Length == 1
            && symbolGroups[0].Elements("DefineConstants").Single().Value ==
                "$(DefineConstants);M02_OWNED_GDT_TSS_TERMINAL_PROBE"
            && dependencyErrors.Length == 1
            && dependencyErrors[0].Attribute("Text")?.Value ==
                "EnableM02OwnedGdtTssTerminalProbe requires " +
                "EnableM02GuardedIstStackAllocationProbe=true."
            && buildScript.Contains(
                "[switch] $EnableM02OwnedGdtTssTerminalProbe",
                StringComparison.Ordinal)
            && buildScript.Contains(TerminalKey, StringComparison.Ordinal)
            && buildScript.Contains(
                "-p:EnableM02OwnedGdtTssTerminalProbe=true",
                StringComparison.Ordinal)
            && buildScript.Contains(
                "EFI_PROBE M02_OWNED_GDT_TSS_TERMINAL",
                StringComparison.Ordinal)
            && buildScript.Contains(
                "owned_gdt_tss_terminal_requires_guarded_ist_stack_allocation",
                StringComparison.Ordinal)
            && buildScript.Contains(
                "EFI_PUBLISH_SOURCE_AUTHORITY status=ok inputs=71",
                StringComparison.Ordinal)
            && buildScript.Contains(
                CanonicalProjectShapeSha256,
                StringComparison.Ordinal)
            && verifier.Contains(
                "[switch]$EnableM02OwnedGdtTssTerminalProbe",
                StringComparison.Ordinal)
            && verifier.Contains(TerminalKey, StringComparison.Ordinal)
            && verifier.Contains(
                "$expectedSourceClosureInputCount = 71",
                StringComparison.Ordinal)
            && verifier.Contains(
                "$compileItems.Count -ne 58",
                StringComparison.Ordinal)
            && verifier.Contains(
                "-p:EnableM02OwnedGdtTssTerminalProbe=true",
                StringComparison.Ordinal);
    }

    private static bool TestPreparationAndCleanup()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(
            Path.Combine(root, "src", "OS", "Boot", "EfiEntry.cs"));
        string preparation = ExtractBlock(
            source,
            "private static bool PrepareM02OwnedGdtTssTerminalProbe(",
            "private static bool IsExactM02OwnedGdtTssTerminalPreparation(");
        string exactGate = ExtractBlock(
            source,
            "private static bool IsExactM02OwnedGdtTssTerminalPreparation(",
            "private static bool ValidateM02OwnedGdtTssTerminalResources()");
        string cleanup = ExtractBlock(
            source,
            "private static bool FailM02OwnedGdtTssTerminalBeforeEntry(",
            "private static void WriteM02OwnedGdtTssTerminalFirmwareLine(");
        string cleanupPlan = ExtractBlock(
            source,
            "private static byte GetM02OwnedGdtTssTerminalCleanupPlan(",
            "private static bool FailM02OwnedGdtTssTerminalBeforeEntry(");
        string efiMain = ExtractBlock(
            source,
            "private static long EfiMain(",
            "private static void Main()");

        int codeAllocation = preparation.IndexOf(
            "M02OwnedGdtTssTerminalEfiLoaderCode",
            StringComparison.Ordinal);
        int dataAllocation = preparation.IndexOf(
            "M02OwnedGdtTssTerminalEfiLoaderData",
            StringComparison.Ordinal);
        int zeroing = preparation.IndexOf(
            "codePage[offset] = 0;",
            StringComparison.Ordinal);
        int contextAtOffsetZero = preparation.IndexOf(
            "(OS.Kernel.Diagnostics.OwnedGdtTssTerminalContext*)dataAddress",
            StringComparison.Ordinal);
        int prepareContext = preparation.IndexOf(
            ".PrepareContext(",
            StringComparison.Ordinal);
        int emit = preparation.IndexOf(".Emit(", StringComparison.Ordinal);
        int validate = preparation.IndexOf(
            ".ValidateReadBack(",
            StringComparison.Ordinal);
        int saveCode = preparation.IndexOf(
            "s_m02OwnedGdtTssTerminalCodeAddress = codeAddress;",
            StringComparison.Ordinal);
        int saveData = preparation.IndexOf(
            "s_m02OwnedGdtTssTerminalDataAddress = dataAddress;",
            StringComparison.Ordinal);

        int resolveConsole = efiMain.IndexOf(
            "GetFirmwareConsole(systemTable)",
            StringComparison.Ordinal);
        int resolveSerial = efiMain.IndexOf(
            "GetFirmwareSerial(systemTable)",
            StringComparison.Ordinal);
        int prepareBeforeMarkers = efiMain.IndexOf(
            "PrepareM02OwnedGdtTssTerminalProbe(",
            StringComparison.Ordinal);
        int normalMarkers = efiMain.IndexOf(
            "WriteBootMarkers(",
            StringComparison.Ordinal);

        int dataCleanup = cleanup.IndexOf(
            "dataAddress,\n                    M02OwnedGdtTssTerminalPageCount",
            StringComparison.Ordinal);
        int codeCleanup = cleanup.IndexOf(
            "codeAddress,\n                    M02OwnedGdtTssTerminalPageCount",
            StringComparison.Ordinal);
        string distinctCodeCleanupGate =
            "if (codeAllocated &&\n" +
            "            (!dataAllocated || codeAddress != dataAddress))";
        string aliasedCodeCleanupMutation = cleanupPlan.Replace(
            distinctCodeCleanupGate,
            "if (codeAllocated)",
            StringComparison.Ordinal);
        int distinctCodeCleanup = cleanupPlan.IndexOf(
            distinctCodeCleanupGate,
            StringComparison.Ordinal);

        return source.Contains(
                "private const uint M02OwnedGdtTssTerminalEfiLoaderCode = 1;",
                StringComparison.Ordinal)
            && source.Contains(
                "private const uint M02OwnedGdtTssTerminalEfiLoaderData = 2;",
                StringComparison.Ordinal)
            && source.Contains(
                "private const ulong M02OwnedGdtTssTerminalPageSize = 4096;",
                StringComparison.Ordinal)
            && source.Contains(
                "private const ulong M02OwnedGdtTssTerminalPageCount = 1;",
                StringComparison.Ordinal)
            && CountOccurrences(
                preparation,
                "bootServices->AllocatePages(") == 2
            && codeAllocation >= 0
            && dataAllocation > codeAllocation
            && preparation.Contains(
                "M02OwnedGdtTssTerminalPagesOverlap(codeAddress, dataAddress)",
                StringComparison.Ordinal)
            && preparation.Contains(
                "IsValidM02OwnedGdtTssTerminalPage(codeAddress)",
                StringComparison.Ordinal)
            && preparation.Contains(
                "IsValidM02OwnedGdtTssTerminalPage(dataAddress)",
                StringComparison.Ordinal)
            && zeroing > dataAllocation
            && contextAtOffsetZero > zeroing
            && prepareContext > contextAtOffsetZero
            && emit > prepareContext
            && validate > emit
            && saveCode > validate
            && saveData > saveCode
            && preparation.Contains(
                ".StubByteCount) ||",
                StringComparison.Ordinal)
            && preparation.Contains(
                ".ContextByteCount))",
                StringComparison.Ordinal)
            && exactGate.Contains(".ContextByteCount == 168", StringComparison.Ordinal)
            && exactGate.Contains(".StubByteCount == 2987", StringComparison.Ordinal)
            && exactGate.Contains(
                ".StubFnv1A64 == 0xc9c0_4dee_967d_640aUL",
                StringComparison.Ordinal)
            && exactGate.Contains(
                "emitted.ContextFingerprint == prepared.ContextFingerprint",
                StringComparison.Ordinal)
            && exactGate.Contains(
                "validated.ContextFingerprint == prepared.ContextFingerprint",
                StringComparison.Ordinal)
            && ExecuteCleanupPlanHarness(cleanupPlan)
            && cleanupPlan.Contains("if (dataAllocated)", StringComparison.Ordinal)
            && cleanupPlan.Contains("plan |= 1;", StringComparison.Ordinal)
            && cleanupPlan.Contains("plan |= 2;", StringComparison.Ordinal)
            && cleanupPlan.Contains("return plan;", StringComparison.Ordinal)
            && CountOccurrences(cleanup, "bootServices->FreePages(") == 2
            && cleanupPlan.Contains(distinctCodeCleanupGate, StringComparison.Ordinal)
            && aliasedCodeCleanupMutation != cleanupPlan
            && !aliasedCodeCleanupMutation.Contains(
                distinctCodeCleanupGate,
                StringComparison.Ordinal)
            && distinctCodeCleanup >= 0
            && dataCleanup >= 0
            && codeCleanup > dataCleanup
            && cleanup.Contains("if ((cleanupPlan & 1) != 0)", StringComparison.Ordinal)
            && cleanup.Contains("if ((cleanupPlan & 2) != 0)", StringComparison.Ordinal)
            && cleanup.Contains(
                "if (bootServices != null && bootServices->FreePages != null)",
                StringComparison.Ordinal)
            && cleanup.Contains(
                "_ = bootServices->FreePages(",
                StringComparison.Ordinal)
            && cleanup.IndexOf(
                "s_m02OwnedGdtTssTerminalCodeAddress = 0;",
                StringComparison.Ordinal) > codeCleanup
            && cleanup.IndexOf(
                "s_m02OwnedGdtTssTerminalDataAddress = 0;",
                StringComparison.Ordinal) > codeCleanup
            && cleanup.IndexOf(
                "s_m02OwnedGdtTssTerminalState = M02OwnedGdtTssTerminalFailedState;",
                StringComparison.Ordinal) > codeCleanup
            && cleanup.Contains("return false;", StringComparison.Ordinal)
            && !cleanup.Contains("return true;", StringComparison.Ordinal)
            && cleanup.IndexOf(
                "M02OwnedGdtTssTerminalPreEntryFailureLine",
                StringComparison.Ordinal) > codeCleanup
            && resolveConsole >= 0
            && resolveSerial > resolveConsole
            && prepareBeforeMarkers > resolveSerial
            && normalMarkers > prepareBeforeMarkers;
    }

    private static bool TestTerminalEntryOrderAndMarkers()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(
            Path.Combine(root, "src", "OS", "Boot", "EfiEntry.cs"));
        string directWriter = ExtractBlock(
            source,
            "private static bool TryWriteM02OwnedGdtTssTerminalDirectLine(",
            "#endif\n\n        // NativeAOT emits GS-cookie");

        string terminalEntry = ExtractBlock(
            source,
            "private static void EnterM02OwnedGdtTssTerminalProbe(",
            "private static bool FailM02OwnedGdtTssTerminalBeforeEntry(");
        int plan = source.IndexOf(
            "EfiEntry.WriteBootSequencePlan(\n                    s_console,",
            StringComparison.Ordinal);
        int enter = IndexOfAfter(
            source,
            "EfiEntry.EnterM02OwnedGdtTssTerminalProbe(",
            plan);
        int validate = terminalEntry.IndexOf(
            "ValidateM02OwnedGdtTssTerminalResources()",
            StringComparison.Ordinal);
        int start = terminalEntry.IndexOf(
            ".TryWriteM02OwnedGdtTssTerminalStartLine()",
            StringComparison.Ordinal);
        int serialize = terminalEntry.IndexOf(
            "X64InstructionPublication.Serialize()",
            StringComparison.Ordinal);
        int functionPointer = terminalEntry.IndexOf(
            "delegate* unmanaged<",
            StringComparison.Ordinal);
        int invoke = terminalEntry.IndexOf(
            "terminalEntry(\n",
            StringComparison.Ordinal);
        int unexpectedReturn = terminalEntry.IndexOf(
            ".TryWriteM02OwnedGdtTssTerminalUnexpectedReturnLine()",
            StringComparison.Ordinal);
        int finalSpin = terminalEntry.LastIndexOf(
            "SpinM02OwnedGdtTssTerminalProbe();",
            StringComparison.Ordinal);
        string postInvocation = invoke >= 0
            ? terminalEntry[invoke..]
            : string.Empty;

        string startMarker = ExtractTerminalMarker(
            source,
            "M02OwnedGdtTssTerminalStartLine");
        string preEntryMarker = ExtractTerminalMarker(
            source,
            "M02OwnedGdtTssTerminalPreEntryFailureLine");
        string unexpectedMarker = ExtractTerminalMarker(
            source,
            "M02OwnedGdtTssTerminalUnexpectedReturnLine");

        return plan >= 0
            && enter > plan
            && validate >= 0
            && start > validate
            && serialize > start
            && functionPointer > serialize
            && invoke > functionPointer
            && unexpectedReturn > invoke
            && finalSpin > unexpectedReturn
            && terminalEntry.Contains(
                "OS.Kernel.Diagnostics.OwnedGdtTssTerminalContext*,\n            void>",
                StringComparison.Ordinal)
            && terminalEntry.Contains(
                "s_m02OwnedGdtTssTerminalDataAddress);",
                StringComparison.Ordinal)
            && directWriter.Contains(
                "s_m02CheckedPortIoBinding.Binding",
                StringComparison.Ordinal)
            && directWriter.Contains(
                "Com1OwnedPortIoBackend backend",
                StringComparison.Ordinal)
            && directWriter.Contains("poll < 128", StringComparison.Ordinal)
            && directWriter.Contains(".LineStatusRegisterPort", StringComparison.Ordinal)
            && directWriter.Contains(".DataRegisterPort", StringComparison.Ordinal)
            && directWriter.Contains("backend.ReadByte(", StringComparison.Ordinal)
            && directWriter.Contains("backend.WriteByte(", StringComparison.Ordinal)
            && !directWriter.Contains("Write(console", StringComparison.Ordinal)
            && !postInvocation.Contains("bootServices->", StringComparison.Ordinal)
            && !postInvocation.Contains("Write(console", StringComparison.Ordinal)
            && !postInvocation.Contains("WriteCrLf(", StringComparison.Ordinal)
            && startMarker == "TEST_START m02.009"
            && preEntryMarker == "M02_OWNED_GDT_TSS_PREENTRY_FAILED"
            && unexpectedMarker == "M02_OWNED_GDT_TSS_UNEXPECTED_RETURN";
    }

    private static bool TestQemuProfileAndNegativeArguments()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string runnerScriptPath = Path.Combine(
            root,
            "scripts",
            "run-qemu-smoke.ps1");
        string source = File.ReadAllText(runnerScriptPath);
        int terminalProfile = source.IndexOf(
            "if ($EnableM02OwnedGdtTssTerminalProbe) {",
            StringComparison.Ordinal);
        int guardedProfile = IndexOfAfter(
            source,
            "elseif ($EnableM02GuardedIstStackAllocationProbe) {",
            terminalProfile);
        string profile = terminalProfile >= 0 && guardedProfile > terminalProfile
            ? source[terminalProfile..guardedProfile]
            : string.Empty;
        string qemuRunner = File.ReadAllText(
            Path.Combine(root, "tools", "OS.QemuRunner", "Program.cs"));

        ProcessResult missingGuarded = RunPowerShell(
            runnerScriptPath,
            "-DryRun",
            "-EnableM02OwnedGdtTssTerminalProbe");
        ProcessResult missingBuild = RunPowerShell(
            runnerScriptPath,
            "-DryRun",
            "-EnableM02GuardedIstStackAllocationProbe",
            "-EnableM02OwnedGdtTssTerminalProbe");
        ProcessResult missingIntermediate = RunPowerShell(
            runnerScriptPath,
            "-DryRun",
            "-BuildEfi",
            "-EnableM02GuardedIstStackAllocationProbe",
            "-EnableM02OwnedGdtTssTerminalProbe");
        string[] cumulativeFlags =
        [
            "-DryRun",
            "-BuildEfi",
            "-EnableM02SafeStubProbe",
            "-EnableM02CheckedPortIoBindingProbe",
            "-EnableM02Com1OwnedPortIoProbe",
            "-EnableM02OwnedCom1ReadyProbe",
            "-EnableM02PanicSafeOwnedCom1Probe",
            "-EnableM02OwnedX64GdtEncodingProbe",
            "-EnableM02GuardedIstStackAllocationProbe",
            "-EnableM02OwnedGdtTssTerminalProbe"
        ];
        ProcessResult skipImage = RunPowerShell(
            runnerScriptPath,
            [.. cumulativeFlags, "-SkipImageBuild"]);
        ProcessResult rawLog = RunPowerShell(
            runnerScriptPath,
            [.. cumulativeFlags, "-M01RawObservationLog", "blocked.log"]);
        ProcessResult cumulativeProfile = RunPowerShell(
            runnerScriptPath,
            cumulativeFlags);
        string[] requiredMarkers = ExtractMarkerList(
            cumulativeProfile.Output,
            "REQUIRED_MARKERS ");
        string[] orderedMarkers = ExtractMarkerList(
            cumulativeProfile.Output,
            "ORDERED_MARKERS ");
        string[] uniqueMarkers = ExtractMarkerList(
            cumulativeProfile.Output,
            "UNIQUE_MARKERS ");
        string[] forbiddenMarkers = ExtractMarkerList(
            cumulativeProfile.Output,
            "FORBIDDEN_MARKERS ");

        return source.Contains(
                "[switch] $EnableM02OwnedGdtTssTerminalProbe",
                StringComparison.Ordinal)
            && source.Contains(ReadyMarker, StringComparison.Ordinal)
            && source.Contains("\"TEST_START m02.009\"", StringComparison.Ordinal)
            && source.Contains("\"TEST_PASS m02.009\"", StringComparison.Ordinal)
            && source.Contains(
                "\"M02_OWNED_GDT_TSS_PREENTRY_FAILED\"",
                StringComparison.Ordinal)
            && source.Contains(
                "\"M02_OWNED_GDT_TSS_VERIFY_FAILED\"",
                StringComparison.Ordinal)
            && source.Contains(
                "\"M02_OWNED_GDT_TSS_UNEXPECTED_RETURN\"",
                StringComparison.Ordinal)
            && source.Contains(
                "profileRequired=59 profileForbidden=43",
                StringComparison.Ordinal)
            && source.Contains(
                "rawObservationActive=false",
                StringComparison.Ordinal)
            && CountOccurrences(
                profile,
                "Merge-QemuSmokeMarkerList -Values @(") == 4
            && cumulativeProfile.ExitCode == 0
            && requiredMarkers.Length == 59
            && orderedMarkers.Length == 59
            && uniqueMarkers.Length == 59
            && forbiddenMarkers.Length == 43
            && requiredMarkers.Distinct(StringComparer.Ordinal).Count() == 59
            && orderedMarkers.Distinct(StringComparer.Ordinal).Count() == 59
            && uniqueMarkers.Distinct(StringComparer.Ordinal).Count() == 59
            && forbiddenMarkers.Distinct(StringComparer.Ordinal).Count() == 43
            && requiredMarkers.SequenceEqual(orderedMarkers)
            && requiredMarkers.SequenceEqual(uniqueMarkers)
            && source.Contains(
                "$EnableM01OwnedPagingProbe -and\n        " +
                "-not $EnableM02OwnedGdtTssTerminalProbe",
                StringComparison.Ordinal)
            && source.Contains(
                "m02_owned_gdt_tss_terminal_probe_forbids_m01_raw_observation_log",
                StringComparison.Ordinal)
            && qemuRunner.Contains(
                "\"QEMU_PROCESS_RESULT disposition=\" +",
                StringComparison.Ordinal)
            && qemuRunner.Contains(
                "case \"--replace-default-required-markers\":",
                StringComparison.Ordinal)
            && qemuRunner.Contains(
                "--replace-default-required-markers must precede marker arguments.",
                StringComparison.Ordinal)
            && source.Contains(
                "$arguments += \"--replace-default-required-markers\"",
                StringComparison.Ordinal)
            && qemuRunner.Contains(
                "? \"runner-terminated\"",
                StringComparison.Ordinal)
            && qemuRunner.Contains("qemu_reboot_or_shutdown", StringComparison.Ordinal)
            && missingGuarded.ExitCode == 1
            && missingGuarded.Output.Contains(
                "m02_owned_gdt_tss_terminal_probe_requires_guarded_ist_stack_allocation",
                StringComparison.Ordinal)
            && missingBuild.ExitCode == 1
            && missingBuild.Output.Contains(
                "m02_owned_gdt_tss_terminal_probe_requires_build_efi",
                StringComparison.Ordinal)
            && missingIntermediate.ExitCode == 1
            && missingIntermediate.Output.Contains(
                "m02_guarded_ist_stack_allocation_probe_requires_owned_x64_gdt_encoding",
                StringComparison.Ordinal)
            && skipImage.ExitCode == 1
            && skipImage.Output.Contains(
                "m02_owned_gdt_tss_terminal_probe_forbids_skip_image_build",
                StringComparison.Ordinal)
            && rawLog.ExitCode == 1
            && rawLog.Output.Contains(
                "m02_owned_gdt_tss_terminal_probe_forbids_m01_raw_observation_log",
                StringComparison.Ordinal);
    }

    private static bool TestQemuRunnerTerminalTranscripts()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string runnerScriptPath = Path.Combine(
            root,
            "scripts",
            "run-qemu-smoke.ps1");
        string[] cumulativeFlags =
        [
            "-DryRun",
            "-BuildEfi",
            "-EnableM02SafeStubProbe",
            "-EnableM02CheckedPortIoBindingProbe",
            "-EnableM02Com1OwnedPortIoProbe",
            "-EnableM02OwnedCom1ReadyProbe",
            "-EnableM02PanicSafeOwnedCom1Probe",
            "-EnableM02OwnedX64GdtEncodingProbe",
            "-EnableM02GuardedIstStackAllocationProbe",
            "-EnableM02OwnedGdtTssTerminalProbe"
        ];
        ProcessResult profile = RunPowerShell(runnerScriptPath, cumulativeFlags);
        string[] requiredMarkers = ExtractMarkerList(
            profile.Output,
            "REQUIRED_MARKERS ");
        string[] orderedMarkers = ExtractMarkerList(
            profile.Output,
            "ORDERED_MARKERS ");
        string[] uniqueMarkers = ExtractMarkerList(
            profile.Output,
            "UNIQUE_MARKERS ");
        string[] forbiddenMarkers = ExtractMarkerList(
            profile.Output,
            "FORBIDDEN_MARKERS ");
        if (profile.ExitCode != 0
            || requiredMarkers.Length != 59
            || orderedMarkers.Length != 59
            || uniqueMarkers.Length != 59
            || forbiddenMarkers.Length != 43)
        {
            return false;
        }

        string tempRoot = Path.Combine(
            Path.GetTempPath(),
            "clros-m02-009-runner-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            string invalidOptionImage = Path.Combine(
                tempRoot,
                "invalid-option-order.raw");
            File.WriteAllText(invalidOptionImage, string.Empty);
            ProcessResult lateReplacement = RunQemuRunner(
                "--dry-run",
                "--image",
                invalidOptionImage,
                "--require-marker",
                "OS_BOOT_OK",
                "--replace-default-required-markers");
            ProcessResult duplicateReplacement = RunQemuRunner(
                "--dry-run",
                "--image",
                invalidOptionImage,
                "--replace-default-required-markers",
                "--replace-default-required-markers");
            if (lateReplacement.ExitCode != 1
                || !lateReplacement.Output.Contains(
                    "--replace-default-required-markers must precede marker arguments.",
                    StringComparison.Ordinal)
                || duplicateReplacement.ExitCode != 1
                || !duplicateReplacement.Output.Contains(
                    "--replace-default-required-markers may be specified only once.",
                    StringComparison.Ordinal))
            {
                return false;
            }

            string persistentSuccess = CreateM02FakeQemuScript(
                tempRoot,
                requiredMarkers,
                persistent: true);
            ProcessResult success = RunM02TerminalQemu(
                tempRoot,
                "success",
                persistentSuccess,
                requiredMarkers,
                orderedMarkers,
                uniqueMarkers,
                forbiddenMarkers,
                timeoutSeconds: 5,
                out string successSummary);
            if (success.ExitCode != 0
                || !success.Output.Contains(
                    "SERIAL_MARKER_STREAM status=Passed terminal=Success",
                    StringComparison.Ordinal)
                || !success.Output.Contains(
                    "QEMU_PROCESS_RESULT disposition=runner-terminated",
                    StringComparison.Ordinal)
                || !success.Output.Contains(
                    "TEST_PASS qemu.smoke",
                    StringComparison.Ordinal)
                || !SummaryHasCleanSuccess(successSummary))
            {
                return false;
            }

            string spontaneousSuccess = CreateM02FakeQemuScript(
                tempRoot,
                requiredMarkers,
                persistent: false);
            ProcessResult spontaneous = RunM02TerminalQemu(
                tempRoot,
                "spontaneous-success",
                spontaneousSuccess,
                requiredMarkers,
                orderedMarkers,
                uniqueMarkers,
                forbiddenMarkers,
                timeoutSeconds: 5,
                out string spontaneousSummary);
            if (spontaneous.ExitCode != 18
                || !spontaneous.Output.Contains(
                    "TEST_FAIL qemu.smoke qemu_reboot_or_shutdown",
                    StringComparison.Ordinal)
                || !spontaneousSummary.Contains(
                    "FAIL_REASON qemu_reboot_or_shutdown",
                    StringComparison.Ordinal)
                || spontaneous.Output.Contains(
                    "TEST_PASS qemu.smoke",
                    StringComparison.Ordinal))
            {
                return false;
            }

            string[] terminalMarkers =
            [
                "TEST_START m02.009",
                ReadyMarker,
                "TEST_PASS m02.009"
            ];
            foreach (string missingMarker in terminalMarkers)
            {
                List<string> missingLines = [.. requiredMarkers];
                if (!missingLines.Remove(missingMarker))
                {
                    return false;
                }

                string missingQemu = CreateM02FakeQemuScript(
                    tempRoot,
                    missingLines,
                    persistent: false);
                ProcessResult missing = RunM02TerminalQemu(
                    tempRoot,
                    "missing-" + SanitizeFileName(missingMarker),
                    missingQemu,
                    requiredMarkers,
                    orderedMarkers,
                    uniqueMarkers,
                    forbiddenMarkers,
                    timeoutSeconds: 5,
                    out string missingSummary);
                if (missing.ExitCode != 5
                    || !missing.Output.Contains(
                        "TEST_FAIL qemu.smoke missing_marker",
                        StringComparison.Ordinal)
                    || !missingSummary.Contains(missingMarker, StringComparison.Ordinal)
                    || !missingSummary.Contains(
                        "FAIL_REASON missing_marker",
                        StringComparison.Ordinal)
                    || missing.Output.Contains(
                        "TEST_PASS qemu.smoke",
                        StringComparison.Ordinal))
                {
                    return false;
                }
            }

            foreach (string duplicateMarker in terminalMarkers)
            {
                List<string> duplicateLines = [.. requiredMarkers];
                int duplicateIndex = duplicateLines.IndexOf(duplicateMarker);
                duplicateLines.Insert(duplicateIndex + 1, duplicateMarker);
                string duplicateQemu = CreateM02FakeQemuScript(
                    tempRoot,
                    duplicateLines,
                    persistent: true);
                ProcessResult duplicate = RunM02TerminalQemu(
                    tempRoot,
                    "duplicate-" + SanitizeFileName(duplicateMarker),
                    duplicateQemu,
                    requiredMarkers,
                    orderedMarkers,
                    uniqueMarkers,
                    forbiddenMarkers,
                    timeoutSeconds: 5,
                    out string duplicateSummary);
                if (duplicate.ExitCode != 17
                    || !duplicate.Output.Contains(
                        "TEST_FAIL qemu.smoke duplicate_unique_marker",
                        StringComparison.Ordinal)
                    || !duplicateSummary.Contains(
                        "FAIL_REASON duplicate_unique_marker",
                        StringComparison.Ordinal)
                    || !duplicateSummary.Contains(
                        duplicateMarker,
                        StringComparison.Ordinal)
                    || duplicate.Output.Contains(
                        "TEST_PASS qemu.smoke",
                        StringComparison.Ordinal))
                {
                    return false;
                }
            }

            List<string> reorderedLines = [.. requiredMarkers];
            int readyIndex = reorderedLines.IndexOf(ReadyMarker);
            int passIndex = reorderedLines.IndexOf("TEST_PASS m02.009");
            (reorderedLines[readyIndex], reorderedLines[passIndex]) =
                (reorderedLines[passIndex], reorderedLines[readyIndex]);
            string reorderedQemu = CreateM02FakeQemuScript(
                tempRoot,
                reorderedLines,
                persistent: true);
            ProcessResult reordered = RunM02TerminalQemu(
                tempRoot,
                "reordered-ready-pass",
                reorderedQemu,
                requiredMarkers,
                orderedMarkers,
                uniqueMarkers,
                forbiddenMarkers,
                timeoutSeconds: 5,
                out string reorderedSummary);
            if (reordered.ExitCode != 10
                || !reordered.Output.Contains(
                    "TEST_FAIL qemu.smoke out_of_order_marker",
                    StringComparison.Ordinal)
                || !reorderedSummary.Contains(
                    "FAIL_REASON out_of_order_marker",
                    StringComparison.Ordinal)
                || reordered.Output.Contains(
                    "TEST_PASS qemu.smoke",
                    StringComparison.Ordinal))
            {
                return false;
            }

            string[] terminalFailures =
            [
                "M02_OWNED_GDT_TSS_PREENTRY_FAILED",
                "M02_OWNED_GDT_TSS_VERIFY_FAILED",
                "M02_OWNED_GDT_TSS_UNEXPECTED_RETURN"
            ];
            foreach (string failureMarker in terminalFailures)
            {
                List<string> failedLines = [.. requiredMarkers];
                int terminalStartIndex = failedLines.IndexOf("TEST_START m02.009");
                failedLines.Insert(terminalStartIndex + 1, failureMarker);
                string failedQemu = CreateM02FakeQemuScript(
                    tempRoot,
                    failedLines,
                    persistent: true);
                ProcessResult failed = RunM02TerminalQemu(
                    tempRoot,
                    "forbidden-" + SanitizeFileName(failureMarker),
                    failedQemu,
                    requiredMarkers,
                    orderedMarkers,
                    uniqueMarkers,
                    forbiddenMarkers,
                    timeoutSeconds: 5,
                    out string failedSummary);
                if (failed.ExitCode != 8
                    || !failed.Output.Contains(
                        "TEST_FAIL qemu.smoke forbidden_marker",
                        StringComparison.Ordinal)
                    || !failedSummary.Contains(failureMarker, StringComparison.Ordinal)
                    || !failedSummary.Contains(
                        "FAIL_REASON forbidden_marker",
                        StringComparison.Ordinal)
                    || failed.Output.Contains(
                        "TEST_PASS qemu.smoke",
                        StringComparison.Ordinal))
                {
                    return false;
                }
            }

            List<string> incompleteLines = [];
            foreach (string marker in requiredMarkers)
            {
                incompleteLines.Add(marker);
                if (marker == "TEST_START m02.009")
                {
                    break;
                }
            }

            string incompleteQemu = CreateM02FakeQemuScript(
                tempRoot,
                incompleteLines,
                persistent: true);
            ProcessResult incomplete = RunM02TerminalQemu(
                tempRoot,
                "incomplete-timeout",
                incompleteQemu,
                requiredMarkers,
                orderedMarkers,
                uniqueMarkers,
                forbiddenMarkers,
                timeoutSeconds: 1,
                out string incompleteSummary);
            return incomplete.ExitCode == 4
                && incomplete.Output.Contains(
                    "TEST_FAIL qemu.smoke timeout",
                    StringComparison.Ordinal)
                && incompleteSummary.Contains(
                    "FAIL_REASON timeout",
                    StringComparison.Ordinal)
                && incompleteSummary.Contains(ReadyMarker, StringComparison.Ordinal)
                && incompleteSummary.Contains(
                    "TEST_PASS m02.009",
                    StringComparison.Ordinal)
                && !incomplete.Output.Contains(
                    "TEST_PASS qemu.smoke",
                    StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static bool TestDocumentationBoundary()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string efiBuild = File.ReadAllText(
            Path.Combine(root, "docs", "efi-build.md"));
        string qemu = File.ReadAllText(
            Path.Combine(root, "docs", "qemu.md"));
        string readme = File.ReadAllText(Path.Combine(root, "README.md"));

        return efiBuild.Contains(
                "EnableM02OwnedGdtTssTerminalProbe",
                StringComparison.Ordinal)
            && efiBuild.Contains("EfiLoaderCode", StringComparison.Ordinal)
            && efiBuild.Contains("EfiLoaderData", StringComparison.Ordinal)
            && efiBuild.Contains("M02_OWNED_GDT_TSS_PREENTRY_FAILED", StringComparison.Ordinal)
            && efiBuild.Contains("pre-`ExitBootServices`", StringComparison.Ordinal)
            && efiBuild.Contains("does not prove W^X", StringComparison.OrdinalIgnoreCase)
            && qemu.Contains(ReadyMarker, StringComparison.Ordinal)
            && qemu.Contains("59 required", StringComparison.OrdinalIgnoreCase)
            && qemu.Contains("43 forbidden", StringComparison.OrdinalIgnoreCase)
            && qemu.Contains("runner-terminated", StringComparison.Ordinal)
            && qemu.Contains("raw-observation shim", StringComparison.OrdinalIgnoreCase)
            && readme.Contains("M02-009", StringComparison.Ordinal)
            && readme.Contains("real QEMU acceptance is pending", StringComparison.OrdinalIgnoreCase)
            && readme.Contains("pre-`ExitBootServices`", StringComparison.Ordinal);
    }

    private static bool ExecuteCleanupPlanHarness(string cleanupPlan)
    {
        if (string.IsNullOrWhiteSpace(cleanupPlan))
        {
            return false;
        }

        string tempRoot = Path.Combine(
            Path.GetTempPath(),
            "clros-m02-009-cleanup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        string projectPath = Path.Combine(tempRoot, "CleanupHarness.csproj");
        string programPath = Path.Combine(tempRoot, "Program.cs");
        File.WriteAllText(
            projectPath,
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>disable</ImplicitUsings>
                <Nullable>enable</Nullable>
              </PropertyGroup>
            </Project>
            """);
        string programSource =
            "using System;\nusing System.Collections.Generic;\n" +
            "internal static class Program\n{\n" +
            cleanupPlan +
            """

                private sealed class FakeBootServices
                {
                    private readonly Queue<(ulong Status, ulong Address)>
                        allocations;
                    private readonly Queue<ulong> cleanupStatuses;

                    internal FakeBootServices(
                        (ulong Status, ulong Address)[] allocations,
                        bool hasFreePages,
                        ulong[] cleanupStatuses)
                    {
                        this.allocations = new(allocations);
                        this.cleanupStatuses = new(cleanupStatuses);
                        HasFreePages = hasFreePages;
                    }

                    internal bool HasFreePages { get; }

                    internal List<ulong> FreeCalls { get; } = new();

                    internal ulong AllocatePages(out ulong address)
                    {
                        (ulong status, ulong allocatedAddress) =
                            allocations.Dequeue();
                        address = allocatedAddress;
                        return status;
                    }

                    internal ulong FreePages(ulong address)
                    {
                        FreeCalls.Add(address);
                        return cleanupStatuses.Count == 0
                            ? 0
                            : cleanupStatuses.Dequeue();
                    }
                }

                private static bool FailHarness(
                    FakeBootServices bootServices,
                    ulong codeAddress,
                    bool codeAllocated,
                    ulong dataAddress,
                    bool dataAllocated,
                    out byte cleanupPlan)
                {
                    cleanupPlan = GetM02OwnedGdtTssTerminalCleanupPlan(
                        codeAllocated,
                        codeAddress,
                        dataAllocated,
                        dataAddress);
                    if (bootServices.HasFreePages)
                    {
                        if ((cleanupPlan & 1) != 0)
                        {
                            _ = bootServices.FreePages(dataAddress);
                        }

                        if ((cleanupPlan & 2) != 0)
                        {
                            _ = bootServices.FreePages(codeAddress);
                        }
                    }

                    return false;
                }

                private static bool ExecuteRejectedPreparationCase(
                    (ulong Status, ulong Address)[] allocations,
                    bool rejectAfterDistinctAllocations,
                    bool hasFreePages,
                    ulong[] cleanupStatuses,
                    byte expectedPlan,
                    ulong[] expectedFreeCalls)
                {
                    FakeBootServices bootServices = new(
                        allocations,
                        hasFreePages,
                        cleanupStatuses);
                    bool codeAllocated = false;
                    bool dataAllocated = false;
                    ulong codeAddress = 0;
                    ulong dataAddress = 0;
                    ulong codeStatus = bootServices.AllocatePages(
                        out codeAddress);
                    bool result;
                    byte cleanupPlan;
                    if (codeStatus != 0 || codeAddress == 0)
                    {
                        result = FailHarness(
                            bootServices,
                            codeAddress,
                            codeAllocated,
                            dataAddress,
                            dataAllocated,
                            out cleanupPlan);
                    }
                    else
                    {
                        codeAllocated = true;
                        ulong dataStatus = bootServices.AllocatePages(
                            out dataAddress);
                        if (dataStatus != 0 || dataAddress == 0)
                        {
                            result = FailHarness(
                                bootServices,
                                codeAddress,
                                codeAllocated,
                                dataAddress,
                                dataAllocated,
                                out cleanupPlan);
                        }
                        else
                        {
                            dataAllocated = true;
                            if (dataAddress == codeAddress ||
                                rejectAfterDistinctAllocations)
                            {
                                result = FailHarness(
                                    bootServices,
                                    codeAddress,
                                    codeAllocated,
                                    dataAddress,
                                    dataAllocated,
                                    out cleanupPlan);
                            }
                            else
                            {
                                return false;
                            }
                        }
                    }

                    if (result || cleanupPlan != expectedPlan ||
                        bootServices.FreeCalls.Count != expectedFreeCalls.Length)
                    {
                        return false;
                    }

                    for (int index = 0;
                        index < bootServices.FreeCalls.Count;
                        index++)
                    {
                        if (bootServices.FreeCalls[index] !=
                            expectedFreeCalls[index])
                        {
                            return false;
                        }
                    }

                    return true;
                }

                private static int Main()
                {
                    bool passed =
                        // Code allocation failure frees nothing.
                        ExecuteRejectedPreparationCase(
                            [(7, 0)], false, true, [], 0, []) &&
                        // Data allocation failure frees code exactly once.
                        ExecuteRejectedPreparationCase(
                            [(0, 0x1000), (7, 0)], false, true, [], 2,
                            [0x1000]) &&
                        // Hostile aliasing is rejected and freed at most once.
                        ExecuteRejectedPreparationCase(
                            [(0, 0x1000), (0, 0x1000)], false, true, [], 1,
                            [0x1000]) &&
                        // Distinct post-allocation failure frees data then code.
                        ExecuteRejectedPreparationCase(
                            [(0, 0x1000), (0, 0x2000)], true, true, [], 3,
                            [0x2000, 0x1000]) &&
                        // Missing FreePages cannot produce success evidence.
                        ExecuteRejectedPreparationCase(
                            [(0, 0x1000), (0, 0x2000)], true, false, [], 3,
                            []) &&
                        // Non-success cleanup statuses preserve failure while
                        // retaining distinct exactly-once cleanup attempts.
                        ExecuteRejectedPreparationCase(
                            [(0, 0x1000), (0, 0x2000)], true, true,
                            [0x8000000000000007, 0x8000000000000007], 3,
                            [0x2000, 0x1000]);
                    Console.WriteLine(
                        passed
                            ? "CLEANUP_HARNESS_PASS"
                            : "CLEANUP_HARNESS_FAIL");
                    return passed ? 0 : 1;
                }
            }
            """;
        File.WriteAllText(programPath, programSource);

        try
        {
            ProcessStartInfo start = new("dotnet")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = tempRoot
            };
            start.ArgumentList.Add("run");
            start.ArgumentList.Add("--project");
            start.ArgumentList.Add(projectPath);
            start.ArgumentList.Add("--configuration");
            start.ArgumentList.Add("Release");
            start.ArgumentList.Add("--nologo");
            using Process process = Process.Start(start) ??
                throw new InvalidOperationException(
                    "Could not start the cleanup-plan harness.");
            Task<string> stdout = process.StandardOutput.ReadToEndAsync();
            Task<string> stderr = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(60_000))
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5_000);
                return false;
            }

            string output = stdout.GetAwaiter().GetResult() +
                stderr.GetAwaiter().GetResult();
            if (process.ExitCode != 0 ||
                !output.Contains("CLEANUP_HARNESS_PASS", StringComparison.Ordinal))
            {
                Console.WriteLine(
                    "M02_009_CLEANUP_HARNESS_DIAGNOSTIC " +
                    EscapeDiagnostic(output));
                return false;
            }

            return true;
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static bool SummaryHasCleanSuccess(string summary)
    {
        return summary.Contains("QEMU_SMOKE_STATUS pass", StringComparison.Ordinal)
            && summary.Contains("MISSING_MARKERS none", StringComparison.Ordinal)
            && summary.Contains("OUT_OF_ORDER_MARKERS none", StringComparison.Ordinal)
            && summary.Contains("DUPLICATE_UNIQUE_MARKERS none", StringComparison.Ordinal)
            && summary.Contains("FORBIDDEN_MARKER_HIT none", StringComparison.Ordinal)
            && summary.Contains("FAIL_REASON none", StringComparison.Ordinal);
    }

    private static string CreateM02FakeQemuScript(
        string tempRoot,
        IReadOnlyList<string> serialLines,
        bool persistent)
    {
        string path = Path.Combine(
            tempRoot,
            "fake-qemu-" + Guid.NewGuid().ToString("N") + ".cmd");
        List<string> script =
        [
            "@echo off",
            "set \"serial=\"",
            ":scan",
            "if \"%~1\"==\"\" goto write",
            "if not \"%~1\"==\"-serial\" goto next",
            "shift",
            "set \"serial=%~1\"",
            ":next",
            "shift",
            "goto scan",
            ":write",
            "if \"%serial:~0,5%\"==\"file:\" set \"serial=%serial:~5%\"",
            "if \"%serial%\"==\"\" exit /b 12"
        ];
        bool firstLine = true;
        foreach (string serialLine in serialLines)
        {
            script.Add(
                (firstLine ? ">" : ">>") +
                " \"%serial%\" echo " + serialLine);
            firstLine = false;
        }

        if (persistent)
        {
            script.Add(":hold");
            script.Add(
                "powershell.exe -NoProfile -Command \"Start-Sleep -Seconds 30\"");
            script.Add("goto hold");
        }
        else
        {
            script.Add("exit /b 0");
        }

        File.WriteAllLines(path, script);
        return path;
    }

    private static ProcessResult RunM02TerminalQemu(
        string tempRoot,
        string scenario,
        string qemuPath,
        IReadOnlyList<string> requiredMarkers,
        IReadOnlyList<string> orderedMarkers,
        IReadOnlyList<string> uniqueMarkers,
        IReadOnlyList<string> forbiddenMarkers,
        int timeoutSeconds,
        out string summary)
    {
        string suffix = SanitizeFileName(scenario) + "-" +
            Guid.NewGuid().ToString("N");
        string imagePath = Path.Combine(tempRoot, suffix + ".raw");
        string ovmfPath = Path.Combine(tempRoot, suffix + "-OVMF_CODE.fd");
        string serialLog = Path.Combine(tempRoot, suffix + "-serial.log");
        string summaryLog = Path.Combine(tempRoot, suffix + "-summary.log");
        File.WriteAllText(imagePath, string.Empty);
        File.WriteAllText(ovmfPath, string.Empty);
        List<string> arguments =
        [
            "--qemu",
            qemuPath,
            "--ovmf-code",
            ovmfPath,
            "--image",
            imagePath,
            "--serial-log",
            serialLog,
            "--summary-log",
            summaryLog,
            "--timeout",
            timeoutSeconds.ToString(),
            "--replace-default-required-markers"
        ];
        foreach (string marker in requiredMarkers)
        {
            arguments.Add("--require-marker");
            arguments.Add(marker);
        }
        foreach (string marker in orderedMarkers)
        {
            arguments.Add("--require-ordered-marker");
            arguments.Add(marker);
        }
        foreach (string marker in uniqueMarkers)
        {
            arguments.Add("--require-unique-marker");
            arguments.Add(marker);
        }
        foreach (string marker in forbiddenMarkers)
        {
            arguments.Add("--forbid-marker");
            arguments.Add(marker);
        }

        ProcessResult result = RunQemuRunner(arguments.ToArray());
        summary = File.Exists(summaryLog)
            ? File.ReadAllText(summaryLog)
            : string.Empty;
        return result;
    }

    private static ProcessResult RunQemuRunner(params string[] arguments)
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string toolPath = HostToolProcess.GetBuiltToolPath(root, "OS.QemuRunner");
        if (!File.Exists(toolPath))
        {
            return new ProcessResult(
                -1,
                "qemu runner tool not found: " + toolPath);
        }

        ProcessStartInfo start = HostToolProcess.CreateDotnetToolStartInfo(
            root,
            "OS.QemuRunner");
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(start) ??
            throw new InvalidOperationException("Could not start OS.QemuRunner.");
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30_000))
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit(5_000);
            throw new TimeoutException(
                "OS.QemuRunner did not exit within 30 seconds.");
        }

        return new ProcessResult(
            process.ExitCode,
            stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult());
    }

    private static string SanitizeFileName(string value)
    {
        char[] characters = value.ToCharArray();
        for (int index = 0; index < characters.Length; index++)
        {
            if (!char.IsLetterOrDigit(characters[index]))
            {
                characters[index] = '-';
            }
        }

        string sanitized = new string(characters).Trim('-');
        return sanitized.Length <= 48 ? sanitized : sanitized[..48];
    }

    private static string EscapeDiagnostic(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);
    }

    private static string ExtractBlock(
        string source,
        string startToken,
        string endToken)
    {
        int start = source.IndexOf(startToken, StringComparison.Ordinal);
        int end = start < 0
            ? -1
            : source.IndexOf(endToken, start + startToken.Length, StringComparison.Ordinal);
        return start >= 0 && end > start
            ? source[start..end]
            : string.Empty;
    }

    private static string ExtractTerminalMarker(
        string source,
        string lineKind)
    {
        string markerMethod = ExtractBlock(
            source,
            "private static bool TryGetM02OwnedGdtTssTerminalLineByte(",
            "private static void SpinM02OwnedGdtTssTerminalProbe()");
        string prefixBlock = ExtractBlock(
            markerMethod,
            "if (index >= 0 && index < 18)",
            "if (lineKind == M02OwnedGdtTssTerminalPreEntryFailureLine)");
        string prefix = ExtractCharacters(prefixBlock);

        if (lineKind == "M02OwnedGdtTssTerminalStartLine")
        {
            string startBlock = ExtractBlock(
                markerMethod,
                "if (lineKind == M02OwnedGdtTssTerminalStartLine)",
                "if (index >= 0 && index < 18)");
            return ExtractCharacters(startBlock);
        }

        string suffixEnd = lineKind ==
                "M02OwnedGdtTssTerminalPreEntryFailureLine"
            ? "if (lineKind == M02OwnedGdtTssTerminalUnexpectedReturnLine)"
            : "value = 0;";
        string suffixBlock = ExtractBlock(
            markerMethod,
            "if (lineKind == " + lineKind + ")",
            suffixEnd);
        return prefix + ExtractCharacters(suffixBlock);
    }

    private static string ExtractCharacters(string source)
    {
        MatchCollection characters = Regex.Matches(
            source,
            @"\(byte\)'(?<value>.)'",
            RegexOptions.CultureInvariant);
        return string.Concat(
            characters
                .Cast<Match>()
                .Select(match => match.Groups["value"].Value));
    }

    private static ProcessResult RunPowerShell(
        string scriptPath,
        params string[] arguments)
    {
        string systemPowerShell = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            "WindowsPowerShell",
            "v1.0",
            "powershell.exe");
        ProcessStartInfo start = new(
            File.Exists(systemPowerShell) ? systemPowerShell : "powershell.exe")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(scriptPath) ?? string.Empty
        };
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-NonInteractive");
        start.ArgumentList.Add("-ExecutionPolicy");
        start.ArgumentList.Add("Bypass");
        start.ArgumentList.Add("-File");
        start.ArgumentList.Add(scriptPath);
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(start) ??
            throw new InvalidOperationException("Could not start PowerShell.");
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30_000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException(
                "PowerShell runner negative-argument check timed out.");
        }

        string output = stdout.GetAwaiter().GetResult() +
            stderr.GetAwaiter().GetResult();
        return new ProcessResult(process.ExitCode, output);
    }

    private static int CountOccurrences(string source, string value)
    {
        int count = 0;
        int index = 0;
        while ((index = source.IndexOf(
                   value,
                   index,
                   StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    private static string[] ExtractMarkerList(string output, string prefix)
    {
        foreach (string rawLine in output.Split('\n'))
        {
            string line = rawLine.TrimEnd('\r');
            if (!line.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            string values = line[prefix.Length..];
            return string.Equals(values, "none", StringComparison.Ordinal)
                ? []
                : values.Split(
                    ';',
                    StringSplitOptions.RemoveEmptyEntries |
                    StringSplitOptions.TrimEntries);
        }

        return [];
    }

    private static int IndexOfAfter(
        string source,
        string value,
        int startIndex)
    {
        if (startIndex < 0 || startIndex > source.Length)
        {
            return -1;
        }

        return source.IndexOf(value, startIndex, StringComparison.Ordinal);
    }

    private readonly record struct ProcessResult(int ExitCode, string Output);
}
