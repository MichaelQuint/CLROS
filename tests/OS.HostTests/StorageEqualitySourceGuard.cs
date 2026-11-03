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
            && HostSourceOperations.IsParameter(HostSourceOperations.Argument(call, 0), symbol.Parameters[0])
            && HostSourceOperations.IsParameter(HostSourceOperations.Argument(call, 1), symbol.Parameters[1]);
    }

    internal static bool Literal(CSharpSourceCatalog catalog, string owner, string name, string expected, int? prefixLength = null)
    {
        var method = catalog.Method(owner, name);
        var model = catalog.Compilation.GetSemanticModel(method.SyntaxTree);
        var symbol = (IMethodSymbol)model.GetDeclaredSymbol(method)!;
        if (ReturnedCall(catalog, method) is not { } call || symbol.Parameters.Length != 1
            || (HostSourceOperations.LocalInitializer(model, HostSourceOperations.Argument(call, 1))
                ?? HostSourceOperations.Argument(call, 1)).Syntax is not LiteralExpressionSyntax literal
            || !literal.IsKind(SyntaxKind.Utf8StringLiteralExpression)
            || literal.Token.ValueText != expected)
            return false;
        var input = HostSourceOperations.Argument(call, 0);
        if (prefixLength is not { } length)
            return HostSourceOperations.IsParameter(input, symbol.Parameters[0]);
        if (HostSourceOperations.Slice(model, input, symbol.Parameters[0], 0, length))
            return true;
        // A local UTF-8 literal has a fixed byte length even though Roslyn does
        // not classify Span.Length as a compile-time constant.
        return System.Text.Encoding.UTF8.GetByteCount(expected) == length
            && HostSourceOperations.Argument(call, 1) is ILocalReferenceOperation signature
            && input is IInvocationOperation { Instance: { } instance, Arguments.Length: 2 } slice
            && slice.TargetMethod.Name == nameof(ReadOnlySpan<byte>.Slice)
            && SymbolEqualityComparer.Default.Equals(slice.TargetMethod.ContainingType, symbol.Parameters[0].Type)
            && HostSourceOperations.IsParameter(instance, symbol.Parameters[0])
            && HostSourceOperations.Constant(model, HostSourceOperations.Argument(slice, 0).Syntax, 0)
            && HostSourceOperations.Argument(slice, 1) is IPropertyReferenceOperation
                { Property.Name: nameof(ReadOnlySpan<byte>.Length), Instance: ILocalReferenceOperation sized } size
            && SymbolEqualityComparer.Default.Equals(size.Property.ContainingType, signature.Local.Type)
            && SymbolEqualityComparer.Default.Equals(sized.Local, signature.Local);
    }

    internal static bool ZeroBuffer(CSharpSourceCatalog catalog, string owner, string name, int width)
    {
        var method = catalog.Method(owner, name);
        var model = catalog.Compilation.GetSemanticModel(method.SyntaxTree);
        var symbol = (IMethodSymbol)model.GetDeclaredSymbol(method)!;
        if (ReturnedCall(catalog, method) is not { } call || symbol.Parameters.Length != 1
            || !HostSourceOperations.IsParameter(HostSourceOperations.Argument(call, 0), symbol.Parameters[0])
            || HostSourceOperations.LocalInitializer(model, HostSourceOperations.Argument(call, 1))?.Syntax
                is not StackAllocArrayCreationExpressionSyntax { Type: ArrayTypeSyntax array, Initializer: { } initializer }
            || model.GetTypeInfo(array.ElementType).Type?.SpecialType != SpecialType.System_Byte
            || array.RankSpecifiers.Count != 1 || array.RankSpecifiers[0].Sizes.Count != 1
            || !HostSourceOperations.Constant(model, array.RankSpecifiers[0].Sizes[0], width)
            || initializer.Expressions.Count != width)
            return false;
        return initializer.Expressions.All(value => HostSourceOperations.Constant(model, value, 0));
    }

    private static IInvocationOperation? ReturnedCall(CSharpSourceCatalog catalog, MethodDeclarationSyntax method)
    {
        var calls = HostSourceOperations.Calls(catalog, method, typeof(OS.Std.MemoryExtensions),
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

}
