using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Manifest;

namespace OS.Tooling;

/// <summary>Host-only packaging of the retained, unchanged synchronous/async
/// socket ELF. Exact producer and output identities grant package authority;
/// they grant no CLROS execution, socket, epoll or Kestrel acceptance.</summary>
public static class NativeAotSocketPackage
{
    private const string App = "NativeAotSocketSmoke", Qualification = "artifacts/socket-qualification-r01/";
    private const int ElfBytes = 2_117_424;
    /// <summary>Raw SHA256 of the one retained original ELF used by both modes.</summary>
    public const string ElfSha256 = "8DDAC5986AFF981448D8B1F3BAF5FDA09E89368C578A7389BBB243FA5BFABE3D";
    // The retained receipt binds its original producer source. The current
    // consumer includes the separately reviewed web race/session extensions;
    // socket-used helpers are unchanged. See step1554. This does not rewrite
    // the original receipt or republish its ELF.
    private const string WebPayloadSource = "tools/OS.Tooling/NativeAotWebPayload.cs";
    private const string CurrentWebPayloadSha256 = "129FD1AE91130FBFFC772D4A189A1FBB4EEEBF8236FF69B6B827A3A40B4C1FA3";
    private static readonly (string Path, string Hash)[] ProducerSources = [
        ("global.json", "FD3FDAAE1B349B01D6E6835EC111903F107A2643F3BCE313FAD4DC20180F5FB2"),
        ("Directory.Build.props", "DF8A0D794DB73060124F5134EB930DCDBF6EE526C2410227F0C668AB6D10C5E5"),
        ("Directory.Build.targets", "85AFD90F9DD0F550B6A3C6AD71D28BF714A8C930158DB872F8DA5D316889931A"),
        ("tools/Directory.Build.props", "44B8268EA6934335DBC79FEF47C1CE1FDEE49393C929C92E322417B14AA321AE"),
        ("tools/OS.Tooling/NativeAotPayload.cs", "44F16ABB380FE7F3CD42BA3C7E54710172F0320998CF2E85E296F8A7CE184AA5"),
        (WebPayloadSource, "4194B7F5E58900EFA1AA10DB1CAA919D326D8339B1F1866AB9E6DEC1D990FEB8"),
        ("tools/OS.Tooling/NativeAotSocketPayload.cs", "950FDC045A55DCD1E57FC1E72B579B042102FB001C445C6AF8072DB1E98E59D9"),
        ("apps/NativeAotSocketSmoke/Program.cs", "45ACEEBA92D225EBDC2E877111138D78778C62D7FF8ACDC4932CF40CD6CE5AF8"),
        ("apps/NativeAotSocketSmoke/NativeAotSocketSmoke.csproj", "D8AFB29D1F4465AD7D3EF3F4A292255DBC5E6975FFD4AD02948E912B1D88564B")];

    /// <summary>Canonical finite diagnostic policy: the same ELF, optional
    /// --async, explicit user IRQs, sixteen threads/fds and one Any:8080 bind.</summary>
    public static string Manifest(bool asynchronous) =>
        "format=clros-linux-elf\nversion=1\nprofile=linux-musl-x64-static-v1\napp.path=apps/" + App +
        "\nargs=" + (asynchronous ? "--async" : "") + "\ndigest=" + ElfSha256.ToLowerInvariant() +
        "\nmemory=67108864\nstack=1572864\nthreads=16\ndescriptors=16\nlisten=0.0.0.0:8080\nenv.CLROS_USER_IRQS=1\n";

