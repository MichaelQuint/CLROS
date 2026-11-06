using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using OS.Tooling;

internal static class HostEfiLeafSourcesTests
{
    [HostTest("EFI leaf closure follows split renamed helpers and preserves original EFI compiler order")]
    private static void SplitSources()
    {
        string root = Path.Combine(Path.GetTempPath(), "clros-efi-leaf-" + Guid.NewGuid().ToString("N"));
        var entry = Document(Path.Combine(root, "relocated.cs"),
            "namespace N; class Entry { public static int Read() => Helper.Value; }");
        var helper = Document(Path.Combine(root, "unrelated-name.cs"),
            "namespace N; class Helper { public const int Value = 1; }");
        var unused = Document(Path.Combine(root, "unused.cs"), "namespace N; class Unused {}");
        var source = Catalog(entry, helper, unused);
        var efi = Catalog(helper, unused, entry);
        var selected = HostEfiLeafSources.Select(source, efi, root, "N", "N.Entry");
        HostCheck.True(selected.Select(document => document.Path).SequenceEqual(
            new[] { helper.Path, entry.Path }, StringComparer.Ordinal));
    }

    [HostTest("EFI leaf closure rejects missing changed foreign and escaping dependencies")]
    private static void RejectedSources()
    {
        string root = Path.Combine(Path.GetTempPath(), "clros-efi-leaf-" + Guid.NewGuid().ToString("N"));
        var entry = Document(Path.Combine(root, "entry.cs"),
            "namespace N; class Entry { public static int Read() => Helper.Value; }");
        var helper = Document(Path.Combine(root, "helper.cs"),
            "namespace N; class Helper { public const int Value = 1; }");
        var source = Catalog(entry, helper);
        Reject(() => HostEfiLeafSources.Select(source, Catalog(entry), root, "N", "N.Entry"),
            "efi_leaf_source_byte_join");
        var changed = Document(helper.Path, "namespace N; class Helper { public const int Value = 2; }");
        Reject(() => HostEfiLeafSources.Select(source, Catalog(entry, changed), root, "N", "N.Entry"),
            "efi_leaf_source_byte_join");

        const string conditional = "namespace N; class Entry { public static int Read() {\n#if EFI_ONLY\nreturn 2;\n#else\nreturn 1;\n#endif\n} }";
        var ordinary = Document(entry.Path, conditional);
        var selected = CSharpSourceDocument.FromBytes(entry.Path, Encoding.UTF8.GetBytes(conditional),
            new CSharpParseOptions(LanguageVersion.CSharp14, preprocessorSymbols: ["EFI_ONLY"]));
        Reject(() => HostEfiLeafSources.Select(Catalog(ordinary), Catalog(selected), root, "N", "N.Entry"),
            "efi_leaf_source_profile_mismatch");

        var foreignEntry = Document(entry.Path,
            "namespace N; class Entry { public static int Read() => Foreign.Helper.Value; }");
        var foreignHelper = Document(helper.Path, "namespace Foreign; class Helper { public const int Value = 1; }");
        var foreign = Catalog(foreignEntry, foreignHelper);
        Reject(() => HostEfiLeafSources.Select(foreign, foreign, root, "N", "N.Entry"),
            "efi_leaf_source_namespace");

        var escaped = Document(Path.Combine(root + "-outside", "helper.cs"), helper.Text);
        var escaping = Catalog(entry, escaped);
        Reject(() => HostEfiLeafSources.Select(escaping, escaping, root, "N", "N.Entry"),
            "efi_leaf_source_outside_layer");
    }

    private static CSharpSourceDocument Document(string path, string text) =>
        CSharpSourceDocument.FromBytes(path, Encoding.UTF8.GetBytes(text), new CSharpParseOptions(LanguageVersion.CSharp14));

    private static CSharpSourceCatalog Catalog(params CSharpSourceDocument[] sources) =>
        new(sources, [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);

    private static void Reject(Action action, string reason)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException failure) when (failure.Message.StartsWith(reason, StringComparison.Ordinal))
        {
            return;
        }
        catch (ToolFailure failure) when (failure.Reason == reason)
        {
            return;
        }
        throw new InvalidOperationException("expected source rejection " + reason);
    }
}
