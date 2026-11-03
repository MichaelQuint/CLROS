using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Tooling;
using System.Text;

internal static class HostEfiRawSourcesTests
{
    [HostTest("EFI raw bodies retain inactive policy text and compiler part order")]
    private static void RawBodies()
    {
        const string omitted = "#if KNOWN_FALSE\nnamespace N { partial class Owner { int omitted; } }\n#endif";
        var source = Catalog(
            "namespace Foreign { class Owner { int decoy; } } namespace N {\n#if UNKNOWN\n[System.Obsolete]\n#endif\npartial class Owner { // retained-comment\n#if UNKNOWN\nint inactive;\n#endif\nint first; } partial class Owner { int second; } }",
            omitted,
            "#if UNKNOWN\nnamespace N { partial class Owner { int third; } }\n#endif");
        string raw = HostEfiRawTypes.Bodies(source, "N.Owner", text => text.Replace(omitted, string.Empty, StringComparison.Ordinal));
        HostCheck.True(raw.Contains("retained-comment", StringComparison.Ordinal) && raw.Contains("inactive", StringComparison.Ordinal));
        HostCheck.True(!raw.Contains("decoy", StringComparison.Ordinal) && !raw.Contains("omitted", StringComparison.Ordinal));
        HostCheck.True(!raw.Contains("System.Obsolete", StringComparison.Ordinal));
        HostCheck.Equal(1, raw.Split("int first;", StringSplitOptions.None).Length - 1);
        HostCheck.True(raw.IndexOf("first", StringComparison.Ordinal) < raw.IndexOf("second", StringComparison.Ordinal)
            && raw.IndexOf("second", StringComparison.Ordinal) < raw.IndexOf("third", StringComparison.Ordinal));
        string declarations = HostEfiRawTypes.Declarations(source, "N.Owner");
        HostCheck.True(declarations.Contains("System.Obsolete", StringComparison.Ordinal));
        HostCheck.Equal(1, declarations.Split("int first;", StringSplitOptions.None).Length - 1);
    }

    [HostTest("EFI raw method selection excludes nested decoys and retains inactive body text")]
    private static void DirectRawMethods()
    {
        const string body = " // comment\n#if UNKNOWN\nForbidden();\n#else\nAllowed();\n#endif\nreturn 1; ";
        string owner = "class Nested { int Read() { return 9; } } int Read() {" + body + "} void TryRaw() {} void TryRaw(int value) {}";
        HostCheck.Equal(body, HostEfiRawTypes.MethodBody(owner, "Read"));
        HostCheck.True(HostEfiRawTypes.MethodBody(owner, "Missing") is null);
        const string attributed = "\n#if UNKNOWN\n[System.Obsolete]\n#endif\nint Read() { return 1; }";
        HostCheck.Equal(" return 1; ", HostEfiRawTypes.MethodBody(attributed, "Read"));
    }

    [HostTest("EFI raw method selection rejects requested overloads and conditional variants")]
    private static void AmbiguousRawMethods()
    {
        foreach (string body in new[]
        {
            "void Read() {} void Read(int value) {}",
            "\n#if UNKNOWN\nvoid Read() {}\n#else\nvoid Read() {}\n#endif\n",
            "void Read() {} void @Read() {}",
            "void Read()\n#if UNKNOWN\n{\n#endif\n#if !UNKNOWN\n{\n#endif\n}",
            "void Read() => Other();",
            "\n#if UNKNOWN\nvoid Read( {}\n#endif\nvoid Other() {}"
        })
            Reject(() => HostEfiRawTypes.MethodBody(body, "Read"));
    }

    [HostTest("EFI raw type bodies reject overlapping conditional headers and malformed witnesses")]
    private static void UnsupportedRawBodies()
    {
        foreach (string text in new[]
        {
            "namespace N {\n#if UNKNOWN\npartial class Owner {\n#else\npartial class Owner {\n#endif\nint value; } }",
            "namespace N { partial class Owner {\n#if UNKNOWN\nvoid Broken( {}\n#endif\n} }"
        })
            Reject(() => HostEfiRawTypes.Bodies(Catalog(text), "N.Owner", static text => text));
    }

