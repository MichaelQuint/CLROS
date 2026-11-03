using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Manifest;

namespace OS.Tooling;

/// <summary>
/// Packages only the freshly qualified RC3 Linux NativeAOT Hello. Host-only input
/// admission binds the qualified ELF, actual produced DLL/object, qualification
/// receipts and current relevant source bytes. It grants no execution authority.
/// Migration evidence is read-only; no builder, compiler or guest is started.
/// </summary>
public static class NativeAotHelloGuest
{
    public const int ElfBytes = 1_241_768;
    public const string ElfSha256 = "C4FDCAAF209A50EE68A0D1606A42BD70883F88721628267EB01DE901553F7E9C";
    private const string Qualification = "artifacts/payload-qualification-r01/";
    private const string Produced = "apps/NativeAotHello/obj/Release/net11.0/linux-musl-x64/";

    // Exact raw bytes, including line endings, from the qualified source inputs.
    private static readonly (string Path, string Hash)[] Sources = [
        ("apps/NativeAotHello/Program.cs", "B41BA4DC930BF9FF7D2A1E38A3965A5E0E1D2F118AA78B0039F9275C522DFBFE"),
        ("apps/NativeAotHello/NativeAotHello.csproj", "D8AFB29D1F4465AD7D3EF3F4A292255DBC5E6975FFD4AD02948E912B1D88564B"),
        ("global.json", "BE8663081FD82196AC86E65E9021D58D7C03A13574CE8CD4DC803937FC3BA30E"),
        ("Directory.Build.props", "DF8A0D794DB73060124F5134EB930DCDBF6EE526C2410227F0C668AB6D10C5E5"),
        ("Directory.Build.targets", "85AFD90F9DD0F550B6A3C6AD71D28BF714A8C930158DB872F8DA5D316889931A"),
        ("tools/Directory.Build.props", "44B8268EA6934335DBC79FEF47C1CE1FDEE49393C929C92E322417B14AA321AE"),
        ("tools/OS.Tooling/NativeAotPayload.cs", "E3EC6BCE13C70C99F3D7306541D68104876FAF73593325107319719811694D2F")
    ];
    private static readonly (string Path, int Bytes, string Hash)[] Receipts = [
        (Qualification + "inputs.json", 119781, "BC146C016C3807026B088BA9EEE1354062DE14A2D15AE484266B5EB02ED613D3"),
        (Qualification + "native-inputs.json", 226492, "B18C26D43B18353A6EE7B1E54800D727FE1767F575AC787C0FC7C190EB31237A"),
        (Qualification + "inspection.json", 2054, "852AC089419F443BA5D0EA7283EE0C1614E5ED603E1FB06DCAA276401DE6336C"),
        (Qualification + "commands.json", 7696, "84103D126C99FAF612CDD85A2C71E684F589914766EF2AE44E4FC2B19A4A431C"),
        (Qualification + "sdk-version.stdout.log", 24, "8F311FDA85131EB472C3F117C2C4E441C86AAEB72740D76F44D89FF1E75AE7AE"),
        (Produced + "NativeAotHello.dll", 4096, "6786392355460964E6097DD849BFBD7E9DD18D82D6008C3BC98F67E468347E31"),
        (Produced + "native/NativeAotHello.o", 3967639, "E14B88F67A1B6C46E5D3CF6FB9AC81268F967FD4009695C59548EA8A7E05F1C8")
    ];

    /// <summary>
    /// Canonical package policy: no arguments or runtime overrides, 64 MiB memory,
    /// 1.5 MiB main stack, four thread slots, sixteen descriptors and explicit user
    /// IRQs. The ELF is fixed by the current candidate identity. The required
    /// endpoint grants no socket.
    /// </summary>
    public const string Manifest = "format=clros-linux-elf\nversion=1\nprofile=linux-musl-x64-static-v1\n" +
        "app.path=apps/NativeAotHello\nargs=\ndigest=" +
        "c4fdcaaf209a50ee68a0d1606a42bd70883f88721628267eb01de901553f7e9c\n" +
        "memory=67108864\nstack=1572864\nthreads=4\ndescriptors=16\nlisten=127.0.0.1:8080\nenv.CLROS_USER_IRQS=1\n";

    /// <summary>
    /// Checks current app/SDK/build/qualifier inputs against reviewed raw source
    /// hashes, expanding admitted source-only splits. Missing or changed inputs throw; unrelated
    /// kernel/tooling edits do not become claims of whole-repository identity.
    /// </summary>
    public static void RequireCurrentSources(string root)
    {
        root = Path.GetFullPath(root);
        foreach (var source in CurrentSources())
        {
            byte[] bytes = ReadBounded(Path.Combine(root, source.Path), 1024 * 1024);
            Require(Hash(bytes) == source.Hash, "hello_source_identity " + source.Path);
        }
        NativeAotCurrentSources.RequireToolingOwnership(root, Sources);
    }

    internal static IReadOnlyList<(string Path, string Hash)> CurrentSources() => NativeAotCurrentSources.Expand(Sources).ToArray();

