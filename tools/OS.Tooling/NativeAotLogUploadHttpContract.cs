using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OS.Tooling;
/// <summary>Finite HTTP/FileStream reference owners. Actual loopback
/// responses, joined managed children and copied files are separate from Linux
/// NativeAOT, CLROS, device FLUSH, cold reboot and physical persistence.</summary>

public static partial class NativeAotLogUploadHttpContract
{
    public const string Application = "MinimalApiLogUploadContract";
    public const int StartupLogBytes = 330, MaximumTranscriptBytes = 8192;
    public const string Policy = "LOG_UPLOAD_HTTP_POLICY serve=once bodyBytes=5126 logRows=6+6 chunkBytes=4094+1032 flushRequested=true\n";
    public const string Ready = "LOG_UPLOAD_HTTP_READY startupLogBytes=330 uploadBytes=0\n";
    private static readonly byte[] Guard = [0x43, 0x4c, 0x52, 0x4f, 0x53, 0, 0xff, 0x31];
    private static readonly NativeAotSocketFixturePublisher Publisher = new(Application, "log-upload-http", [("MinimalApiLogUploadContract.csproj", "98B384443EFE8CAD957E71BAA63A53B9D06ABDDCE31A02CC6B2C04B7FF9A0DC5"), ("Program.cs", "1B10C6E6578E104845074BF716BD7D34F52F36FB1F3B47481C24FDB653ACE043")]);
    private static readonly string[] LinuxToolNames = ["dotnet", "clang", "ld", "gcc", "readelf", "nm", "strace", "setsid"];
    private static readonly string[] LinuxLibraryNames = ["libc.a", "libm.a", "libz.a", "libstdc++.a", "libgcc.a", "libgcc_eh.a", "crt1.o", "crti.o", "crtn.o"];
    /// <summary>Pure fixed recipe substitution. Only the app and output differ
    /// from the original socket static/non-PIE musl command; no publication runs.</summary>

    public static string[] PublishArguments(string originalCommands, string destination) => Publisher.PublishArguments(originalCommands, destination);
    /// <summary>Require the exact two30cf app sources and plain inventory.
    /// Fresh native publication additionally refuses any existing app cache.</summary>

    public static void RequireCurrentSources(string root, bool fresh = false) => Publisher.RequireCurrentSources(root, fresh);
    /// <summary>Copied literal Linux argv, not a shell command. This runs the
    /// finite HTTP writer with the existing unfiltered trace grammar. A plan
    /// cannot authenticate executables, paths, processes or a trace.</summary>

