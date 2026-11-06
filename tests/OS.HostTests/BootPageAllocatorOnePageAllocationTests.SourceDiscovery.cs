using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Tooling;
using System.Text.RegularExpressions;

internal static partial class BootPageAllocatorOnePageAllocationTests
{
    private const string OnePageOwner = "OS.Kernel.Memory.BootPageAllocatorMutator";
    private static readonly string[] OnePageOwners = [OnePageOwner, "OS.Kernel.Memory.BootPageAllocatorMutationStatus", "OS.Kernel.Memory.BootPageAllocatorOnePageRestoreReceipt", "OS.Kernel.Memory.BootPageAllocatorOnePageCoalescingKind", "OS.Kernel.Memory.BootPageAllocatorOnePageResult", "OS.Kernel.Memory.BootPageAllocatorOnePageFreeResult"];
    private static CSharpSourceCatalog OnePageEfi() => HostSourceCatalog.Efi(EfiOptions.Parse([], false).Profile());
    private static CSharpSourceDocument[] OnePageSources(string root, CSharpSourceCatalog host, CSharpSourceCatalog efi) => HostBootPageAllocatorSources.Select(root, host, efi);

    private static string[] NoStdLibOnePageSources(string root, string shim)
    {
        var sources = OnePageSources(root, HostSourceCatalog.Project("src/OS.Kernel/OS.Kernel.csproj"), OnePageEfi());
        ValidateOnePageShimClosure(sources, shim);
        foreach (var document in sources)
            AuthorityFiles.RequirePlainPath(root, document.Path, "one_page_source_link");
        return sources.Select(document => document.Path).ToArray();
    }

    private static void ValidateOnePageShimClosure(IReadOnlyList<CSharpSourceDocument> sources, string shim) => HostBootPageAllocatorSources.ValidateShimClosure(sources, shim);

    private static bool OnePageSourceEnum(CSharpSourceCatalog source, string identity, (string Name, int Value)[] expected)
    {
        var type = source.Type(identity);
        if (type.TypeKind != TypeKind.Enum || type.DeclaredAccessibility != Accessibility.Public || type.EnumUnderlyingType?.SpecialType != SpecialType.System_Int32)
            return false;
        var fields = type.GetMembers().OfType<IFieldSymbol>().Where(field => field.HasConstantValue).ToArray();
        return fields.Select(field => (field.Name, field.ConstantValue)).SequenceEqual(expected.Select(item => (item.Name, (object?)item.Value)));
    }

