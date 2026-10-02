using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using OS.Kernel.Diagnostics;
using OS.Tools.QemuRunner;

internal static unsafe class M02NormalizedInterruptFramePageEfiIntegrationTests
{
    private const string TestName = "m02.010.efi.integration";
    private const string StartMarker = "TEST_START m02.010";
    private const string ReadyMarker =
        "CLROS_M02_010_READY vectors=256 hardwareErrorCodeVectors=9 " +
        "syntheticErrorCodeVectors=247 slotBytes=16 pageBytes=4096 " +
        "normalizedSlots=3 commonTargetDelta=4096 " +
        "fnv1a64=428D0353A96B7ACE readBack=true " +
        "guestMaterialization=true loaderData=true pageReleased=true " +
        "stubExecution=false idtInstalled=false faultTriggered=false " +
        "markerTransport=uefiSerial postExitBootServices=false";
    private const string PassMarker = "TEST_PASS m02.010";
    private const string FailureMarker =
        "M02_NORMALIZED_INTERRUPT_FRAME_PAGE_FAILED";
    private const string FailureTestMarker =
        "TEST_FAIL m02.010 guest_materialization_readback_or_cleanup";
    private const string CompileClosureSha256 =
        "37b56feaae9f232dd547a34d4db872c5e998b2eb7d501eaf422c0c5f97034a79";
    private const string CanonicalProjectShapeSha256 =
        "3166c9719559f17f67232aca1842ba6c59bd6401aa97a07aeb59a5af69d3bd12";
    private const string FrozenProductionSha256 =
        "B66CD19B45E8198897B837DF9EDA6934AFBE087DC141C468BD0DB44E62724DA1";
    private const string FrozenHostLeafSha256 =
        "DEEA918C5AB7DB971AA39682F595204465D9D75DE254E99C034A12440AC46FCC";
    private const string ExpectedPageSha256 =
        "71325A5633E1985BB22B4F8E1581399F63FEDEEB8CBFA1E3507A3F4ED73A71AF";
    private const ulong ExpectedFingerprint = 0x428D0353A96B7ACEUL;

