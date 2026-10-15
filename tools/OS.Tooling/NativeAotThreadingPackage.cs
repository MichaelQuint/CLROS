using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Manifest;

namespace OS.Tooling;

/// <summary>Host-only packaging of step1267's two exact upstream Linux payloads.
/// Reads pinned portable producer evidence and current relevant raw sources;
/// never compiles, maps or executes an app. Returned bytes remain caller-owned.
/// Missing, changed, linked or oversized inputs throw before publication.</summary>
public static class NativeAotThreadingPackage
{
    private const string Qualification = "artifacts/threading-payload-r01/";
    private const string ProjectHash = "D8AFB29D1F4465AD7D3EF3F4A292255DBC5E6975FFD4AD02948E912B1D88564B";
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private static readonly (string Path, string Hash)[] SharedSources = [
        ("global.json", "FD3FDAAE1B349B01D6E6835EC111903F107A2643F3BCE313FAD4DC20180F5FB2"),
        ("Directory.Build.props", "DF8A0D794DB73060124F5134EB930DCDBF6EE526C2410227F0C668AB6D10C5E5"),
        ("Directory.Build.targets", "85AFD90F9DD0F550B6A3C6AD71D28BF714A8C930158DB872F8DA5D316889931A"),
        ("tools/Directory.Build.props", "44B8268EA6934335DBC79FEF47C1CE1FDEE49393C929C92E322417B14AA321AE"),
        ("tools/OS.Tooling/NativeAotPayload.cs", "44F16ABB380FE7F3CD42BA3C7E54710172F0320998CF2E85E296F8A7CE184AA5"),
        ("tools/OS.Tooling/NativeAotThreadingPayload.cs", "01F119E451ABB2747430AF2C8F9E3E96472A0C95D64C81AF36ABF3079FE10AF6"),
        ("tools/OS.Tooling/NativeAotThreadingGuest.cs", "65C4141492E9FC969B29F73FC1F4188CF8DD41EBE5E78A2B46B5B119E1B40FD1")];

    internal readonly record struct Payload(string Name, int Bytes, string Hash, uint Threads, string Source,
        int DllBytes, string DllHash, int ObjectBytes, string ObjectHash, int StdoutBytes, string StdoutHash, int Traces);
    internal static Payload Specification(string app) => app switch
    {
        "NativeAotSuspensionSmoke" => new(app, 1_320_176, "F135EAC7774D92C9091C0A6954B915E60E403B80C50253FD396E2DD26370B8A1", 4,
            "33D3FC94C6A82EF52F83A773A7CB7890A913896502882BD5984036326410DA68",
            7680, "7E67BE8896613F2D38113027BE0563FE408D6017F8D9B381F93CF314030160CF",
            4378164, "1978406603E5A8430352064D2EBC08C6F3366C201901CA2505658635CF376F8F",
            384, "1AD842FF6098A7B43B76FEE1EC20A27A4EC2DB996C44A14F59AA4430CC3A0635", 4),
        "NativeAotThreadPoolSmoke" => new(app, 1_577_712, "E7A89ECABF8FF979AC018026DC6AC9A3174B973B9539FEB221270F09532A9D24", 8,
            "9BB7F4F0483521A6C428CF6CC656F23372F1B55805473D38893E57403C9DB5D8",
            22016, "AECF574A9990222C5FD0337BC0DC131C518FE59D4E17D303303806F14E40BBB5",
            5880820, "E27BA8078E44BFADFCA5F2B3074AEA5BE978D98064A504B563AC320A730FA956",
            629, "E2B9F2E1BDCDECD14214E9576180C2D845BF605A570FA0C64D9B5BA0D7B95049", 7),
        _ => throw new ArgumentException("threading_package_app", nameof(app))
    };

    /// <summary>Canonical no-argument/no-runtime-override policy: 64 MiB memory,
    /// 1.5 MiB main stack, four/eight thread slots, sixteen descriptors and
    /// explicit user IRQs. The required manifest endpoint grants no socket.</summary>
    public static string Manifest(string app)
    {
        var p = Specification(app);
        return "format=clros-linux-elf\nversion=1\nprofile=linux-musl-x64-static-v1\napp.path=apps/" + p.Name +
            "\nargs=\ndigest=" + p.Hash.ToLowerInvariant() + "\nmemory=67108864\nstack=1572864\nthreads=" +
            p.Threads.ToString(System.Globalization.CultureInfo.InvariantCulture) +
            "\ndescriptors=16\nlisten=127.0.0.1:8080\nenv.CLROS_USER_IRQS=1\n";
    }

