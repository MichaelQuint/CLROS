using System.Text;
using System.Security.Cryptography;
using System.Text.Json;

namespace OS.Tooling;

/// <summary>Generate the small host page in a published, uncompressed Blazor
/// asset tree under artifacts. Framework files remain outputs of the pinned SDK.</summary>
public static class BlazorStaticFileAssets
{
    public const string HostPage = """
        <!doctype html>
        <html lang="en">
        <head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
        <title>CLROS FAT32 Blazor fixture</title><base href="/"><link rel="stylesheet" href="site.css">
        </head><body><div id="app">Loading CLROS WebAssembly…</div>
        <script src="_framework/blazor.webassembly.js"></script></body></html>
        """;
    public const string Style = "body{font:18px system-ui;margin:3rem;line-height:1.5;background:#f4f6f8;color:#18232f}main{max-width:52rem}h1{font-size:2rem}#disk-nonce{font-family:monospace}";

    /// <summary>Copy a sealed SDK publication into a fresh artifacts webroot,
    /// excluding compression and debugging sidecars. Publish the directory only
    /// after all inputs have been checked again; retain failed staging evidence.</summary>
    public static void Prepare(string root, string publishedWebRoot, string destination)
    {
        root = Path.GetFullPath(root);
        publishedWebRoot = Path.TrimEndingDirectorySeparator(ArtifactsPath(root, publishedWebRoot));
        destination = Path.TrimEndingDirectorySeparator(ArtifactsPath(root, destination));
        StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (publishedWebRoot.Equals(destination, comparison) ||
            publishedWebRoot.StartsWith(destination + Path.DirectorySeparatorChar, comparison) ||
            destination.StartsWith(publishedWebRoot + Path.DirectorySeparatorChar, comparison))
            throw new InvalidDataException("blazor_source_destination_overlap");
        if (!Directory.Exists(publishedWebRoot) || Directory.Exists(destination) || File.Exists(destination))
            throw new InvalidDataException("blazor_webroot_requires_existing_source_and_fresh_destination");
        AuthorityFiles.RequirePlainPath(root, publishedWebRoot, "blazor_source");
        AuthorityFiles.RequirePlainPath(root, Path.GetDirectoryName(destination)!, "blazor_destination");
        string stage = destination + ".staging-" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(stage);
        var inventory = new SortedDictionary<string, string>(StringComparer.Ordinal);
        using AuthorityFiles files = new();
        void CopyDirectory(string source, string target)
        {
            AuthorityFiles.RequirePlainPath(root, source, "blazor_source");
            foreach (string directory in Directory.GetDirectories(source).Order(StringComparer.Ordinal))
            {
                string child = Path.Combine(target, Path.GetFileName(directory));
                Directory.CreateDirectory(child);
                CopyDirectory(directory, child);
            }
            foreach (string path in Directory.GetFiles(source).Order(StringComparer.Ordinal))
            {
                if (Path.GetExtension(path) is ".br" or ".gz" or ".map" or ".pdb")
                    continue;
                var file = files.Open(root, path, "Blazor publish asset", "blazor_asset", NativeAotPayload.MaximumImageBytes, singleLink: true);
                byte[] bytes = file.ReadBytes(NativeAotPayload.MaximumImageBytes, "blazor_asset");
                File.WriteAllBytes(Path.Combine(target, Path.GetFileName(path)), bytes);
                inventory.Add(Path.GetRelativePath(publishedWebRoot, path).Replace('\\', '/'), Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
            }
        }
        CopyDirectory(publishedWebRoot, stage);
        WriteHostPage(root, stage);
        inventory.Add("index.html", Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(stage, "index.html")))).ToLowerInvariant());
        inventory.Add("site.css", Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(stage, "site.css")))).ToLowerInvariant());
        files.VerifyUnchanged();
        File.WriteAllText(stage + ".json", JsonSerializer.Serialize(new
        {
            sdk = NativeAotPayload.SdkVersion,
            runtime = NativeAotPayload.RuntimeVersion,
            files = inventory
        }), new UTF8Encoding(false));
        Directory.Move(stage, destination);
        File.Move(stage + ".json", destination + ".json");
    }

    /// <summary>Require SDK-produced framework assets and a fresh host page;
    /// reject paths outside artifacts and linked paths before writing.</summary>
    public static void WriteHostPage(string root, string publishedWebRoot)
    {
        root = Path.GetFullPath(root);
        string destination = ArtifactsPath(root, publishedWebRoot);
        AuthorityFiles.RequirePlainPath(root, destination, "blazor_webroot");
        string framework = Path.Combine(destination, "_framework", "blazor.webassembly.js");
        AuthorityFiles.RequirePlainPath(root, framework, "blazor_framework");
        if (!File.Exists(framework))
            throw new InvalidDataException("blazor_framework_missing");
        string index = Path.Combine(destination, "index.html");
        using var stream = new FileStream(index, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        stream.Write(new UTF8Encoding(false).GetBytes(HostPage));
        using var style = new FileStream(Path.Combine(destination, "site.css"), FileMode.CreateNew, FileAccess.Write, FileShare.None);
        style.Write(new UTF8Encoding(false).GetBytes(Style));
    }

    private static string ArtifactsPath(string root, string path)
    {
        string artifacts = Path.GetFullPath(Path.Combine(root, "artifacts")) + Path.DirectorySeparatorChar;
        string full = Path.GetFullPath(path, root);
        if (!full.StartsWith(artifacts, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new InvalidDataException("blazor_webroot_must_be_under_artifacts");
        return full;
    }
}
