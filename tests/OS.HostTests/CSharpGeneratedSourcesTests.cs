using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;
using OS.Tooling;

internal static class CSharpGeneratedSourcesTests
{
    [HostTest("source catalog recovers checked embedded generator syntax and original byte identities")]
    private static void EmbeddedSource()
    {
        InDirectory(root =>
        {
            var options = new CSharpParseOptions(LanguageVersion.CSharp14);
            string source = Path.Combine(root, "Owner.cs");
            string intermediate = Directory.CreateDirectory(Path.Combine(root, "obj")).FullName;
            string generated = Path.Combine(intermediate, "Generated.g.cs");
            string assembly = Path.Combine(root, "sample.dll");
            // Exercise both raw and deflated embedded-source records, including a BOM.
            foreach (string padding in new[] { "", "/*" + new string('x', 4096) + "*/" })
            {
                File.WriteAllText(source, "partial class Owner { public static partial int Run(); }", new UTF8Encoding(true));
                byte[] generatedBytes = Encoding.UTF8.GetBytes("partial class Owner { public static partial int Run() => 7; }" + padding);
                Emit(assembly, source, generated, generatedBytes, embed: true);
                var original = CSharpSourceDocument.Read(source, options);
                var recovered = CSharpGeneratedSources.Read(root, assembly, intermediate, [original], options, receipt: null);
                HostCheck.Equal(1, recovered.Sources.Length);
                HostCheck.True(recovered.Sources[0].IsGenerated);
                HostCheck.Equal(Convert.ToHexString(SHA256.HashData(generatedBytes)), recovered.Sources[0].Sha256);
                HostCheck.Equal(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly))), recovered.Artifacts.AssemblySha256);
                HostCheck.Equal(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.ChangeExtension(assembly, ".pdb")))), recovered.Artifacts.SymbolsSha256);
                var catalog = new CSharpSourceCatalog([original, .. recovered.Sources], References());
                HostCheck.Equal("7", catalog.Method("Owner", "Run").ExpressionBody!.Expression.ToString());
                HostCheck.True(!catalog.Compilation.GetDiagnostics().Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
            }
        });
    }

    [HostTest("source catalog rejects stale sources mismatched or damaged symbols and unlisted non-generated files")]
    private static void InvalidArtifactJoins()
    {
        InDirectory(root =>
        {
            var options = new CSharpParseOptions(LanguageVersion.CSharp14);
            string source = Path.Combine(root, "Owner.cs");
            string intermediate = Directory.CreateDirectory(Path.Combine(root, "obj")).FullName;
            string generated = Path.Combine(intermediate, "Generated.g.cs");
            string assembly = Path.Combine(root, "sample.dll");
            string symbols = Path.ChangeExtension(assembly, ".pdb");
            File.WriteAllText(source, "partial class Owner { public static partial int Run(); }");
            byte[] body = Encoding.UTF8.GetBytes("partial class Owner { public static partial int Run() => 7; }");
            Emit(assembly, source, generated, body, embed: true);
            byte[] validSymbols = File.ReadAllBytes(symbols);
            var original = CSharpSourceDocument.Read(source, options);
            void Read() => CSharpGeneratedSources.Read(root, assembly, intermediate, [original], options, receipt: null);

            File.AppendAllText(source, " // changed");
            var changed = CSharpSourceDocument.Read(source, options);
            Reject(() => CSharpGeneratedSources.Read(root, assembly, intermediate, [changed], options, receipt: null), "source_catalog_pdb_stale_source");
            File.WriteAllText(source, original.Text);

            byte[] damaged = (byte[])validSymbols.Clone();
            damaged[^1] ^= 1;
            File.WriteAllBytes(symbols, damaged);
            Reject(Read, "source_catalog_pdb_checksum");
            File.WriteAllBytes(symbols, validSymbols);

            string other = Path.Combine(root, "other.dll");
            Emit(other, source, generated, Encoding.UTF8.GetBytes("partial class Owner { public static partial int Run() => 9; }"), embed: true);
            File.Copy(Path.ChangeExtension(other, ".pdb"), symbols, overwrite: true);
            Reject(Read, "source_catalog_pdb_identity");

            Emit(assembly, source, generated, body, embed: false);
            Reject(Read, "source_catalog_generated_source_not_embedded");
            Emit(assembly, source, Path.Combine(root, "Unlisted.cs"), body, embed: true);
            Reject(Read, "source_catalog_pdb_unlisted_source");
        });
    }

    private static void Emit(string assembly, string source, string generated, byte[] generatedBytes, bool embed)
    {
        var options = new CSharpParseOptions(LanguageVersion.CSharp14);
        byte[] sourceBytes = File.ReadAllBytes(source);
        var original = SourceText.From(sourceBytes, sourceBytes.Length, Encoding.UTF8, SourceHashAlgorithm.Sha256, canBeEmbedded: true);
        var output = SourceText.From(generatedBytes, generatedBytes.Length, Encoding.UTF8, SourceHashAlgorithm.Sha256, canBeEmbedded: true);
        var compilation = CSharpCompilation.Create("GeneratedFixture",
            [CSharpSyntaxTree.ParseText(original, options, source), CSharpSyntaxTree.ParseText(output, options, generated)],
            References(), new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, deterministic: true));
        string symbols = Path.ChangeExtension(assembly, ".pdb");
        using var pe = File.Create(assembly);
        using var pdb = File.Create(symbols);
        var emitted = compilation.Emit(pe, pdb, options: new EmitOptions(debugInformationFormat: DebugInformationFormat.PortablePdb,
            pdbFilePath: symbols, pdbChecksumAlgorithm: HashAlgorithmName.SHA256),
            embeddedTexts: embed ? [EmbeddedText.FromSource(generated, output)] : []);
        HostCheck.True(emitted.Success, string.Join("; ", emitted.Diagnostics));
    }

    private static MetadataReference[] References() => [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)];

    private static void Reject(Action action, string reason)
    {
        try
        {
            action();
        }
        catch (Exception error) when (error is InvalidOperationException or ToolFailure
            && error.Message.StartsWith(reason, StringComparison.Ordinal))
        {
            return;
        }
        throw new InvalidOperationException("expected rejection " + reason);
    }

    private static void InDirectory(Action<string> action)
    {
        string root = Directory.CreateTempSubdirectory("clros-generated-").FullName;
        try
        {
            action(root);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
