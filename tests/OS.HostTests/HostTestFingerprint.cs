using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using OS.Tooling;

internal sealed partial class HostTestFingerprint
{
    private readonly CSharpSourceCatalog catalog;
    private readonly HostReuseSafety safety;
    private readonly string shared;
    private Lazy<string?> native = new(() => null);
    private readonly Dictionary<string, string> inputFiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, FileStamp> inputStamps = new(StringComparer.OrdinalIgnoreCase);
    private string environment = "";

    // Synthetic catalogs are used by dependency controls. Live runs always use
    // the executing assembly's authenticated compiler receipt below.
    internal HostTestFingerprint(IReadOnlyDictionary<string, string> sources, string shared, IEnumerable<string>? symbols = null)
        : this(new CSharpSourceCatalog(sources.Select(pair => new CSharpSourceDocument(pair.Key,
                HostRunCheckpoint.HashText(pair.Value), CSharpSyntaxTree.ParseText(pair.Value,
                    new CSharpParseOptions(LanguageVersion.CSharp14, preprocessorSymbols: symbols), pair.Key))),
                ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
                    .Select(path => Microsoft.CodeAnalysis.MetadataReference.CreateFromFile(path))), shared)
    {
    }

    internal HostTestFingerprint(CSharpSourceCatalog catalog, string shared)
    {
        this.catalog = catalog;
        this.shared = HostRunCheckpoint.HashText(shared + "\n" + JsonSerializer.Serialize(catalog.Documents.Select(document =>
        {
            var options = (CSharpParseOptions)document.Syntax.Options;
            return new
            {
                document.Path,
                options.LanguageVersion,
                options.Kind,
                options.DocumentationMode,
                Symbols = options.PreprocessorSymbolNames,
                options.Features
            };
        })));
        safety = new(catalog);
    }

    internal static IReadOnlyList<CSharpSourceDocument> SharedSources(CSharpSourceCatalog catalog)
    {
        var entry = catalog.Compilation.GetEntryPoint(default);
        return catalog.Dependencies.Closure(entry is null ? [] : [entry.ContainingType]);
    }

    internal static HostTestFingerprint Create(string root)
    {
        Dictionary<string, string> files = new(StringComparer.OrdinalIgnoreCase);
        var inventory = CSharpBuiltSourceInventory.Read(root, typeof(HostTestFingerprint).Assembly.Location);
        var sources = inventory.Sources.ToDictionary(source => source.Path, source => (source.Text, source.Sha256), StringComparer.OrdinalIgnoreCase);
        string shared = SharedFingerprint(root, files, sources, inventory.Catalog, inventory.Artifacts);
        shared = HostRunCheckpoint.HashText(shared + "\n" + string.Join("\n", inventory.Arguments)
            + "\n" + string.Join("\n", inventory.NonSourceHashes.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => pair.Key + ":" + pair.Value)));
        var result = new HostTestFingerprint(inventory.Catalog, shared)
        {
            native = new(() => HostNativeResumePolicy.InputFingerprint(root)),
            environment = EnvironmentFingerprint()
        };
        foreach (var pair in sources)
            files[pair.Key] = pair.Value.Sha256;
        foreach (var pair in inventory.InputHashes)
            files[pair.Key] = pair.Value;
        // Keep the receipt-bound PE/PDB hashes. Reopening and replacing a hash
        // here could join a changed binary to the earlier source snapshot.
        foreach (var pair in files)
        {
            result.inputFiles[pair.Key] = pair.Value;
            result.inputStamps[pair.Key] = FileStamp.Read(pair.Key);
        }

