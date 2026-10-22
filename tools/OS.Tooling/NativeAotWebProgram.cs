using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OS.Manifest;

namespace OS.Tooling;

/// <summary>Immutable host lookup for the exactly pinned qualified MinimalApiSmoke
/// ELF and its reviewed complete decoder index. Selected configuration remains
/// package data. Construction binds relevant current app/build/publisher sources;
/// it starts no app and grants no guest, HTTP or lifecycle acceptance.</summary>
public sealed class NativeAotWebProgram
{
    internal const int MaximumDisassemblyBytes = 128 * 1024 * 1024;
    internal const int ElfBytes = 8_870_568;
    private const string ElfSha256 = "48FC94E3559A72CE94B20FE7AF55335C5E2B5368656D626A624643E2D7F6BCB8";
    private readonly NativeAotInstructionIndex index;
    internal string SourceRoot { get; }
    /// <summary>Complete decoder identity; a report alone grants no lookup authority.</summary>
    public readonly record struct Inspection(int InstructionCount, int SyscallCount, string Sha256);
    /// <summary>Reviewed complete instruction/syscall identity of this prepared lookup.</summary>
    public Inspection Metadata => Report(index);
    private NativeAotWebProgram(string root, NativeAotInstructionIndex value)
    { SourceRoot = Path.GetFullPath(root); index = value; }
    /// <summary>Accepts a decoded instruction start, excluding operands and load padding.</summary>
    public bool IsInstruction(ulong address) => index.IsInstruction(address);
    /// <summary>Accepts RIP immediately after a decoded original SYSCALL.</summary>
    public bool IsSyscallReturn(ulong address) => index.IsSyscallReturn(address);

    internal readonly record struct Configuration(string Body, string Json, uint Arguments, uint Environment, uint SettingsBytes, bool LogQuota);

    // The external proof deliberately admits one small, explicit configuration
    // shape. General IConfiguration precedence is an application concern. The
    // CLI arguments locate the immutable file and bind 8080; /config separately
    // observes ASPNETCORE_URLS, which may differ from that actual listener.
    internal static unsafe Configuration DescribeConfiguration(ReadOnlySpan<byte> package)
    {
        var view = RequirePackage(package); var m = view.Manifest;
        string manifest = StrictText(package.Slice((int)view.ManifestOffset, (int)view.ManifestBytes));
        Need(manifest.Split('\n').Contains("args=--urls http://0.0.0.0:8080 --contentRoot /app/apps", StringComparer.Ordinal), "configuration_arguments");
        Dictionary<string, string> environment = new(StringComparer.Ordinal);
        foreach (string line in manifest.Split('\n').Where(x => x.StartsWith("env.", StringComparison.Ordinal)))
        {
            int at = line.IndexOf('='); environment.Add(line[4..at], line[(at + 1)..]);
        }
        Need(environment.Keys.All(x => x is "CLROS_USER_IRQS" or "DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE" or "ASPNETCORE_URLS" or "CLROS_LOG_BUDGET_CASE") &&
            environment.GetValueOrDefault("DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE") == "false", "configuration_environment");
        bool quota = environment.ContainsKey("CLROS_LOG_BUDGET_CASE");
        Need(!quota || environment["CLROS_LOG_BUDGET_CASE"] == "records64", "configuration_log_budget");
        string endpoint = environment.GetValueOrDefault("ASPNETCORE_URLS", "http://0.0.0.0:8080");
        Need(endpoint is "http://0.0.0.0:8080" or "http://0.0.0.0:8081", "configuration_endpoint");
        fixed (byte* source = package)
        {
            Need(LinuxElfPackage.GetDataFile(source, (uint)package.Length, 0, out var data) == LinuxElfStatus.Success, "configuration_settings");
            using JsonDocument json = JsonDocument.Parse(package.Slice((int)data.DataOffset, (int)data.DataBytes).ToArray());
            Need(json.RootElement.EnumerateObject().Count() == 1 && json.RootElement.TryGetProperty("OS_SMOKE_EXPECTED_BODY", out var value) &&
                value.ValueKind == JsonValueKind.String, "configuration_settings_shape");
            string body = json.RootElement.GetProperty("OS_SMOKE_EXPECTED_BODY").GetString()!;
            Need(body.Length is > 0 and <= 256 && body.All(c => c is >= ' ' and <= '~'), "configuration_body");
            string response = JsonSerializer.Serialize(new { appId = "minimal.api.smoke", contractId = "kestrel.smoke.package.v0",
                expectedBodyKey = "OS_SMOKE_EXPECTED_BODY", expectedBody = body, expectedBodyBytes = Encoding.UTF8.GetByteCount(body),
                listenEndpoint = endpoint, healthPath = "/healthz" });
            return new(body, response, 5, m.EnvironmentCount, data.DataBytes, quota);
        }
    }
    private static string StrictText(ReadOnlySpan<byte> bytes) => new UTF8Encoding(false, true).GetString(bytes);
    private static Inspection Report(NativeAotInstructionIndex value) => new(value.Report.InstructionCount, value.Report.SyscallCount, value.Report.Sha256);
    private static NativeAotInstructionIndex Parse(ReadOnlySpan<byte> elf, string dump) => NativeAotInstructionIndex.Parse(elf, dump,
        "web_instruction_", "CLROS_WEB_INSTRUCTIONS_V1\n", 2_000_000, MaximumDisassemblyBytes);

