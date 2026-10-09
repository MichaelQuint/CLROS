using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

// A local checkpoint is evidence reuse, not a second execution of a passing test.
internal sealed class HostRunCheckpoint : IDisposable
{
    internal sealed class Record
    {
        public string State { get; set; } = "running";
        public string Fingerprint { get; set; } = "";
        public string Origin { get; set; } = "";
        public string Log { get; set; } = "";
        public string LogHash { get; set; } = "";
        public int ProcessId { get; set; }
        public long ProcessStartedUtcTicks { get; set; }
        public Dictionary<string, string> Artifacts { get; set; } = new(StringComparer.Ordinal);
        public Dictionary<string, string> ArtifactSources { get; set; } = new(StringComparer.Ordinal);
    }

    internal sealed class Receipt
    {
        public int Schema { get; set; } = 1;
        public string Root { get; set; } = "";
        public string Run { get; set; } = "";
        public bool Complete { get; set; }
        public bool FullSuite { get; set; }
        public int Failures { get; set; }
        public int Executed { get; set; }
        public int Reused { get; set; }
        public string[] Selected { get; set; } = [];
        public Dictionary<string, Record> Fixtures { get; set; } = new(StringComparer.Ordinal);
    }

    private readonly string root, directory, path, run = Guid.NewGuid().ToString("N");
    private readonly FileStream runLock;
    private readonly Receipt receipt;
    private readonly Dictionary<string, string> testSources;
    private readonly Dictionary<string, HashSet<string>> typeSources = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<string>> sourceTokens = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> sourceHashes = new(StringComparer.Ordinal);
    private readonly string shared;
    private Dictionary<string, (long Length, long Modified)> before = [];
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private static readonly HashSet<string> SharedSources = new(StringComparer.Ordinal)
        { "Program", "HostTestRunner", "HostRunCheckpoint", "HostFixtureAttribute", "HostToolProcess" };
    // Reviewed: both native profiles are always published before their outputs are read.
    // No prior artifact inputs. Keep this small; unclassified artifact consumers run fresh.
    private static readonly HashSet<string> OwnArtifactFixtures = new(StringComparer.Ordinal)
    {
        "M02IdtrContinuedExecutionEfiIntegrationTests",
        "M02OwnedIdtGatePopulationEfiIntegrationTests",
        "M02Ud2VectorStateEfiIntegrationTests"
    };