        if (!FilesMatch(files) || !InventoryRecorded(root, files))
            throw new InvalidOperationException("checkpoint_inputs_changed_during_snapshot");
        return result;
    }

    // Parsing and provenance must come from one read, including BOM detection
    // equivalent to File.ReadAllText. Reopening for the hash could join revisions.
    internal static (string Text, string Sha256) ReadSource(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        using var stream = new MemoryStream(bytes, writable: false);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return (reader.ReadToEnd(), Convert.ToHexString(SHA256.HashData(bytes)));
    }

    // Cheap scheduling guard, independent of cache eligibility. A changed stamp
    // requires the original byte hash, allowing fixtures to restore exact bytes.
    // This supplements, rather than replaces, exact post-pass and final checks:
    // equal-length edits that preserve timestamps still require those full hashes.
    internal bool BoundaryInputsUnchanged(string root) => EnvironmentFingerprint() == environment && StampedFilesMatch(inputFiles, inputStamps) && InventoryRecorded(root, inputFiles);
    internal static bool InventoryRecorded(string root, IReadOnlyDictionary<string, string> recorded) =>
        InventoryRecorded(root, recorded, HostProjectOutputs.Files(root, typeof(HostTestFingerprint).Assembly));

    internal static bool InventoryRecorded(string root, IReadOnlyDictionary<string, string> recorded, IEnumerable<string> outputFiles) =>
        RepositoryFiles(root).All(file => recorded.ContainsKey(Path.GetFullPath(Path.Combine(root, file))))
        && HostBuildInputs.SourceInputs(root).All(file => recorded.ContainsKey(Path.GetFullPath(file)))
        && outputFiles.All(recorded.ContainsKey);
    internal static bool StampedFilesMatch(IReadOnlyDictionary<string, string> files, IDictionary<string, FileStamp> stamps)
    {
        foreach (var pair in files)
        {
            FileStamp current = FileStamp.Read(pair.Key);
            if (stamps.TryGetValue(pair.Key, out var previous) && current == previous)
                continue;
            if ((current.Exists ? HostRunCheckpoint.HashFile(pair.Key) : "MISSING") != pair.Value)
                return false;
            stamps[pair.Key] = current;
        }

        return true;
    }

    internal bool InputsUnchanged(string root, HostTestRunner.Fixture[] fixtures) => ExactRunInputsUnchanged(root) && (!fixtures.Any(f => f.Lane == HostLane.Native) || !HostNativeResumePolicy.OwnsOutputs(fixtures) || native.Value == HostNativeResumePolicy.InputFingerprint(root));
    // Final validation includes the original host DLL and every recorded byte,
    // even when every selected fixture was fresh or its timestamps were restored.
    internal bool ExactRunInputsUnchanged(string root) => EnvironmentFingerprint() == environment && FilesMatch(inputFiles) && InventoryRecorded(root, inputFiles);
    internal static bool FilesMatch(IReadOnlyDictionary<string, string> files) => files.All(pair => (File.Exists(pair.Key) ? HostRunCheckpoint.HashFile(pair.Key) : "MISSING") == pair.Value);
    private static string EnvironmentFingerprint() => HostRunCheckpoint.HashText(string.Join("\n", Environment.GetEnvironmentVariables().Keys.Cast<string>().Order(StringComparer.Ordinal).Select(key => key + "=" + Environment.GetEnvironmentVariable(key))));
    private static string CompilerOptions()
    {
        using var stream = typeof(HostTestFingerprint).Assembly.GetManifestResourceStream("host-test-options.txt") ?? throw new InvalidOperationException("checkpoint_compiler_options_missing");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    internal string For(HostTestRunner.Fixture[] fixtures, string? caseFilter)
    {
        string type = HostTestRunner.Owner(fixtures[0].Method);
        string selection = JsonSerializer.Serialize(new
        {
            Cases = fixtures.Select(f => new { f.Id, f.Name, f.Lane, f.Order }),
            CaseFilter = caseFilter
        });
        bool isNative = fixtures.Any(f => f.Lane == HostLane.Native);
        bool owned = HostNativeResumePolicy.OwnsOutputs(fixtures);
        return For(type, selection, isNative, owned, isNative && owned ? native.Value : null);
    }

    internal string For(string type, string selection, bool native = false, bool ownedArtifacts = false, string? nativeIdentity = null)
    {
        var owner = catalog.Compilation.GetTypeByMetadataName(type);
        if (owner is null || owner.DeclaringSyntaxReferences.Length == 0)
            return "fresh:unknown_type:" + HostRunCheckpoint.HashText(shared + selection);
        var dependencies = catalog.Dependencies.Closure([owner]);
        string? reason = native && (!ownedArtifacts || nativeIdentity is null)
            ? "native_inputs_unreviewed_or_unavailable" : safety.Reason(owner, ownedArtifacts);
        // Preserve actual compiler source order, including partial initializers.
        string hash = HostRunCheckpoint.HashText(shared + "\n" + selection + "\n" + nativeIdentity + "\n"
            + string.Join("\n", dependencies.Select(document => document.Path + ":" + document.Sha256)));
        return (reason is null ? "" : "fresh:" + reason + ":") + hash;
    }

    private static string SharedFingerprint(string root, Dictionary<string, string> snapshot, IReadOnlyDictionary<string, (string Text, string Sha256)> sources,
        CSharpSourceCatalog catalog, CSharpCompilerArtifacts artifacts)
    {
        var infrastructure = SharedSources(catalog).Select(document => document.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        List<string> inputs = ["CLROS_HOST_RESUME_2", root, Environment.MachineName, System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription, System.Runtime.InteropServices.RuntimeInformation.OSDescription];
        foreach (string relative in RepositoryFiles(root).Order(StringComparer.Ordinal))
        {
            if (sources.TryGetValue(Path.GetFullPath(Path.Combine(root, relative)), out var source))
            {
                // Global imports/assembly attributes can affect unrelated fixtures.
                if (!infrastructure.Contains(Path.GetFullPath(Path.Combine(root, relative))))
                    continue;
            }

            AddFile(Path.Combine(root, relative));
        }

        // The changing host DLL is recorded as provenance, not a shared cache key.
        // Its fixture sources/dependencies and compiler options are keyed separately.
        var producer = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { artifacts.AssemblyPath, artifacts.SymbolsPath };
        if (artifacts.ReceiptPath is { } receipt)
            producer.Add(receipt);
        foreach (var pair in HostProjectOutputs.Snapshot(root, HostProjectOutputs.Read(root, typeof(HostTestFingerprint).Assembly), [AppContext.BaseDirectory]))
        {
            if (!producer.Contains(pair.Key))
                inputs.Add(pair.Key + ":" + pair.Value);
            snapshot[pair.Key] = pair.Value;
        }

        foreach (var generated in catalog.Documents.Where(document => !sources.ContainsKey(document.Path)))
            inputs.Add(generated.Path + ":" + generated.Sha256);
        inputs.Add(CompilerOptions());
        AddFile(Environment.ProcessPath!);
        AddFile(typeof(object).Assembly.Location);
        var sdk = ProcessRunner.RunAsync(new("dotnet", ["--info"], root)).GetAwaiter().GetResult();
        if (!sdk.Succeeded)
            throw new InvalidOperationException("checkpoint_sdk_identity_failed");
        inputs.Add(sdk.StandardOutput);
        // Values (including credentials) are never serialized: only the combined digest.
        inputs.Add(EnvironmentFingerprint());
        string? llvm = Environment.GetEnvironmentVariable("CLROS_LLVM_OBJDUMP") ?? (OperatingSystem.IsWindows() ? Environment.GetEnvironmentVariable("CLROS_LLVM_OBJDUMP", EnvironmentVariableTarget.User) : null);
        if (!string.IsNullOrEmpty(llvm))
            AddFile(llvm);
        return HostRunCheckpoint.HashText(string.Join("\n", inputs));
        void AddFile(string path)
        {
            path = Path.GetFullPath(path);
            string hash = sources.TryGetValue(path, out var source) ? source.Sha256 : File.Exists(path) ? HostRunCheckpoint.HashFile(path) : "MISSING";
            inputs.Add(path + ":" + hash);
            snapshot[path] = hash;
        }
    }

    private static string[] RepositoryFiles(string root)
    {
        var inventory = ProcessRunner.RunAsync(new("git", ["ls-files", "--cached", "--others", "--exclude-standard", "-z"], root)).GetAwaiter().GetResult();
        if (!inventory.Succeeded)
            throw new InvalidOperationException("checkpoint_inventory_failed");
        return inventory.StandardOutput.Split('\0', StringSplitOptions.RemoveEmptyEntries).Where(file => !file.StartsWith("artifacts/", StringComparison.Ordinal)).Distinct(StringComparer.Ordinal).ToArray();
    }
}
