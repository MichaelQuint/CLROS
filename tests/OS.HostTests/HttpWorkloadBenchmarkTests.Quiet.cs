using System.Text.Json;
using OS.Tooling;

internal static partial class HttpWorkloadBenchmarkTests
{
    [HostTest("quiet HTTP owner retains all88 first-plus-ten responses and joins without inventing browser completion")]
    private static void QuietCompletion() => WithAsset((root, inventory, body) =>
    {
        var host = new BenchmarkHost(root, inventory, "good", quiet: true);
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = NativeAotHttpBenchmarkPeer.RunQuietAsync(root, host.Serial, host.RunAsync,
            inventory, Options(10), DiskStaticCapturePolicy.DefaultTimeoutSeconds, stop.Token).GetAwaiter().GetResult();
        HostCheck.True(result.Observations.Complete && result.Observations.Samples.Count == 88 &&
            host.Joined && host.OwnerCancelled && host.Requests.Count == 88 && !Directory.Exists(Path.Combine(result.Directory, "browser")));
        HostCheck.True(result.Observations.Distributions.All(row => row.Phase == "first" ? row.Count == 1 : row.Count == 10));
        using var receipt = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(result.Directory, "owner.json")));
        HostCheck.True(receipt.RootElement.GetProperty("Schema").GetString() == "clros-quiet-http-benchmark-owner-v1" &&
            receipt.RootElement.GetProperty("Complete").GetBoolean() && receipt.RootElement.GetProperty("RunnerJoined").GetBoolean() &&
            receipt.RootElement.GetProperty("EndpointRefused").GetBoolean() && !receipt.RootElement.GetProperty("BrowserCallbackCompleted").GetBoolean() &&
            !result.BrowserQualified && !result.FullAndCleanupQualified && !result.ProductionPerformanceAccepted);
    });

    [HostTest("quiet HTTP owner rejects bad bodies and joins cancelled partial streams")]
    private static void QuietFailures() => WithAsset((root, inventory, body) =>
    {
        foreach (string mode in new[] { "bad-status", "partial" })
        {
            var host = new BenchmarkHost(root, inventory, mode, quiet: true);
            using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var pending = NativeAotHttpBenchmarkPeer.RunQuietAsync(root, host.Serial, host.RunAsync,
                inventory, Options(0), DiskStaticCapturePolicy.DefaultTimeoutSeconds, stop.Token);
            if (mode == "partial")
            {
                host.BodyStarted.Task.WaitAsync(stop.Token).GetAwaiter().GetResult();
                stop.Cancel();
            }
            bool rejected = false;
            try
            {
                _ = pending.GetAwaiter().GetResult();
            }
            catch (Exception error) when (error is ToolFailure or OperationCanceledException)
            {
                rejected = true;
            }
            HostCheck.True(rejected && host.Joined && host.OwnerCancelled);
            using var receipt = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(host.Serial + ".http-benchmark", "owner.json")));
            HostCheck.True(!receipt.RootElement.GetProperty("Complete").GetBoolean() && receipt.RootElement.GetProperty("RunnerJoined").GetBoolean() &&
                !receipt.RootElement.GetProperty("BrowserCallbackCompleted").GetBoolean());
        }
    });

    [HostTest("quiet readiness requires the ordered quiet marker and listener and rejects failures or tracing")]
    private static void QuietReadiness()
    {
        const string marker = "CLROS_HTTP_BENCH_READY\r\n";
        const string listener = "info: Microsoft.Hosting.Lifetime[14]\r\n      Now listening on: http://0.0.0.0:8080\r\n";
        HostCheck.True(NativeAotHttpBenchmarkPeer.IsQuietReady("M10_018_BOOT trace-before-ready\r\n" + marker + listener));
        foreach (string text in new[] { "", marker, listener, listener + marker,
            "X" + marker + listener, "CLROS_HTTP_BENCH_READY\n" + listener,
            "CLROS_HTTP_BENCH_READY", "M10_018_LISTENING synthetic-host-only\r\n",
            marker + "Now listening on: http://0.0.0.0:8081\r\n" })
            HostCheck.True(!NativeAotHttpBenchmarkPeer.IsQuietReady(text));
        foreach (string failure in new[] { "CLROS_HTTP_BENCH_FAIL", "TEST_FAIL", "OS_SMOKE_FAIL",
            "M16_025_END forbidden-trace", "M10_018_LISTENING forbidden-trace" })
        {
            bool rejected = false;
            try
            {
                _ = NativeAotHttpBenchmarkPeer.IsQuietReady(marker + listener + failure + "\r\n");
            }
            catch (ToolFailure)
            {
                rejected = true;
            }
            HostCheck.True(rejected, failure);
        }
    }

    [HostTest("quiet matrix selection retains exact serving flags and rejects duplicate stimulus owners")]
    private static void QuietPlanning()
    {
        var options = FullOptions("whpx", "-EnableM10018ServingBenchmarkProbe", "-HttpBenchmarkWarmSequences", "10");
        var plan = QemuProfiles.Create(options);
        HostCheck.True(plan.IsEnabled("EnableM10018ServingBenchmarkProbe") && plan.IsEnabled("EnableM10018FullProbe") &&
            !plan.IsEnabled("EnableM10018TimingProbe") && !plan.IsEnabled("EnableM10018EvidenceTraceProbe") && !plan.IsEnabled("EnableM10018EvidenceSummaryProbe"));
        using StringWriter output = new();
        int result = QemuSmoke.RunAsync(Repository.FindRoot(), options, output,
            observeQuietServing: (_, _, _) => throw new InvalidOperationException("must not enter")).GetAwaiter().GetResult();
        HostCheck.True(result != 0 && output.ToString().Contains("quiet_serving_duplicate_client_owner", StringComparison.Ordinal));
    }
}
