using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Tooling;

/// <summary>Joins a compiler-bound leaf closure to the original EFI byte
/// snapshots and compiler order. Callers retain independent layer boundaries;
/// discovery cannot introduce another namespace or source tree.</summary>
internal static class HostEfiLeafSources
{
    internal static CSharpSourceDocument[] Select(CSharpSourceCatalog source, CSharpSourceCatalog efi,
        string sourceRoot, string expectedNamespace, string rootType)
    {
        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var selected = new HashSet<string>(comparer);
        foreach (var document in source.DependencySources(rootType).Where(document => !document.IsGenerated))
        {
            _ = EfiEvidence.RelativePath(sourceRoot, document.Path, "efi_leaf_source_outside_layer");
            var model = source.Compilation.GetSemanticModel(document.Syntax);
            foreach (var declaration in document.Syntax.GetRoot().DescendantNodes()
                .Where(node => node is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax))
                if (model.GetDeclaredSymbol(declaration) is not INamedTypeSymbol type
                    || type.ContainingNamespace.ToDisplayString() != expectedNamespace)
                    throw new InvalidOperationException("efi_leaf_source_namespace " + document.Path);
            var admitted = efi.Documents.SingleOrDefault(candidate => comparer.Equals(candidate.Path, document.Path));
            if (admitted is null || admitted.Sha256 != document.Sha256)
                throw new InvalidOperationException("efi_leaf_source_byte_join " + document.Path);
            // Raw bytes can match while preprocessor symbols select another
            // body. This host projection accepts only the same active leaf.
            if (!document.Syntax.GetRoot().DescendantTokens().Select(token => (token.RawKind, token.Text))
                .SequenceEqual(admitted.Syntax.GetRoot().DescendantTokens().Select(token => (token.RawKind, token.Text))))
                throw new InvalidOperationException("efi_leaf_source_profile_mismatch " + document.Path);
            selected.Add(admitted.Path);
        }
        if (selected.Count == 0)
            throw new InvalidOperationException("efi_leaf_source_empty");
        return efi.Documents.Where(document => selected.Contains(document.Path)).ToArray();
    }
}
