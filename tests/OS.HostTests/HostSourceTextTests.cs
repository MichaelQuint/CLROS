using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis;
using OS.Tooling;

internal static class HostSourceTextTests
{
    [HostTest("raw exclusions survive conditional namespaces enum delegates and same-path source replacement")]
    private static void RawSnapshotOwnership()
    {
        var options = new CSharpParseOptions(LanguageVersion.CSharp14, preprocessorSymbols: ["A", "B", "C"]);
        var anchor = CSharpSourceDocument.FromBytes("anchor.cs", System.Text.Encoding.UTF8.GetBytes("""
            namespace One.Two.Three
            {
                partial class Owner { }
                enum State { Ready }
                delegate void Callback();
            }
            """), options);
        var conditional = CSharpSourceDocument.FromBytes("conditional.cs", System.Text.Encoding.UTF8.GetBytes("""
            #if A
            namespace One {
            #endif
            #if B
            namespace Two {
            #endif
            #if C
            namespace Three {
            #endif
            partial class Owner
            {
                private static void Writer() { byte value = (byte)'X'; }
                private const string Forbidden = "namespace-conditional-raw-marker";
            }
            #if C
            }
            #endif
            #if B
            }
            #endif
            #if A
            }
            #endif
            """), options);
        var references = new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) };
        var source = new CSharpSourceCatalog([anchor, conditional], references);
        HostCheck.True(!source.Compilation.GetDiagnostics().Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        HostCheck.True(HostSourceText.HasByteFormatter(source, "One.Two.Three.Owner", "X"));
        HostCheck.True(HostSourceText.Documents(source, "One.Two.Three.Owner").Contains("namespace-conditional-raw-marker"));
        HostCheck.True(HostSourceText.Documents(source, "One.Two.Three.State").Contains("enum State"));
        HostCheck.True(HostSourceText.Documents(source, "One.Two.Three.Callback").Contains("delegate void Callback"));

        var replacement = CSharpSourceDocument.FromBytes(conditional.Path,
            System.Text.Encoding.UTF8.GetBytes(conditional.Text.Replace("(byte)'X'", "(byte)'Y'", StringComparison.Ordinal)), options);
        var changed = new CSharpSourceCatalog([anchor, replacement], references);
        HostCheck.True(HostSourceText.HasByteFormatter(changed, "One.Two.Three.Owner", "Y"));
        HostCheck.True(!HostSourceText.HasByteFormatter(changed, "One.Two.Three.Owner", "X"), "new snapshot must not reuse old discovery");
        HostCheck.True(HostSourceText.HasByteFormatter(source, "One.Two.Three.Owner", "X"), "original snapshot stays intact");
    }

    [HostTest("marker extraction retains private static nongeneric void ownership")]
    private static void MarkerSignature()
    {
        foreach (var (signature, tail, accepted) in new (string, string, bool)[]
        {
            ("private static void Marker()", "", true),
            ("private static void Marker(int unused)", "", true),
            ("public static void Marker()", "", false),
            ("private void Marker()", "", false),
            ("private static int Marker()", "return 0;", false),
            ("private static void Marker<T>()", "", false)
        })
        {
            var source = new CSharpSourceCatalog(
            [
                CSharpSourceDocument.FromBytes("marker.cs", System.Text.Encoding.UTF8.GetBytes(
                    "class Owner { " + signature + " { byte value = (byte)'A'; " + tail + " } }"),
                    new CSharpParseOptions(LanguageVersion.CSharp14))
            ], [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
            HostCheck.True(!source.Compilation.GetDiagnostics().Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error),
                "signature mutation must bind");
            bool succeeded;
            try
            {
                succeeded = HostSourceText.ByteCharacters(source, "Owner", "Marker") == "A";
            }
            catch (InvalidOperationException error) when (error.Message.StartsWith("marker_formatter_signature ", StringComparison.Ordinal))
            {
                succeeded = false;
            }
            HostCheck.Equal(accepted, succeeded, signature);
        }
    }

    [HostTest("type inspection includes partial documents and keeps raw exclusions separate from selected code")]
    private static void PartialDocuments()
    {
        var options = new CSharpParseOptions(LanguageVersion.CSharp14);
        var source = new CSharpSourceCatalog(new[]
        {
            CSharpSourceDocument.FromBytes("one.cs", System.Text.Encoding.UTF8.GetBytes(
                "partial class Owner { static int First() => 1; }"), options),
            CSharpSourceDocument.FromBytes("two.cs", System.Text.Encoding.UTF8.GetBytes("""
                partial class Owner
                {
                    static void Marker()
                    {
                        byte a = (byte)'A';
                        byte b = (byte)'B';
                #if UNSELECTED
                        byte hidden = (byte)'C'; // forbidden-raw-control
                #endif
                    }
                }
                """), options)
        }, [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
        string declarations = HostSourceText.Declarations(source, "Owner");
        HostCheck.True(HostSourceText.Contains(declarations, "First() => 1"));
        HostCheck.True(HostSourceText.Contains(declarations, "void Marker()"));
        HostCheck.True(!declarations.Contains("forbidden-raw-control"));
        HostCheck.True(HostSourceText.Documents(source, "Owner").Contains("forbidden-raw-control"));
        HostCheck.Equal("AB", HostSourceText.ByteCharacters(source.Method("Owner", "Marker")));
        HostCheck.True(!source.Compilation.GetDiagnostics().Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
    }

    [HostTest("formatter exclusions inspect inactive methods and conditional method bodies")]
    private static void ConditionalFormatters()
    {
        var options = new CSharpParseOptions(LanguageVersion.CSharp14);
        var source = new CSharpSourceCatalog(
        [
            CSharpSourceDocument.FromBytes("formatter.cs", System.Text.Encoding.UTF8.GetBytes("""
                partial class Owner
                {
                    private static void Conditional()
                    {
                        byte a = (byte)'A';
                #if DIFFERENT_BODY
                        byte b = (byte)'C';
                #else
                        byte b = (byte)'B';
                #endif
                    }
                #if INACTIVE_HELPER
                    private static void Hidden() { byte d = (byte)'D'; }
                #endif
                    private static int DifferentReturn() { byte e = (byte)'E'; return 0; }
                    private static void Combined()
                    {
                #if FIRST
                        byte first = (byte)'X';
                #endif
                #if SECOND
                        byte second = (byte)'Y';
                #endif
                #if THIRD
                        byte third = (byte)'Z';
                #endif
                    }
                }
                """), options),
            CSharpSourceDocument.FromBytes("arbitrary-conditional-name.cs", System.Text.Encoding.UTF8.GetBytes("""
                #if INACTIVE_PARTIAL
                partial class Owner
                {
                    private static void HiddenPartial() { byte f = (byte)'F'; }
                    private const string Forbidden = "raw-inactive-owner-marker";
                }
                #endif
                class Foreign { private static void HiddenPartial() { byte g = (byte)'G'; } }
                class Owner<T> { private static void HiddenPartial() { byte h = (byte)'H'; } }
                """), options)
        ], [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
        HostCheck.True(HostSourceText.HasByteFormatter(source, "Owner", "AB"));
        HostCheck.True(HostSourceText.HasByteFormatter(source, "Owner", "AC"));
        HostCheck.True(HostSourceText.HasByteFormatter(source, "Owner", "D"));
        HostCheck.True(!HostSourceText.HasByteFormatter(source, "Owner", "AD"));
        HostCheck.True(!HostSourceText.HasByteFormatter(source, "Owner", "E"), "original void formatter scope");
        HostCheck.True(HostSourceText.HasByteFormatter(source, "Owner", "XYZ"), "conservative union across body witnesses");
        HostCheck.True(HostSourceText.HasByteFormatter(source, "Owner", "F"), "entirely inactive partial");
        HostCheck.True(!HostSourceText.HasByteFormatter(source, "Owner", "G"), "foreign owner");
        HostCheck.True(!HostSourceText.HasByteFormatter(source, "Owner", "H"), "generic owner collision");
        HostCheck.True(HostSourceText.Documents(source, "Owner").Contains("raw-inactive-owner-marker"));
    }

    [HostTest("source fragments preserve token boundaries and ordered token positions")]
    private static void TokenBoundaries()
    {
        HostCheck.True(HostSourceText.Contains("Call (\n value );", "Call(value);"));
        HostCheck.True(!HostSourceText.Contains("LongName();", "Name();"));
        HostCheck.True(!HostSourceText.Contains("public static unsafeResult ValidateCatalog()",
            "public static unsafe Result ValidateCatalog("));
        HostCheck.True(!HostSourceText.Contains("a + + b", "a++ + b"));
        HostCheck.True(!HostSourceText.Contains("a + + b", "a ++ b"));
        HostCheck.True(!HostSourceText.Contains("a ++ b", "a + + b"));
        HostCheck.Equal(2, HostSourceText.Count("Call(); OtherCall(); Call();", "Call();"));
        int first = HostSourceText.IndexOf("A(); B();", "A();");
        HostCheck.True(HostSourceText.IndexOf("A(); B();", "B();", first) > first);
        HostCheck.Equal(-1, HostSourceText.IndexOf("A(); B();", "B();", -1));
    }

    [HostTest("active source text keeps the compiler-selected branch and excludes disabled trivia")]
    private static void SelectedBranches()
    {
        const string text = """
            class Owner
            {
            #if SELECTED
                // The inactive method must not enter the active source view.
                static int Value()
                {
            #if INNER
                    return 7;
            #else
                    return 9;
            #endif
                }
            #else
                static string Inactive() => "unselected";
            #endif
            }
            """;
        foreach (bool inner in new[] { false, true })
        {
            var options = new CSharpParseOptions(LanguageVersion.CSharp14,
                preprocessorSymbols: inner ? ["SELECTED", "INNER"] : ["SELECTED"]);
            var root = CSharpSyntaxTree.ParseText(text, options).GetCompilationUnitRoot();
            string active = HostSourceText.Active(root.DescendantNodes().OfType<MethodDeclarationSyntax>().Single());
            HostCheck.Equal("static int Value()\r\n{\r\n    return " + (inner ? "7" : "9") + ";\r\n}", active);
            HostCheck.True(root.ToFullString().Contains("Inactive"), "original document retains inactive source");
        }
    }
}
