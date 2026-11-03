using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using OS.Tooling;
using System.Text;

internal static class HostEfiInspectionSourcesTests
{
    [HostTest("EFI logical source groups moved fields and methods while excluding owner decoys")]
    private static void PartialOwners()
    {
        var source = Catalog(
            "namespace Foreign { public static class EfiEntry { public static int wrong; } }\n"
                + "namespace OS.Boot { public static partial class EfiEntry { private static class Other { private static class Target { static int Wrong() => 0; } } private static partial class Target {} } }",
            "namespace OS.Boot { public static partial class EfiEntry { static int first = 1, second = 2; private static partial class Target { static int Read() { return 3; } } } }",
            "namespace OS.Boot { public static partial class EfiEntry { static int third = 3; } }");
        Valid(source);
        string view = HostEfiLogicalSource.View(source, "OS.Boot.EfiEntry");
        var owner = CSharpSyntaxTree.ParseText(view).GetCompilationUnitRoot().Members.OfType<ClassDeclarationSyntax>().Single();
        var fields = owner.Members.OfType<FieldDeclarationSyntax>().SelectMany(field => field.Declaration.Variables)
            .Select(variable => variable.Identifier.ValueText).ToArray();
        HostCheck.True(fields.SequenceEqual(new[] { "first", "second", "third" }));
        HostCheck.True(!view.Contains("int wrong", StringComparison.Ordinal));
        var target = HostEfiSourceRegions.TypeBody(view, "Target")!.Value;
        var read = HostEfiSourceRegions.MethodBody(view, target, "Read")!.Value;
        HostCheck.True(view[read.Start..read.End].Contains("return 3;", StringComparison.Ordinal));
        HostCheck.True(view[target.Start..target.End].Contains("Read", StringComparison.Ordinal)
            && !view[target.Start..target.End].Contains("Wrong", StringComparison.Ordinal));
    }

    [HostTest("EFI inspection regions select direct class methods and reject duplicates")]
    private static void DirectRegions()
    {
        const string text = "class EfiEntry { class Other { class Target { void Run() { Wrong(); } } } class Target { class Nested { void Run() {} } void Run() { Right(); } } }";
        var target = HostEfiSourceRegions.TypeBody(text, "Target")!.Value;
        var body = HostEfiSourceRegions.MethodBody(text, target, "Run")!.Value;
        HostCheck.Equal(" Right(); ", text[body.Start..body.End]);
        const string duplicate = "void Run() {} void Run() {}";
        Reject(() => HostEfiSourceRegions.MethodBody(duplicate, new(0, duplicate.Length), "Run"));
        foreach (string kind in new[] { "interface", "struct", "record class", "record struct" })
            HostCheck.True(HostEfiSourceRegions.TypeBody(kind + " EfiEntry {}", "EfiEntry") is null);
    }

    [HostTest("EFI logical source retains type attributes and rejects unsupported merged headers")]
    private static void HeaderPolicy()
    {
        var source = Catalog("namespace OS.Boot { [System.Obsolete] public static partial class EfiEntry {} }",
            "namespace OS.Boot { [System.CLSCompliant(false)] public static partial class EfiEntry { static int value; } }");
        Valid(source);
        string view = HostEfiLogicalSource.View(source, "OS.Boot.EfiEntry");
        var owner = CSharpSyntaxTree.ParseText(view).GetCompilationUnitRoot().Members.OfType<ClassDeclarationSyntax>().Single();
        HostCheck.Equal(2, owner.AttributeLists.Count);
        foreach (var parts in new[]
        {
            new[] { "public partial class EfiEntry {}", "public sealed partial class EfiEntry {}" },
            new[] { "public partial class EfiEntry {}", "public partial class EfiEntry : System.IDisposable { public void Dispose() {} }" },
            new[] { "public partial class EfiEntry<T> {}", "public partial class EfiEntry<T> where T : class {}" },
            new[] { "public partial class EfiEntry {}", "public partial class EfiEntry(int value) {}" }
        })
        {
            var changed = Catalog(parts.Select(part => "namespace OS.Boot { " + part + " }").ToArray());
            Valid(changed);
            string name = parts[0].Contains("<T>", StringComparison.Ordinal) ? "OS.Boot.EfiEntry`1" : "OS.Boot.EfiEntry";
            Reject(() => HostEfiLogicalSource.View(changed, name));
        }
    }