    [ModuleInitializer]
    internal static void RunAll()
    {
        Console.WriteLine("TEST_START " + TestName);
        int failures = 0;

        RunCase(
            "project source authority and typed tooling propagation are exact",
            TestProjectAndToolingContract,
            ref failures);
        RunCase(
            "EFI entry allocates materializes reads back and frees in exact order",
            TestEfiEntryContract,
            ref failures);
        RunCase(
            "fake Boot Services cover ownership cleanup and failure suppression",
            TestFakeBootServicesContract,
            ref failures);
        RunCase(
            "QEMU profiles and hostile arguments remain exact",
            TestQemuProfilesAndHostileArguments,
            ref failures);
        RunCase(
            "M02-010 marker transcript rejects missing reordered duplicate and failure records",
            TestMarkerTranscriptRejection,
            ref failures);

        if (failures != 0)
        {
            Console.WriteLine(
                "TEST_FAIL " + TestName + " host-contract failures=" + failures);
            throw new InvalidOperationException(
                "M02-010 EFI integration host tests failed: " + failures);
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
        const string EndianKey =
            @"..\..\std\no-runtime\shared\IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMOSA.cs|Std\IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMOSA.cs";
        int terminalIndex = Array.IndexOf(compileKeys, TerminalKey);
        int normalizedIndex = Array.IndexOf(compileKeys, NormalizedKey);
        int endianIndex = Array.IndexOf(compileKeys, EndianKey);

        XElement[] defaults = project
            .Descendants("EnableM02NormalizedInterruptFramePageProbe")
            .Where(element => element.Parent?.Name.LocalName == "PropertyGroup")
            .ToArray();
        XElement[] symbolGroups = project
            .Descendants("PropertyGroup")
            .Where(group => group.Attribute("Condition")?.Value ==
                "'$(EnableM02NormalizedInterruptFramePageProbe)' == 'true'")
            .ToArray();
        XElement[] exclusionErrors = project
            .Descendants("Error")
            .Where(error => error.Attribute("Condition")?.Value ==
                "'$(EnableM02NormalizedInterruptFramePageProbe)' == 'true' and " +
                "'$(EnableM02OwnedGdtTssTerminalProbe)' == 'true'")
            .ToArray();

        string productionPath = Path.Combine(
            root,
            "src",
            "OS.Kernel",
            "Diagnostics",
            "NormalizeErrorCodeNonErrorCodeInterruptFrame.cs");
        string leafTestPath = Path.Combine(
            root,
            "tests",
            "OS.HostTests",
            "NormalizeErrorCodeNonErrorCodeInterruptFrameTests.cs");
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

        return ReadyMarker.Length == 370
            && compileItems.Length == 77
            && compileHash == CompileClosureSha256
            && terminalIndex >= 0
            && normalizedIndex == terminalIndex + 1
            && endianIndex == normalizedIndex + 13
            && compileKeys.Count(key => key == NormalizedKey) == 1
            && compileItems.All(item =>
                !(item.Attribute("Include")?.Value ?? string.Empty)
                    .Contains('*', StringComparison.Ordinal))
            && defaults.Length == 1
            && defaults[0].Value == "false"
            && defaults[0].Attribute("Condition")?.Value ==
                "'$(EnableM02NormalizedInterruptFramePageProbe)' == ''"
            && symbolGroups.Length == 1
            && symbolGroups[0].Elements("DefineConstants").Single().Value ==
                "$(DefineConstants);M02_NORMALIZED_INTERRUPT_FRAME_PAGE_PROBE"
            && exclusionErrors.Length == 1
            && exclusionErrors[0].Attribute("Text")?.Value ==
                "EnableM02NormalizedInterruptFramePageProbe cannot be combined " +
                "with EnableM02OwnedGdtTssTerminalProbe=true."
            && Sha256File(productionPath) == FrozenProductionSha256
            && Sha256File(leafTestPath) == FrozenHostLeafSha256
            && build.Contains(
                "[switch] $EnableM02NormalizedInterruptFramePageProbe",
                StringComparison.Ordinal)
            && build.Contains(
                "-M02NormalizedInterruptFramePageEnabled " +
                "$EnableM02NormalizedInterruptFramePageProbe.IsPresent",
                StringComparison.Ordinal)
            && build.Contains(
                CanonicalProjectShapeSha256,
                StringComparison.Ordinal)
            && build.Contains(
                "EFI_PUBLISH_SOURCE_AUTHORITY status=ok inputs=90",
                StringComparison.Ordinal)
            && build.Contains(NormalizedKey, StringComparison.Ordinal)
            && build.Contains(
                "m02_normalized_interrupt_frame_page_probe_forbids_owned_gdt_tss_terminal",
                StringComparison.Ordinal)
            && verifier.Contains(
                "[switch]$EnableM02NormalizedInterruptFramePageProbe",
                StringComparison.Ordinal)
            && verifier.Contains(
                "$expectedSourceClosureInputCount = 90",
                StringComparison.Ordinal)
            && verifier.Contains(
                "$compileItems.Count -ne 77",
                StringComparison.Ordinal)
            && verifier.Contains(NormalizedKey, StringComparison.Ordinal)
            && verifier.Contains(
                "m02_normalized_interrupt_frame_page_probe_forbids_owned_gdt_tss_terminal",
                StringComparison.Ordinal)
            && qemu.Contains(
                "[switch] $EnableM02NormalizedInterruptFramePageProbe",
                StringComparison.Ordinal)
            && qemu.Contains(ReadyMarker, StringComparison.Ordinal)
            && qemu.Contains(
                "m02_normalized_interrupt_frame_page_probe_requires_build_efi",
                StringComparison.Ordinal)
            && qemu.Contains(
                "m02_normalized_interrupt_frame_page_probe_forbids_skip_image_build",
                StringComparison.Ordinal)
            && qemu.Contains(
                "m02_normalized_interrupt_frame_page_probe_forbids_owned_gdt_tss_terminal",
                StringComparison.Ordinal)
            && !probes.Contains(
                "EnableM02NormalizedInterruptFramePageProbe",
                StringComparison.Ordinal)
            && !probes.Contains(
                "M02_NORMALIZED_INTERRUPT_FRAME_PAGE_PROBE",
                StringComparison.Ordinal);
    }

    private static bool TestEfiEntryContract()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(
            Path.Combine(root, "src", "OS", "Boot", "EfiEntry.cs"));
        string probe = ExtractMethod(
            source,
            "private static bool WriteM02NormalizedInterruptFramePageProbe(");
        string resultMatcher = ExtractMethod(
            source,
            "private static bool M02NormalizedInterruptFramePageResultMatches(");
        string start = ExtractAsciiMarker(
            source,
            "private static void WriteM02NormalizedInterruptFramePageStartLine(");
        string ready = ExtractAsciiMarker(
            source,
            "private static void WriteM02NormalizedInterruptFramePageSuccessLine(");
        string pass = ExtractAsciiMarker(
            source,
            "private static void WriteM02NormalizedInterruptFramePagePassLine(");
        string[] failureLines = ExtractAsciiLines(
            source,
            "private static void WriteM02NormalizedInterruptFramePageFailureLines(");

        int consoleReady = source.IndexOf(
            "WriteEfiConsoleReady(console, serial);",
            StringComparison.Ordinal);
        int m04Span = source.IndexOf(
            "WriteM04SpanMemoryConformanceProbe(console, serial)",
            consoleReady,
            StringComparison.Ordinal);
        int m02Call = source.IndexOf(
            "WriteM02NormalizedInterruptFramePageProbe(",
            m04Span,
            StringComparison.Ordinal);
        int memoryMap = source.IndexOf(
            "TryCaptureUefiMemoryMap(systemTable)",
            m02Call,
            StringComparison.Ordinal);

        int startCall = probe.IndexOf(
            "WriteM02NormalizedInterruptFramePageStartLine(console, serial);",
            StringComparison.Ordinal);
        int allocate = probe.IndexOf(
            "bootServices->AllocatePages(",
            StringComparison.Ordinal);
        int materialize = probe.IndexOf(".Materialize(", StringComparison.Ordinal);
        int readBack = probe.IndexOf(".ValidateReadBack(", StringComparison.Ordinal);
        int free = probe.IndexOf(
            "bootServices->FreePages(",
            StringComparison.Ordinal);
        int releaseAudit = probe.IndexOf(
            "releaseStatus != 0",
            free,
            StringComparison.Ordinal);
        int readyCall = probe.IndexOf(
            "WriteM02NormalizedInterruptFramePageSuccessLine(console, serial);",
            releaseAudit,
            StringComparison.Ordinal);
        int passCall = probe.IndexOf(
            "WriteM02NormalizedInterruptFramePagePassLine(console, serial);",
            readyCall,
            StringComparison.Ordinal);

        string[] resultTokens =
        [
            "result.IsInitialized",
            "result.Operation == operation",
            ".NormalizeInterruptFramePageStatus.Success",
            "result.ModeledPageAddress == pageAddress",
            "result.CommonTargetAddress == commonTargetAddress",
            "result.PageCapacity ==",
            "result.RequiredBytes ==",
            "result.BytesWritten ==",
            "result.BytesCompared ==",
            "result.FirstMismatchOffset == -1",
            "result.HardwareErrorCodeVectorCount == 9",
            "result.SyntheticErrorCodeVectorCount == 247",
            "result.Fingerprint ==",
            "result.DestinationMutated == materialized",
            "result.ReadBackPerformed",
            "result.Materialized == materialized",
            "result.ReadBackValidated == !materialized",
            "result.Succeeded"
        ];
        string[] exactProbeTokens =
        [
            "M02NormalizedInterruptFrameAllocateAnyPages",
            "M02NormalizedInterruptFrameEfiLoaderData",
            "M02NormalizedInterruptFramePageCount",
            "pageAddress + M02NormalizedInterruptFramePageBytes",
            ".VectorCount == 256",
            ".SlotByteCount == 16",
            ".PageByteCount == 4096",
            ".PageAlignment == 4096",
            ".NormalizedSemanticSlotCount == 3",
            ".HardwareErrorCodeVectorCount == 9",
            ".SyntheticErrorCodeVectorCount == 247",
            "materializeResult.Fingerprint ==",
            "readBackResult.Fingerprint"
        ];
        string[] forbiddenProbeTokens =
        [
            "delegate*",
            "EfiLoaderCode",
            "LoadIdtr",
            "IdtGate",
            "ExecuteStub",
            "ExecuteSlot",
            "M02OwnedGdtTss",
            "SpinM02",
            "FaultProbe"
        ];

        return consoleReady >= 0
            && m04Span > consoleReady
            && m02Call > m04Span
            && memoryMap > m02Call
            && startCall >= 0
            && startCall < allocate
            && allocate < materialize
            && materialize < readBack
            && readBack < free
            && free < releaseAudit
            && releaseAudit < readyCall
            && readyCall < passCall
            && CountOccurrences(probe, "bootServices->AllocatePages(") == 1
            && CountOccurrences(probe, ".Materialize(") == 1
            && CountOccurrences(probe, ".ValidateReadBack(") == 1
            && CountOccurrences(probe, "bootServices->FreePages(") == 1
            && exactProbeTokens.All(token =>
                probe.Contains(token, StringComparison.Ordinal))
            && resultTokens.All(token =>
                resultMatcher.Contains(token, StringComparison.Ordinal))
            && forbiddenProbeTokens.All(token =>
                !probe.Contains(token, StringComparison.OrdinalIgnoreCase))
            && start == StartMarker
            && ready == ReadyMarker
            && pass == PassMarker
            && failureLines.SequenceEqual(
                [FailureMarker, FailureTestMarker],
                StringComparer.Ordinal);
    }

