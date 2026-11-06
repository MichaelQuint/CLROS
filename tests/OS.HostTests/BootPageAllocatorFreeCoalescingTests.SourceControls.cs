using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using OS.Tooling;

internal static partial class BootPageAllocatorFreeCoalescingTests
{
    [HostTest("m01.009 source discovery preserves moved families and rejects boundary drift")]
    private static void FreeDiscoveryControls()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        var actualHost = HostSourceCatalog.Project("src/OS.Kernel/OS.Kernel.csproj");
        var actualEfi = FreeEfi();
        var sources = FreeSources(root, actualHost, actualEfi);
        string shim = FreeShimForControls();
        ValidateFreeShimClosure(sources, shim);
        var host = new CSharpSourceCatalog(actualHost.DependencySources(FreeOwner), actualHost.Compilation.References);
        var parse = (CSharpParseOptions)sources[0].Syntax.Options;
        var shimDocument = FreeControlDocument(Path.Combine(Path.GetTempPath(), "FreeControlShim.cs"), shim, parse);
        var source = new CSharpSourceCatalog(sources.Append(shimDocument), []);
        FreeBound(host);
        FreeBound(source);
        FreeRequire(CheckFreeSource(host, source), "selected source boundary");
        var method = source.Method(FreeOwner, "FreeOnePage");
        var document = source.Document(method);
        var declaration = (TypeDeclarationSyntax)method.Parent!;
        string remaining = document.Text.Remove(method.SpanStart, method.Span.Length).Replace("public static unsafe class", "public static unsafe partial class", StringComparison.Ordinal);
        string moved = "namespace OS.Kernel.Memory; public static unsafe partial class BootPageAllocatorMutator {\n" + method.ToString() + "\n}";
        var movedSource = FreeChange(source, document, remaining, "MovedAllocation.cs", moved);
        var hostDocument = host.Documents.Single(item => item.Path == document.Path);
        var movedHost = FreeChange(host, hostDocument, remaining, "MovedAllocation.cs", moved);
        FreeBound(movedSource);
        FreeBound(movedHost);
        FreeRequire(CheckFreeSource(movedHost, movedSource), "moved allocation method");
        ValidateFreeShimClosure(FreeSources(root, movedHost, movedSource), shim);
        var first = method.ParameterList.Parameters[0];
        var wrongAbi = method.ReplaceNode(first, first.AddModifiers(SyntaxFactory.Token(SyntaxKind.InKeyword)));
        string changedAbiText = document.Text.Remove(method.SpanStart, method.Span.Length).Insert(method.SpanStart, wrongAbi.NormalizeWhitespace().ToFullString());
        var changedAbi = FreeChange(source, document, changedAbiText);
        var changedHostAbi = FreeChange(host, hostDocument, changedAbiText);
        FreeBound(changedAbi);
        FreeBound(changedHostAbi);
        FreeRequire(!CheckFreeSource(changedHostAbi, changedAbi), "in parameter changes original value ABI");
        const string statusOwner = "OS.Kernel.Memory.BootPageAllocatorMutationStatus";
        var enumDocument = source.TypeSources(statusOwner).Single();
        var resultDocument = source.TypeSources("OS.Kernel.Memory.BootPageAllocatorOnePageFreeResult").Single();
        var resultDeclaration = resultDocument.Syntax.GetRoot().DescendantNodes().OfType<StructDeclarationSyntax>().Single();
        var writableResult = resultDeclaration.WithModifiers(SyntaxFactory.TokenList(resultDeclaration.Modifiers.Where(token => !token.IsKind(SyntaxKind.ReadOnlyKeyword))));
        string writableText = resultDocument.Syntax.GetRoot().ReplaceNode(resultDeclaration, writableResult).NormalizeWhitespace().ToFullString();
        var changedResult = FreeChange(source, resultDocument, writableText);
        var changedHostResult = FreeChange(host, host.Documents.Single(item => item.Path == resultDocument.Path), writableText);
        FreeBound(changedResult);
        FreeBound(changedHostResult);
        FreeRequire(!CheckFreeSource(changedHostResult, changedResult), "readonly result changed to mutable struct");
        var movedResult = FreeChange(source, resultDocument, "", "MovedFreeResult.cs", resultDocument.Text);
        var movedHostResult = FreeChange(host, host.Documents.Single(item => item.Path == resultDocument.Path), "", "MovedFreeResult.cs", resultDocument.Text);
        FreeBound(movedResult);
        FreeBound(movedHostResult);
        FreeRequire(CheckFreeSource(movedHostResult, movedResult), "moved result declaration");
        var drifted = FreeChange(source, enumDocument, enumDocument.Text + "\n// changed source identity\n");
        FreeReject(() => FreeSources(root, host, drifted), "host to EFI byte drift");
        FreeReject(() => ValidateFreeShimClosure(sources.Where(item => item.Path != enumDocument.Path).ToArray(), shim), "missing source dependency");
        FreeReject(() => ValidateFreeShimClosure(sources, shim + "\nnamespace System { public struct UnexpectedShimType { } }"), "expanded shim surface");
        string conditional = document.Text.Insert(declaration.CloseBraceToken.SpanStart, "\n#if NOSTDLIB_EFI\nprivate static ConditionalFreeDependency ConditionalDependency() => default;\n#endif\n");
        const string dependency = "namespace OS.Kernel.Memory; public readonly struct ConditionalFreeDependency { }";
        var conditionalSource = FreeChange(source, document, conditional, "ConditionalDependency.cs", dependency);
        var conditionalHost = FreeChange(host, hostDocument, conditional, "ConditionalDependency.cs", dependency);
        FreeBound(conditionalSource);
        FreeBound(conditionalHost);
        FreeReject(() => ValidateFreeShimClosure(FreeSources(root, conditionalHost, conditionalSource), shim), "missing NOSTDLIB-only dependency");
        string foreign = document.Text.Insert(declaration.CloseBraceToken.SpanStart, "\nprivate static Unexpected.Layer.ForeignDependency ForeignDependency() => default;\n");
        const string foreignType = "namespace Unexpected.Layer; public readonly struct ForeignDependency { }";
        var foreignSource = FreeChange(source, document, foreign, "ForeignDependency.cs", foreignType);
        var foreignHost = FreeChange(host, hostDocument, foreign, "ForeignDependency.cs", foreignType);
        FreeBound(foreignSource);
        FreeBound(foreignHost);
        FreeReject(() => FreeSources(root, foreignHost, foreignSource), "foreign dependency layer");
        var legacy = FreeChange(source, null, null, "UnrelatedPlannerName.cs", "namespace OS.Kernel.Memory; public static class BootPageAllocatorPlanner { }");
        FreeBound(legacy);
        FreeRequire(!FreeLegacyLayersExcluded(legacy), "legacy planning layer under unrelated filename");
        foreach (string fragment in new[]
        {
            "private static string HiddenCoreReference() => \"hidden\";",
            "private const bool LegacyName = \"BootPageAllocatorState.cs\" == \"\";",
            "private const bool LegacyName = \"BootPageFreeListPlan.cs\" == \"\";",
            "private const bool LegacyName = \"BootPageAllocatorPlan.cs\" == \"\";",
            "private const bool LegacyName = \"BootMemoryRegionPlan.cs\" == \"\";"
        })
        {
            string hidden = document.Text.Insert(declaration.CloseBraceToken.SpanStart, "\n#if FREE_COALESCING_UNRELATED_DISABLED\n" + fragment + "\n#endif\n");
            var hiddenHost = FreeChange(host, hostDocument, hidden);
            var hiddenEfi = FreeChange(source, document, hidden);
            FreeBound(hiddenHost);
            FreeBound(hiddenEfi);
            var activeDocument = FreeControlDocument(hostDocument.Path, hidden, ((CSharpParseOptions)hostDocument.Syntax.Options).WithPreprocessorSymbols("FREE_COALESCING_UNRELATED_DISABLED"));
            var activeHost = new CSharpSourceCatalog(hiddenHost.Documents.Select(item => item.Path == activeDocument.Path ? activeDocument : item), hiddenHost.Compilation.References, hiddenHost.Compilation.AssemblyName!, hiddenHost.Compilation.Options);
            FreeBound(activeHost);
            FreeRequire(!CheckFreeSource(hiddenHost, hiddenEfi), "unrelated disabled raw production policy: " + fragment);
        }
    }

    [HostTest("m01.009 marker discovery follows partials and rejects reordered or inactive duplicate writes")]
    private static void FreeMarkerDiscoveryControls()
    {
        var actual = HostSourceCatalog.Project("tests/OS.HostTests/OS.HostTests.csproj");
        FreeRequire(FreeMarkerShape(actual), "actual marker shape");
        string ready = string.Concat("Console.WriteLine(", "ReadyMarker);");
        string pass = string.Concat("Console.WriteLine(\"TEST_PASS \" + ", "IssueTestName);");
        string start = string.Concat("Console.WriteLine(\"TEST_START \" + ", "IssueTestName);");
        string failure = string.Concat("Console.WriteLine(\"TEST_FAIL \" + ", "IssueTestName + \" failures=\" + failures);");
        string text = "using System; internal static partial class BootPageAllocatorFreeCoalescingTests { " + "private const string IssueTestName = \"control\"; private const string ReadyMarker = \"" + ReadyMarker + "\"; internal static void RunAll() { " + start + " int failures = 0; if (failures != 0) { " + failure + " throw new InvalidOperationException(); } " + ready + " " + pass + " } }";
        var parse = new CSharpParseOptions(LanguageVersion.CSharp14);
        var document = FreeControlDocument(Path.Combine(Path.GetTempPath(), "FreeMarkerControl.cs"), text, parse);
        var source = new CSharpSourceCatalog([document], actual.Compilation.References);
        FreeBound(source);
        FreeRequire(FreeMarkerShape(source), "minimal marker fixture");
        var run = source.Method(nameof(BootPageAllocatorFreeCoalescingTests), nameof(RunAll));
        var owner = (TypeDeclarationSyntax)run.Parent!;
        var remaining = document.Syntax.GetRoot().ReplaceNode(owner, owner.WithMembers(owner.Members.Remove(run)));
        var moved = SyntaxFactory.CompilationUnit().WithUsings(((CompilationUnitSyntax)document.Syntax.GetRoot()).Usings).AddMembers(owner.WithMembers(SyntaxFactory.SingletonList<MemberDeclarationSyntax>(run)));
        var split = FreeChange(source, document, remaining.NormalizeWhitespace().ToFullString(), "MovedRun.cs", moved.NormalizeWhitespace().ToFullString());
        FreeBound(split);
        FreeRequire(FreeMarkerShape(split), "RunAll moved into an arbitrary partial file");
        Reject(text.Replace(ready + " " + pass, pass + " " + ready, StringComparison.Ordinal), "ready/pass order");
        Reject(text.Replace(ready, ready + " " + ready, StringComparison.Ordinal), "duplicate ready write");
        Reject(text.Replace("if (failures != 0)", "if (failures == 0)", StringComparison.Ordinal), "failure polarity");
        Reject(text.Replace(ready, ready + "\n#if INACTIVE_MARKER_CONTROL\n" + ready + "\n#endif\n", StringComparison.Ordinal), "inactive duplicate ready write");
        foreach (string write in new[]
        {
            start,
            failure,
            pass
        })
            Reject(text.Replace(write, write + "\n#if INACTIVE_MARKER_CONTROL\n" + write + "\n#endif\n", StringComparison.Ordinal), "inactive duplicate original marker: " + write);
        Reject(text.Replace(start, "", StringComparison.Ordinal), "missing start");
        Reject(text.Replace(failure, "", StringComparison.Ordinal), "missing failure write");
        void Reject(string changed, string reason)
        {
            FreeRequire(changed != text, "control applies: " + reason);
            var candidate = FreeChange(source, document, changed);
            FreeBound(candidate);
            FreeRequire(!FreeMarkerShape(candidate), reason);
        }
    }

    [HostTest("m01.009 integration discovery rejects an unowned nested allocator call")]
    private static void FreeIntegrationDiscoveryControls()
    {
        var profile = EfiOptions.Parse([], false).Profile();
        var source = HostSourceCatalog.Efi(profile);
        var device = new EfiAllocatorOwnershipSources(profile).Sources;
        FreeRequire(FreeIntegrationBoundary(source, device), "actual owned integration calls");
        var entryDocument = source.TypeSources("OS.Boot.EfiEntry").First();
        var added = FreeControlDocument(Path.Combine(Path.GetDirectoryName(entryDocument.Path)!, "UnrelatedNestedAllocation.cs"), "namespace OS.Boot; public static unsafe partial class EfiEntry { private static class ExtraAllocation { " + "private static void Call() => OS.Kernel.Memory.BootPageAllocatorMutator.FreeOnePage(default, null, 0, 0, null, 0, 0, default); } }", (CSharpParseOptions)entryDocument.Syntax.Options);
        var candidate = new CSharpSourceCatalog(source.Documents.Append(added), source.Compilation.References, source.Compilation.AssemblyName!, source.Compilation.Options);
        FreeBound(candidate);
        FreeRequire(!FreeIntegrationBoundary(candidate, device), "nested call outside reviewed DMA authority");
        var inactive = FreeControlDocument(added.Path, "#if FREE_COALESCING_HIDDEN_CALL\n" + added.Text + "\n#endif\n", (CSharpParseOptions)added.Syntax.Options);
        var inactiveCandidate = new CSharpSourceCatalog(source.Documents.Append(inactive), source.Compilation.References, source.Compilation.AssemblyName!, source.Compilation.Options);
        FreeBound(inactiveCandidate);
        FreeRequire(!FreeIntegrationBoundary(inactiveCandidate, device), "fully inactive moved partial preserves raw exclusion");
    }

    // Select the same deliberately small shim by its bound file-write destination.
    // Its location and the order of other literals in the workspace builder are irrelevant.
    private static string FreeShimForControls()
    {
        var source = HostSourceCatalog.Project("tests/OS.HostTests/OS.HostTests.csproj");
        var file = source.Compilation.GetTypeByMetadataName(typeof(File).FullName!);
        var path = source.Compilation.GetTypeByMetadataName(typeof(Path).FullName!);
        if (file is null || path is null || file.DeclaringSyntaxReferences.Length != 0 || path.DeclaringSyntaxReferences.Length != 0)
            throw new InvalidOperationException("free_coalescing_shim_framework_identity");
        var method = source.Method(nameof(BootPageAllocatorFreeCoalescingTests), "CreateNoStdLibWorkspace");
        var writes = source.MethodInvocations(method).Where(call => call.TargetMethod.Name == nameof(File.WriteAllText) && SymbolEqualityComparer.Default.Equals(call.TargetMethod.ContainingType, file) && HostSourceOperations.Argument(call, 0) is IInvocationOperation destination && destination.TargetMethod.Name == nameof(Path.Combine) && SymbolEqualityComparer.Default.Equals(destination.TargetMethod.ContainingType, path) && destination.TargetMethod.Parameters.Length == 2 && destination.TargetMethod.Parameters.All(parameter => parameter.Type.SpecialType == SpecialType.System_String) && HostSourceOperations.Argument(destination, 1).ConstantValue is { HasValue: true, Value: "NoStdLibShim.cs" }).ToArray();
        if (writes.Length != 1 || HostSourceOperations.Argument(writes[0], 1).ConstantValue is not { HasValue: true, Value: string content })
            throw new InvalidOperationException("free_coalescing_shim_write_identity");
        return content;
    }

    private static CSharpSourceCatalog FreeChange(CSharpSourceCatalog source, CSharpSourceDocument? document, string? text, string? addedName = null, string? addedText = null)
    {
        var documents = source.Documents.Select(item => item == document ? FreeControlDocument(item.Path, text!, (CSharpParseOptions)item.Syntax.Options) : item);
        if (addedName is not null)
            documents = documents.Append(FreeControlDocument(Path.Combine(Path.GetDirectoryName(document?.Path ?? source.Documents[0].Path)!, addedName), addedText!, (CSharpParseOptions)source.Documents[0].Syntax.Options));
        return new(documents, source.Compilation.References, source.Compilation.AssemblyName!, source.Compilation.Options);
    }

    private static CSharpSourceDocument FreeControlDocument(string path, string text, CSharpParseOptions options) => CSharpSourceDocument.FromBytes(path, System.Text.Encoding.UTF8.GetBytes(text), options);
    private static void FreeBound(CSharpSourceCatalog source)
    {
        var errors = source.Compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray();
        FreeRequire(errors.Length == 0, "control binding: " + string.Join("; ", errors.Select(error => error.ToString())));
    }

    private static void FreeReject(Action action, string reason)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException)
        {
            return;
        }

        throw new InvalidOperationException("free coalescing control accepted " + reason);
    }

    private static void FreeRequire(bool value, string reason)
    {
        if (!value)
            throw new InvalidOperationException("free coalescing control failed " + reason);
    }
}
