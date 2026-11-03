using System.Buffers.Binary;
using System.IO.Compression;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using Microsoft.CodeAnalysis.CSharp;

namespace OS.Tooling;

/// <summary>Recovers embedded generator outputs from a matching portable PDB.
/// The PE/PDB identity, source checksums and evaluated source reads must agree;
/// missing, stale or unsupported artifacts fail with a rebuild requirement.
/// No assembly or generator is loaded or executed.</summary>
internal static class CSharpGeneratedSources
{
    private static readonly Guid EmbeddedSource = new("0e8a571b-6926-466e-b4ad-8ab04611f5fe");
    private static readonly Guid Sha256 = new("8829d00f-11b8-4213-878b-770e8597ac16");

    internal static (CSharpSourceDocument[] Sources, CSharpCompilerArtifacts Artifacts) Read(
        string root, string assemblyPath, string intermediate, IReadOnlyList<CSharpSourceDocument> sources,
        CSharpParseOptions options, CSharpCompilationReceipt? receipt)
    {
        string symbolsPath = Path.ChangeExtension(assemblyPath, ".pdb");
        foreach (string path in new[] { assemblyPath, symbolsPath })
        {
            _ = EfiEvidence.RelativePath(root, path, "source_catalog_artifact_outside_root");
            AuthorityFiles.RequirePlainPath(root, path, "source_catalog_artifact_link");
            if (!File.Exists(path))
                throw new InvalidOperationException("source_catalog_artifact_missing rebuild_required " + path);
        }
        byte[] assemblyBytes = File.ReadAllBytes(assemblyPath);
        byte[] symbolsBytes = File.ReadAllBytes(symbolsPath);
        var artifacts = new CSharpCompilerArtifacts(assemblyPath, Hash(assemblyBytes), symbolsPath, Hash(symbolsBytes));
        receipt?.VerifyArtifacts(artifacts);
        if (receipt is not null)
            artifacts = artifacts with { ReceiptPath = receipt.Path, ReceiptSha256 = receipt.Sha256 };
        using var pe = new PEReader(new MemoryStream(assemblyBytes, writable: false));
        using var provider = MetadataReaderProvider.FromPortablePdbStream(new MemoryStream(symbolsBytes, writable: false));
        var reader = provider.GetMetadataReader();
        var header = reader.DebugMetadataHeader ?? throw new InvalidOperationException("source_catalog_pdb_header");
        var identity = new BlobContentId(header.Id);
        var entries = pe.ReadDebugDirectory();
        var codeView = entries.Where(entry => entry.Type == DebugDirectoryEntryType.CodeView).ToArray();
        var checksums = entries.Where(entry => entry.Type == DebugDirectoryEntryType.PdbChecksum).ToArray();
        if (codeView.Length != 1 || checksums.Length != 1)
            throw new InvalidOperationException("source_catalog_pdb_debug_entries");
        var view = pe.ReadCodeViewDebugDirectoryData(codeView[0]);
        if (view.Guid != identity.Guid || codeView[0].Stamp != identity.Stamp || view.Age != 1)
            throw new InvalidOperationException("source_catalog_pdb_identity rebuild_required");
        var checksum = pe.ReadPdbChecksumDebugDirectoryData(checksums[0]);
        // Portable-PDB content hashing zeroes the 20-byte content ID.
        byte[] content = (byte[])symbolsBytes.Clone();
        content.AsSpan(header.IdStartOffset, header.Id.Length).Clear();
        if (checksum.AlgorithmName != "SHA256" || !SHA256.HashData(content).AsSpan().SequenceEqual(checksum.Checksum.AsSpan()))
            throw new InvalidOperationException("source_catalog_pdb_checksum rebuild_required");

        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var evaluated = sources.ToDictionary(source => Path.GetFullPath(source.Path), comparer);
        HashSet<string> seen = new(comparer);
        List<CSharpSourceDocument> generated = [];
        foreach (var handle in reader.Documents)
        {
            var document = reader.GetDocument(handle);
            string path = Path.GetFullPath(reader.GetString(document.Name));
            if (!seen.Add(path))
                throw new InvalidOperationException("source_catalog_pdb_duplicate_document " + path);
            if (reader.GetGuid(document.HashAlgorithm) != Sha256)
                throw new InvalidOperationException("source_catalog_pdb_source_hash_algorithm " + path);
            string expected = Convert.ToHexString(reader.GetBlobBytes(document.Hash));
            if (evaluated.TryGetValue(path, out var known))
            {
                if (!expected.Equals(known.Sha256, StringComparison.Ordinal))
                    throw new InvalidOperationException("source_catalog_pdb_stale_source rebuild_required " + path);
                continue;
            }
            _ = EfiEvidence.RelativePath(intermediate, path, "source_catalog_pdb_unlisted_source");
            var embedded = reader.GetCustomDebugInformation(handle).Select(reader.GetCustomDebugInformation)
                .Where(info => reader.GetGuid(info.Kind) == EmbeddedSource).ToArray();
            if (embedded.Length != 1)
                throw new InvalidOperationException("source_catalog_generated_source_not_embedded " + path);
            byte[] bytes = Decode(reader.GetBlobBytes(embedded[0].Value));
            if (Hash(bytes) != expected)
                throw new InvalidOperationException("source_catalog_generated_source_checksum " + path);
            generated.Add(CSharpSourceDocument.FromBytes(path, bytes, options) with { IsGenerated = true });
        }
        return (generated.ToArray(), artifacts);
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    private static byte[] Decode(byte[] blob)
    {
        if (blob.Length < sizeof(int))
            throw new InvalidOperationException("source_catalog_embedded_source_header");
        int length = BinaryPrimitives.ReadInt32LittleEndian(blob);
        if (length == 0)
        {
            if (blob.Length - sizeof(int) > 16 * 1024 * 1024)
                throw new InvalidOperationException("source_catalog_embedded_source_size");
            return blob[sizeof(int)..];
        }
        // Bound allocations when inspecting a malformed or incompatible PDB.
        if (length < 0 || length > 16 * 1024 * 1024)
            throw new InvalidOperationException("source_catalog_embedded_source_size");
        byte[] bytes = new byte[length];
        using var compressed = new MemoryStream(blob, sizeof(int), blob.Length - sizeof(int), writable: false);
        using var stream = new DeflateStream(compressed, CompressionMode.Decompress);
        stream.ReadExactly(bytes);
        if (stream.ReadByte() != -1)
            throw new InvalidOperationException("source_catalog_embedded_source_size");
        return bytes;
    }
}
