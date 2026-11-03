using Microsoft.CodeAnalysis.CSharp;
using OS.Tooling;

internal static class HostRuntimeExportsTests
{
    [HostTest("runtime export inventory follows bound attributes across files and conditional declarations")]
    private static void BoundAttributes()
    {
        const string attribute = """
            namespace System.Runtime;
            class RuntimeExportAttribute(string name) : System.Attribute { }
            """;
        const string first = """
            using Export = System.Runtime.RuntimeExportAttribute;
            namespace Moved;
            static partial class Owner { [Export("First")] static void A() {} }
            """;
        const string second = """
            namespace Moved;
            static partial class Owner
            {
            #if ENABLED
                [System.Runtime.RuntimeExport(nameof(Second))] static void Second() {}
            #endif
                [Other.RuntimeExport("Ignored")] static void C() {}
            }
            """;
        const string shadow = """
            namespace Other;
            class RuntimeExportAttribute(string name) : System.Attribute { }
            """;
        var references = HostSourceCatalog.Project("src/OS.Kernel/OS.Kernel.csproj").Compilation.References;
        IReadOnlyDictionary<string, Microsoft.CodeAnalysis.IMethodSymbol> Read(string a, bool enabled)
        {
            var options = new CSharpParseOptions(LanguageVersion.CSharp14, preprocessorSymbols: enabled ? ["ENABLED"] : []);
            var documents = new[] { attribute, a, second, shadow }.Select((text, index) =>
                new CSharpSourceDocument("parts/" + index + ".cs", "synthetic", CSharpSyntaxTree.ParseText(text, options)));
            return HostRuntimeExports.Read(new CSharpSourceCatalog(documents, references));
        }
        HostCheck.True(Read(first, false).Keys.SequenceEqual(["First"]));
        var enabled = Read(first, true);
        HostCheck.True(enabled.Keys.Order(StringComparer.Ordinal).SequenceEqual(["First", "Second"]));
        HostCheck.Equal("Moved.Owner", enabled["First"].ContainingType.ToDisplayString());
        foreach (string mutation in new[]
        {
            first.Replace("\"First\"", "\"Second\"", StringComparison.Ordinal),
            first.Replace("\"First\"", "\"\"", StringComparison.Ordinal),
            first.Replace("static partial class Owner", "class InvalidOwner", StringComparison.Ordinal)
                .Replace("static void A", "void A", StringComparison.Ordinal)
        })
        {
            try
            {
                _ = Read(mutation, true);
            }
            catch (InvalidOperationException error) when (error.Message.StartsWith("runtime_export_", StringComparison.Ordinal)) { continue; }
            throw new InvalidOperationException("runtime export mutation was accepted");
        }
    }
}
