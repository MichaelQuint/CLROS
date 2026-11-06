#if !CHECKED_REFERENCE_CORE_TEST
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Tooling;
using System.Security.Cryptography;
using System.Text;

internal static partial class M04CheckedReferenceBarrierEfiIntegrationTests
{
    private static bool CheckedReferenceHostOracleMatches(CSharpSourceCatalog source) =>
        HostSourceOracle.MatchesUtf8Family(source, EfiContracts.CheckedReferenceHostOracleTypes);

    [HostTest("m04.010.efi reviewed host oracle follows types and retains byte identity")]
    private static void HostOracleSourceControls()
    {
        var source = HostSourceCatalog.Project("src/OS.Kernel/OS.Kernel.csproj");
        Need(CheckedReferenceHostOracleMatches(source), "current reviewed host family");
        const string owner = "OS.Kernel.Runtime.CheckedReferenceBulkBarriersIncludingDotNetN10Bulk";
        var field = (VariableDeclaratorSyntax)source.Type(owner).GetMembers("PageBytes").Single()
            .DeclaringSyntaxReferences.Single().GetSyntax();
        var original = source.Document(field);
        string lf = original.Text.Replace("\r\n", "\n", StringComparison.Ordinal);
        var renamed = Change(original.Path + ".moved.cs", Encoding.UTF8.GetBytes(lf));
        Need(CheckedReferenceHostOracleMatches(renamed), "arbitrary source filename");
        Need(CheckedReferenceHostOracleMatches(Change(original.Path, Encoding.UTF8.GetBytes(lf.Replace("\n", "\r\n", StringComparison.Ordinal)))),
            "explicit CRLF byte variant");
        Need(!CheckedReferenceHostOracleMatches(Change(original.Path, [0xef, 0xbb, 0xbf, .. Encoding.UTF8.GetBytes(lf)])),
            "UTF-8 BOM is not an admitted byte variant");
        Need(!CheckedReferenceHostOracleMatches(Change(original.Path, [0xff, 0xfe, .. Encoding.Unicode.GetBytes(lf)])),
            "UTF-16 is not an admitted byte variant");
        string changed = original.Text.Remove(field.Initializer!.Value.SpanStart, field.Initializer.Value.Span.Length)
            .Insert(field.Initializer.Value.SpanStart, "4095");
        var mutation = Change(original.Path, Encoding.UTF8.GetBytes(changed));
        Need(!mutation.Compilation.GetDiagnostics().Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error),
            "value mutation remains valid code");
        Need(!CheckedReferenceHostOracleMatches(mutation), "changed original constant is rejected");

        CSharpSourceCatalog Change(string path, byte[] bytes) =>
            new(source.Documents.Select(document => document == original
                    ? CSharpSourceDocument.FromBytes(path, bytes, (CSharpParseOptions)document.Syntax.Options) : document),
                source.Compilation.References, source.Compilation.AssemblyName!, source.Compilation.Options);
    }
}
#endif
