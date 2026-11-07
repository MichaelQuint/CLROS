using System.Net;
using System.Net.Sockets;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.Json;

namespace OS.Tooling;

/// <summary>
/// Owns one explicitly selected Full018 HTTP/browser diagnostic. The caller
/// retains original source, native, image and device authorities through return.
/// The first HTTP sequence and warm sequences share the existing matrix budget;
/// browser startup and the outer runner retain their existing bounds.
/// </summary>
/// <remarks>
/// The supplied runner must observe cancellation and join its entire process
/// owner before completing. Every path cancels and awaits both observation and
/// runner tasks. Successful observation deliberately stops the runner and checks
/// endpoint refusal; it does not qualify normal guest shutdown or full cleanup.
/// The browser callback owns and joins its process and preserves its originals.
/// Failed observations and cleanup dispositions remain in a fresh directory.
/// </remarks>
public static partial class NativeAotHttpBenchmarkPeer
{
    public static async Task<Completion> RunAsync(string root, string serial,
        Func<int, CancellationToken, Task<ProcessResult>> runRunner,
        WebRootDiskInventory.Inspection inventory, HttpWorkloadBenchmark.Options options,
        int runnerTimeoutSeconds, Func<int, string, CancellationToken, Task> browser,
        CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(runRunner);
        ArgumentNullException.ThrowIfNull(browser);
        return await RunCoreAsync(root, serial, runRunner, inventory, options, runnerTimeoutSeconds, browser, cancellation);
    }

