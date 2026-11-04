using System.Buffers.Binary;
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

public static partial class NativeAotLogUploadContract
{
    /// <summary>Root-serialized original Linux musl publication and two finite
    /// traced native processes. Reuses the pinned socket publisher unchanged;
    /// requires a fresh app cache, original recipe and fresh artifacts child.
    /// Existing publisher/session APIs own, bound and join their children.
    /// Cancellation is checked between those bounded calls, not promised during
    /// the publisher's existing 45-minute call. Partial outputs/failed receipts
    /// remain in place. Returned evidence handles belong to the caller.
    /// This measures Linux reference requirements, never CLROS/HTTP, complete
    /// syscall closure, locking, physical flush, reboot or SQLite authority.</summary>

    public static async Task<LinuxReference> RunLinuxReferenceAsync(string root, string recipeRoot, string destination, TextWriter output, CancellationToken cancellation = default)
    {
        const string marker = "nativeaot.log-upload-reference";
        output.WriteLine("TEST_START " + marker);
        Need(OperatingSystem.IsLinux(), "linux_builder_required");
        cancellation.ThrowIfCancellationRequested();
        root = Path.GetFullPath(root);
        recipeRoot = Path.GetFullPath(recipeRoot);
        destination = Path.GetFullPath(destination, root);
        RequireFreshOutput(root, destination);
        Need(root != recipeRoot, "original_recipe_required");
        Publisher.RequireCurrentSources(root, fresh: true);
        Need(ToolingBuildInputs.Validate(root).Count == 0, "tooling_inputs_changed");
        string app = Path.Combine(root, "apps", Application);
        string[] SourcePaths() => ToolingBuildInputs.SourceInputs(root).Concat(new[] { Path.Combine(app, Application + ".csproj"), Path.Combine(app, "Program.cs") }).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        Dictionary<string, string> Sources() => SourcePaths().ToDictionary(path => Path.GetRelativePath(root, path).Replace('\\', '/'), NativeAotWebPayload.HashFile, StringComparer.Ordinal);
        var before = Sources();
        Need(before.Count is > 0 and <= 8192, "source_capacity");
        var held = new AuthorityFiles();
        bool transferred = false;
        try
        {
            var originalInputs = new List<AuthorityFile>();
            foreach (string path in new[]
            {
                Path.Combine(app, Application + ".csproj"),
                Path.Combine(app, "Program.cs"),
                Path.Combine(root, "global.json"),
                Path.Combine(root, "Directory.Build.props"),
                Path.Combine(root, "Directory.Build.targets"),
                Path.Combine(root, "tools", "Directory.Build.props")
            }

            )
                originalInputs.Add(held.Open(root, path, "original reference source", "log_upload_linux_source", 16 * 1024 * 1024, singleLink: true));
            string producerDirectory = Path.GetDirectoryName(typeof(NativeAotLogUploadContract).Assembly.Location)!;
            string[] producerPaths = Directory.GetFiles(producerDirectory, "*.dll").Concat(Directory.GetFiles(producerDirectory, "*.deps.json")).Concat(Directory.GetFiles(producerDirectory, "*.runtimeconfig.json")).Order(StringComparer.Ordinal).ToArray();
            Need(producerPaths.Length is > 0 and <= 64, "producer_capacity");
            foreach (string path in producerPaths)
                originalInputs.Add(held.Open(Path.GetPathRoot(path)!, path, "original managed producer closure", "log_upload_linux_producer", 64 * 1024 * 1024, singleLink: true));
            string recipe = Path.Combine(recipeRoot, "artifacts", "socket-qualification-r01");
            foreach (string name in new[]
            {
                "commands.json",
                "native-inputs.json",
                "receipt.json"
            }

            )
                originalInputs.Add(held.Open(recipeRoot, Path.Combine(recipe, name), "original socket recipe", "log_upload_linux_recipe", 16 * 1024 * 1024, singleLink: true));
            Directory.CreateDirectory(destination);
            AuthorityFiles.RequirePlainPath(root, destination, "log_upload_linux_output");
            using var ownership = new FileStream(Path.Combine(destination, "ownership.lock"), FileMode.CreateNew, FileAccess.Write, FileShare.None);
            bool succeeded = false;
            string failure = "";
            List<object> commands = [];
            Dictionary<string, string> native = new(StringComparer.Ordinal);
            NativeAotPayload.Inspection? image = null;
            Transcript? write = null, verify = null;
            TraceValues? writeTrace = null, verifyTrace = null;
            FileValues? actualFiles = null;
            async Task Save(string name, string contents)
            {
                string path = Path.Combine(destination, name);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                await file.WriteAsync(Encoding.UTF8.GetBytes(contents), CancellationToken.None);
            }

            async Task RunTrace(string phase, string executable, string data)
            {
                cancellation.ThrowIfCancellationRequested();
                string[] arguments = LinuxTraceArguments(executable, data, Path.Combine(destination, phase + "-trace"), phase);
                commands.Add(new
                {
                    executable = "setsid",
                    arguments = new[] { "strace" }.Concat(arguments).ToArray(),
                    workingDirectory = root,
                    phase
                });
                bool joined = false;
                string processFailure = "";
                try
                {
                    await NativeAotWebPayload.ExecuteAsync("strace", arguments, root, Path.Combine(destination, phase), TimeSpan.FromMinutes(2));
                    joined = true;
                }
                catch (Exception error)
                {
                    processFailure = error.Message.ReplaceLineEndings(" ");
                    throw;
                }
                finally
                {
                    await Save(phase + ".process.json", JsonSerializer.Serialize(new
                    {
                        Executable = "setsid",
                        Arguments = new[] { "strace" }.Concat(arguments),
                        Succeeded = joined,
                        TerminationConfirmed = joined,
                        Failure = processFailure,
                        Boundary = "Normal return requires the existing owned session and output pipes joined; failed calls grant no cleanup confirmation"
                    }));
                }

                cancellation.ThrowIfCancellationRequested();
            }

            TraceValues ReadTrace(string phase)
            {
                string[] paths = Directory.GetFiles(destination, phase + "-trace.*").Order(StringComparer.Ordinal).ToArray();
                Need(paths.Length is > 0 and <= 64, "trace_inventory");
                Dictionary<string, string> copied = new(StringComparer.Ordinal);
                foreach (string path in paths)
                {
                    var file = held.Open(root, path, "original joined native trace", "log_upload_linux_trace", 4 * 1024 * 1024, singleLink: true);
                    copied.Add(Path.GetFileName(path), file.ReadUtf8(4 * 1024 * 1024, "log_upload_linux_trace"));
                }

                var values = InspectTraceValues(copied, phase);
                Need(values.ExitedFiles == values.Files && values.Rows.Any(row => row.Syscall == "execve") && values.Rows.Any(row => row.Syscall is "exit" or "exit_group"), "trace_process_exit");
                return values;
            }

            void CheckInputs()
            {
                RequireCurrentSources(root);
                Need(ToolingBuildInputs.Validate(root).Count == 0 && before.OrderBy(row => row.Key, StringComparer.Ordinal).SequenceEqual(Sources().OrderBy(row => row.Key, StringComparer.Ordinal)), "linux_sources_changed");
                Need(native.All(row => NativeAotWebPayload.HashFile(row.Key).Equals(row.Value, StringComparison.OrdinalIgnoreCase)), "linux_native_changed");
                held.VerifyUnchanged();
            }

            try
            {
                // The shared publisher retains NuGet/runtime/object inputs.
                // Also bind its system static-link inputs before publication.
                using (var originalNative = JsonDocument.Parse(held.Open(recipeRoot, Path.Combine(recipe, "native-inputs.json"), "original native tools", "log_upload_linux_recipe", 16 * 1024 * 1024, singleLink: true).ReadBytes(16 * 1024 * 1024, "log_upload_linux_recipe")))
                {
                    string gcc = "";
                    foreach (string name in new[]
                    {
                        "gcc",
                        "setsid"
                    }

                    )
                    {
                        string? found = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator).Select(directory => Path.Combine(directory, name)).FirstOrDefault(File.Exists);
                        Need(found is not null, "native_tool_missing");
                        string path = File.ResolveLinkTarget(found!, true)?.FullName ?? Path.GetFullPath(found!);
                        var file = held.Open(Path.GetPathRoot(path)!, path, "original native tool", "log_upload_linux_native", 256 * 1024 * 1024);
                        Need(originalNative.RootElement.TryGetProperty(path, out var prior) && file.Sha256.Equals(prior.GetString(), StringComparison.OrdinalIgnoreCase), "original_tool_changed");
                        native[path] = file.Sha256;
                        originalInputs.Add(file);
                        if (name == "gcc")
                            gcc = path;
                    }

                    foreach (string name in new[]
                    {
                        "libc.a",
                        "libm.a",
                        "libz.a",
                        "libstdc++.a",
                        "libgcc.a",
                        "libgcc_eh.a",
                        "crt1.o",
                        "crti.o",
                        "crtn.o"
                    }

                    )
                    {
                        cancellation.ThrowIfCancellationRequested();
                        string logName = "native-library-" + name;
                        commands.Add(new
                        {
                            executable = "setsid",
                            arguments = new[] { gcc, "-print-file-name=" + name },
                            workingDirectory = root
                        });
                        await NativeAotWebPayload.ExecuteAsync(gcc, ["-print-file-name=" + name], root, Path.Combine(destination, logName), TimeSpan.FromMinutes(2));
                        string path = held.Open(root, Path.Combine(destination, logName + ".stdout.log"), "system static library location", "log_upload_linux_library", 4096, singleLink: true).ReadUtf8(4096, "log_upload_linux_library").Trim();
                        Need(Path.IsPathFullyQualified(path) && File.Exists(path), "static_library_missing");
                        path = File.ResolveLinkTarget(path, true)?.FullName ?? Path.GetFullPath(path);
                        var file = held.Open(Path.GetPathRoot(path)!, path, "original system static-link input", "log_upload_linux_library", 256 * 1024 * 1024);
                        native[path] = file.Sha256;
                        originalInputs.Add(file);
                    }
                }

                for (int i = 0; i < originalInputs.Count; i++)
                {
                    var input = originalInputs[i];
                    string name = "original-inputs/" + i.ToString("D3", CultureInfo.InvariantCulture) + "-" + Path.GetFileName(input.Path);
                    string target = Path.Combine(destination, name);
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    File.Copy(input.Path, target);
                    Need(NativeAotWebPayload.HashFile(target).Equals(input.Sha256, StringComparison.OrdinalIgnoreCase), "original_copy_changed");
                }

                await Save("original-inputs.json", JsonSerializer.Serialize(originalInputs.Select((file, i) => new { file.Path, file.Length, file.Sha256, Retained = "original-inputs/" + i.ToString("D3", CultureInfo.InvariantCulture) + "-" + Path.GetFileName(file.Path) })));
                string publication = Path.Combine(destination, "publication");
                commands.Add(new
                {
                    producer = nameof(NativeAotSocketFixturePublisher),
                    arguments = new[] { "publish", "--recipe-root", recipeRoot, "--output", publication }
                });
                Need(await Publisher.RunAsync(root, ["publish", "--recipe-root", recipeRoot, "--output", publication], output) == 0, "linux_publication_failed");
                cancellation.ThrowIfCancellationRequested();
                using var sourceLease = NativeAotPayload.OpenQualificationLease(root);
                using var recipeLease = NativeAotPayload.OpenQualificationLease(recipeRoot);
                string[] publicationFiles = Directory.GetFiles(publication, "*", SearchOption.AllDirectories).Where(path => Path.GetFileName(path) != "publication.lock").Order(StringComparer.Ordinal).ToArray();
                Need(publicationFiles.Length is > 0 and <= 512, "publication_capacity");
                foreach (string path in publicationFiles)
                    held.Open(root, path, "original pinned publication", "log_upload_linux_publication", 256 * 1024 * 1024, allowEmpty: true, singleLink: true);
                var publicationReceipt = held.Open(root, Path.Combine(publication, "publication.json"), "original publication receipt", "log_upload_linux_publication", 16 * 1024 * 1024, singleLink: true);
                using (var receipt = JsonDocument.Parse(publicationReceipt.ReadBytes(16 * 1024 * 1024, "log_upload_linux_publication")))
                {
                    var value = receipt.RootElement;
                    Need(value.GetProperty("succeeded").GetBoolean() && !value.GetProperty("appExecuted").GetBoolean() && !value.GetProperty("guestExecuted").GetBoolean() && value.GetProperty("application").GetString() == Application && value.GetProperty("sdk").GetString() == NativeAotPayload.SdkVersion && value.GetProperty("runtime").GetString() == NativeAotPayload.RuntimeVersion, "publication_receipt");
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
                        Need(path.StartsWith(publication + Path.DirectorySeparatorChar, StringComparison.Ordinal) && publicationFiles.Contains(path, StringComparer.Ordinal) && NativeAotWebPayload.HashFile(path).Equals(row.Value.GetString(), StringComparison.OrdinalIgnoreCase), "publication_outputs");
                    }
                }

                string executable = Path.Combine(publication, Application, Application);
                var elf = held.Open(root, executable, "original static native executable", "log_upload_linux_elf", NativeAotPayload.MaximumImageBytes, singleLink: true);
                image = NativeAotPayload.Inspect(elf.ReadBytes(NativeAotPayload.MaximumImageBytes, "log_upload_linux_elf"));
                using (var assets = JsonDocument.Parse(held.Open(root, Path.Combine(publication, "assets.json"), "original native assets", "log_upload_linux_assets", 32 * 1024 * 1024, singleLink: true).ReadBytes(32 * 1024 * 1024, "log_upload_linux_assets")))
                {
                    string[] dependencies = assets.RootElement.GetProperty("libraries").EnumerateObject().Select(row => row.Name).Order(StringComparer.Ordinal).ToArray();
                    Need(dependencies.All(name => !name.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) && !name.Contains("EntityFramework", StringComparison.OrdinalIgnoreCase)), "foreign_dependency");
                    foreach (var row in NativeAotPayload.CapturePackageInputs(assets.RootElement))
                        Need(native.TryGetValue(row.Key, out string? hash) && hash.Equals(row.Value, StringComparison.OrdinalIgnoreCase), "dependency_identity");
                    await Save("dependencies.json", JsonSerializer.Serialize(dependencies));
                }

