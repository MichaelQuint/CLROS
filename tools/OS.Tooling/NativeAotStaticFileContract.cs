using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace OS.Tooling;
/// <summary>Linux-reference publication and per-thread syscall/HTTP evidence for
/// the selected static-file application. This supplies no CLROS guest authority.</summary>

public static partial class NativeAotStaticFileContract
{
    public static async Task<int> RunAsync(string root, string[] args, TextWriter output)
    {
        const string marker = "nativeaot.static-files";
        output.WriteLine("TEST_START " + marker);
        try
        {
            root = Path.GetFullPath(root);
            if (args is ["qualify", "--recipe-root", var recipe, "--output", var destination, "--webroot", var webroot])
                await QualifyAsync(root, Path.GetFullPath(recipe), Path.GetFullPath(destination), Path.GetFullPath(webroot), output);
            else if (args is ["validate", var evidence])
                Validate(root, Path.GetFullPath(evidence));
            else
                throw new ArgumentException("nativeaot static-files qualify --recipe-root <original-socket-root> --output <fresh-directory> --webroot <prepared-webroot> | validate <evidence-directory>");
            output.WriteLine("TEST_PASS " + marker);
            return 0;
        }
        catch (Exception error)
        {
            output.WriteLine("TEST_FAIL " + marker + " " + error.Message.ReplaceLineEndings(" "));
            return 1;
        }
    }