    private static async Task<Completion> RunCoreAsync(string root, string serial,
        Func<int, CancellationToken, Task<ProcessResult>> runRunner,
        WebRootDiskInventory.Inspection inventory, HttpWorkloadBenchmark.Options options,
        int runnerTimeoutSeconds, Func<int, string, CancellationToken, Task>? browser,
        CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(runRunner);
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        Need(options.System == "CLROS", "system");
        Need(runnerTimeoutSeconds is >= DiskStaticCapturePolicy.MinimumTimeoutSeconds
            and <= DiskStaticCapturePolicy.MaximumTimeoutSeconds, "runner_timeout");
        var asset = inventory.Find(WebRootDiskInventory.Root + NativeAotDiskTimingPeer.Target);
        Need(asset is { IsDirectory: false, Bytes: >= 65536 and <= DiskStaticHttpContract.MaximumAssetBytes }, "asset");

        root = Path.GetFullPath(root);
        serial = Path.GetFullPath(serial, root);
        string directory = serial + ".http-benchmark";
        Need(directory.StartsWith(Path.Combine(root, "artifacts") + Path.DirectorySeparatorChar,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)
            && !Path.Exists(directory) && !Path.Exists(serial), "fresh_artifacts");
        AuthorityFiles.RequirePlainPath(root, Path.GetDirectoryName(directory)!, "http_benchmark_owner_path");
        cancellation.ThrowIfCancellationRequested();
        Directory.CreateDirectory(directory);
        int port;
        using (TcpListener reservation = new(IPAddress.Loopback, 0))
        {
            reservation.Start();
            port = ((IPEndPoint)reservation.LocalEndpoint).Port;
        }

        using var owned = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(owned.Token);
        deadline.CancelAfter(TimeSpan.FromSeconds(runnerTimeoutSeconds - 30));
        Task<ProcessResult>? runner = null;
        Task? observation = null;
        ProcessResult? joined = null;
        HttpWorkloadBenchmark.Report? report = null;
        Exception? failure = null, cleanupFailure = null;
        bool complete = false, browserCompleted = false, endpointRefused = false;
        try
        {
            runner = runRunner(port, owned.Token);
            observation = Observe();
            if (await Task.WhenAny(observation, runner) == runner)
            {
                cancellation.ThrowIfCancellationRequested();
                throw new ToolFailure("http_benchmark_runner_exited_before_observation");
            }
            await observation;
            cancellation.ThrowIfCancellationRequested();
            owned.Cancel();
            joined = await runner;
            Need(joined.Cancelled && joined.TerminationConfirmed && !joined.TimedOut
                && !joined.OutputLimitExceeded && joined.FailureReason.Length == 0, "runner_join");
            InspectStoppedSerial();
            using Socket stopped = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            using var refusal = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try
            {
                await stopped.ConnectAsync(IPAddress.Loopback, port, refusal.Token);
            }
            catch (SocketException error) when (error.SocketErrorCode == SocketError.ConnectionRefused)
            {
                endpointRefused = true;
            }
            Need(endpointRefused, "endpoint_live");
            complete = true;
        }
        catch (Exception error)
        {
            failure = error;
        }
        finally
        {
            owned.Cancel();
            if (observation is not null)
                try
                {
                    await observation;
                }
                catch (Exception error)
                {
                    failure ??= error;
                }
            if (runner is not null)
                try
                {
                    joined = await runner;
                    if (!joined.TerminationConfirmed)
                        cleanupFailure = new ToolFailure("http_benchmark_runner_join_unconfirmed");
                }
                catch (Exception error)
                {
                    cleanupFailure = error;
                }
            try
            {
                using FileStream receipt = new(Path.Combine(directory, "owner.json"),
                    FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                await JsonSerializer.SerializeAsync(receipt, new
                {
                    Schema = browser is null ? "clros-quiet-http-benchmark-owner-v1" : "clros-http-benchmark-owner-v1",
                    Complete = complete && failure is null && cleanupFailure is null,
                    Failure = Reason(failure),
                    CleanupFailure = Reason(cleanupFailure),
                    Port = port,
                    RunnerTimeoutSeconds = runnerTimeoutSeconds,
                    HttpTimeoutSeconds = DiskStaticCapturePolicy.HttpMatrixTimeoutSeconds,
                    options.WarmSequences,
                    inventory.ImageSha256,
                    BrowserCallbackCompleted = browserCompleted,
                    EndpointRefused = endpointRefused,
                    RunnerJoined = joined?.TerminationConfirmed == true,
                    Runner = joined is null ? null : new
                    {
                        joined.ExitCode,
                        joined.TimedOut,
                        joined.OutputLimitExceeded,
                        joined.Cancelled,
                        joined.TerminationConfirmed,
                        DurationTicks = joined.Duration.Ticks,
                        FailureReason = Tail(joined.FailureReason),
                        StdoutCharacters = joined.StandardOutput.Length,
                        StderrCharacters = joined.StandardError.Length,
                        StdoutTail = Tail(joined.StandardOutput),
                        StderrTail = Tail(joined.StandardError)
                    },
                    ProductionPerformanceAccepted = false,
                    FullAndCleanupQualified = false,
                    BrowserQualified = false
                }, cancellationToken: CancellationToken.None);
                receipt.Flush(true);
            }
            catch (Exception error)
            {
                cleanupFailure = cleanupFailure is null ? error : new AggregateException(cleanupFailure, error);
            }
        }
        if (failure is not null && cleanupFailure is not null && !ReferenceEquals(failure, cleanupFailure))
            throw new AggregateException("http_benchmark_observation_and_cleanup", failure, cleanupFailure);
        if (failure is not null)
            ExceptionDispatchInfo.Capture(failure).Throw();
        if (cleanupFailure is not null)
            ExceptionDispatchInfo.Capture(cleanupFailure).Throw();
        return new(port, directory, report!, joined!);

        async Task Observe()
        {
            using var startup = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
            startup.CancelAfter(TimeSpan.FromSeconds(DiskStaticCapturePolicy.HttpReadinessTimeoutSeconds));
            await NativeAotSocketPeer.WaitForSerialAsync(serial,
                text => text.Contains("M10_018_LISTENING ", StringComparison.Ordinal), startup.Token);
            using (var matrix = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token))
            {
                matrix.CancelAfter(TimeSpan.FromSeconds(DiskStaticCapturePolicy.HttpMatrixTimeoutSeconds));
                report = await HttpWorkloadBenchmark.RunAsync(root, Path.Combine(directory, "requests"),
                    new Uri("http://127.0.0.1:" + port + "/"), inventory, options, matrix.Token);
            }
            if (browser is not null)
            {
                using var browserDeadline = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
                browserDeadline.CancelAfter(TimeSpan.FromSeconds(DiskStaticCapturePolicy.BrowserTimeoutSeconds));
                await browser(port, Path.Combine(directory, "browser"), browserDeadline.Token);
                browserCompleted = true;
            }
        }

        void InspectStoppedSerial()
        {
            AuthorityFiles.RequirePlainPath(root, serial, "http_benchmark_owner_serial");
            using FileStream input = File.OpenRead(serial);
            Need(input.Length is > 0 and <= DiskStaticCapturePolicy.SerialBytes, "serial_extent");
            using StreamReader reader = new(input, new UTF8Encoding(false, true));
            for (string? line; (line = reader.ReadLine()) is not null;)
                Need(!line.StartsWith("TEST_FAIL", StringComparison.Ordinal)
                    && !line.StartsWith("OS_SMOKE_FAIL", StringComparison.Ordinal), "guest_failure");
        }
    }

    private static string? Reason(Exception? error) => error is ToolFailure tool
        ? tool.Reason : error?.GetType().FullName;

    private static string Tail(string value) => value.Length <= 16384 ? value : value[^16384..];

    private static void Need(bool condition, string reason)
    {
        if (!condition)
            throw new ToolFailure("http_benchmark_owner_" + reason);
    }
}
