using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using OS.Tooling;

/// <summary>Restricts the compatibility ledger to scalar/value operations and
/// calls within its inspected owner/value entries. Unknown or implicit runtime
/// operations fail closed; this is a source contract, not emitted-code proof.</summary>
internal static class HostLedgerSourceGuard
{
    internal static void Validate(CSharpSourceCatalog catalog, CSharpSourceDocument document,
        INamedTypeSymbol owner, IReadOnlyList<INamedTypeSymbol> entries)
    {
        var model = catalog.Compilation.GetSemanticModel(document.Syntax);
        if (model.GetDiagnostics().Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
            throw new InvalidOperationException("ledger_binding_error");
        HashSet<IOperation> seen = [];
        foreach (var node in document.Syntax.GetRoot().DescendantNodes(node => node is not AttributeListSyntax))
        {
            if (node is BaseMethodDeclarationSyntax or AccessorDeclarationSyntax
                && model.GetDeclaredSymbol(node) is IMethodSymbol declaration && !ManagedImplementation(declaration))
                throw new InvalidOperationException("ledger_opaque_implementation " + declaration.ToDisplayString());
            if (model.GetOperation(node) is { } root)
                Visit(root);
        }

        bool Owned(INamedTypeSymbol type) => SymbolEqualityComparer.Default.Equals(type, owner)
            || entries.Any(entry => SymbolEqualityComparer.Default.Equals(entry, type));

        void Visit(IOperation operation)
        {
            if (!seen.Add(operation))
                return;
            if (operation.Type is { IsValueType: false })
                throw new InvalidOperationException("ledger_reference_value " + operation.Kind);
            bool allowed = operation switch
            {
                IInvocationOperation call => Owned(call.TargetMethod.ContainingType) && !call.IsVirtual
                    && call.TargetMethod.Arity == 0 && ManagedImplementation(call.TargetMethod),
                IObjectCreationOperation creation => entries.Any(entry => SymbolEqualityComparer.Default.Equals(entry, creation.Type))
                    && creation.Constructor is { } constructor && ManagedImplementation(constructor),
                IPropertyReferenceOperation property => Owned(property.Property.ContainingType) && !property.Property.IsVirtual
                    && new[] { property.Property.GetMethod, property.Property.SetMethod }
                        .Where(accessor => accessor is not null).All(accessor => ManagedImplementation(accessor!)),
                IFieldReferenceOperation field => Owned(field.Field.ContainingType) || field.Field.HasConstantValue,
                IConversionOperation conversion => !conversion.Conversion.IsUserDefined,
                IBinaryOperation binary => binary.OperatorMethod is null,
                IUnaryOperation unary => unary.OperatorMethod is null,
                IBlockOperation or IReturnOperation or ISwitchExpressionOperation or ISwitchExpressionArmOperation
                    or IConstantPatternOperation or IDiscardPatternOperation or IParameterReferenceOperation
                    or ILiteralOperation or IInstanceReferenceOperation or ISimpleAssignmentOperation
                    or IExpressionStatementOperation or IVariableDeclarationGroupOperation or IVariableDeclarationOperation
                    or IVariableDeclaratorOperation or IVariableInitializerOperation or IConditionalOperation
                    or IArgumentOperation or ILocalReferenceOperation or IDefaultValueOperation or IDeclarationExpressionOperation
                    or IConstructorBodyOperation or IMethodBodyOperation or IFieldInitializerOperation => true,
                _ => false
            };
            if (!allowed)
                throw new InvalidOperationException("ledger_runtime_operation " + operation.Kind);
            foreach (var child in operation.ChildOperations)
                Visit(child);
        }
    }

    internal static bool ManagedImplementation(IMethodSymbol method)
    {
        if (method.IsExtern || method.IsAbstract || method.IsVirtual
            || method.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() is
                "System.Runtime.CompilerServices.MethodImplAttribute" or "System.Runtime.InteropServices.DllImportAttribute"
                or "System.Runtime.InteropServices.LibraryImportAttribute" or "System.Runtime.CompilerServices.ModuleInitializerAttribute"))
            return false;
        if (method.IsImplicitlyDeclared && method.MethodKind == MethodKind.Constructor && method.ContainingType.IsValueType)
            return true;
        method = method.PartialImplementationPart ?? method;
        return method.DeclaringSyntaxReferences.Length == 1 && method.DeclaringSyntaxReferences[0].GetSyntax() switch
        {
            BaseMethodDeclarationSyntax declaration => declaration.Body is not null || declaration.ExpressionBody is not null,
            AccessorDeclarationSyntax accessor => accessor.Body is not null || accessor.ExpressionBody is not null
                || method.ContainingType.IsValueType && accessor.Parent?.Parent is PropertyDeclarationSyntax { ExpressionBody: null },
            ArrowExpressionClauseSyntax => true,
            _ => false
        };
    }
}
