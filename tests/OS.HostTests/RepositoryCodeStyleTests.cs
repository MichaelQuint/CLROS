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
            ("Owner.Inner.Feature.cs", "partial class Owner { partial class Inner { void Read() {} } }"),
            ("Owner.Feature.cs", "partial class Owner { partial class Inner { void Read() {} } }"),
            ("Owner.Inner.Leaf.cs", "partial class Owner { partial class Inner { } }"),
            ("Callback.cs", "public delegate void Callback();"),
            ("Value.cs", "public record Value(int Number);"),
            ("Owner.cs", "partial class Owner { int first; } partial class Owner { int second; }"),
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
            ("Owner.Feature.cs", "partial class Owner { private struct Inner { int value; } }", "CLROSSTYLE002"),
            ("Program.cs", "System.Console.WriteLine(1); class Helper {}", "CLROSSTYLE001"),
            ("Owner.Child.cs", "[System.Obsolete] partial class Owner { class Child {} }", "CLROSSTYLE001"),
            ("Owner.cs", "class Owner {} class Owner {}", "CLROSSTYLE001"),
            ("Owner.cs", "partial class Owner {} partial class Owner<T> {}", "CLROSSTYLE001"),
            ("Owner.cs", "namespace A { partial class Owner {} } namespace B { partial class Owner {} }", "CLROSSTYLE001"),
            ("Owner.cs", "partial class Owner {} partial struct Owner {}", "CLROSSTYLE001"),
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
        const string wrapper = "partial class Owner {\n#if FIELD\n int value;\n#endif\n#if CHILD\n class Child {}\n#endif\n}";
        HostCheck.True(RepositoryCodeStyle.CheckFile("Owner.Child.cs", wrapper)
            .Any(value => value.Rule == "CLROSSTYLE001"));
        const string modifier = "public\n#if !NONPARTIAL\npartial\n#endif\nclass Modifier {\n#if CHILD\nclass Child {}\n#endif\n}";
        HostCheck.True(RepositoryCodeStyle.CheckFile("Modifier.Child.cs", modifier)
            .Any(value => value.Rule == "CLROSSTYLE001"));
        const string globals = "#if GLOBAL\nSystem.Console.WriteLine(1);\n#endif\n#if TYPE\nclass Program {}\n#endif";
        HostCheck.True(RepositoryCodeStyle.CheckFile("Program.cs", globals)
            .Any(value => value.Rule == "CLROSSTYLE001"));
        const string defaultActive = "#if !Z\nclass Child {}\n#endif\n#if B || Z\npartial class Container {}\n#endif";
        HostCheck.True(RepositoryCodeStyle.CheckFile("Container.Child.cs", defaultActive)
            .Any(value => value.Rule == "CLROSSTYLE001"));
        const string samePartial = "#if FIRST\npartial class Owner { int first; }\n#endif\n#if SECOND\npartial class Owner { int second; }\n#endif";
        HostCheck.Equal(0, RepositoryCodeStyle.CheckFile("Owner.cs", samePartial).Count);
        const string arity = "partial class Owner\n#if GENERIC\n<T>\n#endif\n{}\n#if SECOND\npartial class Owner {}\n#endif";
        HostCheck.True(RepositoryCodeStyle.CheckFile("Owner.cs", arity).Any(value => value.Rule == "CLROSSTYLE001"));
    }

    [HostTest("source style follows conditional local symbol definitions")]
    private static void LocalSymbols()
    {
        foreach (string source in new[]
        {
            "#if OUTER\n#undef OUTER\n#if !OUTER && INNER\nclass Hidden {}\nstruct Other {}\n#endif\n#endif",
            "#if OUTER\n#define LOCAL\n#endif\n#if LOCAL && INNER\nclass Hidden {}\nstruct Other {}\n#endif"
        })
            HostCheck.True(RepositoryCodeStyle.CheckFile("Hidden.cs", source)
                .Any(value => value.Rule == "CLROSSTYLE001"));
        HostCheck.Equal(0, RepositoryCodeStyle.CheckFile("Owner.cs",
            "#define LOCAL\n#if !LOCAL\nclass Hidden {}\nstruct Other {}\n#endif\nclass Owner {} ").Count);
    }
}
