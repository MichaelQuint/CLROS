using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;

[assembly: InternalsVisibleTo("OS.HostTests")]
namespace OS.Tooling;
/// <summary>Own the explicitly selected bounded development or full HTTP peer
/// and its original runner. Preserve fresh partial failures, join child cleanup and record a
/// stopped endpoint. Complete native/image/packet/file validation remains the
/// enclosing original consumer's obligation; this is not Blazor acceptance.</summary>

public static partial class NativeAotDiskStaticPeer
{
    /// <summary>Start exactly one caller-supplied cancellable runner and a
    /// single pooled HTTP/1.1 connection after actual guest readiness. The owner
    /// retains no live child on return or throw. Development copies five64KiB
    /// bodies; full mode requires an independent image inventory and original
    /// browser callback, with at most16MiB in2MiB entities. Results are copied
    /// to a fresh artifacts directory; no redirects, proxy, decompression
    /// or external content can satisfy the requests. A competing bind, missing
    /// readiness, wrong response, timeout or incomplete cleanup fails.</summary>

    public static Task<Completion> RunAsync(string root, string serial, Func<int, CancellationToken, Task<ProcessResult>> runRunner, CancellationToken cancellation, WebRootDiskInventory.Inspection? fullInventory = null, Func<int, string, CancellationToken, Task>? browser = null, WebRootDiskInventory.VerifiedTruncated? truncated = null) => RunCoreAsync(root, serial, runRunner, cancellation, fullInventory, browser, null, truncated);
    // Host controls may observe copied ordinal/byte counts after a bounded
    // positive read. The observer must return without blocking or throwing;
    // no buffer or live HTTP object escapes. Production callers pass null.
    internal static async Task<Completion> RunCoreAsync(string root, string serial, Func<int, CancellationToken, Task<ProcessResult>> runRunner, CancellationToken cancellation, WebRootDiskInventory.Inspection? fullInventory, Func<int, string, CancellationToken, Task>? browser, Action<int, long>? bodyRead, WebRootDiskInventory.VerifiedTruncated? truncated = null)
    {
        root = Path.GetFullPath(root);
        serial = Path.GetFullPath(serial, root);
        bool full = fullInventory is not null;
        if (full != (browser is not null))
            throw new ToolFailure("disk_http_full_browser_owner_required");
        if (truncated is not null && (!full || !ReferenceEquals(fullInventory, truncated.Baseline)))
            throw new ToolFailure("disk_http_truncated_owner_required");
        var assetPlan = truncated is not null ? DiskStaticHttpContract.CreateTruncatedAssetPlan(truncated) : full ? DiskStaticHttpContract.CreateAssetPlan(fullInventory!) : null;
        string directory = serial + ".disk-http";
        if (!directory.StartsWith(Path.Combine(root, "artifacts") + Path.DirectorySeparatorChar, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) || Path.Exists(directory))
            throw new ToolFailure("disk_http_fresh_artifacts_required");
        AuthorityFiles.RequirePlainPath(root, Path.GetDirectoryName(directory)!, "disk_http_path");
        Directory.CreateDirectory(directory);
        int port;
        using (TcpListener reservation = new(IPAddress.Loopback, 0))
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
            {
                cancellation.ThrowIfCancellationRequested();
                throw new ToolFailure("disk_http_runner_exited_before_peer");
            }

            await peer;
            var result = await runner;
            QemuTranscripts.RequireCompleteProcess(result, "disk_http_runner_incomplete");
            if (!result.Succeeded)
                throw new ToolFailure("disk_http_runner_failed");
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
                    throw new ToolFailure("disk_http_stopped_endpoint_live");
                using FileStream output = new(Path.Combine(directory, "stopped.json"), FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                await JsonSerializer.SerializeAsync(output, new
                {
                    Port = port,
                    EndpointRefused = true,
                    RunnerExit = result.ExitCode
                }, cancellationToken: cancellation);
                output.Flush(true);
            }

            return new(port, directory, NativeAotWebPayload.HashFile(Path.Combine(directory, "http.json")), result, full, truncated is not null);
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
                var joined = await runner;
                string Tail(string text) => text.Length <= 16384 ? text : text[^16384..];
                await Diagnostic("runner.json", new
                {
                    Schema = "clros-disk-http-runner-v1",
                    Joined = true,
                    joined.ExitCode,
                    joined.TimedOut,
                    joined.OutputLimitExceeded,
                    joined.Cancelled,
                    joined.TerminationConfirmed,
                    joined.FailureReason,
                    DurationTicks = joined.Duration.Ticks,
                    StdoutCharacters = joined.StandardOutput.Length,
                    StderrCharacters = joined.StandardError.Length,
                    StdoutTail = Tail(joined.StandardOutput),
                    StderrTail = Tail(joined.StandardError)
                });
            }
            catch (Exception error)
            {
                await Diagnostic("runner.json", new
                {
                    Schema = "clros-disk-http-runner-v1",
                    Joined = true,
                    Faulted = true,
                    Type = error.GetType().FullName,
                    error.HResult
                });
            }
        }

        // Bounded diagnostic values only. Failure to record must never replace
        // the original request/runner failure or bypass owned child joining.
        async Task Diagnostic(string name, object value)
        {
            try
            {
                byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(value);
                if (bytes.Length > 256 * 1024)
                    return;
                using FileStream output = new(Path.Combine(directory, name), FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                await output.WriteAsync(bytes, CancellationToken.None);
                output.Flush(true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        async Task Observe()
        {
            using var startup = CancellationTokenSource.CreateLinkedTokenSource(owned.Token);
            startup.CancelAfter(TimeSpan.FromSeconds(DiskStaticCapturePolicy.HttpReadinessTimeoutSeconds));
            await NativeAotSocketPeer.WaitForSerialAsync(serial, text => text.Contains("M10_018_LISTENING ", StringComparison.Ordinal), startup.Token);
            using var matrix = CancellationTokenSource.CreateLinkedTokenSource(owned.Token);
            matrix.CancelAfter(TimeSpan.FromSeconds(full ? DiskStaticCapturePolicy.HttpMatrixTimeoutSeconds : DiskStaticHttpContract.MatrixTimeoutSeconds));
            HttpClient Client() => new(new SocketsHttpHandler { AllowAutoRedirect = false, AutomaticDecompression = DecompressionMethods.None, UseProxy = false, MaxConnectionsPerServer = 1 })
            {
                BaseAddress = new Uri("http://127.0.0.1:" + port),
                Timeout = TimeSpan.FromSeconds(full ? 240 : 60),
                DefaultRequestVersion = HttpVersion.Version11,
                DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact
            };
            using HttpClient client = Client();
            List<Response> rows = [];
            bool complete = false;
            int totalBodyBytes = 0;
            async Task<Response> Request(HttpClient sender, int ordinal, string method, string path, int expected, IReadOnlyList<DiskStaticHttpContract.Header>? selected = null, uint? expectedBytes = null, string? expectedHash = null)
            {
                long started = Stopwatch.GetTimestamp();
                string stage = "prepare";
                try
                {
                    // The raw target is part of the contract: preserve encoded dot
                    // segments which an ordinary Uri constructor would normalize.
                    var uri = new Uri("http://127.0.0.1:" + port + path, new UriCreationOptions { DangerousDisablePathAndQueryCanonicalization = true });
                    using var request = new HttpRequestMessage(new HttpMethod(method), uri);
                    if (selected is not null)
                        foreach (var header in selected)
                            if (!request.Headers.TryAddWithoutValidation(header.Name, header.Value))
                                throw new ToolFailure("disk_http_request_header");
                    string[] headers = request.Headers.Select(h => h.Key + ": " + string.Join(",", h.Value)).ToArray();
                    stage = "headers";
                    using var response = await sender.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, matrix.Token);
                    stage = "body";
                    using var source = await response.Content.ReadAsStreamAsync(matrix.Token);
                    using MemoryStream body = new();
                    byte[] buffer = new byte[8192];
                    for (int read; (read = await source.ReadAsync(buffer, matrix.Token)) != 0;)
                    {
                        if (body.Length + read > (full ? DiskStaticHttpContract.MaximumAssetBytes : DiskStaticHttpContract.MaximumBodyBytes))
                            throw new ToolFailure("disk_http_body_capacity");
                        body.Write(buffer, 0, read);
                        bodyRead?.Invoke(ordinal, body.Length);
                    }

                    long finished = Stopwatch.GetTimestamp();
                    byte[] bytes = body.ToArray();
                    stage = "record";
                    if (Interlocked.Add(ref totalBodyBytes, bytes.Length) > (full ? DiskStaticHttpContract.MaximumExpectedBodyBytes : 5 * DiskStaticHttpContract.MaximumBodyBytes))
                        throw new ToolFailure("disk_http_aggregate_body_capacity");
                    string name = ordinal.ToString("D3", CultureInfo.InvariantCulture) + ".body";
                    using (FileStream output = new(Path.Combine(directory, name), FileMode.CreateNew, FileAccess.Write, FileShare.Read))
                    {
                        await output.WriteAsync(bytes, matrix.Token);
                        output.Flush(true);
                    }

                    List<string> responseHeaders = [];
                    int headerCharacters = 0;
                    foreach (var header in response.Headers.Concat(response.Content.Headers))
                    {
                        string line = header.Key + ": " + string.Join(",", header.Value);
                        if (responseHeaders.Count == 32 || line.Length is 0 or > 1024 || line.Any(c => c is < ' ' or > '~') || (headerCharacters += line.Length) > 4096)
                            throw new ToolFailure("disk_http_header_capacity");
                        responseHeaders.Add(line);
                    }

                    string hash = Convert.ToHexString(SHA256.HashData(bytes));
                    var row = new Response(ordinal, method, path, (int)response.StatusCode, headers, responseHeaders.ToArray(), bytes.Length, hash, name, started, finished);
                    lock (rows)
                        rows.Add(row);
                    stage = "validate";
                    if ((int)response.StatusCode != expected)
                        throw new ToolFailure("disk_http_status " + path);
                    if (expectedBytes.HasValue && bytes.Length != expectedBytes.Value || expectedHash is not null && hash != expectedHash)
                        throw new ToolFailure("disk_http_response " + method + " " + path);
                    return row;
                }
                catch (Exception error)
                {
                    List<object> chain = [];
                    for (Exception? current = error; current is not null && chain.Count < 8; current = current.InnerException)
                        chain.Add(new
                        {
                            Type = current.GetType().FullName,
                            current.HResult,
                            HttpRequestError = current is HttpRequestException http ? http.HttpRequestError.ToString() : null,
                            SocketError = current is SocketException socket ? socket.SocketErrorCode.ToString() : null,
                            NativeError = current is SocketException native ? (int?)native.NativeErrorCode : null
                        });
                    await Diagnostic("failure-" + ordinal.ToString("D3", CultureInfo.InvariantCulture) + ".json", new
                    {
                        Schema = "clros-disk-http-failure-v1",
                        Ordinal = ordinal,
                        Method = method,
                        Path = path,
                        Stage = stage,
                        Started = started,
                        Failed = Stopwatch.GetTimestamp(),
                        ClockFrequency = Stopwatch.Frequency,
                        MatrixCancelled = matrix.IsCancellationRequested,
                        OwnerCancelled = owned.IsCancellationRequested,
                        CallerCancelled = cancellation.IsCancellationRequested,
                        RunnerState = runner.Status.ToString(),
                        Exceptions = chain
                    });
                    throw;
                }
            }

            try
            {
                if (!full)
                {
                    foreach (string path in DiskStaticHttpContract.Paths)
                        _ = await Request(client, rows.Count, "GET", path, 200);
                }
                else
                {
                    foreach (string path in new[]
                    {
                        "/healthz",
                        "/config",
                        "/api/status",
                        "/api/items"
                    }

                    )
                        _ = await Request(client, rows.Count, "GET", path, 200);
                    foreach (var item in assetPlan!.Requests)
                        _ = await Request(client, rows.Count, item.Method, item.Target, item.Status, item.Headers, item.BodyBytes, item.BodySha256);
                    using HttpClient asset = Client(), json = Client();
                    var largest = DiskStaticHttpContract.ConcurrentAsset(assetPlan);
                    int at = rows.Count;
                    var pending = new[]
                    {
                        Request(asset, at, "GET", largest.Target + "?clros-concurrent=asset", 200, expectedBytes: largest.Bytes, expectedHash: largest.Sha256),
                        Request(json, at + 1, "GET", "/api/status?clros-concurrent=json", 200)
                    };
                    try
                    {
                        _ = await Task.WhenAll(pending);
                    }
                    finally
                    {
                        foreach (var task in pending)
                            try
                            {
                                _ = await task;
                            }
                            catch
                            {
                            }
                    }

                    // Both finite HTTP owners close before browser entry. The
                    // browser owns its own fresh cache, network and process.
                    asset.Dispose();
                    json.Dispose();
                    client.Dispose();
                    // The matrix deadline applies to HTTP qualification. Browser
                    // startup has its own finite deadline and remains linked to
                    // the outer owner, which cancels and joins both children.
                    await browser!(port, Path.Combine(directory, "browser"), owned.Token);
                }

                complete = true;
            }
            finally
            {
                using FileStream receipt = new(Path.Combine(directory, "http.json"), FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                if (full)
                    await JsonSerializer.SerializeAsync(receipt, new
                    {
                        Schema = truncated is null ? "clros-disk-http-full-v1" : "clros-disk-http-truncated-v1",
                        Scope = truncated is null ? "disk-static-full-v1" : "disk-static-truncated-v1",
                        Port = port,
                        Complete = complete,
                        ClockFrequency = Stopwatch.Frequency,
                        ImageSha256 = assetPlan!.ImageSha256,
                        PublishedInventorySha256 = assetPlan.PublishedInventorySha256,
                        Responses = rows.OrderBy(r => r.Ordinal).ToArray()
                    }, cancellationToken: CancellationToken.None);
                else
                    await JsonSerializer.SerializeAsync(receipt, new
                    {
                        Schema = DiskStaticHttpContract.Schema,
                        Scope = DiskStaticHttpContract.Scope,
                        Port = port,
                        Complete = complete,
                        ClockFrequency = Stopwatch.Frequency,
                        Responses = rows.OrderBy(r => r.Ordinal).ToArray()
                    }, cancellationToken: CancellationToken.None);
                receipt.Flush(true);
            }
        }
    }
}
