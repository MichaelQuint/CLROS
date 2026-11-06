using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OS.Tooling;
/// <summary>Own a finite HTTP matrix beside one captured staged guest. Original
/// response bodies and headers remain in fresh caller-owned evidence files.
/// Completion requires joined runner exit; it grants no native, packet or guest
/// qualification without the independent complete transcript consumer.</summary>

public static partial class NativeAotStagedStaticPeer
{
    internal const int MatrixTimeoutSeconds = 110;
    /// <summary>Use at most two pooled HTTP/1.1 connections and bounded response
    /// buffers. One owner starts and joins the runner, cancels it on peer failure
    /// and preserves partial files. A competing port bind, missing readiness,
    /// shutdown before matrix completion, malformed response or late runner exit
    /// fails. The runner delegate must honor cancellation and confirm termination.</summary>

    public static async Task<Completion> RunAsync(string root, string serial, string destination, Func<int, CancellationToken, Task<ProcessResult>> runRunner, CancellationToken cancellation = default)
    {
        root = Path.GetFullPath(root);
        serial = Path.GetFullPath(serial, root);
        destination = Path.GetFullPath(destination, root);
        if (!destination.StartsWith(Path.Combine(root, "artifacts") + Path.DirectorySeparatorChar, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) || Path.Exists(destination))
            throw new ToolFailure("static_peer_fresh_artifacts_required");
        AuthorityFiles.RequirePlainPath(root, Path.GetDirectoryName(destination)!, "static_peer_path");
        Directory.CreateDirectory(destination);
        int port;
        using (var reservation = new TcpListener(IPAddress.Loopback, 0))
        {
            reservation.Start();
            port = ((IPEndPoint)reservation.LocalEndpoint).Port;
        }

        using var owned = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        Task<ProcessResult> runner = runRunner(port, owned.Token);
        Task? peer = null;
        try
        {
            peer = Observe();
            if (await Task.WhenAny(peer, runner) == runner)
                throw new ToolFailure("static_runner_exited_before_http");
            await peer;
            var result = await runner;
            QemuTranscripts.RequireCompleteProcess(result, "static_runner_incomplete");
            if (!result.Succeeded)
                throw new ToolFailure("static_runner_failed");
            using (Socket stopped = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
            {
                using var refusal = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
                refusal.CancelAfter(TimeSpan.FromSeconds(5));
                bool refused = false;
                try
                {
                    await stopped.ConnectAsync(IPAddress.Loopback, port, refusal.Token);
                }
                catch (SocketException error) when (error.SocketErrorCode == SocketError.ConnectionRefused)
                {
                    refused = true;
                }

                if (!refused)
                    throw new ToolFailure("static_peer_stopped_endpoint_live");
                using var output = new FileStream(Path.Combine(destination, "stopped.json"), FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                await JsonSerializer.SerializeAsync(output, new
                {
                    Port = port,
                    EndpointRefused = true,
                    RunnerExit = result.ExitCode
                }, cancellationToken: cancellation);
                output.Flush(true);
            }

            string receipt = Path.Combine(destination, "http.json");
            return new(port, destination, NativeAotWebPayload.HashFile(receipt), result);
        }
        finally
        {
            owned.Cancel();
            if (peer is not null)
                try
                {
                    await peer;
                }
                catch
                {
                }

            try
            {
                _ = await runner;
            }
            catch
            {
            }
        }

        async Task Observe()
        {
            using var startup = CancellationTokenSource.CreateLinkedTokenSource(owned.Token);
            startup.CancelAfter(TimeSpan.FromSeconds(240));
            await NativeAotSocketPeer.WaitForSerialAsync(serial, text => text.Contains("M10_017_LISTENING ", StringComparison.Ordinal), startup.Token);
            using var matrix = CancellationTokenSource.CreateLinkedTokenSource(owned.Token);
            matrix.CancelAfter(TimeSpan.FromSeconds(MatrixTimeoutSeconds));
            using var client = new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false, AutomaticDecompression = DecompressionMethods.None, MaxConnectionsPerServer = 2, UseProxy = false })
            {
                BaseAddress = new Uri("http://127.0.0.1:" + port),
                Timeout = TimeSpan.FromSeconds(45),
                DefaultRequestVersion = HttpVersion.Version11,
                DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact
            };
            var rows = new List<Response>();
            int ordinal = -1;
            async Task<(Response Row, byte[] Body)> Request(string method, string path, int status, byte[]? expected = null, Action<HttpRequestMessage>? configure = null)
            {
                int id = Interlocked.Increment(ref ordinal);
                using var request = new HttpRequestMessage(new HttpMethod(method), path);
                configure?.Invoke(request);
                string[] headers = request.Headers.Select(x => x.Key + ": " + string.Join(",", x.Value)).ToArray();
                long started = Stopwatch.GetTimestamp();
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, matrix.Token);
                using var body = new MemoryStream();
                using (var input = await response.Content.ReadAsStreamAsync(matrix.Token))
                {
                    byte[] buffer = new byte[8192];
                    while (true)
                    {
                        int count = await input.ReadAsync(buffer, matrix.Token);
                        if (count == 0)
                            break;
                        if (body.Length + count > 65536)
                            throw new ToolFailure("static_peer_response_capacity");
                        body.Write(buffer, 0, count);
                    }
                }

                long finished = Stopwatch.GetTimestamp();
                byte[] bytes = body.ToArray();
                string file = id.ToString("D3", System.Globalization.CultureInfo.InvariantCulture) + ".body";
                using (var output = new FileStream(Path.Combine(destination, file), FileMode.CreateNew, FileAccess.Write, FileShare.Read))
                {
                    await output.WriteAsync(bytes, matrix.Token);
                    output.Flush(true);
                }

                var row = new Response(id, method, path, (int)response.StatusCode, headers, response.Headers.Concat(response.Content.Headers).Select(x => x.Key + ": " + string.Join(",", x.Value)).ToArray(), bytes.Length, Convert.ToHexString(SHA256.HashData(bytes)), file, started, finished);
                lock (rows)
                    rows.Add(row);
                if (row.Status != status || expected is not null && !bytes.AsSpan().SequenceEqual(expected))
                    throw new ToolFailure("static_peer_response " + method + " " + path);
                return (row, bytes);
            }

            try
            {
                await Request("GET", "/healthz", 200, "OS_MINIMAL_API_OK"u8.ToArray());
                var configuration = await Request("GET", "/config", 200);
                using (var json = JsonDocument.Parse(configuration.Body))
                {
                    var value = json.RootElement;
                    if (value.GetProperty("appId").GetString() != "minimal.api.static-files" || value.GetProperty("profile").GetString() != "v1" || value.GetProperty("webRoot").GetString() != "/app/wwwroot")
                        throw new ToolFailure("static_peer_configuration");
                }

                var status = await Request("GET", "/api/status", 200);
                using (var json = JsonDocument.Parse(status.Body))
                    if (!json.RootElement.GetProperty("ready").GetBoolean() || json.RootElement.GetProperty("message").GetString() != "CLROS ready — café")
                        throw new ToolFailure("static_peer_status_json");
                var items = await Request("GET", "/api/items", 200);
                using (var json = JsonDocument.Parse(items.Body))
                    if (json.RootElement.GetProperty("items").GetArrayLength() != 2 || json.RootElement.GetProperty("items")[1].GetProperty("detail").ValueKind != JsonValueKind.Null)
                        throw new ToolFailure("static_peer_items_json");
                await Request("GET", "/api/items?empty=true", 200, "{\"items\":[]}"u8.ToArray());
                var assets = NativeAotStaticFilePackage.StagedAssets();
                Response? boundary = null;
                foreach (var asset in assets)
                {
                    string path = "/" + asset.Path[8..];
                    var response = await Request("GET", path, 200, asset.Bytes);
                    string mediaType = Path.GetExtension(path) switch
                    {
                        ".html" => "text/html",
                        ".css" => "text/css",
                        ".js" => "text/javascript",
                        ".json" => "application/json",
                        ".txt" => "text/plain",
                        ".bin" => "application/octet-stream",
                        _ => throw new ToolFailure("static_peer_asset_type")
                    };
                    if (!response.Row.ResponseHeaders.Any(x => x.Equals("Content-Type: " + mediaType, StringComparison.OrdinalIgnoreCase) || x.StartsWith("Content-Type: " + mediaType + ";", StringComparison.OrdinalIgnoreCase)) || !response.Row.ResponseHeaders.Contains("Content-Length: " + asset.Bytes.Length.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparer.OrdinalIgnoreCase) || !response.Row.ResponseHeaders.Contains("Cache-Control: no-store", StringComparer.OrdinalIgnoreCase))
                        throw new ToolFailure("static_peer_asset_headers");
                    if (path == "/boundary.bin")
                        boundary = response.Row;
                }

                await Request("GET", "/", 200, assets[0].Bytes);
                var head = await Request("HEAD", "/boundary.bin", 200, []);
                if (!head.Row.ResponseHeaders.Contains("Content-Length: 65536", StringComparer.OrdinalIgnoreCase))
                    throw new ToolFailure("static_peer_head_length");
                byte[] large = assets.Single(x => x.Path == "wwwroot/boundary.bin").Bytes;
                var range = await Request("GET", "/boundary.bin", 206, large.AsSpan(510, 522).ToArray(), request => request.Headers.Range = new RangeHeaderValue(510, 1031));
                if (!range.Row.ResponseHeaders.Contains("Content-Range: bytes 510-1031/65536", StringComparer.OrdinalIgnoreCase))
                    throw new ToolFailure("static_peer_range");
                await Request("GET", "/boundary.bin", 416, [], request => request.Headers.Range = new RangeHeaderValue(65536, null));
                string Header(string name) => boundary!.ResponseHeaders.Single(x => x.StartsWith(name + ": ", StringComparison.OrdinalIgnoreCase))[(name.Length + 2)..];
                await Request("GET", "/boundary.bin", 304, [], request => request.Headers.TryAddWithoutValidation("If-None-Match", Header("ETag")));
                await Request("GET", "/boundary.bin", 304, [], request => request.Headers.TryAddWithoutValidation("If-Modified-Since", Header("Last-Modified")));
                await Request("GET", "/missing.txt", 404, []);
                await Request("GET", "/appsettings.json", 404, []);
                await Request("GET", "/index.html.exe", 404, []);
                await Request("POST", "/one.bin", 404, []);
                var concurrent = await Task.WhenAll(Request("GET", "/boundary.bin", 200, large), Request("GET", "/boundary.bin", 200, large));
                if (Math.Max(concurrent[0].Row.Started, concurrent[1].Row.Started) >= Math.Min(concurrent[0].Row.Finished, concurrent[1].Row.Finished))
                    throw new ToolFailure("static_peer_concurrent_overlap");
                using (Socket abort = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                {
                    abort.NoDelay = true;
                    abort.ReceiveBufferSize = 1024;
                    await abort.ConnectAsync(IPAddress.Loopback, port, matrix.Token);
                    int localPort = ((IPEndPoint)abort.LocalEndPoint!).Port;
                    byte[] request = Encoding.ASCII.GetBytes("GET /boundary.bin HTTP/1.1\r\nHost: 127.0.0.1:" + port + "\r\nConnection: keep-alive\r\n\r\n");
                    long started = Stopwatch.GetTimestamp();
                    int sent = 0;
                    while (sent < request.Length)
                    {
                        int count = await abort.SendAsync(request.AsMemory(sent), SocketFlags.None, matrix.Token);
                        if (count == 0)
                            throw new ToolFailure("static_peer_abort_send");
                        sent += count;
                    }

                    byte[] prefix = new byte[512];
                    int received = 0;
                    while (received < 12)
                    {
                        int count = await abort.ReceiveAsync(prefix.AsMemory(received), SocketFlags.None, matrix.Token);
                        if (count == 0)
                            throw new ToolFailure("static_peer_abort_eof");
                        received += count;
                    }

                    if (!prefix.AsSpan(0, received).StartsWith("HTTP/1.1 200"u8))
                        throw new ToolFailure("static_peer_abort_response");
                    abort.LingerState = new(true, 0);
                    abort.Close();
                    using var abortReceipt = new FileStream(Path.Combine(destination, "abort.json"), FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                    await JsonSerializer.SerializeAsync(abortReceipt, new
                    {
                        LocalPort = localPort,
                        Request = Convert.ToBase64String(request),
                        Prefix = Convert.ToBase64String(prefix.AsSpan(0, received)),
                        Started = started,
                        Closed = Stopwatch.GetTimestamp(),
                        Close = "reset"
                    }, cancellationToken: matrix.Token);
                    abortReceipt.Flush(true);
                }

                await Request("GET", "/healthz", 200, "OS_MINIMAL_API_OK"u8.ToArray());
                await Request("GET", "/boundary.bin", 200, large);
                using (var input = new FileStream(serial, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    if (input.Length > 8 * 1024 * 1024)
                        throw new ToolFailure("static_peer_serial_capacity");
                    using var reader = new StreamReader(input, Encoding.UTF8);
                    string text = await reader.ReadToEndAsync(matrix.Token);
                    if (text.Contains("M10_017_TERMINATE ", StringComparison.Ordinal) || text.Contains("TEST_FAIL", StringComparison.Ordinal))
                        throw new ToolFailure("static_peer_matrix_after_shutdown");
                }

                await WriteReceipt("http.json", true);
            }
            catch
            {
                await WriteReceipt("http.failed.json", false);
                throw;
            }

            async Task WriteReceipt(string name, bool complete)
            {
                using var output = new FileStream(Path.Combine(destination, name), FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                await JsonSerializer.SerializeAsync(output, new
                {
                    Schema = "clros-staged-http-v1",
                    Port = port,
                    Complete = complete,
                    ClockFrequency = Stopwatch.Frequency,
                    Responses = rows.OrderBy(x => x.Ordinal).ToArray()
                }, cancellationToken: CancellationToken.None);
                output.Flush(true);
            }
        }
    }
}
