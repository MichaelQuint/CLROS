using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using OS.Tooling;

/// <summary>Checks that small storage equality adapters return the shared
/// comparison of their actual inputs. Byte-format expectations stay explicit;
/// parser behavior fixtures independently check valid and invalid data.</summary>
internal static class StorageEqualitySourceGuard
{
    internal static bool Parameters(CSharpSourceCatalog catalog, string owner, string name)
    {
        var method = catalog.Method(owner, name);
        var model = catalog.Compilation.GetSemanticModel(method.SyntaxTree);
        var symbol = (IMethodSymbol)model.GetDeclaredSymbol(method)!;
        return ReturnedCall(catalog, method) is { } call && symbol.Parameters.Length == 2
            && StorageSourceGuard.IsParameter(StorageSourceGuard.Argument(call, 0), symbol.Parameters[0])
            && StorageSourceGuard.IsParameter(StorageSourceGuard.Argument(call, 1), symbol.Parameters[1]);
    }

    internal static bool Literal(CSharpSourceCatalog catalog, string owner, string name, string expected, int? prefixLength = null)
    {
        var method = catalog.Method(owner, name);
        var model = catalog.Compilation.GetSemanticModel(method.SyntaxTree);
        var symbol = (IMethodSymbol)model.GetDeclaredSymbol(method)!;
        if (ReturnedCall(catalog, method) is not { } call || symbol.Parameters.Length != 1
            || StorageSourceGuard.Argument(call, 1).Syntax is not LiteralExpressionSyntax literal
            || !literal.IsKind(SyntaxKind.Utf8StringLiteralExpression)
            || literal.Token.ValueText != expected)
            return false;
        var input = StorageSourceGuard.Argument(call, 0);
        return prefixLength is { } length
            ? Prefix(model, input, symbol.Parameters[0], length)
            : StorageSourceGuard.IsParameter(input, symbol.Parameters[0]);
    }

    internal static bool ZeroBuffer(CSharpSourceCatalog catalog, string owner, string name, int width)
    {
        var method = catalog.Method(owner, name);
        var model = catalog.Compilation.GetSemanticModel(method.SyntaxTree);
        var symbol = (IMethodSymbol)model.GetDeclaredSymbol(method)!;
        if (ReturnedCall(catalog, method) is not { } call || symbol.Parameters.Length != 1
            || !StorageSourceGuard.IsParameter(StorageSourceGuard.Argument(call, 0), symbol.Parameters[0])
            || StorageSourceGuard.LocalInitializer(model, StorageSourceGuard.Argument(call, 1))?.Syntax
                is not StackAllocArrayCreationExpressionSyntax { Type: ArrayTypeSyntax array, Initializer: { } initializer }
            || model.GetTypeInfo(array.ElementType).Type?.SpecialType != SpecialType.System_Byte
            || array.RankSpecifiers.Count != 1 || array.RankSpecifiers[0].Sizes.Count != 1
            || !Constant(model, array.RankSpecifiers[0].Sizes[0], width)
            || initializer.Expressions.Count != width)
            return false;
        return initializer.Expressions.All(value => Constant(model, value, 0));
    }

    private static IInvocationOperation? ReturnedCall(CSharpSourceCatalog catalog, MethodDeclarationSyntax method)
    {
        var calls = StorageSourceGuard.Calls(catalog, method, typeof(OS.Std.MemoryExtensions),
            nameof(OS.Std.MemoryExtensions.SequenceEqual));
        if (calls.Length != 1 || method.DescendantNodes().Any(node => node is
                ForStatementSyntax or ForEachStatementSyntax or ForEachVariableStatementSyntax or WhileStatementSyntax or DoStatementSyntax)
            || method.DescendantNodes().OfType<ElementAccessExpressionSyntax>()
                .Any(element => !calls[0].Syntax.Span.Contains(element.Span)))
            return null;
        IOperation value = calls[0];
        while (value.Parent is IBinaryOperation { OperatorKind: BinaryOperatorKind.ConditionalAnd } conjunction)
            value = conjunction;
        bool returnedByMethod = value.Parent is IReturnOperation { Syntax: ReturnStatementSyntax returned }
                && returned.Parent == method.Body
            || method.ExpressionBody?.Expression == value.Syntax;
        return returnedByMethod ? calls[0] : null;
    }

    private static bool Prefix(SemanticModel model, IOperation input, IParameterSymbol parameter, int width)
    {
        if (input is IInvocationOperation slice && slice.TargetMethod.Name == nameof(ReadOnlySpan<byte>.Slice)
            && slice.Instance is not null && StorageSourceGuard.IsParameter(slice.Instance, parameter)
            && slice.Arguments.Length == 2)
            return Constant(model, StorageSourceGuard.Argument(slice, 0).Syntax, 0)
                && Constant(model, StorageSourceGuard.Argument(slice, 1).Syntax, width);
        return input.Syntax is ElementAccessExpressionSyntax { ArgumentList.Arguments.Count: 1 } element
            && model.GetOperation(element.Expression) is { } instance
            && StorageSourceGuard.IsParameter(instance, parameter)
            && element.ArgumentList.Arguments[0].Expression is RangeExpressionSyntax range
            && (range.LeftOperand is null || Constant(model, range.LeftOperand, 0))
            && range.RightOperand is not null && Constant(model, range.RightOperand, width);
    }

    private static bool Constant(SemanticModel model, SyntaxNode syntax, int value) =>
        model.GetConstantValue(syntax) is { HasValue: true, Value: int actual } && actual == value;
}
