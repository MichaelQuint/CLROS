using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Kernel.Diagnostics;
using OS.Tooling;
using System.Security.Cryptography;
using System.Text;

/// <summary>Shared source authority checks for the bounded fault fixtures.
/// Callers supply independent logical families and expected operations; discovery
/// does not admit new production inputs or execute an exception transition.</summary>
internal static class HostEfiFaultSources
{
    internal static IReadOnlyDictionary<string, CSharpSourceDocument> PinnedFamily(
        string root, CSharpSourceCatalog source, params Type[] owners)
    {
        string directory = Path.Combine(root, "src", "OS");
        var selected = new Dictionary<string, CSharpSourceDocument>(StringComparer.Ordinal);
        foreach (var document in HostSourceText.DocumentSources(source, owners.Select(owner => owner.FullName!).ToArray()))
        {
            string include = Path.GetRelativePath(directory, document.Path).Replace('/', '\\');
            bool family = EfiContracts.SourceFamilyIdentities.TryGetValue(include, out string? familyPin);
            bool restricted = EfiContracts.SourceIdentities.TryGetValue(include, out string? restrictedPin);
            Require(family || restricted, "missing reviewed family identity " + include);
            if (family && restricted)
                Require(familyPin == restrictedPin, "conflicting reviewed identities " + include);
            Require(Hash(document.Text.Replace("\r\n", "\n", StringComparison.Ordinal)) == (familyPin ?? restrictedPin),
                "frozen LF source " + include);
            selected.Add(include, document);
        }
        Require(selected.Count != 0, "empty reviewed family");
        return selected;
    }

    internal static MethodDeclarationSyntax StageOneCaller(CSharpSourceCatalog source, MethodDeclarationSyntax target)
    {
        var symbol = source.Compilation.GetSemanticModel(target.SyntaxTree).GetDeclaredSymbol(target)!;
        var call = HostSourceOperations.CallsTo(source, symbol).Single();
        var method = call.Ancestors().OfType<MethodDeclarationSyntax>().First();
        var caller = source.Compilation.GetSemanticModel(method.SyntaxTree).GetDeclaredSymbol(method)!;
        Require(SymbolEqualityComparer.Default.Equals(caller.ContainingType,
                source.Type("OS.Boot.EfiEntry+M01StageOneAdapter"))
            && caller.IsStatic && caller.Arity == 0 && caller.ReturnsVoid
            && caller.DeclaredAccessibility == Accessibility.Private, "fault stage-one caller");
        _ = source.MethodInvocations(method);
        return method;
    }

    internal static string FalseAuthority(CSharpSourceCatalog source, string fieldName)
    {
        var field = source.Type(typeof(Probes).FullName!).GetMembers(fieldName).OfType<IFieldSymbol>().Single();
        Require(field is { IsConst: true, DeclaredAccessibility: Accessibility.Public, ConstantValue: false }
            && field.Type.SpecialType == SpecialType.System_Boolean, "unique false authority");
        int declarations = 0;
        foreach (var document in HostSourceText.DocumentSources(source, typeof(Probes).FullName!))
        {
            var root = document.Syntax.GetRoot();
            string allBranches = root.ReplaceTrivia(root.DescendantTrivia(descendIntoTrivia: true)
                .Where(trivia => trivia.IsDirective), (trivia, _) => SyntaxFactory.Whitespace(new string(
                    trivia.ToFullString().Select(character => character is '\r' or '\n' ? character : ' ').ToArray())))
                .ToFullString();
            declarations += SyntaxFactory.ParseCompilationUnit(allBranches).DescendantNodes().OfType<FieldDeclarationSyntax>()
                .SelectMany(declaration => declaration.Declaration.Variables)
                .Count(variable => variable.Identifier.ValueText == fieldName);
        }
        Require(declarations == 1, "single raw authority across partial and inactive sources");
        return field.DeclaringSyntaxReferences.Single().GetSyntax().AncestorsAndSelf()
            .OfType<FieldDeclarationSyntax>().Single().ToString();
    }

    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    private static void Require(bool condition, string reason)
    {
        if (!condition)
            throw new InvalidOperationException(reason);
    }
}
