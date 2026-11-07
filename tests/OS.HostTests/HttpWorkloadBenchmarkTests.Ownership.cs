using System.Text.Json;
using OS.Tooling;

internal static partial class HttpWorkloadBenchmarkTests
{
    [HostTest("HTTP benchmark retains the request failure when its receipt cannot be created")]
    private static void ReceiptFailure() => WithAsset((root, inventory, body) =>
    {
        string directory = Fresh(root);
        var inner = new Transport(inventory, body, "json");
        var transport = new ReceiptFailureTransport(directory, inner);
        AggregateException? failure = null;
        try
        {
            _ = HttpWorkloadBenchmark.RunCoreAsync(root, directory, Endpoint, inventory,
                Options(0), transport, CancellationToken.None).GetAwaiter().GetResult();
        }
        catch (AggregateException error)
        {
            failure = error;
        }
        HostCheck.True(failure is not null && failure.InnerExceptions.Count == 2 &&
            failure.InnerExceptions[0] is ToolFailure { Reason: "disk_timing_json_response" } &&
            failure.InnerExceptions[1] is IOException or UnauthorizedAccessException);
        HostCheck.True(inner.Disposed && inner.ActiveBodies == 0);
        HostCheck.True(File.ReadAllBytes(Path.Combine(directory, "000.body")).AsSpan().SequenceEqual("{}"u8));
    });

