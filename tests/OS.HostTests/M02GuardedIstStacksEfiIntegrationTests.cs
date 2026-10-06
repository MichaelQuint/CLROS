using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

internal static class M02GuardedIstStacksEfiIntegrationTests
{
    private const string TestName = "m02.008.efi.integration";
    private const string ReadyMarker =
        "CLROS_M02_008_READY stacks=3 guards=3 doubleFaultIst=1 " +
        "pageFaultIst=2 nmiIst=3 writableMetadata=true " +
        "executableMetadata=false allocationFree=true " +
        "guestArenaAllocated=true guestStackWrites=true " +
        "guestGuardMappings=false guestTss=false guestIdt=false " +
        "arenaFreed=true markerTransport=uefiSerial " +
        "postExitBootServices=false";
    private const string FailureMarker =
        "M02_GUARDED_IST_STACK_ALLOCATION_FAILED";
    private const string CompileClosureSha256 =
        "46c8170ce64d3fc211445ec7ba8cff22161a0435aeff60eb43b31080a0a4076c";
    private const string ProjectFileSha256 =
        "b57007be8a437e884ff3f0aead627c20ddcef0c51de06377b7af21ff50e2e7e1";
    private const string CanonicalProjectShapeSha256 =
        "729187018d532291b5564818f9b158a5bc9a99d5cbff937488c5c1493f28a9b6";

    private static readonly (string Path, string Sha256)[] ImmutableReviewedInputs =
    [
        (
            "src/OS.Kernel/Diagnostics/GuardedISTStacksDoubleFaultPageFaultNMI.cs",
            "cb3deb08256d85ec85cc017aa30938498705ae65956d69235993a411e4fb5703"),
        (
            "tests/OS.HostTests/GuardedISTStacksDoubleFaultPageFaultNMITests.cs",
            "dd82e2462b84effa3f3ec03fc3b64ee8916b0913c6cdb34bac2f3fe276f98f00")
    ];

