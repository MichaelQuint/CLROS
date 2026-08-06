using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

internal static class M02OwnedCom1ReadyEfiIntegrationTests
{
    private const string TestName = "m02.005.efi.integration";
    private const string StandaloneMarker = "OWNED_COM1_READY";
    private const string ReadyMarker =
        "CLROS_M02_005_READY marker=OWNED_COM1_READY markerBytes=16 lineEndingBytes=2 " +
        "configurationWrites=7 lineStatusPolls=18 ownedWriter=true " +
        "ownedMarkerTransport=com1PortIo evidenceMarkerTransport=uefiSerial " +
        "guestPortIo=true guestIn8=true postExitBootServices=false";
    private const string FailureMarker = "M02_OWNED_COM1_READY_FAILED";
    private const string CompileClosureSha256 =
        "b19e83c1f421460ade201addcfaee4b579871ac4a15eeb3d84618ef9de686a77";

    [ModuleInitializer]
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
        string compileText = string.Join("\n", compileKeys) + "\n";
        string compileHash = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(compileText)))
            .ToLowerInvariant();

        const string BackendKey =
            @"..\OS.Kernel\Diagnostics\COM1OwnedPortIOBackend.cs|Diagnostics\COM1OwnedPortIOBackend.cs";
        const string WriterKey =
            @"..\OS.Kernel\Diagnostics\OWNEDCOM1READYExclusivelyOwnedWriter.cs|Diagnostics\OWNEDCOM1READYExclusivelyOwnedWriter.cs";
        const string PanicReporterKey =
            @"..\OS.Kernel\Diagnostics\PanicSafeFormattingSerialReportingOwnedCOM1.cs|Diagnostics\PanicSafeFormattingSerialReportingOwnedCOM1.cs";
        const string GdtKey =
            @"..\OS.Kernel\Diagnostics\OwnedX64GDTContainingNullN64BitCode.cs|Diagnostics\OwnedX64GDTContainingNullN64BitCode.cs";
        const string CatalogKey =
            @"..\..\std\no-runtime\shared\IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMOSA.cs|Std\IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMOSA.cs";
        int backendIndex = Array.IndexOf(compileKeys, BackendKey);
        int writerIndex = Array.IndexOf(compileKeys, WriterKey);
        int panicReporterIndex = Array.IndexOf(compileKeys, PanicReporterKey);
        int gdtIndex = Array.IndexOf(compileKeys, GdtKey);
        int catalogIndex = Array.IndexOf(compileKeys, CatalogKey);

        XElement[] defaults = project
            .Descendants("EnableM02OwnedCom1ReadyProbe")
            .Where(element => element.Parent?.Name.LocalName == "PropertyGroup")
            .ToArray();
        XElement[] symbolGroups = project
            .Descendants("PropertyGroup")
            .Where(element =>
                element.Attribute("Condition")?.Value ==
                    "'$(EnableM02OwnedCom1ReadyProbe)' == 'true'")
            .ToArray();
        string[] dependencyConditions = project
            .Descendants("Error")
            .Select(element => element.Attribute("Condition")?.Value ?? string.Empty)
            .Where(value => value.StartsWith(
                "'$(EnableM02OwnedCom1ReadyProbe)' == 'true'",
                StringComparison.Ordinal))
            .ToArray();

        string buildScript = File.ReadAllText(
            Path.Combine(root, "scripts", "build-efi-shell.ps1"));
        string verifierScript = File.ReadAllText(
            Path.Combine(root, "scripts", "test-efi-artifact.ps1"));

        return compileItems.Length == 56
            && compileHash == CompileClosureSha256
            && backendIndex >= 0
            && writerIndex == backendIndex + 1
            && panicReporterIndex == writerIndex + 1
            && gdtIndex == panicReporterIndex + 1
            && catalogIndex == gdtIndex + 1
            && compileKeys.Count(key => key == WriterKey) == 1
            && compileKeys.Count(key => key == PanicReporterKey) == 1
            && defaults.Length == 1
            && defaults[0].Value == "false"
            && defaults[0].Attribute("Condition")?.Value ==
                "'$(EnableM02OwnedCom1ReadyProbe)' == ''"
            && symbolGroups.Length == 1
            && symbolGroups[0].Elements("DefineConstants").Single().Value ==
                "$(DefineConstants);M02_OWNED_COM1_READY_PROBE"
            && dependencyConditions.Length == 1
            && dependencyConditions[0].Contains(
                "EnableM02Com1OwnedPortIoProbe",
                StringComparison.Ordinal)
            && buildScript.Contains(
                "[switch] $EnableM02OwnedCom1ReadyProbe",
                StringComparison.Ordinal)
            && buildScript.Contains(
                "$arguments += \"-p:EnableM02OwnedCom1ReadyProbe=true\"",
                StringComparison.Ordinal)
            && buildScript.Contains(
                "m02_owned_com1_ready_requires_com1_owned_port_io",
                StringComparison.Ordinal)
            && buildScript.Contains(
                "EFI_PUBLISH_SOURCE_AUTHORITY status=ok inputs=69",
                StringComparison.Ordinal)
            && buildScript.Contains(
                "\"-EnableM02OwnedCom1ReadyProbe\",\n            \"true\"",
                StringComparison.Ordinal)
            && buildScript.Contains(
                "$parameterName -ceq \"EnableM02OwnedCom1ReadyProbe\"",
                StringComparison.Ordinal)
            && verifierScript.Contains(
                "[switch]$EnableM02OwnedCom1ReadyProbe",
                StringComparison.Ordinal)
            && verifierScript.Contains(
                "$expectedSourceClosureInputCount = 69",
                StringComparison.Ordinal)
            && verifierScript.Contains(
                "$compileItems.Count -ne 56",
                StringComparison.Ordinal)
            && verifierScript.Contains(
                "-p:EnableM02OwnedCom1ReadyProbe=true",
                StringComparison.Ordinal);
    }

    private static bool TestEntryCallOrderAndResultGate()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(
            Path.Combine(root, "src", "OS", "Boot", "EfiEntry.cs"));

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
            && CountOccurrences(
                source,
                "new OS.Kernel.Diagnostics.Com1OwnedPortIoBackend(") == 1;
    }

    private static bool TestMarkerTransportSeparation()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(
            Path.Combine(root, "src", "OS", "Boot", "EfiEntry.cs"));
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

    private static bool TestQemuProfileSource()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(
            Path.Combine(root, "scripts", "run-qemu-smoke.ps1"));

        int ownedProfile = source.IndexOf(
            "if ($EnableM02OwnedCom1ReadyProbe) {",
            StringComparison.Ordinal);
        int priorProfile = IndexOfAfter(
            source,
            "elseif ($EnableM02Com1OwnedPortIoProbe) {",
            ownedProfile);

        return source.Contains(
                "[switch] $EnableM02OwnedCom1ReadyProbe",
                StringComparison.Ordinal)
            && source.Contains(
                "$script:M02OwnedCom1ReadyStandaloneMarker = \"" +
                    StandaloneMarker + "\"",
                StringComparison.Ordinal)
            && source.Contains(ReadyMarker, StringComparison.Ordinal)
            && source.Contains(FailureMarker, StringComparison.Ordinal)
            && source.Contains(
                "$script:M02OwnedCom1ReadyRequiredMarkers",
                StringComparison.Ordinal)
            && source.Contains(
                "$script:M02OwnedCom1ReadyForbiddenMarkers",
                StringComparison.Ordinal)
            && source.Contains(
                "m02_owned_com1_ready_probe_requires_com1_owned_port_io",
                StringComparison.Ordinal)
            && source.Contains(
                "m02_owned_com1_ready_probe_requires_build_efi",
                StringComparison.Ordinal)
            && source.Contains(
                "m02_owned_com1_ready_probe_forbids_skip_image_build",
                StringComparison.Ordinal)
            && source.Contains(
                "$buildArguments += \"-EnableM02OwnedCom1ReadyProbe\"",
                StringComparison.Ordinal)
            && source.Contains(
                "QEMU_SCRIPT_M02_OWNED_COM1_READY_PREREQUISITES",
                StringComparison.Ordinal)
            && CountOccurrences(
                source,
                "[System.Management.Automation.SwitchParameter]::new($true)") == 3
            && source.Contains(
                "profileRequired=52 profileForbidden=37",
                StringComparison.Ordinal)
            && ownedProfile >= 0
            && priorProfile > ownedProfile;
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
