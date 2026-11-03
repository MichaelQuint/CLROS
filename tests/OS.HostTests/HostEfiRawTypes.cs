using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using OS.Tooling;

/// <summary>Raw type declaration spans across conditional parts, including
/// comments and inactive member text. Leading/trailing trivia outside the type
/// is excluded so an unrelated adapter in a shared file cannot supply its policy.</summary>
internal static class HostEfiRawTypes
{
    internal static string Declarations(CSharpSourceCatalog source, string owner) => Extract(source, owner, static text => text, bodiesOnly: false);

    internal static string Bodies(CSharpSourceCatalog source, string owner, Func<string, string> selectKnownBranches) =>
        Extract(source, owner, selectKnownBranches, bodiesOnly: true);

    internal static string? MethodBody(string ownerBody, string methodName)
    {
        const string prefix = "class InspectionOwner {\n";
        string text = prefix + ownerBody + "\n}";
        var methods = new Dictionary<int, TextSpan>();
        foreach (var profile in CSharpSourceProfiles.ForSource(text, "RawInspection.cs"))
        {
            var syntax = CSharpSyntaxTree.ParseText(text, profile);
            if (syntax.GetDiagnostics().Any(diagnostic => diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error
                && diagnostic.Id != "CS1029"))
                throw new InvalidOperationException("raw_method_conditional_syntax " + methodName);
            var owner = syntax.GetCompilationUnitRoot().Members.OfType<ClassDeclarationSyntax>().Single();
            foreach (var method in owner.Members.OfType<MethodDeclarationSyntax>()
                .Where(method => method.Identifier.ValueText == methodName))
            {
                if (method.Body is not { } body)
                    throw new InvalidOperationException("raw_method_body_unsupported " + methodName);
                var span = TextSpan.FromBounds(body.OpenBraceToken.Span.End, body.CloseBraceToken.SpanStart);
                if (methods.TryGetValue(method.Identifier.SpanStart, out var previous) && previous != span)
                    throw new InvalidOperationException("raw_method_conditional_header_requires_review " + methodName);
                methods[method.Identifier.SpanStart] = span;
            }
        }
        if (methods.Count == 0)
            return null;
        if (methods.Count != 1)
            throw new InvalidOperationException("raw_method_ambiguous " + methodName);
        var selected = methods.Values.Single();
        return text[selected.Start..selected.End];
    }

    private static string Extract(CSharpSourceCatalog source, string owner, Func<string, string> select, bool bodiesOnly)
    {
        var expected = source.Type(owner).DeclaringSyntaxReferences.Select(reference => reference.GetSyntax())
            .OfType<MemberDeclarationSyntax>().Select(RepositoryCodeStyle.TypeIdentity).Distinct(StringComparer.Ordinal).Single();
        var parts = new List<string>();
        foreach (var document in HostSourceText.DocumentSources(source, owner))
        {
            string text = select(document.Text);
            var seen = new HashSet<(int Start, int End)>();
            var initial = CSharpSyntaxTree.ParseText(text, (CSharpParseOptions)document.Syntax.Options, document.Path);
            var profiles = initial.GetRoot().ContainsDirectives
                ? CSharpSourceProfiles.ForSource(text, document.Path) : [(CSharpParseOptions)document.Syntax.Options];
            foreach (var profile in profiles)
            {
                var syntax = CSharpSyntaxTree.ParseText(text, profile, document.Path);
                if (syntax.GetDiagnostics().Any(diagnostic => diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error
                    && diagnostic.Id != "CS1029"))
                    throw new InvalidOperationException("raw_type_conditional_syntax " + document.Path);
                foreach (var declaration in syntax.GetRoot().DescendantNodes().OfType<MemberDeclarationSyntax>()
                    .Where(RepositoryCodeStyle.IsType).Where(declaration => RepositoryCodeStyle.TypeIdentity(declaration) == expected))
                {
                    if (!bodiesOnly)
                        seen.Add((declaration.SpanStart, declaration.Span.End));
                    else if (declaration is TypeDeclarationSyntax type && !type.OpenBraceToken.IsMissing && !type.CloseBraceToken.IsMissing)
                    {
                        seen.Add((type.OpenBraceToken.Span.End, type.CloseBraceToken.SpanStart));
                    }
                    else
                        throw new InvalidOperationException("raw_type_body_unsupported " + owner);
                }
            }
            TextSpan? pending = null;
            foreach (var span in seen.Select(pair => TextSpan.FromBounds(pair.Start, pair.End))
                .OrderBy(span => span.Start).ThenBy(span => span.End))
            {
                if (pending is { } previous && span.Start < previous.End)
                {
                    if (bodiesOnly && span != previous)
                        throw new InvalidOperationException("raw_type_conditional_header_requires_review " + document.Path);
                    pending = TextSpan.FromBounds(previous.Start, Math.Max(previous.End, span.End));
                }
                else
                {
                    if (pending is { } completed)
                        parts.Add(text[completed.Start..completed.End]);
                    pending = span;
                }
            }
            if (pending is { } last)
                parts.Add(text[last.Start..last.End]);
        }
        if (parts.Count == 0)
            throw new InvalidOperationException("raw_type_family_missing " + owner);
        return string.Join("\n", parts);
    }
}
