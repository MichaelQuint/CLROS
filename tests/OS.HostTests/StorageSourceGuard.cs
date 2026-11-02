using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using OS.Tooling;

/// <summary>Resolves storage source dependencies without filenames, import spelling
/// or local-variable names. Behavioral fixtures own the expected data and statuses.</summary>
internal static class StorageSourceGuard
{
    internal static IInvocationOperation[] Calls(CSharpSourceCatalog catalog, MethodDeclarationSyntax method,
        Type dependency, string name)
    {
        var model = catalog.Compilation.GetSemanticModel(method.SyntaxTree);
        return method.DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Where(invocation => IsMethod(catalog.Invocation(invocation), dependency, name))
            .Select(invocation => (IInvocationOperation)model.GetOperation(invocation)!).ToArray();
    }

    internal static bool IsMethod(IMethodSymbol symbol, Type owner, string name) =>
        symbol.Name == name && symbol.ContainingType.ToDisplayString() == owner.FullName
        && symbol.ContainingAssembly.Name == owner.Assembly.GetName().Name;

    internal static IOperation Value(IOperation operation)
    {
        while (operation is IConversionOperation { Conversion.IsUserDefined: false } conversion)
            operation = conversion.Operand;
        return operation;
    }

    internal static IOperation Argument(IInvocationOperation invocation, int ordinal) =>
        Value(invocation.Arguments.Single(argument => argument.Parameter?.Ordinal == ordinal).Value);

    internal static IOperation? Consumer(IOperation operation)
    {
        while (operation.Parent is IConversionOperation { Conversion.IsUserDefined: false } conversion)
            operation = conversion;
        return operation.Parent;
    }

    internal static bool IsParameter(IOperation operation, IParameterSymbol parameter) =>
        Value(operation) is IParameterReferenceOperation reference
        && SymbolEqualityComparer.Default.Equals(reference.Parameter, parameter);

    internal static bool Slice(SemanticModel model, IOperation input, IParameterSymbol parameter, int offset, int count)
    {
        if (input is IInvocationOperation slice && slice.TargetMethod.Name == nameof(ReadOnlySpan<byte>.Slice)
            && slice.Instance is not null && IsParameter(slice.Instance, parameter)
            && SymbolEqualityComparer.Default.Equals(slice.TargetMethod.ContainingType, parameter.Type)
            && slice.Arguments.Length == 2)
            return Constant(model, Argument(slice, 0).Syntax, offset)
                && Constant(model, Argument(slice, 1).Syntax, count);
        return input.Syntax is ElementAccessExpressionSyntax { ArgumentList.Arguments.Count: 1 } element
            && model.GetOperation(element.Expression) is { } instance && IsParameter(instance, parameter)
            && element.ArgumentList.Arguments[0].Expression is RangeExpressionSyntax range
            && (range.LeftOperand is null ? offset == 0 : Constant(model, range.LeftOperand, offset))
            && range.RightOperand is not null && Constant(model, range.RightOperand, offset + count);
    }

    internal static bool Constant(SemanticModel model, SyntaxNode syntax, int value) =>
        model.GetConstantValue(syntax) is { HasValue: true, Value: int actual } && actual == value;

    internal static IOperation? LocalInitializer(SemanticModel model, IOperation operation)
    {
        if (Value(operation) is not ILocalReferenceOperation local
            || local.Local.DeclaringSyntaxReferences.Single().GetSyntax() is not VariableDeclaratorSyntax { Initializer: { } initializer } variable
            || variable.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault() is not { } method)
            return null;
        foreach (var identifier in method.DescendantNodes().OfType<IdentifierNameSyntax>())
        {
            if (!SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(identifier).Symbol, local.Local))
                continue;
            if (identifier.Ancestors().TakeWhile(node => node != method).Any(node => node switch
            {
                AssignmentExpressionSyntax assignment => assignment.Left.Span.Contains(identifier.Span),
                PrefixUnaryExpressionSyntax prefix => prefix.IsKind(SyntaxKind.PreIncrementExpression) || prefix.IsKind(SyntaxKind.PreDecrementExpression),
                PostfixUnaryExpressionSyntax postfix => postfix.IsKind(SyntaxKind.PostIncrementExpression) || postfix.IsKind(SyntaxKind.PostDecrementExpression),
                ArgumentSyntax argument => argument.RefKindKeyword.IsKind(SyntaxKind.RefKeyword) || argument.RefKindKeyword.IsKind(SyntaxKind.OutKeyword),
                _ => false
            }))
                return null;
        }
        return model.GetOperation(initializer.Value);
    }
}
