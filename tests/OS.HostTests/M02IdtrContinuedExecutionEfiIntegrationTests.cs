using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using OS.Kernel.Diagnostics;
using OS.Tools.QemuRunner;

internal static unsafe class M02IdtrContinuedExecutionEfiIntegrationTests
{
    private const string TestName = "m02.012.efi";
    private const string HostReadyMarker =
        "CLROS_M02_012_EFI_HOST_READY switchDefault=false " +
        "sourceLinked=true codePages=2 dataPages=1 contextBytes=80 " +
        "cleanup=true markerOrder=true guestExecution=false";
    private const string StartMarker = "TEST_START m02.012";
    private const string ReadyMarker =
        "CLROS_M02_012_READY tableBytes=4096 idtrBytes=10 " +
        "idtrLimit=4095 policyFnv=8514F42EBDFC5D55 " +
        "firmwareGatesCopied=0 firmwareIdtrCaptured=true " +
        "ownedIdtrLoaded=true ownedIdtrReadBack=true " +
        "firmwareIdtrRestored=true restoreReadBack=true " +
        "ownedIdtrActive=false continuedExecution=true cliWindow=true " +
        "ifRestored=true instructionPublished=true " +
        "allocationsReleased=true wxEnforced=false " +
        "interruptTriggered=false faultTriggered=false " +
        "markerTransport=uefiSerial postExitBootServices=false";
    private const string PassMarker = "TEST_PASS m02.012";
    private const string FailureMarker =
        "M02_IDTR_CONTINUED_EXECUTION_FAILED";
    private const string FailureTestMarker =
        "TEST_FAIL m02.012 " +
        "idtr_load_readback_restore_return_or_cleanup";
    private const string HostOnlyReadyMarker =
        "CLROS_M02_012_HOST_READY contextBytes=80 stubBytes=52 " +
        "idtrBytes=10 tableBytes=4096 limit=4095 lidtPlan=2 " +
        "sidtPlan=3 cliPlan=1 stiPlan=1 " +
        "returnCookie=4F53494454523132 readBack=true " +
        "guestExecution=false idtrLoaded=false idtrRestored=false " +
        "continuedExecution=false faultTriggered=false " +
        "postExitBootServices=false";
    private const string StandaloneM02011ReadyMarker =
        "CLROS_M02_011_READY vectors=256 gates=256 gateBytes=16 " +
        "tableBytes=4096 selector=8 present=256 dpl0=256 " +
        "interruptGates=256 ownedHandlers=256 firmwareGatesCopied=0 " +
        "nmiIst=3 doubleFaultIst=1 pageFaultIst=2 " +
        "normalizedFnv1a64=8514F42EBDFC5D55 readBack=true " +
        "guestPopulation=true loaderData=true allocationPages=3 " +
        "allocationReleased=true stubExecution=false " +
        "idtrLoaded=false faultTriggered=false " +
        "markerTransport=uefiSerial postExitBootServices=false";
    private const string CompileClosureSha256 =
        "9f0806ad17e616f2ffb199d18b21a604d38f087bd5bfb37aa4ca84ffb91afc67";
    private const string SourceClosureSha256 =
        "c12f9888be8d5644dbe16a27fc744e226d3ec215c88f4282bb6a2357abc83d32";
    private const string FrozenProductionSha256 =
        "7CBF5DD9FB17D8D9608237FC78D06FE00B40D7AFE8669E1196C72B9F1B6848EB";
    private const string FrozenHostLeafSha256 =
        "E9F296DE66CB3FC5ED29C565DDBC99A596EC9D3188019F6E810795821C6A2CD1";
    private const ulong ValidCodeAddress = 0x0020_0000UL;
    private const ulong ValidDataAddress = 0x0030_0000UL;
    private const ulong ValidContextAddress = 0x0040_0000UL;