    [ModuleInitializer]
    internal static void RunAll()
    {
        Console.WriteLine("TEST_START " + TestName);
        int failures = 0;

        RunCase(
            "m02.008 EFI primitive and exact source closure are explicit",
            TestPrimitiveAndCompileClosure,
            ref failures);
        RunCase(
            "m02.008 EFI allocates validates touches releases and frees in order",
            TestGuestArenaSequence,
            ref failures);
        RunCase(
            "m02.008 EFI markers are exact mutually exclusive and pre-firmware-exit",
            TestMarkerEncodingAndFailureGate,
            ref failures);
        RunCase(
            "m02.008 QEMU profile is cumulative exact and fail closed",
            TestQemuProfileSource,
            ref failures);
        RunCase(
            "m02.008 immutable inputs remain exact and documentation boundaries remain semantic",
            TestImmutableInputsAndDocumentationBoundaries,
            ref failures);

        if (failures != 0)
        {
            Console.WriteLine(
                "TEST_FAIL " + TestName + " host-contract failures=" + failures);
            throw new InvalidOperationException(
                "M02-008 EFI integration host tests failed: " + failures);
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

    private static bool TestPrimitiveAndCompileClosure()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string runtime = File.ReadAllText(
            Path.Combine(root, "src", "OS", "Runtime", "MinimalRuntime.cs"));
        MatchCollection maxValueDeclarations = Regex.Matches(
            runtime,
            @"public\s+const\s+ulong\s+MaxValue\s*=\s*0xffff_ffff_ffff_ffffUL\s*;",
            RegexOptions.CultureInvariant);

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

        const string GdtKey =
            @"..\OS.Kernel\Diagnostics\OwnedX64GDTContainingNullN64BitCode.cs|Diagnostics\OwnedX64GDTContainingNullN64BitCode.cs";
        const string GuardedIstKey =
            @"..\OS.Kernel\Diagnostics\GuardedISTStacksDoubleFaultPageFaultNMI.cs|Diagnostics\GuardedISTStacksDoubleFaultPageFaultNMI.cs";
        const string TerminalKey =
            @"..\OS.Kernel\Diagnostics\OwnedGDTTSSGeneratedAuditedCSharpStubBytes.cs|Diagnostics\OwnedGDTTSSGeneratedAuditedCSharpStubBytes.cs";
        const string NormalizedInterruptFrameKey =
            @"..\OS.Kernel\Diagnostics\NormalizeErrorCodeNonErrorCodeInterruptFrame.cs|Diagnostics\NormalizeErrorCodeNonErrorCodeInterruptFrame.cs";
        const string EndianKey =
            @"..\..\std\no-runtime\shared\IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMOSA.cs|Std\IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMOSA.cs";
        int gdtIndex = Array.IndexOf(compileKeys, GdtKey);
        int guardedIstIndex = Array.IndexOf(compileKeys, GuardedIstKey);
        int terminalIndex = Array.IndexOf(compileKeys, TerminalKey);
        int normalizedInterruptFrameIndex =
            Array.IndexOf(compileKeys, NormalizedInterruptFrameKey);
        int endianIndex = Array.IndexOf(compileKeys, EndianKey);

        XElement[] defaults = project
            .Descendants("EnableM02GuardedIstStackAllocationProbe")
            .Where(element => element.Parent?.Name.LocalName == "PropertyGroup")
            .ToArray();
        XElement[] symbolGroups = project
            .Descendants("PropertyGroup")
            .Where(element =>
                element.Attribute("Condition")?.Value ==
                    "'$(EnableM02GuardedIstStackAllocationProbe)' == 'true'")
            .ToArray();
        string[] dependencyConditions = project
            .Descendants("Error")
            .Select(element => element.Attribute("Condition")?.Value ?? string.Empty)
            .Where(value => value.StartsWith(
                "'$(EnableM02GuardedIstStackAllocationProbe)' == 'true'",
                StringComparison.Ordinal))
            .ToArray();

        string buildScript = File.ReadAllText(
            Path.Combine(root, "scripts", "build-efi-shell.ps1"));
        string normalizedBuildScript = buildScript.Replace(
            "\r\n",
            "\n",
            StringComparison.Ordinal);
        string verifierScript = File.ReadAllText(
            Path.Combine(root, "scripts", "test-efi-artifact.ps1"));

        return maxValueDeclarations.Count == 1
            && runtime.Contains("public struct UInt64", StringComparison.Ordinal)
            && compileItems.Length == 112
            && compileHash == CompileClosureSha256
            && projectHash == ProjectFileSha256
            && gdtIndex >= 0
            && guardedIstIndex == gdtIndex + 1
            && terminalIndex == guardedIstIndex + 1
            && normalizedInterruptFrameIndex == terminalIndex + 1
            && endianIndex == normalizedInterruptFrameIndex + 17
            && compileKeys.Count(key => key == GuardedIstKey) == 1
            && defaults.Length == 1
            && defaults[0].Value == "false"
            && defaults[0].Attribute("Condition")?.Value ==
                "'$(EnableM02GuardedIstStackAllocationProbe)' == ''"
            && symbolGroups.Length == 1
            && symbolGroups[0].Elements("DefineConstants").Single().Value ==
                "$(DefineConstants);M02_GUARDED_IST_STACK_ALLOCATION_PROBE"
            && dependencyConditions.Length == 15
            && dependencyConditions.Contains(
                "'$(EnableM02GuardedIstStackAllocationProbe)' == 'true' and '$(EnableM04GcStaticRootMaterializationProbe)' == 'true'",
                StringComparer.Ordinal)
            && dependencyConditions.Contains(
                "'$(EnableM02GuardedIstStackAllocationProbe)' == 'true' and '$(EnableM03TypedExitBootServicesCallProbe)' == 'true'",
                StringComparer.Ordinal)
            && dependencyConditions.Contains(
                "'$(EnableM02GuardedIstStackAllocationProbe)' == 'true' and '$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true'",
                StringComparer.Ordinal)
            && dependencyConditions.Contains(
                "'$(EnableM02GuardedIstStackAllocationProbe)' == 'true' and '$(EnableM04CheckedReferenceBarrierProbe)' == 'true'",
                StringComparer.Ordinal)
            && dependencyConditions.Contains(
                "'$(EnableM02GuardedIstStackAllocationProbe)' == 'true' and '$(EnableM03MemoryMapCaptureProbe)' == 'true'",
                StringComparer.Ordinal)
            && dependencyConditions.Contains(
                "'$(EnableM02GuardedIstStackAllocationProbe)' == 'true' and '$(EnableM03FirmwareEntropySeedProbe)' == 'true'",
                StringComparer.Ordinal)
            && dependencyConditions.Contains(
                "'$(EnableM02GuardedIstStackAllocationProbe)' == 'true' and '$(EnableM02UnexpectedVectorPanicProbe)' == 'true'",
                StringComparer.Ordinal)
            && dependencyConditions.Contains(
                "'$(EnableM02GuardedIstStackAllocationProbe)' == 'true' and '$(EnableM04SzArrayAllocationProbe)' == 'true'",
                StringComparer.Ordinal)
            && dependencyConditions.Contains(
                "'$(EnableM02GuardedIstStackAllocationProbe)' == 'true' and '$(EnableM04StringAllocationProbe)' == 'true'",
                StringComparer.Ordinal)
            && dependencyConditions.Contains(
                "'$(EnableM02GuardedIstStackAllocationProbe)' == 'true' and '$(EnableM04StringAllocationBeforeBindProbe)' == 'true'",
                StringComparer.Ordinal)
            && dependencyConditions.Contains(
                "'$(EnableM02GuardedIstStackAllocationProbe)' == 'true' and '$(EnableM04StringAllocationExhaustionProbe)' == 'true'",
                StringComparer.Ordinal)
            && dependencyConditions.Contains(
                "'$(EnableM02GuardedIstStackAllocationProbe)' == 'true' and '$(EnableM04StringAllocationInvalidLengthProbe)' == 'true'",
                StringComparer.Ordinal)
            && dependencyConditions.Contains(
                "'$(EnableM02GuardedIstStackAllocationProbe)' == 'true' and '$(EnableM04SzArrayAllocationBeforeBindProbe)' == 'true'",
                StringComparer.Ordinal)
            && dependencyConditions.Contains(
                "'$(EnableM02GuardedIstStackAllocationProbe)' == 'true' and '$(EnableM04SzArrayAllocationExhaustionProbe)' == 'true'",
                StringComparer.Ordinal)
            && dependencyConditions.Contains(
                "'$(EnableM02GuardedIstStackAllocationProbe)' == 'true' and '$(EnableM02OwnedX64GdtEncodingProbe)' != 'true'",
                StringComparer.Ordinal)
            && buildScript.Contains(
                "[switch] $EnableM02GuardedIstStackAllocationProbe",
                StringComparison.Ordinal)
            && buildScript.Contains(
                "-p:EnableM02GuardedIstStackAllocationProbe=true",
                StringComparison.Ordinal)
            && buildScript.Contains(
                "guarded_ist_stack_allocation_requires_owned_x64_gdt_encoding",
                StringComparison.Ordinal)
            && buildScript.Contains(
                "EFI_PUBLISH_SOURCE_AUTHORITY status=ok inputs=126",
                StringComparison.Ordinal)
            && buildScript.Contains(
                CanonicalProjectShapeSha256,
                StringComparison.Ordinal)
            && buildScript.Contains(GuardedIstKey, StringComparison.Ordinal)
            && verifierScript.Contains(
                "$expectedSourceClosureInputCount = 126",
                StringComparison.Ordinal)
            && verifierScript.Contains(
                "$compileItems.Count -ne 112",
                StringComparison.Ordinal)
            && verifierScript.Contains(GuardedIstKey, StringComparison.Ordinal)
            && verifierScript.Contains(
                "[switch]$EnableM02GuardedIstStackAllocationProbe",
                StringComparison.Ordinal)
            && verifierScript.Contains(
                "-p:EnableM02GuardedIstStackAllocationProbe=true",
                StringComparison.Ordinal)
            && normalizedBuildScript.Contains(
                "$verificationArguments += @(\n" +
                "            \"-EnableM02GuardedIstStackAllocationProbe\",",
                StringComparison.Ordinal);
    }

    private static bool TestGuestArenaSequence()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(
            Path.Combine(root, "src", "OS", "Boot", "EfiEntry.cs"));
        int priorMarker = source.IndexOf(
            "WriteM02OwnedX64GdtEncodingSuccessLine(",
            StringComparison.Ordinal);
        int switchStart = IndexOfAfter(
            source,
            "#if M02_GUARDED_IST_STACK_ALLOCATION_PROBE",
            priorMarker);
        int switchEnd = IndexOfAfter(source, "#endif", switchStart);
        if (switchStart < 0 || switchEnd <= switchStart)
        {
            return false;
        }

        string probe = source[switchStart..switchEnd];
        int bootServicesGate = probe.IndexOf(
            "bootServices == null ||",
            StringComparison.Ordinal);
        int allocatePages = IndexOfAfter(
            probe,
            "bootServices->AllocatePages(",
            bootServicesGate);
        int request = IndexOfAfter(
            probe,
            "new OS.Kernel.Diagnostics\n                            .GuardedIstStackArenaRequest(",
            allocatePages);
        if (request < 0)
        {
            request = IndexOfAfter(
                probe,
                ".GuardedIstStackArenaRequest(",
                allocatePages);
        }
        int initialize = IndexOfAfter(probe, ".Initialize(", request);
        int allocate = IndexOfAfter(probe, ".Allocate(ref m02GuardedState);", initialize);
        int layoutGate = IndexOfAfter(
            probe,
            "bool m02GuardedLayoutValid =",
            allocate);
        int firstStackWrite = IndexOfAfter(
            probe,
            "m02GuardedDoubleFaultBytes[0] = 0xD1;",
            layoutGate);
        int readBack = IndexOfAfter(
            probe,
            "m02GuardedStackReadBack =",
            firstStackWrite);
        int release = IndexOfAfter(
            probe,
            ".Release(",
            readBack);
        int freePages = IndexOfAfter(
            probe,
            "bootServices->FreePages(",
            release);
        int finalFailureGate = IndexOfAfter(
            probe,
            "if (!m02GuardedLayoutValid ||",
            freePages);
        int success = IndexOfAfter(
            probe,
            "WriteM02GuardedIstStackAllocationSuccessLine(",
            finalFailureGate);

        return source.Contains(
                "EfiBootServices* bootServices,\n            int lineIndex)",
                StringComparison.Ordinal) ||
            source.Contains(
                "EfiBootServices* bootServices,\r\n            int lineIndex)",
                StringComparison.Ordinal)
            ? bootServicesGate >= 0
                && allocatePages > bootServicesGate
                && request > allocatePages
                && initialize > request
                && allocate > initialize
                && layoutGate > allocate
                && firstStackWrite > layoutGate
                && readBack > firstStackWrite
                && release > readBack
                && freePages > release
                && finalFailureGate > freePages
                && success > finalFailureGate
                && probe.Contains(
                    "const ulong m02GuardedPageCount = 6;",
                    StringComparison.Ordinal)
                && probe.Contains(
                    "const ulong m02GuardedPageSize = 4096;",
                    StringComparison.Ordinal)
                && probe.Contains("EfiLoaderData", StringComparison.Ordinal)
                && probe.Contains(
                    "0x004D30322D303038UL",
                    StringComparison.Ordinal)
                && probe.Contains(
                    "stackWritable: true",
                    StringComparison.Ordinal)
                && probe.Contains(
                    "stackExecutable: false",
                    StringComparison.Ordinal)
                && probe.Contains(
                    "m02GuardedDoubleFault.IstIndex == 1",
                    StringComparison.Ordinal)
                && probe.Contains(
                    "m02GuardedPageFault.IstIndex == 2",
                    StringComparison.Ordinal)
                && probe.Contains(
                    "m02GuardedNmi.IstIndex == 3",
                    StringComparison.Ordinal)
                && probe.Contains(
                    "!m02GuardedDoubleFault.GuardPagePresent",
                    StringComparison.Ordinal)
                && probe.Contains(
                    "!m02GuardedPageFault.GuardPagePresent",
                    StringComparison.Ordinal)
                && probe.Contains(
                    "!m02GuardedNmi.GuardPagePresent",
                    StringComparison.Ordinal)
                && probe.Contains(
                    "m02GuardedDoubleFaultBytes[m02GuardedPageSize - 1]",
                    StringComparison.Ordinal)
                && probe.Contains(
                    "m02GuardedPageFaultBytes[m02GuardedPageSize - 1]",
                    StringComparison.Ordinal)
                && probe.Contains(
                    "m02GuardedNmiBytes[m02GuardedPageSize - 1]",
                    StringComparison.Ordinal)
                && !probe.Contains(
                    "(byte*)m02GuardedDoubleFault.GuardBaseAddress",
                    StringComparison.Ordinal)
                && !probe.Contains(
                    "(byte*)m02GuardedPageFault.GuardBaseAddress",
                    StringComparison.Ordinal)
                && !probe.Contains(
                    "(byte*)m02GuardedNmi.GuardBaseAddress",
                    StringComparison.Ordinal)
                && CountOccurrences(probe, ".Initialize(") == 1
                && CountOccurrences(probe, ".Allocate(ref m02GuardedState);") == 1
                && CountOccurrences(probe, ".Release(") == 1
                && CountOccurrences(probe, "m02GuardedStackReadBack =") == 2
            : false;
    }