    private static bool TestFakeBootServicesContract()
    {
        FakeOutcome success = RunFakeWorkflow(FakeScenario.Success);
        FakeOutcome missingServices =
            RunFakeWorkflow(FakeScenario.MissingBootServices);
        FakeOutcome missingAllocate =
            RunFakeWorkflow(FakeScenario.MissingAllocatePages);
        FakeOutcome missingFree =
            RunFakeWorkflow(FakeScenario.MissingFreePages);
        FakeOutcome allocationFailure =
            RunFakeWorkflow(FakeScenario.AllocationFailure);
        FakeOutcome zeroAddress =
            RunFakeWorkflow(FakeScenario.ZeroAddress);
        FakeOutcome misaligned =
            RunFakeWorkflow(FakeScenario.MisalignedAddress);
        FakeOutcome nonCanonical =
            RunFakeWorkflow(FakeScenario.NonCanonicalAddress);
        FakeOutcome wrapping =
            RunFakeWorkflow(FakeScenario.WrappingAddress);
        FakeOutcome mismatch =
            RunFakeWorkflow(FakeScenario.ReadBackMismatch);
        FakeOutcome freeFailure =
            RunFakeWorkflow(FakeScenario.FreeFailure);
        FakeOutcome defaultOutcome = default;

        return success.Succeeded
            && success.ReadyEmitted
            && !success.FailureEmitted
            && success.AllocateCalls == 1
            && success.MaterializeCalls == 1
            && success.ReadBackCalls == 1
            && success.FreeCalls == 1
            && success.AllocateOrder < success.MaterializeOrder
            && success.MaterializeOrder < success.ReadBackOrder
            && success.ReadBackOrder < success.FreeOrder
            && success.Fingerprint == ExpectedFingerprint
            && success.PageSha256 == ExpectedPageSha256
            && success.Vector0 == "6A006A006800000000E9F20F00009090"
            && success.Vector8 == "6A016808000000E9740F000090909090"
            && success.Vector255 == "6A006A0068FF000000E9020000009090"
            && FailedBeforeAllocation(missingServices)
            && FailedBeforeAllocation(missingAllocate)
            && FailedBeforeAllocation(missingFree)
            && allocationFailure.AllocateCalls == 1
            && allocationFailure.FreeCalls == 0
            && allocationFailure.FailureEmitted
            && FailedOwnedInvalidAddress(zeroAddress)
            && FailedOwnedInvalidAddress(misaligned)
            && FailedOwnedInvalidAddress(nonCanonical)
            && FailedOwnedInvalidAddress(wrapping)
            && !mismatch.Succeeded
            && mismatch.MaterializeCalls == 1
            && mismatch.ReadBackCalls == 1
            && mismatch.FreeCalls == 1
            && mismatch.FailureEmitted
            && !mismatch.ReadyEmitted
            && !freeFailure.Succeeded
            && freeFailure.MaterializeCalls == 1
            && freeFailure.ReadBackCalls == 1
            && freeFailure.FreeCalls == 1
            && freeFailure.FailureEmitted
            && !freeFailure.ReadyEmitted
            && !defaultOutcome.Succeeded
            && !defaultOutcome.ReadyEmitted;
    }

