using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

internal static class M04IndependentEndianConformanceEfiIntegrationTests
{
    private const string TestName = "m04.002.efi.integration";
    private const string ReadyMarker =
        OS.Tooling.QemuContracts.Record278;
    private const string FailureMarker =
        OS.Tooling.QemuContracts.Record274;

    [HostFixture(HostLane.Fast)]
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

        HostCheck.Run("" + name, test, ref failures);
    }

    private static bool TestCompileClosureAndFlag()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        XDocument project = XDocument.Load(
            Path.Combine(root, "src", "OS", "OS.csproj"));
        var profile = OS.Tooling.EfiOptions.Parse(["--EnableM04IndependentEndianConformanceProbe"], true).Profile();
        var source = HostSourceCatalog.Efi(profile);
        foreach (string type in new[]
        {
            "OS.Kernel.Diagnostics.Com1OwnedPortIoBackend",
            "OS.Kernel.Diagnostics.OwnedCom1ReadyExclusivelyOwnedWriter",
            "OS.Kernel.Diagnostics.PanicSafeFormattingSerialReportingOwnedCom1",
            "OS.Kernel.Diagnostics.OwnedX64GdtContainingNullN64BitCode",
            "OS.Kernel.Diagnostics.GuardedISTStacksDoubleFaultPageFaultNMI",
            "OS.Kernel.Diagnostics.OwnedGDTTSSGeneratedAuditedCSharpStubBytes",
            "OS.Kernel.Diagnostics.NormalizeErrorCodeNonErrorCodeInterruptFrame",
            "OS.Std.Binary.IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosa"
        })
            _ = source.Type(type);
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

        foreach (bool build in new[] { true, false })
            if (!OS.Tooling.EfiOptions.Parse(["--EnableM04IndependentEndianConformanceProbe"], build).Profile().IsEnabled("EnableM04IndependentEndianConformanceProbe"))
                return false;
        HostCheck.Equal(0, OS.Tooling.ContractAudit.Inspect(root).Count);

        return defaults.Length == 1
            && defaults[0].Value == "false"
            && defaults[0].Attribute("Condition")?.Value ==
                "'$(EnableM04IndependentEndianConformanceProbe)' == ''"
            && symbolGroups.Length == 1
            && symbolGroups[0].Elements("DefineConstants").Single().Value ==
                "$(DefineConstants);M04_INDEPENDENT_ENDIAN_CONFORMANCE_PROBE"
            ;
    }

    private static bool TestNoStdLibCatalogSource()
    {
        const string owner = "OS.Std.Binary.IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosa";
        var source = HostSourceCatalog.Efi(OS.Tooling.EfiOptions.Parse(["--EnableM04IndependentEndianConformanceProbe"], true).Profile());
        var type = source.Type(owner);
        foreach (string name in new[] { "ReferenceFilesRequired", "RequiresHostConformance" })
            HostCheck.True(type.GetMembers(name).OfType<IFieldSymbol>().Single() is
            {
                DeclaredAccessibility: Accessibility.Public, IsConst: true,
                Type.SpecialType: SpecialType.System_Boolean, ConstantValue: false
            });
        var copy = source.Method(owner, "CopyBuiltInCatalog");
        var model = source.Compilation.GetSemanticModel(copy.SyntaxTree);
        var signature = model.GetDeclaredSymbol(copy)!;
        foreach (var (ordinal, element) in new[] { (0, "IndependentEndianReadVector"), (2, "IndependentEndianReverseVector") })
            HostCheck.True(signature.Parameters[ordinal].Type is Microsoft.CodeAnalysis.IPointerTypeSymbol pointer
                && Microsoft.CodeAnalysis.SymbolEqualityComparer.Default.Equals(pointer.PointedAtType, source.Type("OS.Std.Binary." + element)));
        string efiSource = string.Join("\n", new[] { "CopyBuiltInCatalog", "ValidateCatalog", "ComputeCatalogFingerprint" }
            .Select(name => HostSourceText.Active(source.Method(owner, name))));
        return ContainsCode(efiSource, "IndependentEndianConformanceStatus.ReadVectorPointerNull", StringComparison.Ordinal)
            && ContainsCode(efiSource, "IndependentEndianConformanceStatus.ReverseVectorPointerNull", StringComparison.Ordinal)
            && ContainsCode(efiSource, "if (readDestinationCapacity < ReadVectorCount)", StringComparison.Ordinal)
            && ContainsCode(efiSource, "if (reverseDestinationCapacity < ReverseVectorCount)", StringComparison.Ordinal)
            && ContainsCode(efiSource, "public static unsafe IndependentEndianConformanceResult ValidateCatalog(", StringComparison.Ordinal)
            && ContainsCode(efiSource, "public static unsafe ulong ComputeCatalogFingerprint(", StringComparison.Ordinal)
            && HostSecurityCookieSourceGuard.Check(source, owner, "M04SecurityCookieStorageAnchor", 0x434c_524f_5347_5334UL)
            && !efiSource.Contains("Span<", StringComparison.Ordinal)
            && !efiSource.Contains("throw ", StringComparison.Ordinal)
            && !efiSource.Contains("new ", StringComparison.Ordinal);
    }
    private static bool TestEfiEntryMarkerContract()
    {
        const string owner = "OS.Boot.EfiEntry";
        var source = HostSourceCatalog.Efi(OS.Tooling.EfiOptions.Parse(["--EnableM04IndependentEndianConformanceProbe"], true).Profile());
        var type = source.Type(owner);
        var entry = source.Method(owner, "WriteBootMarkers");
        var probe = source.Method(owner, "WriteM04IndependentEndianConformanceProbe");
        string entryText = HostSourceText.Active(entry);
        string probeText = HostSourceText.Active(probe);
        string ownerText = HostSourceText.Documents(source, owner);
        string reconstructedReady = ExtractAsciiMarker(source, "WriteM04IndependentEndianConformanceSuccessLine");
        string reconstructedFailure = ExtractAsciiMarker(source, "WriteM04IndependentEndianConformanceFailureLine");
        int consoleReady = IndexOfAfter(entryText, "WriteEfiConsoleReady(console, serial);", 0);
        int probeCall = IndexOfAfter(entryText, "WriteM04IndependentEndianConformanceProbe(console, serial)", consoleReady);
        int memoryMap = IndexOfAfter(entryText, "TryCaptureUefiMemoryMap(systemTable)", probeCall);
        int copy = IndexOfAfter(probeText, ".CopyBuiltInCatalog(", 0);
        int validate = IndexOfAfter(probeText, ".ValidateCatalog(", copy);
        int guard = IndexOfAfter(probeText, "if (!copyResult.Succeeded ||", validate);
        int success = IndexOfAfter(probeText, "WriteM04IndependentEndianConformanceSuccessLine(console, serial);", guard);
        foreach (string name in new[] { "WriteM04IndependentEndianConformanceProbe",
            "WriteM04IndependentEndianConformanceSuccessLine", "WriteM04IndependentEndianConformanceFailureLine" })
        {
            var target = source.Compilation.GetSemanticModel(source.Method(owner, name).SyntaxTree)
                .GetDeclaredSymbol(source.Method(owner, name));
            int calls = HostSourceOperations.CallsTo(source, target!).Length;
            HostCheck.Equal(1, calls, name + " has one declaration and one bound caller");
        }
        return reconstructedReady == ReadyMarker && reconstructedFailure == FailureMarker
            && consoleReady >= 0 && probeCall > consoleReady && memoryMap > probeCall
            && copy >= 0 && validate > copy && guard > validate && success > guard
            && ContainsCode(probeText, "stackalloc OS.Std.Binary.IndependentEndianReadVector[", StringComparison.Ordinal)
            && ContainsCode(probeText, "stackalloc OS.Std.Binary.IndependentEndianReverseVector[", StringComparison.Ordinal)
            && ContainsCode(probeText, "validationResult.ActualFingerprint != 0xE81E4451E8D999F6UL", StringComparison.Ordinal)
            && HasGuardedSecurityCookieOwners() && FirstExitCompilerRejections()
            && type.GetMembers("M04SecurityCookieStorageAnchor").Length == 0
            && CountOccurrences(ownerText, ReadyMarker) == 0 && CountOccurrences(ownerText, FailureMarker) == 0;
    }
    private static bool HasGuardedSecurityCookieOwners()
    {
        const string stage = "OS.Boot.EfiEntry+M01StageThreeAdapter";
        const string firstExit = "OS.Boot.EfiEntry+M03FirstExitAdapter";
        const string catalog = "OS.Std.Binary.IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosa";
        string[] paging = ["--EnableM01LoadedImageStackProbe", "--EnableM01LiveAllocatorProbe", "--EnableM01OwnedPagingProbe"];
        string[] exited = ["--EnableM01LoadedImageStackProbe", "--EnableM01LiveAllocatorProbe",
            "--EnableAcpiPhysicalDiscoveryProbe", "--EnableM03FirstRealExitBootServicesProbe"];
        foreach (var (arguments, owner, method, value) in new (string[], string?, string?, ulong)[]
        {
            (["--BootPolicy", "Diagnostic"], null, null, 0),
            (["--EnableM04IndependentEndianConformanceProbe"], catalog, "M04SecurityCookieStorageAnchor", 0x434c_524f_5347_5334UL),
            (paging, stage, "SecurityCookieStorageAnchor", 0x434c_524f_5347_5331UL),
            ([.. paging, "--EnableM04IndependentEndianConformanceProbe"], stage, "SecurityCookieStorageAnchor", 0x434c_524f_5347_5331UL),
            (exited, firstExit, "SecurityCookieStorageAnchor", 0x434c_524f_5347_5331UL),
            ([], firstExit, "SecurityCookieStorageAnchor", 0x434c_524f_5347_5331UL)
        })
            if (!HostSecurityCookieSourceGuard.Check(HostSourceCatalog.Efi(OS.Tooling.EfiOptions.Parse(arguments, true).Profile()), owner, method, value))
                return false;
        return true;
    }
    private static bool FirstExitCompilerRejections()
    {
        var source = HostSourceCatalog.Efi(OS.Tooling.EfiOptions.Parse(
            ["--EnableM01LoadedImageStackProbe", "--EnableM01LiveAllocatorProbe",
                "--EnableAcpiPhysicalDiscoveryProbe", "--EnableM03FirstRealExitBootServicesProbe"], true).Profile());
        var documents = source.Documents;
        const string prerequisite = "The first real exit requires the original loaded-image, live allocator and copied ACPI adapters.";
        const string exclusive = "The first real exit is an exclusive firmware-CR3 path, with no intervening paging, heap or controlled exit probe.";
        foreach (var (symbol, add, expected) in new (string, bool, string)[]
        {
            ("M01_LOADED_IMAGE_STACK_PROBE", false, prerequisite),
            ("M01_LIVE_ALLOCATOR_PROBE", false, prerequisite),
            ("ACPI_PHYSICAL_DISCOVERY_PROBE", false, prerequisite),
            ("M01_OWNED_PAGING_PROBE", true, exclusive),
            ("M04_MANAGED_HEAP_PAGE_RANGE_PROBE", true, exclusive),
            ("M04_INDEPENDENT_ENDIAN_CONFORMANCE_PROBE", true, exclusive),
            ("M03_TYPED_EXIT_BOOT_SERVICES_CALL_PROBE", true, exclusive)
        })
        {
            // These deliberately invalid symbol sets must fail in the source
            // compiler, independently of the host profile argument validator.
            bool rejected = documents.Any(document =>
            {
                var options = (CSharpParseOptions)document.Syntax.Options;
                var symbols = options.PreprocessorSymbolNames.ToHashSet(StringComparer.Ordinal);
                HostCheck.True(!symbols.Contains("CLROS_NORMAL_VERIFIED_EXIT"), "diagnostic first-exit authority");
                HostCheck.True(add ? symbols.Add(symbol) : symbols.Remove(symbol), "control changes selected symbols");
                var syntax = CSharpSyntaxTree.ParseText(document.Text, options.WithPreprocessorSymbols(symbols), document.Path);
                return syntax.GetDiagnostics().Any(diagnostic => diagnostic.Id == "CS1029"
                    && diagnostic.GetMessage().Contains(expected, StringComparison.Ordinal));
            });
            HostCheck.True(rejected, "compiler rejects first-exit symbol change: " + symbol);
        }
        return true;
    }

    private static bool TestQemuProfile()
    {
        const string flag = "--EnableM04IndependentEndianConformanceProbe";
        var plan = OS.Tooling.QemuProfiles.Create(OS.Tooling.QemuSmokeOptions.Parse("--boot-policy", "Diagnostic", "--dry-run", "--build-efi", flag));
        if (!plan.Required.Contains(ReadyMarker) || !plan.Forbidden.Contains(FailureMarker) || !plan.IsEnabled("EnableM04IndependentEndianConformanceProbe"))
            return false;
        foreach (string[] arguments in new string[][] { [flag], ["--build-efi", "--skip-image-build", flag] })
        {
            var result = HostToolProcess.RunTool(["qemu", "smoke", "--boot-policy", "Diagnostic", "--dry-run", .. arguments]);
            if (result.ExitCode == 0 || result.Output.Contains("TEST_PASS qemu.runner.dryrun", StringComparison.Ordinal))
                return false;
        }
        return true;
    }

    private static string ExtractAsciiMarker(OS.Tooling.CSharpSourceCatalog source, string methodName) =>
        HostSourceText.ByteCharacters(source, "OS.Boot.EfiEntry", methodName);

    private static bool ContainsCode(string source, string fragment, StringComparison comparison) =>
        comparison == StringComparison.Ordinal && HostSourceText.Contains(source, fragment);

    private static int CountCodeOccurrences(string source, string fragment) => HostSourceText.Count(source, fragment);

    private static int IndexOfAfter(string source, string value, int start) => HostSourceText.IndexOf(source, value, start);

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