    [HostTest("EFI mutation spans tolerate trivia and preserve exact token spelling")]
    private static void MutationSpans()
    {
        const string text = "// Read();\nstring value = \"Read();\"; Read /* gap */ ( ); Read();";
        var all = new TextSpan(0, text.Length);
        var first = HostSourceMutation.Find(text, "Read();", all, requireUnique: false);
        HostCheck.Equal("Read /* gap */ ( );", text[first.Start..first.End]);
        Reject(() => HostSourceMutation.Find(text, "Read();", all, requireUnique: true));
        var tail = TextSpan.FromBounds(first.End, text.Length);
        var second = HostSourceMutation.Find(text, "Read();", tail, requireUnique: true);
        HostCheck.Equal("Read();", text[second.Start..second.End]);
        foreach (var (source, anchor) in new[] { ("\"\\u0061\"", "\"a\""), ("@value", "value"), ("0x1", "1") })
            Reject(() => HostSourceMutation.Find(source, anchor, new(0, source.Length), requireUnique: true));
    }

    [HostTest("EFI raw owner documents retain inactive and comment policy text")]
    private static void RawFamilies()
    {
        var source = Catalog("namespace OS.Boot { public static partial class EfiEntry { static int Value() { return 1; } } }",
            "namespace OS.Boot { public static partial class EfiEntry {\n#if UNSELECTED\n// forbidden-policy-token\nstatic void Hidden() {}\n#endif\n} }");
        Valid(source);
        HostCheck.True(!HostEfiLogicalSource.View(source, "OS.Boot.EfiEntry").Contains("forbidden-policy-token", StringComparison.Ordinal));
        HostCheck.True(HostSourceText.Documents(source, "OS.Boot.EfiEntry").Contains("forbidden-policy-token", StringComparison.Ordinal));
    }

    [HostTest("EFI inspection profiles preserve explicit false flags and normal inclusion")]
    private static void Profiles()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        var selected = new Dictionary<string, bool> { ["EnableM01LoadedImageStackProbe"] = false, ["EnableM01LiveAllocatorProbe"] = true };
        Reject(() => HostEfiInspectionProfiles.Create(root, selected));
        HostCheck.True(!selected["EnableM01LoadedImageStackProbe"] && selected.Count == 2);
        var diagnostic = HostSourceCatalog.Efi(EfiProfile.Create("Diagnostic", new Dictionary<string, bool>()));
        var normal = HostSourceCatalog.Efi(EfiProfile.Create("VerifiedExit", new Dictionary<string, bool>()));
        const string owner = "OS.Boot.EfiEntry+M01StageOneAdapter";
        int Publications(CSharpSourceCatalog source) => source.MethodInvocations(source.Method(owner, "Run"))
            .Count(call => call.TargetMethod.Name == "Serialize" && SymbolEqualityComparer.Default.Equals(
                call.TargetMethod.ContainingType, source.Type("OS.Boot.X64InstructionPublication")));
        HostCheck.Equal(0, Publications(diagnostic));
        HostCheck.Equal(1, Publications(normal));
    }

    private static CSharpSourceCatalog Catalog(params string[] parts) => new(parts.Select((text, index) =>
        CSharpSourceDocument.FromBytes(Path.GetFullPath("InspectionPart" + index + ".cs"), Encoding.UTF8.GetBytes(text),
            new CSharpParseOptions(LanguageVersion.CSharp14))), [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);

    private static void Valid(CSharpSourceCatalog source) => HostCheck.True(!source.Compilation.GetDiagnostics()
        .Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error), "synthetic source must bind before assertion");

    private static void Reject<T>(Func<T> inspect)
    {
        bool rejected = false;
        try
        {
            _ = inspect();
        }
        catch (InvalidOperationException)
        {
            rejected = true;
        }
        HostCheck.True(rejected, "changed scope or unsupported context must reject");
    }
}
