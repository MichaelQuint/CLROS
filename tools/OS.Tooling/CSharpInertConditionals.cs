using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace OS.Tooling;

/// <summary>Finds conditional groups containing only whitespace and conditional
/// directives. Comments, code and every effectful directive prevent removal.
/// Callers changing text must intersect findings across their supported parse
/// configurations and independently verify the resulting compiler inputs.</summary>
internal static class CSharpInertConditionals
{
    internal static IReadOnlyList<TextSpan> Find(SyntaxTree tree)
    {
        var text = tree.GetText();
        var stack = new List<(int Start, bool Inert)>();
        var found = new List<TextSpan>();
        int previousEnd = 0;
        foreach (var directive in tree.GetRoot().DescendantTrivia(descendIntoTrivia: true)
            .Where(trivia => trivia.HasStructure).Select(trivia => trivia.GetStructure())
            .OfType<DirectiveTriviaSyntax>().OrderBy(directive => directive.FullSpan.Start))
        {
            if (stack.Count != 0 && !text.ToString(TextSpan.FromBounds(previousEnd, directive.FullSpan.Start)).All(char.IsWhiteSpace))
                RetainParents();
            bool comment = directive.DescendantTrivia().Any(trivia =>
                trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) || trivia.IsKind(SyntaxKind.MultiLineCommentTrivia)
                || trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
                || trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia));
            if (comment)
                RetainParents();
            if (directive is IfDirectiveTriviaSyntax)
                stack.Add((text.Lines.GetLineFromPosition(directive.SpanStart).Start, !comment));
            else if (directive is EndIfDirectiveTriviaSyntax && stack.Count != 0)
            {
                var frame = stack[^1];
                stack.RemoveAt(stack.Count - 1);
                if (frame.Inert && !comment)
                    found.Add(TextSpan.FromBounds(frame.Start,
                        text.Lines.GetLineFromPosition(directive.SpanStart).EndIncludingLineBreak));
            }
            else if (directive is not ElifDirectiveTriviaSyntax and not ElseDirectiveTriviaSyntax)
                RetainParents();
            previousEnd = directive.FullSpan.End;
        }

        var outer = new List<TextSpan>();
        foreach (var span in found.OrderBy(span => span.Start).ThenByDescending(span => span.End))
            if (outer.Count == 0 || span.Start >= outer[^1].End)
                outer.Add(span);
        return outer;

        void RetainParents()
        {
            for (int index = 0; index < stack.Count; index++)
                stack[index] = (stack[index].Start, false);
        }
    }
}
