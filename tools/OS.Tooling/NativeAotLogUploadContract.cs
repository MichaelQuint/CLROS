using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace OS.Tooling;

/// <summary>Finite ordinary FileStream requirement candidate and owned host
/// references. Copied values and Flush(true) policy do not grant HTTP, physical
/// flush, persistence, SQLite or CLROS execution authority.</summary>
public static class NativeAotLogUploadContract
{
    public const string Application = "NativeAotLogUploadContract", LogName = "LOG.TXT", UploadName = "UPLOAD.BIN";
    public const int MaximumTranscriptBytes = 8192, UploadBytes = 5126;
    private static readonly NativeAotSocketFixturePublisher Publisher = new(Application, "log-upload", [
        ("NativeAotLogUploadContract.csproj", "5CB57DC7E6323F04E7CAE3DADF64FB827BE3CCEE8EB81C97A450C6678CBD5F17"),
        ("Program.cs", "9B11DFDEED8B8251C0935D31A34514BFA1B03B22626BA2203ED4724D9A852149")]);

    public readonly record struct FileValues(int LogBytes, string LogSha256, int UploadBytes, string UploadSha256)
    { public bool GrantsPersistenceOrGuestAuthority => false; }
    public sealed record Transcript(string Phase, FileValues Files)
    { public bool GrantsOriginalProcessOrFlushAuthority => false; }
    public sealed class ManagedReference
    {
        internal ManagedReference(string directory, Transcript write, Transcript verify, IEnumerable<string> artifacts)
        { Directory = directory; Write = write; Verify = verify; Artifacts = Array.AsReadOnly(artifacts.ToArray()); }
        public string Directory { get; }
        public Transcript Write { get; }
        public Transcript Verify { get; }
        public ReadOnlyCollection<string> Artifacts { get; }
        public bool GrantsLinuxNativeAotGuestOrPersistenceAuthority => false;
    }

    public enum TraceKind { CompletedCall, UnfinishedCall, ResumedCall, UnreturnedCall, ProcessExit, Signal, Unclassified }
    public sealed record TraceRow(string Source, int Line, string Raw, TraceKind Kind, string Syscall, string Arguments, string Result);
    /// <summary>Copied raw trace rows, including unfinished/resumed, unreturned
    /// exit_group and unknown records. No inferred returns or descriptor/path
    /// causality. Complete projection is separate from successful process exit.</summary>
    public sealed class TraceValues
    {
        internal TraceValues(string phase, IEnumerable<TraceRow> rows, int exitedFiles, int files)
        { Phase = phase; Rows = Array.AsReadOnly(rows.ToArray()); ExitedFiles = exitedFiles; Files = files; }
        public string Phase { get; }
        public ReadOnlyCollection<TraceRow> Rows { get; }
        public int Files { get; }
        public int ExitedFiles { get; }
        public int UnjoinedRows => Rows.Count(row => row.Kind is TraceKind.UnfinishedCall or TraceKind.ResumedCall);
        public int UnreturnedRows => Rows.Count(row => row.Kind == TraceKind.UnreturnedCall);
        public int UnclassifiedRows => Rows.Count(row => row.Kind == TraceKind.Unclassified);
        public bool CompleteTraceProjection => ExitedFiles == Files && UnjoinedRows == 0 && UnreturnedRows == 0 && UnclassifiedRows == 0;
        public bool GrantsSyscallClosureLockFlushOrPersistenceAuthority => false;
    }
    public sealed record ArtifactIdentity(string Path, long Bytes, string Sha256);
    /// <summary>Original joined Linux reference result. Owns retained input and
    /// evidence handles until disposed. Root must serialize its builder lease;
    /// this does not admit an instruction index, package or CLROS workload.</summary>
    public sealed class LinuxReference : IDisposable
    {
        private AuthorityFiles? held;
        internal LinuxReference(string directory, NativeAotPayload.Inspection image, Transcript write, Transcript verify,
            TraceValues writeTrace, TraceValues verifyTrace, AuthorityFiles held, IEnumerable<AuthorityFile> artifacts)
        {
            Directory = directory; Image = image; Write = write; Verify = verify; WriteTrace = writeTrace; VerifyTrace = verifyTrace; this.held = held;
            Artifacts = Array.AsReadOnly(artifacts.Select(file => new ArtifactIdentity(file.Path, file.Length, file.Sha256)).ToArray());
            ArtifactPaths = Array.AsReadOnly(Artifacts.Select(file => file.Path).ToArray());
        }
        public string Directory { get; }
        public NativeAotPayload.Inspection Image { get; }
        public Transcript Write { get; }
        public Transcript Verify { get; }
        public TraceValues WriteTrace { get; }
        public TraceValues VerifyTrace { get; }
        public ReadOnlyCollection<ArtifactIdentity> Artifacts { get; }
        public ReadOnlyCollection<string> ArtifactPaths { get; }
        public bool CompleteTraceProjection => WriteTrace.CompleteTraceProjection && VerifyTrace.CompleteTraceProjection;
        public bool GrantsGuestHttpLockFlushOrPersistenceAuthority => false;
        public void VerifyUnchanged() { ObjectDisposedException.ThrowIf(held is null, this); held.VerifyUnchanged(); }
        public void Dispose() { held?.Dispose(); held = null; }
    }

    /// <summary>Pure direct strace plan for a finite native write or independent
    /// verifier. Paths are literal canonical Linux paths even on a Windows host;
    /// no shell, trace filter, syscall substitution or file creation occurs.</summary>
    public static string[] LinuxTraceArguments(string executable, string dataDirectory, string tracePrefix, string phase)
    {
        Need(phase is "write" or "verify", "phase");
        foreach (string path in new[] { executable, dataDirectory, tracePrefix })
            Need(path.Length is > 1 and <= 4096 && path.StartsWith('/') && !path.Any(char.IsControl) &&
                !path.Contains('\\') && path[1..].Split('/').All(part => part is not ("" or "." or "..")), "linux_path");
        Need(executable != dataDirectory && executable != tracePrefix && dataDirectory != tracePrefix, "linux_path");
        return ["-ff", "-yy", "-ttt", "-s", "256", "-o", tracePrefix, executable, "--" + phase, "--root", dataDirectory];
    }

