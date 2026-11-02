using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using OS.Std.Text;
using OS.Tooling;

/// <summary>Checks shared decimal sizing and append operations for a host-inspected
/// formatter. Caller-owned formatter fixtures own output bytes and buffer failures.</summary>
internal static class HostFormatterSourceGuard
{
    internal static bool Check(CSharpSourceCatalog catalog, string owner, IReadOnlyList<string> expectedFields)
    {
        var type = catalog.Type(owner);
        if (type.GetMembers("GetUInt64DecimalByteCount").Length != 0 || type.GetMembers("AppendUInt64").Length != 0)
            return false;
        var size = catalog.Method(owner, "GetRequiredBytes");
        var model = catalog.Compilation.GetSemanticModel(size.SyntaxTree);
        var symbol = (IMethodSymbol)model.GetDeclaredSymbol(size)!;
        var counts = HostSourceOperations.Calls(catalog, size, typeof(AsciiDecimalFormatter), nameof(AsciiDecimalFormatter.CountUInt64Digits));
        if (counts.Length != expectedFields.Count || counts.Any(call => !ReturnedSum(call))
            || !counts.Select(call => PropertyPath(HostSourceOperations.Argument(call, 0), symbol.Parameters[0]))
                .Order(StringComparer.Ordinal).SequenceEqual(expectedFields.Order(StringComparer.Ordinal), StringComparer.Ordinal))
            return false;
        return Append("AppendBytes") && Append("AppendUInt64Decimal");

        bool ReturnedSum(IOperation value)
        {
            while (HostSourceOperations.Consumer(value) is IBinaryOperation { OperatorKind: BinaryOperatorKind.Add } addition)
                value = addition;
            return HostSourceOperations.Consumer(value) is IReturnOperation { Syntax: ReturnStatementSyntax returned } && returned.Parent == size.Body;
        }

        bool Append(string name)
        {
            var method = catalog.Method(owner, name);
            var semantic = catalog.Compilation.GetSemanticModel(method.SyntaxTree);
            var declaration = (IMethodSymbol)semantic.GetDeclaredSymbol(method)!;
            var calls = HostSourceOperations.Calls(catalog, method, typeof(AsciiTextBuilder), name);
            if (calls.Length != 1 || declaration.Parameters.Length != 3
                || !HostSourceOperations.IsParameter(HostSourceOperations.Argument(calls[0], 0), declaration.Parameters[1])
                || !HostSourceOperations.IsParameter(HostSourceOperations.Argument(calls[0], 1), declaration.Parameters[2])
                || !HostSourceOperations.IsParameter(HostSourceOperations.Argument(calls[0], 2), declaration.Parameters[0])
                || calls[0].Syntax.Parent is not EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax variable }
                || semantic.GetDeclaredSymbol(variable) is not ILocalSymbol result
                || !HostSourceOperations.IsUnmodifiedLocal(semantic, result, method)
                || variable.Parent?.Parent is not LocalDeclarationStatementSyntax local || local.Parent is not BlockSyntax body
                || method.DescendantNodes().Any(node => node is ForStatementSyntax or ForEachStatementSyntax or WhileStatementSyntax or DoStatementSyntax
                    || node.IsKind(SyntaxKind.DivideExpression) || node.IsKind(SyntaxKind.ModuloExpression) || node.IsKind(SyntaxKind.DivideAssignmentExpression))
                || method.DescendantNodes().OfType<AssignmentExpressionSyntax>().Any(assignment => assignment.Left is ElementAccessExpressionSyntax element
                    && semantic.GetOperation(element.Expression) is { } target && HostSourceOperations.IsParameter(target, declaration.Parameters[1]))
                || method.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(call => catalog.Invocation(call).Name == "CopyTo"))
                return false;
            int next = body.Statements.IndexOf(local) + 1;
            if (next >= body.Statements.Count || body.Statements[next] is not IfStatementSyntax branch
                || semantic.GetOperation(branch.Condition) is not IPropertyReferenceOperation { Property.Name: "Succeeded", Instance: ILocalReferenceOperation succeeded } condition
                || !SymbolEqualityComparer.Default.Equals(succeeded.Local, result)
                || !SymbolEqualityComparer.Default.Equals(condition.Property.ContainingType, calls[0].TargetMethod.ReturnType))
                return false;
            ExpressionStatementSyntax? expression = branch.Statement switch
            {
                ExpressionStatementSyntax direct => direct,
                BlockSyntax { Statements.Count: 1 } block => block.Statements[0] as ExpressionStatementSyntax,
                _ => null
            };
            return expression is not null && semantic.GetOperation(expression.Expression) is ISimpleAssignmentOperation assignment
                && HostSourceOperations.IsParameter(assignment.Target, declaration.Parameters[2])
                && assignment.Value is IPropertyReferenceOperation { Property.Name: "NextOffset", Instance: ILocalReferenceOperation offset } property
                && SymbolEqualityComparer.Default.Equals(offset.Local, result)
                && SymbolEqualityComparer.Default.Equals(property.Property.ContainingType, calls[0].TargetMethod.ReturnType);
        }
    }

    private static string? PropertyPath(IOperation operation, IParameterSymbol parameter)
    {
        List<string> members = [];
        while (operation is IPropertyReferenceOperation { Instance: { } instance } property)
        {
            members.Add(property.Property.Name);
            operation = HostSourceOperations.Value(instance);
        }
        members.Reverse();
        return HostSourceOperations.IsParameter(operation, parameter) ? string.Join('.', members) : null;
    }
}
