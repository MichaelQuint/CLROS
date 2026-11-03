using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Tooling;

internal static partial class BootPageAllocatorFreeCoalescingTests
{
    private const string FreeOwner = "OS.Kernel.Memory.BootPageAllocatorMutator";
    private static readonly string[] FreeOwners = [FreeOwner, "OS.Kernel.Memory.BootPageAllocatorMutationStatus", "OS.Kernel.Memory.BootPageAllocatorOnePageRestoreReceipt", "OS.Kernel.Memory.BootPageAllocatorOnePageCoalescingKind", "OS.Kernel.Memory.BootPageAllocatorOnePageResult", "OS.Kernel.Memory.BootPageAllocatorOnePageFreeResult"];
    private static CSharpSourceCatalog FreeEfi() => HostSourceCatalog.Efi(EfiOptions.Parse([], false).Profile());
    private static CSharpSourceDocument[] FreeSources(string root, CSharpSourceCatalog host, CSharpSourceCatalog efi) => HostBootPageAllocatorSources.Select(root, host, efi);
    private static string[] NoStdLibFreeSources(string root, string shim)
    {
        var sources = FreeSources(root, HostSourceCatalog.Project("src/OS.Kernel/OS.Kernel.csproj"), FreeEfi());
        ValidateFreeShimClosure(sources, shim);
        foreach (var document in sources)
            AuthorityFiles.RequirePlainPath(root, document.Path, "one_page_source_link");
        return sources.Select(document => document.Path).ToArray();
    }

