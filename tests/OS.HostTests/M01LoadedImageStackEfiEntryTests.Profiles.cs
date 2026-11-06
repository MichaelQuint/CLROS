using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Tooling;

internal static partial class M01LoadedImageStackEfiEntryTests
{
    private const string LoadedImageSymbol = "M01_LOADED_IMAGE_STACK_PROBE";
    private const string NormalExitSymbol = "CLROS_NORMAL_VERIFIED_EXIT";
    private const string FirstExitPrerequisiteMessage =
        "The first real exit requires the original loaded-image, live allocator and copied ACPI adapters.";

    private static CSharpSourceCatalog NormalExit() => HostSourceCatalog.Efi(EfiOptions.Parse([], true).Profile());

    private static CSharpSourceCatalog FirstExit() => HostSourceCatalog.Efi(EfiOptions.Parse(
        ["--EnableM01LoadedImageStackProbe", "--EnableM01LiveAllocatorProbe",
         "--EnableAcpiPhysicalDiscoveryProbe", "--EnableM03FirstRealExitBootServicesProbe"], true).Profile());

    private static bool ProfileGates() => NormalIncludesStageOne(NormalExit())
        && RejectsFirstExitWithoutLoadedImage(FirstExit());

    private static bool NormalIncludesStageOne(CSharpSourceCatalog source)
    {
        var symbols = ((CSharpParseOptions)source.Documents[0].Syntax.Options).PreprocessorSymbolNames;
        if (!symbols.Contains(NormalExitSymbol, StringComparer.Ordinal)
            || symbols.Contains(LoadedImageSymbol, StringComparer.Ordinal))
            return false;
        try
        {
            var run = source.Method(StageOwner, "Run");
            var calls = source.MethodInvocations(run);
            foreach (string name in new[] { "TryCaptureLoadedImage", "MaterializeAndInstallStackProbe", "PrepareAndTransfer" })
            {
                var declaration = source.Method(StageOwner, name);
                var target = source.Compilation.GetSemanticModel(declaration.SyntaxTree).GetDeclaredSymbol(declaration)!;
                if (calls.Count(call => SymbolEqualityComparer.Default.Equals(call.TargetMethod, target)) != 1)
                    return false;
            }
            return HostSourceOperations.CallsTo(source, StageOwner, "Run").Length == 1;
        }
        catch (InvalidOperationException error) when (error.Message.StartsWith("source_catalog_", StringComparison.Ordinal))
        {
            return false;
        }
    }

    private static bool RejectsFirstExitWithoutLoadedImage(CSharpSourceCatalog source)
    {
        bool rejected = false;
        foreach (var document in source.Documents)
        {
            var options = (CSharpParseOptions)document.Syntax.Options;
            var symbols = options.PreprocessorSymbolNames.ToHashSet(StringComparer.Ordinal);
            if (symbols.Contains(NormalExitSymbol)
                || !symbols.Contains("M03_FIRST_REAL_EXIT_BOOT_SERVICES_PROBE")
                || !symbols.Contains("M01_LIVE_ALLOCATOR_PROBE")
                || !symbols.Contains("ACPI_PHYSICAL_DISCOVERY_PROBE")
                || !symbols.Remove(LoadedImageSymbol))
                return false;
            // This intentionally invalid profile must be rejected by the source
            // compiler, independently of EfiOptions' argument validation.
            var syntax = CSharpSyntaxTree.ParseText(document.Text, options.WithPreprocessorSymbols(symbols), document.Path);
            rejected |= syntax.GetDiagnostics().Any(diagnostic => diagnostic.Id == "CS1029"
                && diagnostic.GetMessage().Contains(FirstExitPrerequisiteMessage, StringComparison.Ordinal));
        }
        return rejected;
    }

    [HostTest("loaded-image source gates retain normal exit and first-exit prerequisite behavior")]
    private static void ProfileGateControls()
    {
        var normal = NormalExit();
        var firstExit = FirstExit();
        HostCheck.True(NormalIncludesStageOne(normal), "normal exit includes stage one without enabling the diagnostic probe");
        HostCheck.True(RejectsFirstExitWithoutLoadedImage(firstExit), "source compiler prerequisite rejection");

        var movedPrerequisite = MoveFirstExitPrerequisite(firstExit);
        RequireBound(movedPrerequisite);
        HostCheck.True(movedPrerequisite.Documents.Last().Syntax.GetCompilationUnitRoot().Members.Count == 0,
            "the relocated constraint has no declaring type");
        HostCheck.True(RejectsFirstExitWithoutLoadedImage(movedPrerequisite),
            "compiler constraints may move independently of the entry type");

        var run = normal.Method(StageOwner, "Run");
        string conditionalBody = "{\n#if " + LoadedImageSymbol + "\n"
            + string.Join("\n", run.Body!.Statements.Select(HostSourceText.Active))
            + "\n#else\nreturn false;\n#endif\n}";
        var lostNormalRun = ReplaceSpan(normal, normal.Document(run), run.Body.SpanStart, run.Body.Span.Length, conditionalBody);
        RequireBound(lostNormalRun);
        HostCheck.True(!NormalIncludesStageOne(lostNormalRun), "diagnostic-only stage-one entry cannot replace normal exit inclusion");

        var lostPrerequisite = RemoveLoadedImagePrerequisite(firstExit);
        RequireBound(lostPrerequisite);
        HostCheck.True(!RejectsFirstExitWithoutLoadedImage(lostPrerequisite), "removing the source prerequisite must fail this fixture");
    }