    private static bool TestQemuProfilesAndHostileArguments()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        ScriptResult defaultProfile = RunQemuDryRun(root);
        ScriptResult endianProfile = RunQemuDryRun(
            root,
            "-BuildEfi",
            "-EnableM04IndependentEndianConformanceProbe");
        ScriptResult spanProfile = RunQemuDryRun(
            root,
            "-BuildEfi",
            "-EnableM04IndependentEndianConformanceProbe",
            "-EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe");
        ScriptResult m02010 = RunQemuDryRun(
            root,
            "-BuildEfi",
            "-EnableM02NormalizedInterruptFramePageProbe");
        ScriptResult missingBuild = RunQemuDryRun(
            root,
            "-EnableM02NormalizedInterruptFramePageProbe");
        ScriptResult skipImage = RunQemuDryRun(
            root,
            "-BuildEfi",
            "-SkipImageBuild",
            "-EnableM02NormalizedInterruptFramePageProbe");
        ScriptResult terminalCollision = RunQemuDryRun(
            root,
            "-BuildEfi",
            "-EnableM02OwnedGdtTssTerminalProbe",
            "-EnableM02NormalizedInterruptFramePageProbe");

        string[] ordered = MarkerList(m02010.Output, "ORDERED_MARKERS ");
        string[] unique = MarkerList(m02010.Output, "UNIQUE_MARKERS ");
        string[] forbidden = MarkerList(m02010.Output, "FORBIDDEN_MARKERS ");

