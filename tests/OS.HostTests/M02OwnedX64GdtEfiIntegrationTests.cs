using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

internal static class M02OwnedX64GdtEfiIntegrationTests
{
    private const string TestName = "m02.007.efi.integration";
    private const string ReadyMarker =
        "CLROS_M02_007_READY encoding=guestValidated callerOwned=true " +
        "readBack=true gdtBytes=40 tssBytes=104 tableLimit=39 " +
        "selectors=0,8,16,24 guestEncoding=true guestLgdt=false " +
        "guestLtr=false allocationFree=true markerTransport=uefiSerial " +
        "postExitBootServices=false";
    private const string FailureMarker =
        "M02_OWNED_X64_GDT_ENCODING_FAILED";
    private const string CompileClosureSha256 =
        "ecb946bd02797cf4c9332dd568c3c3ce75dc356e790b6c9551265be7650d7af7";
    private const string CanonicalProjectSha256 =
        "8bdc64a0e1e3e85c4045f41f9c0f0adc6e05bf1ad02fcdb248ab394b301dc06d";

    private static readonly (string Path, string Sha256)[] ImmutableReviewedInputs =
    [
        (
            "src/OS.Kernel/Diagnostics/OwnedX64GDTContainingNullN64BitCode.cs",
            "0f8d0828da8228030276232cb5b6077513572d3d5fd76d52cd9ec27d9bcfff04"),
        (
            "tests/OS.HostTests/OwnedX64GDTContainingNullN64BitCodeTests.cs",
            "c41be0cec03ec6acbd07e0325f747c2265314ab839c1f5ebb417ba53f66f4910"),
        (
            "done/step913.md",
            "f5122c30b8b1282ebe7616ecee14cf56df529ee7f6c640d620645938281114cb")
    ];