    public static string[] LinuxTraceArguments(string executable, string data, string tracePrefix, int port)
    {
        Need(port is> 0 and <= 65535, "linux_port");
        Need(new[] { executable, data, tracePrefix }.All(CanonicalLinux), "linux_path");
        // Reuse the unchanged canonical Linux path checks and trace flags.
        string[] prior = NativeAotLogUploadContract.LinuxTraceArguments(executable, data, tracePrefix, "write");
        return[..prior[..8], "--serve-once", "--root", data, "--urls", "http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture)];
    }

    /// <summary>Select exactly eight copied current-tool identities by their
    /// resolved canonical Linux paths in an original native map. Historical
    /// producer/cache rows remain receipt values; no path is opened here and
    /// no basename/relocation fallback exists. The caller must independently
    /// authenticate the sealed socket package and hold/check actual tools.
    /// Borrowed inputs are not retained; returned facts grant no authority.</summary>

    public static ReadOnlyCollection<LinuxToolValue> InspectLinuxRecipeTools(string originalNativeJson, IReadOnlyDictionary<string, LinuxToolValue> resolvedTools)
    {
        Need(originalNativeJson.Length <= 16 * 1024 * 1024 && Encoding.UTF8.GetByteCount(originalNativeJson) <= 16 * 1024 * 1024, "recipe_tool_capacity");
        Need(resolvedTools.Count == LinuxToolNames.Length && LinuxToolNames.All(resolvedTools.ContainsKey) && resolvedTools.All(row => row.Value is not null && row.Key == row.Value.Name && CanonicalLinux(row.Value.Path) && IsHash(row.Value.Sha256)) && resolvedTools.Values.Select(tool => tool.Path).Distinct(StringComparer.Ordinal).Count() == LinuxToolNames.Length, "recipe_tool_paths");
        using var document = JsonDocument.Parse(originalNativeJson);
        Need(document.RootElement.ValueKind == JsonValueKind.Object, "recipe_native_map");
        var rows = document.RootElement.EnumerateObject().ToArray();
        Need(rows.Length is> 0 and <= 16384 && rows.Select(row => row.Name).Distinct(StringComparer.Ordinal).Count() == rows.Length && rows.All(row => row.Value.ValueKind == JsonValueKind.String && IsHash(row.Value.GetString())), "recipe_native_map");
        var native = rows.ToDictionary(row => row.Name, row => row.Value.GetString()!, StringComparer.Ordinal);
        List<LinuxToolValue> selected = [];
        foreach (string name in LinuxToolNames)
        {
            var actual = resolvedTools[name];
            string path = actual.Path;
            Need(native.TryGetValue(path, out string? hash) && actual.Sha256.Equals(hash, StringComparison.OrdinalIgnoreCase), "recipe_tool_identity");
            selected.Add(new(name, path, hash!.ToUpperInvariant()));
        }

        return selected.AsReadOnly();
    }

    /// <summary>Check copied original-map/current-role identities and construct
    /// the one supported trace invocation. This deliberately supports only the
    /// original pinned BusyBox setsid applet, not arbitrary multicall binaries,
    /// aliases or standalone providers. Inputs are borrowed synchronously;
    /// returned argv is copied and readonly. No files are opened or executed.</summary>

    public static LinuxTracePlan InspectLinuxTracePlan(string originalNativeJson, IReadOnlyDictionary<string, LinuxToolValue> resolvedTools, IReadOnlyList<string> traceArguments)
    {
        var tools = InspectLinuxRecipeTools(originalNativeJson, resolvedTools);
        LinuxToolValue session = tools.Single(tool => tool.Name == "setsid"), tracer = tools.Single(tool => tool.Name == "strace");
        Need(session.Path == "/bin/busybox" && session.Sha256 == "01A989EB4D1D04B0D146C790AC536ABD88F374EC74A2E110C58910B840D42045" && tracer.Path == "/usr/bin/strace" && tracer.Sha256 == "BC573F002A4E1F4D3EA2209A01105D3B86DC59FC5DF0FEF3B1B191B2675A391D", "session_provider");
        if (traceArguments is null)
            throw new ToolFailure("log_upload_http_session_arguments");
        Need(traceArguments.Count is> 0 and <= 32, "session_arguments");
        string[] copied = new string[traceArguments.Count];
        for (int i = 0; i < copied.Length; i++)
            copied[i] = traceArguments[i];
        Need(copied.All(argument => argument is not null && argument.Length is> 0 and <= 4096 && !argument.Any(char.IsControl)) && copied.Sum(argument => Encoding.UTF8.GetByteCount(argument)) <= 32768, "session_arguments");
        return new(session.Path, new[] { "setsid", tracer.Path }.Concat(copied));
    }

    /// <summary>Validate one bounded copied gcc library-query line and return
    /// its canonical absolute Linux spelling. GCC may print lexical ../ parts;
    /// normalize those without filesystem access, refusing traversal above /.
    /// No file, archive, symlink target or compiler authority follows. The
    /// original owner resolves and holds the actual existing file separately.</summary>

    public static string InspectLinuxLibraryPath(string library, string stdout)
    {
        Need(LinuxLibraryNames.Contains(library, StringComparer.Ordinal) && stdout.Length <= 4096 && Encoding.UTF8.GetByteCount(stdout) <= 4096, "library_query");
        string line = stdout.EndsWith("\r\n", StringComparison.Ordinal) ? stdout[..^2] : stdout.EndsWith('\n') ? stdout[..^1] : stdout;
        Need(line.Length > 1 && line[0] == '/' && line.All(c => c is >= ' ' and <= '~' && c is not ('"' or '\\')), "library_query");
        List<string> parts = [];
        foreach (string part in line[1..].Split('/'))
        {
            if (part is "" or ".")
                continue;
            if (part == "..")
            {
                Need(parts.Count != 0, "library_query");
                parts.RemoveAt(parts.Count - 1);
            }
            else
                parts.Add(part);
        }

        Need(parts.Count != 0 && parts[^1] == library, "library_query");
        string path = "/" + string.Join('/', parts);
        Need(CanonicalLinux(path), "library_query");
        return path;
    }

    /// <summary>Hold one exact current local native input and require its
    /// caller-captured hash. Caller owns the supplied handle collection through
    /// use and final VerifyUnchanged; failure retains already admitted handles.
    /// Missing/substituted files fail without archive or historical fallback.
    /// Empty files require explicit admission from an authenticated fresh
    /// publication inventory; tools and static libraries remain nonempty.
    /// The original object/link identity is retained and rechecked; this helper
    /// does not require an initial single hard link.
    /// This cross-platform hold helper grants no compiler/publication authority;
    /// the Linux owner separately authenticates its selected paths and recipe.</summary>

    public static AuthorityFile RetainCurrentNativeInput(AuthorityFiles held, string path, string expectedSha256, bool allowEmpty = false)
    {
        Need(Path.IsPathFullyQualified(path) && IsHash(expectedSha256), "native_identity");
        var input = held.Open(Path.GetPathRoot(path)!, path, "original HTTP current native input", "log_upload_http_linux_native", 256 * 1024 * 1024, allowEmpty: allowEmpty);
        Need(input.Sha256.Equals(expectedSha256, StringComparison.OrdinalIgnoreCase), "native_input_changed");
        return input;
    }

    /// <summary>Inspect bounded copied publication values only. The original
    /// owner separately holds every actual source/native/output and ELF. A
    /// matching synthetic receipt never grants publication/process authority.</summary>

    public static PublicationValues InspectLinuxPublicationValues(string json, IReadOnlyDictionary<string, string> expectedSources)
    {
        Need(json.Length <= 16 * 1024 * 1024 && Encoding.UTF8.GetByteCount(json) <= 16 * 1024 * 1024 && expectedSources.Count is> 0 and <= 8192, "publication_capacity");
        using var document = JsonDocument.Parse(json);
        var value = document.RootElement;
        var fields = value.EnumerateObject().ToArray();
        Need(fields.Select(row => row.Name).Distinct(StringComparer.Ordinal).Count() == fields.Length && value.GetProperty("schema").GetString() == "clros.nativeaot.socket-log-upload-http.publication.v1" && value.GetProperty("succeeded").GetBoolean() && value.GetProperty("failure").ValueKind == JsonValueKind.Null && !value.GetProperty("appExecuted").GetBoolean() && !value.GetProperty("guestExecuted").GetBoolean() && value.GetProperty("application").GetString() == Application && value.GetProperty("sdk").GetString() == NativeAotPayload.SdkVersion && value.GetProperty("runtime").GetString() == NativeAotPayload.RuntimeVersion, "publication_policy");
        var sources = value.GetProperty("sources").EnumerateObject().ToArray();
        Need(sources.Length == expectedSources.Count && sources.Select(row => row.Name).Distinct(StringComparer.Ordinal).Count() == sources.Length && sources.All(row => SafeRelative(row.Name) && expectedSources.TryGetValue(row.Name, out string? hash) && IsHash(hash) && string.Equals(hash, row.Value.GetString(), StringComparison.OrdinalIgnoreCase)), "publication_sources");
        var native = value.GetProperty("native").EnumerateObject().ToArray();
        var outputs = value.GetProperty("outputs").EnumerateObject().ToArray();
        Need(native.Length is> 0 and <= 16384 && native.Select(row => row.Name).Distinct(StringComparer.Ordinal).Count() == native.Length && native.All(row => CanonicalLinux(row.Name) && IsHash(row.Value.GetString())), "publication_native");
        Need(outputs.Length is> 0 and <= 512 && outputs.Select(row => row.Name).Distinct(StringComparer.Ordinal).Count() == outputs.Length && outputs.All(row => SafeRelative(row.Name) && IsHash(row.Value.GetString())), "publication_outputs");
        string image = value.GetProperty("image").GetProperty("Sha256").GetString() ?? "";
        Need(IsHash(image) && outputs.Any(row => row.Name == Application + "/" + Application && image.Equals(row.Value.GetString(), StringComparison.OrdinalIgnoreCase)), "publication_image");
        return new(image.ToUpperInvariant(), sources.Length, native.Length, outputs.Length);
    }

    /// <summary>Bind the one successful execve row to copied literal argv. No
    /// process/file authority follows from a synthetic or copied trace. The
    /// original owner separately holds the ELF, tool and session observations.</summary>

    public static void InspectLinuxExecveValues(NativeAotLogUploadContract.TraceValues trace, string executable, IReadOnlyList<string> arguments)
    {
        Need(CanonicalLinux(executable) && arguments.Count <= 16 && arguments.All(argument => argument.Length <= 4096 && argument.All(c => c is >= ' ' and <= '~' && c is not ('"' or '\\'))), "execve_arguments");
        var exec = trace.Rows.Where(row => row.Syscall == "execve").ToArray();
        string quote(string text) => "\"" + text + "\"";
        string prefix = quote(executable) + ", [" + string.Join(", ", new[] { executable }.Concat(arguments).Select(quote)) + "], ";
        Need(exec.Length == 1 && exec[0].Kind == NativeAotLogUploadContract.TraceKind.CompletedCall && exec[0].Result == "0" && exec[0].Arguments.StartsWith(prefix, StringComparison.Ordinal) && exec[0].Arguments.Length > prefix.Length, "execve_identity");
    }

    /// <summary>Inspect synchronous borrowed response bytes and return copied
    /// facts. These exact finite vectors cannot establish an original HTTP
    /// exchange, file, process, device completion or durable storage.</summary>

    public static ResponseFact InspectResponseValues(ResponseKind kind, string method, string target, int status, string contentType, ReadOnlySpan<byte> body)
    {
        Need(body.Length <= NativeAotLogUploadContract.UploadBytes && contentType.Length <= 128 && contentType.All(c => c is >= ' ' and <= '~'), "response_capacity");
        (string Method, string Target, int Status, string Type, byte[] Body) expected = kind switch
        {
            ResponseKind.Health => ("GET", "/healthz", 200, "text/plain; charset=utf-8", Encoding.ASCII.GetBytes("LOG_UPLOAD_HTTP_OK\n")),
            ResponseKind.StartupLog => ("GET", "/log", 200, "text/plain; charset=utf-8", NativeAotLogUploadContract.ExpectedLog()[..StartupLogBytes]),
            ResponseKind.WrongType => ("POST", "/upload", 415, "", []),
            ResponseKind.WrongLength => ("POST", "/upload", 400, "", []),
            ResponseKind.Oversized => ("POST", "/upload", 413, "", []),
            ResponseKind.Busy => ("POST", "/upload", 409, "", []),
            ResponseKind.Accepted => ("POST", "/upload", 201, "text/plain; charset=utf-8", Encoding.ASCII.GetBytes("LOG_UPLOAD_HTTP_ACCEPTED\n")),
            ResponseKind.Log => ("GET", "/log", 200, "text/plain; charset=utf-8", NativeAotLogUploadContract.ExpectedLog()),
            ResponseKind.Upload => ("GET", "/upload", 200, "application/octet-stream", NativeAotLogUploadContract.ExpectedUpload()),
            _ => throw new ToolFailure("log_upload_http_response_kind")};
        Need(method == expected.Method && target == expected.Target && status == expected.Status && contentType == expected.Type && body.SequenceEqual(expected.Body), "response");
        return new(kind, method, target, status, contentType, body.Length, Hash(body));
    }

    /// <summary>Inspect only complete copied finite-app stdout/stderr. Exact
    /// lifecycle text is necessary but grants no original execution authority.</summary>

    public static void InspectServerTranscriptValues(string stdout, string stderr)
    {
        Need(stdout.Length <= MaximumTranscriptBytes && Encoding.UTF8.GetByteCount(stdout) <= MaximumTranscriptBytes && stderr.Length == 0, "transcript");
        Need(stdout.Replace("\r\n", "\n", StringComparison.Ordinal) == "TEST_START m10.027.http\n" + Policy + Ready + "LOG_UPLOAD_HTTP_ACCEPTED logBytes=660 uploadBytes=5126\nTEST_PASS m10.027.http\n", "transcript");
    }

    /// <summary>Read-only fresh-output check. Require a missing child beneath
    /// this checkout's existing plain artifacts tree; never replace an owner.</summary>

    public static void RequireFreshOutput(string root, string destination)
    {
        root = Path.GetFullPath(root);
        destination = Path.GetFullPath(destination, root);
        Need(destination.StartsWith(Path.Combine(root, "artifacts") + Path.DirectorySeparatorChar, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) && !Path.Exists(destination), "fresh_output_required");
        AuthorityFiles.RequirePlainPath(root, Path.GetDirectoryName(destination)!, "log_upload_http_output_parent");
    }

    /// <summary>Build the separate pinned managed Web app and unchanged console
    /// verifier, then own real Windows-job children and bounded loopback HTTP.
    /// Exercise preflight rejection, bad framing, a pending-body busy/abort,
    /// cancellation and real sharing-denied output opens before/after effects. The positive app
    /// stops only after its final response callback; require exact delivered
    /// bytes, actual exit0, refused endpoint and distinct console verification.
    /// Source/build handles remain held through completion. Failure receipts
    /// use an uncancelled writer after every launched child is joined. App data
    /// are not held against writes during the positive operation. No Linux,
    /// native/guest, disk image, device FLUSH or power-loss claim is granted.
    /// Call under this isolated checkout's serialized managed-build ownership.</summary>

    public static async Task<ManagedReference> RunManagedReferenceAsync(string root, string destination, CancellationToken cancellation = default)
    {
        Need(OperatingSystem.IsWindows(), "managed_windows_required");
        root = Path.GetFullPath(root);
        destination = Path.GetFullPath(destination, root);
        RequireFreshOutput(root, destination);
        NativeAotLogUploadContract.RequireCurrentSources(root);
        Need(ToolingBuildInputs.Validate(root).Count == 0, "tooling_inputs_changed");
        using var lease = NativeAotPayload.OpenQualificationLease(root);
        using AuthorityFiles held = new();
        string appDirectory = Path.Combine(root, "apps", Application), oldDirectory = Path.Combine(root, "apps", NativeAotLogUploadContract.Application);
        var inputs = new List<AuthorityFile>();
        foreach (string path in new[]
        {
            Path.Combine(appDirectory, Application + ".csproj"),
            Path.Combine(appDirectory, "Program.cs"),
            Path.Combine(oldDirectory, NativeAotLogUploadContract.Application + ".csproj"),
            Path.Combine(oldDirectory, "Program.cs"),
            Path.Combine(root, "global.json"),
            Path.Combine(root, "Directory.Build.props"),
            Path.Combine(root, "Directory.Build.targets"),
            typeof(NativeAotLogUploadHttpContract).Assembly.Location
        }

        )
            inputs.Add(held.Open(root, path, "managed HTTP source/producer", "log_upload_http_input", 64 * 1024 * 1024, singleLink: true));
        Directory.CreateDirectory(destination);
        AuthorityFiles.RequirePlainPath(root, destination, "log_upload_http_output");
        using var ownership = new FileStream(Path.Combine(destination, "ownership.lock"), FileMode.CreateNew, FileAccess.Write, FileShare.None);
        bool complete = false;
        string failure = "";
        List<object> commands = [];
        List<Control> controls = [];
        List<ResponseFact> responses = [];
        NativeAotLogUploadContract.FileValues? files = null;
        try
        {
            cancellation.ThrowIfCancellationRequested();
            var sdk = await Run("sdk", ["--version"]);
            Need(sdk.StandardOutput.Trim() == NativeAotPayload.SdkVersion, "sdk_pin");
            foreach (string directory in new[]
            {
                appDirectory,
                oldDirectory
            }

            )
            {
                string name = Path.GetFileName(directory), project = Path.Combine(directory, name + ".csproj");
                await Run(name + "-restore", ["restore", project, "--nologo"]);
                await Run(name + "-build", ["build", project, "--no-restore", "--no-incremental", "--nologo", "-m:1", "-p:UseSharedCompilation=false"]);
                string assetsPath = Path.Combine(directory, "obj", "project.assets.json");
                var assets = held.Open(root, assetsPath, "managed HTTP assets", "log_upload_http_assets", 32 * 1024 * 1024, singleLink: true);
                inputs.Add(assets);
                using (var parsed = JsonDocument.Parse(assets.ReadBytes(32 * 1024 * 1024, "log_upload_http_assets")))
                {
                    string[] libraries = parsed.RootElement.GetProperty("libraries").EnumerateObject().Select(row => row.Name).Order(StringComparer.Ordinal).ToArray();
                    Need(libraries.All(library => !library.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) && !library.Contains("EntityFramework", StringComparison.OrdinalIgnoreCase)), "foreign_dependency");
                    await Save(destination, name + "-dependencies.json", JsonSerializer.Serialize(libraries));
                }

                string bin = Path.Combine(directory, "bin", "Debug", NativeAotPayload.TargetFramework);
                string[] built = Directory.GetFiles(bin, "*", SearchOption.AllDirectories);
                Need(built.Length is> 0 and <= 64, "built_capacity");
                inputs.AddRange(built.Select(path => held.Open(root, path, "managed HTTP built output", "log_upload_http_built", 64 * 1024 * 1024, allowEmpty: true, singleLink: true)));
                using var runtime = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(bin, name + ".runtimeconfig.json")));
                var options = runtime.RootElement.GetProperty("runtimeOptions");
                Need(options.GetProperty("tfm").GetString() == NativeAotPayload.TargetFramework && options.GetProperty("rollForward").GetString() == "Disable", "runtime_pin");
                JsonElement[] frameworks = options.TryGetProperty("frameworks", out var multiple) ? multiple.EnumerateArray().ToArray() : [options.GetProperty("framework")];
                Need(frameworks.Select(frame => frame.GetProperty("name").GetString()).Order(StringComparer.Ordinal).SequenceEqual((name == Application ? new[] { "Microsoft.AspNetCore.App", "Microsoft.NETCore.App" } : new[] { "Microsoft.NETCore.App" }).Order(StringComparer.Ordinal)) && frameworks.All(frame => frame.GetProperty("version").GetString() == NativeAotPayload.RuntimeVersion), "runtime_pin");
            }

