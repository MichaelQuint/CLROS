using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Tooling;

internal sealed class HostTestFingerprint
{
    private static readonly HashSet<string> SharedSources = new(StringComparer.Ordinal)
    {
        "Program", "HostTestRunner", "HostCheck", "HostRunCheckpoint", "HostTestFingerprint",
        "HostBuildInputs", "HostWorkerScheduler", "HostToolProcess", "HostNativeResumePolicy"
    };
    private readonly IReadOnlyDictionary<string, string> sources;
    private readonly Dictionary<string, HashSet<string>> types = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<string>> tokens = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> hashes = new(StringComparer.Ordinal);
    private readonly string shared;
    private readonly string allSources;
    private Lazy<string?> native = new(() => null);
    private readonly Dictionary<string, string> inputFiles = new(StringComparer.OrdinalIgnoreCase);
    private string environment = "";

    internal HostTestFingerprint(IReadOnlyDictionary<string, string> sources, string shared, IEnumerable<string>? symbols = null)
    {
        this.sources = sources;
        this.shared = shared;
        foreach (var (file, text) in sources)
        {
            var syntax = CSharpSyntaxTree.ParseText(text, new CSharpParseOptions(LanguageVersion.CSharp14, preprocessorSymbols: symbols)).GetRoot();
            var errors = syntax.GetDiagnostics().Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error).ToArray();
            if (errors.Length != 0) throw new InvalidOperationException("checkpoint_source_parse_failed " + file + " " + string.Join(";", errors.Select(d => d.ToString())));
            foreach (var node in syntax.DescendantNodes().OfType<BaseTypeDeclarationSyntax>()
                .Where(node => !node.Ancestors().OfType<BaseTypeDeclarationSyntax>().Any())) Add(node.Identifier.ValueText, file);
            foreach (var node in syntax.DescendantNodes().OfType<DelegateDeclarationSyntax>()
                .Where(node => !node.Ancestors().OfType<BaseTypeDeclarationSyntax>().Any())) Add(node.Identifier.ValueText, file);
            foreach (var method in syntax.DescendantNodes().OfType<MethodDeclarationSyntax>()
                .Where(method => method.ParameterList.Parameters.FirstOrDefault()?.Modifiers.Any(token => token.RawKind == (int)SyntaxKind.ThisKeyword) == true))
                Add(method.Identifier.ValueText, file);
            tokens[file] = Regex.Matches(text, @"[A-Za-z_][A-Za-z_0-9]*").Select(match => match.Value).ToHashSet(StringComparer.Ordinal);
            hashes[file] = HostRunCheckpoint.HashText(text);
        }
        allSources = HostRunCheckpoint.HashText(string.Join("\n", hashes.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Key + ":" + p.Value)));
    }

    private void Add(string type, string file)
    {
        if (!types.TryGetValue(type, out var files)) types[type] = files = new(StringComparer.Ordinal);
        files.Add(file);
    }

    internal static HostTestFingerprint Create(string root)
    {
        Dictionary<string, string> files = new(StringComparer.OrdinalIgnoreCase);
        string shared = SharedFingerprint(root, files);
        var sources = Directory.GetFiles(Path.Combine(root, "tests", "OS.HostTests"), "*.cs");
        var result = new HostTestFingerprint(sources.ToDictionary(path => Path.GetFileNameWithoutExtension(path), File.ReadAllText, StringComparer.Ordinal),
            shared, CompilerOptions().Split('|')[4].Split([';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            { native = new(() => HostNativeResumePolicy.InputFingerprint(root)), environment = EnvironmentFingerprint() };
        foreach (string file in sources.Append(typeof(HostTestFingerprint).Assembly.Location)) files[file] = HostRunCheckpoint.HashFile(file);
        foreach (var pair in files) result.inputFiles[pair.Key] = pair.Value;
        return result;
    }

    internal bool InputsUnchanged(string root, HostTestRunner.Fixture[] fixtures) =>
        EnvironmentFingerprint() == environment && FilesMatch(inputFiles) &&
        HostBuildInputs.SourceInputs(root).All(file => inputFiles.ContainsKey(Path.GetFullPath(file))) &&
        (!fixtures.Any(f => f.Lane == HostLane.Native) || !HostNativeResumePolicy.OwnsOutputs(fixtures) ||
            native.Value == HostNativeResumePolicy.InputFingerprint(root));

    internal static bool FilesMatch(IReadOnlyDictionary<string, string> files) =>
        files.All(pair => (File.Exists(pair.Key) ? HostRunCheckpoint.HashFile(pair.Key) : "MISSING") == pair.Value);

    internal static bool AffectsAllFixtures(string text, IEnumerable<string>? symbols = null)
    {
        var syntax = CSharpSyntaxTree.ParseText(text, new CSharpParseOptions(LanguageVersion.CSharp14, preprocessorSymbols: symbols)).GetRoot();
        return syntax.DescendantNodes().OfType<UsingDirectiveSyntax>().Any(node => node.GlobalKeyword.RawKind == (int)SyntaxKind.GlobalKeyword) ||
            syntax.DescendantNodes().OfType<AttributeListSyntax>().Any(node => node.Target?.Identifier.ValueText is "assembly" or "module") ||
            syntax.DescendantNodes().Any(node => node.GetType().Name == "ExtensionBlockDeclarationSyntax");
    }

    private static string EnvironmentFingerprint() => HostRunCheckpoint.HashText(string.Join("\n",
        Environment.GetEnvironmentVariables().Keys.Cast<string>().Order(StringComparer.Ordinal).Select(key => key + "=" + Environment.GetEnvironmentVariable(key))));

    private static string CompilerOptions()
    {
        using var stream = typeof(HostTestFingerprint).Assembly.GetManifestResourceStream("host-test-options.txt")
            ?? throw new InvalidOperationException("checkpoint_compiler_options_missing");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    internal string For(HostTestRunner.Fixture[] fixtures, string? caseFilter)
    {
        string type = fixtures[0].Method.DeclaringType!.Name;
        string selection = JsonSerializer.Serialize(new { Cases = fixtures.Select(f => new { f.Id, f.Name, f.Lane, f.Order }), CaseFilter = caseFilter });
        bool isNative = fixtures.Any(f => f.Lane == HostLane.Native);
        bool owned = HostNativeResumePolicy.OwnsOutputs(fixtures);
        return For(type, selection, isNative, owned, isNative && owned ? native.Value : null);
    }

    internal string For(string type, string selection, bool native = false, bool ownedArtifacts = false, string? nativeIdentity = null)
    {
        bool fresh = native && (!ownedArtifacts || nativeIdentity is null);
        HashSet<string> dependencies = new(StringComparer.Ordinal);
        Queue<string> pending = new();
        if (!types.TryGetValue(type, out var owners)) return "fresh:unknown_type:" + HostRunCheckpoint.HashText(shared + selection);
        string reason = fresh ? "native_inputs_unreviewed_or_unavailable" : "";
        foreach (string file in owners) pending.Enqueue(file);
        while (pending.TryDequeue(out string? file))
        {
            if (!dependencies.Add(file)) continue;
            string text = sources[file];
            // Unknown external inputs are not cached. This intentionally includes
            // artifact consumers until their input/output ownership is reviewed.
            if (!ownedArtifacts && Regex.IsMatch(text, @"(?i)(?:""artifacts""|artifacts[/\\]|GetTempPath)") ||
                Regex.IsMatch(text, @"(?i)(?:""references""|GetEnvironmentVariable|HttpClient|WebRequest)")) { fresh = true; reason = "external_input_" + file; }
            bool broad = text.Contains("EnumerateFiles", StringComparison.Ordinal) || text.Contains("GetFiles", StringComparison.Ordinal) ||
                text.Contains(".Assembly", StringComparison.Ordinal) || text.Contains("Discover(", StringComparison.Ordinal);
            if (broad)
                return "fresh:dynamic_source_" + file + ":" + HostRunCheckpoint.HashText(shared + selection + allSources);
            foreach (string token in tokens[file])
            {
                if (types.TryGetValue(token, out var files))
                    foreach (string other in files)
                        if (!SharedSources.Contains(other) && !dependencies.Contains(other)) pending.Enqueue(other);
                if (sources.ContainsKey(token) && !SharedSources.Contains(token) && !dependencies.Contains(token)) pending.Enqueue(token);
            }
        }
        string hash = HostRunCheckpoint.HashText(shared + "\n" + selection + "\n" + nativeIdentity + "\n" +
            string.Join("\n", dependencies.Order(StringComparer.Ordinal).Select(file => file + ":" + hashes[file])));
        return (fresh ? "fresh:" + reason + ":" : "") + hash;
    }

    internal static string SharedFingerprint(string root, Dictionary<string, string>? snapshot = null)
    {
        var inventory = ProcessRunner.RunAsync(new("git", ["ls-files", "--cached", "--others", "--exclude-standard", "-z"], root)).GetAwaiter().GetResult();
        if (!inventory.Succeeded) throw new InvalidOperationException("checkpoint_inventory_failed");
        List<string> inputs = ["CLROS_HOST_RESUME_1", root, Environment.MachineName,
            System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            System.Runtime.InteropServices.RuntimeInformation.OSDescription];
        foreach (string relative in inventory.StandardOutput.Split('\0', StringSplitOptions.RemoveEmptyEntries).Distinct().Order(StringComparer.Ordinal))
        {
            if (relative.StartsWith("artifacts/", StringComparison.Ordinal)) continue;
            if (relative.StartsWith("tests/OS.HostTests/", StringComparison.Ordinal) && relative.LastIndexOf('/') == "tests/OS.HostTests/".Length - 1 &&
                relative.EndsWith(".cs", StringComparison.Ordinal) && !SharedSources.Contains(Path.GetFileNameWithoutExtension(relative)))
            {
                string path = Path.Combine(root, relative);
                // Global imports/assembly attributes can affect unrelated fixtures.
                if (File.Exists(path) && !AffectsAllFixtures(File.ReadAllText(path),
                    CompilerOptions().Split('|')[4].Split([';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))) continue;
            }
            AddFile(Path.Combine(root, relative));
        }
        // The changing host DLL is recorded as provenance, not a shared cache key.
        // Its fixture sources/dependencies and compiler options are keyed separately.
        foreach (string path in Directory.GetFiles(AppContext.BaseDirectory).Order(StringComparer.Ordinal))
            if (Path.GetFileName(path) != "OS.HostTests.dll" && Path.GetFileName(path) != "OS.HostTests.pdb" &&
                Path.GetExtension(path) is ".dll" or ".json") AddFile(path);
        foreach (string tool in new[] { "OS.Tooling", "OS.ImageBuilder", "OS.LibvirtRunner", "OS.ManifestTool", "OS.QemuRunner" })
        {
            string directory = Path.GetDirectoryName(HostToolProcess.GetBuiltToolPath(root, tool))!;
            if (!Directory.Exists(directory)) { inputs.Add(directory + ":MISSING"); continue; }
            foreach (string path in Directory.GetFiles(directory).Where(path => Path.GetExtension(path) is ".dll" or ".json").Order(StringComparer.Ordinal)) AddFile(path);
        }
        inputs.Add(CompilerOptions());
        AddFile(Environment.ProcessPath!);
        AddFile(typeof(object).Assembly.Location);
        var sdk = ProcessRunner.RunAsync(new("dotnet", ["--info"], root)).GetAwaiter().GetResult();
        if (!sdk.Succeeded) throw new InvalidOperationException("checkpoint_sdk_identity_failed");
        inputs.Add(sdk.StandardOutput);
        // Values (including credentials) are never serialized: only the combined digest.
        inputs.Add(EnvironmentFingerprint());
        string? llvm = Environment.GetEnvironmentVariable("CLROS_LLVM_OBJDUMP") ??
            (OperatingSystem.IsWindows() ? Environment.GetEnvironmentVariable("CLROS_LLVM_OBJDUMP", EnvironmentVariableTarget.User) : null);
        if (!string.IsNullOrEmpty(llvm)) AddFile(llvm);
        return HostRunCheckpoint.HashText(string.Join("\n", inputs));

        void AddFile(string path)
        {
            path = Path.GetFullPath(path);
            string hash = File.Exists(path) ? HostRunCheckpoint.HashFile(path) : "MISSING";
            inputs.Add(path + ":" + hash);
            if (snapshot is not null) snapshot[path] = hash;
        }
    }
}
