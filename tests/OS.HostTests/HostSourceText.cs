using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Tooling;

/// <summary>Host-only source views and token/marker checks. Selected views use
/// the caller's compiler profile; original documents remain available for
/// exclusions that deliberately inspect inactive code and comments.</summary>
internal static class HostSourceText
{
    // Documents are immutable snapshots. Cache only discovered names and marker
    // text, not every parsed witness tree; a mutation creates a new document.
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<
        CSharpSourceDocument, Dictionary<string, HashSet<string>>> RawOwners = new();

    internal static string Active(SyntaxNode node) => node
        .ReplaceTrivia(node.DescendantTrivia(descendIntoTrivia: true), (_, _) => default)
        .NormalizeWhitespace().ToFullString();

    // These are inspection views, never compilation inputs. Keep original
    // documents for all-branch firewalls and selected declarations for code checks.
    internal static string Declarations(CSharpSourceCatalog source, params string[] owners) =>
        string.Join("\n", owners.SelectMany(owner => source.Type(owner).DeclaringSyntaxReferences)
            .DistinctBy(reference => (reference.SyntaxTree, reference.Span))
            .Select(reference => Active(reference.GetSyntax())));

    internal static string Documents(CSharpSourceCatalog source, params string[] owners) =>
        string.Join("\n", DocumentSources(source, owners).Select(document => document.Text));

    internal static IReadOnlyList<CSharpSourceDocument> DocumentSources(CSharpSourceCatalog source, params string[] owners)
    {
        var keys = owners.Select(owner => RawOwnerKey((MemberDeclarationSyntax)
            source.Type(owner).DeclaringSyntaxReferences[0].GetSyntax())).ToArray();
        return source.Documents.Where(document =>
        {
            var root = document.Syntax.GetRoot();
            // An already selected declaration proves inclusion of its entire
            // raw document. Do not reparse body switches merely to include it.
            if (root.DescendantNodes(node => node is CompilationUnitSyntax or BaseNamespaceDeclarationSyntax or TypeDeclarationSyntax)
                .OfType<MemberDeclarationSyntax>().Where(RepositoryCodeStyle.IsType)
                .Any(declaration => keys.Contains(RawOwnerKey(declaration), StringComparer.Ordinal)))
                return true;
            // Only conditional documents can hide an otherwise absent owner.
            return root.ContainsDirectives && keys.Any(RawDocumentOwners(document).ContainsKey);
        }).ToArray();
    }

    internal static string ByteCharacters(MethodDeclarationSyntax method) => string.Concat(
        method.DescendantNodes().OfType<CastExpressionSyntax>()
            .Where(cast => cast.Type is PredefinedTypeSyntax type && type.Keyword.IsKind(SyntaxKind.ByteKeyword)
                && cast.Expression is LiteralExpressionSyntax literal && literal.Token.Value is char)
            .Select(cast => (char)((LiteralExpressionSyntax)cast.Expression).Token.Value!));

