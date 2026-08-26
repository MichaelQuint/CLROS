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

internal static class M02OwnedIdtGatePopulationEfiIntegrationTests
{
    private const string TestName = "m02.011.efi";
    private const string HostReadyMarker =
        "CLROS_M02_011_EFI_HOST_READY switchDefault=false " +
        "sourceLinked=true allocationPages=3 cleanup=true " +
        "markerOrder=true noExecution=true";
    private const string StartMarker = "TEST_START m02.011";
    private const string ReadyMarker =
        "CLROS_M02_011_READY vectors=256 gates=256 gateBytes=16 " +
        "tableBytes=4096 selector=8 present=256 dpl0=256 " +
        "interruptGates=256 ownedHandlers=256 firmwareGatesCopied=0 " +
        "nmiIst=3 doubleFaultIst=1 pageFaultIst=2 " +
        "normalizedFnv1a64=8514F42EBDFC5D55 readBack=true " +
        "guestPopulation=true loaderData=true allocationPages=3 " +
        "allocationReleased=true stubExecution=false idtrLoaded=false " +
        "faultTriggered=false markerTransport=uefiSerial " +
        "postExitBootServices=false";
    private const string PassMarker = "TEST_PASS m02.011";
    private const string FailureMarker =
        "M02_OWNED_IDT_GATE_POPULATION_FAILED";
    private const string FailureTestMarker =
        "TEST_FAIL m02.011 guest_population_readback_or_cleanup";
    private const string HostOnlyReadyMarker =
        "CLROS_M02_011_HOST_READY vectors=256 gates=256 gateBytes=16 " +
        "tableBytes=4096 selector=8 present=256 dpl0=256 " +
        "interruptGates=256 ownedHandlers=256 firmwareGatesCopied=0 " +
        "nmiIst=3 doubleFaultIst=1 pageFaultIst=2 " +
        "normalizedFnv1a64=8514F42EBDFC5D55 readBack=true " +
        "guestPopulation=false idtrLoaded=false stubExecution=false " +
        "faultTriggered=false postExitBootServices=false";
    private const string LegacyStartMarker = "TEST_START m02.010";
    private const string LegacyReadyMarker =
        "CLROS_M02_010_READY vectors=256 hardwareErrorCodeVectors=9 " +
        "syntheticErrorCodeVectors=247 slotBytes=16 pageBytes=4096 " +
        "normalizedSlots=3 commonTargetDelta=4096 " +
        "fnv1a64=428D0353A96B7ACE readBack=true " +
        "guestMaterialization=true loaderData=true pageReleased=true " +
        "stubExecution=false idtInstalled=false faultTriggered=false " +
        "markerTransport=uefiSerial postExitBootServices=false";
    private const string LegacyPassMarker = "TEST_PASS m02.010";
    private const string M04EndianReadyMarker =
        "CLROS_M04_002_READY readVectors=25 reverseVectors=7 byteSeries=5 " +
        "uint16Series=7 fingerprint=E81E4451E8D999F6 " +
        "referenceFilesRequired=false hostConformance=false " +
        "guestIntegrated=true gate4=Fail";
    private const string M04SpanReadyMarker =
        "CLROS_M04_003_READY vectors=16 spanVectors=4 overlapVectors=5 " +
        "memoryCastVectors=3 unsafeWidthVectors=4 " +
        "fingerprint=F03FF59D36F39ECD pointerWidth=8 " +
        "referenceFilesRequired=false hostConformance=false " +
        "guestIntegrated=true gate4=Fail";
    private const string CompileClosureSha256 =
        "413ddc044a3469f2f0ac51c1190609edeaa8fbac0d90beb41ab2648559f6549e";
    private const string SourceClosureSha256 =
        "2d24a9b956642dde73162a34dc0e84a426b92e70d39a21b064003abde9171240";
    private const string FrozenProductionSha256 =
        "00EC277C3F6A0DB49CB54B34810202EE1D508318B29733C3F0392A7BC36CFA38";
    private const string FrozenHostLeafSha256 =
        "F7C4C19A643FCD8010B0AE69870A9B6BF129EEC2AA32A53AF867C182C31A2C70";

