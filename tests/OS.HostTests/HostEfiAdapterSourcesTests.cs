using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Tooling;
using System.Text;

internal static class HostEfiAdapterSourcesTests
{
    [HostTest("host EFI adapter extraction follows partial declarations without changing bodies or member order")]
    private static void PartialDeclarations()
    {
        const string first = "namespace OS.Boot; public static partial class EfiEntry { private static partial class Adapter { private static int first = 1; private static bool FailBeforeEntry() { return first == 1; } } }";
        const string second = """
            namespace OS.Boot;
            public static partial class EfiEntry {
                private static partial class Adapter {
                    private static int second = 2;
                    private const string Braces = "{ ignored }";
                    internal static bool Ready() {
            #if UNRELATED_DISABLED
                        // Raw code and comments remain part of the host harness input.
                        Missing();
            #endif
                        return second == 2 && FailBeforeEntry();
                    }
                }
            }
            """;
        var source = Catalog([("arbitrary-first.cs", first), ("moved-second.cs", second)]);
        const string owner = "OS.Boot.EfiEntry+Adapter";
        string raw = HostEfiAdapterSources.Raw(source, owner);
        string host = HostEfiAdapterSources.ForHost(source, owner, "FailBeforeEntry");
        HostCheck.True(raw.Contains("Missing();", StringComparison.Ordinal)
            && !host.Contains("Missing();", StringComparison.Ordinal), "raw guards retain inactive code; SDK input freezes selected branches");
        var original = SyntaxFactory.ParseCompilationUnit(raw);
        var changed = SyntaxFactory.ParseCompilationUnit(host);
        var restored = changed.ReplaceTokens(changed.DescendantNodes().OfType<ClassDeclarationSyntax>()
                .SelectMany(declaration => declaration.Modifiers.Where(token => token.IsKind(SyntaxKind.InternalKeyword)))
                .Concat(changed.DescendantNodes().OfType<MethodDeclarationSyntax>()
                    .Where(method => method.Identifier.ValueText == "FailBeforeEntry")
                    .SelectMany(method => method.Modifiers.Where(token => token.IsKind(SyntaxKind.InternalKeyword)))),
            (token, _) => SyntaxFactory.Token(token.LeadingTrivia, SyntaxKind.PrivateKeyword, token.TrailingTrivia));
        HostCheck.True(restored.DescendantTokens().Select(token => (token.RawKind, token.Text))
            .SequenceEqual(original.DescendantTokens().Select(token => (token.RawKind, token.Text))),
            "selected member tokens change only at explicitly requested access seams");
        HostCheck.True(raw.IndexOf("first = 1", StringComparison.Ordinal) < raw.IndexOf("second = 2", StringComparison.Ordinal),
            "compiler source and initializer order retained");
        var replay = Catalog([("Host.cs", "namespace OS.Boot; public static class EfiEntry { " + host + " }")]);
        HostCheck.True(!replay.Compilation.GetDiagnostics().Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error),
            "the exposed partial host input binds");
        Reject(() => HostEfiAdapterSources.ForHost(source, owner, "Absent"));
        Reject(() => HostEfiAdapterSources.ForHost(source, owner, "Ready"));
        Reject(() => HostEfiAdapterSources.ForHost(source, owner, "FailBeforeEntry", "FailBeforeEntry"));
        Reject(() => HostEfiAdapterSources.Raw(Catalog([("wrong.cs", first.Replace("private static partial class Adapter", "internal static partial class Adapter", StringComparison.Ordinal))]), owner));
    }

    [HostTest("host EFI adapter projection preserves selected branches and rejects import contexts")]
    private static void SelectedBranches()
    {
        const string owner = "OS.Boot.EfiEntry+Adapter";
        const string body = """
            namespace OS.Boot;
            public static class EfiEntry {
                private static class Adapter {
                    internal static int Value() {
            #if ENABLED
                        return 1;
            #else
                        return 2;
            #endif
                    }
                }
            }
            """;
        foreach (var (sourceText, symbols, expected) in new[]
        {
            (body, new[] { "ENABLED" }, 1),
            ("#define ENABLED\n" + body, Array.Empty<string>(), 1),
            (body.Replace("ENABLED", "NET11_0", StringComparison.Ordinal), Array.Empty<string>(), 2),
            (body.Replace("return 2;", "return 1;", StringComparison.Ordinal), Array.Empty<string>(), 1)
        })
        {
            var source = Catalog([("selected.cs", sourceText)], symbols);
            HostCheck.True(!source.Compilation.GetDiagnostics().Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error),
                "conditional source control binds before projection");
            string projected = HostEfiAdapterSources.ForHost(source, owner);
            foreach (string[] hostSymbols in new[] { Array.Empty<string>(), new[] { "ENABLED", "RELEASE", "NET", "NET11_0" } })
            {
                var replay = Catalog([("host.cs", "namespace OS.Boot; public static class EfiEntry { " + projected + " }")], hostSymbols);
                HostCheck.True(!replay.Compilation.GetDiagnostics().Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error),
                    "projected selected source binds under independent host symbols");
                var result = replay.Method(owner, "Value").DescendantNodes().OfType<ReturnStatementSyntax>().Single();
                HostCheck.True(replay.Compilation.GetSemanticModel(result.SyntaxTree).GetConstantValue(result.Expression!).Value is int value
                    && value == expected, "host symbols cannot change the selected EFI branch");
            }
        }
        const string simple = "namespace OS.Boot; public static class EfiEntry { private static partial class Adapter { internal static int Value() => 1; } }";
        foreach (var files in new[]
        {
            new[] { ("alias.cs", "using ValueAlias = System.Int32;\n" + simple.Replace("static int Value", "static ValueAlias Value", StringComparison.Ordinal)) },
            new[] { ("namespace.cs", "namespace OS.Boot { using System; public static class EfiEntry { private static class Adapter { internal static IntPtr Value() => default; } } }") },
            new[] { ("adapter.cs", simple.Replace("static int Value() => 1", "static IntPtr Value() => default", StringComparison.Ordinal)), ("global.cs", "global using System;") }
        })
        {
            var source = Catalog(files);
            HostCheck.True(!source.Compilation.GetDiagnostics().Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error),
                "import control is valid before bounded extraction rejects it");
            Reject(() => HostEfiAdapterSources.ForHost(source, owner));
        }
        var unused = Catalog([("adapter.cs", simple), ("global.cs", "global using System;")]);
        HostCheck.True(HostEfiAdapterSources.ForHost(unused, owner).Contains("Value() => 1", StringComparison.Ordinal),
            "unused global imports do not prevent independently bound projection");
        var contextDirective = Catalog([("line.cs", simple.Replace("internal static int Value", "\n#line 100\ninternal static int Value", StringComparison.Ordinal))]);
        Reject(() => HostEfiAdapterSources.ForHost(contextDirective, owner));
    }

    private static CSharpSourceCatalog Catalog((string Path, string Text)[] files, string[]? symbols = null)
    {
        var parse = new CSharpParseOptions(LanguageVersion.CSharp14, preprocessorSymbols: symbols);
        return new CSharpSourceCatalog(files.Select(file => CSharpSourceDocument.FromBytes(file.Path, Encoding.UTF8.GetBytes(file.Text), parse)),
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
    }

    [HostTest("host EFI adapter rejects an implicit collection protocol with changed imported binding")]
    private static void ImportProtocolControls()
    {
        const string text = """
            namespace Imported {
                public static class Extensions { public static void Add(this OS.Boot.Bag target, int value) { } }
            }
            namespace OS { public static class Fallback { public static void Add(this object target, int value) { } } }
            namespace OS.Boot {
                using Imported;
                public sealed class Bag : System.Collections.IEnumerable {
                    public System.Collections.IEnumerator GetEnumerator() => throw new System.NotSupportedException();
                }
                public static class EfiEntry {
                    private static class Adapter { internal static Bag Value() => new Bag { 1 }; }
                }
            }
            """;
        var original = Catalog([("source.cs", text)]);
        var stripped = Catalog([("source.cs", text.Replace("using Imported;", "", StringComparison.Ordinal))]);
        var identities = new List<string>();
        foreach (var source in new[] { original, stripped })
        {
            HostCheck.True(!source.Compilation.GetDiagnostics().Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error),
                "both implicit protocol controls bind without errors");
            var element = source.Documents[0].Syntax.GetRoot().DescendantNodes()
                .OfType<InitializerExpressionSyntax>().Single().Expressions.Single();
            identities.Add(source.Compilation.GetSemanticModel(element.SyntaxTree).GetCollectionInitializerSymbolInfo(element)
                .Symbol!.GetDocumentationCommentId()!);
        }
        HostCheck.True(identities[0] != identities[1], "the valid control changes the implicit Add method");
        Reject(() => HostEfiAdapterSources.ForHost(original, "OS.Boot.EfiEntry+Adapter"));
    }

    private static void Reject(Action operation)
    {
        try
        {
            operation();
        }
        catch (InvalidOperationException)
        {
            return;
        }
        throw new InvalidOperationException("invalid adapter extraction accepted");
    }
}
