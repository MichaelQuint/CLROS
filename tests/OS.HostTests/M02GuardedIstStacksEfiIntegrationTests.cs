using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Tooling;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

internal static partial class M02GuardedIstStacksEfiIntegrationTests
{
    private const string TestName = "m02.008.efi.integration";
    private const string ReadyMarker =
        OS.Tooling.QemuContracts.Record112;
    private const string FailureMarker =
        OS.Tooling.QemuContracts.Record111;
    private static readonly string CompileClosureSha256 =
        OS.Tooling.EfiContracts.CompileInventorySha256;
    private const string ProjectFileSha256 =
        OS.Tooling.EfiContracts.ProjectSourceSha256;
    private const string CanonicalProjectShapeSha256 =
        OS.Tooling.EfiContracts.CanonicalProjectSha256;

    [HostFixture(HostLane.Fast)]
    internal static void RunAll()
    {
        Console.WriteLine("TEST_START " + TestName);
        int failures = 0;

        RunCase(
            "m02.008 EFI primitive and exact source closure are explicit",
            TestPrimitiveAndCompileClosure,
            ref failures);
        RunCase(
            "m02.008 EFI allocates validates touches releases and frees in order",
            TestGuestArenaSequence,
            ref failures);
        RunCase(
            "m02.008 EFI markers are exact mutually exclusive and pre-firmware-exit",
            TestMarkerEncodingAndFailureGate,
            ref failures);
        RunCase(
            "m02.008 QEMU profile is cumulative exact and fail closed",
            TestQemuProfileSource,
            ref failures);
        RunCase(
            "m02.008 immutable inputs remain exact and documentation boundaries remain semantic",
            TestImmutableInputsAndDocumentationBoundaries,
            ref failures);

        if (failures != 0)
        {
            Console.WriteLine(
                "TEST_FAIL " + TestName + " host-contract failures=" + failures);
            throw new InvalidOperationException(
                "M02-008 EFI integration host tests failed: " + failures);
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

    private static bool TestPrimitiveAndCompileClosure()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        var catalog = IstSources();
        var primitive = catalog.Type("System.UInt64");
        var maximum = primitive.GetMembers("MaxValue").OfType<IFieldSymbol>().Single();
        bool maxValue = primitive.TypeKind == TypeKind.Struct
            && primitive.DeclaredAccessibility == Accessibility.Public
            && maximum.IsConst
            && maximum.DeclaredAccessibility == Accessibility.Public
            && maximum.Type.SpecialType == SpecialType.System_UInt64
            && maximum.ConstantValue is ulong.MaxValue;
        foreach (string owner in new[]
        {
            "OS.Kernel.Diagnostics.OwnedX64GdtContainingNullN64BitCode",
            "OS.Kernel.Diagnostics.GuardedISTStacksDoubleFaultPageFaultNMI",
            "OS.Kernel.Diagnostics.OwnedGDTTSSGeneratedAuditedCSharpStubBytes",
            "OS.Kernel.Diagnostics.NormalizeErrorCodeNonErrorCodeInterruptFrame",
            "OS.Std.Binary.IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosa"
        }

        )
            foreach (var document in catalog.TypeSources(owner))
            {
                string include = Path.GetRelativePath(Path.Combine(root, "src", "OS"), document.Path).Replace('/', '\\');
                if (owner != "OS.Std.Binary.IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosa")
                    HostCheck.True(HostEfiSourceAdmission.Accepts(include, document.Text), "compiled reviewed source " + owner);
            }

        XDocument project = XDocument.Load(Path.Combine(root, "src", "OS", "OS.csproj"));
        XElement[] defaults = project.Descendants("EnableM02GuardedIstStackAllocationProbe").Where(element => element.Parent?.Name.LocalName == "PropertyGroup").ToArray();
        XElement[] symbolGroups = project.Descendants("PropertyGroup").Where(element => element.Attribute("Condition")?.Value == "'$(EnableM02GuardedIstStackAllocationProbe)' == 'true'").ToArray();
        string[] dependencyConditions = project.Descendants("Error").Select(element => element.Attribute("Condition")?.Value ?? string.Empty).Where(value => value.StartsWith("'$(EnableM02GuardedIstStackAllocationProbe)' == 'true'", StringComparison.Ordinal)).ToArray();
        return (maxValue
            && defaults.Length == 1
            && defaults[0].Value == "false"
            && defaults[0].Attribute("Condition")?.Value == "'$(EnableM02GuardedIstStackAllocationProbe)' == ''"
            && symbolGroups.Length == 1
            && symbolGroups[0].Elements("DefineConstants").Single().Value == "$(DefineConstants);M02_GUARDED_IST_STACK_ALLOCATION_PROBE"
            && dependencyConditions.Contains("'$(EnableM02GuardedIstStackAllocationProbe)' == 'true' and '$(EnableM04GcStaticRootMaterializationProbe)' == 'true'", StringComparer.Ordinal)
            && dependencyConditions.Contains("'$(EnableM02GuardedIstStackAllocationProbe)' == 'true' and '$(EnableM03TypedExitBootServicesCallProbe)' == 'true'", StringComparer.Ordinal)
            && dependencyConditions.Contains("'$(EnableM02GuardedIstStackAllocationProbe)' == 'true' and '$(EnableM04TypeManagerModuleRegistrationProbe)' == 'true'", StringComparer.Ordinal)
            && dependencyConditions.Contains("'$(EnableM02GuardedIstStackAllocationProbe)' == 'true' and '$(EnableM04CheckedReferenceBarrierProbe)' == 'true'", StringComparer.Ordinal)
            && dependencyConditions.Contains("'$(EnableM02GuardedIstStackAllocationProbe)' == 'true' and '$(EnableM03MemoryMapCaptureProbe)' == 'true'", StringComparer.Ordinal)
            && dependencyConditions.Contains("'$(EnableM02GuardedIstStackAllocationProbe)' == 'true' and '$(EnableM03FirmwareEntropySeedProbe)' == 'true'", StringComparer.Ordinal)
            && dependencyConditions.Contains("'$(EnableM02GuardedIstStackAllocationProbe)' == 'true' and '$(EnableM02UnexpectedVectorPanicProbe)' == 'true'", StringComparer.Ordinal)
            && dependencyConditions.Contains("'$(EnableM02GuardedIstStackAllocationProbe)' == 'true' and '$(EnableM04SzArrayAllocationProbe)' == 'true'", StringComparer.Ordinal)
            && dependencyConditions.Contains("'$(EnableM02GuardedIstStackAllocationProbe)' == 'true' and '$(EnableM04StringAllocationProbe)' == 'true'", StringComparer.Ordinal)
            && dependencyConditions.Contains("'$(EnableM02GuardedIstStackAllocationProbe)' == 'true' and '$(EnableM04StringAllocationBeforeBindProbe)' == 'true'", StringComparer.Ordinal)
            && dependencyConditions.Contains("'$(EnableM02GuardedIstStackAllocationProbe)' == 'true' and '$(EnableM04StringAllocationExhaustionProbe)' == 'true'", StringComparer.Ordinal)
            && dependencyConditions.Contains("'$(EnableM02GuardedIstStackAllocationProbe)' == 'true' and '$(EnableM04StringAllocationInvalidLengthProbe)' == 'true'", StringComparer.Ordinal)
            && dependencyConditions.Contains("'$(EnableM02GuardedIstStackAllocationProbe)' == 'true' and '$(EnableM04SzArrayAllocationBeforeBindProbe)' == 'true'", StringComparer.Ordinal)
            && dependencyConditions.Contains("'$(EnableM02GuardedIstStackAllocationProbe)' == 'true' and '$(EnableM04SzArrayAllocationExhaustionProbe)' == 'true'", StringComparer.Ordinal)
            && dependencyConditions.Contains("'$(EnableM02GuardedIstStackAllocationProbe)' == 'true' and '$(EnableM02OwnedX64GdtEncodingProbe)' != 'true'", StringComparer.Ordinal))
            && OS.Tooling.ContractAudit.Inspect(root).Count == 0;
    }

    private static bool TestGuestArenaSequence()
    {
        var catalog = IstSources();
        var method = IstEvidenceMethod(catalog);
        string source = method.ToFullString();
        int priorMarker = source.IndexOf("WriteM02OwnedX64GdtEncodingSuccessLine(", StringComparison.Ordinal);
        int switchStart = IndexOfAfter(source, "#if M02_GUARDED_IST_STACK_ALLOCATION_PROBE", priorMarker);
        int switchEnd = IndexOfAfter(source, "#endif", switchStart);
        if (switchStart < 0 || switchEnd <= switchStart)
        {
            return false;
        }

        int bodyStart = source.IndexOf('\n', switchStart);
        if (bodyStart < 0 || bodyStart >= switchEnd)
            return false;
        string probe = source[bodyStart..switchEnd];
        int bootServicesGate = HostSourceText.IndexOf(probe, "bootServices == null ||");
        int allocatePages = HostSourceText.IndexOf(probe, "bootServices->AllocatePages(", bootServicesGate);
        int request = HostSourceText.IndexOf(probe, ".GuardedIstStackArenaRequest(", allocatePages);
        int initialize = HostSourceText.IndexOf(probe, ".Initialize(", request);
        int allocate = HostSourceText.IndexOf(probe, ".Allocate(ref m02GuardedState);", initialize);
        int layoutGate = HostSourceText.IndexOf(probe, "bool m02GuardedLayoutValid =", allocate);
        int firstStackWrite = HostSourceText.IndexOf(probe, "m02GuardedDoubleFaultBytes[0] = 0xD1;", layoutGate);
        int readBack = HostSourceText.IndexOf(probe, "m02GuardedStackReadBack =", firstStackWrite);
        int release = HostSourceText.IndexOf(probe, ".Release(", readBack);
        int freePages = HostSourceText.IndexOf(probe, "bootServices->FreePages(", release);
        int finalFailureGate = HostSourceText.IndexOf(probe, "if (!m02GuardedLayoutValid ||", freePages);
        int success = HostSourceText.IndexOf(probe, "WriteM02GuardedIstStackAllocationSuccessLine(", finalFailureGate);
        return HostSourceText.Contains(HostSourceText.Active(method.ParameterList), "EfiBootServices* bootServices, int lineIndex)") ? bootServicesGate >= 0 && allocatePages > bootServicesGate && request > allocatePages && initialize > request && allocate > initialize && layoutGate > allocate && firstStackWrite > layoutGate && readBack > firstStackWrite && release > readBack && freePages > release && finalFailureGate > freePages && success > finalFailureGate && HostSourceText.Contains(probe, "const ulong m02GuardedPageCount = 6;") && HostSourceText.Contains(probe, "const ulong m02GuardedPageSize = 4096;") && HostSourceText.Contains(probe, "EfiLoaderData") && HostSourceText.Contains(probe, "0x004D30322D303038UL") && HostSourceText.Contains(probe, "stackWritable: true") && HostSourceText.Contains(probe, "stackExecutable: false") && HostSourceText.Contains(probe, "m02GuardedDoubleFault.IstIndex == 1") && HostSourceText.Contains(probe, "m02GuardedPageFault.IstIndex == 2") && HostSourceText.Contains(probe, "m02GuardedNmi.IstIndex == 3") && HostSourceText.Contains(probe, "!m02GuardedDoubleFault.GuardPagePresent") && HostSourceText.Contains(probe, "!m02GuardedPageFault.GuardPagePresent") && HostSourceText.Contains(probe, "!m02GuardedNmi.GuardPagePresent") && HostSourceText.Contains(probe, "m02GuardedDoubleFaultBytes[m02GuardedPageSize - 1]") && HostSourceText.Contains(probe, "m02GuardedPageFaultBytes[m02GuardedPageSize - 1]") && HostSourceText.Contains(probe, "m02GuardedNmiBytes[m02GuardedPageSize - 1]") && !probe.Contains("(byte*)m02GuardedDoubleFault.GuardBaseAddress", StringComparison.Ordinal) && !probe.Contains("(byte*)m02GuardedPageFault.GuardBaseAddress", StringComparison.Ordinal) && !probe.Contains("(byte*)m02GuardedNmi.GuardBaseAddress", StringComparison.Ordinal) && CountOccurrences(probe, ".Initialize(") == 1 && CountOccurrences(probe, ".Allocate(ref m02GuardedState);") == 1 && CountOccurrences(probe, ".Release(") == 1 && CountOccurrences(probe, "m02GuardedStackReadBack =") == 2 : false;
    }

    private static bool TestMarkerEncodingAndFailureGate()
    {
        var catalog = IstSources();
        var method = IstEvidenceMethod(catalog);
        string source = method.ToFullString();
        string raw = HostSourceText.Documents(catalog, "OS.Boot.EfiEntry");
        source = HostSourceText.Active(method);
        string reconstructedReady = HostSourceText.ByteCharacters(catalog, IstEntryOwner, "WriteM02GuardedIstStackAllocationSuccessLine");
        string reconstructedFailure = HostSourceText.ByteCharacters(catalog, IstEntryOwner, "WriteM02GuardedIstStackAllocationFailureLine");
        int finalGate = HostSourceText.IndexOf(source, "if (!m02GuardedLayoutValid ||");
        int failureCall = HostSourceText.IndexOf(source, "WriteM02GuardedIstStackAllocationFailureLine(", finalGate);
        int abortReturn = HostSourceText.IndexOf(source, "return;", failureCall);
        int successCall = HostSourceText.IndexOf(source, "WriteM02GuardedIstStackAllocationSuccessLine(", abortReturn);
        return reconstructedReady == ReadyMarker && reconstructedFailure == FailureMarker && ReadyMarker.Contains("guestGuardMappings=false", StringComparison.Ordinal) && ReadyMarker.Contains("guestTss=false", StringComparison.Ordinal) && ReadyMarker.Contains("guestIdt=false", StringComparison.Ordinal) && ReadyMarker.Contains("arenaFreed=true", StringComparison.Ordinal) && ReadyMarker.Contains("postExitBootServices=false", StringComparison.Ordinal) && !ReadyMarker.Contains("CloudReady", StringComparison.OrdinalIgnoreCase) && !ReadyMarker.Contains("production", StringComparison.OrdinalIgnoreCase) && finalGate >= 0 && failureCall > finalGate && abortReturn > failureCall && successCall > abortReturn && CountOccurrences(raw, "WriteM02GuardedIstStackAllocationSuccessLine(") == 2 && CountOccurrences(raw, "WriteM02GuardedIstStackAllocationFailureLine(") == 4 && !raw.Contains("\"" + ReadyMarker + "\"", StringComparison.Ordinal) && !raw.Contains("\"" + FailureMarker + "\"", StringComparison.Ordinal);
    }

    private static bool TestQemuProfileSource() => EfiProfileTestControls.Returning("M02GuardedIstStackAllocation", OS.Tooling.QemuContracts.M02GuardedIstStackAllocationRequiredMarkers, OS.Tooling.QemuContracts.M02GuardedIstStackAllocationForbiddenMarkers, ReadyMarker, FailureMarker);

    private static bool TestImmutableInputsAndDocumentationBoundaries()
    {
        HostCheck.Registered(typeof(GuardedISTStacksDoubleFaultPageFaultNMITests));
        string root = HostToolProcess.FindRepositoryRoot();
        if (!HostSourceOracle.MatchesUtf8Family(HostSourceCatalog.Project("src/OS.Kernel/OS.Kernel.csproj"), EfiContracts.GuardedIstHostOracleTypes))
            return false;
        string diagnostics = File.ReadAllText(Path.Combine(root, "docs", "diagnostics.md"));
        string bootOrder = File.ReadAllText(Path.Combine(root, "docs", "boot-order.md"));
        if (!HasRequiredDocumentationBoundaries(diagnostics, bootOrder))
        {
            return false;
        }

        string fixtureRoot = Path.Combine(Path.GetTempPath(), "clros-m02-008-documentation-boundaries-" + Guid.NewGuid().ToString("N"));
        bool hostileFixturesRejected;
        try
        {
            Directory.CreateDirectory(fixtureRoot);
            hostileFixturesRejected = WriteHostileFixtureAndRequireRejection(fixtureRoot, "diagnostics-missing-issue-identity", diagnostics.Replace("M02-008 adds a caller-owned", "A removed issue adds a caller-owned", StringComparison.Ordinal), bootOrder) && WriteHostileFixtureAndRequireRejection(fixtureRoot, "diagnostics-missing-three-guard-intent", diagnostics.Replace("one non-present guard range followed", "one unspecified range followed", StringComparison.Ordinal), bootOrder) && WriteHostileFixtureAndRequireRejection(fixtureRoot, "diagnostics-missing-three-stack-intent", diagnostics.Replace("three stack ranges", "an unspecified set of stack ranges", StringComparison.Ordinal), bootOrder) && WriteHostileFixtureAndRequireRejection(fixtureRoot, "diagnostics-missing-loaderdata-evidence", diagnostics.Replace("temporary six-page UEFI", "temporary unspecified", StringComparison.Ordinal), bootOrder) && WriteHostileFixtureAndRequireRejection(fixtureRoot, "diagnostics-missing-stack-only-readback", diagnostics.Replace("writes and reads back only the", "touches an unspecified set of", StringComparison.Ordinal), bootOrder) && WriteHostileFixtureAndRequireRejection(fixtureRoot, "diagnostics-missing-release-free-boundary", diagnostics.Replace("releases the lease, and frees the same arena before return", "retains the lease and leaves cleanup unspecified", StringComparison.Ordinal), bootOrder) && WriteHostileFixtureAndRequireRejection(fixtureRoot, "diagnostics-missing-capability-negation", Regex.Replace(diagnostics, @"The proof does\s+not change page tables", "The proof does change page tables", RegexOptions.CultureInvariant), bootOrder) && WriteHostileFixtureAndRequireRejection(fixtureRoot, "diagnostics-missing-guard-nonclaim", diagnostics.Replace("map guards non-present", "activate guard mappings", StringComparison.Ordinal), bootOrder) && WriteHostileFixtureAndRequireRejection(fixtureRoot, "diagnostics-missing-tss-nonclaim", diagnostics.Replace("write TSS IST slots", "install live TSS IST slots", StringComparison.Ordinal), bootOrder) && WriteHostileFixtureAndRequireRejection(fixtureRoot, "diagnostics-missing-idt-nonclaim", diagnostics.Replace("descriptor tables", "active descriptor state", StringComparison.Ordinal), bootOrder) && WriteHostileFixtureAndRequireRejection(fixtureRoot, "diagnostics-missing-fault-nonclaim", diagnostics.Replace("execute a fault", "exercise live fault behavior", StringComparison.Ordinal), bootOrder) && WriteHostileFixtureAndRequireRejection(fixtureRoot, "diagnostics-missing-exitbootservices-nonclaim", diagnostics.Replace("cross real", "complete a live", StringComparison.Ordinal), bootOrder) && WriteHostileFixtureAndRequireRejection(fixtureRoot, "boot-order-missing-issue-identity", diagnostics, bootOrder.Replace("M02-008 adds a host-tested", "A removed issue adds a host-tested", StringComparison.Ordinal)) && WriteHostileFixtureAndRequireRejection(fixtureRoot, "boot-order-missing-three-pair-intent", diagnostics, bootOrder.Replace("three adjacent", "an unspecified number of adjacent", StringComparison.Ordinal)) && WriteHostileFixtureAndRequireRejection(fixtureRoot, "boot-order-missing-loaderdata-evidence", diagnostics, bootOrder.Replace("temporary six-page LoaderData allocation", "temporary allocation", StringComparison.Ordinal)) && WriteHostileFixtureAndRequireRejection(fixtureRoot, "boot-order-missing-stack-only-readback", diagnostics, bootOrder.Replace("stack-only write/readback", "unspecified memory access", StringComparison.Ordinal)) && WriteHostileFixtureAndRequireRejection(fixtureRoot, "boot-order-missing-release-boundary", diagnostics, bootOrder.Replace("lease release", "lease retention", StringComparison.Ordinal)) && WriteHostileFixtureAndRequireRejection(fixtureRoot, "boot-order-missing-same-allocation-free-boundary", diagnostics, bootOrder.Replace("same-base", "unbound", StringComparison.Ordinal)) && WriteHostileFixtureAndRequireRejection(fixtureRoot, "boot-order-missing-guard-nonclaim", diagnostics, bootOrder.Replace("no guard mapping", "active guard mapping", StringComparison.Ordinal)) && WriteHostileFixtureAndRequireRejection(fixtureRoot, "boot-order-missing-tss-idt-nonclaim", diagnostics, bootOrder.Replace("TSS/IDT installation", "active descriptor installation", StringComparison.Ordinal)) && WriteHostileFixtureAndRequireRejection(fixtureRoot, "boot-order-missing-fault-nonclaim", diagnostics, bootOrder.Replace("fault execution", "live fault behavior", StringComparison.Ordinal)) && WriteHostileFixtureAndRequireRejection(fixtureRoot, "boot-order-missing-exitbootservices-nonclaim", diagnostics, bootOrder.Replace("`ExitBootServices` transition", "`ExitBootServices` success", StringComparison.Ordinal));
        }
        finally
        {
            if (Directory.Exists(fixtureRoot))
            {
                Directory.Delete(fixtureRoot, recursive: true);
            }
        }

        if (!hostileFixturesRejected)
        {
            return false;
        }

        string efiBuild = File.ReadAllText(Path.Combine(root, "docs", "efi-build.md"));
        string qemu = File.ReadAllText(Path.Combine(root, "docs", "qemu.md"));
        string readme = File.ReadAllText(Path.Combine(root, "README.md"));
        return efiBuild.Contains("EnableM02GuardedIstStackAllocationProbe", StringComparison.Ordinal) && efiBuild.Contains("six", StringComparison.OrdinalIgnoreCase) && efiBuild.Contains("UEFI LoaderData", StringComparison.Ordinal) && efiBuild.Contains("pre-`ExitBootServices`", StringComparison.Ordinal) && efiBuild.Contains("does not make the guard-intent pages non-present", StringComparison.Ordinal) && qemu.Contains("CLROS_M02_008_READY", StringComparison.Ordinal) && qemu.Contains("guestGuardMappings=false", StringComparison.Ordinal) && qemu.Contains("M02_GUARDED_IST_STACK_ALLOCATION_FAILED", StringComparison.Ordinal) && qemu.Contains("postExitBootServices=false", StringComparison.Ordinal) && readme.Contains("(docs/efi-build.md)", StringComparison.Ordinal) && readme.Contains("(docs/qemu.md)", StringComparison.Ordinal);
    }

    private static bool HasRequiredDocumentationBoundaries(
        string diagnostics,
        string bootOrder)
    {
        const string DiagnosticsSectionStart =
            "M02-008 adds a caller-owned guarded-IST arena allocator";
        const string DiagnosticsNextIssueStart = "M02-009 host tests";
        const string DiagnosticsSectionEnd = "## Next Steps";
        const string BootOrderSectionStart =
            "- M02-008 adds a host-tested, allocation-free guarded-IST arena allocator";
        const string BootOrderSectionEnd = "- Fault-entry frame layout tests";

        string diagnosticsBoundary = NormalizeDocumentationText(
            ExtractSectionBeforeAny(
                diagnostics,
                DiagnosticsSectionStart,
                DiagnosticsNextIssueStart,
                DiagnosticsSectionEnd));
        string bootOrderBoundary = NormalizeDocumentationText(
            ExtractSectionBeforeAny(
                bootOrder,
                BootOrderSectionStart,
                BootOrderSectionEnd));

        return diagnosticsBoundary.Contains(
                DiagnosticsSectionStart,
                StringComparison.Ordinal)
            && diagnosticsBoundary.Contains(
                "double fault, page fault, and NMI",
                StringComparison.Ordinal)
            && diagnosticsBoundary.Contains(
                "one non-present guard range followed by one writable, " +
                "non-executable downward-growing stack range for each fixed IST index",
                StringComparison.Ordinal)
            && diagnosticsBoundary.Contains(
                "temporary six-page UEFI LoaderData arena",
                StringComparison.Ordinal)
            && diagnosticsBoundary.Contains(
                "writes and reads back only the three stack ranges",
                StringComparison.Ordinal)
            && diagnosticsBoundary.Contains(
                "releases the lease, and frees the same arena before return",
                StringComparison.Ordinal)
            && diagnosticsBoundary.Contains(
                "The proof does not change page tables, map guards non-present, " +
                "write TSS IST slots, load descriptor tables, install handlers, " +
                "execute a fault, or cross real `ExitBootServices`.",
                StringComparison.Ordinal)
            && !diagnosticsBoundary.Contains(
                "The proof does change page tables",
                StringComparison.Ordinal)
            && diagnosticsBoundary.Contains(
                "map guards non-present",
                StringComparison.Ordinal)
            && diagnosticsBoundary.Contains(
                "write TSS IST slots",
                StringComparison.Ordinal)
            && diagnosticsBoundary.Contains(
                "load descriptor tables",
                StringComparison.Ordinal)
            && diagnosticsBoundary.Contains(
                "execute a fault",
                StringComparison.Ordinal)
            && diagnosticsBoundary.Contains(
                "cross real `ExitBootServices`",
                StringComparison.Ordinal)
            && bootOrderBoundary.Contains(
                BootOrderSectionStart,
                StringComparison.Ordinal)
            && bootOrderBoundary.Contains(
                "double fault, page fault, and NMI",
                StringComparison.Ordinal)
            && bootOrderBoundary.Contains(
                "three adjacent guard-plus-stack reservations",
                StringComparison.Ordinal)
            && bootOrderBoundary.Contains(
                "temporary six-page LoaderData allocation",
                StringComparison.Ordinal)
            && bootOrderBoundary.Contains(
                "stack-only write/readback",
                StringComparison.Ordinal)
            && bootOrderBoundary.Contains(
                "lease release, and same-base `FreePages` before return",
                StringComparison.Ordinal)
            && bootOrderBoundary.Contains(
                "no guard mapping, TSS/IDT installation, fault execution, or real " +
                "`ExitBootServices` transition",
                StringComparison.Ordinal);
    }

    private static bool WriteHostileFixtureAndRequireRejection(
        string fixtureRoot,
        string caseName,
        string diagnostics,
        string bootOrder)
    {
        string caseRoot = Path.Combine(fixtureRoot, caseName);
        Directory.CreateDirectory(caseRoot);
        string diagnosticsPath = Path.Combine(caseRoot, "diagnostics.md");
        string bootOrderPath = Path.Combine(caseRoot, "boot-order.md");
        File.WriteAllText(diagnosticsPath, diagnostics);
        File.WriteAllText(bootOrderPath, bootOrder);
        return !HasRequiredDocumentationBoundaries(
            File.ReadAllText(diagnosticsPath),
            File.ReadAllText(bootOrderPath));
    }

    private static string NormalizeDocumentationText(string source)
    {
        return Regex.Replace(
            source,
            @"\s+",
            " ",
            RegexOptions.CultureInvariant);
    }

    private static string ExtractSectionBeforeAny(
        string source,
        string startToken,
        params string[] endTokens)
    {
        int start = source.IndexOf(startToken, StringComparison.Ordinal);
        if (start < 0)
        {
            return string.Empty;
        }

        int end = -1;
        foreach (string endToken in endTokens)
        {
            int candidate = source.IndexOf(
                endToken,
                start + startToken.Length,
                StringComparison.Ordinal);
            if (candidate >= 0 && (end < 0 || candidate < end))
            {
                end = candidate;
            }
        }

        return end > start ? source[start..end] : string.Empty;
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
