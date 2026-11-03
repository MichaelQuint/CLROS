using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using OS.Tooling;

/// <summary>Checks the original image and registered-storage data flow by bound
/// symbols. File placement, aliases and local names do not supply authority.
/// This host source check does not replace mapped-buffer or native-code tests.</summary>
internal static class HostTypeManagerSourceGuard
{
    internal const string Adapter = "OS.Boot.EfiEntry+M01StageOneAdapter";
    internal const string Probe = "OS.Runtime.NativeAotModuleRegistrationProbe";
    private const string Core = "OS.Kernel.Runtime.TypeManagerModuleMetadataLiveNativeAOTImage";

    internal static bool OriginalImage(CSharpSourceCatalog catalog)
    {
        var capture = catalog.Method(Adapter, "TryGetOriginalTypeManagerImage");
        var model = catalog.Compilation.GetSemanticModel(capture.SyntaxTree);
        var symbol = model.GetDeclaredSymbol(capture)!;
        if (symbol.Parameters.Length != 2 || symbol.Parameters.Any(parameter => parameter.RefKind != RefKind.Out))
            return false;
        foreach (var identifier in capture.DescendantNodes().OfType<IdentifierNameSyntax>())
            if (model.GetSymbolInfo(identifier).Symbol is IParameterSymbol parameter
                && symbol.Parameters.Any(expected => Same(expected, parameter))
                && identifier.Ancestors().TakeWhile(node => node != capture).Any(node => node is RefExpressionSyntax
                    || node is ArgumentSyntax argument && argument.RefKindKeyword.Kind() is SyntaxKind.RefKeyword or SyntaxKind.OutKeyword))
                return false;
        var returns = capture.DescendantNodes().OfType<ReturnStatementSyntax>().ToArray();
        if (capture.Body?.Statements.LastOrDefault() is not ReturnStatementSyntax success
            || returns.Any(statement => statement != success && model.GetConstantValue(statement.Expression!) is not { HasValue: true, Value: false })
            || model.GetConstantValue(success.Expression!) is not { HasValue: true, Value: true })
            return false;
        string[] fields = ["LoadedImageCapture", "LoadedImageVirtualMapping"];
        for (int index = 0; index < symbol.Parameters.Length; index++)
        {
            var writes = capture.DescendantNodes().OfType<AssignmentExpressionSyntax>()
                .Select(syntax => model.GetOperation(syntax)).OfType<ISimpleAssignmentOperation>()
                .Where(assignment => HostSourceOperations.IsParameter(assignment.Target, symbol.Parameters[index])).ToArray();
            if (writes.Length == 0 || Value(writes[^1].Value) is not ILocalReferenceOperation local
                || !ScratchMember(HostSourceOperations.LocalInitializer(model, local), fields[index])
                || writes[^1].Syntax.Parent is not ExpressionStatementSyntax { Parent: BlockSyntax body }
                || body != capture.Body)
                return false;
        }

        bool ScratchMember(IOperation? operation, string name)
        {
            var (member, instance) = operation switch
            {
                IPropertyReferenceOperation property => (property.Property.Name, property.Instance),
                IFieldReferenceOperation field => (field.Field.Name, field.Instance),
                _ => (null, null)
            };
            return member == name && instance is IFieldReferenceOperation scratch
                && scratch.Field.Name == "s_evidenceScratch" && scratch.Field.IsStatic
                && Same(scratch.Field.ContainingType, catalog.Type(Adapter));
        }

        var continuation = catalog.Method(Adapter, "M01StageOneContinuation");
        var captures = Calls(catalog, continuation, Adapter, "TryGetOriginalTypeManagerImage");
        var runs = Calls(catalog, continuation, Probe, "Run");
        if (captures.Length != 1 || runs.Length != 1 || captures[0].Syntax.SpanStart >= runs[0].Syntax.SpanStart
            || !Active(captures[0].Syntax, continuation) || !Active(runs[0].Syntax, continuation))
            return false;
        for (int index = 0; index < symbol.Parameters.Length; index++)
        {
            if (Value(Argument(captures[0], index)) is not ILocalReferenceOperation output
                || !Local(Argument(runs[0], index), output.Local))
                return false;
            var callerModel = catalog.Compilation.GetSemanticModel(continuation.SyntaxTree);
            if (continuation.DescendantNodes().OfType<AssignmentExpressionSyntax>()
                .Select(syntax => callerModel.GetOperation(syntax)).OfType<ISimpleAssignmentOperation>()
                .Any(assignment => Local(assignment.Target, output.Local)))
                return false;
            foreach (var identifier in continuation.DescendantNodes().OfType<IdentifierNameSyntax>())
            {
                if (!SymbolEqualityComparer.Default.Equals(callerModel.GetSymbolInfo(identifier).Symbol, output.Local)
                    || captures[0].Syntax.Span.Contains(identifier.Span))
                    continue;
                if (identifier.Ancestors().TakeWhile(node => node != continuation).Any(node => node is RefExpressionSyntax
                    || node is AssignmentExpressionSyntax write && write.Left.Span.Contains(identifier.Span)
                    || node is ArgumentSyntax argument && argument.RefKindKeyword.Kind() is SyntaxKind.RefKeyword or SyntaxKind.OutKeyword))
                    return false;
            }
        }
        return true;
    }

