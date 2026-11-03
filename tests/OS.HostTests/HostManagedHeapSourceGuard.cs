using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using OS.Tooling;

/// <summary>Checks the selected fixed-page adapter's storage and call wiring.
/// Compiler ownership replaces file/region spelling; the existing mapped-buffer
/// fixture independently checks allocation, retained failures and ledger bytes.</summary>
internal static class HostManagedHeapSourceGuard
{
    internal const string Owner = "OS.Boot.EfiEntry+M04ManagedHeapAdapter";
    private const string StageOne = "OS.Boot.EfiEntry+M01StageOneAdapter";
    private const string StageTwo = "OS.Boot.EfiEntry+M01StageTwoAdapter";
    private const string Core = "OS.Kernel.Runtime.ManagedHeapPageRangeLiveAllocator";

    internal static bool Wiring(CSharpSourceCatalog catalog)
    {
        var initialize = catalog.Method(StageTwo, "RunAfterStageOne");
        var calls = catalog.MethodInvocations(initialize);
        var checkpoint = calls.Single(call => Is(call, Owner, "Checkpoint"));
        var completed = calls.Single(call => Is(call, StageTwo, "ValidateCompletedResult"));
        var model = catalog.Compilation.GetSemanticModel(initialize.SyntaxTree);
        var heap = catalog.Type(StageTwo).GetMembers("s_heapState").OfType<IFieldSymbol>().Single();
        if (!heap.IsStatic || !FixedStorage(catalog, checkpoint, heap)
            || !Boolean(HostSourceOperations.Argument(checkpoint, 0), true)
            || completed.Syntax.SpanStart >= checkpoint.Syntax.SpanStart
            || calls.Any(call => Is(call, StageTwo, "Fail") && call.Syntax.SpanStart > checkpoint.Syntax.SpanStart)
            || calls.Any(call => Is(call, "OS.Boot.EfiEntry+M01StageThreeAdapter", "RunAfterStageTwo")))
            return false;
        var gate = checkpoint.Syntax.Ancestors().OfType<IfStatementSyntax>().First();
        if (model.GetOperation(gate.Condition) is not IUnaryOperation
                { OperatorKind: UnaryOperatorKind.Not, Operand: IInvocationOperation condition }
            || !SameSyntax(condition.Syntax, checkpoint.Syntax) || !FalseReturn(gate.Statement))
            return false;

        var retain = catalog.Method(StageTwo, "ValidateManagedHeapBeforeBootPlan");
        var retained = catalog.MethodInvocations(retain).Single(call => Is(call, Owner, "Checkpoint"));
        if (!FixedStorage(catalog, retained, heap) || !Boolean(HostSourceOperations.Argument(retained, 0), false)
            || retained.Syntax.Parent is not ReturnStatementSyntax)
            return false;
        var continuation = catalog.Method(StageOne, "M01StageOneContinuation");
        var later = catalog.MethodInvocations(continuation);
        var validation = later.Single(call => Is(call, StageTwo, "ValidateManagedHeapBeforeBootPlan"));
        var plan = later.Single(call => Is(call, "OS.Boot.EfiEntry", "WriteBootSequencePlan"));
        var retainedGate = validation.Syntax.Ancestors().OfType<IfStatementSyntax>().FirstOrDefault();
        var retainedModel = catalog.Compilation.GetSemanticModel(continuation.SyntaxTree);
        if (retainedGate is null || retainedModel.GetOperation(retainedGate.Condition) is not IUnaryOperation
                { OperatorKind: UnaryOperatorKind.Not, Operand: IInvocationOperation checkedResult }
            || !SameSyntax(checkedResult.Syntax, validation.Syntax))
            return false;
        var stop = retainedGate.Statement is BlockSyntax { Statements.Count: 1 } block ? block.Statements[0] : retainedGate.Statement;
        if (stop is not ExpressionStatementSyntax statement
            || retainedModel.GetOperation(statement.Expression) is not IInvocationOperation spin
            || !Is(spin, StageOne, "Spin"))
            return false;
        return Active(catalog, validation, continuation) && Active(catalog, plan, continuation)
            && validation.Syntax.SpanStart < plan.Syntax.SpanStart;
    }

