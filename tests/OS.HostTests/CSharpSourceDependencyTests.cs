using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using OS.Tooling;

internal static class CSharpSourceDependencyTests
{
    [HostTest("source dependency closure follows implicit iteration await deconstruction and query calls")]
    private static void ImplicitPatternDependencies()
    {
        var samples = new (string Name, string Entry, string Helpers)[]
        {
            ("foreach",
                "namespace N; class Entry { public static int Run() { int total=0; foreach (int value in new System.Version()) total+=value; return total; } }",
                "namespace N; static class Extensions { public static Enumerator GetEnumerator(this System.Version source) => new(); } struct Enumerator { public int Current => 1; public bool MoveNext() => false; }"),
            ("await",
                "namespace N; class Entry { public static async System.Threading.Tasks.Task<int> Run() => await new System.Version(); }",
                "namespace N; static class Extensions { public static Awaiter GetAwaiter(this System.Version source) => new(); } struct Awaiter : System.Runtime.CompilerServices.INotifyCompletion { public bool IsCompleted => true; public int GetResult() => 1; public void OnCompleted(System.Action callback) {} }"),
            ("deconstruct",
                "namespace N; class Entry { public static int Run() { var (left,right)=new System.Version(); return left+right; } }",
                "namespace N; static class Extensions { public static void Deconstruct(this System.Version source, out int left, out int right) { left=1; right=2; } }"),
            ("query",
                "namespace N; class Entry { public static int Run() => from value in new System.Version() select value; }",
                "namespace N; static class Extensions { public static T Select<T>(this System.Version source, System.Func<int,T> project) => project(1); }")
        };
        foreach (var sample in samples)
        {
            var catalog = Create(("entry.cs", sample.Entry), ("helpers.cs", sample.Helpers),
                ("unrelated.cs", "class Unrelated {}"));
            HostCheck.True(!catalog.Compilation.GetDiagnostics().Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error), sample.Name);
            var closure = catalog.DependencySources("N.Entry");
            HostCheck.True(closure.Select(document => document.Path).SequenceEqual(["entry.cs", "helpers.cs"], StringComparer.Ordinal), sample.Name);
            var subset = catalog.Compilation.RemoveAllSyntaxTrees().AddSyntaxTrees(closure.Select(document => document.Syntax));
            HostCheck.True(!subset.GetDiagnostics().Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error), sample.Name);
        }
    }

    [HostTest("source dependency closure preserves assembly and module attribute units")]
    private static void CompilationAttributes()
    {
        var catalog = Create(
            ("module.cs", "[module: System.Runtime.CompilerServices.SkipLocalsInit]"),
            ("assembly.cs", "[assembly: System.Runtime.CompilerServices.InternalsVisibleTo(\"Friend\")]"),
            ("entry.cs", "namespace N; class Entry { public static int Run() => 1; }"),
            ("unrelated.cs", "class Unrelated {}"));
        var closure = catalog.DependencySources("N.Entry");
        HostCheck.True(closure.Select(document => document.Path).SequenceEqual(["module.cs", "assembly.cs", "entry.cs"], StringComparer.Ordinal));
        var subset = catalog.Compilation.RemoveAllSyntaxTrees().AddSyntaxTrees(closure.Select(document => document.Syntax));
        HostCheck.True(subset.SourceModule.GetAttributes().Any(attribute =>
            attribute.AttributeClass?.ToDisplayString() == typeof(SkipLocalsInitAttribute).FullName));
        HostCheck.True(subset.Assembly.GetAttributes().Any(attribute =>
            attribute.AttributeClass?.ToDisplayString() == typeof(InternalsVisibleToAttribute).FullName
            && Equals(attribute.ConstructorArguments.Single().Value, "Friend")));
    }

    [HostTest("source dependency closure never accepts a missing collection spread dependency")]
    private static void AdditionalPatternIsCompleteOrRejected()
    {
        var catalog = Create(
            ("entry.cs", "namespace N; class Entry { public static int[] Run() => [.. new System.Version()]; }"),
            ("helpers.cs", "namespace N; static class Extensions { public static Enumerator GetEnumerator(this System.Version source) => new(); } struct Enumerator { public int Current => 1; public bool MoveNext() => false; }"));
        HostCheck.True(!catalog.Compilation.GetDiagnostics().Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        IReadOnlyList<CSharpSourceDocument> closure;
        try
        {
            closure = catalog.DependencySources("N.Entry");
        }
        catch (InvalidOperationException error) when (error.Message.StartsWith("source_catalog_dependency_incomplete", StringComparison.Ordinal))
        {
            return;
        }
        var subset = catalog.Compilation.RemoveAllSyntaxTrees().AddSyntaxTrees(closure.Select(document => document.Syntax));
        HostCheck.True(!subset.GetDiagnostics().Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error),
            "a supported collection-spread closure must bind completely");
    }

    private static CSharpSourceCatalog Create(params (string Path, string Text)[] sources) => new(
        sources.Select(source => new CSharpSourceDocument(source.Path,
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source.Text))),
            CSharpSyntaxTree.ParseText(source.Text, new CSharpParseOptions(LanguageVersion.CSharp14), source.Path))),
        [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
}
