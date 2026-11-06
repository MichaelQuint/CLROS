using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using OS.Tooling;

internal static class HostBootSourceGuardTests
{
    [HostTest("boot source boundary follows aliases moved partials and exact API contracts")]
    private static void Surface()
    {
        string[] expected = ["public static int Read(int count)"];
        const string method = "public static int Read(int count) => count;";
        var source = Create("public static partial class Authority { " + method + " }",
            "public static partial class Authority {} ");
        HostCheck.True(HostBootSourceGuard.Authority(source, "Authority", expected));
        HostCheck.True(HostBootSourceGuard.Authority(Create(
            "using Number = System.Int32; public static class Authority { public static Number Read(Number count) => count; }"),
            "Authority", expected));
        foreach (string mutation in new[] { method.Replace("int count", "long count"), method + "public static int Extra;",
            method.Replace("=> count", "{ string forbidden = null; return count; }").TrimEnd(';'),
            "public static string Read(int count) => null;" })
            HostCheck.True(!HostBootSourceGuard.Authority(Create("public static class Authority { " + mutation + " }"),
                "Authority", expected));
        var conditional = Create("public static class Authority { " + method + "\n#if NOSTDLIB_EFI\npublic static int Leak;\n#endif\n}");
        HostCheck.True(!HostBootSourceGuard.Authority(conditional, "Authority", expected));
        foreach (string attributed in new[] { "[return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.I4)] public static int Read(int count) => count;",
            "public static int Read([System.Runtime.InteropServices.In] int count) => count;" })
            HostCheck.True(!HostBootSourceGuard.Authority(Create("public static class Authority { " + attributed + " }"), "Authority", expected));
    }

    [HostTest("boot source calls bind aliases and reject wrong owners duplicates forbidden and dormant calls")]
    private static void Confinement()
    {
        const string dependencies = "static class Target { public static void Required() {} public static void Forbidden() {} } "
            + "static class Foreign { public static void Required() {} }";
        bool Check(string body, string extra = "", bool enabled = true) => HostBootSourceGuard.CallsConfined(
            Create("using Alias = Target; partial class Root { public class Adapter { void Run() { " + body + " } } " + extra + " }",
                dependencies, "partial class Root {} class Sibling { void Run() { Target.Forbidden(); } }"),
            "Root", "Root+Adapter", "Target", ["Required"], ["Forbidden"], enabled);
        HostCheck.True(Check("Alias.Required();"));
        HostCheck.True(!Check("Foreign.Required();"));
        HostCheck.True(!Check("Alias.Required(); Alias.Required();"));
        HostCheck.True(!Check("Alias.Required(); Alias.Forbidden();"));
        HostCheck.True(!Check("", "void Outside() { Target.Required(); }"));
        HostCheck.True(!Check("void Dormant() { Alias.Required(); }"));
        HostCheck.True(!Check("System.Action dormant = () => Alias.Required();"));
        HostCheck.True(!Check("Alias.Required(); System.Action indirect = Alias.Forbidden; indirect();"));
        HostCheck.True(!Check("Alias.Required(); unsafe { delegate*<void> indirect = &Alias.Forbidden; indirect(); }"));
        HostCheck.True(Check("", enabled: false));
        HostCheck.True(!Check("Alias.Required();", enabled: false));
    }

    [HostTest("boot source order follows bound methods rather than physical file order")]
    private static void Order()
    {
        const string target = "static class Target { public static void First() {} public static void Last() {} }";
        bool Check(string body, string outside = "")
        {
            var catalog = Create("partial class Root { void Other() { " + outside + " } }", target,
                "using Alias = Target; partial class Root { void Phase() { " + body + " } }");
            return HostBootSourceGuard.OrderedCalls(catalog, "Root", "Phase", "Target", "First", "Last")
                && HostBootSourceGuard.CallsConfined(catalog, "Root", "Root", "Target", ["First", "Last"], [], true, "Phase");
        }
        HostCheck.True(Check("Alias.First(); Alias.Last();"));
        HostCheck.True(!Check("Alias.Last(); Alias.First();"));
        HostCheck.True(!Check("Alias.First(); Alias.First(); Alias.Last();"));
        HostCheck.True(!Check("void Dormant() { Alias.First(); Alias.Last(); }"));
        HostCheck.True(!Check("Alias.First(); Alias.Last();", "Target.First();"));
    }

    [HostTest("boot callbacks resolve actual address targets")]
    private static void Callback()
    {
        const string dependencies = "struct References { public ulong Entry; } unsafe static class Stub { public static void Prepare(delegate*<void> entry) {} }";
        bool Check(string field, string argument, string decoy = "", string fieldPrefix = "", string argumentPrefix = "") => HostBootSourceGuard.Callback(Create(dependencies,
            "unsafe class Root { static void Enter() {} static void Foreign() {} void Prepare() { " + decoy
            + "var references = new References { Entry = " + fieldPrefix + "(ulong)(delegate*<void>)&" + field
            + " }; Stub.Prepare(" + argumentPrefix + "&" + argument + "); } }"),
            "Root", "Prepare", "Enter", "References", "Entry", "Stub", "entry");
        HostCheck.True(Check("Enter", "Enter"));
        HostCheck.True(!Check("Foreign", "Enter"));
        HostCheck.True(!Check("Enter", "Foreign"));
        HostCheck.True(!Check("Foreign", "Foreign", "delegate*<void> decoy = &Enter;"));
        HostCheck.True(!Check("Foreign", "Foreign", "void Dormant() { delegate*<void> decoy = &Enter; }"));
        HostCheck.True(!Check("Enter", "Enter", fieldPrefix: "(ulong)(uint)"));
        HostCheck.True(!Check("Enter", "Enter", argumentPrefix: "(delegate*<void>)(uint)(ulong)(delegate*<void>)"));
    }

    private static CSharpSourceCatalog Create(params string[] sources)
    {
        var options = new CSharpParseOptions(LanguageVersion.CSharp14, preprocessorSymbols: ["NOSTDLIB_EFI"]);
        var documents = sources.Select((source, index) => new CSharpSourceDocument("arbitrary/" + index + ".cs",
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source))),
            CSharpSyntaxTree.ParseText(source, options, "arbitrary/" + index + ".cs")));
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path));
        return new(documents, references, compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
            allowUnsafe: true, platform: Platform.X64));
    }
}
