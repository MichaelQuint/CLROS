using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace OS.Tooling;
/// <summary>Separate host-only Linux qualification for the focused upstream
/// threading workloads. Owns fresh app caches, output files and Linux child
/// sessions under one repository lease. Original payload producers remain
/// unchanged. Publication/output/trace receipts grant no CLROS guest authority.</summary>

public static partial class NativeAotThreadingPayload
{
    public static IReadOnlyList<string> Applications { get; } = Array.AsReadOnly<string>(["NativeAotSuspensionSmoke", "NativeAotThreadPoolSmoke"]);

    private static readonly UTF8Encoding Utf8 = new(false, true);
    /// <summary>Fixed static/non-PIE musl publication arguments. Unknown app
    /// names fail before paths/children are used. The returned list is fresh and
    /// immutable; it grants no producer or execution authority.</summary>

    public static IReadOnlyList<string> PublishArguments(string app, string destination)
    {
        Need(Applications.Contains(app, StringComparer.Ordinal), "app");
        return Array.AsReadOnly<string>(["publish", $"apps/{app}/{app}.csproj", "-c", "Release", "-f", NativeAotPayload.TargetFramework, "-r", "linux-musl-x64", "--self-contained", "true", "-o", Path.GetFullPath(destination), "--nologo", "-p:PublishAot=true", "-p:StaticExecutable=true", "-p:PositionIndependentExecutable=false", "-p:InvariantGlobalization=true", "-p:ServerGarbageCollection=false", "-p:EventSourceSupport=false", "-p:EnableDiagnostics=false", "-p:DebuggerSupport=false", "-p:RuntimeFrameworkVersion=" + NativeAotPayload.RuntimeVersion, "-p:TargetLatestRuntimePatch=false", "-p:UseSharedCompilation=false", "-nr:false"]);
    }

    /// <summary>Preserve only the selected focused app's bin/obj trees under
    /// fresh output. Caller must hold OpenQualificationLease. Every source and
    /// destination is bounded and checked for links before any move; overlap,
    /// preexisting archives and cross-filesystem moves fail without deletion.</summary>

    public static void ArchiveBuildCache(string root, string app, string destination)
    {
        Need(Applications.Contains(app, StringComparer.Ordinal), "app");
        root = Path.GetFullPath(root);
        destination = Path.GetFullPath(destination);
        string appRoot = Path.Combine(root, "apps", app), archive = Path.Combine(destination, "retained-build-cache", app);
        AuthorityFiles.RequirePlainPath(root, appRoot, "threading_cache_path");
        AuthorityFiles.RequirePlainPath(destination, destination, "threading_cache_path");
        string relative = Path.GetRelativePath(Path.Combine(root, "apps"), destination);
        Need(relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) || Path.IsPathRooted(relative), "cache_output_overlap");
        EfiBuildFiles.RequirePlainOrMissing(destination, archive, "threading_cache_path");
        Need(!Path.Exists(archive), "cache_archive_exists");
        string[] sources = [Path.Combine(appRoot, "bin"), Path.Combine(appRoot, "obj")];
        foreach (string source in sources)
        {
            EfiBuildFiles.RequirePlainOrMissing(appRoot, source, "threading_cache_path");
            Need(!File.Exists(source), "cache_not_directory");
            if (!Directory.Exists(source))
                continue;
            Stack<string> pending = new();
            pending.Push(source);
            while (pending.TryPop(out string? directory))
                foreach (string item in Directory.EnumerateFileSystemEntries(directory))
                {
                    FileAttributes attributes = File.GetAttributes(item);
                    Need((attributes & FileAttributes.ReparsePoint) == 0, "cache_reparse_point");
                    if ((attributes & FileAttributes.Directory) != 0)
                        pending.Push(item);
                }
        }

