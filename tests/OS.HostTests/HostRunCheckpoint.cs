using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OS.Tooling;

// Checkpoints retain historical evidence. They never replay TEST_PASS output.
internal sealed class HostRunCheckpoint : IDisposable
{
    internal sealed record Selection(string Lane, string? Filter, string? CaseFilter);
    internal sealed record Entry(string Fingerprint, string Origin, string LogHash,
        HostTestRunner.FixtureResult Result, Dictionary<string, string> Evidence, string Producer, string ProducerHash)
    {
        public Dictionary<string, string> ArtifactSources { get; init; } = new(StringComparer.Ordinal);
    }
    internal sealed class Receipt
    {
        public int Schema { get; set; } = 1;
        public string Root { get; set; } = "";
        public string Machine { get; set; } = "";
        public Selection Selection { get; set; } = new("fast", null, null);
        public Dictionary<string, Entry> Entries { get; set; } = new(StringComparer.Ordinal);
    }

    private readonly string path, directory;
    private readonly FileStream lease;
    private readonly Receipt receipt;
    private readonly string producer, producerHash;
    private readonly Dictionary<string, bool> checkedProducers = new(StringComparer.Ordinal);
    private readonly object sync = new();
    private bool runInvalidated;
    internal string Run { get; } = Guid.NewGuid().ToString("N");

    internal HostRunCheckpoint(string root, string directory, bool resume, Selection? selection = null)
    {
        this.directory = Path.GetFullPath(directory);
        path = Path.Combine(this.directory, "checkpoint.json");
        if (resume && !File.Exists(path)) throw new InvalidOperationException("resume_checkpoint_missing " + path);
        if (!resume && Directory.Exists(directory) && Directory.EnumerateFileSystemEntries(directory).Any())
            throw new InvalidOperationException("results_directory_not_empty use_--resume");
        Directory.CreateDirectory(directory);
        lease = new(Path.Combine(directory, "run.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        try
        {
            receipt = resume ? JsonSerializer.Deserialize<Receipt>(File.ReadAllText(path))
                ?? throw new InvalidOperationException("resume_checkpoint_empty")
                : new() { Root = Path.GetFullPath(root), Machine = Environment.MachineName };
            if (receipt.Schema != 1 || receipt.Root != Path.GetFullPath(root) || receipt.Machine != Environment.MachineName || receipt.Entries is null)
                throw new InvalidOperationException("resume_checkpoint_incompatible");
            receipt.Selection = selection ?? receipt.Selection;
            // A worker holds a read lease through its entire fixture. This also
            // refuses reuse if a parent was interrupted without stopping it.
            foreach (string file in Directory.EnumerateFiles(directory, "*.worker.lock"))
                using (File.Open(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
            string assembly = typeof(HostRunCheckpoint).Assembly.Location;
            producerHash = HashFile(assembly);
            producer = Path.Combine(directory, "producers", producerHash + ".dll");
            Directory.CreateDirectory(Path.GetDirectoryName(producer)!);
            if (!File.Exists(producer)) File.Copy(assembly, producer);
            if (!Matches(producer, producerHash)) throw new IOException("checkpoint_producer_changed");
            Save();
        }
        catch { lease.Dispose(); throw; }
    }

    internal static Selection ReadSelection(string directory) =>
        JsonSerializer.Deserialize<Receipt>(File.ReadAllText(Path.Combine(directory, "checkpoint.json")))?.Selection
        ?? throw new InvalidOperationException("resume_selection_missing");

    internal void InvalidateRun()
    {
        lock (sync)
        {
            runInvalidated = true;
            foreach (string key in receipt.Entries.Where(item => item.Value.Origin == Run).Select(item => item.Key).ToArray()) receipt.Entries.Remove(key);
            Save();
        }
    }

    internal bool TryReuse(string id, string fingerprint, out Entry? entry)
    {
        lock (sync)
        {
            entry = null;
            if (fingerprint.StartsWith("fresh:", StringComparison.Ordinal) ||
                !receipt.Entries.TryGetValue(id, out Entry? previous) || previous is null ||
                previous.Fingerprint != fingerprint || string.IsNullOrWhiteSpace(previous.Origin) ||
                previous.Result is null || previous.Result.Id != id || !Passed(previous.Result) || !EvidenceMatches(previous)) return false;
            entry = previous;
            return true;
        }
    }

    internal string Start(string id)
    {
        lock (sync)
        {
            // Removing the old pass before launch makes crashes and interrupted
            // workers unconditionally runnable, even before results.json exists.
            receipt.Entries.Remove(id);
            Save();
            return Path.Combine(directory, id + "." + Run + ".log");
        }
    }

    internal void Finish(string id, string fingerprint, HostTestRunner.FixtureResult result, IEnumerable<string>? outputs = null)
    {
        lock (sync) if (runInvalidated) return;
        Dictionary<string, string> evidence = new(StringComparer.Ordinal);
        Dictionary<string, string> artifacts = new(StringComparer.Ordinal);
        foreach (string output in outputs ?? [])
        {
            string hash = HashFile(output);
            string retained = Path.Combine(directory, "evidence", hash);
            Directory.CreateDirectory(Path.GetDirectoryName(retained)!);
            if (!File.Exists(retained)) File.Copy(output, retained);
            if (HashFile(retained) != hash) throw new IOException("checkpoint_evidence_changed " + output);
            evidence[retained] = hash;
            artifacts[output] = retained;
        }
        lock (sync)
        {
            // Invalidation can race an already-finishing worker's evidence copy.
            // Never recreate a reusable pass after this run has lost its inputs.
            if (runInvalidated) return;
            receipt.Entries[id] = new(fingerprint, Run, HashFile(result.Log), result, evidence, producer, producerHash) { ArtifactSources = artifacts };
            Save();
        }
    }

    internal bool EvidenceMatches(Entry entry)
    {
        if (string.IsNullOrEmpty(entry.Producer) || string.IsNullOrEmpty(entry.ProducerHash)) return false;
        string producerKey = entry.Producer + "|" + entry.ProducerHash;
        if (!checkedProducers.TryGetValue(producerKey, out bool valid))
            checkedProducers[producerKey] = valid = IsRetained(entry.Producer) && Matches(entry.Producer, entry.ProducerHash);
        return valid && IsRetained(entry.Result.Log) && Matches(entry.Result.Log, entry.LogHash) && entry.Evidence is not null &&
            entry.ArtifactSources is not null && entry.ArtifactSources.Values.All(entry.Evidence.ContainsKey) &&
            entry.Evidence.All(item => IsRetained(item.Key) && Matches(item.Key, item.Value));
    }

    internal int ValidateResults(IEnumerable<HostTestRunner.FixtureResult> results)
    {
        lock (sync)
        {
            checkedProducers.Clear();
            return results.Where(Passed).Count(result => !receipt.Entries.TryGetValue(result.Id, out var entry) || !EvidenceMatches(entry));
        }
    }

    private bool IsRetained(string file) => !string.IsNullOrEmpty(file) &&
        Path.GetFullPath(file).StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    internal static bool Passed(HostTestRunner.FixtureResult result) => result.ExitCode == 0 && !result.TimedOut &&
        result.Failed == 0 && result.Started > 0 && result.Started == result.Passed && result.Error is null;

    private void Save() => AtomicReceipt.WriteJson(path, receipt);
    internal static string HashFile(string path) { using var file = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(file)); }
    internal static string HashText(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    private static bool Matches(string path, string hash) => !string.IsNullOrEmpty(hash) && File.Exists(path) && HashFile(path) == hash;
    public void Dispose() => lease.Dispose();
}