    /// <summary>Returns a bounded offline report over borrowed exact executable
    /// rows. Partial or synthetic reports confer no prepared-program authority.</summary>
    public static Inspection Inspect(ReadOnlySpan<byte> elf, string dump) => Report(Parse(elf, dump));

    /// <summary>Requires the original ELF, bounded V2 web package policy and
    /// independently reviewed complete index. Source files stay locked/unchanged
    /// through preparation; supplied package/text remain caller-owned synchronous
    /// borrows. Missing, stale, redirected or altered inputs throw before return.</summary>
    public static NativeAotWebProgram Create(string root, ReadOnlySpan<byte> package, string dump)
    {
        LinuxElfPackageView view = RequirePackage(package);
        using AuthorityFiles sources = new(); HoldSources(root, sources);
        var parsed = Parse(package.Slice((int)view.PayloadOffset, (int)view.PayloadBytes), dump);
        if (Report(parsed) != Reviewed || !parsed.SyscallContinuationsValid || !parsed.IsInstruction(0x408f77))
            throw new InvalidDataException("web_instruction_identity");
        HoldSources(root, sources); // Also recheck absent overrides and the complete app inventory.
        sources.VerifyUnchanged(); return new(root, parsed);
    }

    // LLVM22.1.3: all 4,586,191 bytes of five executable sections, every 198
    // syscall continuation and the original entry. Executable-load bytes outside
    // these sections grant no starts. This is not a control-flow proof.
    private static Inspection Reviewed => new(1_429_880, 198, "89092BF5DBEA03886D1EABA1389E36528B837172777C69B2D5FFE4EC04329C86");

    /// <summary>Reads distinct bounded plain single-link package/dump snapshots
    /// under concurrent read locks, then prepares lookup. Files remain caller-owned;
    /// invalid UTF8, aliases, changing reads or failed authority checks throw.</summary>
    public static NativeAotWebProgram Read(string root, string packagePath, string disassemblyPath)
    {
        packagePath = Path.GetFullPath(packagePath); disassemblyPath = Path.GetFullPath(disassemblyPath);
        using AuthorityFiles files = new();
        var package = files.Open(Path.GetDirectoryName(packagePath)!, packagePath, "web package", "web_instruction_package", LinuxElfPackage.MaximumBytes, singleLink: true);
        var dump = files.Open(Path.GetDirectoryName(disassemblyPath)!, disassemblyPath, "web disassembly", "web_instruction_dump", MaximumDisassemblyBytes, singleLink: true);
        AuthorityFiles.RequireDistinct(package, dump);
        var program = Create(root, package.ReadBytes(LinuxElfPackage.MaximumBytes, "web_instruction_package"),
            dump.ReadUtf8(MaximumDisassemblyBytes, "web_instruction_dump"));
        files.VerifyUnchanged(); return program;
    }

    private static unsafe LinuxElfPackageView RequirePackage(ReadOnlySpan<byte> package)
    {
        var view = LinuxElfPackage.Validate(package);
        Need(view.Succeeded && view.DataFileCount == 1 && view.PayloadBytes == ElfBytes, "package_identity");
        Need(Convert.ToHexString(SHA256.HashData(package.Slice((int)view.PayloadOffset, (int)view.PayloadBytes))) == ElfSha256, "package_identity");
        var m = view.Manifest; var manifest = package.Slice((int)view.ManifestOffset, (int)view.ManifestBytes);
        Need(manifest.Slice((int)m.Path.Offset, (int)m.Path.Length).SequenceEqual("apps/MinimalApiSmoke"u8) &&
            m.MemoryBytes == 67_108_864 && m.StackBytes == 1_572_864 && m.Threads == 16 && m.Descriptors == 16 &&
            m.Ipv4 == 0 && m.Port == 8080, "package_policy");
        bool userIrqs = false;
        for (uint i = 0; i < m.EnvironmentCount; i++)
            if (manifest.Slice((int)m.EnvironmentRanges[i * 4], (int)m.EnvironmentRanges[i * 4 + 1]).SequenceEqual("CLROS_USER_IRQS"u8))
                userIrqs = manifest.Slice((int)m.EnvironmentRanges[i * 4 + 2], (int)m.EnvironmentRanges[i * 4 + 3]).SequenceEqual("1"u8);
        Need(userIrqs, "package_policy");
        fixed (byte* source = package)
        {
            Need(LinuxElfPackage.GetDataFile(source, (uint)package.Length, 0, out var data) == LinuxElfStatus.Success &&
                package.Slice((int)data.PathOffset, (int)data.PathBytes).SequenceEqual("apps/appsettings.json"u8), "package_settings");
            using JsonDocument settings = JsonDocument.Parse(package.Slice((int)data.DataOffset, (int)data.DataBytes).ToArray());
            Need(settings.RootElement.ValueKind == JsonValueKind.Object, "package_settings");
        }
        return view;
    }

