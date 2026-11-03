using Microsoft.CodeAnalysis;
using OS.Tooling;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

internal static partial class M02OwnedCom1ReadyEfiIntegrationTests
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
        var catalog = OwnedCom1Sources();
        foreach (Type owner in new[]
        {
            typeof(OS.Kernel.Diagnostics.Com1OwnedPortIoBackend),
            typeof(OS.Kernel.Diagnostics.OwnedCom1ReadyExclusivelyOwnedWriter),
            typeof(OS.Kernel.Diagnostics.PanicSafeFormattingSerialReportingOwnedCom1),
            typeof(OS.Kernel.Diagnostics.OwnedX64GdtContainingNullN64BitCode),
            typeof(OS.Kernel.Diagnostics.GuardedISTStacksDoubleFaultPageFaultNMI),
            typeof(OS.Kernel.Diagnostics.OwnedGDTTSSGeneratedAuditedCSharpStubBytes),
            typeof(OS.Kernel.Diagnostics.NormalizeErrorCodeNonErrorCodeInterruptFrame),
            typeof(OS.Std.Binary.IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosa)
        }

        )
            _ = catalog.TypeSources(owner.FullName!);
        XElement[] defaults = project.Descendants("EnableM02OwnedCom1ReadyProbe").Where(element => element.Attribute("Condition")?.Value == "'$(EnableM02OwnedCom1ReadyProbe)' == ''").ToArray();
        XElement[] ud2ProfileOverrides = project.Descendants("PropertyGroup").Where(group => group.Attribute("Condition")?.Value == "'$(EnableM02Ud2VectorStateProbe)' == 'true'").SelectMany(group => group.Elements("EnableM02OwnedCom1ReadyProbe")).ToArray();
        XElement[] symbolGroups = project.Descendants("PropertyGroup").Where(element => element.Attribute("Condition")?.Value == "'$(EnableM02OwnedCom1ReadyProbe)' == 'true'").ToArray();
        string[] dependencyConditions = project.Descendants("Error").Select(element => element.Attribute("Condition")?.Value ?? string.Empty).Where(value => value.StartsWith("'$(EnableM02OwnedCom1ReadyProbe)' == 'true'", StringComparison.Ordinal)).ToArray();
        return (defaults.Length == 1
            && defaults[0].Value == "false"
            && defaults[0].Attribute("Condition")?.Value == "'$(EnableM02OwnedCom1ReadyProbe)' == ''"
            && ud2ProfileOverrides.Length == 1
            && ud2ProfileOverrides[0].Value == "true"
            && symbolGroups.Length == 1
            && symbolGroups[0].Elements("DefineConstants").Single().Value == "$(DefineConstants);M02_OWNED_COM1_READY_PROBE"
            && dependencyConditions.Contains("'$(EnableM02OwnedCom1ReadyProbe)' == 'true' and '$(EnableM04GcStaticRootMaterializationProbe)' == 'true'", StringComparer.Ordinal)
            && dependencyConditions.Contains("'$(EnableM02OwnedCom1ReadyProbe)' == 'true' and '$(EnableM03TypedExitBootServicesCallProbe)' == 'true'", StringComparer.Ordinal)
            && dependencyConditions.Contains("'$(EnableM02OwnedCom1ReadyProbe)' == 'true' and '$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true'", StringComparer.Ordinal)
            && dependencyConditions.Contains("'$(EnableM02OwnedCom1ReadyProbe)' == 'true' and '$(EnableM04CheckedReferenceBarrierProbe)' == 'true'", StringComparer.Ordinal)
            && dependencyConditions.Contains("'$(EnableM02OwnedCom1ReadyProbe)' == 'true' and '$(EnableM03MemoryMapCaptureProbe)' == 'true'", StringComparer.Ordinal)
            && dependencyConditions.Contains("'$(EnableM02OwnedCom1ReadyProbe)' == 'true' and '$(EnableM03FirmwareEntropySeedProbe)' == 'true'", StringComparer.Ordinal)
            && dependencyConditions.Contains("'$(EnableM02OwnedCom1ReadyProbe)' == 'true' and '$(EnableM04SzArrayAllocationProbe)' == 'true'", StringComparer.Ordinal)
            && dependencyConditions.Contains("'$(EnableM02OwnedCom1ReadyProbe)' == 'true' and '$(EnableM04StringAllocationProbe)' == 'true'", StringComparer.Ordinal)
            && dependencyConditions.Contains("'$(EnableM02OwnedCom1ReadyProbe)' == 'true' and '$(EnableM04StringAllocationBeforeBindProbe)' == 'true'", StringComparer.Ordinal)
            && dependencyConditions.Contains("'$(EnableM02OwnedCom1ReadyProbe)' == 'true' and '$(EnableM04StringAllocationExhaustionProbe)' == 'true'", StringComparer.Ordinal)
            && dependencyConditions.Contains("'$(EnableM02OwnedCom1ReadyProbe)' == 'true' and '$(EnableM04StringAllocationInvalidLengthProbe)' == 'true'", StringComparer.Ordinal)
            && dependencyConditions.Contains("'$(EnableM02OwnedCom1ReadyProbe)' == 'true' and '$(EnableM04SzArrayAllocationBeforeBindProbe)' == 'true'", StringComparer.Ordinal)
            && dependencyConditions.Contains("'$(EnableM02OwnedCom1ReadyProbe)' == 'true' and '$(EnableM04SzArrayAllocationExhaustionProbe)' == 'true'", StringComparer.Ordinal)
            && dependencyConditions.Contains("'$(EnableM02OwnedCom1ReadyProbe)' == 'true' and '$(EnableM02Com1OwnedPortIoProbe)' != 'true'", StringComparer.Ordinal))
            && OS.Tooling.ContractAudit.Inspect(root).Count == 0;
    }

    private static bool TestEntryCallOrderAndResultGate()
    {
        var catalog = OwnedCom1Sources();
        string source = HostSourceText.Active(OwnedCom1Caller(catalog));
        string rawSource = HostSourceText.Documents(catalog, "OS.Boot.EfiEntry");
        var panic = OwnedPanicSources();
        int m02Configuration = HostSourceText.IndexOf(source, "M02Com1OwnedPortIoBackendIntegration.Configure(");
        int m02ConfigurationGuard = HostSourceText.IndexOf(source, "if (!com1Configuration.Succeeded)", m02Configuration);
        int m02Ready = HostSourceText.IndexOf(source, "WriteM02Com1OwnedPortIoSuccessLine(", m02ConfigurationGuard);
        int backendConstruction = HostSourceText.IndexOf(source, "new OS.Kernel.Diagnostics.Com1OwnedPortIoBackend(", m02Ready);
        int writerCall = HostSourceText.IndexOf(source, ".Emit(ownedCom1Backend);", backendConstruction);
        int resultGuard = HostSourceText.IndexOf(source, "if (!ownedCom1Ready.Succeeded ||", writerCall);
        int successLine = HostSourceText.IndexOf(source, "WriteM02OwnedCom1ReadySuccessLine(", resultGuard);
        int stageThreeLineLoop = HostSourceText.IndexOf(source, "GetM01StageThreeSuccessLineLength(", successLine);
        return m02Configuration >= 0 && m02ConfigurationGuard > m02Configuration && m02Ready > m02ConfigurationGuard && backendConstruction > m02Ready && writerCall > backendConstruction && resultGuard > writerCall && successLine > resultGuard && stageThreeLineLoop > successLine && HostSourceText.Contains(source, "ownedCom1Ready.Configuration.WritesCompleted != 7") && HostSourceText.Contains(source, "ownedCom1Ready.MarkerBytesWritten != 16") && HostSourceText.Contains(source, "ownedCom1Ready.LineEndingBytesWritten != 2") && HostSourceText.Contains(source, "ownedCom1Ready.LineStatusPolls != 18") && HostSourceText.Contains(source, "!ownedCom1Ready.OwnedBackendUsed") && HostSourceText.Contains(source, "ownedCom1Ready.FirmwareFallbackUsed") && CountOccurrences(rawSource, ".OwnedCom1ReadyExclusivelyOwnedWriter") == 1 && CountOccurrences(rawSource, ".Emit(ownedCom1Backend);") == 1 && HasExpectedBackendConstructions(panic);
    }

    private static bool TestMarkerTransportSeparation()
    {
        var catalog = OwnedCom1Sources();
        string source = HostSourceText.Documents(catalog, "OS.Boot.EfiEntry");
        string reconstructedReady = OwnedCom1Marker(catalog, "WriteM02OwnedCom1ReadySuccessLine");
        string reconstructedFailure = OwnedCom1Marker(catalog, "WriteM02OwnedCom1ReadyFailureLine");
        bool standaloneFormatterExists = HostSourceText.HasByteFormatter(catalog, "OS.Boot.EfiEntry", StandaloneMarker);
        return reconstructedReady == ReadyMarker && reconstructedFailure == FailureMarker && !standaloneFormatterExists && CountOccurrences(source, "WriteM02OwnedCom1ReadySuccessLine(") == 2 && CountOccurrences(source, "WriteM02OwnedCom1ReadyFailureLine(") == 2 && CountOccurrences(source, ReadyMarker) == 0 && CountOccurrences(source, FailureMarker) == 0 && !source.Contains("\"" + StandaloneMarker + "\"", StringComparison.Ordinal);
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
