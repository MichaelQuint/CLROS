using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Tooling;

/// <summary>Reads the runtime export ABI from bound attributes in one evaluated
/// compilation. Names and owners remain independent of declaration filenames.
/// This is source inventory, not evidence of emitted symbols or guest execution.</summary>
internal static class HostRuntimeExports
{
    internal static IReadOnlyDictionary<string, IMethodSymbol> Read(CSharpSourceCatalog catalog)
    {
        var exportAttribute = catalog.Type("System.Runtime.RuntimeExportAttribute");
        Dictionary<string, IMethodSymbol> exports = new(StringComparer.Ordinal);
        foreach (var document in catalog.Documents)
        {
            var model = catalog.Compilation.GetSemanticModel(document.Syntax);
            foreach (var declaration in document.Syntax.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>())
            {
                var method = (IMethodSymbol)model.GetDeclaredSymbol(declaration)!;
                foreach (var attribute in method.GetAttributes().Where(attribute =>
                    SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, exportAttribute)))
                {
                    if (attribute.ConstructorArguments is not [{ Value: string name }]
                        || string.IsNullOrWhiteSpace(name) || !method.IsStatic)
                        throw new InvalidOperationException("runtime_export_invalid " + method.ToDisplayString());
                    if (!exports.TryAdd(name, method))
                        throw new InvalidOperationException("runtime_export_duplicate " + name);
                }
            }
        }
        return exports;
    }
}
