using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Tooling;

/// <summary>Discovers the original bounded allocation probe's runtime and host
/// source families. The POD core's explicit member boundary excludes the separate
/// reference-stack service, even though both share a partial type. These are
/// inspection/SDK inputs; discovery grants no native execution or source admission.</summary>
internal static class HostRhpAllocationSources
{
    internal const string CoreOwner = "OS.Runtime.RhpNewFastAllocationCore";

    private static string[] RuntimeOwners() =>
    [
        "OS.Runtime.RhpAdmissionStatus", "OS.Runtime.RhpAllocationControl",
        "OS.Runtime.RhpObjectObservation", "OS.Runtime.RhpObservationStorage",
        "OS.Runtime.RhpAllocationAttempt", CoreOwner, "OS.Runtime.RhpNewFastAllocation",
        "OS.Runtime.RhpPlainFixture", "OS.Runtime.RhpReferenceFixture",
        "OS.Runtime.RhpGenericFixture`1", "OS.Runtime.RhpFixedValueFixture",
        "OS.Runtime.RhpReferenceValueFixture", "OS.Runtime.RhpGenericValueFixture`1",
        "OS.Runtime.RhpEnumFixture"
    ];

    // Original POD member authority, not a file inventory. A new member requires
    // review before the bounded SDK harness may compile it. Moves keep identities.
    private static string[] CoreMembers() =>
    [
        "Canary", "ControlBytes", "ObservationBytes", "Prepare", "Bind", "Allocate",
        "AllocateMixed", "AllocateStrings", "AllocateCore", "RejectedAllocationUnchanged",
        "Complete", "CompleteMixed", "CompleteStrings", "CheckCompleted", "Retained",
        "Ready", "Storage", "PrefixDisjoint", "TryReadPrefix", "Image", "Observe",
        "Prior", "Untouched", "ReadScalar", "Zero", "Range", "Overlap", "Read64",
        "Read32", "Read16"
    ];

