using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Tooling;
using System.Text.RegularExpressions;

internal static partial class BootReservationTableValidatorTests
{
    private const string ReservationOwner = "OS.Kernel.Memory.BootReservationTableValidator";
    private const string RangeOwner = "OS.Kernel.Memory.BootReservedRange";
    private const string KindOwner = "OS.Kernel.Memory.BootReservationKind";
    private static readonly string[] ReservationOwners = [ReservationOwner, RangeOwner, KindOwner,
        "OS.Kernel.Memory.BootReservedRangeStatus", "OS.Kernel.Memory.BootReservationTableStatus",
        "OS.Kernel.Memory.BootReservationTableValidationResult"];

    private static CSharpSourceCatalog ReservationEfi() => HostSourceCatalog.Efi(EfiOptions.Parse([], false).Profile());

    private static bool ReservationKindSource(CSharpSourceCatalog source,
        (string Name, byte Value)[] required, int runtimeCount)
    {
        var type = source.Type(KindOwner);
        if (type.TypeKind != TypeKind.Enum || type.DeclaredAccessibility != Accessibility.Public
            || type.EnumUnderlyingType?.SpecialType != SpecialType.System_Byte)
            return false;
        var syntax = (EnumDeclarationSyntax)type.DeclaringSyntaxReferences.Single().GetSyntax();
        var model = source.Compilation.GetSemanticModel(syntax.SyntaxTree);
        var declarations = syntax.Members.Select(member => (IFieldSymbol)model.GetDeclaredSymbol(member)!).ToArray();
        if (declarations.Length != runtimeCount || declarations.Length < required.Length)
            return false;
        // The original raw enum reader also saw inactive declarations. Collect
        // their physical locations so a hidden extra member cannot disappear
        // merely because the selected compiler branch omits it.
        var rawTypes = new HashSet<(string Path, int Position)>();
        var rawMembers = new HashSet<(string Path, int Position)>();
        foreach (var document in HostSourceText.DocumentSources(source, KindOwner))
            foreach (var options in CSharpSourceProfiles.ForSource(document.Text))
                foreach (var candidate in CSharpSyntaxTree.ParseText(document.Text, options).GetRoot()
                    .DescendantNodes().OfType<EnumDeclarationSyntax>().Where(candidate => candidate.Identifier.ValueText == "BootReservationKind"))
                {
                    rawTypes.Add((document.Path, candidate.Identifier.SpanStart));
                    foreach (var member in candidate.Members)
                        rawMembers.Add((document.Path, member.Identifier.SpanStart));
                }
        if (rawTypes.Count != 1 || rawMembers.Count != declarations.Length)
            return false;
        int previous = -1;
        for (int index = 0; index < declarations.Length; index++)
        {
            var declaration = declarations[index];
            if (declaration.ConstantValue is not byte value || value <= previous)
                return false;
            previous = value;
            if (index < required.Length)
            {
                if (declaration.Name != required[index].Name || value != required[index].Value)
                    return false;
            }
            else if (value <= (byte)OS.Kernel.Memory.BootReservationKind.PageProtectionProbe)
                return false;
        }
        return true;
    }