    internal static bool RegisteredStorage(CSharpSourceCatalog catalog)
    {
        var method = catalog.Method(Probe, "Execute");
        var model = catalog.Compilation.GetSemanticModel(method.SyntaxTree);
        var parameters = model.GetDeclaredSymbol(method)!.Parameters;
        if (parameters.Length != 5)
            return false;
        var registrations = Calls(catalog, method, Core, "RegisterOnce");
        bool Inputs(IInvocationOperation call, int count) => Enumerable.Range(0, count)
            .All(index => HostSourceOperations.IsParameter(Argument(call, index), parameters[index]));
        var full = registrations.Where(call => Inputs(call, 4) && InitializerLocal(model, call) is not null).ToArray();
        if (full.Length != 1 || InitializerLocal(model, full[0]) is not { } registered
            || !HostSourceOperations.IsUnmodifiedLocal(model, registered, method))
            return false;
        bool Short(IInvocationOperation call)
        {
            if (!Inputs(call, 3) || Value(Argument(call, 3)) is not IBinaryOperation
                { OperatorKind: BinaryOperatorKind.Subtract } subtract || !Number(subtract.RightOperand, 1)
                || !Member(subtract.LeftOperand, "RequiredStorage", out var valid)
                || HostSourceOperations.LocalInitializer(model, valid) is not IInvocationOperation validate)
                return false;
            return IsCall(catalog, validate, Core, "Validate") && Inputs(validate, 2);
        }
        if (registrations.Count(Short) != 1)
            return false;

        var sections = Calls(catalog, method, Core, "TryGetSection");
        var slots = sections.Where(call => Inputs(call, 4) && Number(Argument(call, 5), 204)
            && Member(Argument(call, 4), "Handle", out var result) && Same(result.Local, registered)).ToArray();
        if (slots.Length != 1 || InitializerLocal(model, slots[0]) is not { } slot
            || !HostSourceOperations.IsUnmodifiedLocal(model, slot, method))
            return false;
        var reads = Calls(catalog, method, Probe, "ReadWord").Where(call =>
            Member(Argument(call, 0), "Section", out var source) && Same(source.Local, slot)
            && InitializerLocal(model, call) is not null).ToArray();
        if (reads.Length != 1 || InitializerLocal(model, reads[0]) is not { } consumer
            || !HostSourceOperations.IsUnmodifiedLocal(model, consumer, method))
            return false;
        var consumed = sections.Where(call => Local(Argument(call, 4), consumer)).ToArray();
        ulong[] required = [0, 202, 204, 213];
        if (consumed.Length != required.Length || consumed.Any(call => !Inputs(call, 4))
            || required.Any(id => consumed.Count(call => Number(Argument(call, 5), id)) != 1))
            return false;

        var canaries = Calls(catalog, method, Probe, "Canary");
        if (canaries.Length != 1 || canaries[0].Syntax.Parent is not AssignmentExpressionSyntax assignment
            || assignment.Left is not ElementAccessExpressionSyntax destination
            || model.GetOperation(destination.Expression) is not { } storage
            || !HostSourceOperations.IsParameter(storage, parameters[2]))
            return false;
        bool Rejects(IfStatementSyntax guard)
        {
            if (guard.Parent != method.Body || guard.SpanStart <= full[0].Syntax.SpanStart
                || guard.Span.End >= assignment.SpanStart || model.GetOperation(guard.Condition) is not { } condition)
                return false;
            var rejection = guard.Statement is BlockSyntax { Statements.Count: 1 } block ? block.Statements[0] : guard.Statement;
            return rejection is ReturnStatementSyntax { Expression: { } expression }
                && model.GetOperation(expression) is IInvocationOperation reject
                && IsCall(catalog, reject, Probe, "Reject")
                && Disjuncts(condition).Any(term => term is IUnaryOperation { OperatorKind: UnaryOperatorKind.Not } not
                    && Member(not.Operand, "Succeeded", out var result) && Same(result.Local, registered));
        }
        return method.Body!.Statements.OfType<IfStatementSyntax>().Any(Rejects)
            && Bounded(catalog);
    }

