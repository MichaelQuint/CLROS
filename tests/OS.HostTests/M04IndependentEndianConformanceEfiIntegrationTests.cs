using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

internal static class M04IndependentEndianConformanceEfiIntegrationTests
{
    private const string TestName = "m04.002.efi.integration";
    private const string ReadyMarker =
        OS.Tooling.QemuContracts.Record278;
    private const string FailureMarker =
        OS.Tooling.QemuContracts.Record274;
    private static readonly string CompileClosureSha256 =
        OS.Tooling.EfiContracts.CompileInventorySha256;

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
        XElement[] compileItems = project.Descendants("Compile").ToArray();
        string[] compileKeys = compileItems
            .Select(item =>
                (item.Attribute("Include")?.Value ?? string.Empty) + "|" +
                (item.Attribute("Link")?.Value ?? string.Empty))
            .ToArray();
        string compileHash = Convert.ToHexString(
                SHA256.HashData(
                    Encoding.UTF8.GetBytes(string.Join("\n", compileKeys) + "\n")))
            .ToLowerInvariant();
        const string CatalogKey =
            @"..\..\std\no-runtime\shared\Binary\IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMOSA.cs|Std\Binary\IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMOSA.cs";
        const string GuardedIstKey =
            @"..\OS.Kernel\Diagnostics\GuardedISTStacksDoubleFaultPageFaultNMI.cs|Diagnostics\GuardedISTStacksDoubleFaultPageFaultNMI.cs";
        const string TerminalKey =
            @"..\OS.Kernel\Diagnostics\OwnedGDTTSSGeneratedAuditedCSharpStubBytes.cs|Diagnostics\OwnedGDTTSSGeneratedAuditedCSharpStubBytes.cs";
        const string NormalizedInterruptFrameKey =
            @"..\OS.Kernel\Diagnostics\NormalizeErrorCodeNonErrorCodeInterruptFrame.cs|Diagnostics\NormalizeErrorCodeNonErrorCodeInterruptFrame.cs";
        int backendIndex = Array.IndexOf(
            compileKeys,
            @"..\OS.Kernel\Diagnostics\COM1OwnedPortIOBackend.cs|Diagnostics\COM1OwnedPortIOBackend.cs");
        int ownedWriterIndex = Array.IndexOf(
            compileKeys,
            @"..\OS.Kernel\Diagnostics\OWNEDCOM1READYExclusivelyOwnedWriter.cs|Diagnostics\OWNEDCOM1READYExclusivelyOwnedWriter.cs");
        int panicReporterIndex = Array.IndexOf(
            compileKeys,
            @"..\OS.Kernel\Diagnostics\PanicSafeFormattingSerialReportingOwnedCOM1.cs|Diagnostics\PanicSafeFormattingSerialReportingOwnedCOM1.cs");
        int gdtIndex = Array.IndexOf(
            compileKeys,
            @"..\OS.Kernel\Diagnostics\OwnedX64GDTContainingNullN64BitCode.cs|Diagnostics\OwnedX64GDTContainingNullN64BitCode.cs");
        int guardedIstIndex = Array.IndexOf(compileKeys, GuardedIstKey);
        int terminalIndex = Array.IndexOf(compileKeys, TerminalKey);
        int normalizedInterruptFrameIndex =
            Array.IndexOf(compileKeys, NormalizedInterruptFrameKey);
        int catalogIndex = Array.IndexOf(compileKeys, CatalogKey);

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
            if (!OS.Tooling.EfiOptions.Parse(["--EnableM04IndependentEndianConformanceProbe"], build).Profile().IsEnabled("EnableM04IndependentEndianConformanceProbe")) return false;
        HostCheck.Equal(0, OS.Tooling.ContractAudit.Inspect(root).Count);

        return compileItems.Length == OS.Tooling.EfiContracts.CompileItems.Count
            && compileHash == CompileClosureSha256
            && backendIndex >= 0
            && ownedWriterIndex == backendIndex + 1
            && panicReporterIndex == ownedWriterIndex + 1
            && gdtIndex == panicReporterIndex + 1
            && guardedIstIndex == gdtIndex + 1
            && terminalIndex == guardedIstIndex + 1
            && normalizedInterruptFrameIndex == terminalIndex + 1
            && catalogIndex == normalizedInterruptFrameIndex + 17
            && compileKeys.Count(key => key == CatalogKey) == 1
            && defaults.Length == 1
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
        string root = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(
            Path.Combine(
                root,
                "std",
                "no-runtime",
                "shared",
                "Binary",
                "IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMOSA.cs"));

        int hostBranch = source.IndexOf("#if !NOSTDLIB_EFI", StringComparison.Ordinal);
        int efiBranch = source.IndexOf("#else", hostBranch, StringComparison.Ordinal);
        int end = source.IndexOf("#endif", efiBranch, StringComparison.Ordinal);
        string efiSource = hostBranch >= 0 && efiBranch > hostBranch && end > efiBranch
            ? source[efiBranch..end]
            : string.Empty;

