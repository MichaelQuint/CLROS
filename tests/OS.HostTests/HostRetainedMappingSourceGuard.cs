using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using OS.Tooling;

/// <summary>Checks the two retained-device append sites by method, parameter,
/// local-array and index identity. This does not establish general pointer-alias
/// or control-flow correctness outside the selected append statements.</summary>
internal static class HostRetainedMappingSourceGuard
{
    internal static bool Check(CSharpSourceCatalog source, string fieldName)
    {
        if (fieldName is not ("EcamStart" or "VirtioBarStart"))
            return false;
        var method = source.Method("OS.Boot.FirstExitRetainedMappings", "WriteRangesCore");
        var model = source.Compilation.GetSemanticModel(method.SyntaxTree);
        var owner = model.GetDeclaredSymbol(method)!;
        var references = source.Type("OS.Boot.FirstExitImageReferences");
        var field = references.GetMembers(fieldName).OfType<IFieldSymbol>().SingleOrDefault();
        var parameter = owner.Parameters.Single(item => Same(item.Type, references));
        if (field is null || !HostSourceOperations.IsUnmodifiedParameter(model, parameter, method))
            return false;
        var writes = method.DescendantNodes().OfType<AssignmentExpressionSyntax>()
            .Where(write => Same(model.GetSymbolInfo(write.Right).Symbol, field)).ToArray();
        if (writes.Length != 1 || writes[0] is not { Left: ElementAccessExpressionSyntax start, Right: MemberAccessExpressionSyntax value } write
            || !Same(model.GetSymbolInfo(value.Expression).Symbol, parameter)
            || start.ArgumentList.Arguments.Count != 1
            || model.GetSymbolInfo(start.Expression).Symbol is not ILocalSymbol startArray
            || model.GetSymbolInfo(start.ArgumentList.Arguments[0].Expression).Symbol is not ILocalSymbol index
            || !Same(index.ContainingSymbol, owner)
            || write.Parent is not ExpressionStatementSyntax statement || statement.Parent is not BlockSyntax block
            || block.Statements.IndexOf(statement) + 1 >= block.Statements.Count
            || block.Statements[block.Statements.IndexOf(statement) + 1] is not ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax lengthWrite }
            || lengthWrite.Left is not ElementAccessExpressionSyntax length
            || length.ArgumentList.Arguments.Count != 1
            || model.GetSymbolInfo(length.Expression).Symbol is not ILocalSymbol lengthArray
            || Same(startArray, lengthArray)
            || length.ArgumentList.Arguments[0].Expression is not PostfixUnaryExpressionSyntax increment
            || !increment.IsKind(SyntaxKind.PostIncrementExpression)
            || !Same(model.GetSymbolInfo(increment.Operand).Symbol, index)
            || statement.Ancestors().TakeWhile(node => node != method).Any(node => node is LocalFunctionStatementSyntax or AnonymousFunctionExpressionSyntax)
            || model.AnalyzeControlFlow(statement) is not { Succeeded: true, StartPointIsReachable: true })
            return false;
        var capacity = source.Type("OS.Boot.FirstExitRetainedMappings").GetMembers("MaximumAllocations").OfType<IFieldSymbol>().Single();
        if (!Array(startArray) || !Array(lengthArray))
            return false;
        // Retain the independent old rejection of a fixed device index that
        // discards package/image/clock ownership accumulated earlier.
        return !method.DescendantNodes().OfType<AssignmentExpressionSyntax>().Any(assignment =>
            Same(model.GetSymbolInfo(assignment.Left).Symbol, index)
            && model.GetConstantValue(assignment.Right) is { HasValue: true, Value: 8 });

        bool Array(ILocalSymbol local)
        {
            if (!Same(local.ContainingSymbol, owner)
                || local.Type is not IPointerTypeSymbol { PointedAtType.SpecialType: SpecialType.System_UInt64 }
                || local.DeclaringSyntaxReferences.Single().GetSyntax() is not VariableDeclaratorSyntax
                { Initializer.Value: StackAllocArrayCreationExpressionSyntax { Type: ArrayTypeSyntax array } }
                || array.RankSpecifiers.Count != 1 || array.RankSpecifiers[0].Sizes.Count != 1
                || !Same(model.GetSymbolInfo(array.RankSpecifiers[0].Sizes[0]).Symbol, capacity))
                return false;
            // Writes to array elements are expected. Replacing the pointer
            // variable or taking its address/ref defeats original-local identity.
            foreach (var identifier in method.DescendantNodes().OfType<IdentifierNameSyntax>())
            {
                if (!Same(model.GetSymbolInfo(identifier).Symbol, local))
                    continue;
                SyntaxNode value = identifier;
                while (value.Parent is ParenthesizedExpressionSyntax parent)
                    value = parent;
                if (value.Parent is AssignmentExpressionSyntax assignment && assignment.Left == value
                    || value.Parent is PrefixUnaryExpressionSyntax prefix && prefix.Kind() is SyntaxKind.AddressOfExpression or SyntaxKind.PreIncrementExpression or SyntaxKind.PreDecrementExpression
                    || value.Parent is PostfixUnaryExpressionSyntax postfix && postfix.Kind() is SyntaxKind.PostIncrementExpression or SyntaxKind.PostDecrementExpression
                    || value.Parent is RefExpressionSyntax
                    || value.Parent is ArgumentSyntax argument && argument.RefKindKeyword.Kind() is SyntaxKind.RefKeyword or SyntaxKind.OutKeyword)
                    return false;
            }
            return true;
        }
    }

    private static bool Same(ISymbol? first, ISymbol? second) => first is not null && second is not null
        && SymbolEqualityComparer.Default.Equals(first, second);
}