        EfiBuildFiles.InitializeDirectory(archive, "threading_cache_path");
        foreach (string source in sources)
            if (Directory.Exists(source))
                Directory.Move(source, Path.Combine(archive, Path.GetFileName(source)));
        Need(sources.All(source => !Path.Exists(source)), "cache_still_present");
    }

    /// <summary>Read bounded per-thread strace files from a supplied directory.
    /// Requires complete zero exits plus observed execve/exit_group, records all
    /// exact file hashes and syscall names, and rejects malformed UTF8 or fatal
    /// traces. This is observed coverage, never an exhaustive syscall contract.</summary>

    public static TraceInventory ReadTraceInventory(string directory, string app)
    {
        Need(Applications.Contains(app, StringComparer.Ordinal), "app");
        string prefix = app + "-trace.";
        string[] files = Directory.GetFiles(directory, prefix + "*").Order(StringComparer.Ordinal).ToArray();
        Need(files.Length is > 0 and <= 64, "trace_inventory");
        Dictionary<string, string> hashes = new(StringComparer.Ordinal);
        SortedSet<string> calls = new(StringComparer.Ordinal);
        long total = 0;
        foreach (string file in files)
        {
            string name = Path.GetFileName(file), pid = name[prefix.Length..];
            Need(int.TryParse(pid, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int id) && id > 0 && id.ToString(System.Globalization.CultureInfo.InvariantCulture) == pid, "trace_identity");
            AuthorityFiles.RequirePlainPath(directory, file, "threading_trace_path");
            long bytes = new FileInfo(file).Length;
            total = checked(total + bytes);
            Need(bytes is > 0 and <= 4 * 1024 * 1024 && total <= 16 * 1024 * 1024, "trace_size");
            byte[] data = File.ReadAllBytes(file);
            Need(data.LongLength == bytes, "trace_changed");
            string text = Utf8.GetString(data);
            Need(text.TrimEnd('\r', '\n').EndsWith("+++ exited with 0 +++", StringComparison.Ordinal) && !text.Contains("+++ killed by", StringComparison.Ordinal), "trace_exit");
            foreach (string line in text.Split('\n'))
            {
                Match match = Regex.Match(line, "^([a-zA-Z0-9_]+)\\(", RegexOptions.CultureInvariant);
                if (match.Success)
                    calls.Add(match.Groups[1].Value);
                Need(!Regex.IsMatch(line, "^\\+\\+\\+ exited with (?!0 \\+\\+\\+)[0-9]+", RegexOptions.CultureInvariant), "trace_exit");
            }

            hashes.Add(name, Convert.ToHexString(SHA256.HashData(data)));
        }

        Need(calls.Contains("execve") && calls.Contains("exit_group"), "trace_incomplete");
        return new(hashes.AsReadOnly(), Array.AsReadOnly(calls.ToArray()));
    }

    public static async Task<int> RunAsync(string root, string[] args, TextWriter output)
    {
        const string marker = "nativeaot.threading-payload";
        output.WriteLine("TEST_START " + marker);
        try
        {
            if (args is not ["qualify", "--output", var destination])
                throw new ArgumentException("nativeaot threading qualify --output <fresh-directory>");
            await QualifyAsync(Path.GetFullPath(root), Path.GetFullPath(destination), output);
            output.WriteLine("TEST_PASS " + marker);
            return 0;
        }
        catch (Exception error)
        {
            output.WriteLine("TEST_FAIL " + marker + " " + error.Message.ReplaceLineEndings(" "));
            return 1;
        }
    }

    private static async Task QualifyAsync(string root, string destination, TextWriter output)
    {
        Need(OperatingSystem.IsLinux(), "linux_builder_required");
        using var lease = NativeAotPayload.OpenQualificationLease(root);
        string outputRelative = Path.GetRelativePath(Path.Combine(root, "apps"), destination);
        Need(outputRelative == ".." || outputRelative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) || Path.IsPathRooted(outputRelative), "cache_output_overlap");
        Need(!Path.Exists(destination), "fresh_output_required");
        EfiBuildFiles.InitializeDirectory(destination, "threading_output_path");
        using FileStream ownership = new(Path.Combine(destination, "qualification.lock"), FileMode.CreateNew, FileAccess.Write, FileShare.None);
        Need(ToolingBuildInputs.Validate(root).Count == 0, "tooling_inputs_changed");
        string[] SourcePaths() => Applications.SelectMany(app => Directory.EnumerateFiles(Path.Combine(root, "apps", app), "*", SearchOption.AllDirectories)).Where(path => !Path.GetRelativePath(root, path).Split(Path.DirectorySeparatorChar).Any(p => p is "bin" or "obj" or "artifacts")).Concat(ToolingBuildInputs.SourceInputs(root)).Concat(new[] { "NuGet.Config", "nuget.config" }.Select(p => Path.Combine(root, p)).Where(File.Exists)).Select(p => Path.GetRelativePath(root, p).Replace('\\', '/')).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        string[] paths = SourcePaths();
        Dictionary<string, string> sources = paths.ToDictionary(p => p, p => Hash(Path.Combine(root, p)), StringComparer.Ordinal);
        Dictionary<string, string> native = new(StringComparer.Ordinal);
        List<Command> commands = [];
        List<App> apps = [];
        string outcome = "failed";
        void Input(string path)
        {
            path = File.ResolveLinkTarget(path, true)?.FullName ?? Path.GetFullPath(path);
            string hash = Hash(path);
            Need(!native.TryGetValue(path, out string? prior) || prior == hash, "native_inputs_changed");
            native[path] = hash;
        }

        async Task<string> Run(string name, string executable, string[] arguments, TimeSpan? timeout = null)
        {
            string log = Path.Combine(destination, name);
            int index = commands.Count;
            commands.Add(new(executable, arguments, name, -1));
            int code = await ExecuteAsync(executable, arguments, root, log, timeout ?? TimeSpan.FromMinutes(2));
            commands[index] = new(executable, arguments, name, code);
            Need(code == 0, "process_exit_" + name + "_" + code);
            return Utf8.GetString(await File.ReadAllBytesAsync(log + ".stdout.log"));
        }

        try
        {
            Need((await Run("sdk-version", "dotnet", ["--version"])).Trim() == NativeAotPayload.SdkVersion, "sdk_pin_mismatch");
            await Run("dotnet-info", "dotnet", ["--info"]);
            await Run("kernel", "uname", ["-a"]);
            await Run("packages", "apk", ["info", "-vv"]);
            await Run("cpu", "cat", ["/proc/cpuinfo"]);
            string dotnet = "";
            foreach (string name in new[]
            {
                "dotnet",
                "clang",
                "ld",
                "gcc",
                "strace",
                "readelf",
                "nm",
                "setsid"
            }

            )
            {
                string? tool = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator).Select(p => Path.Combine(p, name)).FirstOrDefault(File.Exists);
                Need(tool is not null, "native_tool_missing_" + name);
                Input(tool!);
                if (name == "dotnet")
                    dotnet = File.ResolveLinkTarget(tool!, true)?.FullName ?? Path.GetFullPath(tool!);
                if (name is "clang" or "ld" or "gcc" or "strace")
                    await Run("version-" + name, name, ["--version"]);
            }

            string sdk = Path.Combine(Path.GetDirectoryName(dotnet)!, "sdk", NativeAotPayload.SdkVersion);
            Need(Directory.Exists(sdk), "sdk_directory");
            foreach (string file in Directory.EnumerateFiles(sdk, "*", SearchOption.AllDirectories))
                Input(file);
            foreach (string library in new[]
            {
                "libc.a",
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
                string path = (await Run("library-" + library, "gcc", ["-print-file-name=" + library])).Trim();
                Need(Path.IsPathFullyQualified(path) && File.Exists(path), "static_dependency_missing_" + library);
                Input(path);
            }

            foreach (string app in Applications)
            {
                Need(paths.All(p => Hash(Path.Combine(root, p)) == sources[p]), "source_inputs_changed");
                ArchiveBuildCache(root, app, destination);
                output.WriteLine("NATIVEAOT_THREADING_PUBLISH " + app);
                await output.FlushAsync();
                string publish = Path.Combine(destination, app);
                await Run(app + "-publish", "dotnet", PublishArguments(app, publish).ToArray(), TimeSpan.FromMinutes(45));
                string intermediate = Path.Combine(root, "apps", app, "obj", "Release", NativeAotPayload.TargetFramework, "linux-musl-x64");
                foreach (string file in new[]
                {
                    Path.Combine(intermediate, app + ".dll"),
                    Path.Combine(intermediate, "native", app + ".o")
                }

                )
                {
                    AuthorityFiles.RequirePlainPath(root, file, "threading_produced_input_path");
                    Need(new FileInfo(file).Length > 0, "produced_input_empty");
                    Input(file);
                }

                string assets = Path.Combine(root, "apps", app, "obj", "project.assets.json");
                File.Copy(assets, Path.Combine(destination, app + "-assets.json"));
                using (JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(assets)))
                    foreach (var input in NativeAotPayload.CapturePackageInputs(document.RootElement))
                    {
                        Need(!native.TryGetValue(input.Key, out string? prior) || prior == input.Value, "native_inputs_changed");
                        native[input.Key] = input.Value;
                    }

                int response = 0;
                foreach (string file in Directory.EnumerateFiles(Path.Combine(root, "apps", app, "obj"), "*.rsp", SearchOption.AllDirectories))
                {
                    Input(file);
                    File.Copy(file, Path.Combine(destination, app + "-response-" + (++response) + ".rsp"));
                }

                Need(response > 0, "compiler_response_missing");
                string executable = Path.Combine(publish, app);
                byte[] image = File.ReadAllBytes(executable);
                var inspection = NativeAotPayload.Inspect(image);
                Input(executable);
                await Run(app + "-elf", "readelf", ["-h", "-l", "-d", "-n", "-W", executable]);
                await Run(app + "-symbols", "nm", ["-a", executable]);
                string text = await Run(app + "-run", "strace", ["-ff", "-s", "256", "-o", Path.Combine(destination, app + "-trace"), executable]);
                if (app == "NativeAotSuspensionSmoke")
                    NativeAotThreadingGuest.ValidateSuspensionOutput(text);
                else
                    NativeAotThreadingGuest.ValidateThreadPoolOutput(text);
                TraceInventory trace = ReadTraceInventory(destination, app);
                apps.Add(new(app, inspection, trace));
                output.WriteLine("NATIVEAOT_THREADING_EXECUTED " + app + " sha256=" + inspection.Sha256 + " threads=" + trace.Files.Count);
                await output.FlushAsync();
            }

            Need(SourcePaths().SequenceEqual(paths) && ToolingBuildInputs.Validate(root).Count == 0 && paths.All(p => Hash(Path.Combine(root, p)) == sources[p]), "source_inputs_changed");
            Need(native.All(p => Hash(p.Key) == p.Value), "native_inputs_changed");
            outcome = "passed";
        }
        finally
        {
            using FileStream file = new(Path.Combine(destination, "receipt.json"), FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            using Utf8JsonWriter writer = new(file, new()
            {
                Indented = true
            });
            writer.WriteStartObject();
            writer.WriteNumber("schema", 1);
            writer.WriteString("outcome", outcome);
            writer.WriteString("boundary", "Linux host only; no CLROS guest authority");
            writer.WriteString("sdk", NativeAotPayload.SdkVersion);
            writer.WriteString("runtime", NativeAotPayload.RuntimeVersion);
            void Map(string key, IEnumerable<KeyValuePair<string, string>> rows)
            {
                writer.WriteStartObject(key);
                foreach (var row in rows.OrderBy(p => p.Key, StringComparer.Ordinal))
                    writer.WriteString(row.Key, row.Value);
                writer.WriteEndObject();
            }

            Map("sources", sources);
            Map("nativeInputs", native);
            writer.WriteStartArray("commands");
            foreach (var command in commands)
            {
                writer.WriteStartObject();
                writer.WriteString("executable", command.Executable);
                writer.WriteStartArray("arguments");
                foreach (string argument in command.Arguments)
                    writer.WriteStringValue(argument);
                writer.WriteEndArray();
                writer.WriteString("log", command.Log);
                writer.WriteNumber("exitCode", command.ExitCode);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteStartArray("applications");
            foreach (var app in apps)
            {
                writer.WriteStartObject();
                writer.WriteString("name", app.Name);
                writer.WriteString("sha256", app.Image.Sha256);
                writer.WriteNumber("entry", app.Image.Entry);
                writer.WriteNumber("programHeaders", app.Image.ProgramHeaderCount);
                writer.WriteStartArray("loads");
                foreach (var load in app.Image.Loads)
                {
                    writer.WriteStartObject();
                    writer.WriteNumber("flags", load.Flags);
                    writer.WriteNumber("offset", load.Offset);
                    writer.WriteNumber("address", load.Address);
                    writer.WriteNumber("fileBytes", load.FileSize);
                    writer.WriteNumber("memoryBytes", load.MemorySize);
                    writer.WriteNumber("alignment", load.Alignment);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                Map("traceFiles", app.Trace.Files);
                writer.WriteStartArray("observedSyscalls");
                foreach (string call in app.Trace.Syscalls)
                    writer.WriteStringValue(call);
                writer.WriteEndArray();
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }
    }

    // The original producer is source-pinned by Hello admission. Keep its source
    // unchanged; this private session scope follows its bounded Linux ownership
    // rule instead of using ProcessRunner's unconfirmed Linux tree result.
    private static async Task<int> ExecuteAsync(string executable, string[] arguments, string root, string log, TimeSpan timeout)
    {
        ProcessStartInfo start = new("setsid")
        {
            WorkingDirectory = root,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        start.ArgumentList.Add(executable);
        foreach (string argument in arguments)
            start.ArgumentList.Add(argument);
        foreach (var setting in new Dictionary<string, string>
        {
            {
                "DOTNET_CLI_TELEMETRY_OPTOUT",
                "1"
            },
            {
                "DOTNET_NOLOGO",
                "1"
            },
            {
                "DOTNET_SYSTEM_GLOBALIZATION_INVARIANT",
                "1"
            },
            {
                "DOTNET_CLI_USE_MSBUILD_SERVER",
                "0"
            },
            {
                "MSBUILDDISABLENODEREUSE",
                "1"
            },
            {
                "DOTNET_CLI_UI_LANGUAGE",
                "en"
            }
        }

        )
            start.Environment[setting.Key] = setting.Value;
        using Process process = Process.Start(start) ?? throw new IOException("threading_process_start");
        using CancellationTokenSource deadline = new(timeout);
        Task stdout = Capture(process.StandardOutput.BaseStream, log + ".stdout.log"), stderr = Capture(process.StandardError.BaseStream, log + ".stderr.log");
        async Task Capture(Stream input, string path)
        {
            try
            {
                await using FileStream file = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                byte[] bytes = new byte[4096];
                int total = 0;
                while (true)
                {
                    int read = await input.ReadAsync(bytes);
                    if (read == 0)
                        return;
                    total = checked(total + read);
                    Need(total <= 4 * 1024 * 1024, "process_output_limit");
                    await file.WriteAsync(bytes.AsMemory(0, read));
                }
            }
            catch
            {
                deadline.Cancel();
                _ = Kill(-process.Id, 9);
                try
                {
                    if (!process.HasExited)
                        process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException) when (process.HasExited)
                {
                }

                throw;
            }
        }

        try
        {
            await Task.WhenAll(process.WaitForExitAsync(deadline.Token), stdout, stderr).WaitAsync(deadline.Token);
            return process.ExitCode;
        }
        finally
        {
            int killed = Kill(-process.Id, 9);
            Need(killed == 0 || Marshal.GetLastPInvokeError() == 3, "session_cleanup");
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            Stopwatch time = Stopwatch.StartNew();
            while (Kill(-process.Id, 0) == 0)
            {
                Need(time.Elapsed < TimeSpan.FromSeconds(10), "session_still_live");
                await Task.Delay(10);
            }

            Need(Marshal.GetLastPInvokeError() == 3, "session_confirmation");
            await Task.WhenAll(stdout, stderr).WaitAsync(TimeSpan.FromSeconds(10));
        }
    }

    [DllImport("libc", EntryPoint = "kill", SetLastError = true)]
    private static extern int Kill(int pid, int signal);
    private static string Hash(string path)
    {
        using FileStream file = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(file));
    }

    private static void Need(bool condition, string reason)
    {
        if (!condition)
            throw new InvalidDataException("nativeaot_threading_" + reason);
    }
}
