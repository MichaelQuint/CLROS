using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

internal static partial class EfiProfileTestControls
{
    /// <summary>Keeps independent syntax trees and their source coordinates while
    /// applying one conditional profile. File-scoped namespaces stay separate.</summary>
    private sealed class SourceForest(IReadOnlyList<SyntaxTree> trees)
    {
        internal IReadOnlyList<SyntaxNode> Roots { get; } = trees.Select(tree => tree.GetRoot()).ToArray();

        internal IEnumerable<SyntaxNode> DescendantNodes() => Roots.SelectMany(root => root.DescendantNodes());

        internal IEnumerable<Diagnostic> GetDiagnostics() => trees.SelectMany(tree => tree.GetDiagnostics());

        internal static SourceForest Parse(IReadOnlyDictionary<string, string> sources, CSharpParseOptions options) =>
            new(sources.Select(source => CSharpSyntaxTree.ParseText(source.Value, options, source.Key)).ToArray());
    }
}
