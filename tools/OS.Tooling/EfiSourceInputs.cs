using System.Text.Json;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace OS.Tooling;

/// <summary>Read the admitted, isolated EFI compile selection and active source
/// declarations for explicit probe selectors. Query-only MSBuild and source
/// analysis emit no code and run no publisher, guest or source initializer. Raw-byte
/// source hashes and symbols describe inputs only, not emitted ABI/stack or
/// acceptance. Unknown/duplicate selectors and altered project shape reject.</summary>
public static class EfiSourceInputs
{
    public static async Task RunAsync(string root, string[] selectors, TextWriter output, CancellationToken cancellation = default)
    {
        Dictionary<string, bool> selected = new(StringComparer.Ordinal);
        foreach (string selector in selectors)
            if (!EfiContracts.DiagnosticProbes.Contains(selector, StringComparer.Ordinal) || !selected.TryAdd(selector, true))
                throw new ToolFailure("efi_source_selector", selector);
        var profile = EfiProfile.Create(null, selected);
        var evaluation = await CSharpEfiProjectEvaluation.LoadAsync(root, profile, cancellation: cancellation);
        var documents = evaluation.Sources.Documents;
        var value = new
        {
            Schema = "clros-efi-source-inputs-v1", profile.BootPolicy, profile.Flags,
            Symbols = ((CSharpParseOptions)documents[0].Syntax.Options).PreprocessorSymbolNames.Order(StringComparer.Ordinal).ToArray(),
            Sources = documents.Select(document => new
            {
                Path = Path.GetRelativePath(root, document.Path).Replace('\\', '/'), document.Sha256,
                ActiveMembers = document.Syntax.GetRoot().DescendantNodes().Count(node => node is MemberDeclarationSyntax &&
                    node is not BaseNamespaceDeclarationSyntax && node is not TypeDeclarationSyntax)
            }).ToArray(),
            HashKind = "SHA256-original-bytes", SourceOnly = true, NativePublication = false, GuestExecuted = false
        };
        await output.WriteLineAsync(JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
    }
}