    private static bool TestMarkerEncodingAndFailureGate()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(
            Path.Combine(root, "src", "OS", "Boot", "EfiEntry.cs"));
        string normalizedSource = source.Replace(
            "\r\n",
            "\n",
            StringComparison.Ordinal);
        string reconstructedReady = ExtractAsciiMarker(
            normalizedSource,
            "private static void\n            WriteM02GuardedIstStackAllocationSuccessLine(");
        string reconstructedFailure = ExtractAsciiMarker(
            normalizedSource,
            "private static void\n            WriteM02GuardedIstStackAllocationFailureLine(");

        int finalGate = source.IndexOf(
            "if (!m02GuardedLayoutValid ||",
            StringComparison.Ordinal);
        int failureCall = IndexOfAfter(
            source,
            "WriteM02GuardedIstStackAllocationFailureLine(",
            finalGate);
        int abortReturn = IndexOfAfter(source, "return;", failureCall);
        int successCall = IndexOfAfter(
            source,
            "WriteM02GuardedIstStackAllocationSuccessLine(",
            abortReturn);

        return reconstructedReady == ReadyMarker
            && reconstructedFailure == FailureMarker
            && ReadyMarker.Contains(
                "guestGuardMappings=false",
                StringComparison.Ordinal)
            && ReadyMarker.Contains("guestTss=false", StringComparison.Ordinal)
            && ReadyMarker.Contains("guestIdt=false", StringComparison.Ordinal)
            && ReadyMarker.Contains("arenaFreed=true", StringComparison.Ordinal)
            && ReadyMarker.Contains(
                "postExitBootServices=false",
                StringComparison.Ordinal)
            && !ReadyMarker.Contains("CloudReady", StringComparison.OrdinalIgnoreCase)
            && !ReadyMarker.Contains("production", StringComparison.OrdinalIgnoreCase)
            && finalGate >= 0
            && failureCall > finalGate
            && abortReturn > failureCall
            && successCall > abortReturn
            && CountOccurrences(
                source,
                "WriteM02GuardedIstStackAllocationSuccessLine(") == 2
            && CountOccurrences(
                source,
                "WriteM02GuardedIstStackAllocationFailureLine(") == 4
            && !source.Contains("\"" + ReadyMarker + "\"", StringComparison.Ordinal)
            && !source.Contains("\"" + FailureMarker + "\"", StringComparison.Ordinal);
    }

    private static bool TestQemuProfileSource()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(
            Path.Combine(root, "scripts", "run-qemu-smoke.ps1"));
        int terminalProfile = source.IndexOf(
            "if ($EnableM02OwnedGdtTssTerminalProbe) {",
            StringComparison.Ordinal);
        int guardedProfile = IndexOfAfter(
            source,
            "elseif ($EnableM02GuardedIstStackAllocationProbe) {",
            terminalProfile);
        int gdtProfile = IndexOfAfter(
            source,
            "elseif ($EnableM02OwnedX64GdtEncodingProbe) {",
            guardedProfile);
        string profile = guardedProfile >= 0 && gdtProfile > guardedProfile
            ? source[guardedProfile..gdtProfile]
            : string.Empty;

        return source.Contains(
                "[switch] $EnableM02GuardedIstStackAllocationProbe",
                StringComparison.Ordinal)
            && source.Contains(
                "$script:M02GuardedIstStackAllocationReadyMarker =",
                StringComparison.Ordinal)
            && source.Contains(ReadyMarker, StringComparison.Ordinal)
            && source.Contains(FailureMarker, StringComparison.Ordinal)
            && source.Contains(
                "$script:M02GuardedIstStackAllocationRequiredMarkers",
                StringComparison.Ordinal)
            && source.Contains(
                "$script:M02GuardedIstStackAllocationForbiddenMarkers",
                StringComparison.Ordinal)
            && source.Contains(
                "$marker -eq $script:M02OwnedX64GdtEncodingReadyMarker",
                StringComparison.Ordinal)
            && source.Contains(
                "m02_guarded_ist_stack_allocation_probe_requires_owned_x64_gdt_encoding",
                StringComparison.Ordinal)
            && source.Contains(
                "m02_guarded_ist_stack_allocation_probe_requires_build_efi",
                StringComparison.Ordinal)
            && source.Contains(
                "m02_guarded_ist_stack_allocation_probe_forbids_skip_image_build",
                StringComparison.Ordinal)
            && source.Contains(
                "-EnableM02GuardedIstStackAllocationProbe",
                StringComparison.Ordinal)
            && source.Contains(
                "profileRequired=56 profileForbidden=40",
                StringComparison.Ordinal)
            && source.Contains(
                "profileRequired=55 profileForbidden=39",
                StringComparison.Ordinal)
            && CountOccurrences(
                profile,
                "Merge-QemuSmokeMarkerList -Values @(") == 4
            && terminalProfile >= 0
            && guardedProfile >= 0
            && gdtProfile > guardedProfile;
    }

    private static bool TestImmutableInputsAndDocumentationBoundaries()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        foreach ((string relativePath, string expectedSha256) in ImmutableReviewedInputs)
        {
            string path = Path.Combine(
                root,
                relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path))
            {
                return false;
            }

            string actual = Convert.ToHexString(
                    SHA256.HashData(File.ReadAllBytes(path)))
                .ToLowerInvariant();
            if (actual != expectedSha256)
            {
                return false;
            }
        }

        string diagnostics = File.ReadAllText(
            Path.Combine(root, "docs", "diagnostics.md"));
        string bootOrder = File.ReadAllText(
            Path.Combine(root, "docs", "boot-order.md"));
        if (!HasRequiredDocumentationBoundaries(diagnostics, bootOrder))
        {
            return false;
        }

        string fixtureRoot = Path.Combine(
            Path.GetTempPath(),
            "clros-m02-008-documentation-boundaries-" + Guid.NewGuid().ToString("N"));
        bool hostileFixturesRejected;
        try
        {
            Directory.CreateDirectory(fixtureRoot);
            hostileFixturesRejected =
                WriteHostileFixtureAndRequireRejection(
                    fixtureRoot,
                    "diagnostics-missing-issue-identity",
                    diagnostics.Replace(
                        "M02-008 adds a caller-owned",
                        "A removed issue adds a caller-owned",
                        StringComparison.Ordinal),
                    bootOrder) &&
                WriteHostileFixtureAndRequireRejection(
                    fixtureRoot,
                    "diagnostics-missing-three-guard-intent",
                    diagnostics.Replace(
                        "one non-present guard range followed",
                        "one unspecified range followed",
                        StringComparison.Ordinal),
                    bootOrder) &&
                WriteHostileFixtureAndRequireRejection(
                    fixtureRoot,
                    "diagnostics-missing-three-stack-intent",
                    diagnostics.Replace(
                        "three stack ranges",
                        "an unspecified set of stack ranges",
                        StringComparison.Ordinal),
                    bootOrder) &&
                WriteHostileFixtureAndRequireRejection(
                    fixtureRoot,
                    "diagnostics-missing-loaderdata-evidence",
                    diagnostics.Replace(
                        "temporary six-page UEFI",
                        "temporary unspecified",
                        StringComparison.Ordinal),
                    bootOrder) &&
                WriteHostileFixtureAndRequireRejection(
                    fixtureRoot,
                    "diagnostics-missing-stack-only-readback",
                    diagnostics.Replace(
                        "writes and reads back only the",
                        "touches an unspecified set of",
                        StringComparison.Ordinal),
                    bootOrder) &&
                WriteHostileFixtureAndRequireRejection(
                    fixtureRoot,
                    "diagnostics-missing-release-free-boundary",
                    diagnostics.Replace(
                        "releases the lease, and frees the same arena before return",
                        "retains the lease and leaves cleanup unspecified",
                        StringComparison.Ordinal),
                    bootOrder) &&
                WriteHostileFixtureAndRequireRejection(
                    fixtureRoot,
                    "diagnostics-missing-capability-negation",
                    Regex.Replace(
                        diagnostics,
                        @"The proof does\s+not change page tables",
                        "The proof does change page tables",
                        RegexOptions.CultureInvariant),
                    bootOrder) &&
                WriteHostileFixtureAndRequireRejection(
                    fixtureRoot,
                    "diagnostics-missing-guard-nonclaim",
                    diagnostics.Replace(
                        "map guards non-present",
                        "activate guard mappings",
                        StringComparison.Ordinal),
                    bootOrder) &&
                WriteHostileFixtureAndRequireRejection(
                    fixtureRoot,
                    "diagnostics-missing-tss-nonclaim",
                    diagnostics.Replace(
                        "write TSS IST slots",
                        "install live TSS IST slots",
                        StringComparison.Ordinal),
                    bootOrder) &&
                WriteHostileFixtureAndRequireRejection(
                    fixtureRoot,
                    "diagnostics-missing-idt-nonclaim",
                    diagnostics.Replace(
                        "descriptor tables",
                        "active descriptor state",
                        StringComparison.Ordinal),
                    bootOrder) &&
                WriteHostileFixtureAndRequireRejection(
                    fixtureRoot,
                    "diagnostics-missing-fault-nonclaim",
                    diagnostics.Replace(
                        "execute a fault",
                        "exercise live fault behavior",
                        StringComparison.Ordinal),
                    bootOrder) &&
                WriteHostileFixtureAndRequireRejection(
                    fixtureRoot,
                    "diagnostics-missing-exitbootservices-nonclaim",
                    diagnostics.Replace(
                        "cross real",
                        "complete a live",
                        StringComparison.Ordinal),
                    bootOrder) &&
                WriteHostileFixtureAndRequireRejection(
                    fixtureRoot,
                    "boot-order-missing-issue-identity",
                    diagnostics,
                    bootOrder.Replace(
                        "M02-008 adds a host-tested",
                        "A removed issue adds a host-tested",
                        StringComparison.Ordinal)) &&
                WriteHostileFixtureAndRequireRejection(
                    fixtureRoot,
                    "boot-order-missing-three-pair-intent",
                    diagnostics,
                    bootOrder.Replace(
                        "three adjacent",
                        "an unspecified number of adjacent",
                        StringComparison.Ordinal)) &&
                WriteHostileFixtureAndRequireRejection(
                    fixtureRoot,
                    "boot-order-missing-loaderdata-evidence",
                    diagnostics,
                    bootOrder.Replace(
                        "temporary six-page LoaderData allocation",
                        "temporary allocation",
                        StringComparison.Ordinal)) &&
                WriteHostileFixtureAndRequireRejection(
                    fixtureRoot,
                    "boot-order-missing-stack-only-readback",
                    diagnostics,
                    bootOrder.Replace(
                        "stack-only write/readback",
                        "unspecified memory access",
                        StringComparison.Ordinal)) &&
                WriteHostileFixtureAndRequireRejection(
                    fixtureRoot,
                    "boot-order-missing-release-boundary",
                    diagnostics,
                    bootOrder.Replace(
                        "lease release",
                        "lease retention",
                        StringComparison.Ordinal)) &&
                WriteHostileFixtureAndRequireRejection(
                    fixtureRoot,
                    "boot-order-missing-same-allocation-free-boundary",
                    diagnostics,
                    bootOrder.Replace(
                        "same-base",
                        "unbound",
                        StringComparison.Ordinal)) &&
                WriteHostileFixtureAndRequireRejection(
                    fixtureRoot,
                    "boot-order-missing-guard-nonclaim",
                    diagnostics,
                    bootOrder.Replace(
                        "no guard mapping",
                        "active guard mapping",
                        StringComparison.Ordinal)) &&
                WriteHostileFixtureAndRequireRejection(
                    fixtureRoot,
                    "boot-order-missing-tss-idt-nonclaim",
                    diagnostics,
                    bootOrder.Replace(
                        "TSS/IDT installation",
                        "active descriptor installation",
                        StringComparison.Ordinal)) &&
                WriteHostileFixtureAndRequireRejection(
                    fixtureRoot,
                    "boot-order-missing-fault-nonclaim",
                    diagnostics,
                    bootOrder.Replace(
                        "fault execution",
                        "live fault behavior",
                        StringComparison.Ordinal)) &&
                WriteHostileFixtureAndRequireRejection(
                    fixtureRoot,
                    "boot-order-missing-exitbootservices-nonclaim",
                    diagnostics,
                    bootOrder.Replace(
                        "`ExitBootServices` transition",
                        "`ExitBootServices` success",
                        StringComparison.Ordinal));
        }
        finally
        {
            if (Directory.Exists(fixtureRoot))
            {
                Directory.Delete(fixtureRoot, recursive: true);
            }
        }

        if (!hostileFixturesRejected)
        {
            return false;
        }

        string efiBuild = File.ReadAllText(
            Path.Combine(root, "docs", "efi-build.md"));
        string qemu = File.ReadAllText(
            Path.Combine(root, "docs", "qemu.md"));
        string readme = File.ReadAllText(Path.Combine(root, "README.md"));

        return efiBuild.Contains(
                "EnableM02GuardedIstStackAllocationProbe",
                StringComparison.Ordinal)
            && efiBuild.Contains("six", StringComparison.OrdinalIgnoreCase)
            && efiBuild.Contains("UEFI LoaderData", StringComparison.Ordinal)
            && efiBuild.Contains(
                "pre-`ExitBootServices`",
                StringComparison.Ordinal)
            && efiBuild.Contains(
                "does not make the guard-intent pages non-present",
                StringComparison.Ordinal)
            && qemu.Contains(
                "CLROS_M02_008_READY",
                StringComparison.Ordinal)
            && qemu.Contains(
                "guestGuardMappings=false",
                StringComparison.Ordinal)
            && qemu.Contains(
                "M02_GUARDED_IST_STACK_ALLOCATION_FAILED",
                StringComparison.Ordinal)
            && qemu.Contains(
                "postExitBootServices=false",
                StringComparison.Ordinal)
            && readme.Contains(
                "M02-008",
                StringComparison.Ordinal)
            && readme.Contains(
                "temporary six-page UEFI LoaderData arena",
                StringComparison.Ordinal)
            && readme.Contains(
                "does not install a TSS, IST, GDT, or IDT",
                StringComparison.Ordinal);
    }

    private static bool HasRequiredDocumentationBoundaries(
        string diagnostics,
        string bootOrder)
    {
        const string DiagnosticsSectionStart =
            "M02-008 adds a caller-owned guarded-IST arena allocator";
        const string DiagnosticsNextIssueStart = "M02-009 host tests";
        const string DiagnosticsSectionEnd = "## Next Steps";
        const string BootOrderSectionStart =
            "- M02-008 adds a host-tested, allocation-free guarded-IST arena allocator";
        const string BootOrderSectionEnd = "- Fault-entry frame layout tests";

        string diagnosticsBoundary = NormalizeDocumentationText(
            ExtractSectionBeforeAny(
                diagnostics,
                DiagnosticsSectionStart,
                DiagnosticsNextIssueStart,
                DiagnosticsSectionEnd));
        string bootOrderBoundary = NormalizeDocumentationText(
            ExtractSectionBeforeAny(
                bootOrder,
                BootOrderSectionStart,
                BootOrderSectionEnd));

        return diagnosticsBoundary.Contains(
                DiagnosticsSectionStart,
                StringComparison.Ordinal)
            && diagnosticsBoundary.Contains(
                "double fault, page fault, and NMI",
                StringComparison.Ordinal)
            && diagnosticsBoundary.Contains(
                "one non-present guard range followed by one writable, " +
                "non-executable downward-growing stack range for each fixed IST index",
                StringComparison.Ordinal)
            && diagnosticsBoundary.Contains(
                "temporary six-page UEFI LoaderData arena",
                StringComparison.Ordinal)
            && diagnosticsBoundary.Contains(
                "writes and reads back only the three stack ranges",
                StringComparison.Ordinal)
            && diagnosticsBoundary.Contains(
                "releases the lease, and frees the same arena before return",
                StringComparison.Ordinal)
            && diagnosticsBoundary.Contains(
                "The proof does not change page tables, map guards non-present, " +
                "write TSS IST slots, load descriptor tables, install handlers, " +
                "execute a fault, or cross real `ExitBootServices`.",
                StringComparison.Ordinal)
            && !diagnosticsBoundary.Contains(
                "The proof does change page tables",
                StringComparison.Ordinal)
            && diagnosticsBoundary.Contains(
                "map guards non-present",
                StringComparison.Ordinal)
            && diagnosticsBoundary.Contains(
                "write TSS IST slots",
                StringComparison.Ordinal)
            && diagnosticsBoundary.Contains(
                "load descriptor tables",
                StringComparison.Ordinal)
            && diagnosticsBoundary.Contains(
                "execute a fault",
                StringComparison.Ordinal)
            && diagnosticsBoundary.Contains(
                "cross real `ExitBootServices`",
                StringComparison.Ordinal)
            && bootOrderBoundary.Contains(
                BootOrderSectionStart,
                StringComparison.Ordinal)
            && bootOrderBoundary.Contains(
                "double fault, page fault, and NMI",
                StringComparison.Ordinal)
            && bootOrderBoundary.Contains(
                "three adjacent guard-plus-stack reservations",
                StringComparison.Ordinal)
            && bootOrderBoundary.Contains(
                "temporary six-page LoaderData allocation",
                StringComparison.Ordinal)
            && bootOrderBoundary.Contains(
                "stack-only write/readback",
                StringComparison.Ordinal)
            && bootOrderBoundary.Contains(
                "lease release, and same-base `FreePages` before return",
                StringComparison.Ordinal)
            && bootOrderBoundary.Contains(
                "no guard mapping, TSS/IDT installation, fault execution, or real " +
                "`ExitBootServices` transition",
                StringComparison.Ordinal);
    }

    private static bool WriteHostileFixtureAndRequireRejection(
        string fixtureRoot,
        string caseName,
        string diagnostics,
        string bootOrder)
    {
        string caseRoot = Path.Combine(fixtureRoot, caseName);
        Directory.CreateDirectory(caseRoot);
        string diagnosticsPath = Path.Combine(caseRoot, "diagnostics.md");
        string bootOrderPath = Path.Combine(caseRoot, "boot-order.md");
        File.WriteAllText(diagnosticsPath, diagnostics);
        File.WriteAllText(bootOrderPath, bootOrder);
        return !HasRequiredDocumentationBoundaries(
            File.ReadAllText(diagnosticsPath),
            File.ReadAllText(bootOrderPath));
    }

    private static string NormalizeDocumentationText(string source)
    {
        return Regex.Replace(
            source,
            @"\s+",
            " ",
            RegexOptions.CultureInvariant);
    }

    private static string ExtractSectionBeforeAny(
        string source,
        string startToken,
        params string[] endTokens)
    {
        int start = source.IndexOf(startToken, StringComparison.Ordinal);
        if (start < 0)
        {
            return string.Empty;
        }

        int end = -1;
        foreach (string endToken in endTokens)
        {
            int candidate = source.IndexOf(
                endToken,
                start + startToken.Length,
                StringComparison.Ordinal);
            if (candidate >= 0 && (end < 0 || candidate < end))
            {
                end = candidate;
            }
        }

        return end > start ? source[start..end] : string.Empty;
    }

    private static string ExtractAsciiMarker(string source, string methodToken)
    {
        int start = source.IndexOf(methodToken, StringComparison.Ordinal);
        int end = start < 0
            ? -1
            : source.IndexOf(
                "        private static ",
                start + methodToken.Length,
                StringComparison.Ordinal);
        if (start < 0 || end <= start)
        {
            return string.Empty;
        }

        MatchCollection characters = Regex.Matches(
            source[start..end],
            @"\(byte\)'(?<value>.)'",
            RegexOptions.CultureInvariant);
        return string.Concat(
            characters
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
}
