using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Tooling;

internal static class HostSecurityCookieSourceGuardTests
{
    [HostTest("cookie source ownership follows partial declarations and rejects changed exports or storage values")]
    private static void BoundControls()
    {
        const string owner = "OS.Std.Binary.IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosa";
        const string name = "M04SecurityCookieStorageAnchor";
        const ulong value = 0x434c_524f_5347_5334UL;
        var source = HostSourceCatalog.Efi(EfiOptions.Parse(["--EnableM04IndependentEndianConformanceProbe"], true).Profile());
        HostCheck.True(Check(source));
        var method = source.Method(owner, name);
        var document = source.Document(method);
        var root = document.Syntax.GetCompilationUnitRoot();
        var returned = (ReturnStatementSyntax)method.Body!.Statements.Single();
        HostCheck.True(!Check(Change(method.WithBody(method.Body.ReplaceNode(returned.Expression!,
            SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(value + 1)))))),
            "wrong cookie storage constant");
        HostCheck.True(!Check(Change(method.WithAttributeLists(default))), "export removed");
        HostCheck.True(!Check(Change(method.WithParameterList(method.ParameterList.AddParameters(
            SyntaxFactory.Parameter(SyntaxFactory.Identifier("extra")).WithType(SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.IntKeyword))))))),
            "extra ABI parameter");
        HostCheck.True(!Check(Change(method.WithBody(method.Body.WithStatements(method.Body.Statements.Insert(0,
            SyntaxFactory.EmptyStatement()))))), "anchor gained a statement");
        var expression = method.WithBody(null).WithExpressionBody(SyntaxFactory.ArrowExpressionClause(returned.Expression!))
            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));
        HostCheck.True(Check(Change(expression)), "equivalent expression body");

        var declaration = method.Ancestors().OfType<ClassDeclarationSyntax>().First();
        var remaining = declaration.RemoveNode(method, SyntaxRemoveOptions.KeepExteriorTrivia)!
            .AddModifiers(SyntaxFactory.Token(SyntaxKind.PartialKeyword).WithTrailingTrivia(SyntaxFactory.Space));
        var changed = Replace(document, root.ReplaceNode(declaration, remaining));
        var activeMethod = method.ReplaceTrivia(method.DescendantTrivia(descendIntoTrivia: true), (_, _) => default);
        string movedPath = document.Path + ".cookie-control.cs";
        var moved = CSharpSourceDocument.FromBytes(movedPath, System.Text.Encoding.UTF8.GetBytes(
            "namespace OS.Std.Binary; public static partial class IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosa { "
            + activeMethod.NormalizeWhitespace().ToFullString() + " }"), (CSharpParseOptions)document.Syntax.Options);
        HostCheck.True(Check(Rebuild(source.Documents.Select(item => item.Path == document.Path ? changed : item).Append(moved))),
            "cookie moved to another partial source");
        var duplicate = CSharpSourceDocument.FromBytes(movedPath, System.Text.Encoding.UTF8.GetBytes(
            "namespace OS.Std.Binary; public static class ForeignCookie { " + activeMethod.NormalizeWhitespace().ToFullString() + " }"),
            (CSharpParseOptions)document.Syntax.Options);
        HostCheck.True(!Check(Rebuild(source.Documents.Append(duplicate))), "duplicate runtime export");
        HostCheck.True(!Check(Rebuild(source.Documents.Select(item => item.Path == document.Path ? changed : item).Append(duplicate))),
            "foreign owner of the sole export");

        CSharpSourceCatalog Change(MethodDeclarationSyntax after) => Rebuild(source.Documents.Select(item =>
            item.Path == document.Path ? Replace(document, root.ReplaceNode(method, after)) : item));
        CSharpSourceDocument Replace(CSharpSourceDocument original, CompilationUnitSyntax after) =>
            new(original.Path, "in-memory cookie control", CSharpSyntaxTree.Create(after,
                (CSharpParseOptions)original.Syntax.Options, original.Path));
        CSharpSourceCatalog Rebuild(IEnumerable<CSharpSourceDocument> documents) =>
            new(documents, source.Compilation.References, source.Compilation.AssemblyName!, source.Compilation.Options);
        static bool Check(CSharpSourceCatalog candidate)
        {
            try
            {
                return HostSecurityCookieSourceGuard.Check(candidate, owner, name, value);
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }
    }
}