        return defaultProfile.ExitCode == 0
            && MarkerList(defaultProfile.Output, "REQUIRED_MARKERS ").Length == 12
            && MarkerList(defaultProfile.Output, "ORDERED_MARKERS ").Length == 0
            && MarkerList(defaultProfile.Output, "UNIQUE_MARKERS ").Length == 0
            && MarkerList(defaultProfile.Output, "FORBIDDEN_MARKERS ").Length == 2
            && !defaultProfile.Output.Contains(ReadyMarker, StringComparison.Ordinal)
            && endianProfile.ExitCode == 0
            && MarkerList(endianProfile.Output, "REQUIRED_MARKERS ").Length == 15
            && MarkerList(endianProfile.Output, "ORDERED_MARKERS ").Length == 3
            && MarkerList(endianProfile.Output, "UNIQUE_MARKERS ").Length == 1
            && MarkerList(endianProfile.Output, "FORBIDDEN_MARKERS ").Length == 3
            && spanProfile.ExitCode == 0
            && MarkerList(spanProfile.Output, "REQUIRED_MARKERS ").Length == 16
            && MarkerList(spanProfile.Output, "ORDERED_MARKERS ").Length == 4
            && MarkerList(spanProfile.Output, "UNIQUE_MARKERS ").Length == 2
            && MarkerList(spanProfile.Output, "FORBIDDEN_MARKERS ").Length == 4
            && m02010.ExitCode == 0
            && MarkerList(m02010.Output, "REQUIRED_MARKERS ").Length == 17
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
            && FailsWith(
                missingBuild,
                "m02_normalized_interrupt_frame_page_probe_requires_build_efi")
            && FailsWith(
                skipImage,
                "m02_normalized_interrupt_frame_page_probe_forbids_skip_image_build")
            && FailsWith(
                terminalCollision,
                "m02_normalized_interrupt_frame_page_probe_forbids_owned_gdt_tss_terminal");
    }

    private static bool TestMarkerTranscriptRejection()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        ScriptResult profile = RunQemuDryRun(
            root,
            "-BuildEfi",
            "-EnableM02NormalizedInterruptFramePageProbe");
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

        List<string> duplicateLines = successLines.ToList();
        duplicateLines.Insert(duplicateLines.IndexOf(ReadyMarker) + 1, ReadyMarker);
        SerialMarkerCompletionResult duplicate = ParseTranscript(
            required,
            forbidden,
            ordered,
            unique,
            duplicateLines);

        List<string> failureLines = successLines.ToList();
        failureLines.Insert(failureLines.Count - 1, FailureMarker);
        SerialMarkerCompletionResult failure = ParseTranscript(
            required,
            forbidden,
            ordered,
            unique,
            failureLines);

        return success.Passed
            && success.OrderedSeenCount == 5
            && success.UniqueSeenCount == 3
            && missing.MissingRequiredMarkers is { Count: 1 }
            && missing.MissingRequiredMarkers[0] == ReadyMarker
            && !missing.Passed
            && reordered.OutOfOrderOccurrenceCount != 0
            && !reordered.Passed
            && duplicate.DuplicateUniqueOccurrenceCount != 0
            && !duplicate.Passed
            && failure.ForbiddenOccurrenceCount != 0
            && !failure.Passed;
    }

    private static FakeOutcome RunFakeWorkflow(FakeScenario scenario)
    {
        bool hasBootServices = scenario != FakeScenario.MissingBootServices;
        bool hasAllocate = scenario != FakeScenario.MissingAllocatePages;
        bool hasFree = scenario != FakeScenario.MissingFreePages;
        if (!hasBootServices || !hasAllocate || !hasFree)
        {
            return FakeOutcome.FailBeforeAllocation();
        }

        int sequence = 0;
        int allocateCalls = 1;
        int allocateOrder = ++sequence;
        ulong allocationStatus =
            scenario == FakeScenario.AllocationFailure ? 1UL : 0UL;
        bool ownsNativeAllocation = scenario is
            FakeScenario.Success or
            FakeScenario.ReadBackMismatch or
            FakeScenario.FreeFailure;
        void* nativeAllocation = null;
        ulong pageAddress;
        if (ownsNativeAllocation)
        {
            nativeAllocation = NativeMemory.AlignedAlloc(4096, 4096);
            if (nativeAllocation == null)
            {
                throw new OutOfMemoryException(
                    "Could not allocate the fake EFI page.");
            }

            pageAddress = (ulong)nativeAllocation;
        }
        else
        {
            pageAddress = scenario switch
            {
                FakeScenario.MisalignedAddress => 0x1001UL,
                FakeScenario.NonCanonicalAddress => 0x0000_8000_0000_0000UL,
                FakeScenario.WrappingAddress => 0xffff_ffff_ffff_f000UL,
                _ => 0UL
            };
        }

        try
        {
            bool allocationSucceeded = allocationStatus == 0;
            if (!allocationSucceeded)
            {
                return new FakeOutcome(
                    false,
                    false,
                    true,
                    allocateCalls,
                    0,
                    0,
                    0,
                    allocateOrder,
                    0,
                    0,
                    0,
                    0,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty);
            }

            bool addressValid =
                pageAddress != 0 &&
                (pageAddress & 4095UL) == 0 &&
                NormalizeErrorCodeNonErrorCodeInterruptFrame
                    .IsCanonicalX64Address(pageAddress) &&
                pageAddress <= ~0UL - 4096UL;
            ulong commonTargetAddress =
                addressValid ? pageAddress + 4096UL : 0;
            addressValid =
                addressValid &&
                NormalizeErrorCodeNonErrorCodeInterruptFrame
                    .IsCanonicalX64Address(pageAddress + 4096UL - 1UL) &&
                NormalizeErrorCodeNonErrorCodeInterruptFrame
                    .IsCanonicalX64Address(commonTargetAddress);

            int materializeCalls = 0;
            int readBackCalls = 0;
            int materializeOrder = 0;
            int readBackOrder = 0;
            bool resultsMatch = false;
            ulong fingerprint = 0;
            string pageSha256 = string.Empty;
            string vector0 = string.Empty;
            string vector8 = string.Empty;
            string vector255 = string.Empty;
            if (addressValid)
            {
                materializeCalls++;
                materializeOrder = ++sequence;
                NormalizeInterruptFramePageResult materialized =
                    NormalizeErrorCodeNonErrorCodeInterruptFrame.Materialize(
                        (byte*)pageAddress,
                        4096,
                        pageAddress,
                        commonTargetAddress);
                if (scenario == FakeScenario.ReadBackMismatch)
                {
                    ((byte*)pageAddress)[0] ^= 0xff;
                }

                readBackCalls++;
                readBackOrder = ++sequence;
                NormalizeInterruptFramePageResult readBack =
                    NormalizeErrorCodeNonErrorCodeInterruptFrame.ValidateReadBack(
                        (byte*)pageAddress,
                        4096,
                        pageAddress,
                        commonTargetAddress);
                resultsMatch =
                    FakeResultMatches(
                        materialized,
                        NormalizeInterruptFramePageOperation.Materialize,
                        pageAddress,
                        commonTargetAddress,
                        true) &&
                    FakeResultMatches(
                        readBack,
                        NormalizeInterruptFramePageOperation.ValidateReadBack,
                        pageAddress,
                        commonTargetAddress,
                        false) &&
                    materialized.Fingerprint == readBack.Fingerprint;
                fingerprint = readBack.Fingerprint;
                if (resultsMatch)
                {
                    ReadOnlySpan<byte> page = new((void*)pageAddress, 4096);
                    pageSha256 = Convert.ToHexString(SHA256.HashData(page));
                    vector0 = Convert.ToHexString(page[..16]);
                    vector8 = Convert.ToHexString(page.Slice(8 * 16, 16));
                    vector255 = Convert.ToHexString(page.Slice(255 * 16, 16));
                }
            }

            int freeCalls = 1;
            int freeOrder = ++sequence;
            ulong freeStatus = scenario == FakeScenario.FreeFailure ? 1UL : 0UL;
            bool succeeded = addressValid && resultsMatch && freeStatus == 0;
            return new FakeOutcome(
                succeeded,
                succeeded,
                !succeeded,
                allocateCalls,
                materializeCalls,
                readBackCalls,
                freeCalls,
                allocateOrder,
                materializeOrder,
                readBackOrder,
                freeOrder,
                fingerprint,
                pageSha256,
                vector0,
                vector8,
                vector255);
        }
        finally
        {
            if (nativeAllocation != null)
            {
                NativeMemory.AlignedFree(nativeAllocation);
            }
        }
    }

    private static bool FakeResultMatches(
        NormalizeInterruptFramePageResult result,
        NormalizeInterruptFramePageOperation operation,
        ulong pageAddress,
        ulong commonTargetAddress,
        bool materialized)
    {
        return result.IsInitialized
            && result.Operation == operation
            && result.Status == NormalizeInterruptFramePageStatus.Success
            && result.ModeledPageAddress == pageAddress
            && result.CommonTargetAddress == commonTargetAddress
            && result.PageCapacity == 4096
            && result.RequiredBytes == 4096
            && result.BytesWritten == (materialized ? 4096 : 0)
            && result.BytesCompared == 4096
            && result.FirstMismatchOffset == -1
            && result.HardwareErrorCodeVectorCount == 9
            && result.SyntheticErrorCodeVectorCount == 247
            && result.Fingerprint == ExpectedFingerprint
            && result.DestinationMutated == materialized
            && result.ReadBackPerformed
            && result.Materialized == materialized
            && result.ReadBackValidated == !materialized
            && result.Succeeded;
    }

    private static bool FailedBeforeAllocation(FakeOutcome outcome)
    {
        return !outcome.Succeeded
            && !outcome.ReadyEmitted
            && outcome.FailureEmitted
            && outcome.AllocateCalls == 0
            && outcome.MaterializeCalls == 0
            && outcome.ReadBackCalls == 0
            && outcome.FreeCalls == 0;
    }

    private static bool FailedOwnedInvalidAddress(FakeOutcome outcome)
    {
        return !outcome.Succeeded
            && !outcome.ReadyEmitted
            && outcome.FailureEmitted
            && outcome.AllocateCalls == 1
            && outcome.MaterializeCalls == 0
            && outcome.ReadBackCalls == 0
            && outcome.FreeCalls == 1
            && outcome.AllocateOrder < outcome.FreeOrder;
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
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-NonInteractive");
        start.ArgumentList.Add("-ExecutionPolicy");
        start.ArgumentList.Add("Bypass");
        start.ArgumentList.Add("-File");
        start.ArgumentList.Add(script);
        start.ArgumentList.Add("-DryRun");
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
            throw new TimeoutException("M02-010 QEMU dry run timed out.");
        }

        return new ScriptResult(
            process.ExitCode,
            stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult());
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

    private enum FakeScenario
    {
        Success,
        MissingBootServices,
        MissingAllocatePages,
        MissingFreePages,
        AllocationFailure,
        ZeroAddress,
        MisalignedAddress,
        NonCanonicalAddress,
        WrappingAddress,
        ReadBackMismatch,
        FreeFailure
    }

    private readonly record struct FakeOutcome(
        bool Succeeded,
        bool ReadyEmitted,
        bool FailureEmitted,
        int AllocateCalls,
        int MaterializeCalls,
        int ReadBackCalls,
        int FreeCalls,
        int AllocateOrder,
        int MaterializeOrder,
        int ReadBackOrder,
        int FreeOrder,
        ulong Fingerprint,
        string PageSha256,
        string Vector0,
        string Vector8,
        string Vector255)
    {
        public static FakeOutcome FailBeforeAllocation()
        {
            return new FakeOutcome(
                false,
                false,
                true,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty);
        }
    }

    private readonly record struct ScriptResult(int ExitCode, string Output);
}
