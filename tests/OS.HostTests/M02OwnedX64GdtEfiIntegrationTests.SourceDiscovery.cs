using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Tooling;

internal static partial class M02OwnedX64GdtEfiIntegrationTests
{
    private const string GdtEntryOwner = "OS.Boot.EfiEntry+M01StageThreeAdapter";
    private static CSharpSourceCatalog GdtSources() => HostSourceCatalog.Efi(EfiOptions.Parse(EfiProfileTestControls.ReturningArguments("M02OwnedX64GdtEncoding"), true).Profile());
    private static MethodDeclarationSyntax GdtEvidenceMethod(CSharpSourceCatalog source)
    {
        var call = HostSourceOperations.CallsTo(source, GdtEntryOwner, "WriteM02OwnedX64GdtEncodingSuccessLine").Single();
        var method = call.Ancestors().OfType<MethodDeclarationSyntax>().First();
        var symbol = source.Compilation.GetSemanticModel(method.SyntaxTree).GetDeclaredSymbol(method)!;
        HostCheck.True(SymbolEqualityComparer.Default.Equals(symbol.ContainingType, source.Type(GdtEntryOwner)) && symbol.IsStatic && symbol.Arity == 0 && symbol.ReturnsVoid && symbol.DeclaredAccessibility == Accessibility.Private, "original private static evidence owner");
        _ = source.MethodInvocations(method);
        return method;
    }

