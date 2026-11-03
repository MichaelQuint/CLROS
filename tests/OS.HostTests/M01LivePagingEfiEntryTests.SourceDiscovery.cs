using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Tooling;
using System.Text;

internal static partial class M01LivePagingEfiEntryTests
{
    private const string PagingEntry = "OS.Boot.EfiEntry";
    private const string PagingCaller = PagingEntry + "+M01StageTwoAdapter";
    private const string PagingAdapter = PagingEntry + "+M01StageThreeAdapter";

    private static CSharpSourceCatalog PagingSources(bool stageOneEnabled, bool stageTwoEnabled, bool stageThreeEnabled) =>
        HostSourceCatalog.Efi(HostEfiInspectionProfiles.Create(HostToolProcess.FindRepositoryRoot(), new Dictionary<string, bool>
        {
            ["EnableM01LoadedImageStackProbe"] = stageOneEnabled,
            ["EnableM01LiveAllocatorProbe"] = stageTwoEnabled,
            ["EnableM01OwnedPagingProbe"] = stageThreeEnabled
        }));

    private static string SelectActualProfile(bool stageOneEnabled, bool stageTwoEnabled, bool stageThreeEnabled)
    {
        var source = PagingSources(stageOneEnabled, stageTwoEnabled, stageThreeEnabled);
        string view = HostEfiLogicalSource.View(source, PagingEntry);
        if (!stageThreeEnabled)
            return view;
        var body = HostEfiSourceRegions.TypeBody(view, "M01StageThreeAdapter")
            ?? throw new InvalidOperationException("paging_source_adapter_body");
        return view[..body.Start] + PagingLegacyRawBody(source) + view[body.End..];
    }

    private static string PagingRawSource() => HostSourceText.Documents(PagingSources(false, false, false), PagingEntry);

    private static string PagingLegacyRawProfile(bool stageOneEnabled, bool stageTwoEnabled, bool stageThreeEnabled) =>
        string.Join("\n", HostSourceText.DocumentSources(PagingSources(stageOneEnabled, stageTwoEnabled, stageThreeEnabled), PagingEntry)
            .Select(document => SelectProfile(document.Text, stageOneEnabled, stageTwoEnabled, stageThreeEnabled)));

    private static string PagingRawCaller() => HostEfiRawTypes.Bodies(PagingSources(true, true, true), PagingCaller,
        text => SelectProfile(text, stageOneEnabled: true, stageTwoEnabled: true, stageThreeEnabled: true));

    private static string PagingLegacyRawBody(CSharpSourceCatalog source) => HostEfiRawTypes.Bodies(source, PagingAdapter,
        text => SelectProfile(text, stageOneEnabled: true, stageTwoEnabled: true, stageThreeEnabled: true));

    private static bool PagingOffGuards(CSharpSourceCatalog source)
    {
        try
        {
            var adapter = source.Compilation.GetTypeByMetadataName(PagingAdapter);
            if (adapter?.GetMembers("RunAfterStageTwo").Any() == true)
                return false;
            if (source.Compilation.GetTypeByMetadataName(PagingCaller)?.GetMembers("RunAfterStageOne").Any() != true)
                return true;
            var caller = source.Method(PagingCaller, "RunAfterStageOne");
            return !source.MethodInvocations(caller).Any(call => call.TargetMethod.Name == "RunAfterStageTwo"
                && SymbolEqualityComparer.Default.Equals(call.TargetMethod.ContainingType, adapter));
        }
        catch (InvalidOperationException)
        {
            // An opened call guard can expose a reference to the still-disabled
            // adapter. An unresolved call must not look like zero bound calls.
            return false;
        }
    }

