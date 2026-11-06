using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Tooling;
using System.Text;

internal static partial class M02UnexpectedVectorPanicEfiIntegrationTests
{
    [HostTest("m02.016.efi bound panic sources follow partial moves and reject changed signatures")]
    private static void PanicSourceControls()
    {
        var source = PanicSources();
        var method = PanicMethod(source, "internal static void Run(");
        var document = source.Document(method);
        var parent = (ClassDeclarationSyntax)method.Parent!;
        var partial = parent.Modifiers.Any(SyntaxKind.PartialKeyword) ? parent
            : parent.AddModifiers(SyntaxFactory.Token(SyntaxKind.PartialKeyword));
        var root = document.Syntax.GetRoot();
        string remaining = root.ReplaceNode(parent, partial.WithMembers(parent.Members.Remove(method)))
            .NormalizeWhitespace().ToFullString();
        string moved = root.ReplaceNode(parent, partial.WithMembers(SyntaxFactory.SingletonList<MemberDeclarationSyntax>(method)))
            .NormalizeWhitespace().ToFullString();
        var relocated = ChangePanic(source, document, remaining, moved);
        var selected = PanicMethod(relocated, "internal static void Run(");
        Require(PanicTerminalTail(selected) && PanicTemporaryAdmissionFirst(selected.ToString()),
            "relocated Run retains active and raw cleanup boundaries");
        Require(HostEfiAdapterSources.ForHost(relocated, PanicOwner, "FailBeforeEntry", "PlaceSentinel")
            .Contains("internal static void Run", StringComparison.Ordinal), "relocated adapter remains projectable");

        var wrong = method.WithReturnType(SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.IntKeyword)).WithTriviaFrom(method.ReturnType))
            .WithBody(SyntaxFactory.Block(SyntaxFactory.ParseStatement("return 0;")));
        var changed = ChangePanic(source, document, root.ReplaceNode(method, wrong).ToFullString());
        RejectPanic(() => PanicMethod(changed, "internal static void Run("), "valid wrong-return Run rejected");

        // Change every partial owner's accessibility together so this is valid C#.
        var ownerDocuments = source.Type(PanicOwner).DeclaringSyntaxReferences.Select(reference => reference.GetSyntax())
            .Cast<ClassDeclarationSyntax>().ToArray();
        var changedDocuments = source.Documents.Select(item =>
        {
            var declarations = ownerDocuments.Where(declaration => declaration.SyntaxTree == item.Syntax).ToArray();
            if (declarations.Length == 0)
                return item;
            var updated = item.Syntax.GetRoot().ReplaceNodes(declarations, (declaration, _) =>
            {
                var token = declaration.Modifiers.Single(modifier => modifier.IsKind(SyntaxKind.PrivateKeyword));
                return declaration.WithModifiers(declaration.Modifiers.Replace(token,
                    SyntaxFactory.Token(token.LeadingTrivia, SyntaxKind.InternalKeyword, token.TrailingTrivia)));
            });
            return CSharpSourceDocument.FromBytes(item.Path, Encoding.UTF8.GetBytes(updated.ToFullString()),
                (CSharpParseOptions)item.Syntax.Options);
        }).ToArray();
        var changedOwner = new CSharpSourceCatalog(changedDocuments, source.Compilation.References,
            source.Compilation.AssemblyName!, source.Compilation.Options);
        Require(!changedOwner.Compilation.GetDiagnostics().Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error),
            "changed owner is valid C# before selection");
        RejectPanic(() => PanicMethod(changedOwner, "internal static void Run("), "adapter accessibility remains independent");
    }

    [HostTest("m02.016.efi panic return selectors reject valid ref and ref-readonly signatures")]
    private static void PanicReturnControls()
    {
        foreach (string prefix in new[] { "", "ref ", "ref readonly " })
        {
            string text = "namespace OS.Boot { internal static class EfiEntry { private static class M02UnexpectedVectorPanicAdapter {"
                + "private static bool s_value; internal static " + prefix + "bool Prepare() { return "
                + (prefix.Length == 0 ? "" : "ref ") + "s_value; } } } }";
            var document = CSharpSourceDocument.FromBytes("ReturnControl.cs", Encoding.UTF8.GetBytes(text),
                new CSharpParseOptions(LanguageVersion.CSharp14));
            var source = new CSharpSourceCatalog([document],
                [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)], "ReturnControl",
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            Require(!source.Compilation.GetDiagnostics().Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error),
                "return control compiles before selection");
            if (prefix.Length == 0)
                _ = PanicMethod(source, "internal static bool Prepare(");
            else
                RejectPanic(() => PanicMethod(source, "internal static bool Prepare("), "by-reference return rejected");
        }
    }

    [HostTest("m02.016.efi panic raw exclusions retain comments and inactive source")]
    private static void PanicRawControls()
    {
        var source = PanicSources();
        var method = PanicMethod(source, "internal static void Run(");
        var document = source.Document(method);
        string raw = method.ToString();
        foreach (string extra in new[]
        {
            "\n// FreePages remains forbidden in the terminal tail.\n",
            "\n#if UNRELATED_DISABLED\n// FailBeforeEntry remains forbidden.\n#endif\n"
        })
        {
            string text = raw.Insert(method.Body!.CloseBraceToken.SpanStart - method.SpanStart, extra);
            var changed = ReplacePanicMethod(source, document, method, text);
            Require(!PanicTerminalTail(PanicMethod(changed, "internal static void Run(")), "raw tail exclusion retained");
        }
        string misleading = raw.Insert(method.Body!.OpenBraceToken.Span.End - method.SpanStart,
            "\n// s_authority.State = 2; FreePages must not escape the original raw boundary.\n");
        var earlier = ReplacePanicMethod(source, document, method, misleading);
        Require(!PanicTerminalTail(PanicMethod(earlier, "internal static void Run(")), "earlier raw anchor rejected");
        string late = raw.Replace("s_authority.State = 2;", "s_authority.State=2; services->FreePages(c, 2);", StringComparison.Ordinal);
        late = late.Insert(late.LastIndexOf('}'), "\n// s_authority.State = 2;\n");
        var later = ReplacePanicMethod(source, document, method, late);
        Require(!PanicTerminalTail(PanicMethod(later, "internal static void Run(")), "late raw anchor cannot hide cleanup");

        const string cleanupCall = "FailBeforeEntry(console, serial, services, c, allocatedC, d, allocatedD, (ulong)image, imageBytes, binding.PageAddress, observationAddress, scratchAddress, selectorsAddress);";
        foreach (string extra in new[] { "\n// " + cleanupCall + "\n", "\n#if UNRELATED_DISABLED\n" + cleanupCall + "\n#endif\n" })
        {
            var changed = ReplacePanicMethod(source, document, method,
                raw.Insert(method.Body.CloseBraceToken.SpanStart - method.SpanStart, extra));
            Require(!PanicTemporaryAdmissionFirst(PanicMethod(changed, "internal static void Run(").ToString()),
                "extra raw cleanup call cannot disappear during active-token projection");
        }
    }

    [HostTest("m02.016.efi SDK member projection rejects attributes and import-dependent bindings")]
    private static void PanicSdkSourceControls()
    {
        var source = PanicSources();
        string[] members = ["private struct Authority", "internal static bool Extent(", "private static void FailBeforeEntry("];
        var selected = PanicSdkMembers(source, members, cleanup: true);
        Require(HostSourceText.Contains(selected.Members, "internal struct Authority")
            && HostSourceText.Contains(selected.Members, "internal static void FailBeforeEntry(")
            && !HostSourceText.Contains(selected.Members, "void Run("), "bounded SDK members and original exposure");
        var method = PanicMethod(source, "internal static bool Extent(");
        var document = source.Document(method);
        var attribute = SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(
            SyntaxFactory.Attribute(SyntaxFactory.ParseName("System.Runtime.CompilerServices.MethodImpl"))
                .WithArgumentList(SyntaxFactory.AttributeArgumentList(SyntaxFactory.SingletonSeparatedList(
                    SyntaxFactory.AttributeArgument(SyntaxFactory.ParseExpression("System.Runtime.CompilerServices.MethodImplOptions.NoInlining")))))));
        var changed = ChangePanic(source, document, document.Syntax.GetRoot().ReplaceNode(method,
            method.AddAttributeLists(attribute)).ToFullString());
        RejectPanic(() => PanicSdkMembers(changed, members, cleanup: true), "selected attributes cannot silently disappear");

        var alias = SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System.UInt64"))
            .WithAlias(SyntaxFactory.NameEquals("PanicWord"));
        var parameter = method.ParameterList.Parameters[0];
        var aliasedMethod = method.ReplaceNode(parameter.Type!, SyntaxFactory.IdentifierName("PanicWord").WithTriviaFrom(parameter.Type!));
        var root = ((CompilationUnitSyntax)document.Syntax.GetRoot()).ReplaceNode(method, aliasedMethod).AddUsings(alias);
        var dependent = ChangePanic(source, document, root.NormalizeWhitespace().ToFullString());
        RejectPanic(() => PanicSdkMembers(dependent, members, cleanup: true), "import-dependent SDK projection rejected");
    }

    private static void RejectPanic<T>(Func<T> select, string reason)
    {
        bool rejected = false;
        try
        {
            _ = select();
        }
        catch (InvalidOperationException)
        {
            rejected = true;
        }
        Require(rejected, reason);
    }

    private static CSharpSourceCatalog ReplacePanicMethod(CSharpSourceCatalog source, CSharpSourceDocument document,
        MethodDeclarationSyntax method, string text)
    {
        var replacement = (MethodDeclarationSyntax)SyntaxFactory.ParseMemberDeclaration(text,
            options: (CSharpParseOptions)method.SyntaxTree.Options)!;
        return ChangePanic(source, document, document.Syntax.GetRoot().ReplaceNode(method, replacement).ToFullString());
    }

    private static CSharpSourceCatalog ChangePanic(CSharpSourceCatalog source, CSharpSourceDocument original,
        string replacement, string? added = null)
    {
        var parse = (CSharpParseOptions)original.Syntax.Options;
        var documents = source.Documents.Select(document => document.Path == original.Path
            ? CSharpSourceDocument.FromBytes(document.Path, Encoding.UTF8.GetBytes(replacement), parse) : document).ToList();
        if (added is not null)
            documents.Add(CSharpSourceDocument.FromBytes(Path.Combine(Path.GetDirectoryName(original.Path)!, "ArbitraryPanicPart.cs"),
                Encoding.UTF8.GetBytes(added), parse));
        var candidate = new CSharpSourceCatalog(documents, source.Compilation.References,
            source.Compilation.AssemblyName!, source.Compilation.Options);
        foreach (var declaration in candidate.Type(PanicOwner).DeclaringSyntaxReferences.Select(reference => reference.GetSyntax()))
            Require(!candidate.Compilation.GetSemanticModel(declaration.SyntaxTree).GetDiagnostics(declaration.Span)
                .Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error), "panic mutation binds before selection");
        return candidate;
    }
}
