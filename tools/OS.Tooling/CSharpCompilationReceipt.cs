using System.Security.Cryptography;
using System.Text;
using System.Xml;
using Microsoft.CodeAnalysis;

namespace OS.Tooling;

/// <summary>Joins host generated-source discovery to a successful compiler
/// invocation's declared inputs and arguments. The producer must remain frozen;
/// this receipt is not a sandbox or a native-build admission record.</summary>
internal sealed record CSharpCompilationReceipt(string Path, string Sha256, XmlDocument Inputs,
    string AssemblySha256, string SymbolsSha256)
{
    internal IReadOnlyList<string> Arguments { get; private init; } = [];

    // A runner inspects the build it is executing, rather than evaluating a new
    // project context. Its companion receipt is joined to that exact PE/PDB by
    // CSharpBuiltSourceInventory before any source inventory can be consumed.
    internal static CSharpCompilationReceipt ReadBuilt(string root, string path)
    {
        byte[] bytes = ReadBytes(root, path);
        using var stream = new MemoryStream(bytes, writable: false);
        var receipt = Parse(stream);
        string inputs = receipt.DocumentElement?.SelectSingleNode("Inputs")?.OuterXml ?? "";
        string[] arguments = receipt.SelectNodes("/HostCompilationReceipt/Arguments/Argument")!
            .Cast<XmlElement>().Select(argument => argument.InnerText).ToArray();
        return ReadSnapshot(path, bytes, inputs, arguments);
    }

    internal static CSharpCompilationReceipt Read(string root, string path, string currentInputs, IReadOnlyList<string> arguments)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(currentInputs))
            throw new InvalidOperationException("source_catalog_receipt_discovery_missing rebuild_required");
        return ReadSnapshot(path, ReadBytes(root, path), currentInputs, arguments);
    }

    private static byte[] ReadBytes(string root, string path)
    {
        _ = EfiEvidence.RelativePath(root, path, "source_catalog_receipt_outside_root");
        if (!File.Exists(path))
            throw new InvalidOperationException("source_catalog_receipt_missing rebuild_required");
        AuthorityFiles.RequirePlainPath(root, path, "source_catalog_receipt_link");
        return File.ReadAllBytes(path);
    }

    private static CSharpCompilationReceipt ReadSnapshot(string path, byte[] bytes, string currentInputs, IReadOnlyList<string> arguments)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        var receipt = Parse(stream);
        var element = receipt.DocumentElement;
        if (element?.Name != "HostCompilationReceipt" || element.GetAttribute("Version") != "1"
            || element.GetAttribute("Producer") != "SdkTargetsTriggeredByCompilation"
            || element.ChildNodes.Count != 3)
            throw new InvalidOperationException("source_catalog_receipt_schema");
        using var inputStream = new MemoryStream(Encoding.UTF8.GetBytes(currentInputs), writable: false);
        var inputs = Parse(inputStream);
        if (inputs.DocumentElement?.Name != "Inputs" || inputs.DocumentElement.ChildNodes.Count == 0
            || element.SelectSingleNode("Inputs")?.OuterXml != inputs.DocumentElement.OuterXml
            || element.GetAttribute("InputSha256") != Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(inputs.DocumentElement.OuterXml))))
            throw new InvalidOperationException("source_catalog_receipt_inputs rebuild_required");
        string[] recordedArguments = element.SelectNodes("Arguments/Argument")!.Cast<XmlElement>()
            .Select(argument => argument.InnerText).ToArray();
        if (arguments.Count == 0 || !arguments.SequenceEqual(recordedArguments, StringComparer.Ordinal))
            throw new InvalidOperationException("source_catalog_receipt_arguments rebuild_required");
        var outputs = element.SelectNodes("Outputs/File")!.Cast<XmlElement>().ToArray();
        if (outputs.Length != 2 || outputs.Count(file => file.GetAttribute("Kind") == "PE") != 1
            || outputs.Count(file => file.GetAttribute("Kind") == "PDB") != 1)
            throw new InvalidOperationException("source_catalog_receipt_outputs");
        string OutputHash(string kind)
        {
            string hash = outputs.Single(file => file.GetAttribute("Kind") == kind).GetAttribute("Sha256");
            if (hash.Length != 64 || !hash.All(Uri.IsHexDigit))
                throw new InvalidOperationException("source_catalog_receipt_output_hash");
            return hash;
        }
        return new(path, Convert.ToHexString(SHA256.HashData(bytes)), inputs, OutputHash("PE"), OutputHash("PDB"))
        {
            Arguments = Array.AsReadOnly(recordedArguments)
        };
    }

    internal void VerifyArtifacts(CSharpCompilerArtifacts artifacts)
    {
        if (artifacts.AssemblySha256 != AssemblySha256 || artifacts.SymbolsSha256 != SymbolsSha256)
            throw new InvalidOperationException("source_catalog_receipt_artifact_mismatch rebuild_required");
    }

    internal void VerifySources(IReadOnlyList<CSharpSourceDocument> sources)
    {
        var expected = Inputs.SelectNodes("/Inputs/Declared/File[@Kind='Source']")!.Cast<XmlElement>().ToArray();
        if (sources.Count != expected.Length || sources.Where((source, index) =>
                source.Path != expected[index].GetAttribute("Path") || source.Sha256 != expected[index].GetAttribute("Sha256")).Any())
            throw new InvalidOperationException("source_catalog_receipt_source_snapshot rebuild_required");
    }

    internal void VerifyFilesUnchanged()
    {
        foreach (XmlElement file in Inputs.SelectNodes("/Inputs/*/File")!)
        {
            string path = file.GetAttribute("Path");
            if (!System.IO.Path.IsPathFullyQualified(path))
                throw new InvalidOperationException("source_catalog_receipt_input_path");
            AuthorityFiles.RequirePlainPath(System.IO.Path.GetPathRoot(path)!, path, "source_catalog_receipt_input_link");
            using var stream = File.OpenRead(path);
            if (!long.TryParse(file.GetAttribute("Length"), System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture, out long length)
                || stream.Length != length || Convert.ToHexString(SHA256.HashData(stream)) != file.GetAttribute("Sha256"))
                throw new InvalidOperationException("source_catalog_receipt_input_changed rebuild_required " + path);
        }
    }

    internal PortableExecutableReference ReadReference(string path, MetadataReferenceProperties properties)
    {
        path = System.IO.Path.GetFullPath(path);
        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var admitted = Inputs.SelectNodes("/Inputs/Declared/File[@Kind='Reference']")!.Cast<XmlElement>()
            .Where(file => comparer.Equals(file.GetAttribute("Path"), path)).ToArray();
        if (admitted.Length == 0)
            throw new InvalidOperationException("source_catalog_reference_not_recorded " + path);
        AuthorityFiles.RequirePlainPath(System.IO.Path.GetPathRoot(path)!, path, "source_catalog_receipt_input_link");
        byte[] bytes = File.ReadAllBytes(path);
        string hash = Convert.ToHexString(SHA256.HashData(bytes));
        if (admitted.Any(file => file.GetAttribute("Length") != bytes.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)
                || file.GetAttribute("Sha256") != hash))
            throw new InvalidOperationException("source_catalog_reference_changed rebuild_required " + path);
        // Roslyn owns this authenticated image. A lazy file-backed reference
        // could otherwise bind bytes reopened after the input check.
        return MetadataReference.CreateFromImage(bytes, properties, filePath: path);
    }

    private static XmlDocument Parse(Stream stream)
    {
        using var reader = XmlReader.Create(stream, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            IgnoreWhitespace = true,
            MaxCharactersInDocument = 16 * 1024 * 1024
        });
        var document = new XmlDocument { XmlResolver = null };
        document.Load(reader);
        return document;
    }
}
