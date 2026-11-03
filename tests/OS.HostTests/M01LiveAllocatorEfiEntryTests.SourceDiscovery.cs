using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Tooling;

internal static partial class M01LiveAllocatorEfiEntryTests
{
    private const string EntryOwner = "OS.Boot.EfiEntry";
    private const string StageOneOwner = EntryOwner + "+M01StageOneAdapter";
    private const string StageTwoOwner = EntryOwner + "+M01StageTwoAdapter";
    private static CSharpSourceCatalog Enabled() => Selected(true, true);
    private static CSharpSourceCatalog Selected(bool stageOne, bool stageTwo)
    {
        List<string> arguments = ["--BootPolicy", "Diagnostic"];
        if (stageOne)
            arguments.Add("--EnableM01LoadedImageStackProbe");
        if (stageTwo)
            arguments.Add("--EnableM01LiveAllocatorProbe");
        return HostSourceCatalog.Efi(EfiOptions.Parse(arguments.ToArray(), true).Profile());
    }

    private static bool RejectsMissingStageOne()
    {
        if (!StageTwoProjectGate(System.Xml.Linq.XDocument.Load(Path.Combine(
            HostToolProcess.FindRepositoryRoot(), "src", "OS", "OS.csproj"))))
            return false;
        foreach (bool build in new[]
        {
            false,
            true
        }

        )
        {
            try
            {
                _ = EfiOptions.Parse(["--BootPolicy", "Diagnostic", "--EnableM01LoadedImageStackProbe:false", "--EnableM01LiveAllocatorProbe"], build).Profile();
                return false;
            }
            catch (ToolFailure failure) when (failure.Reason == "m01_live_allocator_requires_loaded_image_stack")
            {
            }
        }

        return true;
    }

    private static bool StageTwoProjectGate(System.Xml.Linq.XDocument project) =>
        project.Descendants("Error").Any(error => error.Attribute("Condition")?.Value ==
            "'$(EnableM01LiveAllocatorProbe)' == 'true' and '$(EnableM01LoadedImageStackProbe)' != 'true'");

    // Inspection views contain compiler-selected members in evaluated source
    // order. They are never compiled, and never determine a build source list.
    private static string TypeText(CSharpSourceCatalog source, string owner)
    {
        var declarations = source.Type(owner).DeclaringSyntaxReferences.Select(reference => (TypeDeclarationSyntax)reference.GetSyntax()).ToArray();
        return string.Join("\n", source.Documents.SelectMany(document => declarations.Where(declaration => declaration.SyntaxTree == document.Syntax).OrderBy(declaration => declaration.SpanStart).SelectMany(declaration => declaration.Members).Select(HostSourceText.Active)));
    }

    private static string MethodBody(CSharpSourceCatalog source, string owner, string name)
    {
        var method = source.Method(owner, name);
        if (source.Compilation.GetSemanticModel(method.SyntaxTree).GetDiagnostics(method.Span).Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
            throw new InvalidOperationException("source_catalog_invalid_method " + owner + "." + name);
        if (method.Body is null)
            throw new InvalidOperationException("source_catalog_required_block " + owner + "." + name);
        return string.Join("\n", method.Body.Statements.Select(HostSourceText.Active));
    }

    private static int BoundCalls(CSharpSourceCatalog source, string owner, string name)
    {
        var method = source.Method(owner, name);
        var symbol = source.Compilation.GetSemanticModel(method.SyntaxTree).GetDeclaredSymbol(method)!;
        return HostSourceOperations.CallsTo(source, symbol).Length;
    }

    private static string RawSources(CSharpSourceCatalog source) => HostSourceText.Documents(source, EntryOwner);
    private static List<string[]> FindInvocationArguments(string selectedText, string methodName)
    {
        // The input is an inspection view of active statements or type members,
        // never a compilation input. Roslyn owns strings, nesting and separators.
        SyntaxNode syntax = SyntaxFactory.ParseStatement("{" + selectedText + "}");
        if (syntax.ContainsDiagnostics)
            syntax = CSharpSyntaxTree.ParseText("class Inspection {" + selectedText + "}", new CSharpParseOptions(LanguageVersion.CSharp14)).GetRoot();
        if (syntax.ContainsDiagnostics)
            throw new InvalidOperationException("source_catalog_invalid_inspection_view");
        return syntax.DescendantNodes().OfType<InvocationExpressionSyntax>().Where(invocation => (invocation.Expression switch
        {
            SimpleNameSyntax name => name.Identifier.ValueText,
            MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
            MemberBindingExpressionSyntax binding => binding.Name.Identifier.ValueText,
            _ => null
        }) == methodName).Select(invocation => invocation.ArgumentList.Arguments.Select(HostSourceText.Active).ToArray()).ToList();
    }

    private static CSharpSourceCatalog Mutate(CSharpSourceCatalog source, string oldText, string newText)
    {
        // The continuation and stage-two adapter are separate bound authorities;
        // no filename or physical adjacency selects the mutation target.
        string owner = oldText.StartsWith("if (!M01StageTwoAdapter", StringComparison.Ordinal) ? StageOneOwner : StageTwoOwner;
        var candidates = source.TypeSources(owner, includeNested: false).Where(document => document.Text.Contains(oldText, StringComparison.Ordinal)).ToArray();
        if (candidates.Length != 1)
            throw new InvalidOperationException("mutation_anchor_missing_or_ambiguous " + oldText);
        var selected = candidates[0];
        int index = selected.Text.LastIndexOf(oldText, StringComparison.Ordinal);
        string changed = selected.Text[..index] + newText + selected.Text[(index + oldText.Length)..];
        var replacement = CSharpSourceDocument.FromBytes(selected.Path, System.Text.Encoding.UTF8.GetBytes(changed), (CSharpParseOptions)selected.Syntax.Options);
        return new(source.Documents.Select(document => document == selected ? replacement : document), source.Compilation.References, source.Compilation.AssemblyName!, source.Compilation.Options);
    }

    private static CSharpSourceCatalog MutateFallback(CSharpSourceCatalog source)
    {
        var method = source.Method(StageTwoOwner, "RunAfterStageOne");
        if (method.Body?.Statements.Single()is not ReturnStatementSyntax { Expression: LiteralExpressionSyntax expression } || !expression.IsKind(SyntaxKind.FalseLiteralExpression))
            throw new InvalidOperationException("disabled_fallback_shape");
        var document = source.Document(method);
        string changed = document.Text[..expression.SpanStart] + "true" + document.Text[expression.Span.End..];
        var replacement = CSharpSourceDocument.FromBytes(document.Path, System.Text.Encoding.UTF8.GetBytes(changed), (CSharpParseOptions)document.Syntax.Options);
        return new(source.Documents.Select(item => item == document ? replacement : item), source.Compilation.References, source.Compilation.AssemblyName!, source.Compilation.Options);
    }
}