    [ModuleInitializer]
    internal static void RunAll()
    {
        Console.WriteLine("TEST_START " + TestName);
        int failures = 0;

        RunCase(
            "M02-011 project source authority and tooling propagation are exact",
            TestProjectAndToolingContract,
            ref failures);
        RunCase(
            "M02-011 default and enabled NativeAOT artifacts prove exact provenance",
            TestDefaultAndEnabledEfiArtifacts,
            ref failures);
        RunCase(
            "M02-011 EFI arena population cleanup and result order are exact",
            TestEfiEntryContract,
            ref failures);
        RunCase(
            "M02-011 fake Boot Services owns validates and releases every allocation path",
            TestFakeBootServicesOrchestration,
            ref failures);
        RunCase(
            "M02-011 QEMU profile and hostile arguments remain exact",
            TestQemuProfilesAndHostileArguments,
            ref failures);
        RunCase(
            "M02-011 transcript rejects missing reordered duplicate and failure records",
            TestMarkerTranscriptRejection,
            ref failures);

        if (failures != 0)
        {
            Console.WriteLine(
                "TEST_FAIL " + TestName + " host-contract failures=" + failures);
            throw new InvalidOperationException(
                "M02-011 EFI integration host tests failed: " + failures);
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

        const string TerminalKey =
            @"..\OS.Kernel\Diagnostics\OwnedGDTTSSGeneratedAuditedCSharpStubBytes.cs|Diagnostics\OwnedGDTTSSGeneratedAuditedCSharpStubBytes.cs";
        const string NormalizedKey =
            @"..\OS.Kernel\Diagnostics\NormalizeErrorCodeNonErrorCodeInterruptFrame.cs|Diagnostics\NormalizeErrorCodeNonErrorCodeInterruptFrame.cs";
        const string IdtKey =
            @"..\OS.Kernel\Diagnostics\PopulateAllN256IDTVectorsOwnedGatesNever.cs|Diagnostics\PopulateAllN256IDTVectorsOwnedGatesNever.cs";
        const string EndianKey =
            @"..\..\std\no-runtime\shared\IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMOSA.cs|Std\IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMOSA.cs";
        int terminalIndex = Array.IndexOf(compileKeys, TerminalKey);
        int normalizedIndex = Array.IndexOf(compileKeys, NormalizedKey);
        int idtIndex = Array.IndexOf(compileKeys, IdtKey);
        int endianIndex = Array.IndexOf(compileKeys, EndianKey);

        XElement[] defaults = project
            .Descendants("EnableM02OwnedIdtGatePopulationProbe")
            .Where(element => element.Parent?.Name.LocalName == "PropertyGroup")
            .ToArray();
        XElement[] symbolGroups = project
            .Descendants("PropertyGroup")
            .Where(group => group.Attribute("Condition")?.Value ==
                "'$(EnableM02OwnedIdtGatePopulationProbe)' == 'true'")
            .ToArray();
        XElement[] terminalExclusions = project
            .Descendants("Error")
            .Where(error => error.Attribute("Condition")?.Value ==
                "'$(EnableM02OwnedIdtGatePopulationProbe)' == 'true' and " +
                "'$(EnableM02OwnedGdtTssTerminalProbe)' == 'true'")
            .ToArray();
        XElement[] normalizedExclusions = project
            .Descendants("Error")
            .Where(error => error.Attribute("Condition")?.Value ==
                "'$(EnableM02OwnedIdtGatePopulationProbe)' == 'true' and " +
                "'$(EnableM02NormalizedInterruptFramePageProbe)' == 'true'")
            .ToArray();

        string productionPath = Path.Combine(
            root,
            "src",
            "OS.Kernel",
            "Diagnostics",
            "PopulateAllN256IDTVectorsOwnedGatesNever.cs");
        string leafTestPath = Path.Combine(
            root,
            "tests",
            "OS.HostTests",
            "PopulateAllN256IDTVectorsOwnedGatesNeverTests.cs");
        string build = File.ReadAllText(
            Path.Combine(root, "scripts", "build-efi-shell.ps1"));
        string verifier = File.ReadAllText(
            Path.Combine(root, "scripts", "test-efi-artifact.ps1"));
        string qemu = File.ReadAllText(
            Path.Combine(root, "scripts", "run-qemu-smoke.ps1"));
        string probes = File.ReadAllText(
            Path.Combine(
                root,
                "src",
                "OS.Kernel",
                "Diagnostics",
                "Probes.cs"));
        string[] buildCompileFirewall = ExtractPowerShellArray(
            build,
            "$script:ExpectedEfiCompileItems = @(");
        string[] verifierCompileFirewall = ExtractPowerShellArray(
            verifier,
            "$expectedEfiCompileItems = @(");

        return compileItems.Length == 61
            && compileHash == CompileClosureSha256
            && buildCompileFirewall.SequenceEqual(
                compileKeys,
                StringComparer.Ordinal)
            && verifierCompileFirewall.SequenceEqual(
                compileKeys,
                StringComparer.Ordinal)
            && terminalIndex >= 0
            && normalizedIndex == terminalIndex + 1
            && idtIndex == normalizedIndex + 1
            && endianIndex == idtIndex + 1
            && compileKeys.Count(key => key == IdtKey) == 1
            && compileItems.All(item =>
                !(item.Attribute("Include")?.Value ?? string.Empty)
                    .Contains('*', StringComparison.Ordinal))
            && defaults.Length == 1
            && defaults[0].Value == "false"
            && defaults[0].Attribute("Condition")?.Value ==
                "'$(EnableM02OwnedIdtGatePopulationProbe)' == ''"
            && symbolGroups.Length == 1
            && symbolGroups[0].Elements("DefineConstants").Single().Value ==
                "$(DefineConstants);M02_OWNED_IDT_GATE_POPULATION_PROBE"
            && terminalExclusions.Length == 1
            && terminalExclusions[0].Attribute("Text")?.Value ==
                "EnableM02OwnedIdtGatePopulationProbe cannot be combined " +
                "with EnableM02OwnedGdtTssTerminalProbe=true."
            && normalizedExclusions.Length == 1
            && normalizedExclusions[0].Attribute("Text")?.Value ==
                "EnableM02OwnedIdtGatePopulationProbe cannot be combined " +
                "with EnableM02NormalizedInterruptFramePageProbe=true."
            && Sha256File(productionPath) == FrozenProductionSha256
            && Sha256File(leafTestPath) == FrozenHostLeafSha256
            && build.Contains(
                "[switch] $EnableM02OwnedIdtGatePopulationProbe",
                StringComparison.Ordinal)
            && build.Contains(
                "-M02OwnedIdtGatePopulationEnabled " +
                "$EnableM02OwnedIdtGatePopulationProbe.IsPresent",
                StringComparison.Ordinal)
            && build.Contains(
                "EFI_PUBLISH_SOURCE_AUTHORITY status=ok inputs=74",
                StringComparison.Ordinal)
            && build.Contains(IdtKey, StringComparison.Ordinal)
            && build.Contains(
                "M02_OWNED_IDT_GATE_POPULATION_PROBE",
                StringComparison.Ordinal)
            && verifier.Contains(
                "[switch]$EnableM02OwnedIdtGatePopulationProbe",
                StringComparison.Ordinal)
            && verifier.Contains(
                "$expectedSourceClosureInputCount = 74",
                StringComparison.Ordinal)
            && verifier.Contains(
                "$compileItems.Count -ne 61",
                StringComparison.Ordinal)
            && verifier.Contains(IdtKey, StringComparison.Ordinal)
            && verifier.Contains(
                "-p:EnableM02OwnedIdtGatePopulationProbe=true",
                StringComparison.Ordinal)
            && qemu.Contains(
                "[switch] $EnableM02OwnedIdtGatePopulationProbe",
                StringComparison.Ordinal)
            && qemu.Contains(
                "-EnableM02OwnedIdtGatePopulationProbe",
                StringComparison.Ordinal)
            && qemu.Contains(ReadyMarker, StringComparison.Ordinal)
            && qemu.Contains(
                "m02_owned_idt_gate_population_probe_requires_build_efi",
                StringComparison.Ordinal)
            && qemu.Contains(
                "m02_owned_idt_gate_population_probe_forbids_skip_image_build",
                StringComparison.Ordinal)
            && qemu.Contains(
                "m02_owned_idt_gate_population_probe_forbids_owned_gdt_tss_terminal",
                StringComparison.Ordinal)
            && qemu.Contains(
                "m02_owned_idt_gate_population_probe_forbids_normalized_interrupt_frame_page_probe",
                StringComparison.Ordinal)
            && !probes.Contains(
                "EnableM02OwnedIdtGatePopulationProbe",
                StringComparison.Ordinal)
            && !probes.Contains(
                "M02_OWNED_IDT_GATE_POPULATION_PROBE",
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
            "BOOTX64-M02-011-HOSTTEST-" + runIdentity + "-DEFAULT.EFI");
        string enabledArtifact = Path.Combine(
            artifactDirectory,
            "BOOTX64-M02-011-HOSTTEST-" + runIdentity + "-ENABLED.EFI");
        ScriptResult defaultBuild = RunPowerShellScript(
            root,
            script,
            600_000,
            "-EfiOutputPath",
            defaultArtifact,
            "-LogPath",
            Path.Combine(
                logDirectory,
                "m02-011-hosttest-" + runIdentity + "-default.log"));
        ScriptResult enabledBuild = RunPowerShellScript(
            root,
            script,
            600_000,
            "-EnableM02OwnedIdtGatePopulationProbe",
            "-EfiOutputPath",
            enabledArtifact,
            "-LogPath",
            Path.Combine(
                logDirectory,
                "m02-011-hosttest-" + runIdentity + "-enabled.log"));

        if (defaultBuild.ExitCode != 0 || enabledBuild.ExitCode != 0)
        {
            Console.WriteLine(
                "M02_011_ARTIFACT_BUILD_AUDIT defaultExit=" +
                defaultBuild.ExitCode + " enabledExit=" +
                enabledBuild.ExitCode + " defaultOutput=" +
                TailForAudit(defaultBuild.Output) + " enabledOutput=" +
                TailForAudit(enabledBuild.Output));
            return false;
        }

        string defaultMap = File.ReadAllText(defaultArtifact + ".link.map");
        string enabledMap = File.ReadAllText(enabledArtifact + ".link.map");
        byte[] defaultBytes = File.ReadAllBytes(defaultArtifact);
        bool defaultExcluded =
            !defaultMap.Contains(
                "WriteM02OwnedIdtGatePopulationProbe",
                StringComparison.Ordinal) &&
            !ContainsAscii(
                defaultBytes,
                "M02_OWNED_IDT_GATE_POPULATION_PROBE") &&
            !ContainsAscii(defaultBytes, ReadyMarker);
        bool enabledIncluded = enabledMap.Contains(
            "WriteM02OwnedIdtGatePopulationProbe",
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
            "M02_011_ARTIFACT_BUILD_AUDIT defaultExit=0 enabledExit=0 " +
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
        string block = ExtractBlock(
            source,
            "private const uint M02OwnedIdtGateAllocateAnyPages",
            "#endif");
        string probe = ExtractMethod(
            source,
            "private static bool WriteM02OwnedIdtGatePopulationProbe(");
        string stubMatcher = ExtractMethod(
            source,
            "private static bool M02OwnedIdtGateStubResultMatches(");
        string tableMatcher = ExtractMethod(
            source,
            "private static bool M02OwnedIdtGateTableResultMatches(");
        string start = ExtractAsciiMarker(
            source,
            "private static void WriteM02OwnedIdtGatePopulationStartLine(");
        string ready = ExtractAsciiMarker(
            source,
            "private static void WriteM02OwnedIdtGatePopulationSuccessLine(");
        string pass = ExtractAsciiMarker(
            source,
            "private static void WriteM02OwnedIdtGatePopulationPassLine(");
        string[] failureLines = ExtractAsciiLines(
            source,
            "private static void WriteM02OwnedIdtGatePopulationFailureLines(");

        int consoleReady = source.IndexOf(
            "WriteEfiConsoleReady(console, serial);",
            StringComparison.Ordinal);
        int m04Span = source.IndexOf(
            "WriteM04SpanMemoryConformanceProbe(console, serial)",
            consoleReady,
            StringComparison.Ordinal);
        int m02Call = source.IndexOf(
            "WriteM02OwnedIdtGatePopulationProbe(",
            m04Span,
            StringComparison.Ordinal);
        int memoryMap = source.IndexOf(
            "TryCaptureUefiMemoryMap(systemTable)",
            m02Call,
            StringComparison.Ordinal);

        int startCall = probe.IndexOf(
            "WriteM02OwnedIdtGatePopulationStartLine(console, serial);",
            StringComparison.Ordinal);
        int allocate = probe.IndexOf(
            "bootServices->AllocatePages(",
            StringComparison.Ordinal);
        int allocationFailure = probe.IndexOf(
            "if (allocationStatus != 0)",
            allocate,
            StringComparison.Ordinal);
        int addressValidation = probe.IndexOf(
            "bool addressValid =",
            allocationFailure,
            StringComparison.Ordinal);
        int stubMaterialize = probe.IndexOf(
            "stubMaterializeResult =",
            addressValidation,
            StringComparison.Ordinal);
        int stubReadBack = probe.IndexOf(
            "stubReadBackResult =",
            stubMaterialize,
            StringComparison.Ordinal);
        int idtMaterialize = probe.IndexOf(
            "idtMaterializeResult =",
            stubReadBack,
            StringComparison.Ordinal);
        int idtReadBack = probe.IndexOf(
            "idtReadBackResult =",
            idtMaterialize,
            StringComparison.Ordinal);
        int free = probe.IndexOf(
            "bootServices->FreePages(",
            idtReadBack,
            StringComparison.Ordinal);
        int releaseAudit = probe.IndexOf(
            "releaseStatus != 0",
            free,
            StringComparison.Ordinal);
        int readyCall = probe.IndexOf(
            "WriteM02OwnedIdtGatePopulationSuccessLine(console, serial);",
            releaseAudit,
            StringComparison.Ordinal);
        int passCall = probe.IndexOf(
            "WriteM02OwnedIdtGatePopulationPassLine(console, serial);",
            readyCall,
            StringComparison.Ordinal);

        string[] exactBlockTokens =
        [
            "private const uint M02OwnedIdtGateAllocateAnyPages = 0;",
            "private const uint M02OwnedIdtGateEfiLoaderData = 2;",
            "private const ulong M02OwnedIdtGateAllocationPages = 3;",
            "private const ulong M02OwnedIdtGatePageBytes = 4096;",
            "M02OwnedIdtGateAllocationPages * M02OwnedIdtGatePageBytes",
            "0x641BA73668C75E0EUL",
            "0x8514F42EBDFC5D55UL"
        ];
        string[] exactProbeTokens =
        [
            "bootServices->AllocatePages == null",
            "bootServices->FreePages == null",
            "M02OwnedIdtGateAllocateAnyPages",
            "M02OwnedIdtGateEfiLoaderData",
            "M02OwnedIdtGateAllocationPages",
            "arenaAddress != 0",
            "(arenaAddress & (M02OwnedIdtGatePageBytes - 1)) == 0",
            ".IsCanonicalX64Address(arenaAddress)",
            "arenaAddress <= ~0UL - M02OwnedIdtGateArenaBytes",
            "arenaAddress + M02OwnedIdtGatePageBytes",
            "arenaAddress + (2 * M02OwnedIdtGatePageBytes)",
            "arenaAddress + M02OwnedIdtGateArenaBytes - 1",
            "((arenaAddress >> 47) & 1UL) ==",
            ".NormalizeErrorCodeNonErrorCodeInterruptFrame",
            ".PopulateAllN256IDTVectorsOwnedGatesNever",
            "stubMaterializeResult.Fingerprint ==",
            "stubReadBackResult.Fingerprint",
            "idtMaterializeResult.Fingerprint ==",
            "idtReadBackResult.Fingerprint",
            "if (!addressValid || !resultsMatch || releaseStatus != 0)"
        ];
        string[] stubResultTokens =
        [
            "result.IsInitialized",
            "result.Operation == operation",
            ".NormalizeInterruptFramePageStatus.Success",
            "result.ModeledPageAddress == pageAddress",
            "result.CommonTargetAddress == commonTargetAddress",
            "result.PageCapacity == M02OwnedIdtGatePageBytes",
            "result.RequiredBytes == (int)M02OwnedIdtGatePageBytes",
            "result.BytesWritten ==",
            "result.BytesCompared == (int)M02OwnedIdtGatePageBytes",
            "result.FirstMismatchOffset == -1",
            "result.HardwareErrorCodeVectorCount == 9",
            "result.SyntheticErrorCodeVectorCount == 247",
            "result.Fingerprint == M02OwnedIdtGateStubExpectedFnv1A64",
            "result.DestinationMutated == materialized",
            "result.ReadBackPerformed",
            "result.Materialized == materialized",
            "result.ReadBackValidated == !materialized",
            "result.Succeeded"
        ];
        string[] tableResultTokens =
        [
            "result.IsInitialized",
            "result.Operation == operation",
            "OwnedIdtGateTableStatus.Success",
            "result.ModeledTableAddress == tableAddress",
            "result.OwnedStubPageAddress == stubPageAddress",
            "result.TableCapacity == M02OwnedIdtGatePageBytes",
            "result.RequiredBytes == (int)M02OwnedIdtGatePageBytes",
            "result.DescriptorCount == 256",
            "result.PresentDescriptorCount == 256",
            "result.IstRoutedDescriptorCount == 3",
            "result.DefaultIstDescriptorCount == 253",
            "result.FirmwareGateCopyCount == 0",
            "result.BytesWritten ==",
            "result.BytesCompared == (int)M02OwnedIdtGatePageBytes",
            "result.FirstMismatchOffset == -1",
            "result.FirstMismatchVector == -1",
            "result.NormalizedPolicyFingerprint ==",
            "M02OwnedIdtGateNormalizedPolicyFnv1A64",
            "result.DestinationMutated == materialized",
            "result.ReadBackPerformed",
            "result.Materialized == materialized",
            "result.ReadBackValidated == !materialized",
            "result.Succeeded"
        ];
        string[] forbiddenProbeTokens =
        [
            "delegate*",
            "EfiLoaderCode",
            "LoadIdtr",
            "LoadIDTR",
            "ReadIdtr",
            "StoreIdtr",
            "FirmwareIdt",
            "FirmwareGate",
            "CopyFirmware",
            "sidt",
            "lidt",
            "Execute",
            "Invoke",
            "Spin",
            "FaultProbe",
            "TriggerFault",
            "RaiseFault"
        ];

        return ReadyMarker.Length == 442
            && consoleReady >= 0
            && m04Span > consoleReady
            && m02Call > m04Span
            && memoryMap > m02Call
            && startCall >= 0
            && startCall < allocate
            && allocate < allocationFailure
            && allocationFailure < addressValidation
            && addressValidation < stubMaterialize
            && stubMaterialize < stubReadBack
            && stubReadBack < idtMaterialize
            && idtMaterialize < idtReadBack
            && idtReadBack < free
            && free < releaseAudit
            && releaseAudit < readyCall
            && readyCall < passCall
            && !probe.Contains(
                "allocationStatus != 0 ||",
                StringComparison.Ordinal)
            && CountOccurrences(probe, "bootServices->AllocatePages(") == 1
            && CountOccurrences(probe, ".Materialize(") == 2
            && CountOccurrences(probe, ".ValidateReadBack(") == 2
            && CountOccurrences(probe, "bootServices->FreePages(") == 1
            && Regex.IsMatch(
                probe,
                @"bootServices->FreePages\(\s*arenaAddress,\s*" +
                @"M02OwnedIdtGateAllocationPages\s*\)",
                RegexOptions.CultureInvariant)
            && Regex.IsMatch(
                probe,
                @"\Aprivate static bool " +
                @"WriteM02OwnedIdtGatePopulationProbe\(\s*" +
                @"EfiSimpleTextOutputProtocol\* console,\s*" +
                @"EfiSerialIoProtocol\* serial,\s*" +
                @"EfiSystemTable\* systemTable\s*\)",
                RegexOptions.CultureInvariant)
            && exactBlockTokens.All(token =>
                block.Contains(token, StringComparison.Ordinal))
            && exactProbeTokens.All(token =>
                probe.Contains(token, StringComparison.Ordinal))
            && stubResultTokens.All(token =>
                stubMatcher.Contains(token, StringComparison.Ordinal))
            && tableResultTokens.All(token =>
                tableMatcher.Contains(token, StringComparison.Ordinal))
            && forbiddenProbeTokens.All(token =>
                !probe.Contains(token, StringComparison.OrdinalIgnoreCase))
            && start == StartMarker
            && ready == ReadyMarker
            && pass == PassMarker
            && failureLines.SequenceEqual(
                [FailureMarker, FailureTestMarker],
                StringComparer.Ordinal);
    }

    private static unsafe bool TestFakeBootServicesOrchestration()
    {
        const ulong ValidArenaAddress = 0x0000_0000_0020_0000UL;

        FakeProbeTrace success = RunFakeProbe(new FakeProbePlan(
            HasBootServices: true,
            HasAllocatePages: true,
            HasFreePages: true,
            AllocateStatus: 0,
            ArenaAddress: ValidArenaAddress,
            MutateStubReadBack: false,
            MutateIdtReadBack: false,
            FreeStatus: 0));
        FakeProbeTrace missingBootServices = RunFakeProbe(default);
        FakeProbeTrace missingAllocate = RunFakeProbe(new FakeProbePlan(
            HasBootServices: true,
            HasAllocatePages: false,
            HasFreePages: true,
            AllocateStatus: 0,
            ArenaAddress: ValidArenaAddress,
            MutateStubReadBack: false,
            MutateIdtReadBack: false,
            FreeStatus: 0));
        FakeProbeTrace missingFree = RunFakeProbe(new FakeProbePlan(
            HasBootServices: true,
            HasAllocatePages: true,
            HasFreePages: false,
            AllocateStatus: 0,
            ArenaAddress: ValidArenaAddress,
            MutateStubReadBack: false,
            MutateIdtReadBack: false,
            FreeStatus: 0));
        FakeProbeTrace allocationFailure = RunFakeProbe(new FakeProbePlan(
            HasBootServices: true,
            HasAllocatePages: true,
            HasFreePages: true,
            AllocateStatus: 1,
            ArenaAddress: 0,
            MutateStubReadBack: false,
            MutateIdtReadBack: false,
            FreeStatus: 0));

        ulong[] invalidAddresses =
        [
            0,
            ValidArenaAddress + 1,
            0x0000_8000_0000_0000UL,
            0xffff_ffff_ffff_f000UL,
            0x0000_7fff_ffff_f000UL
        ];
        bool invalidCasesExact = invalidAddresses.All(address =>
            IsInvalidAddressTraceExact(
                RunFakeProbe(new FakeProbePlan(
                    HasBootServices: true,
                    HasAllocatePages: true,
                    HasFreePages: true,
                    AllocateStatus: 0,
                    ArenaAddress: address,
                    MutateStubReadBack: false,
                    MutateIdtReadBack: false,
                    FreeStatus: 0)),
                address));

        FakeProbeTrace stubResultFailure = RunFakeProbe(new FakeProbePlan(
            HasBootServices: true,
            HasAllocatePages: true,
            HasFreePages: true,
            AllocateStatus: 0,
            ArenaAddress: ValidArenaAddress,
            MutateStubReadBack: false,
            MutateIdtReadBack: false,
            FreeStatus: 0,
            FailStubMaterializeResult: true));
        FakeProbeTrace idtResultFailure = RunFakeProbe(new FakeProbePlan(
            HasBootServices: true,
            HasAllocatePages: true,
            HasFreePages: true,
            AllocateStatus: 0,
            ArenaAddress: ValidArenaAddress,
            MutateStubReadBack: false,
            MutateIdtReadBack: false,
            FreeStatus: 0,
            FailIdtMaterializeResult: true));

        FakeProbeTrace stubMutation = RunFakeProbe(new FakeProbePlan(
            HasBootServices: true,
            HasAllocatePages: true,
            HasFreePages: true,
            AllocateStatus: 0,
            ArenaAddress: ValidArenaAddress,
            MutateStubReadBack: true,
            MutateIdtReadBack: false,
            FreeStatus: 0));
        FakeProbeTrace idtMutation = RunFakeProbe(new FakeProbePlan(
            HasBootServices: true,
            HasAllocatePages: true,
            HasFreePages: true,
            AllocateStatus: 0,
            ArenaAddress: ValidArenaAddress,
            MutateStubReadBack: false,
            MutateIdtReadBack: true,
            FreeStatus: 0));
        FakeProbeTrace releaseFailure = RunFakeProbe(new FakeProbePlan(
            HasBootServices: true,
            HasAllocatePages: true,
            HasFreePages: true,
            AllocateStatus: 0,
            ArenaAddress: ValidArenaAddress,
            MutateStubReadBack: false,
            MutateIdtReadBack: false,
            FreeStatus: 1));

        Console.WriteLine(
            "M02_011_FAKE_AUDIT " +
            "successReady=" + success.ReadyEmitted.ToString().ToLowerInvariant() +
            " successPass=" + success.PassEmitted.ToString().ToLowerInvariant() +
            " successFailure=" + success.FailureEmitted.ToString().ToLowerInvariant() +
            " stubFnv=" + success.StubFingerprint.ToString("X16") +
            " normalizedFnv=" +
                success.NormalizedPolicyFingerprint.ToString("X16") +
            " stubSha256=" + success.StubPageSha256 +
            " slots=" + success.RepresentativeSlotsExact.ToString().ToLowerInvariant() +
            " descriptors=" +
                success.RepresentativeDescriptorsExact.ToString().ToLowerInvariant() +
            " handlers=" +
                success.AllHandlersInsideStubPage.ToString().ToLowerInvariant() +
            " thirdPage=" + success.ThirdPageUnchanged.ToString().ToLowerInvariant() +
            " prerequisites=" +
                (IsPreAllocationFailureExact(missingBootServices) &&
                 IsPreAllocationFailureExact(missingAllocate) &&
                 IsPreAllocationFailureExact(missingFree))
                .ToString().ToLowerInvariant() +
            " allocateFailure=" +
                (allocationFailure.AllocateCalls == 1 &&
                 allocationFailure.FreeCalls == 0 &&
                 allocationFailure.FailureEmitted)
                .ToString().ToLowerInvariant() +
            " invalidAddresses=" + invalidCasesExact.ToString().ToLowerInvariant() +
            " page0ResultFailure=" +
                IsPostAllocationFailureExact(
                    stubResultFailure,
                    ValidArenaAddress)
                .ToString().ToLowerInvariant() +
            " page1ResultFailure=" +
                IsPostAllocationFailureExact(
                    idtResultFailure,
                    ValidArenaAddress)
                .ToString().ToLowerInvariant() +
            " stubReadBackMismatch=" +
                IsPostAllocationFailureExact(stubMutation, ValidArenaAddress)
                .ToString().ToLowerInvariant() +
            " idtReadBackMismatch=" +
                IsPostAllocationFailureExact(idtMutation, ValidArenaAddress)
                .ToString().ToLowerInvariant() +
            " releaseFailure=" +
                IsPostAllocationFailureExact(releaseFailure, ValidArenaAddress)
                .ToString().ToLowerInvariant());

        return success.StartEmitted
            && success.AllocateCalls == 1
            && success.StubMaterializeCalls == 1
            && success.StubReadBackCalls == 1
            && success.IdtMaterializeCalls == 1
            && success.IdtReadBackCalls == 1
            && success.FreeCalls == 1
            && success.FreedAddress == ValidArenaAddress
            && success.FreedPages == 3
            && success.ReleaseCompleted
            && success.ReadyEmitted
            && success.PassEmitted
            && !success.FailureEmitted
            && success.StubFingerprint == 0x641BA73668C75E0EUL
            && success.NormalizedPolicyFingerprint == 0x8514F42EBDFC5D55UL
            && success.StubPageSha256 ==
                "650F6D5EB96B0E625C2FF5FB852C0A67C6CE68D0CB92CD4B137E4DBC28636630"
            && success.RepresentativeSlotsExact
            && success.RepresentativeDescriptorsExact
            && success.AllHandlersInsideStubPage
            && success.ThirdPageUnchanged
            && IsPreAllocationFailureExact(missingBootServices)
            && IsPreAllocationFailureExact(missingAllocate)
            && IsPreAllocationFailureExact(missingFree)
            && allocationFailure.StartEmitted
            && allocationFailure.AllocateCalls == 1
            && allocationFailure.FreeCalls == 0
            && allocationFailure.StubMaterializeCalls == 0
            && allocationFailure.IdtMaterializeCalls == 0
            && allocationFailure.FailureEmitted
            && !allocationFailure.ReadyEmitted
            && !allocationFailure.PassEmitted
            && invalidCasesExact
            && IsPostAllocationFailureExact(
                stubResultFailure,
                ValidArenaAddress)
            && IsPostAllocationFailureExact(
                idtResultFailure,
                ValidArenaAddress)
            && IsPostAllocationFailureExact(stubMutation, ValidArenaAddress)
            && IsPostAllocationFailureExact(idtMutation, ValidArenaAddress)
            && IsPostAllocationFailureExact(releaseFailure, ValidArenaAddress)
            && stubResultFailure.ReleaseCompleted
            && idtResultFailure.ReleaseCompleted
            && stubMutation.ReleaseCompleted
            && stubMutation.ThirdPageUnchanged
            && idtMutation.ThirdPageUnchanged
            && !releaseFailure.ReleaseCompleted;
    }

    private static unsafe FakeProbeTrace RunFakeProbe(FakeProbePlan plan)
    {
        const int PageBytes = 4096;
        const int ArenaPages = 3;
        const int ArenaBytes = PageBytes * ArenaPages;

        FakeProbeTrace trace = new()
        {
            StartEmitted = true
        };
        if (!plan.HasBootServices ||
            !plan.HasAllocatePages ||
            !plan.HasFreePages)
        {
            trace.FailureEmitted = true;
            return trace;
        }

        trace.AllocateCalls++;
        if (plan.AllocateStatus != 0)
        {
            trace.FailureEmitted = true;
            return trace;
        }

        bool addressValid = IsArenaAddressValid(plan.ArenaAddress);
        bool resultsMatch = false;
        if (addressValid)
        {
            ulong tableAddress = plan.ArenaAddress + PageBytes;
            ulong commonTargetAddress = plan.ArenaAddress + (2UL * PageBytes);
            byte* arenaPointer = (byte*)NativeMemory.AlignedAlloc(
                ArenaBytes,
                PageBytes);
            if (arenaPointer == null)
            {
                throw new OutOfMemoryException(
                    "Could not allocate the aligned fake EFI arena.");
            }

            try
            {
                Span<byte> arena = new(arenaPointer, ArenaBytes);
                arena.Fill(0xa5);
                trace.StubMaterializeCalls++;
                NormalizeInterruptFramePageResult stubMaterialize =
                    NormalizeErrorCodeNonErrorCodeInterruptFrame.Materialize(
                        arenaPointer,
                        PageBytes,
                        plan.ArenaAddress,
                        commonTargetAddress);
                if (plan.MutateStubReadBack)
                {
                    arenaPointer[7] ^= 0x01;
                }

                trace.StubReadBackCalls++;
                NormalizeInterruptFramePageResult stubReadBack =
                    NormalizeErrorCodeNonErrorCodeInterruptFrame.ValidateReadBack(
                        arenaPointer,
                        PageBytes,
                        plan.ArenaAddress,
                        commonTargetAddress);

                trace.IdtMaterializeCalls++;
                OwnedIdtGateTableResult idtMaterialize =
                    PopulateAllN256IDTVectorsOwnedGatesNever.Materialize(
                        arenaPointer + PageBytes,
                        PageBytes,
                        tableAddress,
                        plan.ArenaAddress);
                if (plan.MutateIdtReadBack)
                {
                    arenaPointer[PageBytes + 13] ^= 0x01;
                }

                trace.IdtReadBackCalls++;
                OwnedIdtGateTableResult idtReadBack =
                    PopulateAllN256IDTVectorsOwnedGatesNever.ValidateReadBack(
                        arenaPointer + PageBytes,
                        PageBytes,
                        tableAddress,
                        plan.ArenaAddress);

                resultsMatch =
                    !plan.FailStubMaterializeResult &&
                    stubMaterialize.Materialized &&
                    stubReadBack.ReadBackValidated &&
                    stubMaterialize.Fingerprint == 0x641BA73668C75E0EUL &&
                    stubMaterialize.Fingerprint == stubReadBack.Fingerprint &&
                    !plan.FailIdtMaterializeResult &&
                    idtMaterialize.Materialized &&
                    idtReadBack.ReadBackValidated &&
                    idtMaterialize.Fingerprint == idtReadBack.Fingerprint &&
                    idtMaterialize.NormalizedPolicyFingerprint ==
                        0x8514F42EBDFC5D55UL &&
                    idtReadBack.NormalizedPolicyFingerprint ==
                        0x8514F42EBDFC5D55UL &&
                    idtMaterialize.FirmwareGateCopyCount == 0 &&
                    idtReadBack.FirmwareGateCopyCount == 0;

                trace.StubFingerprint = stubMaterialize.Fingerprint;
                trace.NormalizedPolicyFingerprint =
                    idtMaterialize.NormalizedPolicyFingerprint;
                trace.RepresentativeDescriptorsExact =
                    AuditRepresentativeDescriptors(
                        arena.Slice(PageBytes, PageBytes),
                        plan.ArenaAddress);
                trace.AllHandlersInsideStubPage =
                    AuditAllHandlers(
                        arena.Slice(PageBytes, PageBytes),
                        plan.ArenaAddress);
                trace.StubPageSha256 = Convert.ToHexString(
                    SHA256.HashData(arena.Slice(0, PageBytes)));
                trace.RepresentativeSlotsExact =
                    Convert.ToHexString(arena.Slice(0, 16)) ==
                        "6A006A006800000000E9F21F00009090" &&
                    Convert.ToHexString(arena.Slice(8 * 16, 16)) ==
                        "6A016808000000E9741F000090909090" &&
                    Convert.ToHexString(arena.Slice(255 * 16, 16)) ==
                        "6A006A0068FF000000E9021000009090";
                trace.ThirdPageUnchanged = arena
                    .Slice(2 * PageBytes, PageBytes)
                    .IndexOfAnyExcept((byte)0xa5) < 0;
            }
            finally
            {
                NativeMemory.AlignedFree(arenaPointer);
            }
        }

        trace.FreeCalls++;
        trace.FreedAddress = plan.ArenaAddress;
        trace.FreedPages = ArenaPages;
        trace.ReleaseCompleted = plan.FreeStatus == 0;
        if (addressValid && resultsMatch && trace.ReleaseCompleted)
        {
            trace.ReadyEmitted = true;
            trace.PassEmitted = true;
        }
        else
        {
            trace.FailureEmitted = true;
        }

        return trace;
    }

    private static bool IsArenaAddressValid(ulong arenaAddress)
    {
        const ulong PageBytes = 4096;
        const ulong ArenaBytes = 3 * PageBytes;
        if (arenaAddress == 0 ||
            (arenaAddress & (PageBytes - 1)) != 0 ||
            !PopulateAllN256IDTVectorsOwnedGatesNever
                .IsCanonicalX64Address(arenaAddress) ||
            arenaAddress > ~0UL - ArenaBytes)
        {
            return false;
        }

        ulong tableAddress = arenaAddress + PageBytes;
        ulong commonTargetAddress = arenaAddress + (2 * PageBytes);
        ulong endInclusive = arenaAddress + ArenaBytes - 1;
        return PopulateAllN256IDTVectorsOwnedGatesNever
                .IsCanonicalX64Address(tableAddress) &&
            PopulateAllN256IDTVectorsOwnedGatesNever
                .IsCanonicalX64Address(commonTargetAddress) &&
            PopulateAllN256IDTVectorsOwnedGatesNever
                .IsCanonicalX64Address(endInclusive) &&
            ((arenaAddress >> 47) & 1UL) ==
                ((endInclusive >> 47) & 1UL);
    }

    private static bool AuditRepresentativeDescriptors(
        ReadOnlySpan<byte> table,
        ulong stubPageAddress)
    {
        int[] vectors = [0, 2, 8, 14, 255];
        foreach (int vector in vectors)
        {
            ReadOnlySpan<byte> descriptor = table.Slice(vector * 16, 16);
            byte expectedIst = vector switch
            {
                2 => 3,
                8 => 1,
                14 => 2,
                _ => 0
            };
            if (DecodeHandler(descriptor) !=
                    stubPageAddress + ((ulong)vector * 16) ||
                (ushort)(descriptor[2] | (descriptor[3] << 8)) != 0x0008 ||
                descriptor[4] != expectedIst ||
                descriptor[5] != 0x8e ||
                descriptor[12] != 0 ||
                descriptor[13] != 0 ||
                descriptor[14] != 0 ||
                descriptor[15] != 0)
            {
                return false;
            }
        }

        return true;
    }

    private static bool AuditAllHandlers(
        ReadOnlySpan<byte> table,
        ulong stubPageAddress)
    {
        for (int vector = 0; vector < 256; vector++)
        {
            ulong handler = DecodeHandler(table.Slice(vector * 16, 16));
            if (handler != stubPageAddress + ((ulong)vector * 16) ||
                handler < stubPageAddress ||
                handler >= stubPageAddress + 4096)
            {
                return false;
            }
        }

        return true;
    }

    private static ulong DecodeHandler(ReadOnlySpan<byte> descriptor)
    {
        return descriptor[0] |
            ((ulong)descriptor[1] << 8) |
            ((ulong)descriptor[6] << 16) |
            ((ulong)descriptor[7] << 24) |
            ((ulong)descriptor[8] << 32) |
            ((ulong)descriptor[9] << 40) |
            ((ulong)descriptor[10] << 48) |
            ((ulong)descriptor[11] << 56);
    }

    private static bool IsPreAllocationFailureExact(FakeProbeTrace trace)
    {
        return trace.StartEmitted &&
            trace.AllocateCalls == 0 &&
            trace.StubMaterializeCalls == 0 &&
            trace.StubReadBackCalls == 0 &&
            trace.IdtMaterializeCalls == 0 &&
            trace.IdtReadBackCalls == 0 &&
            trace.FreeCalls == 0 &&
            trace.FailureEmitted &&
            !trace.ReadyEmitted &&
            !trace.PassEmitted;
    }

    private static bool IsInvalidAddressTraceExact(
        FakeProbeTrace trace,
        ulong expectedReturnedAddress)
    {
        return trace.StartEmitted &&
            trace.AllocateCalls == 1 &&
            trace.StubMaterializeCalls == 0 &&
            trace.StubReadBackCalls == 0 &&
            trace.IdtMaterializeCalls == 0 &&
            trace.IdtReadBackCalls == 0 &&
            trace.FreeCalls == 1 &&
            trace.FreedAddress == expectedReturnedAddress &&
            trace.FreedPages == 3 &&
            trace.ReleaseCompleted &&
            trace.FailureEmitted &&
            !trace.ReadyEmitted &&
            !trace.PassEmitted;
    }

    private static bool IsPostAllocationFailureExact(
        FakeProbeTrace trace,
        ulong expectedReturnedAddress)
    {
        return trace.StartEmitted &&
            trace.AllocateCalls == 1 &&
            trace.StubMaterializeCalls == 1 &&
            trace.StubReadBackCalls == 1 &&
            trace.IdtMaterializeCalls == 1 &&
            trace.IdtReadBackCalls == 1 &&
            trace.FreeCalls == 1 &&
            trace.FreedAddress == expectedReturnedAddress &&
            trace.FreedPages == 3 &&
            trace.FailureEmitted &&
            !trace.ReadyEmitted &&
            !trace.PassEmitted;
    }

    private static bool TestQemuProfilesAndHostileArguments()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        ScriptResult defaultProfile = RunQemuDryRun(root);
        ScriptResult m02011 = RunQemuDryRun(
            root,
            "-BuildEfi",
            "-EnableM02OwnedIdtGatePopulationProbe");
        ScriptResult missingBuild = RunQemuDryRun(
            root,
            "-EnableM02OwnedIdtGatePopulationProbe");
        ScriptResult skipImage = RunQemuDryRun(
            root,
            "-BuildEfi",
            "-SkipImageBuild",
            "-EnableM02OwnedIdtGatePopulationProbe");
        ScriptResult terminalCollision = RunQemuDryRun(
            root,
            "-BuildEfi",
            "-EnableM02OwnedGdtTssTerminalProbe",
            "-EnableM02OwnedIdtGatePopulationProbe");
        ScriptResult normalizedCollision = RunQemuDryRun(
            root,
            "-BuildEfi",
            "-EnableM02NormalizedInterruptFramePageProbe",
            "-EnableM02OwnedIdtGatePopulationProbe");
        ScriptResult combinedM04 = RunQemuDryRun(
            root,
            "-BuildEfi",
            "-EnableM04IndependentEndianConformanceProbe",
            "-EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe",
            "-EnableM02OwnedIdtGatePopulationProbe");

        string[] required = MarkerList(m02011.Output, "REQUIRED_MARKERS ");
        string[] ordered = MarkerList(m02011.Output, "ORDERED_MARKERS ");
        string[] unique = MarkerList(m02011.Output, "UNIQUE_MARKERS ");
        string[] forbidden = MarkerList(m02011.Output, "FORBIDDEN_MARKERS ");
        string[] combinedOrdered = MarkerList(
            combinedM04.Output,
            "ORDERED_MARKERS ");
        string[] combinedUnique = MarkerList(
            combinedM04.Output,
            "UNIQUE_MARKERS ");

        return defaultProfile.ExitCode == 0
            && MarkerList(defaultProfile.Output, "REQUIRED_MARKERS ")
                .SequenceEqual(
                    ExpectedBaselineRequiredMarkers(),
                    StringComparer.Ordinal)
            && MarkerList(defaultProfile.Output, "ORDERED_MARKERS ").Length == 0
            && MarkerList(defaultProfile.Output, "UNIQUE_MARKERS ").Length == 0
            && MarkerList(defaultProfile.Output, "FORBIDDEN_MARKERS ").Length == 2
            && !defaultProfile.Output.Contains(ReadyMarker, StringComparison.Ordinal)
            && m02011.ExitCode == 0
            && required.SequenceEqual(
                ExpectedM02011RequiredMarkers(),
                StringComparer.Ordinal)
            && ordered.SequenceEqual(
                [
                    "EFI_CONSOLE_READY",
                    StartMarker,
                    ReadyMarker,
                    PassMarker,
                    "UEFI_MEMORY_MAP_READY"
                ],
                StringComparer.Ordinal)
            && unique.SequenceEqual(
                [StartMarker, ReadyMarker, PassMarker],
                StringComparer.Ordinal)
            && forbidden.SequenceEqual(
                ["OS_SMOKE_FAIL", "TEST_FAIL", FailureMarker],
                StringComparer.Ordinal)
            && combinedM04.ExitCode == 0
            && MarkerList(combinedM04.Output, "REQUIRED_MARKERS ").Length == 19
            && combinedOrdered.SequenceEqual(
                [
                    "EFI_CONSOLE_READY",
                    M04EndianReadyMarker,
                    M04SpanReadyMarker,
                    StartMarker,
                    ReadyMarker,
                    PassMarker,
                    "UEFI_MEMORY_MAP_READY"
                ],
                StringComparer.Ordinal)
            && combinedUnique.SequenceEqual(
                [
                    M04EndianReadyMarker,
                    M04SpanReadyMarker,
                    StartMarker,
                    ReadyMarker,
                    PassMarker
                ],
                StringComparer.Ordinal)
            && FailsWith(
                missingBuild,
                "m02_owned_idt_gate_population_probe_requires_build_efi")
            && FailsWith(
                skipImage,
                "m02_owned_idt_gate_population_probe_forbids_skip_image_build")
            && FailsWith(
                terminalCollision,
                "m02_owned_idt_gate_population_probe_forbids_owned_gdt_tss_terminal")
            && FailsWith(
                normalizedCollision,
                "m02_owned_idt_gate_population_probe_forbids_normalized_interrupt_frame_page_probe");
    }

    private static bool TestMarkerTranscriptRejection()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        ScriptResult profile = RunQemuDryRun(
            root,
            "-BuildEfi",
            "-EnableM02OwnedIdtGatePopulationProbe");
        if (profile.ExitCode != 0)
        {
            return false;
        }

        string[] required = MarkerList(profile.Output, "REQUIRED_MARKERS ");
        string[] ordered = MarkerList(profile.Output, "ORDERED_MARKERS ");
        string[] unique = MarkerList(profile.Output, "UNIQUE_MARKERS ");
        string[] forbidden = MarkerList(profile.Output, "FORBIDDEN_MARKERS ");
        List<string> successLines = required
            .Where(marker =>
                !ordered.Contains(marker, StringComparer.Ordinal) &&
                marker != "OS_SMOKE_PASS")
            .ToList();
        successLines.AddRange(ordered);
        successLines.Add("OS_SMOKE_PASS");

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

        List<string> hostOnlyLines = successLines.ToList();
        hostOnlyLines[hostOnlyLines.IndexOf(ReadyMarker)] = HostOnlyReadyMarker;
        SerialMarkerCompletionResult hostOnly = ParseTranscript(
            required,
            forbidden,
            ordered,
            unique,
            hostOnlyLines);

        List<string> legacyLines = successLines.ToList();
        int guestReadyIndex = legacyLines.IndexOf(ReadyMarker);
        legacyLines.RemoveAt(guestReadyIndex);
        legacyLines.InsertRange(
            guestReadyIndex,
            [LegacyStartMarker, LegacyReadyMarker, LegacyPassMarker]);
        SerialMarkerCompletionResult legacy = ParseTranscript(
            required,
            forbidden,
            ordered,
            unique,
            legacyLines);

        List<string> failureLines = successLines.ToList();
        failureLines.Insert(failureLines.Count - 1, FailureMarker);
        SerialMarkerCompletionResult failure = ParseTranscript(
            required,
            forbidden,
            ordered,
            unique,
            failureLines);

        return required.SequenceEqual(
                ExpectedM02011RequiredMarkers(),
                StringComparer.Ordinal)
            && ordered.Length == 5
            && unique.Length == 3
            && forbidden.Length == 3
            && success.Passed
            && success.OrderedSeenCount == 5
            && success.UniqueSeenCount == 3
            && missing.MissingRequiredMarkers is { Count: 1 }
            && missing.MissingRequiredMarkers[0] == ReadyMarker
            && !missing.Passed
            && reordered.OutOfOrderOccurrenceCount != 0
            && !reordered.Passed
            && duplicateStart.DuplicateUniqueOccurrenceCount != 0
            && !duplicateStart.Passed
            && duplicateReady.DuplicateUniqueOccurrenceCount != 0
            && !duplicateReady.Passed
            && duplicatePass.DuplicateUniqueOccurrenceCount != 0
            && !duplicatePass.Passed
            && hostOnly.MissingRequiredMarkers is { Count: 1 }
            && hostOnly.MissingRequiredMarkers[0] == ReadyMarker
            && !hostOnly.Passed
            && legacy.MissingRequiredMarkers is { Count: 1 }
            && legacy.MissingRequiredMarkers[0] == ReadyMarker
            && !legacy.Passed
            && failure.ForbiddenOccurrenceCount != 0
            && !failure.Passed;
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

    private static string[] ExpectedM02011RequiredMarkers()
    {
        return
        [
            .. ExpectedBaselineRequiredMarkers(),
            "EFI_CONSOLE_READY",
            StartMarker,
            ReadyMarker,
            PassMarker,
            "UEFI_MEMORY_MAP_READY"
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
        string script = Path.Combine(root, "scripts", "run-qemu-smoke.ps1");
        return RunPowerShellScript(
            root,
            script,
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
                "M02-011 PowerShell command timed out: " + script);
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
        JsonElement sourceInputs = evidence.GetProperty("sourceClosureInputs");
        JsonElement[] inputElements = sourceInputs.EnumerateArray().ToArray();
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
            && evaluation.GetProperty("compileCount").GetInt32() == 61
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
                    "EnableM02OwnedIdtGatePopulationProbe")
                .GetString() == (enabled ? "true" : "false")
            && properties.GetProperty(
                    "EnableM02NormalizedInterruptFramePageProbe")
                .GetString() == "false"
            && properties.GetProperty("EnableM02OwnedGdtTssTerminalProbe")
                .GetString() == "false"
            && properties.GetProperty("EnableM04IndependentEndianConformanceProbe")
                .GetString() == "false"
            && properties.GetProperty(
                    "EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe")
                .GetString() == "false"
            && inputElements.Length == 74
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

    private static bool ContainsAscii(byte[] bytes, string value)
    {
        return bytes.AsSpan().IndexOf(Encoding.ASCII.GetBytes(value)) >= 0;
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

    private static string ExtractBlock(
        string source,
        string startToken,
        string endToken)
    {
        int start = source.IndexOf(startToken, StringComparison.Ordinal);
        int end = start < 0
            ? -1
            : source.IndexOf(
                endToken,
                start + startToken.Length,
                StringComparison.Ordinal);
        return start >= 0 && end > start
            ? source[start..end]
            : string.Empty;
    }

    private static string ExtractAsciiMarker(string source, string methodToken)
    {
        return ExtractCharacters(ExtractMethod(source, methodToken));
    }

    private static string[] ExtractAsciiLines(string source, string methodToken)
    {
        string method = ExtractMethod(source, methodToken);
        return method
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

    private readonly record struct FakeProbePlan(
        bool HasBootServices,
        bool HasAllocatePages,
        bool HasFreePages,
        ulong AllocateStatus,
        ulong ArenaAddress,
        bool MutateStubReadBack,
        bool MutateIdtReadBack,
        ulong FreeStatus,
        bool FailStubMaterializeResult = false,
        bool FailIdtMaterializeResult = false);

    private sealed class FakeProbeTrace
    {
        public bool StartEmitted { get; set; }
        public int AllocateCalls { get; set; }
        public int StubMaterializeCalls { get; set; }
        public int StubReadBackCalls { get; set; }
        public int IdtMaterializeCalls { get; set; }
        public int IdtReadBackCalls { get; set; }
        public int FreeCalls { get; set; }
        public ulong FreedAddress { get; set; }
        public int FreedPages { get; set; }
        public bool ReleaseCompleted { get; set; }
        public bool ReadyEmitted { get; set; }
        public bool PassEmitted { get; set; }
        public bool FailureEmitted { get; set; }
        public ulong StubFingerprint { get; set; }
        public ulong NormalizedPolicyFingerprint { get; set; }
        public string StubPageSha256 { get; set; } = string.Empty;
        public bool RepresentativeSlotsExact { get; set; }
        public bool RepresentativeDescriptorsExact { get; set; }
        public bool AllHandlersInsideStubPage { get; set; }
        public bool ThirdPageUnchanged { get; set; }
    }

    private readonly record struct ScriptResult(int ExitCode, string Output);
}
