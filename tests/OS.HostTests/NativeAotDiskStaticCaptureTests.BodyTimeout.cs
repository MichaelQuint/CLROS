using System.Text.Json;
using OS.Tooling;

internal static partial class NativeAotDiskStaticCaptureTests
{
    [HostTest("disk static peer expires its streamed body at the existing request deadline before the outer owner", HostLane.Native)]
    private static async Task BodyTimeout()
    {
        // Expensive loopback host control: exercise the real existing60-second
        // development request deadline without adding a production override.
        var host = new Host(Mode.BodyTimeout);
        using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        Task<NativeAotDiskStaticPeer.Completion> operation = NativeAotDiskStaticPeer.RunAsync(
            host.Root, host.Serial, host.RunAsync, watchdog.Token);
        bool timedOut = false;
        try
        {
            try
            {
                _ = await operation;
            }
            catch (OperationCanceledException) when (!watchdog.IsCancellationRequested)
            {
                timedOut = true;
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

        HostCheck.True(timedOut && host.Joined && host.OwnerCancelled && host.Launches == 1 &&
            host.BodyStarted.Task.IsCompletedSuccessfully && host.BodyFinished.Task.IsCompletedSuccessfully,
            "the blocked body must expire and all owned host work must join");
        await RequireStopped(host.Port);
        using var failure = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(host.Destination, "failure-003.json")));
        var failed = failure.RootElement;
        HostCheck.Equal("body", failed.GetProperty("Stage").GetString());
        long frequency = failed.GetProperty("ClockFrequency").GetInt64();
        long started = failed.GetProperty("Started").GetInt64(), failedAt = failed.GetProperty("Failed").GetInt64();
        HostCheck.True(frequency > 0 && failedAt >= started &&
            (Int128)failedAt - started >= (Int128)59 * frequency,
            "the existing60-second deadline must not be replaced by a shortened test bound");
        HostCheck.True(!failed.GetProperty("MatrixCancelled").GetBoolean() &&
            !failed.GetProperty("OwnerCancelled").GetBoolean() && !failed.GetProperty("CallerCancelled").GetBoolean(),
            "request expiry must precede matrix, owner and caller cancellation");
        using var receipt = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(host.Destination, "http.json")));
        HostCheck.True(!receipt.RootElement.GetProperty("Complete").GetBoolean() &&
            receipt.RootElement.GetProperty("Responses").GetArrayLength() == 3);
        HostCheck.True(host.Requests.SequenceEqual(DiskStaticHttpContract.Paths.Take(4)) &&
            !File.Exists(Path.Combine(host.Destination, "003.body")) &&
            !File.Exists(Path.Combine(host.Destination, "stopped.json")));
        using var joined = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(host.Destination, "runner.json")));
        HostCheck.True(joined.RootElement.GetProperty("Joined").GetBoolean());
    }
}