    private static IEnumerable<(string Path, string Hash)> Sources(Payload p) => SharedSources.Concat([
        ($"apps/{p.Name}/Program.cs", p.Source), ($"apps/{p.Name}/{p.Name}.csproj", ProjectHash)]);

    /// <summary>Checks the selected app and seven relevant shared build/producer
    /// sources against the original raw hashes. Unrelated kernel/tooling edits
    /// do not become a claim of whole-repository identity.</summary>
    public static void RequireCurrentSources(string root, string app)
    {
        var p = Specification(app); root = Path.GetFullPath(root);
        foreach (string relative in new[] { "NuGet.Config", "nuget.config", "apps/Directory.Build.props", "apps/Directory.Build.targets" })
            Need(!Path.Exists(Path.Combine(root, relative)), "source_override " + relative);
        string directory = Path.Combine(root, "apps", app); AuthorityFiles.RequirePlainPath(root, directory, "threading_source_path");
        Stack<string> pending = new(); pending.Push(directory); List<string> files = []; int entries = 0;
        while (pending.TryPop(out string? next)) foreach (string path in Directory.EnumerateFileSystemEntries(next))
        {
            Need(++entries <= 128, "source_inventory");
            if (Path.GetFileName(path) is "bin" or "obj" or "artifacts" && Directory.Exists(path)) continue;
            FileAttributes attributes = File.GetAttributes(path); Need((attributes & FileAttributes.ReparsePoint) == 0, "source_link");
            if ((attributes & FileAttributes.Directory) != 0) pending.Push(path); else files.Add(Path.GetRelativePath(directory, path).Replace('\\', '/'));
        }
        Need(files.Order(StringComparer.Ordinal).SequenceEqual(new[] { app + ".csproj", "Program.cs" }.Order(StringComparer.Ordinal)), "source_inventory");
        foreach (var source in Sources(p))
            Need(Hash(Read(root, source.Path, 1024 * 1024)) == source.Hash, "source_identity " + source.Path);
    }

    /// <summary>Creates fresh bytes only after the fixed receipt, selected ELF,
    /// produced DLL/object, stdout and every complete thread trace match. The
    /// receipt binds Linux SDK/native inputs; unavailable Linux files are not
    /// claimed to have been rehashed on this host. All evidence is read-only.</summary>
    public static unsafe byte[] Create(string root, string evidenceRoot, string app)
    {
        var p = Specification(app); RequireCurrentSources(root, app);
        byte[] receipt = Pinned(evidenceRoot, Qualification + "receipt.json", 1_221_776,
            "68FF27629E8BA6AE044D6D61C761EEED5631EA1A49E621A4E8FB9857EF22690B");
        using JsonDocument document = JsonDocument.Parse(receipt); var data = document.RootElement;
        Need(data.GetProperty("schema").GetInt32() == 1 && data.GetProperty("outcome").GetString() == "passed" &&
            data.GetProperty("sdk").GetString() == NativeAotPayload.SdkVersion &&
            data.GetProperty("runtime").GetString() == NativeAotPayload.RuntimeVersion, "producer_policy");
        foreach (var source in Sources(p)) Need(data.GetProperty("sources").GetProperty(source.Path).GetString() == source.Hash, "producer_source");
        var commands = data.GetProperty("commands");
        Need(commands.GetArrayLength() == 25 && commands.EnumerateArray().All(c => c.GetProperty("exitCode").GetInt32() == 0), "producer_commands");
        string intermediate = $"apps/{app}/obj/Release/net11.0/linux-musl-x64/";
        _ = Pinned(evidenceRoot, intermediate + app + ".dll", p.DllBytes, p.DllHash);
        _ = Pinned(evidenceRoot, intermediate + "native/" + app + ".o", p.ObjectBytes, p.ObjectHash);
        Need(data.GetProperty("nativeInputs").GetProperty("/clros-threading-r01/" + intermediate + app + ".dll").GetString() == p.DllHash &&
            data.GetProperty("nativeInputs").GetProperty("/clros-threading-r01/" + intermediate + "native/" + app + ".o").GetString() == p.ObjectHash, "producer_outputs");
        string stdout = Utf8.GetString(Pinned(evidenceRoot, Qualification + app + "-run.stdout.log", p.StdoutBytes, p.StdoutHash));
        if (p.Threads == 4) NativeAotThreadingGuest.ValidateSuspensionOutput(stdout); else NativeAotThreadingGuest.ValidateThreadPoolOutput(stdout);
        string traces = Path.Combine(Path.GetFullPath(evidenceRoot), Qualification); AuthorityFiles.RequirePlainPath(Path.GetFullPath(evidenceRoot), traces, "threading_evidence_path");
        var observed = NativeAotThreadingPayload.ReadTraceInventory(traces, app);
        var recorded = data.GetProperty("applications").EnumerateArray().Single(a => a.GetProperty("name").GetString() == app);
        Need(recorded.GetProperty("sha256").GetString() == p.Hash && observed.Files.Count == p.Traces &&
            recorded.GetProperty("traceFiles").EnumerateObject().Count() == p.Traces &&
            observed.Files.All(f => recorded.GetProperty("traceFiles").GetProperty(f.Key).GetString() == f.Value) &&
            observed.Syscalls.SequenceEqual(recorded.GetProperty("observedSyscalls").EnumerateArray().Select(s => s.GetString()!)), "producer_traces");
        byte[] elf = Pinned(evidenceRoot, Qualification + app + "/" + app, p.Bytes, p.Hash);
        _ = NativeAotPayload.Inspect(elf);
        fixed (byte* image = elf) Need(LinuxElfLoader.Validate(image, (uint)elf.Length, 64 * 1024 * 1024, 1_572_864, out _) == LinuxElfLoadStatus.Ready, "loader");
        byte[] manifest = Encoding.ASCII.GetBytes(Manifest(app)), package = new byte[64 + manifest.Length + elf.Length];
        Header(manifest.Length, elf.Length).CopyTo(package, 0); manifest.CopyTo(package, 64); elf.CopyTo(package, 64 + manifest.Length);
        RequirePackage(app, package); RequireCurrentSources(root, app); return package;
    }