    /// <summary>Requires exact reviewed current raw app/build/consumer inputs.
    /// Additional app files or local restore/build policy reject; bin/obj are
    /// excluded. The immutable receipt separately retains its producer pins;
    /// current consumer identity grants no original-source or publication claim.</summary>
    public static void RequireCurrentSources(string root)
    {
        root = Path.GetFullPath(root);
        foreach (string relative in new[] { "NuGet.Config", "nuget.config", "Directory.Packages.props",
            "apps/Directory.Build.props", "apps/Directory.Build.targets", "apps/Directory.Packages.props" })
            Need(!Path.Exists(Path.Combine(root, relative)), "source_override " + relative);
        string directory = Path.Combine(root, "apps", App);
        AuthorityFiles.RequirePlainPath(root, directory, "socket_source_path");
        Stack<string> pending = new(); pending.Push(directory); List<string> files = []; int entries = 0;
        while (pending.TryPop(out string? next)) foreach (string path in Directory.EnumerateFileSystemEntries(next))
        {
            Need(++entries <= 128, "source_inventory");
            FileAttributes attributes = File.GetAttributes(path); Need((attributes & FileAttributes.ReparsePoint) == 0, "source_link");
            if ((attributes & FileAttributes.Directory) != 0)
            { if (Path.GetFileName(path) is not ("bin" or "obj" or "artifacts")) pending.Push(path); }
            else files.Add(Path.GetRelativePath(directory, path).Replace('\\', '/'));
        }
        Need(files.Order(StringComparer.Ordinal).SequenceEqual(new[] { App + ".csproj", "Program.cs" }.Order(StringComparer.Ordinal)), "source_inventory");
        foreach (var source in ProducerSources)
        {
            string expected = source.Path == WebPayloadSource ? CurrentWebPayloadSha256 : source.Hash;
            Need(Hash(Read(root, source.Path, 1024 * 1024)) == expected, "source_identity " + source.Path);
        }
    }

    /// <summary>Reads the immutable original receipt, all 32 retained outputs,
    /// produced DLL/object and original producer DLL before creating owned bytes.
    /// Missing, linked, changed, extra or oversized inputs fail closed. External
    /// Linux inputs remain receipt identities, not rehashed Windows inputs.</summary>
    public static unsafe byte[] Create(string root, string evidenceRoot, bool asynchronous)
    {
        RequireCurrentSources(root); evidenceRoot = Path.GetFullPath(evidenceRoot);
        byte[] receipt = Read(evidenceRoot, Qualification + "receipt.json", 322_416);
        Need(receipt.Length == 322_416 && Hash(receipt) == "E2600BF12DACCC159AECFAE1A3209027905AB2757D228009F0C6ADDC6A084D0B", "receipt_identity");
        using JsonDocument document = JsonDocument.Parse(receipt); var data = document.RootElement;
        Need(data.GetProperty("schema").GetString() == "clros.nativeaot.socket.v2" &&
            data.GetProperty("sdk").GetString() == NativeAotPayload.SdkVersion &&
            data.GetProperty("runtime").GetString() == NativeAotPayload.RuntimeVersion, "producer_policy");
        foreach (var source in ProducerSources) Need(data.GetProperty("sources").GetProperty(source.Path).GetString() == source.Hash, "producer_source");
        var outputs = data.GetProperty("outputs").EnumerateObject().ToArray(); Need(outputs.Length == 32, "output_inventory");
        foreach (var item in outputs) Need(Hash(Read(evidenceRoot, Qualification + item.Name, 16 * 1024 * 1024)) == item.Value.GetString(), "output_identity " + item.Name);
        string directory = Path.Combine(evidenceRoot, Qualification);
        Stack<string> pending = new(); pending.Push(directory); List<string> inventory = []; int entries = 0;
        while (pending.TryPop(out string? next)) foreach (string path in Directory.EnumerateFileSystemEntries(next))
        {
            Need(++entries <= 128, "output_inventory");
            FileAttributes attributes = File.GetAttributes(path); Need((attributes & FileAttributes.ReparsePoint) == 0, "output_link");
            if ((attributes & FileAttributes.Directory) != 0) pending.Push(path);
            else inventory.Add(Path.GetRelativePath(directory, path).Replace('\\', '/'));
        }
        Need(inventory.Order(StringComparer.Ordinal).SequenceEqual(outputs.Select(item => item.Name).Concat(["receipt.json", "qualification.lock"]).Order(StringComparer.Ordinal)), "output_inventory");
        foreach (string path in new[] { "apps/NativeAotSocketSmoke/obj/Release/net11.0/linux-musl-x64/NativeAotSocketSmoke.dll",
            "apps/NativeAotSocketSmoke/obj/Release/net11.0/linux-musl-x64/native/NativeAotSocketSmoke.o",
            "tools/OS.Tooling/bin/Debug/net11.0/OS.Tooling.dll" })
            Need(Hash(Read(evidenceRoot, path, 16 * 1024 * 1024)) == data.GetProperty("native").GetProperty("/clros-async-r05/" + path).GetString(), "producer_output " + path);
        NativeAotSocketPayload.ValidateOutput(Encoding.UTF8.GetString(Read(evidenceRoot, Qualification + "run.stdout.log", 4096)));
        var observed = NativeAotSocketPayload.ValidateAsyncOutput(Encoding.UTF8.GetString(Read(evidenceRoot, Qualification + "async.stdout.log", 4096)));
        Need(observed.PendingAccepts > 0 && observed.PendingReceives > 0 && observed.PendingSends > 0, "async_observation");
        byte[] elf = Read(evidenceRoot, Qualification + App + "/" + App, ElfBytes);
        Need(elf.Length == ElfBytes && Hash(elf) == ElfSha256, "elf_identity"); _ = NativeAotPayload.Inspect(elf);
        fixed (byte* image = elf) Need(LinuxElfLoader.Validate(image, (uint)elf.Length, 67108864, 1572864, out _) == LinuxElfLoadStatus.Ready, "loader");
        byte[] manifest = Encoding.ASCII.GetBytes(Manifest(asynchronous)), package = new byte[64 + manifest.Length + elf.Length];
        Header(manifest.Length).CopyTo(package, 0); manifest.CopyTo(package, 64); elf.CopyTo(package, 64 + manifest.Length);
        RequirePackage(package, asynchronous); RequireCurrentSources(root); return package;
    }

