using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Tooling;
using System.Text;

internal static partial class M02DoubleFaultIstEfiIntegrationTests
{
    [HostTest("m02.015.efi Prepare rejects valid ref and ref-readonly return signatures")]
    private static void DoubleFaultReturnControls()
    {
        foreach (string prefix in new[] { "", "ref ", "ref readonly " })
        {
            string text = "namespace OS.Boot { internal static class EfiEntry { private static class M02DoubleFaultIstAdapter {"
                + "private static bool s_value; internal static " + prefix + "bool Prepare() { return "
                + (prefix.Length == 0 ? "" : "ref ") + "s_value; } } } }";
            var document = CSharpSourceDocument.FromBytes("ReturnControl.cs", Encoding.UTF8.GetBytes(text),
                new CSharpParseOptions(LanguageVersion.CSharp14));
            var source = new CSharpSourceCatalog([document],
                [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)], "ReturnControl",
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            Require(!source.Compilation.GetDiagnostics().Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error),
                "return signature control must be valid C# before selection");
            bool rejected = false;
            try
            {
                _ = DoubleFaultMethod(source, "Prepare", SpecialType.System_Boolean);
            }
            catch (InvalidOperationException)
            {
                rejected = true;
            }
            Require(rejected == (prefix.Length != 0), "only the original by-value bool return is accepted");
        }
    }

    [HostTest("m02.015.efi bound Run follows a partial move and rejects a changed signature")]
    private static void DoubleFaultSourceControls()
    {
        var source = DoubleFaultSources();
        var method = DoubleFaultMethod(source, "Run", SpecialType.System_Void);
        var document = source.Document(method);
        var parent = (ClassDeclarationSyntax)method.Parent!;
        var partial = parent.Modifiers.Any(SyntaxKind.PartialKeyword) ? parent
            : parent.AddModifiers(SyntaxFactory.Token(SyntaxKind.PartialKeyword));
        var root = document.Syntax.GetRoot();
        string remaining = root.ReplaceNode(parent, partial.WithMembers(parent.Members.Remove(method)))
            .NormalizeWhitespace().ToFullString();
        string moved = root.ReplaceNode(parent, partial.WithMembers(SyntaxFactory.SingletonList<MemberDeclarationSyntax>(method)))
            .NormalizeWhitespace().ToFullString();
        var relocated = ChangeDoubleFault(source, document, remaining, moved);
        var selected = DoubleFaultMethod(relocated, "Run", SpecialType.System_Void);
        Require(DoubleFaultFlow(selected), "moved selected Run retains its terminal boundary");
        _ = HostEfiFaultSources.StageOneCaller(relocated, selected);
        Require(HostEfiAdapterSources.ForHost(relocated, AdapterOwner, "FailBeforeEntry", "SamePrepared")
            .Contains("internal static void Run", StringComparison.Ordinal), "moved adapter remains projectable");

        var wrong = method.WithReturnType(SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.IntKeyword)).WithTriviaFrom(method.ReturnType))
            .WithBody(SyntaxFactory.Block(SyntaxFactory.ParseStatement("return 0;")));
        var wrongSource = ChangeDoubleFault(source, document, root.ReplaceNode(method, wrong).ToFullString());
        bool rejected = false;
        try
        {
            _ = DoubleFaultMethod(wrongSource, "Run", SpecialType.System_Void);
        }
        catch (InvalidOperationException)
        {
            rejected = true;
        }
        Require(rejected, "valid wrong-return Run rejects independently of compiler errors");
    }

    [HostTest("m02.015.efi raw post-attempt exclusions retain comments and disabled branches")]
    private static void DoubleFaultRawTerminalControls()
    {
        var source = DoubleFaultSources();
        var method = DoubleFaultMethod(source, "Run", SpecialType.System_Void);
        var document = source.Document(method);
        foreach (string suffix in new[]
        {
            "\n// FreePages is forbidden even in this raw tail comment.\n",
            "\n#if UNRELATED_DISABLED\n// FailBeforeEntry is forbidden in an inactive raw tail.\n#endif\n"
        })
        {
            string raw = method.ToString();
            int offset = method.Body!.CloseBraceToken.SpanStart - method.SpanStart;
            var mutation = (MethodDeclarationSyntax)SyntaxFactory.ParseMemberDeclaration(raw.Insert(offset, suffix),
                options: (CSharpParseOptions)method.SyntaxTree.Options)!;
            var changed = ChangeDoubleFault(source, document, document.Syntax.GetRoot().ReplaceNode(method, mutation).ToFullString());
            Require(!DoubleFaultFlow(DoubleFaultMethod(changed, "Run", SpecialType.System_Void)),
                "post-attempt raw exclusion must not become an active-token check");
        }
        var loop = method.DescendantNodes().OfType<WhileStatementSyntax>()
            .Single(statement => statement.Condition.IsKind(SyntaxKind.TrueLiteralExpression));
        var reordered = method.WithBody(method.Body!.WithStatements(method.Body.Statements.Remove(loop).Insert(0, loop)));
        var earlierLoop = ChangeDoubleFault(source, document, document.Syntax.GetRoot().ReplaceNode(method, reordered).ToFullString());
        Require(!DoubleFaultFlow(DoubleFaultMethod(earlierLoop, "Run", SpecialType.System_Void)),
            "terminal loop cannot precede the attempted transition");
        string deceptive = method.ToString().Replace("s_state = 2;", "s_state=2; services->FreePages(c,3);", StringComparison.Ordinal);
        deceptive = deceptive.Insert(deceptive.LastIndexOf('}'), "\n// s_state = 2;\n");
        var deceptiveMethod = (MethodDeclarationSyntax)SyntaxFactory.ParseMemberDeclaration(deceptive,
            options: (CSharpParseOptions)method.SyntaxTree.Options)!;
        var deceptiveSource = ChangeDoubleFault(source, document, document.Syntax.GetRoot().ReplaceNode(method, deceptiveMethod).ToFullString());
        Require(!DoubleFaultFlow(DoubleFaultMethod(deceptiveSource, "Run", SpecialType.System_Void)),
            "a later raw comment cannot move the post-attempt cleanup boundary");
    }

    private static CSharpSourceCatalog ChangeDoubleFault(CSharpSourceCatalog source, CSharpSourceDocument original,
        string replacement, string? added = null)
    {
        var parse = (CSharpParseOptions)original.Syntax.Options;
        string movedPath = Path.Combine(Path.GetDirectoryName(original.Path)!, "ArbitraryDoubleFaultPart.cs");
        var documents = source.Documents.Select(document => document.Path == original.Path
            ? CSharpSourceDocument.FromBytes(document.Path, Encoding.UTF8.GetBytes(replacement), parse) : document).ToList();
        if (added is not null)
            documents.Add(CSharpSourceDocument.FromBytes(movedPath, Encoding.UTF8.GetBytes(added), parse));
        var candidate = new CSharpSourceCatalog(documents, source.Compilation.References,
            source.Compilation.AssemblyName!, source.Compilation.Options);
        foreach (var declaration in candidate.Type(AdapterOwner).DeclaringSyntaxReferences.Select(reference => reference.GetSyntax()))
            Require(!candidate.Compilation.GetSemanticModel(declaration.SyntaxTree).GetDiagnostics(declaration.Span)
                .Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error), "double-fault source mutation binds");
        return candidate;
    }
}
