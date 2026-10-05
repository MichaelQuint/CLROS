using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

internal static class M04IndependentEndianConformanceEfiIntegrationTests
{
    private const string TestName = "m04.002.efi.integration";
    private const string ReadyMarker =
        "CLROS_M04_002_READY readVectors=25 reverseVectors=7 byteSeries=5 uint16Series=7 fingerprint=E81E4451E8D999F6 referenceFilesRequired=false hostConformance=false guestIntegrated=true gate4=Fail";
    private const string FailureMarker =
        "M04_ENDIAN_CONFORMANCE_FAILED";
    private const string CompileClosureSha256 =
        "54bfa32fa851eb9a5f96ee8f3b8d9bd3f916b01dc629d5497cb42d97f6d79993";

    [ModuleInitializer]
    internal static void RunAll()
    {
        Console.WriteLine("TEST_START " + TestName);
        int failures = 0;

        RunCase(
            "m04.002 EFI compile closure and default-off flag are exact",
            TestCompileClosureAndFlag,
            ref failures);
        RunCase(
            "m04.002 EFI catalog exposes a fail-closed no-stdlib pointer path",
            TestNoStdLibCatalogSource,
            ref failures);
        RunCase(
            "m04.002 EFI entry validates caller-owned vectors before memory map markers",
            TestEfiEntryMarkerContract,
            ref failures);
        RunCase(
            "m04.002 QEMU profile requires one guest marker and forbids failure",
            TestQemuProfile,
            ref failures);

        if (failures != 0)
        {
            Console.WriteLine(
                "TEST_FAIL " + TestName + " host-contract failures=" + failures);
            throw new InvalidOperationException(
                "M04-002 EFI integration host tests failed: " + failures);
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

    private static bool TestCompileClosureAndFlag()
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
        const string CatalogKey =
            @"..\..\std\no-runtime\shared\IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMOSA.cs|Std\IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMOSA.cs";
        const string GuardedIstKey =
            @"..\OS.Kernel\Diagnostics\GuardedISTStacksDoubleFaultPageFaultNMI.cs|Diagnostics\GuardedISTStacksDoubleFaultPageFaultNMI.cs";
        const string TerminalKey =
            @"..\OS.Kernel\Diagnostics\OwnedGDTTSSGeneratedAuditedCSharpStubBytes.cs|Diagnostics\OwnedGDTTSSGeneratedAuditedCSharpStubBytes.cs";
        const string NormalizedInterruptFrameKey =
            @"..\OS.Kernel\Diagnostics\NormalizeErrorCodeNonErrorCodeInterruptFrame.cs|Diagnostics\NormalizeErrorCodeNonErrorCodeInterruptFrame.cs";
        int backendIndex = Array.IndexOf(
            compileKeys,
            @"..\OS.Kernel\Diagnostics\COM1OwnedPortIOBackend.cs|Diagnostics\COM1OwnedPortIOBackend.cs");
        int ownedWriterIndex = Array.IndexOf(
            compileKeys,
            @"..\OS.Kernel\Diagnostics\OWNEDCOM1READYExclusivelyOwnedWriter.cs|Diagnostics\OWNEDCOM1READYExclusivelyOwnedWriter.cs");
        int panicReporterIndex = Array.IndexOf(
            compileKeys,
            @"..\OS.Kernel\Diagnostics\PanicSafeFormattingSerialReportingOwnedCOM1.cs|Diagnostics\PanicSafeFormattingSerialReportingOwnedCOM1.cs");
        int gdtIndex = Array.IndexOf(
            compileKeys,
            @"..\OS.Kernel\Diagnostics\OwnedX64GDTContainingNullN64BitCode.cs|Diagnostics\OwnedX64GDTContainingNullN64BitCode.cs");
        int guardedIstIndex = Array.IndexOf(compileKeys, GuardedIstKey);
        int terminalIndex = Array.IndexOf(compileKeys, TerminalKey);
        int normalizedInterruptFrameIndex =
            Array.IndexOf(compileKeys, NormalizedInterruptFrameKey);
        int catalogIndex = Array.IndexOf(compileKeys, CatalogKey);

        XElement[] defaults = project
            .Descendants("EnableM04IndependentEndianConformanceProbe")
            .Where(element => element.Parent?.Name.LocalName == "PropertyGroup")
            .ToArray();
        XElement[] symbolGroups = project
            .Descendants("PropertyGroup")
            .Where(element =>
                element.Attribute("Condition")?.Value ==
                    "'$(EnableM04IndependentEndianConformanceProbe)' == 'true'")
            .ToArray();

        string buildScript = File.ReadAllText(
            Path.Combine(root, "scripts", "build-efi-shell.ps1"));
        string verifierScript = File.ReadAllText(
            Path.Combine(root, "scripts", "test-efi-artifact.ps1"));

        return compileItems.Length == 89
            && compileHash == CompileClosureSha256
            && backendIndex >= 0
            && ownedWriterIndex == backendIndex + 1
            && panicReporterIndex == ownedWriterIndex + 1
            && gdtIndex == panicReporterIndex + 1
            && guardedIstIndex == gdtIndex + 1
            && terminalIndex == guardedIstIndex + 1
            && normalizedInterruptFrameIndex == terminalIndex + 1
            && catalogIndex == normalizedInterruptFrameIndex + 17
            && compileKeys.Count(key => key == CatalogKey) == 1
            && defaults.Length == 1
            && defaults[0].Value == "false"
            && defaults[0].Attribute("Condition")?.Value ==
                "'$(EnableM04IndependentEndianConformanceProbe)' == ''"
            && symbolGroups.Length == 1
            && symbolGroups[0].Elements("DefineConstants").Single().Value ==
                "$(DefineConstants);M04_INDEPENDENT_ENDIAN_CONFORMANCE_PROBE"
            && buildScript.Contains(
                "[switch] $EnableM04IndependentEndianConformanceProbe",
                StringComparison.Ordinal)
            && buildScript.Contains(
                "$arguments +=\n                \"-p:EnableM04IndependentEndianConformanceProbe=true\"",
                StringComparison.Ordinal)
            && buildScript.Contains(
                "EFI_PUBLISH_SOURCE_AUTHORITY status=ok inputs=103",
                StringComparison.Ordinal)
            && buildScript.Contains(
                "\"-EnableM04IndependentEndianConformanceProbe\",\n            \"true\"",
                StringComparison.Ordinal)
            && buildScript.Contains(
                "$parameterName -ceq \"EnableM04IndependentEndianConformanceProbe\"",
                StringComparison.Ordinal)
            && buildScript.Contains(
                "[System.Boolean]::Parse($parameterValue)",
                StringComparison.Ordinal)
            && verifierScript.Contains(
                "[switch]$EnableM04IndependentEndianConformanceProbe",
                StringComparison.Ordinal)
            && verifierScript.Contains(
                "$expectedSourceClosureInputCount = 103",
                StringComparison.Ordinal)
            && verifierScript.Contains(
                "$compileItems.Count -ne 89",
                StringComparison.Ordinal);
    }

    private static bool TestNoStdLibCatalogSource()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(
            Path.Combine(
                root,
                "std",
                "no-runtime",
                "shared",
                "IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMOSA.cs"));

        int hostBranch = source.IndexOf("#if !NOSTDLIB_EFI", StringComparison.Ordinal);
        int efiBranch = source.IndexOf("#else", hostBranch, StringComparison.Ordinal);
        int end = source.IndexOf("#endif", efiBranch, StringComparison.Ordinal);
        string efiSource = hostBranch >= 0 && efiBranch > hostBranch && end > efiBranch
            ? source[efiBranch..end]
            : string.Empty;

        return source.Contains(
                "public const bool ReferenceFilesRequired = false;",
                StringComparison.Ordinal)
            && source.Contains(
                "public const bool RequiresHostConformance = false;",
                StringComparison.Ordinal)
            && efiSource.Contains(
                "IndependentEndianReadVector* readDestination",
                StringComparison.Ordinal)
            && efiSource.Contains(
                "IndependentEndianReverseVector* reverseDestination",
                StringComparison.Ordinal)
            && efiSource.Contains(
                "IndependentEndianConformanceStatus.ReadVectorPointerNull",
                StringComparison.Ordinal)
            && efiSource.Contains(
                "IndependentEndianConformanceStatus.ReverseVectorPointerNull",
                StringComparison.Ordinal)
            && efiSource.Contains(
                "if (readDestinationCapacity < ReadVectorCount)",
                StringComparison.Ordinal)
            && efiSource.Contains(
                "if (reverseDestinationCapacity < ReverseVectorCount)",
                StringComparison.Ordinal)
            && efiSource.Contains(
                "public static unsafe IndependentEndianConformanceResult ValidateCatalog(",
                StringComparison.Ordinal)
            && efiSource.Contains(
                "public static unsafe ulong ComputeCatalogFingerprint(",
                StringComparison.Ordinal)
            && source.Contains(
                "#if NOSTDLIB_EFI && M04_INDEPENDENT_ENDIAN_CONFORMANCE_PROBE && !M01_OWNED_PAGING_PROBE",
                StringComparison.Ordinal)
            && source.Contains(
                "[System.Runtime.RuntimeExport(\"__security_cookie\")]",
                StringComparison.Ordinal)
            && source.Contains(
                "private static ulong M04SecurityCookieStorageAnchor()",
                StringComparison.Ordinal)
            && CountOccurrences(source, "\"__security_cookie\"") == 1
            && !efiSource.Contains("Span<", StringComparison.Ordinal)
            && !efiSource.Contains("throw ", StringComparison.Ordinal)
            && !efiSource.Contains("new ", StringComparison.Ordinal);
    }

