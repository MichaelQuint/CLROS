using System.Text.Json;
using OS.Tooling;

internal static partial class NativeAotDiskStaticCaptureTests
{
    [HostTest("disk static peer preserves status and receipt errors and joins its cancelled host")]
    private static async Task ReceiptFailure()
    {
        // Real loopback host control only; synthetic readiness is not guest evidence.
        var host = new Host(Mode.WrongStatusReceiptFailure);
        using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        Task<NativeAotDiskStaticPeer.Completion> operation = NativeAotDiskStaticPeer.RunAsync(
            host.Root, host.Serial, host.RunAsync, watchdog.Token);
        Exception? observed = null;
        bool observedBeforeWatchdog = false;
        try
        {
            try
            {
                _ = await operation;
            }
            catch (Exception error)
            {
                observed = error;
                observedBeforeWatchdog = !watchdog.IsCancellationRequested;
            }
        }
        finally
        {
            watchdog.Cancel();
            try
            {
                _ = await operation;
            }
            catch
            {
            }
        }

        HostCheck.True(observed is not null && observedBeforeWatchdog &&
            host.Launches == 1 && host.Joined && host.OwnerCancelled,
            "the original status error and receipt collision must precede the watchdog and join the host");
        await RequireStopped(host.Port);
        using var failure = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(host.Destination, "failure-000.json")));
        var failed = failure.RootElement;
        HostCheck.Equal("clros-disk-http-failure-v1", failed.GetProperty("Schema").GetString());
        HostCheck.Equal(0, failed.GetProperty("Ordinal").GetInt32());
        HostCheck.Equal("GET", failed.GetProperty("Method").GetString());
        HostCheck.Equal("/healthz", failed.GetProperty("Path").GetString());
        HostCheck.Equal("validate", failed.GetProperty("Stage").GetString());
        HostCheck.True(!failed.GetProperty("MatrixCancelled").GetBoolean() &&
            !failed.GetProperty("OwnerCancelled").GetBoolean() && !failed.GetProperty("CallerCancelled").GetBoolean(),
            "status validation must fail before matrix, owner or caller cancellation");
        HostCheck.True(host.Requests.SequenceEqual(DiskStaticHttpContract.Paths.Take(1)) &&
            Directory.GetFiles(host.Destination, "*.body").Length == 1 &&
            !File.Exists(Path.Combine(host.Destination, "stopped.json")));
        HostCheck.True((await File.ReadAllBytesAsync(Path.Combine(host.Destination, "000.body"))).AsSpan().SequenceEqual("host-unavailable"u8));
        HostCheck.True((await File.ReadAllBytesAsync(Path.Combine(host.Destination, "http.json"))).AsSpan().SequenceEqual("{preserved-invalid-http-receipt\r\n"u8));
        using var joined = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(host.Destination, "runner.json")));
        HostCheck.True(joined.RootElement.GetProperty("Joined").GetBoolean());
        HostCheck.True(observed is AggregateException, "Expected an ordered aggregate; observed: " + observed);
        var aggregate = (AggregateException)observed!;
        HostCheck.True(aggregate.Message.StartsWith("disk_http_observation_and_receipt", StringComparison.Ordinal) &&
            aggregate.InnerExceptions.Count == 2);
        HostCheck.True(aggregate.InnerExceptions[0] is ToolFailure { Reason: "disk_http_status /healthz" } &&
            aggregate.InnerExceptions[1] is IOException,
            "the original observation must be first and the receipt I/O failure second");
    }
}
