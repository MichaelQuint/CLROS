using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

internal static class M02OwnedCom1ReadyEfiIntegrationTests
{
    private const string TestName = "m02.005.efi.integration";
    private const string StandaloneMarker = OS.Tooling.QemuContracts.Record113;
    private const string ReadyMarker =
        OS.Tooling.QemuContracts.Record114;
    private const string FailureMarker = OS.Tooling.QemuContracts.Record108;
    private static readonly string CompileClosureSha256 =
        OS.Tooling.EfiContracts.CompileInventorySha256;

    [HostFixture(HostLane.Fast)]
    internal static void RunAll()
    {
        Console.WriteLine("TEST_START " + TestName);
        int failures = 0;

        RunCase(
            "m02.005 EFI closure links one default-off owned writer",
            TestCompileClosureAndSwitch,
            ref failures);
        RunCase(
            "m02.005 EFI calls the owned writer once after m02.004",
            TestEntryCallOrderAndResultGate,
            ref failures);
        RunCase(
            "m02.005 UEFI formatting excludes the standalone owned marker",
            TestMarkerTransportSeparation,
            ref failures);
        RunCase(
            "m02.005 QEMU profile requires and forbids exact markers",
            TestQemuProfileSource,
            ref failures);

        if (failures != 0)
        {
            Console.WriteLine(
                "TEST_FAIL " + TestName + " host-contract failures=" + failures);
            throw new InvalidOperationException(
                "M02-005 EFI integration host tests failed: " + failures);
        }

        Console.WriteLine("TEST_PASS " + TestName);
    }

    private static void RunCase(
        string name,
        Func<bool> test,
        ref int failures)
    {

        HostCheck.Run("" + name, test, ref failures);
    }