    internal static bool OriginalAuthority(CSharpSourceCatalog catalog)
    {
        var checkpoint = catalog.Method(Owner, "Checkpoint");
        var model = catalog.Compilation.GetSemanticModel(checkpoint.SyntaxTree);
        var parameters = model.GetDeclaredSymbol(checkpoint)!.Parameters;
        // Taking the address of an original pointee field is required here.
        // Taking the address/ref of the pointer parameter itself can replace it.
        var escapes = checkpoint.DescendantNodes().Select(node => node switch
        {
            PrefixUnaryExpressionSyntax unary when unary.Kind() is SyntaxKind.AddressOfExpression
                or SyntaxKind.PreIncrementExpression or SyntaxKind.PreDecrementExpression => unary.Operand,
            PostfixUnaryExpressionSyntax unary => unary.Operand,
            RefExpressionSyntax reference => reference.Expression,
            ArgumentSyntax argument when argument.RefKindKeyword.Kind() is SyntaxKind.RefKeyword or SyntaxKind.OutKeyword => argument.Expression,
            _ => null
        }).Where(expression => expression is not null);
        if (escapes.Any(expression => model.GetSymbolInfo(expression!).Symbol is IParameterSymbol parameter
            && parameters.Any(expected => Same(expected, parameter))))
            return false;
        var state = parameters[1];
        var calls = catalog.MethodInvocations(checkpoint);
        foreach (string operation in new[] { "ReserveAndInitializeOnce", "ValidateRetained" })
        {
            var call = calls.Single(candidate => Is(candidate, Core, operation));
            if (!OriginalField(HostSourceOperations.Argument(call, 0), "AllocatorStateValue")
                || !OriginalField(HostSourceOperations.Argument(call, 2), "FreeNodeValue"))
                return false;
        }
        foreach (var reference in catalog.Type(Owner).DeclaringSyntaxReferences)
        {
            var declaration = reference.GetSyntax();
            var semantic = catalog.Compilation.GetSemanticModel(declaration.SyntaxTree);
            foreach (var method in declaration.DescendantNodes().OfType<MethodDeclarationSyntax>())
                if (catalog.MethodInvocations(method).Any(call => call.TargetMethod.Name is "FreeOnePage" or "FreePages"))
                    return false;
            foreach (var name in declaration.DescendantNodes().OfType<SimpleNameSyntax>())
                if (semantic.GetSymbolInfo(name).Symbol is IPropertySymbol or IFieldSymbol
                    && name.Identifier.ValueText == "AllocatedPageStart")
                    return false;
            foreach (var assignment in declaration.DescendantNodes().OfType<AssignmentExpressionSyntax>())
            {
                var target = semantic.GetOperation(assignment.Left);
                if (target is ILocalReferenceOperation)
                    continue;
                // The packed diagnostic writer consumes a scalar byte count.
                // Its value parameter is scratch, not pointed-to heap authority.
                if (target is IParameterReferenceOperation { Parameter.RefKind: RefKind.None } parameter
                    && parameter.Type?.SpecialType is SpecialType.System_Int32 or SpecialType.System_UInt32
                        or SpecialType.System_Int64 or SpecialType.System_UInt64)
                    continue;
                if (target is not IFieldReferenceOperation { Field.IsStatic: true } field
                    || !Same(field.Field.ContainingType, catalog.Type(Owner))
                    || field.Field.Name != "s_reservationObserved")
                    return false;
            }
        }
        return true;

        bool OriginalField(IOperation argument, string name)
        {
            if (HostSourceOperations.LocalInitializer(model, argument) is not IAddressOfOperation
                { Reference: IFieldReferenceOperation field }
                || field.Syntax is not MemberAccessExpressionSyntax access || !access.IsKind(SyntaxKind.PointerMemberAccessExpression)
                || field.Field.Name != name || !Same(model.GetSymbolInfo(access.Expression).Symbol!, state))
                return false;
            return state.Type is IPointerTypeSymbol pointer && Same(pointer.PointedAtType, field.Field.ContainingType);
        }
    }