    /// <summary>Copy bounded complete strace -ff/-yy/-ttt text without dropping
    /// signal-pipe unfinished calls or inventing exit_group returns. All source
    /// lines and errors stay visible. Path annotations remain lexical values;
    /// neither an errno nor BCL success establishes locking or physical flush.</summary>
    public static TraceValues InspectTraceValues(IReadOnlyDictionary<string, string> files, string phase)
    {
        Need(phase is "write" or "verify", "phase"); Need(files.Count is > 0 and <= 64, "trace_inventory");
        List<TraceRow> rows = []; int total = 0, exited = 0;
        foreach (var file in files.OrderBy(row => row.Key, StringComparer.Ordinal))
        {
            string prefix = phase + "-trace.";
            Need(file.Key.StartsWith(prefix, StringComparison.Ordinal) && file.Key[prefix.Length..].All(char.IsAsciiDigit) &&
                int.TryParse(file.Key.AsSpan(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out int pid) && pid > 0 &&
                pid.ToString(CultureInfo.InvariantCulture) == file.Key[prefix.Length..], "trace_identity");
            Need(file.Value.Length is > 0 and <= 4 * 1024 * 1024 && Encoding.UTF8.GetByteCount(file.Value) <= 4 * 1024 * 1024 &&
                (total += Encoding.UTF8.GetByteCount(file.Value)) <= 16 * 1024 * 1024, "trace_capacity");
            Need(file.Value.EndsWith('\n'), "trace_truncated");
            string[] lines = file.Value[..^1].Split('\n'); Need(lines.Length <= 200000, "trace_capacity");
            TraceRow? last = null;
            for (int i = 0; i < lines.Length; i++)
            {
                string raw = lines[i]; Need(raw.Length <= 8192 && !raw.Any(c => char.IsControl(c) && c != '\t'), "trace_line");
                Need(rows.Count < 200000, "trace_capacity");
                var timed = Regex.Match(raw, @"^\d+\.\d+ (.*)$", RegexOptions.CultureInvariant);
                string body = timed.Success ? timed.Groups[1].Value : raw, syscall = "", arguments = "", result = "";
                TraceKind kind = TraceKind.Unclassified;
                if (timed.Success)
                {
                    var call = Regex.Match(body, @"^([a-zA-Z0-9_]+)\((.*)\) += (.+)$", RegexOptions.CultureInvariant);
                    if (body.Contains("<unfinished ...>", StringComparison.Ordinal))
                    {
                        kind = TraceKind.UnfinishedCall;
                        var start = Regex.Match(body, @"^([a-zA-Z0-9_]+)\((.*)$", RegexOptions.CultureInvariant);
                        if (start.Success) { syscall = start.Groups[1].Value; arguments = start.Groups[2].Value; }
                    }
                    else if (body.StartsWith("<... ", StringComparison.Ordinal) && body.Contains(" resumed>", StringComparison.Ordinal))
                    { kind = TraceKind.ResumedCall; arguments = body; }
                    else if (call.Success)
                    {
                        syscall = call.Groups[1].Value; arguments = call.Groups[2].Value; result = call.Groups[3].Value;
                        kind = result.StartsWith('?') ? TraceKind.UnreturnedCall : TraceKind.CompletedCall;
                    }
                    else if (body.StartsWith("+++ ", StringComparison.Ordinal))
                    { kind = TraceKind.ProcessExit; result = body; }
                    else if (body.StartsWith("--- ", StringComparison.Ordinal))
                    { kind = TraceKind.Signal; arguments = body; }
                }
                last = new(file.Key, i + 1, raw, kind, syscall, arguments, result); rows.Add(last);
            }
            if (last is { Kind: TraceKind.ProcessExit, Result: "+++ exited with 0 +++" }) exited++;
        }
        return new(phase, rows, exited, files.Count);
    }

    /// <summary>Return fresh deterministic bytes. No stream, source or process
    /// is opened, and callers cannot mutate a retained expected-byte buffer.</summary>
    public static byte[] ExpectedLog() => Encoding.UTF8.GetBytes(string.Join("", Enumerable.Range(1, 12)
        .Select(row => "CLROS_LOG v1 sequence=" + row.ToString("D2", CultureInfo.InvariantCulture) + " message=CLROS ready — café\n")));
    public static byte[] ExpectedUpload()
    {
        byte[] bytes = new byte[UploadBytes];
        for (int i = 0; i < bytes.Length; i++) bytes[i] = unchecked((byte)((i * 73 + 19) ^ (i >> 3) ^ (i >> 9)));
        return bytes;
    }

    /// <summary>Inspect synchronous copied complete file bytes and EOF shape,
    /// not original file identity, storage I/O, process or durability evidence.</summary>
    public static FileValues InspectFileValues(ReadOnlySpan<byte> log, ReadOnlySpan<byte> upload)
    {
        Need(log.SequenceEqual(ExpectedLog()), "log_bytes"); Need(upload.SequenceEqual(ExpectedUpload()), "upload_bytes");
        return new(log.Length, Hash(log), upload.Length, Hash(upload));
    }

    /// <summary>Parse only bounded complete copied stdout. Require exact phase,
    /// requested policy and independently expected byte identities. Normal CRLF
    /// is accepted; extra/partial lines, controls or pass-only summaries reject.</summary>
    public static Transcript InspectTranscriptValues(string text, string phase)
    {
        Need(phase is "write" or "verify", "phase");
        Need(text.Length <= MaximumTranscriptBytes && Encoding.UTF8.GetByteCount(text) <= MaximumTranscriptBytes, "transcript");
        string normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal);
        var files = InspectFileValues(ExpectedLog(), ExpectedUpload());
        string expected = "TEST_START m10.027\nLOG_UPLOAD_PHASE " + phase + "\n" +
            "LOG_UPLOAD_POLICY append=managed-reopen io=synchronous buffering=0 flushRequested=" + (phase == "write" ? "true" : "false") + "\n" +
            "LOG_UPLOAD_FILES logBytes=" + files.LogBytes.ToString(CultureInfo.InvariantCulture) + " logSha256=" + files.LogSha256 +
            " uploadBytes=" + files.UploadBytes.ToString(CultureInfo.InvariantCulture) + " uploadSha256=" + files.UploadSha256 + "\nTEST_PASS m10.027\n";
        Need(normalized == expected, "transcript"); return new(phase, files);
    }

