using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using OS.Tooling;

internal static class HostSemanticFingerprintTests
{
    [HostTest("host resume dependencies reject lexical collisions and follow bound aliases")]
    private static void BoundIdentity()
    {
        Dictionary<string, string> sources = new()
        {
            ["fixture.cs"] = "using Chosen = Actual.Value; class Fixture { int Run() => Chosen.Host; }",
            ["value.cs"] = "namespace Actual; class Value { internal const int Host = 1; }",
            ["unrelated.cs"] = "class DiskCapture { class Host { string Run() => System.IO.File.ReadAllText(\"input\"); } }"
        };
        string first = Key(sources);
        HostCheck.True(!first.StartsWith("fresh:", StringComparison.Ordinal));
        sources["unrelated.cs"] = sources["unrelated.cs"].Replace("input", "changed");
        HostCheck.Equal(first, Key(sources));
        sources["value.cs"] = sources["value.cs"].Replace("= 1", "= 2");
        HostCheck.True(first != Key(sources));
        sources["fixture.cs"] = "class Fixture { string Run() => \"Host Value DiskCapture\"; }";
        first = Key(sources);
        sources["value.cs"] = sources["value.cs"].Replace("= 2", "= 3");
        HostCheck.Equal(first, Key(sources));
    }

    [HostTest("host resume keeps indirect and external helper execution fresh")]
    private static void ExecutionBoundary()
    {
        foreach (string body in new[]
        {
            "return System.IO.File.ReadAllText(\"input\").Length;",
            "return System.Environment.TickCount;",
            "return System.Activator.CreateInstance(typeof(object)) is null ? 0 : 1;",
            "System.Func<int> call = () => 1; return call();",
            "return ((System.IComparable<int>)1).CompareTo(2);"
        })
        {
            Dictionary<string, string> sources = new()
            {
                ["entry.cs"] = "return Helper.Read();",
                ["fixture.cs"] = "class Fixture { int Run() => Helper.Read(); }",
                ["helper.cs"] = "class Helper { internal static int Read() { " + body + " } }"
            };
            var catalog = Catalog(sources, executable: true);
            HostCheck.True(HostTestFingerprint.SharedSources(catalog).Any(document => document.Path == "helper.cs"));
            HostCheck.True(new HostTestFingerprint(catalog, "shared").For("Fixture", "all").StartsWith("fresh:", StringComparison.Ordinal), body);
        }
        foreach (string definition in new[]
        {
            "static class Helper { internal static int Value = System.Environment.TickCount; }",
            "static class Helper { static Helper() { System.IO.File.ReadAllText(\"input\"); } internal static int Value = 1; }"
        })
            HostCheck.True(Key(new Dictionary<string, string>
            {
                ["fixture.cs"] = "class Fixture { int Run() => Helper.Value; }",
                ["helper.cs"] = definition
            }).StartsWith("fresh:", StringComparison.Ordinal));
        // Opaque metadata fields/operators and unhandled compiler constructs
        // must not become reusable just because there is no invocation syntax.
        foreach (string body in new[]
        {
            "return System.DateTime.MinValue.Ticks;",
            "System.Numerics.BigInteger a = default, b = default; return (long)(a + b);",
            "lock (new object()) return 1;",
            "return System.Threading.Thread.CurrentThread.ManagedThreadId;"
        })
            HostCheck.True(Key(new Dictionary<string, string>
            {
                ["fixture.cs"] = "class Fixture { long Run() { " + body + " } }"
            }).StartsWith("fresh:", StringComparison.Ordinal), body);
        HostCheck.True(!Key(new Dictionary<string, string>
        {
            ["fixture.cs"] = "class Fixture { long Run() => (long)(1m + 2m); }"
        }).StartsWith("fresh:", StringComparison.Ordinal), "folded decimal constant has no runtime operator call");
    }