    private static readonly (string Path, string Hash)[] Sources = [
        ("global.json", "FD3FDAAE1B349B01D6E6835EC111903F107A2643F3BCE313FAD4DC20180F5FB2"),
        ("Directory.Build.props", "DF8A0D794DB73060124F5134EB930DCDBF6EE526C2410227F0C668AB6D10C5E5"),
        ("Directory.Build.targets", "85AFD90F9DD0F550B6A3C6AD71D28BF714A8C930158DB872F8DA5D316889931A"),
        ("tools/Directory.Build.props", "44B8268EA6934335DBC79FEF47C1CE1FDEE49393C929C92E322417B14AA321AE"),
        ("tools/OS.Tooling/NativeAotPayload.cs", "44F16ABB380FE7F3CD42BA3C7E54710172F0320998CF2E85E296F8A7CE184AA5"),
        ("apps/MinimalApiSmoke/Program.cs", "EA20A6457F22DBBA7EB0FC0423266E41B335B405802D3F4D980690FDBE539C5E"),
        ("apps/MinimalApiSmoke/KestrelChurnExercise.cs", "5EEF724987A8FEA8BF4ADF667DA5DC578B8DA1A6A62477A550295546D66CBDF1"),
        ("apps/MinimalApiSmoke/KestrelSignalProbe.cs", "D44146A94C28492DCB739E02A701E38F2F0C5C90F0E93103AB9E91C08732B0A7"),
        ("apps/MinimalApiSmoke/KestrelSignalProbeAbi.cs", "DF485562F1A9384E02A0700D2D1C7A39A3C96EF0804D68B527DBC078EBC275FA"),
        ("apps/MinimalApiSmoke/KestrelSignalRaceState.cs", "DC9280162252D0A0D3134AF1AFB4BF9C891602F11CDB093C3B94032D779BB80B"),
        ("apps/MinimalApiSmoke/KestrelVectorExercise.cs", "0A2E42A133FD7D56767686D388D9DA22D464B3955E6AA31BF386D5CDF3A87001"),
        ("apps/MinimalApiSmoke/MinimalApiSmoke.csproj", "CA968E841DBCACBC8C78B3201F97419A43321458E2D6617BB9B83C0D46023C80"),
        ("apps/MinimalApiSmoke/appsettings.json", "FF0AB2FFB263DF50157F565C6AA386B50D23C9AC9A03F47D53B225C0471A0CE6"),
        ("apps/MinimalApiSmoke/appsettings.Development.json", "817DB0BF2ECCA78364C41DBAF7949BF2504CF47A1C3D0D54F3BE8178FEFC030B")];

    internal static void HoldSources(string root, AuthorityFiles files)
    {
        root = Path.GetFullPath(root);
        foreach (string relative in new[] { "NuGet.Config", "nuget.config", "Directory.Packages.props",
            "apps/Directory.Build.props", "apps/Directory.Build.targets", "apps/Directory.Packages.props" })
            Need(!Path.Exists(Path.Combine(root, relative)), "source_override " + relative);
        string directory = Path.Combine(root, "apps", "MinimalApiSmoke");
        AuthorityFiles.RequirePlainPath(root, directory, "web_source_path");
        Stack<string> pending = new(); pending.Push(directory); List<string> names = []; int entries = 0;
        while (pending.TryPop(out string? next)) foreach (string path in Directory.EnumerateFileSystemEntries(next))
        {
            Need(++entries <= 128, "source_inventory");
            FileAttributes attributes = File.GetAttributes(path); Need((attributes & FileAttributes.ReparsePoint) == 0, "source_link");
            if ((attributes & FileAttributes.Directory) != 0)
            { if (Path.GetFileName(path) is not ("bin" or "obj" or "artifacts")) pending.Push(path); }
            else names.Add(Path.GetRelativePath(directory, path).Replace('\\', '/'));
        }
        Need(names.Order(StringComparer.Ordinal).SequenceEqual(new[] { "KestrelChurnExercise.cs", "KestrelSignalProbe.cs", "KestrelSignalProbeAbi.cs", "KestrelSignalRaceState.cs", "KestrelVectorExercise.cs", "MinimalApiSmoke.csproj", "Program.cs", "appsettings.Development.json", "appsettings.json" }.Order(StringComparer.Ordinal)), "source_inventory");
        foreach (var source in Sources)
            Need(files.Open(root, Path.Combine(root, source.Path), "web app source", "web_source", 1024 * 1024).Sha256.Equals(source.Hash, StringComparison.OrdinalIgnoreCase), "source_identity " + source.Path);
    }
    private static void Need(bool valid, string reason)
    { if (!valid) throw new InvalidDataException("web_" + reason); }
}
