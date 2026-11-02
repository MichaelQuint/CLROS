using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using OS.Tooling;

/// <summary>Checks the shared-copy dependency and its failure branch in a selected
/// storage method. Behavioral fixtures separately verify copied bytes and bounds.</summary>
internal static class StorageBufferCopySourceGuard
{
    internal static bool Check(CSharpSourceCatalog catalog, string owner, string methodName, string failureStatus)
    {
        MethodDeclarationSyntax method = catalog.Method(owner, methodName);
        SemanticModel model = catalog.Compilation.GetSemanticModel(method.SyntaxTree);
        var declaration = (IMethodSymbol)model.GetDeclaredSymbol(method)!;
        IParameterSymbol destination = declaration.Parameters.Single(parameter =>
            parameter.Type is INamedTypeSymbol type
            && type.OriginalDefinition.MetadataName == typeof(Span<>).Name
            && type.ContainingNamespace.ToDisplayString() == typeof(Span<>).Namespace
            && type.TypeArguments.Single().SpecialType == SpecialType.System_Byte);
        var calls = method.DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Select(syntax => (Syntax: syntax, Symbol: catalog.Invocation(syntax),
                Operation: (IInvocationOperation)model.GetOperation(syntax)!)).ToArray();
        var copies = calls.Where(call => call.Symbol.Name == nameof(OS.Std.Memory.BufferOperations.MemoryCopy)
            && call.Symbol.ContainingType.ToDisplayString() == typeof(OS.Std.Memory.BufferOperations).FullName
            && call.Symbol.ContainingAssembly.Name == typeof(OS.Std.Memory.BufferOperations).Assembly.GetName().Name).ToArray();
        if (copies.Length != 1)
            return false;

        var copy = copies[0];
        if (!copy.Operation.Arguments.Any(argument => argument.Parameter?.Ordinal == 1
                && argument.Value is IParameterReferenceOperation parameter
                && SymbolEqualityComparer.Default.Equals(parameter.Parameter, destination))
            || calls.Any(call => call.Symbol.Name == "CopyTo" && call.Operation.Arguments.Any(argument =>
                argument.Value is IParameterReferenceOperation parameter
                && SymbolEqualityComparer.Default.Equals(parameter.Parameter, destination)))
            || copy.Syntax.Parent is not EqualsValueClauseSyntax initializer
            || initializer.Parent is not VariableDeclaratorSyntax variable
            || model.GetDeclaredSymbol(variable) is not ILocalSymbol result)
            return false;

        BlockSyntax? body = variable.Ancestors().OfType<BlockSyntax>().FirstOrDefault();
        return body is not null && body.Statements.OfType<IfStatementSyntax>().Any(statement =>
            statement.SpanStart > copy.Syntax.Span.End
            && model.GetOperation(statement.Condition) is IUnaryOperation
            {
                OperatorKind: UnaryOperatorKind.Not,
                Operand: IPropertyReferenceOperation
                {
                    Property.Name: "Succeeded",
                    Instance: ILocalReferenceOperation local
                }
            }
            && SymbolEqualityComparer.Default.Equals(local.Local, result)
            && statement.Statement.DescendantNodesAndSelf().OfType<ReturnStatementSyntax>().Any(returned =>
                returned.Expression?.DescendantNodesAndSelf().OfType<ExpressionSyntax>().Any(expression =>
                    model.GetSymbolInfo(expression).Symbol is IFieldSymbol field
                    && field.Name == "DestinationTooSmall"
                    && field.ContainingType.ToDisplayString() == failureStatus) == true));
    }
}