    private static void ValidateFreeShimClosure(IReadOnlyList<CSharpSourceDocument> sources, string shim) => HostBootPageAllocatorSources.ValidateShimClosure(sources, shim);
    private static bool CheckFreeSource(CSharpSourceCatalog host, CSharpSourceCatalog efi)
    {
        var production = HostSourceText.DocumentSources(host, [..FreeOwners, "OS.Kernel.Memory.BootPageAllocatorMutationResult"]);
        foreach (var document in production)
            if (!efi.Documents.Any(candidate => candidate.Path.Equals(document.Path, StringComparison.OrdinalIgnoreCase) && candidate.Sha256 == document.Sha256))
                return SourceFailure("raw-production-source-join");
        string core = string.Join("\n", production.Select(document => RemoveHostOnlyBlocks(RemoveComments(document.Text))));
        string[] forbidden = ["BootPageAllocatorState.cs", "BootPageFreeListPlan.cs", "BootPageAllocatorPlan.cs", "BootMemoryRegionPlan.cs", "BootPageFreeListPlanResult", "BootPageAllocatorPlanResult", "Span<", "ReadOnlySpan<", "System.", "string", "String", "Exception", "Reflection", "Activator", "typeof", "object", "dynamic", "delegate", "record ", "List<", "Dictionary<", "IEnumerable<", "ulong.MaxValue", "EfiEntry", "BootSequence", "Qemu", "QEMU", "OSHost_", "RuntimeExport", "UnmanagedCallersOnly"];
        foreach (string fragment in forbidden)
            if (core.Contains(fragment, StringComparison.Ordinal))
                return SourceFailure("nostdlib-fragment=" + fragment);
        if (System.Text.RegularExpressions.Regex.IsMatch(core, @"(?m)^\s*(?:global\s+)?using\b") || System.Text.RegularExpressions.Regex.IsMatch(core, @"\b(?:throw|try|catch|finally)\b") || System.Text.RegularExpressions.Regex.IsMatch(core, @"\b(object|dynamic|delegate|record|Exception|String|Array|Type)\b"))
            return SourceFailure("production-surface");
        foreach (string identity in new[]
        {
            "OS.Kernel.Memory.BootPageAllocatorOnePageRestoreReceipt",
            "OS.Kernel.Memory.BootPageAllocatorOnePageFreeResult"
        })
            if (efi.Type(identity) is not { TypeKind: TypeKind.Struct, IsReadOnly: true, IsRecord: false, Arity: 0, DeclaredAccessibility: Accessibility.Public })
                return SourceFailure("readonly-result-source");
        var method = efi.Method(FreeOwner, "FreeOnePage");
        var symbol = efi.Compilation.GetSemanticModel(method.SyntaxTree).GetDeclaredSymbol(method)!;
        string[] parameters = ["OS.Kernel.Memory.BootPageAllocatorState", "OS.Kernel.Memory.BootPageFreeListNode*", "ulong", "ulong", "OS.Kernel.Memory.BootReservedRange*", "ulong", "ulong", "OS.Kernel.Memory.BootPageAllocatorOnePageResult"];
        if (!symbol.IsStatic || symbol.Arity != 0 || symbol.DeclaredAccessibility != Accessibility.Public || symbol.ReturnsByRef || symbol.ReturnsByRefReadonly || symbol.ReturnType.ToDisplayString() != "OS.Kernel.Memory.BootPageAllocatorOnePageFreeResult" || symbol.Parameters.Any(parameter => parameter.RefKind != RefKind.None) || !symbol.Parameters.Select(parameter => parameter.Type.ToDisplayString()).SequenceEqual(parameters, StringComparer.Ordinal))
            return SourceFailure("free-one-page-pointer-abi");
        _ = efi.MethodInvocations(method);
        var span = host.Compilation.GetTypeByMetadataName("System.Span`1");
        var node = host.Type("OS.Kernel.Memory.BootPageFreeListNode");
        if (span is null || !host.Type(FreeOwner).GetMembers().OfType<IMethodSymbol>().Any(candidate => candidate.Parameters.Any(parameter => parameter.Type is INamedTypeSymbol named && SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, span) && named.TypeArguments.Length == 1 && SymbolEqualityComparer.Default.Equals(named.TypeArguments[0], node))))
            return SourceFailure("host-span-boundary");
        return FreeLegacyLayersExcluded(efi);
    }

    private static bool FreeLegacyLayersExcluded(CSharpSourceCatalog source) => HostBootPageAllocatorSources.LegacyLayersExcluded(source);
    private static bool FreeIntegrationBoundary(CSharpSourceCatalog source, IReadOnlyDictionary<string, string> deviceSources)
    {
        var entry = HostSourceText.DocumentSources(source, "OS.Boot.EfiEntry").ToDictionary(document => document.Path, document => document.Text, StringComparer.Ordinal);
        foreach (var document in deviceSources)
        {
            if (entry.TryGetValue(document.Key, out string? previous) && previous != document.Value)
                throw new InvalidOperationException("one_page_integration_snapshot_changed");
            entry[document.Key] = document.Value;
        }

        const string call = "BootPageAllocatorMutator.FreeOnePage";
        if (!EfiProfileTestControls.RemoveOwnedDmaCalls(entry, call, out var remaining, out string reason))
            return SourceFailure("owned-device-release-conditional " + reason);
        string text = string.Join("\n", remaining.Values) + "\n" + HostSourceText.Documents(source, "OS.Boot.BootSequence", "OS.Kernel.Diagnostics.Probes");
        string[] forbidden = [nameof(OS.Kernel.Memory.BootPageAllocatorOnePageRestoreReceipt), nameof(OS.Kernel.Memory.BootPageAllocatorOnePageFreeResult), "FreeOnePage", ReadyMarker];
        return forbidden.All(name => !text.Contains(name, StringComparison.Ordinal));
    }

    private static bool FreeMarkerShape(OS.Tooling.CSharpSourceCatalog source)
    {
        string testSource = HostSourceText.Documents(source, nameof(BootPageAllocatorFreeCoalescingTests));
        string startWrite = string.Concat("Console.WriteLine(\"TEST_START \" + ", "IssueTestName);");
        string failureWrite = string.Concat("Console.WriteLine(\"TEST_FAIL \" + ", "IssueTestName + \" failures=\" + failures);");
        string readyWrite = string.Concat("Console.WriteLine(", "ReadyMarker);");
        string passWrite = string.Concat("Console.WriteLine(\"TEST_PASS \" + ", "IssueTestName);");
        var runAll = source.Method(nameof(BootPageAllocatorFreeCoalescingTests), nameof(RunAll), method => method.IsStatic && method.Arity == 0 && method.ReturnsVoid && method.Parameters.Length == 0 && method.DeclaredAccessibility == Microsoft.CodeAnalysis.Accessibility.Internal);
        _ = source.MethodInvocations(runAll);
        string runAllSource = runAll.ToString();
        string active = HostSourceText.Active(runAll);
        int startIndex = HostSourceText.IndexOf(active, startWrite);
        int failureGate = HostSourceText.IndexOf(active, "if (failures != 0)");
        int failureIndex = HostSourceText.IndexOf(active, failureWrite);
        int failureThrow = HostSourceText.IndexOf(active, "throw new InvalidOperationException(");
        int readyIndex = HostSourceText.IndexOf(active, readyWrite);
        int passIndex = HostSourceText.IndexOf(active, passWrite);
        if (CountOccurrences(testSource, "\"" + ReadyMarker + "\"") != 1 || CountOccurrences(testSource, readyWrite) != 1 || CountOccurrences(runAllSource, "ReadyMarker") != 1 || CountOccurrences(runAllSource, startWrite) != 1 || CountOccurrences(runAllSource, failureWrite) != 1 || CountOccurrences(runAllSource, passWrite) != 1 || startIndex < 0 || failureGate <= startIndex || failureIndex <= failureGate || failureThrow <= failureIndex || readyIndex <= failureThrow || passIndex <= readyIndex)
        {
            return false;
        }

        return true;
    }
}
