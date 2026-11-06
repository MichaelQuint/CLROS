using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using OS.Storage;
using OS.Tooling;

/// <summary>Checks the loader's shared GPT selection and its success/not-found
/// routing. Existing disk fixtures verify GPT parsing and the selected partition.</summary>
internal static class StorageGptSelectorSourceGuard
{
    internal static bool Check(CSharpSourceCatalog catalog, string owner, string name)
    {
        var method = catalog.Method(owner, name);
        var model = catalog.Compilation.GetSemanticModel(method.SyntaxTree);
        var calls = HostSourceOperations.Calls(catalog, method, typeof(GptPartitionSelector), nameof(GptPartitionSelector.SelectByPartitionTypeGuid));
        if (calls.Length != 1 || HostSourceOperations.Argument(calls[0], 2) is not IFieldReferenceOperation guid
            || guid.Field.Name != nameof(GptParser.EfiSystemPartitionTypeGuid)
            || guid.Field.ContainingType.ToDisplayString() != typeof(GptParser).FullName
            || guid.Field.ContainingAssembly.Name != typeof(GptParser).Assembly.GetName().Name
            || calls[0].Syntax.Parent is not EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax variable }
            || model.GetDeclaredSymbol(variable) is not ILocalSymbol status)
            return false;
        IOperation selected = HostSourceOperations.Argument(calls[0], 3);
        if (selected is IDeclarationExpressionOperation declaration)
            selected = declaration.Expression;
        if (selected is not ILocalReferenceOperation result || !HostSourceOperations.IsUnmodifiedLocal(model, result.Local, method))
            return false;
        bool success = false, notFound = false;
        foreach (var branch in method.DescendantNodes().OfType<IfStatementSyntax>())
        {
            var condition = model.GetOperation(branch.Condition);
            if (condition is IBinaryOperation { OperatorKind: BinaryOperatorKind.ConditionalAnd } conjunction
                && EqualsStatus(conjunction.LeftOperand, nameof(GptPartitionSelectorStatus.Success))
                && conjunction.RightOperand is IPropertyReferenceOperation { Property.Name: "Succeeded", Instance: ILocalReferenceOperation succeeded }
                && SymbolEqualityComparer.Default.Equals(succeeded.Local, result.Local)
                && ReturnedFactory(branch, nameof(GptEspLocationReadinessResult.Success)) is { } factory
                && HostSourceOperations.Argument(factory, 1) is IPropertyReferenceOperation { Property.Name: "Descriptor", Instance: ILocalReferenceOperation descriptor }
                && SymbolEqualityComparer.Default.Equals(descriptor.Local, result.Local))
                success = true;
            if (condition is not null && EqualsStatus(condition, nameof(GptPartitionSelectorStatus.NotFound))
                && ReturnedFactory(branch, nameof(GptEspLocationReadinessResult.FailMissingEsp)) is not null)
                notFound = true;
        }
        return success && notFound;

        bool EqualsStatus(IOperation operation, string expected)
        {
            if (operation is not IBinaryOperation { OperatorKind: BinaryOperatorKind.Equals } equality)
                return false;
            return Pair(equality.LeftOperand, equality.RightOperand) || Pair(equality.RightOperand, equality.LeftOperand);
            bool Pair(IOperation left, IOperation right) => HostSourceOperations.Value(left) is ILocalReferenceOperation local
                && SymbolEqualityComparer.Default.Equals(local.Local, status)
                && HostSourceOperations.LocalInitializer(model, local) is not null
                && HostSourceOperations.Value(right) is IFieldReferenceOperation field && field.Field.Name == expected
                && SymbolEqualityComparer.Default.Equals(field.Field.ContainingType, calls[0].TargetMethod.ReturnType);
        }

        IInvocationOperation? ReturnedFactory(IfStatementSyntax branch, string expected)
        {
            ReturnStatementSyntax? returned = branch.Statement switch
            {
                ReturnStatementSyntax direct => direct,
                BlockSyntax { Statements.Count: 1 } block => block.Statements[0] as ReturnStatementSyntax,
                _ => null
            };
            return returned?.Expression is not null && model.GetOperation(returned.Expression) is IInvocationOperation factory
                && HostSourceOperations.IsMethod(factory.TargetMethod, typeof(GptEspLocationReadinessResult), expected) ? factory : null;
        }
    }
}