    private static async Task QualifyAsync(string root, string recipe, string destination, string webroot, TextWriter output)
    {
        if (!OperatingSystem.IsLinux())
            throw new InvalidDataException("static_files_linux_builder_required");
        var assets = WebRootImageFixture.SnapshotPublishedRoot(webroot);
        Need(assets.Files.Any(x => x.Path == "index.html") && assets.Files.Any(x => x.Path == "_framework/blazor.webassembly.js"), "static_files_blazor_assets_missing");
        Need(await NativeAotStaticFilePublisher.RunAsync(root, ["publish", "--recipe-root", recipe, "--output", destination], output) == 0, "static_files_publication_failed");
        using var lease = NativeAotPayload.OpenQualificationLease(root);
        Need(ToolingBuildInputs.Validate(root).Count == 0, "static_files_source_changed");
        const string referenceRoot = "/app/wwwroot";
        Need(!Path.Exists(referenceRoot), "static_files_reference_root_exists");
        Directory.CreateDirectory(referenceRoot);
        using (AuthorityFiles files = new())
        {
            foreach (var asset in assets.Files)
            {
                var source = files.Open(root, Path.Combine(webroot, asset.Path), "static reference asset", "static_reference_asset", asset.Bytes, allowEmpty: true, singleLink: true);
                byte[] bytes = source.ReadBytes(asset.Bytes, "static_reference_asset");
                Need(Hash(bytes) == asset.Sha256, "static_files_asset_changed");
                string target = Path.Combine(referenceRoot, asset.Path);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                await File.WriteAllBytesAsync(target, bytes);
                File.SetLastWriteTimeUtc(target, new DateTime(1980, 1, 1, 0, 0, 0, DateTimeKind.Utc));
                File.SetUnixFileMode(target, UnixFileMode.UserRead | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
            }

            files.VerifyUnchanged();
        }

        await File.WriteAllTextAsync(Path.Combine(referenceRoot, "fixture-nonce.txt"), "CLROS-DISK-A-0001\n");
        File.SetLastWriteTimeUtc(Path.Combine(referenceRoot, "fixture-nonce.txt"), new DateTime(1980, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        File.SetUnixFileMode(Path.Combine(referenceRoot, "fixture-nonce.txt"), UnixFileMode.UserRead | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
        foreach (string directory in Directory.GetDirectories(referenceRoot, "*", SearchOption.AllDirectories).Append(referenceRoot))
            File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        string endpoint = "http://127.0.0.1:" + port;
        string executable = Path.Combine(destination, NativeAotStaticFilePublisher.Application, NativeAotStaticFilePublisher.Application);
        string[] arguments = ["-ff", "-yy", "-ttt", "-s", "256", "-o", Path.Combine(destination, "static-trace"), executable, "--urls", endpoint];
        await File.WriteAllTextAsync(Path.Combine(destination, "static-command.json"), JsonSerializer.Serialize(new { executable = "setsid", arguments = new[] { "strace" }.Concat(arguments).ToArray(), workingDirectory = root, environment = new { CLROS_STATIC_FILES_PROFILE = "v1" }, endpoint, referenceRoot }));
        List<Observation> observations = [];
        int requestId = -1;
        using var process = Start(arguments, root);
        Task stdout = NativeAotWebPayload.CaptureAsync(process.StandardOutput, Path.Combine(destination, "static-run.stdout.log"), process);
        Task stderr = NativeAotWebPayload.CaptureAsync(process.StandardError, Path.Combine(destination, "static-run.stderr.log"), process);
        try
        {
            using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
            {
                BaseAddress = new Uri(endpoint),
                Timeout = TimeSpan.FromSeconds(30)
            };
            var ready = Stopwatch.StartNew();
            while (true)
            {
                Need(!process.HasExited && ready.Elapsed < TimeSpan.FromSeconds(60), "static_files_startup_failed");
                try
                {
                    using var response = await client.GetAsync("/healthz");
                    if (response.StatusCode == HttpStatusCode.OK)
                        break;
                }
                catch (HttpRequestException)
                {
                }

                await Task.Delay(100);
            }

            async Task<Observation> Request(string method, string path, Action<HttpRequestMessage>? headers = null, bool concurrent = false)
            {
                int id = Interlocked.Increment(ref requestId);
                using var request = new HttpRequestMessage(new HttpMethod(method), path);
                headers?.Invoke(request);
                long started = Stopwatch.GetTimestamp();
                using var response = await client.SendAsync(request);
                byte[] bytes = await response.Content.ReadAsByteArrayAsync();
                long finished = Stopwatch.GetTimestamp();
                Need(bytes.Length <= NativeAotPayload.MaximumImageBytes, "static_files_response_too_large");
                string bodyFile = "http-" + id.ToString("D4", System.Globalization.CultureInfo.InvariantCulture) + ".body";
                await File.WriteAllBytesAsync(Path.Combine(destination, bodyFile), bytes);
                var row = new Observation(method, path, (int)response.StatusCode, response.Content.Headers.ContentType?.MediaType, response.Content.Headers.ContentLength, bytes.Length, Hash(bytes), bodyFile, response.Headers.ETag?.ToString(), response.Content.Headers.LastModified?.ToString("R"), response.Content.Headers.ContentRange?.ToString(), concurrent, started, finished);
                Need(response.Content.Headers.ContentEncoding.Count == 0, "static_files_unexpected_compression");
                lock (observations)
                    observations.Add(row);
                return row;
            }

            await Request("GET", "/healthz");
            await Request("GET", "/config");
            await Request("GET", "/api/status");
            await Request("GET", "/api/items");
            await Request("GET", "/api/items?empty=true");
            await Request("GET", "/fixture-nonce.txt");
            var index = await Request("GET", "/");
            await Request("HEAD", "/");
            foreach (var asset in assets.Files)
                await Request("GET", "/" + string.Join('/', asset.Path.Split('/').Select(Uri.EscapeDataString)));
            var largest = assets.Files.OrderByDescending(x => x.Bytes).First();
            string rangePath = "/" + largest.Path;
            Need(largest.Bytes > 1032, "static_files_range_asset_too_small");
            await Request("GET", rangePath, request => request.Headers.Range = new RangeHeaderValue(510, 1031));
            await Request("GET", rangePath, request => request.Headers.Range = new RangeHeaderValue(largest.Bytes, null));
            Need(index.ETag is not null && index.LastModified is not null, "static_files_metadata_missing");
            await Request("GET", "/", request => request.Headers.TryAddWithoutValidation("If-None-Match", index.ETag));
            await Request("GET", "/", request => request.Headers.TryAddWithoutValidation("If-Modified-Since", index.LastModified));
            await Request("GET", "/.clros-missing");
            await Request("POST", "/index.html");
            TaskCompletionSource start = new(TaskCreationOptions.RunContinuationsAsynchronously);
            async Task ConcurrentGet()
            {
                await start.Task;
                await Request("GET", rangePath, concurrent: true);
            }

            Task firstRead = ConcurrentGet(), secondRead = ConcurrentGet();
            start.SetResult();
            await Task.WhenAll(firstRead, secondRead);
            observations.Sort((a, b) => StringComparer.Ordinal.Compare(a.BodyFile, b.BodyFile));
            await File.WriteAllTextAsync(Path.Combine(destination, "static-http.json"), JsonSerializer.Serialize(observations));
            ValidateHttp(destination, assets, observations);
            string[] children = (await File.ReadAllTextAsync($"/proc/{process.Id}/task/{process.Id}/children")).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            Need(children.Length == 1 && int.TryParse(children[0], out int child) && File.ResolveLinkTarget("/proc/" + children[0] + "/exe", true)?.FullName == executable, "static_files_shutdown_owner");
            Need(Kill(int.Parse(children[0], System.Globalization.CultureInfo.InvariantCulture), 15) == 0, "static_files_shutdown_signal");
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15));
            await Task.WhenAll(stdout, stderr);
            Need(process.ExitCode == 0 && File.ReadAllText(Path.Combine(destination, "static-run.stderr.log")).Contains("CLROS_STATIC_FILES_APP_END v1", StringComparison.Ordinal), "static_files_shutdown_failed");
            bool refused = false;
            try
            {
                using var response = await client.GetAsync("/healthz");
            }
            catch (HttpRequestException)
            {
                refused = true;
            }

            Need(refused, "static_files_stopped_endpoint_live");
            await File.WriteAllTextAsync(Path.Combine(destination, "static-shutdown.json"), JsonSerializer.Serialize(new Shutdown(15, process.ExitCode, refused)));
        }
        finally
        {
            await NativeAotWebPayload.StopSessionAsync(process);
            await Task.WhenAll(stdout, stderr);
        }

        string[] syscalls = TraceSyscalls(destination);
        Need(syscalls.Contains("execve") && syscalls.Contains("exit_group"), "static_files_trace_incomplete");
        await File.WriteAllLinesAsync(Path.Combine(destination, "static-observed-syscalls.txt"), syscalls);
        await File.WriteAllTextAsync(Path.Combine(destination, "static-boundary.txt"), "Linux-reference NativeAOT/file ABI and HTTP observations only. Per-thread strace is observed coverage, not an exhaustive syscall contract. No CLROS guest/disk/browser or writable-storage authority.\n");
        using var publication = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(destination, "publication.json")));
        var sources = publication.RootElement.GetProperty("sources").Deserialize<Dictionary<string, string>>()!;
        Need(ToolingBuildInputs.Validate(root).Count == 0 && sources.All(x => FileHash(Path.Combine(root, x.Key)).Equals(x.Value, StringComparison.OrdinalIgnoreCase)), "static_files_source_changed");
        var receipt = new Receipt("clros.nativeaot.static-files.v1", NativeAotPayload.SdkVersion, NativeAotPayload.RuntimeVersion, assets, observations.ToArray(), sources, OutputHashes(destination));
        await File.WriteAllTextAsync(Path.Combine(destination, "static-receipt.json"), JsonSerializer.Serialize(receipt));
        Validate(root, destination);
    }

