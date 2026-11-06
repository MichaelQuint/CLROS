using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Tooling;

/// <summary>Finds active canonical System declarations by bound metadata identity,
/// including generic arity and nested types. A caller supplies the source boundary;
/// this inventory does not decide which declarations are supported.</summary>
internal static class HostCanonicalTypeInventory
{
    internal static IReadOnlyDictionary<string, INamedTypeSymbol> Read(CSharpSourceCatalog catalog,
        IEnumerable<CSharpSourceDocument> documents)
    {
        Dictionary<string, INamedTypeSymbol> result = new(StringComparer.Ordinal);
        foreach (var document in documents)
        {
            var model = catalog.Compilation.GetSemanticModel(document.Syntax);
            foreach (var declaration in document.Syntax.GetRoot().DescendantNodes()
                .Where(node => node is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax))
            {
                var type = (INamedTypeSymbol)model.GetDeclaredSymbol(declaration)!;
                string space = type.ContainingNamespace.ToDisplayString();
                if (space != "System" && !space.StartsWith("System.", StringComparison.Ordinal))
                    continue;
                string name = MetadataName(type);
                if (result.TryGetValue(name, out var previous)
                    && !SymbolEqualityComparer.Default.Equals(previous, type))
                    throw new InvalidOperationException("canonical_type_ambiguous " + name);
                result[name] = type;
            }
        }
        return result;
    }

    private static string MetadataName(INamedTypeSymbol type)
        => type.ContainingType is { } parent ? MetadataName(parent) + "+" + type.MetadataName
            : type.ContainingNamespace.ToDisplayString() + "." + type.MetadataName;
}
