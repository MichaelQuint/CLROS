using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using OS.Tooling;

internal static class HostLinuxSignalSourceGuardTests
{
    [HostTest("signal source guards follow partials and symbols while retaining APIC and gateway authorization")]
    private static void BoundSourceControls()
    {
        var source = HostSourceCatalog.Efi(EfiOptions.Parse(["--EnableM16009Probe"], true).Profile());
        bool Check(CSharpSourceCatalog candidate)
        {
            try
            {
                return HostLinuxSignalSourceGuard.Boot(candidate) && HostLinuxSignalSourceGuard.Gateway(candidate);
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }
        HostCheck.True(Check(source), "actual signal profile");
        var common = source.Method(HostLinuxSignalSourceGuard.Owner, "CooperativeAdmitPool");
        var model = source.Compilation.GetSemanticModel(common.SyntaxTree);
        var gateCall = source.MethodInvocations(common).Single(call => call.TargetMethod.Name == "LinuxNativeUserGates");
        var gateSyntax = (InvocationExpressionSyntax)gateCall.Syntax;
        var choice = (ConditionalExpressionSyntax)gateSyntax.Parent!;
        var local = (ILocalSymbol)model.GetSymbolInfo(choice.Condition).Symbol!;
        var tokens = common.DescendantTokens().Where(token => token.IsKind(SyntaxKind.IdentifierToken)
            && token.Parent is { } node && SymbolEqualityComparer.Default.Equals(node is VariableDeclaratorSyntax declaration
                ? model.GetDeclaredSymbol(declaration) : node is IdentifierNameSyntax name ? model.GetSymbolInfo(name).Symbol : null, local)).ToArray();
        var renamed = common.ReplaceTokens(tokens,
            (before, _) => SyntaxFactory.Identifier(before.LeadingTrivia, "authenticatedGateway", before.TrailingTrivia));
        HostCheck.True(Check(Change(common, renamed)), "bound local rename");

        var document = source.Document(common);
        string aliasText = "using GateOperations = OS.Kernel.Compatibility.LinuxX64.X64LinuxUserTransition;\n"
            + document.Text.Replace("X64LinuxUserTransition.", "GateOperations.", StringComparison.Ordinal);
        var alias = CSharpSourceDocument.FromBytes(document.Path, System.Text.Encoding.UTF8.GetBytes(aliasText),
            (CSharpParseOptions)document.Syntax.Options);
        HostCheck.True(Check(Rebuild(source.Documents.Select(item => item.Path == document.Path ? alias : item))), "bound alias");

        var root = document.Syntax.GetCompilationUnitRoot();
        var remaining = Replace(document, root.RemoveNode(common, SyntaxRemoveOptions.KeepExteriorTrivia)!);
        string imports = string.Join("\n", root.Usings.Select(item => item.WithoutTrivia().NormalizeWhitespace().ToFullString()));
        string path = Path.Combine(Path.GetDirectoryName(document.Path)!, "signal-moved-control.cs");
        HostCheck.True(source.Documents.All(item => item.Path != path));
        var moved = CSharpSourceDocument.FromBytes(path, System.Text.Encoding.UTF8.GetBytes(imports
            + "\nnamespace OS.Boot; public static unsafe partial class EfiEntry { private static partial class M03FirstExitAdapter { "
            + common.WithoutLeadingTrivia().WithoutTrailingTrivia().ToFullString() + " } }"), (CSharpParseOptions)common.SyntaxTree.Options);
        HostCheck.True(Check(Rebuild(source.Documents.Select(item => item.Path == document.Path ? remaining : item).Append(moved))),
            "common gate moved to another partial");

        var handler = source.Method(HostLinuxSignalSourceGuard.Owner, "LinuxNativeUserInterrupt");
        var direct = (InvocationExpressionSyntax)source.MethodInvocations(handler).Single(call => call.TargetMethod.Name == "LinuxNativeUserGates").Syntax;
        var strict = direct.AddArgumentListArguments(SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(SyntaxKind.FalseLiteralExpression)));
        HostCheck.True(Check(Change(handler, handler.ReplaceNode(direct, strict))), "explicit false remains strict");

        var argument = gateSyntax.ArgumentList.Arguments[2];
        HostCheck.True(!Check(Change(common, common.ReplaceNode(argument,
            argument.WithExpression(SyntaxFactory.LiteralExpression(SyntaxKind.FalseLiteralExpression))))), "common syscall envelope removed");
        HostCheck.True(!Check(Change(handler, handler.ReplaceNode(direct,
            direct.AddArgumentListArguments(SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(SyntaxKind.TrueLiteralExpression)))))),
            "direct IRQ must remain strict");
        var authentication = source.MethodInvocations(common).Single(call => call.TargetMethod.Name == "ValidateCallback");
        HostCheck.True(!Check(Change(common, common.ReplaceNode(authentication.Syntax,
            SyntaxFactory.LiteralExpression(SyntaxKind.TrueLiteralExpression)))), "callback authentication removed");
        HostCheck.True(!Check(Change(common, common.ReplaceNode(choice.Condition,
            SyntaxFactory.LiteralExpression(SyntaxKind.TrueLiteralExpression)))), "authentication result bypassed");

