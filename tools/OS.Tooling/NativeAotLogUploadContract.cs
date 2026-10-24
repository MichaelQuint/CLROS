using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OS.Tooling;

/// <summary>Finite ordinary FileStream requirement candidate and owned Windows
/// managed reference. Copied values and Flush(true) policy do not grant native,
/// HTTP, physical flush, persistence, SQLite or CLROS execution authority.</summary>
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

    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static void Need(bool condition, string reason) { if (!condition) throw new ToolFailure("log_upload_" + reason); }
}