    [HostTest("EFI guard mutations target the nearest dominating branch and preserve other guards")]
    private static void GuardSelection()
    {
        foreach (string ending in new[] { "\n", "\r\n" })
        {
            string text = "#if FLAG\npartial class C {}\n#endif\npartial class C { string decoy = \"#if FLAG\"; // #if FLAG\n#if FLAG // keep comment\nvoid Target() {}\n#endif\n}";
            text = text.Replace("\n", ending, StringComparison.Ordinal);
            var document = Document(text, "FLAG");
            var changed = HostEfiConditionalMutation.OpenGuard(document, Target(document), "FLAG");
            HostCheck.True(changed.Text.StartsWith("#if FLAG" + ending + "partial class C {}", StringComparison.Ordinal));
            HostCheck.True(changed.Text.Contains("#if true // FLAG // keep comment", StringComparison.Ordinal));
            var off = Document(changed.Text);
            HostCheck.True(Target(off) is not null);
        }
        var repeated = Document("class C {\n#if FLAG\n#if FLAG\nvoid Target() {}\n#endif\n#endif\n}", "FLAG");
        string inner = HostEfiConditionalMutation.OpenGuard(repeated, Target(repeated), "FLAG").Text;
        HostCheck.True(inner.Contains("#if FLAG\n#if true // FLAG", StringComparison.Ordinal));
        // Opening one branch does not assert that outer conditions have opened.
        HostCheck.True(!Document(inner).Syntax.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Any());
    }

    [HostTest("EFI guard mutations reject unsupported branches and partial target coverage")]
    private static void GuardBoundaries()
    {
        foreach (var (text, symbols) in new (string, string[])[]
        {
            ("class C { void Target() {} }", []),
            ("class C {\n#if FLAG && OTHER\nvoid Target() {}\n#endif\n}", ["FLAG", "OTHER"]),
            ("class C {\n#if FLAG\n#if OTHER\nvoid Target() {}\n#endif\n#endif\n}", ["FLAG", "OTHER"]),
            ("class C {\n#if OTHER\nvoid Other() {}\n#else\nvoid Target() {}\n#endif\n}", []),
            ("class C {\n#if OTHER\nvoid Other() {}\n#elif FLAG\nvoid Target() {}\n#endif\n}", ["FLAG"]),
            ("class C {\n#if FLAG\n[System.Obsolete]\n#endif\nvoid Target() {}\n}", ["FLAG"])
        })
        {
            var document = Document(text, symbols);
            Reject(() => HostEfiConditionalMutation.OpenGuard(document, Target(document), "FLAG"));
        }
        var partial = Document("class C { void Target() {\n#if FLAG\nCall(\n#endif\n0); } }", "FLAG");
        var call = partial.Syntax.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().Single();
        Reject(() => HostEfiConditionalMutation.OpenGuard(partial, call, "FLAG"));
        var original = Document("class C {\n#if FLAG\nvoid Target() {}\n#endif\n}", "FLAG");
        Reject(() => HostEfiConditionalMutation.OpenGuard(Document(original.Text, "FLAG"), Target(original), "FLAG"));
    }

    private static MethodDeclarationSyntax Target(CSharpSourceDocument document) => document.Syntax.GetRoot().DescendantNodes()
        .OfType<MethodDeclarationSyntax>().Single(method => method.Identifier.ValueText == "Target");

    private static CSharpSourceDocument Document(string text, params string[] symbols)
    {
        var document = CSharpSourceDocument.FromBytes(Path.GetFullPath("RawInspection.cs"), Encoding.UTF8.GetBytes(text),
            new CSharpParseOptions(LanguageVersion.CSharp14, preprocessorSymbols: symbols));
        HostCheck.True(!document.Syntax.GetDiagnostics().Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        return document;
    }

    private static CSharpSourceCatalog Catalog(params string[] parts) => new(parts.Select((text, index) =>
        CSharpSourceDocument.FromBytes(Path.GetFullPath("RawPart" + index + ".cs"), Encoding.UTF8.GetBytes(text),
            new CSharpParseOptions(LanguageVersion.CSharp14))), [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);

    private static void Reject<T>(Func<T> action)
    {
        try
        {
            _ = action();
        }
        catch (InvalidOperationException)
        {
            return;
        }
        throw new InvalidOperationException("unsupported raw source or guard must reject");
    }
}
