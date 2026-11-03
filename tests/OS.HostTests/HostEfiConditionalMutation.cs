using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Tooling;
using System.Text;

/// <summary>Retargets a guard mutation to the actual declaration/call's nearest
/// conditional branch. It never selects an empty wrapper or the first text hit.</summary>
internal static class HostEfiConditionalMutation
{
    internal static CSharpSourceDocument OpenGuard(CSharpSourceDocument document, SyntaxNode target, string symbol)
    {
        if (target.SyntaxTree != document.Syntax)
            throw new InvalidOperationException("guard_mutation_document");
        var stack = new Stack<DirectiveTriviaSyntax>();
        foreach (var directive in document.Syntax.GetRoot().DescendantTrivia(descendIntoTrivia: true)
            .Select(trivia => trivia.GetStructure()).OfType<DirectiveTriviaSyntax>()
            .Where(directive => directive.SpanStart < target.SpanStart).OrderBy(directive => directive.SpanStart))
        {
            if (directive is IfDirectiveTriviaSyntax)
                stack.Push(directive);
            else if (directive is ElifDirectiveTriviaSyntax or ElseDirectiveTriviaSyntax)
            {
                if (stack.Count == 0)
                    throw new InvalidOperationException("guard_mutation_branch");
                stack.Pop();
                stack.Push(directive);
            }
            else if (directive is EndIfDirectiveTriviaSyntax)
            {
                if (stack.Count == 0)
                    throw new InvalidOperationException("guard_mutation_end");
                stack.Pop();
            }
        }
        if (stack.Count == 0 || stack.Peek() is not IfDirectiveTriviaSyntax
            { Condition: IdentifierNameSyntax condition } selected || condition.Identifier.ValueText != symbol)
            throw new InvalidOperationException("guard_mutation_unsupported_target");
        var boundary = selected.GetRelatedDirectives().FirstOrDefault(directive => directive.SpanStart > selected.SpanStart);
        if (boundary is null || target.Span.End > boundary.SpanStart)
            throw new InvalidOperationException("guard_mutation_partial_target");
        string changed = document.Text[..condition.SpanStart] + "true // " + symbol + document.Text[condition.Span.End..];
        return CSharpSourceDocument.FromBytes(document.Path, Encoding.UTF8.GetBytes(changed), (CSharpParseOptions)document.Syntax.Options);
    }
}
