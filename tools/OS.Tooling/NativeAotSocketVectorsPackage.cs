using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Manifest;

namespace OS.Tooling;

/// <summary>Packages only the original published BufferList fixture. Publication
/// inputs remain borrowed under read-only locks until verified; returned package
/// bytes are caller owned. Source, policy or input changes reject. Package identity
/// grants neither Linux execution qualification nor CLROS guest acceptance.</summary>
public static class NativeAotSocketVectorsPackage
{
    public const int ElfBytes = 1_845_008;
    public const string ElfSha256 = "94D330D2BEED5BF11010D6D681D87F7164E55DF8972875CBE6B7BB40A9D77D3B";
    private const string App = NativeAotSocketVectorsPublisher.Application;
    private const string ReceiptSha256 = "D7DAB228EBE88E961E778834ED47781A03D9FFA305BAA7724350C35223EC30A9";
    private static readonly (string Name, string Sha256)[] AppSources = [
        ("Program.cs", "B6D30356A7A372FE6A3C355022FB33B219767A8B0F271EB57123044B39340752"),
        ("SocketVectorsFixture.cs", "C4212A52061F6FF07553B0138B1592377D2F55DA90A103D2B34D297A7039554D"),
        (App + ".csproj", "D8AFB29D1F4465AD7D3EF3F4A292255DBC5E6975FFD4AD02948E912B1D88564B")];

    /// <summary>Finite four-peer diagnostic policy: no arguments, sixteen thread
    /// and fd slots, explicit user IRQs and one passive IPv4 bind.</summary>
    public static string Manifest => "format=clros-linux-elf\nversion=1\nprofile=linux-musl-x64-static-v1\napp.path=apps/" + App +
        "\nargs=\ndigest=" + ElfSha256.ToLowerInvariant() +
        "\nmemory=67108864\nstack=1572864\nthreads=16\ndescriptors=16\nlisten=0.0.0.0:8080\nenv.CLROS_USER_IRQS=1\n";

    /// <summary>Validate current original socket build/consumer inputs and the
    /// publisher's exact three app files. Generated bin/obj are excluded; source
    /// links, overrides and extra app files reject. No publication is performed.</summary>
    public static void RequireCurrentSources(string root)
    {
        root = Path.GetFullPath(root);
        NativeAotSocketPackage.RequireCurrentSources(root);
        NativeAotSocketVectorsPublisher.RequireCurrentSources(root);
    }