    /// <summary>Rehash original sources, every retained output and each HTTP body;
    /// reject changed or incomplete evidence. No process or guest is executed.</summary>

    public static void Validate(string root, string destination)
    {
        AuthorityFiles.RequirePlainPath(destination, destination, "static_files_evidence_path");
        string path = Path.Combine(destination, "static-receipt.json");
        Need(new FileInfo(path).Length is> 0 and <= 4 * 1024 * 1024, "static_files_receipt_size");
        var receipt = JsonSerializer.Deserialize<Receipt>(File.ReadAllText(path)) ?? throw new InvalidDataException("static_files_receipt_missing");
        Need(receipt.Schema == "clros.nativeaot.static-files.v1" && receipt.Sdk == NativeAotPayload.SdkVersion && receipt.Runtime == NativeAotPayload.RuntimeVersion, "static_files_receipt_pin");
        Need(receipt.Sources.Count is> 0 and <= 10000 && receipt.Outputs.Count is> 0 and <= 10000, "static_files_receipt_inventory");
        Need(NativeAotStaticFilePublisher.SourcePaths(root).SequenceEqual(receipt.Sources.Keys.Order(StringComparer.Ordinal)), "static_files_source_inventory");
        foreach (var source in receipt.Sources)
        {
            AuthorityFiles.RequirePlainPath(root, Path.Combine(root, source.Key), "static_files_source_path");
            Need(FileHash(Path.Combine(root, source.Key)).Equals(source.Value, StringComparison.OrdinalIgnoreCase), "static_files_source_changed");
        }

        Need(OutputHashes(destination).OrderBy(x => x.Key).SequenceEqual(receipt.Outputs.OrderBy(x => x.Key)), "static_files_output_changed");
        ValidateNativeInputs(JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(destination, "native-inputs.json")))!);
        NativeAotStaticFilePublisher.RequireCurrentSources(root);
        using var publication = JsonDocument.Parse(File.ReadAllText(Path.Combine(destination, "publication.json")));
        var published = publication.RootElement;
        Need(published.GetProperty("succeeded").GetBoolean() && published.GetProperty("application").GetString() == NativeAotStaticFilePublisher.Application && published.GetProperty("sdk").GetString() == receipt.Sdk && published.GetProperty("runtime").GetString() == receipt.Runtime, "static_files_publication_policy");
        var publishedSources = published.GetProperty("sources").Deserialize<Dictionary<string, string>>()!;
        Need(publishedSources.OrderBy(x => x.Key).SequenceEqual(receipt.Sources.OrderBy(x => x.Key)), "static_files_publication_sources");
        string elf = Path.Combine(destination, NativeAotStaticFilePublisher.Application, NativeAotStaticFilePublisher.Application);
        Need(new FileInfo(elf).Length <= NativeAotPayload.MaximumImageBytes, "static_files_elf_size");
        Need(NativeAotPayload.Inspect(File.ReadAllBytes(elf)).Sha256 == published.GetProperty("image").GetProperty("Sha256").GetString(), "static_files_elf_changed");
        Need(TraceSyscalls(destination).SequenceEqual(File.ReadAllLines(Path.Combine(destination, "static-observed-syscalls.txt"))), "static_files_syscall_inventory");
        string[] trace = Directory.GetFiles(destination, "static-trace.*").SelectMany(File.ReadLines).ToArray();
        ValidateShutdown(File.ReadAllText(Path.Combine(destination, "static-run.stderr.log")), trace, JsonSerializer.Deserialize<Shutdown>(File.ReadAllText(Path.Combine(destination, "static-shutdown.json")))!);
        ValidateHttp(destination, receipt.Assets, receipt.Http);
    }

    /// <summary>Rehash the original absolute compiler/runtime inputs. The caller
    /// retains their lifetime; missing, redirected or changed inputs invalidate replay.</summary>

    public static void ValidateNativeInputs(IReadOnlyDictionary<string, string> inputs)
    {
        Need(inputs is not null && inputs.Count is> 0 and <= 10000, "static_files_native_inventory");
        foreach (var input in inputs!)
        {
            Need(Path.IsPathFullyQualified(input.Key), "static_files_native_path");
            AuthorityFiles.RequirePlainPath(Path.GetPathRoot(input.Key)!, input.Key, "static_files_native_path");
            Need(FileHash(input.Key).Equals(input.Value, StringComparison.OrdinalIgnoreCase), "static_files_native_changed");
        }
    }

    /// <summary>Check the retained finite termination observations. These bytes
    /// establish a reference replay boundary, not a fresh process execution.</summary>

    public static void ValidateShutdown(string stderr, IReadOnlyList<string> trace, Shutdown shutdown)
    {
        Need(shutdown is { Signal: 15, ExitCode: 0, EndpointRefused: true }, "static_files_shutdown_receipt");
        Need(stderr.Split('\n').Count(x => x.TrimEnd('\r') == "CLROS_STATIC_FILES_APP_END v1") == 1 && !stderr.Contains("CLROS_STATIC_FILES_APP_FAIL", StringComparison.Ordinal), "static_files_shutdown_marker");
        Need(trace.Any(x => x.Contains("--- SIGTERM {", StringComparison.Ordinal)) && trace.Any(x => Regex.IsMatch(x, @"\bexit_group\(0\)\s+=")), "static_files_shutdown_trace");
    }

    private static string[] TraceSyscalls(string destination)
    {
        string[] files = Directory.GetFiles(destination, "static-trace.*");
        Need(files.Length is> 0 and <= 256, "static_files_trace_inventory");
        return files.SelectMany(File.ReadLines).Select(line => Regex.Match(line, @"(?:^|\s)([a-zA-Z_][a-zA-Z0-9_]*)\(")).Where(match => match.Success).Select(match => match.Groups[1].Value).Distinct().Order(StringComparer.Ordinal).ToArray();
    }

    /// <summary>Check independent recorded HTTP bodies against the measured
    /// asset inventory, generated-JSON contract and finite metadata/range cases.</summary>

    public static void ValidateHttp(string destination, WebRootImageFixture.Snapshot assets, IReadOnlyList<Observation> rows)
    {
        Need(rows.Count == assets.Files.Length + 16 && rows.Select(x => x.BodyFile).Distinct().Count() == rows.Count, "static_files_http_inventory");
        foreach (var row in rows)
        {
            Need(Regex.IsMatch(row.BodyFile, @"^http-[0-9]{4}\.body$"), "static_files_body_path");
            string body = Path.Combine(destination, row.BodyFile);
            AuthorityFiles.RequirePlainPath(destination, body, "static_files_body_path");
            Need(new FileInfo(body).Length == row.BodyBytes && FileHash(body) == row.Sha256, "static_files_http_body_changed");
        }

        Observation Get(string method, string path, int status = 200) => rows.Single(x => !x.Concurrent && x.Method == method && x.Path == path && x.Status == status);
        byte[] Bytes(Observation row) => File.ReadAllBytes(Path.Combine(destination, row.BodyFile));
        Need(System.Text.Encoding.UTF8.GetString(Bytes(Get("GET", "/healthz"))) == "OS_MINIMAL_API_OK", "static_files_health");
        using var config = JsonDocument.Parse(Bytes(Get("GET", "/config")));
        Need(config.RootElement.GetProperty("appId").GetString() == "minimal.api.static-files", "static_files_config");
        using var status = JsonDocument.Parse(Bytes(Get("GET", "/api/status")));
        Need(status.RootElement.ValueKind == JsonValueKind.Object && status.RootElement.GetProperty("profile").GetString() == "v1" && status.RootElement.GetProperty("ready").GetBoolean() && status.RootElement.GetProperty("message").GetString() == "CLROS ready — café", "static_files_status_json");
        using var items = JsonDocument.Parse(Bytes(Get("GET", "/api/items")));
        Need(items.RootElement.GetProperty("items").GetArrayLength() == 2 && items.RootElement.GetProperty("items")[1].GetProperty("detail").ValueKind == JsonValueKind.Null, "static_files_items_json");
        using var empty = JsonDocument.Parse(Bytes(Get("GET", "/api/items?empty=true")));
        Need(empty.RootElement.GetProperty("items").GetArrayLength() == 0, "static_files_empty_json");
        Need(Bytes(Get("GET", "/fixture-nonce.txt")).AsSpan().SequenceEqual("CLROS-DISK-A-0001\n"u8), "static_files_reference_nonce");
        foreach (var asset in assets.Files)
        {
            string url = "/" + string.Join('/', asset.Path.Split('/').Select(Uri.EscapeDataString));
            var row = Get("GET", url);
            Need(row.Sha256 == asset.Sha256 && row.ContentLength == asset.Bytes && row.BodyBytes == asset.Bytes, "static_files_asset_response");
            if (asset.Path.EndsWith(".wasm", StringComparison.Ordinal))
                Need(row.ContentType == "application/wasm", "static_files_wasm_type");
        }

        var indexAsset = assets.Files.Single(x => x.Path == "index.html");
        Need(Get("GET", "/").Sha256 == indexAsset.Sha256 && Get("HEAD", "/").BodyBytes == 0 && Get("HEAD", "/").ContentLength == indexAsset.Bytes, "static_files_default_and_head");
        var largest = assets.Files.OrderByDescending(x => x.Bytes).First();
        string largestUrl = "/" + largest.Path;
        var concurrent = rows.Where(x => x.Concurrent).OrderBy(x => x.Started).ToArray();
        Need(concurrent.Length == 2 && concurrent.All(x => x.Method == "GET" && x.Path == largestUrl && x.Status == 200 && x.BodyBytes == largest.Bytes && x.ContentLength == largest.Bytes && x.Sha256 == largest.Sha256 && x.Started > 0 && x.Finished > x.Started) && concurrent[1].Started < concurrent[0].Finished, "static_files_concurrent_reads");
        var range = Get("GET", largestUrl, 206);
        Need(range.BodyBytes == 522 && Bytes(range).AsSpan().SequenceEqual(Bytes(Get("GET", largestUrl)).AsSpan(510, 522)) && range.ContentRange == $"bytes 510-1031/{largest.Bytes}", "static_files_range");
        Need(Get("GET", largestUrl, 416).ContentRange == "bytes */" + largest.Bytes, "static_files_unsatisfiable_range");
        Need(rows.Count(x => x.Method == "GET" && x.Path == "/" && x.Status == 304 && x.BodyBytes == 0) == 2, "static_files_conditional");
        Get("GET", "/.clros-missing", 404);
        Get("POST", "/index.html", 404);
    }

    private static Process Start(string[] arguments, string root)
    {
        var info = new ProcessStartInfo("setsid")
        {
            WorkingDirectory = root,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        info.ArgumentList.Add("strace");
        foreach (string argument in arguments)
            info.ArgumentList.Add(argument);
        info.Environment["CLROS_STATIC_FILES_PROFILE"] = "v1";
        info.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
        info.Environment["DOTNET_ENVIRONMENT"] = "Production";
        info.Environment.Remove("OS_SMOKE_EXPECTED_BODY");
        return Process.Start(info) ?? throw new InvalidDataException("static_files_start_failed");
    }

    private static Dictionary<string, string> OutputHashes(string destination)
    {
        Dictionary<string, string> hashes = new(StringComparer.Ordinal);
        Walk(destination);
        return hashes;
        void Walk(string directory)
        {
            AuthorityFiles.RequirePlainPath(destination, directory, "static_files_output_path");
            foreach (string path in Directory.GetFileSystemEntries(directory).Order(StringComparer.Ordinal))
            {
                AuthorityFiles.RequirePlainPath(destination, path, "static_files_output_path");
                if (Directory.Exists(path))
                {
                    if (Path.GetFileName(path) != "retained-build-cache")
                        Walk(path);
                    continue;
                }

                if (Path.GetFileName(path) == "static-receipt.json")
                    continue;
                Need(hashes.Count < 10000, "static_files_output_inventory");
                hashes.Add(Path.GetRelativePath(destination, path).Replace('\\', '/'), FileHash(path));
            }
        }
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static string FileHash(string path)
    {
        using var file = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(file));
    }

    private static void Need(bool condition, string reason)
    {
        if (!condition)
            throw new InvalidDataException(reason);
    }

    [DllImport("libc", EntryPoint = "kill", SetLastError = true)]
    private static extern int Kill(int pid, int signal);
}
