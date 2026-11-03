using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using OS.Tooling;

internal static class HostSourceOperationsTests
{
    [HostTest("bound callers include nested constructors accessors and local functions across partial files")]
    private static void NestedCallers()
    {
        const string first = """
            public partial class Owner
            {
                private static void Target() { }
                private static void Direct() { Target(); Foreign.Target(); }
            #if UNSELECTED
                private static void Inactive() { Target(); }
            #endif
            }
            public class Foreign { public static void Target() { } }
            """;
        const string second = """
            public partial class Owner
            {
                private class Nested
                {
                    public Nested() { Target(); }
                    public int Value { get { Target(); return 0; } }
                    public void Container() { void Local() { Target(); } Local(); }
                }
            }
            """;
        foreach (string suffix in new[] { ".cs", ".renamed.cs" })
        {
            var options = new CSharpParseOptions(LanguageVersion.CSharp14);
            var source = new CSharpSourceCatalog(new[]
            {
                CSharpSourceDocument.FromBytes("owner" + suffix, System.Text.Encoding.UTF8.GetBytes(first), options),
                CSharpSourceDocument.FromBytes("nested" + suffix, System.Text.Encoding.UTF8.GetBytes(second), options)
            }, [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
            var declaration = source.Method("Owner", "Target");
            var target = source.Compilation.GetSemanticModel(declaration.SyntaxTree).GetDeclaredSymbol(declaration)!;
            HostCheck.Equal(4, HostSourceOperations.CallsTo(source, target).Length);
            HostCheck.Equal(4, HostSourceOperations.CallsTo(source, "Owner", "Target").Length);
            HostCheck.True(!source.Compilation.GetDiagnostics().Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        }
    }
}
