using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using OS.Kernel.Diagnostics;

internal static class M02CheckedPortIoFunctionPointerEfiIntegrationTests
{
    private const string TestName = "m02.003.efi.integration";
    private const string ReadyMarker =
        "CLROS_M02_003_READY bindingReady=true out8AddressChecked=true in8AddressChecked=true guestInvocation=false portIoExecuted=false";
    private const string FailureMarker =
        "M02_CHECKED_PORT_IO_BINDING_FAILED";
    private const string CompileClosureSha256 =
        "fafc07e210dde2a3a8e2d537f54032b2809cb01258a23b4d513e1d868dc74a9b";

    [ModuleInitializer]
    internal static void RunAll()
    {
        Console.WriteLine("TEST_START " + TestName);
        int failures = 0;

        RunCase(
            "m02.003 EFI compile closure and switches are exact",
            TestCompileClosureAndSwitches,
            ref failures);
        RunCase(
            "m02.003 EFI binding source is value-only and never invokes port I/O",
            TestBindingSourceBoundary,
            ref failures);
        RunCase(
            "m02.003 EFI marker ownership and post-audit order are exact",
            TestMarkerOwnershipAndOrder,
            ref failures);
        RunCase(
            "m02.003 QEMU profile propagates the checked-binding evidence boundary",
            TestQemuProfileSource,
            ref failures);

        if (failures != 0)
        {
            Console.WriteLine(
                "TEST_FAIL " + TestName + " host-contract failures=" + failures);
            throw new InvalidOperationException(
                "M02-003 EFI integration host tests failed: " + failures);
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
        catch (Exception ex)
        {
            failures++;
            Console.WriteLine(
                "TEST_FAIL " + name + " " + ex.GetType().Name + ": " +
                ex.Message);
        }
    }

    private static bool TestCompileClosureAndSwitches()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string projectPath = Path.Combine(root, "src", "OS", "OS.csproj");
        XDocument project = XDocument.Load(projectPath);
        XElement[] compileItems = project
            .Descendants("Compile")
            .ToArray();
        string[] compileKeys = compileItems
            .Select(item =>
                (item.Attribute("Include")?.Value ?? string.Empty) + "|" +
                (item.Attribute("Link")?.Value ?? string.Empty))
            .ToArray();
        string compileText = string.Join("\n", compileKeys) + "\n";
        string compileHash = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(compileText)))
            .ToLowerInvariant();
        int binderIndex = Array.IndexOf(
            compileKeys,
            @"..\OS.Kernel\Diagnostics\InvokeIn8Out8CheckedUnmanagedFunctionPointers.cs|Diagnostics\InvokeIn8Out8CheckedUnmanagedFunctionPointers.cs");
        int helperIndex = Array.IndexOf(
            compileKeys,
            @"Boot\M02CheckedPortIoFunctionPointerIntegration.cs|");

        XElement[] defaults = project
            .Descendants("EnableM02CheckedPortIoBindingProbe")
            .Where(element => element.Attribute("Condition")?.Value ==
                "'$(EnableM02CheckedPortIoBindingProbe)' == ''")
            .ToArray();
        XElement[] ud2ProfileOverrides = project
            .Descendants("PropertyGroup")
            .Where(group => group.Attribute("Condition")?.Value ==
                "'$(EnableM02Ud2VectorStateProbe)' == 'true'")
            .SelectMany(group => group.Elements(
                "EnableM02CheckedPortIoBindingProbe"))
            .ToArray();
        XElement[] symbols = project
            .Descendants("PropertyGroup")
            .Where(element =>
                element.Attribute("Condition")?.Value ==
                    "'$(EnableM02CheckedPortIoBindingProbe)' == 'true'")
            .ToArray();
        string[] errorConditions = project
            .Descendants("Error")
            .Select(element => element.Attribute("Condition")?.Value ?? string.Empty)
            .Where(value => value.Contains(
                "EnableM02CheckedPortIoBindingProbe",
                StringComparison.Ordinal))
            .ToArray();
        string[] prerequisiteConditions = errorConditions
            .Where(value =>
                value.Contains(
                    "'$(EnableM02CheckedPortIoBindingProbe)' == 'true'",
                    StringComparison.Ordinal) &&
                value.Contains(
                    "'$(EnableM02SafeStubProbe)' != 'true'",
                    StringComparison.Ordinal))
            .ToArray();
        string[] dependentConditions = errorConditions
            .Where(value =>
                value.Contains(
                    "'$(EnableM02Com1OwnedPortIoProbe)' == 'true'",
                    StringComparison.Ordinal) &&
                value.Contains(
                    "'$(EnableM02CheckedPortIoBindingProbe)' != 'true'",
                    StringComparison.Ordinal))
            .ToArray();
        string buildScript = File.ReadAllText(
            Path.Combine(root, "scripts", "build-efi-shell.ps1"));
        string artifactScript = File.ReadAllText(
            Path.Combine(root, "scripts", "test-efi-artifact.ps1"));

        return compileItems.Length == 75
            && compileKeys.Count(key => key.Contains(
                "InvokeIn8Out8CheckedUnmanagedFunctionPointers.cs",
                StringComparison.Ordinal)) == 1
            && compileKeys.Count(key => key.Contains(
                "M02CheckedPortIoFunctionPointerIntegration.cs",
                StringComparison.Ordinal)) == 1
            && compileKeys.All(key => !key.Contains(
                "SerialPortConfiguration.cs",
                StringComparison.Ordinal))
            && binderIndex >= 0
            && helperIndex > binderIndex
            && compileHash == CompileClosureSha256
            && defaults.Length == 1
            && defaults[0].Value == "false"
            && defaults[0].Attribute("Condition")?.Value ==
                "'$(EnableM02CheckedPortIoBindingProbe)' == ''"
            && ud2ProfileOverrides.Length == 1
            && ud2ProfileOverrides[0].Value == "true"
            && symbols.Length == 1
            && symbols[0].Elements("DefineConstants").Single().Value ==
                "$(DefineConstants);M02_CHECKED_PORT_IO_BINDING_PROBE"
            && errorConditions.Length == 4
            && errorConditions.Contains(
                "'$(EnableM02PageFaultCr2ErrorCodeProbe)' == 'true' and '$(EnableM02CheckedPortIoBindingProbe)' != 'true'",
                StringComparer.Ordinal)
            && errorConditions.Contains(
                "'$(EnableM04ManagedHeapPageRangeProbe)' == 'true' and '$(EnableM02CheckedPortIoBindingProbe)' == 'true'",
                StringComparer.Ordinal)
            && prerequisiteConditions.Length == 1
            && dependentConditions.Length == 1
            && buildScript.Contains(
                "EFI_PUBLISH_SOURCE_AUTHORITY status=ok inputs=88",
                StringComparison.Ordinal)
            && buildScript.Contains(
                "[switch] $EnableM02CheckedPortIoBindingProbe",
                StringComparison.Ordinal)
            && buildScript.Contains(
                "$arguments += \"-p:EnableM02CheckedPortIoBindingProbe=true\"",
                StringComparison.Ordinal)
            && artifactScript.Contains(
                "$expectedSourceClosureInputCount = 88",
                StringComparison.Ordinal)
            && artifactScript.Contains(
                "$compileItems.Count -ne 75",
                StringComparison.Ordinal);
    }

    private static bool TestBindingSourceBoundary()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string production = File.ReadAllText(
            Path.Combine(
                root,
                "src",
                "OS.Kernel",
                "Diagnostics",
                "InvokeIn8Out8CheckedUnmanagedFunctionPointers.cs"));
        string helper = File.ReadAllText(
            Path.Combine(
                root,
                "src",
                "OS",
                "Boot",
                "M02CheckedPortIoFunctionPointerIntegration.cs"));

        return production.Contains(
                "delegate* unmanaged<ushort, byte, void>",
                StringComparison.Ordinal)
            && production.Contains(
                "delegate* unmanaged<ushort, byte>",
                StringComparison.Ordinal)
            && production.Contains("#if !NOSTDLIB_EFI", StringComparison.Ordinal)
            && production.Contains(
                "public const ushort RequiredCom1BasePort = 0x03f8;",
                StringComparison.Ordinal)
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                CheckedPortIoFunctionPointerBindingRequest>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                CheckedPortIoFunctionPointerBindingResult>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                CheckedPortIoFunctionPointerInvocationResult>()
            && helper.Contains(
                "public readonly struct M02CheckedPortIoFunctionPointerIntegrationResult",
                StringComparison.Ordinal)
            && helper.Contains(
                "public const bool GuestInvocation = false;",
                StringComparison.Ordinal)
            && helper.Contains(
                "public const bool PortIoExecuted = false;",
                StringComparison.Ordinal)
            && helper.Contains(
                ".CheckedPortIoFunctionPointerBindingRequest\n                .Create(",
                StringComparison.Ordinal)
            && helper.Contains(
                ".InvokeIn8Out8CheckedUnmanagedFunctionPointers\n                .Bind(request);",
                StringComparison.Ordinal)
            && !helper.Contains(".InvokeOut8(", StringComparison.Ordinal)
            && !helper.Contains(".InvokeIn8(", StringComparison.Ordinal)
            && !helper.Contains("throw ", StringComparison.Ordinal)
            && !helper.Contains("string ", StringComparison.Ordinal)
            && !helper.Contains("object ", StringComparison.Ordinal)
            && !helper.Contains("delegate", StringComparison.Ordinal)
            && !helper.Contains("[]", StringComparison.Ordinal);
    }

    private static bool TestMarkerOwnershipAndOrder()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(
            Path.Combine(root, "src", "OS", "Boot", "EfiEntry.cs"));
        string reconstructedReady = ExtractAsciiMarker(
            source,
            "private static void WriteM02CheckedPortIoBindingSuccessLine(");
        string reconstructedFailure = ExtractAsciiMarker(
            source,
            "private static void WriteM02CheckedPortIoBindingFailureLine(");

        int executionAudit = source.IndexOf(
            "if (!s_m02ExecutionAudit.Succeeded)",
            StringComparison.Ordinal);
        int binding = source.IndexOf(
            "M02CheckedPortIoFunctionPointerIntegration.Bind(",
            executionAudit,
            StringComparison.Ordinal);
        int bindingGuard = source.IndexOf(
            "if (!s_m02CheckedPortIoBinding.Succeeded)",
            binding,
            StringComparison.Ordinal);
        int m02Ready = source.IndexOf(
            "WriteM02SuccessLine(console, serial, 5);",
            bindingGuard,
            StringComparison.Ordinal);
        int checkedReady = source.IndexOf(
            "WriteM02CheckedPortIoBindingSuccessLine(",
            m02Ready,
            StringComparison.Ordinal);

        return reconstructedReady == ReadyMarker
            && reconstructedFailure == FailureMarker
            && executionAudit >= 0
            && binding > executionAudit
            && bindingGuard > binding
            && m02Ready > bindingGuard
            && checkedReady > m02Ready
            && CountOccurrences(
                source,
                "WriteM02CheckedPortIoBindingSuccessLine(") == 2
            && CountOccurrences(source, ReadyMarker) == 0
            && !source.Contains(".InvokeOut8(", StringComparison.Ordinal)
            && !source.Contains(".InvokeIn8(", StringComparison.Ordinal);
    }

    private static bool TestQemuProfileSource()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(
            Path.Combine(root, "scripts", "run-qemu-smoke.ps1"));

        return source.Contains(
                "[switch] $EnableM02CheckedPortIoBindingProbe",
                StringComparison.Ordinal)
            && source.Contains(ReadyMarker, StringComparison.Ordinal)
            && source.Contains(FailureMarker, StringComparison.Ordinal)
            && source.Contains(
                "m02_checked_port_io_binding_probe_requires_safe_stub",
                StringComparison.Ordinal)
            && source.Contains(
                "m02_checked_port_io_binding_probe_requires_build_efi",
                StringComparison.Ordinal)
            && source.Contains(
                "m02_checked_port_io_binding_probe_forbids_skip_image_build",
                StringComparison.Ordinal)
            && source.Contains(
                "$buildArguments += \"-EnableM02CheckedPortIoBindingProbe\"",
                StringComparison.Ordinal)
            && source.Contains("\"M02_OUT8_EXECUTED\"", StringComparison.Ordinal)
            && source.Contains("\"M02_IN8_EXECUTED\"", StringComparison.Ordinal)
            && source.Contains("\"M02_PORT_IO_ATTEMPTED\"", StringComparison.Ordinal)
            && source.Contains(
                "\"M02_PRIVILEGED_STUB_TARGETED\"",
                StringComparison.Ordinal)
            && source.Contains("\"TEST_FAIL\"", StringComparison.Ordinal);
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
}
