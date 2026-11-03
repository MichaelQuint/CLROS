using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Tooling;

internal static class BootPageAllocatorStateMaterializerSourceTests
{
    [HostTest("materializer source discovery preserves moved partials and rejects changed contracts and foreign dependencies")]
    private static void SourceControls()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        var host = HostSourceCatalog.Project("src/OS.Kernel/OS.Kernel.csproj");
        var efi = HostSourceCatalog.Efi(EfiOptions.Parse([], false).Profile());
        var fixture = HostSourceCatalog.Project("tests/OS.HostTests/OS.HostTests.csproj")
            .Method("BootPageAllocatorStateMaterializerTests", "CreateNoStdLibWorkspace");
        string shim = fixture.DescendantTokens().Single(token => token.IsKind(SyntaxKind.MultiLineRawStringLiteralToken)
            && token.ValueText.StartsWith("namespace System", StringComparison.Ordinal)).ValueText;
        RunControls(root, host, efi, shim);
    }

    internal static void RunControls(string root, CSharpSourceCatalog originalHost, CSharpSourceCatalog originalEfi, string shim)
    {
        const string owner = "OS.Kernel.Memory.BootPageAllocatorStateMaterializer";
        var selected = BootPageAllocatorStateMaterializerTests.MaterializerSources(root, originalHost, originalEfi);
        HostCheck.True(BootPageAllocatorStateMaterializerTests.CheckMaterializerSource(originalEfi, selected), "actual EFI baseline");
        BootPageAllocatorStateMaterializerTests.ValidateMaterializerShimClosure(selected, shim);
        var host = new CSharpSourceCatalog(originalHost.Documents.Where(document => selected.Any(item => item.Path == document.Path)),
            originalHost.Compilation.References, "MaterializerHostControls", originalHost.Compilation.Options);
        var parse = new CSharpParseOptions(LanguageVersion.CSharp14, preprocessorSymbols: ["NOSTDLIB_EFI"]);
        var shimDocument = CSharpSourceDocument.FromBytes(Path.Combine(root, "artifacts", "materializer-control-shim.cs"), System.Text.Encoding.UTF8.GetBytes(shim), parse);
        var efi = new CSharpSourceCatalog(selected.Select(document => CSharpSourceDocument.FromBytes(document.Path,
            System.Text.Encoding.UTF8.GetBytes(document.Text), parse)).Append(shimDocument), [], "MaterializerEfiControls",
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        var source = efi.Document(efi.Method(owner, "Materialize"));
        var parent = source.Syntax.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().Single();

        string movedPath = Path.ChangeExtension(source.Path, ".moved-control.cs");
        var renamedHost = Rebuild(host, host.Documents.Select(document => document.Path == source.Path ? Move(document, movedPath) : document));
        var renamedEfi = Rebuild(efi, efi.Documents.Select(document => document.Path == source.Path ? Move(document, movedPath) : document));
        Accept(renamedHost, renamedEfi, "owner moved without filename conventions");

        var preflight = efi.Method(owner, "TryValidatePointerAndSummaryMetadata");
        var partial = parent.WithModifiers(parent.Modifiers.Add(SyntaxFactory.Token(default, SyntaxKind.PartialKeyword,
            SyntaxFactory.TriviaList(SyntaxFactory.Space))));
        partial = partial.WithMembers(parent.Members.Remove(preflight));
        string split = source.Syntax.GetRoot().ReplaceNode(parent, partial).ToFullString();
        string helper = "namespace OS.Kernel.Memory; public static unsafe partial class BootPageAllocatorStateMaterializer {\n"
            + preflight.ToFullString() + "\n}";
        var movedHost = Change(host, source.Path, split, movedPath, helper);
        var movedEfi = Change(efi, source.Path, split, movedPath, helper);
        Accept(movedHost, movedEfi, "preflight moved to a separate partial source");

        RejectContract(source.Text.Insert(parent.OpenBraceToken.Span.End,
            "\n#if !NOSTDLIB_EFI\nprivate static string HiddenManagedState;\n#endif\n"),
            "forbidden constructs remain rejected in inactive branches");

        var maximum = parent.DescendantNodes().OfType<VariableDeclaratorSyntax>().Single(variable => variable.Identifier.ValueText == "MaximumUnsignedLong");
        var zero = maximum.WithInitializer(maximum.Initializer!.WithValue(SyntaxFactory.ParseExpression("0UL")));
        string zeroText = source.Syntax.GetRoot().ReplaceNode(maximum, zero).ToFullString();
        Reject(() => BootPageAllocatorStateMaterializerTests.MaterializerSources(root, Change(host, source.Path, zeroText), efi),
            "materializer_efi_source_join", "host and EFI bytes must match");
        RejectContract(zeroText, "maximum unsigned value contract");

        var size = parent.Members.OfType<PropertyDeclarationSyntax>().Single(property => property.Identifier.ValueText == "ActualSourceNodeStructureSize");
        RejectContract(source.Syntax.GetRoot().ReplaceNode(size, size.WithExpressionBody(SyntaxFactory.ArrowExpressionClause(
            SyntaxFactory.ParseExpression("48UL")))).ToFullString(), "sizeof may not become a literal");
        var materialize = efi.Method(owner, "Materialize");
        var write = materialize.DescendantNodes().OfType<AssignmentExpressionSyntax>().Single(assignment => assignment.Left.ToString() == "destinationState[0]");
        RejectContract(source.Syntax.GetRoot().ReplaceNode(write.Left, SyntaxFactory.ParseExpression("destinationState[1]")).ToFullString(),
            "original destination slot remains required");
        RejectContract(source.Syntax.GetRoot().ReplaceNode(preflight, preflight.WithModifiers(
            SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword), SyntaxFactory.Token(SyntaxKind.StaticKeyword))))
            .NormalizeWhitespace().ToFullString(), "preflight retains private static bool signature");
        var statements = preflight.Body!.Statements;
        int bound = statements.IndexOf(statements.OfType<IfStatementSyntax>().Single(branch => branch.Condition.ToString() == "sourceNodeCount > MaximumSourceNodeCount"));
        int capacity = statements.IndexOf(statements.OfType<IfStatementSyntax>().Single(branch => branch.Condition.ToString() == "sourceNodeCapacity < sourceNodeCount"));
        var reordered = statements.ToArray();
        (reordered[bound], reordered[capacity]) = (reordered[capacity], reordered[bound]);
        RejectContract(source.Syntax.GetRoot().ReplaceNode(preflight, preflight.WithBody(preflight.Body.WithStatements(SyntaxFactory.List(reordered)))).ToFullString(),
            "pointer validation precedence remains ordered");

        var resultDocument = efi.TypeSources("OS.Kernel.Memory.BootPageAllocatorStateMaterializationResult").Single();
        var summary = resultDocument.Syntax.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Single(call => call.Expression.ToString().EndsWith(".IsRepresentableSuccessSummary", StringComparison.Ordinal));
        var changedResult = resultDocument.Syntax.GetRoot().ReplaceNode(summary, SyntaxFactory.LiteralExpression(SyntaxKind.TrueLiteralExpression)).ToFullString();
        var noSummary = Change(efi, resultDocument.Path, changedResult);
        Valid(noSummary);
        HostCheck.True(!BootPageAllocatorStateMaterializerTests.CheckMaterializerSource(noSummary,
            noSummary.Documents.Where(document => document != shimDocument).ToArray()), "summary must use its bound validator");

        var count = parent.DescendantNodes().OfType<VariableDeclaratorSyntax>().Single(variable => variable.Identifier.ValueText == "MaximumSourceNodeCount");
        string foreign = source.Syntax.GetRoot().ReplaceNode(count.Initializer!.Value, SyntaxFactory.ParseExpression("Foreign.MaterializerDependency.Maximum")).ToFullString();
        string foreignPath = Path.Combine(Path.GetDirectoryName(source.Path)!, "dependency-control.g.cs");
        const string foreignSource = "// <auto-generated/>\nnamespace Foreign; public static class MaterializerDependency { public const ulong Maximum = 4096; }";
        var foreignHost = Change(host, source.Path, foreign, foreignPath, foreignSource);
        var foreignEfi = Change(efi, source.Path, foreign, foreignPath, foreignSource);
        Valid(foreignEfi);
        Reject(() => BootPageAllocatorStateMaterializerTests.MaterializerSources(root, foreignHost, foreignEfi),
            "materializer_dependency_layer", "a generated marker cannot hide a foreign first-party dependency");

        Reject(() => BootPageAllocatorStateMaterializerTests.ValidateMaterializerShimClosure(selected,
            shim.Replace("public struct UInt64 { }", "", StringComparison.Ordinal)), "materializer_shim_surface", "required shim type missing");
        Reject(() => BootPageAllocatorStateMaterializerTests.ValidateMaterializerShimClosure(selected,
            shim + "\nnamespace System { public class Unreviewed { } }"), "materializer_shim_surface", "unreviewed shim type added");
        string conditional = source.Text.Remove(count.Initializer!.Value.SpanStart, count.Initializer.Value.Span.Length)
            .Insert(count.Initializer.Value.SpanStart, "\n#if NOSTDLIB_EFI\nConditionalMaterializerDependency.Maximum\n#else\n4096\n#endif\n");
        string conditionalSource = "#if NOSTDLIB_EFI\nnamespace OS.Kernel.Memory; public static class ConditionalMaterializerDependency { public const ulong Maximum = 4096; }\n#endif\n";
        var conditionalHost = Change(host, source.Path, conditional, foreignPath, conditionalSource);
        var conditionalEfi = Change(efi, source.Path, conditional, foreignPath, conditionalSource);
        Valid(conditionalEfi);
        var conditionalSelection = BootPageAllocatorStateMaterializerTests.MaterializerSources(root, conditionalHost, conditionalEfi);
        Reject(() => BootPageAllocatorStateMaterializerTests.ValidateMaterializerShimClosure(conditionalSelection, shim),
            "materializer_shim_closure", "missing NOSTDLIB-only dependency fails closed");

        void Accept(CSharpSourceCatalog currentHost, CSharpSourceCatalog currentEfi, string reason)
        {
            var documents = BootPageAllocatorStateMaterializerTests.MaterializerSources(root, currentHost, currentEfi);
            Valid(currentEfi);
            HostCheck.True(BootPageAllocatorStateMaterializerTests.CheckMaterializerSource(currentEfi, documents), reason);
            BootPageAllocatorStateMaterializerTests.ValidateMaterializerShimClosure(documents, shim);
        }
        void RejectContract(string text, string reason)
        {
            var changed = Change(efi, source.Path, text);
            Valid(changed);
            HostCheck.True(!BootPageAllocatorStateMaterializerTests.CheckMaterializerSource(changed,
                changed.Documents.Where(document => document != shimDocument).ToArray()), reason);
        }
        static void Valid(CSharpSourceCatalog catalog)
        {
            var errors = catalog.Compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray();
            HostCheck.True(errors.Length == 0, "mutation must bind: " + string.Join("; ", errors.Select(error => error.ToString())));
        }
        static void Reject(Action action, string prefix, string reason)
        {
            try
            {
                action();
            }
            catch (InvalidOperationException error) when (error.Message.StartsWith(prefix, StringComparison.Ordinal))
            {
                return;
            }
            throw new InvalidOperationException("materializer_control_not_rejected " + reason);
        }
        static CSharpSourceCatalog Rebuild(CSharpSourceCatalog catalog, IEnumerable<CSharpSourceDocument> documents) =>
            new(documents, catalog.Compilation.References, catalog.Compilation.AssemblyName!, catalog.Compilation.Options);
        static CSharpSourceDocument Move(CSharpSourceDocument document, string path) =>
            CSharpSourceDocument.FromBytes(path, System.Text.Encoding.UTF8.GetBytes(document.Text), (CSharpParseOptions)document.Syntax.Options);
        static CSharpSourceCatalog Change(CSharpSourceCatalog catalog, string path, string text, string? extraPath = null, string? extra = null)
        {
            var parse = (CSharpParseOptions)catalog.Documents.Single(document => document.Path == path).Syntax.Options;
            var replacement = CSharpSourceDocument.FromBytes(path, System.Text.Encoding.UTF8.GetBytes(text), parse);
            var documents = catalog.Documents.Select(document => document.Path == path ? replacement : document);
            if (extraPath is not null)
                documents = documents.Append(CSharpSourceDocument.FromBytes(extraPath, System.Text.Encoding.UTF8.GetBytes(extra!), parse));
            return Rebuild(catalog, documents);
        }
    }
}
