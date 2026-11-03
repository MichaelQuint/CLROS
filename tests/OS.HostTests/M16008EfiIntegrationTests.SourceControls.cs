using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Tooling;

internal static unsafe partial class M16008EfiIntegrationTests
{
    [HostTest("m16 thread source contracts follow partial files and reject changed boot and layout behavior")]
    private static void SourceControls()
    {
        const string flag = "EnableM16008Probe";
        const string owner = "OS.Boot.EfiEntry+M03FirstExitAdapter";
        var source = Select(flag);
        bool Boot(CSharpSourceCatalog candidate) => Accepts(() => ValidateBoot(candidate, threads: true));
        bool Layout(CSharpSourceCatalog candidate) => Accepts(() => ValidateLayout(name => name == flag ? candidate : Select(name)));
        HostCheck.True(Boot(source) && Layout(source), "selected compiler baseline");

        var guard = source.Method(owner, "CooperativeGuardPage");
        var model = source.Compilation.GetSemanticModel(guard.SyntaxTree);
        var parameter = model.GetDeclaredSymbol(guard)!.Parameters.Single();
        var names = guard.DescendantTokens().Where(token => token.IsKind(SyntaxKind.IdentifierToken)
            && token.Parent is { } node && SymbolEqualityComparer.Default.Equals(node is ParameterSyntax declaration
                ? model.GetDeclaredSymbol(declaration) : node is IdentifierNameSyntax name ? model.GetSymbolInfo(name).Symbol : null, parameter));
        var renamed = guard.ReplaceTokens(names,
            (before, _) => SyntaxFactory.Identifier(before.LeadingTrivia, "selectedPage", before.TrailingTrivia));
        HostCheck.True(Layout(Change(guard, renamed)), "parameter identity survives rename");
        var invocation = guard.ExpressionBody!.Expression;
        var block = guard.WithExpressionBody(null).WithSemicolonToken(default)
            .WithBody(SyntaxFactory.Block(SyntaxFactory.ReturnStatement(invocation)));
        HostCheck.True(Layout(Change(guard, block)), "equivalent block return");
        HostCheck.True(!Layout(Change(guard, block.WithBody(SyntaxFactory.Block(
            SyntaxFactory.ExpressionStatement(invocation),
            SyntaxFactory.ReturnStatement(SyntaxFactory.LiteralExpression(SyntaxKind.FalseLiteralExpression)))))),
            "guard result discarded");

        var document = source.Document(guard);
        var root = document.Syntax.GetCompilationUnitRoot();
        var remaining = Replace(document, root.RemoveNode(guard, SyntaxRemoveOptions.KeepExteriorTrivia)!);
        string path = Path.Combine(Path.GetDirectoryName(document.Path)!, "thread-guard-moved-control.cs");
        HostCheck.True(source.Documents.All(item => item.Path != path));
        string imports = string.Join("\n", root.Usings.Select(item => item.WithoutTrivia().NormalizeWhitespace().ToFullString()));
        var activeBlock = block.ReplaceTrivia(block.DescendantTrivia(descendIntoTrivia: true), (_, _) => default);
        var moved = CSharpSourceDocument.FromBytes(path, System.Text.Encoding.UTF8.GetBytes(imports
            + "\nnamespace OS.Boot; public static unsafe partial class EfiEntry { private static partial class M03FirstExitAdapter { "
            + activeBlock.NormalizeWhitespace().ToFullString() + " } }"), (CSharpParseOptions)guard.SyntaxTree.Options);
        HostCheck.True(Layout(Rebuild(source.Documents.Select(item => item.Path == document.Path ? remaining : item).Append(moved))),
            "helper moved between partial declarations");

        var diagnose = source.Method(owner, "Diagnose");
        var apic = source.MethodInvocations(diagnose).Single(call => call.TargetMethod.Name == "RunOwnedApicSetup");
        var gate = apic.Syntax.Ancestors().OfType<IfStatementSyntax>().First();
        HostCheck.True(!Boot(Change(diagnose, diagnose.ReplaceNode(gate.Condition,
            ((PrefixUnaryExpressionSyntax)gate.Condition).Operand))), "APIC failure polarity");
        var phases = (BlockSyntax)gate.Parent!;
        HostCheck.True(!Boot(Change(diagnose, diagnose.ReplaceNode(phases, SyntaxFactory.Block(
            SyntaxFactory.IfStatement(SyntaxFactory.LiteralExpression(SyntaxKind.FalseLiteralExpression), phases))))),
            "dormant phase calls");
        var plan = source.Method(owner, "LinuxUserTransitionPlan");
        var resume = plan.DescendantNodes().OfType<AssignmentExpressionSyntax>()
            .Single(assignment => assignment.Left.ToString() == "ResumeUser");
        HostCheck.True(!Boot(Change(plan, plan.ReplaceNode(resume.Right,
            SyntaxFactory.LiteralExpression(SyntaxKind.FalseLiteralExpression)))), "user resumption disabled");

        var capacity = source.Type(owner).GetMembers("LinuxLedgerCapacity").Single().DeclaringSyntaxReferences.Single().GetSyntax();
        var field = (VariableDeclaratorSyntax)capacity;
        HostCheck.True(!Layout(Change(field, field.WithInitializer(field.Initializer!.WithValue(
            SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(16)))))),
            "ledger budget changed");

        CSharpSourceCatalog Change(SyntaxNode before, SyntaxNode after)
        {
            var original = source.Document(before);
            var changed = Replace(original, original.Syntax.GetRoot().ReplaceNode(before, after));
            return Rebuild(source.Documents.Select(item => item.Path == original.Path ? changed : item));
        }
        CSharpSourceDocument Replace(CSharpSourceDocument original, SyntaxNode changed) =>
            new(original.Path, "in-memory thread source control", CSharpSyntaxTree.Create((CompilationUnitSyntax)changed,
                (CSharpParseOptions)original.Syntax.Options, original.Path));
        CSharpSourceCatalog Rebuild(IEnumerable<CSharpSourceDocument> documents) =>
            new(documents, source.Compilation.References, source.Compilation.AssemblyName!, source.Compilation.Options);
        static bool Accepts(Action check)
        {
            try
            {
                check();
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }
    }
}