    /// <summary>Accepts only the exact canonical header/policy and qualified ELF.
    /// This checks bytes alone and grants neither prepared instruction lookup
    /// nor guest execution/transcript authority. Unknown app names throw.</summary>
    public static void RequirePackage(string app, ReadOnlySpan<byte> package)
    {
        var p = Specification(app); byte[] manifest = Encoding.ASCII.GetBytes(Manifest(app));
        Need(package.Length == 64 + manifest.Length + p.Bytes && package[..64].SequenceEqual(Header(manifest.Length, p.Bytes)), "identity");
        Need(LinuxElfPackage.Validate(package).Succeeded && package.Slice(64, manifest.Length).SequenceEqual(manifest) &&
            Hash(package[(64 + manifest.Length)..]) == p.Hash, "identity");
    }

    /// <summary>Validates all input first, then flushes an owned sibling temporary
    /// and publishes by no-overwrite rename below this repository's artifacts.
    /// Existing output/linked parents reject. Only the owned temporary is deleted
    /// on failure. Returns package SHA256, never an execution success marker.</summary>
    public static string Write(string root, string evidenceRoot, string app, string destination)
    {
        _ = Specification(app); root = Path.GetFullPath(root); destination = Path.GetFullPath(destination);
        string artifacts = Path.Combine(root, "artifacts");
        void Output()
        {
            Need(destination.StartsWith(artifacts + Path.DirectorySeparatorChar, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal), "output_path");
            AuthorityFiles.RequirePlainPath(root, Path.GetDirectoryName(destination)!, "threading_output_path");
        }
        Output(); Need(!Path.Exists(destination), "output_exists"); byte[] bytes = Create(root, evidenceRoot, app);
        string temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp"; bool owns = false;
        try
        {
            Output(); using (FileStream file = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { owns = true; file.Write(bytes); file.Flush(flushToDisk: true); }
            RequireCurrentSources(root, app); Output(); File.Move(temporary, destination, overwrite: false);
        }
        finally { if (owns) File.Delete(temporary); }
        return Hash(bytes);
    }

    private static byte[] Header(int manifest, int elf)
    {
        byte[] header = new byte[64]; BinaryPrimitives.WriteUInt64LittleEndian(header, LinuxElfPackage.Magic);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(8), 1); BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(12), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(16), 64); BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(20), (uint)manifest);
        BinaryPrimitives.WriteUInt64LittleEndian(header.AsSpan(24), (ulong)elf); return header;
    }
    private static byte[] Pinned(string root, string path, int length, string hash)
    { byte[] bytes = Read(root, path, length); Need(bytes.Length == length && Hash(bytes) == hash, "evidence_identity " + path); return bytes; }
    private static byte[] Read(string root, string relative, int maximum)
    {
        root = Path.GetFullPath(root); string path = Path.Combine(root, relative);
        AuthorityFiles.RequirePlainPath(root, path, "threading_evidence_path");
        using FileStream file = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        Need(file.Length is > 0 && file.Length <= maximum, "input_size " + relative);
        byte[] bytes = new byte[(int)file.Length]; file.ReadExactly(bytes); Need(file.ReadByte() == -1, "input_changed"); return bytes;
    }
    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static void Need(bool condition, string reason) { if (!condition) throw new InvalidDataException("threading_package_" + reason); }
}
