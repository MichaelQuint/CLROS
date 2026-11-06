using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Tooling;

internal static unsafe partial class M02SafeStubPageEfiEntryTests
{
    private const string SafeStubOwner = "OS.Boot.EfiEntry+M01StageThreeAdapter";
    private static CSharpSourceCatalog SafeStubSources() => HostSourceCatalog.Efi(
        EfiOptions.Parse(EfiProfileTestControls.ReturningArguments("M02SafeStub"), true).Profile());

    private static MethodDeclarationSyntax SafeStubMethod(CSharpSourceCatalog source, string name, SpecialType returns)
    {
        var method = source.Method(SafeStubOwner, name, symbol => symbol.IsStatic && symbol.Arity == 0
            && !symbol.ReturnsByRef && !symbol.ReturnsByRefReadonly
            && symbol.DeclaredAccessibility == Accessibility.Private && symbol.ReturnType.SpecialType == returns);
        _ = source.MethodInvocations(method);
        return method;
    }

    private static MethodDeclarationSyntax SafeStubCaller(CSharpSourceCatalog source)
    {
        _ = SafeStubMethod(source, "PrepareM02SafeStubPage", SpecialType.System_Boolean);
        var writer = SafeStubMethod(source, "WriteSuccessLine", SpecialType.System_Void);
        var target = source.Compilation.GetSemanticModel(writer.SyntaxTree).GetDeclaredSymbol(writer)!;
        var call = HostSourceOperations.CallsTo(source, SafeStubOwner, "PrepareM02SafeStubPage").Single();
        var method = call.Ancestors().OfType<MethodDeclarationSyntax>().First();
        var symbol = source.Compilation.GetSemanticModel(method.SyntaxTree).GetDeclaredSymbol(method)!;
        HostCheck.True(SymbolEqualityComparer.Default.Equals(symbol.ContainingType, source.Type(SafeStubOwner))
            && symbol.IsStatic && symbol.Arity == 0 && !symbol.ReturnsByRef && !symbol.ReturnsByRefReadonly
            && symbol.ReturnType.SpecialType == SpecialType.System_Boolean
            && symbol.DeclaredAccessibility == Accessibility.Public, "original stage-three prepare caller");
        HostCheck.True(source.MethodInvocations(method).Any(invocation => SymbolEqualityComparer.Default.Equals(invocation.TargetMethod, target)),
            "stage-three caller uses the selected success writer");
        return method;
    }

    private static bool SafeStubOrder(string source, params string[] fragments)
    {
        int previous = -1;
        foreach (string fragment in fragments)
        {
            int next = HostSourceText.IndexOf(source, fragment, previous + 1);
            if (next < 0)
                return false;
            previous = next;
        }
        return true;
    }

    [HostTest("m02.017 bound discovery follows moved partial methods and rejects foreign or ambiguous callers")]
    private static void SafeStubDiscoveryControls()
    {
        const string prefix = "namespace OS.Boot; public static partial class EfiEntry { private partial class M01StageThreeAdapter { ";
        const string suffix = " } }";
        const string targets = "private static bool PrepareM02SafeStubPage() => true; private static void WriteSuccessLine() { }";
        const string call = "_ = PrepareM02SafeStubPage(); WriteSuccessLine();";
        var parse = new CSharpParseOptions(LanguageVersion.CSharp14);
        var references = new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) };
        foreach (var (caller, expected) in new[]
        {
            ("public static bool Moved() { " + call + " return true; }", true),
            ("private static bool Moved() { " + call + " return true; }", false),
            ("public static bool Moved<T>() { " + call + " return true; }", false),
            ("public static bool Moved() { " + call + call + " return true; }", false),
            ("public static bool Moved() { _ = PrepareM02SafeStubPage(); return true; }", false),
            ("private static class Foreign { public static bool Moved() { " + call + " return true; } }", false)
        })
        {
            var source = new CSharpSourceCatalog([
                CSharpSourceDocument.FromBytes("targets.cs", System.Text.Encoding.UTF8.GetBytes(prefix + targets + suffix), parse),
                CSharpSourceDocument.FromBytes("arbitrary-caller.cs", System.Text.Encoding.UTF8.GetBytes(prefix + caller + suffix), parse)
            ], references);
            HostCheck.True(!source.Compilation.GetDiagnostics().Any(item => item.Severity == DiagnosticSeverity.Error), "safe-stub control binds");
            bool accepted;
            try
            {
                _ = SafeStubCaller(source);
                accepted = true;
            }
            catch (InvalidOperationException)
            {
                accepted = false;
            }
            HostCheck.True(accepted == expected, "safe-stub caller ownership, uniqueness and writer binding");
        }
        HostCheck.True(SafeStubOrder("Alpha ( );\n Beta();\n Gamma();", "Alpha();", "Beta();", "Gamma();"), "whitespace-independent sequence");
        HostCheck.True(!SafeStubOrder("Beta(); Alpha(); Gamma();", "Alpha();", "Beta();", "Gamma();"), "reordered steps reject");
        HostCheck.True(!SafeStubOrder("Alpha(); Gamma();", "Alpha();", "Beta();", "Gamma();"), "missing step rejects");
    }
}
