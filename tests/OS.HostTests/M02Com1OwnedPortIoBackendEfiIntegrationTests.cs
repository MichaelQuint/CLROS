using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using OS.Kernel.Diagnostics;

internal static class M02Com1OwnedPortIoBackendEfiIntegrationTests
{
    private const string TestName = "m02.004.efi.integration";
    private const string ReadyMarker =
        "CLROS_M02_004_READY configurationWrites=7 bindingReady=true guestPortIo=true guestIn8=false markerTransport=uefiSerial postExitBootServices=false";
    private const string FailureMarker =
        "M02_COM1_CONFIGURATION_FAILED";
    private const string CompileClosureSha256 =
        "30dd633e38173653aa4b75b8059fa3036097f082284defc2335dbd8f9a9d7d66";

    [ModuleInitializer]
    internal static void RunAll()
    {
        Console.WriteLine("TEST_START " + TestName);
        int failures = 0;

        RunCase(
            "m02.004 EFI compile closure and default-false switch are exact",
            TestCompileClosureAndSwitch,
            ref failures);
        RunCase(
            "m02.004 EFI helper performs one bounded write-only configuration",
            TestIntegrationSourceBoundary,
            ref failures);
        RunCase(
            "m02.004 EFI marker is ordered after checked binding and configuration",
            TestMarkerOwnershipAndOrder,
            ref failures);
        RunCase(
            "m02.004 QEMU profile owns exact guest success and failure markers",
            TestQemuProfileSource,
            ref failures);

        if (failures != 0)
        {
            Console.WriteLine(
                "TEST_FAIL " + TestName + " host-contract failures=" + failures);
            throw new InvalidOperationException(
                "M02-004 EFI integration host tests failed: " + failures);
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

        int checkedBinder = Array.IndexOf(
            compileKeys,
            @"..\OS.Kernel\Diagnostics\InvokeIn8Out8CheckedUnmanagedFunctionPointers.cs|Diagnostics\InvokeIn8Out8CheckedUnmanagedFunctionPointers.cs");
        int backend = Array.IndexOf(
            compileKeys,
            @"..\OS.Kernel\Diagnostics\COM1OwnedPortIOBackend.cs|Diagnostics\COM1OwnedPortIOBackend.cs");
        int checkedHelper = Array.IndexOf(
            compileKeys,
            @"Boot\M02CheckedPortIoFunctionPointerIntegration.cs|");
        int com1Helper = Array.IndexOf(
            compileKeys,
            @"Boot\M02Com1OwnedPortIoBackendIntegration.cs|");

        XElement[] defaults = project
            .Descendants("EnableM02Com1OwnedPortIoProbe")
            .Where(element => element.Attribute("Condition")?.Value ==
                "'$(EnableM02Com1OwnedPortIoProbe)' == ''")
            .ToArray();
        XElement[] ud2ProfileOverrides = project
            .Descendants("PropertyGroup")
            .Where(group => group.Attribute("Condition")?.Value ==
                "'$(EnableM02Ud2VectorStateProbe)' == 'true'")
            .SelectMany(group => group.Elements(
                "EnableM02Com1OwnedPortIoProbe"))
            .ToArray();
        XElement[] symbols = project
            .Descendants("PropertyGroup")
            .Where(element =>
                element.Attribute("Condition")?.Value ==
                    "'$(EnableM02Com1OwnedPortIoProbe)' == 'true'")
            .ToArray();
        string[] dependencyConditions = project
            .Descendants("Error")
            .Select(element => element.Attribute("Condition")?.Value ?? string.Empty)
            .Where(value => value.Contains(
                "'$(EnableM02Com1OwnedPortIoProbe)' == 'true'",
                StringComparison.Ordinal))
            .ToArray();

        string buildScript = File.ReadAllText(
            Path.Combine(root, "scripts", "build-efi-shell.ps1"));
        string artifactScript = File.ReadAllText(
            Path.Combine(root, "scripts", "test-efi-artifact.ps1"));

        return compileItems.Length == 69
            && compileHash == CompileClosureSha256
            && checkedBinder >= 0
            && backend == checkedBinder + 1
            && checkedHelper >= 0
            && com1Helper == checkedHelper + 1
            && compileKeys.Count(key => key.Contains(
                "COM1OwnedPortIOBackend.cs",
                StringComparison.Ordinal)) == 1
            && compileKeys.Count(key => key.Contains(
                "M02Com1OwnedPortIoBackendIntegration.cs",
                StringComparison.Ordinal)) == 1
            && defaults.Length == 1
            && defaults[0].Value == "false"
            && defaults[0].Attribute("Condition")?.Value ==
                "'$(EnableM02Com1OwnedPortIoProbe)' == ''"
            && ud2ProfileOverrides.Length == 1
            && ud2ProfileOverrides[0].Value == "true"
            && symbols.Length == 1
            && symbols[0].Elements("DefineConstants").Single().Value ==
                "$(DefineConstants);M02_COM1_OWNED_PORT_IO_PROBE"
            && dependencyConditions.Length == 1
            && dependencyConditions[0].Contains(
                "EnableM02CheckedPortIoBindingProbe",
                StringComparison.Ordinal)
            && buildScript.Contains(
                "[switch] $EnableM02Com1OwnedPortIoProbe",
                StringComparison.Ordinal)
            && buildScript.Contains(
                "$arguments += \"-p:EnableM02Com1OwnedPortIoProbe=true\"",
                StringComparison.Ordinal)
            && buildScript.Contains(
                "m02_com1_owned_port_io_requires_checked_binding",
                StringComparison.Ordinal)
            && buildScript.Contains(
                "EFI_PUBLISH_SOURCE_AUTHORITY status=ok inputs=82",
                StringComparison.Ordinal)
            && artifactScript.Contains(
                "$expectedSourceClosureInputCount = 82",
                StringComparison.Ordinal)
            && artifactScript.Contains(
                "$compileItems.Count -ne 69",
                StringComparison.Ordinal);
    }

    private static bool TestIntegrationSourceBoundary()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string checkedHelper = File.ReadAllText(
            Path.Combine(
                root,
                "src",
                "OS",
                "Boot",
                "M02CheckedPortIoFunctionPointerIntegration.cs"));
        string integration = File.ReadAllText(
            Path.Combine(
                root,
                "src",
                "OS",
                "Boot",
                "M02Com1OwnedPortIoBackendIntegration.cs"));

        return checkedHelper.Contains(
                "Binding => _binding;",
                StringComparison.Ordinal)
            && checkedHelper.Contains("_binding.Succeeded", StringComparison.Ordinal)
            && integration.Contains(
                "public readonly struct M02Com1OwnedPortIoBackendIntegrationResult",
                StringComparison.Ordinal)
            && integration.Contains(
                "new(checkedBinding.Binding);",
                StringComparison.Ordinal)
            && CountOccurrences(integration, "backend.ConfigureDefault();") == 1
            && integration.Contains(
                "public const bool GuestIn8 = false;",
                StringComparison.Ordinal)
            && integration.Contains(
                "public const bool MarkerTransportUsesUefiSerial = true;",
                StringComparison.Ordinal)
            && integration.Contains(
                "public const bool PostExitBootServices = false;",
                StringComparison.Ordinal)
            && !integration.Contains(".ReadByte(", StringComparison.Ordinal)
            && !integration.Contains(".InvokeIn8(", StringComparison.Ordinal)
            && !integration.Contains(".InvokeOut8(", StringComparison.Ordinal)
            && !integration.Contains("throw ", StringComparison.Ordinal)
            && !integration.Contains("string ", StringComparison.Ordinal)
            && !integration.Contains("object ", StringComparison.Ordinal)
            && !integration.Contains("delegate", StringComparison.Ordinal)
            && !integration.Contains("[]", StringComparison.Ordinal)
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                Com1OwnedPortIoBackend>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                Com1OwnedPortIoConfigurationResult>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                Com1OwnedPortIoOperationResult>();
    }