    [HostTest("m02.007 source discovery retains reviewed bytes across source renames")]
    private static void GdtOracleControls()
    {
        var source = HostSourceCatalog.Project("src/OS.Kernel/OS.Kernel.csproj");
        bool Matches(CSharpSourceCatalog candidate) => HostSourceOracle.MatchesUtf8Family(candidate, EfiContracts.OwnedGdtHostOracleTypes);
        HostCheck.True(Matches(source), "actual complete reviewed host family");
        var field = (VariableDeclaratorSyntax)source.Type("OS.Kernel.Diagnostics.OwnedX64GdtContainingNullN64BitCode").GetMembers("LogicalDescriptorCount").Single().DeclaringSyntaxReferences.Single().GetSyntax();
        var document = source.Document(field);
        string lf = document.Text.ReplaceLineEndings("\n");
        CSharpSourceCatalog Change(string path, byte[] bytes) => new(source.Documents.Select(item => item == document ? CSharpSourceDocument.FromBytes(path, bytes, (Microsoft.CodeAnalysis.CSharp.CSharpParseOptions)item.Syntax.Options) : item), source.Compilation.References, source.Compilation.AssemblyName!, source.Compilation.Options);
        HostCheck.True(Matches(Change(document.Path + ".moved.cs", System.Text.Encoding.UTF8.GetBytes(lf))), "arbitrary source filename preserves independent identity");
        HostCheck.True(Matches(Change(document.Path, System.Text.Encoding.UTF8.GetBytes(lf.ReplaceLineEndings("\r\n")))), "explicit CRLF variant");
        HostCheck.True(!Matches(Change(document.Path, [0xef, 0xbb, 0xbf, ..System.Text.Encoding.UTF8.GetBytes(lf)])), "BOM remains outside reviewed byte variants");
        HostCheck.True(!Matches(Change(document.Path, [0xff, 0xfe, ..System.Text.Encoding.Unicode.GetBytes(lf)])), "UTF16 remains outside reviewed byte variants");
        string changed = document.Text.Remove(field.Initializer!.Value.SpanStart, field.Initializer.Value.Span.Length).Insert(field.Initializer.Value.SpanStart, "3");
        var mutated = Change(document.Path, System.Text.Encoding.UTF8.GetBytes(changed));
        HostCheck.True(!mutated.Compilation.GetDiagnostics().Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error), "changed constant is valid code");
        HostCheck.True(!Matches(mutated), "changed independent result constant is rejected");
    }

    [HostTest("m02.007 evidence discovery follows partial files and rejects foreign or ambiguous callers")]
    private static void GdtEvidenceDiscoveryControls()
    {
        const string prefix = "namespace OS.Boot; public static partial class EfiEntry { private partial class M01StageThreeAdapter { ";
        const string suffix = " } }";
        const string marker = "private static void WriteM02OwnedX64GdtEncodingSuccessLine() { }";
        var parse = new Microsoft.CodeAnalysis.CSharp.CSharpParseOptions(Microsoft.CodeAnalysis.CSharp.LanguageVersion.CSharp14);
        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location)
        };
        foreach (var(signature, expected)in new[]
        {
            ("private static void ArbitrarilyNamedCaller()", true),
            ("private static void ArbitrarilyNamedCaller<T>()", false),
            ("public static void ArbitrarilyNamedCaller()", false),
            ("private void ArbitrarilyNamedCaller()", false)
        }

        )
        {
            var source = new CSharpSourceCatalog([CSharpSourceDocument.FromBytes("marker.cs", System.Text.Encoding.UTF8.GetBytes(prefix + marker + suffix), parse), CSharpSourceDocument.FromBytes("moved-caller.cs", System.Text.Encoding.UTF8.GetBytes(prefix + signature + " { WriteM02OwnedX64GdtEncodingSuccessLine(); }" + suffix), parse)], references);
            Check(source, expected);
        }

        foreach (string caller in new[]
        {
            "private static class Foreign { private static void Call() { WriteM02OwnedX64GdtEncodingSuccessLine(); } }",
            "private static void Call() { WriteM02OwnedX64GdtEncodingSuccessLine(); WriteM02OwnedX64GdtEncodingSuccessLine(); }"
        }

        )
            Check(new CSharpSourceCatalog([CSharpSourceDocument.FromBytes("foreign.cs", System.Text.Encoding.UTF8.GetBytes(prefix + marker + caller + suffix), parse)], references), false);
        static void Check(CSharpSourceCatalog source, bool expected)
        {
            HostCheck.True(!source.Compilation.GetDiagnostics().Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error), "caller control binds");
            bool accepted;
            try
            {
                _ = GdtEvidenceMethod(source);
                accepted = true;
            }
            catch (InvalidOperationException)
            {
                accepted = false;
            }

            HostCheck.True(accepted == expected, "caller ownership and signature");
        }
    }
    // This is the original raw guard-position policy, applied inside the
    // compiler-selected caller. Active token ordering is checked separately.
    private static bool GdtHeaderAfterPriorEvidence(MethodDeclarationSyntax method)
    {
        string raw = method.ToFullString();
        int prior = raw.IndexOf("WriteM02PanicSafeOwnedCom1EvidenceLine(", StringComparison.Ordinal);
        int header = prior < 0 ? -1 : raw.IndexOf("#if M02_OWNED_X64_GDT_ENCODING_PROBE", prior, StringComparison.Ordinal);
        int workspace = header < 0 ? -1 : raw.IndexOf("M02OwnedX64GdtEncodingStackWorkspace", header, StringComparison.Ordinal);
        return prior >= 0 && header > prior && workspace > header;
    }

    [HostTest("m02.007 original conditional guard follows prior evidence")]
    private static void GdtHeaderDiscoveryControls()
    {
        const string before = """
            class HeaderControl
            {
                private static void WriteM02PanicSafeOwnedCom1EvidenceLine() { }
                private static void Call()
                {
                    WriteM02PanicSafeOwnedCom1EvidenceLine();
            #if M02_OWNED_X64_GDT_ENCODING_PROBE
                    int M02OwnedX64GdtEncodingStackWorkspace = 0;
            #endif
                }
            }
            """;
        string moved = before.Replace("    WriteM02PanicSafeOwnedCom1EvidenceLine();\n#if M02_OWNED_X64_GDT_ENCODING_PROBE",
            "#if M02_OWNED_X64_GDT_ENCODING_PROBE\n    WriteM02PanicSafeOwnedCom1EvidenceLine();", StringComparison.Ordinal);
        HostCheck.True(moved != before, "header mutation applies");
        var options = new Microsoft.CodeAnalysis.CSharp.CSharpParseOptions(
            Microsoft.CodeAnalysis.CSharp.LanguageVersion.CSharp14,
            preprocessorSymbols: ["M02_OWNED_X64_GDT_ENCODING_PROBE"]);
        var references = new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) };
        foreach (var (text, expected) in new[] { (before, true), (moved, false) })
        {
            var source = new CSharpSourceCatalog([
                CSharpSourceDocument.FromBytes("relocated-header.cs", System.Text.Encoding.UTF8.GetBytes(text), options)], references);
            HostCheck.True(!source.Compilation.GetDiagnostics().Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error),
                "reordered header is valid active code");
            HostCheck.True(GdtHeaderAfterPriorEvidence(source.Method("HeaderControl", "Call")) == expected,
                "raw header retains original relative scope");
        }
    }
}
