using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using OS.Tooling;

internal static partial class BootPageAllocatorOnePageAllocationTests
{
    [HostTest("m01.008 source discovery preserves moved families and rejects boundary drift")]
    private static void OnePageDiscoveryControls()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        var actualHost = HostSourceCatalog.Project("src/OS.Kernel/OS.Kernel.csproj");
        var actualEfi = OnePageEfi();
        var sources = OnePageSources(root, actualHost, actualEfi);
        string shim = OnePageShimForControls();
        ValidateOnePageShimClosure(sources, shim);
        var host = new CSharpSourceCatalog(actualHost.DependencySources(OnePageOwner), actualHost.Compilation.References);
        var parse = (CSharpParseOptions)sources[0].Syntax.Options;
        var shimDocument = OnePageControlDocument(Path.Combine(Path.GetTempPath(), "OnePageControlShim.cs"), shim, parse);
        var source = new CSharpSourceCatalog(sources.Append(shimDocument), []);
        OnePageBound(host);
        OnePageBound(source);
        OnePageRequire(CheckOnePageSource(host, source), "selected source boundary");
        var method = source.Method(OnePageOwner, "AllocateOnePage");
        var document = source.Document(method);
        var declaration = (TypeDeclarationSyntax)method.Parent!;
        string remaining = document.Text.Remove(method.SpanStart, method.Span.Length).Replace("public static unsafe class", "public static unsafe partial class", StringComparison.Ordinal);
        string moved = "namespace OS.Kernel.Memory; public static unsafe partial class BootPageAllocatorMutator {\n" + method.ToString() + "\n}";
        var movedSource = OnePageChange(source, document, remaining, "MovedAllocation.cs", moved);
        var hostDocument = host.Documents.Single(item => item.Path == document.Path);
        var movedHost = OnePageChange(host, hostDocument, remaining, "MovedAllocation.cs", moved);
        OnePageBound(movedSource);
        OnePageBound(movedHost);
        OnePageRequire(CheckOnePageSource(movedHost, movedSource), "moved allocation method");
        ValidateOnePageShimClosure(OnePageSources(root, movedHost, movedSource), shim);
        var first = method.ParameterList.Parameters[0];
        var wrongAbi = method.ReplaceNode(first, first.AddModifiers(SyntaxFactory.Token(SyntaxKind.InKeyword)));
        string changedAbiText = document.Text.Remove(method.SpanStart, method.Span.Length).Insert(method.SpanStart, wrongAbi.NormalizeWhitespace().ToFullString());
        var changedAbi = OnePageChange(source, document, changedAbiText);
        var changedHostAbi = OnePageChange(host, hostDocument, changedAbiText);
        OnePageBound(changedAbi);
        OnePageBound(changedHostAbi);
        OnePageRequire(!CheckOnePageSource(changedHostAbi, changedAbi), "in parameter changes original value ABI");
        const string statusOwner = "OS.Kernel.Memory.BootPageAllocatorMutationStatus";
        var status = source.Type(statusOwner);
        var expected = status.GetMembers().OfType<IFieldSymbol>().Where(field => field.HasConstantValue).Select(field => (field.Name, (int)field.ConstantValue!)).ToArray();
        var enumeration = (EnumDeclarationSyntax)status.DeclaringSyntaxReferences.Single().GetSyntax();
        var enumDocument = source.Document(enumeration);
        var movedEnum = OnePageChange(source, enumDocument, "", "MovedStatus.cs", enumDocument.Text);
        OnePageBound(movedEnum);
        OnePageRequire(OnePageSourceEnum(movedEnum, statusOwner, expected), "moved status enum");
        var changedEnum = enumeration.ReplaceNode(enumeration.Members[0], enumeration.Members[0].WithEqualsValue(SyntaxFactory.EqualsValueClause(SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(99)))));
        var wrongEnum = OnePageChange(source, enumDocument, enumDocument.Syntax.GetRoot().ReplaceNode(enumeration, changedEnum).NormalizeWhitespace().ToFullString());
        OnePageBound(wrongEnum);
        OnePageRequire(!OnePageSourceEnum(wrongEnum, statusOwner, expected), "changed status constant");
        var drifted = OnePageChange(source, enumDocument, enumDocument.Text + "\n// changed source identity\n");
        OnePageReject(() => OnePageSources(root, host, drifted), "host to EFI byte drift");
        OnePageReject(() => ValidateOnePageShimClosure(sources.Where(item => item.Path != enumDocument.Path).ToArray(), shim), "missing source dependency");
        OnePageReject(() => ValidateOnePageShimClosure(sources, shim + "\nnamespace System { public struct UnexpectedShimType { } }"), "expanded shim surface");
        string conditional = document.Text.Insert(declaration.CloseBraceToken.SpanStart, "\n#if NOSTDLIB_EFI\nprivate static ConditionalOnePageDependency ConditionalDependency() => default;\n#endif\n");
        const string dependency = "namespace OS.Kernel.Memory; public readonly struct ConditionalOnePageDependency { }";
        var conditionalSource = OnePageChange(source, document, conditional, "ConditionalDependency.cs", dependency);
        var conditionalHost = OnePageChange(host, hostDocument, conditional, "ConditionalDependency.cs", dependency);
        OnePageBound(conditionalSource);
        OnePageBound(conditionalHost);
        OnePageReject(() => ValidateOnePageShimClosure(OnePageSources(root, conditionalHost, conditionalSource), shim), "missing NOSTDLIB-only dependency");
        string foreign = document.Text.Insert(declaration.CloseBraceToken.SpanStart, "\nprivate static Unexpected.Layer.ForeignDependency ForeignDependency() => default;\n");
        const string foreignType = "namespace Unexpected.Layer; public readonly struct ForeignDependency { }";
        var foreignSource = OnePageChange(source, document, foreign, "ForeignDependency.cs", foreignType);
        var foreignHost = OnePageChange(host, hostDocument, foreign, "ForeignDependency.cs", foreignType);
        OnePageBound(foreignSource);
        OnePageBound(foreignHost);
        OnePageReject(() => OnePageSources(root, foreignHost, foreignSource), "foreign dependency layer");
        var legacy = OnePageChange(source, null, null, "UnrelatedPlannerName.cs", "namespace OS.Kernel.Memory; public static class BootPageAllocatorPlanner { }");
        OnePageBound(legacy);
        OnePageRequire(!OnePageLegacyLayersExcluded(legacy), "legacy planning layer under unrelated filename");
        foreach (string fragment in new[]
        {
            "private static string HiddenCoreReference() => \"hidden\";",
            "private const bool LegacyName = \"BootPageAllocatorState.cs\" == \"\";",
            "private const bool LegacyName = \"BootPageFreeListPlan.cs\" == \"\";",
            "private const bool LegacyName = \"BootPageAllocatorPlan.cs\" == \"\";",
            "private const bool LegacyName = \"BootMemoryRegionPlan.cs\" == \"\";"
        }

        )
        {
            string hidden = document.Text.Insert(declaration.CloseBraceToken.SpanStart, "\n#if ONE_PAGE_UNRELATED_DISABLED\n" + fragment + "\n#endif\n");
            var hiddenHost = OnePageChange(host, hostDocument, hidden);
            var hiddenEfi = OnePageChange(source, document, hidden);
            OnePageBound(hiddenHost);
            OnePageBound(hiddenEfi);
            var activeDocument = OnePageControlDocument(hostDocument.Path, hidden, ((CSharpParseOptions)hostDocument.Syntax.Options).WithPreprocessorSymbols("ONE_PAGE_UNRELATED_DISABLED"));
            var activeHost = new CSharpSourceCatalog(hiddenHost.Documents.Select(item => item.Path == activeDocument.Path ? activeDocument : item), hiddenHost.Compilation.References, hiddenHost.Compilation.AssemblyName!, hiddenHost.Compilation.Options);
            OnePageBound(activeHost);
            OnePageRequire(!CheckOnePageSource(hiddenHost, hiddenEfi), "unrelated disabled raw production policy: " + fragment);
        }
    }

    [HostTest("m01.008 marker discovery follows partials and rejects reordered or inactive duplicate writes")]
    private static void OnePageMarkerDiscoveryControls()
    {
        var actual = HostSourceCatalog.Project("tests/OS.HostTests/OS.HostTests.csproj");
        OnePageRequire(OnePageMarkerShape(actual), "actual marker shape");
        string ready = string.Concat("Console.WriteLine(", "ReadyMarker);");
        string pass = string.Concat("Console.WriteLine(\"TEST_PASS \" + ", "IssueTestName);");
        string text = "using System; internal static partial class BootPageAllocatorOnePageAllocationTests { " + "private const string IssueTestName = \"control\"; private const string ReadyMarker = \"" + ReadyMarker + "\"; internal static void RunAll() { int failures = 0; if (failures != 0) { throw new InvalidOperationException(); } " + ready + " " + pass + " } }";
        var parse = new CSharpParseOptions(LanguageVersion.CSharp14);
        var document = OnePageControlDocument(Path.Combine(Path.GetTempPath(), "OnePageMarkerControl.cs"), text, parse);
        var source = new CSharpSourceCatalog([document], actual.Compilation.References);
        OnePageBound(source);
        OnePageRequire(OnePageMarkerShape(source), "minimal marker fixture");
        var run = source.Method(nameof(BootPageAllocatorOnePageAllocationTests), nameof(RunAll));
        var owner = (TypeDeclarationSyntax)run.Parent!;
        var remaining = document.Syntax.GetRoot().ReplaceNode(owner, owner.WithMembers(owner.Members.Remove(run)));
        var moved = SyntaxFactory.CompilationUnit().WithUsings(((CompilationUnitSyntax)document.Syntax.GetRoot()).Usings).AddMembers(owner.WithMembers(SyntaxFactory.SingletonList<MemberDeclarationSyntax>(run)));
        var split = OnePageChange(source, document, remaining.NormalizeWhitespace().ToFullString(), "MovedRun.cs", moved.NormalizeWhitespace().ToFullString());
        OnePageBound(split);
        OnePageRequire(OnePageMarkerShape(split), "RunAll moved into an arbitrary partial file");
        Reject(text.Replace(ready + " " + pass, pass + " " + ready, StringComparison.Ordinal), "ready/pass order");
        Reject(text.Replace(ready, ready + " " + ready, StringComparison.Ordinal), "duplicate ready write");
        Reject(text.Replace("if (failures != 0)", "if (failures == 0)", StringComparison.Ordinal), "failure polarity");
        Reject(text.Replace(ready, ready + "\n#if INACTIVE_MARKER_CONTROL\n" + ready + "\n#endif\n", StringComparison.Ordinal), "inactive duplicate ready write");
        void Reject(string changed, string reason)
        {
            OnePageRequire(changed != text, "control applies: " + reason);
            var candidate = OnePageChange(source, document, changed);
            OnePageBound(candidate);
            OnePageRequire(!OnePageMarkerShape(candidate), reason);
        }
    }

    [HostTest("m01.008 integration discovery rejects an unowned nested allocator call")]
    private static void OnePageIntegrationDiscoveryControls()
    {
        var profile = EfiOptions.Parse([], false).Profile();
        var source = HostSourceCatalog.Efi(profile);
        var device = new EfiAllocatorOwnershipSources(profile).Sources;
        OnePageRequire(OnePageIntegrationBoundary(source, device), "actual owned integration calls");
        var entryDocument = source.TypeSources("OS.Boot.EfiEntry").First();
        var added = OnePageControlDocument(Path.Combine(Path.GetDirectoryName(entryDocument.Path)!, "UnrelatedNestedAllocation.cs"), "namespace OS.Boot; public static unsafe partial class EfiEntry { private static class ExtraAllocation { " + "private static void Call() => OS.Kernel.Memory.BootPageAllocatorMutator.AllocateOnePage(default, null, 0, 0, null, 0, 0, 0, 0); } }", (CSharpParseOptions)entryDocument.Syntax.Options);
        var candidate = new CSharpSourceCatalog(source.Documents.Append(added), source.Compilation.References, source.Compilation.AssemblyName!, source.Compilation.Options);
        OnePageBound(candidate);
        OnePageRequire(!OnePageIntegrationBoundary(candidate, device), "nested call outside reviewed DMA authority");
        var inactive = OnePageControlDocument(added.Path, "#if ONE_PAGE_HIDDEN_CALL\n" + added.Text + "\n#endif\n", (CSharpParseOptions)added.Syntax.Options);
        var inactiveCandidate = new CSharpSourceCatalog(source.Documents.Append(inactive), source.Compilation.References, source.Compilation.AssemblyName!, source.Compilation.Options);
        OnePageBound(inactiveCandidate);
        OnePageRequire(!OnePageIntegrationBoundary(inactiveCandidate, device), "fully inactive moved partial preserves raw exclusion");
    }

    // Select the same deliberately small shim by its bound file-write destination.
    // Its location and the order of other literals in the workspace builder are irrelevant.
    private static string OnePageShimForControls()
    {
        var source = HostSourceCatalog.Project("tests/OS.HostTests/OS.HostTests.csproj");
        var file = source.Compilation.GetTypeByMetadataName(typeof(File).FullName!);
        var path = source.Compilation.GetTypeByMetadataName(typeof(Path).FullName!);
        if (file is null || path is null || file.DeclaringSyntaxReferences.Length != 0 || path.DeclaringSyntaxReferences.Length != 0)
            throw new InvalidOperationException("one_page_shim_framework_identity");
        var method = source.Method(nameof(BootPageAllocatorOnePageAllocationTests), "CreateNoStdLibWorkspace");
        var writes = source.MethodInvocations(method).Where(call => call.TargetMethod.Name == nameof(File.WriteAllText) && SymbolEqualityComparer.Default.Equals(call.TargetMethod.ContainingType, file) && HostSourceOperations.Argument(call, 0)is IInvocationOperation destination && destination.TargetMethod.Name == nameof(Path.Combine) && SymbolEqualityComparer.Default.Equals(destination.TargetMethod.ContainingType, path) && destination.TargetMethod.Parameters.Length == 2 && destination.TargetMethod.Parameters.All(parameter => parameter.Type.SpecialType == SpecialType.System_String) && HostSourceOperations.Argument(destination, 1).ConstantValue is { HasValue: true, Value: "NoStdLibShim.cs" }).ToArray();
        if (writes.Length != 1 || HostSourceOperations.Argument(writes[0], 1).ConstantValue is not { HasValue: true, Value: string content })
            throw new InvalidOperationException("one_page_shim_write_identity");
        return content;
    }

    private static CSharpSourceCatalog OnePageChange(CSharpSourceCatalog source, CSharpSourceDocument? document, string? text, string? addedName = null, string? addedText = null)
    {
        var documents = source.Documents.Select(item => item == document ? OnePageControlDocument(item.Path, text!, (CSharpParseOptions)item.Syntax.Options) : item);
        if (addedName is not null)
            documents = documents.Append(OnePageControlDocument(Path.Combine(Path.GetDirectoryName(document?.Path ?? source.Documents[0].Path)!, addedName), addedText!, (CSharpParseOptions)source.Documents[0].Syntax.Options));
        return new(documents, source.Compilation.References, source.Compilation.AssemblyName!, source.Compilation.Options);
    }

    private static CSharpSourceDocument OnePageControlDocument(string path, string text, CSharpParseOptions options) => CSharpSourceDocument.FromBytes(path, System.Text.Encoding.UTF8.GetBytes(text), options);
    private static void OnePageBound(CSharpSourceCatalog source)
    {
        var errors = source.Compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray();
        OnePageRequire(errors.Length == 0, "control binding: " + string.Join("; ", errors.Select(error => error.ToString())));
    }

    private static void OnePageReject(Action action, string reason)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException)
        {
            return;
        }

        throw new InvalidOperationException("one page control accepted " + reason);
    }

    private static void OnePageRequire(bool value, string reason)
    {
        if (!value)
            throw new InvalidOperationException("one page control failed " + reason);
    }
}
