using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using OS.Tooling;

internal static class HostNoStdLibClosureTests
{
    [HostTest("no-stdlib planning boundary follows declarations across moved files and partial helpers")]
    private static void DeclarationBoundaries()
    {
        var references = new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) };
        var good = new CSharpSourceDocument("moved.cs", "synthetic",
            CSharpSyntaxTree.ParseText("namespace OS.Boot; partial class Planner { public struct State {} }"));
        var catalog = new CSharpSourceCatalog([good], references);
        HostNoStdLibClosure.RequirePlanningTypes(catalog, good);
        foreach (string source in new[]
        {
            "namespace OS.Boot; class BootSequence {}",
            "namespace OS.Kernel.Boot; class BootSequence {}",
            "namespace OS.Boot; partial class EfiEntry { public struct Helper {} }",
            "namespace OS.Boot; class Probes {}",
            "namespace OS.Kernel.Memory; class NullPageFaultProbePlan {}",
            "namespace OS.Networking; class Planner {}",
            "namespace OS.Networking; delegate void Planner();"
        })
        {
            var invalid = new CSharpSourceDocument("harmless-name.cs", "synthetic", CSharpSyntaxTree.ParseText(source));
            var badCatalog = new CSharpSourceCatalog([invalid], references);
            try
            {
                HostNoStdLibClosure.RequirePlanningTypes(badCatalog, invalid);
            }
            catch (InvalidOperationException error) when (error.Message.StartsWith("nostdlib_dependency_layer", StringComparison.Ordinal))
            {
                continue;
            }
            throw new InvalidOperationException("unexpected dependency admission " + source);
        }
    }
}
