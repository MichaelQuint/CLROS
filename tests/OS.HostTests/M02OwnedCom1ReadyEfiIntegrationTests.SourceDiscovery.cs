using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Tooling;
using System.Text.RegularExpressions;

internal static partial class M02OwnedCom1ReadyEfiIntegrationTests
{
    private const string OwnedCom1Owner = "OS.Boot.EfiEntry+M01StageThreeAdapter";
    private const string PanicOwner = "OS.Boot.EfiEntry+M02UnexpectedVectorPanicAdapter";
    private const string PanicSymbol = "M02_UNEXPECTED_VECTOR_PANIC_PROBE";
    private const string BackendConstruction = "new OS.Kernel.Diagnostics.Com1OwnedPortIoBackend(";
    private const string PanicBinding = "s_authority.Backend = new OS.Kernel.Diagnostics.Com1OwnedPortIoBackend(binding.Binding);";

    private static CSharpSourceCatalog OwnedCom1Sources() => HostSourceCatalog.Efi(
        EfiOptions.Parse(EfiProfileTestControls.ReturningArguments("M02OwnedCom1Ready"), true).Profile());

    private static CSharpSourceCatalog OwnedPanicSources() => HostSourceCatalog.Efi(
        EfiOptions.Parse(["--EnableM02UnexpectedVectorPanicProbe"], true).Profile());

    private static MethodDeclarationSyntax OwnedCom1Caller(CSharpSourceCatalog source)
    {
        _ = OwnedCom1Method(source, "WriteM02OwnedCom1ReadySuccessLine");
        var call = HostSourceOperations.CallsTo(source, OwnedCom1Owner, "WriteM02OwnedCom1ReadySuccessLine").Single();
        var method = call.Ancestors().OfType<MethodDeclarationSyntax>().First();
        var symbol = source.Compilation.GetSemanticModel(method.SyntaxTree).GetDeclaredSymbol(method)!;
        HostCheck.True(SymbolEqualityComparer.Default.Equals(symbol.ContainingType, source.Type(OwnedCom1Owner))
            && symbol.IsStatic && symbol.Arity == 0 && symbol.ReturnsVoid
            && symbol.DeclaredAccessibility == Accessibility.Private, "owned COM1 evidence caller");
        _ = source.MethodInvocations(method);
        return method;
    }

    private static MethodDeclarationSyntax OwnedCom1Method(CSharpSourceCatalog source, string name)
    {
        var method = source.Method(OwnedCom1Owner, name, symbol => symbol.IsStatic && symbol.Arity == 0
            && symbol.ReturnsVoid && symbol.DeclaredAccessibility == Accessibility.Private);
        _ = source.MethodInvocations(method);
        return method;
    }

    private static string OwnedCom1Marker(CSharpSourceCatalog source, string name)
    {
        string method = OwnedCom1Method(source, name).ToString();
        // Retain the original raw character matcher, including inactive text.
        return string.Concat(Regex.Matches(method, @"\(byte\)'(?<value>.)'", RegexOptions.CultureInvariant)
            .Cast<Match>().Select(match => match.Groups["value"].Value));
    }

    private static bool HasExpectedBackendConstructions(CSharpSourceCatalog source)
    {
        const string backend = "OS.Kernel.Diagnostics.Com1OwnedPortIoBackend";
        var owner = source.Type(PanicOwner);
        if (!owner.IsStatic || owner.Arity != 0 || owner.DeclaredAccessibility != Accessibility.Private)
            return false;
        string identity = RawPanicOwner((MemberDeclarationSyntax)owner.DeclaringSyntaxReferences[0].GetSyntax());
        var declarations = HostSourceText.DocumentSources(source, PanicOwner)
            .SelectMany(document => CSharpSourceProfiles.ForSource(document.Text, document.Path)
                .SelectMany(options => CSharpSyntaxTree.ParseText(document.Text, options, document.Path).GetRoot()
                    .DescendantNodes().OfType<ClassDeclarationSyntax>())
                .Where(declaration => RawPanicOwner(declaration) == identity)
                .DistinctBy(declaration => declaration.Span)).ToArray();
        if (declarations.Length == 0)
            return false;
        foreach (var declaration in declarations)
        {
            if (!declaration.Modifiers.Any(SyntaxKind.PrivateKeyword) || !declaration.Modifiers.Any(SyntaxKind.StaticKeyword)
                || declaration.Modifiers.Any(token => !token.IsKind(SyntaxKind.PrivateKeyword)
                    && !token.IsKind(SyntaxKind.StaticKeyword) && !token.IsKind(SyntaxKind.PartialKeyword))
                || (declarations.Length > 1 && !declaration.Modifiers.Any(SyntaxKind.PartialKeyword)))
                return false;
            var guard = declaration.GetLeadingTrivia().LastOrDefault(trivia => trivia.IsDirective).GetStructure() as IfDirectiveTriviaSyntax;
            if (guard is null || guard.Condition.ToString() != PanicSymbol
                || guard.GetRelatedDirectives().Count != 2
                || guard.GetRelatedDirectives()[1] is not EndIfDirectiveTriviaSyntax)
                return false;
            string body = declaration.ToString().ReplaceLineEndings("\n");
            if (body.Contains("\n#if", StringComparison.Ordinal) || body.Contains("\n#else", StringComparison.Ordinal)
                || body.Contains("\n#elif", StringComparison.Ordinal))
                return false;
        }
        string panic = string.Join("\n", declarations.Select(declaration => declaration.ToString()));
        string all = HostSourceText.Documents(source, "OS.Boot.EfiEntry");
        if (!panic.Contains(PanicBinding, StringComparison.Ordinal)
            || CountOccurrences(panic, BackendConstruction) != 1
            || CountOccurrences(all, BackendConstruction) - CountOccurrences(panic, BackendConstruction) != 3)
            return false;
        // The raw construction must also be active and bound to the retained
        // panic owner. Inactive text and a same-named foreign constructor cannot
        // satisfy the original separately gated fourth-construction policy.
        var constructions = owner.DeclaringSyntaxReferences.Select(reference => reference.GetSyntax())
            .SelectMany(declaration => declaration.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
            .Where(creation => creation.ToString().StartsWith(BackendConstruction, StringComparison.Ordinal)).ToArray();
        if (constructions.Length != 1)
            return false;
        var construction = constructions[0];
        var model = source.Compilation.GetSemanticModel(construction.SyntaxTree);
        var constructor = model.GetSymbolInfo(construction).Symbol as IMethodSymbol;
        var method = construction.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
        if (constructor is null || method is null
            || !SymbolEqualityComparer.Default.Equals(constructor.ContainingType, source.Type(backend))
            || !SymbolEqualityComparer.Default.Equals(model.GetDeclaredSymbol(method)!.ContainingType, owner))
            return false;
        _ = source.MethodInvocations(method);
        return true;
    }
    private static string RawPanicOwner(MemberDeclarationSyntax declaration)
    {
        string identity = RepositoryCodeStyle.TypeIdentity(declaration);
        // Raw exclusions reserve the containing chain and arity across namespaces,
        // matching HostSourceText.DocumentSources. Active ownership stays semantic.
        return identity[(identity.LastIndexOf(':') + 1)..];
    }
}