    private static bool TestCompileClosureAndSwitch()
{
    string root = HostToolProcess.FindRepositoryRoot();
    XDocument project = XDocument.Load(Path.Combine(root, "src", "OS", "OS.csproj"));
    XElement[] compileItems = project.Descendants("Compile").ToArray();
    string[] compileKeys = compileItems.Select(item => (item.Attribute("Include")?.Value ?? string.Empty) + "|" + (item.Attribute("Link")?.Value ?? string.Empty)).ToArray();
    string compileText = string.Join("\n", compileKeys) + "\n";
    string compileHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(compileText))).ToLowerInvariant();
    const string BackendKey = @"..\OS.Kernel\Diagnostics\COM1OwnedPortIOBackend.cs|Diagnostics\COM1OwnedPortIOBackend.cs";
    const string WriterKey = @"..\OS.Kernel\Diagnostics\OWNEDCOM1READYExclusivelyOwnedWriter.cs|Diagnostics\OWNEDCOM1READYExclusivelyOwnedWriter.cs";
    const string PanicReporterKey = @"..\OS.Kernel\Diagnostics\PanicSafeFormattingSerialReportingOwnedCOM1.cs|Diagnostics\PanicSafeFormattingSerialReportingOwnedCOM1.cs";
    const string GdtKey = @"..\OS.Kernel\Diagnostics\OwnedX64GDTContainingNullN64BitCode.cs|Diagnostics\OwnedX64GDTContainingNullN64BitCode.cs";
    const string GuardedIstKey = @"..\OS.Kernel\Diagnostics\GuardedISTStacksDoubleFaultPageFaultNMI.cs|Diagnostics\GuardedISTStacksDoubleFaultPageFaultNMI.cs";
    const string TerminalKey = @"..\OS.Kernel\Diagnostics\OwnedGDTTSSGeneratedAuditedCSharpStubBytes.cs|Diagnostics\OwnedGDTTSSGeneratedAuditedCSharpStubBytes.cs";
    const string NormalizedInterruptFrameKey = @"..\OS.Kernel\Diagnostics\NormalizeErrorCodeNonErrorCodeInterruptFrame.cs|Diagnostics\NormalizeErrorCodeNonErrorCodeInterruptFrame.cs";
    const string CatalogKey = @"..\..\std\no-runtime\shared\Binary\IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMOSA.cs|Std\Binary\IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMOSA.cs";
    int backendIndex = Array.IndexOf(compileKeys, BackendKey);
    int writerIndex = Array.IndexOf(compileKeys, WriterKey);
    int panicReporterIndex = Array.IndexOf(compileKeys, PanicReporterKey);
    int gdtIndex = Array.IndexOf(compileKeys, GdtKey);
    int guardedIstIndex = Array.IndexOf(compileKeys, GuardedIstKey);
    int terminalIndex = Array.IndexOf(compileKeys, TerminalKey);
    int normalizedInterruptFrameIndex = Array.IndexOf(compileKeys, NormalizedInterruptFrameKey);
    int catalogIndex = Array.IndexOf(compileKeys, CatalogKey);
    XElement[] defaults = project.Descendants("EnableM02OwnedCom1ReadyProbe").Where(element => element.Attribute("Condition")?.Value == "'$(EnableM02OwnedCom1ReadyProbe)' == ''").ToArray();
    XElement[] ud2ProfileOverrides = project.Descendants("PropertyGroup").Where(group => group.Attribute("Condition")?.Value == "'$(EnableM02Ud2VectorStateProbe)' == 'true'").SelectMany(group => group.Elements("EnableM02OwnedCom1ReadyProbe")).ToArray();
    XElement[] symbolGroups = project.Descendants("PropertyGroup").Where(element => element.Attribute("Condition")?.Value == "'$(EnableM02OwnedCom1ReadyProbe)' == 'true'").ToArray();
    string[] dependencyConditions = project.Descendants("Error").Select(element => element.Attribute("Condition")?.Value ?? string.Empty).Where(value => value.StartsWith("'$(EnableM02OwnedCom1ReadyProbe)' == 'true'", StringComparison.Ordinal)).ToArray();
    return (compileItems.Length == OS.Tooling.EfiContracts.CompileItems.Count && compileHash == CompileClosureSha256 && backendIndex >= 0 && writerIndex == backendIndex + 1 && panicReporterIndex == writerIndex + 1 && gdtIndex == panicReporterIndex + 1 && guardedIstIndex == gdtIndex + 1 && terminalIndex == guardedIstIndex + 1 && normalizedInterruptFrameIndex == terminalIndex + 1 && catalogIndex == normalizedInterruptFrameIndex + 17 && compileKeys.Count(key => key == WriterKey) == 1 && compileKeys.Count(key => key == PanicReporterKey) == 1 && defaults.Length == 1 && defaults[0].Value == "false" && defaults[0].Attribute("Condition")?.Value == "'$(EnableM02OwnedCom1ReadyProbe)' == ''" && ud2ProfileOverrides.Length == 1 && ud2ProfileOverrides[0].Value == "true" && symbolGroups.Length == 1 && symbolGroups[0].Elements("DefineConstants").Single().Value == "$(DefineConstants);M02_OWNED_COM1_READY_PROBE" && dependencyConditions.Length == 14 && dependencyConditions.Contains("'$(EnableM02OwnedCom1ReadyProbe)' == 'true' and '$(EnableM04GcStaticRootMaterializationProbe)' == 'true'", StringComparer.Ordinal) && dependencyConditions.Contains("'$(EnableM02OwnedCom1ReadyProbe)' == 'true' and '$(EnableM03TypedExitBootServicesCallProbe)' == 'true'", StringComparer.Ordinal) && dependencyConditions.Contains("'$(EnableM02OwnedCom1ReadyProbe)' == 'true' and '$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true'", StringComparer.Ordinal) && dependencyConditions.Contains("'$(EnableM02OwnedCom1ReadyProbe)' == 'true' and '$(EnableM04CheckedReferenceBarrierProbe)' == 'true'", StringComparer.Ordinal) && dependencyConditions.Contains("'$(EnableM02OwnedCom1ReadyProbe)' == 'true' and '$(EnableM03MemoryMapCaptureProbe)' == 'true'", StringComparer.Ordinal) && dependencyConditions.Contains("'$(EnableM02OwnedCom1ReadyProbe)' == 'true' and '$(EnableM03FirmwareEntropySeedProbe)' == 'true'", StringComparer.Ordinal) && dependencyConditions.Contains("'$(EnableM02OwnedCom1ReadyProbe)' == 'true' and '$(EnableM04SzArrayAllocationProbe)' == 'true'", StringComparer.Ordinal) && dependencyConditions.Contains("'$(EnableM02OwnedCom1ReadyProbe)' == 'true' and '$(EnableM04StringAllocationProbe)' == 'true'", StringComparer.Ordinal) && dependencyConditions.Contains("'$(EnableM02OwnedCom1ReadyProbe)' == 'true' and '$(EnableM04StringAllocationBeforeBindProbe)' == 'true'", StringComparer.Ordinal) && dependencyConditions.Contains("'$(EnableM02OwnedCom1ReadyProbe)' == 'true' and '$(EnableM04StringAllocationExhaustionProbe)' == 'true'", StringComparer.Ordinal) && dependencyConditions.Contains("'$(EnableM02OwnedCom1ReadyProbe)' == 'true' and '$(EnableM04StringAllocationInvalidLengthProbe)' == 'true'", StringComparer.Ordinal) && dependencyConditions.Contains("'$(EnableM02OwnedCom1ReadyProbe)' == 'true' and '$(EnableM04SzArrayAllocationBeforeBindProbe)' == 'true'", StringComparer.Ordinal) && dependencyConditions.Contains("'$(EnableM02OwnedCom1ReadyProbe)' == 'true' and '$(EnableM04SzArrayAllocationExhaustionProbe)' == 'true'", StringComparer.Ordinal) && dependencyConditions.Contains("'$(EnableM02OwnedCom1ReadyProbe)' == 'true' and '$(EnableM02Com1OwnedPortIoProbe)' != 'true'", StringComparer.Ordinal)) && OS.Tooling.ContractAudit.Inspect(root).Count == 0;
}

    private static bool TestEntryCallOrderAndResultGate()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(
            Path.Combine(root, "src", "OS", "Boot", "EfiEntry.cs")).Replace("\r\n", "\n", StringComparison.Ordinal);

        int m02Configuration = source.IndexOf(
            "M02Com1OwnedPortIoBackendIntegration.Configure(",
            StringComparison.Ordinal);
        int m02ConfigurationGuard = IndexOfAfter(
            source,
            "if (!com1Configuration.Succeeded)",
            m02Configuration);
        int m02Ready = IndexOfAfter(
            source,
            "WriteM02Com1OwnedPortIoSuccessLine(",
            m02ConfigurationGuard);
        int backendConstruction = IndexOfAfter(
            source,
            "new OS.Kernel.Diagnostics.Com1OwnedPortIoBackend(",
            m02Ready);
        int writerCall = IndexOfAfter(
            source,
            ".Emit(ownedCom1Backend);",
            backendConstruction);
        int resultGuard = IndexOfAfter(
            source,
            "if (!ownedCom1Ready.Succeeded ||",
            writerCall);
        int successLine = IndexOfAfter(
            source,
            "WriteM02OwnedCom1ReadySuccessLine(",
            resultGuard);
        int stageThreeLineLoop = IndexOfAfter(
            source,
            "GetM01StageThreeSuccessLineLength(",
            successLine);

        return m02Configuration >= 0
            && m02ConfigurationGuard > m02Configuration
            && m02Ready > m02ConfigurationGuard
            && backendConstruction > m02Ready
            && writerCall > backendConstruction
            && resultGuard > writerCall
            && successLine > resultGuard
            && stageThreeLineLoop > successLine
            && source.Contains(
                "ownedCom1Ready.Configuration.WritesCompleted != 7",
                StringComparison.Ordinal)
            && source.Contains(
                "ownedCom1Ready.MarkerBytesWritten != 16",
                StringComparison.Ordinal)
            && source.Contains(
                "ownedCom1Ready.LineEndingBytesWritten != 2",
                StringComparison.Ordinal)
            && source.Contains(
                "ownedCom1Ready.LineStatusPolls != 18",
                StringComparison.Ordinal)
            && source.Contains(
                "!ownedCom1Ready.OwnedBackendUsed",
                StringComparison.Ordinal)
            && source.Contains(
                "ownedCom1Ready.FirmwareFallbackUsed",
                StringComparison.Ordinal)
            && CountOccurrences(
                source,
                ".OwnedCom1ReadyExclusivelyOwnedWriter") == 1
            && CountOccurrences(source, ".Emit(ownedCom1Backend);") == 1
            && HasExpectedBackendConstructions(source)
            && !HasExpectedBackendConstructions(source.Replace(
                "#if M02_UNEXPECTED_VECTOR_PANIC_PROBE\n    private static class M02UnexpectedVectorPanicAdapter",
                "#if UNKNOWN_PANIC_PROFILE\n    private static class M02UnexpectedVectorPanicAdapter",
                StringComparison.Ordinal))
            && !HasExpectedBackendConstructions(source.Replace(
                "s_authority.Backend = new OS.Kernel.Diagnostics.Com1OwnedPortIoBackend(binding.Binding);",
                "s_authority.Backend = default;",
                StringComparison.Ordinal))
            && !HasExpectedBackendConstructions(source.Replace(
                "Com1OwnedPortIoBackend(binding.Binding)",
                "Com1OwnedPortIoBackend(default)",
                StringComparison.Ordinal))
            && !HasExpectedBackendConstructions(source +
                "\nnew OS.Kernel.Diagnostics.Com1OwnedPortIoBackend(default);");
    }

    private static bool HasExpectedBackendConstructions(string source)
    {
        const string Construction =
            "new OS.Kernel.Diagnostics.Com1OwnedPortIoBackend(";
        const string PanicStart =
            "#if M02_UNEXPECTED_VECTOR_PANIC_PROBE\n    private static class M02UnexpectedVectorPanicAdapter";
        int start = source.IndexOf(PanicStart, StringComparison.Ordinal);
        int end = start < 0 ? -1 : source.IndexOf(
            "\n#endif", start + PanicStart.Length, StringComparison.Ordinal);
        if (start < 0 || end < 0 || CountOccurrences(source, PanicStart) != 1)
        {
            return false;
        }

        string panic = source[start..end];
        // Keep the original three constructions exact; the fourth belongs only
        // to the separately gated panic adapter and its retained binding.
        return !panic.Contains("\n#if", StringComparison.Ordinal)
            && !panic.Contains("\n#else", StringComparison.Ordinal)
            && !panic.Contains("\n#elif", StringComparison.Ordinal)
            && panic.Contains(
                "s_authority.Backend = new OS.Kernel.Diagnostics.Com1OwnedPortIoBackend(binding.Binding);",
                StringComparison.Ordinal)
            && CountOccurrences(panic, Construction) == 1
            && CountOccurrences(source.Remove(start, end - start), Construction) == 3;
    }

    private static bool TestMarkerTransportSeparation()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(
            Path.Combine(root, "src", "OS", "Boot", "EfiEntry.cs")).Replace("\r\n", "\n", StringComparison.Ordinal);
        string reconstructedReady = ExtractAsciiMarker(
            source,
            "private static void WriteM02OwnedCom1ReadySuccessLine(");
        string reconstructedFailure = ExtractAsciiMarker(
            source,
            "private static void WriteM02OwnedCom1ReadyFailureLine(");

        MatchCollection methods = Regex.Matches(
            source,
            @"private static void (?<name>[A-Za-z0-9_]+)\(",
            RegexOptions.CultureInvariant);
        bool standaloneFormatterExists = methods
            .Cast<Match>()
            .Select(match =>
                ExtractAsciiMarker(
                    source,
                    "private static void " + match.Groups["name"].Value + "("))
            .Any(marker => marker == StandaloneMarker);

        return reconstructedReady == ReadyMarker
            && reconstructedFailure == FailureMarker
            && !standaloneFormatterExists
            && CountOccurrences(
                source,
                "WriteM02OwnedCom1ReadySuccessLine(") == 2
            && CountOccurrences(
                source,
                "WriteM02OwnedCom1ReadyFailureLine(") == 2
            && CountOccurrences(source, ReadyMarker) == 0
            && CountOccurrences(source, FailureMarker) == 0
            && !source.Contains(
                "\"" + StandaloneMarker + "\"",
                StringComparison.Ordinal);
    }

    private static bool TestQemuProfileSource() => EfiProfileTestControls.Returning("M02OwnedCom1Ready", OS.Tooling.QemuContracts.M02OwnedCom1ReadyRequiredMarkers, OS.Tooling.QemuContracts.M02OwnedCom1ReadyForbiddenMarkers, ReadyMarker, FailureMarker);

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