        var owner = common.Ancestors().OfType<TypeDeclarationSyntax>().First();
        var decoy = SyntaxFactory.ParseMemberDeclaration("""
            private static unsafe class SignalGateDecoy
            {
                internal static bool Call(LinuxNativeControl* c) => LinuxNativeUserGates(c, true, true);
            }
            """)!;
        var extra = Replace(document, root.ReplaceNode(owner, owner.AddMembers(decoy)));
        HostCheck.True(!Check(Rebuild(source.Documents.Select(item => item.Path == document.Path ? extra : item))),
            "nested helper cannot admit an unauthenticated syscall envelope");

        var gates = source.Method(HostLinuxSignalSourceGuard.Owner, "LinuxNativeUserGates");
        var predicate = (InvocationExpressionSyntax)source.MethodInvocations(gates)
            .Single(call => call.TargetMethod.Name == "ValidateActiveUserGateObservation").Syntax;
        HostCheck.True(!Check(Change(gates, gates.ReplaceNode(predicate.ArgumentList.Arguments[4].Expression,
            SyntaxFactory.LiteralExpression(SyntaxKind.TrueLiteralExpression)))), "strict mode must reach observation validator");
        var gateModel = source.Compilation.GetSemanticModel(gates.SyntaxTree);
        var allow = gateModel.GetDeclaredSymbol(gates)!.Parameters[2];
        HostCheck.True(!Check(Change(gates, gates.WithBody(gates.Body!.WithStatements(gates.Body.Statements.Insert(0,
            SyntaxFactory.ParseStatement(allow.Name + " = true;")))))), "strict parameter overwritten");
        HostCheck.True(!Check(Change(gates, gates.WithBody(gates.Body!.WithStatements(gates.Body.Statements.InsertRange(0,
            [
                SyntaxFactory.ParseStatement("bool* permission = &" + allow.Name + ";"),
                SyntaxFactory.ParseStatement("*permission = true;")
            ]))))), "strict parameter escaped through pointer");
        var returned = (ReturnStatementSyntax)predicate.Parent!;
        var discarded = gates.Body!.WithStatements(gates.Body.Statements.Replace(returned,
            SyntaxFactory.ExpressionStatement(predicate)).Add(SyntaxFactory.ReturnStatement(
                SyntaxFactory.LiteralExpression(SyntaxKind.TrueLiteralExpression))));
        HostCheck.True(!Check(Change(gates, gates.WithBody(discarded))), "observation result discarded");
        var gateStatement = gateSyntax.Ancestors().OfType<StatementSyntax>().First();
        HostCheck.True(!Check(Change(common, common.ReplaceNode(gateStatement,
            SyntaxFactory.IfStatement(SyntaxFactory.LiteralExpression(SyntaxKind.FalseLiteralExpression),
                SyntaxFactory.Block(gateStatement))))), "common gate in unreachable statement");
        var dormant = gates.Body!.WithStatements(gates.Body.Statements.Replace(returned,
            SyntaxFactory.ParseStatement("bool UnusedObservation() { " + returned + " }"))
            .Add(SyntaxFactory.ReturnStatement(SyntaxFactory.LiteralExpression(SyntaxKind.TrueLiteralExpression))));
        HostCheck.True(!Check(Change(gates, gates.WithBody(dormant))), "observation in dormant local function");

        var diagnose = source.Method(HostLinuxSignalSourceGuard.Owner, "Diagnose");
        var apic = source.MethodInvocations(diagnose).Single(call => call.TargetMethod.Name == "RunOwnedApicSetup");
        var apicGate = apic.Syntax.Ancestors().OfType<IfStatementSyntax>().First();
        HostCheck.True(!Check(Change(diagnose, diagnose.ReplaceNode(apicGate.Condition,
            ((PrefixUnaryExpressionSyntax)apicGate.Condition).Operand))), "APIC rejection polarity");
        var bootBlock = (BlockSyntax)apicGate.Parent!;
        HostCheck.True(!Check(Change(diagnose, diagnose.ReplaceNode(bootBlock,
            SyntaxFactory.Block(SyntaxFactory.IfStatement(
                SyntaxFactory.LiteralExpression(SyntaxKind.FalseLiteralExpression), bootBlock))))),
            "APIC and signal calls in unreachable block");

        CSharpSourceCatalog Change(MethodDeclarationSyntax before, MethodDeclarationSyntax after)
        {
            var original = source.Document(before);
            var changed = Replace(original, original.Syntax.GetRoot().ReplaceNode(before, after));
            return Rebuild(source.Documents.Select(item => item.Path == original.Path ? changed : item));
        }
        CSharpSourceDocument Replace(CSharpSourceDocument original, SyntaxNode changed) =>
            new(original.Path, "in-memory signal control", CSharpSyntaxTree.Create((CompilationUnitSyntax)changed,
                (CSharpParseOptions)original.Syntax.Options, original.Path));
        CSharpSourceCatalog Rebuild(IEnumerable<CSharpSourceDocument> documents) =>
            new(documents, source.Compilation.References, source.Compilation.AssemblyName!, source.Compilation.Options);
    }
}