    /// <summary>Plan only the app/output substitution in the original pinned
    /// static/non-PIE musl recipe. This does not publish or admit an ELF/index.</summary>
    public static string[] PublishArguments(string originalCommands, string destination) => Publisher.PublishArguments(originalCommands, destination);
    public static void RequireCurrentSources(string root) => Publisher.RequireCurrentSources(root);

    /// <summary>Require a missing child under an existing plain artifacts
    /// parent. This read-only check never creates/replaces a directory.</summary>
    public static void RequireFreshOutput(string root, string destination)
    {
        root = Path.GetFullPath(root); destination = Path.GetFullPath(destination, root);
        string prefix = Path.Combine(root, "artifacts") + Path.DirectorySeparatorChar;
        Need(destination.StartsWith(prefix, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) &&
            !Path.Exists(destination), "fresh_output_required");
        AuthorityFiles.RequirePlainPath(root, Path.GetDirectoryName(destination)!, "log_upload_output_parent");
    }

    /// <summary>Own distinct normal managed build/write/verify children in
    /// Windows job containment. Require pre-existing empty files, independently
    /// inspect output bytes and retain the nonempty rejection without blocking
    /// writes artificially. All outputs/failures stay in a fresh artifact tree.
    /// No Linux/native/guest process is started; app bin/obj remain this isolated
    /// checkout's normal build outputs. Call under its serialized build ownership.</summary>
    public static async Task<ManagedReference> RunManagedReferenceAsync(string root, string destination, CancellationToken cancellation = default)
    {
        Need(OperatingSystem.IsWindows(), "managed_windows_required"); root = Path.GetFullPath(root); destination = Path.GetFullPath(destination, root);
        RequireFreshOutput(root, destination); RequireCurrentSources(root);
        using var lease = NativeAotPayload.OpenQualificationLease(root); using AuthorityFiles held = new();
        Need(ToolingBuildInputs.Validate(root).Count == 0, "tooling_inputs_changed");
        string app = Path.Combine(root, "apps", Application), project = Path.Combine(app, Application + ".csproj");
        var inputs = new[] { project, Path.Combine(app, "Program.cs"), Path.Combine(root, "global.json"),
            Path.Combine(root, "Directory.Build.props"), Path.Combine(root, "Directory.Build.targets"), typeof(NativeAotLogUploadContract).Assembly.Location }
            .Select(path => held.Open(root, path, "log upload source/producer", "log_upload_source", 64 * 1024 * 1024, singleLink: true)).ToList();
        Directory.CreateDirectory(destination); AuthorityFiles.RequirePlainPath(root, destination, "log_upload_output_path");
        using var ownership = new FileStream(Path.Combine(destination, "ownership.lock"), FileMode.CreateNew, FileAccess.Write, FileShare.None);
        bool succeeded = false; string failure = ""; var commands = new List<object>(); var artifacts = new List<string>();
        Transcript? write = null, verify = null; FileValues? fileValues = null;
        async Task<ProcessResult> Run(string name, string[] arguments, bool success = true)
        {
            commands.Add(new { executable = "dotnet", arguments, workingDirectory = root, phase = name });
            var result = await ProcessRunner.RunAsync(new("dotnet", arguments, root) { Timeout = TimeSpan.FromMinutes(5), OutputCharacterLimit = 4 * 1024 * 1024 }, cancellation);
            await Save(name + ".stdout.log", result.StandardOutput); await Save(name + ".stderr.log", result.StandardError);
            await Save(name + ".process.json", JsonSerializer.Serialize(result));
            QemuTranscripts.RequireCompleteProcess(result, "log_upload_" + name + "_incomplete");
            Need(success ? result.Succeeded : result.ExitCode == 1 && result.TerminationConfirmed, "process_" + name); return result;
        }
        async Task Save(string name, string contents)
        { string path = Path.Combine(destination, name); await File.WriteAllTextAsync(path, contents, CancellationToken.None); artifacts.Add(path); }
        FileValues ReadOutputs(string data)
        {
            using AuthorityFiles copied = new();
            var log = copied.Open(root, Path.Combine(data, LogName), "copied managed log", "log_upload_log", 4096, singleLink: true);
            var upload = copied.Open(root, Path.Combine(data, UploadName), "copied managed upload", "log_upload_upload", UploadBytes, singleLink: true);
            AuthorityFiles.RequireDistinct(log, upload);
            var values = InspectFileValues(log.ReadBytes(4096, "log_upload_log"), upload.ReadBytes(UploadBytes, "log_upload_upload")); copied.VerifyUnchanged(); return values;
        }
        try
        {
            var sdk = await Run("sdk", ["--version"]); Need(sdk.StandardOutput.Trim() == NativeAotPayload.SdkVersion, "sdk_pin");
            await Run("restore", ["restore", project, "--nologo"]);
            await Run("build", ["build", project, "--no-restore", "--no-incremental", "--nologo", "-m:1", "-p:UseSharedCompilation=false"]);
            string assetsPath = Path.Combine(app, "obj", "project.assets.json");
            var assets = held.Open(root, assetsPath, "log upload assets", "log_upload_assets", 32 * 1024 * 1024, singleLink: true); inputs.Add(assets);
            using (var parsed = JsonDocument.Parse(assets.ReadBytes(32 * 1024 * 1024, "log_upload_assets")))
            {
                string[] dependencies = parsed.RootElement.GetProperty("libraries").EnumerateObject().Select(row => row.Name).Order(StringComparer.Ordinal).ToArray();
                Need(dependencies.All(name => !name.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) && !name.Contains("EntityFramework", StringComparison.OrdinalIgnoreCase)), "foreign_dependency");
                await Save("dependencies.json", JsonSerializer.Serialize(dependencies));
            }
            string bin = Path.Combine(app, "bin", "Debug", "net11.0"); string[] built = Directory.GetFiles(bin, "*", SearchOption.AllDirectories);
            Need(built.Length is > 0 and <= 64, "built_count");
            inputs.AddRange(built.Select(path => held.Open(root, path, "log upload built output", "log_upload_built", 64 * 1024 * 1024, allowEmpty: true, singleLink: true)));
            using (var runtime = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(bin, Application + ".runtimeconfig.json"))))
            {
                var options = runtime.RootElement.GetProperty("runtimeOptions");
                Need(options.GetProperty("tfm").GetString() == NativeAotPayload.TargetFramework && options.GetProperty("rollForward").GetString() == "Disable" &&
                    options.GetProperty("framework").GetProperty("name").GetString() == "Microsoft.NETCore.App" &&
                    options.GetProperty("framework").GetProperty("version").GetString() == NativeAotPayload.RuntimeVersion, "runtime_pin");
            }
            string data = Path.Combine(destination, "data"); Directory.CreateDirectory(data);
            string log = Path.Combine(data, LogName), upload = Path.Combine(data, UploadName), executable = Path.Combine(bin, Application + ".dll");
            await File.WriteAllBytesAsync(log, [], cancellation); await File.WriteAllBytesAsync(upload, [], cancellation);
            var wrote = await Run("write", [executable, "--write", "--root", data]); Need(wrote.StandardError.Length == 0, "write_stderr");
            write = InspectTranscriptValues(wrote.StandardOutput, "write");
            fileValues = ReadOutputs(data); Need(fileValues.Value == write.Files, "write_files");
            // No held data handle manufactures a write failure for this control.
            var nonempty = await Run("nonempty", [executable, "--write", "--root", data], success: false);
            Need(nonempty.StandardError.Length == 0 && nonempty.StandardOutput.Replace("\r\n", "\n", StringComparison.Ordinal) ==
                "TEST_START m10.027\nLOG_UPLOAD_PHASE write\nTEST_FAIL m10.027 InvalidOperationException\n", "nonempty_transcript");
            Need(ReadOutputs(data) == fileValues.Value, "nonempty_modified");
            foreach (bool nonemptyLog in new[] { true, false })
            {
                string name = nonemptyLog ? "nonempty-log-only" : "nonempty-upload-only", negative = Path.Combine(destination, name); Directory.CreateDirectory(negative);
                byte[] first = nonemptyLog ? ExpectedLog() : [], second = nonemptyLog ? [] : ExpectedUpload();
                await File.WriteAllBytesAsync(Path.Combine(negative, LogName), first, cancellation);
                await File.WriteAllBytesAsync(Path.Combine(negative, UploadName), second, cancellation);
                var rejected = await Run(name, [executable, "--write", "--root", negative], success: false);
                Need(rejected.StandardError.Length == 0 && rejected.StandardOutput.Replace("\r\n", "\n", StringComparison.Ordinal) ==
                    "TEST_START m10.027\nLOG_UPLOAD_PHASE write\nTEST_FAIL m10.027 InvalidOperationException\n", "nonempty_transcript");
                using AuthorityFiles negativeFiles = new();
                var firstHeld = negativeFiles.Open(root, Path.Combine(negative, LogName), "nonempty control log", "log_upload_negative", 4096, allowEmpty: true, singleLink: true);
                var secondHeld = negativeFiles.Open(root, Path.Combine(negative, UploadName), "nonempty control upload", "log_upload_negative", UploadBytes, allowEmpty: true, singleLink: true);
                AuthorityFiles.RequireDistinct(firstHeld, secondHeld);
                Need(firstHeld.ReadBytes(4096, "log_upload_negative").AsSpan().SequenceEqual(first) &&
                    secondHeld.ReadBytes(UploadBytes, "log_upload_negative").AsSpan().SequenceEqual(second), "nonempty_modified"); negativeFiles.VerifyUnchanged();
                artifacts.Add(firstHeld.Path); artifacts.Add(secondHeld.Path);
            }
            var relative = await Run("relative-root", [executable, "--write", "--root", "."], success: false);
            Need(relative.StandardError.Length == 0 && relative.StandardOutput.Replace("\r\n", "\n", StringComparison.Ordinal) ==
                "TEST_START m10.027\nTEST_FAIL m10.027 InvalidOperationException\n", "relative_root_control");
            string missing = Path.Combine(destination, "missing-upload"); Directory.CreateDirectory(missing);
            await File.WriteAllBytesAsync(Path.Combine(missing, LogName), [], cancellation);
            var absent = await Run("missing-upload", [executable, "--write", "--root", missing], success: false);
            Need(absent.StandardError.Length == 0 && absent.StandardOutput.Replace("\r\n", "\n", StringComparison.Ordinal) ==
                "TEST_START m10.027\nLOG_UPLOAD_PHASE write\nTEST_FAIL m10.027 FileNotFoundException\n" &&
                new FileInfo(Path.Combine(missing, LogName)).Length == 0 && !Path.Exists(Path.Combine(missing, UploadName)), "missing_input_control");
            string corrupt = Path.Combine(destination, "corrupt-upload"); Directory.CreateDirectory(corrupt);
            byte[] corrupted = ExpectedUpload(); corrupted[4094] ^= 1;
            await File.WriteAllBytesAsync(Path.Combine(corrupt, LogName), ExpectedLog(), cancellation);
            await File.WriteAllBytesAsync(Path.Combine(corrupt, UploadName), corrupted, cancellation);
            var corruptResult = await Run("corrupt-upload", [executable, "--verify", "--root", corrupt], success: false);
            Need(corruptResult.StandardError.Length == 0 && corruptResult.StandardOutput.Replace("\r\n", "\n", StringComparison.Ordinal) ==
                "TEST_START m10.027\nLOG_UPLOAD_PHASE verify\nTEST_FAIL m10.027 InvalidOperationException\n", "corrupt_input_control");
            using (AuthorityFiles corruptedFiles = new())
            {
                var copied = corruptedFiles.Open(root, Path.Combine(corrupt, UploadName), "corrupt verifier input", "log_upload_corrupt", UploadBytes, singleLink: true);
                Need(copied.ReadBytes(UploadBytes, "log_upload_corrupt").AsSpan().SequenceEqual(corrupted), "corrupt_input_modified"); corruptedFiles.VerifyUnchanged();
            }
            var logHeld = held.Open(root, log, "original managed log", "log_upload_log", 4096, singleLink: true);
            var uploadHeld = held.Open(root, upload, "original managed upload", "log_upload_upload", UploadBytes, singleLink: true);
            AuthorityFiles.RequireDistinct(logHeld, uploadHeld);
            var verified = await Run("verify", [executable, "--verify", "--root", data]); Need(verified.StandardError.Length == 0, "verify_stderr");
            verify = InspectTranscriptValues(verified.StandardOutput, "verify"); Need(write.Files == verify.Files, "verify_files");
            Need(Directory.GetFiles(data).Select(Path.GetFileName).Order(StringComparer.Ordinal).SequenceEqual(new[] { LogName, UploadName }.Order(StringComparer.Ordinal)), "data_inventory");
            inputs.Add(logHeld); inputs.Add(uploadHeld); artifacts.Add(log); artifacts.Add(upload);
            await Save("inputs.json", JsonSerializer.Serialize(inputs.ToDictionary(file => Path.GetRelativePath(root, file.Path).Replace('\\', '/'), file => file.Sha256)));
            foreach (var input in inputs)
            {
                string copy = Path.Combine(destination, "originals", Path.GetRelativePath(root, input.Path)); Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
                File.Copy(input.Path, copy); Need(NativeAotWebPayload.HashFile(copy).Equals(input.Sha256, StringComparison.OrdinalIgnoreCase), "retained_input_hash"); artifacts.Add(copy);
            }
            held.VerifyUnchanged(); RequireCurrentSources(root); Need(ToolingBuildInputs.Validate(root).Count == 0, "tooling_inputs_changed"); succeeded = true;
        }
        catch (Exception error) { failure = error is ToolFailure tool ? tool.Reason : error.GetType().Name; throw; }
        finally
        {
            await File.WriteAllTextAsync(Path.Combine(destination, "commands.json"), JsonSerializer.Serialize(commands), CancellationToken.None);
            await File.WriteAllTextAsync(Path.Combine(destination, "receipt.json"), JsonSerializer.Serialize(new
            { Schema = "CLROS_LOG_UPLOAD_MANAGED_REFERENCE_V1", Succeeded = succeeded, Failure = failure, Write = write, Verify = verify, Files = fileValues,
                LinuxNativeAotQualified = false, GuestQualified = false, HttpQualified = false, FlushObserved = false, PersistenceQualified = false }), CancellationToken.None);
        }
        artifacts.Add(Path.Combine(destination, "commands.json")); artifacts.Add(Path.Combine(destination, "receipt.json"));
        return new(destination, write!, verify!, artifacts);
    }

    /// <summary>Root-serialized original Linux musl publication and two finite
    /// traced native processes. Reuses the pinned socket publisher unchanged;
    /// requires a fresh app cache, original recipe and fresh artifacts child.
    /// Existing publisher/session APIs own, bound and join their children.
    /// Cancellation is checked between those bounded calls, not promised during
    /// the publisher's existing 45-minute call. Partial outputs/failed receipts
    /// remain in place. Returned evidence handles belong to the caller.
    /// This measures Linux reference requirements, never CLROS/HTTP, complete
    /// syscall closure, locking, physical flush, reboot or SQLite authority.</summary>
    public static async Task<LinuxReference> RunLinuxReferenceAsync(string root, string recipeRoot, string destination,
        TextWriter output, CancellationToken cancellation = default)
    {
        const string marker = "nativeaot.log-upload-reference";
        output.WriteLine("TEST_START " + marker); Need(OperatingSystem.IsLinux(), "linux_builder_required");
        cancellation.ThrowIfCancellationRequested(); root = Path.GetFullPath(root); recipeRoot = Path.GetFullPath(recipeRoot);
        destination = Path.GetFullPath(destination, root); RequireFreshOutput(root, destination);
        Need(root != recipeRoot, "original_recipe_required"); Publisher.RequireCurrentSources(root, fresh: true);
        Need(ToolingBuildInputs.Validate(root).Count == 0, "tooling_inputs_changed");
        string app = Path.Combine(root, "apps", Application);
        string[] SourcePaths() => ToolingBuildInputs.SourceInputs(root).Concat(new[] { Path.Combine(app, Application + ".csproj"), Path.Combine(app, "Program.cs") })
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        Dictionary<string, string> Sources() => SourcePaths().ToDictionary(path => Path.GetRelativePath(root, path).Replace('\\', '/'), NativeAotWebPayload.HashFile, StringComparer.Ordinal);
        var before = Sources(); Need(before.Count is > 0 and <= 8192, "source_capacity");
        var held = new AuthorityFiles(); bool transferred = false;
        try
        {
            var originalInputs = new List<AuthorityFile>();
            foreach (string path in new[] { Path.Combine(app, Application + ".csproj"), Path.Combine(app, "Program.cs"), Path.Combine(root, "global.json"),
                Path.Combine(root, "Directory.Build.props"), Path.Combine(root, "Directory.Build.targets"), Path.Combine(root, "tools", "Directory.Build.props") })
                originalInputs.Add(held.Open(root, path, "original reference source", "log_upload_linux_source", 16 * 1024 * 1024, singleLink: true));
            string producerDirectory = Path.GetDirectoryName(typeof(NativeAotLogUploadContract).Assembly.Location)!;
            string[] producerPaths = Directory.GetFiles(producerDirectory, "*.dll").Concat(Directory.GetFiles(producerDirectory, "*.deps.json"))
                .Concat(Directory.GetFiles(producerDirectory, "*.runtimeconfig.json")).Order(StringComparer.Ordinal).ToArray();
            Need(producerPaths.Length is > 0 and <= 64, "producer_capacity");
            foreach (string path in producerPaths)
                originalInputs.Add(held.Open(Path.GetPathRoot(path)!, path, "original managed producer closure", "log_upload_linux_producer", 64 * 1024 * 1024, singleLink: true));
            string recipe = Path.Combine(recipeRoot, "artifacts", "socket-qualification-r01");
            foreach (string name in new[] { "commands.json", "native-inputs.json", "receipt.json" })
                originalInputs.Add(held.Open(recipeRoot, Path.Combine(recipe, name), "original socket recipe", "log_upload_linux_recipe", 16 * 1024 * 1024, singleLink: true));
            Directory.CreateDirectory(destination); AuthorityFiles.RequirePlainPath(root, destination, "log_upload_linux_output");
            using var ownership = new FileStream(Path.Combine(destination, "ownership.lock"), FileMode.CreateNew, FileAccess.Write, FileShare.None);
            bool succeeded = false; string failure = ""; List<object> commands = []; Dictionary<string, string> native = new(StringComparer.Ordinal);
            NativeAotPayload.Inspection? image = null; Transcript? write = null, verify = null; TraceValues? writeTrace = null, verifyTrace = null;
            async Task Save(string name, string contents)
            {
                string path = Path.Combine(destination, name); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                await file.WriteAsync(Encoding.UTF8.GetBytes(contents), CancellationToken.None);
            }
            async Task RunTrace(string phase, string executable, string data)
            {
                cancellation.ThrowIfCancellationRequested();
                string[] arguments = LinuxTraceArguments(executable, data, Path.Combine(destination, phase + "-trace"), phase);
                commands.Add(new { executable = "setsid", arguments = new[] { "strace" }.Concat(arguments).ToArray(), workingDirectory = root, phase });
                bool joined = false; string processFailure = "";
                try
                {
                    await NativeAotWebPayload.ExecuteAsync("strace", arguments, root, Path.Combine(destination, phase), TimeSpan.FromMinutes(2));
                    joined = true;
                }
                catch (Exception error) { processFailure = error.Message.ReplaceLineEndings(" "); throw; }
                finally
                {
                    await Save(phase + ".process.json", JsonSerializer.Serialize(new { Executable = "setsid", Arguments = new[] { "strace" }.Concat(arguments),
                        Succeeded = joined, TerminationConfirmed = joined, Failure = processFailure,
                        Boundary = "Normal return requires the existing owned session and output pipes joined; failed calls grant no cleanup confirmation" }));
                }
                cancellation.ThrowIfCancellationRequested();
            }
            TraceValues ReadTrace(string phase)
            {
                string[] paths = Directory.GetFiles(destination, phase + "-trace.*").Order(StringComparer.Ordinal).ToArray();
                Need(paths.Length is > 0 and <= 64, "trace_inventory"); Dictionary<string, string> copied = new(StringComparer.Ordinal);
                foreach (string path in paths)
                {
                    var file = held.Open(root, path, "original joined native trace", "log_upload_linux_trace", 4 * 1024 * 1024, singleLink: true);
                    copied.Add(Path.GetFileName(path), file.ReadUtf8(4 * 1024 * 1024, "log_upload_linux_trace"));
                }
                var values = InspectTraceValues(copied, phase);
                Need(values.ExitedFiles == values.Files && values.Rows.Any(row => row.Syscall == "execve") &&
                    values.Rows.Any(row => row.Syscall is "exit" or "exit_group"), "trace_process_exit"); return values;
            }
            void CheckInputs()
            {
                RequireCurrentSources(root); Need(ToolingBuildInputs.Validate(root).Count == 0 &&
                    before.OrderBy(row => row.Key, StringComparer.Ordinal).SequenceEqual(Sources().OrderBy(row => row.Key, StringComparer.Ordinal)), "linux_sources_changed");
                Need(native.All(row => NativeAotWebPayload.HashFile(row.Key).Equals(row.Value, StringComparison.OrdinalIgnoreCase)), "linux_native_changed");
                held.VerifyUnchanged();
            }
            try
            {
                // The shared publisher retains NuGet/runtime/object inputs.
                // Also bind its system static-link inputs before publication.
                using (var originalNative = JsonDocument.Parse(held.Open(recipeRoot, Path.Combine(recipe, "native-inputs.json"),
                    "original native tools", "log_upload_linux_recipe", 16 * 1024 * 1024, singleLink: true).ReadBytes(16 * 1024 * 1024, "log_upload_linux_recipe")))
                {
                    string gcc = "";
                    foreach (string name in new[] { "gcc", "setsid" })
                    {
                        string? found = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator).Select(directory => Path.Combine(directory, name)).FirstOrDefault(File.Exists);
                        Need(found is not null, "native_tool_missing");
                        string path = File.ResolveLinkTarget(found!, true)?.FullName ?? Path.GetFullPath(found!);
                        var file = held.Open(Path.GetPathRoot(path)!, path, "original native tool", "log_upload_linux_native", 256 * 1024 * 1024);
                        Need(originalNative.RootElement.TryGetProperty(path, out var prior) && file.Sha256.Equals(prior.GetString(), StringComparison.OrdinalIgnoreCase), "original_tool_changed");
                        native[path] = file.Sha256; originalInputs.Add(file); if (name == "gcc") gcc = path;
                    }
                    foreach (string name in new[] { "libc.a", "libm.a", "libz.a", "libstdc++.a", "libgcc.a", "libgcc_eh.a", "crt1.o", "crti.o", "crtn.o" })
                    {
                        cancellation.ThrowIfCancellationRequested(); string logName = "native-library-" + name;
                        commands.Add(new { executable = "setsid", arguments = new[] { gcc, "-print-file-name=" + name }, workingDirectory = root });
                        await NativeAotWebPayload.ExecuteAsync(gcc, ["-print-file-name=" + name], root, Path.Combine(destination, logName), TimeSpan.FromMinutes(2));
                        string path = held.Open(root, Path.Combine(destination, logName + ".stdout.log"), "system static library location", "log_upload_linux_library", 4096, singleLink: true)
                            .ReadUtf8(4096, "log_upload_linux_library").Trim();
                        Need(Path.IsPathFullyQualified(path) && File.Exists(path), "static_library_missing");
                        path = File.ResolveLinkTarget(path, true)?.FullName ?? Path.GetFullPath(path);
                        var file = held.Open(Path.GetPathRoot(path)!, path, "original system static-link input", "log_upload_linux_library", 256 * 1024 * 1024);
                        native[path] = file.Sha256; originalInputs.Add(file);
                    }
                }
                for (int i = 0; i < originalInputs.Count; i++)
                {
                    var input = originalInputs[i]; string name = "original-inputs/" + i.ToString("D3", CultureInfo.InvariantCulture) + "-" + Path.GetFileName(input.Path);
                    string target = Path.Combine(destination, name); Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.Copy(input.Path, target);
                    Need(NativeAotWebPayload.HashFile(target).Equals(input.Sha256, StringComparison.OrdinalIgnoreCase), "original_copy_changed");
                }
                await Save("original-inputs.json", JsonSerializer.Serialize(originalInputs.Select((file, i) => new
                { file.Path, file.Length, file.Sha256, Retained = "original-inputs/" + i.ToString("D3", CultureInfo.InvariantCulture) + "-" + Path.GetFileName(file.Path) })));
                string publication = Path.Combine(destination, "publication");
                commands.Add(new { producer = nameof(NativeAotSocketFixturePublisher), arguments = new[] { "publish", "--recipe-root", recipeRoot, "--output", publication } });
                Need(await Publisher.RunAsync(root, ["publish", "--recipe-root", recipeRoot, "--output", publication], output) == 0, "linux_publication_failed");
                cancellation.ThrowIfCancellationRequested();
                using var sourceLease = NativeAotPayload.OpenQualificationLease(root); using var recipeLease = NativeAotPayload.OpenQualificationLease(recipeRoot);
                string[] publicationFiles = Directory.GetFiles(publication, "*", SearchOption.AllDirectories)
                    .Where(path => Path.GetFileName(path) != "publication.lock").Order(StringComparer.Ordinal).ToArray();
                Need(publicationFiles.Length is > 0 and <= 512, "publication_capacity");
                foreach (string path in publicationFiles) held.Open(root, path, "original pinned publication", "log_upload_linux_publication", 256 * 1024 * 1024, allowEmpty: true, singleLink: true);
                var publicationReceipt = held.Open(root, Path.Combine(publication, "publication.json"), "original publication receipt", "log_upload_linux_publication", 16 * 1024 * 1024, singleLink: true);
                using (var receipt = JsonDocument.Parse(publicationReceipt.ReadBytes(16 * 1024 * 1024, "log_upload_linux_publication")))
                {
                    var value = receipt.RootElement;
                    Need(value.GetProperty("succeeded").GetBoolean() && !value.GetProperty("appExecuted").GetBoolean() && !value.GetProperty("guestExecuted").GetBoolean() &&
                        value.GetProperty("application").GetString() == Application && value.GetProperty("sdk").GetString() == NativeAotPayload.SdkVersion &&
                        value.GetProperty("runtime").GetString() == NativeAotPayload.RuntimeVersion, "publication_receipt");
                    var recordedSources = value.GetProperty("sources").EnumerateObject().ToDictionary(row => row.Name, row => row.Value.GetString()!, StringComparer.Ordinal);
                    Need(recordedSources.OrderBy(row => row.Key, StringComparer.Ordinal).SequenceEqual(before.OrderBy(row => row.Key, StringComparer.Ordinal)), "publication_sources");
                    foreach (var row in value.GetProperty("native").EnumerateObject())
                    {
                        string hash = row.Value.GetString()!;
                        Need(!native.TryGetValue(row.Name, out string? prior) || hash.Equals(prior, StringComparison.OrdinalIgnoreCase), "native_identity_changed");
                        native[row.Name] = hash;
                    }
                    Need(native.Count is > 0 and <= 16384 && native.All(row => Path.IsPathFullyQualified(row.Key) && row.Value.Length == 64 && row.Value.All(Uri.IsHexDigit)), "native_inventory");
                    foreach (var row in value.GetProperty("outputs").EnumerateObject())
                    {
                        string path = Path.GetFullPath(row.Name, publication);
                        Need(path.StartsWith(publication + Path.DirectorySeparatorChar, StringComparison.Ordinal) && publicationFiles.Contains(path, StringComparer.Ordinal) &&
                            NativeAotWebPayload.HashFile(path).Equals(row.Value.GetString(), StringComparison.OrdinalIgnoreCase), "publication_outputs");
                    }
                }
                string executable = Path.Combine(publication, Application, Application);
                var elf = held.Open(root, executable, "original static native executable", "log_upload_linux_elf", NativeAotPayload.MaximumImageBytes, singleLink: true);
                image = NativeAotPayload.Inspect(elf.ReadBytes(NativeAotPayload.MaximumImageBytes, "log_upload_linux_elf"));
                using (var assets = JsonDocument.Parse(held.Open(root, Path.Combine(publication, "assets.json"), "original native assets", "log_upload_linux_assets", 32 * 1024 * 1024, singleLink: true)
                    .ReadBytes(32 * 1024 * 1024, "log_upload_linux_assets")))
                {
                    string[] dependencies = assets.RootElement.GetProperty("libraries").EnumerateObject().Select(row => row.Name).Order(StringComparer.Ordinal).ToArray();
                    Need(dependencies.All(name => !name.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) && !name.Contains("EntityFramework", StringComparison.OrdinalIgnoreCase)), "foreign_dependency");
                    foreach (var row in NativeAotPayload.CapturePackageInputs(assets.RootElement))
                        Need(native.TryGetValue(row.Key, out string? hash) && hash.Equals(row.Value, StringComparison.OrdinalIgnoreCase), "dependency_identity");
                    await Save("dependencies.json", JsonSerializer.Serialize(dependencies));
                }
                CheckInputs(); string data = Path.Combine(destination, "data"); Directory.CreateDirectory(data);
                string log = Path.Combine(data, LogName), upload = Path.Combine(data, UploadName);
                foreach (string path in new[] { log, upload }) { using var empty = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None); }
                string logIdentity, uploadIdentity;
                using (var empty = new AuthorityFiles())
                {
                    var a = empty.Open(root, log, "pre-existing empty log", "log_upload_linux_empty", 0, allowEmpty: true, singleLink: true);
                    var b = empty.Open(root, upload, "pre-existing empty upload", "log_upload_linux_empty", 0, allowEmpty: true, singleLink: true);
                    AuthorityFiles.RequireDistinct(a, b); logIdentity = a.InitialIdentity.Key; uploadIdentity = b.InitialIdentity.Key;
                    await Save("initial-files.json", JsonSerializer.Serialize(new[] { new { a.Path, a.Length, a.Sha256, Identity = logIdentity }, new { b.Path, b.Length, b.Sha256, Identity = uploadIdentity } }));
                    empty.VerifyUnchanged();
                }
                await RunTrace("write", executable, data);
                write = InspectTranscriptValues(held.Open(root, Path.Combine(destination, "write.stdout.log"), "original native write stdout", "log_upload_linux_stdout", MaximumTranscriptBytes, singleLink: true)
                    .ReadUtf8(MaximumTranscriptBytes, "log_upload_linux_stdout"), "write");
                Need(held.Open(root, Path.Combine(destination, "write.stderr.log"), "original write stderr", "log_upload_linux_stderr", 0, allowEmpty: true, singleLink: true).Length == 0, "write_stderr");
                writeTrace = ReadTrace("write"); await Save("write-trace-values.json", JsonSerializer.Serialize(writeTrace));
                var originalLog = held.Open(root, log, "original native log", "log_upload_linux_log", 4096, singleLink: true);
                var originalUpload = held.Open(root, upload, "original native upload", "log_upload_linux_upload", UploadBytes, singleLink: true);
                AuthorityFiles.RequireDistinct(originalLog, originalUpload, elf);
                Need(originalLog.InitialIdentity.Key == logIdentity && originalUpload.InitialIdentity.Key == uploadIdentity &&
                    write.Files == InspectFileValues(originalLog.ReadBytes(4096, "log_upload_linux_log"), originalUpload.ReadBytes(UploadBytes, "log_upload_linux_upload")), "write_files");
                CheckInputs(); await RunTrace("verify", executable, data);
                verify = InspectTranscriptValues(held.Open(root, Path.Combine(destination, "verify.stdout.log"), "original independent native verify stdout", "log_upload_linux_stdout", MaximumTranscriptBytes, singleLink: true)
                    .ReadUtf8(MaximumTranscriptBytes, "log_upload_linux_stdout"), "verify");
                Need(held.Open(root, Path.Combine(destination, "verify.stderr.log"), "original verify stderr", "log_upload_linux_stderr", 0, allowEmpty: true, singleLink: true).Length == 0, "verify_stderr");
                verifyTrace = ReadTrace("verify"); await Save("verify-trace-values.json", JsonSerializer.Serialize(verifyTrace));
                Need(write.Files == verify.Files && verify.Files == InspectFileValues(originalLog.ReadBytes(4096, "log_upload_linux_log"), originalUpload.ReadBytes(UploadBytes, "log_upload_linux_upload")) &&
                    Directory.GetFiles(data).Select(Path.GetFileName).Order(StringComparer.Ordinal).SequenceEqual(new[] { LogName, UploadName }.Order(StringComparer.Ordinal)), "verify_files");
                CheckInputs(); succeeded = true;
            }
            catch (Exception error)
            {
                failure = error is ToolFailure tool ? tool.Reason : error.Message.ReplaceLineEndings(" ");
                output.WriteLine("TEST_FAIL " + marker + " " + failure); throw;
            }
            finally
            {
                await Save("commands.json", JsonSerializer.Serialize(commands)); await Save("sources.json", JsonSerializer.Serialize(before));
                await Save("native-inputs.json", JsonSerializer.Serialize(native));
                var outputs = Directory.GetFiles(destination, "*", SearchOption.AllDirectories).Where(path => Path.GetFileName(path) is not ("ownership.lock" or "publication.lock"))
                    .Order(StringComparer.Ordinal).ToDictionary(path => Path.GetRelativePath(destination, path).Replace('\\', '/'), NativeAotWebPayload.HashFile, StringComparer.Ordinal);
                await Save("receipt.json", JsonSerializer.Serialize(new { Schema = "CLROS_LOG_UPLOAD_LINUX_REFERENCE_V1", Succeeded = succeeded, Failure = failure,
                    Image = image, Write = write, Verify = verify, WriteUnjoinedRows = writeTrace?.UnjoinedRows, VerifyUnjoinedRows = verifyTrace?.UnjoinedRows,
                    WriteUnreturnedRows = writeTrace?.UnreturnedRows, VerifyUnreturnedRows = verifyTrace?.UnreturnedRows,
                    CompleteTraceProjection = writeTrace?.CompleteTraceProjection == true && verifyTrace?.CompleteTraceProjection == true,
                    Sources = before, Native = native, Outputs = outputs, GuestQualified = false, HttpQualified = false, SyscallClosureQualified = false,
                    LockQualified = false, FlushObserved = false, PersistenceQualified = false, SqliteQualified = false }));
            }
            string[] artifactPaths = Directory.GetFiles(destination, "*", SearchOption.AllDirectories).Where(path => Path.GetFileName(path) is not ("ownership.lock" or "publication.lock"))
                .Order(StringComparer.Ordinal).ToArray(); Need(artifactPaths.Length is > 0 and <= 768, "artifact_capacity");
            var artifacts = artifactPaths.Select(path => held.Open(root, path, "original Linux reference evidence", "log_upload_linux_evidence", 256 * 1024 * 1024, allowEmpty: true, singleLink: true)).ToArray();
            AuthorityFiles.RequireDistinct(artifacts); held.VerifyUnchanged();
            var result = new LinuxReference(destination, image!, write!, verify!, writeTrace!, verifyTrace!, held, artifacts);
            transferred = true; output.WriteLine("TEST_PASS " + marker); return result;
        }
        finally { if (!transferred) held.Dispose(); }
    }

    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static void Need(bool condition, string reason) { if (!condition) throw new ToolFailure("log_upload_" + reason); }
}
