using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using OS.Tooling;

/// <summary>Checks the shared-copy dependency and its failure branch in a selected
/// storage method. Behavioral fixtures separately verify copied bytes and bounds.</summary>
internal static class StorageBufferCopySourceGuard
{
    internal static bool Check(CSharpSourceCatalog catalog, string owner, string methodName, string failureStatus,
        string failureMember = "DestinationTooSmall", Func<IOperation, IParameterSymbol, bool>? destinationMatches = null)
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
        destinationMatches ??= HostSourceOperations.IsParameter;
        if (!destinationMatches(HostSourceOperations.Argument(copy.Operation, 1), destination)
            || calls.Any(call => call.Symbol.Name == "CopyTo" && call.Operation.Arguments.Any(argument =>
                argument.Value.DescendantsAndSelf().OfType<IParameterReferenceOperation>()
                    .Any(parameter => SymbolEqualityComparer.Default.Equals(parameter.Parameter, destination))))
            || copy.Syntax.Parent is not EqualsValueClauseSyntax initializer
            || initializer.Parent is not VariableDeclaratorSyntax variable
            || model.GetDeclaredSymbol(variable) is not ILocalSymbol result)
            return false;

        if (variable.Parent is not VariableDeclarationSyntax { Variables.Count: 1 } variables
            || variables.Parent is not LocalDeclarationStatementSyntax localDeclaration
            || localDeclaration.Parent is not BlockSyntax body)
            return false;

        int next = body.Statements.IndexOf(localDeclaration) + 1;
        if (next >= body.Statements.Count || body.Statements[next] is not IfStatementSyntax statement
            || model.GetOperation(statement.Condition) is not IUnaryOperation
            {
                OperatorKind: UnaryOperatorKind.Not,
                Operand: IPropertyReferenceOperation
                {
                    Property.Name: "Succeeded",
                    Instance: ILocalReferenceOperation local
                }
            }
            || !SymbolEqualityComparer.Default.Equals(local.Local, result))
            return false;

        ReturnStatementSyntax? returned = statement.Statement switch
        {
            ReturnStatementSyntax direct => direct,
            BlockSyntax { Statements.Count: 1 } block => block.Statements[0] as ReturnStatementSyntax,
            _ => null
        };
        if (returned?.Expression is null)
            return false;

        IOperation? operation = model.GetOperation(returned.Expression);
        if (operation is IFieldReferenceOperation directStatus)
            return IsFailureStatus(directStatus)
                && SymbolEqualityComparer.Default.Equals(directStatus.Field.Type, declaration.ReturnType);

        return operation is IInvocationOperation factory
            && factory.TargetMethod.IsStatic
            && SymbolEqualityComparer.Default.Equals(factory.TargetMethod.ReturnType, declaration.ReturnType)
            && factory.Arguments.Any(argument => argument.Value is IFieldReferenceOperation status
                && IsFailureStatus(status)
                && SymbolEqualityComparer.Default.Equals(argument.Parameter?.Type, status.Field.Type));

        bool IsFailureStatus(IFieldReferenceOperation status) =>
            status.Field.Name == failureMember
            && status.Field.ContainingType.TypeKind == TypeKind.Enum
            && status.Field.ContainingType.ToDisplayString() == failureStatus;
    }
}
