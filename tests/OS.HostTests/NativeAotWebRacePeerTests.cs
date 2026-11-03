using System.Net;
using System.Net.Sockets;
using System.Text;
using OS.Tooling;
using OS.Tools.QemuRunner;

internal static class NativeAotWebRacePeerTests
{
    [HostTest("web race owner retains actual early runner stdout and joins pending READY")]
    private static async Task EarlyRunner()
    {
        var paths = Paths();
        ProcessResult? child = null;
        Exception error = await Refused(() => NativeAotWebRacePeer.RunOwnedAsync(paths.Root, 20080, "health", paths.Report, paths.Summary,
            async (_, token) => child = await ProcessRunner.RunAsync(new("dotnet", [HostTools.ManagedTool(paths.Root, "OS.QemuRunner"), "--help"], paths.Root)
            {
                Timeout = TimeSpan.FromSeconds(10)
            }, token)));
        HostCheck.True(error is ToolFailure { Reason: "web_race_owner_runner_before_ready" });
        HostCheck.True(child is { Succeeded: true, TerminationConfirmed: true });
        HostCheck.Equal(child!.StandardOutput, File.ReadAllText(paths.Report + ".runner.log"));
        HostCheck.Equal(child.StandardError, File.ReadAllText(paths.Report + ".runner.stderr.log"));
        HostCheck.True(!File.Exists(paths.Report) && !File.Exists(paths.Report + ".wire.json"));
        ClosedLogs(paths.Report);
    }

    [HostTest("web race owner refuses aliases stale outputs and outside paths before starting runner")]
    private static async Task PathsBeforeRunner()
    {
        foreach (string kind in new[] { "alias", "stale-report", "stale-wire", "stale-summary", "outside", "invalid" })
        {
            var paths = Paths();
            string report = paths.Report, summary = paths.Summary;
            switch (kind)
            {
                case "alias":
                    summary = report + ".attempts.json";
                    break;
                case "stale-report":
                    File.WriteAllText(report, "retained");
                    break;
                case "stale-wire":
                    File.WriteAllText(report + ".wire.json", "retained");
                    break;
                case "stale-summary":
                    File.WriteAllText(summary, "retained");
                    break;
                case "outside":
                    report = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json");
                    break;
                case "invalid":
                    report += "\n";
                    break;
            }
            int starts = 0;
            _ = await Refused(() => NativeAotWebRacePeer.RunOwnedAsync(paths.Root, 20080, "health", report, summary,
                (_, _) =>
{
    starts++;
    throw new InvalidOperationException("must not start");
}));
            HostCheck.Equal(0, starts);
            HostCheck.True(!File.Exists(paths.Report + ".runner.log"));
            foreach (string retained in new[] { paths.Report, paths.Report + ".wire.json", paths.Summary }.Where(File.Exists))
                HostCheck.Equal("retained", File.ReadAllText(retained));
        }
    }

    [HostTest("web race owner validates health port and initial cancellation before creating outputs")]
    private static async Task InputsBeforeRunner()
    {
        foreach (var value in new[] { (0, "health"), (65536, "health"), (20080, ""), (20080, "x\n"), (20080, new string('x', 1025)) })
        {
            var paths = Paths();
            int starts = 0;
            _ = await Refused(() => NativeAotWebRacePeer.RunOwnedAsync(paths.Root, value.Item1, value.Item2, paths.Report, paths.Summary,
                (_, _) =>
{
    starts++;
    throw new InvalidOperationException("must not start");
}));
            HostCheck.Equal(0, starts);
            HostCheck.True(!File.Exists(paths.Report + ".runner.log"));
        }
        var cancelledPaths = Paths();
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();
        Exception error = await Refused(() => NativeAotWebRacePeer.RunOwnedAsync(cancelledPaths.Root, 20080, "health", cancelledPaths.Report, cancelledPaths.Summary,
            (_, _) => throw new InvalidOperationException("must not start"), cancel.Token));
        HostCheck.True(error is OperationCanceledException);
        HostCheck.True(!File.Exists(cancelledPaths.Report + ".runner.log"));
    }

    // Deliberately incomplete loopback services and synthetic runner results
    // exercise failure ownership only. Neither control constructs Completion,
    // launches QEMU or supplies guest/process/native acceptance evidence.
    [HostTest("web race owner HTTP failure closes runner pipe joins task and retains partial reports")]
    private static Task FailedHttp() => FailedCapture(cancelAfterRequest: false);

    [HostTest("web race owner external cancellation joins actual response wait and closes original reports")]
    private static Task CancelledHttp() => FailedCapture(cancelAfterRequest: true);