    private static bool TestEfiEntryMarkerContract()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(
            Path.Combine(root, "src", "OS", "Boot", "EfiEntry.cs"));
        string reconstructedReady = ExtractAsciiMarker(
            source,
            "private static void WriteM04IndependentEndianConformanceSuccessLine(");
        string reconstructedFailure = ExtractAsciiMarker(
            source,
            "private static void WriteM04IndependentEndianConformanceFailureLine(");

        int consoleReady = source.IndexOf(
            "WriteEfiConsoleReady(console, serial);",
            StringComparison.Ordinal);
        int probe = IndexOfAfter(
            source,
            "WriteM04IndependentEndianConformanceProbe(console, serial)",
            consoleReady);
        int memoryMap = IndexOfAfter(
            source,
            "TryCaptureUefiMemoryMap(systemTable)",
            probe);
        int copy = source.IndexOf(".CopyBuiltInCatalog(", StringComparison.Ordinal);
        int validate = IndexOfAfter(source, ".ValidateCatalog(", copy);
        int guard = IndexOfAfter(source, "if (!copyResult.Succeeded ||", validate);
        int success = IndexOfAfter(
            source,
            "WriteM04IndependentEndianConformanceSuccessLine(console, serial);",
            guard);

        return reconstructedReady == ReadyMarker
            && reconstructedFailure == FailureMarker
            && consoleReady >= 0
            && probe > consoleReady
            && memoryMap > probe
            && copy >= 0
            && validate > copy
            && guard > validate
            && success > guard
            && source.Contains(
                "stackalloc OS.Std.Binary.IndependentEndianReadVector[",
                StringComparison.Ordinal)
            && source.Contains(
                "stackalloc OS.Std.Binary.IndependentEndianReverseVector[",
                StringComparison.Ordinal)
            && source.Contains(
                "validationResult.ActualFingerprint != 0xE81E4451E8D999F6UL",
                StringComparison.Ordinal)
            && CountOccurrences(
                source,
                "WriteM04IndependentEndianConformanceProbe(") == 2
            && CountOccurrences(
                source,
                "WriteM04IndependentEndianConformanceSuccessLine(") == 2
            && CountOccurrences(
                source,
                "WriteM04IndependentEndianConformanceFailureLine(") == 2
            && CountOccurrences(source, "\"__security_cookie\"") == 1
            && !source.Contains(
                "M04SecurityCookieStorageAnchor",
                StringComparison.Ordinal)
            && CountOccurrences(source, ReadyMarker) == 0
            && CountOccurrences(source, FailureMarker) == 0;
    }

    private static bool TestQemuProfile()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(
            Path.Combine(root, "scripts", "run-qemu-smoke.ps1"));

        return source.Contains(
                "[switch] $EnableM04IndependentEndianConformanceProbe",
                StringComparison.Ordinal)
            && source.Contains(ReadyMarker, StringComparison.Ordinal)
            && source.Contains(FailureMarker, StringComparison.Ordinal)
            && source.Contains(
                "$script:M04IndependentEndianConformanceRequiredMarkers",
                StringComparison.Ordinal)
            && source.Contains(
                "$script:M04IndependentEndianConformanceForbiddenMarkers",
                StringComparison.Ordinal)
            && source.Contains(
                "m04_independent_endian_conformance_probe_requires_build_efi",
                StringComparison.Ordinal)
            && source.Contains(
                "m04_independent_endian_conformance_probe_forbids_skip_image_build",
                StringComparison.Ordinal)
            && source.Contains(
                "$buildArguments +=\n                \"-EnableM04IndependentEndianConformanceProbe\"",
                StringComparison.Ordinal)
            && source.Contains(
                "profileRequired=15 profileForbidden=3",
                StringComparison.Ordinal);
    }

    private static string ExtractAsciiMarker(string source, string methodToken)
    {
        int start = source.IndexOf(methodToken, StringComparison.Ordinal);
        int end = start < 0
            ? -1
            : source.IndexOf(
                "\n    private static ",
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

    private static int IndexOfAfter(string source, string value, int start)
    {
        return start < 0 || start > source.Length
            ? -1
            : source.IndexOf(value, start, StringComparison.Ordinal);
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
}
