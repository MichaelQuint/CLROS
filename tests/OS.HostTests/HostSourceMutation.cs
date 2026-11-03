using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

/// <summary>Applies a single deliberate expression mutation for a negative
/// control. Comments, whitespace and unrelated declarations cannot satisfy its
/// anchor. Missing or ambiguous anchors fail before the control is evaluated.</summary>
internal static class HostSourceMutation
{
    internal static string ReplaceExpression(string source, string expected, string replacement,
        IEnumerable<string>? symbols = null, bool ifConditionOnly = false)
    {
        var options = new CSharpParseOptions(LanguageVersion.CSharp14, preprocessorSymbols: symbols);
        var tree = CSharpSyntaxTree.ParseText(source, options);
        var original = SyntaxFactory.ParseExpression(expected, options: options);
        var changed = SyntaxFactory.ParseExpression(replacement, options: options);
        if (tree.GetDiagnostics().Concat(original.GetDiagnostics()).Concat(changed.GetDiagnostics())
            .Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
            throw new InvalidOperationException("source_mutation_invalid_syntax");
        var root = tree.GetRoot();
        var matches = root.DescendantNodes().OfType<ExpressionSyntax>()
            .Where(expression => SyntaxFactory.AreEquivalent(expression, original)
                && (!ifConditionOnly || expression.Parent is IfStatementSyntax statement
                    && statement.Condition == expression)).ToArray();
        if (matches.Length != 1)
            throw new InvalidOperationException("source_mutation_anchor_missing_or_ambiguous " + expected);
        return root.ReplaceNode(matches[0], changed.WithTriviaFrom(matches[0])).ToFullString();
    }

    /// <summary>Finds a character span in active inspection text. Token spellings remain exact;
    /// trivia may vary. Raw comment/inactive-source guards remain separate.</summary>
    internal static TextSpan Find(string source, string anchor, TextSpan scope, bool requireUnique)
    {
        if (scope.Start < 0 || scope.End > source.Length)
            throw new InvalidOperationException("source_mutation_scope");
        var options = new CSharpParseOptions(LanguageVersion.CSharp14);
        SyntaxToken[] Tokens(string text) => SyntaxFactory.ParseTokens(text, options: options)
            .Where(token => !token.IsKind(SyntaxKind.EndOfFileToken)).ToArray();
        var wanted = Tokens(anchor);
        if (wanted.Length == 0)
            throw new InvalidOperationException("source_mutation_empty_anchor");
        var actual = Tokens(source.Substring(scope.Start, scope.Length));
        TextSpan? found = null;
        for (int index = 0; index <= actual.Length - wanted.Length; index++)
        {
            bool matches = true;
            for (int offset = 0; offset < wanted.Length; offset++)
                if (actual[index + offset].RawKind != wanted[offset].RawKind
                    || actual[index + offset].Text != wanted[offset].Text)
                {
                    matches = false;
                    break;
                }
            if (!matches)
                continue;
            if (found is not null)
                throw new InvalidOperationException("source_mutation_ambiguous_anchor " + anchor);
            found = TextSpan.FromBounds(scope.Start + actual[index].SpanStart,
                scope.Start + actual[index + wanted.Length - 1].Span.End);
            if (!requireUnique)
                break;
        }
        return found ?? throw new InvalidOperationException("source_mutation_missing_anchor " + anchor);
    }
}