    private static bool Bounded(CSharpSourceCatalog catalog)
    {
        var owner = catalog.Type(Probe);
        foreach (var declaration in owner.DeclaringSyntaxReferences.Select(reference => reference.GetSyntax()))
        {
            var model = catalog.Compilation.GetSemanticModel(declaration.SyntaxTree);
            foreach (var node in declaration.DescendantNodes())
            {
                if (node is ThrowStatementSyntax or ThrowExpressionSyntax or FunctionPointerTypeSyntax)
                    return false;
                if (node is TypeSyntax type && model.GetTypeInfo(type).Type is { } bound
                    && (bound is IArrayTypeSymbol || bound.SpecialType == SpecialType.System_String
                        || bound.ContainingNamespace?.ToDisplayString().StartsWith("System.Reflection", StringComparison.Ordinal) == true
                        || bound.ToDisplayString() == "System.Runtime.InteropServices.Marshal"))
                    return false;
                if (node is InvocationExpressionSyntax call && catalog.Invocation(call).Name is
                    "AllocatePool" or "AllocatePages" or "RhCreateTypeManager" or "RhGetModuleSection")
                    return false;
            }
        }
        return new[] { "ReadWord", "ReadAbi" }.All(name => owner.GetMembers(name).OfType<IMethodSymbol>().Single()
            .GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() == "System.Runtime.CompilerServices.MethodImplAttribute"
                && attribute.ConstructorArguments.Length == 1 && attribute.ConstructorArguments[0].Value is int options && (options & 8) != 0));
    }

    private static IEnumerable<IOperation> Disjuncts(IOperation operation) => Value(operation) is IBinaryOperation
        { OperatorKind: BinaryOperatorKind.ConditionalOr } binary ? Disjuncts(binary.LeftOperand).Concat(Disjuncts(binary.RightOperand)) : [Value(operation)];
    private static bool Active(SyntaxNode node, MethodDeclarationSyntax method) =>
        !node.Ancestors().TakeWhile(parent => parent != method).Any(parent => parent is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax);
    private static ILocalSymbol? InitializerLocal(SemanticModel model, IInvocationOperation call) =>
        call.Syntax.Parent is EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax declaration }
            ? model.GetDeclaredSymbol(declaration) as ILocalSymbol : null;
    private static bool Member(IOperation operation, string name, out ILocalReferenceOperation local)
    {
        var (member, instance) = Value(operation) switch
        {
            IPropertyReferenceOperation property => (property.Property.Name, property.Instance),
            IFieldReferenceOperation field => (field.Field.Name, field.Instance),
            _ => (null, null)
        };
        local = instance is null ? null! : Value(instance) as ILocalReferenceOperation ?? null!;
        return member == name && local is not null;
    }
    private static bool Number(IOperation operation, ulong expected) => operation.ConstantValue.HasValue
        && operation.ConstantValue.Value is not null && Convert.ToUInt64(operation.ConstantValue.Value) == expected;
    private static bool Local(IOperation operation, ILocalSymbol local) => Value(operation) is ILocalReferenceOperation reference && Same(reference.Local, local);
    private static bool Same(ISymbol first, ISymbol second) => SymbolEqualityComparer.Default.Equals(first, second);
    private static IOperation Value(IOperation operation) => HostSourceOperations.Value(operation) is IDeclarationExpressionOperation declaration
        ? HostSourceOperations.Value(declaration.Expression) : HostSourceOperations.Value(operation);
    private static IOperation Argument(IInvocationOperation call, int ordinal) => HostSourceOperations.Argument(call, ordinal);
    private static bool IsCall(CSharpSourceCatalog catalog, IInvocationOperation call, string owner, string name) =>
        call.TargetMethod.Name == name && Same(call.TargetMethod.ContainingType, catalog.Type(owner));
    private static IInvocationOperation[] Calls(CSharpSourceCatalog catalog, MethodDeclarationSyntax method, string owner, string name) =>
        catalog.MethodInvocations(method).Where(call => IsCall(catalog, call, owner, name)).ToArray();
}
