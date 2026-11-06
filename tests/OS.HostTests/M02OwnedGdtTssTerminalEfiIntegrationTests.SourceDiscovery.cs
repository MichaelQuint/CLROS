using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Tooling;

internal static partial class M02OwnedGdtTssTerminalEfiIntegrationTests
{
    private static CSharpSourceCatalog TerminalSources() =>
        HostSourceCatalog.Efi(EfiOptions.Parse(TerminalArguments, true).Profile());

    private static MethodDeclarationSyntax TerminalMethod(CSharpSourceCatalog source,
        string owner, string name, SpecialType returns)
    {
        var method = source.Method(owner, name, symbol => symbol.IsStatic && symbol.Arity == 0
            && !symbol.ReturnsByRef && !symbol.ReturnsByRefReadonly
            && symbol.DeclaredAccessibility == Accessibility.Private && symbol.ReturnType.SpecialType == returns);
        _ = source.MethodInvocations(method);
        return method;
    }

    private static MethodDeclarationSyntax TerminalCaller(CSharpSourceCatalog source)
    {
        var call = HostSourceOperations.CallsTo(source, "OS.Boot.EfiEntry", "EnterM02OwnedGdtTssTerminalProbe").Single();
        var method = call.Ancestors().OfType<MethodDeclarationSyntax>().First();
        var symbol = source.Compilation.GetSemanticModel(method.SyntaxTree).GetDeclaredSymbol(method)!;
        HostCheck.True(SymbolEqualityComparer.Default.Equals(symbol.ContainingType,
            source.Type("OS.Boot.EfiEntry+M01StageOneAdapter")), "original stage-one terminal owner");
        _ = source.MethodInvocations(method);
        return method;
    }

    private static int LastTerminalToken(string source, string fragment)
    {
        int last = -1;
        int cursor = 0;
        while ((cursor = HostSourceText.IndexOf(source, fragment, cursor)) >= 0)
        {
            last = cursor;
            cursor++;
        }
        return last;
    }

    [HostTest("m02.009 terminal discovery follows partial files and rejects foreign or ambiguous callers")]
    private static void TerminalDiscoveryControls()
    {
        const string owner = "namespace OS.Boot; public static partial class EfiEntry { ";
        const string target = "private static void EnterM02OwnedGdtTssTerminalProbe() { }";
        const string call = "EnterM02OwnedGdtTssTerminalProbe();";
        var parse = new CSharpParseOptions(LanguageVersion.CSharp14);
        var references = new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) };
        foreach (var (caller, expected) in new[]
        {
            ("private partial class M01StageOneAdapter { private static void RenamedCaller() { " + call + " } }", true),
            ("private partial class M01StageOneAdapter { private static void RenamedCaller() { " + call + call + " } }", false),
            ("private partial class M01StageOneAdapter { private static class Foreign { private static void Call() { " + call + " } } }", false)
        })
        {
            var source = new CSharpSourceCatalog([
                CSharpSourceDocument.FromBytes("terminal.cs", System.Text.Encoding.UTF8.GetBytes(owner + target + " }"), parse),
                CSharpSourceDocument.FromBytes("arbitrary-caller.cs", System.Text.Encoding.UTF8.GetBytes(owner + caller + " }"), parse)
            ], references);
            HostCheck.True(!source.Compilation.GetDiagnostics().Any(item => item.Severity == DiagnosticSeverity.Error), "caller control binds");
            bool accepted;
            try
            {
                _ = TerminalMethod(source, "OS.Boot.EfiEntry", "EnterM02OwnedGdtTssTerminalProbe", SpecialType.System_Void);
                _ = TerminalCaller(source);
                accepted = true;
            }
            catch (InvalidOperationException)
            {
                accepted = false;
            }

            HostCheck.True(accepted == expected, "terminal caller ownership and uniqueness");
        }
    }
}