                CheckInputs();
                string data = Path.Combine(destination, "data");
                Directory.CreateDirectory(data);
                string log = Path.Combine(data, LogName), upload = Path.Combine(data, UploadName);
                foreach (string path in new[]
                {
                    log,
                    upload
                }

                )
                {
                    using var empty = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                }

                string logIdentity, uploadIdentity;
                using (var empty = new AuthorityFiles())
                {
                    var a = empty.Open(root, log, "pre-existing empty log", "log_upload_linux_empty", 0, allowEmpty: true, singleLink: true);
                    var b = empty.Open(root, upload, "pre-existing empty upload", "log_upload_linux_empty", 0, allowEmpty: true, singleLink: true);
                    AuthorityFiles.RequireDistinct(a, b);
                    logIdentity = a.InitialIdentity.Key;
                    uploadIdentity = b.InitialIdentity.Key;
                    await Save("initial-files.json", JsonSerializer.Serialize(new[] { new { a.Path, a.Length, a.Sha256, Identity = logIdentity }, new { b.Path, b.Length, b.Sha256, Identity = uploadIdentity } }));
                    empty.VerifyUnchanged();
                }

                await RunTrace("write", executable, data);
                write = InspectTranscriptValues(held.Open(root, Path.Combine(destination, "write.stdout.log"), "original native write stdout", "log_upload_linux_stdout", MaximumTranscriptBytes, singleLink: true).ReadUtf8(MaximumTranscriptBytes, "log_upload_linux_stdout"), "write");
                Need(held.Open(root, Path.Combine(destination, "write.stderr.log"), "original write stderr", "log_upload_linux_stderr", 0, allowEmpty: true, singleLink: true).Length == 0, "write_stderr");
                writeTrace = ReadTrace("write");
                await Save("write-trace-values.json", JsonSerializer.Serialize(writeTrace));
                var originalLog = held.Open(root, log, "original native log", "log_upload_linux_log", 4096, singleLink: true);
                var originalUpload = held.Open(root, upload, "original native upload", "log_upload_linux_upload", UploadBytes, singleLink: true);
                AuthorityFiles.RequireDistinct(originalLog, originalUpload, elf);
                actualFiles = InspectFileValues(originalLog.ReadBytes(4096, "log_upload_linux_log"), originalUpload.ReadBytes(UploadBytes, "log_upload_linux_upload"));
                Need(originalLog.InitialIdentity.Key == logIdentity && originalUpload.InitialIdentity.Key == uploadIdentity && write.ExpectedFiles == actualFiles.Value, "write_files");
                CheckInputs();
                await RunTrace("verify", executable, data);
                verify = InspectTranscriptValues(held.Open(root, Path.Combine(destination, "verify.stdout.log"), "original independent native verify stdout", "log_upload_linux_stdout", MaximumTranscriptBytes, singleLink: true).ReadUtf8(MaximumTranscriptBytes, "log_upload_linux_stdout"), "verify");
                Need(held.Open(root, Path.Combine(destination, "verify.stderr.log"), "original verify stderr", "log_upload_linux_stderr", 0, allowEmpty: true, singleLink: true).Length == 0, "verify_stderr");
                verifyTrace = ReadTrace("verify");
                await Save("verify-trace-values.json", JsonSerializer.Serialize(verifyTrace));
                Need(write.ExpectedFiles == verify.ExpectedFiles && verify.ExpectedFiles == InspectFileValues(originalLog.ReadBytes(4096, "log_upload_linux_log"), originalUpload.ReadBytes(UploadBytes, "log_upload_linux_upload")) && Directory.GetFiles(data).Select(Path.GetFileName).Order(StringComparer.Ordinal).SequenceEqual(new[] { LogName, UploadName }.Order(StringComparer.Ordinal)), "verify_files");
                CheckInputs();
                succeeded = true;
            }
            catch (Exception error)
            {
                failure = error is ToolFailure tool ? tool.Reason : error.Message.ReplaceLineEndings(" ");
                output.WriteLine("TEST_FAIL " + marker + " " + failure);
                throw;
            }
            finally
            {
                await Save("commands.json", JsonSerializer.Serialize(commands));
                await Save("sources.json", JsonSerializer.Serialize(before));
                await Save("native-inputs.json", JsonSerializer.Serialize(native));
                var outputs = Directory.GetFiles(destination, "*", SearchOption.AllDirectories).Where(path => Path.GetFileName(path) is not ("ownership.lock" or "publication.lock")).Order(StringComparer.Ordinal).ToDictionary(path => Path.GetRelativePath(destination, path).Replace('\\', '/'), NativeAotWebPayload.HashFile, StringComparer.Ordinal);
                await Save("receipt.json", JsonSerializer.Serialize(new
                {
                    Schema = "CLROS_LOG_UPLOAD_LINUX_REFERENCE_V2",
                    Succeeded = succeeded,
                    Failure = failure,
                    Image = image,
                    Write = write,
                    Verify = verify,
                    Files = actualFiles,
                    WriteUnjoinedRows = writeTrace?.UnjoinedRows,
                    VerifyUnjoinedRows = verifyTrace?.UnjoinedRows,
                    WriteUnreturnedRows = writeTrace?.UnreturnedRows,
                    VerifyUnreturnedRows = verifyTrace?.UnreturnedRows,
                    CompleteTraceProjection = writeTrace?.CompleteTraceProjection == true && verifyTrace?.CompleteTraceProjection == true,
                    Sources = before,
                    Native = native,
                    Outputs = outputs,
                    GuestQualified = false,
                    HttpQualified = false,
                    SyscallClosureQualified = false,
                    LockQualified = false,
                    FlushObserved = false,
                    PersistenceQualified = false,
                    SqliteQualified = false
                }));
            }

            string[] artifactPaths = Directory.GetFiles(destination, "*", SearchOption.AllDirectories).Where(path => Path.GetFileName(path) is not ("ownership.lock" or "publication.lock")).Order(StringComparer.Ordinal).ToArray();
            Need(artifactPaths.Length is > 0 and <= 768, "artifact_capacity");
            var artifacts = artifactPaths.Select(path => held.Open(root, path, "original Linux reference evidence", "log_upload_linux_evidence", 256 * 1024 * 1024, allowEmpty: true, singleLink: true)).ToArray();
            AuthorityFiles.RequireDistinct(artifacts);
            held.VerifyUnchanged();
            var result = new LinuxReference(destination, image!, write!, verify!, writeTrace!, verifyTrace!, held, artifacts);
            // PublishLinuxReference takes ownership, including failed final output.
            transferred = true;
            return PublishLinuxReference(result, output, marker);
        }
        finally
        {
            if (!transferred)
                held.Dispose();
        }
    }

    /// <summary>Finish the host-side Linux result handoff after producer checks.
    /// Takes ownership of result on entry. Successful final output returns the
    /// same result with its retained handles for caller disposal; failed output
    /// disposes it before rethrowing. Receipt and qualification state is unchanged.</summary>
    internal static LinuxReference PublishLinuxReference(LinuxReference result, TextWriter output, string marker)
    {
        try
        {
            output.WriteLine("TEST_PASS " + marker);
            return result;
        }
        catch
        {
            result.Dispose();
            throw;
        }
    }

}
