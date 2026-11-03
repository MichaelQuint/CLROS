using System.Net;
using System.Net.Sockets;
using System.Text;
using OS.Tooling;

internal static class NativeAotServiceQuotaOwnerTests
{
    // The real separate peer child talks to these deliberately incomplete local
    // HTTP endpoints. Synthetic runner results exercise ownership only; none of
    // these controls creates a successful Completion or guest/quota authority.
    [HostTest("quota owner awaits actual peer after runner22 then retains its genuine endpoint refusal")]
    private static async Task ExpectedCaptureAwaitsPeer()
    {
        var paths = Paths();
        using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        TcpListener? listener = null;
        TcpClient? client = null;
        var request = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        bool runnerEnded = false;
        Task<NativeAotServiceQuotaPeer.Completion>? owner = null;
        try
        {
            owner = NativeAotServiceQuotaPeer.RunOwnedAsync(paths.Root, paths.Peer, paths.Serial, async (port, token) =>
            {
                listener = new TcpListener(IPAddress.Loopback, port);
                listener.Start();
                Listening(paths.Serial);
                client = await listener.AcceptTcpClientAsync(token);
                request.SetResult(await Headers(client.GetStream(), token));
                runnerEnded = true;
                return Runner(22);
            }, limit.Token);
            HostCheck.True((await request.Task.WaitAsync(limit.Token)).StartsWith("GET /healthz HTTP/1.1\r\n", StringComparison.Ordinal));
            await Response(client!.GetStream(), "CLROS_SERVICE_HEALTH_OK", limit.Token);
            string next = await Headers(client.GetStream(), limit.Token);
            HostCheck.True(next.StartsWith("GET /__clros/service/lifecycle/status HTTP/1.1\r\n", StringComparison.Ordinal));
            await Response(client.GetStream(), "CLROS_LIFECYCLE_STATUS active=1 waiting=0 waits=0 canceled=0 echoes=0 streams=0 rejected=0 read=0 written=0", limit.Token);
            string? refusal = null;
            try
            {
                _ = await owner;
            }
            catch (ToolFailure error) { refusal = error.Reason; }
            HostCheck.True(runnerEnded);
            HostCheck.Equal("quota_peer_owner_peer_failed", refusal ?? "accepted");
            HostCheck.Equal("", File.ReadAllText(paths.Peer));
            HostCheck.True(File.ReadAllText(paths.Peer + ".stderr.log").Contains("quota_peer_initial_lifecycle", StringComparison.Ordinal));
            RequireClosedLogs(paths.Peer);
        }
        finally
        {
            limit.Cancel();
            client?.Dispose();
            listener?.Stop();
            if (owner is not null)
                try
                {
                    _ = await owner;
                }
                catch { }
        }
    }

