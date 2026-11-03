using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Kernel.Diagnostics;
using OS.Kernel.Runtime;
using OS.Tooling;

internal static partial class M02UnexpectedVectorPanicEfiIntegrationTests
{
    private const string PanicOwner = "OS.Boot.EfiEntry+M02UnexpectedVectorPanicAdapter";

    private static CSharpSourceCatalog PanicSources() =>
        HostSourceCatalog.Efi(EfiOptions.Parse(["--" + Flag], true).Profile());

    private static Type[] PanicOwners() =>
    [
        typeof(UnexpectedVectorPanicStatus), typeof(UnexpectedVectorPanicRequest),
        typeof(UnexpectedVectorPanicResult), typeof(UnexpectedVectorPanicEvidenceDeterministicQEMUFailureClassification)
    ];

    private static Type[] PanicAllocationOwners() =>
    [
        typeof(GuestTestRhpNewFast), typeof(RhpNewFastStatus), typeof(RhpNewFastResult),
        typeof(RhpNewFastOperation), typeof(RhpNewFastHeapAccess), typeof(RhpNewFastBumpState)
    ];

    // These are the original fixture's ABI selectors, independent of filenames.
    private static MethodDeclarationSyntax PanicMethod(CSharpSourceCatalog source, string signature)
    {
        Require(source.Type(PanicOwner) is
        {
            TypeKind: TypeKind.Class, IsStatic: true, Arity: 0,
            DeclaredAccessibility: Accessibility.Private, ContainingType: not null
        }, "original private static adapter owner");
        string[] parts = signature.Split(' ');
        if (parts.Length != 4 || parts[1] != "static" || !parts[3].EndsWith('('))
            throw new InvalidOperationException("panic_method_selector " + signature);
        Accessibility access = parts[0] switch
        {
            "internal" => Accessibility.Internal,
            "private" => Accessibility.Private,
            _ => throw new InvalidOperationException("panic_method_access " + signature)
        };
        SpecialType result = parts[2] switch
        {
            "void" => SpecialType.System_Void,
            "bool" => SpecialType.System_Boolean,
            "byte" => SpecialType.System_Byte,
            "uint" => SpecialType.System_UInt32,
            _ => throw new InvalidOperationException("panic_method_return " + signature)
        };
        var method = source.Method(PanicOwner, parts[3][..^1], symbol => symbol.IsStatic && symbol.Arity == 0
            && symbol.DeclaredAccessibility == access && symbol.ReturnType.SpecialType == result
            && !symbol.ReturnsByRef && !symbol.ReturnsByRefReadonly);
        _ = source.MethodInvocations(method);
        return method;
    }

    private static CSharpSourceDocument PanicAuthorityDocument(CSharpSourceCatalog source)
    {
        _ = HostEfiFaultSources.FalseAuthority(source, nameof(Probes.EnableUnexpectedVectorPanicProbe));
        var field = source.Type(typeof(Probes).FullName!).GetMembers(nameof(Probes.EnableUnexpectedVectorPanicProbe))
            .OfType<IFieldSymbol>().Single();
        return source.Document(field.DeclaringSyntaxReferences.Single().GetSyntax());
    }

    private static bool PanicTerminalTail(MethodDeclarationSyntax method)
    {
        var attempts = method.DescendantNodes().OfType<AssignmentExpressionSyntax>()
            .Where(assignment => assignment.IsKind(SyntaxKind.SimpleAssignmentExpression)
                && assignment.Left is MemberAccessExpressionSyntax
                {
                    Expression: IdentifierNameSyntax { Identifier.ValueText: "s_authority" },
                    Name.Identifier.ValueText: "State"
                }
                && assignment.Right is LiteralExpressionSyntax { Token.Value: int value } && value == 2).ToArray();
        if (attempts.Length != 1)
            return false;
        string raw = method.ToString();
        int attempt = attempts[0].SpanStart - method.SpanStart;
        int rawAnchor = raw.IndexOf("s_authority.State = 2;", StringComparison.Ordinal);
        if (rawAnchor >= 0 && rawAnchor != attempt)
            return false;
        string tail = raw[attempt..];
        return !tail.Contains("FreePages", StringComparison.Ordinal)
            && !tail.Contains("FailBeforeEntry", StringComparison.Ordinal)
            && HostSourceText.Contains(tail, "Spin();");
    }

