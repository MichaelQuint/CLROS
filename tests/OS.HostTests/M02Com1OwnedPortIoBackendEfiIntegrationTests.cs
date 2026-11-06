using System.Runtime.CompilerServices;
using System.Xml.Linq;
using OS.Kernel.Diagnostics;

internal static class M02Com1OwnedPortIoBackendEfiIntegrationTests
{
    private const string TestName = "m02.004.efi.integration";
    private const string ReadyMarker =
        OS.Tooling.QemuContracts.Record085;
    private const string FailureMarker =
        OS.Tooling.QemuContracts.Record084;
    [HostFixture(HostLane.Fast)]
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

        HostCheck.Run("" + name, test, ref failures);
    }

    private static bool TestCompileClosureAndSwitch()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        XDocument project = XDocument.Load(Path.Combine(root, "src", "OS", "OS.csproj"));
        var catalog = Source();
        foreach (string type in new[] { "OS.Kernel.Diagnostics.InvokeIn8Out8CheckedUnmanagedFunctionPointers",
        "OS.Kernel.Diagnostics.Com1OwnedPortIoBackend", "OS.Boot.M02CheckedPortIoFunctionPointerIntegration",
        "OS.Boot.M02Com1OwnedPortIoBackendIntegration" })
            _ = catalog.Type(type);
        XElement[] defaults = project.Descendants("EnableM02Com1OwnedPortIoProbe").Where(element => element.Attribute("Condition")?.Value == "'$(EnableM02Com1OwnedPortIoProbe)' == ''").ToArray();
        XElement[] ud2ProfileOverrides = project.Descendants("PropertyGroup").Where(group => group.Attribute("Condition")?.Value == "'$(EnableM02Ud2VectorStateProbe)' == 'true'").SelectMany(group => group.Elements("EnableM02Com1OwnedPortIoProbe")).ToArray();
        XElement[] symbols = project.Descendants("PropertyGroup").Where(element => element.Attribute("Condition")?.Value == "'$(EnableM02Com1OwnedPortIoProbe)' == 'true'").ToArray();
        string[] dependencyConditions = project.Descendants("Error").Select(element => element.Attribute("Condition")?.Value ?? string.Empty).Where(value => value.StartsWith("'$(EnableM02Com1OwnedPortIoProbe)' == 'true'", StringComparison.Ordinal)).ToArray();
        return (defaults.Length == 1 && defaults[0].Value == "false" && defaults[0].Attribute("Condition")?.Value == "'$(EnableM02Com1OwnedPortIoProbe)' == ''" && ud2ProfileOverrides.Length == 1 && ud2ProfileOverrides[0].Value == "true" && symbols.Length == 1 && symbols[0].Elements("DefineConstants").Single().Value == "$(DefineConstants);M02_COM1_OWNED_PORT_IO_PROBE" && dependencyConditions.Contains("'$(EnableM02Com1OwnedPortIoProbe)' == 'true' and '$(EnableM04GcStaticRootMaterializationProbe)' == 'true'", StringComparer.Ordinal) && dependencyConditions.Contains("'$(EnableM02Com1OwnedPortIoProbe)' == 'true' and '$(EnableM03TypedExitBootServicesCallProbe)' == 'true'", StringComparer.Ordinal) && dependencyConditions.Contains("'$(EnableM02Com1OwnedPortIoProbe)' == 'true' and '$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true'", StringComparer.Ordinal) && dependencyConditions.Contains("'$(EnableM02Com1OwnedPortIoProbe)' == 'true' and '$(EnableM04CheckedReferenceBarrierProbe)' == 'true'", StringComparer.Ordinal) && dependencyConditions.Contains("'$(EnableM02Com1OwnedPortIoProbe)' == 'true' and '$(EnableM03MemoryMapCaptureProbe)' == 'true'", StringComparer.Ordinal) && dependencyConditions.Contains("'$(EnableM02Com1OwnedPortIoProbe)' == 'true' and '$(EnableM03FirmwareEntropySeedProbe)' == 'true'", StringComparer.Ordinal) && dependencyConditions.Contains("'$(EnableM02Com1OwnedPortIoProbe)' == 'true' and '$(EnableM04SzArrayAllocationProbe)' == 'true'", StringComparer.Ordinal) && dependencyConditions.Contains("'$(EnableM02Com1OwnedPortIoProbe)' == 'true' and '$(EnableM04StringAllocationProbe)' == 'true'", StringComparer.Ordinal) && dependencyConditions.Contains("'$(EnableM02Com1OwnedPortIoProbe)' == 'true' and '$(EnableM04StringAllocationBeforeBindProbe)' == 'true'", StringComparer.Ordinal) && dependencyConditions.Contains("'$(EnableM02Com1OwnedPortIoProbe)' == 'true' and '$(EnableM04StringAllocationExhaustionProbe)' == 'true'", StringComparer.Ordinal) && dependencyConditions.Contains("'$(EnableM02Com1OwnedPortIoProbe)' == 'true' and '$(EnableM04StringAllocationInvalidLengthProbe)' == 'true'", StringComparer.Ordinal) && dependencyConditions.Contains("'$(EnableM02Com1OwnedPortIoProbe)' == 'true' and '$(EnableM04SzArrayAllocationBeforeBindProbe)' == 'true'", StringComparer.Ordinal) && dependencyConditions.Contains("'$(EnableM02Com1OwnedPortIoProbe)' == 'true' and '$(EnableM04SzArrayAllocationExhaustionProbe)' == 'true'", StringComparer.Ordinal) && dependencyConditions.Contains("'$(EnableM02Com1OwnedPortIoProbe)' == 'true' and '$(EnableM02CheckedPortIoBindingProbe)' != 'true'", StringComparer.Ordinal)) && OS.Tooling.ContractAudit.Inspect(root).Count == 0;
    }

    private static bool TestIntegrationSourceBoundary()
    {
        var source = Source();
        const string binder = "OS.Boot.M02CheckedPortIoFunctionPointerIntegration";
        const string backend = "OS.Boot.M02Com1OwnedPortIoBackendIntegration";
        string checkedHelper = HostSourceText.Declarations(source, binder, binder + "Result", binder + "Status");
        string[] owners = [backend, backend + "Result", backend + "Status"];
        string integration = HostSourceText.Declarations(source, owners);
        string raw = HostSourceText.Documents(source, owners);
        return HostSourceText.Contains(checkedHelper, "Binding => _binding;")
            && HostSourceText.Contains(checkedHelper, "_binding.Succeeded")
            && source.Type("OS.Boot.M02Com1OwnedPortIoBackendIntegrationResult") is
            {
                TypeKind: Microsoft.CodeAnalysis.TypeKind.Struct,
                IsReadOnly: true, IsRecord: false, DeclaredAccessibility: Microsoft.CodeAnalysis.Accessibility.Public
            }
            && HostSourceText.Contains(integration, "new(checkedBinding.Binding);")
            && HostSourceText.Count(integration, "backend.ConfigureDefault();") == 1
            && HostSourceText.Contains(integration, "public const bool GuestIn8 = false;")
            && HostSourceText.Contains(integration, "public const bool MarkerTransportUsesUefiSerial = true;")
            && HostSourceText.Contains(integration, "public const bool PostExitBootServices = false;")
            && !raw.Contains(".ReadByte(", StringComparison.Ordinal) && !raw.Contains(".InvokeIn8(", StringComparison.Ordinal)
            && !raw.Contains(".InvokeOut8(", StringComparison.Ordinal) && !raw.Contains("throw ", StringComparison.Ordinal)
            && !raw.Contains("string ", StringComparison.Ordinal) && !raw.Contains("object ", StringComparison.Ordinal)
            && !raw.Contains("delegate", StringComparison.Ordinal) && !raw.Contains("[]", StringComparison.Ordinal)
            && !RuntimeHelpers.IsReferenceOrContainsReferences<Com1OwnedPortIoBackend>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<Com1OwnedPortIoConfigurationResult>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<Com1OwnedPortIoOperationResult>();
    }

    private static bool TestMarkerOwnershipAndOrder()
    {
        var source = Source();
        string run = HostSourceText.Active(source.Method(Adapter, "WriteSuccessLine"));
        string raw = HostSourceText.Documents(source, "OS.Boot.EfiEntry");
        int audit = HostSourceText.IndexOf(run, "if (!s_m02ExecutionAudit.Succeeded)");
        int binding = HostSourceText.IndexOf(run, "M02CheckedPortIoFunctionPointerIntegration.Bind(", audit);
        int bindingGuard = HostSourceText.IndexOf(run, "if (!s_m02CheckedPortIoBinding.Succeeded)", binding);
        int configuration = HostSourceText.IndexOf(run, "M02Com1OwnedPortIoBackendIntegration.Configure(", bindingGuard);
        int configurationGuard = HostSourceText.IndexOf(run, "if (!com1Configuration.Succeeded)", configuration);
        int checkedReady = HostSourceText.IndexOf(run, "WriteM02CheckedPortIoBindingSuccessLine(", configurationGuard);
        int com1Ready = HostSourceText.IndexOf(run, "WriteM02Com1OwnedPortIoSuccessLine(", checkedReady);
        HostCheck.Equal(1, Calls(source, "OS.Boot.M02Com1OwnedPortIoBackendIntegration", "Configure"));
        HostCheck.Equal(1, Calls(source, Adapter, "WriteM02Com1OwnedPortIoSuccessLine"));
        HostCheck.Equal(1, Calls(source, Adapter, "WriteM02Com1OwnedPortIoFailureLine"));
        return Marker(source, "WriteM02Com1OwnedPortIoSuccessLine") == ReadyMarker
            && Marker(source, "WriteM02Com1OwnedPortIoFailureLine") == FailureMarker
            && audit >= 0 && binding > audit && bindingGuard > binding && configuration > bindingGuard
            && configurationGuard > configuration && checkedReady > configurationGuard && com1Ready > checkedReady
            && !raw.Contains(ReadyMarker, StringComparison.Ordinal) && !raw.Contains(FailureMarker, StringComparison.Ordinal);
    }

    private static bool TestQemuProfileSource() => EfiProfileTestControls.Returning("M02Com1OwnedPortIo", OS.Tooling.QemuContracts.M02Com1OwnedPortIoRequiredMarkers, OS.Tooling.QemuContracts.M02Com1OwnedPortIoForbiddenMarkers, ReadyMarker, FailureMarker);

    private const string Adapter = "OS.Boot.EfiEntry+M01StageThreeAdapter";

    private static OS.Tooling.CSharpSourceCatalog Source() => HostSourceCatalog.Efi(OS.Tooling.EfiOptions.Parse(
        EfiProfileTestControls.ReturningArguments("M02Com1OwnedPortIo"), true).Profile());

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
