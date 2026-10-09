using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using OS.Kernel.Diagnostics;

internal static class M02PanicSafeOwnedCom1EfiIntegrationTests
{
    private const string TestName = "m02.006.efi.integration";
    private const string ReadyMarker =
        OS.Tooling.QemuContracts.Record115;
    private const string EvidenceMarker =
        OS.Tooling.QemuContracts.Record116;
    private const string FailureMarker = OS.Tooling.QemuContracts.Record109;
    private static readonly string CompileClosureSha256 =
        OS.Tooling.EfiContracts.CompileInventorySha256;
    private const string CanonicalProjectSha256 =
        OS.Tooling.EfiContracts.CanonicalProjectSha256;

    [HostFixture(HostLane.Fast)]
    internal static void RunAll()
    {
        Console.WriteLine("TEST_START " + TestName);
        int failures = 0;

        RunCase(
            "m02.006 EFI closure links one default-off panic reporter",
            TestCompileClosureAndSwitch,
            ref failures);
        RunCase(
            "m02.006 EFI calls the panic reporter after m02.005",
            TestEntryCallOrderAndResultGate,
            ref failures);
        RunCase(
            "m02.006 direct and UEFI marker transports remain separate",
            TestMarkerTransportSeparation,
            ref failures);
        RunCase(
            "m02.006 QEMU profile enforces exact positive and negative evidence",
            TestQemuProfileSource,
            ref failures);

        if (failures != 0)
        {
            Console.WriteLine(
                "TEST_FAIL " + TestName + " host-contract failures=" + failures);
            throw new InvalidOperationException(
                "M02-006 EFI integration host tests failed: " + failures);
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
    string compileHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", compileKeys) + "\n"))).ToLowerInvariant();
    const string WriterKey = @"..\OS.Kernel\Diagnostics\OWNEDCOM1READYExclusivelyOwnedWriter.cs|Diagnostics\OWNEDCOM1READYExclusivelyOwnedWriter.cs";
    const string ReporterKey = @"..\OS.Kernel\Diagnostics\PanicSafeFormattingSerialReportingOwnedCOM1.cs|Diagnostics\PanicSafeFormattingSerialReportingOwnedCOM1.cs";
    const string GdtKey = @"..\OS.Kernel\Diagnostics\OwnedX64GDTContainingNullN64BitCode.cs|Diagnostics\OwnedX64GDTContainingNullN64BitCode.cs";
    const string GuardedIstKey = @"..\OS.Kernel\Diagnostics\GuardedISTStacksDoubleFaultPageFaultNMI.cs|Diagnostics\GuardedISTStacksDoubleFaultPageFaultNMI.cs";
    const string TerminalKey = @"..\OS.Kernel\Diagnostics\OwnedGDTTSSGeneratedAuditedCSharpStubBytes.cs|Diagnostics\OwnedGDTTSSGeneratedAuditedCSharpStubBytes.cs";
    const string NormalizedInterruptFrameKey = @"..\OS.Kernel\Diagnostics\NormalizeErrorCodeNonErrorCodeInterruptFrame.cs|Diagnostics\NormalizeErrorCodeNonErrorCodeInterruptFrame.cs";
    const string CatalogKey = @"..\..\std\no-runtime\shared\Binary\IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMOSA.cs|Std\Binary\IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMOSA.cs";
    int writerIndex = Array.IndexOf(compileKeys, WriterKey);
    int reporterIndex = Array.IndexOf(compileKeys, ReporterKey);
    int gdtIndex = Array.IndexOf(compileKeys, GdtKey);
    int guardedIstIndex = Array.IndexOf(compileKeys, GuardedIstKey);
    int terminalIndex = Array.IndexOf(compileKeys, TerminalKey);
    int normalizedInterruptFrameIndex = Array.IndexOf(compileKeys, NormalizedInterruptFrameKey);
    int catalogIndex = Array.IndexOf(compileKeys, CatalogKey);
    XElement[] defaults = project.Descendants("EnableM02PanicSafeOwnedCom1Probe").Where(element => element.Attribute("Condition")?.Value == "'$(EnableM02PanicSafeOwnedCom1Probe)' == ''").ToArray();
    XElement[] ud2ProfileOverrides = project.Descendants("PropertyGroup").Where(group => group.Attribute("Condition")?.Value == "'$(EnableM02Ud2VectorStateProbe)' == 'true'").SelectMany(group => group.Elements("EnableM02PanicSafeOwnedCom1Probe")).ToArray();
    XElement[] symbolGroups = project.Descendants("PropertyGroup").Where(element => element.Attribute("Condition")?.Value == "'$(EnableM02PanicSafeOwnedCom1Probe)' == 'true'").ToArray();
    string[] dependencyConditions = project.Descendants("Error").Select(element => element.Attribute("Condition")?.Value ?? string.Empty).Where(value => value.StartsWith("'$(EnableM02PanicSafeOwnedCom1Probe)' == 'true'", StringComparison.Ordinal)).ToArray();
    return (compileItems.Length == OS.Tooling.EfiContracts.CompileItems.Count && compileHash == CompileClosureSha256 && writerIndex >= 0 && reporterIndex == writerIndex + 1 && gdtIndex == reporterIndex + 1 && guardedIstIndex == gdtIndex + 1 && terminalIndex == guardedIstIndex + 1 && normalizedInterruptFrameIndex == terminalIndex + 1 && catalogIndex == normalizedInterruptFrameIndex + 17 && compileKeys.Count(key => key == ReporterKey) == 1 && defaults.Length == 1 && defaults[0].Value == "false" && defaults[0].Attribute("Condition")?.Value == "'$(EnableM02PanicSafeOwnedCom1Probe)' == ''" && ud2ProfileOverrides.Length == 1 && ud2ProfileOverrides[0].Value == "true" && symbolGroups.Length == 1 && symbolGroups[0].Elements("DefineConstants").Single().Value == "$(DefineConstants);M02_PANIC_SAFE_OWNED_COM1_PROBE" && dependencyConditions.Length == 14 && dependencyConditions.Contains("'$(EnableM02PanicSafeOwnedCom1Probe)' == 'true' and '$(EnableM04GcStaticRootMaterializationProbe)' == 'true'", StringComparer.Ordinal) && dependencyConditions.Contains("'$(EnableM02PanicSafeOwnedCom1Probe)' == 'true' and '$(EnableM03TypedExitBootServicesCallProbe)' == 'true'", StringComparer.Ordinal) && dependencyConditions.Contains("'$(EnableM02PanicSafeOwnedCom1Probe)' == 'true' and '$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true'", StringComparer.Ordinal) && dependencyConditions.Contains("'$(EnableM02PanicSafeOwnedCom1Probe)' == 'true' and '$(EnableM04CheckedReferenceBarrierProbe)' == 'true'", StringComparer.Ordinal) && dependencyConditions.Contains("'$(EnableM02PanicSafeOwnedCom1Probe)' == 'true' and '$(EnableM03MemoryMapCaptureProbe)' == 'true'", StringComparer.Ordinal) && dependencyConditions.Contains("'$(EnableM02PanicSafeOwnedCom1Probe)' == 'true' and '$(EnableM03FirmwareEntropySeedProbe)' == 'true'", StringComparer.Ordinal) && dependencyConditions.Contains("'$(EnableM02PanicSafeOwnedCom1Probe)' == 'true' and '$(EnableM04SzArrayAllocationProbe)' == 'true'", StringComparer.Ordinal) && dependencyConditions.Contains("'$(EnableM02PanicSafeOwnedCom1Probe)' == 'true' and '$(EnableM04StringAllocationProbe)' == 'true'", StringComparer.Ordinal) && dependencyConditions.Contains("'$(EnableM02PanicSafeOwnedCom1Probe)' == 'true' and '$(EnableM04StringAllocationBeforeBindProbe)' == 'true'", StringComparer.Ordinal) && dependencyConditions.Contains("'$(EnableM02PanicSafeOwnedCom1Probe)' == 'true' and '$(EnableM04StringAllocationExhaustionProbe)' == 'true'", StringComparer.Ordinal) && dependencyConditions.Contains("'$(EnableM02PanicSafeOwnedCom1Probe)' == 'true' and '$(EnableM04StringAllocationInvalidLengthProbe)' == 'true'", StringComparer.Ordinal) && dependencyConditions.Contains("'$(EnableM02PanicSafeOwnedCom1Probe)' == 'true' and '$(EnableM04SzArrayAllocationBeforeBindProbe)' == 'true'", StringComparer.Ordinal) && dependencyConditions.Contains("'$(EnableM02PanicSafeOwnedCom1Probe)' == 'true' and '$(EnableM04SzArrayAllocationExhaustionProbe)' == 'true'", StringComparer.Ordinal) && dependencyConditions.Contains("'$(EnableM02PanicSafeOwnedCom1Probe)' == 'true' and '$(EnableM02OwnedCom1ReadyProbe)' != 'true'", StringComparer.Ordinal)) && OS.Tooling.ContractAudit.Inspect(root).Count == 0;
}

    private static bool TestEntryCallOrderAndResultGate()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(
            Path.Combine(root, "src", "OS", "Boot", "EfiEntry.cs")).Replace("\r\n", "\n", StringComparison.Ordinal);

        int priorSuccess = source.IndexOf(
            "WriteM02OwnedCom1ReadySuccessLine(",
            StringComparison.Ordinal);
        int reporterCall = IndexOfAfter(
            source,
            ".EmitReadinessMarker(ownedCom1Backend);",
            priorSuccess);
        int resultGuard = IndexOfAfter(
            source,
            "if (!panicSafeOwnedCom1.Succeeded ||",
            reporterCall);
        int failureLine = IndexOfAfter(
            source,
            "WriteM02PanicSafeOwnedCom1FailureLine(",
            resultGuard);
        int evidenceLine = IndexOfAfter(
            source,
            "WriteM02PanicSafeOwnedCom1EvidenceLine(",
            failureLine);
        int stageThreeLineLoop = IndexOfAfter(
            source,
            "GetM01StageThreeSuccessLineLength(",
            evidenceLine);

        return priorSuccess >= 0
            && reporterCall > priorSuccess
            && resultGuard > reporterCall
            && failureLine > resultGuard
            && evidenceLine > failureLine
            && stageThreeLineLoop > evidenceLine
            && source.Contains(
                "panicSafeOwnedCom1.Format.RequiredBytes !=",
                StringComparison.Ordinal)
            && source.Contains(
                ".ReadinessSerialByteCount",
                StringComparison.Ordinal)
            && source.Contains(
                "panicSafeOwnedCom1.Configuration.WritesCompleted !=",
                StringComparison.Ordinal)
            && source.Contains(
                ".RequiredConfigurationWriteCount",
                StringComparison.Ordinal)
            && source.Contains(
                "panicSafeOwnedCom1.LineStatusPolls !=",
                StringComparison.Ordinal)
            && source.Contains(
                "panicSafeOwnedCom1.FailedByteIndex != -1",
                StringComparison.Ordinal)
            && source.Contains(
                "!panicSafeOwnedCom1.OwnedBackendUsed",
                StringComparison.Ordinal)
            && source.Contains(
                "panicSafeOwnedCom1.FirmwareFallbackUsed",
                StringComparison.Ordinal)
            && source.Contains(
                ".DataRegisterPort",
                StringComparison.Ordinal)
            && source.Contains(
                ".LineFeed",
                StringComparison.Ordinal)
            && CountOccurrences(
                source,
                ".EmitReadinessMarker(ownedCom1Backend);") == 1;
    }

