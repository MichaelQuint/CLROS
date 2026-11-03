using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace OS.Tooling;

public static partial class RepositoryCodeStyle
{
    // Continuation headers can be deeper than their block braces. Require only
    // containment, preserving both invocation-aligned and header-aligned styles.
    internal static IEnumerable<(BlockSyntax Block, int MinimumIndent)> LambdaBlocksWithMinimumIndent(SyntaxNode root)
    {
        foreach (var block in root.DescendantNodes().OfType<BlockSyntax>())
        {
            if (block.Parent is not AnonymousFunctionExpressionSyntax lambda)
                continue;
            var owner = lambda.Ancestors().FirstOrDefault(value =>
                value is StatementSyntax and not BlockSyntax or MemberDeclarationSyntax);
            if (owner is not null)
                yield return (block, LineIndentation(owner.GetFirstToken()));
        }
    }

    internal static int LineIndentation(SyntaxToken token)
    {
        var line = token.SyntaxTree!.GetText().Lines.GetLineFromPosition(token.SpanStart);
        int columns = 0;
        foreach (char value in line.ToString())
        {
            if (value == ' ')
                columns++;
            else if (value == '\t')
                columns += 4 - columns % 4;
            else
                break;
        }

        return columns;
    }
}
