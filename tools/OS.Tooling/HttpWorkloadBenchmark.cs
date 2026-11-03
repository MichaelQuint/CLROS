using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;

namespace OS.Tooling;

/// <summary>
/// Observes an explicit first sequence and up to ten warm sequences against an
/// already owned HTTP endpoint, using the actual disk inventory's asset bytes.
/// JSON, health, HEAD,64KiB range, complete asset and concurrent asset/JSON requests
/// retain exact assertions. Each request has the existing180-second ceiling.
/// </summary>
/// <remarks>
/// This host-only observer does not start, extend or stop a guest. Its caller
/// owns readiness, source/native/device/input guards, the existing outer profile
/// deadline and final process cleanup. Cancellation joins both concurrent requests
/// before return; failed observations remain in a fresh artifact directory.
/// It neither performs browser startup nor grants original guest acceptance.
/// </remarks>
public static partial class HttpWorkloadBenchmark
{
    public static Task<Report> RunAsync(string root, string directory, Uri endpoint,
        WebRootDiskInventory.Inspection inventory, Options options,
        CancellationToken cancellation = default) =>
        RunCoreAsync(root, directory, endpoint, inventory, options, null, cancellation);

    // An injected HTTP transport is a host control, never a guest qualification.
    internal static async Task<Report> RunCoreAsync(string root, string directory, Uri endpoint,
        WebRootDiskInventory.Inspection inventory, Options options, HttpMessageHandler? transport,
        CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        if (!endpoint.IsAbsoluteUri || endpoint.Scheme != "http" || endpoint.UserInfo.Length != 0 ||
            endpoint.AbsolutePath != "/" || endpoint.Query.Length != 0 || endpoint.Fragment.Length != 0)
            throw new ToolFailure("http_benchmark_endpoint");

        var file = inventory.Find(WebRootDiskInventory.Root + NativeAotDiskTimingPeer.Target);
        if (file is null || file.IsDirectory || file.Bytes < 65536 ||
            file.Bytes > DiskStaticHttpContract.MaximumAssetBytes)
            throw new ToolFailure("http_benchmark_asset");
        var asset = new DiskStaticHttpContract.Asset(file, true);
        byte[] fullBody = file.CopyBytes();
        byte[] rangeBody = fullBody[..65536];
        byte[] healthBody = "OS_MINIMAL_API_OK"u8.ToArray();

        root = Path.GetFullPath(root);
        directory = Path.GetFullPath(directory, root);
        if (!directory.StartsWith(Path.Combine(root, "artifacts") + Path.DirectorySeparatorChar,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) ||
            Path.Exists(directory))
            throw new ToolFailure("http_benchmark_fresh_artifacts");
        AuthorityFiles.RequirePlainPath(root, Path.GetDirectoryName(directory)!, "http_benchmark_path");
        Directory.CreateDirectory(directory);

        using HttpClient client = new(transport ?? new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.None,
            UseProxy = false,
            MaxConnectionsPerServer = 2
        })
        {
            BaseAddress = endpoint,
            Timeout = Timeout.InfiniteTimeSpan,
            DefaultRequestVersion = HttpVersion.Version11,
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact
        };
        List<Sample> samples = [];
        int nextOrdinal = 0;
        bool complete = false;
        string? failure = null;
        try
        {
            for (int sequence = 0; sequence <= options.WarmSequences; sequence++)
            {
                await Request(sequence, "json", "GET", "/api/status", 200, null);
                await Request(sequence, "health", "GET", "/healthz", 200, healthBody);
                await Request(sequence, "head", "HEAD", NativeAotDiskTimingPeer.Target, 200, []);
                await Request(sequence, "range", "GET", NativeAotDiskTimingPeer.Target, 206, rangeBody);
                await Request(sequence, "asset", "GET", NativeAotDiskTimingPeer.Target, 200, fullBody);

                // A failed member cancels its sibling; WhenAll joins both before
                // the report is sealed or the caller can release the endpoint.
                using var pair = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
                await Task.WhenAll(Concurrent("concurrent-asset", NativeAotDiskTimingPeer.Target, fullBody),
                    Concurrent("concurrent-json", "/api/status", null));
                await Request(sequence, "health-after", "GET", "/healthz", 200, healthBody);

                async Task Concurrent(string name, string path, byte[]? expected)
                {
                    try
                    {
                        await Request(sequence, name, "GET", path, 200, expected, pair.Token);
                    }
                    catch
                    {
                        pair.Cancel();
                        throw;
                    }
                }
            }

            cancellation.ThrowIfCancellationRequested();
            complete = true;
        }
        catch (Exception error)
        {
            failure = error is ToolFailure tool ? tool.Reason : error.GetType().FullName;
            throw;
        }
        finally
        {
            var report = CreateReport();
            using FileStream receipt = new(Path.Combine(directory, "benchmark.json"),
                FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            await JsonSerializer.SerializeAsync(receipt, report, cancellationToken: CancellationToken.None);
            receipt.Flush(true);
        }

        return CreateReport();

        Report CreateReport()
        {
            Sample[] ordered;
            lock (samples)
                ordered = samples.OrderBy(sample => sample.Ordinal).ToArray();
            return new("clros-http-workload-benchmark-v1", options.System, options.Backend,
                inventory.ImageSha256, asset.Sha256, asset.Bytes, Stopwatch.Frequency,
                options.WarmSequences, options.RequestTimeout.Ticks, complete, failure,
                ordered, Summarize(ordered, Stopwatch.Frequency));
        }

        async Task Request(int sequence, string name, string method, string path, int status, byte[]? expected,
            CancellationToken? requestCancellation = null)
        {
            int ordinal = Interlocked.Increment(ref nextOrdinal) - 1;
            long started = Stopwatch.GetTimestamp();
            long? headersReceived = null, bodyReceived = null, bodyWriteStarted = null, bodyWritten = null, bodyFlushed = null;
            string[] requestHeaders = [], responseHeaders = [];
            int? actualStatus = null;
            long receivedBytes = 0;
            string? hash = null, requestFailure = null;
            string bodyFile = ordinal.ToString("D3", CultureInfo.InvariantCulture) + ".body";
            bool valid = false;
            using MemoryStream body = new();
            try
            {
                CancellationToken owner = requestCancellation ?? cancellation;
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(owner);
                deadline.CancelAfter(options.RequestTimeout);
                using var request = new HttpRequestMessage(new HttpMethod(method), path);
                if (status == 206)
                    request.Headers.Range = new(0, rangeBody.Length - 1);
                requestHeaders = CopyHeaders(request.Headers);
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
                headersReceived = Stopwatch.GetTimestamp();
                actualStatus = (int)response.StatusCode;
                responseHeaders = CopyHeaders(response.Headers.Concat(response.Content.Headers));
                using var source = await response.Content.ReadAsStreamAsync(deadline.Token);
                byte[] buffer = new byte[8192];
                int limit = expected?.Length ?? 1024;
                for (int count; (count = await source.ReadAsync(buffer, deadline.Token)) != 0;)
                {
                    receivedBytes += count;
                    int retain = Math.Min(count, limit - checked((int)body.Length));
                    body.Write(buffer, 0, retain);
                    if (receivedBytes > limit)
                        throw new ToolFailure("http_benchmark_body_bound");
                }

                bodyReceived = Stopwatch.GetTimestamp();
                byte[] actual = body.ToArray();
                hash = Convert.ToHexString(SHA256.HashData(actual));
                await SaveBody(actual, owner);

                if (actualStatus != status || expected is not null && !actual.AsSpan().SequenceEqual(expected))
                    throw new ToolFailure("http_benchmark_response");
                if (expected is null)
                    NativeAotDiskTimingPeer.InspectStatusJson(actual, response.Content.Headers.ContentType?.MediaType);
                if (path == NativeAotDiskTimingPeer.Target &&
                    (response.Headers.ETag?.ToString() != asset.ETag ||
                    response.Content.Headers.ContentType?.ToString() != asset.MediaType ||
                    response.Content.Headers.ContentLength != (status == 206 ? rangeBody.Length : asset.Bytes) ||
                    (status == 206 ? response.Content.Headers.ContentRange?.ToString() !=
                        $"bytes 0-{rangeBody.Length - 1}/{asset.Bytes}" : response.Content.Headers.ContentRange is not null)))
                    throw new ToolFailure("http_benchmark_metadata");
                valid = true;
            }
            catch (Exception error)
            {
                requestFailure = error is ToolFailure tool ? tool.Reason : error.GetType().FullName;
                if (bodyFlushed is null)
                {
                    // An interrupted or oversized response still has a bounded
                    // original prefix. Preserve it after cancellation, without
                    // inventing EOF or overwriting a failed body-file attempt.
                    byte[] captured = body.ToArray();
                    hash = Convert.ToHexString(SHA256.HashData(captured));
                    if (File.Exists(Path.Combine(directory, bodyFile)))
                        bodyFile = ordinal.ToString("D3", CultureInfo.InvariantCulture) + ".failed.body";
                    try
                    {
                        await SaveBody(captured, CancellationToken.None);
                    }
                    catch (Exception evidenceError)
                    {
                        throw new AggregateException("http_benchmark_partial_evidence", error, evidenceError);
                    }
                }
                throw;
            }
            finally
            {
                var sample = new Sample(ordinal, sequence, name, method, path, requestHeaders, responseHeaders, started,
                    headersReceived, bodyReceived, bodyWriteStarted, bodyWritten, bodyFlushed, Stopwatch.GetTimestamp(),
                    actualStatus, receivedBytes, body.Length, hash, bodyFile,
                    bodyReceived.HasValue && body.Length == receivedBytes, valid, requestFailure);
                lock (samples)
                    samples.Add(sample);
            }

            async Task SaveBody(byte[] actual, CancellationToken token)
            {
                bodyWriteStarted = Stopwatch.GetTimestamp();
                bodyWritten = null;
                bodyFlushed = null;
                using FileStream output = new(Path.Combine(directory, bodyFile),
                    FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                await output.WriteAsync(actual, token);
                bodyWritten = Stopwatch.GetTimestamp();
                output.Flush(true);
                bodyFlushed = Stopwatch.GetTimestamp();
            }
        }
    }

    internal static IReadOnlyList<Distribution> Summarize(IEnumerable<Sample> samples, long frequency)
    {
        List<Distribution> distributions = [];
        foreach (var group in samples.Where(sample => sample.Complete)
            .GroupBy(sample => (sample.Case, Phase: sample.Sequence == 0 ? "first" : "warm")))
        {
            Sample[] rows = group.OrderBy(sample => sample.Sequence).ToArray();
            double[] network = rows.Select(sample => Milliseconds(sample.BodyReceived!.Value - sample.Started)).ToArray();
            double[] sorted = network.Order().ToArray();
            double median = (sorted[(sorted.Length - 1) / 2] + sorted[sorted.Length / 2]) / 2;
            distributions.Add(new(group.Key.Case, group.Key.Phase, rows.Length, sorted[0], median,
                network.Average(), sorted[^1], network,
                rows.Select(sample => Milliseconds(sample.BodyWritten!.Value - sample.BodyWriteStarted!.Value)).ToArray(),
                rows.Select(sample => Milliseconds(sample.BodyFlushed!.Value - sample.BodyWritten!.Value)).ToArray()));
        }
        return distributions;

        double Milliseconds(long ticks) => 1000d * ticks / frequency;
    }

    private static string[] CopyHeaders(IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers)
    {
        List<string> result = [];
        int characters = 0;
        foreach (var header in headers)
        {
            string line = header.Key + ": " + string.Join(",", header.Value);
            if (result.Count == 32 || line.Length is 0 or > 1024 || line.Any(c => c is < ' ' or > '~') ||
                (characters += line.Length) > 4096)
                throw new ToolFailure("http_benchmark_headers");
            result.Add(line);
        }
        return result.ToArray();
    }
}
