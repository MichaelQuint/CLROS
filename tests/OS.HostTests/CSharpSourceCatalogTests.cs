using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Tooling;

internal static class CSharpSourceCatalogTests
{
    [HostTest("source catalog follows moved partial types nested arity and method syntax")]
    private static void DeclarationIdentity()
    {
        var catalog = Create(
            ("unrelated-name.cs", "namespace A; public partial class Owner<T> { public static int Get() => 7; }"),
            ("moved/child.cs", "namespace A; public partial class Owner<T> { public struct Child<U> {} }"));
        HostCheck.Equal(2, catalog.TypeSources("A.Owner`1").Count);
        HostCheck.Equal("moved/child.cs", catalog.TypeSources("A.Owner`1+Child`1").Single().Path);
        var method = catalog.Method("A.Owner`1", "Get");
        HostCheck.Equal("7", method.ExpressionBody!.Expression.ToString());
        HostCheck.Equal("unrelated-name.cs", catalog.Document(method).Path);
        Reject(() => catalog.Type("A.Owner.Child"), "source_catalog_type_missing_or_ambiguous");
    }

    [HostTest("source catalog rejects ambiguous overloads conflicting partials and missing declarations")]
    private static void Ambiguity()
    {
        var catalog = Create(("a.cs", "class C { void M(int x) {} void M(string x) {} }"));
        Reject(() => catalog.Method("C", "M"), "source_catalog_method_missing_or_ambiguous");
        HostCheck.Equal("int", catalog.Method("C", "M", method => method.Parameters.Single().Type.SpecialType == SpecialType.System_Int32)
            .ParameterList.Parameters.Single().Type!.ToString());
        Reject(() => catalog.Method("C", "Missing"), "source_catalog_method_missing_or_ambiguous");
        Reject(() => Create(("a.cs", "partial class C {}"), ("b.cs", "partial struct C {}")), "source_catalog_invalid_declarations");
        Reject(() => Create(("a.cs", "class C {}"), ("b.cs", "class C {}")), "source_catalog_invalid_declarations");
        Reject(() => Create(("a.cs", "partial class C<T> where T:class {}"), ("b.cs", "partial class C<T> where T:struct {}")), "source_catalog_invalid_declarations");
    }

    [HostTest("source catalog binds aliased calls and rejects same-name foreign or unresolved callees")]
    private static void SemanticCalls()
    {
        const string source = """
            using Renamed = Approved.Buffer;
            namespace Approved { public static class Buffer { public static void Copy() {} } }
            namespace Foreign { public static class Buffer { public static void Copy() {} } }
            class C { void Good() { Renamed.Copy(); } void Bad() { Foreign.Buffer.Copy(); } void Missing() { Unknown.Copy(); } }
            """;
        var catalog = Create(("anything.cs", source));
        InvocationExpressionSyntax Call(string name) => catalog.Method("C", name).DescendantNodes().OfType<InvocationExpressionSyntax>().Single();
        HostCheck.Equal("Approved.Buffer", catalog.Invocation(Call("Good")).ContainingType.ToDisplayString());
        HostCheck.True(catalog.Invocation(Call("Bad")).ContainingType.ToDisplayString() != "Approved.Buffer");
        Reject(() => catalog.Invocation(Call("Missing")), "source_catalog_invalid_invocation");
    }

    [HostTest("source catalog distinguishes method calls function pointers and nameof without ignoring binding errors")]
    private static void InvocationKinds()
    {
        var catalog = Create(("calls.cs", """
            unsafe class C
            {
                static void Target() {}
                static void Run(System.Action action, delegate* unmanaged<void> pointer)
                {
                    Target(); action(); pointer(); _ = nameof(Target);
                }
                static void Invalid() { Target(); Unknown(); }
            }
            """));
        var calls = catalog.MethodInvocations(catalog.Method("C", "Run"));
        HostCheck.Equal(2, calls.Count);
        HostCheck.True(calls.Any(call => call.TargetMethod.Name == "Target" && call.TargetMethod.MethodKind == MethodKind.Ordinary));
        HostCheck.True(calls.Any(call => call.TargetMethod.MethodKind == MethodKind.DelegateInvoke));
        Reject(() => catalog.MethodInvocations(catalog.Method("C", "Invalid")), "source_catalog_method_binding");
        var foreign = Create(("foreign.cs", "class D { static void Run() {} }"));
        try
        {
            catalog.MethodInvocations(foreign.Method("D", "Run"));
        }
        catch (ArgumentException error) when (error.Message.StartsWith("source_catalog_foreign_syntax", StringComparison.Ordinal))
        {
            return;
        }
        throw new InvalidOperationException("foreign method syntax accepted");
    }

