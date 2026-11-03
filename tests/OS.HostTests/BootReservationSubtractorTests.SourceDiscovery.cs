using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Tooling;
using System.Text.RegularExpressions;

internal static partial class BootReservationSubtractorTests
{
    private const string SubtractorOwner = "OS.Kernel.Memory.BootReservationSubtractor";
    private static readonly string[] SubtractorOwners = [SubtractorOwner, "OS.Kernel.Memory.BootReservationSourceRange", "OS.Kernel.Memory.BootReservationSourceRangeStatus", "OS.Kernel.Memory.BootReservationSubtractionResult", "OS.Kernel.Memory.BootReservationSubtractionStatus"];
    private static CSharpSourceCatalog SubtractorEfi() => HostSourceCatalog.Efi(EfiOptions.Parse([], false).Profile());
    private static bool TestSourceFirewalls()
    {
        var source = SubtractorEfi();
        string project = File.ReadAllText(Path.Combine(HostToolProcess.FindRepositoryRoot(), "src", "OS", "OS.csproj"));
        return Regex.IsMatch(project, @"<EnableDefaultCompileItems>\s*false\s*</EnableDefaultCompileItems>", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase) && CheckSubtractorSource(source) && !System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<OS.Kernel.Memory.BootReservationSourceRange>() && !System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<OS.Kernel.Memory.BootReservationSubtractionResult>();
    }

    internal static bool CheckSubtractorSource(CSharpSourceCatalog source)
    {
        string production = RemoveComments(HostSourceText.Documents(source, SubtractorOwners));
        string[] forbiddenFragments = ["Span<", "ReadOnlySpan<", "Memory<", "ReadOnlyMemory<", "string", "System.", "System::", "throw ", "try ", "catch ", "finally ", "typeof", "Reflection", "Activator", "dynamic", "delegate", "record ", "List<", "Dictionary<", "IEnumerable<", "BootMemoryRegionPlan", "BootPageAllocatorPlan", "ulong.MaxValue", "EfiEntry", "BootSequence", "Qemu", "QEMU", "OSHost_", "RuntimeExport", "UnmanagedCallersOnly"];
        foreach (string fragment in forbiddenFragments)
            if (production.Contains(fragment, StringComparison.Ordinal))
                return SourceFailure("forbidden-fragment=" + fragment);
        if (Regex.IsMatch(production, @"(?m)^\s*(?:global\s+)?using\b", RegexOptions.CultureInvariant) || Regex.IsMatch(production, @"\b(?:throw|try|catch|finally)\b", RegexOptions.CultureInvariant) || Regex.IsMatch(production, @"\b(object|dynamic|delegate|record|Exception|String|Array|Type)\b", RegexOptions.CultureInvariant))
            return SourceFailure("production-surface");
        var owner = source.Type(SubtractorOwner);
        var maximum = owner.GetMembers("MaximumUnsignedLong").OfType<IFieldSymbol>().Single();
        if (owner.TypeKind != TypeKind.Class || !owner.IsStatic || owner.IsRecord || owner.Arity != 0 || owner.DeclaredAccessibility != Accessibility.Public || !owner.DeclaringSyntaxReferences.Any(reference => reference.GetSyntax() is TypeDeclarationSyntax declaration && declaration.Modifiers.Any(SyntaxKind.UnsafeKeyword)) || !Pod("OS.Kernel.Memory.BootReservationSourceRange") || !Pod("OS.Kernel.Memory.BootReservationSubtractionResult") || maximum is not { IsConst: true, DeclaredAccessibility: Accessibility.Private, ConstantValue: ulong.MaxValue })
            return SourceFailure("production-surface");
        string[] excluded = ["BootMemoryRegionPlanner", "BootMemoryRegionPlanStatus", "BootMemoryRegionPlanResult", "BootPageAllocatorPlanner", "BootPageAllocatorPlanStatus", "BootPageAllocatorPlanResult"];
        foreach (string type in excluded)
            if (source.Compilation.GetTypeByMetadataName("OS.Kernel.Memory." + type) is not null)
                return SourceFailure("efi-compile-import=" + type);
        string integration = HostSourceText.Documents(source, "OS.Boot.EfiEntry", "OS.Boot.BootSequence", "OS.Kernel.Diagnostics.Probes");
        string[] forbiddenIntegrationNames = [nameof(OS.Kernel.Memory.BootReservationSubtractor), nameof(OS.Kernel.Memory.BootReservationSourceRange), nameof(OS.Kernel.Memory.BootReservationSubtractionResult), ReadyMarker];
        foreach (string name in forbiddenIntegrationNames)
            if (integration.Contains(name, StringComparison.Ordinal))
                return SourceFailure("integration=" + name);
        return true;
        bool Pod(string identity) => source.Type(identity) is { TypeKind: TypeKind.Struct, IsReadOnly: true, IsRecord: false, Arity: 0, DeclaredAccessibility: Accessibility.Public };
    }

    private static bool SourceEnum<T>((string Name, byte Value)[] expected)
        where T : struct, Enum => CheckSubtractorEnum(SubtractorEfi(), typeof(T).FullName!, expected);
    internal static bool CheckSubtractorEnum(CSharpSourceCatalog source, string identity, (string Name, byte Value)[] expected)
    {
        var type = source.Type(identity);
        if (type.TypeKind != TypeKind.Enum || type.DeclaredAccessibility != Accessibility.Public || type.EnumUnderlyingType?.SpecialType != SpecialType.System_Byte)
            return false;
        var fields = type.GetMembers().OfType<IFieldSymbol>().Where(field => field.HasConstantValue).ToArray();
        return fields.Length == expected.Length && fields.Select(field => (field.Name, field.ConstantValue)).SequenceEqual(expected.Select(value => (value.Name, (object?)value.Value)));
    }