    [ModuleInitializer]
    internal static void RunAll()
    {
        Console.WriteLine("TEST_START " + TestName);
        int failures = 0;

        RunCase(
            "m02.007 EFI closure links one reviewed encoder behind a default-off switch",
            TestCompileClosureAndSwitch,
            ref failures);
        RunCase(
            "m02.007 EFI encodes caller-owned guest stack storage after m02.006",
            TestEntryCallOrderAndResultGate,
            ref failures);
        RunCase(
            "m02.007 EFI markers are exact and fail closed",
            TestMarkerEncodingAndMutualExclusion,
            ref failures);
        RunCase(
            "m02.007 QEMU profile enforces exact guest evidence",
            TestQemuProfileSource,
            ref failures);
        RunCase(
            "m02.007 immutable inputs and documentation boundaries remain exact",
            TestImmutableInputsAndDocumentationBoundaries,
            ref failures);

        if (failures != 0)
        {
            Console.WriteLine(
                "TEST_FAIL " + TestName + " host-contract failures=" + failures);
            throw new InvalidOperationException(
                "M02-007 EFI integration host tests failed: " + failures);
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

    private static bool TestCompileClosureAndSwitch()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        XDocument project = XDocument.Load(
            Path.Combine(root, "src", "OS", "OS.csproj"));
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

        const string ReporterKey =
            @"..\OS.Kernel\Diagnostics\PanicSafeFormattingSerialReportingOwnedCOM1.cs|Diagnostics\PanicSafeFormattingSerialReportingOwnedCOM1.cs";
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
        int reporterIndex = Array.IndexOf(compileKeys, ReporterKey);
        int gdtIndex = Array.IndexOf(compileKeys, GdtKey);
        int guardedIstIndex = Array.IndexOf(compileKeys, GuardedIstKey);
        int terminalIndex = Array.IndexOf(compileKeys, TerminalKey);
        int normalizedInterruptFrameIndex =
            Array.IndexOf(compileKeys, NormalizedInterruptFrameKey);
        int endianIndex = Array.IndexOf(compileKeys, EndianKey);

        XElement[] defaults = project
            .Descendants("EnableM02OwnedX64GdtEncodingProbe")
            .Where(element => element.Parent?.Name.LocalName == "PropertyGroup")
            .ToArray();
        XElement[] symbolGroups = project
            .Descendants("PropertyGroup")
            .Where(element =>
                element.Attribute("Condition")?.Value ==
                    "'$(EnableM02OwnedX64GdtEncodingProbe)' == 'true'")
            .ToArray();
        string[] dependencyConditions = project
            .Descendants("Error")
            .Select(element => element.Attribute("Condition")?.Value ?? string.Empty)
            .Where(value => value.StartsWith(
                "'$(EnableM02OwnedX64GdtEncodingProbe)' == 'true'",
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

        return compileItems.Length == 85
            && compileHash == CompileClosureSha256
            && reporterIndex >= 0
            && gdtIndex == reporterIndex + 1
            && guardedIstIndex == gdtIndex + 1
            && terminalIndex == guardedIstIndex + 1
            && normalizedInterruptFrameIndex == terminalIndex + 1
            && endianIndex == normalizedInterruptFrameIndex + 17
            && compileKeys.Count(key => key == GdtKey) == 1
            && defaults.Length == 1
            && defaults[0].Value == "false"
            && defaults[0].Attribute("Condition")?.Value ==
                "'$(EnableM02OwnedX64GdtEncodingProbe)' == ''"
            && symbolGroups.Length == 1
            && symbolGroups[0].Elements("DefineConstants").Single().Value ==
                "$(DefineConstants);M02_OWNED_X64_GDT_ENCODING_PROBE"
            && dependencyConditions.Length == 10
            && dependencyConditions.Contains(
                "'$(EnableM02OwnedX64GdtEncodingProbe)' == 'true' and '$(EnableM03MemoryMapCaptureProbe)' == 'true'",
                StringComparer.Ordinal)
            && dependencyConditions.Contains(
                "'$(EnableM02OwnedX64GdtEncodingProbe)' == 'true' and '$(EnableM02UnexpectedVectorPanicProbe)' == 'true'",
                StringComparer.Ordinal)
            && dependencyConditions.Contains(
                "'$(EnableM02OwnedX64GdtEncodingProbe)' == 'true' and '$(EnableM04SzArrayAllocationProbe)' == 'true'",
                StringComparer.Ordinal)
            && dependencyConditions.Contains(
                "'$(EnableM02OwnedX64GdtEncodingProbe)' == 'true' and '$(EnableM04StringAllocationProbe)' == 'true'",
                StringComparer.Ordinal)
            && dependencyConditions.Contains(
                "'$(EnableM02OwnedX64GdtEncodingProbe)' == 'true' and '$(EnableM04StringAllocationBeforeBindProbe)' == 'true'",
                StringComparer.Ordinal)
            && dependencyConditions.Contains(
                "'$(EnableM02OwnedX64GdtEncodingProbe)' == 'true' and '$(EnableM04StringAllocationExhaustionProbe)' == 'true'",
                StringComparer.Ordinal)
            && dependencyConditions.Contains(
                "'$(EnableM02OwnedX64GdtEncodingProbe)' == 'true' and '$(EnableM04StringAllocationInvalidLengthProbe)' == 'true'",
                StringComparer.Ordinal)
            && dependencyConditions.Contains(
                "'$(EnableM02OwnedX64GdtEncodingProbe)' == 'true' and '$(EnableM04SzArrayAllocationBeforeBindProbe)' == 'true'",
                StringComparer.Ordinal)
            && dependencyConditions.Contains(
                "'$(EnableM02OwnedX64GdtEncodingProbe)' == 'true' and '$(EnableM04SzArrayAllocationExhaustionProbe)' == 'true'",
                StringComparer.Ordinal)
            && dependencyConditions.Contains(
                "'$(EnableM02OwnedX64GdtEncodingProbe)' == 'true' and '$(EnableM02PanicSafeOwnedCom1Probe)' != 'true'",
                StringComparer.Ordinal)
            && buildScript.Contains(
                "[switch] $EnableM02OwnedX64GdtEncodingProbe",
                StringComparison.Ordinal)
            && buildScript.Contains(
                "$arguments += \"-p:EnableM02OwnedX64GdtEncodingProbe=true\"",
                StringComparison.Ordinal)
            && buildScript.Contains(
                "m02_owned_x64_gdt_encoding_requires_panic_safe_owned_com1",
                StringComparison.Ordinal)
            && buildScript.Contains(
                "EFI_PUBLISH_SOURCE_AUTHORITY status=ok inputs=98",
                StringComparison.Ordinal)
            && buildScript.Contains(
                CanonicalProjectSha256,
                StringComparison.Ordinal)
            && buildScript.Contains(GdtKey, StringComparison.Ordinal)
            && verifierScript.Contains(
                "$expectedSourceClosureInputCount = 98",
                StringComparison.Ordinal)
            && verifierScript.Contains(
                "$compileItems.Count -ne 85",
                StringComparison.Ordinal)
            && verifierScript.Contains(GdtKey, StringComparison.Ordinal)
            && verifierScript.Contains(
                "[switch]$EnableM02OwnedX64GdtEncodingProbe",
                StringComparison.Ordinal)
            && verifierScript.Contains(
                "\"EnableM02OwnedX64GdtEncodingProbe,\" +",
                StringComparison.Ordinal)
            && verifierScript.Contains(
                "$arguments += \"-p:EnableM02OwnedX64GdtEncodingProbe=true\"",
                StringComparison.Ordinal)
            && verifierScript.Contains(
                "EnableM02OwnedX64GdtEncodingProbe =",
                StringComparison.Ordinal)
            && normalizedBuildScript.Contains(
                "$verificationArguments += @(\n" +
                "            \"-EnableM02OwnedX64GdtEncodingProbe\",",
                StringComparison.Ordinal)
            && buildScript.Contains(
                "$parameterName -ceq \"EnableM02OwnedX64GdtEncodingProbe\" -or",
                StringComparison.Ordinal);
    }

    private static bool TestEntryCallOrderAndResultGate()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(
            Path.Combine(root, "src", "OS", "Boot", "EfiEntry.cs"));

        int priorEvidence = source.IndexOf(
            "WriteM02PanicSafeOwnedCom1EvidenceLine(",
            StringComparison.Ordinal);
        int switchStart = IndexOfAfter(
            source,
            "#if M02_OWNED_X64_GDT_ENCODING_PROBE",
            priorEvidence);
        int stackWorkspace = IndexOfAfter(
            source,
            "M02OwnedX64GdtEncodingStackWorkspace",
            switchStart);
        int tssPointer = IndexOfAfter(
            source,
            "byte* m02OwnedTss =",
            stackWorkspace);
        int gdtPointer = IndexOfAfter(
            source,
            "byte* m02OwnedGdt =",
            tssPointer);
        int encodeCall = IndexOfAfter(
            source,
            ".Encode(",
            gdtPointer);
        int resultGate = IndexOfAfter(
            source,
            "if (!m02OwnedGdtEncoding.IsInitialized ||",
            encodeCall);
        int failureLine = IndexOfAfter(
            source,
            "WriteM02OwnedX64GdtEncodingFailureLine(",
            resultGate);
        int successLine = IndexOfAfter(
            source,
            "WriteM02OwnedX64GdtEncodingSuccessLine(",
            failureLine);
        int stageThreeLineLoop = IndexOfAfter(
            source,
            "GetM01StageThreeSuccessLineLength(",
            successLine);

        return priorEvidence >= 0
            && switchStart > priorEvidence
            && stackWorkspace > switchStart
            && tssPointer > stackWorkspace
            && gdtPointer > tssPointer
            && encodeCall > gdtPointer
            && resultGate > encodeCall
            && failureLine > resultGate
            && successLine > failureLine
            && stageThreeLineLoop > successLine
            && source.Contains(
                "const int m02OwnedTssByteCount = 104;",
                StringComparison.Ordinal)
            && source.Contains(
                "public fixed byte TssBytes[104];",
                StringComparison.Ordinal)
            && source.Contains(
                "public fixed byte GdtBytes[40];",
                StringComparison.Ordinal)
            && source.Contains(
                "m02OwnedWorkspace = default;",
                StringComparison.Ordinal)
            && source.Contains(
                "m02OwnedWorkspace.TssBytes;",
                StringComparison.Ordinal)
            && source.Contains(
                "m02OwnedWorkspace.GdtBytes;",
                StringComparison.Ordinal)
            && source.Contains(
                "m02OwnedTss[tssOffset] = 0;",
                StringComparison.Ordinal)
            && source.Contains(
                "bool m02OwnedTssRemainsZero = true;",
                StringComparison.Ordinal)
            && source.Contains(
                ".MinimumTssLimit);",
                StringComparison.Ordinal)
            && source.Contains(
                "m02OwnedGdtEncoding.BytesWritten != 40",
                StringComparison.Ordinal)
            && source.Contains(
                "m02OwnedGdtEncoding.RequiredBytes != 40",
                StringComparison.Ordinal)
            && source.Contains(
                "m02OwnedGdtEncoding.TableLimit != 39",
                StringComparison.Ordinal)
            && source.Contains(
                "m02OwnedGdtEncoding.Verification.BytesCompared != 40",
                StringComparison.Ordinal)
            && source.Contains(
                "m02OwnedGdtEncoding.Verification.FirstMismatchOffset != -1",
                StringComparison.Ordinal)
            && source.Contains(
                "m02OwnedGdtEncoding.Verification.ExpectedFingerprint",
                StringComparison.Ordinal)
            && source.Contains(".NullSelector != 0", StringComparison.Ordinal)
            && source.Contains(".KernelCodeSelector != 8", StringComparison.Ordinal)
            && source.Contains(".KernelDataSelector != 16", StringComparison.Ordinal)
            && source.Contains(".TssSelector != 24", StringComparison.Ordinal)
            && CountOccurrences(source, "m02OwnedGdtEncoding =") == 1
            && !source.Contains("Lgdt(", StringComparison.OrdinalIgnoreCase)
            && !source.Contains("Ltr(", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TestMarkerEncodingAndMutualExclusion()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(
            Path.Combine(root, "src", "OS", "Boot", "EfiEntry.cs"));
        string reconstructedReady = ExtractAsciiMarker(
            source,
            "private static void WriteM02OwnedX64GdtEncodingSuccessLine(");
        string reconstructedFailure = ExtractAsciiMarker(
            source,
            "private static void WriteM02OwnedX64GdtEncodingFailureLine(");

        int resultGate = source.IndexOf(
            "if (!m02OwnedGdtEncoding.IsInitialized ||",
            StringComparison.Ordinal);
        int failureCall = IndexOfAfter(
            source,
            "WriteM02OwnedX64GdtEncodingFailureLine(",
            resultGate);
        int abortReturn = IndexOfAfter(source, "return;", failureCall);
        int successCall = IndexOfAfter(
            source,
            "WriteM02OwnedX64GdtEncodingSuccessLine(",
            abortReturn);

        return reconstructedReady == ReadyMarker
            && reconstructedFailure == FailureMarker
            && ReadyMarker.Contains("guestEncoding=true", StringComparison.Ordinal)
            && ReadyMarker.Contains("guestLgdt=false", StringComparison.Ordinal)
            && ReadyMarker.Contains("guestLtr=false", StringComparison.Ordinal)
            && ReadyMarker.Contains(
                "postExitBootServices=false",
                StringComparison.Ordinal)
            && !ReadyMarker.Contains("CloudReady", StringComparison.OrdinalIgnoreCase)
            && !ReadyMarker.Contains("production", StringComparison.OrdinalIgnoreCase)
            && resultGate >= 0
            && failureCall > resultGate
            && abortReturn > failureCall
            && successCall > abortReturn
            && CountOccurrences(
                source,
                "WriteM02OwnedX64GdtEncodingSuccessLine(") == 2
            && CountOccurrences(
                source,
                "WriteM02OwnedX64GdtEncodingFailureLine(") == 2
            && !source.Contains("\"" + ReadyMarker + "\"", StringComparison.Ordinal)
            && !source.Contains("\"" + FailureMarker + "\"", StringComparison.Ordinal);
    }

    private static bool TestQemuProfileSource()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(
            Path.Combine(root, "scripts", "run-qemu-smoke.ps1"));

        int gdtProfile = source.IndexOf(
            "elseif ($EnableM02OwnedX64GdtEncodingProbe) {",
            StringComparison.Ordinal);
        int panicProfile = IndexOfAfter(
            source,
            "elseif ($EnableM02PanicSafeOwnedCom1Probe) {",
            gdtProfile);
        int gdtBuildPolicy = source.IndexOf(
            "m02_owned_x64_gdt_encoding_probe_requires_build_efi",
            StringComparison.Ordinal);
        int inheritedBuildPolicy = source.IndexOf(
            "m02_panic_safe_owned_com1_probe_requires_build_efi",
            StringComparison.Ordinal);
        int gdtSkipPolicy = source.IndexOf(
            "m02_owned_x64_gdt_encoding_probe_forbids_skip_image_build",
            StringComparison.Ordinal);
        int inheritedSkipPolicy = source.IndexOf(
            "m02_panic_safe_owned_com1_probe_forbids_skip_image_build",
            StringComparison.Ordinal);
        string gdtProfileSource = gdtProfile >= 0 && panicProfile > gdtProfile
            ? source[gdtProfile..panicProfile]
            : string.Empty;

        return source.Contains(
                "[switch] $EnableM02OwnedX64GdtEncodingProbe",
                StringComparison.Ordinal)
            && source.Contains(
                "$script:M02OwnedX64GdtEncodingReadyMarker =",
                StringComparison.Ordinal)
            && source.Contains(ReadyMarker, StringComparison.Ordinal)
            && source.Contains(FailureMarker, StringComparison.Ordinal)
            && source.Contains(
                "$script:M02OwnedX64GdtEncodingRequiredMarkers",
                StringComparison.Ordinal)
            && source.Contains(
                "$script:M02OwnedX64GdtEncodingForbiddenMarkers",
                StringComparison.Ordinal)
            && source.Contains(
                "$marker -eq $script:M02PanicSafeOwnedCom1EvidenceMarker",
                StringComparison.Ordinal)
            && source.Contains(
                "m02_owned_x64_gdt_encoding_probe_requires_panic_safe_owned_com1",
                StringComparison.Ordinal)
            && source.Contains(
                "m02_owned_x64_gdt_encoding_probe_requires_build_efi",
                StringComparison.Ordinal)
            && source.Contains(
                "m02_owned_x64_gdt_encoding_probe_forbids_skip_image_build",
                StringComparison.Ordinal)
            && source.Contains(
                "$buildArguments += \"-EnableM02OwnedX64GdtEncodingProbe\"",
                StringComparison.Ordinal)
            && source.Contains(
                "profileRequired=55 profileForbidden=39",
                StringComparison.Ordinal)
            && source.Contains(
                "profileRequired=54 profileForbidden=38",
                StringComparison.Ordinal)
            && source.Contains("\"TEST_FAIL\"", StringComparison.Ordinal)
            && gdtBuildPolicy >= 0
            && inheritedBuildPolicy > gdtBuildPolicy
            && gdtSkipPolicy > gdtBuildPolicy
            && inheritedSkipPolicy > gdtSkipPolicy
            && CountOccurrences(
                gdtProfileSource,
                "Merge-QemuSmokeMarkerList -Values @(") == 4
            && !gdtProfileSource.Contains(
                "ConvertTo-QemuSmokeMarkerList -Values @(",
                StringComparison.Ordinal)
            && gdtProfile >= 0
            && panicProfile > gdtProfile;
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
            "clros-m02-007-documentation-boundaries-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(fixtureRoot);
            return WriteHostileFixtureAndRequireRejection(
                    fixtureRoot,
                    "missing-issue-identity",
                    diagnostics.Replace(
                        "outside M02-007",
                        "outside a removed issue",
                        StringComparison.Ordinal),
                    bootOrder) &&
                WriteHostileFixtureAndRequireRejection(
                    fixtureRoot,
                    "missing-guest-install-boundary",
                    diagnostics.Replace(
                        "Guest encoding is not the same as loading the table",
                        "Guest encoding loads the table",
                        StringComparison.Ordinal),
                    bootOrder) &&
                WriteHostileFixtureAndRequireRejection(
                    fixtureRoot,
                    "missing-claim-boundary",
                    diagnostics.Replace(
                        "provider, cloud-readiness, Kestrel, or production claims",
                        "provider behavior",
                        StringComparison.Ordinal),
                    bootOrder) &&
                WriteHostileFixtureAndRequireRejection(
                    fixtureRoot,
                    "missing-evidence-size",
                    diagnostics.Replace(
                        "M02-007 host tests encode the exact 40-byte table",
                        "M02-007 host tests encode an unspecified table",
                        StringComparison.Ordinal),
                    bootOrder) &&
                WriteHostileFixtureAndRequireRejection(
                    fixtureRoot,
                    "missing-evidence-lifecycle",
                    RemoveEvidenceLifecycleBoundary(diagnostics),
                    bootOrder) &&
                WriteHostileFixtureAndRequireRejection(
                    fixtureRoot,
                    "missing-boot-size",
                    diagnostics,
                    bootOrder.Replace(
                        "M02-007 adds a 40-byte caller-owned GDT encoder",
                        "M02-007 adds an unspecified encoder",
                        StringComparison.Ordinal)) &&
                WriteHostileFixtureAndRequireRejection(
                    fixtureRoot,
                    "missing-boot-install-boundary",
                    diagnostics,
                    RemoveBootInstallBoundary(bootOrder)) &&
                WriteHostileFixtureAndRequireRejection(
                    fixtureRoot,
                    "missing-boot-lifecycle",
                    diagnostics,
                    RemoveBootLifecycleBoundary(bootOrder));
        }
        finally
        {
            if (Directory.Exists(fixtureRoot))
            {
                Directory.Delete(fixtureRoot, recursive: true);
            }
        }
    }

    private static bool HasRequiredDocumentationBoundaries(
        string diagnostics,
        string bootOrder)
    {
        const string DiagnosticsSectionStart =
            "`OwnedX64GdtContainingNullN64BitCode` strengthens";
        const string DiagnosticsSectionEnd = "`FaultProbeStubPlan`";
        const string DiagnosticsEvidenceStart = "M02-007 host tests encode";
        const string DiagnosticsEvidenceEnd = "## Next Steps";
        const string BootOrderSectionStart =
            "- M02-007 adds a 40-byte caller-owned GDT encoder";
        const string BootOrderSectionEnd = "- Fault-entry frame layout tests";

        string diagnosticsBoundary = ExtractSection(
            diagnostics,
            DiagnosticsSectionStart,
            DiagnosticsSectionEnd);
        string diagnosticsEvidence = ExtractSection(
            diagnostics,
            DiagnosticsEvidenceStart,
            DiagnosticsEvidenceEnd);
        string bootOrderBoundary = ExtractSection(
            bootOrder,
            BootOrderSectionStart,
            BootOrderSectionEnd);
        bool bootOrderDeniesInstall =
            bootOrderBoundary.Contains(
                "will not load the table or prove `lgdt`, `ltr`",
                StringComparison.Ordinal) ||
            bootOrderBoundary.Contains(
                "explicitly reports no `lgdt` or `ltr`",
                StringComparison.Ordinal) ||
            bootOrderBoundary.Contains(
                "without executing `lgdt` or `ltr`",
                StringComparison.Ordinal);
        bool diagnosticsEvidenceHasLifecycleBoundary =
            (diagnosticsEvidence.Contains(
                    "`guestEncoding=false`",
                    StringComparison.Ordinal) &&
                diagnosticsEvidence.Contains(
                    "required before M02-007 can close",
                    StringComparison.Ordinal)) ||
            (diagnosticsEvidence.Contains(
                    "pre-`ExitBootServices`",
                    StringComparison.Ordinal) &&
                diagnosticsEvidence.Contains(
                    "does not install the GDT or TSS",
                    StringComparison.Ordinal));
        bool bootOrderHasLifecycleBoundary =
            (bootOrderBoundary.Contains(
                    "Current evidence is host-only",
                    StringComparison.Ordinal) &&
                bootOrderBoundary.Contains(
                    "post-`ExitBootServices` diagnostics",
                    StringComparison.Ordinal)) ||
            (bootOrderBoundary.Contains(
                    "pre-firmware-exit",
                    StringComparison.Ordinal) &&
                bootOrderBoundary.Contains(
                    "post-`ExitBootServices` diagnostics",
                    StringComparison.Ordinal));
        bool diagnosticsEvidenceDeniesPostFirmwareClaim =
            !diagnosticsEvidence.Contains(
                "postExitBootServices=true",
                StringComparison.Ordinal) &&
            !diagnosticsEvidence.Contains(
                "guest result remains post-`ExitBootServices`",
                StringComparison.Ordinal) &&
            !diagnosticsEvidence.Contains(
                "guest result survives `ExitBootServices`",
                StringComparison.Ordinal);
        bool bootOrderDeniesPostFirmwareClaim =
            !bootOrderBoundary.Contains(
                "guest marker remains post-firmware-exit",
                StringComparison.Ordinal) &&
            !bootOrderBoundary.Contains(
                "guest result proves post-`ExitBootServices` diagnostics",
                StringComparison.Ordinal);

        return diagnosticsBoundary.Contains("outside M02-007", StringComparison.Ordinal) &&
            diagnosticsBoundary.Contains(
                "Guest encoding is not the same as loading the table",
                StringComparison.Ordinal) &&
            diagnosticsBoundary.Contains(
                "live `lgdt`, `ltr`",
                StringComparison.Ordinal) &&
            diagnosticsBoundary.Contains(
                "provider, cloud-readiness, Kestrel, or production claims",
                StringComparison.Ordinal) &&
            diagnosticsEvidence.Contains("exact 40-byte table", StringComparison.Ordinal) &&
            diagnosticsEvidence.Contains("`guestLgdt=false`", StringComparison.Ordinal) &&
            diagnosticsEvidence.Contains("`guestLtr=false`", StringComparison.Ordinal) &&
            diagnosticsEvidenceHasLifecycleBoundary &&
            diagnosticsEvidenceDeniesPostFirmwareClaim &&
            bootOrderBoundary.Contains(
                "M02-007 adds a 40-byte caller-owned GDT encoder",
                StringComparison.Ordinal) &&
            bootOrderBoundary.Contains(
                "no-stdlib core and exact readback verification",
                StringComparison.Ordinal) &&
            bootOrderDeniesInstall &&
            bootOrderHasLifecycleBoundary &&
            bootOrderDeniesPostFirmwareClaim;
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

    private static string RemoveEvidenceLifecycleBoundary(string diagnostics)
    {
        return diagnostics
            .Replace(
                "`guestEncoding=false`",
                "`guestEncoding=unknown`",
                StringComparison.Ordinal)
            .Replace(
                "required before M02-007 can close",
                "deferred without an issue boundary",
                StringComparison.Ordinal)
            .Replace(
                "pre-`ExitBootServices`",
                "at an unspecified lifecycle point",
                StringComparison.Ordinal)
            .Replace(
                "does not install the GDT or TSS",
                "installation status is unspecified",
                StringComparison.Ordinal);
    }

    private static string RemoveBootInstallBoundary(string bootOrder)
    {
        return bootOrder
            .Replace(
                "will not load the table or prove `lgdt`, `ltr`",
                "loads the table and executes descriptor instructions",
                StringComparison.Ordinal)
            .Replace(
                "explicitly reports no `lgdt` or `ltr`",
                "reports active descriptor instructions",
                StringComparison.Ordinal)
            .Replace(
                "without executing `lgdt` or `ltr`",
                "after executing descriptor instructions",
                StringComparison.Ordinal);
    }

    private static string RemoveBootLifecycleBoundary(string bootOrder)
    {
        return bootOrder
            .Replace(
                "Current evidence is host-only",
                "Current lifecycle is unspecified",
                StringComparison.Ordinal)
            .Replace(
                "pre-firmware-exit",
                "at an unspecified lifecycle point",
                StringComparison.Ordinal)
            .Replace(
                "post-`ExitBootServices` diagnostics",
                "diagnostics at an unspecified lifecycle point",
                StringComparison.Ordinal);
    }

    private static string ExtractSection(
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
