using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Tooling;

internal static class HostRetainedMappingSourceGuardTests
{
    [HostTest("retained device append checks preserve bound locals across moved sources and reject ownership changes")]
    private static void BoundControls()
    {
        foreach (string leaf in new[]
        {
            "M16008",
            "M16011",
            "M16012",
            "M10017",
            "M16025",
            "M16027",
            "M10018"
        }

        )
        {
            var source = Load(leaf);
            Record(leaf + " ECAM", true, HostRetainedMappingSourceGuard.Check(source, "EcamStart"));
            Record(leaf + " BAR", leaf != "M16008", HostRetainedMappingSourceGuard.Check(source, "VirtioBarStart"));
        }

        var baseline = Load("M16025");
        const string owner = "OS.Boot.FirstExitRetainedMappings";
        var method = baseline.Method(owner, "WriteRangesCore");
        var document = baseline.Document(method);
        var model = baseline.Compilation.GetSemanticModel(method.SyntaxTree);
        var field = baseline.Type("OS.Boot.FirstExitImageReferences").GetMembers("EcamStart").Single();
        var assignment = method.DescendantNodes().OfType<AssignmentExpressionSyntax>().Single(node => Same(model.GetSymbolInfo(node.Right).Symbol, field));
        var statement = (ExpressionStatementSyntax)assignment.Parent!;
        var block = (BlockSyntax)statement.Parent!;
        int position = block.Statements.IndexOf(statement);
        var lengthStatement = (ExpressionStatementSyntax)block.Statements[position + 1];
        var lengthWrite = (AssignmentExpressionSyntax)lengthStatement.Expression;
        var start = (ElementAccessExpressionSyntax)assignment.Left;
        var length = (ElementAccessExpressionSyntax)lengthWrite.Left;
        var index = (ILocalSymbol)model.GetSymbolInfo(start.ArgumentList.Arguments[0].Expression).Symbol!;
        var originalParameter = (IParameterSymbol)model.GetSymbolInfo(((MemberAccessExpressionSyntax)assignment.Right).Expression).Symbol!;
        var locals = new[]
        {
            model.GetSymbolInfo(start.Expression).Symbol!,
            model.GetSymbolInfo(length.Expression).Symbol!,
            index,
            originalParameter
        };
        var renames = locals.Select((symbol, n) => (symbol, name: "renamed" + n)).ToArray();
        var tokens = method.DescendantTokens().Where(token => token.IsKind(SyntaxKind.IdentifierToken) && Symbol(token.Parent!) is { } symbol && renames.Any(pair => Same(pair.symbol, symbol))).ToArray();
        var renamed = method.ReplaceTokens(tokens, (token, _) => SyntaxFactory.Identifier(token.LeadingTrivia, renames.Single(pair => Same(pair.symbol, Symbol(token.Parent!))).name, token.TrailingTrivia));
        Record("bound parameter array and index renaming", true, Check(renamed));
        var moved = new CSharpSourceDocument(document.Path + ".moved.cs", document.Sha256, CSharpSyntaxTree.ParseText(document.Text, (CSharpParseOptions)document.Syntax.Options, document.Path + ".moved.cs"));
        var movedSource = new CSharpSourceCatalog(baseline.Documents.Select(item => item.Path == document.Path ? moved : item), baseline.Compilation.References, baseline.Compilation.AssemblyName!, baseline.Compilation.Options);
        Record("owner moved to different source filename", true, HostRetainedMappingSourceGuard.Check(movedSource, "EcamStart"));
        Record("fixed start index", false, Check(method.ReplaceNode(start.ArgumentList.Arguments[0].Expression, SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(8)))));
        Record("missing length increment", false, Check(method.ReplaceNode(length.ArgumentList.Arguments[0].Expression, SyntaxFactory.IdentifierName(index.Name))));
        Record("length overwrites start array", false, Check(method.ReplaceNode(length.Expression, start.Expression.WithoutTrivia())));
        Record("fixed reset discards previous owners", false, Check(method.ReplaceNode(block, block.WithStatements(block.Statements.Insert(position, SyntaxFactory.ParseStatement(index.Name + " = 8;"))))));
        Record("pointer variable replaced", false, Check(method.ReplaceNode(block, block.WithStatements(block.Statements.Insert(position, SyntaxFactory.ParseStatement(start.Expression + " = " + length.Expression + ";"))))));
        var dormant = SyntaxFactory.IfStatement(SyntaxFactory.LiteralExpression(SyntaxKind.FalseLiteralExpression), SyntaxFactory.Block(statement, lengthStatement));
        Record("dormant append pair", false, Check(method.ReplaceNode(block, block.WithStatements(block.Statements.RemoveAt(position + 1).RemoveAt(position).Insert(position, dormant)))));
        var copy = SyntaxFactory.ParseStatement("var copiedReference = " + originalParameter.Name + ";");
        var copied = assignment.ReplaceNode(((MemberAccessExpressionSyntax)assignment.Right).Expression, SyntaxFactory.IdentifierName("copiedReference"));
        var changedBlock = block.ReplaceNode(assignment, copied);
        Record("copied receiver replaces original parameter", false, Check(method.ReplaceNode(block, changedBlock.WithStatements(changedBlock.Statements.Insert(position, copy)))));
        ISymbol? Symbol(SyntaxNode node) => node switch
        {
            VariableDeclaratorSyntax variable => model.GetDeclaredSymbol(variable),
            ParameterSyntax parameter => model.GetDeclaredSymbol(parameter),
            IdentifierNameSyntax name => model.GetSymbolInfo(name).Symbol,
            _ => null
        };
        bool Check(MethodDeclarationSyntax changed)
        {
            var root = document.Syntax.GetCompilationUnitRoot().ReplaceNode(method, changed);
            var replacement = new CSharpSourceDocument(document.Path, "in-memory mutation", CSharpSyntaxTree.Create(root, (CSharpParseOptions)document.Syntax.Options, document.Path));
            var source = new CSharpSourceCatalog(baseline.Documents.Select(item => item.Path == document.Path ? replacement : item), baseline.Compilation.References, baseline.Compilation.AssemblyName!, baseline.Compilation.Options);
            var actual = source.Method(owner, "WriteRangesCore");
            var errors = source.Compilation.GetSemanticModel(actual.SyntaxTree).GetDiagnostics(actual.Span).Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray();
            if (errors.Length != 0)
                throw new Exception("invalid mutation " + string.Join(";", errors.Select(error => error.ToString())));
            return HostRetainedMappingSourceGuard.Check(source, "EcamStart");
        }

        void Record(string name, bool expected, bool actual)
        {
            HostCheck.True(expected == actual, name);
        }

        static bool Same(ISymbol? first, ISymbol? second) => first is not null && second is not null && SymbolEqualityComparer.Default.Equals(first, second);
        static CSharpSourceCatalog Load(string leaf) => HostSourceCatalog.Efi(EfiOptions.Parse(["--Enable" + leaf + "Probe"], true).Profile());
    }
}