    [HostTest("source catalog uses selected symbols and keeps text and byte identity from one read")]
    private static void ConditionalInputs()
    {
        string directory = Directory.CreateTempSubdirectory("clros-catalog-").FullName;
        try
        {
            string path = Path.Combine(directory, "arbitrary.cs");
            const string source = "#if CHOSEN\nclass Selected {}\n#else\nclass Ordinary {}\n#endif\n";
            File.WriteAllText(path, source, new UTF8Encoding(false));
            var chosen = CSharpSourceDocument.Read(path, new CSharpParseOptions(LanguageVersion.CSharp14, preprocessorSymbols: ["CHOSEN"]));
            var catalog = new CSharpSourceCatalog([chosen], References());
            HostCheck.Equal("Selected", catalog.Type("Selected").Name);
            Reject(() => catalog.Type("Ordinary"), "source_catalog_type_missing_or_ambiguous");
            HostCheck.Equal(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source))), chosen.Sha256);
            File.WriteAllText(path, "class Changed {}");
            HostCheck.Equal(source, chosen.Text);
            HostCheck.Equal("Selected", catalog.Type("Selected").Name);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [HostTest("source catalog derives dependency files from symbols while preserving compiler order")]
    private static void DependencyClosure()
    {
        var catalog = Create(
            ("renamed-value.cs", "namespace N; internal struct Value { public int Number; }"),
            ("globals.g.cs", "global using Alias = N.Helper;"),
            ("unrelated.cs", "namespace N; internal class Unrelated {}"),
            ("entry.cs", "namespace N; internal static class Entry { public static int Run() => Alias.Read().Number; }"),
            ("moved-helper.cs", "namespace N; internal static class Helper { public static Value Read() => new Value { Number=7 }; }"));
        string[] files = catalog.DependencySources("N.Entry").Select(document => document.Path).ToArray();
        HostCheck.True(files.SequenceEqual(new[] { "renamed-value.cs", "globals.g.cs", "entry.cs", "moved-helper.cs" }, StringComparer.Ordinal));
        var invalid = Create(("invalid.cs", "class C { void Run() { Missing.Call(); } }"));
        Reject(() => invalid.DependencySources("C"), "source_catalog_dependency_binding");
    }

    [HostTest("source catalog evaluates actual kernel Compile symbols global usings and bound shared calls")]
    private static void ActualKernelProject()
    {
        var catalog = HostSourceCatalog.Project("src/OS.Kernel/OS.Kernel.csproj");
        var method = catalog.Method("OS.Storage.OsPackageFileStagingPlanner", "StageOpenedFile");
        var calls = method.DescendantNodes().OfType<InvocationExpressionSyntax>().Select(catalog.Invocation).ToArray();
        HostCheck.True(calls.Any(call => call.Name == "MemoryCopy"
            && call.ContainingType.ToDisplayString() == "OS.Std.Memory.BufferOperations"));
        HostCheck.True(catalog.Compilation.SyntaxTrees.Any(tree => tree.GetRoot().DescendantNodes()
            .OfType<UsingDirectiveSyntax>().Any(import => import.GlobalKeyword.IsKind(SyntaxKind.GlobalKeyword))));
        HostCheck.True(catalog.Documents.Where(document => document.IsGenerated).Any(document => document.Syntax.GetRoot().DescendantNodes()
            .OfType<UsingDirectiveSyntax>().Any(import => import.GlobalKeyword.IsKind(SyntaxKind.GlobalKeyword))));
        HostCheck.True(catalog.TypeSources("OS.Storage.OsPackageFileStagingPlanner").All(document => File.Exists(document.Path) && !document.IsGenerated));
    }

    [HostTest("source catalog preserves executable project context for top-level programs")]
    private static void ActualExecutableProject()
    {
        var catalog = HostSourceCatalog.Project("tools/OS.Tooling/OS.Tooling.csproj");
        HostCheck.Equal(OutputKind.ConsoleApplication, catalog.Compilation.Options.OutputKind);
        HostCheck.True(catalog.Compilation.GetEntryPoint(default) is not null);
        HostCheck.Equal(NullableContextOptions.Enable, catalog.Compilation.Options.NullableContextOptions);
    }

    [HostTest("source catalog rejects an unbuilt compiler profile without refreshing its receipt")]
    private static void UnbuiltCompilerProfile()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        var baseline = CSharpProjectEvaluation.LoadAsync(root, "src/OS.Kernel/OS.Kernel.csproj").GetAwaiter().GetResult();
        string receiptPath = baseline.CompilerArtifacts.ReceiptPath!;
        byte[] receipt = File.ReadAllBytes(receiptPath);
        string changedOptimize = baseline.Sources.Compilation.Options.OptimizationLevel == OptimizationLevel.Release ? "false" : "true";
        Reject(() => CSharpProjectEvaluation.LoadAsync(root, "src/OS.Kernel/OS.Kernel.csproj",
            new Dictionary<string, string> { ["Optimize"] = changedOptimize }).GetAwaiter().GetResult(), "source_catalog_receipt_arguments");
        HostCheck.True(receipt.SequenceEqual(File.ReadAllBytes(receiptPath)), "evaluation must not rewrite an accepted producer receipt");
    }

    private static CSharpSourceCatalog Create(params (string Path, string Text)[] sources)
        => new(sources.Select(source => new CSharpSourceDocument(source.Path,
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source.Text))),
            CSharpSyntaxTree.ParseText(source.Text, new CSharpParseOptions(LanguageVersion.CSharp14), source.Path))), References());

    private static MetadataReference[] References() => [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)];

    private static void Reject(Action action, string reason)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException error) when (error.Message.StartsWith(reason, StringComparison.Ordinal))
        {
            return;
        }
        throw new InvalidOperationException("expected rejection " + reason);
    }
}