    [HostTest("quota owner genuine peer failure cancels and joins runner and refuses stale logs before another child")]
    private static async Task PeerFailureJoinsRunner()
    {
        var paths = Paths();
        using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        bool runnerJoined = false, served = false;
        int starts = 0;
        string? refusal = null;
        try
        {
            _ = await NativeAotServiceQuotaPeer.RunOwnedAsync(paths.Root, paths.Peer, paths.Serial, async (port, token) =>
            {
                starts++;
                using var listener = new TcpListener(IPAddress.Loopback, port);
                listener.Start();
                Listening(paths.Serial);
                try
                {
                    using var client = await listener.AcceptTcpClientAsync(token);
                    _ = await Headers(client.GetStream(), token);
                    await Response(client.GetStream(), "wrong-health", token);
                    served = true;
                    await Task.Delay(Timeout.Infinite, token);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { }
                finally { runnerJoined = true; }
                return Runner(1) with
                {
                    Cancelled = token.IsCancellationRequested
                };
            }, limit.Token);
        }
        catch (ToolFailure error) { refusal = error.Reason; }
        HostCheck.True(served && runnerJoined);
        HostCheck.Equal("quota_peer_owner_peer_failed", refusal ?? "accepted");
        HostCheck.True(File.ReadAllText(paths.Peer + ".stderr.log").Contains("quota_peer_startup_health", StringComparison.Ordinal));
        HostCheck.Equal("synthetic-owned-runner", File.ReadAllText(paths.Peer + ".runner.log"));
        RequireClosedLogs(paths.Peer);
        refusal = null;
        try
        {
            _ = await NativeAotServiceQuotaPeer.RunOwnedAsync(paths.Root, paths.Peer, paths.Serial, (_, _) => { starts++; return Task.FromResult(Runner(22)); }, limit.Token);
        }
        catch (ToolFailure error) { refusal = error.Reason; }
        HostCheck.Equal("quota_peer_owner_log_not_fresh", refusal ?? "accepted");
        HostCheck.Equal(1, starts);
    }

    [HostTest("quota owner external cancellation joins real pending peer and runner before closing preserved logs")]
    private static async Task CancellationJoinsBoth()
    {
        var paths = Paths();
        using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(limit.Token);
        var requested = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        bool runnerJoined = false, cancelled = false;
        var owner = NativeAotServiceQuotaPeer.RunOwnedAsync(paths.Root, paths.Peer, paths.Serial, async (port, token) =>
        {
            using var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            Listening(paths.Serial);
            try
            {
                using var client = await listener.AcceptTcpClientAsync(token);
                _ = await Headers(client.GetStream(), token);
                requested.SetResult();
                await Task.Delay(Timeout.Infinite, token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            finally { runnerJoined = true; }
            return Runner(1) with
            {
                Cancelled = token.IsCancellationRequested
            };
        }, caller.Token);
        try
        {
            await requested.Task.WaitAsync(limit.Token);
            caller.Cancel();
            try
            {
                _ = await owner;
            }
            catch (OperationCanceledException) when (caller.IsCancellationRequested) { cancelled = true; }
            HostCheck.True(cancelled && runnerJoined && owner.IsCompleted);
            RequireClosedLogs(paths.Peer);
        }
        finally { caller.Cancel(); try { _ = await owner; } catch { } }
    }

    [HostTest("quota owner rejects ordinary runner exit while joining actual peer response wait")]
    private static async Task OrdinaryExitIsNotQuota()
    {
        var paths = Paths();
        using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        TcpListener? listener = null;
        TcpClient? client = null;
        string? refusal = null;
        try
        {
            _ = await NativeAotServiceQuotaPeer.RunOwnedAsync(paths.Root, paths.Peer, paths.Serial, async (port, token) =>
            {
                listener = new TcpListener(IPAddress.Loopback, port);
                listener.Start();
                Listening(paths.Serial);
                client = await listener.AcceptTcpClientAsync(token);
                _ = await Headers(client.GetStream(), token);
                return Runner(0);
            }, limit.Token);
        }
        catch (ToolFailure error) { refusal = error.Reason; }
        finally { client?.Dispose(); listener?.Stop(); }
        HostCheck.Equal("quota_peer_owner_runner_disposition", refusal ?? "accepted");
        RequireClosedLogs(paths.Peer);
    }

    [HostTest("quota peer child route rejects malformed union and noncanonical port arguments before HTTP")]
    private static void StrictChildArguments()
    {
        foreach (string[] tail in new string[][] {
            [], ["--port"], ["--port", "0"], ["--port", "65536"], ["--port", "01"], ["--port", "+1"],
            ["--port", " 1"], ["--port", "1 "], ["--port", "x"], ["--port=1"],
            ["--port", "1", "--lifecycle"], ["--port", "1", "--serial", "unused"], ["--port", "1", "--port", "2"] })
        {
            var result = HostToolProcess.RunTool(["nativeaot", "service", "quota-peer", .. tail]);
            HostCheck.Equal(1, result.ExitCode);
            HostCheck.Equal("", result.Output);
            HostCheck.True(result.Error.Contains("quota_peer_cli", StringComparison.Ordinal));
        }
    }

    [HostTest("quota observed owner retains separate child partial wire failure and rejects invalid companion paths")]
    private static async Task PartialWireFailure()
    {
        var paths = Paths();
        using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        string wire = paths.Peer + ".wire.json";
        bool runnerEnded = false, accepted = false;
        string? refusal = null;
        TcpListener? listener = null;
        TcpClient? client = null;
        Task<NativeAotServiceQuotaPeer.Completion>? owner = null;
        try
        {
            owner = NativeAotServiceQuotaPeer.RunObservedOwnedAsync(paths.Root, paths.Peer, paths.Serial, async (port, token) =>
            {
                listener = new TcpListener(IPAddress.Loopback, port);
                listener.Start();
                Listening(paths.Serial);
                client = await listener.AcceptTcpClientAsync(token);
                _ = await Headers(client.GetStream(), token);
                await Response(client.GetStream(), "CLROS_SERVICE_HEALTH_OK", token);
                _ = await Headers(client.GetStream(), token);
                await Response(client.GetStream(), "CLROS_LIFECYCLE_STATUS active=1 waiting=0 waits=0 canceled=0 echoes=0 streams=0 rejected=0 read=0 written=0", token);
                runnerEnded = true;
                return Runner(22);
            }, limit.Token);
            try
            {
                _ = await owner;
                accepted = true;
            }
            catch (ToolFailure error) { refusal = error.Reason; }
            HostCheck.True(!accepted && runnerEnded && owner.IsCompleted);
            HostCheck.Equal("quota_peer_owner_peer_failed", refusal ?? "accepted");
            HostCheck.Equal("", File.ReadAllText(paths.Peer));
            HostCheck.True(File.ReadAllText(paths.Peer + ".stderr.log").Contains("quota_peer_initial_lifecycle", StringComparison.Ordinal));
            RequireClosedLogs(paths.Peer);
            using (FileStream closed = new(wire, FileMode.Open, FileAccess.Read, FileShare.None))
                HostCheck.True(closed.Length is > 0 and <= NativeAotServiceQuotaPeer.WireMaximumReportBytes);
            var trace = NativeAotServiceQuotaPeer.InspectWireReport(File.ReadAllBytes(wire));
            HostCheck.True(trace.Purpose == "quota-partial" && trace.Samples.Count == 1 && trace.Samples[0].Name == "health.startup" &&
                trace.Exchanges.Count == 2 && trace.Operations.Count > 0 && trace.Connections.All(c => c.DisposedTicks > 0));
            byte[] preserved = File.ReadAllBytes(wire);
            string receipt = "QUOTA_WIRE_REPORT kind=quota-partial sha256=" +
                Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(preserved)) + " bytes=" +
                preserved.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\r\n";
            HostCheck.True(File.ReadAllText(paths.Peer + ".stderr.log").StartsWith(receipt, StringComparison.Ordinal));
            foreach (string[] tail in new string[][] {
                ["--wire-report"], ["--wire-report", "relative.json"], ["--wire-report", wire],
                ["--wire-report", wire + ".fresh", "--wire-report", wire + ".other"], ["--wire-report", paths.Root] })
            {
                var result = HostToolProcess.RunTool(["nativeaot", "service", "quota-peer", "--port", "1", .. tail]);
                HostCheck.Equal(1, result.ExitCode);
                HostCheck.Equal("", result.Output);
                HostCheck.True(result.Error.Contains("quota_peer_", StringComparison.Ordinal));
            }
            HostCheck.True(File.ReadAllBytes(wire).AsSpan().SequenceEqual(preserved));
        }
        finally
        {
            limit.Cancel();
            client?.Dispose();
            listener?.Stop();
            if (owner is not null)
                try
                {
                    _ = await owner;
                }
                catch { }
        }
    }

    [HostTest("quota observed owner successful process receipt rejects partial extra noncanonical and overflow records")]
    private static void WireProcessReceipt()
    {
        string good = "QUOTA_WIRE_REPORT kind=quota-run sha256=" + new string('A', 64) + " bytes=123\r\n";
        var copied = NativeAotServiceQuotaPeer.InspectWireReceiptForTests(good);
        HostCheck.Equal(new string('A', 64), copied.Sha256);
        HostCheck.Equal(123, copied.Bytes);
        foreach (string bad in new[] { "", good + good, good + "extra", good.Replace("\r\n", "\n", StringComparison.Ordinal),
            good.Replace("quota-run", "quota-partial", StringComparison.Ordinal), good.Replace("sha256=", "hash=", StringComparison.Ordinal),
            good.Replace(new string('A', 64), new string('a', 64), StringComparison.Ordinal),
            good.Replace(new string('A', 64), new string('A', 63), StringComparison.Ordinal),
            good.Replace("bytes=123", "bytes=0123", StringComparison.Ordinal), good.Replace("bytes=123", "bytes=+123", StringComparison.Ordinal),
            good.Replace("bytes=123", "bytes=0", StringComparison.Ordinal), good.Replace("bytes=123", "bytes=33554433", StringComparison.Ordinal) })
        {
            bool rejected = false;
            try
            {
                _ = NativeAotServiceQuotaPeer.InspectWireReceiptForTests(bad);
            }
            catch (ToolFailure error) { rejected = error.Reason == "quota_peer_owner_wire_process_receipt"; }
            HostCheck.True(rejected);
        }
    }

    private static (string Root, string Peer, string Serial) Paths()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string directory = Path.Combine(root, "artifacts", "host-tests", "quota-owner-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return (root, Path.Combine(directory, "peer.log"), Path.Combine(directory, "serial.log"));
    }

    private static ProcessResult Runner(int exit) => new(exit, "synthetic-owned-runner", "", false, false, false, TimeSpan.Zero);
    private static void Listening(string serial) => File.WriteAllText(serial, "\r\nCLROS_M16_014_LISTENING\r\n");
    private static async Task<string> Headers(NetworkStream stream, CancellationToken cancellation)
    {
        byte[] bytes = new byte[8192];
        int count = 0;
        while (count < bytes.Length)
        {
            int read = await stream.ReadAsync(bytes.AsMemory(count, 1), cancellation);
            if (read == 0)
                throw new IOException("actual_peer_ended_before_request");
            count += read;
            if (count >= 4 && bytes[count - 4] == 13 && bytes[count - 3] == 10 && bytes[count - 2] == 13 && bytes[count - 1] == 10)
                return Encoding.ASCII.GetString(bytes, 0, count);
        }
        throw new IOException("actual_peer_header_bounds");
    }

    private static async Task Response(NetworkStream stream, string body, CancellationToken cancellation)
    {
        byte[] bytes = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: " + Encoding.ASCII.GetByteCount(body).ToString(System.Globalization.CultureInfo.InvariantCulture) + "\r\n\r\n" + body);
        await stream.WriteAsync(bytes, cancellation);
        await stream.FlushAsync(cancellation);
    }

    private static void RequireClosedLogs(string peer)
    {
        foreach (string path in new[] { peer, peer + ".stderr.log", peer + ".runner.log", peer + ".runner.stderr.log" })
        {
            using FileStream closed = new(path, FileMode.Open, FileAccess.Read, FileShare.None);
            HostCheck.True(closed.Length >= 0);
        }
    }
}