    [ModuleInitializer]
    internal static void RunAll()
    {
        Console.WriteLine("TEST_START " + TestName);
        int failures = 0;

        RunCase(
            "M02-012 project source authority and tooling propagation are exact",
            TestProjectAndToolingContract,
            ref failures);
        RunCase(
            "M02-012 default and enabled NativeAOT artifacts prove exact provenance",
            TestDefaultAndEnabledEfiArtifacts,
            ref failures);
        RunCase(
            "M02-012 EFI adapter ordering and fail-closed boundaries are exact",
            TestEfiEntryContract,
            ref failures);
        RunCase(
            "M02-012 fake firmware covers hostile allocation audit and cleanup paths",
            TestFakeFirmwareOrchestration,
            ref failures);
        RunCase(
            "M02-012 QEMU profile and hostile arguments remain exact",
            TestQemuProfilesAndHostileArguments,
            ref failures);
        RunCase(
            "M02-012 transcript rejects missing reordered duplicate and failure records",
            TestMarkerTranscriptRejection,
            ref failures);

        if (failures != 0)
        {
            Console.WriteLine(
                "TEST_FAIL " + TestName + " host-contract failures=" + failures);
            throw new InvalidOperationException(
                "M02-012 EFI integration host tests failed: " + failures);
        }

        Console.WriteLine(HostReadyMarker);
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
                "TEST_FAIL " + name + " " + exception.GetType().Name +
                ": " + exception.Message);
        }
    }

    private static bool TestProjectAndToolingContract()
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
                    Encoding.UTF8.GetBytes(
                        string.Join("\n", compileKeys) + "\n")))
            .ToLowerInvariant();

        const string NormalizedKey =
            @"..\OS.Kernel\Diagnostics\NormalizeErrorCodeNonErrorCodeInterruptFrame.cs|Diagnostics\NormalizeErrorCodeNonErrorCodeInterruptFrame.cs";
        const string IdtKey =
            @"..\OS.Kernel\Diagnostics\PopulateAllN256IDTVectorsOwnedGatesNever.cs|Diagnostics\PopulateAllN256IDTVectorsOwnedGatesNever.cs";
        const string IdtrKey =
            @"..\OS.Kernel\Diagnostics\IDTRContinuedExecution.cs|Diagnostics\IDTRContinuedExecution.cs";
        const string EndianKey =
            @"..\..\std\no-runtime\shared\IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMOSA.cs|Std\IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMOSA.cs";
        int normalizedIndex = Array.IndexOf(compileKeys, NormalizedKey);
        int idtIndex = Array.IndexOf(compileKeys, IdtKey);
        int idtrIndex = Array.IndexOf(compileKeys, IdtrKey);
        int endianIndex = Array.IndexOf(compileKeys, EndianKey);

        XElement[] defaults = project
            .Descendants("EnableM02IdtrContinuedExecutionProbe")
            .Where(element => element.Parent?.Name.LocalName == "PropertyGroup")
            .ToArray();
        XElement[] symbolGroups = project
            .Descendants("PropertyGroup")
            .Where(group => group.Attribute("Condition")?.Value ==
                "'$(EnableM02IdtrContinuedExecutionProbe)' == 'true'")
            .ToArray();
        string[] forbiddenSwitches =
        [
            "EnableM02OwnedGdtTssTerminalProbe",
            "EnableM02NormalizedInterruptFramePageProbe",
            "EnableM02OwnedIdtGatePopulationProbe"
        ];
        string[] arrayExclusions =
        [
            "EnableM04SzArrayAllocationProbe",
            "EnableM04SzArrayAllocationBeforeBindProbe",
            "EnableM04SzArrayAllocationExhaustionProbe"
        ];
        string[] stringExclusions =
        [
            "EnableM04StringAllocationProbe",
            "EnableM04StringAllocationBeforeBindProbe",
            "EnableM04StringAllocationExhaustionProbe",
            "EnableM04StringAllocationInvalidLengthProbe"
        ];
        XElement[] idtrExclusions = project
            .Descendants("Error")
            .Where(error =>
                (error.Attribute("Condition")?.Value ?? string.Empty).StartsWith(
                    "'$(EnableM02IdtrContinuedExecutionProbe)' == 'true'",
                    StringComparison.Ordinal))
            .ToArray();
        XElement[] ud2Exclusions = project
            .Descendants("Error")
            .Where(error => error.Attribute("Condition")?.Value ==
                "'$(EnableM02Ud2VectorStateProbe)' == 'true' and " +
                "'$(EnableM02IdtrContinuedExecutionProbe)' == 'true'")
            .ToArray();
        bool exclusionsExact = idtrExclusions.Length == forbiddenSwitches.Length + arrayExclusions.Length + stringExclusions.Length + 2 &&
            idtrExclusions.Count(error =>
                error.Attribute("Condition")?.Value == "'$(EnableM02IdtrContinuedExecutionProbe)' == 'true' and '$(EnableM03MemoryMapCaptureProbe)' == 'true'" &&
                error.Attribute("Text")?.Value == "m03_memory_map_capture_profile_conflict EnableM03MemoryMapCaptureProbe") == 1 &&
            idtrExclusions.Count(error =>
                error.Attribute("Condition")?.Value == "'$(EnableM02IdtrContinuedExecutionProbe)' == 'true' and '$(EnableM02UnexpectedVectorPanicProbe)' == 'true'" &&
                error.Attribute("Text")?.Value == "m02_unexpected_vector_panic_profile_invalid EnableM02UnexpectedVectorPanicProbe") == 1 &&
            forbiddenSwitches.All(forbidden =>
            idtrExclusions.Count(error =>
                error.Attribute("Condition")?.Value ==
                    "'$(EnableM02IdtrContinuedExecutionProbe)' == 'true' and " +
                    "'$({0})' == 'true'".Replace("{0}", forbidden,
                        StringComparison.Ordinal) &&
                error.Attribute("Text")?.Value ==
                    "EnableM02IdtrContinuedExecutionProbe cannot be " +
                    "combined with " + forbidden + "=true.") == 1) &&
            arrayExclusions.All(forbidden =>
                idtrExclusions.Count(error =>
                    error.Attribute("Condition")?.Value ==
                        "'$(EnableM02IdtrContinuedExecutionProbe)' == 'true' and " +
                        "'$(" + forbidden + ")' == 'true'" &&
                    error.Attribute("Text")?.Value ==
                        "m04_sz_array_profile_invalid " + forbidden) == 1) &&
            stringExclusions.All(forbidden =>
                idtrExclusions.Count(error =>
                    error.Attribute("Condition")?.Value ==
                        "'$(EnableM02IdtrContinuedExecutionProbe)' == 'true' and " +
                        "'$(" + forbidden + ")' == 'true'" &&
                    error.Attribute("Text")?.Value ==
                        "m04_string_profile_invalid " + forbidden) == 1);

        string productionPath = Path.Combine(
            root,
            "src",
            "OS.Kernel",
            "Diagnostics",
            "IDTRContinuedExecution.cs");
        string hostLeafPath = Path.Combine(
            root,
            "tests",
            "OS.HostTests",
            "IDTRContinuedExecutionTests.cs");
        string build = File.ReadAllText(
            Path.Combine(root, "scripts", "build-efi-shell.ps1"));
        string verifier = File.ReadAllText(
            Path.Combine(root, "scripts", "test-efi-artifact.ps1"));
        string qemu = File.ReadAllText(
            Path.Combine(root, "scripts", "run-qemu-smoke.ps1"));
        string probes = File.ReadAllText(Path.Combine(
            root,
            "src",
            "OS.Kernel",
            "Diagnostics",
            "Probes.cs"));
        string bootSequence = File.ReadAllText(Path.Combine(
            root,
            "src",
            "OS.Kernel",
            "Boot",
            "BootSequence.cs"));
        string[] buildCompileFirewall = ExtractPowerShellArray(
            build,
            "$script:ExpectedEfiCompileItems = @(");
        string[] verifierCompileFirewall = ExtractPowerShellArray(
            verifier,
            "$expectedEfiCompileItems = @(");

        return compileItems.Length == 88
            && compileHash == CompileClosureSha256
            && buildCompileFirewall.SequenceEqual(
                compileKeys,
                StringComparer.Ordinal)
            && verifierCompileFirewall.SequenceEqual(
                compileKeys,
                StringComparer.Ordinal)
            && normalizedIndex >= 0
            && idtIndex == normalizedIndex + 1
            && idtrIndex == idtIndex + 1
            && endianIndex == idtrIndex + 15
            && compileKeys.Count(key => key == IdtrKey) == 1
            && compileItems.All(item =>
                !(item.Attribute("Include")?.Value ?? string.Empty)
                    .Contains('*', StringComparison.Ordinal))
            && defaults.Length == 1
            && defaults[0].Value == "false"
            && defaults[0].Attribute("Condition")?.Value ==
                "'$(EnableM02IdtrContinuedExecutionProbe)' == ''"
            && symbolGroups.Length == 1
            && symbolGroups[0].Elements("DefineConstants").Single().Value ==
                "$(DefineConstants);M02_IDTR_CONTINUED_EXECUTION_PROBE"
            && exclusionsExact
            && ud2Exclusions.Length == 1
            && ud2Exclusions[0].Attribute("Text")?.Value ==
                "EnableM02Ud2VectorStateProbe cannot be combined with " +
                "EnableM02IdtrContinuedExecutionProbe=true."
            && Sha256File(productionPath) == FrozenProductionSha256
            && Sha256File(hostLeafPath) == FrozenHostLeafSha256
            && build.Contains(
                "[switch] $EnableM02IdtrContinuedExecutionProbe",
                StringComparison.Ordinal)
            && build.Contains(
                "-p:EnableM02IdtrContinuedExecutionProbe=true",
                StringComparison.Ordinal)
            && build.Contains(
                "EFI_PUBLISH_SOURCE_AUTHORITY status=ok inputs=102",
                StringComparison.Ordinal)
            && verifier.Contains(
                "[switch]$EnableM02IdtrContinuedExecutionProbe",
                StringComparison.Ordinal)
            && verifier.Contains(
                "$expectedSourceClosureInputCount = 102",
                StringComparison.Ordinal)
            && verifier.Contains(
                "$compileItems.Count -ne 88",
                StringComparison.Ordinal)
            && qemu.Contains(
                "[switch] $EnableM02IdtrContinuedExecutionProbe",
                StringComparison.Ordinal)
            && qemu.Contains(
                "m02_idtr_continued_execution_probe_requires_build_efi",
                StringComparison.Ordinal)
            && qemu.Contains(
                "m02_idtr_continued_execution_probe_forbids_skip_image_build",
                StringComparison.Ordinal)
            && qemu.Contains(
                "m02_idtr_continued_execution_marker_requires_typed_profile",
                StringComparison.Ordinal)
            && qemu.Contains(HostOnlyReadyMarker, StringComparison.Ordinal)
            && qemu.Contains(StandaloneM02011ReadyMarker, StringComparison.Ordinal)
            && !probes.Contains(
                "EnableM02IdtrContinuedExecutionProbe",
                StringComparison.Ordinal)
            && !probes.Contains(
                "M02_IDTR_CONTINUED_EXECUTION_PROBE",
                StringComparison.Ordinal)
            && !bootSequence.Contains(
                "EnableM02IdtrContinuedExecutionProbe",
                StringComparison.Ordinal)
            && !bootSequence.Contains(
                "M02_IDTR_CONTINUED_EXECUTION_PROBE",
                StringComparison.Ordinal)
            && TestBuildAndVerifierCollisionContracts(root);
    }

    private static bool TestBuildAndVerifierCollisionContracts(string root)
    {
        string buildScript = Path.Combine(
            root,
            "scripts",
            "build-efi-shell.ps1");
        string verifierScript = Path.Combine(
            root,
            "scripts",
            "test-efi-artifact.ps1");
        string collisionArtifact = Path.Combine(
            root,
            "artifacts",
            "efi",
            "BOOTX64-M02-012-COLLISION-" + Environment.ProcessId + ".EFI");
        string collisionLog = Path.Combine(
            root,
            "artifacts",
            "logs",
            "m02-012-collision-" + Environment.ProcessId + ".log");
        (string Switch, string Reason)[] collisions =
        [
            (
                "-EnableM02OwnedGdtTssTerminalProbe",
                "m02_idtr_continued_execution_probe_forbids_owned_gdt_tss_terminal"),
            (
                "-EnableM02NormalizedInterruptFramePageProbe",
                "m02_idtr_continued_execution_probe_forbids_normalized_interrupt_frame_page_probe"),
            (
                "-EnableM02OwnedIdtGatePopulationProbe",
                "m02_idtr_continued_execution_probe_forbids_owned_idt_gate_population_probe")
        ];

        foreach ((string collisionSwitch, string reason) in collisions)
        {
            ScriptResult buildCollision = RunPowerShellScript(
                root,
                buildScript,
                30_000,
                "-EnableM02IdtrContinuedExecutionProbe",
                collisionSwitch,
                "-EfiOutputPath",
                collisionArtifact,
                "-LogPath",
                collisionLog);
            ScriptResult verifierCollision = RunPowerShellScript(
                root,
                verifierScript,
                30_000,
                "-EnableM02IdtrContinuedExecutionProbe",
                collisionSwitch,
                "-EfiArtifact",
                collisionArtifact);
            if (!FailsWith(buildCollision, reason) ||
                !FailsWith(verifierCollision, reason))
            {
                return false;
            }
        }

        ScriptResult allowedM04Build = RunProcess(
            root,
            "dotnet",
            60_000,
            "build",
            Path.Combine(root, "src", "OS", "OS.csproj"),
            "--nologo",
            "-m:1",
            "-p:EnableM02IdtrContinuedExecutionProbe=true",
            "-p:EnableM04IndependentEndianConformanceProbe=true");
        return allowedM04Build.ExitCode == 0 &&
            allowedM04Build.Output.Contains(
                "0 Error(s)",
                StringComparison.Ordinal);
    }

    private static bool TestDefaultAndEnabledEfiArtifacts()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string script = Path.Combine(root, "scripts", "build-efi-shell.ps1");
        string artifactDirectory = Path.Combine(root, "artifacts", "efi");
        string logDirectory = Path.Combine(root, "artifacts", "logs");
        Directory.CreateDirectory(artifactDirectory);
        Directory.CreateDirectory(logDirectory);

        string runIdentity = Environment.ProcessId.ToString();
        string defaultArtifact = Path.Combine(
            artifactDirectory,
            "BOOTX64-M02-012-HOSTTEST-" + runIdentity + "-DEFAULT.EFI");
        string enabledArtifact = Path.Combine(
            artifactDirectory,
            "BOOTX64-M02-012-HOSTTEST-" + runIdentity + "-ENABLED.EFI");
        ScriptResult defaultBuild = RunPowerShellScript(
            root,
            script,
            600_000,
            "-EfiOutputPath",
            defaultArtifact,
            "-LogPath",
            Path.Combine(
                logDirectory,
                "m02-012-hosttest-" + runIdentity + "-default.log"));
        ScriptResult enabledBuild = RunPowerShellScript(
            root,
            script,
            600_000,
            "-EnableM02IdtrContinuedExecutionProbe",
            "-EfiOutputPath",
            enabledArtifact,
            "-LogPath",
            Path.Combine(
                logDirectory,
                "m02-012-hosttest-" + runIdentity + "-enabled.log"));

        if (defaultBuild.ExitCode != 0 || enabledBuild.ExitCode != 0)
        {
            Console.WriteLine(
                "M02_012_ARTIFACT_BUILD_AUDIT defaultExit=" +
                defaultBuild.ExitCode + " enabledExit=" +
                enabledBuild.ExitCode + " defaultOutput=" +
                TailForAudit(defaultBuild.Output) + " enabledOutput=" +
                TailForAudit(enabledBuild.Output));
            return false;
        }

        string defaultMap = File.ReadAllText(defaultArtifact + ".link.map");
        string enabledMap = File.ReadAllText(enabledArtifact + ".link.map");
        bool defaultExcluded =
            !defaultMap.Contains(
                "M02IdtrContinuedExecution",
                StringComparison.Ordinal) &&
            !defaultMap.Contains(
                "IDTRContinuedExecution",
                StringComparison.Ordinal);
        bool enabledIncluded =
            enabledMap.Contains(
                "WriteM02IdtrContinuedExecutionProbe",
                StringComparison.Ordinal) &&
            enabledMap.Contains(
                "M02IdtrContinuedExecutionSafetyRestored",
                StringComparison.Ordinal) &&
            enabledMap.Contains(
                "IDTRContinuedExecution__AuditExecution",
                StringComparison.Ordinal) &&
            enabledMap.Contains(
                "IDTRContinuedExecution__PrepareContext",
                StringComparison.Ordinal) &&
            enabledMap.Contains(
                "IDTRContinuedExecution__EmitStub",
                StringComparison.Ordinal) &&
            enabledMap.Contains(
                "IDTRContinuedExecution__ValidateStubReadBack",
                StringComparison.Ordinal);
        bool verifierPassed =
            defaultBuild.Output.Contains(
                "TEST_PASS efi.artifact.verify",
                StringComparison.Ordinal) &&
            enabledBuild.Output.Contains(
                "TEST_PASS efi.artifact.verify",
                StringComparison.Ordinal);
        bool provenanceExact =
            ValidateLinkEvidence(root, defaultArtifact, enabled: false) &&
            ValidateLinkEvidence(root, enabledArtifact, enabled: true);

        Console.WriteLine(
            "M02_012_ARTIFACT_BUILD_AUDIT defaultExit=0 enabledExit=0 " +
            "defaultExcluded=" + defaultExcluded.ToString().ToLowerInvariant() +
            " enabledIncluded=" + enabledIncluded.ToString().ToLowerInvariant() +
            " verifierPassed=" + verifierPassed.ToString().ToLowerInvariant() +
            " provenanceExact=" + provenanceExact.ToString().ToLowerInvariant());
        return defaultExcluded &&
            enabledIncluded &&
            verifierPassed &&
            provenanceExact;
    }

    private static bool TestEfiEntryContract()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(
            Path.Combine(root, "src", "OS", "Boot", "EfiEntry.cs"));
        string probe = ExtractMethod(
            source,
            "private static bool WriteM02IdtrContinuedExecutionProbe(");
        string safety = ExtractMethod(
            source,
            "private static bool M02IdtrContinuedExecutionSafetyRestored(");
        string normalizedResultMatches = ExtractMethod(
            source,
            "private static bool M02IdtrContinuedExecutionNormalizedResultMatches(");
        string idtResultMatches = ExtractMethod(
            source,
            "private static bool M02IdtrContinuedExecutionIdtResultMatches(");
        string planMatches = ExtractMethod(
            source,
            "private static bool M02IdtrContinuedExecutionPlanMatches(");
        string auditMatches = ExtractMethod(
            source,
            "private static bool M02IdtrContinuedExecutionAuditMatches(");
        string range = ExtractMethod(
            source,
            "private static bool IsValidM02IdtrContinuedExecutionRange(");
        string overlap = ExtractMethod(
            source,
            "private static bool M02IdtrContinuedExecutionRangesOverlap(");
        string cleanup = ExtractMethod(
            source,
            "private static bool FailM02IdtrContinuedExecutionBeforeInvocation(");
        string spin = ExtractMethod(
            source,
            "private static void SpinM02IdtrContinuedExecutionAfterUnverifiedRestore(");
        string start = ExtractAsciiMarker(
            source,
            "private static void WriteM02IdtrContinuedExecutionStartLine(");
        string ready = ExtractAsciiMarker(
            source,
            "private static void WriteM02IdtrContinuedExecutionSuccessLine(");
        string pass = ExtractAsciiMarker(
            source,
            "private static void WriteM02IdtrContinuedExecutionPassLine(");
        string[] failures = ExtractAsciiLines(
            source,
            "private static void WriteM02IdtrContinuedExecutionFailureLines(");

        int consoleReady = source.IndexOf(
            "WriteEfiConsoleReady(console, serial);",
            StringComparison.Ordinal);
        int m04 = source.IndexOf(
            "WriteM04SpanMemoryConformanceProbe(console, serial)",
            consoleReady,
            StringComparison.Ordinal);
        int invocation = source.IndexOf(
            "WriteM02IdtrContinuedExecutionProbe(",
            consoleReady,
            StringComparison.Ordinal);
        int memoryMap = source.IndexOf(
            "TryCaptureUefiMemoryMap(systemTable)",
            invocation,
            StringComparison.Ordinal);

        int firstAllocation = probe.IndexOf(
            "bootServices->AllocatePages(",
            StringComparison.Ordinal);
        int secondAllocation = probe.IndexOf(
            "bootServices->AllocatePages(",
            firstAllocation + 1,
            StringComparison.Ordinal);
        int firstMutation = probe.IndexOf(
            "code[offset] = 0;",
            StringComparison.Ordinal);
        int normalizedMaterialize = probe.IndexOf(
            ".NormalizeErrorCodeNonErrorCodeInterruptFrame\n                    .Materialize(",
            StringComparison.Ordinal);
        int normalizedReadBack = probe.IndexOf(
            ".NormalizeErrorCodeNonErrorCodeInterruptFrame\n                    .ValidateReadBack(",
            StringComparison.Ordinal);
        int idtMaterialize = probe.IndexOf(
            ".PopulateAllN256IDTVectorsOwnedGatesNever\n                .Materialize(",
            StringComparison.Ordinal);
        int idtReadBack = probe.IndexOf(
            ".PopulateAllN256IDTVectorsOwnedGatesNever\n                .ValidateReadBack(",
            StringComparison.Ordinal);
        int prepare = probe.IndexOf(".PrepareContext(", StringComparison.Ordinal);
        int emit = probe.IndexOf("IDTRContinuedExecution.EmitStub(", StringComparison.Ordinal);
        int validate = probe.IndexOf(".ValidateStubReadBack(", StringComparison.Ordinal);
        int serialize = probe.IndexOf(
            "X64InstructionPublication.Serialize()",
            StringComparison.Ordinal);
        int transitionCall = probe.IndexOf(
            "ulong executionCookie = transition(context);",
            StringComparison.Ordinal);
        int safetyCall = probe.IndexOf(
            "M02IdtrContinuedExecutionSafetyRestored(",
            transitionCall,
            StringComparison.Ordinal);
        int audit = probe.IndexOf(
            "IDTRContinuedExecution.AuditExecution(",
            safetyCall,
            StringComparison.Ordinal);
        int successDataFree = probe.LastIndexOf(
            "ulong dataReleaseStatus = bootServices->FreePages(",
            StringComparison.Ordinal);
        int successCodeFree = probe.LastIndexOf(
            "ulong codeReleaseStatus = bootServices->FreePages(",
            StringComparison.Ordinal);
        int readyCall = probe.IndexOf(
            "WriteM02IdtrContinuedExecutionSuccessLine(console, serial);",
            StringComparison.Ordinal);
        int passCall = probe.IndexOf(
            "WriteM02IdtrContinuedExecutionPassLine(console, serial);",
            StringComparison.Ordinal);
        string firstAllocationBlock = firstAllocation >= 0 &&
            secondAllocation > firstAllocation
                ? probe[firstAllocation..secondAllocation]
                : string.Empty;
        string secondAllocationBlock = secondAllocation >= 0 &&
            firstMutation > secondAllocation
                ? probe[secondAllocation..firstMutation]
                : string.Empty;
        int auditFailureStart = probe.IndexOf(
            "if (!M02IdtrContinuedExecutionAuditMatches(",
            StringComparison.Ordinal);
        string auditFailureReleaseBlock = auditFailureStart >= 0 &&
            successDataFree > auditFailureStart
                ? probe[auditFailureStart..successDataFree]
                : string.Empty;
        string successReleaseBlock = successDataFree >= 0 &&
            readyCall > successDataFree
                ? probe[successDataFree..readyCall]
                : string.Empty;
        int cleanupDataRelease = cleanup.IndexOf(
            "dataAddress,\n                    M02IdtrContinuedExecutionDataPages",
            StringComparison.Ordinal);
        int cleanupCodeRelease = cleanup.IndexOf(
            "codeAddress,\n                    M02IdtrContinuedExecutionCodePages",
            StringComparison.Ordinal);
        string postReturnPreSafety = transitionCall >= 0 && safetyCall > transitionCall
            ? probe[transitionCall..safetyCall]
            : string.Empty;
        int transitionStatementEnd = transitionCall >= 0
            ? probe.IndexOf(';', transitionCall)
            : -1;
        string postTransitionGuardPrefix = transitionStatementEnd >= 0 &&
            safetyCall > transitionStatementEnd
                ? Regex.Replace(
                    probe[(transitionStatementEnd + 1)..safetyCall],
                    @"\s+",
                    string.Empty)
                : string.Empty;

        return probe.Length != 0
            && start == StartMarker
            && ready == ReadyMarker
            && pass == PassMarker
            && failures.SequenceEqual(
                [FailureMarker, FailureTestMarker],
                StringComparer.Ordinal)
            && consoleReady >= 0
            && m04 > consoleReady
            && invocation > m04
            && memoryMap > invocation
            && CountOccurrences(probe, "bootServices->AllocatePages(") == 2
            && firstAllocation >= 0
            && secondAllocation > firstAllocation
            && firstAllocationBlock.Contains(
                "M02IdtrContinuedExecutionAllocateAnyPages,\n            M02IdtrContinuedExecutionEfiLoaderCode,\n            M02IdtrContinuedExecutionCodePages,\n            &codeAddress);",
                StringComparison.Ordinal)
            && !firstAllocationBlock.Contains(
                "M02IdtrContinuedExecutionEfiLoaderData",
                StringComparison.Ordinal)
            && !firstAllocationBlock.Contains("&dataAddress", StringComparison.Ordinal)
            && secondAllocationBlock.Contains(
                "M02IdtrContinuedExecutionAllocateAnyPages,\n            M02IdtrContinuedExecutionEfiLoaderData,\n            M02IdtrContinuedExecutionDataPages,\n            &dataAddress);",
                StringComparison.Ordinal)
            && !secondAllocationBlock.Contains(
                "M02IdtrContinuedExecutionEfiLoaderCode",
                StringComparison.Ordinal)
            && !secondAllocationBlock.Contains("&codeAddress", StringComparison.Ordinal)
            && firstMutation > secondAllocation
            && normalizedMaterialize > firstMutation
            && normalizedReadBack > normalizedMaterialize
            && idtMaterialize > normalizedReadBack
            && idtReadBack > idtMaterialize
            && prepare > idtReadBack
            && emit > prepare
            && validate > emit
            && serialize > validate
            && transitionCall > serialize
            && safetyCall > transitionCall
            && audit > safetyCall
            && successDataFree > audit
            && successCodeFree > successDataFree
            && readyCall > successCodeFree
            && passCall > readyCall
            && CountOccurrences(probe, "bootServices->FreePages(") == 4
            && CountOccurrences(
                auditFailureReleaseBlock,
                "bootServices->FreePages(") == 2
            && auditFailureReleaseBlock.IndexOf(
                "dataAddress,\n                M02IdtrContinuedExecutionDataPages",
                StringComparison.Ordinal) >= 0
            && auditFailureReleaseBlock.IndexOf(
                "dataAddress,\n                M02IdtrContinuedExecutionDataPages",
                StringComparison.Ordinal) <
                auditFailureReleaseBlock.IndexOf(
                    "codeAddress,\n                M02IdtrContinuedExecutionCodePages",
                    StringComparison.Ordinal)
            && CountOccurrences(
                successReleaseBlock,
                "bootServices->FreePages(") == 2
            && successReleaseBlock.Contains(
                "ulong dataReleaseStatus = bootServices->FreePages(\n            dataAddress,\n            M02IdtrContinuedExecutionDataPages);",
                StringComparison.Ordinal)
            && successReleaseBlock.Contains(
                "ulong codeReleaseStatus = bootServices->FreePages(\n            codeAddress,\n            M02IdtrContinuedExecutionCodePages);",
                StringComparison.Ordinal)
            && successReleaseBlock.Contains(
                "if (dataReleaseStatus != 0 || codeReleaseStatus != 0)",
                StringComparison.Ordinal)
            && !postReturnPreSafety.Contains("Write", StringComparison.Ordinal)
            && !postReturnPreSafety.Contains("bootServices->", StringComparison.Ordinal)
            && CountOccurrences(postReturnPreSafety, "(") == 2
            && postTransitionGuardPrefix == "if(!"
            && probe.Contains(
                "delegate* unmanaged<\n            OS.Kernel.Diagnostics.IDTRContinuedExecutionContext*,\n            ulong>",
                StringComparison.Ordinal)
            && probe.Contains("commonTarget[0] = 0x48;", StringComparison.Ordinal)
            && probe.Contains("commonTarget[1] = 0x83;", StringComparison.Ordinal)
            && probe.Contains("commonTarget[2] = 0xC4;", StringComparison.Ordinal)
            && probe.Contains("commonTarget[3] = 0x18;", StringComparison.Ordinal)
            && probe.Contains("commonTarget[4] = 0x48;", StringComparison.Ordinal)
            && probe.Contains("commonTarget[5] = 0xCF;", StringComparison.Ordinal)
            && probe.Contains(
                "M02IdtrContinuedExecutionTransitionOffset = 0x1040",
                StringComparison.Ordinal) == false
            && source.Contains(
                "M02IdtrContinuedExecutionTransitionOffset = 0x1040;",
                StringComparison.Ordinal)
            && source.Contains(
                "private const ulong M02IdtrContinuedExecutionCodePages = 2;",
                StringComparison.Ordinal)
            && source.Contains(
                "private const ulong M02IdtrContinuedExecutionDataPages = 1;",
                StringComparison.Ordinal)
            && source.Contains(
                "private const ulong M02IdtrContinuedExecutionContextWorkspaceBytes = 96;",
                StringComparison.Ordinal)
            && normalizedResultMatches.Contains(
                ".NormalizeInterruptFramePageStatus.Success",
                StringComparison.Ordinal)
            && normalizedResultMatches.Contains(
                "result.Fingerprint ==\n                M02IdtrContinuedExecutionNormalizedFnv1A64",
                StringComparison.Ordinal)
            && normalizedResultMatches.Contains(
                "result.ReadBackPerformed",
                StringComparison.Ordinal)
            && normalizedResultMatches.Contains("result.Succeeded", StringComparison.Ordinal)
            && idtResultMatches.Contains(
                ".OwnedIdtGateTableStatus.Success",
                StringComparison.Ordinal)
            && idtResultMatches.Contains(
                "result.NormalizedPolicyFingerprint ==\n                M02IdtrContinuedExecutionPolicyFnv1A64",
                StringComparison.Ordinal)
            && idtResultMatches.Contains("result.ReadBackPerformed", StringComparison.Ordinal)
            && idtResultMatches.Contains("result.Succeeded", StringComparison.Ordinal)
            && planMatches.Contains(
                ".IDTRContinuedExecutionStatus.Success",
                StringComparison.Ordinal)
            && planMatches.Contains(
                "result.ContextFingerprint != 0",
                StringComparison.Ordinal)
            && planMatches.Contains(
                "result.StubFingerprint == 0xAFF59D6480DE7F3CUL",
                StringComparison.Ordinal)
            && planMatches.Contains("result.Succeeded", StringComparison.Ordinal)
            && probe.Contains(
                "IDTRContinuedExecution.StubFnv1A64 !=\n                0xAFF59D6480DE7F3CUL",
                StringComparison.Ordinal)
            && probe.Contains(
                "IDTRContinuedExecution\n                .HasExternalRelocations",
                StringComparison.Ordinal)
            && auditMatches.Contains(
                ".IDTRContinuedExecutionStatus.Success",
                StringComparison.Ordinal)
            && auditMatches.Contains("audit.ExpectedCookie", StringComparison.Ordinal)
            && auditMatches.Contains("audit.ObservedCookie", StringComparison.Ordinal)
            && auditMatches.Contains("audit.TransitionObserved", StringComparison.Ordinal)
            && auditMatches.Contains("audit.OwnedIdtrLoaded", StringComparison.Ordinal)
            && auditMatches.Contains("audit.FirmwareIdtrRestored", StringComparison.Ordinal)
            && auditMatches.Contains("audit.InterruptFlagRestored", StringComparison.Ordinal)
            && auditMatches.Contains("audit.ContinuedExecution", StringComparison.Ordinal)
            && auditMatches.Contains("audit.Succeeded", StringComparison.Ordinal)
            && safety.Contains("LoadedIdtrOffset", StringComparison.Ordinal)
            && safety.Contains("loadedBase != ownedIdtAddress", StringComparison.Ordinal)
            && safety.Contains("PreviousIdtrOffset", StringComparison.Ordinal)
            && safety.Contains("RestoredIdtrOffset", StringComparison.Ordinal)
            && safety.Contains("previousBase == ownedIdtAddress", StringComparison.Ordinal)
            && safety.Contains("RflagsBeforeOffset", StringComparison.Ordinal)
            && safety.Contains("RflagsAfterOffset", StringComparison.Ordinal)
            && !safety.Contains("bootServices", StringComparison.Ordinal)
            && !safety.Contains("WriteM02", StringComparison.Ordinal)
            && range.Contains("address > ~0UL - length", StringComparison.Ordinal)
            && overlap.Contains(
                "secondAddress - firstAddress < firstLength",
                StringComparison.Ordinal)
            && overlap.Contains(
                "firstAddress - secondAddress < secondLength",
                StringComparison.Ordinal)
            && cleanup.Contains(
                "!M02IdtrContinuedExecutionRangesOverlap(",
                StringComparison.Ordinal)
            && CountOccurrences(cleanup, "bootServices->FreePages(") == 2
            && cleanupDataRelease >= 0
            && cleanupCodeRelease >= 0
            && cleanupDataRelease < cleanupCodeRelease
            && spin.Contains("while (true)", StringComparison.Ordinal)
            && !spin.Contains("Write", StringComparison.Ordinal)
            && !spin.Contains("FreePages", StringComparison.Ordinal);
    }

    private static bool TestFakeFirmwareOrchestration()
    {
        FakeTrace success = RunFakeProbe(new FakePlan());
        FakeTrace missingBootServices = RunFakeProbe(new FakePlan
        {
            BootServicesPresent = false
        });
        FakeTrace missingAllocate = RunFakeProbe(new FakePlan
        {
            AllocatePresent = false
        });
        FakeTrace missingFree = RunFakeProbe(new FakePlan
        {
            FreePresent = false
        });
        FakeTrace codeAllocationFailure = RunFakeProbe(new FakePlan
        {
            CodeAllocationStatus = 1
        });
        FakeTrace dataAllocationFailure = RunFakeProbe(new FakePlan
        {
            DataAllocationStatus = 1
        });

        FakeTrace[] invalidCodeRanges =
        [
            RunFakeProbe(new FakePlan { CodeAddress = 0 }),
            RunFakeProbe(new FakePlan { CodeAddress = ValidCodeAddress + 1 }),
            RunFakeProbe(new FakePlan { CodeAddress = 0x0000_8000_0000_0000UL }),
            RunFakeProbe(new FakePlan { CodeAddress = 0x0000_7FFF_FFFF_F000UL }),
            RunFakeProbe(new FakePlan { CodeAddress = ~0UL - 8192UL + 1UL })
        ];
        FakeTrace[] invalidDataRanges =
        [
            RunFakeProbe(new FakePlan { DataAddress = 0 }),
            RunFakeProbe(new FakePlan { DataAddress = ValidDataAddress + 1 }),
            RunFakeProbe(new FakePlan { DataAddress = 0x0000_8000_0000_0000UL }),
            RunFakeProbe(new FakePlan { DataAddress = ~0UL - 4096UL + 1UL })
        ];
        FakeTrace equalAlias = RunFakeProbe(new FakePlan
        {
            DataAddress = ValidCodeAddress
        });
        FakeTrace partialOverlap = RunFakeProbe(new FakePlan
        {
            DataAddress = ValidCodeAddress + 4096
        });

        FakeTrace normalizedMismatch = RunFakeProbe(new FakePlan
        {
            CorruptNormalizedPage = true
        });
        FakeTrace idtMismatch = RunFakeProbe(new FakePlan
        {
            CorruptIdt = true
        });
        FakeTrace stubMismatch = RunFakeProbe(new FakePlan
        {
            CorruptTransitionStub = true
        });
        FakeTrace publicationFailure = RunFakeProbe(new FakePlan
        {
            PublicationSucceeded = false
        });

        FakeTrace allZero = RunFakeProbe(new FakePlan
        {
            Observation = FakeObservation.AllZero
        });
        FakeTrace loadedMismatch = RunFakeProbe(new FakePlan
        {
            Observation = FakeObservation.LoadedMismatch
        });
        FakeTrace restoredMismatch = RunFakeProbe(new FakePlan
        {
            Observation = FakeObservation.RestoredMismatch
        });
        FakeTrace interruptFlagMismatch = RunFakeProbe(new FakePlan
        {
            Observation = FakeObservation.InterruptFlagMismatch
        });
        FakeTrace noTransition = RunFakeProbe(new FakePlan
        {
            Observation = FakeObservation.NoTransition
        });
        FakeTrace wrongCookie = RunFakeProbe(new FakePlan
        {
            Observation = FakeObservation.WrongCookie
        });
        FakeTrace invariantMismatch = RunFakeProbe(new FakePlan
        {
            Observation = FakeObservation.InvariantMismatch
        });
        FakeTrace dataReleaseFailure = RunFakeProbe(new FakePlan
        {
            DataReleaseStatus = 1
        });
        FakeTrace codeReleaseFailure = RunFakeProbe(new FakePlan
        {
            CodeReleaseStatus = 1
        });

        bool serviceFailuresExact =
            IsPreAllocationFailure(missingBootServices) &&
            IsPreAllocationFailure(missingAllocate) &&
            IsPreAllocationFailure(missingFree);
        bool invalidRangesExact = invalidCodeRanges.All(IsReturningFailure) &&
            invalidDataRanges.All(IsReturningFailure) &&
            invalidCodeRanges.All(trace => trace.AllocationCalls.Count == 1) &&
            invalidDataRanges.All(trace => trace.AllocationCalls.Count == 2) &&
            invalidCodeRanges.All(trace => trace.ReleaseCalls.Count == 1) &&
            invalidDataRanges.All(trace => trace.ReleaseCalls.Count == 2);
        bool aliasesExact =
            IsReturningFailure(equalAlias) &&
            IsReturningFailure(partialOverlap) &&
            equalAlias.ReleaseCalls.Count == 1 &&
            partialOverlap.ReleaseCalls.Count == 1 &&
            equalAlias.ReleaseCalls[0].Address == ValidCodeAddress &&
            equalAlias.ReleaseCalls[0].Pages == 1 &&
            partialOverlap.ReleaseCalls[0].Address == ValidCodeAddress + 4096 &&
            partialOverlap.ReleaseCalls[0].Pages == 1;
        bool preInvocationFailuresExact =
            IsReturningFailure(codeAllocationFailure) &&
            codeAllocationFailure.ReleaseCalls.Count == 0 &&
            IsReturningFailure(dataAllocationFailure) &&
            dataAllocationFailure.ReleaseCalls.Count == 1 &&
            dataAllocationFailure.ReleaseCalls[0] ==
                new FakeRelease(ValidCodeAddress, 2, 0) &&
            IsReturningFailure(normalizedMismatch) &&
            IsReturningFailure(idtMismatch) &&
            IsReturningFailure(stubMismatch) &&
            IsReturningFailure(publicationFailure) &&
            ReverseReleaseExact(normalizedMismatch) &&
            ReverseReleaseExact(idtMismatch) &&
            ReverseReleaseExact(stubMismatch) &&
            ReverseReleaseExact(publicationFailure);
        bool failClosedExact =
            IsFailClosed(allZero) &&
            IsFailClosed(loadedMismatch) &&
            IsFailClosed(restoredMismatch) &&
            IsFailClosed(interruptFlagMismatch) &&
            IsFailClosed(noTransition);
        bool auditedFailuresExact =
            IsReturningFailure(wrongCookie) &&
            IsReturningFailure(invariantMismatch) &&
            ReverseReleaseExact(wrongCookie) &&
            ReverseReleaseExact(invariantMismatch) &&
            wrongCookie.Events.IndexOf("safety-restored") <
                wrongCookie.Events.IndexOf("audit-failed") &&
            invariantMismatch.Events.IndexOf("safety-restored") <
                invariantMismatch.Events.IndexOf("audit-failed");
        bool releaseFailuresExact =
            IsReturningFailure(dataReleaseFailure) &&
            IsReturningFailure(codeReleaseFailure) &&
            ReverseReleaseExact(dataReleaseFailure) &&
            ReverseReleaseExact(codeReleaseFailure);

        return serviceFailuresExact
            && invalidRangesExact
            && aliasesExact
            && preInvocationFailuresExact
            && failClosedExact
            && auditedFailuresExact
            && releaseFailuresExact
            && success.Succeeded
            && success.ReadyEmitted
            && !success.FailureEmitted
            && !success.FailClosed
            && success.AllocationCalls.SequenceEqual(
                [
                    new FakeAllocation(
                        "LoaderCode",
                        ValidCodeAddress,
                        2,
                        0),
                    new FakeAllocation(
                        "LoaderData",
                        ValidDataAddress,
                        1,
                        0)
                ])
            && ReverseReleaseExact(success)
            && success.Events.IndexOf("validated-before-mutation") <
                success.Events.IndexOf("materialized")
            && success.Events.IndexOf("materialized") <
                success.Events.IndexOf("published")
            && success.Events.IndexOf("published") <
                success.Events.IndexOf("host-simulated-invocation")
            && success.Events.IndexOf("host-simulated-invocation") <
                success.Events.IndexOf("safety-restored")
            && success.Events.IndexOf("safety-restored") <
                success.Events.IndexOf("audited")
            && success.Events.IndexOf("audited") <
                success.Events.IndexOf("free-data")
            && success.Events.IndexOf("free-code") <
                success.Events.IndexOf("ready-pass");
    }

    private static FakeTrace RunFakeProbe(FakePlan plan)
    {
        FakeTrace trace = new();
        trace.Events.Add("start");
        if (!plan.BootServicesPresent ||
            !plan.AllocatePresent ||
            !plan.FreePresent)
        {
            trace.FailureEmitted = true;
            trace.Events.Add("failure");
            return trace;
        }

        byte* codeStorage = (byte*)NativeMemory.AlignedAlloc(8192, 4096);
        byte* dataStorage = (byte*)NativeMemory.AlignedAlloc(4096, 4096);
        byte* contextStorage = (byte*)NativeMemory.AlignedAlloc(96, 16);
        if (codeStorage == null || dataStorage == null || contextStorage == null)
        {
            if (codeStorage != null) NativeMemory.AlignedFree(codeStorage);
            if (dataStorage != null) NativeMemory.AlignedFree(dataStorage);
            if (contextStorage != null) NativeMemory.AlignedFree(contextStorage);
            throw new OutOfMemoryException("Could not allocate fake EFI storage.");
        }

        try
        {
            trace.AllocationCalls.Add(new FakeAllocation(
                "LoaderCode",
                plan.CodeAddress,
                2,
                plan.CodeAllocationStatus));
            trace.Events.Add("allocate-code");
            bool codeAllocated = plan.CodeAllocationStatus == 0;
            if (!codeAllocated || !IsValidFakeRange(plan.CodeAddress, 8192, 4096))
            {
                return FailFakeBeforeInvocation(
                    plan,
                    trace,
                    codeAllocated,
                    false);
            }

            trace.AllocationCalls.Add(new FakeAllocation(
                "LoaderData",
                plan.DataAddress,
                1,
                plan.DataAllocationStatus));
            trace.Events.Add("allocate-data");
            bool dataAllocated = plan.DataAllocationStatus == 0;
            if (!dataAllocated ||
                !IsValidFakeRange(plan.DataAddress, 4096, 4096) ||
                FakeRangesOverlap(
                    plan.CodeAddress,
                    8192,
                    plan.DataAddress,
                    4096))
            {
                return FailFakeBeforeInvocation(
                    plan,
                    trace,
                    codeAllocated,
                    dataAllocated);
            }

            trace.Events.Add("validated-before-mutation");
            Clear(codeStorage, 8192);
            Clear(dataStorage, 4096);
            Clear(contextStorage, 96);

            ulong commonTargetAddress = plan.CodeAddress + 4096;
            ulong transitionAddress = plan.CodeAddress + 0x1040;
            NormalizeInterruptFramePageResult normalizedMaterialized =
                NormalizeErrorCodeNonErrorCodeInterruptFrame.Materialize(
                    codeStorage,
                    4096,
                    plan.CodeAddress,
                    commonTargetAddress);
            byte* commonTarget = codeStorage + 4096;
            commonTarget[0] = 0x48;
            commonTarget[1] = 0x83;
            commonTarget[2] = 0xC4;
            commonTarget[3] = 0x18;
            commonTarget[4] = 0x48;
            commonTarget[5] = 0xCF;
            if (plan.CorruptNormalizedPage)
            {
                codeStorage[5] ^= 0x01;
            }
            NormalizeInterruptFramePageResult normalizedReadBack =
                NormalizeErrorCodeNonErrorCodeInterruptFrame.ValidateReadBack(
                    codeStorage,
                    4096,
                    plan.CodeAddress,
                    commonTargetAddress);

            OwnedIdtGateTableResult idtMaterialized =
                PopulateAllN256IDTVectorsOwnedGatesNever.Materialize(
                    dataStorage,
                    4096,
                    plan.DataAddress,
                    plan.CodeAddress);
            if (plan.CorruptIdt)
            {
                dataStorage[7] ^= 0x01;
            }
            OwnedIdtGateTableResult idtReadBack =
                PopulateAllN256IDTVectorsOwnedGatesNever.ValidateReadBack(
                    dataStorage,
                    4096,
                    plan.DataAddress,
                    plan.CodeAddress);

            IDTRContinuedExecutionContext* context =
                (IDTRContinuedExecutionContext*)contextStorage;
            IDTRContinuedExecutionPlanResult contextPrepared =
                IDTRContinuedExecution.PrepareContext(
                    context,
                    ValidContextAddress,
                    IDTRContinuedExecution.ContextByteCount,
                    plan.DataAddress);
            IDTRContinuedExecutionPlanResult stubEmitted =
                IDTRContinuedExecution.EmitStub(
                    codeStorage + 0x1040,
                    transitionAddress,
                    4096 - 0x40,
                    context,
                    ValidContextAddress,
                    IDTRContinuedExecution.ContextByteCount,
                    plan.DataAddress);
            if (plan.CorruptTransitionStub)
            {
                codeStorage[0x1040] ^= 0x01;
            }
            IDTRContinuedExecutionPlanResult stubReadBack =
                IDTRContinuedExecution.ValidateStubReadBack(
                    codeStorage + 0x1040,
                    transitionAddress,
                    4096 - 0x40,
                    context,
                    ValidContextAddress,
                    IDTRContinuedExecution.ContextByteCount,
                    plan.DataAddress);

            bool materialized =
                normalizedMaterialized.Succeeded &&
                normalizedReadBack.Succeeded &&
                normalizedMaterialized.Fingerprint ==
                    normalizedReadBack.Fingerprint &&
                idtMaterialized.Succeeded &&
                idtReadBack.Succeeded &&
                idtMaterialized.Fingerprint == idtReadBack.Fingerprint &&
                contextPrepared.Succeeded &&
                stubEmitted.Succeeded &&
                stubReadBack.Succeeded &&
                stubEmitted.StubFingerprint ==
                    IDTRContinuedExecution.StubFnv1A64 &&
                CommonTargetMatches(commonTarget);
            if (!materialized)
            {
                return FailFakeBeforeInvocation(plan, trace, true, true);
            }

            trace.Events.Add("materialized");
            if (!plan.PublicationSucceeded)
            {
                return FailFakeBeforeInvocation(plan, trace, true, true);
            }

            trace.Events.Add("published");
            ulong cookie = PopulateFakeObservation(
                (byte*)context,
                plan.DataAddress,
                plan.Observation);
            trace.Events.Add("host-simulated-invocation");

            if (!FakeSafetyRestored(context, plan.DataAddress))
            {
                trace.FailClosed = true;
                trace.Events.Add("fail-closed-no-callback");
                return trace;
            }

            trace.Events.Add("safety-restored");
            IDTRContinuedExecutionAuditResult auditResult =
                IDTRContinuedExecution.AuditExecution(
                    context,
                    ValidContextAddress,
                    IDTRContinuedExecution.ContextByteCount,
                    plan.DataAddress,
                    cookie);
            if (!auditResult.Succeeded)
            {
                trace.Events.Add("audit-failed");
                ReleaseFake(plan, trace, plan.DataAddress, 1, data: true);
                ReleaseFake(plan, trace, plan.CodeAddress, 2, data: false);
                trace.FailureEmitted = true;
                trace.Events.Add("failure");
                return trace;
            }

            trace.Events.Add("audited");
            ulong dataRelease = ReleaseFake(
                plan,
                trace,
                plan.DataAddress,
                1,
                data: true);
            ulong codeRelease = ReleaseFake(
                plan,
                trace,
                plan.CodeAddress,
                2,
                data: false);
            if (dataRelease != 0 || codeRelease != 0)
            {
                trace.FailureEmitted = true;
                trace.Events.Add("failure");
                return trace;
            }

            trace.Succeeded = true;
            trace.ReadyEmitted = true;
            trace.Events.Add("ready-pass");
            return trace;
        }
        finally
        {
            NativeMemory.AlignedFree(codeStorage);
            NativeMemory.AlignedFree(dataStorage);
            NativeMemory.AlignedFree(contextStorage);
        }
    }

    private static FakeTrace FailFakeBeforeInvocation(
        FakePlan plan,
        FakeTrace trace,
        bool codeAllocated,
        bool dataAllocated)
    {
        if (dataAllocated)
        {
            ReleaseFake(plan, trace, plan.DataAddress, 1, data: true);
        }
        if (codeAllocated &&
            (!dataAllocated ||
                !FakeRangesOverlap(
                    plan.CodeAddress,
                    8192,
                    plan.DataAddress,
                    4096)))
        {
            ReleaseFake(plan, trace, plan.CodeAddress, 2, data: false);
        }

        trace.FailureEmitted = true;
        trace.Events.Add("failure");
        return trace;
    }

    private static ulong ReleaseFake(
        FakePlan plan,
        FakeTrace trace,
        ulong address,
        ulong pages,
        bool data)
    {
        ulong status = data
            ? plan.DataReleaseStatus
            : plan.CodeReleaseStatus;
        trace.ReleaseCalls.Add(new FakeRelease(address, pages, status));
        trace.Events.Add(data ? "free-data" : "free-code");
        return status;
    }

    private static ulong PopulateFakeObservation(
        byte* context,
        ulong ownedIdtAddress,
        FakeObservation observation)
    {
        if (observation == FakeObservation.AllZero)
        {
            Clear(context, IDTRContinuedExecution.ContextByteCount);
            return IDTRContinuedExecution.ExecutionCookie;
        }

        const ushort FirmwareLimit = 0x03ff;
        const ulong FirmwareBase = 0x0000_0000_0010_0000UL;
        WriteIdtr(
            context + IDTRContinuedExecution.PreviousIdtrOffset,
            observation == FakeObservation.NoTransition
                ? IDTRContinuedExecution.IdtrLimit
                : FirmwareLimit,
            observation == FakeObservation.NoTransition
                ? ownedIdtAddress
                : FirmwareBase);
        WriteIdtr(
            context + IDTRContinuedExecution.LoadedIdtrOffset,
            observation == FakeObservation.LoadedMismatch
                ? FirmwareLimit
                : IDTRContinuedExecution.IdtrLimit,
            observation == FakeObservation.LoadedMismatch
                ? FirmwareBase
                : ownedIdtAddress);
        WriteIdtr(
            context + IDTRContinuedExecution.RestoredIdtrOffset,
            observation == FakeObservation.NoTransition
                ? IDTRContinuedExecution.IdtrLimit
                : FirmwareLimit,
            observation == FakeObservation.NoTransition
                ? ownedIdtAddress
                : observation == FakeObservation.RestoredMismatch
                    ? FirmwareBase + 0x1000
                    : FirmwareBase);
        WriteU64(
            context + IDTRContinuedExecution.RflagsBeforeOffset,
            0x202);
        WriteU64(
            context + IDTRContinuedExecution.RflagsAfterOffset,
            observation == FakeObservation.InterruptFlagMismatch
                ? 0x002UL
                : 0x202UL);
        if (observation == FakeObservation.InvariantMismatch)
        {
            context[10] = 1;
        }

        return observation == FakeObservation.WrongCookie
            ? IDTRContinuedExecution.ExecutionCookie ^ 1UL
            : IDTRContinuedExecution.ExecutionCookie;
    }

    private static bool FakeSafetyRestored(
        IDTRContinuedExecutionContext* context,
        ulong ownedIdtAddress)
    {
        byte* bytes = (byte*)context;
        ushort loadedLimit = ReadU16(
            bytes + IDTRContinuedExecution.LoadedIdtrOffset);
        ulong loadedBase = ReadU64(
            bytes + IDTRContinuedExecution.LoadedIdtrOffset + 2);
        if (loadedLimit != IDTRContinuedExecution.IdtrLimit ||
            loadedBase != ownedIdtAddress)
        {
            return false;
        }

        for (int offset = 0;
            offset < IDTRContinuedExecution.IdtrByteCount;
            offset++)
        {
            if (bytes[IDTRContinuedExecution.PreviousIdtrOffset + offset] !=
                bytes[IDTRContinuedExecution.RestoredIdtrOffset + offset])
            {
                return false;
            }
        }

        ushort previousLimit = ReadU16(
            bytes + IDTRContinuedExecution.PreviousIdtrOffset);
        ulong previousBase = ReadU64(
            bytes + IDTRContinuedExecution.PreviousIdtrOffset + 2);
        if (previousLimit == IDTRContinuedExecution.IdtrLimit &&
            previousBase == ownedIdtAddress)
        {
            return false;
        }

        ulong before = ReadU64(
            bytes + IDTRContinuedExecution.RflagsBeforeOffset);
        ulong after = ReadU64(
            bytes + IDTRContinuedExecution.RflagsAfterOffset);
        return ((before ^ after) &
            IDTRContinuedExecution.RflagsInterruptEnableBit) == 0;
    }

    private static bool IsValidFakeRange(
        ulong address,
        ulong length,
        ulong alignment)
    {
        if (address == 0 ||
            length == 0 ||
            alignment == 0 ||
            (address & (alignment - 1)) != 0 ||
            !IDTRContinuedExecution.IsCanonicalX64Address(address) ||
            address > ~0UL - length)
        {
            return false;
        }

        ulong endInclusive = address + length - 1;
        return IDTRContinuedExecution.IsCanonicalX64Address(endInclusive) &&
            ((address >> 47) & 1UL) == ((endInclusive >> 47) & 1UL);
    }

    private static bool FakeRangesOverlap(
        ulong firstAddress,
        ulong firstLength,
        ulong secondAddress,
        ulong secondLength)
    {
        return firstAddress <= secondAddress
            ? secondAddress - firstAddress < firstLength
            : firstAddress - secondAddress < secondLength;
    }

    private static bool CommonTargetMatches(byte* target)
    {
        return target[0] == 0x48 &&
            target[1] == 0x83 &&
            target[2] == 0xC4 &&
            target[3] == 0x18 &&
            target[4] == 0x48 &&
            target[5] == 0xCF;
    }

    private static void Clear(byte* destination, int length)
    {
        for (int index = 0; index < length; index++)
        {
            destination[index] = 0;
        }
    }

    private static void WriteIdtr(
        byte* destination,
        ushort limit,
        ulong address)
    {
        destination[0] = (byte)limit;
        destination[1] = (byte)(limit >> 8);
        WriteU64(destination + 2, address);
    }

    private static void WriteU64(byte* destination, ulong value)
    {
        for (int offset = 0; offset < 8; offset++)
        {
            destination[offset] = (byte)(value >> (offset * 8));
        }
    }

    private static ushort ReadU16(byte* source)
    {
        return (ushort)(source[0] | (source[1] << 8));
    }

    private static ulong ReadU64(byte* source)
    {
        ulong value = 0;
        for (int offset = 0; offset < 8; offset++)
        {
            value |= (ulong)source[offset] << (offset * 8);
        }
        return value;
    }

    private static bool IsPreAllocationFailure(FakeTrace trace)
    {
        return !trace.Succeeded &&
            trace.FailureEmitted &&
            !trace.FailClosed &&
            trace.AllocationCalls.Count == 0 &&
            trace.ReleaseCalls.Count == 0 &&
            !trace.Events.Contains("host-simulated-invocation");
    }

    private static bool IsReturningFailure(FakeTrace trace)
    {
        return !trace.Succeeded &&
            trace.FailureEmitted &&
            !trace.FailClosed &&
            !trace.ReadyEmitted;
    }

    private static bool IsFailClosed(FakeTrace trace)
    {
        return !trace.Succeeded &&
            trace.FailClosed &&
            !trace.FailureEmitted &&
            !trace.ReadyEmitted &&
            trace.ReleaseCalls.Count == 0 &&
            trace.Events.Last() == "fail-closed-no-callback";
    }

    private static bool ReverseReleaseExact(FakeTrace trace)
    {
        return trace.ReleaseCalls.SequenceEqual(
        [
            new FakeRelease(ValidDataAddress, 1,
                trace.ReleaseCalls.Count > 0
                    ? trace.ReleaseCalls[0].Status
                    : 0),
            new FakeRelease(ValidCodeAddress, 2,
                trace.ReleaseCalls.Count > 1
                    ? trace.ReleaseCalls[1].Status
                    : 0)
        ]);
    }

    private static bool TestQemuProfilesAndHostileArguments()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        ScriptResult defaultProfile = RunQemuDryRun(root);
        ScriptResult m02012 = RunQemuDryRun(
            root,
            "-BuildEfi",
            "-EnableM02IdtrContinuedExecutionProbe");
        ScriptResult missingBuild = RunQemuDryRun(
            root,
            "-EnableM02IdtrContinuedExecutionProbe");
        ScriptResult skipImage = RunQemuDryRun(
            root,
            "-BuildEfi",
            "-SkipImageBuild",
            "-EnableM02IdtrContinuedExecutionProbe");
        ScriptResult terminalCollision = RunQemuDryRun(
            root,
            "-BuildEfi",
            "-EnableM02IdtrContinuedExecutionProbe",
            "-EnableM02OwnedGdtTssTerminalProbe");
        ScriptResult normalizedCollision = RunQemuDryRun(
            root,
            "-BuildEfi",
            "-EnableM02IdtrContinuedExecutionProbe",
            "-EnableM02NormalizedInterruptFramePageProbe");
        ScriptResult idtCollision = RunQemuDryRun(
            root,
            "-BuildEfi",
            "-EnableM02IdtrContinuedExecutionProbe",
            "-EnableM02OwnedIdtGatePopulationProbe");
        ScriptResult markerWithoutProfile = RunQemuDryRun(
            root,
            "-RequireMarker",
            ReadyMarker);
        ScriptResult markerPrefixWithoutProfile = RunQemuDryRun(
            root,
            "-RequireMarker",
            "CLROS_M02_012_READY");
        ScriptResult packedMarkerWithoutProfile = RunQemuDryRun(
            root,
            "-RequireMarker",
            "  " + ReadyMarker + "  , OS_BOOT_OK");
        ScriptResult orderedMarkerWithoutProfile = RunQemuDryRun(
            root,
            "-RequireOrderedMarker",
            ReadyMarker);
        ScriptResult uniqueMarkerWithoutProfile = RunQemuDryRun(
            root,
            "-RequireUniqueMarker",
            ReadyMarker);
        ScriptResult startMarkerWithoutProfile = RunQemuDryRun(
            root,
            "-RequireMarker",
            StartMarker);
        ScriptResult passMarkerWithoutProfile = RunQemuDryRun(
            root,
            "-RequireMarker",
            PassMarker);
        ScriptResult typedExplicitReady = RunQemuDryRun(
            root,
            "-BuildEfi",
            "-EnableM02IdtrContinuedExecutionProbe",
            "-RequireMarker",
            ReadyMarker);
        ScriptResult combinedEndian = RunQemuDryRun(
            root,
            "-BuildEfi",
            "-EnableM02IdtrContinuedExecutionProbe",
            "-EnableM04IndependentEndianConformanceProbe");
        ScriptResult combinedSpan = RunQemuDryRun(
            root,
            "-BuildEfi",
            "-EnableM02IdtrContinuedExecutionProbe",
            "-EnableM04IndependentEndianConformanceProbe",
            "-EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe");

        string[] defaultRequired = MarkerList(
            defaultProfile.Output,
            "REQUIRED_MARKERS ");
        string[] required = MarkerList(m02012.Output, "REQUIRED_MARKERS ");
        string[] ordered = MarkerList(m02012.Output, "ORDERED_MARKERS ");
        string[] unique = MarkerList(m02012.Output, "UNIQUE_MARKERS ");
        string[] forbidden = MarkerList(m02012.Output, "FORBIDDEN_MARKERS ");
        string[] endianOrdered = MarkerList(
            combinedEndian.Output,
            "ORDERED_MARKERS ");
        string[] endianForbidden = MarkerList(
            combinedEndian.Output,
            "FORBIDDEN_MARKERS ");
        string[] spanOrdered = MarkerList(
            combinedSpan.Output,
            "ORDERED_MARKERS ");
        string[] spanForbidden = MarkerList(
            combinedSpan.Output,
            "FORBIDDEN_MARKERS ");
        string[] typedExplicitRequired = MarkerList(
            typedExplicitReady.Output,
            "REQUIRED_MARKERS ");
        string[] typedExplicitOrdered = MarkerList(
            typedExplicitReady.Output,
            "ORDERED_MARKERS ");
        string[] typedExplicitUnique = MarkerList(
            typedExplicitReady.Output,
            "UNIQUE_MARKERS ");
        string[] typedExplicitForbidden = MarkerList(
            typedExplicitReady.Output,
            "FORBIDDEN_MARKERS ");

        const string EndianReady =
            "CLROS_M04_002_READY readVectors=25 reverseVectors=7 " +
            "byteSeries=5 uint16Series=7 fingerprint=E81E4451E8D999F6 " +
            "referenceFilesRequired=false hostConformance=false " +
            "guestIntegrated=true gate4=Fail";
        const string SpanReady =
            "CLROS_M04_003_READY vectors=16 spanVectors=4 overlapVectors=5 " +
            "memoryCastVectors=3 unsafeWidthVectors=4 " +
            "fingerprint=F03FF59D36F39ECD pointerWidth=8 " +
            "referenceFilesRequired=false hostConformance=false " +
            "guestIntegrated=true gate4=Fail";

        return defaultProfile.ExitCode == 0
            && defaultRequired.SequenceEqual(
                ExpectedBaselineRequiredMarkers(),
                StringComparer.Ordinal)
            && !defaultProfile.Output.Contains(StartMarker, StringComparison.Ordinal)
            && m02012.ExitCode == 0
            && required.SequenceEqual(
                ExpectedM02012RequiredMarkers(),
                StringComparer.Ordinal)
            && ordered.SequenceEqual(
                ExpectedM02012OrderedMarkers(),
                StringComparer.Ordinal)
            && unique.SequenceEqual(
                [
                    StartMarker,
                    ReadyMarker,
                    PassMarker,
                    "OS_BOOT_OK",
                    "TEST_PASS boot.sequence",
                    "OS_SMOKE_PASS"
                ],
                StringComparer.Ordinal)
            && forbidden.SequenceEqual(
                [
                    "OS_SMOKE_FAIL",
                    "TEST_FAIL",
                    FailureMarker,
                    HostOnlyReadyMarker,
                    StandaloneM02011ReadyMarker
                ],
                StringComparer.Ordinal)
            && m02012.Output.Contains(
                "profileRequired=17 profileOrdered=17 profileUnique=6 " +
                "profileForbidden=5",
                StringComparison.Ordinal)
            && FailsWith(
                missingBuild,
                "m02_idtr_continued_execution_probe_requires_build_efi")
            && FailsWith(
                skipImage,
                "m02_idtr_continued_execution_probe_forbids_skip_image_build")
            && FailsWith(
                terminalCollision,
                "m02_idtr_continued_execution_probe_forbids_owned_gdt_tss_terminal")
            && FailsWith(
                normalizedCollision,
                "m02_idtr_continued_execution_probe_forbids_normalized_interrupt_frame_page_probe")
            && FailsWith(
                idtCollision,
                "m02_idtr_continued_execution_probe_forbids_owned_idt_gate_population_probe")
            && FailsWith(
                markerWithoutProfile,
                "m02_idtr_continued_execution_marker_requires_typed_profile")
            && FailsWith(
                markerPrefixWithoutProfile,
                "m02_idtr_continued_execution_marker_requires_typed_profile")
            && FailsWith(
                packedMarkerWithoutProfile,
                "m02_idtr_continued_execution_marker_requires_typed_profile")
            && FailsWith(
                orderedMarkerWithoutProfile,
                "m02_idtr_continued_execution_marker_requires_typed_profile")
            && FailsWith(
                uniqueMarkerWithoutProfile,
                "m02_idtr_continued_execution_marker_requires_typed_profile")
            && FailsWith(
                startMarkerWithoutProfile,
                "m02_idtr_continued_execution_marker_requires_typed_profile")
            && FailsWith(
                passMarkerWithoutProfile,
                "m02_idtr_continued_execution_marker_requires_typed_profile")
            && typedExplicitReady.ExitCode == 0
            && typedExplicitRequired.SequenceEqual(
                ExpectedM02012RequiredMarkers(),
                StringComparer.Ordinal)
            && typedExplicitOrdered.SequenceEqual(
                ExpectedM02012OrderedMarkers(),
                StringComparer.Ordinal)
            && typedExplicitUnique.SequenceEqual(unique, StringComparer.Ordinal)
            && typedExplicitForbidden.SequenceEqual(
                forbidden,
                StringComparer.Ordinal)
            && combinedEndian.ExitCode == 0
            && endianOrdered.SequenceEqual(
                [
                    "EFI_CONSOLE_READY",
                    EndianReady,
                    .. ExpectedM02012OrderedMarkers().Skip(1)
                ],
                StringComparer.Ordinal)
            && endianForbidden.Contains(
                "M04_ENDIAN_CONFORMANCE_FAILED",
                StringComparer.Ordinal)
            && endianForbidden.Contains(FailureMarker, StringComparer.Ordinal)
            && endianForbidden.Contains("TEST_FAIL", StringComparer.Ordinal)
            && combinedSpan.ExitCode == 0
            && spanOrdered.SequenceEqual(
                [
                    "EFI_CONSOLE_READY",
                    EndianReady,
                    SpanReady,
                    .. ExpectedM02012OrderedMarkers().Skip(1)
                ],
                StringComparer.Ordinal)
            && spanForbidden.Contains(
                "M04_ENDIAN_CONFORMANCE_FAILED",
                StringComparer.Ordinal)
            && spanForbidden.Contains(
                "M04_SPAN_MEMORY_CONFORMANCE_FAILED",
                StringComparer.Ordinal)
            && spanForbidden.Contains(FailureMarker, StringComparer.Ordinal)
            && spanForbidden.Contains("OS_SMOKE_FAIL", StringComparer.Ordinal)
            && spanForbidden.Contains("TEST_FAIL", StringComparer.Ordinal);
    }

    private static bool TestMarkerTranscriptRejection()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        ScriptResult profile = RunQemuDryRun(
            root,
            "-BuildEfi",
            "-EnableM02IdtrContinuedExecutionProbe");
        if (profile.ExitCode != 0)
        {
            return false;
        }

        string[] required = MarkerList(profile.Output, "REQUIRED_MARKERS ");
        string[] ordered = MarkerList(profile.Output, "ORDERED_MARKERS ");
        string[] unique = MarkerList(profile.Output, "UNIQUE_MARKERS ");
        string[] forbidden = MarkerList(profile.Output, "FORBIDDEN_MARKERS ");
        List<string> successLines = ordered.ToList();

        SerialMarkerCompletionResult success = ParseTranscript(
            required,
            forbidden,
            ordered,
            unique,
            successLines);
        SerialMarkerCompletionResult missing = ParseTranscript(
            required,
            forbidden,
            ordered,
            unique,
            successLines.Where(line => line != ReadyMarker));

        List<string> reorderedLines = successLines.ToList();
        int consoleIndex = reorderedLines.IndexOf("EFI_CONSOLE_READY");
        int startIndex = reorderedLines.IndexOf(StartMarker);
        (reorderedLines[consoleIndex], reorderedLines[startIndex]) =
            (reorderedLines[startIndex], reorderedLines[consoleIndex]);
        SerialMarkerCompletionResult reordered = ParseTranscript(
            required,
            forbidden,
            ordered,
            unique,
            reorderedLines);
        SerialMarkerCompletionResult duplicateStart = ParseTranscript(
            required,
            forbidden,
            ordered,
            unique,
            DuplicateMarker(successLines, StartMarker));
        SerialMarkerCompletionResult duplicateReady = ParseTranscript(
            required,
            forbidden,
            ordered,
            unique,
            DuplicateMarker(successLines, ReadyMarker));
        SerialMarkerCompletionResult duplicatePass = ParseTranscript(
            required,
            forbidden,
            ordered,
            unique,
            DuplicateMarker(successLines, PassMarker));
        SerialMarkerCompletionResult duplicateOsBoot = ParseTranscript(
            required,
            forbidden,
            ordered,
            unique,
            DuplicateMarker(successLines, "OS_BOOT_OK"));
        SerialMarkerCompletionResult duplicateBootPass = ParseTranscript(
            required,
            forbidden,
            ordered,
            unique,
            DuplicateMarker(successLines, "TEST_PASS boot.sequence"));
        SerialMarkerCompletionResult duplicateSmokePass = ParseTranscript(
            required,
            forbidden,
            ordered,
            unique,
            DuplicateMarker(successLines, "OS_SMOKE_PASS"));

        List<string> reorderedOrdinaryLines = successLines.ToList();
        int osBootIndex = reorderedOrdinaryLines.IndexOf("OS_BOOT_OK");
        int bootPassIndex = reorderedOrdinaryLines.IndexOf(
            "TEST_PASS boot.sequence");
        (reorderedOrdinaryLines[osBootIndex],
            reorderedOrdinaryLines[bootPassIndex]) =
            (reorderedOrdinaryLines[bootPassIndex],
                reorderedOrdinaryLines[osBootIndex]);
        SerialMarkerCompletionResult reorderedOrdinary = ParseTranscript(
            required,
            forbidden,
            ordered,
            unique,
            reorderedOrdinaryLines);

        List<string> nonExactLines = successLines.ToList();
        nonExactLines[nonExactLines.IndexOf(ReadyMarker)] = ReadyMarker.Replace(
            "wxEnforced=false",
            "wxEnforced=true",
            StringComparison.Ordinal);
        SerialMarkerCompletionResult nonExact = ParseTranscript(
            required,
            forbidden,
            ordered,
            unique,
            nonExactLines);
        SerialMarkerCompletionResult hostOnly = ParseReplacement(
            required,
            forbidden,
            ordered,
            unique,
            successLines,
            HostOnlyReadyMarker);
        SerialMarkerCompletionResult standalone = ParseReplacement(
            required,
            forbidden,
            ordered,
            unique,
            successLines,
            StandaloneM02011ReadyMarker);
        SerialMarkerCompletionResult failureSentinel = ParseInjected(
            required,
            forbidden,
            ordered,
            unique,
            successLines,
            FailureMarker);
        SerialMarkerCompletionResult failureTest = ParseInjected(
            required,
            forbidden,
            ordered,
            unique,
            successLines,
            FailureTestMarker);
        SerialMarkerCompletionResult smokeFailure = ParseInjected(
            required,
            forbidden,
            ordered,
            unique,
            successLines,
            "OS_SMOKE_FAIL");

        Console.WriteLine(
            "M02_012_TRANSCRIPT_AUDIT success=" + success.Status +
            " missing=" + missing.Status +
            " reordered=" + reordered.Status +
            " duplicateStart=" + duplicateStart.Status +
            " duplicateReady=" + duplicateReady.Status +
            " duplicatePass=" + duplicatePass.Status +
            " duplicateOsBoot=" + duplicateOsBoot.Status +
            " duplicateBootPass=" + duplicateBootPass.Status +
            " duplicateSmokePass=" + duplicateSmokePass.Status +
            " reorderedOrdinary=" + reorderedOrdinary.Status +
            " nonExact=" + nonExact.Status +
            " hostOnly=" + hostOnly.Status +
            " standalone=" + standalone.Status +
            " failureSentinel=" + failureSentinel.Status +
            " failureTest=" + failureTest.Status +
            " smokeFailure=" + smokeFailure.Status);

        return success.Passed
            && success.OrderedSeenCount == 17
            && success.UniqueSeenCount == 6
            && missing.Status == SerialMarkerCompletionStatus.OutOfOrderMarker
            && !missing.Passed
            && reordered.OutOfOrderOccurrenceCount != 0
            && !reordered.Passed
            && duplicateStart.DuplicateUniqueOccurrenceCount != 0
            && !duplicateStart.Passed
            && duplicateReady.DuplicateUniqueOccurrenceCount != 0
            && !duplicateReady.Passed
            && duplicatePass.DuplicateUniqueOccurrenceCount != 0
            && !duplicatePass.Passed
            && duplicateOsBoot.DuplicateUniqueOccurrenceCount != 0
            && !duplicateOsBoot.Passed
            && duplicateBootPass.DuplicateUniqueOccurrenceCount != 0
            && !duplicateBootPass.Passed
            && duplicateSmokePass.DuplicateUniqueOccurrenceCount != 0
            && !duplicateSmokePass.Passed
            && reorderedOrdinary.OutOfOrderOccurrenceCount != 0
            && !reorderedOrdinary.Passed
            && !nonExact.Passed
            && nonExact.Status == SerialMarkerCompletionStatus.OutOfOrderMarker
            && hostOnly.ForbiddenOccurrenceCount != 0
            && !hostOnly.Passed
            && standalone.ForbiddenOccurrenceCount != 0
            && !standalone.Passed
            && failureSentinel.ForbiddenOccurrenceCount != 0
            && !failureSentinel.Passed
            && failureTest.ForbiddenOccurrenceCount != 0
            && !failureTest.Passed
            && smokeFailure.ForbiddenOccurrenceCount != 0
            && !smokeFailure.Passed;
    }

    private static SerialMarkerCompletionResult ParseReplacement(
        IReadOnlyList<string> required,
        IReadOnlyList<string> forbidden,
        IReadOnlyList<string> ordered,
        IReadOnlyList<string> unique,
        IReadOnlyList<string> successLines,
        string replacement)
    {
        List<string> lines = successLines.ToList();
        lines[lines.IndexOf(ReadyMarker)] = replacement;
        return ParseTranscript(required, forbidden, ordered, unique, lines);
    }

    private static SerialMarkerCompletionResult ParseInjected(
        IReadOnlyList<string> required,
        IReadOnlyList<string> forbidden,
        IReadOnlyList<string> ordered,
        IReadOnlyList<string> unique,
        IReadOnlyList<string> successLines,
        string injected)
    {
        List<string> lines = successLines.ToList();
        lines.Insert(lines.Count - 1, injected);
        return ParseTranscript(required, forbidden, ordered, unique, lines);
    }

    private static List<string> DuplicateMarker(
        IReadOnlyList<string> lines,
        string marker)
    {
        List<string> duplicate = lines.ToList();
        int index = duplicate.IndexOf(marker);
        if (index < 0)
        {
            throw new InvalidOperationException(
                "Cannot duplicate missing marker: " + marker);
        }
        duplicate.Insert(index + 1, marker);
        return duplicate;
    }

    private static string[] ExpectedBaselineRequiredMarkers()
    {
        return
        [
            "OS_BOOT_OK",
            "TEST_PASS boot.sequence",
            "OS_SMOKE_PASS",
            "UEFI_MEMORY_MAP_OWNED",
            "BOOT_HANDOFF_READY",
            "UEFI_MEMORY_MAP_NORMALIZED",
            "BOOT_MEMORY_MAP_ACCEPTED",
            "BOOT_MEMORY_REGIONS_READY",
            "BOOT_MEMORY_REGION_COUNT",
            "BOOT_PAGE_ALLOCATOR_READY",
            "BOOT_ACPI_RSDP_HANDOFF_READY",
            "UEFI_ACPI_RSDP_READY"
        ];
    }

    private static string[] ExpectedM02012RequiredMarkers()
    {
        return
        [
            "OS_BOOT_OK",
            "TEST_PASS boot.sequence",
            "OS_SMOKE_PASS",
            "EFI_CONSOLE_READY",
            StartMarker,
            ReadyMarker,
            PassMarker,
            "UEFI_MEMORY_MAP_READY",
            "UEFI_MEMORY_MAP_OWNED",
            "BOOT_HANDOFF_READY",
            "UEFI_MEMORY_MAP_NORMALIZED",
            "BOOT_MEMORY_MAP_ACCEPTED",
            "BOOT_MEMORY_REGIONS_READY",
            "BOOT_MEMORY_REGION_COUNT",
            "BOOT_PAGE_ALLOCATOR_READY",
            "BOOT_ACPI_RSDP_HANDOFF_READY",
            "UEFI_ACPI_RSDP_READY"
        ];
    }

    private static string[] ExpectedM02012OrderedMarkers()
    {
        return
        [
            "EFI_CONSOLE_READY",
            StartMarker,
            ReadyMarker,
            PassMarker,
            "UEFI_MEMORY_MAP_READY",
            "UEFI_MEMORY_MAP_OWNED",
            "BOOT_HANDOFF_READY",
            "UEFI_MEMORY_MAP_NORMALIZED",
            "BOOT_MEMORY_MAP_ACCEPTED",
            "BOOT_MEMORY_REGIONS_READY",
            "BOOT_MEMORY_REGION_COUNT",
            "BOOT_PAGE_ALLOCATOR_READY",
            "BOOT_ACPI_RSDP_HANDOFF_READY",
            "UEFI_ACPI_RSDP_READY",
            "OS_BOOT_OK",
            "TEST_PASS boot.sequence",
            "OS_SMOKE_PASS"
        ];
    }

    private static SerialMarkerCompletionResult ParseTranscript(
        IReadOnlyList<string> required,
        IReadOnlyList<string> forbidden,
        IReadOnlyList<string> ordered,
        IReadOnlyList<string> unique,
        IEnumerable<string> lines)
    {
        SerialMarkerStateMachine parser = new(
            required,
            forbidden,
            orderedMarkers: ordered,
            uniqueMarkers: unique);
        parser.Feed(Encoding.ASCII.GetBytes(string.Join("\n", lines) + "\n"));
        return parser.Complete();
    }

    private static ScriptResult RunQemuDryRun(
        string root,
        params string[] arguments)
    {
        return RunPowerShellScript(
            root,
            Path.Combine(root, "scripts", "run-qemu-smoke.ps1"),
            30_000,
            ["-DryRun", .. arguments]);
    }

    private static ScriptResult RunPowerShellScript(
        string root,
        string script,
        int timeoutMilliseconds,
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
            WorkingDirectory = root
        };
        string windowsPowerShellModules = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            "WindowsPowerShell",
            "v1.0",
            "Modules");
        string inheritedModulePath =
            Environment.GetEnvironmentVariable("PSModulePath") ?? string.Empty;
        start.Environment["PSModulePath"] = inheritedModulePath.Length == 0
            ? windowsPowerShellModules
            : windowsPowerShellModules + Path.PathSeparator + inheritedModulePath;
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-NonInteractive");
        start.ArgumentList.Add("-ExecutionPolicy");
        start.ArgumentList.Add("Bypass");
        start.ArgumentList.Add("-File");
        start.ArgumentList.Add(script);
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(start) ??
            throw new InvalidOperationException("Could not start PowerShell.");
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(timeoutMilliseconds))
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
            throw new TimeoutException(
                "M02-012 PowerShell command timed out: " + script);
        }
        return new ScriptResult(
            process.ExitCode,
            stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult());
    }

    private static ScriptResult RunProcess(
        string root,
        string fileName,
        int timeoutMilliseconds,
        params string[] arguments)
    {
        ProcessStartInfo start = new(fileName)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = root
        };
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(start) ??
            throw new InvalidOperationException("Could not start process: " + fileName);
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(timeoutMilliseconds))
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
            throw new TimeoutException(
                "M02-012 process timed out: " + fileName);
        }

        return new ScriptResult(
            process.ExitCode,
            stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult());
    }

    private static bool ValidateLinkEvidence(
        string root,
        string artifactPath,
        bool enabled)
    {
        using JsonDocument document = JsonDocument.Parse(
            File.ReadAllText(artifactPath + ".link.json"));
        JsonElement evidence = document.RootElement;
        JsonElement evaluation = evidence.GetProperty("postRestoreEvaluation");
        JsonElement properties = evaluation.GetProperty("properties");
        JsonElement[] inputElements = evidence
            .GetProperty("sourceClosureInputs")
            .EnumerateArray()
            .ToArray();
        string[] actualPaths = inputElements
            .Select(input => input.GetProperty("path").GetString() ?? string.Empty)
            .ToArray();
        string[] expectedPaths = ExpectedSourceAuthorityPaths(root);
        bool inputHashesExact = inputElements.All(input =>
        {
            string relativePath =
                input.GetProperty("path").GetString() ?? string.Empty;
            string evidenceSha =
                input.GetProperty("sha256").GetString() ?? string.Empty;
            return relativePath.Length != 0 &&
                evidenceSha == Sha256File(
                        Path.Combine(root, relativePath.Replace('/', '\\')))
                    .ToLowerInvariant();
        });
        FileInfo artifact = new(artifactPath);

        return evidence.GetProperty("schemaVersion").GetInt32() == 3
            && evidence.GetProperty("configuration").GetString() == "Release"
            && evidence.GetProperty("runtimeIdentifier").GetString() == "win-x64"
            && evidence.GetProperty("artifactBytes").GetInt64() == artifact.Length
            && evidence.GetProperty("artifactSha256").GetString() ==
                Sha256File(artifactPath).ToLowerInvariant()
            && evidence.GetProperty("sourceClosureSha256").GetString() ==
                SourceClosureSha256
            && evidence.GetProperty("sourceProjectSha256").GetString() ==
                Sha256File(Path.Combine(root, "src", "OS", "OS.csproj"))
                    .ToLowerInvariant()
            && evidence.GetProperty("entrySourceSha256").GetString() ==
                Sha256File(Path.Combine(root, "src", "OS", "Boot", "EfiEntry.cs"))
                    .ToLowerInvariant()
            && evaluation.GetProperty("compileCount").GetInt32() == 88
            && evaluation.GetProperty("compileSha256").GetString() ==
                CompileClosureSha256
            && evaluation.GetProperty("projectReferenceCount").GetInt32() == 0
            && evaluation.GetProperty("referenceCount").GetInt32() == 0
            && properties.GetProperty("TargetFramework").GetString() == "net10.0"
            && properties.GetProperty("RuntimeIdentifier").GetString() == "win-x64"
            && properties.GetProperty("PublishAot").GetString() == "true"
            && properties.GetProperty("SelfContained").GetString() == "true"
            && properties.GetProperty("DisableFrameworkLibGeneration").GetString() ==
                "true"
            && properties.GetProperty(
                    "EnableM02IdtrContinuedExecutionProbe")
                .GetString() == (enabled ? "true" : "false")
            && properties.GetProperty(
                    "EnableM02NormalizedInterruptFramePageProbe")
                .GetString() == "false"
            && properties.GetProperty(
                    "EnableM02OwnedIdtGatePopulationProbe")
                .GetString() == "false"
            && properties.GetProperty("EnableM02OwnedGdtTssTerminalProbe")
                .GetString() == "false"
            && properties.GetProperty("EnableM04IndependentEndianConformanceProbe")
                .GetString() == "false"
            && properties.GetProperty(
                    "EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe")
                .GetString() == "false"
            && inputElements.Length == 102
            && actualPaths.SequenceEqual(expectedPaths, StringComparer.Ordinal)
            && inputHashesExact;
    }

    private static string[] ExpectedSourceAuthorityPaths(string root)
    {
        string projectPath = Path.Combine(root, "src", "OS", "OS.csproj");
        string projectDirectory = Path.GetDirectoryName(projectPath) ??
            throw new InvalidOperationException("OS project directory is missing.");
        XDocument project = XDocument.Load(projectPath);
        List<string> paths =
        [
            "CLROS.slnx",
            "Directory.Build.props",
            "Directory.Build.targets",
            "global.json",
            "scripts/build-efi-shell.ps1",
            "scripts/test-efi-artifact.ps1",
            "src/OS/OS.csproj",
            "tools/Directory.Build.props",
            "tools/OS.X64TextSerializerGenerator/Amd64CoffWriter.cs",
            "tools/OS.X64TextSerializerGenerator/CheckedReferenceBarrierThunkGenerator.cs",
            "tools/OS.X64TextSerializerGenerator/InstructionPublicationSerializerGenerator.cs",
            "tools/OS.X64TextSerializerGenerator/OS.X64TextSerializerGenerator.csproj",
            "tools/OS.X64TextSerializerGenerator/OS.X64TextSerializerGenerator.targets",
            "tools/OS.X64TextSerializerGenerator/Program.cs"
        ];
        paths.AddRange(project
            .Descendants("Compile")
            .Select(item => item.Attribute("Include")?.Value ?? string.Empty)
            .Select(include => Path.GetFullPath(
                Path.Combine(projectDirectory, include)))
            .Select(fullPath => Path.GetRelativePath(root, fullPath)
                .Replace('\\', '/')));
        return paths
            .Distinct(StringComparer.Ordinal)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
    }

    private static string TailForAudit(string value)
    {
        const int MaximumCharacters = 2_000;
        string tail = value.Length <= MaximumCharacters
            ? value
            : value[^MaximumCharacters..];
        return tail
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", " | ", StringComparison.Ordinal);
    }

    private static bool FailsWith(ScriptResult result, string reason)
    {
        return result.ExitCode == 1 &&
            result.Output.Contains(reason, StringComparison.Ordinal);
    }

    private static string[] MarkerList(string output, string prefix)
    {
        string? line = output
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(candidate => candidate.StartsWith(
                prefix,
                StringComparison.Ordinal));
        if (line is null)
        {
            throw new InvalidOperationException(
                "Missing QEMU marker summary line: " + prefix);
        }
        string value = line[prefix.Length..];
        return value.Length == 0 ||
            string.Equals(value, "none", StringComparison.Ordinal)
            ? []
            : value.Split(';', StringSplitOptions.RemoveEmptyEntries);
    }

    private static string[] ExtractPowerShellArray(
        string source,
        string declaration)
    {
        int start = source.IndexOf(declaration, StringComparison.Ordinal);
        if (start < 0)
        {
            return [];
        }
        int bodyStart = start + declaration.Length;
        int close = source.IndexOf("\n)", bodyStart, StringComparison.Ordinal);
        if (close < 0)
        {
            return [];
        }
        return Regex.Matches(
                source[bodyStart..close],
                @"(?m)^\s*'(?<value>[^'\r\n]*)',?\s*$",
                RegexOptions.CultureInvariant)
            .Cast<Match>()
            .Select(match => match.Groups["value"].Value)
            .ToArray();
    }

    private static string ExtractMethod(string source, string methodToken)
    {
        int start = source.IndexOf(methodToken, StringComparison.Ordinal);
        if (start < 0)
        {
            return string.Empty;
        }
        int open = source.IndexOf('{', start);
        if (open < 0)
        {
            return string.Empty;
        }
        int depth = 0;
        for (int index = open; index < source.Length; index++)
        {
            if (source[index] == '{')
            {
                depth++;
            }
            else if (source[index] == '}' && --depth == 0)
            {
                return source[start..(index + 1)];
            }
        }
        return string.Empty;
    }

    private static string ExtractAsciiMarker(string source, string methodToken)
    {
        return ExtractCharacters(ExtractMethod(source, methodToken));
    }

    private static string[] ExtractAsciiLines(string source, string methodToken)
    {
        return ExtractMethod(source, methodToken)
            .Split(
                "WriteCrLf(console, serial);",
                StringSplitOptions.RemoveEmptyEntries)
            .Select(ExtractCharacters)
            .Where(value => value.Length != 0)
            .ToArray();
    }

    private static string ExtractCharacters(string source)
    {
        return string.Concat(
            Regex.Matches(
                    source,
                    @"\(byte\)'(?<value>.)'",
                    RegexOptions.CultureInvariant)
                .Cast<Match>()
                .Select(match => match.Groups["value"].Value));
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

    private static string Sha256File(string path)
    {
        return Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    }

    private enum FakeObservation
    {
        Success,
        AllZero,
        LoadedMismatch,
        RestoredMismatch,
        InterruptFlagMismatch,
        NoTransition,
        WrongCookie,
        InvariantMismatch
    }

    private sealed class FakePlan
    {
        public bool BootServicesPresent { get; init; } = true;
        public bool AllocatePresent { get; init; } = true;
        public bool FreePresent { get; init; } = true;
        public ulong CodeAddress { get; init; } = ValidCodeAddress;
        public ulong DataAddress { get; init; } = ValidDataAddress;
        public ulong CodeAllocationStatus { get; init; }
        public ulong DataAllocationStatus { get; init; }
        public bool CorruptNormalizedPage { get; init; }
        public bool CorruptIdt { get; init; }
        public bool CorruptTransitionStub { get; init; }
        public bool PublicationSucceeded { get; init; } = true;
        public FakeObservation Observation { get; init; }
        public ulong DataReleaseStatus { get; init; }
        public ulong CodeReleaseStatus { get; init; }
    }

    private sealed class FakeTrace
    {
        public List<FakeAllocation> AllocationCalls { get; } = [];
        public List<FakeRelease> ReleaseCalls { get; } = [];
        public List<string> Events { get; } = [];
        public bool Succeeded { get; set; }
        public bool ReadyEmitted { get; set; }
        public bool FailureEmitted { get; set; }
        public bool FailClosed { get; set; }
    }

    private readonly record struct FakeAllocation(
        string Type,
        ulong Address,
        ulong Pages,
        ulong Status);

    private readonly record struct FakeRelease(
        ulong Address,
        ulong Pages,
        ulong Status);

    private readonly record struct ScriptResult(int ExitCode, string Output);
}
