using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Tooling;

internal static unsafe partial class M02NormalizedInterruptFramePageEfiIntegrationTests
{
    private const string FrameOwner = "OS.Boot.EfiEntry";

    private static CSharpSourceCatalog FrameSources() => HostSourceCatalog.Efi(
        EfiOptions.Parse(["--EnableM02NormalizedInterruptFramePageProbe"], true).Profile());

    private static MethodDeclarationSyntax FrameMethod(CSharpSourceCatalog source, string name, SpecialType returns)
    {
        var method = source.Method(FrameOwner, name, symbol => symbol.IsStatic && symbol.Arity == 0
            && !symbol.ReturnsByRef && !symbol.ReturnsByRefReadonly
            && symbol.DeclaredAccessibility == Accessibility.Private && symbol.ReturnType.SpecialType == returns);
        _ = source.MethodInvocations(method);
        return method;
    }

    private static MethodDeclarationSyntax FrameCaller(CSharpSourceCatalog source)
    {
        var call = HostSourceOperations.CallsTo(source, FrameOwner, "WriteM02NormalizedInterruptFramePageProbe").Single();
        var method = call.Ancestors().OfType<MethodDeclarationSyntax>().First();
        var symbol = source.Compilation.GetSemanticModel(method.SyntaxTree).GetDeclaredSymbol(method)!;
        HostCheck.True(SymbolEqualityComparer.Default.Equals(symbol.ContainingType, source.Type(FrameOwner))
            && symbol.IsStatic && symbol.Arity == 0 && symbol.ReturnsVoid
            && symbol.DeclaredAccessibility == Accessibility.Private, "original private static boot marker owner");
        _ = source.MethodInvocations(method);
        return method;
    }

    private static bool ProfileMarkers(ScriptResult result, IEnumerable<string> required,
        IEnumerable<string> ordered, IEnumerable<string> unique, IEnumerable<string> forbidden) =>
        result.ExitCode == 0
        && MarkerList(result.Output, "REQUIRED_MARKERS ").SequenceEqual(required.Distinct(StringComparer.Ordinal))
        && MarkerList(result.Output, "ORDERED_MARKERS ").SequenceEqual(ordered.Distinct(StringComparer.Ordinal))
        && MarkerList(result.Output, "UNIQUE_MARKERS ").SequenceEqual(unique.Distinct(StringComparer.Ordinal))
        && MarkerList(result.Output, "FORBIDDEN_MARKERS ").SequenceEqual(forbidden.Distinct(StringComparer.Ordinal));

    [HostTest("m02.010 source discovery retains exact raw oracle bytes across source renames")]
    private static void FrameOracleControls()
    {
        var source = HostSourceCatalog.Project("src/OS.Kernel/OS.Kernel.csproj");
        bool Matches(CSharpSourceCatalog candidate) => HostSourceOracle.MatchesRawFamily(candidate, EfiContracts.NormalizedFrameHostOracleTypes);
        HostCheck.True(Matches(source), "actual complete reviewed host family");
        var field = (VariableDeclaratorSyntax)source.Type("OS.Kernel.Diagnostics.NormalizeErrorCodeNonErrorCodeInterruptFrame")
            .GetMembers("VectorCount").Single().DeclaringSyntaxReferences.Single().GetSyntax();
        var document = source.Document(field);
        string lf = document.Text.ReplaceLineEndings("\n");
        CSharpSourceCatalog Change(string path, byte[] bytes) => new(source.Documents.Select(item => item == document
            ? CSharpSourceDocument.FromBytes(path, bytes, (CSharpParseOptions)item.Syntax.Options) : item),
            source.Compilation.References, source.Compilation.AssemblyName!, source.Compilation.Options);
        HostCheck.True(Matches(Change(document.Path + ".moved.cs", System.Text.Encoding.UTF8.GetBytes(lf))), "source name is independent of raw identity");
        HostCheck.True(!Matches(Change(document.Path, System.Text.Encoding.UTF8.GetBytes(lf.ReplaceLineEndings("\r\n")))), "original raw pin did not admit alternate newlines");
        HostCheck.True(!Matches(Change(document.Path, [0xef, 0xbb, 0xbf, ..System.Text.Encoding.UTF8.GetBytes(lf)])), "BOM rejected");
        HostCheck.True(!Matches(Change(document.Path, [0xff, 0xfe, ..System.Text.Encoding.Unicode.GetBytes(lf)])), "UTF16 rejected");
        string changed = document.Text.Remove(field.Initializer!.Value.SpanStart, field.Initializer.Value.Span.Length)
            .Insert(field.Initializer.Value.SpanStart, "255");
        var mutated = Change(document.Path, System.Text.Encoding.UTF8.GetBytes(changed));
        HostCheck.True(!mutated.Compilation.GetDiagnostics().Any(item => item.Severity == DiagnosticSeverity.Error), "changed constant is valid code");
        HostCheck.True(!Matches(mutated), "changed constant rejected");
    }

    [HostTest("m02.010 caller discovery follows partial files and rejects foreign or ambiguous callers")]
    private static void FrameCallerControls()
    {
        const string prefix = "namespace OS.Boot; public static partial class EfiEntry { ";
        const string target = "private static bool WriteM02NormalizedInterruptFramePageProbe() => true;";
        const string call = "_ = WriteM02NormalizedInterruptFramePageProbe();";
        var parse = new CSharpParseOptions(LanguageVersion.CSharp14);
        var references = new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) };
        foreach (var (caller, expected) in new[]
        {
            ("private static void ArbitraryName() { " + call + " }", true),
            ("private static void ArbitraryName<T>() { " + call + " }", false),
            ("public static void ArbitraryName() { " + call + " }", false),
            ("private static void ArbitraryName() { " + call + call + " }", false),
            ("private static class Foreign { private static void Call() { " + call + " } }", false)
        })
        {
            var source = new CSharpSourceCatalog([
                CSharpSourceDocument.FromBytes("target.cs", System.Text.Encoding.UTF8.GetBytes(prefix + target + " }"), parse),
                CSharpSourceDocument.FromBytes("moved-caller.cs", System.Text.Encoding.UTF8.GetBytes(prefix + caller + " }"), parse)
            ], references);
            HostCheck.True(!source.Compilation.GetDiagnostics().Any(item => item.Severity == DiagnosticSeverity.Error), "caller control binds");
            bool accepted;
            try
            {
                _ = FrameCaller(source);
                accepted = true;
            }
            catch (InvalidOperationException)
            {
                accepted = false;
            }

            HostCheck.True(accepted == expected, "caller ownership and signature");
        }
    }
}