    /// <summary>
    /// Returns a fresh package from the extracted RC3 original directory. Exact
    /// receipts include 998 source and 1042 native input identities, producer
    /// outputs and the SDK/publish configuration. All inputs are read, never run;
    /// missing or mismatched evidence throws before a package can be returned.
    /// </summary>
    public static unsafe byte[] CreatePackage(string root, string evidenceRoot)
    {
        RequireCurrentSources(root);
        evidenceRoot = Path.GetFullPath(evidenceRoot);
        foreach (var receipt in Receipts)
            ReadPinned(Path.Combine(evidenceRoot, receipt.Path), receipt.Bytes, receipt.Hash);
        byte[] elf = ReadPinned(Path.Combine(evidenceRoot, Qualification + "NativeAotHello/NativeAotHello"), ElfBytes, ElfSha256);
        NativeAotPayload.Inspect(elf);
        fixed (byte* image = elf)
            Require(LinuxElfLoader.Validate(image, (uint)elf.Length, 64 * 1024 * 1024, 1_572_864, out _) ==
                LinuxElfLoadStatus.Ready, "hello_loader_admission");
        byte[] manifest = Encoding.ASCII.GetBytes(Manifest);
        byte[] package = new byte[64 + manifest.Length + elf.Length];
        BinaryPrimitives.WriteUInt64LittleEndian(package, LinuxElfPackage.Magic);
        BinaryPrimitives.WriteUInt32LittleEndian(package.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(package.AsSpan(12), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(package.AsSpan(16), 64);
        BinaryPrimitives.WriteUInt32LittleEndian(package.AsSpan(20), (uint)manifest.Length);
        BinaryPrimitives.WriteUInt64LittleEndian(package.AsSpan(24), (ulong)elf.Length);
        manifest.CopyTo(package, 64);
        elf.CopyTo(package, 64 + manifest.Length);
        RequirePackage(package);
        RequireCurrentSources(root);
        return package;
    }

    /// <summary>
    /// Accepts only the complete canonical policy/header and exact upstream ELF.
    /// Validation neither maps nor executes bytes and does not validate a guest
    /// transcript. The caller retains ownership of the supplied span.
    /// </summary>
    public static void RequirePackage(ReadOnlySpan<byte> package)
    {
        byte[] manifest = Encoding.ASCII.GetBytes(Manifest);
        Require(package.Length == 64 + manifest.Length + ElfBytes, "hello_package_identity");
        var view = LinuxElfPackage.Validate(package);
        Require(view.Succeeded && view.ManifestBytes == manifest.Length &&
            package.Slice(64, manifest.Length).SequenceEqual(manifest) &&
            Hash(package[(64 + manifest.Length)..]) == ElfSha256, "hello_package_identity");
    }

    /// <summary>
    /// Validates all inputs before writing. Publishes a flushed sibling temporary
    /// file with a no-overwrite rename, deleting only its own temporary on failure.
    /// Output must be below this repository's artifacts directory through existing
    /// non-reparse directories. Returns the package SHA256, never guest success.
    /// </summary>
    public static string WritePackage(string root, string evidenceRoot, string destination)
    {
        string path = Path.GetFullPath(destination);
        string artifacts = Path.Combine(Path.GetFullPath(root), "artifacts") + Path.DirectorySeparatorChar;
        Require(path.StartsWith(artifacts, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal),
            "hello_output_outside_artifacts");
        RequireOutputParent(path, artifacts.TrimEnd(Path.DirectorySeparatorChar));
        if (File.Exists(path) || Directory.Exists(path))
            throw new IOException("hello_output_exists");
        byte[] package = CreatePackage(root, evidenceRoot);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        bool ownsTemporary = false;
        try
        {
            RequireOutputParent(path, artifacts.TrimEnd(Path.DirectorySeparatorChar));
            using (FileStream stream = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                ownsTemporary = true;
                stream.Write(package);
                stream.Flush(flushToDisk: true);
            }
            RequireCurrentSources(root);
            RequireOutputParent(path, artifacts.TrimEnd(Path.DirectorySeparatorChar));
            File.Move(temporary, path, overwrite: false);
        }
        finally
        {
            if (ownsTemporary)
                File.Delete(temporary);
        }
        return Hash(package);
    }

    private static void RequireOutputParent(string path, string artifacts)
    {
        StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        for (DirectoryInfo? directory = new(Path.GetDirectoryName(path)!); directory is not null; directory = directory.Parent)
        {
            if (!directory.Exists)
                throw new DirectoryNotFoundException("hello_output_parent_missing");
            Require((directory.Attributes & FileAttributes.ReparsePoint) == 0, "hello_output_reparse");
            if (string.Equals(directory.FullName, artifacts, comparison))
                return;
        }
        throw new InvalidDataException("hello_output_outside_artifacts");
    }

    private static byte[] ReadPinned(string path, int length, string hash)
    {
        byte[] bytes = ReadBounded(path, length);
        Require(bytes.Length == length && Hash(bytes) == hash, "hello_evidence_identity " + Path.GetFileName(path));
        return bytes;
    }

    private static byte[] ReadBounded(string path, int maximum)
    {
        using FileStream file = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        Require(file.Length > 0 && file.Length <= maximum, "hello_input_size " + Path.GetFileName(path));
        byte[] bytes = new byte[checked((int)file.Length)];
        file.ReadExactly(bytes);
        Require(file.ReadByte() == -1, "hello_input_changed " + Path.GetFileName(path));
        return bytes;
    }

    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static void Require(bool condition, string reason)
    {
        if (!condition)
            throw new InvalidDataException(reason);
    }
}
