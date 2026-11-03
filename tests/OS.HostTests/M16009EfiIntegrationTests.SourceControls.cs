using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Tooling;

internal static unsafe partial class M16009EfiIntegrationTests
{
    [HostTest("m16-009 leaf discovery follows moved partials and symbols while rejecting read and ownership decoys")]
    private static void LeafReuseDiscoveryControls()
    {
        var original = LeafReuseSources();
        HostCheck.True(original.PoolBoundary() && original.ReadOrder());

        var moved = original;
        foreach (string name in new[] { "CooperativeFindLeaf", "CooperativeAdmitPool", "CooperativeWalkPoolLeaf",
            "CooperativeAuditPool", "CooperativeSameDecodePreconditions", "CooperativeGuardPage" })
            moved = RelocateLeafMember(moved, moved.Method(name));
        foreach (string name in new[] { "Page", "TablePages", "CooperativePoolPages", "CooperativeCodePages" })
        {
            var symbol = moved.Catalog.Type(EfiLeafReuseSources.Owner).GetMembers(name).OfType<IFieldSymbol>().Single();
            var variable = (VariableDeclaratorSyntax)symbol.DeclaringSyntaxReferences.Single().GetSyntax();
            moved = RelocateLeafMember(moved, (FieldDeclarationSyntax)variable.Parent!.Parent!);
        }
        HostCheck.True(moved.PoolBoundary() && moved.ReadOrder(), "separate method/constant partials");
        foreach (string name in new[] { "Page", "TablePages", "CooperativePoolPages", "CooperativeCodePages" })
            HostCheck.Equal(name, moved.Constant(name).Declaration.Variables.Single().Identifier.ValueText);

        var renamed = RenameLeafLocal(moved, "CooperativeWalkPoolLeaf", "raw", "readValue");
        renamed = RenameLeafLocal(renamed, "CooperativeWalkPoolLeaf", "preconditions", "firstPolicy");
        renamed = RenameLeafLocal(renamed, "CooperativeAuditPool", "raw", "readAgain");
        renamed = RenameLeafLocal(renamed, "CooperativeAuditPool", "currentPreconditions", "livePolicy");
        HostCheck.True(renamed.PoolBoundary() && renamed.ReadOrder(), "bound local identities");

        var find = moved.Method("CooperativeFindLeaf");
        var findCall = EfiLeafReuseSources.Syntax(moved.Calls(find, "CooperativeWalkPoolLeaf").Single());
        var qualified = moved.Replace(find, find.ReplaceNode(findCall.Expression,
            SyntaxFactory.ParseExpression("global::OS.Boot.EfiEntry.M03FirstExitAdapter.CooperativeWalkPoolLeaf")));
        HostCheck.True(qualified.PoolBoundary() && qualified.ReadOrder(), "qualified same owner");

        var walk = original.Method("CooperativeWalkPoolLeaf");
        var raw = walk.DescendantNodes().OfType<VariableDeclaratorSyntax>().Single(variable => variable.Identifier.ValueText == "raw");
        var wrongRead = original.Replace(walk, walk.ReplaceNode(raw.Initializer!.Value, SyntaxFactory.ParseExpression("*memory.Entries")));
        HostCheck.True(!wrongRead.ReadOrder(), "first raw must come from the returned slot");
        var context = walk.DescendantNodes().OfType<VariableDeclaratorSyntax>().Single(variable => variable.Identifier.ValueText == "preconditions");
        HostCheck.True(!SwapLeafStatements(original, walk, (StatementSyntax)raw.Parent!.Parent!,
            (StatementSyntax)context.Parent!.Parent!).ReadOrder(), "context cannot precede first raw read");
        var capture = walk.DescendantNodes().OfType<AssignmentExpressionSyntax>()
            .Single(assignment => assignment.Left.ToString() == "*leafDecoded");
        HostCheck.True(!original.Replace(walk, walk.ReplaceNode(capture.Parent!, SyntaxFactory.EmptyStatement())).ReadOrder(), "missing decoded capture");
        var leafLevel = capture.Ancestors().OfType<IfStatementSyntax>().Single(gate => gate.Condition is BinaryExpressionSyntax condition && condition.IsKind(SyntaxKind.EqualsExpression));
        HostCheck.True(!original.Replace(walk, walk.ReplaceNode(((BinaryExpressionSyntax)leafLevel.Condition).Right,
            SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(2)))).ReadOrder(), "wrong capture level");

