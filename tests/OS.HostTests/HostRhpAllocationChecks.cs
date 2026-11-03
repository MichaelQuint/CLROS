using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Kernel.Runtime;
using OS.Tooling;
using System.Security.Cryptography;
using System.Text;

/// <summary>Independent source contracts for the bounded allocation fixtures.
/// Source discovery supplies locations; original pins, signatures and runtime
/// contracts remain separate requirements.</summary>
internal static class HostRhpAllocationChecks
{
    internal static IReadOnlyDictionary<string, CSharpSourceDocument> KernelFamily(string root, CSharpSourceCatalog source)
    {
        var family = HostEfiFaultSources.PinnedFamily(root, source,
            typeof(GuestTestRhpNewFast), typeof(RhpNewFastStatus), typeof(RhpNewFastResult),
            typeof(RhpNewFastOperation), typeof(RhpNewFastHeapAccess), typeof(RhpNewFastBumpState));
        if (family.Values.Count(document => Hash(document.Text) == EfiContracts.AllocationSourceSha256) != 1)
            throw new InvalidOperationException("rhp_source_original_kernel_identity");
        return family;
    }

    internal static CSharpSourceDocument FrozenLeaf(CSharpSourceCatalog source, string owner, string hash)
    {
        var documents = HostSourceText.DocumentSources(source, owner);
        if (documents.Count != 1 || Hash(documents[0].Text) != hash)
            throw new InvalidOperationException("rhp_source_frozen_leaf_requires_review " + owner);
        return documents[0];
    }

    internal static CSharpSourceDocument FrozenEntry(CSharpSourceCatalog source)
    {
        var entry = source.Compilation.GetEntryPoint(default);
        if (entry is not { IsStatic: true, Arity: 0, ReturnsVoid: true, DeclaredAccessibility: Accessibility.Private }
            || !SymbolEqualityComparer.Default.Equals(entry.ContainingType, source.Type("OS.Boot.EfiEntry"))
            || entry.DeclaringSyntaxReferences.Length != 1)
            throw new InvalidOperationException("rhp_source_entry_identity");
        var document = source.Document(entry.DeclaringSyntaxReferences[0].GetSyntax());
        if (Hash(document.Text) != EfiContracts.EfiEntrySourceSha256)
            throw new InvalidOperationException("rhp_source_frozen_entry");
        return document;
    }

    internal static MethodDeclarationSyntax CoreAllocation(CSharpSourceCatalog source)
    {
        var result = source.Type("OS.Runtime.RhpAllocationAttempt");
        var method = source.Method(HostRhpAllocationSources.CoreOwner, "AllocateCore", symbol =>
            symbol.IsStatic && symbol.Arity == 0 && symbol.DeclaredAccessibility == Accessibility.Private
            && !symbol.ReturnsByRef && !symbol.ReturnsByRefReadonly
            && SymbolEqualityComparer.Default.Equals(symbol.ReturnType, result));
        _ = source.MethodInvocations(method);
        return method;
    }

    internal static MethodDeclarationSyntax Handoff(CSharpSourceCatalog source)
    {
        var method = source.Method("OS.Boot.EfiEntry+M01StageTwoAdapter", "RunRhpNewFastAfterZeroCheckpoint", symbol =>
            symbol.IsStatic && symbol.Arity == 0 && symbol.ReturnsVoid && symbol.Parameters.Length == 0
            && symbol.DeclaredAccessibility == Accessibility.Public);
        _ = source.MethodInvocations(method);
        return HostEfiFaultSources.StageOneCaller(source, method);
    }

    internal static void BumpAndBinder(CSharpSourceCatalog source)
    {
        var field = source.Type("OS.Runtime.RhpNewFastAllocation").GetMembers("s_bump").OfType<IFieldSymbol>().Single();
        if (!field.IsStatic || field.DeclaredAccessibility != Accessibility.Private
            || !SymbolEqualityComparer.Default.Equals(field.Type, source.Type(typeof(RhpNewFastBumpState).FullName!))
            || field.DeclaringSyntaxReferences.Single().GetSyntax() is not VariableDeclaratorSyntax { Initializer: null })
            throw new InvalidOperationException("rhp_source_bump_state");
        var initialize = source.Method(typeof(GuestTestRhpNewFast).FullName!, "InitializeOnce");
        var target = source.Compilation.GetSemanticModel(initialize.SyntaxTree).GetDeclaredSymbol(initialize)!;
        var bind = source.Method(HostRhpAllocationSources.CoreOwner, "Bind", symbol => symbol.IsStatic
            && symbol.Arity == 0 && symbol.DeclaredAccessibility == Accessibility.Internal);
        if (source.MethodInvocations(bind).Count(call => SymbolEqualityComparer.Default.Equals(call.TargetMethod, target)) != 1)
            throw new InvalidOperationException("rhp_source_original_binder");
    }

    internal static string ArrayShape(CSharpSourceCatalog source)
    {
        var owner = source.Type("System.Array");
        var field = owner.GetMembers("_numComponents").OfType<IFieldSymbol>().Single();
        var length = owner.GetMembers("Length").OfType<IPropertySymbol>().Single();
        if (field.IsStatic || field.DeclaredAccessibility != Accessibility.Private || field.Type.SpecialType != SpecialType.System_Int32
            || length.IsStatic || length.DeclaredAccessibility != Accessibility.Public || length.Type.SpecialType != SpecialType.System_Int32
            || length.RefKind != RefKind.None || length.GetMethod is null || length.SetMethod is not null
            || length.DeclaringSyntaxReferences.Single().GetSyntax() is not PropertyDeclarationSyntax
                { ExpressionBody.Expression: IdentifierNameSyntax expression }
            || !SymbolEqualityComparer.Default.Equals(source.Compilation.GetSemanticModel(expression.SyntaxTree)
                .GetSymbolInfo(expression).Symbol, field))
            throw new InvalidOperationException("rhp_source_array_shape");
        return HostSourceText.Declarations(source, "System.Array");
    }

    internal static void StringAuthority(CSharpSourceCatalog source)
    {
        foreach (string name in new[]
        {
            nameof(OS.Kernel.Diagnostics.Probes.EnableStringAllocationProbe),
            nameof(OS.Kernel.Diagnostics.Probes.EnableStringAllocationBeforeBindProbe),
            nameof(OS.Kernel.Diagnostics.Probes.EnableStringAllocationExhaustionProbe),
            nameof(OS.Kernel.Diagnostics.Probes.EnableStringAllocationInvalidLengthProbe)
        })
            _ = HostEfiFaultSources.FalseAuthority(source, name);
    }

    internal static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
        text.Replace("\r\n", "\n", StringComparison.Ordinal)))).ToLowerInvariant();
}
