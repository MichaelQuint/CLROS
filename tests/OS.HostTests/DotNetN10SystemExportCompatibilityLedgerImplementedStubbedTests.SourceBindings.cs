using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using OS.Std.NoRuntime;
using OS.Tooling;

internal static partial class DotNetN10SystemExportCompatibilityLedgerImplementedStubbedTests
{
    private static IEnumerable<string> CanonicalDeclarations(CSharpSourceCatalog catalog)
        => HostCanonicalTypeInventory.Read(catalog, RuntimeDocuments(catalog)).Keys;

    private static IReadOnlyList<CSharpSourceDocument> RuntimeDocuments(CSharpSourceCatalog catalog)
    {
        string root = HostToolProcess.FindRepositoryRoot();
        var paths = EfiContracts.SourceAdmissionReasons.Where(pair => pair.Value == "reviewed_minimal_runtime_source_identity")
            .Select(pair => Path.GetFullPath(pair.Key, Path.Combine(root, "src", "OS")))
            .ToHashSet(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        var documents = catalog.Documents.Where(document => paths.Contains(document.Path)).ToArray();
        if (documents.Length != paths.Count)
            throw new InvalidOperationException("ledger_runtime_family_missing");
        return documents;
    }

    private static List<RuntimeExportSourceEntry> ExtractRuntimeExports(CSharpSourceCatalog ordinary)
    {
        HostCheck.True(HaltDoesNotReturn(ordinary), "runtime fail-fast implementation");
        var family = RuntimeDocuments(ordinary).Select(document => document.Syntax).ToHashSet();
        return HostRuntimeExports.Read(ordinary).Where(pair => pair.Value.DeclaringSyntaxReferences
                .Any(reference => family.Contains(reference.SyntaxTree)))
            .Select(pair => new RuntimeExportSourceEntry(pair.Key, ClassifyRuntimeExport(ordinary, pair.Key, pair.Value))).ToList();
    }

    private static DotNetN10RuntimeExportBehavior ClassifyRuntimeExport(CSharpSourceCatalog catalog, string name, IMethodSymbol symbol)
    {
        var method = (MethodDeclarationSyntax)symbol.DeclaringSyntaxReferences.Single().GetSyntax();
        if (name is "RhpNewFast" or "RhpNewArrayFast" or "RhpNewPtrArrayFast" or "RhNewString")
        {
            if (!HostRuntimeAllocationBoundary.WrapperSignature(catalog, symbol, name)
                || !HostRuntimeAllocationBoundary.Halts(catalog, method))
                return DotNetN10RuntimeExportBehavior.Unknown;
            var profile = name switch
            {
                "RhpNewFast" => HostRuntimeAllocationBoundary.AllocationProfile(),
                "RhNewString" => HostRuntimeAllocationBoundary.AllocationProfile("EnableM04SzArrayAllocationProbe", "EnableM04StringAllocationProbe"),
                _ => HostRuntimeAllocationBoundary.AllocationProfile("EnableM04SzArrayAllocationProbe")
            };
            bool matches = name switch
            {
                "RhpNewFast" => HostRuntimeAllocationBoundary.Allocates(profile, name, "Allocate"),
                "RhNewString" => HostRuntimeAllocationBoundary.Allocates(profile, name, "AllocateString")
                    && HostRuntimeAllocationBoundary.StringAllocator(profile),
                _ => HostRuntimeAllocationBoundary.Allocates(profile, name, "AllocateArray", name == "RhpNewPtrArrayFast")
            };
            return !matches ? DotNetN10RuntimeExportBehavior.Unknown : name switch
            {
                "RhpNewFast" => DotNetN10RuntimeExportBehavior.ProfileBoundFixedAllocation,
                "RhNewString" => DotNetN10RuntimeExportBehavior.ProfileBoundStringAllocation,
                _ => DotNetN10RuntimeExportBehavior.ProfileBoundSzArrayAllocation
            };
        }
        if (method.Body?.Statements.Count == 0)
            return DotNetN10RuntimeExportBehavior.LinkOnlyNoOp;
        if (FailsFast(catalog, method))
            return DotNetN10RuntimeExportBehavior.FailFast;
        if (name == "RhpStackProbe" && StackProbe(catalog, symbol, method))
            return DotNetN10RuntimeExportBehavior.AuditedStackProbe;
        return DotNetN10RuntimeExportBehavior.Unknown;
    }

    internal static bool FailsFast(CSharpSourceCatalog catalog, MethodDeclarationSyntax method)
    {
        if (method.Body?.Statements is not { Count: 1 or 2 } statements
            || statements[0] is not ExpressionStatementSyntax { Expression: InvocationExpressionSyntax call }
            || catalog.Invocation(call) is not { Name: "Halt", IsStatic: true, Parameters.Length: 0 } halt
            || !SymbolEqualityComparer.Default.Equals(halt.ContainingType, catalog.Type("System.RuntimeFailFast")))
            return false;
        if (statements.Count == 1)
            return true;
        return statements[1] is ReturnStatementSyntax { Expression: { } expression }
            && catalog.Compilation.GetSemanticModel(method.SyntaxTree).GetOperation(expression)
                is IParameterReferenceOperation or ILiteralOperation;
    }

    internal static bool HaltDoesNotReturn(CSharpSourceCatalog catalog)
    {
        var method = catalog.Method("System.RuntimeFailFast", "Halt");
        var model = catalog.Compilation.GetSemanticModel(method.SyntaxTree);
        var symbol = (IMethodSymbol)model.GetDeclaredSymbol(method)!;
        return symbol is { IsStatic: true, Arity: 0, ReturnsVoid: true, Parameters.Length: 0 }
            && HostLedgerSourceGuard.ManagedImplementation(symbol)
            && method.Body?.Statements is [WhileStatementSyntax { Statement: BlockSyntax { Statements.Count: 0 } } loop]
            && model.GetConstantValue(loop.Condition) is { HasValue: true, Value: true };
    }

    private static bool StackProbe(CSharpSourceCatalog catalog, IMethodSymbol symbol, MethodDeclarationSyntax method)
    {
        // Keep the reviewed algorithm independent of source-file layout. Broader
        // stack-probe behavior and emitted machine code have separate fixtures.
        const string body = "{ ulong probeEntry = s_probeEntry; if (probeEntry == 0) { while (true) { } } ((delegate* managed<void>)(void*)probeEntry)(); }";
        if (!SyntaxFactory.AreEquivalent(method.Body, SyntaxFactory.ParseStatement(body))
            || symbol.ReturnType.SpecialType != SpecialType.System_Void || symbol.Parameters.Length != 0
            || !SymbolEqualityComparer.Default.Equals(symbol.ContainingType, catalog.Type("System.Runtime.X64StackProbeRuntime")))
            return false;
        var model = catalog.Compilation.GetSemanticModel(method.SyntaxTree);
        var local = method.Body!.Statements.OfType<LocalDeclarationStatementSyntax>().Single().Declaration.Variables.Single();
        return model.GetOperation(local.Initializer!.Value) is IFieldReferenceOperation field
            && field.Field.IsStatic && field.Field.Type.SpecialType == SpecialType.System_UInt64
            && SymbolEqualityComparer.Default.Equals(field.Field.ContainingType, symbol.ContainingType);
    }

    private static IReadOnlyList<CSharpSourceDocument> LedgerDocuments(CSharpSourceCatalog catalog)
        => new[] { typeof(DotNetN10SystemExportCompatibilityLedgerImplementedStubbed),
            typeof(DotNetN10SystemDeclarationCompatibilityEntry), typeof(DotNetN10RuntimeExportCompatibilityEntry) }
            .SelectMany(type => catalog.TypeSources(type.FullName!)).DistinctBy(document => document.Path).ToArray();

    private static bool ContainsCanonicalSystemNamespace(CSharpSourceCatalog catalog)
        => HostCanonicalTypeInventory.Read(catalog, LedgerDocuments(catalog)).Count != 0;

    private static void TestNoCanonicalSystemNamespaceSurface(CSharpSourceCatalog catalog)
    {
        HostCheck.True(!DotNetN10SystemExportCompatibilityLedgerImplementedStubbed.AddsCanonicalSystemNamespace
            && !DotNetN10SystemExportCompatibilityLedgerImplementedStubbed.SatisfiesRuntimeCompletionGate4
            && !ContainsCanonicalSystemNamespace(catalog));
        foreach (var type in new[] { typeof(DotNetN10SystemExportCompatibilityLedgerImplementedStubbed),
            typeof(DotNetN10SystemDeclarationCompatibilityEntry), typeof(DotNetN10RuntimeExportCompatibilityEntry) })
            HostCheck.Equal("OS.Std.NoRuntime", catalog.Type(type.FullName!).ContainingNamespace.ToDisplayString());
    }

    private static void TestEntriesAreReferenceFreeAndAllocationFreeByConstruction(CSharpSourceCatalog catalog)
    {
        HostCheck.True(!RuntimeHelpers.IsReferenceOrContainsReferences<DotNetN10SystemDeclarationCompatibilityEntry>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<DotNetN10RuntimeExportCompatibilityEntry>());
        var owner = catalog.Type(typeof(DotNetN10SystemExportCompatibilityLedgerImplementedStubbed).FullName!);
        var entries = new[] { typeof(DotNetN10SystemDeclarationCompatibilityEntry), typeof(DotNetN10RuntimeExportCompatibilityEntry) }
            .Select(type => catalog.Type(type.FullName!)).ToArray();
        HostCheck.True(entries.All(type => type.IsReadOnly && type.IsValueType));
        foreach (var document in LedgerDocuments(catalog))
        {
            HostLedgerSourceGuard.Validate(catalog, document, owner, entries);
            var model = catalog.Compilation.GetSemanticModel(document.Syntax);
            foreach (var node in document.Syntax.GetRoot().DescendantNodes())
            {
                if (node is AnonymousFunctionExpressionSyntax or QueryExpressionSyntax or ThrowStatementSyntax
                    or ThrowExpressionSyntax or AwaitExpressionSyntax or ArrayCreationExpressionSyntax
                    or ImplicitArrayCreationExpressionSyntax or AnonymousObjectCreationExpressionSyntax)
                    throw new InvalidOperationException("ledger_allocation_or_runtime_surface " + node.Kind());
                if (node is FieldDeclarationSyntax field)
                    HostCheck.True(field.Modifiers.Any(SyntaxKind.ConstKeyword), "ledger mutable/static storage");
                if (node is InvocationExpressionSyntax invocation)
                    HostCheck.True(SymbolEqualityComparer.Default.Equals(catalog.Invocation(invocation).ContainingType, owner),
                        "ledger foreign call");
                if (node is BaseObjectCreationExpressionSyntax creation)
                    HostCheck.True(model.GetOperation(creation) is IObjectCreationOperation operation
                        && entries.Any(type => SymbolEqualityComparer.Default.Equals(type, operation.Type)), "ledger allocation");
            }
        }
        foreach (string name in new[] { "GetSystemDeclaration", "GetRuntimeExport" })
            HostCheck.True(catalog.Method(owner.ToDisplayString(), name).Body?.Statements
                is [ReturnStatementSyntax { Expression: SwitchExpressionSyntax }], "ledger indexed retrieval");
    }
}