    internal HostRunCheckpoint(string root, string directory, bool resume)
    {
        this.root = Path.GetFullPath(root);
        this.directory = Path.GetFullPath(directory);
        path = Path.Combine(directory, "result.json");
        if (resume && !File.Exists(path)) throw new InvalidOperationException("Resume receipt missing: " + path);
        if (!resume && Directory.Exists(directory) && Directory.EnumerateFileSystemEntries(directory).Any())
            throw new InvalidOperationException("Results directory must be new or empty; use --resume for an existing run.");
        Directory.CreateDirectory(directory);
        runLock = new FileStream(Path.Combine(directory, "run.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        try
        {
            receipt = resume ? JsonSerializer.Deserialize<Receipt>(File.ReadAllText(path), Json)
                ?? throw new InvalidOperationException("Empty resume receipt.") : new Receipt { Root = this.root };
            if (receipt.Schema != 1 || receipt.Root != this.root || receipt.Fixtures == null)
                throw new InvalidOperationException("Incompatible resume receipt or workspace.");
            foreach (Record record in receipt.Fixtures.Values.Where(r => r.State == "running" && r.ProcessId != 0))
            {
                Process? child = null;
                try { child = Process.GetProcessById(record.ProcessId); }
                catch (ArgumentException) { }
                using (child)
                    if (child != null && !child.HasExited && child.StartTime.ToUniversalTime().Ticks == record.ProcessStartedUtcTicks)
                        throw new InvalidOperationException("Previous fixture process is still running: " + record.ProcessId);
            }
            receipt.Complete = false;
            receipt.Run = run;
            testSources = Directory.GetFiles(Path.Combine(root, "tests", "OS.HostTests"), "*.cs")
                .ToDictionary(p => Path.GetFileNameWithoutExtension(p), File.ReadAllText, StringComparer.Ordinal);
            foreach (var source in testSources)
            {
                // Use the SDK parser so generated C# strings and private nested helper names
                // cannot connect otherwise unrelated fixtures. References to nested types
                // also name their containing top-level type (including aliases/base types).
                var syntax = CSharpSyntaxTree.ParseText(source.Value).GetRoot();
                foreach (var declaration in syntax.DescendantNodes().OfType<BaseTypeDeclarationSyntax>()
                    .Where(d => !d.Ancestors().OfType<BaseTypeDeclarationSyntax>().Any()))
                {
                    string name = declaration.Identifier.ValueText;
                    if (!typeSources.TryGetValue(name, out var paths)) typeSources[name] = paths = new(StringComparer.Ordinal);
                    paths.Add(source.Key);
                }
                foreach (var declaration in syntax.DescendantNodes().OfType<DelegateDeclarationSyntax>()
                    .Where(d => !d.Ancestors().OfType<BaseTypeDeclarationSyntax>().Any()))
                {
                    string name = declaration.Identifier.ValueText;
                    if (!typeSources.TryGetValue(name, out var paths)) typeSources[name] = paths = new(StringComparer.Ordinal);
                    paths.Add(source.Key);
                }
                sourceTokens[source.Key] = Regex.Matches(source.Value, @"[A-Za-z_][A-Za-z_0-9]*")
                    .Select(m => m.Value).ToHashSet(StringComparer.Ordinal);
                sourceHashes[source.Key] = HashText(source.Value);
            }
            shared = SharedFingerprint();
            Save();
        }
        catch { runLock.Dispose(); throw; }
    }

    internal string Fingerprint(string type, string[]? artifactInputs = null)
    {
        if (artifactInputs == null && OwnArtifactFixtures.Contains(type)) artifactInputs = [];
        HashSet<string> dependencies = new(StringComparer.Ordinal);
        HashSet<string> visited = new(StringComparer.Ordinal);
        bool externalInput = false;
        Queue<string> pending = new();
        pending.Enqueue(testSources.ContainsKey(type) ? type : "Program");
        while (pending.TryDequeue(out string? name))
        {
            if (!visited.Add(name)) continue;
            dependencies.Add(name);
            string source = testSources[name];
            if (artifactInputs == null && Regex.IsMatch(source, @"(?i)(?:""artifacts""|artifacts[/\\])")) externalInput = true;
            // Unknown environment-selected inputs and reference checkouts are not safe cache inputs.
            if (source.Contains("\"references\"", StringComparison.Ordinal)) externalInput = true;
            foreach (Match read in Regex.Matches(source, @"Environment\.GetEnvironmentVariable\s*\(\s*(?<argument>[^)]*)\)"))
                if (read.Groups["argument"].Value.Trim() is not ("\"PATH\"" or "\"PSModulePath\"" or "\"NUGET_PACKAGES\"" or "\"CLROS_LLVM_OBJDUMP\"" or "\"CLROS_TEST_ROOT\""))
                    externalInput = true;
            // Over-include lexical references (including strings used to read/copy test source).
            // Broad/dynamic source discovery gets the entire test-source set, never a guessed subset.
            bool broad = source.Contains("EnumerateFiles", StringComparison.Ordinal) || source.Contains("GetFiles", StringComparison.Ordinal)
                || source.Contains("Assembly.Load", StringComparison.Ordinal);
            HashSet<string> tokens = sourceTokens[name];
            if (broad) dependencies.UnionWith(testSources.Keys);
            foreach (string token in tokens)
                if (typeSources.TryGetValue(token, out var sources))
                    foreach (string sourceFile in sources)
                        if (!SharedSources.Contains(sourceFile) && !visited.Contains(sourceFile)) pending.Enqueue(sourceFile);
            foreach (string other in testSources.Keys)
                if (!SharedSources.Contains(other) && tokens.Contains(other) && !visited.Contains(other)) pending.Enqueue(other);
        }
        string inputs = shared + "\n" + string.Join("\n", dependencies.Order(StringComparer.Ordinal)
            .Select(name => name + ":" + sourceHashes[name]));
        foreach (string input in artifactInputs ?? [])
        {
            string path = Path.GetFullPath(Path.Combine(root, input));
            if (!path.StartsWith(Path.Combine(root, "artifacts") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Declared artifact input must be under artifacts: " + input);
            bool exists = File.Exists(path);
            inputs += "\n" + path + ":" + (exists ? HashFile(path) : "MISSING");
            if (!exists) externalInput = true;
        }
        string? externalRoot = Environment.GetEnvironmentVariable("CLROS_TEST_ROOT");
        if (!string.IsNullOrEmpty(externalRoot) && Path.GetFullPath(externalRoot) != root) externalInput = true;
        return (externalInput ? "fresh:" : "") + HashText(inputs);
    }

    internal bool TryReuse(string id, string fingerprint, out string origin)
    {
        origin = "not_run";
        if (fingerprint.StartsWith("fresh:", StringComparison.Ordinal)) { origin = "external_input_not_fingerprinted"; return false; }
        if (!receipt.Fixtures.TryGetValue(id, out Record? record)) return false;
        origin = "previous_" + record.State;
        if (record.State != "passed") return false;
        origin = "inputs_changed";
        if (record.Fingerprint != fingerprint) return false;
        origin = "evidence_missing_or_changed";
        if (string.IsNullOrEmpty(record.Origin) || !Matches(record.Log, record.LogHash)
            || record.Artifacts == null || record.Artifacts.Any(item => !Matches(item.Key, item.Value))) return false;
        origin = record.Origin;
        return true;
    }

    internal string Start(string id, string fingerprint)
    {
        before = SnapshotArtifacts();
        string log = Path.Combine(directory, id + "." + run + ".log");
        receipt.Fixtures[id] = new Record { Fingerprint = fingerprint, Origin = run, Log = log };
        Save(); // A killed process leaves 'running', which is never reusable.
        return log;
    }

    internal void Finish(string id, bool passed)
    {
        Record record = receipt.Fixtures[id];
        record.State = passed ? "passed" : "failed";
        record.LogHash = HashFile(record.Log);
        foreach (var item in SnapshotArtifacts())
            if ((!before.TryGetValue(item.Key, out var old) || old != item.Value)
                // Existing live console logs belong to the caller. Each fixture's own log is hashed above.
                && !(before.ContainsKey(item.Key) && item.Key.EndsWith(".log", StringComparison.OrdinalIgnoreCase)))
            {
                string hash = HashFile(item.Key);
                string retained = Path.Combine(directory, "evidence", hash);
                Directory.CreateDirectory(Path.GetDirectoryName(retained)!);
                if (!File.Exists(retained)) File.Copy(item.Key, retained);
                if (HashFile(retained) != hash) throw new IOException("Artifact changed while retaining evidence: " + item.Key);
                record.Artifacts[retained] = hash;
                record.ArtifactSources[item.Key] = retained;
            }
        Save();
    }

    internal void ChildStarted(string id, Process process)
    {
        receipt.Fixtures[id].ProcessId = process.Id;
        receipt.Fixtures[id].ProcessStartedUtcTicks = process.StartTime.ToUniversalTime().Ticks;
        Save();
    }

    internal void Complete(string[] selected, bool fullSuite, int failures, int executed, int reused)
    {
        foreach (string id in selected)
        {
            Record record = receipt.Fixtures[id];
            if (record.State == "passed" && (!Matches(record.Log, record.LogHash) || record.Artifacts.Any(item => !Matches(item.Key, item.Value))))
                throw new InvalidOperationException("Retained evidence changed during this run: " + id);
        }
        if (SharedFingerprint() != shared) throw new InvalidOperationException("Shared inputs changed during this run; results are not a stable batch.");
        foreach (var source in testSources)
            if (File.ReadAllText(Path.Combine(root, "tests", "OS.HostTests", source.Key + ".cs")) != source.Value)
                throw new InvalidOperationException("Test source changed during this run: " + source.Key);
        if (Directory.GetFiles(Path.Combine(root, "tests", "OS.HostTests"), "*.cs").Length != testSources.Count)
            throw new InvalidOperationException("Test source inventory changed during this run.");
        receipt.Selected = selected;
        receipt.FullSuite = fullSuite;
        receipt.Failures = failures;
        receipt.Executed = executed;
        receipt.Reused = reused;
        receipt.Complete = true;
        Save();
    }

    private string SharedFingerprint()
    {
        using Process git = new() { StartInfo = new ProcessStartInfo("git")
            { WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false } };
        foreach (string arg in new[] { "ls-files", "--cached", "--others", "--exclude-standard", "-z" }) git.StartInfo.ArgumentList.Add(arg);
        git.Start();
        string files = git.StandardOutput.ReadToEnd();
        string errors = git.StandardError.ReadToEnd();
        git.WaitForExit();
        if (git.ExitCode != 0) throw new InvalidOperationException("Cannot enumerate repository inputs: " + errors);
        List<string> inputs = [];
        foreach (string file in files.Split('\0', StringSplitOptions.RemoveEmptyEntries).Distinct().Order(StringComparer.Ordinal))
        {
            if (file.StartsWith("artifacts/", StringComparison.Ordinal)) continue;
            if (file.StartsWith("tests/OS.HostTests/", StringComparison.Ordinal) && file.LastIndexOf('/') == "tests/OS.HostTests/".Length - 1 && file.EndsWith(".cs", StringComparison.Ordinal)
                && !SharedSources.Contains(Path.GetFileNameWithoutExtension(file))) continue;
            string absolute = Path.Combine(root, file);
            if (File.Exists(absolute)) inputs.Add(file + ":" + HashFile(absolute));
            else inputs.Add(file + ":MISSING");
        }
        // Rebuilding the host assembly after a fixture edit must not invalidate unrelated passes.
        // Actual referenced/producer binaries, runtime and environment remain shared inputs.
        foreach (string tool in new[] { "OS.ImageBuilder", "OS.LibvirtRunner", "OS.ManifestTool", "OS.QemuRunner" })
        {
            string file = HostToolProcess.GetBuiltToolPath(root, tool);
            inputs.Add(file + ":" + (File.Exists(file) ? HashFile(file) : "MISSING"));
        }
        foreach (string file in Directory.GetFiles(AppContext.BaseDirectory, "*.dll").Order(StringComparer.Ordinal))
            if (Path.GetFileName(file) != "OS.HostTests.dll") inputs.Add(file + ":" + HashFile(file));
        inputs.Add(System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription);
        inputs.Add(System.Runtime.InteropServices.RuntimeInformation.OSDescription);
        inputs.Add(Environment.MachineName);
        string options = Path.Combine(AppContext.BaseDirectory, "OS.HostTests.options.txt");
        inputs.Add(File.Exists(options) ? HashFile(options) : "NO_HOST_COMPILER_OPTIONS");
        using (Process sdk = new() { StartInfo = new ProcessStartInfo("dotnet", "--info")
            { WorkingDirectory = root, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true } })
        {
            sdk.Start();
            inputs.Add(sdk.StandardOutput.ReadToEnd());
            sdk.WaitForExit();
            if (sdk.ExitCode != 0) throw new InvalidOperationException("Cannot fingerprint the .NET toolchain.");
        }
        // Store only the digest, never environment values or credentials in the receipt/log.
        foreach (string key in Environment.GetEnvironmentVariables().Keys.Cast<string>().Order(StringComparer.Ordinal))
            inputs.Add(key + "=" + Environment.GetEnvironmentVariable(key));
        string? llvm = Environment.GetEnvironmentVariable("CLROS_LLVM_OBJDUMP");
        if (!string.IsNullOrEmpty(llvm)) inputs.Add("llvm:" + (File.Exists(llvm) ? HashFile(llvm) : "MISSING"));
        // Native compiler/linker discovery can change without PATH changing.
        string visualStudio = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft Visual Studio");
        if (Directory.Exists(visualStudio))
            foreach (string name in new[] { "link.exe", "cl.exe", "llvm-objdump.exe" })
                foreach (string file in Directory.EnumerateFiles(visualStudio, name, SearchOption.AllDirectories).Order(StringComparer.Ordinal))
                    inputs.Add(file + ":" + HashFile(file));
        string packages = Environment.GetEnvironmentVariable("NUGET_PACKAGES")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");
        foreach (string name in new[] { "runtime.win-x64.microsoft.dotnet.ilcompiler", "microsoft.dotnet.ilcompiler", "microsoft.net.illink.tasks", "microsoft.netcore.app.runtime.nativeaot.win-x64" })
        {
            string package = Path.Combine(packages, name);
            if (!Directory.Exists(package)) { inputs.Add(package + ":MISSING"); continue; }
            foreach (string file in Directory.EnumerateFiles(package, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
                inputs.Add(file + ":" + HashFile(file));
        }
        return HashText(string.Join("\n", inputs));
    }

    private Dictionary<string, (long, long)> SnapshotArtifacts()
    {
        string artifacts = Path.Combine(root, "artifacts");
        Dictionary<string, (long, long)> snapshot = new(StringComparer.Ordinal);
        if (!Directory.Exists(artifacts)) return snapshot;
        foreach (string file in Directory.EnumerateFiles(artifacts, "*", SearchOption.AllDirectories))
        {
            if (file.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) continue;
            FileInfo info = new(file);
            snapshot[file] = (info.Length, info.LastWriteTimeUtc.Ticks);
        }
        return snapshot;
    }

    private void Save()
    {
        string temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(receipt, Json), new UTF8Encoding(false));
        File.Move(temporary, path, true);
    }

    internal static string HashFile(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
    internal static string HashText(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    private static bool Matches(string path, string hash) => !string.IsNullOrEmpty(path) && File.Exists(path) && HashFile(path) == hash;
    public void Dispose() => runLock.Dispose();
}
