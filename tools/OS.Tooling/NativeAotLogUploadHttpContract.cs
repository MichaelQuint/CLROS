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
        Need(port is > 0 and <= 65535, "linux_port");
        Need(new[] { executable, data, tracePrefix }.All(CanonicalLinux), "linux_path");
        // Reuse the unchanged canonical Linux path checks and trace flags.
        string[] prior = NativeAotLogUploadContract.LinuxTraceArguments(executable, data, tracePrefix, "write");
        return [.. prior[..8], "--serve-once", "--root", data, "--urls", "http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture)];
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
        Need(rows.Length is > 0 and <= 16384 && rows.Select(row => row.Name).Distinct(StringComparer.Ordinal).Count() == rows.Length && rows.All(row => row.Value.ValueKind == JsonValueKind.String && IsHash(row.Value.GetString())), "recipe_native_map");
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
        Need(traceArguments.Count is > 0 and <= 32, "session_arguments");
        string[] copied = new string[traceArguments.Count];
        for (int i = 0; i < copied.Length; i++)
            copied[i] = traceArguments[i];
        Need(copied.All(argument => argument is not null && argument.Length is > 0 and <= 4096 && !argument.Any(char.IsControl)) && copied.Sum(argument => Encoding.UTF8.GetByteCount(argument)) <= 32768, "session_arguments");
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
        Need(json.Length <= 16 * 1024 * 1024 && Encoding.UTF8.GetByteCount(json) <= 16 * 1024 * 1024 && expectedSources.Count is > 0 and <= 8192, "publication_capacity");
        using var document = JsonDocument.Parse(json);
        var value = document.RootElement;
        var fields = value.EnumerateObject().ToArray();
        Need(fields.Select(row => row.Name).Distinct(StringComparer.Ordinal).Count() == fields.Length && value.GetProperty("schema").GetString() == "clros.nativeaot.socket-log-upload-http.publication.v1" && value.GetProperty("succeeded").GetBoolean() && value.GetProperty("failure").ValueKind == JsonValueKind.Null && !value.GetProperty("appExecuted").GetBoolean() && !value.GetProperty("guestExecuted").GetBoolean() && value.GetProperty("application").GetString() == Application && value.GetProperty("sdk").GetString() == NativeAotPayload.SdkVersion && value.GetProperty("runtime").GetString() == NativeAotPayload.RuntimeVersion, "publication_policy");
        var sources = value.GetProperty("sources").EnumerateObject().ToArray();
        Need(sources.Length == expectedSources.Count && sources.Select(row => row.Name).Distinct(StringComparer.Ordinal).Count() == sources.Length && sources.All(row => SafeRelative(row.Name) && expectedSources.TryGetValue(row.Name, out string? hash) && IsHash(hash) && string.Equals(hash, row.Value.GetString(), StringComparison.OrdinalIgnoreCase)), "publication_sources");
        var native = value.GetProperty("native").EnumerateObject().ToArray();
        var outputs = value.GetProperty("outputs").EnumerateObject().ToArray();
        Need(native.Length is > 0 and <= 16384 && native.Select(row => row.Name).Distinct(StringComparer.Ordinal).Count() == native.Length && native.All(row => CanonicalLinux(row.Name) && IsHash(row.Value.GetString())), "publication_native");
        Need(outputs.Length is > 0 and <= 512 && outputs.Select(row => row.Name).Distinct(StringComparer.Ordinal).Count() == outputs.Length && outputs.All(row => SafeRelative(row.Name) && IsHash(row.Value.GetString())), "publication_outputs");
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
            _ => throw new ToolFailure("log_upload_http_response_kind")
        };
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
        catch (SocketException error) when (error.SocketErrorCode == SocketError.ConnectionRefused)
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
            records.Add(new
            {
                Name = name,
                file.Length,
                file.Sha256
            });
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
    private static bool SafeRelative(string path) => path.Length is > 0 and <= 4096 && path[0] != '/' && !path.Any(c => c is ':' or '\\' || char.IsControl(c)) && path.Split('/').All(part => part is not ("" or "." or ".."));
    private static bool CanonicalLinux(string path) => path.Length is > 1 and <= 4096 && path[0] == '/' && path.All(c => c is >= ' ' and <= '~' && c is not ('"' or '\\')) && path[1..].Split('/').All(part => part is not ("" or "." or ".."));
    private static void Need(bool value, string reason)
    {
        if (!value)
            throw new ToolFailure("log_upload_http_" + reason);
    }
}
