using System.Runtime.CompilerServices;
using System.Xml.Linq;
using OS.Kernel.Diagnostics;

internal static class M02CheckedPortIoFunctionPointerEfiIntegrationTests
{
    private const string TestName = "m02.003.efi.integration";
    private const string ReadyMarker =
        OS.Tooling.QemuContracts.Record035;
    private const string FailureMarker =
        OS.Tooling.QemuContracts.Record034;
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
        var catalog = Source();
        _ = catalog.Type("OS.Kernel.Diagnostics.InvokeIn8Out8CheckedUnmanagedFunctionPointers");
        _ = catalog.Type("OS.Boot.M02CheckedPortIoFunctionPointerIntegration");
        HostCheck.True(catalog.Compilation.GetTypeByMetadataName("OS.Kernel.Diagnostics.SerialPortConfiguration") is null,
            "host serial configuration is outside the EFI input set");
        XElement[] defaults = project.Descendants("EnableM02CheckedPortIoBindingProbe").Where(element => element.Attribute("Condition")?.Value == "'$(EnableM02CheckedPortIoBindingProbe)' == ''").ToArray();
        XElement[] ud2ProfileOverrides = project.Descendants("PropertyGroup").Where(group => group.Attribute("Condition")?.Value == "'$(EnableM02Ud2VectorStateProbe)' == 'true'").SelectMany(group => group.Elements("EnableM02CheckedPortIoBindingProbe")).ToArray();
        XElement[] symbols = project.Descendants("PropertyGroup").Where(element => element.Attribute("Condition")?.Value == "'$(EnableM02CheckedPortIoBindingProbe)' == 'true'").ToArray();
        // Keep this historical inventory scoped to its existing profiles. The new
        // cooperative/timer and containment exclusions have their own exact fixtures.
        string[] errorConditions = project.Descendants("Error")
            .Where(element => !(element.Attribute("Text")?.Value ?? string.Empty).StartsWith("m07_026_", StringComparison.Ordinal) &&
                !(element.Attribute("Text")?.Value ?? string.Empty).StartsWith("socket_containment_", StringComparison.Ordinal) &&
                !(element.Attribute("Text")?.Value ?? string.Empty).StartsWith("m09_004_", StringComparison.Ordinal) &&
                !(element.Attribute("Text")?.Value ?? string.Empty).StartsWith("m09_005_", StringComparison.Ordinal) &&
                !(element.Attribute("Text")?.Value ?? string.Empty).StartsWith("m09_006_", StringComparison.Ordinal) &&
                !(element.Attribute("Text")?.Value ?? string.Empty).StartsWith("m09_009_", StringComparison.Ordinal) &&
                !(element.Attribute("Text")?.Value ?? string.Empty).StartsWith("m09_010_", StringComparison.Ordinal))
            .Select(element => element.Attribute("Condition")?.Value ?? string.Empty).Where(value => value.Contains("EnableM02CheckedPortIoBindingProbe", StringComparison.Ordinal)).ToArray();
        string[] prerequisiteConditions = errorConditions.Where(value => value.Contains("'$(EnableM02CheckedPortIoBindingProbe)' == 'true'", StringComparison.Ordinal) && value.Contains("'$(EnableM02SafeStubProbe)' != 'true'", StringComparison.Ordinal)).ToArray();
        string[] dependentConditions = errorConditions.Where(value => value.Contains("'$(EnableM02Com1OwnedPortIoProbe)' == 'true'", StringComparison.Ordinal) && value.Contains("'$(EnableM02CheckedPortIoBindingProbe)' != 'true'", StringComparison.Ordinal)).ToArray();
        return (defaults.Length == 1 && defaults[0].Value == "false" && defaults[0].Attribute("Condition")?.Value == "'$(EnableM02CheckedPortIoBindingProbe)' == ''" && ud2ProfileOverrides.Length == 1 && ud2ProfileOverrides[0].Value == "true" && symbols.Length == 1 && symbols[0].Elements("DefineConstants").Single().Value == "$(DefineConstants);M02_CHECKED_PORT_IO_BINDING_PROBE" && errorConditions.Count(condition => string.Equals(condition, "'$(EnableM03FirstRealExitBootServicesProbe)' == 'true' and '$(EnableM02CheckedPortIoBindingProbe)' == 'true'", StringComparison.Ordinal)) == 1 && errorConditions.Contains("'$(EnableM03TypedExitBootServicesCallProbe)' == 'true' and '$(EnableM02CheckedPortIoBindingProbe)' == 'true'", StringComparer.Ordinal) && errorConditions.Contains("'$(EnableM02CheckedPortIoBindingProbe)' == 'true' and '$(EnableM03TypedExitBootServicesCallProbe)' == 'true'", StringComparer.Ordinal) && errorConditions.Contains("'$(EnableM02CheckedPortIoBindingProbe)' == 'true' and '$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true'", StringComparer.Ordinal) && errorConditions.Contains("'$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true' and '$(EnableM02CheckedPortIoBindingProbe)' == 'true'", StringComparer.Ordinal) && errorConditions.Contains("'$(EnableM04CheckedReferenceBarrierProbe)' == 'true' and '$(EnableM02CheckedPortIoBindingProbe)' == 'true'", StringComparer.Ordinal) && errorConditions.Contains("'$(EnableM02CheckedPortIoBindingProbe)' == 'true' and '$(EnableM04CheckedReferenceBarrierProbe)' == 'true'", StringComparer.Ordinal) && errorConditions.Contains("'$(EnableM02CheckedPortIoBindingProbe)' == 'true' and '$(EnableM03MemoryMapCaptureProbe)' == 'true'", StringComparer.Ordinal) && errorConditions.Contains("'$(EnableM02CheckedPortIoBindingProbe)' == 'true' and '$(EnableM03FirmwareEntropySeedProbe)' == 'true'", StringComparer.Ordinal) && errorConditions.Contains("'$(EnableM03MemoryMapCaptureProbe)' == 'true' and '$(EnableM02CheckedPortIoBindingProbe)' == 'true'", StringComparer.Ordinal) && errorConditions.Contains("'$(EnableM03FirmwareEntropySeedProbe)' == 'true' and '$(EnableM02CheckedPortIoBindingProbe)' == 'true'", StringComparer.Ordinal) && errorConditions.Contains("'$(EnableM02CheckedPortIoBindingProbe)' == 'true' and '$(EnableM04StringAllocationProbe)' == 'true'", StringComparer.Ordinal) && errorConditions.Contains("'$(EnableM02CheckedPortIoBindingProbe)' == 'true' and '$(EnableM04StringAllocationBeforeBindProbe)' == 'true'", StringComparer.Ordinal) && errorConditions.Contains("'$(EnableM02CheckedPortIoBindingProbe)' == 'true' and '$(EnableM04StringAllocationExhaustionProbe)' == 'true'", StringComparer.Ordinal) && errorConditions.Contains("'$(EnableM02CheckedPortIoBindingProbe)' == 'true' and '$(EnableM04StringAllocationInvalidLengthProbe)' == 'true'", StringComparer.Ordinal) && errorConditions.Contains("'$(EnableM02UnexpectedVectorPanicProbe)' == 'true' and '$(EnableM02CheckedPortIoBindingProbe)' != 'true'", StringComparer.Ordinal) && errorConditions.Contains("'$(EnableM04RhpNewFastProbe)' == 'true' and '$(EnableM02CheckedPortIoBindingProbe)' == 'true'", StringComparer.Ordinal) && errorConditions.Contains("'$(EnableM02PageFaultCr2ErrorCodeProbe)' == 'true' and '$(EnableM02CheckedPortIoBindingProbe)' != 'true'", StringComparer.Ordinal) && errorConditions.Contains("'$(EnableM02DoubleFaultIstProbe)' == 'true' and '$(EnableM02CheckedPortIoBindingProbe)' != 'true'", StringComparer.Ordinal) && errorConditions.Contains("'$(EnableM04ManagedHeapPageRangeProbe)' == 'true' and '$(EnableM02CheckedPortIoBindingProbe)' == 'true'", StringComparer.Ordinal) && errorConditions.Contains("'$(EnableM04SzArrayAllocationProbe)' == 'true' and '$(EnableM02CheckedPortIoBindingProbe)' == 'true'", StringComparer.Ordinal) && errorConditions.Contains("'$(EnableM04StringAllocationProbe)' == 'true' and '$(EnableM02CheckedPortIoBindingProbe)' == 'true'", StringComparer.Ordinal) && errorConditions.Contains("'$(EnableM04StringAllocationBeforeBindProbe)' == 'true' and '$(EnableM02CheckedPortIoBindingProbe)' == 'true'", StringComparer.Ordinal) && errorConditions.Contains("'$(EnableM04StringAllocationExhaustionProbe)' == 'true' and '$(EnableM02CheckedPortIoBindingProbe)' == 'true'", StringComparer.Ordinal) && errorConditions.Contains("'$(EnableM04StringAllocationInvalidLengthProbe)' == 'true' and '$(EnableM02CheckedPortIoBindingProbe)' == 'true'", StringComparer.Ordinal) && errorConditions.Contains("'$(EnableM02CheckedPortIoBindingProbe)' == 'true' and '$(EnableM04SzArrayAllocationProbe)' == 'true'", StringComparer.Ordinal) && errorConditions.Contains("'$(EnableM02CheckedPortIoBindingProbe)' == 'true' and '$(EnableM04StringAllocationProbe)' == 'true'", StringComparer.Ordinal) && errorConditions.Contains("'$(EnableM02CheckedPortIoBindingProbe)' == 'true' and '$(EnableM04StringAllocationBeforeBindProbe)' == 'true'", StringComparer.Ordinal) && errorConditions.Contains("'$(EnableM02CheckedPortIoBindingProbe)' == 'true' and '$(EnableM04StringAllocationExhaustionProbe)' == 'true'", StringComparer.Ordinal) && errorConditions.Contains("'$(EnableM02CheckedPortIoBindingProbe)' == 'true' and '$(EnableM04StringAllocationInvalidLengthProbe)' == 'true'", StringComparer.Ordinal) && errorConditions.Contains("'$(EnableM04SzArrayAllocationBeforeBindProbe)' == 'true' and '$(EnableM02CheckedPortIoBindingProbe)' == 'true'", StringComparer.Ordinal) && errorConditions.Contains("'$(EnableM02CheckedPortIoBindingProbe)' == 'true' and '$(EnableM04SzArrayAllocationBeforeBindProbe)' == 'true'", StringComparer.Ordinal) && errorConditions.Contains("'$(EnableM04SzArrayAllocationExhaustionProbe)' == 'true' and '$(EnableM02CheckedPortIoBindingProbe)' == 'true'", StringComparer.Ordinal) && errorConditions.Contains("'$(EnableM02CheckedPortIoBindingProbe)' == 'true' and '$(EnableM04SzArrayAllocationExhaustionProbe)' == 'true'", StringComparer.Ordinal) && prerequisiteConditions.Length == 1 && dependentConditions.Length == 1) && OS.Tooling.ContractAudit.Inspect(root).Count == 0;
    }

    private static bool TestBindingSourceBoundary()
    {
        var source = Source();
        const string binder = "OS.Kernel.Diagnostics.InvokeIn8Out8CheckedUnmanagedFunctionPointers";
        const string integration = "OS.Boot.M02CheckedPortIoFunctionPointerIntegration";
        string[] helpers = [integration, integration + "Result", integration + "Status"];
        string production = HostSourceText.Declarations(source, binder);
        string helper = HostSourceText.Declarations(source, helpers);
        string raw = HostSourceText.Documents(source, helpers);
        var bound = source.Type(binder).GetMembers("Bind").OfType<Microsoft.CodeAnalysis.IMethodSymbol>().Single();
        HostCheck.True(bound.Parameters.Length == 1 && Microsoft.CodeAnalysis.SymbolEqualityComparer.Default.Equals(
            bound.Parameters[0].Type, source.Type("OS.Kernel.Diagnostics.CheckedPortIoFunctionPointerBindingRequest")),
            "EFI binder excludes its host-only plan overload");
        var host = HostSourceCatalog.Project("src/OS.Kernel/OS.Kernel.csproj");
        HostCheck.True(host.Type(binder).GetMembers("Bind").OfType<Microsoft.CodeAnalysis.IMethodSymbol>().Any(method =>
            method.Parameters.Length == 1 && method.Parameters[0].Type.Name == "DiagnosticStubCallableBindingPlanResult"),
            "host binder retains its guarded plan overload");
        return HostSourceText.Contains(production, "delegate* unmanaged<ushort, byte, void>")
            && HostSourceText.Contains(production, "delegate* unmanaged<ushort, byte>")
            && HostSourceText.Contains(production, "public const ushort RequiredCom1BasePort = 0x03f8;")
            && !RuntimeHelpers.IsReferenceOrContainsReferences<CheckedPortIoFunctionPointerBindingRequest>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<CheckedPortIoFunctionPointerBindingResult>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<CheckedPortIoFunctionPointerInvocationResult>()
            && source.Type("OS.Boot.M02CheckedPortIoFunctionPointerIntegrationResult") is
            {
                TypeKind: Microsoft.CodeAnalysis.TypeKind.Struct,
                IsReadOnly: true, IsRecord: false, DeclaredAccessibility: Microsoft.CodeAnalysis.Accessibility.Public
            }
            && HostSourceText.Contains(helper, "public const bool GuestInvocation = false;")
            && HostSourceText.Contains(helper, "public const bool PortIoExecuted = false;")
            && HostSourceText.Contains(helper, ".CheckedPortIoFunctionPointerBindingRequest.Create(")
            && HostSourceText.Contains(helper, ".InvokeIn8Out8CheckedUnmanagedFunctionPointers.Bind(request);")
            && !raw.Contains(".InvokeOut8(", StringComparison.Ordinal) && !raw.Contains(".InvokeIn8(", StringComparison.Ordinal)
            && !raw.Contains("throw ", StringComparison.Ordinal) && !raw.Contains("string ", StringComparison.Ordinal)
            && !raw.Contains("object ", StringComparison.Ordinal) && !raw.Contains("delegate", StringComparison.Ordinal)
            && !raw.Contains("[]", StringComparison.Ordinal);
    }

    private static bool TestMarkerOwnershipAndOrder()
    {
        var source = Source();
        string run = HostSourceText.Active(source.Method(Adapter, "WriteSuccessLine"));
        string raw = HostSourceText.Documents(source, "OS.Boot.EfiEntry");
        int audit = HostSourceText.IndexOf(run, "if (!s_m02ExecutionAudit.Succeeded)");
        int binding = HostSourceText.IndexOf(run, "M02CheckedPortIoFunctionPointerIntegration.Bind(", audit);
        int guard = HostSourceText.IndexOf(run, "if (!s_m02CheckedPortIoBinding.Succeeded)", binding);
        int ready = HostSourceText.IndexOf(run, "WriteM02SuccessLine(console, serial, 5);", guard);
        int checkedReady = HostSourceText.IndexOf(run, "WriteM02CheckedPortIoBindingSuccessLine(", ready);
        HostCheck.Equal(1, Calls(source, Adapter, "WriteM02CheckedPortIoBindingSuccessLine"));
        return Marker(source, "WriteM02CheckedPortIoBindingSuccessLine") == ReadyMarker
            && Marker(source, "WriteM02CheckedPortIoBindingFailureLine") == FailureMarker
            && audit >= 0 && binding > audit && guard > binding && ready > guard && checkedReady > ready
            && !raw.Contains(ReadyMarker, StringComparison.Ordinal)
            && !raw.Contains(".InvokeOut8(", StringComparison.Ordinal) && !raw.Contains(".InvokeIn8(", StringComparison.Ordinal);
    }

    private static bool TestQemuProfileSource() => EfiProfileTestControls.Returning("M02CheckedPortIoBinding", OS.Tooling.QemuContracts.M02CheckedPortIoBindingRequiredMarkers, OS.Tooling.QemuContracts.M02CheckedPortIoBindingForbiddenMarkers, ReadyMarker, FailureMarker);

    private const string Adapter = "OS.Boot.EfiEntry+M01StageThreeAdapter";

    private static OS.Tooling.CSharpSourceCatalog Source() => HostSourceCatalog.Efi(OS.Tooling.EfiOptions.Parse(
        EfiProfileTestControls.ReturningArguments("M02CheckedPortIoBinding"), true).Profile());

    private static string Marker(OS.Tooling.CSharpSourceCatalog source, string method) =>
        HostSourceText.ByteCharacters(source, Adapter, method);

    private static int Calls(OS.Tooling.CSharpSourceCatalog source, string owner, string method)
    {
        var declaration = source.Method(owner, method);
        var symbol = Microsoft.CodeAnalysis.CSharp.CSharpExtensions.GetDeclaredSymbol(
            source.Compilation.GetSemanticModel(declaration.SyntaxTree), declaration)!;
        return HostSourceOperations.CallsTo(source, symbol).Length;
    }
}