    private static bool PanicTemporaryAdmissionFirst(string body)
    {
        const string cleanupCall = "FailBeforeEntry(console, serial, services, c, allocatedC, d, allocatedD, (ulong)image, imageBytes, binding.PageAddress, observationAddress, scratchAddress, selectorsAddress);";
        // Retain the original raw occurrence guard as well as active call order.
        // A commented or disabled extra cleanup call must not disappear here.
        return HostSourceText.IndexOf(body, "ulong* selectors = stackalloc") >= 0
            && HostSourceText.IndexOf(body, "ulong* selectors = stackalloc") < HostSourceText.IndexOf(body, "!TemporaryStorage(")
            && HostSourceText.IndexOf(body, "!TemporaryStorage(") < HostSourceText.IndexOf(body, "services->AllocatePages(")
            && HostSourceText.Count(body, cleanupCall) == 6
            && body.Split(cleanupCall, StringSplitOptions.None).Length == 7;
    }

    private static (string Constants, string Members) PanicSdkMembers(CSharpSourceCatalog source,
        IReadOnlyList<string> signatures, bool cleanup)
    {
        // This shared projection freezes the selected branches and verifies that
        // removing visible imports does not change selected bindings. The two
        // original SDK harnesses then retain their distinct bounded member sets.
        string projected = HostEfiAdapterSources.ForHost(source, PanicOwner, "FailBeforeEntry", "PlaceSentinel");
        var declarations = CSharpSyntaxTree.ParseText(projected, new CSharpParseOptions(LanguageVersion.CSharp14))
            .GetCompilationUnitRoot().Members.OfType<ClassDeclarationSyntax>().ToArray();
        var members = declarations.SelectMany(declaration => declaration.Members).ToArray();
        var selected = new List<string>();
        foreach (string signature in signatures)
        {
            if (signature == "private struct Authority")
            {
                var authority = source.Type(PanicOwner + "+Authority");
                Require(authority is { TypeKind: TypeKind.Struct, Arity: 0, DeclaredAccessibility: Accessibility.Private },
                    "original nested cleanup authority identity");
                var structures = members.OfType<StructDeclarationSyntax>()
                    .Where(declaration => declaration.Identifier.ValueText == "Authority").ToArray();
                Require(structures.Length == authority.DeclaringSyntaxReferences.Length && structures.Length != 0,
                    "complete nested cleanup authority declarations");
                foreach (var structure in structures)
                {
                    Require(structure.AttributeLists.Count == 0, "SDK authority attribute context requires review");
                    var access = structure.Modifiers.Single(token => token.IsKind(SyntaxKind.PrivateKeyword));
                    selected.Add(structure.WithModifiers(structure.Modifiers.Replace(access,
                        SyntaxFactory.Token(access.LeadingTrivia, SyntaxKind.InternalKeyword, access.TrailingTrivia))).ToString());
                }
                continue;
            }
            var actual = PanicMethod(source, signature);
            var method = members.OfType<MethodDeclarationSyntax>()
                .Single(declaration => declaration.Identifier.ValueText == actual.Identifier.ValueText);
            Require(method.AttributeLists.Count == 0, "SDK selected method attribute context requires review");
            selected.Add(method.ToString());
        }
        var constants = members.OfType<FieldDeclarationSyntax>()
            .Where(field => field.Modifiers.Any(SyntaxKind.ConstKeyword))
            .Where(field => !cleanup || field.Declaration.Variables.Any(variable => variable.Identifier.ValueText is "Page" or "LowEnd"))
            .ToArray();
        foreach (var field in constants)
            Require(field.AttributeLists.Count == 0 && field.Modifiers.Any(SyntaxKind.PrivateKeyword),
                "original private constant field context");
        Require(constants.Length != 0, "nonempty original constants");
        return (string.Join("\n", constants.Select(field => field.ToString())), string.Join("\n", selected));
    }

    private static CSharpSourceDocument[] PanicCleanupSources(CSharpSourceCatalog source)
    {
        string[] owners =
        [
            "OS.Boot.M02CheckedPortIoFunctionPointerIntegrationStatus",
            "OS.Boot.M02CheckedPortIoFunctionPointerIntegrationResult",
            "OS.Boot.M02CheckedPortIoFunctionPointerIntegration",
            "OS.Boot.M02SafeStubPageIntegrationPhase", "OS.Boot.M02SafeStubPageIntegrationStatus",
            "OS.Boot.M02SafeStubPageIntegrationResult", "OS.Boot.M02SafeStubPageIntegration"
        ];
        var selected = owners.SelectMany(owner => source.TypeSources(owner)).ToHashSet();
        return source.Documents.Where(selected.Contains).ToArray();
    }
}