    private static async Task FailedCapture(bool cancelAfterRequest)
    {
        var paths = Paths();
        using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(limit.Token);
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var requested = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        bool joined = false, pipeFailed = false;
        Task<NativeAotWebRacePeer.Completion>? owner = null;
        try
        {
            owner = NativeAotWebRacePeer.RunOwnedAsync(paths.Root, port, "health", paths.Report, paths.Summary, async (name, token) =>
            {
                Task<WebRaceRendezvous.Report> gate = WebRaceRendezvous.WaitInRunnerAsync(name, port, Environment.ProcessId, TimeSpan.FromSeconds(10), cancellation: token);
                try
                {
                    using var client = await listener.AcceptTcpClientAsync(token);
                    string headers = await Headers(client.GetStream(), token);
                    HostCheck.True(headers.StartsWith("GET /__clros/churn/status HTTP/1.1\r\n", StringComparison.Ordinal));
                    requested.SetResult();
                    if (!cancelAfterRequest)
                        await client.GetStream().WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 502 Bad Gateway\r\nContent-Length: 0\r\nContent-Type: text/plain\r\n\r\n"), token);
                    try
                    {
                        _ = await gate;
                    }
                    catch (Exception error) when (error is IOException or OperationCanceledException)
                    {
                        pipeFailed = true;
                    }
                }
                finally
                {
                    try
                    {
                        _ = await gate;
                    }
                    catch (Exception error) when (error is IOException or OperationCanceledException)
                    {
                    }
                    joined = true;
                }
                return new(1, "synthetic-failed-race-runner", "", false, false, token.IsCancellationRequested, TimeSpan.Zero);
            }, caller.Token);
            await requested.Task.WaitAsync(limit.Token);
            if (cancelAfterRequest)
                caller.Cancel();
            Exception error = await Refused(() => owner);
            HostCheck.True(cancelAfterRequest ? ContainsCancellation(error) : error is InvalidDataException { Message: "web_signalrace_churn_status" });
            HostCheck.True(joined && pipeFailed && owner.IsCompleted);
            ClosedLogs(paths.Report);
            HostCheck.Equal("synthetic-failed-race-runner", File.ReadAllText(paths.Report + ".runner.log"));
            foreach (string report in new[] { paths.Report, paths.Report + ".attempts.json", paths.Report + ".wire.json" })
            {
                using var file = new FileStream(report, FileMode.Open, FileAccess.Read, FileShare.None);
                HostCheck.True(file.Length > 0);
            }
            var wire = NativeAotWebPayload.InspectSignalRaceWire(File.ReadAllBytes(paths.Report + ".wire.json"));
            HostCheck.True(wire.Purpose == "signalrace-partial" && wire.Connections.All(connection => connection.DisposedTicks > 0));
        }
        finally
        {
            caller.Cancel();
            listener.Stop();
            if (owner is not null)
                try
                {
                    _ = await owner;
                }
                catch
                {
                }
        }
    }

    [HostTest("web race runner explicit dry run preserves ordinary bytes and refuses malformed selections without IPC")]
    private static async Task RunnerArguments()
    {
        var paths = Paths();
        string name = "clros-web-race-" + Guid.NewGuid().ToString("N");
        string[] ordinary = ["--continuing-web", "--socket-forward-port", "20080", "--virtio-negotiation-device", "--arp-receive-fixture",
            "--ethernet-capture", paths.Report + ".pcap", "--summary-log", paths.Summary, "--replace-default-required-markers",
            "--http-probe-url", "http://127.0.0.1:20080/healthz", "--http-probe-expected-body", "health", "--web-config-body", "{\"selected\":true}", "--dry-run"];
        async Task<ProcessResult> Run(string[] args) => await ProcessRunner.RunAsync(new("dotnet", [HostTools.ManagedTool(paths.Root, "OS.QemuRunner"), .. args], paths.Root)
        {
            Timeout = TimeSpan.FromSeconds(10)
        });
        var before = await Run(ordinary);
        var explicitMode = await Run([.. ordinary, "--web-race-pipe", name]);
        HostCheck.True(before.Succeeded && explicitMode.Succeeded);
        HostCheck.Equal(before.StandardOutput, explicitMode.StandardOutput);
        foreach (string[] args in new string[][] {
            [.. ordinary, "--web-race-pipe", "bad"], [.. ordinary, "--web-race-pipe", name, "--web-race-pipe", name],
            [.. ordinary, "--web-race-pipe", name, "--web-log-case", "records64"], ["--web-race-pipe", name, "--dry-run"],
            [.. ordinary, "--web-race-pipe", name, "--timeout", "286"] })
        {
            var refused = await Run(args);
            HostCheck.Equal(1, refused.ExitCode);
            HostCheck.True(refused.StandardError.Contains("qemu.runner.invalid_args", StringComparison.Ordinal));
        }
        HostCheck.True(!File.Exists(paths.Summary) && !File.Exists(paths.Report));
    }

