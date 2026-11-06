#if !ACPI_BOOTSTRAP_HOST
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Tooling;
using System.Text;

internal static partial class M03AcpiBootstrapCopyEfiIntegrationTests
{
    private static bool AcpiHostOracleMatches(CSharpSourceCatalog source) =>
        HostSourceOracle.MatchesUtf8Family(source, EfiContracts.AcpiBootstrapHostOracleTypes);

    [HostTest("m03.002.efi reviewed host oracle follows types and retains byte identity")]
    private static void HostOracleSourceControls()
    {
        var source = HostSourceCatalog.Project("src/OS.Kernel/OS.Kernel.csproj");
        Need(AcpiHostOracleMatches(source), "current reviewed host family");
        const string owner = "OS.Boot.RSDPSelectedRSDTXSDTMCFGMADTHPETBootstrap";
        var field = (VariableDeclaratorSyntax)source.Type(owner).GetMembers("RsdpSignature").Single()
            .DeclaringSyntaxReferences.Single().GetSyntax();
        var original = source.Document(field);
        string lf = original.Text.Replace("\r\n", "\n", StringComparison.Ordinal);
        Need(AcpiHostOracleMatches(Change(original.Path + ".moved.cs", Encoding.UTF8.GetBytes(lf))),
            "arbitrary source filename");
        Need(AcpiHostOracleMatches(Change(original.Path, Encoding.UTF8.GetBytes(lf.Replace("\n", "\r\n", StringComparison.Ordinal)))),
            "explicit CRLF byte variant");
        Need(!AcpiHostOracleMatches(Change(original.Path, [0xef, 0xbb, 0xbf, .. Encoding.UTF8.GetBytes(lf)])),
            "UTF-8 BOM is not an admitted byte variant");
        Need(!AcpiHostOracleMatches(Change(original.Path, [0xff, 0xfe, .. Encoding.Unicode.GetBytes(lf)])),
            "UTF-16 is not an admitted byte variant");
        string changed = original.Text.Remove(field.Initializer!.Value.SpanStart, field.Initializer.Value.Span.Length)
            .Insert(field.Initializer.Value.SpanStart, "0");
        var mutation = Change(original.Path, Encoding.UTF8.GetBytes(changed));
        Need(!mutation.Compilation.GetDiagnostics().Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error),
            "value mutation remains valid code");
        Need(!AcpiHostOracleMatches(mutation), "changed original constant is rejected");

        CSharpSourceCatalog Change(string path, byte[] bytes) =>
            new(source.Documents.Select(document => document == original
                    ? CSharpSourceDocument.FromBytes(path, bytes, (CSharpParseOptions)document.Syntax.Options) : document),
                source.Compilation.References, source.Compilation.AssemblyName!, source.Compilation.Options);
    }
}
#endif
