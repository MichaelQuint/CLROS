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
        "b19e83c1f421460ade201addcfaee4b579871ac4a15eeb3d84618ef9de686a77";
    private const string CanonicalProjectSha256 =
        "d44a5571337c8b5d7bebb2e016ee5cdcf946e569a333a238ec69382bf4ad4444";

    private static readonly (string Path, string Sha256)[] ReadOnlyInputs =
    [
        (
            "src/OS.Kernel/Diagnostics/OwnedX64GDTContainingNullN64BitCode.cs",
            "0f8d0828da8228030276232cb5b6077513572d3d5fd76d52cd9ec27d9bcfff04"),
        (
            "tests/OS.HostTests/OwnedX64GDTContainingNullN64BitCodeTests.cs",
            "c41be0cec03ec6acbd07e0325f747c2265314ab839c1f5ebb417ba53f66f4910"),
        (
            "docs/diagnostics.md",
            "5020366c52db1d35079ff9200ac3584b05c9090289e19e4fd249d8b406a29d31"),
        (
            "docs/boot-order.md",
            "74263be6dca0ab9ddb158905075e9adeaab5c31a4547e3b840bac7b0c3040776"),
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
            "m02.007 reviewed leaf inputs remain byte-identical",
            TestReadOnlyInputHashes,
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
        const string EndianKey =
            @"..\..\std\no-runtime\shared\IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMOSA.cs|Std\IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMOSA.cs";
        int reporterIndex = Array.IndexOf(compileKeys, ReporterKey);
        int gdtIndex = Array.IndexOf(compileKeys, GdtKey);
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
            .Where(value => value.Contains(
                "EnableM02OwnedX64GdtEncodingProbe",
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

        return compileItems.Length == 56
            && compileHash == CompileClosureSha256
            && reporterIndex >= 0
            && gdtIndex == reporterIndex + 1
            && endianIndex == gdtIndex + 1
            && compileKeys.Count(key => key == GdtKey) == 1
            && defaults.Length == 1
            && defaults[0].Value == "false"
            && defaults[0].Attribute("Condition")?.Value ==
                "'$(EnableM02OwnedX64GdtEncodingProbe)' == ''"
            && symbolGroups.Length == 1
            && symbolGroups[0].Elements("DefineConstants").Single().Value ==
                "$(DefineConstants);M02_OWNED_X64_GDT_ENCODING_PROBE"
            && dependencyConditions.Length == 1
            && dependencyConditions[0].Contains(
                "EnableM02PanicSafeOwnedCom1Probe",
                StringComparison.Ordinal)
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
                "EFI_PUBLISH_SOURCE_AUTHORITY status=ok inputs=69",
                StringComparison.Ordinal)
            && buildScript.Contains(
                CanonicalProjectSha256,
                StringComparison.Ordinal)
            && buildScript.Contains(GdtKey, StringComparison.Ordinal)
            && verifierScript.Contains(
                "$expectedSourceClosureInputCount = 69",
                StringComparison.Ordinal)
            && verifierScript.Contains(
                "$compileItems.Count -ne 56",
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
                "$parameterName -ceq \"EnableM02OwnedX64GdtEncodingProbe\")",
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
            "if ($EnableM02OwnedX64GdtEncodingProbe) {",
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

    private static bool TestReadOnlyInputHashes()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        foreach ((string relativePath, string expectedSha256) in ReadOnlyInputs)
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

        return true;
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