    internal static IReadOnlyList<CSharpSourceDocument> RuntimeFamily(CSharpSourceCatalog source)
    {
        var owners = RuntimeOwners().Select(source.Type).ToHashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        var core = source.Type(CoreOwner);
        if (core is not { TypeKind: TypeKind.Class, IsStatic: true, Arity: 0, DeclaredAccessibility: Accessibility.Internal })
            throw new InvalidOperationException("rhp_source_core_owner");
        var members = CoreMembers().Select(name =>
        {
            var candidates = core.GetMembers(name).Where(member => !member.IsImplicitlyDeclared).ToArray();
            return candidates.Length == 1 && candidates[0].DeclaringSyntaxReferences.Length != 0
                ? candidates[0] : throw new InvalidOperationException("rhp_source_core_member " + name);
        }).ToHashSet(SymbolEqualityComparer.Default);
        var selected = owners.Where(owner => !SymbolEqualityComparer.Default.Equals(owner, core))
            .SelectMany(owner => source.TypeSources(MetadataName(owner)))
            .Concat(members.SelectMany(member => member.DeclaringSyntaxReferences)
                .Select(reference => source.Document(reference.GetSyntax()))).ToHashSet();
        foreach (var document in selected)
        {
            var model = source.Compilation.GetSemanticModel(document.Syntax);
            foreach (var declaration in document.Syntax.GetRoot().DescendantNodes()
                .OfType<MemberDeclarationSyntax>().Where(RepositoryCodeStyle.IsType))
            {
                if (model.GetDeclaredSymbol(declaration) is not INamedTypeSymbol type || !owners.Contains(type))
                    throw new InvalidOperationException("rhp_source_unrelated_runtime_owner " + document.Path);
                if (model.GetDiagnostics(declaration.Span).Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
                    throw new InvalidOperationException("rhp_source_runtime_binding " + document.Path);
                if (!SymbolEqualityComparer.Default.Equals(type, core))
                    continue;
                foreach (var member in ((TypeDeclarationSyntax)declaration).Members)
                {
                    var symbols = member is FieldDeclarationSyntax field
                        ? field.Declaration.Variables.Select(variable => model.GetDeclaredSymbol(variable))
                        : [model.GetDeclaredSymbol(member)];
                    if (symbols.Any(symbol => symbol is null || !members.Contains(symbol)))
                        throw new InvalidOperationException("rhp_source_expanded_core " + document.Path);
                }
            }
            foreach (var root in WitnessRoots(document))
            {
                RequireDocumentContext(root, "OS.Runtime", document.Path);
                foreach (var declaration in root.DescendantNodes().OfType<MemberDeclarationSyntax>().Where(RepositoryCodeStyle.IsType))
                {
                    string identity = Identity(declaration);
                    var owner = owners.SingleOrDefault(item => identity == SyntaxIdentity(item));
                    if (owner is null || owner.DeclaringSyntaxReferences.All(reference => reference.GetSyntax().RawKind != declaration.RawKind))
                        throw new InvalidOperationException("rhp_source_conditional_runtime_owner " + document.Path);
                    if (!SymbolEqualityComparer.Default.Equals(owner, core))
                        continue;
                    foreach (var member in ((TypeDeclarationSyntax)declaration).Members)
                        foreach (string name in MemberNames(member))
                        {
                            var original = members.SingleOrDefault(item => item.Name == name);
                            if (original is null || !original.DeclaringSyntaxReferences.Any(reference =>
                                reference.SyntaxTree == document.Syntax && reference.Span.Start == MemberStart(member, name)
                                && Header(MemberDeclaration(reference.GetSyntax())).SequenceEqual(Header(member))))
                                throw new InvalidOperationException("rhp_source_conditional_core_member " + name);
                        }
                }
            }
        }
        return source.Documents.Where(selected.Contains).ToArray();
    }

    internal static IReadOnlyList<CSharpSourceDocument> ProgramFamily(CSharpSourceCatalog host,
        string program, string symbol)
    {
        // Witnesses include SDK-only branches without inventing an SDK-generated
        // NET/TRACE symbol list. This is conservative source-family selection,
        // not an exhaustive semantic model of every SDK compiler configuration.
        if (string.IsNullOrWhiteSpace(symbol))
            throw new ArgumentException("rhp_source_sdk_symbol", nameof(symbol));
        var selected = new List<CSharpSourceDocument>();
        foreach (var document in host.Documents)
        {
            string owner = ":" + program + "`0";
            if (!WitnessRoots(document).Any(root => root.DescendantNodes().OfType<MemberDeclarationSyntax>()
                .Where(RepositoryCodeStyle.IsType).Any(type => Identity(type) == owner)))
                continue;
            foreach (var root in WitnessRoots(document))
            {
                RequireDocumentContext(root, "", document.Path);
                var types = root.DescendantNodes().OfType<MemberDeclarationSyntax>().Where(RepositoryCodeStyle.IsType);
                if (types.Any(type => Identity(type) != owner && !Identity(type).StartsWith(owner + "+", StringComparison.Ordinal)))
                    throw new InvalidOperationException("rhp_source_sdk_program_scope " + document.Path);
                foreach (var declaration in types.Where(type => Identity(type) == owner))
                    if (declaration is not ClassDeclarationSyntax type || !type.Modifiers.Any(SyntaxKind.StaticKeyword)
                        || type.Modifiers.Any(SyntaxKind.PublicKeyword) || type.Modifiers.Any(SyntaxKind.FileKeyword))
                        throw new InvalidOperationException("rhp_source_sdk_program_owner " + document.Path);
            }
            selected.Add(document);
        }
        if (selected.Count == 0)
            throw new InvalidOperationException("rhp_source_sdk_program_missing " + program);
        var selectedParse = new CSharpParseOptions(LanguageVersion.CSharp14, preprocessorSymbols: [symbol]);
        if (!selected.Any(document => CSharpSyntaxTree.ParseText(document.Text, selectedParse).GetRoot()
            .DescendantNodes().OfType<TypeDeclarationSyntax>().Any(type => Identity(type) == ":" + program + "`0")))
            throw new InvalidOperationException("rhp_source_sdk_entry_symbol " + symbol);
        return selected;
    }

    private static string MetadataName(INamedTypeSymbol type) => type.ContainingNamespace.ToDisplayString() + "." + type.MetadataName;

    private static string SyntaxIdentity(INamedTypeSymbol type) => type.ContainingNamespace.ToDisplayString()
        + ":" + type.Name + "`" + type.Arity;

    private static IEnumerable<CompilationUnitSyntax> WitnessRoots(CSharpSourceDocument document)
    {
        if (!document.Syntax.GetRoot().ContainsDirectives)
        {
            yield return (CompilationUnitSyntax)document.Syntax.GetRoot();
            yield break;
        }
        foreach (var parse in CSharpSourceProfiles.ForSource(document.Text, document.Path))
            yield return CSharpSyntaxTree.ParseText(document.Text, parse, document.Path).GetCompilationUnitRoot();
    }

    private static void RequireDocumentContext(CompilationUnitSyntax root, string space, string path)
    {
        if (root.GetDiagnostics().Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            || root.AttributeLists.Count != 0 || root.Members.Any(member => member is GlobalStatementSyntax)
            || root.DescendantNodes().OfType<UsingDirectiveSyntax>().Any(import => import.GlobalKeyword.IsKind(SyntaxKind.GlobalKeyword))
            || root.DescendantNodes().OfType<ExternAliasDirectiveSyntax>().Any())
            throw new InvalidOperationException("rhp_source_compilation_context " + path);
        // Inspect namespace declarations even in witnesses with no active type.
        // Independent namespace/type guards cannot hide a different owner.
        foreach (var declaration in root.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>())
        {
            string actual = Namespace(declaration.AncestorsAndSelf());
            if (space.Length == 0 || (actual != space && !space.StartsWith(actual + ".", StringComparison.Ordinal)))
                throw new InvalidOperationException("rhp_source_namespace " + path);
        }
    }

    private static string Identity(MemberDeclarationSyntax declaration) => Namespace(declaration.Ancestors()) + ":"
        + string.Join("+", declaration.AncestorsAndSelf().OfType<MemberDeclarationSyntax>().Where(RepositoryCodeStyle.IsType)
            .Reverse().Select(type => RepositoryCodeStyle.TypeName(type) + "`" + (type switch
            {
                TypeDeclarationSyntax value => value.TypeParameterList?.Parameters.Count ?? 0,
                DelegateDeclarationSyntax value => value.TypeParameterList?.Parameters.Count ?? 0,
                _ => 0
            })));

    private static string Namespace(IEnumerable<SyntaxNode> ancestors) => string.Join(".", ancestors.OfType<BaseNamespaceDeclarationSyntax>()
        .Reverse().SelectMany(value => value.Name.DescendantTokens().Where(token => token.IsKind(SyntaxKind.IdentifierToken))
            .Select(token => token.ValueText)));

    private static IEnumerable<string> MemberNames(MemberDeclarationSyntax member) => member switch
    {
        FieldDeclarationSyntax field => field.Declaration.Variables.Select(variable => variable.Identifier.ValueText),
        MethodDeclarationSyntax method => [method.Identifier.ValueText],
        PropertyDeclarationSyntax property => [property.Identifier.ValueText],
        _ => throw new InvalidOperationException("rhp_source_core_member_kind " + member.Kind())
    };

    private static int MemberStart(MemberDeclarationSyntax member, string name) => member is FieldDeclarationSyntax field
        ? field.Declaration.Variables.Single(variable => variable.Identifier.ValueText == name).SpanStart : member.SpanStart;

    private static MemberDeclarationSyntax MemberDeclaration(SyntaxNode node) => node is VariableDeclaratorSyntax variable
        ? (FieldDeclarationSyntax)variable.Parent!.Parent! : (MemberDeclarationSyntax)node;

    private static IEnumerable<(int Kind, string Text)> Header(MemberDeclarationSyntax member) => (member switch
    {
        MethodDeclarationSyntax method => (MemberDeclarationSyntax)method.WithBody(null).WithExpressionBody(null)
            .WithSemicolonToken(default),
        PropertyDeclarationSyntax property => property.WithAccessorList(null).WithExpressionBody(null)
            .WithInitializer(null).WithSemicolonToken(default),
        FieldDeclarationSyntax field => field,
        _ => throw new InvalidOperationException("rhp_source_core_header_kind " + member.Kind())
    }).DescendantTokens().Select(token => (token.RawKind, token.Text));
}
