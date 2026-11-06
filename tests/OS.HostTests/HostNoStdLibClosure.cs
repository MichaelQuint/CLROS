using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Tooling;

/// <summary>Discovers kernel inputs for host no-stdlib compiler fixtures. The
/// fixture still supplies its shim and validates the emitted metadata. Dependency
/// discovery cannot admit boot orchestration or probe entry points into a pure
/// planning fixture, even when those types move into a different source file.</summary>
internal static class HostNoStdLibClosure
{
    internal static string[] Kernel(string repositoryRoot, params string[] roots)
    {
        var catalog = HostSourceCatalog.Project("src/OS.Kernel/OS.Kernel.csproj");
        var sources = catalog.DependencySources(roots).Where(source => !source.IsGenerated).ToArray();
        string kernelRoot = Path.GetFullPath(Path.Combine(repositoryRoot, "src", "OS.Kernel"));
        foreach (var source in sources)
        {
            _ = EfiEvidence.RelativePath(kernelRoot, source.Path, "nostdlib_dependency_outside_kernel");
            AuthorityFiles.RequirePlainPath(repositoryRoot, source.Path, "nostdlib_dependency_link");
            RequirePlanningTypes(catalog, source);
        }
        if (sources.Length == 0)
            throw new InvalidOperationException("nostdlib_dependency_empty");
        return sources.Select(source => source.Path).ToArray();
    }

    internal static void RequirePlanningTypes(CSharpSourceCatalog catalog, CSharpSourceDocument source)
    {
        var model = catalog.Compilation.GetSemanticModel(source.Syntax);
        foreach (var declaration in source.Syntax.GetRoot().DescendantNodes()
            .Where(node => node is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax))
        {
            var type = (INamedTypeSymbol)model.GetDeclaredSymbol(declaration)!;
            if (type.ContainingNamespace.ToDisplayString() is not ("OS.Boot" or "OS.Kernel.Boot" or "OS.Kernel.Memory")
                || type.Name is "BootSequence" or "EfiEntry" or "Probes" or "NullPageFaultProbePlan")
                throw new InvalidOperationException("nostdlib_dependency_layer " + type.ToDisplayString());
        }
    }
}
