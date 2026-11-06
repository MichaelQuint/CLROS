using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace OS.Tooling;

/// <summary>Resolves source declarations by compiler identity in one selected
/// project/profile. Partial declarations are kept together and overload ambiguity
/// fails explicitly. This host snapshot supplies syntax and semantic evidence;
/// callers still define independent behavioral and dependency expectations.</summary>
public sealed class CSharpSourceCatalog
{
    private readonly IReadOnlyDictionary<SyntaxTree, CSharpSourceDocument> documents;
    private CSharpSourceDependencies? dependencies;
    internal CSharpSourceDependencies Dependencies => dependencies ??= new(this);

    public CSharpCompilation Compilation
    {
        get;
    }
    public IReadOnlyList<CSharpSourceDocument> Documents
    {
        get;
    }

    public CSharpSourceCatalog(IEnumerable<CSharpSourceDocument> sources,
        IEnumerable<MetadataReference> references, string assemblyName = "SourceInspection",
        CSharpCompilationOptions? compilationOptions = null)
    {
        var items = sources.ToArray();
        if (items.Select(item => item.Path).Distinct(OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal).Count() != items.Length)
            throw new InvalidOperationException("source_catalog_duplicate_document");
        foreach (var item in items)
        {
            var errors = item.Syntax.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray();
            if (errors.Length != 0)
                throw new InvalidOperationException("source_catalog_invalid_syntax " + item.Path + " " + string.Join("; ", errors.Select(error => error.ToString())));
        }
        Documents = Array.AsReadOnly(items);
        documents = items.ToDictionary(item => item.Syntax);
        Compilation = CSharpCompilation.Create(assemblyName, items.Select(item => item.Syntax), references,
            compilationOptions ?? new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        var declarationErrors = Compilation.GetDeclarationDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray();
        if (declarationErrors.Length != 0)
            throw new InvalidOperationException("source_catalog_invalid_declarations "
                + string.Join("; ", declarationErrors.Select(error => error.ToString())));
    }

    public INamedTypeSymbol Type(string metadataName)
    {
        var symbol = Compilation.GetTypeByMetadataName(metadataName);
        if (symbol is null || symbol.DeclaringSyntaxReferences.Length == 0)
            throw new InvalidOperationException("source_catalog_type_missing_or_ambiguous " + metadataName);
        if (symbol.DeclaringSyntaxReferences.Length > 1
            && symbol.DeclaringSyntaxReferences.Any(reference => reference.GetSyntax() is not TypeDeclarationSyntax declaration
                || !declaration.Modifiers.Any(SyntaxKind.PartialKeyword)))
            throw new InvalidOperationException("source_catalog_duplicate_type " + metadataName);
        return symbol;
    }

    public IReadOnlyList<CSharpSourceDocument> TypeSources(string metadataName, bool includeNested = true)
    {
        HashSet<SyntaxTree> trees = [];
        Visit(Type(metadataName));
        // Preserve the evaluated compiler input order, including initializer order.
        return Documents.Where(document => trees.Contains(document.Syntax)).ToArray();

        void Visit(INamedTypeSymbol type)
        {
            foreach (var declaration in type.DeclaringSyntaxReferences)
                trees.Add(declaration.SyntaxTree);
            if (includeNested)
                foreach (var child in type.GetTypeMembers())
                    Visit(child);
        }
    }

    public MethodDeclarationSyntax Method(string metadataName, string name,
        Func<IMethodSymbol, bool>? signature = null)
    {
        var methods = Type(metadataName).GetMembers(name).OfType<IMethodSymbol>()
            .Where(method => signature is null || signature(method)).ToArray();
        if (methods.Length != 1)
            throw new InvalidOperationException("source_catalog_method_missing_or_ambiguous " + metadataName + "." + name);
        var selected = methods[0].PartialImplementationPart ?? methods[0];
        var declarations = selected.DeclaringSyntaxReferences.Select(reference => reference.GetSyntax())
            .OfType<MethodDeclarationSyntax>().ToArray();
        if (declarations.Length != 1)
            throw new InvalidOperationException("source_catalog_method_declaration " + metadataName + "." + name);
        return declarations[0];
    }

    public CSharpSourceDocument Document(SyntaxNode node)
        => documents.TryGetValue(node.SyntaxTree, out var document) ? document
            : throw new ArgumentException("source_catalog_foreign_syntax", nameof(node));

    /// <summary>Finds complete source dependencies of selected types within this
    /// evaluated project. Referenced assemblies remain references, not copied
    /// source. Global imports and assembly/module attributes participate in the
    /// closure. Selected files are rebound against the reduced compilation with
    /// the original options and references. This validates source dependencies,
    /// not an executable entrypoint or qualification. Callers must independently
    /// constrain allowed layers and supply intentional fixture/shim sources.</summary>
    public IReadOnlyList<CSharpSourceDocument> DependencySources(params string[] metadataNames)
    {
        var result = Dependencies.Closure(metadataNames.Select(Type));
        var subset = Compilation.RemoveAllSyntaxTrees().AddSyntaxTrees(result.Select(document => document.Syntax));
        // A source slice can omit the entrypoint, but must retain every binding
        // under the original options and references.
        var errors = subset.GetDeclarationDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error && diagnostic.Location.IsInSource)
            .Concat(result.SelectMany(document => subset.GetSemanticModel(document.Syntax).GetDiagnostics())
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).Distinct().ToArray();
        if (errors.Length != 0)
            throw new InvalidOperationException("source_catalog_dependency_incomplete "
                + string.Join("; ", errors.Select(error => error.ToString())));
        return result;
    }

    /// <summary>Returns bound ordinary and delegate calls in a method. Function
    /// pointer calls and nameof expressions have different compiler operations
    /// and are not method symbols. Invalid body bindings fail before filtering.</summary>
    public IReadOnlyList<IInvocationOperation> MethodInvocations(MethodDeclarationSyntax method)
    {
        _ = Document(method);
        var model = Compilation.GetSemanticModel(method.SyntaxTree);
        var errors = model.GetDiagnostics(method.Span)
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray();
        if (errors.Length != 0)
            throw new InvalidOperationException("source_catalog_method_binding " + method.Identifier.ValueText + " "
                + string.Join("; ", errors.Select(error => error.ToString())));
        return method.DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Select(invocation => model.GetOperation(invocation)).OfType<IInvocationOperation>().ToArray();
    }

    public IMethodSymbol Invocation(InvocationExpressionSyntax invocation)
    {
        _ = Document(invocation);
        var model = Compilation.GetSemanticModel(invocation.SyntaxTree);
        if (model.GetDiagnostics(invocation.Span).Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
            throw new InvalidOperationException("source_catalog_invalid_invocation " + invocation);
        var symbol = model.GetSymbolInfo(invocation).Symbol;
        return symbol as IMethodSymbol
            ?? throw new InvalidOperationException("source_catalog_unresolved_invocation " + invocation);
    }
}
