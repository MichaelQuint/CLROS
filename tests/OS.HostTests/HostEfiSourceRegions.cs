using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

/// <summary>Direct-owner character regions in logical EFI inspection text.
/// Regions target the named EfiEntry owner, never a same-named nested decoy.
/// Text can be a deliberately malformed mutation; missing bodies fail selection.</summary>
internal static class HostEfiSourceRegions
{
    internal static TextSpan? TypeBody(string source, string typeName)
    {
        var root = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.CSharp14)).GetCompilationUnitRoot();
        var owner = root.Members.OfType<ClassDeclarationSyntax>().SingleOrDefault(type => type.Identifier.ValueText == "EfiEntry");
        if (owner is null)
            return null;
        if (typeName != "EfiEntry")
            owner = owner.Members.OfType<ClassDeclarationSyntax>().SingleOrDefault(type => type.Identifier.ValueText == typeName);
        return owner is null || owner.OpenBraceToken.IsMissing || owner.CloseBraceToken.IsMissing ? null
            : TextSpan.FromBounds(owner.OpenBraceToken.Span.End, owner.CloseBraceToken.SpanStart);
    }

    internal static TextSpan? MethodBody(string source, TextSpan scope, string name)
    {
        const string prefix = "class InspectionOwner {\n";
        if (scope.End > source.Length)
            throw new InvalidOperationException("inspection_method_scope");
        var root = CSharpSyntaxTree.ParseText(prefix + source.Substring(scope.Start, scope.Length) + "\n}",
            new CSharpParseOptions(LanguageVersion.CSharp14)).GetCompilationUnitRoot();
        var owner = root.Members.OfType<ClassDeclarationSyntax>().Single();
        var method = owner.Members.OfType<MethodDeclarationSyntax>()
            .Where(method => method.Identifier.ValueText == name && method.Body is not null).SingleOrDefault();
        if (method?.Body is not { } body || body.OpenBraceToken.IsMissing || body.CloseBraceToken.IsMissing)
            return null;
        int start = scope.Start + body.OpenBraceToken.Span.End - prefix.Length;
        int end = scope.Start + body.CloseBraceToken.SpanStart - prefix.Length;
        return start >= scope.Start && end <= scope.End ? TextSpan.FromBounds(start, end) : null;
    }
}