    private static bool TestMarkerOwnershipAndOrder()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(
            Path.Combine(root, "src", "OS", "Boot", "EfiEntry.cs"));
        string reconstructedReady = ExtractAsciiMarker(
            source,
            "private static void WriteM02Com1OwnedPortIoSuccessLine(");
        string reconstructedFailure = ExtractAsciiMarker(
            source,
            "private static void WriteM02Com1OwnedPortIoFailureLine(");

        int executionAudit = source.IndexOf(
            "if (!s_m02ExecutionAudit.Succeeded)",
            StringComparison.Ordinal);
        int checkedBinding = IndexOfAfter(
            source,
            "M02CheckedPortIoFunctionPointerIntegration.Bind(",
            executionAudit,
            StringComparison.Ordinal);
        int checkedGuard = IndexOfAfter(
            source,
            "if (!s_m02CheckedPortIoBinding.Succeeded)",
            checkedBinding,
            StringComparison.Ordinal);
        int configuration = IndexOfAfter(
            source,
            "M02Com1OwnedPortIoBackendIntegration.Configure(",
            checkedGuard,
            StringComparison.Ordinal);
        int configurationGuard = IndexOfAfter(
            source,
            "if (!com1Configuration.Succeeded)",
            configuration,
            StringComparison.Ordinal);
        int checkedReady = IndexOfAfter(
            source,
            "WriteM02CheckedPortIoBindingSuccessLine(",
            configurationGuard,
            StringComparison.Ordinal);
        int com1Ready = IndexOfAfter(
            source,
            "WriteM02Com1OwnedPortIoSuccessLine(",
            checkedReady,
            StringComparison.Ordinal);