    [HostTest("host resume refuses hidden conditional concatenation and generic operator dispatch")]
    private static void HiddenDispatch()
    {
        foreach (string source in new[]
        {
            "class Fixture { static int Run() { Flag f=default; Flag result=f&&f; return 1; } } struct Flag { public static bool operator true(Flag f)=>true; public static bool operator false(Flag f) { System.IO.File.ReadAllText(\"input\"); return false; } public static Flag operator &(Flag a,Flag b)=>a; }",
            "class Fixture { static int Run() { Value v=default; string text=\"x\"+v; return 1; } } struct Value { public override string ToString()=>System.IO.File.ReadAllText(\"input\"); }",
            "class Fixture { static int Run() { Value v=default; string text=\"x\"; text+=v; return 1; } } struct Value { public override string ToString()=>System.IO.File.ReadAllText(\"input\"); }",
            "class Fixture { static int Run() { dynamic v=default(Value); dynamic result=v+v; return 1; } } struct Value { public static Value operator +(Value a,Value b) { System.IO.File.ReadAllText(\"input\"); return a; } }",
            "class Fixture { static int Run() { Value value=default; Value result=Factory.Add(value,value); return 1; } } interface IAdd<T> where T:IAdd<T> { static abstract T operator +(T a,T b); } struct Value:IAdd<Value> { public static Value operator +(Value a,Value b) { System.IO.File.ReadAllText(\"input\"); return a; } } static class Factory { public static T Add<T>(T a,T b) where T:IAdd<T> => a+b; }"
        })
            HostCheck.True(Key(new Dictionary<string, string> { ["fixture.cs"] = source }).StartsWith("fresh:", StringComparison.Ordinal), source);
    }

    [HostTest("host resume invalidates active globals initializer order and module initialization")]
    private static void CompilationBoundary()
    {
        Dictionary<string, string> sources = new()
        {
            ["fixture.cs"] = "partial class Fixture { static int A = B + 1; int Run() => A; }",
            ["moved.cs"] = "partial class Fixture { static int B = 1; }"
        };
        string first = Key(sources);
        HostCheck.True(first != Key(sources.Reverse().ToDictionary(pair => pair.Key, pair => pair.Value)));
        sources["global.cs"] = "global using Chosen = System.Int32;";
        HostCheck.True(first != Key(sources));
        sources.Remove("global.cs");
        sources["module.cs"] = "static class Startup { [System.Runtime.CompilerServices.ModuleInitializer] internal static void Initialize() {} }";
        HostCheck.True(Key(sources).StartsWith("fresh:module_initializer:", StringComparison.Ordinal));
    }

    [HostTest("host resume binds implicit conversions and extension blocks under selected symbols")]
    private static void ImplicitDependencies()
    {
        Dictionary<string, string> sources = new()
        {
            ["fixture.cs"] = "class Fixture { int Run() { Value v = default; return v; } }",
            ["value.cs"] = "struct Value { public static implicit operator int(Value v) => 1; }"
        };
        string first = Key(sources);
        sources["value.cs"] = sources["value.cs"].Replace("=> 1", "=> 2");
        HostCheck.True(first != Key(sources));
        sources["fixture.cs"] = "class Fixture { int Run() => 1.Plus(); }";
        sources["value.cs"] = "static class Extensions { extension(int value) { public int Plus() => value + 1; } }";
        first = Key(sources);
        sources["value.cs"] = sources["value.cs"].Replace("+ 1", "+ 2");
        HostCheck.True(first != Key(sources));
        sources["value.cs"] = "#if SELECTED\nstatic class Extensions { public static int Plus(this int value) => 1; }\n#else\nstatic class Extensions { public static int Plus(this int value) => 2; }\n#endif";
        HostCheck.True(new HostTestFingerprint(sources, "shared", ["SELECTED"]).For("Fixture", "all") != Key(sources));
    }

    private static string Key(IReadOnlyDictionary<string, string> sources) => new HostTestFingerprint(sources, "shared").For("Fixture", "all");

    internal static CSharpSourceCatalog Catalog(IReadOnlyDictionary<string, string> sources, bool executable = false)
        => new(sources.Select(pair => new CSharpSourceDocument(pair.Key, HostRunCheckpoint.HashText(pair.Value),
                CSharpSyntaxTree.ParseText(pair.Value, new CSharpParseOptions(LanguageVersion.CSharp14), pair.Key))),
            ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
                .Select(path => MetadataReference.CreateFromFile(path)),
            compilationOptions: new CSharpCompilationOptions(executable ? OutputKind.ConsoleApplication : OutputKind.DynamicallyLinkedLibrary,
                allowUnsafe: true));
}
