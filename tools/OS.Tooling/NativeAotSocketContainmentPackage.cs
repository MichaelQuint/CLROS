using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Manifest;

namespace OS.Tooling;

/// <summary>Packages only the original published containment control. All
/// publication outputs remain borrowed under read-only locks until checked;
/// returned package bytes are caller owned. Publication is not Linux or CLROS
/// execution qualification. Unsupported policy, source or input changes throw.</summary>
public static class NativeAotSocketContainmentPackage
{
    public const int ElfBytes = 1_585_456;
    public const string ElfSha256 = "785F992A35A9F3250EC2D3BA76422FEF51F8CC11D710EA295E53741C82A7A737";
    private const string App = NativeAotSocketContainmentPublisher.Application;
    private const string ReceiptSha256 = "E13FB7A1C76D7DF6D604259412F3FED859E1CAAD05C3EBB17B517DAF7DECD553";
    private static readonly (string Name, string Sha256)[] AppSources = [
        ("Program.cs", "FDC66C995386EFDB0DEC18BF6DB4BB7463FEB5C4BDD3C59E59FBD6F0CFC56B0C"),
        (App + ".csproj", "D8AFB29D1F4465AD7D3EF3F4A292255DBC5E6975FFD4AD02948E912B1D88564B")];

    /// <summary>Finite three-peer diagnostic policy; no arguments, sixteen
    /// thread/fd slots, explicit user IRQs and one passive IPv4 bind.</summary>
    public static string Manifest => "format=clros-linux-elf\nversion=1\nprofile=linux-musl-x64-static-v1\napp.path=apps/" + App +
        "\nargs=\ndigest=" + ElfSha256.ToLowerInvariant() +
        "\nmemory=67108864\nstack=1572864\nthreads=16\ndescriptors=16\nlisten=0.0.0.0:8080\nenv.CLROS_USER_IRQS=1\n";

    /// <summary>Validate current original build/recipe inputs and the exact two
    /// containment app files. Generated bin/obj are excluded; overrides, links
    /// and extra source files reject. No publication or execution is performed.</summary>
    public static void RequireCurrentSources(string root)
    {
        root = Path.GetFullPath(root); NativeAotSocketPackage.RequireCurrentSources(root);
        string directory = Path.Combine(root, "apps", App);
        var inventory = Inventory(root, directory, generated: true);
        Need(inventory.SequenceEqual(AppSources.Select(s => s.Name).Order(StringComparer.Ordinal)), "source_inventory");
        using AuthorityFiles locks = new();
        foreach (var source in AppSources)
            Need(Hash(Read(locks, root, Path.Combine("apps", App, source.Name), 1024 * 1024)) == source.Sha256, "source_identity");
        locks.VerifyUnchanged();
    }

    /// <summary>Require the immutable original publication receipt and its
    /// complete20-output inventory, producer, recipe, managed/native objects and
    /// original ELF. Linux external native inputs remain pinned receipt values.
    /// Returned bytes grant package identity only, never guest success.</summary>
    public static unsafe byte[] Create(string root, string publicationRoot)
    {
        RequireCurrentSources(root); publicationRoot = Path.GetFullPath(publicationRoot);
        using AuthorityFiles locks = new();
        byte[] receipt = Read(locks, publicationRoot, "publication.json", 1024 * 1024);
        Need(Hash(receipt) == ReceiptSha256, "publication_identity");
        using var document = JsonDocument.Parse(receipt); var data = document.RootElement;
        Need(data.GetProperty("schema").GetString() == "clros.nativeaot.socket-containment.publication.v1" &&
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
        Need(Inventory(publicationRoot, publicationRoot, generated: false).SequenceEqual(
            outputs.Select(o => o.Name).Concat(["publication.json", "publication.lock"]).Order(StringComparer.Ordinal)), "output_inventory");
        Need(Hash(retained["recipe/receipt.json"]) == "E2600BF12DACCC159AECFAE1A3209027905AB2757D228009F0C6ADDC6A084D0B" &&
            Hash(retained["originals/OS.Tooling.dll"]) == "3075F4590F65C38A492260E106C7995F64F57F5DE7D1CA3D400B9DE837F8202C", "producer_identity");
        byte[] elf = retained[App + "/" + App];
        Need(elf.Length == ElfBytes && Hash(elf) == ElfSha256, "elf_identity");
        _ = NativeAotPayload.Inspect(elf);
        fixed (byte* image = elf) Need(LinuxElfLoader.Validate(image, (uint)elf.Length, 67108864, 1572864, out _) == LinuxElfLoadStatus.Ready, "loader");
        byte[] manifest = Encoding.ASCII.GetBytes(Manifest), package = new byte[64 + manifest.Length + elf.Length];
        Header(manifest.Length).CopyTo(package, 0); manifest.CopyTo(package, 64); elf.CopyTo(package, 64 + manifest.Length);
        RequirePackage(package); locks.VerifyUnchanged(); RequireCurrentSources(root); return package;
    }

    /// <summary>Admit only canonical policy/header and original ELF bytes.
    /// Caller bytes are borrowed synchronously; no instruction lookup or
    /// execution authority is produced.</summary>
    public static void RequirePackage(ReadOnlySpan<byte> package)
    {
        byte[] manifest = Encoding.ASCII.GetBytes(Manifest);
        Need(package.Length == 64 + manifest.Length + ElfBytes && package[..64].SequenceEqual(Header(manifest.Length)), "identity");
        Need(LinuxElfPackage.Validate(package).Succeeded && package.Slice(64, manifest.Length).SequenceEqual(manifest) &&
            Hash(package[(64 + manifest.Length)..]) == ElfSha256, "identity");
    }

    private static string[] Inventory(string root, string directory, bool generated)
    {
        AuthorityFiles.RequirePlainPath(root, directory, "socket_containment_inventory");
        Stack<string> pending = new(); pending.Push(directory); List<string> files = []; int entries = 0;
        while (pending.TryPop(out string? current)) foreach (string path in Directory.EnumerateFileSystemEntries(current))
        {
            Need(++entries <= 256, "inventory_bound");
            var attributes = File.GetAttributes(path); Need((attributes & FileAttributes.ReparsePoint) == 0, "inventory_link");
            if ((attributes & FileAttributes.Directory) != 0)
            { if (!generated || Path.GetFileName(path) is not ("bin" or "obj")) pending.Push(path); }
            else files.Add(Path.GetRelativePath(directory, path).Replace('\\', '/'));
        }
        return files.Order(StringComparer.Ordinal).ToArray();
    }
    private static byte[] Read(AuthorityFiles locks, string root, string relative, int maximum)
    {
        var file = locks.Open(root, Path.GetFullPath(relative, root), "containment original", "socket_containment_input", maximum,
            allowEmpty: true, singleLink: true);
        return file.ReadBytes(maximum, "socket_containment_input");
    }
    private static byte[] Header(int manifest)
    {
        byte[] header = new byte[64]; BinaryPrimitives.WriteUInt64LittleEndian(header, LinuxElfPackage.Magic);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(8), 1); BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(12), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(16), 64); BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(20), (uint)manifest);
        BinaryPrimitives.WriteUInt64LittleEndian(header.AsSpan(24), ElfBytes); return header;
    }
    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static void Need(bool condition, string reason) { if (!condition) throw new InvalidDataException("socket_containment_package_" + reason); }
}