        return reconstructedReady == ReadyMarker
            && reconstructedFailure == FailureMarker
            && executionAudit >= 0
            && checkedBinding > executionAudit
            && checkedGuard > checkedBinding
            && configuration > checkedGuard
            && configurationGuard > configuration
            && checkedReady > configurationGuard
            && com1Ready > checkedReady
            && CountOccurrences(
                source,
                "M02Com1OwnedPortIoBackendIntegration.Configure(") == 1
            && CountOccurrences(
                source,
                "WriteM02Com1OwnedPortIoSuccessLine(") == 2
            && CountOccurrences(
                source,
                "WriteM02Com1OwnedPortIoFailureLine(") == 2
            && CountOccurrences(source, ReadyMarker) == 0
            && CountOccurrences(source, FailureMarker) == 0;
    }

    private static bool TestQemuProfileSource()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(
            Path.Combine(root, "scripts", "run-qemu-smoke.ps1"));

        return source.Contains(
                "[switch] $EnableM02Com1OwnedPortIoProbe",
                StringComparison.Ordinal)
            && source.Contains(ReadyMarker, StringComparison.Ordinal)
            && source.Contains(FailureMarker, StringComparison.Ordinal)
            && source.Contains(
                "$script:M02Com1OwnedPortIoRequiredMarkers",
                StringComparison.Ordinal)
            && source.Contains(
                "$script:M02Com1OwnedPortIoForbiddenMarkers",
                StringComparison.Ordinal)
            && source.Contains(
                "m02_com1_owned_port_io_probe_requires_checked_binding",
                StringComparison.Ordinal)
            && source.Contains(
                "m02_com1_owned_port_io_probe_requires_build_efi",
                StringComparison.Ordinal)
            && source.Contains(
                "m02_com1_owned_port_io_probe_forbids_skip_image_build",
                StringComparison.Ordinal)
            && source.Contains(
                "$buildArguments += \"-EnableM02Com1OwnedPortIoProbe\"",
                StringComparison.Ordinal)
            && source.Contains(
                "profileRequired=50 profileForbidden=36",
                StringComparison.Ordinal);
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
        int startIndex,
        StringComparison comparison)
    {
        if (startIndex < 0 || startIndex > source.Length)
        {
            return -1;
        }

        return source.IndexOf(value, startIndex, comparison);
    }
}
