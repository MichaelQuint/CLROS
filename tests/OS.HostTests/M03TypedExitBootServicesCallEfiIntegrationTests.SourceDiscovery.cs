#if !TYPED_EBS_CAPTURE_HOST
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using OS.Tooling;

internal static partial class M03TypedExitBootServicesCallEfiIntegrationTests
{
    private static void CheckTypedHostAdmission(CSharpSourceCatalog source, string root)
    {
        const string reason = "reviewed_typed_ebs_adapter_source_identity";
        const string owner = "OS.Boot.TypedNoStdlibExitBootServicesCallAdapter";
        string Relative(CSharpSourceDocument document) =>
            Path.GetRelativePath(Path.Combine(root, "src", "OS"), document.Path).Replace('/', '\\');
        var compiled = source.Documents.ToDictionary(Relative, StringComparer.Ordinal);
        var family = EfiContracts.SourceAdmissionReasons.Where(pair => pair.Value == reason).ToArray();
        Need(family.Length != 0, "reviewed typed host family exists");
        foreach (var member in family)
        {
            Need(compiled.TryGetValue(member.Key, out var document), "reviewed typed host member is compiled");
            Need(ReviewedTypedMember(document!, member.Key), "exact reviewed host member bytes");
        }
        Need(source.TypeSources(owner).All(document => family.Any(member => member.Key == Relative(document))),
            "semantic typed adapter belongs to the reviewed family");
    }

    private static bool ReviewedTypedMember(CSharpSourceDocument document, string include) =>
        EfiContracts.SourceIdentities.TryGetValue(include, out string? pin)
        && string.Equals(document.Sha256, pin, StringComparison.OrdinalIgnoreCase)
        && EfiSourceAdmission.Inspect(document.Text, include).Succeeded;

    [HostTest("m03.004.efi typed host source preserves raw byte identity")]
    private static void TypedHostByteControls()
    {
        string root = Root();
        var source = HostSourceCatalog.Efi(EfiOptions.Parse(["--" + Flag], true).Profile());
        foreach (var document in source.TypeSources("OS.Boot.TypedNoStdlibExitBootServicesCallAdapter"))
        {
            string include = Path.GetRelativePath(Path.Combine(root, "src", "OS"), document.Path).Replace('/', '\\');
            Need(ReviewedTypedMember(document, include), "actual reviewed raw source");
            var parse = (CSharpParseOptions)document.Syntax.Options;
            byte[] utf8 = Encoding.UTF8.GetBytes(document.Text);
            byte[][] altered =
            [
                [.. Encoding.UTF8.GetPreamble(), .. utf8],
                [.. Encoding.Unicode.GetPreamble(), .. Encoding.Unicode.GetBytes(document.Text)],
                Encoding.UTF8.GetBytes(document.Text.ReplaceLineEndings("\r\n"))
            ];
            foreach (byte[] bytes in altered)
            {
                var changed = CSharpSourceDocument.FromBytes(document.Path, bytes, parse);
                Need(changed.Sha256 != document.Sha256, "byte control changes original input");
                Need(changed.Text.ReplaceLineEndings("\n") == document.Text.ReplaceLineEndings("\n"),
                    "byte control preserves decoded code");
                Need(!ReviewedTypedMember(changed, include), "decoded identity cannot replace raw identity");
            }
        }
    }

    private static bool TypedCallbackSignature(IMethodSymbol method) =>
        method.IsStatic && method.Arity == 0 && !method.ReturnsByRef && !method.ReturnsByRefReadonly
        && method.DeclaredAccessibility == Accessibility.Private
        && method.ReturnType.SpecialType == SpecialType.System_UInt64;

    [HostTest("m03.004.efi typed callback declaration contract")]
    private static void TypedCallbackControls()
    {
        const string text = """
            internal class Callbacks
            {
                private static ulong value;
                private static ulong Original() => value;
                private static ulong Generic<T>() => value;
                private static ref ulong Reference() => ref value;
                private static ref readonly ulong ReadOnlyReference() => ref value;
                private ulong Instance() => value;
                public static ulong Public() => value;
                private static long Signed() => 0;
            }
            """;
        var source = new CSharpSourceCatalog(
            [CSharpSourceDocument.FromBytes("arbitrary-callback-location.cs", Encoding.UTF8.GetBytes(text),
                new CSharpParseOptions(LanguageVersion.CSharp14))],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
        Need(!source.Compilation.GetDiagnostics().Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error),
            "all callback controls compile");
        foreach (string name in new[] { "Original", "Generic", "Reference", "ReadOnlyReference", "Instance", "Public", "Signed" })
        {
            var method = source.Type("Callbacks").GetMembers(name).OfType<IMethodSymbol>().Single();
            Need(TypedCallbackSignature(method) == (name == "Original"), name);
        }
    }
}
#endif
