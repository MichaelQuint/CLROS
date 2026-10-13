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
        OS.Tooling.QemuContracts.Record035;
    private const string FailureMarker =
        OS.Tooling.QemuContracts.Record034;
    private static readonly string CompileClosureSha256 =
        OS.Tooling.EfiContracts.CompileInventorySha256;

    [HostFixture(HostLane.Fast)]
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

        HostCheck.Run("" + name, test, ref failures);
    }

    private static bool TestCompileClosureAndSwitches()
{
    string root = HostToolProcess.FindRepositoryRoot();
    string projectPath = Path.Combine(root, "src", "OS", "OS.csproj");
    XDocument project = XDocument.Load(projectPath);
    XElement[] compileItems = project.Descendants("Compile").ToArray();
    string[] compileKeys = compileItems.Select(item => (item.Attribute("Include")?.Value ?? string.Empty) + "|" + (item.Attribute("Link")?.Value ?? string.Empty)).ToArray();
    string compileText = string.Join("\n", compileKeys) + "\n";
    string compileHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(compileText))).ToLowerInvariant();
    int binderIndex = Array.IndexOf(compileKeys, @"..\OS.Kernel\Diagnostics\InvokeIn8Out8CheckedUnmanagedFunctionPointers.cs|Diagnostics\InvokeIn8Out8CheckedUnmanagedFunctionPointers.cs");
    int helperIndex = Array.IndexOf(compileKeys, @"Boot\M02CheckedPortIoFunctionPointerIntegration.cs|");
    XElement[] defaults = project.Descendants("EnableM02CheckedPortIoBindingProbe").Where(element => element.Attribute("Condition")?.Value == "'$(EnableM02CheckedPortIoBindingProbe)' == ''").ToArray();
    XElement[] ud2ProfileOverrides = project.Descendants("PropertyGroup").Where(group => group.Attribute("Condition")?.Value == "'$(EnableM02Ud2VectorStateProbe)' == 'true'").SelectMany(group => group.Elements("EnableM02CheckedPortIoBindingProbe")).ToArray();
    XElement[] symbols = project.Descendants("PropertyGroup").Where(element => element.Attribute("Condition")?.Value == "'$(EnableM02CheckedPortIoBindingProbe)' == 'true'").ToArray();
    // Keep this historical inventory scoped to its existing profiles. The new
    // cooperative exclusion is independently checked by M09004EfiIntegrationTests.
    string[] errorConditions = project.Descendants("Error")
        .Where(element => !(element.Attribute("Text")?.Value ?? string.Empty).StartsWith("m09_004_", StringComparison.Ordinal))
        .Select(element => element.Attribute("Condition")?.Value ?? string.Empty).Where(value => value.Contains("EnableM02CheckedPortIoBindingProbe", StringComparison.Ordinal)).ToArray();
    string[] prerequisiteConditions = errorConditions.Where(value => value.Contains("'$(EnableM02CheckedPortIoBindingProbe)' == 'true'", StringComparison.Ordinal) && value.Contains("'$(EnableM02SafeStubProbe)' != 'true'", StringComparison.Ordinal)).ToArray();
    string[] dependentConditions = errorConditions.Where(value => value.Contains("'$(EnableM02Com1OwnedPortIoProbe)' == 'true'", StringComparison.Ordinal) && value.Contains("'$(EnableM02CheckedPortIoBindingProbe)' != 'true'", StringComparison.Ordinal)).ToArray();
    return (compileItems.Length == OS.Tooling.EfiContracts.CompileItems.Count && compileKeys.Count(key => key.Contains("InvokeIn8Out8CheckedUnmanagedFunctionPointers.cs", StringComparison.Ordinal)) == 1 && compileKeys.Count(key => key.Contains("M02CheckedPortIoFunctionPointerIntegration.cs", StringComparison.Ordinal)) == 1 && compileKeys.All(key => !key.Contains("SerialPortConfiguration.cs", StringComparison.Ordinal)) && binderIndex >= 0 && helperIndex > binderIndex && compileHash == CompileClosureSha256 && defaults.Length == 1 && defaults[0].Value == "false" && defaults[0].Attribute("Condition")?.Value == "'$(EnableM02CheckedPortIoBindingProbe)' == ''" && ud2ProfileOverrides.Length == 1 && ud2ProfileOverrides[0].Value == "true" && symbols.Length == 1 && symbols[0].Elements("DefineConstants").Single().Value == "$(DefineConstants);M02_CHECKED_PORT_IO_BINDING_PROBE" && errorConditions.Length == 35 && errorConditions.Count(condition => string.Equals(condition, "'$(EnableM03FirstRealExitBootServicesProbe)' == 'true' and '$(EnableM02CheckedPortIoBindingProbe)' == 'true'", StringComparison.Ordinal)) == 1 && errorConditions.Contains("'$(EnableM03TypedExitBootServicesCallProbe)' == 'true' and '$(EnableM02CheckedPortIoBindingProbe)' == 'true'", StringComparer.Ordinal) && errorConditions.Contains("'$(EnableM02CheckedPortIoBindingProbe)' == 'true' and '$(EnableM03TypedExitBootServicesCallProbe)' == 'true'", StringComparer.Ordinal) && errorConditions.Contains("'$(EnableM02CheckedPortIoBindingProbe)' == 'true' and '$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true'", StringComparer.Ordinal) && errorConditions.Contains("'$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true' and '$(EnableM02CheckedPortIoBindingProbe)' == 'true'", StringComparer.Ordinal) && errorConditions.Contains("'$(EnableM04CheckedReferenceBarrierProbe)' == 'true' and '$(EnableM02CheckedPortIoBindingProbe)' == 'true'", StringComparer.Ordinal) && errorConditions.Contains("'$(EnableM02CheckedPortIoBindingProbe)' == 'true' and '$(EnableM04CheckedReferenceBarrierProbe)' == 'true'", StringComparer.Ordinal) && errorConditions.Contains("'$(EnableM02CheckedPortIoBindingProbe)' == 'true' and '$(EnableM03MemoryMapCaptureProbe)' == 'true'", StringComparer.Ordinal) && errorConditions.Contains("'$(EnableM02CheckedPortIoBindingProbe)' == 'true' and '$(EnableM03FirmwareEntropySeedProbe)' == 'true'", StringComparer.Ordinal) && errorConditions.Contains("'$(EnableM03MemoryMapCaptureProbe)' == 'true' and '$(EnableM02CheckedPortIoBindingProbe)' == 'true'", StringComparer.Ordinal) && errorConditions.Contains("'$(EnableM03FirmwareEntropySeedProbe)' == 'true' and '$(EnableM02CheckedPortIoBindingProbe)' == 'true'", StringComparer.Ordinal) && errorConditions.Contains("'$(EnableM02CheckedPortIoBindingProbe)' == 'true' and '$(EnableM04StringAllocationProbe)' == 'true'", StringComparer.Ordinal) && errorConditions.Contains("'$(EnableM02CheckedPortIoBindingProbe)' == 'true' and '$(EnableM04StringAllocationBeforeBindProbe)' == 'true'", StringComparer.Ordinal) && errorConditions.Contains("'$(EnableM02CheckedPortIoBindingProbe)' == 'true' and '$(EnableM04StringAllocationExhaustionProbe)' == 'true'", StringComparer.Ordinal) && errorConditions.Contains("'$(EnableM02CheckedPortIoBindingProbe)' == 'true' and '$(EnableM04StringAllocationInvalidLengthProbe)' == 'true'", StringComparer.Ordinal) && errorConditions.Contains("'$(EnableM02UnexpectedVectorPanicProbe)' == 'true' and '$(EnableM02CheckedPortIoBindingProbe)' != 'true'", StringComparer.Ordinal) && errorConditions.Contains("'$(EnableM04RhpNewFastProbe)' == 'true' and '$(EnableM02CheckedPortIoBindingProbe)' == 'true'", StringComparer.Ordinal) && errorConditions.Contains("'$(EnableM02PageFaultCr2ErrorCodeProbe)' == 'true' and '$(EnableM02CheckedPortIoBindingProbe)' != 'true'", StringComparer.Ordinal) && errorConditions.Contains("'$(EnableM02DoubleFaultIstProbe)' == 'true' and '$(EnableM02CheckedPortIoBindingProbe)' != 'true'", StringComparer.Ordinal) && errorConditions.Contains("'$(EnableM04ManagedHeapPageRangeProbe)' == 'true' and '$(EnableM02CheckedPortIoBindingProbe)' == 'true'", StringComparer.Ordinal) && errorConditions.Contains("'$(EnableM04SzArrayAllocationProbe)' == 'true' and '$(EnableM02CheckedPortIoBindingProbe)' == 'true'", StringComparer.Ordinal) && errorConditions.Contains("'$(EnableM04StringAllocationProbe)' == 'true' and '$(EnableM02CheckedPortIoBindingProbe)' == 'true'", StringComparer.Ordinal) && errorConditions.Contains("'$(EnableM04StringAllocationBeforeBindProbe)' == 'true' and '$(EnableM02CheckedPortIoBindingProbe)' == 'true'", StringComparer.Ordinal) && errorConditions.Contains("'$(EnableM04StringAllocationExhaustionProbe)' == 'true' and '$(EnableM02CheckedPortIoBindingProbe)' == 'true'", StringComparer.Ordinal) && errorConditions.Contains("'$(EnableM04StringAllocationInvalidLengthProbe)' == 'true' and '$(EnableM02CheckedPortIoBindingProbe)' == 'true'", StringComparer.Ordinal) && errorConditions.Contains("'$(EnableM02CheckedPortIoBindingProbe)' == 'true' and '$(EnableM04SzArrayAllocationProbe)' == 'true'", StringComparer.Ordinal) && errorConditions.Contains("'$(EnableM02CheckedPortIoBindingProbe)' == 'true' and '$(EnableM04StringAllocationProbe)' == 'true'", StringComparer.Ordinal) && errorConditions.Contains("'$(EnableM02CheckedPortIoBindingProbe)' == 'true' and '$(EnableM04StringAllocationBeforeBindProbe)' == 'true'", StringComparer.Ordinal) && errorConditions.Contains("'$(EnableM02CheckedPortIoBindingProbe)' == 'true' and '$(EnableM04StringAllocationExhaustionProbe)' == 'true'", StringComparer.Ordinal) && errorConditions.Contains("'$(EnableM02CheckedPortIoBindingProbe)' == 'true' and '$(EnableM04StringAllocationInvalidLengthProbe)' == 'true'", StringComparer.Ordinal) && errorConditions.Contains("'$(EnableM04SzArrayAllocationBeforeBindProbe)' == 'true' and '$(EnableM02CheckedPortIoBindingProbe)' == 'true'", StringComparer.Ordinal) && errorConditions.Contains("'$(EnableM02CheckedPortIoBindingProbe)' == 'true' and '$(EnableM04SzArrayAllocationBeforeBindProbe)' == 'true'", StringComparer.Ordinal) && errorConditions.Contains("'$(EnableM04SzArrayAllocationExhaustionProbe)' == 'true' and '$(EnableM02CheckedPortIoBindingProbe)' == 'true'", StringComparer.Ordinal) && errorConditions.Contains("'$(EnableM02CheckedPortIoBindingProbe)' == 'true' and '$(EnableM04SzArrayAllocationExhaustionProbe)' == 'true'", StringComparer.Ordinal) && prerequisiteConditions.Length == 1 && dependentConditions.Length == 1) && OS.Tooling.ContractAudit.Inspect(root).Count == 0;
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
            Path.Combine(root, "src", "OS", "Boot", "EfiEntry.cs")).Replace("\r\n", "\n", StringComparison.Ordinal);
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

    private static bool TestQemuProfileSource() => EfiProfileTestControls.Returning("M02CheckedPortIoBinding", OS.Tooling.QemuContracts.M02CheckedPortIoBindingRequiredMarkers, OS.Tooling.QemuContracts.M02CheckedPortIoBindingForbiddenMarkers, ReadyMarker, FailureMarker);

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
