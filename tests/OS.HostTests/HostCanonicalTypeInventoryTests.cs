using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using OS.Tooling;

internal static class HostCanonicalTypeInventoryTests
{
    [HostTest("canonical type inventory follows active metadata identities across partial files")]
    private static void ActiveIdentity()
    {
        var options = new CSharpParseOptions(LanguageVersion.CSharp14, preprocessorSymbols: ["ENABLED"]);
        string[] sources =
        [
            "namespace System.Moved; public partial class Item { public struct Child<T,U> {} } public class Item<T> {}",
            "namespace System.Moved { public partial class Item {} public delegate void Callback(); }",
            "#if ENABLED\nnamespace System.Moved; public enum State { Ready }\n#else\nclass Inactive {}\n#endif",
            "namespace Other; class Decoy { const string Text = \"namespace System; class Fake {}\"; }"
        ];
        var documents = sources.Select((source, index) => new CSharpSourceDocument("moved/" + index + ".cs", "synthetic",
            CSharpSyntaxTree.ParseText(source, options))).ToArray();
        var references = HostSourceCatalog.Project("src/OS.Kernel/OS.Kernel.csproj").Compilation.References;
        var catalog = new CSharpSourceCatalog(documents, references);
        HostCheck.True(HostCanonicalTypeInventory.Read(catalog, documents).Keys.Order(StringComparer.Ordinal).SequenceEqual(
            new[] { "System.Moved.Callback", "System.Moved.Item", "System.Moved.Item+Child`2", "System.Moved.Item`1", "System.Moved.State" }));
        HostCheck.Equal(0, HostCanonicalTypeInventory.Read(catalog, [documents[3]]).Count);
    }

    [HostTest("runtime fail-fast source guard rejects conditional dormant and shadow calls")]
    private static void FailFastBinding()
    {
        bool Check(string body)
        {
            var catalog = HostSemanticFingerprintTests.Catalog(new Dictionary<string, string>
            {
                ["boundary.cs"] = "namespace System { static class RuntimeFailFast { public static void Halt() {} } }",
                ["owner.cs"] = "using Stop = System.RuntimeFailFast; static class Owner { static void Other() {} static void Run() " + body + " }"
            });
            return DotNetN10SystemExportCompatibilityLedgerImplementedStubbedTests.FailsFast(catalog, catalog.Method("Owner", "Run"));
        }
        HostCheck.True(Check("{ Stop.Halt(); }"));
        foreach (string body in new[] { "{ if (false) Stop.Halt(); }", "{ void Dormant() { Stop.Halt(); } }",
            "{ Other(); Stop.Halt(); }", "{ Stop.Halt(); Other(); }", "{ Other(); }", "{ }" })
            HostCheck.True(!Check(body), body);
    }
}