            string server = Path.Combine(appDirectory, "bin", "Debug", NativeAotPayload.TargetFramework, Application + ".dll");
            foreach (string control in new[]
            {
                "missing-upload",
                "nonempty-log",
                "nonempty-upload",
                "cancel-body",
                "io-open-denied",
                "log-append-denied"
            }

            )
                controls.Add(await Session(control, server));
            controls.Add(await Session("positive", server));
            string positive = Path.Combine(destination, "positive", "data");
            // These handles are acquired only after the server is gone. They
            // cannot manufacture its positive writes or failed-write controls.
            var log = held.Open(root, Path.Combine(positive, NativeAotLogUploadContract.LogName), "original managed HTTP log", "log_upload_http_log", 4096, singleLink: true);
            var upload = held.Open(root, Path.Combine(positive, NativeAotLogUploadContract.UploadName), "original managed HTTP upload", "log_upload_http_upload", 5126, singleLink: true);
            var guard = held.Open(root, Path.Combine(positive, "GUARD.BIN"), "managed HTTP guard", "log_upload_http_guard", 64, singleLink: true);
            AuthorityFiles.RequireDistinct(log, upload, guard);
            files = NativeAotLogUploadContract.InspectFileValues(log.ReadBytes(4096, "log_upload_http_log"), upload.ReadBytes(5126, "log_upload_http_upload"));
            Need(guard.ReadBytes(64, "log_upload_http_guard").AsSpan().SequenceEqual(Guard), "guard_changed");
            string verifier = Path.Combine(oldDirectory, "bin", "Debug", NativeAotPayload.TargetFramework, NativeAotLogUploadContract.Application + ".dll");
            var verified = await Run("independent-console-verify", [verifier, "--verify", "--root", positive]);
            Need(verified.StandardError.Length == 0, "verifier_stderr");
            Need(NativeAotLogUploadContract.InspectTranscriptValues(verified.StandardOutput, "verify").ExpectedFiles == files.Value, "verifier_files");
            await Save(destination, "inputs.json", JsonSerializer.Serialize(inputs.Select(input => new { input.Path, input.Length, input.Sha256 })));
            held.VerifyUnchanged();
            NativeAotLogUploadContract.RequireCurrentSources(root);
            Need(ToolingBuildInputs.Validate(root).Count == 0, "tooling_inputs_changed");
            complete = true;
        }
        catch (Exception error)
        {
            failure = error is ToolFailure tool ? tool.Reason : error.GetType().Name;
            throw;
        }
        finally
        {
            await Save(destination, "commands.json", JsonSerializer.Serialize(commands));
            await Save(destination, "receipt.json", JsonSerializer.Serialize(new { Schema = "CLROS_LOG_UPLOAD_HTTP_MANAGED_REFERENCE_V1", Complete = complete, Failure = failure, Files = files, Responses = responses, Controls = controls, FlushRequested = true, DeviceFlushObserved = false, LinuxNativeAotQualified = false, GuestQualified = false, ColdRebootQualified = false, PhysicalPersistenceQualified = false }));
        }

        return new(destination, files!.Value, responses, controls);
        async Task<ProcessResult> Run(string phase, string[] arguments)
        {
            commands.Add(new { Phase = phase, Executable = "dotnet", Arguments = arguments, WorkingDirectory = root });
            var result = await ProcessRunner.RunAsync(new("dotnet", arguments, root) { Timeout = TimeSpan.FromMinutes(3), OutputCharacterLimit = 4 * 1024 * 1024 }, cancellation);
            await Save(destination, phase + ".stdout.log", result.StandardOutput);
            await Save(destination, phase + ".stderr.log", result.StandardError);
            await Save(destination, phase + ".process.json", JsonSerializer.Serialize(result));
            QemuTranscripts.RequireCompleteProcess(result, "log_upload_http_process_incomplete");
            Need(result.Succeeded, "process_" + phase);
            return result;
        }

        async Task<Control> Session(string name, string server)
        {
            string directory = Path.Combine(destination, name), data = Path.Combine(directory, "data");
            Directory.CreateDirectory(data);
            string log = Path.Combine(data, NativeAotLogUploadContract.LogName), upload = Path.Combine(data, NativeAotLogUploadContract.UploadName);
            await File.WriteAllBytesAsync(log, name == "nonempty-log" ? [0x71] : [], cancellation);
            if (name != "missing-upload")
                await File.WriteAllBytesAsync(upload, name == "nonempty-upload" ? [0x72] : [], cancellation);
            await File.WriteAllBytesAsync(Path.Combine(data, "GUARD.BIN"), Guard, cancellation);
            string before = Snapshot(data);
            int port = ReservePort();
            using var owner = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            StringBuilder observed = new();
            var arguments = new[]
            {
                server,
                "--serve-once",
                "--root",
                data,
                "--urls",
                "http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture)
            };
            commands.Add(new { Phase = name, Executable = "dotnet", Arguments = arguments, WorkingDirectory = root });
            Task<ProcessResult> child = ProcessRunner.RunAsync(new("dotnet", arguments, root) { Timeout = TimeSpan.FromSeconds(90), OutputCharacterLimit = 65536, OutputReceived = (text, error) =>
            {
                if (!error)
                    lock (observed)
                    {
                        if (observed.Length + text.Length <= MaximumTranscriptBytes)
                            observed.Append(text);
                        if (observed.ToString().Replace("\r\n", "\n", StringComparison.Ordinal).Contains(Ready, StringComparison.Ordinal))
                            ready.TrySetResult();
                    }
            } }, owner.Token);
            bool succeeded = false, refused = false, joined = false;
            string sessionFailure = "", startup = "", snapshotFailure = "";
            ProcessResult? result = null;
            List<ResponseFact> sessionResponses = [];
            try
            {
                if (name is "missing-upload" or "nonempty-log" or "nonempty-upload")
                {
                    result = await child;
                    QemuTranscripts.RequireCompleteProcess(result, "log_upload_http_preflight_incomplete");
                    string expected = "TEST_START m10.027.http\nTEST_FAIL m10.027.http " + (name == "missing-upload" ? "FileNotFoundException" : "InvalidOperationException") + "\n";
                    Need(result.ExitCode == 1 && result.StandardError.Length == 0 && result.StandardOutput.Replace("\r\n", "\n", StringComparison.Ordinal) == expected, "preflight_control");
                    Need(Snapshot(data) == before, "preflight_modified");
                }
                else
                {
                    using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
                    deadline.CancelAfter(TimeSpan.FromSeconds(40));
                    var first = await Task.WhenAny(ready.Task, child).WaitAsync(deadline.Token);
                    Need(first == ready.Task, "exited_before_ready");
                    await ready.Task;
                    using HttpClient client = Client(port);
                    await WaitPhase(client, "ready", deadline.Token);
                    startup = Snapshot(data);
                    RequireStartup(data);
                    if (name == "cancel-body")
                    {
                        using var partial = await Partial(port, deadline.Token);
                        await WaitPhase(client, "receiving", deadline.Token);
                        Need(Snapshot(data) == startup, "partial_modified");
                        owner.Cancel();
                        result = await child;
                        Need(result.Cancelled && result.TerminationConfirmed && !result.TimedOut && !result.OutputLimitExceeded && result.FailureReason.Length == 0, "cancel_control");
                        Need(Snapshot(data) == startup, "cancel_modified");
                    }
                    else if (name is "io-open-denied" or "log-append-denied")
                    {
                        using (var deny = new FileStream(name == "io-open-denied" ? upload : log, FileMode.Open, FileAccess.Read, FileShare.Read))
                        {
                            using var request = new HttpRequestMessage(HttpMethod.Post, "/upload")
                            {
                                Content = Content(NativeAotLogUploadContract.ExpectedUpload())
                            };
                            using var response = await client.SendAsync(request, deadline.Token);
                            Need((int)response.StatusCode == 500 && (await response.Content.ReadAsByteArrayAsync(deadline.Token)).Length == 0, "io_failure_control");
                        }

                        result = await child;
                        QemuTranscripts.RequireCompleteProcess(result, "log_upload_http_io_incomplete");
                        Need(result.ExitCode == 1 && result.StandardError.Length == 0 && !result.StandardOutput.Contains("TEST_PASS", StringComparison.Ordinal), "io_failure_control");
                        if (name == "io-open-denied")
                            Need(Snapshot(data) == startup, "denied_open_modified");
                        else
                        {
                            // The upload was already written/flushed/closed
                            // when the later log open failed. Preserve it.
                            Need(File.ReadAllBytes(log).AsSpan().SequenceEqual(NativeAotLogUploadContract.ExpectedLog().AsSpan(0, StartupLogBytes)) && File.ReadAllBytes(upload).AsSpan().SequenceEqual(NativeAotLogUploadContract.ExpectedUpload()) && File.ReadAllBytes(Path.Combine(data, "GUARD.BIN")).AsSpan().SequenceEqual(Guard), "partial_effects_control");
                        }
                    }
                    else
                    {
                        await Request(ResponseKind.Health);
                        await Request(ResponseKind.StartupLog);
                        await Request(ResponseKind.WrongType, new byte[5126], "text/plain");
                        await Request(ResponseKind.WrongLength, [0x01]);
                        await Request(ResponseKind.Oversized, new byte[5127]);
                        Need(Snapshot(data) == startup, "bad_request_modified");
                        using (var partial = await Partial(port, deadline.Token))
                        {
                            await WaitPhase(client, "receiving", deadline.Token);
                            await Request(ResponseKind.Busy, NativeAotLogUploadContract.ExpectedUpload());
                            Need(Snapshot(data) == startup, "busy_modified");
                        }

                        await WaitPhase(client, "ready", deadline.Token);
                        Need(Snapshot(data) == startup, "abort_modified");
                        await Save(directory, "pre-request.json", startup);
                        await Request(ResponseKind.Accepted, NativeAotLogUploadContract.ExpectedUpload());
                        NativeAotLogUploadContract.InspectFileValues(File.ReadAllBytes(log), File.ReadAllBytes(upload));
                        string accepted = Snapshot(data);
                        await Request(ResponseKind.Busy, NativeAotLogUploadContract.ExpectedUpload());
                        Need(Snapshot(data) == accepted, "duplicate_modified");
                        await Request(ResponseKind.Log);
                        await Request(ResponseKind.Upload);
                        result = await child;
                        QemuTranscripts.RequireCompleteProcess(result, "log_upload_http_server_incomplete");
                        Need(result.Succeeded, "server_exit");
                        InspectServerTranscriptValues(result.StandardOutput, result.StandardError);
                        Need(File.ReadAllBytes(Path.Combine(data, "GUARD.BIN")).AsSpan().SequenceEqual(Guard), "guard_changed");
                        responses.AddRange(sessionResponses);
                        succeeded = true;
                    }

                    async Task Request(ResponseKind kind, byte[]? bytes = null, string type = "application/octet-stream")
                    {
                        string method = kind is ResponseKind.Health or ResponseKind.StartupLog or ResponseKind.Log or ResponseKind.Upload ? "GET" : "POST";
                        string target = kind == ResponseKind.Health ? "/healthz" : kind is ResponseKind.StartupLog or ResponseKind.Log ? "/log" : "/upload";
                        using var request = new HttpRequestMessage(new HttpMethod(method), target);
                        if (bytes is not null)
                        {
                            request.Content = new ByteArrayContent(bytes);
                            request.Content.Headers.ContentType = new(type);
                        }

                        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
                        byte[] body = await ReadBounded(response, deadline.Token);
                        string contentType = response.Content.Headers.ContentType?.ToString() ?? "";
                        var fact = InspectResponseValues(kind, method, target, (int)response.StatusCode, contentType, body);
                        sessionResponses.Add(fact);
                        string ordinal = sessionResponses.Count.ToString("D2", CultureInfo.InvariantCulture);
                        await File.WriteAllBytesAsync(Path.Combine(directory, ordinal + ".body"), body, CancellationToken.None);
                    }
                }

                await Refused(port, cancellation);
                refused = true;
            }
            catch (Exception error)
            {
                sessionFailure = error is ToolFailure tool ? tool.Reason : error.GetType().Name;
                throw;
            }
            finally
            {
                owner.Cancel();
                try
                {
                    result = await child;
                    joined = result.TerminationConfirmed;
                }
                finally
                {
                    if (result is not null)
                    {
                        await Save(directory, "server.stdout.log", result.StandardOutput);
                        await Save(directory, "server.stderr.log", result.StandardError);
                        await Save(directory, "server.process.json", JsonSerializer.Serialize(result));
                    }

                    string actual = "";
                    try
                    {
                        actual = Snapshot(data);
                    }
                    catch (Exception error)
                    {
                        snapshotFailure = error is ToolFailure tool ? tool.Reason : error.GetType().Name;
                    }

                    await Save(directory, "http.json", JsonSerializer.Serialize(new { Schema = "CLROS_LOG_UPLOAD_HTTP_SESSION_V1", Name = name, Complete = succeeded && snapshotFailure.Length == 0, ExpectedFailure = name != "positive", Failure = sessionFailure, Joined = joined, EndpointRefused = refused, Port = port, Server = result, Responses = sessionResponses, BeforeFilesJson = before, PreRequestFilesJson = startup, ActualFilesJson = actual, SnapshotFailure = snapshotFailure, DeviceFlushObserved = false, LinuxNativeAotQualified = false, GuestQualified = false, PhysicalPersistenceQualified = false }));
                }
            }

            Need(joined, "server_not_joined");
            Need(snapshotFailure.Length == 0, "session_snapshot_failed");
            return new(name, name != "positive", joined, result!.Cancelled, result.ExitCode, refused);
        }
    }

    /// <summary>Root-serialized Linux reference only. Freshly publish this exact
    /// HTTP app and compose the unchanged console's fresh LinuxReference in a
    /// separate namespace. Keep that live owner and original source/compiler/
    /// runtime/publication handles through an additional --verify of HTTP data.
    /// Own each setsid/strace session, join capture/group cleanup and retain raw
    /// write-trace/verify-trace files, including failures and incomplete lexical
    /// projection. Portable controls omit Windows sharing-denial and physical
    /// I/O-error injection. Positive completion requires natural exit0, exact
    /// delivered/file bytes, refused endpoint and the distinct verifier. Failed
    /// receipts retain actual partial state without asserting rollback. Existing
    /// HTTP sessions and the additional verifier have90-second limits. The
    /// composed console owner retains its existing2-minute trace and45-minute
    /// publication limits; cancellation is checked between publisher calls.
    /// Bounded fd censuses are observed boundary counts, never a peak/fit proof;
    /// this owner neither changes descriptor limits nor drops retained inputs.
    /// No CLROS, syscall closure, device-FLUSH, recovery or persistence authority.
    /// Caller must hold the external exclusive Linux-builder lease.</summary>

    public static async Task<LinuxReference> RunLinuxReferenceAsync(string root, string recipeRoot, string destination, TextWriter output, CancellationToken cancellation = default)
    {
        Need(OperatingSystem.IsLinux(), "linux_builder_required");
        cancellation.ThrowIfCancellationRequested();
        root = Path.GetFullPath(root);
        recipeRoot = Path.GetFullPath(recipeRoot);
        destination = Path.GetFullPath(destination, root);
        Need(root != recipeRoot, "original_recipe_required");
        RequireFreshOutput(root, destination);
        RequireCurrentSources(root, fresh: true);
        NativeAotLogUploadContract.RequireCurrentSources(root);
        Need(ToolingBuildInputs.Validate(root).Count == 0, "tooling_inputs_changed");
        const string marker = "nativeaot.log-upload-http-reference";
        output.WriteLine("TEST_START " + marker);
        string[] SourcePaths() => ToolingBuildInputs.SourceInputs(root).Concat(new[] { Path.Combine(root, "apps", Application, "Program.cs"), Path.Combine(root, "apps", Application, Application + ".csproj") }).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        Dictionary<string, string> Sources() => SourcePaths().ToDictionary(path => Path.GetRelativePath(root, path).Replace('\\', '/'), NativeAotWebPayload.HashFile, StringComparer.Ordinal);
        var before = Sources();
        Need(before.Count is> 0 and <= 8192, "source_capacity");
        AuthorityFiles held = new();
        NativeAotLogUploadContract.LinuxReference? verifier = null;
        bool transferred = false;
        List<object> commands = [], fdBoundaries = [];
        List<Control> controls = [];
        List<ResponseFact> responses = [];
        Dictionary<string, string> native = new(StringComparer.Ordinal);
        bool complete = false;
        string failure = "";
        NativeAotPayload.Inspection? image = null;
        NativeAotLogUploadContract.FileValues? files = null;
        NativeAotLogUploadContract.TraceValues? writeTrace = null, verifyTrace = null;
        string strace = "", originalNative = "";
        ReadOnlyCollection<LinuxToolValue> selectedTools = Array.AsReadOnly(Array.Empty<LinuxToolValue>());
        FileStream? sourceLease = null, recipeLease = null;
        try
        {
            foreach (string path in SourcePaths())
                held.Open(root, path, "original compiled HTTP producer source", "log_upload_http_linux_source", 16 * 1024 * 1024, singleLink: true);
            foreach (string path in new[]
            {
                Path.Combine(root, "global.json"),
                Path.Combine(root, "Directory.Build.props"),
                Path.Combine(root, "Directory.Build.targets"),
                Path.Combine(root, "tools", "Directory.Build.props"),
                Path.Combine(root, "apps", Application, "Program.cs"),
                Path.Combine(root, "apps", Application, Application + ".csproj"),
                Path.Combine(root, "apps", NativeAotLogUploadContract.Application, "Program.cs"),
                Path.Combine(root, "apps", NativeAotLogUploadContract.Application, NativeAotLogUploadContract.Application + ".csproj")
            }

            )
                held.Open(root, path, "original HTTP reference source", "log_upload_http_linux_source", 16 * 1024 * 1024, singleLink: true);
            string producer = Path.GetDirectoryName(typeof(NativeAotLogUploadHttpContract).Assembly.Location)!;
            string[] producerFiles = Directory.GetFiles(producer, "*.dll").Concat(Directory.GetFiles(producer, "*.deps.json")).Concat(Directory.GetFiles(producer, "*.runtimeconfig.json")).Order(StringComparer.Ordinal).ToArray();
            Need(producerFiles.Length is> 0 and <= 64, "producer_capacity");
            foreach (string path in producerFiles)
                held.Open(Path.GetPathRoot(path)!, path, "original HTTP producer closure", "log_upload_http_linux_producer", 64 * 1024 * 1024, singleLink: true);
            string recipe = Path.Combine(recipeRoot, "artifacts", "socket-qualification-r01");
            foreach (string name in new[]
            {
                "commands.json",
                "native-inputs.json",
                "receipt.json"
            }

            )
                held.Open(recipeRoot, Path.Combine(recipe, name), "original socket recipe", "log_upload_http_linux_recipe", 16 * 1024 * 1024, singleLink: true);
            // This existing validator authenticates E260, all32 retained
            // outputs, the three relocated producer outputs and original ELF.
            // Historical absolute native-map rows are not current filenames.
            _ = NativeAotSocketPackage.Create(root, recipeRoot, asynchronous: true);
            originalNative = held.Open(recipeRoot, Path.Combine(recipe, "native-inputs.json"), "original recipe native inputs", "log_upload_http_linux_recipe", 16 * 1024 * 1024, singleLink: true).ReadUtf8(16 * 1024 * 1024, "log_upload_http_linux_recipe");
            selectedTools = InspectLinuxRecipeTools(originalNative, LinuxToolNames.ToDictionary(name => name, name =>
            {
                string path = ResolveToolPath(name);
                return new LinuxToolValue(name, path, NativeAotWebPayload.HashFile(path));
            }, StringComparer.Ordinal));
            foreach (var tool in selectedTools)
                HoldNative(tool.Path, tool.Sha256);
            strace = selectedTools.Single(tool => tool.Name == "strace").Path;
            // Reject an unsupported provider before publication/tool execution.
            _ = TracePlan(["--version"]);
            Directory.CreateDirectory(destination);
            AuthorityFiles.RequirePlainPath(root, destination, "log_upload_http_linux_output");
            using var ownership = new FileStream(Path.Combine(destination, "ownership.lock"), FileMode.CreateNew, FileAccess.Write, FileShare.None);
            try
            {
                // The later nested console publication cannot retroactively
                // authenticate the system archives used by this first link.
                string gcc = selectedTools.Single(tool => tool.Name == "gcc").Path;
                foreach (string library in LinuxLibraryNames)
                {
                    cancellation.ThrowIfCancellationRequested();
                    CheckInputs();
                    string queryLog = "native-library-" + library;
                    // ExecuteAsync uses its existing logical setsid wrapper;
                    // this is distinct from the pinned physical trace plan.
                    commands.Add(new { Executable = "setsid", Arguments = new[] { gcc, "-print-file-name=" + library }, WorkingDirectory = root, Phase = "HTTP static-link discovery" });
                    bool succeeded = false;
                    string queryFailure = "";
                    try
                    {
                        cancellation.ThrowIfCancellationRequested();
                        await NativeAotWebPayload.ExecuteAsync(gcc, ["-print-file-name=" + library], root, Path.Combine(destination, queryLog), TimeSpan.FromMinutes(2));
                        succeeded = true;
                    }
                    catch (Exception error)
                    {
                        queryFailure = error.Message.ReplaceLineEndings(" ");
                        throw;
                    }
                    finally
                    {
                        await Save(destination, queryLog + ".process.json", JsonSerializer.Serialize(new { NormalOwnedSessionReturned = succeeded, Executable = gcc, Arguments = new[] { "-print-file-name=" + library }, MaximumSeconds = 120, Failure = queryFailure }));
                    }

                    var answer = held.Open(root, Path.Combine(destination, queryLog + ".stdout.log"), "original HTTP static library query", "log_upload_http_linux_library", 4096, singleLink: true);
                    var errors = held.Open(root, Path.Combine(destination, queryLog + ".stderr.log"), "original HTTP static library query stderr", "log_upload_http_linux_library", 0, allowEmpty: true, singleLink: true);
                    string path = InspectLinuxLibraryPath(library, answer.ReadUtf8(4096, "log_upload_http_linux_library"));
                    Need(File.Exists(path) && errors.Length == 0, "static_library_missing");
                    path = File.ResolveLinkTarget(path, true)?.FullName ?? Path.GetFullPath(path);
                    HoldNative(path, NativeAotWebPayload.HashFile(path));
                    CheckInputs();
                }

                CheckInputs();
                cancellation.ThrowIfCancellationRequested();
                ObserveDescriptors("before-http-publication");
                string publication = Path.Combine(destination, "http-publication");
                commands.Add(new { Producer = nameof(NativeAotSocketFixturePublisher), Application, RecipeRoot = recipeRoot, Output = publication });
                Need(await Publisher.RunAsync(root, ["publish", "--recipe-root", recipeRoot, "--output", publication], output) == 0, "linux_publication_failed");
                cancellation.ThrowIfCancellationRequested();
                CheckInputs();
                string[] publishedFiles = Directory.GetFiles(publication, "*", SearchOption.AllDirectories).Where(path => Path.GetFileName(path) != "publication.lock").Order(StringComparer.Ordinal).ToArray();
                Need(publishedFiles.Length is> 0 and <= 512, "publication_capacity");
                foreach (string path in publishedFiles)
                    held.Open(root, path, "original HTTP publication", "log_upload_http_linux_publication", 256 * 1024 * 1024, allowEmpty: true, singleLink: true);
                var publicationFile = held.Open(root, Path.Combine(publication, "publication.json"), "original HTTP publication receipt", "log_upload_http_linux_publication", 16 * 1024 * 1024, singleLink: true);
                string publicationJson = publicationFile.ReadUtf8(16 * 1024 * 1024, "log_upload_http_linux_publication");
                var values = InspectLinuxPublicationValues(publicationJson, before);
                using (var parsed = JsonDocument.Parse(publicationJson))
                {
                    foreach (var row in parsed.RootElement.GetProperty("native").EnumerateObject())
                        HoldNative(row.Name, row.Value.GetString()!, allowEmpty: true);
                    var outputs = parsed.RootElement.GetProperty("outputs").EnumerateObject().ToArray();
                    Need(outputs.Select(row => row.Name).Order(StringComparer.Ordinal).SequenceEqual(publishedFiles.Where(path => Path.GetFileName(path) != "publication.json").Select(path => Path.GetRelativePath(publication, path).Replace('\\', '/')).Order(StringComparer.Ordinal)), "publication_output_inventory");
                    foreach (var row in outputs)
                        Need(held.Open(root, Path.Combine(publication, row.Name), "original HTTP output", "log_upload_http_linux_publication", 256 * 1024 * 1024, allowEmpty: true, singleLink: true).Sha256.Equals(row.Value.GetString(), StringComparison.OrdinalIgnoreCase), "publication_output_changed");
                }

                string executable = Path.Combine(publication, Application, Application);
                var elf = held.Open(root, executable, "original HTTP native executable", "log_upload_http_linux_elf", NativeAotPayload.MaximumImageBytes, singleLink: true);
                image = NativeAotPayload.Inspect(elf.ReadBytes(NativeAotPayload.MaximumImageBytes, "log_upload_http_linux_elf"));
                Need(image.Sha256.Equals(values.ImageSha256, StringComparison.OrdinalIgnoreCase), "publication_image");
                ObserveDescriptors("after-http-publication-holds");
                // A live original owner is composed, not reconstructed from a
                // copied old receipt or ELF. Its own fresh write/verify data is
                // separate from the HTTP app's data and proves no HTTP effect.
                commands.Add(new { Producer = nameof(NativeAotLogUploadContract), Output = Path.Combine(destination, "verifier-reference") });
                verifier = await NativeAotLogUploadContract.RunLinuxReferenceAsync(root, recipeRoot, Path.Combine(destination, "verifier-reference"), output, cancellation);
                sourceLease = NativeAotPayload.OpenQualificationLease(root);
                recipeLease = NativeAotPayload.OpenQualificationLease(recipeRoot);
                // Reuse the original selected roles and their live binary holds.
                // A later path merely appearing in native cannot replace a role.
                CheckInputs();
                ObserveDescriptors("after-console-reference-holds");
                foreach (string name in new[]
                {
                    "missing-upload",
                    "nonempty-log",
                    "nonempty-upload",
                    "cancel-body",
                    "positive"
                }

                )
                {
                    var session = await Session(name, executable);
                    controls.Add(session.Control);
                    if (name == "positive")
                        writeTrace = session.Trace;
                }

                string data = Path.Combine(destination, "positive", "data");
                var log = held.Open(root, Path.Combine(data, NativeAotLogUploadContract.LogName), "original HTTP log", "log_upload_http_linux_log", 4096, singleLink: true);
                var upload = held.Open(root, Path.Combine(data, NativeAotLogUploadContract.UploadName), "original HTTP upload", "log_upload_http_linux_upload", 5126, singleLink: true);
                var guard = held.Open(root, Path.Combine(data, "GUARD.BIN"), "original HTTP guard", "log_upload_http_linux_guard", 64, singleLink: true);
                AuthorityFiles.RequireDistinct(log, upload, guard, elf);
                files = NativeAotLogUploadContract.InspectFileValues(log.ReadBytes(4096, "log_upload_http_linux_log"), upload.ReadBytes(5126, "log_upload_http_linux_upload"));
                Need(guard.ReadBytes(64, "log_upload_http_linux_guard").AsSpan().SequenceEqual(Guard), "guard_changed");
                string verifyExecutable = Path.Combine(verifier.Directory, "publication", NativeAotLogUploadContract.Application, NativeAotLogUploadContract.Application);
                var verifyElf = held.Open(root, verifyExecutable, "original unchanged console verifier", "log_upload_http_linux_verifier", NativeAotPayload.MaximumImageBytes, singleLink: true);
                Need(verifyElf.Sha256.Equals(verifier.Image.Sha256, StringComparison.OrdinalIgnoreCase), "verifier_identity");
                string[] verifyArguments = NativeAotLogUploadContract.LinuxTraceArguments(verifyExecutable, data, Path.Combine(destination, "verify-trace"), "verify");
                LinuxTracePlan verifyPlan = TracePlan(verifyArguments);
                commands.Add(new { verifyPlan.Executable, verifyPlan.Arguments, WorkingDirectory = root });
                bool verifyJoined = false, verifyNatural = false;
                int? verifyExit = null;
                ObserveDescriptors("before-additional-verifier");
                cancellation.ThrowIfCancellationRequested();
                using (var verifyProcess = StartTrace(verifyPlan))
                using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
                {
                    deadline.CancelAfter(TimeSpan.FromSeconds(90));
                    Task verifyStdout = NativeAotWebPayload.CaptureAsync(verifyProcess.StandardOutput, Path.Combine(destination, "verify.stdout.log"), verifyProcess);
                    Task verifyStderr = NativeAotWebPayload.CaptureAsync(verifyProcess.StandardError, Path.Combine(destination, "verify.stderr.log"), verifyProcess);
                    try
                    {
                        await Task.WhenAll(verifyProcess.WaitForExitAsync(deadline.Token), verifyStdout, verifyStderr).WaitAsync(deadline.Token);
                        verifyExit = verifyProcess.ExitCode;
                        verifyNatural = true;
                        Need(verifyExit == 0, "linux_verifier_exit");
                    }
                    finally
                    {
                        try
                        {
                            await NativeAotWebPayload.StopSessionAsync(verifyProcess);
                            verifyJoined = true;
                        }
                        finally
                        {
                            try
                            {
                                await Task.WhenAll(verifyStdout, verifyStderr).WaitAsync(TimeSpan.FromSeconds(10));
                            }
                            finally
                            {
                                await Save(destination, "verify-process.json", JsonSerializer.Serialize(new { Joined = verifyJoined, NaturalExitObserved = verifyNatural, ExitCode = verifyExit }));
                            }
                        }
                    }
                }

                var verifyOut = held.Open(root, Path.Combine(destination, "verify.stdout.log"), "original independent verifier stdout", "log_upload_http_linux_verify", MaximumTranscriptBytes, singleLink: true);
                var verifyError = held.Open(root, Path.Combine(destination, "verify.stderr.log"), "original independent verifier stderr", "log_upload_http_linux_verify", 0, allowEmpty: true, singleLink: true);
                Need(NativeAotLogUploadContract.InspectTranscriptValues(verifyOut.ReadUtf8(MaximumTranscriptBytes, "log_upload_http_linux_verify"), "verify").ExpectedFiles == files.Value && verifyError.Length == 0, "verifier_files");
                verifyTrace = ReadTraces(destination, "verify");
                Need(verifyTrace.ExitedFiles == verifyTrace.Files && verifyTrace.Rows.Any(row => row.Syscall == "execve") && verifyTrace.Rows.Any(row => row.Syscall == "exit_group"), "verify_trace_exit");
                InspectLinuxExecveValues(verifyTrace, verifyExecutable, ["--verify", "--root", data]);
                ObserveDescriptors("after-additional-verifier-join");
                await Save(destination, "verify-trace-values.json", JsonSerializer.Serialize(verifyTrace));
                CheckInputs();
                complete = true;
            }
            catch (Exception error)
            {
                failure = error is ToolFailure tool ? tool.Reason : error.Message.ReplaceLineEndings(" ");
                throw;
            }
            finally
            {
                await Save(destination, "commands.json", JsonSerializer.Serialize(commands));
                await Save(destination, "sources.json", JsonSerializer.Serialize(before));
                await Save(destination, "native-inputs.json", JsonSerializer.Serialize(native));
                ObserveDescriptors("before-final-receipt");
                await Save(destination, "fd-boundaries.json", JsonSerializer.Serialize(new { ObservedBoundaries = fdBoundaries, PeakOrCapacityProof = false, DescriptorLimitsChanged = false }));
                string[] outputs = OutputPaths();
                await Save(destination, "receipt.json", JsonSerializer.Serialize(new { Schema = "CLROS_LOG_UPLOAD_HTTP_LINUX_REFERENCE_V1", Complete = complete, Failure = failure, HttpImage = image, VerifierImage = verifier?.Image, ConsoleReferenceDirectory = verifier?.Directory, Files = files, Responses = responses, Controls = controls, WriteTraceProjectionComplete = writeTrace?.CompleteTraceProjection, VerifyTraceProjectionComplete = verifyTrace?.CompleteTraceProjection, Sources = before, Native = native, Outputs = outputs.ToDictionary(path => Path.GetRelativePath(destination, path).Replace('\\', '/'), NativeAotWebPayload.HashFile, StringComparer.Ordinal), WindowsSharingDenialControlsRun = false, LinuxPhysicalIoErrorInjectionQualified = false, SyscallClosureQualified = false, CallerCancellationRequested = cancellation.IsCancellationRequested, DeviceFlushObserved = false, GuestQualified = false, ColdRebootQualified = false, PhysicalPersistenceQualified = false }));
            }

            string[] artifacts = OutputPaths();
            Need(artifacts.Length is> 0 and <= 2048, "artifact_capacity");
            var originals = artifacts.Select(path => held.Open(root, path, "original HTTP reference evidence", "log_upload_http_linux_evidence", 256 * 1024 * 1024, allowEmpty: true, singleLink: true)).ToArray();
            AuthorityFiles.RequireDistinct(originals);
            CheckInputs();
            // This last count is written only to the caller-owned producer
            // output: all evidence files are already immutable and held.
            ObserveDescriptors("after-final-output-holds");
            var result = new LinuxReference(destination, image!, verifier!, files!.Value, responses, writeTrace!, verifyTrace!, held, originals.Select(file => new NativeAotLogUploadContract.ArtifactIdentity(file.Path, file.Length, file.Sha256)));
            transferred = true;
            output.WriteLine("TEST_PASS " + marker);
            return result;
        }
        catch (Exception error)
        {
            // A final evidence/identity check can fail after the provisional
            // receipt was written. Preserve those exact bytes, then revoke its
            // Complete flag before throwing. No live process is represented by
            // these evidence handles, and no result is transferred on failure.
            string receipt = Path.Combine(destination, "receipt.json");
            if (complete && File.Exists(receipt))
            {
                held.Dispose();
                verifier?.Dispose();
                try
                {
                    string previous = Path.Combine(destination, "receipt-before-late-failure.json");
                    File.Copy(receipt, previous);
                    var failed = JsonNode.Parse(await File.ReadAllTextAsync(previous, CancellationToken.None))!.AsObject();
                    failed["Complete"] = false;
                    failed["Failure"] = error is ToolFailure tool ? tool.Reason : error.GetType().Name;
                    failed["LateEvidenceFailure"] = true;
                    failed["PreviousReceipt"] = Path.GetFileName(previous);
                    failed["PreviousReceiptSha256"] = NativeAotWebPayload.HashFile(previous);
                    await File.WriteAllTextAsync(receipt, failed.ToJsonString(), CancellationToken.None);
                }
                catch (Exception retainedFailure)
                {
                    throw new AggregateException(error, retainedFailure);
                }
            }

            throw;
        }
        finally
        {
            recipeLease?.Dispose();
            sourceLease?.Dispose();
            if (!transferred)
            {
                held.Dispose();
                verifier?.Dispose();
            }
        }

        string[] OutputPaths() => Directory.GetFiles(destination, "*", SearchOption.AllDirectories).Where(path => Path.GetFileName(path)is not ("ownership.lock" or "publication.lock")).Order(StringComparer.Ordinal).ToArray();
        void HoldNative(string path, string hash, bool allowEmpty = false)
        {
            Need(CanonicalLinux(path) && IsHash(hash) && (native.Count < 16384 || native.ContainsKey(path)), "native_identity");
            Need(!native.TryGetValue(path, out string? prior) || prior.Equals(hash, StringComparison.OrdinalIgnoreCase), "native_identity_changed");
            RetainCurrentNativeInput(held, path, hash, allowEmpty);
            native[path] = hash;
        }

        void CheckInputs()
        {
            RequireCurrentSources(root);
            NativeAotLogUploadContract.RequireCurrentSources(root);
            Need(ToolingBuildInputs.Validate(root).Count == 0 && before.OrderBy(row => row.Key, StringComparer.Ordinal).SequenceEqual(Sources().OrderBy(row => row.Key, StringComparer.Ordinal)), "linux_sources_changed");
            verifier?.VerifyUnchanged();
            held.VerifyUnchanged();
        }

        void ObserveDescriptors(string phase)
        {
            Need(fdBoundaries.Count < 32, "fd_observation_capacity");
            int? count = null;
            string? censusFailure = null;
            try
            {
                int observed = 0;
                foreach (string _ in Directory.EnumerateFileSystemEntries("/proc/self/fd"))
                    if (++observed > 65536)
                        throw new IOException("fd_census_capacity");
                count = observed;
            }
            catch (Exception error)when (error is IOException or UnauthorizedAccessException)
            {
                censusFailure = error.GetType().Name;
            }

            // A diagnostic census must not prevent failure receipts or group
            // cleanup when the original resource failure was descriptor exhaustion.
            fdBoundaries.Add(new { Phase = phase, ObservedOpenFileDescriptors = count, NativeInputPaths = native.Count, NestedConsoleArtifacts = verifier?.Artifacts.Count ?? 0, CensusFailure = censusFailure, PeakOrCapacityProof = false });
            output.WriteLine("LOG_UPLOAD_HTTP_FD_BOUNDARY phase=" + phase + " observed=" + (count?.ToString(CultureInfo.InvariantCulture) ?? "unavailable") + " nativeInputs=" + native.Count.ToString(CultureInfo.InvariantCulture) + " peakOrCapacityProof=false");
        }

        string ResolveToolPath(string name)
        {
            string? found = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator).Select(directory => Path.Combine(directory, name)).FirstOrDefault(File.Exists);
            Need(found is not null, "native_tool_missing");
            string path = File.ResolveLinkTarget(found!, true)?.FullName ?? Path.GetFullPath(found!);
            Need(CanonicalLinux(path), "recipe_tool_paths");
            return path;
        }

        LinuxTracePlan TracePlan(string[] arguments) => InspectLinuxTracePlan(originalNative, selectedTools.ToDictionary(tool => tool.Name, StringComparer.Ordinal), arguments);
        Process StartTrace(LinuxTracePlan plan) => Process.Start(plan.PrepareStartInfo(root)) ?? throw new IOException("log_upload_http_linux_process_start");
        NativeAotLogUploadContract.TraceValues ReadTraces(string directory, string phase)
        {
            string[] paths = Directory.GetFiles(directory, phase + "-trace.*").Order(StringComparer.Ordinal).ToArray();
            Need(paths.Length is> 0 and <= 64, "trace_inventory");
            Dictionary<string, string> copied = new(StringComparer.Ordinal);
            long total = 0;
            foreach (string path in paths)
            {
                var file = held.Open(root, path, "original joined trace", "log_upload_http_linux_trace", 4 * 1024 * 1024, singleLink: true);
                Need((total += file.Length) <= 16 * 1024 * 1024, "trace_capacity");
                copied.Add(Path.GetFileName(path), file.ReadUtf8(4 * 1024 * 1024, "log_upload_http_linux_trace"));
            }

            return NativeAotLogUploadContract.InspectTraceValues(copied, phase);
        }

        async Task<(Control Control, NativeAotLogUploadContract.TraceValues? Trace)> Session(string name, string executable)
        {
            CheckInputs();
            cancellation.ThrowIfCancellationRequested();
            string directory = Path.Combine(destination, name), data = Path.Combine(directory, "data");
            Directory.CreateDirectory(data);
            string log = Path.Combine(data, NativeAotLogUploadContract.LogName), upload = Path.Combine(data, NativeAotLogUploadContract.UploadName);
            await File.WriteAllBytesAsync(log, name == "nonempty-log" ? [0x71] : [], cancellation);
            if (name != "missing-upload")
                await File.WriteAllBytesAsync(upload, name == "nonempty-upload" ? [0x72] : [], cancellation);
            await File.WriteAllBytesAsync(Path.Combine(data, "GUARD.BIN"), Guard, cancellation);
            string baseline = Snapshot(data), startup = "";
            int port = ReservePort();
            Dictionary<string, string> identities = new(StringComparer.Ordinal);
            using (AuthorityFiles seeds = new())
            {
                foreach (string path in Directory.GetFiles(data))
                {
                    var file = seeds.Open(root, path, "original HTTP seed", "log_upload_http_linux_seed", 64, allowEmpty: true, singleLink: true);
                    identities.Add(Path.GetFileName(path), file.InitialIdentity.Key);
                }

                seeds.VerifyUnchanged();
                await Save(directory, "initial-files.json", JsonSerializer.Serialize(new { Snapshot = baseline, Identities = identities }));
            }

            string[] arguments = LinuxTraceArguments(executable, data, Path.Combine(directory, "write-trace"), port);
            LinuxTracePlan plan = TracePlan(arguments);
            commands.Add(new { plan.Executable, plan.Arguments, WorkingDirectory = root, Phase = name });
            ObserveDescriptors("before-" + name);
            cancellation.ThrowIfCancellationRequested();
            using var process = StartTrace(plan);
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            deadline.CancelAfter(TimeSpan.FromSeconds(90));
            using CancellationTokenSource monitorStop = new();
            var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Task stdout = Capture(process.StandardOutput, "server.stdout.log", true), stderr = Capture(process.StandardError, "server.stderr.log", false), monitor = Monitor();
            bool natural = false, joined = false, refused = false, succeeded = false, cancelledBody = false;
            int? exit = null;
            string sessionFailure = "";
            TcpClient? heldPartial = null;
            List<ResponseFact> rows = [];
            NativeAotLogUploadContract.TraceValues? trace = null;
            try
            {
                if (name is "missing-upload" or "nonempty-log" or "nonempty-upload")
                {
                    await process.WaitForExitAsync(deadline.Token);
                    await Task.WhenAll(stdout, stderr).WaitAsync(deadline.Token);
                    natural = true;
                    exit = process.ExitCode;
                    Need(exit == 1 && File.ReadAllText(Path.Combine(directory, "server.stderr.log")).Length == 0 && File.ReadAllText(Path.Combine(directory, "server.stdout.log")).Replace("\r\n", "\n", StringComparison.Ordinal) == "TEST_START m10.027.http\nTEST_FAIL m10.027.http " + (name == "missing-upload" ? "FileNotFoundException" : "InvalidOperationException") + "\n" && Snapshot(data) == baseline, "linux_preflight");
                }
                else
                {
                    Need(await Task.WhenAny(ready.Task, process.WaitForExitAsync()).WaitAsync(deadline.Token) == ready.Task && !process.HasExited, "linux_ready");
                    using HttpClient client = Client(port);
                    await WaitPhase(client, "ready", deadline.Token);
                    RequireStartup(data);
                    startup = Snapshot(data);
                    var owner = Observe();
                    Need(Observe() == owner, "linux_process_identity_changed");
                    await Save(directory, "process-owner.json", JsonSerializer.Serialize(new { Identity = owner, Executable = executable, ImageSha256 = image!.Sha256, Arguments = arguments[8..] }));
                    await Save(directory, "pre-request.json", startup);
                    if (name == "cancel-body")
                    {
                        heldPartial = await Partial(port, deadline.Token);
                        await WaitPhase(client, "receiving", deadline.Token);
                        Need(Snapshot(data) == startup && Observe() == owner, "linux_partial_modified");
                        cancelledBody = true;
                    }
                    else
                    {
                        await Request(ResponseKind.Health);
                        await Request(ResponseKind.StartupLog);
                        await Request(ResponseKind.WrongType, new byte[5126], "text/plain");
                        await Request(ResponseKind.WrongLength, [1]);
                        await Request(ResponseKind.Oversized, new byte[5127]);
                        Need(Snapshot(data) == startup, "linux_bad_request_modified");
                        using (var partial = await Partial(port, deadline.Token))
                        {
                            await WaitPhase(client, "receiving", deadline.Token);
                            await Request(ResponseKind.Busy, NativeAotLogUploadContract.ExpectedUpload());
                            Need(Snapshot(data) == startup, "linux_busy_modified");
                        }

                        await WaitPhase(client, "ready", deadline.Token);
                        Need(Snapshot(data) == startup, "linux_abort_modified");
                        await Request(ResponseKind.Accepted, NativeAotLogUploadContract.ExpectedUpload());
                        NativeAotLogUploadContract.InspectFileValues(File.ReadAllBytes(log), File.ReadAllBytes(upload));
                        string accepted = Snapshot(data);
                        await Request(ResponseKind.Busy, NativeAotLogUploadContract.ExpectedUpload());
                        Need(Snapshot(data) == accepted, "linux_duplicate_modified");
                        await Request(ResponseKind.Log);
                        Need(Observe() == owner, "linux_process_identity_changed");
                        await Request(ResponseKind.Upload);
                        await process.WaitForExitAsync(deadline.Token);
                        await Task.WhenAll(stdout, stderr).WaitAsync(deadline.Token);
                        natural = true;
                        exit = process.ExitCode;
                        Need(exit == 0, "linux_server_exit");
                        InspectServerTranscriptValues(File.ReadAllText(Path.Combine(directory, "server.stdout.log")), File.ReadAllText(Path.Combine(directory, "server.stderr.log")));
                        Need(File.ReadAllBytes(Path.Combine(data, "GUARD.BIN")).AsSpan().SequenceEqual(Guard), "guard_changed");
                        succeeded = true;
                    }

                    async Task Request(ResponseKind kind, byte[]? bytes = null, string type = "application/octet-stream")
                    {
                        string method = kind is ResponseKind.Health or ResponseKind.StartupLog or ResponseKind.Log or ResponseKind.Upload ? "GET" : "POST";
                        string target = kind == ResponseKind.Health ? "/healthz" : kind is ResponseKind.StartupLog or ResponseKind.Log ? "/log" : "/upload";
                        using var request = new HttpRequestMessage(new HttpMethod(method), target);
                        if (bytes is not null)
                        {
                            request.Content = new ByteArrayContent(bytes);
                            request.Content.Headers.ContentType = new(type);
                        }

                        string prefix = (rows.Count + 1).ToString("D2", CultureInfo.InvariantCulture);
                        if (bytes is not null)
                            await File.WriteAllBytesAsync(Path.Combine(directory, prefix + ".request-body"), bytes, CancellationToken.None);
                        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
                        byte[] body = await ReadBounded(response, deadline.Token);
                        await File.WriteAllBytesAsync(Path.Combine(directory, prefix + ".body"), body, CancellationToken.None);
                        string contentType = response.Content.Headers.ContentType?.ToString() ?? "";
                        await Save(directory, prefix + ".response.json", JsonSerializer.Serialize(new { ExpectedKind = kind, Method = method, Target = target, Status = (int)response.StatusCode, ContentType = contentType, Bytes = body.Length, Sha256 = Hash(body), CapturedBeforeValueValidation = true }));
                        rows.Add(InspectResponseValues(kind, method, target, (int)response.StatusCode, contentType, body));
                    }
                }
            }
            catch (Exception error)
            {
                sessionFailure = error is ToolFailure tool ? tool.Reason : error.GetType().Name;
                throw;
            }
            finally
            {
                Exception? cleanup = null;
                try
                {
                    await NativeAotWebPayload.StopSessionAsync(process);
                    joined = true;
                    exit = process.ExitCode;
                }
                catch (Exception error)
                {
                    cleanup = error;
                }

                monitorStop.Cancel();
                try
                {
                    await monitor;
                }
                catch (OperationCanceledException)when (monitorStop.IsCancellationRequested)
                {
                }
                catch (Exception error)
                {
                    cleanup = cleanup is null ? error : new AggregateException(cleanup, error);
                }

                try
                {
                    await Task.WhenAll(stdout, stderr).WaitAsync(TimeSpan.FromSeconds(10));
                }
                catch (Exception error)
                {
                    cleanup = cleanup is null ? error : new AggregateException(cleanup, error);
                }

                heldPartial?.Dispose();
                ObserveDescriptors("after-" + name + "-join");
                try
                {
                    await Refused(port, CancellationToken.None);
                    refused = true;
                }
                catch (Exception error)
                {
                    cleanup = cleanup is null ? error : new AggregateException(cleanup, error);
                }

                string actual = "", snapshotFailure = "", projectionFailure = "";
                try
                {
                    actual = Snapshot(data);
                    using AuthorityFiles final = new();
                    Need(Directory.GetFiles(data).Length == identities.Count, "linux_file_identity_changed");
                    foreach (var seed in identities)
                        Need(final.Open(root, Path.Combine(data, seed.Key), "original HTTP final file", "log_upload_http_linux_final", 8192, allowEmpty: true, singleLink: true).InitialIdentity.Key == seed.Value, "linux_file_identity_changed");
                    final.VerifyUnchanged();
                }
                catch (Exception error)
                {
                    snapshotFailure = error.Message;
                }

                try
                {
                    trace = ReadTraces(directory, "write");
                }
                catch (Exception error)
                {
                    projectionFailure = error.Message;
                }

                if (succeeded && (trace is null || trace.ExitedFiles != trace.Files || !trace.Rows.Any(row => row.Syscall == "execve") || !trace.Rows.Any(row => row.Syscall == "exit_group")))
                    cleanup = cleanup is null ? new ToolFailure("log_upload_http_linux_trace_exit") : cleanup;
                if (trace is not null && natural)
                    try
                    {
                        InspectLinuxExecveValues(trace, executable, ["--serve-once", "--root", data, "--urls", "http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture)]);
                    }
                    catch (Exception error)
                    {
                        cleanup = cleanup is null ? error : new AggregateException(cleanup, error);
                    }

                await Save(directory, "http.json", JsonSerializer.Serialize(new { Schema = "CLROS_LOG_UPLOAD_HTTP_LINUX_SESSION_V1", Name = name, Complete = succeeded && cleanup is null && snapshotFailure.Length == 0, ExpectedFailure = name != "positive", Failure = sessionFailure, CleanupFailure = cleanup?.Message, NaturalExitObserved = natural, Joined = joined, EndpointRefused = refused, ExitCode = exit, ControlledBodyCancellation = cancelledBody, CallerCancellationRequested = cancellation.IsCancellationRequested, WaitCancelled = deadline.IsCancellationRequested, Port = port, Responses = rows, BeforeFilesJson = baseline, PreRequestFilesJson = startup, ActualFilesJson = actual, SnapshotFailure = snapshotFailure, Trace = trace, TraceProjectionFailure = projectionFailure, DeviceFlushObserved = false, GuestQualified = false, PhysicalPersistenceQualified = false }));
                if (cleanup is not null)
                    throw cleanup;
                Need(snapshotFailure.Length == 0, "linux_snapshot_failed");
            }

            Need(joined && refused && (!cancelledBody || Snapshot(data) == startup), "linux_session_cleanup");
            if (name == "positive")
                responses.AddRange(rows);
            return (new(name, name != "positive", joined, cancelledBody, exit!.Value, refused), trace);
            NativeAotSqliteContract.LinuxRecoveryIdentity Observe()
            {
                string ReadProc(string path)
                {
                    using FileStream file = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using MemoryStream bytes = new();
                    byte[] buffer = new byte[4096];
                    for (int read; (read = file.Read(buffer)) != 0;)
                    {
                        Need(bytes.Length + read <= 65536, "linux_proc_capacity");
                        bytes.Write(buffer, 0, read);
                    }

                    return new UTF8Encoding(false, true).GetString(bytes.ToArray());
                }

                string children = ReadProc($"/proc/{process.Id}/task/{process.Id}/children");
                string[] ids = children.Split((char[]? )null, StringSplitOptions.RemoveEmptyEntries);
                Need(ids.Length == 1 && ids[0].All(char.IsAsciiDigit), "linux_proc_children");
                string child = ids[0];
                string commandLine = ReadProc($"/proc/{child}/cmdline");
                Need(File.ResolveLinkTarget($"/proc/{process.Id}/exe", true)?.FullName == strace && commandLine.EndsWith('\0') && commandLine[..^1].Split('\0').SequenceEqual(new[] { executable }.Concat(arguments[8..])), "linux_process_arguments");
                // Reuse this existing copied proc/session parser only. It has
                // no SQLite semantics and grants no process/signal authority.
                return NativeAotSqliteContract.InspectLinuxRecoveryIdentityValues(process.Id, ReadProc($"/proc/{process.Id}/stat"), children, ReadProc($"/proc/{child}/stat"), ReadProc($"/proc/{child}/status"), File.ResolveLinkTarget($"/proc/{child}/exe", true)?.FullName ?? "", executable);
            }

            async Task Capture(StreamReader reader, string name, bool standardOutput)
            {
                try
                {
                    using FileStream logFile = new(Path.Combine(directory, name), FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                    char[] buffer = new char[1024];
                    StringBuilder text = new();
                    for (int count; (count = await reader.ReadAsync(buffer)) != 0;)
                    {
                        text.Append(buffer, 0, count);
                        byte[] bytes = Encoding.UTF8.GetBytes(buffer.AsSpan(0, count).ToString());
                        Need(logFile.Length + bytes.Length <= MaximumTranscriptBytes, "linux_console_capacity");
                        await logFile.WriteAsync(bytes);
                        await logFile.FlushAsync();
                        if (standardOutput && text.ToString().Replace("\r\n", "\n", StringComparison.Ordinal).Contains(Ready, StringComparison.Ordinal))
                            ready.TrySetResult();
                    }
                }
                catch
                {
                    deadline.Cancel();
                    throw;
                }
            }

            async Task Monitor()
            {
                try
                {
                    while (true)
                    {
                        string[] paths = Directory.GetFiles(directory, "write-trace.*");
                        Need(paths.Length <= 64, "linux_trace_capacity");
                        long bytes = 0;
                        foreach (string path in paths)
                        {
                            long length = new FileInfo(path).Length;
                            Need(length <= 4 * 1024 * 1024 && (bytes += length) <= 16 * 1024 * 1024, "linux_trace_capacity");
                        }

                        await Task.Delay(100, monitorStop.Token);
                    }
                }
                catch (OperationCanceledException)when (monitorStop.IsCancellationRequested)
                {
                }
                catch
                {
                    deadline.Cancel();
                    throw;
                }
            }
        }
    }

    private static HttpClient Client(int port) => new(new SocketsHttpHandler { AllowAutoRedirect = false, AutomaticDecompression = DecompressionMethods.None, UseProxy = false, MaxConnectionsPerServer = 2 })
    {
        BaseAddress = new Uri("http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture)),
        Timeout = TimeSpan.FromSeconds(10),
        DefaultRequestVersion = HttpVersion.Version11,
        DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact
    };
    private static ByteArrayContent Content(byte[] bytes)
    {
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new("application/octet-stream");
        return content;
    }

    private static int ReservePort()
    {
        using TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static async Task<TcpClient> Partial(int port, CancellationToken cancellation)
    {
        TcpClient client = new();
        try
        {
            await client.ConnectAsync(IPAddress.Loopback, port, cancellation);
            byte[] headers = Encoding.ASCII.GetBytes("POST /upload HTTP/1.1\r\nHost: 127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture) + "\r\nContent-Type: application/octet-stream\r\nContent-Length: 5126\r\nConnection: close\r\n\r\n");
            await client.GetStream().WriteAsync(headers, cancellation);
            await client.GetStream().WriteAsync(new byte[31], cancellation);
            return client;
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    private static async Task WaitPhase(HttpClient client, string expected, CancellationToken cancellation)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));
        while (true)
        {
            using var response = await client.GetAsync("/healthz", deadline.Token);
            byte[] bytes = await ReadBounded(response, deadline.Token);
            InspectResponseValues(ResponseKind.Health, "GET", "/healthz", (int)response.StatusCode, response.Content.Headers.ContentType?.ToString() ?? "", bytes);
            Need(response.Headers.TryGetValues("X-LogUpload-Phase", out var phases) && phases.Count() == 1, "phase_header");
            if (phases!.Single() == expected)
                return;
            await Task.Delay(10, deadline.Token);
        }
    }

    private static async Task<byte[]> ReadBounded(HttpResponseMessage response, CancellationToken cancellation)
    {
        Need(response.Version == HttpVersion.Version11 && response.Headers.Count() + response.Content.Headers.Count() <= 32, "http_envelope");
        int characters = 0;
        foreach (var header in response.Headers.Concat(response.Content.Headers))
        {
            string line = header.Key + ": " + string.Join(",", header.Value);
            Need(line.Length <= 1024 && line.All(c => c is >= ' ' and <= '~') && (characters += line.Length) <= 4096, "http_envelope");
        }

        Need(response.Content.Headers.ContentLength is >= 0 and <= 5126 && response.Headers.TransferEncoding.Count == 0, "http_envelope");
        using Stream stream = await response.Content.ReadAsStreamAsync(cancellation);
        using MemoryStream bytes = new();
        byte[] buffer = new byte[1024];
        for (int read; (read = await stream.ReadAsync(buffer, cancellation)) != 0;)
        {
            Need(bytes.Length + read <= 5126, "response_capacity");
            bytes.Write(buffer, 0, read);
        }

        Need(bytes.Length == response.Content.Headers.ContentLength, "http_envelope");
        return bytes.ToArray();
    }

    private static async Task Refused(int port, CancellationToken cancellation)
    {
        using Socket socket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));
        bool refused = false;
        try
        {
            await socket.ConnectAsync(IPAddress.Loopback, port, deadline.Token);
        }
        catch (SocketException error)when (error.SocketErrorCode == SocketError.ConnectionRefused)
        {
            refused = true;
        }

        Need(refused, "stopped_endpoint_live");
    }

    private static void RequireStartup(string data)
    {
        Need(File.ReadAllBytes(Path.Combine(data, NativeAotLogUploadContract.LogName)).AsSpan().SequenceEqual(NativeAotLogUploadContract.ExpectedLog().AsSpan(0, StartupLogBytes)) && new FileInfo(Path.Combine(data, NativeAotLogUploadContract.UploadName)).Length == 0 && File.ReadAllBytes(Path.Combine(data, "GUARD.BIN")).AsSpan().SequenceEqual(Guard), "startup_files");
    }

    private static string Snapshot(string data)
    {
        string[] names = Directory.GetFiles(data).Select(Path.GetFileName).Cast<string>().Order(StringComparer.Ordinal).ToArray();
        Need(names.All(name => name is "LOG.TXT" or "UPLOAD.BIN" or "GUARD.BIN") && names.Length <= 3, "data_inventory");
        using AuthorityFiles held = new();
        List<object> records = [];
        foreach (string name in names)
        {
            var file = held.Open(data, Path.Combine(data, name), "managed HTTP data snapshot", "log_upload_http_snapshot", 8192, allowEmpty: true, singleLink: true);
            records.Add(new { Name = name, file.Length, file.Sha256 });
        }

        held.VerifyUnchanged();
        return JsonSerializer.Serialize(records);
    }

    private static async Task Save(string directory, string name, string contents)
    {
        using FileStream output = new(Path.Combine(directory, name), FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        byte[] bytes = Encoding.UTF8.GetBytes(contents);
        await output.WriteAsync(bytes, CancellationToken.None);
        output.Flush(flushToDisk: true);
    }

    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static bool IsHash(string? hash) => hash is { Length: 64 } && hash.All(Uri.IsHexDigit);
    private static bool SafeRelative(string path) => path.Length is> 0 and <= 4096 && path[0] != '/' && !path.Any(c => c is ':' or '\\' || char.IsControl(c)) && path.Split('/').All(part => part is not ("" or "." or ".."));
    private static bool CanonicalLinux(string path) => path.Length is> 1 and <= 4096 && path[0] == '/' && path.All(c => c is >= ' ' and <= '~' && c is not ('"' or '\\')) && path[1..].Split('/').All(part => part is not ("" or "." or ".."));
    private static void Need(bool value, string reason)
    {
        if (!value)
            throw new ToolFailure("log_upload_http_" + reason);
    }
}
