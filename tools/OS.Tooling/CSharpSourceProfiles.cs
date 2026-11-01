using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace OS.Tooling;

/// <summary>Enumerates symbol assignments covering satisfiable conditional-source
/// branches for host syntax inspection. These are syntax configurations, not
/// supported EFI workload selections or authority to execute a guest.</summary>
public static class CSharpSourceProfiles
{
    public static IReadOnlyList<CSharpParseOptions> ForSource(string source)
    {
        var options = new CSharpParseOptions(LanguageVersion.CSharp14);
        var result = new List<CSharpParseOptions> { options };
        var seen = new HashSet<string>(StringComparer.Ordinal) { string.Empty };
        var branches = new List<List<ExpressionSyntax>>();
        var conditions = new List<List<(ExpressionSyntax Expression, bool Expected)>>();
        var paths = new List<(ExpressionSyntax Expression, bool Expected)[]>();
        var pathKeys = new HashSet<string>(StringComparer.Ordinal);
        var tree = CSharpSyntaxTree.ParseText(source, options);

        foreach (var trivia in tree.GetRoot().DescendantTrivia(descendIntoTrivia: true))
        {
            switch (trivia.GetStructure())
            {
                case IfDirectiveTriviaSyntax directive:
                    branches.Add([directive.Condition]);
                    conditions.Add([(directive.Condition, true)]);
                    AddConfiguration();
                    break;
                case ElifDirectiveTriviaSyntax directive when branches.Count != 0:
                    conditions[^1] = branches[^1].Select(value => (value, false)).ToList();
                    conditions[^1].Add((directive.Condition, true));
                    branches[^1].Add(directive.Condition);
                    AddConfiguration();
                    break;
                case ElseDirectiveTriviaSyntax when branches.Count != 0:
                    conditions[^1] = branches[^1].Select(value => (value, false)).ToList();
                    AddConfiguration();
                    break;
                case EndIfDirectiveTriviaSyntax when branches.Count != 0:
                    branches.RemoveAt(branches.Count - 1);
                    conditions.RemoveAt(conditions.Count - 1);
                    break;
            }
        }

        // A file can hide two independently enabled types in sibling branches.
        // Single-branch witnesses do not detect that conflict. Every violation
        // of the one-type rule has a pair of simultaneously present declarations.
        for (int first = 0; first < paths.Count; first++)
            for (int second = first + 1; second < paths.Count; second++)
                AddConfiguration([.. paths[first], .. paths[second]]);

        return result;

        void AddConfiguration((ExpressionSyntax Expression, bool Expected)[]? selected = null)
        {
            var required = selected ?? conditions.SelectMany(value => value).ToArray();
            if (selected is null && pathKeys.Add(string.Join(";", required.Select(value => value.Expected + ":" + value.Expression.ToString()))))
                paths.Add(required);
            string[] names = required.SelectMany(value => value.Expression.DescendantNodesAndSelf()
                .OfType<IdentifierNameSyntax>().Select(name => name.Identifier.ValueText))
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            var values = new Dictionary<string, bool>(StringComparer.Ordinal);
            if (!Solve(0))
                return;

            string[] enabled = values.Where(pair => pair.Value).Select(pair => pair.Key)
                .Order(StringComparer.Ordinal).ToArray();
            if (seen.Add(string.Join(";", enabled)))
                result.Add(options.WithPreprocessorSymbols(enabled));

            bool Solve(int index)
            {
                if (required.Any(value => Evaluate(value.Expression, values) is bool actual && actual != value.Expected))
                    return false;

                if (index == names.Length)
                    return true;

                values[names[index]] = false;
                if (Solve(index + 1))
                    return true;

                values[names[index]] = true;
                if (Solve(index + 1))
                    return true;

                values.Remove(names[index]);
                return false;
            }
        }
    }

    private static bool? Evaluate(ExpressionSyntax expression, IReadOnlyDictionary<string, bool> values)
    {
        switch (expression)
        {
            case IdentifierNameSyntax name:
                return values.TryGetValue(name.Identifier.ValueText, out bool value) ? value : null;
            case LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.TrueLiteralExpression):
                return true;
            case LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.FalseLiteralExpression):
                return false;
            case ParenthesizedExpressionSyntax parenthesized:
                return Evaluate(parenthesized.Expression, values);
            case PrefixUnaryExpressionSyntax unary when unary.IsKind(SyntaxKind.LogicalNotExpression):
                return !Evaluate(unary.Operand, values);
            case BinaryExpressionSyntax binary:
                bool? left = Evaluate(binary.Left, values), right = Evaluate(binary.Right, values);
                return binary.Kind() switch
                {
                    SyntaxKind.LogicalAndExpression => left & right,
                    SyntaxKind.LogicalOrExpression => left | right,
                    SyntaxKind.EqualsExpression => left.HasValue && right.HasValue ? left == right : null,
                    SyntaxKind.NotEqualsExpression => left.HasValue && right.HasValue ? left != right : null,
                    _ => throw new InvalidDataException("unsupported_preprocessor_expression")
                };
            default:
                throw new InvalidDataException("unsupported_preprocessor_expression");
        }
    }
}
