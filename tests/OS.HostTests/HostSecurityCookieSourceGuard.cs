using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Tooling;

/// <summary>Checks the selected source owner of the no-CRT cookie storage
/// export. This is compiler-bound source inspection, not emitted-symbol proof.</summary>
internal static class HostSecurityCookieSourceGuard
{
    internal static bool Check(CSharpSourceCatalog source, string? owner, string? name = null, ulong value = 0)
    {
        var exports = HostRuntimeExports.Read(source);
        if (owner is null)
            return !exports.ContainsKey("__security_cookie");
        if (!exports.TryGetValue("__security_cookie", out var method)
            || !SymbolEqualityComparer.Default.Equals(method.ContainingType, source.Type(owner))
            || method.Name != name || !method.IsStatic || method.DeclaredAccessibility != Accessibility.Private
            || method.Arity != 0 || method.Parameters.Length != 0 || method.ReturnType.SpecialType != SpecialType.System_UInt64)
            return false;
        var declaration = source.Method(owner, method.Name);
        var model = source.Compilation.GetSemanticModel(declaration.SyntaxTree);
        if (model.GetDiagnostics(declaration.Span).Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
            return false;
        ExpressionSyntax? returned = declaration.ExpressionBody?.Expression
            ?? (declaration.Body is { Statements.Count: 1 } body && body.Statements[0] is ReturnStatementSyntax statement
                ? statement.Expression : null);
        return returned is not null && model.GetConstantValue(returned)
            is { HasValue: true, Value: ulong constant } && constant == value;
    }
}
