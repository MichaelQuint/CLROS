using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Tooling;

/// <summary>Finds one private EFI adapter by compiler identity. The host fixtures
/// may expose named private seams in their isolated SDK harness; original member
/// active member tokens, attributes and compiler declaration order remain.
/// Raw views retain inactive code. The generated host input freezes the selected
/// EFI branches so SDK symbols cannot select another body. Import-dependent
/// bindings and other directive contexts fail before projection.
/// This does not grant source admission or permission to execute native bytes.</summary>
internal static class HostEfiAdapterSources
{
    internal static IReadOnlyList<ClassDeclarationSyntax> Declarations(CSharpSourceCatalog source, string owner)
    {
        var type = source.Type(owner);
        if (type is not { IsStatic: true, Arity: 0, DeclaredAccessibility: Accessibility.Private, ContainingType: not null })
            throw new InvalidOperationException("efi_adapter_owner " + owner);
        var declarations = type.DeclaringSyntaxReferences.Select(reference => reference.GetSyntax()).ToArray();
        if (declarations.Any(declaration => declaration is not ClassDeclarationSyntax))
            throw new InvalidOperationException("efi_adapter_class " + owner);
        foreach (var declaration in declarations)
            if (source.Compilation.GetSemanticModel(declaration.SyntaxTree).GetDiagnostics(declaration.Span)
                .Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
                throw new InvalidOperationException("efi_adapter_binding " + owner);
        return source.Documents.SelectMany(document => declarations.OfType<ClassDeclarationSyntax>()
            .Where(declaration => declaration.SyntaxTree == document.Syntax).OrderBy(declaration => declaration.SpanStart)).ToArray();
    }

    internal static string Raw(CSharpSourceCatalog source, string owner) =>
        string.Join("\n", Declarations(source, owner).Select(declaration => declaration.ToString()));

    internal static string ForHost(CSharpSourceCatalog source, string owner, params string[] exposedMethods)
    {
        if (exposedMethods.Distinct(StringComparer.Ordinal).Count() != exposedMethods.Length)
            throw new InvalidOperationException("efi_adapter_duplicate_exposure");
        var methods = exposedMethods.Select(name => source.Method(owner, name,
            symbol => symbol.IsStatic && symbol.DeclaredAccessibility == Accessibility.Private)).ToArray();
        var declarations = Declarations(source, owner);
        RequireImportIndependent(source, declarations);
        foreach (var method in methods)
            if (!declarations.Any(declaration => IsDirectMember(method, declaration)))
                throw new InvalidOperationException("efi_adapter_foreign_exposure " + method.Identifier.ValueText);
        return string.Join("\n", declarations.Select(declaration =>
        {
            var selected = declaration.Members.OfType<MethodDeclarationSyntax>()
                .Where(member => methods.Any(method => method.SyntaxTree == member.SyntaxTree && method.Span == member.Span)).ToArray();
            var changed = declaration.ReplaceNodes(selected, (original, _) =>
                original.WithModifiers(Expose(original.Modifiers)));
            return SelectedHostCode(changed.WithModifiers(Expose(changed.Modifiers)));
        }));
    }

    private static bool IsDirectMember(MethodDeclarationSyntax method, ClassDeclarationSyntax declaration) =>
        method.SyntaxTree == declaration.SyntaxTree && method.Parent?.Span == declaration.Span;

    private static void RequireImportIndependent(CSharpSourceCatalog source, IReadOnlyList<ClassDeclarationSyntax> declarations)
    {
        // Implicit protocols need dedicated binding evidence. These adapters
        // use explicit calls; reject those other protocols at this boundary.
        if (declarations.SelectMany(declaration => declaration.DescendantNodes()).Any(node =>
            node is CommonForEachStatementSyntax or AwaitExpressionSyntax or QueryExpressionSyntax
                or CollectionExpressionSyntax or InterpolatedStringExpressionSyntax
                or AssignmentExpressionSyntax { Left: TupleExpressionSyntax or DeclarationExpressionSyntax }
                || node.IsKind(SyntaxKind.CollectionInitializerExpression)
                || node is UsingStatementSyntax statement && statement.AwaitKeyword.RawKind != 0
                || node is LocalDeclarationStatementSyntax local && local.AwaitKeyword.RawKind != 0))
            throw new InvalidOperationException("efi_adapter_implicit_protocol");
        var compilation = source.Compilation;
        var annotations = declarations.Select((_, index) => new SyntaxAnnotation("adapter", index.ToString())).ToArray();
        var selected = declarations.ToArray();
        foreach (var tree in source.Compilation.SyntaxTrees)
        {
            var root = tree.GetRoot();
            var positions = declarations.Select((declaration, index) => (declaration, index))
                .Where(item => item.declaration.SyntaxTree == tree).ToArray();
            var scopes = positions.SelectMany(item => item.declaration.Ancestors())
                .Where(node => node is CompilationUnitSyntax or BaseNamespaceDeclarationSyntax).ToArray();
            var imports = root.DescendantNodes().Where(node => node is UsingDirectiveSyntax import
                    && import.GlobalKeyword.IsKind(SyntaxKind.GlobalKeyword)
                || node is UsingDirectiveSyntax or ExternAliasDirectiveSyntax
                    && scopes.Any(scope => node.Parent is { } parent && scope.RawKind == parent.RawKind && scope.Span == parent.Span))
                .Select(node => node.Span).ToHashSet();
            if (imports.Count == 0)
                continue;
            root = root.ReplaceNodes(positions.Select(item => item.declaration), (original, changed) =>
                changed.WithAdditionalAnnotations(annotations[positions.Single(item => item.declaration == original).index]));
            root = root.RemoveNodes(root.DescendantNodes().Where(node => imports.Contains(node.Span)
                    && node is UsingDirectiveSyntax or ExternAliasDirectiveSyntax),
                SyntaxRemoveOptions.KeepExteriorTrivia)!;
            var replacement = tree.WithRootAndOptions(root, tree.Options);
            compilation = compilation.ReplaceSyntaxTree(tree, replacement);
            foreach (var item in positions)
                selected[item.index] = replacement.GetRoot().GetAnnotatedNodes(annotations[item.index]).OfType<ClassDeclarationSyntax>().Single();
        }
        for (int index = 0; index < declarations.Count; index++)
        {
            var original = declarations[index];
            var projected = selected[index];
            var before = source.Compilation.GetSemanticModel(original.SyntaxTree);
            var after = compilation.GetSemanticModel(projected.SyntaxTree);
            if (after.GetDiagnostics(projected.Span).Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
                throw new InvalidOperationException("efi_adapter_import_binding");
            var left = original.DescendantNodes().Where(node => node is ExpressionSyntax or AttributeSyntax).ToArray();
            var right = projected.DescendantNodes().Where(node => node is ExpressionSyntax or AttributeSyntax).ToArray();
            if (left.Length != right.Length)
                throw new InvalidOperationException("efi_adapter_import_shape");
            for (int item = 0; item < left.Length; item++)
            {
                if (left[item].RawKind != right[item].RawKind
                    || SymbolIdentity(before.GetSymbolInfo(left[item]).Symbol) != SymbolIdentity(after.GetSymbolInfo(right[item]).Symbol))
                    throw new InvalidOperationException("efi_adapter_import_symbol");
                if (left[item] is ExpressionSyntax first && right[item] is ExpressionSyntax second)
                {
                    var firstType = before.GetTypeInfo(first);
                    var secondType = after.GetTypeInfo(second);
                    if (SymbolIdentity(firstType.Type) != SymbolIdentity(secondType.Type)
                        || SymbolIdentity(firstType.ConvertedType) != SymbolIdentity(secondType.ConvertedType)
                        || SymbolIdentity(before.GetConversion(first).MethodSymbol) != SymbolIdentity(after.GetConversion(second).MethodSymbol)
                        || !before.GetConstantValue(first).Equals(after.GetConstantValue(second)))
                        throw new InvalidOperationException("efi_adapter_import_expression");
                }
            }
        }
    }

    private static string? SymbolIdentity(ISymbol? symbol) => symbol is null ? null
        : symbol.Kind + "|" + symbol.ContainingAssembly?.Identity + "|" + symbol.GetDocumentationCommentId()
            + "|" + symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);

    private static string SelectedHostCode(ClassDeclarationSyntax declaration)
    {
        var trivia = declaration.DescendantTrivia(descendIntoTrivia: true)
            .Where(item => declaration.Span.Contains(item.Span) && (item.IsDirective || item.IsKind(SyntaxKind.DisabledTextTrivia)))
            .ToArray();
        foreach (var item in trivia.Where(item => item.IsDirective))
            if (item.GetStructure() is not (IfDirectiveTriviaSyntax or ElifDirectiveTriviaSyntax
                or ElseDirectiveTriviaSyntax or EndIfDirectiveTriviaSyntax))
                throw new InvalidOperationException("efi_adapter_directive_context " + item.Kind());
        string text = declaration.ReplaceTrivia(trivia, (item, _) => SyntaxFactory.Whitespace(new string(
            item.ToFullString().Select(character => character is '\r' or '\n' ? character : ' ').ToArray()))).ToString();
        var projected = SyntaxFactory.ParseMemberDeclaration(text)
            ?? throw new InvalidOperationException("efi_adapter_projection_empty");
        if (projected is not ClassDeclarationSyntax
            || projected.GetDiagnostics().Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            || !declaration.DescendantTokens().Select(token => (token.RawKind, token.Text))
                .SequenceEqual(projected.DescendantTokens().Select(token => (token.RawKind, token.Text))))
            throw new InvalidOperationException("efi_adapter_projection_changed");
        return text;
    }

    private static SyntaxTokenList Expose(SyntaxTokenList modifiers)
    {
        var access = modifiers.Single(token => token.IsKind(SyntaxKind.PrivateKeyword));
        return modifiers.Replace(access, SyntaxFactory.Token(access.LeadingTrivia, SyntaxKind.InternalKeyword, access.TrailingTrivia));
    }
}