    // The host graph stops at metadata System types. Admit only byte-identical
    // inputs from the selected EFI project, then bind the unchanged local shim
    // under NOSTDLIB_EFI before producing separate Compile items.
    internal static CSharpSourceDocument[] SubtractorSources(string repositoryRoot, CSharpSourceCatalog host, CSharpSourceCatalog efi)
    {
        string kernelRoot = Path.Combine(repositoryRoot, "src", "OS.Kernel");
        string objectRoot = Path.GetFullPath(Path.Combine(kernelRoot, "obj")) + Path.DirectorySeparatorChar;
        var selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var document in host.DependencySources(SubtractorOwner))
        {
            var root = document.Syntax.GetRoot();
            var declarations = root.DescendantNodes().Where(node => node is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax).ToArray();
            bool generated = document.IsGenerated || document.Text.AsSpan(0, Math.Min(document.Text.Length, 256)).Contains("<autogenerated", StringComparison.OrdinalIgnoreCase);
            if (generated && document.Path.StartsWith(objectRoot, StringComparison.OrdinalIgnoreCase) && declarations.Length == 0 && root is CompilationUnitSyntax { Members.Count: 0, Externs.Count: 0 })
                continue;
            _ = EfiEvidence.RelativePath(kernelRoot, document.Path, "subtractor_dependency_outside_kernel");
            var admitted = efi.Documents.SingleOrDefault(value => string.Equals(value.Path, document.Path, StringComparison.OrdinalIgnoreCase));
            if (admitted is null || admitted.Sha256 != document.Sha256)
                throw new InvalidOperationException("subtractor_efi_source_join " + document.Path);
            var model = efi.Compilation.GetSemanticModel(admitted.Syntax);
            var types = admitted.Syntax.GetRoot().DescendantNodes().Where(node => node is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax).Select(node => (INamedTypeSymbol)model.GetDeclaredSymbol(node)!).ToArray();
            if (types.Length == 0 || types.Any(type => type.ContainingNamespace.ToDisplayString() is not ("OS.Boot" or "OS.Kernel.Memory")))
                throw new InvalidOperationException("subtractor_dependency_layer " + document.Path);
            selected.Add(admitted.Path);
        }

        if (selected.Count == 0)
            throw new InvalidOperationException("subtractor_dependency_empty");
        return efi.Documents.Where(document => selected.Contains(document.Path)).ToArray();
    }

    private static string[] NoStdLibSubtractorSources(string repositoryRoot, string shim)
    {
        var sources = SubtractorSources(repositoryRoot, HostSourceCatalog.Project("src/OS.Kernel/OS.Kernel.csproj"), SubtractorEfi());
        ValidateSubtractorShimClosure(sources, shim);
        foreach (var document in sources)
            AuthorityFiles.RequirePlainPath(repositoryRoot, document.Path, "subtractor_source_link");
        return sources.Select(document => document.Path).ToArray();
    }

    internal static void ValidateSubtractorShimClosure(IReadOnlyList<CSharpSourceDocument> sources, string shim)
    {
        var parse = new CSharpParseOptions(LanguageVersion.CSharp14, preprocessorSymbols: ["NOSTDLIB_EFI"]);
        var shimTree = CSharpSyntaxTree.ParseText(shim, parse, "NoStdLibShim.cs");
        string[] allowed = ["System.Object", "System.ValueType", "System.Enum", "System.Delegate", "System.MulticastDelegate", "System.Attribute", "System.Void", "System.Boolean", "System.Byte", "System.Int32", "System.UInt32", "System.UInt64", "System.IntPtr", "System.UIntPtr", "System.AttributeTargets", "System.AttributeUsageAttribute", "System.Runtime.CompilerServices.IsReadOnlyAttribute"];
        var declarations = shimTree.GetRoot().DescendantNodes().OfType<BaseTypeDeclarationSyntax>().ToArray();
        var identities = declarations.Select(type => string.Join(".", type.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().Reverse().Select(space => string.Concat(space.Name.DescendantTokens().Select(token => token.ValueText)))) + "." + type.Identifier.ValueText).ToArray();
        if (identities.Length != allowed.Length || !identities.Order(StringComparer.Ordinal).SequenceEqual(allowed.Order(StringComparer.Ordinal)) || declarations.Any(type => type.Ancestors().OfType<BaseTypeDeclarationSyntax>().Any() || type is TypeDeclarationSyntax { TypeParameterList: not null }) || shimTree.GetRoot().DescendantNodes().OfType<DelegateDeclarationSyntax>().Any())
            throw new InvalidOperationException("subtractor_shim_surface");
        var compilation = CSharpCompilation.Create("SubtractorShimClosure", sources.Select(document => CSharpSyntaxTree.ParseText(document.Text, parse, document.Path)).Append(shimTree), [], new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        var errors = compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray();
        if (errors.Length != 0)
            throw new InvalidOperationException("subtractor_shim_closure " + string.Join("; ", errors.Select(error => error.ToString())));
    }
}
