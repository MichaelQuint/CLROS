using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Tooling;

internal static class HostManagedHeapSourceGuardTests
{
    [HostTest("heap adapter execution preserves conflicting aliases across moved partial declarations", HostLane.Native)]
    private static void AdapterDiscovery()
    {
        var source = M04ManagedHeapPageRangeEfiIntegrationTests.Sources();
        var checkpoint = source.Method(HostManagedHeapSourceGuard.Owner, "Checkpoint");
        var document = source.Document(checkpoint);
        var root = document.Syntax.GetCompilationUnitRoot();
        var owner = checkpoint.Ancestors().OfType<ClassDeclarationSyntax>().First();
        var partial = owner.AddModifiers(SyntaxFactory.Token(SyntaxKind.PartialKeyword));
        var model = source.Compilation.GetSemanticModel(checkpoint.SyntaxTree);
        var core = source.Type("OS.Kernel.Runtime.ManagedHeapPageRangeLiveAllocator");
        var names = checkpoint.DescendantNodes().OfType<IdentifierNameSyntax>().Where(name =>
            SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(name).Symbol, core)).ToArray();
        HostCheck.True(names.Length > 2, "allocator calls and constant references use the moved alias");
        var renamed = checkpoint.ReplaceNodes(names, (_, _) => SyntaxFactory.IdentifierName("HeapLibrary"));
        var left = root.ReplaceNode(owner, partial.WithMembers(owner.Members.Remove(checkpoint)))
            .AddUsings(SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System.UInt64"))
                .WithAlias(SyntaxFactory.NameEquals("HeapLibrary")));
        var movedOwner = partial.WithMembers(SyntaxFactory.SingletonList<MemberDeclarationSyntax>(renamed));
        movedOwner = movedOwner.ReplaceTrivia(movedOwner.DescendantTrivia(descendIntoTrivia: true), (_, _) => default);
        string imports = string.Join("\n", root.Usings.Select(import => import.WithoutTrivia().NormalizeWhitespace().ToFullString()));
        string path = Path.Combine(Path.GetDirectoryName(document.Path)!, "heap-partial-alias-control.cs");
        HostCheck.True(source.Documents.All(item => item.Path != path));
        var moved = CSharpSourceDocument.FromBytes(path, System.Text.Encoding.UTF8.GetBytes(imports
            + "\nusing HeapLibrary = OS.Kernel.Runtime.ManagedHeapPageRangeLiveAllocator;\n"
            + "namespace OS.Boot; public static unsafe partial class EfiEntry { "
            + movedOwner.NormalizeWhitespace().ToFullString() + " }"), (CSharpParseOptions)document.Syntax.Options);
        var retained = new CSharpSourceDocument(document.Path, "heap partial alias control", CSharpSyntaxTree.Create(
            left, (CSharpParseOptions)document.Syntax.Options, document.Path));
        var changed = new CSharpSourceCatalog(source.Documents.Select(item => item.Path == document.Path ? retained : item)
            .Append(moved), source.Compilation.References, source.Compilation.AssemblyName!, source.Compilation.Options);
        HostCheck.True(HostManagedHeapSourceGuard.Wiring(changed) && HostManagedHeapSourceGuard.OriginalAuthority(changed));
        HostCheck.Equal(2, HostManagedHeapSourceGuard.AdapterSources(changed).Count);
        M04ManagedHeapPageRangeEfiIntegrationTests.TestAdapterSource(changed);
    }

    [HostTest("heap source checks follow moved declarations and renamed locals while retaining original storage")]
    private static void BoundControls()
    {
        var source = M04ManagedHeapPageRangeEfiIntegrationTests.Sources();
        bool Check(CSharpSourceCatalog catalog)
        {
            try
            {
                return HostManagedHeapSourceGuard.Wiring(catalog) && HostManagedHeapSourceGuard.OriginalAuthority(catalog);
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }
        HostCheck.True(Check(source), "selected heap source");
        var checkpoint = source.Method(HostManagedHeapSourceGuard.Owner, "Checkpoint");
        var model = source.Compilation.GetSemanticModel(checkpoint.SyntaxTree);
        var variable = checkpoint.DescendantNodes().OfType<VariableDeclaratorSyntax>()
            .Single(declaration => declaration.Identifier.ValueText == "allocator");
        var local = model.GetDeclaredSymbol(variable)!;
        var tokens = checkpoint.DescendantTokens().Where(token => token.IsKind(SyntaxKind.IdentifierToken)
            && token.Parent is { } node && SymbolEqualityComparer.Default.Equals(node is VariableDeclaratorSyntax declaration
                ? model.GetDeclaredSymbol(declaration) : node is IdentifierNameSyntax name ? model.GetSymbolInfo(name).Symbol : null, local));
        HostCheck.True(Check(Change(checkpoint, checkpoint.ReplaceTokens(tokens,
            (before, _) => SyntaxFactory.Identifier(before.LeadingTrivia, "originalAllocator", before.TrailingTrivia)))), "local rename");

        var document = source.Document(checkpoint);
        var root = document.Syntax.GetCompilationUnitRoot();
        var owner = checkpoint.Ancestors().OfType<TypeDeclarationSyntax>().First();
        var remaining = Replace(document, root.RemoveNode(owner, SyntaxRemoveOptions.KeepExteriorTrivia)!);
        string imports = string.Join("\n", root.Usings.Select(import => import.WithoutTrivia().NormalizeWhitespace().ToFullString()));
        string path = Path.Combine(Path.GetDirectoryName(document.Path)!, "heap-moved-control.cs");
        HostCheck.True(source.Documents.All(item => item.Path != path));
        var activeOwner = owner.ReplaceTrivia(owner.DescendantTrivia(descendIntoTrivia: true), (_, _) => default)
            .NormalizeWhitespace().ToFullString();
        var moved = CSharpSourceDocument.FromBytes(path, System.Text.Encoding.UTF8.GetBytes(imports
            + "\nnamespace OS.Boot; public static unsafe partial class EfiEntry { " + activeOwner + " }"),
            (CSharpParseOptions)document.Syntax.Options);
        HostCheck.True(Check(Rebuild(source.Documents.Select(item => item.Path == document.Path ? remaining : item).Append(moved))),
            "adapter moved to another partial file");

        var alias = CSharpSourceDocument.FromBytes(document.Path, System.Text.Encoding.UTF8.GetBytes(
            "using HeapOperations = OS.Kernel.Runtime.ManagedHeapPageRangeLiveAllocator;\n"
            + document.Text.Replace("ManagedHeapPageRangeLiveAllocator.", "HeapOperations.", StringComparison.Ordinal)),
            (CSharpParseOptions)document.Syntax.Options);
        HostCheck.True(Check(Rebuild(source.Documents.Select(item => item.Path == document.Path ? alias : item))), "bound allocator alias");

        HostCheck.True(!Check(Change(checkpoint, checkpoint.ReplaceNode(variable.Initializer!.Value,
            SyntaxFactory.ParseExpression("(BootPageAllocatorState*)0")))), "substitute allocator authority");
        var state = model.GetDeclaredSymbol(checkpoint)!.Parameters[4].Name;
        HostCheck.True(!Check(Change(checkpoint, checkpoint.WithBody(checkpoint.Body!.WithStatements(
            checkpoint.Body.Statements.Insert(0, SyntaxFactory.ParseStatement("*" + state + " = default;")))))),
            "adapter resets retained heap");
        string input = model.GetDeclaredSymbol(checkpoint)!.Parameters[1].Name;
        HostCheck.True(!Check(Change(checkpoint, checkpoint.WithBody(checkpoint.Body!.WithStatements(
            checkpoint.Body.Statements.Insert(0, SyntaxFactory.ParseStatement(input + " = null;")))))),
            "original state pointer overwritten");
        HostCheck.True(!Check(Change(checkpoint, checkpoint.WithBody(checkpoint.Body!.WithStatements(
            checkpoint.Body.Statements.InsertRange(0,
            [
                SyntaxFactory.ParseStatement("M01LiveAllocatorStaticState** replacement = &" + input + ";"),
                SyntaxFactory.ParseStatement("*replacement = null;")
            ]))))), "original state pointer escaped");

        const string stage = "OS.Boot.EfiEntry+M01StageTwoAdapter";
        var initialize = source.Method(stage, "RunAfterStageOne");
        var call = (InvocationExpressionSyntax)source.MethodInvocations(initialize).Single(invocation =>
            invocation.TargetMethod.Name == "Checkpoint").Syntax;
        HostCheck.True(!Check(Change(initialize, initialize.ReplaceNode(call.ArgumentList.Arguments[0].Expression,
            SyntaxFactory.LiteralExpression(SyntaxKind.FalseLiteralExpression)))), "initial checkpoint mode removed");
        HostCheck.True(!Check(Change(initialize, initialize.ReplaceNode(call.ArgumentList.Arguments[4].Expression,
            SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression)))), "fixed heap bypassed");
        var gate = call.Ancestors().OfType<IfStatementSyntax>().First();
        HostCheck.True(!Check(Change(initialize, initialize.ReplaceNode(gate.Condition,
            ((PrefixUnaryExpressionSyntax)gate.Condition).Operand))), "initial failure polarity inverted");
        var retain = source.Method(stage, "ValidateManagedHeapBeforeBootPlan");
        var retained = (InvocationExpressionSyntax)source.MethodInvocations(retain).Single(invocation =>
            invocation.TargetMethod.Name == "Checkpoint").Syntax;
        HostCheck.True(!Check(Change(retain, retain.ReplaceNode(retained.ArgumentList.Arguments[0].Expression,
            SyntaxFactory.LiteralExpression(SyntaxKind.TrueLiteralExpression)))), "retained checkpoint reinitializes");
        var continuation = source.Method("OS.Boot.EfiEntry+M01StageOneAdapter", "M01StageOneContinuation");
        var validation = (InvocationExpressionSyntax)source.MethodInvocations(continuation).Single(invocation =>
            invocation.TargetMethod.Name == "ValidateManagedHeapBeforeBootPlan").Syntax;
        var retainedGate = validation.Ancestors().OfType<IfStatementSyntax>().First();
        HostCheck.True(!Check(Change(continuation, continuation.ReplaceNode(retainedGate,
            SyntaxFactory.ExpressionStatement(validation)))), "retained validation result ignored");
        HostCheck.True(!Check(Change(continuation, continuation.ReplaceNode(retainedGate.Condition,
            ((PrefixUnaryExpressionSyntax)retainedGate.Condition).Operand))), "retained failure polarity inverted");
        HostCheck.True(!Check(Change(continuation, continuation.ReplaceNode(retainedGate.Statement,
            SyntaxFactory.EmptyStatement()))), "retained failure continues to boot plan");

        CSharpSourceCatalog Change(MethodDeclarationSyntax before, MethodDeclarationSyntax after)
        {
            var original = source.Document(before);
            var changed = Replace(original, original.Syntax.GetRoot().ReplaceNode(before, after));
            return Rebuild(source.Documents.Select(item => item.Path == original.Path ? changed : item));
        }
        CSharpSourceDocument Replace(CSharpSourceDocument original, SyntaxNode changed) =>
            new(original.Path, "in-memory heap source control", CSharpSyntaxTree.Create((CompilationUnitSyntax)changed,
                (CSharpParseOptions)original.Syntax.Options, original.Path));
        CSharpSourceCatalog Rebuild(IEnumerable<CSharpSourceDocument> documents) =>
            new(documents, source.Compilation.References, source.Compilation.AssemblyName!, source.Compilation.Options);
    }
}
