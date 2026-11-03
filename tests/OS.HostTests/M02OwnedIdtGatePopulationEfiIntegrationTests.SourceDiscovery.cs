using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Tooling;

internal static partial class M02OwnedIdtGatePopulationEfiIntegrationTests
{
    private const string IdtOwner = "OS.Boot.EfiEntry";

    private static CSharpSourceCatalog IdtSources() => HostSourceCatalog.Efi(
        EfiOptions.Parse(["--EnableM02OwnedIdtGatePopulationProbe"], true).Profile());

    private static MethodDeclarationSyntax IdtMethod(CSharpSourceCatalog source, string name, SpecialType returns)
    {
        var method = source.Method(IdtOwner, name, symbol => symbol.IsStatic && symbol.Arity == 0
            && !symbol.ReturnsByRef && !symbol.ReturnsByRefReadonly
            && symbol.DeclaredAccessibility == Accessibility.Private && symbol.ReturnType.SpecialType == returns);
        _ = source.MethodInvocations(method);
        return method;
    }

    private static MethodDeclarationSyntax IdtCaller(CSharpSourceCatalog source)
    {
        var call = HostSourceOperations.CallsTo(source, IdtOwner, "WriteM02OwnedIdtGatePopulationProbe").Single();
        var method = call.Ancestors().OfType<MethodDeclarationSyntax>().First();
        var symbol = source.Compilation.GetSemanticModel(method.SyntaxTree).GetDeclaredSymbol(method)!;
        HostCheck.True(SymbolEqualityComparer.Default.Equals(symbol.ContainingType, source.Type(IdtOwner))
            && symbol.IsStatic && symbol.Arity == 0 && symbol.ReturnsVoid
            && symbol.DeclaredAccessibility == Accessibility.Private, "original private static boot marker owner");
        _ = source.MethodInvocations(method);
        return method;
    }

    [HostTest("m02.011 source discovery retains exact raw oracle bytes across source renames")]
    private static void IdtOracleControls()
    {
        var source = HostSourceCatalog.Project("src/OS.Kernel/OS.Kernel.csproj");
        bool Matches(CSharpSourceCatalog candidate) => HostSourceOracle.MatchesRawFamily(candidate, EfiContracts.OwnedIdtHostOracleTypes);
        HostCheck.True(Matches(source), "actual complete reviewed host family");
        var field = (VariableDeclaratorSyntax)source.Type("OS.Kernel.Diagnostics.PopulateAllN256IDTVectorsOwnedGatesNever")
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

    [HostTest("m02.011 caller discovery follows partial files and rejects foreign or ambiguous callers")]
    private static void IdtCallerControls()
    {
        const string prefix = "namespace OS.Boot; public static partial class EfiEntry { ";
        const string target = "private static bool WriteM02OwnedIdtGatePopulationProbe() => true;";
        const string call = "_ = WriteM02OwnedIdtGatePopulationProbe();";
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
                _ = IdtCaller(source);
                accepted = true;
            }
            catch (InvalidOperationException)
            {
                accepted = false;
            }

            HostCheck.True(accepted == expected, "caller ownership and signature");
        }
    }
    private static string IdtConstants(CSharpSourceCatalog source)
    {
        var owner = source.Type(IdtOwner);
        return string.Join("\n", new[]
        {
            "M02OwnedIdtGateAllocateAnyPages", "M02OwnedIdtGateEfiLoaderData",
            "M02OwnedIdtGateAllocationPages", "M02OwnedIdtGatePageBytes",
            "M02OwnedIdtGateArenaBytes", "M02OwnedIdtGateStubExpectedFnv1A64",
            "M02OwnedIdtGateNormalizedPolicyFnv1A64"
        }.Select(name =>
        {
            var field = owner.GetMembers(name).OfType<IFieldSymbol>().Single();
            HostCheck.True(field.IsConst && field.DeclaredAccessibility == Accessibility.Private, "original private constant");
            var declaration = field.DeclaringSyntaxReferences.Single().GetSyntax().Ancestors().OfType<FieldDeclarationSyntax>().First();
            return declaration.ToString();
        }));
    }
}
