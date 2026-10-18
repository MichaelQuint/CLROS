using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Inspection = OS.Tooling.NativeAotPayload.Inspection;

namespace OS.Tooling;

/// <summary>Linux-only web evidence extension around the unchanged, pinned
/// three-app publisher. Owns its extra HTTP child sessions and a sealed receipt;
/// publication remains exclusively NativeAotPayload's recipe. No CLROS guest
/// authority follows from this builder or its read-only receipt validation.</summary>
public static class NativeAotWebPayload
{
    private const string SdkVersion = NativeAotPayload.SdkVersion;
    private const string RuntimeVersion = NativeAotPayload.RuntimeVersion;
    private const string DefaultWebBody = "OS_MINIMAL_API_OK";
    private const string LoggingBody = "CLROS_LOG_OK workers=8 records=64 writev=32 invalid_errno=9";
    private static readonly string[] PayloadApps = ["NativeAotHello", "NativeAotRuntimeSmoke", "MinimalApiSmoke"];
    public sealed record QualificationReceipt(string Schema, string Sdk, string Runtime,
        IReadOnlyDictionary<string, string> Sources, IReadOnlyDictionary<string, string> Outputs);

    public static async Task<int> RunAsync(string root, string[] args, TextWriter output)
    {
        string marker = args.FirstOrDefault() switch { "validate" => "nativeaot.web.validate",
            "package" => "nativeaot.web.package", _ => "nativeaot.web" };
        output.WriteLine("TEST_START " + marker);
        try
        {
            if (args is ["qualify", "--output", var destination])
                await QualifyAsync(Path.GetFullPath(root), Path.GetFullPath(destination), output);
            else if (args is ["validate", var evidence])
                ValidateQualification(Path.GetFullPath(root), Path.GetFullPath(evidence));
            else if (args is ["package", "--recipe-root", var recipeRoot, "--recipe-evidence", var recipeEvidence,
                "--output", var package])
                output.WriteLine("NATIVEAOT_WEB_PACKAGE sha256=" + NativeAotWebPackage.Write(root, recipeRoot, recipeEvidence, package));
            else throw new ArgumentException("nativeaot web qualify --output <fresh-directory> | validate <evidence-directory> | package --recipe-root <source> --recipe-evidence <evidence> --output <fresh-package>");
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
        Require(OperatingSystem.IsLinux(), "linux_builder_required");
        int result = await NativeAotPayload.RunAsync(root, ["qualify", "--output", destination], output);
        Require(result == 0, "native_payload_qualification_failed");
        using var lease = NativeAotPayload.OpenQualificationLease(root);
        Require(ToolingBuildInputs.Validate(root).Count == 0, "tooling_inputs_changed");
        var sources = JsonSerializer.Deserialize<Dictionary<string, string>>(
            await File.ReadAllTextAsync(Path.Combine(destination, "inputs.json")));
        Require(sources is not null && SourcePaths(root).SequenceEqual(sources.Keys.Order()) &&
            sources.All(row => HashFile(Path.Combine(root, row.Key)) == row.Value), "source_inputs_changed");
        var commands = new List<object>();
        try
        {
            string executable = Path.Combine(destination, "MinimalApiSmoke", "MinimalApiSmoke");
            await RunHttpAsync(root, destination, executable, commands, configured: false);
            await RunHttpAsync(root, destination, executable, commands, configured: true);
        }
        finally
        {
            await File.WriteAllTextAsync(Path.Combine(destination, "web-commands.json"), JsonSerializer.Serialize(commands));
        }
        var receipt = new QualificationReceipt("clros.nativeaot.web.v2", SdkVersion, RuntimeVersion,
            sources, OutputHashes(destination));
        await File.WriteAllTextAsync(Path.Combine(destination, "web-receipt.json"), JsonSerializer.Serialize(receipt));
        ValidateQualification(root, destination);
    }

    private static Inspection InspectFile(string path)
    {
        Require(new FileInfo(path).Length <= NativeAotPayload.MaximumImageBytes, "elf_image_size");
        return NativeAotPayload.Inspect(File.ReadAllBytes(path));
    }

    /// <summary>
    /// Rechecks this qualifier's sealed source/output inventory and observed app results.
    /// The supplied repository must contain the original source bytes. Missing, changed,
    /// redirected or unexpected evidence fails closed; copied/synthetic receipts grant no
    /// execution authority. This does not reexecute a payload or qualify a CLROS guest.
    /// </summary>
    public static void ValidateQualification(string root, string destination)
    {
        AuthorityFiles.RequirePlainPath(destination, destination, "native_evidence_path");
        string receiptPath = Path.Combine(destination, "web-receipt.json");
        AuthorityFiles.RequirePlainPath(destination, receiptPath, "native_evidence_path");
        Require(new FileInfo(receiptPath).Length is > 0 and <= 4 * 1024 * 1024, "native_receipt_size");
        QualificationReceipt receipt = JsonSerializer.Deserialize<QualificationReceipt>(File.ReadAllText(receiptPath))
            ?? throw new InvalidDataException("native_receipt_missing");
        Require(receipt.Schema == "clros.nativeaot.web.v2" && receipt.Sdk == SdkVersion &&
            receipt.Runtime == RuntimeVersion, "native_receipt_pin");
        Require(receipt.Sources is not null && receipt.Outputs is not null &&
            receipt.Sources.Count is > 0 and <= 10000 && receipt.Outputs.Count is > 0 and <= 10000,
            "native_receipt_inventory");
        string[] inputs = SourcePaths(root);
        Require(inputs.SequenceEqual(receipt.Sources.Keys.Order()), "source_inputs_changed");
        foreach (string path in inputs)
        {
            AuthorityFiles.RequirePlainPath(root, Path.Combine(root, path), "native_source_path");
            Require(HashFile(Path.Combine(root, path)) == receipt.Sources[path], "source_inputs_changed");
        }
        Dictionary<string, string> outputs = OutputHashes(destination);
        Require(outputs.OrderBy(row => row.Key).SequenceEqual(receipt.Outputs.OrderBy(row => row.Key)),
            "native_evidence_changed");
        var nativeInputs = JsonSerializer.Deserialize<Dictionary<string, string>>(
            File.ReadAllText(Path.Combine(destination, "native-inputs.json")));
        Require(nativeInputs is not null && nativeInputs.Count > 0 &&
            nativeInputs.All(row => Path.IsPathFullyQualified(row.Key) && File.Exists(row.Key) &&
                HashFile(row.Key) == row.Value), "native_inputs_changed");
        using JsonDocument inspections = JsonDocument.Parse(File.ReadAllText(Path.Combine(destination, "inspection.json")));
        foreach (string app in PayloadApps)
        {
            Inspection actual = InspectFile(Path.Combine(destination, app, app));
            Require(actual.Sha256 == inspections.RootElement.GetProperty(app).GetProperty("Sha256").GetString(),
                "native_payload_changed");
            if (app != "MinimalApiSmoke")
                ValidateRunOutput(app, File.ReadAllText(Path.Combine(destination, app + "-run.stdout.log")));
        }
        ValidateHttpEvidence(File.ReadAllText(Path.Combine(destination, "web-http-default.json")), DefaultWebBody);
        using JsonDocument configured = JsonDocument.Parse(File.ReadAllText(Path.Combine(destination, "web-http.json")));
        string configuredBody = configured.RootElement.GetProperty("body").GetString()!;
        Require(configuredBody.StartsWith(DefaultWebBody + "_", StringComparison.Ordinal) &&
            configuredBody.Length == DefaultWebBody.Length + 33, "http_config_body");
        ValidateHttpEvidence(configured.RootElement.GetRawText(), configuredBody);
        ValidateHttpTraces(destination, "WebMinimalApiSmoke-trace");
        ValidateHttpTraces(destination, "WebMinimalApiSmoke-default-trace");
        foreach (string suffix in new[] { "", "-default" }) ValidateRecordedLogging(destination, suffix);
    }

    /// <summary>
    /// Host-only grammar checks over borrowed Linux output/strace bytes. Requires
    /// complete constructor observations, both log streams, eight distinct native
    /// writer traces and the actual invalid-fd errno. Starts no process and grants
    /// no publication, native-code or CLROS guest authority.
    /// </summary>
    public static void ValidateLoggingEvidence(string stdout, string stderr, IReadOnlyDictionary<string, string> traces)
    {
        Require(stdout.Length <= 4 * 1024 * 1024 && stderr.Length <= 1024 * 1024 &&
            traces.Count is > 0 and <= 128 && traces.All(t => t.Value.Length <= 16 * 1024 * 1024), "logging_size");
        string[] startup = ["CLROS_APP_ENTRY minimal.api.smoke", "CLROS_APP_CCTOR minimal.api.smoke",
            "CLROS_APP_CTOR minimal.api.smoke", "CLROS_APP_BUILT minimal.api.smoke"];
        Require(!stderr.Contains('\r') && stderr.EndsWith('\n'), "logging_stderr_framing");
        string[] errors = stderr[..^1].Split('\n');
        Require(errors.Length == 36 && errors.Take(4).SequenceEqual(startup), "logging_startup");
        Require(!stdout.Contains('\r') && stdout.EndsWith('\n') &&
            !stdout.Split('\n').Any(line => line.StartsWith("TEST_FAIL", StringComparison.Ordinal) ||
                line.StartsWith("CLROS_APP_FAIL", StringComparison.Ordinal)), "logging_stdout_framing");
        string[] Catalog(uint fd) => Enumerable.Range(0, 8).SelectMany(worker => Enumerable.Range(0, 4)
            .Select(record => $"CLROS_LOG fd={fd} worker={worker} record={record}")).Order(StringComparer.Ordinal).ToArray();
        string[] outputRows = stdout.Split('\n').Where(line => line.StartsWith("CLROS_LOG", StringComparison.Ordinal)).ToArray();
        Require(outputRows.Order(StringComparer.Ordinal).SequenceEqual(Catalog(1)) &&
            errors.Skip(4).Order(StringComparer.Ordinal).SequenceEqual(Catalog(2)), "logging_records");
        HashSet<(int Worker, int Record)> seen = []; Dictionary<int, string> writers = []; int invalid = 0;
        foreach (var trace in traces) foreach (string row in trace.Value.Split('\n'))
        {
            if (row.StartsWith("writev(1234,", StringComparison.Ordinal))
            { Require(row.Contains(" = -1 EBADF ", StringComparison.Ordinal), "logging_invalid_fd"); invalid++; }
            if (!row.StartsWith("writev(2,", StringComparison.Ordinal) || !row.Contains("CLROS_LOG", StringComparison.Ordinal)) continue;
            Require(row.Length <= 1024, "logging_writev_size");
            Match match = Regex.Match(row, @"^writev\(2,.*CLROS_LOG fd=2 worker=([0-7]) record=([0-3]).* = ([1-9][0-9]*)$", RegexOptions.CultureInvariant);
            Require(match.Success, "logging_writev");
            int worker = int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            int record = int.Parse(match.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
            Require(int.TryParse(match.Groups[3].Value, out int bytes) &&
                bytes == Encoding.ASCII.GetByteCount($"CLROS_LOG fd=2 worker={worker} record={record}\n") &&
                seen.Add((worker, record)), "logging_writev_count");
            Require(!writers.TryGetValue(worker, out string? owner) || owner == trace.Key, "logging_writer_owner");
            writers[worker] = trace.Key;
        }
        Require(invalid == 1 && seen.Count == 32 && writers.Count == 8 &&
            writers.Values.Distinct(StringComparer.Ordinal).Count() == 8, "logging_trace_inventory");
    }

    private static void ValidateRecordedLogging(string directory, string suffix)
    {
        using JsonDocument receipt = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "web-logging" + suffix + ".json")));
        var row = receipt.RootElement;
        Require(row.GetProperty("status").GetInt32() == 200 && row.GetProperty("body").GetString() == LoggingBody &&
            row.GetProperty("repeatStatus").GetInt32() == 409 && row.GetProperty("healthAfter").GetInt32() == 200,
            "logging_http");
        string prefix = "WebMinimalApiSmoke" + suffix;
        var traces = Directory.GetFiles(directory, prefix + "-trace.*")
            .ToDictionary(path => Path.GetFileName(path)!, File.ReadAllText, StringComparer.Ordinal);
        ValidateLoggingEvidence(File.ReadAllText(Path.Combine(directory, prefix + "-run.stdout.log")),
            File.ReadAllText(Path.Combine(directory, prefix + "-run.stderr.log")), traces);
    }

    /// <summary>Validates complete observed stdout from the two finite original apps.
    /// Exact start/pass order and absence of failure markers are required; arbitrary
    /// app names and malformed transcripts fail. Text alone is not execution evidence.</summary>
    public static void ValidateRunOutput(string app, string text)
    {
        string marker = app switch { "NativeAotHello" => "nativeaot.hello",
            "NativeAotRuntimeSmoke" => "nativeaot.runtime", _ => throw new InvalidDataException("native_payload_app") };
        string[] lines = text.Replace("\r", "").Split('\n');
        Require(!lines.Any(line => line.StartsWith("TEST_FAIL", StringComparison.Ordinal)) &&
            lines.Count(line => line == "TEST_START " + marker) == 1 &&
            lines.Count(line => line == "TEST_PASS " + marker) == 1 &&
            Array.IndexOf(lines, "TEST_START " + marker) < Array.IndexOf(lines, "TEST_PASS " + marker),
            "payload_run_marker");
    }

    /// <summary>Checks recorded HTTP/config observations, including exact body bytes,
    /// loopback endpoint, three paths and matching app/config identity. Malformed or
    /// incomplete data throws; this parser does not make a network or guest claim.</summary>
    public static void ValidateHttpEvidence(string json, string expectedBody)
    {
        Require(expectedBody.Length is > 0 and <= 256 && json.Length <= 16384, "http_evidence_size");
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement row = document.RootElement;
        Require(Uri.TryCreate(row.GetProperty("endpoint").GetString(), UriKind.Absolute, out Uri? endpoint) &&
            endpoint.Scheme == "http" && endpoint.Host == "127.0.0.1" && endpoint.Port is > 0 and <= 65535 &&
            endpoint.AbsolutePath == "/" && endpoint.Query.Length == 0 && endpoint.Fragment.Length == 0 &&
            endpoint.UserInfo.Length == 0, "http_endpoint");
        Require(row.GetProperty("paths").EnumerateArray().Select(value => value.GetString())
            .SequenceEqual(new[] { "/", "/healthz", "/config" }) && row.GetProperty("status").GetInt32() == 200 &&
            row.GetProperty("body").GetString() == expectedBody &&
            row.GetProperty("appId").GetString() == "minimal.api.smoke" &&
            row.GetProperty("configBody").GetString() == expectedBody &&
            row.GetProperty("bodyBytes").GetInt32() == Encoding.UTF8.GetByteCount(expectedBody), "http_status_or_body");
    }

    private static string[] SourcePaths(string root) => PayloadApps
        .SelectMany(app => Directory.EnumerateFiles(Path.Combine(root, "apps", app), "*", SearchOption.AllDirectories))
        .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
        .Where(path => !path.Split('/').Any(part => part is "bin" or "obj" or "artifacts"))
        .Concat(ToolingBuildInputs.SourceInputs(root).Select(path => Path.GetRelativePath(root, path).Replace('\\', '/')))
        .Concat(new[] { "NuGet.Config", "nuget.config" }.Where(path => File.Exists(Path.Combine(root, path))))
        .Distinct().Order().ToArray();

    private static Dictionary<string, string> OutputHashes(string destination)
    {
        var hashes = new Dictionary<string, string>(StringComparer.Ordinal);
        Stack<string> pending = new();
        pending.Push(destination);
        while (pending.TryPop(out string? directory))
        foreach (string file in Directory.EnumerateFileSystemEntries(directory))
        {
            string path = Path.GetRelativePath(destination, file).Replace('\\', '/');
            if (path is "web-receipt.json" or "qualification.lock" or "retained-build-cache") continue;
            AuthorityFiles.RequirePlainPath(destination, file, "native_evidence_path");
            if (Directory.Exists(file)) { pending.Push(file); continue; }
            Require(hashes.Count < 10000, "native_evidence_inventory");
            hashes.Add(path, HashFile(file));
        }
        return hashes;
    }

    private static void ValidateHttpTraces(string destination, string prefix)
    {
        string[] traces = Directory.GetFiles(destination, prefix + ".*");
        Require(traces.Length > 0 && !traces.SelectMany(File.ReadLines)
            .Any(line => Regex.IsMatch(line, "^inotify_(init|init1|add_watch)\\(")), "http_file_watcher");
    }

    private static async Task RunHttpAsync(string root, string destination, string executable, List<object> commands, bool configured)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        string endpoint = "http://127.0.0.1:" + port;
        string suffix = configured ? "" : "-default";
        string trace = Path.Combine(destination, "WebMinimalApiSmoke" + suffix + "-trace");
        string log = Path.Combine(destination, "WebMinimalApiSmoke" + suffix + "-run");
        string expectedBody = DefaultWebBody + (configured ? "_" + Guid.NewGuid().ToString("N") : "");
        string[] arguments = ["-ff", "-s", "256", "-o", trace, executable, "--urls", endpoint];
        string workingDirectory = Path.GetDirectoryName(executable)!;
        foreach (string name in new[] { "appsettings.json", "appsettings.Development.json" })
            Require(HashFile(Path.Combine(workingDirectory, name)) ==
                HashFile(Path.Combine(root, "apps", "MinimalApiSmoke", name)), "http_published_config_changed");
        commands.Add(new { executable = "strace", arguments, workingDirectory, log });
        using var process = Start("strace", arguments, workingDirectory, configured ? expectedBody : null);
        Task stdout = CaptureAsync(process.StandardOutput, log + ".stdout.log", process);
        Task stderr = CaptureAsync(process.StandardError, log + ".stderr.log", process);
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        using var client = new HttpClient(new HttpClientHandler { UseProxy = false }) { Timeout = TimeSpan.FromSeconds(30) };
        try
        {
            while (true)
            {
                Require(!process.HasExited, "http_payload_exited");
                try
                {
                    using var response = await client.GetAsync(endpoint + "/healthz", deadline.Token);
                    if (response.IsSuccessStatusCode) break;
                }
                catch (HttpRequestException) { }
                await Task.Delay(100, deadline.Token);
            }
            foreach (string path in new[] { "/", "/healthz" })
            {
                using var response = await client.GetAsync(endpoint + path, deadline.Token);
                Require(response.StatusCode == HttpStatusCode.OK &&
                    response.Content.Headers.ContentType?.MediaType == "text/plain" &&
                    await response.Content.ReadAsStringAsync(deadline.Token) == expectedBody, "http_status_or_body");
            }
            using var config = JsonDocument.Parse(await client.GetStringAsync(endpoint + "/config", deadline.Token));
            Require(config.RootElement.GetProperty("appId").GetString() == "minimal.api.smoke" &&
                config.RootElement.GetProperty("expectedBody").GetString() == expectedBody, "http_config_contract");
            string evidence = JsonSerializer.Serialize(new { endpoint, paths = new[] { "/", "/healthz", "/config" },
                status = 200, body = expectedBody, appId = "minimal.api.smoke",
                configBody = config.RootElement.GetProperty("expectedBody").GetString(),
                bodyBytes = config.RootElement.GetProperty("expectedBodyBytes").GetInt32() });
            ValidateHttpEvidence(evidence, expectedBody);
            await File.WriteAllTextAsync(Path.Combine(destination, "web-http" + suffix + ".json"), evidence);
            using var logging = await client.PostAsync(endpoint + "/__clros/logexercise", null, deadline.Token);
            string loggingBody = await logging.Content.ReadAsStringAsync(deadline.Token);
            Require(logging.StatusCode == HttpStatusCode.OK && loggingBody == LoggingBody, "logging_http");
            using var repeat = await client.PostAsync(endpoint + "/__clros/logexercise", null, deadline.Token);
            Require(repeat.StatusCode == HttpStatusCode.Conflict, "logging_repeat");
            using var healthAfter = await client.GetAsync(endpoint + "/healthz", deadline.Token);
            Require(healthAfter.StatusCode == HttpStatusCode.OK &&
                await healthAfter.Content.ReadAsStringAsync(deadline.Token) == expectedBody, "logging_health_after");
            await File.WriteAllTextAsync(Path.Combine(destination, "web-logging" + suffix + ".json"),
                JsonSerializer.Serialize(new { status = 200, body = loggingBody, repeatStatus = 409, healthAfter = 200 }));
            string[] children = (await File.ReadAllTextAsync($"/proc/{process.Id}/task/{process.Id}/children", deadline.Token))
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            Require(children.Length == 1 && int.TryParse(children[0], out _), "http_tracee_identity");
            Require(File.ResolveLinkTarget($"/proc/{children[0]}/exe", true)?.FullName == executable, "http_tracee_executable");
            await ExecuteAsync("kill", ["-TERM", children[0]], root,
                Path.Combine(destination, "WebMinimalApiSmoke" + suffix + "-shutdown"), TimeSpan.FromSeconds(10));
            await process.WaitForExitAsync(deadline.Token);
            Require(process.ExitCode == 0, "http_shutdown_exit");
            await Task.WhenAll(stdout, stderr).WaitAsync(deadline.Token);
            ValidateHttpTraces(destination, Path.GetFileName(trace));
            ValidateRecordedLogging(destination, suffix);
        }
        finally
        {
            await StopSessionAsync(process);
            await Task.WhenAll(stdout, stderr).WaitAsync(TimeSpan.FromSeconds(10));
        }
    }

    internal static async Task ExecuteAsync(string executable, string[] args, string root, string log, TimeSpan timeout)
    {
        using var process = Start(executable, args, root);
        using var deadline = new CancellationTokenSource(timeout);
        Task stdout = CaptureAsync(process.StandardOutput, log + ".stdout.log", process);
        Task stderr = CaptureAsync(process.StandardError, log + ".stderr.log", process);
        try
        {
            await Task.WhenAll(process.WaitForExitAsync(deadline.Token), stdout, stderr).WaitAsync(deadline.Token);
            Require(process.ExitCode == 0, "native_process_exit_" + process.ExitCode + "_" + log);
        }
        finally
        {
            await StopSessionAsync(process);
            await Task.WhenAll(stdout, stderr).WaitAsync(TimeSpan.FromSeconds(10));
        }
    }

    internal static Process Start(string executable, string[] args, string root, string? expectedBody = "OS_MINIMAL_API_OK")
    {
        var start = new ProcessStartInfo("setsid") { WorkingDirectory = root, UseShellExecute = false,
            RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        start.ArgumentList.Add(executable);
        foreach (string argument in args) start.ArgumentList.Add(argument);
        start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        start.Environment["DOTNET_NOLOGO"] = "1";
        start.Environment.Remove("OS_SMOKE_EXPECTED_BODY");
        if (expectedBody is not null) start.Environment["OS_SMOKE_EXPECTED_BODY"] = expectedBody;
        start.Environment["DOTNET_SYSTEM_GLOBALIZATION_INVARIANT"] = "1";
        start.Environment["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0";
        start.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        start.Environment["DOTNET_ENVIRONMENT"] = "Production";
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
        return Process.Start(start) ?? throw new IOException("native_process_start");
    }

    internal static async Task CaptureAsync(StreamReader reader, string path, Process process)
    {
        try
        {
            await using var log = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            char[] buffer = new char[4096];
            int total = 0;
            while (true)
            {
                int count = await reader.ReadAsync(buffer);
                if (count == 0) return;
                total = checked(total + count);
                Require(total <= 32 * 1024 * 1024, "native_log_limit");
                await log.WriteAsync(Encoding.UTF8.GetBytes(buffer.AsSpan(0, count).ToString()));
            }
        }
        catch
        {
            Kill(-process.Id, 9);
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) when (process.HasExited) { }
            throw;
        }
    }

    internal static string HashFile(string path)
    {
        using var file = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(file));
    }
    // Every Linux child starts in its own session/process group. This also covers
    // descendants retaining pipes after a successful compiler/wrapper exit.
    internal static async Task StopSessionAsync(Process process)
    {
        int result = Kill(-process.Id, 9);
        Require(result == 0 || Marshal.GetLastPInvokeError() == 3, "native_session_cleanup");
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
        var elapsed = Stopwatch.StartNew();
        while (Kill(-process.Id, 0) == 0)
        {
            Require(elapsed.Elapsed < TimeSpan.FromSeconds(10), "native_session_still_live");
            await Task.Delay(10);
        }
        Require(Marshal.GetLastPInvokeError() == 3, "native_session_confirmation");
    }
    [DllImport("libc", EntryPoint = "kill", SetLastError = true)]
    private static extern int Kill(int pid, int signal);
    private static void Require([DoesNotReturnIf(false)] bool condition, string reason)
    {
        if (!condition) throw new InvalidDataException(reason);
    }
}