    private static CSharpSourceCatalog MoveFirstExitPrerequisite(CSharpSourceCatalog source)
    {
        string? guard = null;
        string? condition = null;
        var documents = source.Documents.Select(document =>
        {
            var directives = document.Syntax.GetRoot().DescendantTrivia(descendIntoTrivia: true)
                .Select(trivia => trivia.GetStructure()).OfType<DirectiveTriviaSyntax>().ToArray();
            var spans = new List<Microsoft.CodeAnalysis.Text.TextSpan>();
            for (int index = 1; index < directives.Length - 1; index++)
            {
                if (directives[index] is not ErrorDirectiveTriviaSyntax error
                    || !error.ToString().Contains(FirstExitPrerequisiteMessage, StringComparison.Ordinal))
                    continue;
                if (directives[index - 1] is not IfDirectiveTriviaSyntax start
                    || directives[index + 1] is not EndIfDirectiveTriviaSyntax end)
                    throw new InvalidOperationException("prerequisite relocation requires the original direct guard");
                string key = string.Join("|", start.Condition.DescendantTokens().Select(token => token.RawKind + ":" + token.Text));
                if (condition is not null && condition != key)
                    throw new InvalidOperationException("prerequisite relocation found different guard conditions");
                condition = key;
                var span = Microsoft.CodeAnalysis.Text.TextSpan.FromBounds(start.FullSpan.Start, end.FullSpan.End);
                guard ??= document.Text.Substring(span.Start, span.Length);
                spans.Add(span);
            }
            string text = document.Text;
            foreach (var span in spans.OrderByDescending(span => span.Start))
                text = text.Remove(span.Start, span.Length);
            var changed = spans.Count == 0 ? document : CSharpSourceDocument.FromBytes(document.Path,
                System.Text.Encoding.UTF8.GetBytes(text), (CSharpParseOptions)document.Syntax.Options);
            HostCheck.True(!changed.Syntax.GetRoot().DescendantTrivia(descendIntoTrivia: true)
                .Select(trivia => trivia.GetStructure()).OfType<ErrorDirectiveTriviaSyntax>()
                .Any(error => error.ToString().Contains(FirstExitPrerequisiteMessage, StringComparison.Ordinal)),
                "all duplicated prerequisite guards were removed");
            return changed;
        }).ToArray();
        HostCheck.True(guard is not null, "actual prerequisite relocation anchor");
        string moved = "#if M03_FIRST_REAL_EXIT_BOOT_SERVICES_PROBE || " + NormalExitSymbol + "\n"
            + guard + "\n#endif\n";
        var added = CSharpSourceDocument.FromBytes(Path.Combine(Path.GetDirectoryName(documents[0].Path)!, "MovedFirstExitConstraints.cs"),
            System.Text.Encoding.UTF8.GetBytes(moved), (CSharpParseOptions)documents[0].Syntax.Options);
        return new(documents.Append(added), source.Compilation.References, source.Compilation.AssemblyName!, source.Compilation.Options);
    }

    private static CSharpSourceCatalog RemoveLoadedImagePrerequisite(CSharpSourceCatalog source)
    {
        int changes = 0;
        var documents = source.Documents.Select(document =>
        {
            string text = document.Text;
            var targets = document.Syntax.GetRoot().DescendantTrivia(descendIntoTrivia: true)
                .Select(trivia => trivia.GetStructure()).OfType<IfDirectiveTriviaSyntax>()
                .Where(directive => directive.Condition.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>()
                    .Any(name => name.Identifier.ValueText == "M01_LIVE_ALLOCATOR_PROBE")
                    && directive.Condition.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>()
                        .Any(name => name.Identifier.ValueText == "ACPI_PHYSICAL_DISCOVERY_PROBE"))
                .SelectMany(directive => directive.Condition.DescendantNodesAndSelf().OfType<PrefixUnaryExpressionSyntax>())
                .Where(expression => expression.IsKind(SyntaxKind.LogicalNotExpression)
                    && expression.Operand.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>()
                        .Any(name => name.Identifier.ValueText == LoadedImageSymbol))
                .OrderByDescending(expression => expression.SpanStart).ToArray();
            foreach (var target in targets)
            {
                text = text.Remove(target.SpanStart, target.Span.Length).Insert(target.SpanStart, "false");
                changes++;
            }
            return targets.Length == 0 ? document : CSharpSourceDocument.FromBytes(document.Path,
                System.Text.Encoding.UTF8.GetBytes(text), (CSharpParseOptions)document.Syntax.Options);
        }).ToArray();
        HostCheck.True(changes != 0, "actual prerequisite mutation anchor");
        return new(documents, source.Compilation.References, source.Compilation.AssemblyName!, source.Compilation.Options);
    }
}