    internal static string ByteCharacters(CSharpSourceCatalog source, string owner, string name)
    {
        var method = source.Method(owner, name);
        var model = source.Compilation.GetSemanticModel(method.SyntaxTree);
        var symbol = model.GetDeclaredSymbol(method)!;
        if (symbol.DeclaredAccessibility != Accessibility.Private || !symbol.IsStatic
            || symbol.Arity != 0 || !symbol.ReturnsVoid
            || model.GetDiagnostics(method.Span).Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
            throw new InvalidOperationException("marker_formatter_signature " + owner + "." + name);
        return ByteCharacters(method);
    }

    internal static bool HasByteFormatter(CSharpSourceCatalog source, string owner, string marker)
    {
        string key = RawOwnerKey((MemberDeclarationSyntax)source.Type(owner).DeclaringSyntaxReferences[0].GetSyntax());
        return source.Documents.Any(document => RawDocumentOwners(document).TryGetValue(key, out var markers)
            && markers.Contains(marker));
    }

    private static string RawOwnerKey(MemberDeclarationSyntax declaration)
    {
        string identity = RepositoryCodeStyle.TypeIdentity(declaration);
        // Raw exclusions conservatively reserve the containing name chain and
        // generic arity across namespaces. Conditional namespace combinations
        // must not remove protection. Active semantic checks retain exact owners.
        return identity[(identity.LastIndexOf(':') + 1)..];
    }

    private static Dictionary<string, HashSet<string>> RawDocumentOwners(CSharpSourceDocument document) =>
        RawOwners.GetValue(document, static item =>
        {
            var owners = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            var physical = new Dictionary<(string Owner, int Method), SortedDictionary<int, char>>();
            IEnumerable<SyntaxTree> trees = item.Syntax.GetRoot().ContainsDirectives
                ? CSharpSourceProfiles.ForSource(item.Text, item.Path).Select(options =>
                    CSharpSyntaxTree.ParseText(item.Text, options, item.Path))
                : [item.Syntax];
            foreach (var syntax in trees)
            {
                if (syntax.GetDiagnostics().Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error
                    && diagnostic.Id != "CS1029"))
                    throw new InvalidOperationException("marker_formatter_source_syntax " + item.Path);
                foreach (var declaration in syntax.GetRoot().DescendantNodes().OfType<MemberDeclarationSyntax>()
                    .Where(RepositoryCodeStyle.IsType))
                {
                    string owner = RawOwnerKey(declaration);
                    if (!owners.TryGetValue(owner, out var markers))
                        owners.Add(owner, markers = new(StringComparer.Ordinal));
                    foreach (var method in declaration.DescendantNodes().OfType<MethodDeclarationSyntax>())
                    {
                        if (!method.Modifiers.Any(SyntaxKind.PrivateKeyword) || !method.Modifiers.Any(SyntaxKind.StaticKeyword)
                            || method.TypeParameterList is not null
                            || method.ReturnType is not PredefinedTypeSyntax type || !type.Keyword.IsKind(SyntaxKind.VoidKeyword))
                            continue;
                        markers.Add(ByteCharacters(method));
                        var key = (owner, method.Identifier.SpanStart);
                        if (!physical.TryGetValue(key, out var casts))
                            physical.Add(key, casts = []);
                        foreach (var cast in method.DescendantNodes().OfType<CastExpressionSyntax>())
                            if (cast.Type is PredefinedTypeSyntax castType && castType.Keyword.IsKind(SyntaxKind.ByteKeyword)
                                && cast.Expression is LiteralExpressionSyntax literal && literal.Token.Value is char value)
                                casts[cast.SpanStart] = value;
                    }
                }
            }
            // Witnesses cover each cast, not every body-switch combination.
            // Also retain the conservative union for each physical method; this
            // is an exclusion view, never an executable profile or input source.
            foreach (var (key, casts) in physical)
                owners[key.Owner].Add(string.Concat(casts.Values));
            return owners;
        });

    internal static bool Contains(string source, string fragment) => IndexOf(source, fragment) >= 0;

    internal static int IndexOf(string source, string fragment, int start = 0) =>
        IndexOf(Tokens(source), Tokens(fragment), start);

    internal static int Count(string source, string fragment)
    {
        var tokens = Tokens(source);
        var expected = Tokens(fragment);
        int count = 0;
        int cursor = 0;
        while ((cursor = IndexOf(tokens, expected, cursor)) >= 0)
        {
            count++;
            cursor += expected.Length;
        }
        return count;
    }

    private static SyntaxToken[] Tokens(string source) => SyntaxFactory.ParseTokens(source)
        .Where(token => !token.IsKind(SyntaxKind.EndOfFileToken)).ToArray();

    private static int IndexOf(SyntaxToken[] tokens, SyntaxToken[] expected, int start)
    {
        if (expected.Length == 0)
            throw new ArgumentException("source_fragment_empty");
        if (start < 0)
            return -1;
        for (int index = start; index <= tokens.Length - expected.Length; index++)
        {
            int offset = 0;
            while (offset < expected.Length && tokens[index + offset].RawKind == expected[offset].RawKind
                && tokens[index + offset].Text == expected[offset].Text)
                offset++;
            if (offset == expected.Length)
                return index;
        }
        return -1;
    }
}
