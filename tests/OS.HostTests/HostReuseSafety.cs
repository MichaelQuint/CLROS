using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using OS.Tooling;

/// <summary>Conservative execution boundary for optional checkpoint reuse.
/// Source dependencies alone cannot prove a runtime call graph. Indirect calls,
/// reflection, external state and unreviewed native methods therefore run fresh.
/// Shared runner code is inspected when a fixture actually calls into it.</summary>
internal sealed class HostReuseSafety(CSharpSourceCatalog catalog)
{
    private readonly bool hasModuleInitializer = catalog.Documents.Any(document =>
        document.Syntax.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Any(method => catalog.Compilation.GetSemanticModel(method.SyntaxTree).GetDeclaredSymbol(method)!
                .GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString()
                    == "System.Runtime.CompilerServices.ModuleInitializerAttribute")));

    internal string? Reason(INamedTypeSymbol fixture, bool ownedArtifacts)
    {
        if (hasModuleInitializer) return "module_initializer";
        HashSet<ISymbol> visited = new(SymbolEqualityComparer.Default);
        Queue<ISymbol> pending = new();
        foreach (var member in fixture.GetMembers()) Add(member);
        while (pending.TryDequeue(out var symbol))
        {
            if (!visited.Add(symbol.OriginalDefinition)) continue;
            if (symbol is IMethodSymbol method && method.IsExtern)
                return "external_method:" + method.ToDisplayString();
            foreach (var reference in symbol.DeclaringSyntaxReferences)
            {
                var syntax = reference.GetSyntax();
                var model = catalog.Compilation.GetSemanticModel(syntax.SyntaxTree);
                var operations = new HashSet<IOperation>();
                foreach (var node in syntax.DescendantNodesAndSelf(node => node is not AttributeListSyntax))
                {
                    if (node is AwaitExpressionSyntax or CommonForEachStatementSyntax or QueryExpressionSyntax
                        or PointerTypeSyntax or FunctionPointerTypeSyntax)
                        return "implicit_dispatch:" + symbol.ToDisplayString();
                    if (node is LiteralExpressionSyntax literal && model.GetConstantValue(literal) is { HasValue: true, Value: string value }
                        && (value.Contains("references/", StringComparison.OrdinalIgnoreCase)
                            || !ownedArtifacts && (value.Contains("artifacts/", StringComparison.OrdinalIgnoreCase)
                                || value.Contains("artifacts\\", StringComparison.OrdinalIgnoreCase) || value == "artifacts")))
                        return "external_path:" + symbol.ToDisplayString();
                    if (model.GetOperation(node) is { } root) Include(root);
                }
                foreach (var operation in operations)
                {
                    if (operation.Type?.TypeKind == TypeKind.Dynamic)
                        return "dynamic_operation:" + operation.Kind;
                    // Conditional user operators can invoke true/false as well
                    // as the selected &, and generic operators can dispatch to
                    // static abstract implementations. Keep this whole family
                    // fresh instead of assuming one visible OperatorMethod is
                    // a complete call graph.
                    if (operation is IBinaryOperation { OperatorMethod: not null }
                        or IUnaryOperation { OperatorMethod: not null }
                        or IConversionOperation { OperatorMethod: not null }
                        or IIncrementOrDecrementOperation { OperatorMethod: not null }
                        or ICompoundAssignmentOperation { OperatorMethod: not null })
                        return "operator_dispatch:" + symbol.ToDisplayString();
                    if (operation is IBinaryOperation { OperatorKind: BinaryOperatorKind.Add, Type.SpecialType: SpecialType.System_String,
                            ConstantValue.HasValue: false }
                        or ICompoundAssignmentOperation { OperatorKind: BinaryOperatorKind.Add, Type.SpecialType: SpecialType.System_String })
                        return "string_concatenation:" + symbol.ToDisplayString();
                    switch (operation)
                    {
                        case IDynamicInvocationOperation or IDynamicMemberReferenceOperation or IDynamicObjectCreationOperation
                            or IFunctionPointerInvocationOperation:
                            return "indirect_dispatch:" + symbol.ToDisplayString();
                        case IUsingOperation or IUsingDeclarationOperation or IDeconstructionAssignmentOperation
                            or ITypeParameterObjectCreationOperation or IWithOperation or IInterpolatedStringHandlerCreationOperation
                            or ICollectionExpressionOperation:
                            return "implicit_dispatch:" + symbol.ToDisplayString();
                        case IInvocationOperation call:
                            var target = call.TargetMethod;
                            if (target.MethodKind == MethodKind.DelegateInvoke || call.IsVirtual || target.IsAbstract || target.IsVirtual
                                || target.ContainingType.TypeKind == TypeKind.Interface
                                || target.Parameters.Any(parameter => parameter.Type.TypeKind == TypeKind.Delegate))
                                return "indirect_dispatch:" + target.ToDisplayString();
                            string? reason = External(target);
                            if (reason is not null) return reason;
                            Add(target.ReducedFrom ?? target);
                            break;
                        case IObjectCreationOperation creation:
                            if (creation.Constructor is { } constructor)
                            {
                                string? construction = External(constructor);
                                if (construction is not null) return construction;
                                Add(constructor);
                            }
                            break;
                        case IPropertyReferenceOperation property:
                            string? access = External(property.Property);
                            if (access is not null) return access;
                            if (property.Property.IsVirtual || property.Property.ContainingType.TypeKind == TypeKind.Interface)
                                return "indirect_property:" + property.Property.ToDisplayString();
                            Add(property.Property);
                            break;
                        case IFieldReferenceOperation field:
                            if (!field.Field.HasConstantValue && External(field.Field) is { } fieldAccess) return fieldAccess;
                            Add(field.Field);
                            break;
                        case IEventReferenceOperation @event:
                            if (External(@event.Event) is { } eventAccess) return eventAccess;
                            Add(@event.Event);
                            break;
                        default:
                            if (!Supported(operation)) return "unsupported_operation:" + operation.Kind;
                            break;
                    }
                }

                void Include(IOperation operation)
                {
                    if (!operations.Add(operation)) return;
                    foreach (var child in operation.ChildOperations) Include(child);
                }
            }
        }
        return null;

        void Add(ISymbol? member)
        {
            if (member is null) return;
            member = member.OriginalDefinition;
            if (member.DeclaringSyntaxReferences.Length != 0 && !visited.Contains(member)) pending.Enqueue(member);
            if (member.ContainingType is not { } owner || owner.DeclaringSyntaxReferences.Length == 0 || visited.Contains(owner)) return;
            // Initializers and base constructors can execute without an explicit
            // invocation syntax in the selected method. Hashing only the caller
            // would miss their external-state dependencies.
            visited.Add(owner);
            foreach (var initializer in owner.GetMembers().Where(item => item is IFieldSymbol or IPropertySymbol or IEventSymbol
                         || item is IMethodSymbol { MethodKind: MethodKind.StaticConstructor or MethodKind.Constructor }))
                if (initializer.DeclaringSyntaxReferences.Length != 0) pending.Enqueue(initializer);
            for (var parent = owner.BaseType; parent is not null; parent = parent.BaseType)
                foreach (var constructor in parent.Constructors) Add(constructor);
        }
    }

    // A referenced binary's hash authenticates its code, not all state that code
    // may read. No namespace, assembly name or reviewed native output owner can
    // substitute for inspecting that implementation. Keep opaque calls fresh.
    private static string? External(ISymbol symbol) => symbol.DeclaringSyntaxReferences.Length == 0
        ? "opaque_metadata:" + symbol.ToDisplayString() : null;

    // Explicitly bounded reuse analysis. New compiler operation forms do not
    // become eligible by omission. These nodes have no hidden invocation beyond
    // their visited children; calls/member access are handled separately above.
    private static bool Supported(IOperation operation) => operation is
        IMethodBodyOperation or IConstructorBodyOperation or IBlockOperation
        or IReturnOperation or ILiteralOperation or ILocalReferenceOperation or IParameterReferenceOperation
        or IVariableDeclarationGroupOperation or IVariableDeclarationOperation or IVariableDeclaratorOperation
        or IVariableInitializerOperation or IFieldInitializerOperation or IPropertyInitializerOperation
        or IExpressionStatementOperation or IArgumentOperation or IParenthesizedOperation
        or IBinaryOperation or IUnaryOperation or IConversionOperation or ISimpleAssignmentOperation
        or ICompoundAssignmentOperation or IIncrementOrDecrementOperation
        or IConditionalOperation or IConditionalAccessOperation or IConditionalAccessInstanceOperation
        or ILoopOperation or IBranchOperation or ISwitchOperation or ISwitchCaseOperation
        or ISingleValueCaseClauseOperation or IDefaultCaseClauseOperation
        or IArrayCreationOperation or IArrayInitializerOperation or IArrayElementReferenceOperation
        or IDefaultValueOperation or ITypeOfOperation or ISizeOfOperation or INameOfOperation
        or IInstanceReferenceOperation or IEmptyOperation;
}
