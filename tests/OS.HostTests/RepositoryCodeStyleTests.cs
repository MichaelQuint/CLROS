using OS.Tooling;

internal static class RepositoryCodeStyleTests
{
    [HostTest("source style permits standalone, helper and partial filenames")]
    private static void AcceptedFiles()
    {
        foreach (var (path, source) in new[]
        {
            ("Owner.cs", "namespace N; public class Owner {}"),
            ("Owner.Result.cs", "namespace N; public readonly struct Result {}"),
            ("Owner.Read.cs", "namespace N; public partial class Owner { void Read() {} }"),
            ("Owner.Inner.cs", "namespace N; public partial class Owner { private struct Inner { int value; } }"),
            ("Owner.Inner.Leaf.cs", "partial class Owner { partial class Inner { private enum Leaf { A } } }"),
            ("Callback.cs", "public delegate void Callback();"),
            ("Value.cs", "public record Value(int Number);"),
            ("Program.cs", "System.Console.WriteLine(1);"),
            ("Generated.g.cs", "class A {} class B {}")
        })
            HostCheck.Equal(0, RepositoryCodeStyle.CheckFile(path, source).Count, path);
    }

    [HostTest("source style rejects multiple types, mixed parents and incorrect names")]
    private static void RejectedFiles()
    {
        foreach (var (path, source, rule) in new[]
        {
            ("A.cs", "class A {} struct B {}", "CLROSSTYLE001"),
            ("Owner.cs", "partial class Owner { int value; private struct Inner {} }", "CLROSSTYLE001"),
            ("Owner.Inner.cs", "partial class Owner { struct Inner {} enum Other { A } }", "CLROSSTYLE001"),
            ("Wrong.cs", "public struct Value {}", "CLROSSTYLE002"),
            ("Inner.cs", "partial class Owner { private struct Inner {} }", "CLROSSTYLE002"),
            ("Program.cs", "System.Console.WriteLine(1); class Helper {}", "CLROSSTYLE001"),
            ("Broken.cs", "class Broken {", "CLROSSTYLE000")
        })
            HostCheck.True(RepositoryCodeStyle.CheckFile(path, source).Any(value => value.Rule == rule), path + " " + rule);
    }

    [HostTest("source style inspects nested conditional and elif alternatives")]
    private static void ConditionalBranches()
    {
        const string source = """
            #if A && B && !C
            class Hidden { }
            struct AlsoHidden { }
            #elif !A && C
            #if D
            enum Another { A }
            struct Extra { }
            #endif
            #else
            class Owner { }
            #endif
            """;
        var result = RepositoryCodeStyle.CheckFile("Owner.cs", source);
        foreach (string name in new[] { "Hidden", "AlsoHidden", "Another", "Extra" })
            HostCheck.True(result.Any(value => value.Rule == "CLROSSTYLE002" && value.Message.Contains(name, StringComparison.Ordinal)), name);
        HostCheck.Equal(0, RepositoryCodeStyle.CheckFile("Owner.cs", "#if false\nnot C#\n#endif\nclass Owner {} ").Count);
    }

    [HostTest("source style detects simultaneously enabled independent declarations")]
    private static void IndependentBranches()
    {
        const string source = "#if FIRST\nclass Child {}\n#endif\n#if SECOND\npartial class Container {}\n#endif\n";
        HostCheck.True(RepositoryCodeStyle.CheckFile("Container.Child.cs", source)
            .Any(value => value.Rule == "CLROSSTYLE001"));
        const string exclusive = "#if FIRST\nclass Owner {}\n#elif SECOND\nstruct Owner {}\n#endif";
        HostCheck.Equal(0, RepositoryCodeStyle.CheckFile("Owner.cs", exclusive).Count);
    }
}
