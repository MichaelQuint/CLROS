using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Tooling;

internal static partial class M02GuardedIstStacksEfiIntegrationTests
{
    private const string IstEntryOwner = "OS.Boot.EfiEntry+M01StageThreeAdapter";

    private static CSharpSourceCatalog IstSources() =>
        HostSourceCatalog.Efi(EfiOptions.Parse(
            EfiProfileTestControls.ReturningArguments("M02GuardedIstStackAllocation"), true).Profile());

    private static MethodDeclarationSyntax IstEvidenceMethod(CSharpSourceCatalog source)
    {
        var call = HostSourceOperations.CallsTo(source, IstEntryOwner, "WriteM02GuardedIstStackAllocationSuccessLine").Single();
        var method = call.Ancestors().OfType<MethodDeclarationSyntax>().First();
        var symbol = source.Compilation.GetSemanticModel(method.SyntaxTree).GetDeclaredSymbol(method)!;
        HostCheck.True(SymbolEqualityComparer.Default.Equals(symbol.ContainingType, source.Type(IstEntryOwner))
            && symbol.IsStatic && symbol.Arity == 0 && symbol.ReturnsVoid
            && symbol.DeclaredAccessibility == Accessibility.Private, "original private static evidence owner");
        _ = source.MethodInvocations(method);
        return method;
    }
    [HostTest("m02.008 source discovery retains reviewed bytes across source renames")]
    private static void IstOracleControls()
    {
        var source = HostSourceCatalog.Project("src/OS.Kernel/OS.Kernel.csproj");
        bool Matches(CSharpSourceCatalog candidate) => HostSourceOracle.MatchesUtf8Family(candidate, EfiContracts.GuardedIstHostOracleTypes);
        HostCheck.True(Matches(source), "actual complete reviewed host family");
        var field = (VariableDeclaratorSyntax)source.Type("OS.Kernel.Diagnostics.GuardedISTStacksDoubleFaultPageFaultNMI").GetMembers("PageSize").Single().DeclaringSyntaxReferences.Single().GetSyntax();
        var document = source.Document(field);
        string lf = document.Text.ReplaceLineEndings("\n");
        CSharpSourceCatalog Change(string path, byte[] bytes) => new(source.Documents.Select(item => item == document ? CSharpSourceDocument.FromBytes(path, bytes, (Microsoft.CodeAnalysis.CSharp.CSharpParseOptions)item.Syntax.Options) : item), source.Compilation.References, source.Compilation.AssemblyName!, source.Compilation.Options);
        HostCheck.True(Matches(Change(document.Path + ".moved.cs", System.Text.Encoding.UTF8.GetBytes(lf))), "arbitrary source filename preserves independent identity");
        HostCheck.True(Matches(Change(document.Path, System.Text.Encoding.UTF8.GetBytes(lf.ReplaceLineEndings("\r\n")))), "explicit CRLF variant");
        HostCheck.True(!Matches(Change(document.Path, [0xef, 0xbb, 0xbf, ..System.Text.Encoding.UTF8.GetBytes(lf)])), "BOM remains outside reviewed byte variants");
        HostCheck.True(!Matches(Change(document.Path, [0xff, 0xfe, ..System.Text.Encoding.Unicode.GetBytes(lf)])), "UTF16 remains outside reviewed byte variants");
        string changed = document.Text.Remove(field.Initializer!.Value.SpanStart, field.Initializer.Value.Span.Length).Insert(field.Initializer.Value.SpanStart, "4095");
        var mutated = Change(document.Path, System.Text.Encoding.UTF8.GetBytes(changed));
        HostCheck.True(!mutated.Compilation.GetDiagnostics().Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error), "changed constant is valid code");
        HostCheck.True(!Matches(mutated), "changed independent result constant is rejected");
    }

    [HostTest("m02.008 evidence discovery follows partial files and rejects foreign or ambiguous callers")]
    private static void IstEvidenceDiscoveryControls()
    {
        const string prefix = "namespace OS.Boot; public static partial class EfiEntry { private partial class M01StageThreeAdapter { ";
        const string suffix = " } }";
        const string marker = "private static void WriteM02GuardedIstStackAllocationSuccessLine() { }";
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
            var source = new CSharpSourceCatalog([CSharpSourceDocument.FromBytes("marker.cs", System.Text.Encoding.UTF8.GetBytes(prefix + marker + suffix), parse), CSharpSourceDocument.FromBytes("moved-caller.cs", System.Text.Encoding.UTF8.GetBytes(prefix + signature + " { WriteM02GuardedIstStackAllocationSuccessLine(); }" + suffix), parse)], references);
            Check(source, expected);
        }

        foreach (string caller in new[]
        {
            "private static class Foreign { private static void Call() { WriteM02GuardedIstStackAllocationSuccessLine(); } }",
            "private static void Call() { WriteM02GuardedIstStackAllocationSuccessLine(); WriteM02GuardedIstStackAllocationSuccessLine(); }"
        }

        )
            Check(new CSharpSourceCatalog([CSharpSourceDocument.FromBytes("foreign.cs", System.Text.Encoding.UTF8.GetBytes(prefix + marker + caller + suffix), parse)], references), false);
        static void Check(CSharpSourceCatalog source, bool expected)
        {
            HostCheck.True(!source.Compilation.GetDiagnostics().Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error), "caller control binds");
            bool accepted;
            try
            {
                _ = IstEvidenceMethod(source);
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