    [HostTest("HTTP benchmark owner observes before browser and deliberately joins a stopped endpoint")]
    private static void OwnerCompletion() => WithAsset((root, inventory, body) =>
    {
        var host = new BenchmarkHost(root, inventory, "good");
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        int browsers = 0;
        var observed = NativeAotHttpBenchmarkPeer.RunAsync(root, host.Serial, host.RunAsync,
            inventory, Options(1), DiskStaticCapturePolicy.DefaultTimeoutSeconds,
            (port, directory, token) =>
            {
                HostCheck.True(host.Requests.Count == 16 && port == host.Port && token.CanBeCanceled);
                Directory.CreateDirectory(directory);
                browsers++;
                return Task.CompletedTask;
            }, stop.Token).GetAwaiter().GetResult();
        HostCheck.True(host.Launches == 1 && host.Joined && host.OwnerCancelled && browsers == 1);
        HostCheck.True(observed.Runner.Cancelled && observed.Observations.Complete &&
            observed.Observations.Samples.Count == 16 && !observed.BrowserQualified && !observed.FullAndCleanupQualified);
        using var receipt = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(observed.Directory, "owner.json")));
        HostCheck.True(receipt.RootElement.GetProperty("Complete").GetBoolean() &&
            receipt.RootElement.GetProperty("EndpointRefused").GetBoolean() &&
            receipt.RootElement.GetProperty("RunnerJoined").GetBoolean());
    });

    [HostTest("HTTP benchmark owner joins early exits and wrong HTTP before browser entry")]
    private static void OwnerFailures() => WithAsset((root, inventory, body) =>
    {
        foreach (string mode in new[] { "early", "bad-status" })
        {
            var host = new BenchmarkHost(root, inventory, mode);
            using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            bool browser = false, rejected = false;
            try
            {
                _ = NativeAotHttpBenchmarkPeer.RunAsync(root, host.Serial, host.RunAsync,
                    inventory, Options(0), DiskStaticCapturePolicy.DefaultTimeoutSeconds,
                    (_, _, _) =>
                    {
                        browser = true;
                        return Task.CompletedTask;
                    }, stop.Token).GetAwaiter().GetResult();
            }
            catch (ToolFailure)
            {
                rejected = true;
            }
            HostCheck.True(rejected && !browser && host.Joined && host.Launches == 1);
            using var receipt = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(host.Serial + ".http-benchmark", "owner.json")));
            HostCheck.True(!receipt.RootElement.GetProperty("Complete").GetBoolean() &&
                receipt.RootElement.GetProperty("RunnerJoined").GetBoolean());
        }
    });

    [HostTest("HTTP benchmark owner cancellation joins an incomplete streamed body and preserves its prefix")]
    private static void OwnerCancellation() => WithAsset((root, inventory, body) =>
    {
        var host = new BenchmarkHost(root, inventory, "partial");
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        bool browser = false, cancelled = false;
        var pending = NativeAotHttpBenchmarkPeer.RunAsync(root, host.Serial, host.RunAsync,
            inventory, Options(0), DiskStaticCapturePolicy.DefaultTimeoutSeconds,
            (_, _, _) =>
            {
                browser = true;
                return Task.CompletedTask;
            }, stop.Token);
        host.BodyStarted.Task.WaitAsync(stop.Token).GetAwaiter().GetResult();
        stop.Cancel();
        try
        {
            _ = pending.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
        }
        HostCheck.True(cancelled && host.Joined && host.OwnerCancelled &&
            host.BodyFinished.Task.IsCompletedSuccessfully && !browser);
        string directory = Path.Combine(host.Serial + ".http-benchmark", "requests");
        using var report = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(directory, "benchmark.json")));
        var sample = report.RootElement.GetProperty("Samples").EnumerateArray().Last();
        HostCheck.True(!sample.GetProperty("BodyComplete").GetBoolean() &&
            sample.GetProperty("CapturedBytes").GetInt64() <= 8 &&
            File.ReadAllBytes(Path.Combine(directory, sample.GetProperty("BodyFile").GetString()!)).Length <= 8);
    });

    [HostTest("HTTP benchmark owner retains browser and receipt failures after runner cleanup")]
    private static void OwnerBrowserFailure() => WithAsset((root, inventory, body) =>
    {
        var host = new BenchmarkHost(root, inventory, "good");
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        AggregateException? failure = null;
        try
        {
            _ = NativeAotHttpBenchmarkPeer.RunAsync(root, host.Serial, host.RunAsync,
                inventory, Options(0), DiskStaticCapturePolicy.DefaultTimeoutSeconds,
                (_, _, _) =>
                {
                    Directory.CreateDirectory(Path.Combine(host.Serial + ".http-benchmark", "owner.json"));
                    throw new ToolFailure("synthetic_browser_failure");
                }, stop.Token).GetAwaiter().GetResult();
        }
        catch (AggregateException error)
        {
            failure = error;
        }
        HostCheck.True(host.Joined && host.OwnerCancelled && failure is not null &&
            failure.InnerExceptions.Any(error => error is ToolFailure { Reason: "synthetic_browser_failure" }) &&
            failure.InnerExceptions.Any(error => error is IOException or UnauthorizedAccessException));
    });

    [HostTest("HTTP benchmark owner rejects failed runner dispositions late guest failures and competing binds")]
    private static void OwnerRunnerFailures() => WithAsset((root, inventory, body) =>
    {
        foreach (string mode in new[] { "timeout", "output", "unconfirmed", "failure", "serial-failure", "bind" })
        {
            var host = new BenchmarkHost(root, inventory, "good");
            using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            bool rejected = false;
            try
            {
                _ = NativeAotHttpBenchmarkPeer.RunAsync(root, host.Serial, Runner, inventory,
                    Options(0), DiskStaticCapturePolicy.DefaultTimeoutSeconds,
                    (_, _, _) => Task.CompletedTask, stop.Token).GetAwaiter().GetResult();
            }
            catch (Exception error) when (error is ToolFailure or AggregateException)
            {
                rejected = true;
            }
            HostCheck.True(rejected && host.Joined && host.Launches == 1, mode);
            using var receipt = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(host.Serial + ".http-benchmark", "owner.json")));
            HostCheck.True(!receipt.RootElement.GetProperty("Complete").GetBoolean());
            if (mode != "bind")
                HostCheck.True(receipt.RootElement.GetProperty("BrowserCallbackCompleted").GetBoolean() &&
                    receipt.RootElement.GetProperty("Failure").GetString() ==
                    (mode == "serial-failure" ? "http_benchmark_owner_guest_failure" : "http_benchmark_owner_runner_join"));
            else
                HostCheck.True(!receipt.RootElement.GetProperty("BrowserCallbackCompleted").GetBoolean() &&
                    receipt.RootElement.GetProperty("CleanupFailure").ValueKind == JsonValueKind.String);

            async Task<ProcessResult> Runner(int port, CancellationToken token)
            {
                using var occupied = mode == "bind" ? new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, port) : null;
                occupied?.Start();
                var result = await host.RunAsync(port, token);
                if (mode == "serial-failure")
                    File.AppendAllText(host.Serial, "TEST_FAIL synthetic-late-guest\n");
                return mode switch
                {
                    "timeout" => result with { TimedOut = true },
                    "output" => result with { OutputLimitExceeded = true },
                    "unconfirmed" => result with { TerminationConfirmed = false },
                    "failure" => result with { FailureReason = "synthetic-runner-failure" },
                    _ => result
                };
            }
        }
    });
}