        var audit = original.Method("CooperativeAuditPool");
        var secondRead = audit.DescendantNodes().OfType<VariableDeclaratorSyntax>().Single(variable => variable.Identifier.ValueText == "raw");
        var guard = original.Calls(audit, "CooperativeGuardPage").Single().Syntax.Ancestors().OfType<LocalDeclarationStatementSyntax>().Single();
        HostCheck.True(!SwapLeafStatements(original, audit, (StatementSyntax)secondRead.Parent!.Parent!, guard).ReadOrder(), "guard cannot precede second raw read");

        var decoy = AddLeafMember(original, SyntaxFactory.ParseMemberDeclaration("""
            private static unsafe class LeafProofDecoy
            {
                internal static bool CooperativeAdmitPool(Storage* state, bool ownedIst, out FirstExitDiagnosticMemory memory, out ulong root)
                { memory = default; root = 0; return true; }
                internal static OS.Kernel.Memory.X64PageTableEntryResult TryDecode(OS.Kernel.Memory.X64PageTableLevel level,
                    ulong raw, OS.Kernel.Memory.X64PageTableEncodingPreconditions preconditions)
                    => OS.Kernel.Memory.X64PageTableEntry.TryDecode(level, raw, preconditions);
            }
            """)!);
        find = decoy.Method("CooperativeFindLeaf");
        var admission = EfiLeafReuseSources.Syntax(decoy.Calls(find, "CooperativeAdmitPool").Single());
        HostCheck.True(!decoy.Replace(find, find.ReplaceNode(admission.Expression,
            SyntaxFactory.ParseExpression("LeafProofDecoy.CooperativeAdmitPool"))).PoolBoundary(), "foreign admission with same name");
        audit = decoy.Method("CooperativeAuditPool");
        var fallback = EfiLeafReuseSources.Syntax(decoy.Calls(audit, "OS.Kernel.Memory.X64PageTableEntry", "TryDecode").Single());
        HostCheck.True(!decoy.Replace(audit, audit.ReplaceNode(fallback.Expression,
            SyntaxFactory.ParseExpression("LeafProofDecoy.TryDecode"))).ReadOrder(), "foreign fallback decoder");
        var extraWalk = AddLeafMember(original, SyntaxFactory.ParseMemberDeclaration("""
            private static bool ExtraLeafCall(FirstExitDiagnosticMemory memory, ulong pool, ulong root, ulong address, out ulong* leaf)
                => CooperativeWalkPoolLeaf(memory, pool, root, address, out leaf);
            """)!);
        HostCheck.True(!extraWalk.PoolBoundary(), "walk caller inventory includes every partial");
        var nestedWalk = AddLeafMember(original, SyntaxFactory.ParseMemberDeclaration("""
            private static unsafe class LeafProofNestedCaller
            {
                private static bool Walk(FirstExitDiagnosticMemory memory, ulong pool, ulong root, ulong address, out ulong* leaf)
                    => CooperativeWalkPoolLeaf(memory, pool, root, address, out leaf);
            }
            """)!);
        HostCheck.True(!nestedWalk.PoolBoundary(), "walk caller inventory includes nested helpers");
    }

    private static EfiLeafReuseSources SwapLeafStatements(EfiLeafReuseSources sources, MethodDeclarationSyntax method,
        StatementSyntax first, StatementSyntax second)
    {
        var block = (BlockSyntax)first.Parent!;
        HostCheck.True(second.Parent == block);
        var statements = block.Statements.ToArray();
        int a = Array.IndexOf(statements, first), b = Array.IndexOf(statements, second);
        (statements[a], statements[b]) = (statements[b], statements[a]);
        return sources.Replace(method, method.ReplaceNode(block, block.WithStatements(SyntaxFactory.List(statements))));
    }

    private static EfiLeafReuseSources RenameLeafLocal(EfiLeafReuseSources sources, string name, string oldName, string newName)
    {
        var method = sources.Method(name);
        var model = sources.Catalog.Compilation.GetSemanticModel(method.SyntaxTree);
        var variable = method.DescendantNodes().OfType<VariableDeclaratorSyntax>().Single(item => item.Identifier.ValueText == oldName);
        var symbol = model.GetDeclaredSymbol(variable);
        var references = method.DescendantNodes().OfType<IdentifierNameSyntax>()
            .Where(item => SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(item).Symbol, symbol));
        var renamed = method.ReplaceNodes(references, (original, _) => original.WithIdentifier(
            SyntaxFactory.Identifier(original.Identifier.LeadingTrivia, newName, original.Identifier.TrailingTrivia)));
        variable = renamed.DescendantNodes().OfType<VariableDeclaratorSyntax>().Single(item => item.Identifier.ValueText == oldName);
        return sources.Replace(method, renamed.ReplaceNode(variable, variable.WithIdentifier(
            SyntaxFactory.Identifier(variable.Identifier.LeadingTrivia, newName, variable.Identifier.TrailingTrivia))));
    }

    private static EfiLeafReuseSources AddLeafMember(EfiLeafReuseSources sources, MemberDeclarationSyntax member)
    {
        var method = sources.Method("CooperativeWalkPoolLeaf");
        var owner = method.Ancestors().OfType<TypeDeclarationSyntax>().First();
        return RebindLeafDocument(sources, sources.Catalog.Document(method), owner.SyntaxTree.GetRoot()
            .ReplaceNode(owner, owner.AddMembers(member)));
    }

    private static EfiLeafReuseSources RelocateLeafMember(EfiLeafReuseSources sources, MemberDeclarationSyntax member)
    {
        var document = sources.Catalog.Document(member);
        var root = document.Syntax.GetCompilationUnitRoot();
        var updated = CSharpSourceDocument.FromBytes(document.Path,
            System.Text.Encoding.UTF8.GetBytes(root.RemoveNode(member, SyntaxRemoveOptions.KeepExteriorTrivia | SyntaxRemoveOptions.KeepUnbalancedDirectives)!.ToFullString()),
            (CSharpParseOptions)document.Syntax.Options);
        var documents = sources.Catalog.Documents.Select(item => item == document ? updated : item).ToList();
        IEnumerable<MemberDeclarationSyntax> parts = member is FieldDeclarationSyntax field
            ? field.Declaration.Variables.Select(variable => (MemberDeclarationSyntax)field.WithDeclaration(
                field.Declaration.WithVariables(SyntaxFactory.SingletonSeparatedList(variable))))
            : [member];
        foreach (var part in parts)
        {
            MemberDeclarationSyntax wrapper = LeafActiveSyntax(part);
            foreach (var parent in member.Ancestors().OfType<TypeDeclarationSyntax>())
            {
                var modifiers = parent.Modifiers;
                if (!modifiers.Any(SyntaxKind.PartialKeyword)) modifiers = modifiers.Add(SyntaxFactory.Token(SyntaxKind.PartialKeyword));
                wrapper = LeafActiveSyntax(parent.WithMembers(default)).WithAttributeLists(default).WithBaseList(null).WithParameterList(null).WithModifiers(modifiers)
                    .WithMembers(SyntaxFactory.SingletonList(wrapper)).WithoutLeadingTrivia().WithoutTrailingTrivia();
            }
            var space = SyntaxFactory.NamespaceDeclaration(SyntaxFactory.ParseName(sources.Catalog.Type(EfiLeafReuseSources.Owner)
                .ContainingNamespace.ToDisplayString())).WithMembers(SyntaxFactory.SingletonList(wrapper));
            var unit = SyntaxFactory.CompilationUnit().WithUsings(SyntaxFactory.List(root.Usings.Select(LeafActiveSyntax)))
                .WithMembers(SyntaxFactory.SingletonList<MemberDeclarationSyntax>(space)).NormalizeWhitespace();
            string path = Path.Combine(Path.GetDirectoryName(document.Path)!, "leaf-proof-unrelated-" + documents.Count + ".cs");
            documents.Add(CSharpSourceDocument.FromBytes(path, System.Text.Encoding.UTF8.GetBytes(unit.ToFullString()),
                (CSharpParseOptions)document.Syntax.Options));
        }
        return new(new CSharpSourceCatalog(documents, sources.Catalog.Compilation.References,
            sources.Catalog.Compilation.AssemblyName!, sources.Catalog.Compilation.Options));
    }

    private static T LeafActiveSyntax<T>(T node) where T : SyntaxNode
    {
        var bare = node.WithoutLeadingTrivia().WithoutTrailingTrivia();
        return bare.ReplaceTrivia(bare.DescendantTrivia(descendIntoTrivia: true), (_, _) => default);
    }
    private static EfiLeafReuseSources RebindLeafDocument(EfiLeafReuseSources sources, CSharpSourceDocument document, SyntaxNode root)
    {
        var updated = CSharpSourceDocument.FromBytes(document.Path, System.Text.Encoding.UTF8.GetBytes(root.ToFullString()),
            (CSharpParseOptions)document.Syntax.Options);
        return new(new CSharpSourceCatalog(sources.Catalog.Documents.Select(item => item == document ? updated : item),
            sources.Catalog.Compilation.References, sources.Catalog.Compilation.AssemblyName!, sources.Catalog.Compilation.Options));
    }
}




