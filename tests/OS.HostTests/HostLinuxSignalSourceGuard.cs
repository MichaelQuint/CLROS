using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using OS.Tooling;

/// <summary>Checks the signal profile's boot and syscall-gate wiring by compiler
/// ownership. Independent runtime/CPU fixtures retain responsibility for the
/// observations themselves; these checks follow partial files and local names.</summary>
internal static class HostLinuxSignalSourceGuard
{
    internal const string Owner = "OS.Boot.EfiEntry+M03FirstExitAdapter";
    private const string BootSequence = "OS.Boot.BootSequence";
    private const string Transition = "OS.Kernel.Compatibility.LinuxX64.X64LinuxUserTransition";

    internal static bool Boot(CSharpSourceCatalog catalog)
    {
        string[] required = ["RunOwnedApicSetup", "RunOwnedLinuxSignals"];
        if (!HostBootSourceGuard.CallsConfined(catalog, "OS.Boot.EfiEntry", Owner, BootSequence,
                required, ["RunOwnedLinuxThreads", "RunOwnedLinuxDescriptors"], true, "Diagnose")
            || !HostBootSourceGuard.OrderedCalls(catalog, Owner, "Diagnose", BootSequence, required))
            return false;
        var method = catalog.Method(Owner, "Diagnose");
        var calls = catalog.MethodInvocations(method).Where(call =>
            Same(call.TargetMethod.ContainingType, catalog.Type(BootSequence))).ToArray();
        var apic = calls.Single(call => call.TargetMethod.Name == required[0]);
        var signal = calls.Single(call => call.TargetMethod.Name == required[1]);
        if (!Active(catalog, apic, method) || !Active(catalog, signal, method))
            return false;
        var gate = apic.Syntax.Ancestors().OfType<IfStatementSyntax>().First();
        var signalGate = signal.Syntax.Ancestors().OfType<IfStatementSyntax>().First();
        var model = catalog.Compilation.GetSemanticModel(method.SyntaxTree);
        bool rejects = gate.Statement is ReturnStatementSyntax
            || gate.Statement is BlockSyntax { Statements.Count: 1 } block && block.Statements[0] is ReturnStatementSyntax;
        if (!rejects || gate.Parent != signalGate.Parent
            || HostSourceOperations.Value(model.GetOperation(gate.Condition)!) is not IUnaryOperation
                { OperatorKind: UnaryOperatorKind.Not, Operand: IInvocationOperation condition }
            || !Same(condition.TargetMethod, apic.TargetMethod))
            return false;
        var ready = catalog.Method(Owner, "RunOwnedLinuxSyscalls").DescendantNodes().OfType<LiteralExpressionSyntax>()
            .Where(literal => literal.IsKind(SyntaxKind.StringLiteralExpression)).Select(literal => literal.Token.ValueText);
        var ownerText = catalog.Type(Owner).DeclaringSyntaxReferences.SelectMany(reference =>
            reference.GetSyntax().DescendantNodes().OfType<LiteralExpressionSyntax>())
            .Where(literal => literal.IsKind(SyntaxKind.StringLiteralExpression)).Select(literal => literal.Token.ValueText);
        return ready.Contains("CLROS_M16_009_READY\r\nTEST_PASS m16-009\r\n", StringComparer.Ordinal)
            && !ownerText.Contains("CLROS_M16_008_READY\r\nTEST_PASS m16-008\r\n", StringComparer.Ordinal);
    }

