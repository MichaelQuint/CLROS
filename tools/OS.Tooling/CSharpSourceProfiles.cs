using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace OS.Tooling;

/// <summary>Enumerates symbol assignments covering satisfiable conditional-source
/// branches for host syntax inspection. These are syntax configurations, not
/// supported EFI workload selections or authority to execute a guest.</summary>
public static partial class CSharpSourceProfiles
{
    public static IReadOnlyList<CSharpParseOptions> ForSource(string source)
    {
        var options = new CSharpParseOptions(LanguageVersion.CSharp14);
        var result = new List<CSharpParseOptions> { options };
        var seen = new HashSet<string>(StringComparer.Ordinal) { string.Empty };
        var groups = new List<ConditionalGroup>();
        var paths = new List<(ExpressionSyntax Expression, bool Expected)[]>();
        var pathKeys = new HashSet<string>(StringComparer.Ordinal);
        var localSymbols = new Dictionary<string, ExpressionSyntax>(StringComparer.Ordinal);
        var shapes = new Dictionary<string, HashSet<(int Start, int Kind, string Name, bool HasOwnMembers, bool Partial)>>(StringComparer.Ordinal);
        var tree = CSharpSyntaxTree.ParseText(source, options);
        shapes.Add(string.Empty, Declarations(tree).ToHashSet());

        foreach (var trivia in tree.GetRoot().DescendantTrivia(descendIntoTrivia: true))
        {
            switch (trivia.GetStructure())
            {
                case IfDirectiveTriviaSyntax directive:
                    var condition = Rewrite(directive.Condition);
                    groups.Add(new ConditionalGroup(condition));
                    AddConfiguration();
                    break;
                case ElifDirectiveTriviaSyntax directive when groups.Count != 0:
                    groups[^1].Conditions = groups[^1].Alternatives.Select(value => (value, false)).ToList();
                    condition = Rewrite(directive.Condition);
                    groups[^1].Conditions.Add((condition, true));
                    groups[^1].Alternatives.Add(condition);
                    AddConfiguration();
                    break;
                case ElseDirectiveTriviaSyntax when groups.Count != 0:
                    groups[^1].HasElse = true;
                    groups[^1].Conditions = groups[^1].Alternatives.Select(value => (value, false)).ToList();
                    AddConfiguration();
                    break;
                case EndIfDirectiveTriviaSyntax when groups.Count != 0:
                    if (!groups[^1].HasElse)
                    {
                        groups[^1].Conditions = groups[^1].Alternatives.Select(value => (value, false)).ToList();
                        AddConfiguration();
                    }
                    // Keep default-active alternatives too: a sibling may
                    // need a different witness to coexist with another type.
                    if (groups[^1].StructureChanged)
                        foreach (var path in groups[^1].Paths)
                            if (pathKeys.Add(string.Join(";", path.Select(value => value.Expected + ":" + value.Expression.ToFullString()))))
                                paths.Add(path);
                    groups.RemoveAt(groups.Count - 1);
                    break;
                case DefineDirectiveTriviaSyntax directive:
                    SetLocalSymbol(directive.Name.ValueText, true);
                    break;
                case UndefDirectiveTriviaSyntax directive:
                    SetLocalSymbol(directive.Name.ValueText, false);
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

        // Saved formulas already refer to the initial input symbols. Expanding
        // their identifiers again would incorrectly apply later local mutations.
        ExpressionSyntax Rewrite(ExpressionSyntax expression) => expression switch
        {
            IdentifierNameSyntax name => localSymbols.GetValueOrDefault(name.Identifier.ValueText, name),
            ParenthesizedExpressionSyntax parenthesized => parenthesized.WithExpression(Rewrite(parenthesized.Expression)),
            PrefixUnaryExpressionSyntax unary => unary.WithOperand(Rewrite(unary.Operand)),
            BinaryExpressionSyntax binary => binary.WithLeft(Rewrite(binary.Left)).WithRight(Rewrite(binary.Right)),
            _ => expression
        };

        void SetLocalSymbol(string name, bool defined)
        {
            ExpressionSyntax active = SyntaxFactory.LiteralExpression(SyntaxKind.TrueLiteralExpression);
            foreach (var (expression, expected) in groups.SelectMany(value => value.Conditions))
                active = SyntaxFactory.BinaryExpression(SyntaxKind.LogicalAndExpression, active,
                    expected ? SyntaxFactory.ParenthesizedExpression(expression)
                        : SyntaxFactory.PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, SyntaxFactory.ParenthesizedExpression(expression)));

            var previous = localSymbols.GetValueOrDefault(name, SyntaxFactory.IdentifierName(name));
            localSymbols[name] = defined
                ? SyntaxFactory.BinaryExpression(SyntaxKind.LogicalOrExpression,
                    SyntaxFactory.ParenthesizedExpression(active), SyntaxFactory.ParenthesizedExpression(previous))
                : SyntaxFactory.BinaryExpression(SyntaxKind.LogicalAndExpression,
                    SyntaxFactory.PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, SyntaxFactory.ParenthesizedExpression(active)),
                    SyntaxFactory.ParenthesizedExpression(previous));
        }

        void AddConfiguration((ExpressionSyntax Expression, bool Expected)[]? selected = null)
        {
            var required = selected ?? groups.SelectMany(value => value.Conditions).ToArray();
            var candidate = FindConfiguration(required);
            if (candidate is null)
                return;

            string key = string.Join(";", candidate.PreprocessorSymbolNames);
            if (seen.Add(key))
                result.Add(candidate);

            // Compare against an active parent, not an entirely disabled file.
            // Otherwise every method-body switch below an outer type guard
            // would generate redundant pairs for the same declarations.
            if (selected is null)
            {
                groups[^1].Paths.Add(required);
                var parent = FindConfiguration(groups.Take(groups.Count - 1).SelectMany(value => value.Conditions).ToArray());
                if (parent is not null && !Shape(candidate).SetEquals(Shape(parent)))
                    groups[^1].StructureChanged = true;
            }
        }

        HashSet<(int Start, int Kind, string Name, bool HasOwnMembers, bool Partial)> Shape(CSharpParseOptions candidate)
        {
            string key = string.Join(";", candidate.PreprocessorSymbolNames);
            if (!shapes.TryGetValue(key, out var declarations))
                shapes.Add(key, declarations = Declarations(CSharpSyntaxTree.ParseText(source, candidate)).ToHashSet());
            return declarations;
        }

        CSharpParseOptions? FindConfiguration((ExpressionSyntax Expression, bool Expected)[] required)
        {
            string[] names = required.SelectMany(value => value.Expression.DescendantNodesAndSelf()
                .OfType<IdentifierNameSyntax>().Select(name => name.Identifier.ValueText))
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            var values = new Dictionary<string, bool>(StringComparer.Ordinal);
            if (!Solve(0))
                return null;

            string[] enabled = values.Where(pair => pair.Value).Select(pair => pair.Key)
                .Order(StringComparer.Ordinal).ToArray();
            return options.WithPreprocessorSymbols(enabled);

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

    private static IEnumerable<(int Start, int Kind, string Name, bool HasOwnMembers, bool Partial)> Declarations(SyntaxTree tree)
    {
        foreach (var node in tree.GetRoot().DescendantNodes())
        {
            if (node is BaseTypeDeclarationSyntax type)
                yield return (type.SpanStart, type.RawKind, RepositoryCodeStyle.TypeIdentity(type),
                    type is not TypeDeclarationSyntax container || container.Members.Any(member => member is not BaseTypeDeclarationSyntax and not DelegateDeclarationSyntax)
                        || container.AttributeLists.Count != 0 || container.BaseList is not null || container.ParameterList is not null
                        || (container.TypeParameterList is not null && container.TypeParameterList.Parameters.Any(value => value.AttributeLists.Count != 0)),
                    type.Modifiers.Any(SyntaxKind.PartialKeyword));
            else if (node is DelegateDeclarationSyntax callback)
                yield return (callback.SpanStart, callback.RawKind, RepositoryCodeStyle.TypeIdentity(callback), true, false);
            else if (node is GlobalStatementSyntax statement)
                yield return (statement.SpanStart, statement.RawKind, string.Empty, true, false);
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