    private static bool TestMarkerTransportSeparation()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(
            Path.Combine(root, "src", "OS", "Boot", "EfiEntry.cs")).Replace("\r\n", "\n", StringComparison.Ordinal);
        string reconstructedEvidence = ExtractAsciiMarker(
            source,
            "private static void WriteM02PanicSafeOwnedCom1EvidenceLine(");
        string reconstructedFailure = ExtractAsciiMarker(
            source,
            "private static void WriteM02PanicSafeOwnedCom1FailureLine(");

        var productionReady = new StringBuilder(
            PanicSafeFormattingSerialReportingOwnedCom1.ReadinessMarkerByteCount);
        for (int index = 0;
            index < PanicSafeFormattingSerialReportingOwnedCom1.ReadinessMarkerByteCount;
            index++)
        {
            if (!PanicSafeFormattingSerialReportingOwnedCom1
                    .TryGetReadinessMarkerByte(index, out byte value))
            {
                return false;
            }

            productionReady.Append((char)value);
        }

        MatchCollection methods = Regex.Matches(
            source,
            @"private static void (?<name>[A-Za-z0-9_]+)\(",
            RegexOptions.CultureInvariant);
        bool directReadyFormatterExists = methods
            .Cast<Match>()
            .Select(match =>
                ExtractAsciiMarker(
                    source,
                    "private static void " + match.Groups["name"].Value + "("))
            .Any(marker => marker == ReadyMarker);

        return productionReady.ToString() == ReadyMarker
            && productionReady.Length == 125
            && PanicSafeFormattingSerialReportingOwnedCom1.ReadinessSerialByteCount ==
                productionReady.Length + 2
            && reconstructedEvidence == EvidenceMarker
            && reconstructedFailure == FailureMarker
            && !directReadyFormatterExists
            && !source.Contains("\"" + ReadyMarker + "\"", StringComparison.Ordinal)
            && CountOccurrences(
                source,
                "WriteM02PanicSafeOwnedCom1EvidenceLine(") == 2
            && CountOccurrences(
                source,
                "WriteM02PanicSafeOwnedCom1FailureLine(") == 2;
    }

    private static bool TestQemuProfileSource() => EfiProfileTestControls.Returning("M02PanicSafeOwnedCom1", OS.Tooling.QemuContracts.M02PanicSafeOwnedCom1RequiredMarkers, OS.Tooling.QemuContracts.M02PanicSafeOwnedCom1ForbiddenMarkers, ReadyMarker, FailureMarker);

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
