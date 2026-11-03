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
        return CountFields(catalog, owner, "GetRequiredBytes",
                expectedFields.Select(field => (field, nameof(AsciiDecimalFormatter.CountUInt64Digits))).ToArray())
            && Append(catalog, owner, "AppendBytes", "AppendBytes")
            && Append(catalog, owner, "AppendUInt64Decimal", "AppendUInt64Decimal");
    }

    internal static bool CountFields(CSharpSourceCatalog catalog, string owner, string sizingMethod,
        IReadOnlyList<(string Field, string Counter)> expectedFields)
    {
        var size = catalog.Method(owner, sizingMethod);
        var model = catalog.Compilation.GetSemanticModel(size.SyntaxTree);
        var symbol = (IMethodSymbol)model.GetDeclaredSymbol(size)!;
        var counts = size.DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Select(call =>
            {
                _ = catalog.Invocation(call);
                return (IInvocationOperation)model.GetOperation(call)!;
            })
            .Where(call => HostSourceOperations.IsMethod(call.TargetMethod, typeof(AsciiDecimalFormatter), call.TargetMethod.Name)).ToArray();
        return symbol.Parameters.Length == 1 && counts.Length == expectedFields.Count && counts.All(ReturnedSum)
            && counts.Select(call => call.TargetMethod.Name + ":" + MemberPath(Argument(call, 0), symbol.Parameters[0]))
                .Order(StringComparer.Ordinal).SequenceEqual(expectedFields.Select(field => field.Counter + ":" + field.Field).Order(StringComparer.Ordinal), StringComparer.Ordinal);

        bool ReturnedSum(IOperation value)
        {
            while (Consumer(value) is IBinaryOperation { OperatorKind: BinaryOperatorKind.Add, OperatorMethod: null } addition)
                value = addition;
            return Consumer(value) is IReturnOperation returned
                && (returned.Syntax is ReturnStatementSyntax statement && statement.Parent == size.Body
                    || size.ExpressionBody is { } expression && expression.Expression.Span.Contains(value.Syntax.Span)
                        && !value.Syntax.Ancestors().TakeWhile(node => node != size).Any(node => node is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax));
        }
    }

    internal static bool Append(CSharpSourceCatalog catalog, string owner, string wrapperMethod, string sharedMethod, bool tryStyle = false)
    {
        var method = catalog.Method(owner, wrapperMethod);
        var semantic = catalog.Compilation.GetSemanticModel(method.SyntaxTree);
        var declaration = (IMethodSymbol)semantic.GetDeclaredSymbol(method)!;
        var calls = HostSourceOperations.Calls(catalog, method, typeof(AsciiTextBuilder), sharedMethod);
        if (calls.Length != 1 || declaration.Parameters.Length != 3 || declaration.Parameters[2].RefKind != RefKind.Ref
            || declaration.ReturnType.SpecialType != (tryStyle ? SpecialType.System_Boolean : SpecialType.System_Void)
            || !IsParameter(Argument(calls[0], 0), declaration.Parameters[1])
            || !IsParameter(Argument(calls[0], 1), declaration.Parameters[2])
            || !IsParameter(Argument(calls[0], 2), declaration.Parameters[0])
            || calls[0].Syntax.Parent is not EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax variable }
            || semantic.GetDeclaredSymbol(variable) is not ILocalSymbol result
            || !HostSourceOperations.IsUnmodifiedLocal(semantic, result, method)
            || variable.Parent?.Parent is not LocalDeclarationStatementSyntax { Declaration.Variables.Count: 1 } local || local.Parent != method.Body
            || method.Body is not { } body || body.Statements.Count != (tryStyle ? 4 : 2)
            || body.Statements[0] != local || body.Statements[1] is not IfStatementSyntax { Else: null } branch)
            return false;

        var condition = semantic.GetOperation(branch.Condition);
        if (tryStyle)
        {
            if (condition is not IUnaryOperation { OperatorKind: UnaryOperatorKind.Not } negated
                || !ResultProperty(negated.Operand, "Succeeded") || !Returns(branch.Statement, false)
                || !Returns(body.Statements[3], true))
                return false;
            return Advances(body.Statements[2]);
        }
        return ResultProperty(condition, "Succeeded") && Advances(branch.Statement);

        bool ResultProperty(IOperation? operation, string name) => operation is IPropertyReferenceOperation
            { Instance: ILocalReferenceOperation localResult } property && property.Property.Name == name
            && SymbolEqualityComparer.Default.Equals(localResult.Local, result)
            && SymbolEqualityComparer.Default.Equals(property.Property.ContainingType, calls[0].TargetMethod.ReturnType);

        bool Advances(StatementSyntax statement)
        {
            if (statement is BlockSyntax { Statements.Count: 1 } block)
                statement = block.Statements[0];
            return statement is ExpressionStatementSyntax expression && semantic.GetOperation(expression.Expression) is ISimpleAssignmentOperation assignment
                && HostSourceOperations.IsParameter(assignment.Target, declaration.Parameters[2])
                && ResultProperty(assignment.Value, "NextOffset");
        }

        bool Returns(StatementSyntax statement, bool value)
        {
            if (statement is BlockSyntax { Statements.Count: 1 } block)
                statement = block.Statements[0];
            return statement is ReturnStatementSyntax { Expression: { } expression }
                && semantic.GetConstantValue(expression) is { HasValue: true, Value: bool returned } && returned == value;
        }
    }

    private static string? MemberPath(IOperation? operation, IParameterSymbol parameter)
    {
        List<string> members = [];
        while (operation is IMemberReferenceOperation { Instance: { } instance } member
            && member is IPropertyReferenceOperation or IFieldReferenceOperation)
        {
            members.Add(member.Member.Name);
            operation = Value(instance);
        }
        members.Reverse();
        return IsParameter(operation, parameter) ? string.Join('.', members) : null;
    }

    private static IOperation? Argument(IInvocationOperation call, int ordinal)
        => Value(call.Arguments.Single(argument => argument.Parameter?.Ordinal == ordinal).Value);

    private static bool IsParameter(IOperation? value, IParameterSymbol parameter)
        => value is IParameterReferenceOperation reference && SymbolEqualityComparer.Default.Equals(reference.Parameter, parameter);

    private static IOperation? Value(IOperation value)
    {
        while (value is IConversionOperation conversion)
        {
            if (!PreservesValue(conversion))
                return null;
            value = conversion.Operand;
        }
        return value;
    }

    private static IOperation? Consumer(IOperation value)
    {
        while (value.Parent is IConversionOperation conversion)
        {
            if (!PreservesValue(conversion))
                return null;
            value = conversion;
        }
        return value.Parent;
    }

    // A syntactically present count/forwarding call must use the original value.
    // Explicit narrowing, floating-point and user conversions cannot establish
    // that contract. Widening integral conversions used by shared builders can.
    private static bool PreservesValue(IConversionOperation conversion)
    {
        if (conversion.Conversion.IsIdentity)
            return true;
        return conversion.Operand.Type is { } source && conversion.Type is { } target
            && Integral(source.SpecialType) && Integral(target.SpecialType)
            && conversion.SemanticModel!.Compilation.ClassifyCommonConversion(source, target) is
                { IsImplicit: true, IsNumeric: true, IsUserDefined: false };
    }

    private static bool Integral(SpecialType type) => type is SpecialType.System_Byte or SpecialType.System_SByte
        or SpecialType.System_Int16 or SpecialType.System_UInt16 or SpecialType.System_Int32 or SpecialType.System_UInt32
        or SpecialType.System_Int64 or SpecialType.System_UInt64 or SpecialType.System_Char;
}
