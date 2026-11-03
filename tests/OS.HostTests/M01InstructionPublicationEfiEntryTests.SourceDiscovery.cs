using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Tooling;

internal static partial class M01InstructionPublicationEfiEntryTests
{
    private const string InspectionOwner = "OS.Boot.EfiEntry";

    private static string SelectActualProfile(bool stageOneEnabled, bool stageTwoEnabled, bool stageThreeEnabled,
        bool terminalEnabled, bool idtrContinuedExecutionEnabled = false, bool ud2VectorStateEnabled = false,
        bool pageFaultCr2ErrorCodeEnabled = false, bool rhpNewFastEnabled = false, bool rhpBeforeBindEnabled = false,
        bool rhpExhaustionEnabled = false, bool doubleFaultIstEnabled = false, bool arrayEnabled = false,
        bool arrayBeforeBindEnabled = false, bool arrayExhaustionEnabled = false, bool unexpectedVectorPanicEnabled = false,
        bool stringEnabled = false, bool stringBeforeBindEnabled = false, bool stringExhaustionEnabled = false,
        bool stringInvalidLengthEnabled = false, bool firstExitEnabled = false)
    {
        Dictionary<string, bool> selected = new(StringComparer.Ordinal)
        {
            ["EnableM01LoadedImageStackProbe"] = stageOneEnabled,
            ["EnableM01LiveAllocatorProbe"] = stageTwoEnabled,
            ["EnableM01OwnedPagingProbe"] = stageThreeEnabled,
            ["EnableM02OwnedGdtTssTerminalProbe"] = terminalEnabled,
            ["EnableM02IdtrContinuedExecutionProbe"] = idtrContinuedExecutionEnabled,
            ["EnableM02Ud2VectorStateProbe"] = ud2VectorStateEnabled,
            ["EnableM02PageFaultCr2ErrorCodeProbe"] = pageFaultCr2ErrorCodeEnabled,
            ["EnableM04RhpNewFastProbe"] = rhpNewFastEnabled,
            ["EnableM04RhpNewFastBeforeBindFailureProbe"] = rhpBeforeBindEnabled,
            ["EnableM04RhpNewFastExhaustionFailureProbe"] = rhpExhaustionEnabled,
            ["EnableM02DoubleFaultIstProbe"] = doubleFaultIstEnabled,
            ["EnableM04SzArrayAllocationProbe"] = arrayEnabled,
            ["EnableM04SzArrayAllocationBeforeBindProbe"] = arrayBeforeBindEnabled,
            ["EnableM04SzArrayAllocationExhaustionProbe"] = arrayExhaustionEnabled,
            ["EnableM02UnexpectedVectorPanicProbe"] = unexpectedVectorPanicEnabled,
            ["EnableM04StringAllocationProbe"] = stringEnabled,
            ["EnableM04StringAllocationBeforeBindProbe"] = stringBeforeBindEnabled,
            ["EnableM04StringAllocationExhaustionProbe"] = stringExhaustionEnabled,
            ["EnableM04StringAllocationInvalidLengthProbe"] = stringInvalidLengthEnabled,
            ["EnableM03FirstRealExitBootServicesProbe"] = firstExitEnabled
        };
        var source = HostSourceCatalog.Efi(HostEfiInspectionProfiles.Create(HostToolProcess.FindRepositoryRoot(), selected));
        var entry = source.Type(InspectionOwner);
        if (entry is not { TypeKind: TypeKind.Class, IsStatic: true, Arity: 0, DeclaredAccessibility: Accessibility.Public })
            throw new InvalidOperationException("instruction_source_entry_owner");
        foreach (string name in new[]
        {
            "M01StageOneAdapter", "M01StageTwoAdapter", "M01StageThreeAdapter", "M02PageFaultAdapter",
            "M02DoubleFaultIstAdapter", "M02UnexpectedVectorPanicAdapter", "M03FirstExitAdapter"
        })
        {
            var owner = source.Compilation.GetTypeByMetadataName(InspectionOwner + "+" + name);
            if (owner is not null && owner is not
                { TypeKind: TypeKind.Class, IsStatic: true, Arity: 0, DeclaredAccessibility: Accessibility.Private })
                throw new InvalidOperationException("instruction_source_adapter_owner " + name);
        }
        return HostEfiLogicalSource.View(source, InspectionOwner);
    }

    private static IReadOnlyList<CSharpSourceDocument> InstructionRawDocuments() => HostSourceText.DocumentSources(
        HostSourceCatalog.Efi(EfiProfile.Create("Diagnostic", new Dictionary<string, bool>())), InspectionOwner);

    private static IEnumerable<string> InstructionMarkerDocuments(string testsDirectory)
    {
        var host = HostSourceCatalog.Project("tests/OS.HostTests/OS.HostTests.csproj");
        string ownerName = typeof(M01InstructionPublicationEfiEntryTests).FullName!;
        var owner = host.Type(ownerName);
        var marker = owner.GetMembers(nameof(ReadyMarker)).OfType<IFieldSymbol>().Single();
        if (!marker.IsConst || marker.Type.SpecialType != SpecialType.System_String || !Equals(marker.ConstantValue, ReadyMarker))
            throw new InvalidOperationException("instruction_source_ready_marker");
        // Retain every original top-level raw test file, and also the actual
        // fixture family if it moves to a subdirectory or linked source location.
        return Directory.EnumerateFiles(testsDirectory, "*.cs", SearchOption.TopDirectoryOnly)
            .Concat(HostSourceText.DocumentSources(host, ownerName).Select(document => document.Path))
            .Select(Path.GetFullPath).Distinct(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
    }

    private static string InsertShadowBeforeStageOne(string source)
    {
        var root = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.CSharp14)).GetCompilationUnitRoot();
        var owner = root.Members.OfType<ClassDeclarationSyntax>().Single(type => type.Identifier.ValueText == "EfiEntry");
        var target = owner.Members.OfType<ClassDeclarationSyntax>().Single(type => type.Identifier.ValueText == "M01StageOneAdapter");
        return source[..target.SpanStart] + "private static class X64InstructionPublication {}\n" + source[target.SpanStart..];
    }
}
