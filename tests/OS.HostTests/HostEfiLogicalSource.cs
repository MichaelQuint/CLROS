using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Tooling;

/// <summary>Inspection text for a compiler-owned type and its nested types.
/// Combines partial members in compiler input order while keeping each member's
/// original text and comments. This is never emitted or supplied to an SDK.
/// Raw source policies must continue inspecting complete original documents.</summary>
internal static class HostEfiLogicalSource
{
    internal static string View(CSharpSourceCatalog source, string owner)
    {
        var order = source.Documents.Select((document, index) => (document.Syntax, index))
            .ToDictionary(item => item.Syntax, item => item.index);
        return Type(source.Type(owner));

        string Type(INamedTypeSymbol symbol)
        {
            var declarations = symbol.DeclaringSyntaxReferences.Select(reference => reference.GetSyntax())
                .OfType<TypeDeclarationSyntax>().OrderBy(node => order[node.SyntaxTree]).ThenBy(node => node.SpanStart).ToArray();
            if (declarations.Length == 0)
                throw new InvalidOperationException("inspection_type_declaration " + symbol);
            var first = declarations[0];
            // This bounded view does not reconstruct a merged type signature.
            // Reject header shapes that a first actual header cannot represent.
            // Simple containing wrappers retain the original modifiers; attributes
            // are accumulated below because all parts contribute them to one type.
            if (declarations.Length > 1 && (declarations.Any(declaration => declaration.BaseList is not null
                    || declaration.ParameterList is not null || declaration.TypeParameterList is not null
                    || declaration.ConstraintClauses.Count != 0)
                || declarations.Any(declaration => !declaration.Modifiers.Where(token => !token.IsKind(SyntaxKind.PartialKeyword))
                    .Select(token => token.RawKind).SequenceEqual(first.Modifiers.Where(token => !token.IsKind(SyntaxKind.PartialKeyword))
                        .Select(token => token.RawKind)))))
                throw new InvalidOperationException("inspection_partial_header_requires_review " + symbol);
            var entries = declarations.SelectMany(declaration => declaration.Members).OrderBy(node => order[node.SyntaxTree])
                .ThenBy(node => node.SpanStart).ToArray();
            var nested = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
            var members = new List<string>();
            foreach (var entry in entries)
            {
                var model = source.Compilation.GetSemanticModel(entry.SyntaxTree);
                if (entry is TypeDeclarationSyntax type)
                {
                    var child = model.GetDeclaredSymbol(type)!;
                    if (nested.Add(child))
                        members.Add(Type(child));
                }
                else
                {
                    // The view preserves complete direct members, including
                    // multi-variable field declarations and nested enums/delegates.
                    members.Add(SelectedText(entry));
                }
            }
            // Use one actual header and all attributes that apply to the logical
            // owner. Contracts bind its identity/signature separately. The brace
            // is a syntax token; comments and attribute strings are not anchors.
            if (first.OpenBraceToken.IsMissing)
                throw new InvalidOperationException("inspection_type_body " + symbol);
            var shell = first.WithMembers(default).WithCloseBraceToken(default).WithSemicolonToken(default)
                .WithAttributeLists(SyntaxFactory.List(declarations.SelectMany(declaration => declaration.AttributeLists)));
            return SelectedText(shell) + "\n" + string.Join("\n", members) + "\n}";
        }
    }

    private static string SelectedText(SyntaxNode node) => node.ReplaceTrivia(
        node.DescendantTrivia(descendIntoTrivia: true).Where(trivia => trivia.IsDirective || trivia.IsKind(SyntaxKind.DisabledTextTrivia)),
        (trivia, _) => SyntaxFactory.Whitespace(new string(trivia.ToFullString()
            .Select(character => character is '\r' or '\n' ? character : ' ').ToArray()))).ToFullString();
}