    [HostTest("web event-owned runner dry parse pairs either argument order and refuses mixed missing or duplicate profiles")]
    private static async Task EventRunnerArguments()
    {
        var paths = Paths();
        string pipe = "clros-web-race-v3-" + Guid.NewGuid().ToString("N"), legacy = "clros-web-race-" + Guid.NewGuid().ToString("N");
        string[] ordinary = ["--continuing-web","--socket-forward-port","20080","--virtio-negotiation-device","--arp-receive-fixture",
            "--ethernet-capture",paths.Report+".pcap","--summary-log",paths.Summary,"--replace-default-required-markers",
            "--http-probe-url","http://127.0.0.1:20080/healthz","--http-probe-expected-body","health","--web-config-body","{\"selected\":true}","--timeout","285","--dry-run"];
        async Task<ProcessResult> Run(string[] args) => await ProcessRunner.RunAsync(new("dotnet", [HostTools.ManagedTool(paths.Root, "OS.QemuRunner"), .. args], paths.Root)
        {
            Timeout = TimeSpan.FromSeconds(10)
        });
        var before = await Run(ordinary);
        foreach (string[] args in new string[][] { [.. ordinary, "--web-event-owned-signal-race", "--web-race-pipe", pipe], [.. ordinary, "--web-race-pipe", pipe, "--web-event-owned-signal-race"] })
        {
            var result = await Run(args);
            HostCheck.True(before.Succeeded && result.Succeeded);
            HostCheck.Equal(before.StandardOutput, result.StandardOutput);
        }
        foreach (string[] args in new string[][]
        {
            [..ordinary,"--web-event-owned-signal-race"],[..ordinary,"--web-race-pipe",pipe],
            [..ordinary,"--web-event-owned-signal-race","--web-race-pipe",legacy],
            [..ordinary,"--web-event-owned-signal-race","--web-event-owned-signal-race","--web-race-pipe",pipe],
            [..ordinary,"--web-event-owned-signal-race","--web-race-pipe",pipe,"--web-log-case","records64"],
            [..ordinary,"--web-event-owned-signal-race","--web-race-pipe",pipe,"--timeout","286"],
            ["--web-event-owned-signal-race","--web-race-pipe",pipe,"--dry-run"]
        })
        {
            var result = await Run(args);
            HostCheck.Equal(1, result.ExitCode);
            HostCheck.True(result.StandardError.Contains("qemu.runner.invalid_args", StringComparison.Ordinal));
        }
        HostCheck.True(!File.Exists(paths.Summary) && !File.Exists(paths.Report));
    }

    private static (string Root, string Report, string Summary) Paths()
    {
        string root = HostToolProcess.FindRepositoryRoot(), directory = Path.Combine(root, "artifacts", "host-tests", "web-race-owner-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return (root, Path.Combine(directory, "selected.json"), Path.Combine(directory, "runner.summary"));
    }
    private static void ClosedLogs(string path)
    {
        foreach (string suffix in new[] { ".runner.log", ".runner.stderr.log" })
            using (var closed = new FileStream(path + suffix, FileMode.Open, FileAccess.Read, FileShare.None))
            {
            }
    }
    private static bool ContainsCancellation(Exception error) => error is OperationCanceledException || error is AggregateException aggregate && aggregate.InnerExceptions.Any(ContainsCancellation);
    private static async Task<Exception> Refused(Func<Task> operation)
    {
        try
        {
            await operation();
        }
        catch (Exception error) when (error is ToolFailure or IOException or InvalidDataException or ArgumentException or OperationCanceledException or AggregateException)
        {
            return error;
        }
        throw new InvalidOperationException("Expected refusal.");
    }
    private static async Task<string> Headers(Stream stream, CancellationToken token)
    {
        byte[] bytes = new byte[8192];
        int count = 0;
        while (count < bytes.Length)
        {
            if (await stream.ReadAsync(bytes.AsMemory(count, 1), token) == 0)
                throw new EndOfStreamException();
            count++;
            if (count >= 4 && bytes.AsSpan(count - 4, 4).SequenceEqual("\r\n\r\n"u8))
                return Encoding.ASCII.GetString(bytes, 0, count);
        }
        throw new InvalidDataException("Independent request bound.");
    }
}
