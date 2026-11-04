using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32.SafeHandles;

namespace OS.Tooling;
/// <summary>Direct Microsoft.Data.Sqlite candidate and host requirement values.
/// No EF Core, native publication, guest storage or durability is admitted here.
/// Linux static linkage and original traced NativeAOT execution remain separate.</summary>

public static partial class NativeAotSqliteContract
{
    /// <summary>Own one fresh Linux source/cache namespace, original recipe
    /// lease, pinned upstream object/archive build, static NativeAOT publication
    /// and finite traced reference process. Preserve every attempted command and
    /// failed output. The original socket recipe and accepted application bytes
    /// remain read-only. This supplies Linux-reference requirements only; guest
    /// execution, fault/recovery and durable transactions require their own gates.
    /// Caller must serialize the external builder/transport with other owners.
    /// Optional archiveDependencyRoot explicitly reuses only the reviewed r03
    /// receipt's two identical objects/archives, held through consumer finish.
    /// It does not claim a new compilation. Default builds twice with unchanged
    /// ten-minute per-compile limits; every app publication and process is fresh.</summary>

    public static async Task<int> RunLinuxReferenceAsync(string root, string recipeRoot, string sourceArchive, string destination, TextWriter output, string? archiveDependencyRoot = null)
    {
        const string marker = "nativeaot.sqlite-reference";
        output.WriteLine("TEST_START " + marker);
        Need(OperatingSystem.IsLinux(), "linux_builder_required");
        root = Path.GetFullPath(root);
        recipeRoot = Path.GetFullPath(recipeRoot);
        sourceArchive = Path.GetFullPath(sourceArchive, root);
        destination = Path.GetFullPath(destination, root);
        Need(root != recipeRoot && sourceArchive.StartsWith(Path.Combine(root, "artifacts") + "/", StringComparison.Ordinal) && destination.StartsWith(Path.Combine(root, "artifacts") + "/", StringComparison.Ordinal) && !Path.Exists(destination), "linux_fresh_output");
        using var owned = NativeAotPayload.OpenQualificationLease(root);
        using var recipeLease = NativeAotPayload.OpenQualificationLease(recipeRoot);
        using AuthorityFiles held = new();
        Need(ToolingBuildInputs.Validate(root).Count == 0, "tooling_inputs_changed");
        _ = NativeAotSocketPackage.Create(root, recipeRoot, asynchronous: true);
        string app = Path.Combine(root, "apps", Application), project = Path.Combine(app, Application + ".csproj");
        Need(!Path.Exists(Path.Combine(app, "bin")) && !Path.Exists(Path.Combine(app, "obj")) && Directory.GetFiles(app).Select(Path.GetFileName).Order(StringComparer.Ordinal).SequenceEqual(new[] { Application + ".csproj", "Program.cs" }.Order(StringComparer.Ordinal)), "linux_fresh_cache");
        string[] SourcePaths() => ToolingBuildInputs.SourceInputs(root).Concat(new[] { project, Path.Combine(app, "Program.cs") }).Distinct().Order(StringComparer.Ordinal).ToArray();
        var sources = SourcePaths().ToDictionary(p => p, NativeAotWebPayload.HashFile, StringComparer.Ordinal);
        var original = held.Open(recipeRoot, Path.Combine(recipeRoot, "artifacts/socket-qualification-r01/commands.json"), "original static recipe", "sqlite_original_recipe", 2 * 1024 * 1024, singleLink: true);
        string recipeCommands = Encoding.UTF8.GetString(original.ReadBytes(2 * 1024 * 1024, "sqlite_original_recipe"));
        var sourceInput = held.Open(root, sourceArchive, "upstream source ZIP", "sqlite_upstream_source", 4 * 1024 * 1024, singleLink: true);
        var upstream = InspectSourceArchiveValues(sourceInput.ReadBytes(4 * 1024 * 1024, "sqlite_upstream_source"));
        IReadOnlyDictionary<string, AuthorityFile>? dependencyInputs = null;
        if (archiveDependencyRoot is not null)
        {
            archiveDependencyRoot = Path.GetFullPath(archiveDependencyRoot);
            Need(archiveDependencyRoot != root && archiveDependencyRoot != recipeRoot && archiveDependencyRoot != destination && !archiveDependencyRoot.StartsWith(destination + "/", StringComparison.Ordinal) && !destination.StartsWith(archiveDependencyRoot + "/", StringComparison.Ordinal), "dependency_path");
            dependencyInputs = HoldArchiveDependencyInputs(held, archiveDependencyRoot, ArchiveDependencyReceiptSha256);
        }

        Directory.CreateDirectory(destination);
        AuthorityFiles.RequirePlainPath(root, destination, "sqlite_native_output");
        using var ownership = new FileStream(Path.Combine(destination, "qualification.lock"), FileMode.CreateNew, FileAccess.Write, FileShare.None);
        List<object> commands = [];
        Dictionary<string, string> native = new(StringComparer.Ordinal);
        bool succeeded = false;
        string failure = "";
        NativeAotPayload.Inspection? image = null;
        Archive? archive = null;
        Transcript? transcript = null, verification = null;
        Trace? trace = null, verificationTrace = null;
        ArchiveDependency? dependency = null;
        Dictionary<string, CompilerInput> compilerInputs = new(StringComparer.Ordinal);
        int freshCompilesCompleted = 0;
        IReadOnlyList<string>? imports = null, retainedImports = null;
        string[] metadataWithoutArchive = [];
        IReadOnlyList<WeakUndefined>? weakUndefined = null;
        async Task<string> Run(string name, string executable, IReadOnlyList<string> arguments, TimeSpan? timeout = null)
        {
            TimeSpan limit = timeout ?? TimeSpan.FromMinutes(2);
            var attempt = new CommandAttempt
            {
                executable = executable,
                arguments = arguments.ToArray(),
                workingDirectory = root,
                log = name,
                phase = name,
                limitSeconds = limit.TotalSeconds
            };
            commands.Add(attempt);
            var elapsed = Stopwatch.StartNew();
            try
            {
                await NativeAotWebPayload.ExecuteAsync(executable, attempt.arguments, root, Path.Combine(destination, name), limit);
                string text = await File.ReadAllTextAsync(Path.Combine(destination, name + ".stdout.log"));
                attempt.outcome = "succeeded";
                return text;
            }
            catch (OperationCanceledException)
            {
                attempt.outcome = "timed_out";
                attempt.failure = "sqlite_contract_command_timeout";
                throw new ToolFailure(attempt.failure, "phase=" + name + " limitSeconds=" + limit.TotalSeconds.ToString(CultureInfo.InvariantCulture));
            }
            catch (Exception error)
            {
                attempt.outcome = "failed";
                attempt.failure = error is ToolFailure tool ? tool.Reason : error.GetType().Name;
                throw;
            }
            finally
            {
                attempt.elapsedSeconds = elapsed.Elapsed.TotalSeconds;
            }
        }

        string Native(string path)
        {
            path = File.ResolveLinkTarget(path, true)?.FullName ?? Path.GetFullPath(path);
            string hash = NativeAotWebPayload.HashFile(path);
            Need(!native.TryGetValue(path, out string? prior) || prior == hash, "native_input_changed");
            native[path] = hash;
            return path;
        }

        try
        {
            Need((await Run("sdk", "dotnet", ["--version"])).Trim() == NativeAotPayload.SdkVersion, "sdk_pin");
            var originalNative = JsonSerializer.Deserialize<Dictionary<string, string>>(await File.ReadAllTextAsync(Path.Combine(recipeRoot, "artifacts/socket-qualification-r01/native-inputs.json")))!;
            foreach (string name in CompilerTools)
            {
                string? path = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator).Select(p => Path.Combine(p, name)).FirstOrDefault(File.Exists);
                Need(path is not null, "native_tool_missing");
                path = File.ResolveLinkTarget(path!, true)?.FullName ?? Path.GetFullPath(path!);
                Native(path);
                compilerInputs.Add(name, new(path, native[path]));
                if (name != "ar")
                    Need(originalNative.TryGetValue(path, out var hash) && hash == native[path], "original_tool_changed");
                if (name is "gcc" or "ar")
                    await Run("version-" + name, name, ["--version"]);
            }

            foreach (string library in CompilerLibraries)
            {
                string path = (await Run("library-" + library, "gcc", ["-print-file-name=" + library])).Trim();
                Need(Path.IsPathFullyQualified(path) && File.Exists(path), "static_library_missing");
                path = Native(path);
                compilerInputs.Add(library, new(path, native[path]));
            }

            string sourceDirectory = Path.Combine(destination, "upstream");
            Directory.CreateDirectory(sourceDirectory);
            string c = Path.Combine(sourceDirectory, "sqlite3.c"), header = Path.Combine(sourceDirectory, "sqlite3.h"), a = Path.Combine(destination, "libe_sqlite3.a");
            await File.WriteAllBytesAsync(c, upstream.CopyC());
            await File.WriteAllBytesAsync(header, upstream.CopyHeader());
            File.Copy(sourceArchive, Path.Combine(sourceDirectory, "sqlite-amalgamation-3530400.zip"));
            Native(c);
            Native(header);
            await Run("restore", "dotnet", ["restore", project, "-r", "linux-musl-x64", "--nologo", "-p:PublishAot=true", "-p:SqliteNativeArchive=" + a], TimeSpan.FromMinutes(10));
            string assetsPath = Path.Combine(app, "obj/project.assets.json");
            var assets = held.Open(root, assetsPath, "native candidate assets", "sqlite_native_assets", 32 * 1024 * 1024, singleLink: true);
            byte[] assetBytes = assets.ReadBytes(32 * 1024 * 1024, "sqlite_native_assets");
            var dependencies = InspectDependencyValues(assetBytes);
            await File.WriteAllBytesAsync(Path.Combine(destination, "assets.json"), assetBytes);
            using (var document = JsonDocument.Parse(assetBytes))
            {
                foreach (var pin in PackagePins)
                {
                    string relative = pin.Key.ToLowerInvariant() + "/" + pin.Value + "/" + pin.Key.ToLowerInvariant() + "." + pin.Value + ".nupkg";
                    string? package = document.RootElement.GetProperty("packageFolders").EnumerateObject().Select(p => Path.Combine(p.Name, relative)).FirstOrDefault(File.Exists);
                    Need(package is not null, "package_archive_missing");
                    var file = held.Open(Path.GetPathRoot(package!)!, package!, "signed package", "sqlite_native_package", 64 * 1024 * 1024, singleLink: true);
                    Need(file.Sha256.Equals(PackageArchiveSha256[pin.Key], StringComparison.OrdinalIgnoreCase), "package_archive_hash");
                    Native(package!);
                    string retained = Path.Combine(destination, "packages", Path.GetFileName(package!));
                    Directory.CreateDirectory(Path.GetDirectoryName(retained)!);
                    File.Copy(package!, retained);
                    if (pin.Key == "SQLitePCLRaw.provider.e_sqlite3")
                        imports = InspectProviderImports(file.ReadBytes(1024 * 1024, "sqlite_native_provider"));
                }

                foreach (var input in NativeAotPayload.CapturePackageInputs(document.RootElement))
                {
                    Need(!native.TryGetValue(input.Key, out var old) || old == input.Value, "native_input_changed");
                    native[input.Key] = input.Value;
                }
            }

            Need(imports is not null, "provider_import_missing");
            if (dependencyInputs is not null)
            {
                Dictionary<string, byte[]> values = new(StringComparer.Ordinal);
                foreach (string relative in DependencyArtifacts)
                    values.Add(relative, dependencyInputs[relative].ReadBytes(16 * 1024 * 1024, "sqlite_archive_dependency"));
                foreach (var pair in new[]
                {
                    ("upstream/sqlite3.c", SourceCsha256),
                    ("upstream/sqlite3.h", SourceHeaderSha256),
                    ("upstream/sqlite-amalgamation-3530400.zip", SourceArchiveSha256)
                }

                )
                    Need(dependencyInputs[pair.Item1].Sha256.Equals(pair.Item2, StringComparison.OrdinalIgnoreCase), "dependency_source");
                byte[] receiptBytes = dependencyInputs["receipt.json"].ReadBytes(2 * 1024 * 1024, "sqlite_archive_dependency");
                dependency = InspectArchiveDependencyValues(receiptBytes, ArchiveDependencyReceiptSha256, values, imports!, compilerInputs);
                foreach (var pair in values)
                {
                    string path = Path.Combine(destination, pair.Key == "commands.json" ? "dependency/commands.json" : pair.Key);
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    await File.WriteAllBytesAsync(path, pair.Value);
                    if (pair.Key != "commands.json")
                        Native(path);
                }

                await File.WriteAllBytesAsync(Path.Combine(destination, "dependency/receipt.json"), receiptBytes);
                archive = dependency.Archive;
            }
            else
                for (int round = 0; round < 2; round++)
                {
                    string roundDirectory = Path.Combine(destination, "round" + round);
                    Directory.CreateDirectory(roundDirectory);
                    string obj = Path.Combine(roundDirectory, "sqlite3.o"), lib = round == 0 ? a : Path.Combine(destination, "libe_sqlite3-repeat.a");
                    await Run("compile-r" + round, "gcc", StaticCompileArguments(c, obj, imports!), TimeSpan.FromMinutes(10));
                    freshCompilesCompleted++;
                    await Run("archive-r" + round, "ar", ["crD", lib, obj]);
                    Native(obj);
                    Native(lib);
                }

            Need(NativeAotWebPayload.HashFile(a) == NativeAotWebPayload.HashFile(Path.Combine(destination, "libe_sqlite3-repeat.a")), "archive_reproducibility");
            archive = InspectStaticArchiveValues(await File.ReadAllBytesAsync(a));
            metadataWithoutArchive = imports!.Where(n => !archive.DefinedSqliteSymbols.Contains(n, StringComparer.Ordinal)).ToArray();
            await Run("archive-defined", "nm", ["-g", "--defined-only", a]);
            await Run("archive-undefined", "nm", ["-u", a]);
            string publish = Path.Combine(destination, Application);
            await Run("publish", "dotnet", StaticPublishArguments(recipeCommands, publish, a), TimeSpan.FromMinutes(45));
            string executable = Path.Combine(publish, Application);
            var executableInput = held.Open(root, executable, "static native executable", "sqlite_native_executable", NativeAotPayload.MaximumImageBytes, singleLink: true);
            image = NativeAotPayload.Inspect(executableInput.ReadBytes(NativeAotPayload.MaximumImageBytes, "sqlite_native_executable"));
            Native(executable);
            await Run("elf", "readelf", ["-h", "-l", "-d", "-n", "-W", executable]);
            await Run("symbols", "nm", ["-a", executable]);
            weakUndefined = InspectFinalUndefinedValues(await Run("final-undefined", "nm", ["-u", "-P", executable]));
            string relocations = await Run("final-relocations", "readelf", ["-W", "-r", executable]);
            Need(!relocations.Contains("sqlite3", StringComparison.Ordinal), "final_sqlite_relocation");
            string intermediate = Path.Combine(app, "obj/Release/net11.0/linux-musl-x64");
            retainedImports = InspectRetainedImportValues(await Run("managed-object-imports", "nm", ["-u", Path.Combine(intermediate, "native", Application + ".o")]), imports!);
            RequireProviderSymbols(archive, retainedImports);
            await Run("managed-object-symbols", "readelf", ["-W", "-s", Path.Combine(intermediate, "native", Application + ".o")]);
            foreach (string input in new[]
            {
                Path.Combine(intermediate, Application + ".dll"),
                Path.Combine(intermediate, "native", Application + ".o")
            }

            )
            {
                Native(input);
                string copy = Path.Combine(destination, "originals", Path.GetFileName(input));
                Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
                File.Copy(input, copy);
            }

            bool direct = false;
            foreach (string rsp in Directory.GetFiles(Path.Combine(app, "obj"), "*.rsp", SearchOption.AllDirectories))
            {
                if ((await File.ReadAllTextAsync(rsp)).Contains("--directpinvoke:e_sqlite3", StringComparison.Ordinal))
                    direct = true;
                Native(rsp);
                string copy = Path.Combine(destination, "responses", Path.GetRelativePath(Path.Combine(app, "obj"), rsp));
                Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
                File.Copy(rsp, copy);
            }

            Need(direct, "direct_pinvoke_setting");
            string map = Path.Combine(intermediate, "native", Application + ".map.xml");
            Need(File.Exists(map), "ilc_map_missing");
            Native(map);
            File.Copy(map, Path.Combine(destination, "originals", Application + ".map.xml"));
            string database = Path.Combine(destination, "contract.db");
            string result = await Run("reference", "strace", LinuxReferenceTraceArguments(executable, database, false, Path.Combine(destination, "sqlite-trace")), TimeSpan.FromMinutes(2));
            transcript = InspectTranscriptValues(result);
            Need(transcript.SourceSha256 == Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(SourceId))), "engine_source_id");
            string[] traces = Directory.GetFiles(destination, "sqlite-trace.*");
            Need(traces.Length is > 0 and <= 64, "trace_inventory");
            Dictionary<string, string> traceValues = new(StringComparer.Ordinal);
            foreach (string file in traces)
            {
                string suffix = file[(file.LastIndexOf('.') + 1)..];
                Need(int.TryParse(suffix, out int pid) && pid > 0 && new FileInfo(file).Length is > 0 and <= 4 * 1024 * 1024, "trace_identity");
                string text = await File.ReadAllTextAsync(file);
                Need(text.TrimEnd('\n', '\r').EndsWith("+++ exited with 0 +++", StringComparison.Ordinal) && !text.Contains("+++ killed by", StringComparison.Ordinal), "trace_process_exit");
                traceValues.Add(Path.GetFileName(file), text);
                Native(file);
            }

            trace = InspectTraceValues(traceValues, database);
            Need(trace.Calls.Any(c => c.Syscall == "execve") && trace.Calls.Any(c => c.Syscall == "exit_group") && trace.Calls.Any(c => c.MentionsDatabase && c.Syscall is "fsync" or "fdatasync") && trace.Calls.Any(c => c.MentionsDatabase && c.Syscall == "fcntl") && trace.Calls.Any(c => c.MentionsDatabase && c.Syscall is "unlink" or "unlinkat"), "trace_storage_closure");
            Native(database);
            var actualDatabase = held.Open(root, database, "committed database across independent verify", "sqlite_verify_database", 1024 * 1024, singleLink: true);
            result = await Run("verify", "strace", LinuxReferenceTraceArguments(executable, database, true, Path.Combine(destination, "sqlite-verify-trace")), TimeSpan.FromMinutes(2));
            verification = InspectVerificationTranscriptValues(result);
            string[] verifyTraces = Directory.GetFiles(destination, "sqlite-verify-trace.*");
            Need(verifyTraces.Length is > 0 and <= 64, "verify_trace_inventory");
            Dictionary<string, string> verifyTraceValues = new(StringComparer.Ordinal);
            var writerPids = traces.Select(file => file[(file.LastIndexOf('.') + 1)..]).ToHashSet(StringComparer.Ordinal);
            foreach (string file in verifyTraces)
            {
                string suffix = file[(file.LastIndexOf('.') + 1)..];
                Need(int.TryParse(suffix, out int pid) && pid > 0 && !writerPids.Contains(suffix) && new FileInfo(file).Length is > 0 and <= 4 * 1024 * 1024, "verify_trace_identity");
                string text = await File.ReadAllTextAsync(file);
                Need(text.TrimEnd('\n', '\r').EndsWith("+++ exited with 0 +++", StringComparison.Ordinal) && !text.Contains("+++ killed by", StringComparison.Ordinal), "verify_trace_process_exit");
                verifyTraceValues.Add(Path.GetFileName(file), text);
                Native(file);
            }

            verificationTrace = InspectTraceValues(verifyTraceValues, database);
            Need(verificationTrace.Calls.Any(c => c.Syscall == "execve") && verificationTrace.Calls.Any(c => c.Syscall == "exit_group") && verificationTrace.Calls.Any(c => c.MentionsDatabase && c.Syscall == "pread64"), "verify_trace_read_closure");
            actualDatabase.VerifyUnchanged();
            Need(SourcePaths().SequenceEqual(sources.Keys) && sources.All(p => NativeAotWebPayload.HashFile(p.Key) == p.Value) && native.All(p => NativeAotWebPayload.HashFile(p.Key) == p.Value) && ToolingBuildInputs.Validate(root).Count == 0, "linux_inputs_changed");
            held.VerifyUnchanged();
            await File.WriteAllTextAsync(Path.Combine(destination, "dependencies.json"), JsonSerializer.Serialize(dependencies));
            await File.WriteAllTextAsync(Path.Combine(destination, "trace-values.json"), JsonSerializer.Serialize(trace));
            await File.WriteAllTextAsync(Path.Combine(destination, "verify-trace-values.json"), JsonSerializer.Serialize(verificationTrace));
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
            await File.WriteAllTextAsync(Path.Combine(destination, "commands.json"), JsonSerializer.Serialize(commands));
            await File.WriteAllTextAsync(Path.Combine(destination, "sources.json"), JsonSerializer.Serialize(sources));
            await File.WriteAllTextAsync(Path.Combine(destination, "native-inputs.json"), JsonSerializer.Serialize(native));
            var files = Directory.GetFiles(destination, "*", SearchOption.AllDirectories).Where(p => Path.GetFileName(p) != "qualification.lock").Order(StringComparer.Ordinal).ToDictionary(p => Path.GetRelativePath(destination, p), NativeAotWebPayload.HashFile);
            await File.WriteAllTextAsync(Path.Combine(destination, "receipt.json"), JsonSerializer.Serialize(new
            {
                Schema = "clros-sqlite-linux-reference-v2",
                Succeeded = succeeded,
                Failure = failure,
                Archive = archive,
                ArchiveBuild = dependency is not null ? "reused_verified_dependency" : archiveDependencyRoot is not null ? "dependency_not_validated" : freshCompilesCompleted == 2 ? "fresh_two_compiles" : "fresh_incomplete",
                FreshCompilesCompleted = freshCompilesCompleted,
                ArchiveDependencyRoot = archiveDependencyRoot,
                ArchiveDependency = dependency,
                Imports = imports,
                RetainedImports = retainedImports,
                MetadataWithoutArchive = metadataWithoutArchive,
                WeakUndefined = weakUndefined,
                Image = image,
                Transcript = transcript,
                Verification = verification,
                TraceUnjoinedLines = trace?.UnjoinedLines,
                VerificationTraceUnjoinedLines = verificationTrace?.UnjoinedLines,
                TraceUnreturnedCalls = trace?.UnreturnedCalls,
                VerificationTraceUnreturnedCalls = verificationTrace?.UnreturnedCalls,
                TraceTerminalUnreturnedCalls = trace?.TerminalUnreturnedCalls,
                VerificationTraceTerminalUnreturnedCalls = verificationTrace?.TerminalUnreturnedCalls,
                SourceArchiveSha256,
                SourceArchiveSha3,
                SourceId,
                Sources = sources,
                Native = native,
                Outputs = files,
                GuestQualified = false,
                DurabilityQualified = false,
                RecoveryQualified = false,
                CompleteTraceProjection = trace?.CompleteTraceProjection == true && verificationTrace?.CompleteTraceProjection == true
            }));
        }

        output.WriteLine("TEST_PASS " + marker);
        return 0;
    }

}