    /// <summary>Admits only canonical package bytes for the selected mode and
    /// original ELF. This grants no instruction-index or execution authority.</summary>
    public static void RequirePackage(ReadOnlySpan<byte> package, bool asynchronous)
    {
        byte[] manifest = Encoding.ASCII.GetBytes(Manifest(asynchronous));
        Need(package.Length == 64 + manifest.Length + ElfBytes && package[..64].SequenceEqual(Header(manifest.Length)), "identity");
        Need(LinuxElfPackage.Validate(package).Succeeded && package.Slice(64, manifest.Length).SequenceEqual(manifest) &&
            Hash(package[(64 + manifest.Length)..]) == ElfSha256, "identity");
    }

    /// <summary>Publishes a fresh package below artifacts after all admission.
    /// Flushes an owned sibling temporary and uses a no-overwrite rename. Only
    /// its own temporary is removed on failure; all evidence remains read-only.</summary>
    public static string Write(string root, string evidenceRoot, bool asynchronous, string destination)
    {
        root = Path.GetFullPath(root); destination = Path.GetFullPath(destination);
        void Output()
        {
            Need(destination.StartsWith(Path.Combine(root, "artifacts") + Path.DirectorySeparatorChar,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal), "output_path");
            AuthorityFiles.RequirePlainPath(root, Path.GetDirectoryName(destination)!, "socket_output_path");
        }
        using var lease = NativeAotPayload.OpenQualificationLease(root);
        Output(); Need(!Path.Exists(destination), "output_exists"); byte[] bytes = Create(root, evidenceRoot, asynchronous);
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

    private static byte[] Header(int manifest)
    {
        byte[] header = new byte[64]; BinaryPrimitives.WriteUInt64LittleEndian(header, LinuxElfPackage.Magic);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(8), 1); BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(12), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(16), 64); BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(20), (uint)manifest);
        BinaryPrimitives.WriteUInt64LittleEndian(header.AsSpan(24), ElfBytes); return header;
    }
    private static byte[] Read(string root, string relative, int maximum)
    {
        root = Path.GetFullPath(root); string path = Path.GetFullPath(Path.Combine(root, relative));
        Need(path.StartsWith(root + Path.DirectorySeparatorChar, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal), "input_path");
        AuthorityFiles.RequirePlainPath(root, path, "socket_evidence_path");
        using FileStream file = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        Need(file.Length >= 0 && file.Length <= maximum, "input_size " + relative);
        byte[] bytes = new byte[(int)file.Length]; file.ReadExactly(bytes); Need(file.ReadByte() == -1, "input_changed"); return bytes;
    }
    private static string Hash(ReadOnlySpan<byte> value) => Convert.ToHexString(SHA256.HashData(value));
    private static void Need(bool condition, string reason) { if (!condition) throw new InvalidDataException("socket_package_" + reason); }
}
