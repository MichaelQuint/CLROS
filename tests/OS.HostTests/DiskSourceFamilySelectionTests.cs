using Microsoft.CodeAnalysis.CSharp;
using OS.Tooling;

internal static class DiskSourceFamilySelectionTests
{
    [HostTest("disk source family discovery requires compiled pinned helpers and semantic anchors")]
    private static void FamilyControls()
    {
        string directory = Path.Combine(Path.GetTempPath(), "clros-source-family-model");
        const string reason = "reviewed_storage_family";
        const string first = "namespace StorageParts; static partial class Owner { private static int Read() => 1; }";
        const string second = "namespace StorageParts; static partial class Owner { private struct Buffer { public int Value; } }";
        const string unrelated = "namespace StorageParts; static class Other { private static int Read() => 2; }";
        var options = new CSharpParseOptions(LanguageVersion.CSharp14);
        CSharpSourceDocument Document(string relative, string text)
        {
            string path = Path.Combine(directory, relative);
            return new(path, ContractAudit.HashSource(text), CSharpSyntaxTree.ParseText(text, options, path));
        }
        var firstDocument = Document("renamed.cs", first);
        var secondDocument = Document(Path.Combine("partials", "relocated.cs"), second);
        var otherDocument = Document("unrelated.cs", unrelated);
        var references = HostSourceCatalog.Project("src/OS.Kernel/OS.Kernel.csproj").Compilation.References;
        var catalog = new CSharpSourceCatalog([secondDocument, otherDocument, firstDocument], references);
        var reasons = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["renamed.cs"] = reason,
            ["partials/relocated.cs"] = reason
        };
        var pins = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["renamed.cs"] = ContractAudit.HashSource(first),
            ["partials/relocated.cs"] = ContractAudit.HashSource(second)
        };
        Dictionary<string, IReadOnlyList<CSharpSourceDocument>> Anchors(CSharpSourceCatalog source) =>
            new(StringComparer.Ordinal)
            {
                [reason] = source.TypeSources("StorageParts.Owner")
            };
        var selected = DiskSourceFamilySelection.Select(catalog, directory, reasons, pins, Anchors(catalog));
        HostCheck.True(selected.Values.ToHashSet().SetEquals(catalog.TypeSources("StorageParts.Owner")),
            "both moved owner and nested helper are selected without the unrelated source");

        void Reject(CSharpSourceCatalog source, IReadOnlyDictionary<string, string> selectedReasons,
            IReadOnlyDictionary<string, string> selectedPins, IReadOnlyDictionary<string, IReadOnlyList<CSharpSourceDocument>> anchors)
        {
            try
            {
                _ = DiskSourceFamilySelection.Select(source, directory, selectedReasons, selectedPins, anchors);
            }
            catch (InvalidOperationException error) when (error.Message.StartsWith("disk_source_family_", StringComparison.Ordinal)) { return; }
            throw new InvalidOperationException("unreviewed disk source family accepted");
        }
        var missingPin = new Dictionary<string, string>(pins, StringComparer.Ordinal);
        missingPin.Remove("partials/relocated.cs");
        Reject(catalog, reasons, missingPin, Anchors(catalog));
        var missingHelper = new Dictionary<string, string>(reasons, StringComparer.Ordinal);
        missingHelper.Remove("partials/relocated.cs");
        Reject(catalog, missingHelper, pins, Anchors(catalog));
        Reject(catalog, reasons, new Dictionary<string, string>(pins, StringComparer.Ordinal) { ["partials/relocated.cs"] = "changed" }, Anchors(catalog));
        var withoutHelper = new CSharpSourceCatalog([firstDocument, otherDocument], references);
        Reject(withoutHelper, reasons, pins, Anchors(withoutHelper));
        Reject(catalog, reasons, pins, new Dictionary<string, IReadOnlyList<CSharpSourceDocument>>
        {
            [reason] = catalog.TypeSources("StorageParts.Other")
        });
        Reject(catalog, reasons, pins, new Dictionary<string, IReadOnlyList<CSharpSourceDocument>>());
        Reject(catalog, reasons, pins, new Dictionary<string, IReadOnlyList<CSharpSourceDocument>> { [reason] = [] });
        Reject(catalog, new Dictionary<string, string>(reasons, StringComparer.Ordinal) { ["./renamed.cs"] = reason },
            new Dictionary<string, string>(pins, StringComparer.Ordinal) { ["./renamed.cs"] = pins["renamed.cs"] }, Anchors(catalog));
    }
}
