using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace OS.Tooling;

public static partial class RepositoryCodeStyle
{
    // Return token boundaries, rather than searching text: strings, comments,
    // for-header semicolons and expression-bodied helpers are not statements.
    internal static IReadOnlyList<SyntaxToken> StatementLineBreaks(SyntaxNode root)
    {
        var lines = root.SyntaxTree.GetText().Lines;
        var breaks = new Dictionary<int, SyntaxToken>();
        foreach (var block in root.DescendantNodes().OfType<BlockSyntax>())
        {
            Before(block.OpenBraceToken);
            if (block.Statements.Count == 0)
                After(block.OpenBraceToken, block.CloseBraceToken);
            else
            {
                After(block.OpenBraceToken, block.Statements[0].GetFirstToken());
                Separate(block.Statements);
                After(block.Statements[^1].GetLastToken(), block.CloseBraceToken);
            }
        }

        foreach (var statement in root.DescendantNodes().OfType<SwitchStatementSyntax>())
        {
            Before(statement.OpenBraceToken);
            foreach (var section in statement.Sections)
            {
                foreach (var label in section.Labels)
                    Before(label.GetFirstToken());
                if (section.Statements.Count != 0)
                    After(section.Labels[^1].GetLastToken(), section.Statements[0].GetFirstToken());
                Separate(section.Statements);
            }

            Before(statement.CloseBraceToken);
        }

        foreach (var node in root.DescendantNodes())
        {
            StatementSyntax? body = node switch
            {
                IfStatementSyntax value => value.Statement,
                ElseClauseSyntax value when value.Statement is not IfStatementSyntax => value.Statement,
                ForStatementSyntax value => value.Statement,
                ForEachStatementSyntax value => value.Statement,
                ForEachVariableStatementSyntax value => value.Statement,
                WhileStatementSyntax value => value.Statement,
                DoStatementSyntax value => value.Statement,
                UsingStatementSyntax value => value.Statement,
                LockStatementSyntax value => value.Statement,
                FixedStatementSyntax value => value.Statement,
                LabeledStatementSyntax value => value.Statement,
                _ => null
            };
            if (body is not null)
                Before(body.GetFirstToken());
            if (node is ElseClauseSyntax other)
                Before(other.ElseKeyword);
            else if (node is CatchClauseSyntax caught)
                Before(caught.CatchKeyword);
            else if (node is FinallyClauseSyntax final)
                Before(final.FinallyKeyword);
        }

        var globals = root.ChildNodes().OfType<GlobalStatementSyntax>().ToArray();
        for (int i = 1; i < globals.Length; i++)
            After(globals[i - 1].GetLastToken(), globals[i].GetFirstToken());
        return breaks.OrderBy(value => value.Key).Select(value => value.Value).ToArray();

        void Separate(SyntaxList<StatementSyntax> statements)
        {
            for (int i = 1; i < statements.Count; i++)
                After(statements[i - 1].GetLastToken(), statements[i].GetFirstToken());
        }

        void Before(SyntaxToken token) => After(token.GetPreviousToken(), token);

        void After(SyntaxToken previous, SyntaxToken token)
        {
            if (previous.RawKind != 0 && token.RawKind != 0 && !previous.IsMissing && !token.IsMissing
                && lines.GetLineFromPosition(previous.Span.End).LineNumber == lines.GetLineFromPosition(token.SpanStart).LineNumber)
                breaks.TryAdd(token.SpanStart, token);
        }
    }
}