    internal static bool Gateway(CSharpSourceCatalog catalog)
    {
        var common = catalog.Method(Owner, "CooperativeAdmitPool");
        var handler = catalog.Method(Owner, "LinuxNativeUserInterrupt");
        var gates = catalog.Method(Owner, "LinuxNativeUserGates");
        var model = catalog.Compilation.GetSemanticModel(common.SyntaxTree);
        var gateModel = catalog.Compilation.GetSemanticModel(gates.SyntaxTree);
        var gateSymbol = gateModel.GetDeclaredSymbol(gates)!;
        if (gateSymbol.Parameters.Length != 3 || !gateSymbol.Parameters[2].HasExplicitDefaultValue
            || gateSymbol.Parameters[2].ExplicitDefaultValue is not false
            || !HostSourceOperations.IsUnmodifiedParameter(gateModel, gateSymbol.Parameters[2], gates))
            return false;
        var commonCalls = Calls(catalog, common, Owner, gateSymbol.Name);
        if (commonCalls.Length != 1 || !Active(catalog, commonCalls[0], common) || !Boolean(Argument(commonCalls[0], 1), true)
            || !Boolean(Argument(commonCalls[0], 2), true)
            || commonCalls[0].Syntax.Parent is not ConditionalExpressionSyntax choice
            || choice.WhenTrue != commonCalls[0].Syntax
            || model.GetOperation(choice.Condition) is not ILocalReferenceOperation authenticated
            || HostSourceOperations.LocalInitializer(model, authenticated) is not { } initializer)
            return false;
        var validations = Calls(catalog, common, Transition, "ValidateCallback");
        if (validations.Length != 1 || !Active(catalog, validations[0], common) || !Conjuncts(initializer).OfType<IInvocationOperation>()
                .Any(call => SameSyntax(call.Syntax, validations[0].Syntax))
            || validations[0].Syntax.SpanStart >= commonCalls[0].Syntax.SpanStart)
            return false;
        var direct = Calls(catalog, handler, Owner, gateSymbol.Name);
        if (direct.Length != 1 || !Active(catalog, direct[0], handler) || !Boolean(Argument(direct[0], 1), true)
            || !Boolean(Argument(direct[0], 2), false))
            return false;
        var predicates = Calls(catalog, gates, Transition, "ValidateActiveUserGateObservation");
        if (predicates.Length != 1 || !Active(catalog, predicates[0], gates) || predicates[0].Arguments.Length != 5
            || predicates[0].Syntax.Parent is not ReturnStatementSyntax { Parent: BlockSyntax body } || body != gates.Body
            || !HostSourceOperations.IsParameter(Argument(predicates[0], 4), gateSymbol.Parameters[2]))
            return false;

        // Every caller outside the authenticated common path stays strict,
        // including helpers moved into another file or nested owner.
        foreach (var reference in catalog.Type(Owner).DeclaringSyntaxReferences)
        {
            var declaration = reference.GetSyntax();
            var semantic = catalog.Compilation.GetSemanticModel(declaration.SyntaxTree);
            foreach (var name in declaration.DescendantNodes().OfType<SimpleNameSyntax>())
            {
                if (!SymbolEqualityComparer.Default.Equals(semantic.GetSymbolInfo(name).Symbol, gateSymbol))
                    continue;
                SyntaxNode expression = name.Parent is MemberAccessExpressionSyntax member && member.Name == name ? member : name;
                if (expression.Parent is not InvocationExpressionSyntax invocation || invocation.Expression != expression
                    || semantic.GetOperation(invocation) is not IInvocationOperation call)
                    return false;
                if (!SameSyntax(call.Syntax, commonCalls[0].Syntax) && !Boolean(Argument(call, 2), false))
                    return false;
            }
        }
        return true;
    }

    private static IEnumerable<IOperation> Conjuncts(IOperation operation) => HostSourceOperations.Value(operation) is
        IBinaryOperation { OperatorKind: BinaryOperatorKind.ConditionalAnd } binary
            ? Conjuncts(binary.LeftOperand).Concat(Conjuncts(binary.RightOperand)) : [HostSourceOperations.Value(operation)];
    private static bool Boolean(IOperation operation, bool expected) =>
        HostSourceOperations.Value(operation).ConstantValue is { HasValue: true, Value: bool value } && value == expected;
    private static IOperation Argument(IInvocationOperation call, int ordinal) => HostSourceOperations.Argument(call, ordinal);
    private static bool Same(ISymbol first, ISymbol second) => SymbolEqualityComparer.Default.Equals(first, second);
    private static bool SameSyntax(SyntaxNode first, SyntaxNode second) => first.SyntaxTree == second.SyntaxTree && first.Span == second.Span;
    private static bool Active(CSharpSourceCatalog catalog, IInvocationOperation call, MethodDeclarationSyntax method)
    {
        if (call.Syntax.Ancestors().TakeWhile(node => node != method)
            .Any(node => node is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax))
            return false;
        var statement = call.Syntax.Ancestors().OfType<StatementSyntax>().FirstOrDefault();
        // Compiler reachability rejects dormant statements. This does not prove
        // that every runtime path reaches a call or that every predicate is true.
        return statement is not null && catalog.Compilation.GetSemanticModel(method.SyntaxTree)
            .AnalyzeControlFlow(statement) is { Succeeded: true, StartPointIsReachable: true };
    }
    private static IInvocationOperation[] Calls(CSharpSourceCatalog catalog, MethodDeclarationSyntax method, string owner, string name) =>
        catalog.MethodInvocations(method).Where(call => call.TargetMethod.Name == name
            && Same(call.TargetMethod.ContainingType, catalog.Type(owner))).ToArray();
}