    private static bool CheckOnePageSource(CSharpSourceCatalog host, CSharpSourceCatalog efi)
    {
        var production = HostSourceText.DocumentSources(host, [.. OnePageOwners, "OS.Kernel.Memory.BootPageAllocatorMutationResult"]);
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
        var owner = efi.Type(OnePageOwner);
        if (owner is not { TypeKind: TypeKind.Class, IsStatic: true, IsRecord: false, Arity: 0, DeclaredAccessibility: Accessibility.Public } || !owner.DeclaringSyntaxReferences.Any(reference => reference.GetSyntax() is TypeDeclarationSyntax type && type.Modifiers.Any(SyntaxKind.UnsafeKeyword)))
            return SourceFailure("production-surface");
        var method = efi.Method(OnePageOwner, "AllocateOnePage");
        var symbol = efi.Compilation.GetSemanticModel(method.SyntaxTree).GetDeclaredSymbol(method)!;
        string[] parameters = ["OS.Kernel.Memory.BootPageAllocatorState", "OS.Kernel.Memory.BootPageFreeListNode*", "ulong", "ulong", "OS.Kernel.Memory.BootReservedRange*", "ulong", "ulong", "ulong", "ulong"];
        if (!symbol.IsStatic || symbol.Arity != 0 || symbol.DeclaredAccessibility != Accessibility.Public || symbol.ReturnsByRef || symbol.ReturnsByRefReadonly || symbol.ReturnType.ToDisplayString() != "OS.Kernel.Memory.BootPageAllocatorOnePageResult" || symbol.Parameters.Any(parameter => parameter.RefKind != RefKind.None) || !symbol.Parameters.Select(parameter => parameter.Type.ToDisplayString()).SequenceEqual(parameters, StringComparer.Ordinal))
            return SourceFailure("one-page-pointer-abi");
        _ = efi.MethodInvocations(method);
        var span = host.Compilation.GetTypeByMetadataName("System.Span`1");
        var node = host.Type("OS.Kernel.Memory.BootPageFreeListNode");
        if (span is null || !host.Type(OnePageOwner).GetMembers("Allocate").OfType<IMethodSymbol>().Any(candidate => candidate.Parameters.Any(parameter => parameter.Type is INamedTypeSymbol named && SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, span) && named.TypeArguments.Length == 1 && SymbolEqualityComparer.Default.Equals(named.TypeArguments[0], node))) || efi.Type(OnePageOwner).GetMembers("Allocate").Length != 0 || efi.Compilation.GetTypeByMetadataName("OS.Kernel.Memory.BootPageAllocatorMutationResult") is not null)
            return SourceFailure("host-span-boundary");
        return OnePageLegacyLayersExcluded(efi);
    }

    private static bool OnePageLegacyLayersExcluded(CSharpSourceCatalog source) => HostBootPageAllocatorSources.LegacyLayersExcluded(source);

    private static bool OnePageIntegrationBoundary(CSharpSourceCatalog source, IReadOnlyDictionary<string, string> deviceSources)
    {
        var entry = HostSourceText.DocumentSources(source, "OS.Boot.EfiEntry").ToDictionary(document => document.Path, document => document.Text, StringComparer.Ordinal);
        foreach (var document in deviceSources)
        {
            if (entry.TryGetValue(document.Key, out string? previous) && previous != document.Value)
                throw new InvalidOperationException("one_page_integration_snapshot_changed");
            entry[document.Key] = document.Value;
        }

        const string call = "BootPageAllocatorMutator.AllocateOnePage";
        if (!EfiProfileTestControls.RemoveOwnedDmaCalls(entry, call, out var remaining, out string reason))
            return SourceFailure("owned-device-allocation-conditional " + reason);
        string text = string.Join("\n", remaining.Values) + "\n" + HostSourceText.Documents(source, "OS.Boot.BootSequence", "OS.Kernel.Diagnostics.Probes");
        string[] forbidden = [nameof(OS.Kernel.Memory.BootPageAllocatorOnePageResult), "AllocateOnePage", ReadyMarker];
        return forbidden.All(name => !text.Contains(name, StringComparison.Ordinal));
    }

    private static bool OnePageMarkerShape(CSharpSourceCatalog catalog)
    {
        string testSource = HostSourceText.Documents(catalog, nameof(BootPageAllocatorOnePageAllocationTests));
        string readyWriteCall = string.Concat("Console.WriteLine(", "ReadyMarker);");
        var runAll = catalog.Method(nameof(BootPageAllocatorOnePageAllocationTests), nameof(RunAll), method => method.IsStatic && method.Arity == 0 && method.ReturnsVoid && method.Parameters.Length == 0 && method.DeclaredAccessibility == Microsoft.CodeAnalysis.Accessibility.Internal);
        _ = catalog.MethodInvocations(runAll);
        string runAllSource = HostSourceText.Active(runAll);
        int failureGate = HostSourceText.IndexOf(runAllSource, "if (failures != 0)");
        int failureThrow = HostSourceText.IndexOf(runAllSource, "throw new InvalidOperationException(");
        int readyWrite = HostSourceText.IndexOf(runAllSource, readyWriteCall);
        int passWrite = HostSourceText.IndexOf(runAllSource, "Console.WriteLine(\"TEST_PASS \" + IssueTestName);");
        return !(CountOccurrences(testSource, "\"" + ReadyMarker + "\"") != 1 || CountOccurrences(testSource, readyWriteCall) != 1 || CountOccurrences(runAllSource, "ReadyMarker") != 1 || failureGate < 0 || failureThrow <= failureGate || readyWrite <= failureThrow || passWrite <= readyWrite);
    }

    // Preserve the original raw firewall: only the explicit host-only block is
    // excluded. Other disabled text remains subject to the independent policy.
    private static string RemoveHostOnlyBlocks(string source)
    {
        return Regex.Replace(source, @"(?ms)^\s*#if\s+!NOSTDLIB_EFI\s*$.*?^\s*#endif\s*$", string.Empty, RegexOptions.CultureInvariant);
    }
}