    /// <summary>Require the immutable original receipt and complete20-output
    /// inventory, producer, recipe, managed/native objects and original ELF.
    /// Linux external native inputs remain receipt identities. All retained files
    /// must remain unchanged and unaliased until return; no output is written.</summary>
    public static unsafe byte[] Create(string root, string publicationRoot)
    {
        RequireCurrentSources(root); publicationRoot = Path.GetFullPath(publicationRoot);
        using AuthorityFiles locks = new();
        byte[] receipt = Read(locks, publicationRoot, "publication.json", 1024 * 1024);
        Need(Hash(receipt) == ReceiptSha256, "publication_identity");
        using var document = JsonDocument.Parse(receipt); var data = document.RootElement;
        Need(data.GetProperty("schema").GetString() == "clros.nativeaot.socket-vectors.publication.v1" &&
            data.GetProperty("sdk").GetString() == NativeAotPayload.SdkVersion &&
            data.GetProperty("runtime").GetString() == NativeAotPayload.RuntimeVersion &&
            data.GetProperty("application").GetString() == App && data.GetProperty("succeeded").GetBoolean() &&
            !data.GetProperty("appExecuted").GetBoolean() && !data.GetProperty("guestExecuted").GetBoolean(), "publication_policy");
        foreach (var source in AppSources)
            Need(data.GetProperty("sources").GetProperty("apps/" + App + "/" + source.Name).GetString() == source.Sha256, "producer_source");
        var outputs = data.GetProperty("outputs").EnumerateObject().ToArray();
        Need(outputs.Length == 20, "output_inventory");
        var retained = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var output in outputs)
        {
            byte[] bytes = Read(locks, publicationRoot, output.Name, 16 * 1024 * 1024);
            Need(Hash(bytes) == output.Value.GetString(), "output_identity"); retained.Add(output.Name, bytes);
        }
        Need(Inventory(publicationRoot).SequenceEqual(outputs.Select(output => output.Name)
            .Concat(["publication.json", "publication.lock"]).Order(StringComparer.Ordinal)), "output_inventory");
        Need(Hash(retained["recipe/receipt.json"]) == "E2600BF12DACCC159AECFAE1A3209027905AB2757D228009F0C6ADDC6A084D0B" &&
            Hash(retained["originals/OS.Tooling.dll"]) == "F4EF8A047C7025EDA400C50621C18BAB50F5FE5E9ECAD9F72E4EF372F57BC519", "producer_identity");
        byte[] elf = retained[App + "/" + App];
        Need(elf.Length == ElfBytes && Hash(elf) == ElfSha256, "elf_identity");
        _ = NativeAotPayload.Inspect(elf);
        fixed (byte* image = elf) Need(LinuxElfLoader.Validate(image, (uint)elf.Length, 67108864, 1572864, out _) == LinuxElfLoadStatus.Ready, "loader");
        byte[] manifest = Encoding.ASCII.GetBytes(Manifest), package = new byte[64 + manifest.Length + elf.Length];
        Header(manifest.Length).CopyTo(package, 0); manifest.CopyTo(package, 64); elf.CopyTo(package, 64 + manifest.Length);
        RequirePackage(package); locks.VerifyUnchanged(); RequireCurrentSources(root); return package;
    }

    /// <summary>Admit only canonical header/policy and original ELF bytes.
    /// Caller bytes are borrowed synchronously. No instruction-index, execution
    /// completion or guest authority is produced.</summary>
    public static void RequirePackage(ReadOnlySpan<byte> package)
    {
        byte[] manifest = Encoding.ASCII.GetBytes(Manifest);
        Need(package.Length == 64 + manifest.Length + ElfBytes && package[..64].SequenceEqual(Header(manifest.Length)), "identity");
        Need(LinuxElfPackage.Validate(package).Succeeded && package.Slice(64, manifest.Length).SequenceEqual(manifest) &&
            Hash(package[(64 + manifest.Length)..]) == ElfSha256, "identity");
    }

    /// <summary>Write a fresh canonical package beneath artifacts after checking
    /// original publication inputs. Own the temporary file through atomic move;
    /// never replace existing output. Return its digest, without execution proof.</summary>
    public static string Write(string root, string publicationRoot, string destination)
    {
        root = Path.GetFullPath(root); destination = Path.GetFullPath(destination);
        void Output()
        {
            Need(destination.StartsWith(Path.Combine(root, "artifacts") + Path.DirectorySeparatorChar,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal), "output_path");
            AuthorityFiles.RequirePlainPath(root, Path.GetDirectoryName(destination)!, "socket_vectors_output_path");
        }
        using var lease = NativeAotPayload.OpenQualificationLease(root);
        Output(); Need(!Path.Exists(destination), "output_exists"); byte[] bytes = Create(root, publicationRoot);
        string temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp"; bool owns = false;
        try
        {
            Output(); using (FileStream file = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { owns = true; file.Write(bytes); file.Flush(flushToDisk: true); }
            RequireCurrentSources(root); Output(); File.Move(temporary, destination, overwrite: false);
        }
        finally { if (owns) File.Delete(temporary); }
        return Hash(bytes);
    }

    private static string[] Inventory(string root)
    {
        AuthorityFiles.RequirePlainPath(root, root, "socket_vectors_inventory");
        Stack<string> pending = new(); pending.Push(root); List<string> files = []; int entries = 0;
        while (pending.TryPop(out string? current)) foreach (string path in Directory.EnumerateFileSystemEntries(current))
        {
            Need(++entries <= 256, "inventory_bound");
            var attributes = File.GetAttributes(path); Need((attributes & FileAttributes.ReparsePoint) == 0, "inventory_link");
            if ((attributes & FileAttributes.Directory) != 0) pending.Push(path);
            else files.Add(Path.GetRelativePath(root, path).Replace('\\', '/'));
        }
        return files.Order(StringComparer.Ordinal).ToArray();
    }

    private static byte[] Read(AuthorityFiles locks, string root, string relative, int maximum)
    {
        var file = locks.Open(root, Path.GetFullPath(relative, root), "vectors original", "socket_vectors_input", maximum,
            allowEmpty: true, singleLink: true);
        return file.ReadBytes(maximum, "socket_vectors_input");
    }

    private static byte[] Header(int manifest)
    {
        byte[] header = new byte[64]; BinaryPrimitives.WriteUInt64LittleEndian(header, LinuxElfPackage.Magic);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(8), 1); BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(12), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(16), 64); BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(20), (uint)manifest);
        BinaryPrimitives.WriteUInt64LittleEndian(header.AsSpan(24), ElfBytes); return header;
    }
    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static void Need(bool condition, string reason) { if (!condition) throw new InvalidDataException("socket_vectors_package_" + reason); }
}
