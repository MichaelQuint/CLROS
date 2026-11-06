using System.Text.Json;
using OS.Tooling;

internal static partial class HttpWorkloadBenchmarkTests
{
    [HostTest("HTTP benchmark separates first and ten warm sequences with exact inventory bodies and joined concurrency")]
    private static void Sequences() => WithAsset((root, inventory, body) =>
    {
        var transport = new Transport(inventory, body, "good");
        string directory = Fresh(root);
        var report = HttpWorkloadBenchmark.RunCoreAsync(root, directory, Endpoint, inventory,
            Options(), transport, CancellationToken.None).GetAwaiter().GetResult();
        HostCheck.True(report.Complete && report.Samples.Count == 88 && report.Distributions.Count == 16);
        HostCheck.True(!report.ProductionPerformanceAccepted && !report.FullAndCleanupQualified && !report.BrowserQualified);
        HostCheck.True(report.Samples.All(sample => sample.Complete && sample.Started <= sample.HeadersReceived &&
            sample.HeadersReceived <= sample.BodyReceived && sample.BodyReceived <= sample.BodyWriteStarted &&
            sample.BodyWriteStarted <= sample.BodyWritten && sample.BodyWritten <= sample.BodyFlushed &&
            sample.BodyFlushed <= sample.Finished));
        HostCheck.True(report.Distributions.Where(group => group.Phase == "first").All(group => group.Count == 1) &&
            report.Distributions.Where(group => group.Phase == "warm").All(group => group.Count == 10));
        HostCheck.True(report.Samples.Where(sample => sample.Case == "asset").All(sample => sample.Bytes == body.Length) &&
            report.Samples.Where(sample => sample.Case == "range").All(sample => sample.Bytes == 65536));
        HostCheck.True(transport.MaximumInFlight == 2 && transport.ActiveBodies == 0 && transport.Disposed);
        foreach (var sample in report.Samples)
            HostCheck.Equal(sample.Sha256, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                File.ReadAllBytes(Path.Combine(directory, sample.BodyFile)))));
        using var receipt = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(directory, "benchmark.json")));
        HostCheck.True(receipt.RootElement.GetProperty("Complete").GetBoolean() &&
            receipt.RootElement.GetProperty("Samples").GetArrayLength() == 88);
    });

    [HostTest("HTTP benchmark preserves failed bodies and rejects bad status JSON metadata content and bounds")]
    private static void Rejections() => WithAsset((root, inventory, body) =>
    {
        foreach (string mode in new[] { "status", "json", "json-type", "health", "head-etag", "head-length",
            "range-header", "range-body", "asset-body", "oversized-json", "headers", "interrupted" })
        {
            string directory = Fresh(root);
            var transport = new Transport(inventory, body, mode);
            bool rejected = false;
            try
            {
                _ = HttpWorkloadBenchmark.RunCoreAsync(root, directory, Endpoint, inventory,
                    Options(0), transport, CancellationToken.None).GetAwaiter().GetResult();
            }
            catch (Exception error) when (error is ToolFailure or IOException)
            {
                rejected = true;
            }
            using var receipt = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(directory, "benchmark.json")));
            var failed = receipt.RootElement.GetProperty("Samples").EnumerateArray().Last();
            HostCheck.True(rejected && !receipt.RootElement.GetProperty("Complete").GetBoolean() &&
                !failed.GetProperty("Complete").GetBoolean() && transport.ActiveBodies == 0 && transport.Disposed, mode);
            byte[] captured = File.ReadAllBytes(Path.Combine(directory, failed.GetProperty("BodyFile").GetString()!));
            HostCheck.True(captured.Length == failed.GetProperty("CapturedBytes").GetInt32() &&
                Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(captured)) ==
                failed.GetProperty("Sha256").GetString(), mode);
            if (mode == "oversized-json")
                HostCheck.True(failed.GetProperty("Bytes").GetInt32() == 1025 && captured.Length == 1024 &&
                    !failed.GetProperty("BodyComplete").GetBoolean(), mode);
            if (mode == "interrupted")
                HostCheck.True(failed.GetProperty("Bytes").GetInt32() == 1 && captured.Length == 1 &&
                    !failed.GetProperty("BodyComplete").GetBoolean(), mode);
        }
    });

    [HostTest("HTTP benchmark covers streamed-body deadline and cancels then joins a failing concurrent sibling")]
    private static void Cancellation() => WithAsset((root, inventory, body) =>
    {
        foreach (string mode in new[] { "body-timeout", "concurrent-failure" })
        {
            string directory = Fresh(root);
            var transport = new Transport(inventory, body, mode);
            bool rejected = false;
            try
            {
                _ = HttpWorkloadBenchmark.RunCoreAsync(root, directory, Endpoint, inventory,
                    Options(0, TimeSpan.FromMilliseconds(200)), transport, CancellationToken.None).GetAwaiter().GetResult();
            }
            catch (Exception error) when (error is OperationCanceledException or ToolFailure)
            {
                rejected = true;
            }
            using var receipt = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(directory, "benchmark.json")));
            var samples = receipt.RootElement.GetProperty("Samples").EnumerateArray().ToArray();
            HostCheck.True(rejected && transport.ActiveBodies == 0 && transport.Disposed &&
                !receipt.RootElement.GetProperty("Complete").GetBoolean(), mode);
            if (mode == "body-timeout")
                HostCheck.True(samples.Length == 1 && samples[0].GetProperty("HeadersReceived").ValueKind == JsonValueKind.Number &&
                    samples[0].GetProperty("BodyReceived").ValueKind == JsonValueKind.Null &&
                    samples[0].GetProperty("Bytes").GetInt32() == 1 &&
                    samples[0].GetProperty("CapturedBytes").GetInt32() == 1 &&
                    File.ReadAllBytes(Path.Combine(directory, samples[0].GetProperty("BodyFile").GetString()!)).Length == 1 &&
                    !samples[0].GetProperty("BodyComplete").GetBoolean());
            else
                HostCheck.True(samples.Length == 7 && samples.Count(sample => !sample.GetProperty("Complete").GetBoolean()) == 2);
        }
    });

    [HostTest("HTTP benchmark invalid controls reject before creating outputs or sending requests")]
    private static void Controls() => WithAsset((root, inventory, body) =>
    {
        foreach (var options in new[]
        {
            new HttpWorkloadBenchmark.Options { System = "unknown", Backend = "whpx" },
            new HttpWorkloadBenchmark.Options { System = "CLROS", Backend = "auto" },
            Options(-1), Options(11), Options(0, TimeSpan.Zero), Options(0, TimeSpan.FromSeconds(181))
        })
        {
            string directory = Fresh(root);
            using var transport = new Transport(inventory, body, "good");
            bool rejected = false;
            try
            {
                _ = HttpWorkloadBenchmark.RunCoreAsync(root, directory, Endpoint, inventory,
                    options, transport, CancellationToken.None).GetAwaiter().GetResult();
            }
            catch (ToolFailure)
            {
                rejected = true;
            }
            HostCheck.True(rejected && !Path.Exists(directory) && transport.Requests == 0);
        }
    });

    private static readonly Uri Endpoint = new("http://127.0.0.1:1/");

    private static HttpWorkloadBenchmark.Options Options(int warm = 10, TimeSpan? timeout = null) =>
        new()
        {
            System = "CLROS",
            Backend = "whpx",
            WarmSequences = warm,
            RequestTimeout = timeout ?? TimeSpan.FromSeconds(180)
        };

    private static string Fresh(string root) => Path.Combine(root, "artifacts", "tests",
        "http-benchmark-" + Guid.NewGuid().ToString("N"));

    private static void WithAsset(Action<string, WebRootDiskInventory.Inspection, byte[]> test) =>
        WebRootImageFixtureTests.WithImage((root, directory, image, source) =>
        {
            Directory.CreateDirectory(Path.Combine(root, "artifacts", "tests"));
            byte[] body = Enumerable.Range(0, 70001).Select(index => (byte)(index * 37 ^ (index >> 9))).ToArray();
            File.WriteAllBytes(Path.Combine(source, "_framework", "System.Private.CoreLib.wasm"), body);
            string imported = Path.Combine(directory, "benchmark.raw");
            var receipt = WebRootImageFixture.Apply(root, image, source,
                WebRootImageFixture.SnapshotPublishedRoot(source), imported);
            test(root, WebRootDiskInventory.Inspect(imported, File.ReadAllBytes(imported), receipt), body);
        });
}
