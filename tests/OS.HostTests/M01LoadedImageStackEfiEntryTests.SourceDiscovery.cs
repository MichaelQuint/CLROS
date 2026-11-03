using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Tooling;

internal static partial class M01LoadedImageStackEfiEntryTests
{
    private const string EntryOwner = "OS.Boot.EfiEntry";
    private const string StageOwner = EntryOwner + "+M01StageOneAdapter";
    private const string StageTwoOwner = EntryOwner + "+M01StageTwoAdapter";
    private const string RuntimeOwner = "System.Runtime.X64StackProbeRuntime";

    private static CSharpSourceCatalog Enabled() => Selected(true);

    private static CSharpSourceCatalog Selected(bool enabled) => HostSourceCatalog.Efi(EfiOptions.Parse(
        enabled ? ["--EnableM01LoadedImageStackProbe"] : ["--BootPolicy", "Diagnostic"], true).Profile());

    private static CSharpSourceCatalog Rhp() => HostSourceCatalog.Efi(EfiOptions.Parse(
        ["--EnableM01LoadedImageStackProbe", "--EnableM01LiveAllocatorProbe",
         "--EnableM04IndependentEndianConformanceProbe", "--EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe",
         "--EnableM04ArithmeticConformanceProbe", "--EnableM04LayoutContractProbe",
         "--EnableM04ManagedHeapPageRangeProbe", "--EnableM04RhpNewFastProbe"], true).Profile());

    // Selected members are an inspection view, never concatenated compiler inputs.
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<CSharpSourceCatalog, string> StageViews = new();

    private static string StageText(CSharpSourceCatalog source) => StageViews.GetValue(source,
        static snapshot => HostSourceText.Declarations(snapshot, StageOwner));

    private static bool BoundCoordinatorCalls(CSharpSourceCatalog source)
    {
        var entry = source.Type(EntryOwner);
        foreach (string name in new[]
        {
            "PrepareFromStaticScratch",
            "RecordTransferFromStaticState",
            "ObserveContinuationRspFromStaticState",
            "ReportUnexpectedReturnFromStaticState"
        })
        {
            int count = HostSourceOperations.CallsTo(source, "OS.Boot.M01LoadedImageStackIntegration", name)
                .Count(call => call.Ancestors().OfType<TypeDeclarationSyntax>().Any(declaration =>
                    SymbolEqualityComparer.Default.Equals(source.Compilation.GetSemanticModel(declaration.SyntaxTree)
                        .GetDeclaredSymbol(declaration), entry)));
            if (count != 1)
                return false;
        }

        return true;
    }

    private static string? Body(CSharpSourceCatalog source, string owner, string name)
    {
        var method = source.Method(owner, name);
        if (source.Compilation.GetSemanticModel(method.SyntaxTree).GetDiagnostics(method.Span).Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
            throw new InvalidOperationException("source_catalog_invalid_method " + owner + "." + name);
        return method.Body is null ? null : string.Join("\n", method.Body.Statements.Select(HostSourceText.Active));
    }

    private static CSharpSourceCatalog MutateMethod(CSharpSourceCatalog source, string owner, string method, string search, string replacement)
    {
        var declaration = source.Method(owner, method);
        return ReplaceTokens(source, declaration, search, replacement);
    }

    private static CSharpSourceCatalog Mutate(CSharpSourceCatalog source, string owner, string search, string replacement)
    {
        // Search only declarations of the bound owner, in compiler source order.
        // Same-name methods or text in other owners cannot become a fallback.
        var declarations = source.Type(owner).DeclaringSyntaxReferences.Select(reference => reference.GetSyntax()).ToArray();
        foreach (var document in source.Documents)
            foreach (var declaration in declarations.Where(declaration => declaration.SyntaxTree == document.Syntax).OrderBy(declaration => declaration.SpanStart))
                if (TokenSpan(declaration, search) is { } span)
                    return ReplaceSpan(source, document, span.Start, span.Length, replacement);
        throw new InvalidOperationException("mutation_anchor_missing " + search);
    }

    private static CSharpSourceCatalog ReplaceTokens(CSharpSourceCatalog source, SyntaxNode declaration, string search, string replacement)
    {
        var span = TokenSpan(declaration, search) ?? throw new InvalidOperationException("mutation_anchor_missing " + search);
        return ReplaceSpan(source, source.Document(declaration), span.Start, span.Length, replacement);
    }

    private static Microsoft.CodeAnalysis.Text.TextSpan? TokenSpan(SyntaxNode declaration, string search)
    {
        var expected = SyntaxFactory.ParseTokens(search).Where(token => !token.IsKind(SyntaxKind.EndOfFileToken)).ToArray();
        if (expected.Length == 0)
            throw new InvalidOperationException("mutation_anchor_empty");
        var tokens = declaration.DescendantTokens().ToArray();
        for (int index = 0; index <= tokens.Length - expected.Length; index++)
        {
            int offset = 0;
            while (offset < expected.Length
                && (tokens[index + offset].RawKind == expected[offset].RawKind
                    || expected[offset].IsKind(SyntaxKind.IdentifierToken)
                        && SyntaxFacts.GetContextualKeywordKind(expected[offset].ValueText) == tokens[index + offset].Kind())
                && tokens[index + offset].Text == expected[offset].Text)
                offset++;
            if (offset == expected.Length)
                return Microsoft.CodeAnalysis.Text.TextSpan.FromBounds(tokens[index].SpanStart, tokens[index + offset - 1].Span.End);
        }

        return null;
    }

    private static CSharpSourceCatalog ReplaceSpan(CSharpSourceCatalog source, CSharpSourceDocument document, int start, int length, string replacement)
    {
        string changed = document.Text.Remove(start, length).Insert(start, replacement);
        var next = CSharpSourceDocument.FromBytes(document.Path, System.Text.Encoding.UTF8.GetBytes(changed), (CSharpParseOptions)document.Syntax.Options);
        return new(source.Documents.Select(item => item == document ? next : item), source.Compilation.References, source.Compilation.AssemblyName!, source.Compilation.Options);
    }

    private static bool Rejects(Func<CSharpSourceCatalog> mutation, Func<CSharpSourceCatalog, bool> validator)
    {
        try
        {
            return !validator(mutation());
        }
        catch (InvalidOperationException error) when (error.Message.StartsWith("source_catalog_", StringComparison.Ordinal))
        {
            // Some original malformed-call controls intentionally fail to bind.
            // Missing mutation anchors are never classified as rejection proof.
            return true;
        }
    }
}