    // Use the actual selected partial declarations. Only the two allocator calls
    // are replaced by the independent failure-injection shims in the old fixture.
    internal static IReadOnlyList<string> AdapterSources(CSharpSourceCatalog catalog)
    {
        var parts = catalog.Type(Owner).DeclaringSyntaxReferences.Select(reference => reference.GetSyntax()).ToArray();
        var text = new List<string>();
        foreach (var part in parts)
        {
            var calls = part.DescendantNodes().OfType<InvocationExpressionSyntax>().Where(call =>
                catalog.Invocation(call) is { } method && method.ContainingType.ToDisplayString() == Core
                    && method.Name is "ReserveAndInitializeOnce" or "ValidateRetained").ToArray();
            var replacement = part.ReplaceNodes(calls, (before, rewritten) => rewritten.WithExpression(
                SyntaxFactory.IdentifierName(catalog.Invocation(before).Name == "ReserveAndInitializeOnce" ? "Reserve" : "Retain")));
            // Keep each declaration's import scope separate. Aliases in two
            // partial files may intentionally have different meanings.
            MemberDeclarationSyntax wrapped = SyntaxFactory.ClassDeclaration("Program")
                .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.UnsafeKeyword),
                    SyntaxFactory.Token(SyntaxKind.StaticKeyword), SyntaxFactory.Token(SyntaxKind.PartialKeyword)))
                .WithMembers(SyntaxFactory.SingletonList((MemberDeclarationSyntax)replacement));
            foreach (var space in part.Ancestors().OfType<BaseNamespaceDeclarationSyntax>())
                wrapped = space.WithMembers(SyntaxFactory.SingletonList(wrapped));
            var root = part.SyntaxTree.GetCompilationUnitRoot();
            var output = SyntaxFactory.CompilationUnit().WithExterns(root.Externs).WithUsings(root.Usings)
                .WithMembers(SyntaxFactory.SingletonList(wrapped));
            // Emit selected active syntax only, without conditional directives
            // or disabled text from the original EFI document.
            output = output.ReplaceTrivia(output.DescendantTrivia(descendIntoTrivia: true),
                (_, _) => default);
            text.Add(output.NormalizeWhitespace().ToFullString());
        }
        return text;
    }

    private static bool FixedStorage(CSharpSourceCatalog catalog, IInvocationOperation call, IFieldSymbol expected)
    {
        var method = call.Syntax.Ancestors().OfType<MethodDeclarationSyntax>().First();
        var model = catalog.Compilation.GetSemanticModel(method.SyntaxTree);
        if (!Active(catalog, call, method) || HostSourceOperations.Argument(call, 4) is not ILocalReferenceOperation local
            || !HostSourceOperations.IsUnmodifiedLocal(model, local.Local, method)
            || local.Local.DeclaringSyntaxReferences.Single().GetSyntax() is not VariableDeclaratorSyntax variable
            || variable.Ancestors().OfType<FixedStatementSyntax>().FirstOrDefault() is not { } pinned
            || !call.Syntax.Ancestors().Contains(pinned) || variable.Initializer is null
            || model.GetOperation(variable.Initializer.Value) is not IAddressOfOperation { Reference: IFieldReferenceOperation field })
            return false;
        return Same(field.Field, expected);
    }

    private static bool Active(CSharpSourceCatalog catalog, IInvocationOperation call, MethodDeclarationSyntax method) =>
        !call.Syntax.Ancestors().TakeWhile(node => node != method)
            .Any(node => node is LocalFunctionStatementSyntax or AnonymousFunctionExpressionSyntax)
        && catalog.Compilation.GetSemanticModel(method.SyntaxTree).AnalyzeControlFlow(
            call.Syntax.Ancestors().OfType<StatementSyntax>().First()) is { Succeeded: true, StartPointIsReachable: true };
    private static bool FalseReturn(StatementSyntax statement) => statement switch
    {
        ReturnStatementSyntax { Expression: { } value } => value.IsKind(SyntaxKind.FalseLiteralExpression),
        BlockSyntax { Statements.Count: 1 } block => FalseReturn(block.Statements[0]),
        _ => false
    };
    private static bool Boolean(IOperation operation, bool expected) => operation.ConstantValue is
        { HasValue: true, Value: bool value } && value == expected;
    private static bool Is(IInvocationOperation call, string owner, string method) => call.TargetMethod.Name == method
        && Same(call.TargetMethod.ContainingType, call.TargetMethod.ContainingAssembly.GetTypeByMetadataName(owner)!);
    private static bool Same(ISymbol first, ISymbol second) => SymbolEqualityComparer.Default.Equals(first, second);
    private static bool SameSyntax(SyntaxNode first, SyntaxNode second) => first.SyntaxTree == second.SyntaxTree && first.Span == second.Span;
}