    private static bool PagingActualGuards()
    {
        foreach (var source in new[]
        {
            PagingSources(false, false, false), PagingSources(true, false, false), PagingSources(true, true, false),
            HostSourceCatalog.Efi(EfiProfile.Create("VerifiedExit", new Dictionary<string, bool>()))
        })
            if (!PagingOffGuards(source))
                return false;
        var enabled = PagingSources(true, true, true);
        var normal = HostSourceCatalog.Efi(EfiProfile.Create("VerifiedExit", new Dictionary<string, bool>()));
        var normalCaller = normal.Method(PagingCaller, "RunAfterStageOne");
        if (!normalCaller.DescendantTokens().Any(token => token.ValueText == "M01StageTwoSuccessLineCount"))
            return false;
        _ = normal.MethodInvocations(normalCaller);
        var target = enabled.Method(PagingAdapter, "RunAfterStageTwo");
        var symbol = enabled.Compilation.GetSemanticModel(target.SyntaxTree).GetDeclaredSymbol(target)!;
        var calls = enabled.MethodInvocations(enabled.Method(PagingCaller, "RunAfterStageOne"));
        return calls.Count(call => SymbolEqualityComparer.Default.Equals(call.TargetMethod, symbol)) == 1;
    }

    private static string PagingGuardMutationFailure()
    {
        var enabled = PagingSources(true, true, true);
        var off = PagingSources(true, true, false);
        if (!PagingOffGuards(off))
            return "guard-baseline-invalid";
        var target = enabled.Method(PagingAdapter, "RunAfterStageTwo");
        var symbol = enabled.Compilation.GetSemanticModel(target.SyntaxTree).GetDeclaredSymbol(target)!;
        var call = enabled.MethodInvocations(enabled.Method(PagingCaller, "RunAfterStageOne"))
            .Single(call => SymbolEqualityComparer.Default.Equals(call.TargetMethod, symbol));
        var original = enabled.Document(call.Syntax);
        var changed = HostEfiConditionalMutation.OpenGuard(original, call.Syntax, StageThreeSymbol);
        if (changed.Text == original.Text)
            return "guard-mutation-unchanged";
        var parse = (CSharpParseOptions)off.Documents.Single(document => document.Path == changed.Path).Syntax.Options;
        var mutant = new CSharpSourceCatalog(off.Documents.Select(document => document.Path == changed.Path
            ? CSharpSourceDocument.FromBytes(document.Path, Encoding.UTF8.GetBytes(changed.Text), parse) : document),
            off.Compilation.References, off.Compilation.AssemblyName!, off.Compilation.Options);
        return PagingOffGuards(mutant) ? "guard-mutation-accepted" : "guards";
    }

    private static string PagingMarkerDocument()
    {
        var host = HostSourceCatalog.Project("tests/OS.HostTests/OS.HostTests.csproj");
        var field = host.Type(typeof(M01LivePagingEfiEntryTests).FullName!).GetMembers(nameof(ReadyMarker)).OfType<IFieldSymbol>().Single();
        if (!field.IsConst || field.Type.SpecialType != SpecialType.System_String || !Equals(field.ConstantValue, ReadyMarker))
            throw new InvalidOperationException("paging_source_ready_marker");
        return host.Document(field.DeclaringSyntaxReferences.Single().GetSyntax()).Path;
    }

    private static string PagingPatMutation(string source, string oldText, string newText)
    {
        var owner = HostEfiSourceRegions.TypeBody(source, "M01StageThreeAdapter")
            ?? throw new InvalidOperationException("paging_pat_owner");
        var method = HostEfiSourceRegions.MethodBody(source, owner, "CaptureArchitecturalPreflight")
            ?? throw new InvalidOperationException("paging_pat_method");
        const string prefix = "class InspectionOwner { void Capture() {\n";
        var tree = CSharpSyntaxTree.ParseText(prefix + source[method.Start..method.End] + "\n} }");
        var callee = SyntaxFactory.ParseExpression("OS.Kernel.Memory.X64PatMappingToken.TryCreate");
        var invocation = tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Single(call => SyntaxFactory.AreEquivalent(call.Expression, callee));
        var argument = invocation.ArgumentList.Arguments[0].Expression;
        var comma = invocation.ArgumentList.Arguments.GetSeparator(0);
        var span = HostSourceMutation.Find(source, oldText, method, requireUnique: true);
        if (span.Start != method.Start + argument.SpanStart - prefix.Length
            || span.End != method.Start + comma.Span.End - prefix.Length)
            throw new InvalidOperationException("paging_pat_argument_anchor");
        return source[..span.Start] + newText + source[span.End..];
    }
}
