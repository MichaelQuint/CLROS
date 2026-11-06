using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using OS.Tooling;

/// <summary>Checks the shared ASCII dependencies and FAT name field boundaries.
/// Existing FAT fixtures remain responsible for lookup and decoded-name behavior.</summary>
internal static class StorageAsciiSourceGuard
{
    internal static bool LongNames(CSharpSourceCatalog catalog, string owner, string name)
    {
        var method = catalog.Method(owner, name);
        var model = catalog.Compilation.GetSemanticModel(method.SyntaxTree);
        var symbol = (IMethodSymbol)model.GetDeclaredSymbol(method)!;
        var calls = HostSourceOperations.Calls(catalog, method, typeof(OS.Std.Ascii), nameof(OS.Std.Ascii.ToUpperInvariant));
        if (calls.Length != 2 || symbol.Parameters.Length != 2 || catalog.Type(owner).GetMembers("ToUpperAscii").Length != 0
            || HostSourceOperations.Consumer(calls[0]) is not IBinaryOperation { OperatorKind: BinaryOperatorKind.NotEquals } comparison
            || HostSourceOperations.Consumer(calls[1]) is not IBinaryOperation other || other.Syntax.Span != comparison.Syntax.Span
            || comparison.Syntax.Parent is not IfStatementSyntax branch || branch.Condition != comparison.Syntax
            || DirectReturn(branch.Statement) is not { Expression: { } expression }
            || model.GetConstantValue(expression) is not { HasValue: true, Value: false })
            return false;
        return Pair(0, 1) || Pair(1, 0);

        bool Pair(int left, int right)
        {
            var first = IndexedArgument(calls[0], symbol.Parameters[left]);
            var second = IndexedArgument(calls[1], symbol.Parameters[right]);
            return first is not null && SymbolEqualityComparer.Default.Equals(first, second);
        }

        ISymbol? IndexedArgument(IInvocationOperation call, IParameterSymbol parameter)
        {
            if (HostSourceOperations.Argument(call, 0).Syntax is not ElementAccessExpressionSyntax { ArgumentList.Arguments.Count: 1 } element
                || model.GetOperation(element.Expression) is not { } instance || !HostSourceOperations.IsParameter(instance, parameter))
                return null;
            return model.GetSymbolInfo(element.ArgumentList.Arguments[0].Expression).Symbol;
        }
    }

    internal static bool ShortNameFields(CSharpSourceCatalog catalog, string owner, string name)
    {
        var method = catalog.Method(owner, name);
        var model = catalog.Compilation.GetSemanticModel(method.SyntaxTree);
        var symbol = (IMethodSymbol)model.GetDeclaredSymbol(method)!;
        var calls = HostSourceOperations.Calls(catalog, method, typeof(OS.Std.Text.AsciiString), nameof(OS.Std.Text.AsciiString.TrimTrailingSpace));
        return calls.Length == 2 && symbol.Parameters.Length == 1
            && catalog.Type(owner).GetMembers("TrimTrailingSpaces").Length == 0
            && Field(0, 8) && Field(8, 3);

        bool Field(int offset, int width)
        {
            var matches = calls.Where(call => HostSourceOperations.Slice(model, HostSourceOperations.Argument(call, 0), symbol.Parameters[0], offset, width)).ToArray();
            if (matches.Length != 1 || matches[0].Parent is not IPropertyReferenceOperation { Property.Name: "Length" } length
                || length.Syntax.Parent is not EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax variable }
                || model.GetDeclaredSymbol(variable) is not ILocalSymbol local)
                return false;
            return method.DescendantNodes().OfType<ForStatementSyntax>().Any(loop => loop.Condition is BinaryExpressionSyntax condition
                && condition.IsKind(SyntaxKind.LessThanExpression)
                && model.GetOperation(condition.Right) is ILocalReferenceOperation bound
                && SymbolEqualityComparer.Default.Equals(bound.Local, local)
                && HostSourceOperations.LocalInitializer(model, bound) is not null);
        }
    }

    private static ReturnStatementSyntax? DirectReturn(StatementSyntax statement) => statement switch
    {
        ReturnStatementSyntax returned => returned,
        BlockSyntax { Statements.Count: 1 } block => block.Statements[0] as ReturnStatementSyntax,
        _ => null
    };
}