    private static bool CheckReservationSource(CSharpSourceCatalog source)
    {
        var documents = HostSourceText.DocumentSources(source, ReservationOwners);
        string[] forbiddenFragments = ["using ", "System.", "System::", "throw ", "catch ", "finally ", "typeof", "Reflection", "Activator", "dynamic", "delegate", "interface ", "record ", "Span<", "ReadOnlySpan<", "Memory<", "ReadOnlyMemory<", "ulong.MaxValue", "List<", "Dictionary<", "IEnumerable<", "GetRawValue", "operator "];
        var ownerParts = new HashSet<(string Path, int Position)>();
        bool allOwnerPartsPartial = true;
        foreach (var document in documents)
        {
            string text = RemoveLineComments(document.Text);
            foreach (string fragment in forbiddenFragments)
                if (text.Contains(fragment, StringComparison.Ordinal))
                    return SourceFirewallFailure("forbidden-fragment=" + fragment);
            if (Regex.IsMatch(text, @"(?m)^\s*(?:global\s+)?using\b", RegexOptions.CultureInvariant)
                || Regex.IsMatch(text, @"\b(string|object|dynamic|delegate|interface|record|var|is|as|Object|Delegate|MulticastDelegate|Attribute|String|Array|Type|Exception|Span|ReadOnlySpan)\b", RegexOptions.CultureInvariant)
                || Regex.IsMatch(text, @"\[\s*\]", RegexOptions.CultureInvariant)
                || Regex.IsMatch(text, @"\bnew\s*\(", RegexOptions.CultureInvariant))
                return SourceFirewallFailure("lexical-surface=" + Path.GetFileName(document.Path));
            foreach (Match allocation in Regex.Matches(text, @"\bnew\s+([A-Za-z_][A-Za-z0-9_]*)", RegexOptions.CultureInvariant))
                if (allocation.Groups[1].Value is not (nameof(OS.Kernel.Memory.BootReservedRange)
                    or nameof(OS.Kernel.Memory.BootReservationTableValidationResult) or "ValidationState"))
                    return SourceFirewallFailure("allocation-target=" + allocation.Groups[1].Value);
            // The former one-class rule describes one logical owner. Permit its
            // partial declarations, while retaining the raw inactive-class firewall.
            var ownerKeywords = new HashSet<int>();
            foreach (var options in CSharpSourceProfiles.ForSource(text))
            {
                var tree = CSharpSyntaxTree.ParseText(text, options, document.Path);
                foreach (var declaration in tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>())
                {
                    string space = string.Join(".", declaration.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().Reverse()
                        .Select(item => string.Concat(item.Name.DescendantTokens().Select(token => token.ValueText))));
                    if (space + "." + declaration.Identifier.ValueText == ReservationOwner
                        && !declaration.Ancestors().OfType<BaseTypeDeclarationSyntax>().Any()
                        && declaration.TypeParameterList is null)
                    {
                        if (!declaration.Modifiers.Any(SyntaxKind.PublicKeyword)
                            || !declaration.Modifiers.Any(SyntaxKind.StaticKeyword)
                            || !declaration.Modifiers.Any(SyntaxKind.UnsafeKeyword)
                            || declaration.Modifiers.Any(token => !token.IsKind(SyntaxKind.PublicKeyword)
                                && !token.IsKind(SyntaxKind.StaticKeyword)
                                && !token.IsKind(SyntaxKind.UnsafeKeyword)
                                && !token.IsKind(SyntaxKind.PartialKeyword)))
                            return SourceFirewallFailure("class-modifiers");
                        ownerParts.Add((document.Path, declaration.Keyword.SpanStart));
                        allOwnerPartsPartial &= declaration.Modifiers.Any(SyntaxKind.PartialKeyword);
                        ownerKeywords.Add(declaration.Keyword.SpanStart);
                    }
                }
            }
            if (Regex.Matches(text, @"\bclass\b", RegexOptions.CultureInvariant).Any(match => !ownerKeywords.Contains(match.Index)))
                return SourceFirewallFailure("class-shape");
        }
        if (ownerParts.Count > 1 && !allOwnerPartsPartial)
            return SourceFirewallFailure("class-partial-shape");
        var owner = source.Type(ReservationOwner);
        return owner is { TypeKind: TypeKind.Class, IsStatic: true, IsRecord: false, Arity: 0, DeclaredAccessibility: Accessibility.Public }
            && owner.DeclaringSyntaxReferences.Any(reference => reference.GetSyntax() is ClassDeclarationSyntax declaration
                && declaration.Modifiers.Any(SyntaxKind.UnsafeKeyword));
    }

    internal static CSharpSourceDocument[] ReservationSources(string root, CSharpSourceCatalog host, CSharpSourceCatalog efi)
    {
        string kernelRoot = Path.Combine(root, "src", "OS.Kernel");
        string objectRoot = Path.GetFullPath(Path.Combine(kernelRoot, "obj")) + Path.DirectorySeparatorChar;
        var selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var document in host.DependencySources(ReservationOwner, RangeOwner))
        {
            var syntax = document.Syntax.GetRoot();
            var declarations = syntax.DescendantNodes().Where(node => node is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax).ToArray();
            bool generated = document.IsGenerated || document.Text.AsSpan(0, Math.Min(document.Text.Length, 256)).Contains("<autogenerated", StringComparison.OrdinalIgnoreCase);
            if (generated && document.Path.StartsWith(objectRoot, StringComparison.OrdinalIgnoreCase) && declarations.Length == 0 && syntax is CompilationUnitSyntax { Members.Count: 0, Externs.Count: 0 })
                continue;
            _ = EfiEvidence.RelativePath(kernelRoot, document.Path, "reservation_dependency_outside_kernel");
            var admitted = efi.Documents.SingleOrDefault(value => string.Equals(value.Path, document.Path, StringComparison.OrdinalIgnoreCase));
            if (admitted is null || admitted.Sha256 != document.Sha256)
                throw new InvalidOperationException("reservation_efi_source_join " + document.Path);
            var model = host.Compilation.GetSemanticModel(document.Syntax);
            if (declarations.Length == 0 || declarations.Any(node => model.GetDeclaredSymbol(node) is not INamedTypeSymbol type || type.ContainingNamespace.ToDisplayString() is not ("OS.Boot" or "OS.Kernel.Memory")))
                throw new InvalidOperationException("reservation_dependency_layer " + document.Path);
            var efiModel = efi.Compilation.GetSemanticModel(admitted.Syntax);
            if (admitted.Syntax.GetRoot().DescendantNodes().Where(node => node is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax).Any(node => efiModel.GetDeclaredSymbol(node) is not INamedTypeSymbol type || type.ContainingNamespace.ToDisplayString() is not ("OS.Boot" or "OS.Kernel.Memory")))
                throw new InvalidOperationException("reservation_efi_dependency_layer " + document.Path);
            selected.Add(admitted.Path);
        }

        if (selected.Count == 0)
            throw new InvalidOperationException("reservation_dependency_empty");
        return efi.Documents.Where(document => selected.Contains(document.Path)).ToArray();
    }

    private static string[] NoStdLibReservationSources(string root, string shim)
    {
        var sources = ReservationSources(root, HostSourceCatalog.Project("src/OS.Kernel/OS.Kernel.csproj"), ReservationEfi());
        // The unchanged oracle uses exactly the same restricted System shim.
        HostBootPageAllocatorSources.ValidateShimClosure(sources, shim);
        foreach (var document in sources)
            AuthorityFiles.RequirePlainPath(root, document.Path, "reservation_source_link");
        return sources.Select(document => document.Path).ToArray();
    }
}