        return source.Contains(
                "public const bool ReferenceFilesRequired = false;",
                StringComparison.Ordinal)
            && source.Contains(
                "public const bool RequiresHostConformance = false;",
                StringComparison.Ordinal)
            && efiSource.Contains(
                "IndependentEndianReadVector* readDestination",
                StringComparison.Ordinal)
            && efiSource.Contains(
                "IndependentEndianReverseVector* reverseDestination",
                StringComparison.Ordinal)
            && efiSource.Contains(
                "IndependentEndianConformanceStatus.ReadVectorPointerNull",
                StringComparison.Ordinal)
            && efiSource.Contains(
                "IndependentEndianConformanceStatus.ReverseVectorPointerNull",
                StringComparison.Ordinal)
            && efiSource.Contains(
                "if (readDestinationCapacity < ReadVectorCount)",
                StringComparison.Ordinal)
            && efiSource.Contains(
                "if (reverseDestinationCapacity < ReverseVectorCount)",
                StringComparison.Ordinal)
            && efiSource.Contains(
                "public static unsafe IndependentEndianConformanceResult ValidateCatalog(",
                StringComparison.Ordinal)
            && efiSource.Contains(
                "public static unsafe ulong ComputeCatalogFingerprint(",
                StringComparison.Ordinal)
            && source.Contains(
                "#if NOSTDLIB_EFI && M04_INDEPENDENT_ENDIAN_CONFORMANCE_PROBE && !M01_OWNED_PAGING_PROBE",
                StringComparison.Ordinal)
            && source.Contains(
                "[System.Runtime.RuntimeExport(\"__security_cookie\")]",
                StringComparison.Ordinal)
            && source.Contains(
                "private static ulong M04SecurityCookieStorageAnchor()",
                StringComparison.Ordinal)
            && CountOccurrences(source, "\"__security_cookie\"") == 1
            && !efiSource.Contains("Span<", StringComparison.Ordinal)
            && !efiSource.Contains("throw ", StringComparison.Ordinal)
            && !efiSource.Contains("new ", StringComparison.Ordinal);
    }

    private static bool TestEfiEntryMarkerContract()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(
            Path.Combine(root, "src", "OS", "Boot", "EfiEntry.cs")).Replace("\r\n", "\n", StringComparison.Ordinal);
        string reconstructedReady = ExtractAsciiMarker(
            source,
            "private static void WriteM04IndependentEndianConformanceSuccessLine(");
        string reconstructedFailure = ExtractAsciiMarker(
            source,
            "private static void WriteM04IndependentEndianConformanceFailureLine(");

        int consoleReady = source.IndexOf(
            "WriteEfiConsoleReady(console, serial);",
            StringComparison.Ordinal);
        int probe = IndexOfAfter(
            source,
            "WriteM04IndependentEndianConformanceProbe(console, serial)",
            consoleReady);
        int memoryMap = IndexOfAfter(
            source,
            "TryCaptureUefiMemoryMap(systemTable)",
            probe);
        int copy = source.IndexOf(".CopyBuiltInCatalog(", StringComparison.Ordinal);
        int validate = IndexOfAfter(source, ".ValidateCatalog(", copy);
        int guard = IndexOfAfter(source, "if (!copyResult.Succeeded ||", validate);
        int success = IndexOfAfter(
            source,
            "WriteM04IndependentEndianConformanceSuccessLine(console, serial);",
            guard);

        return reconstructedReady == ReadyMarker
            && reconstructedFailure == FailureMarker
            && consoleReady >= 0
            && probe > consoleReady
            && memoryMap > probe
            && copy >= 0
            && validate > copy
            && guard > validate
            && success > guard
            && source.Contains(
                "stackalloc OS.Std.Binary.IndependentEndianReadVector[",
                StringComparison.Ordinal)
            && source.Contains(
                "stackalloc OS.Std.Binary.IndependentEndianReverseVector[",
                StringComparison.Ordinal)
            && source.Contains(
                "validationResult.ActualFingerprint != 0xE81E4451E8D999F6UL",
                StringComparison.Ordinal)
            && CountOccurrences(
                source,
                "WriteM04IndependentEndianConformanceProbe(") == 2
            && CountOccurrences(
                source,
                "WriteM04IndependentEndianConformanceSuccessLine(") == 2
            && CountOccurrences(
                source,
                "WriteM04IndependentEndianConformanceFailureLine(") == 2
            && HasGuardedSecurityCookieOwners(source)
            && !source.Contains(
                "M04SecurityCookieStorageAnchor",
                StringComparison.Ordinal)
            && CountOccurrences(source, ReadyMarker) == 0
            && CountOccurrences(source, FailureMarker) == 0;
    }

    private static bool HasGuardedSecurityCookieOwners(string source)
    {
        source = source.Replace("\r\n", "\n", StringComparison.Ordinal);
        const string export =
            "[System.Runtime.RuntimeExport(\"__security_cookie\")]";
        // Cookie ownership depends on these prerequisites/exclusions, not on
        // unrelated conditional using directives above them.
        string incompatibleProfiles = """
            #if !(M01_LOADED_IMAGE_STACK_PROBE || CLROS_NORMAL_VERIFIED_EXIT) || !(M01_LIVE_ALLOCATOR_PROBE || CLROS_NORMAL_VERIFIED_EXIT) || !(ACPI_PHYSICAL_DISCOVERY_PROBE || CLROS_NORMAL_VERIFIED_EXIT)
            #error The first real exit requires the original loaded-image, live allocator and copied ACPI adapters.
            #endif
            #if M01_OWNED_PAGING_PROBE || M04_MANAGED_HEAP_PAGE_RANGE_PROBE || M04_INDEPENDENT_ENDIAN_CONFORMANCE_PROBE || M03_TYPED_EXIT_BOOT_SERVICES_CALL_PROBE
            #error The first real exit is an exclusive firmware-CR3 path, with no intervening paging, heap or controlled exit probe.
            #endif
            """.Replace("\r\n", "\n", StringComparison.Ordinal);

        bool HasOwner(string guard, string owner, string declaration)
        {
            string ownerToken = "    private static " + (owner == "M03FirstExitAdapter" ? "partial " : string.Empty)
                + "class " + owner + "\n    {";
            // Only comments/blank lines may separate the guard and owner.
            // Adapter types close at four spaces; their members close deeper.
            MatchCollection matches = Regex.Matches(
                source,
                "^#if " + Regex.Escape(guard) + "\n"
                    + @"(?:[ \t]*//[^\n]*\n|[ \t]*\n)*"
                    + Regex.Escape(ownerToken)
                    + @"\n(?<body>[\s\S]*?)\n    \}\n#endif(?=\n|\z)",
                RegexOptions.Multiline | RegexOptions.CultureInvariant);
            if (CountOccurrences(source, ownerToken) != 1 || matches.Count != 1)
            {
                return false;
            }

            string body = matches[0].Groups["body"].Value;
            return CountOccurrences(body, export) == 1
                && CountOccurrences(body, "\"__security_cookie\"") == 1
                && CountOccurrences(body, "SecurityCookieStorageAnchor(") == 1
                && CountOccurrences(body, declaration) == 1;
        }

        return CountOccurrences(source, "\"__security_cookie\"") == 2
            && CountOccurrences(source, export) == 2
            && CountOccurrences(source, incompatibleProfiles) == 1
            && HasOwner(
                "(M03_FIRST_REAL_EXIT_BOOT_SERVICES_PROBE || CLROS_NORMAL_VERIFIED_EXIT)",
                "M03FirstExitAdapter",
                export + "\n        private static ulong SecurityCookieStorageAnchor()"
                    + " => 0x434c_524f_5347_5331UL;")
            && HasOwner(
                "M01_OWNED_PAGING_PROBE",
                "M01StageThreeAdapter",
                export + "\n        private static ulong SecurityCookieStorageAnchor()\n"
                    + "        {\n            return 0x434c_524f_5347_5331UL;\n"
                    + "        }");
    }

    private static bool TestQemuProfile()
{
    const string flag = "--EnableM04IndependentEndianConformanceProbe";
    var plan = OS.Tooling.QemuProfiles.Create(OS.Tooling.QemuSmokeOptions.Parse("--boot-policy", "Diagnostic", "--dry-run", "--build-efi", flag));
    if (!plan.Required.Contains(ReadyMarker) || !plan.Forbidden.Contains(FailureMarker) || !plan.IsEnabled("EnableM04IndependentEndianConformanceProbe")) return false;
    foreach (string[] arguments in new string[][] { [flag], ["--build-efi", "--skip-image-build", flag] })
    {
        var result = HostToolProcess.RunTool(["qemu", "smoke", "--boot-policy", "Diagnostic", "--dry-run", ..arguments]);
        if (result.ExitCode == 0 || result.Output.Contains("TEST_PASS qemu.runner.dryrun", StringComparison.Ordinal)) return false;
    }
    return true;
}

    private static string ExtractAsciiMarker(string source, string methodToken)
    {
        int start = source.IndexOf(methodToken, StringComparison.Ordinal);
        int end = start < 0
            ? -1
            : source.IndexOf(
                "\n    private static ",
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

    private static int IndexOfAfter(string source, string value, int start)
    {
        return start < 0 || start > source.Length
            ? -1
            : source.IndexOf(value, start, StringComparison.Ordinal);
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
