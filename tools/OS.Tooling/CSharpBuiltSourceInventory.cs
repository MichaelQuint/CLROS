using System.Security.Cryptography;
using System.Xml;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace OS.Tooling;

/// <summary>Discovers the exact ordered Compile inventory of an already-built
/// host assembly. The companion receipt is bound to its PE/PDB and every current
/// declared compiler input. This does not evaluate projects or execute build
/// targets. It is a host-run input snapshot, not native admission evidence.</summary>
internal sealed record CSharpBuiltSourceInventory(IReadOnlyList<CSharpSourceDocument> Sources,
    CSharpParseOptions ParseOptions, IReadOnlyDictionary<string, string> InputHashes,
    IReadOnlyDictionary<string, string> NonSourceHashes, IReadOnlyList<string> Arguments,
    CSharpCompilerArtifacts Artifacts)
{
    internal CSharpSourceCatalog Catalog { get; private init; } = null!;

    internal static CSharpBuiltSourceInventory Read(string root, string assembly)
    {
        string receiptPath = Path.ChangeExtension(assembly, ".host-compilation.xml");
        var receipt = CSharpCompilationReceipt.ReadBuilt(root, receiptPath);
        string symbols = Path.ChangeExtension(assembly, ".pdb");
        foreach (string path in new[] { assembly, symbols })
        {
            _ = EfiEvidence.RelativePath(root, path, "source_catalog_artifact_outside_root");
            AuthorityFiles.RequirePlainPath(root, path, "source_catalog_artifact_link");
        }
        var artifacts = new CSharpCompilerArtifacts(assembly, Hash(assembly), symbols, Hash(symbols))
        {
            ReceiptPath = receiptPath,
            ReceiptSha256 = receipt.Sha256
        };
        receipt.VerifyArtifacts(artifacts);
        string project = receipt.Inputs.DocumentElement!.GetAttribute("Project");
        _ = EfiEvidence.RelativePath(root, project, "source_catalog_project_outside_root");
        var arguments = CSharpCommandLineParser.Default.Parse(receipt.Arguments, Path.GetDirectoryName(project)!, sdkDirectory: null);
        if (arguments.Errors.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
            throw new InvalidOperationException("source_catalog_compiler_arguments");
        var sources = arguments.SourceFiles.Select(source =>
        {
            _ = EfiEvidence.RelativePath(root, source.Path, "source_catalog_source_outside_root");
            AuthorityFiles.RequirePlainPath(root, source.Path, "source_catalog_source_link");
            return CSharpSourceDocument.Read(source.Path, arguments.ParseOptions);
        }).ToArray();
        receipt.VerifySources(sources);
        var generated = CSharpGeneratedSources.Read(root, assembly, arguments.OutputDirectory,
            sources, arguments.ParseOptions, receipt);
        var references = arguments.MetadataReferences.Select(reference =>
            receipt.ReadReference(Path.GetFullPath(reference.Reference, Path.GetDirectoryName(project)!),
                reference.Properties)).ToArray();
        string? identity = Path.GetFileNameWithoutExtension(arguments.OutputFileName);
        if (string.IsNullOrEmpty(identity) || identity != Path.GetFileNameWithoutExtension(assembly))
            throw new InvalidOperationException("source_catalog_assembly_identity");
        var catalog = new CSharpSourceCatalog([.. sources, .. generated.Sources], references,
            identity, arguments.CompilationOptions);
        receipt.VerifyFilesUnchanged();
        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        Dictionary<string, string> inputs = new(comparer), shared = new(comparer);
        foreach (XmlElement file in receipt.Inputs.SelectNodes("/Inputs/*/File")!)
        {
            string path = file.GetAttribute("Path"), hash = file.GetAttribute("Sha256");
            if (inputs.TryGetValue(path, out string? previous) && previous != hash)
                throw new InvalidOperationException("source_catalog_conflicting_input_hash");
            inputs[path] = hash;
            if (file.GetAttribute("Kind") != "Source")
                shared[path] = hash;
        }
        inputs[receiptPath] = receipt.Sha256;
        inputs[assembly] = artifacts.AssemblySha256;
        inputs[symbols] = artifacts.SymbolsSha256;
        return new(Array.AsReadOnly(sources), arguments.ParseOptions, inputs, shared, receipt.Arguments, artifacts)
        {
            Catalog = catalog
        };
    }

    private static string Hash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
}
