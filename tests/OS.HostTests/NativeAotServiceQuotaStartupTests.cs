using System.Net;
using System.Net.Sockets;
using System.Text;
using OS.Tooling;

internal static class NativeAotServiceQuotaStartupTests
{
    [HostTest("quota failure diagnostic retains typed transport chain and aggregate siblings without payload")]
    private static void FailureDiagnostic()
    {
        const string secret = "DO_NOT_EMIT_MESSAGE_STACK_OR_PAYLOAD";
        var socket = new SocketException((int)SocketError.ConnectionReset);
        var input = new IOException(secret, socket);
        var request = new HttpRequestException(HttpRequestError.Unknown, secret, input);
        var response = new HttpIOException(HttpRequestError.ResponseEnded, secret);
        var guard = new ToolFailure("quota_wire_operation_state", secret);
        var aggregate = new AggregateException(secret, request, response, guard);
        string actual = NativeAotServiceQuotaPeer.DescribeFailure(aggregate);
        string expected = Line(0, -1, aggregate) + Line(1, 0, request, http: "Unknown") +
            Line(2, 0, response, http: "ResponseEnded") + Line(3, 0, guard, reason: "quota_wire_operation_state") +
            Line(4, 1, input) + Line(5, 4, socket, socket: socket.SocketErrorCode.ToString(),
                native: socket.NativeErrorCode.ToString(System.Globalization.CultureInfo.InvariantCulture));
        HostCheck.Equal(expected, actual);
        HostCheck.True(!actual.Contains(secret, StringComparison.Ordinal) && actual.Length <= 4096);
    }

    [HostTest("quota failure diagnostic bounds nodes and reasons while omitting exception messages")]
    private static void FailureDiagnosticBounds()
    {
        const string secret = "PRIVATE_MESSAGE_MUST_NOT_APPEAR";
        foreach (string reason in new[] { "safe_reason.v1", new string('Z', 96), "unsafe-reason", "bad reason", "bad\r\nreason", "bad\u00e9", new string('Z', 97) })
        {
            var error = new ToolFailure(reason, secret);
            bool safe = reason is "safe_reason.v1" || reason == new string('Z', 96);
            string text = NativeAotServiceQuotaPeer.DescribeFailure(error);
            HostCheck.Equal(Line(0, -1, error, reason: safe ? reason : "-"), text);
            HostCheck.True(!text.Contains(secret, StringComparison.Ordinal));
        }
        Exception chain = new IOException(secret);
        for (int i = 1; i < 8; i++) chain = new IOException(secret, chain);
        string exact = NativeAotServiceQuotaPeer.DescribeFailure(chain);
        string[] exactRows = exact.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        HostCheck.Equal(8, exactRows.Length); HostCheck.True(!exact.Contains("truncated", StringComparison.Ordinal));
        Exception? item = chain;
        for (int i = 0; i < 8; i++, item = item!.InnerException)
            HostCheck.Equal(Line(i, i - 1, item!)[..^2], exactRows[i]);
        string bounded = NativeAotServiceQuotaPeer.DescribeFailure(new IOException(secret, chain));
        string[] boundedRows = bounded.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        HostCheck.True(boundedRows.Length == 9 && boundedRows[^1] == "QUOTA_PEER_ERROR truncated=true" &&
            bounded.EndsWith("\r\n", StringComparison.Ordinal) && bounded.Length <= 4096 && !bounded.Contains(secret, StringComparison.Ordinal));
        var wide = new AggregateException(secret, Enumerable.Range(0, 12).Select(_ => new IOException(secret)));
        string wideText = NativeAotServiceQuotaPeer.DescribeFailure(wide);
        string[] wideRows = wideText.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        HostCheck.Equal(9, wideRows.Length);
        HostCheck.Equal(Line(0, -1, wide)[..^2], wideRows[0]);
        for (int i = 1; i < 8; i++) HostCheck.Equal(Line(i, 0, wide.InnerExceptions[i - 1])[..^2], wideRows[i]);
        HostCheck.Equal("QUOTA_PEER_ERROR truncated=true", wideRows[^1]);
    }

    private static string Line(int node, int parent, Exception error, string http = "-", string socket = "-", string native = "-", string reason = "-") =>
        FormattableString.Invariant($"QUOTA_PEER_ERROR node={node} parent={parent} type={error.GetType().FullName} hresult={error.HResult:X8} http={http} socket={socket} native={native} reason={reason}\r\n");

    [HostTest("quota observed startup retries failed health then retains valid health and refused initial lifecycle")]
    private static async Task RetriedHealthFailure()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
        using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        byte[] truncated = "HTTP/1.1 200 OK\r\nContent-Length: 4\r\n\r\nx"u8.ToArray();
        byte[] healthy = Response("CLROS_SERVICE_HEALTH_OK");
        byte[] invalid = Response("CLROS_LIFECYCLE_STATUS active=1 waiting=0 waits=0 canceled=0 echoes=0 streams=0 rejected=0 read=0 written=0");
        var requests = new List<byte[]>(); int served = 0, callbacks = 0;
        NativeAotServiceQuotaPeer.WireTrace? snapshot = null; string? refusal = null;
        var serving = Serve();
        async Task Serve()
        {
            using (var first = await listener.AcceptTcpClientAsync(stop.Token))
            {
                NetworkStream firstStream = first.GetStream();
                requests.Add(await Headers(firstStream, stop.Token));
                await firstStream.WriteAsync(truncated, stop.Token); served++;
                // Actual incomplete body EOF cannot become a complete health response.
                // A fresh logical retry must open another observed connection.
                first.Client.Shutdown(SocketShutdown.Send);
                HostCheck.Equal(0, await firstStream.ReadAsync(new byte[1], stop.Token));
            }
            using var second = await listener.AcceptTcpClientAsync(stop.Token);
            NetworkStream secondStream = second.GetStream();
            foreach (byte[] response in new[] { healthy, invalid })
            {
                requests.Add(await Headers(secondStream, stop.Token));
                await secondStream.WriteAsync(response, stop.Token); served++;
            }
            HostCheck.Equal(0, await secondStream.ReadAsync(new byte[1], stop.Token));
        }
        try
        {
            try
            {
                _ = await NativeAotServiceQuotaPeer.RunObservedAsync(port, deadline.Token, trace =>
                {
                    callbacks++;
                    snapshot = NativeAotServiceQuotaPeer.InspectWireReport(Encoding.UTF8.GetBytes(
                        NativeAotServiceQuotaPeer.SerializeWireReport(trace)));
                });
            }
            catch (ToolFailure error) { refusal = error.Reason; }
            await serving.WaitAsync(deadline.Token);
        }
        finally
        {
            stop.Cancel(); listener.Stop();
            try { await serving.WaitAsync(TimeSpan.FromSeconds(5)); }
            catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
            catch (SocketException) when (stop.IsCancellationRequested) { }
            catch (ObjectDisposedException) when (stop.IsCancellationRequested) { }
        }
        HostCheck.Equal("quota_peer_initial_lifecycle", refusal ?? "accepted");
        HostCheck.True(served == 3 && callbacks == 1 && snapshot is { Purpose: "quota-partial" });
        var actual = snapshot!;
        HostCheck.True(actual.Connections.Count == 2 && actual.Connections.All(c => c.Role == "control" && c.DisposedTicks > 0) &&
            actual.Exchanges.Count == 3 && actual.Samples.Count == 1 && actual.Samples[0].Name == "health.startup" &&
            Encoding.ASCII.GetString(Convert.FromBase64String(actual.Samples[0].BodyBase64)) == "CLROS_SERVICE_HEALTH_OK");
        HostCheck.True(actual.Exchanges[0] is { Id: 1, Role: "control", Method: "GET", Path: "/healthz", Outcome: "failed", Status: 0 } &&
            actual.Exchanges[1] is { Id: 2, Role: "control", Method: "GET", Path: "/healthz", Outcome: "complete", Status: 200 } &&
            actual.Exchanges[2] is { Id: 3, Role: "control", Method: "GET", Path: "/__clros/service/lifecycle/status", Outcome: "complete", Status: 200 } &&
            Encoding.ASCII.GetString(Convert.FromBase64String(actual.Exchanges[1].BodyBase64)) == "CLROS_SERVICE_HEALTH_OK");
        HostCheck.True(actual.Exchanges[0].ConnectionIds.Single() != actual.Exchanges[1].ConnectionIds.Single() &&
            actual.Exchanges[1].ConnectionIds.Single() == actual.Exchanges[2].ConnectionIds.Single());
        for (int i = 0; i < 2; i++)
        {
            int connection = actual.Exchanges[i].ConnectionIds.Single();
            byte[] writes = Bytes(actual, connection, "write"), reads = Bytes(actual, connection, "read");
            byte[] expectedWrites = i == 0 ? requests[0] : [.. requests[1], .. requests[2]];
            byte[] expectedReads = i == 0 ? truncated : [.. healthy, .. invalid];
            HostCheck.True(writes.AsSpan().SequenceEqual(expectedWrites) && reads.AsSpan().SequenceEqual(expectedReads));
        }
        HostCheck.True(requests.Take(2).All(r => Encoding.ASCII.GetString(r).StartsWith("GET /healthz HTTP/1.1\r\n", StringComparison.Ordinal)) &&
            Encoding.ASCII.GetString(requests[2]).StartsWith("GET /__clros/service/lifecycle/status HTTP/1.1\r\n", StringComparison.Ordinal));
    }

    private static byte[] Bytes(NativeAotServiceQuotaPeer.WireTrace trace, int connection, string direction) =>
        trace.Operations.Where(o => o.ConnectionId == connection && o.Direction == direction && o.Outcome == "complete")
            .SelectMany(o => Convert.FromBase64String(o.DataBase64)).ToArray();

    private static byte[] Response(string body) => Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: " +
        body.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\r\n\r\n" + body);

    private static async Task<byte[]> Headers(NetworkStream stream, CancellationToken token)
    {
        using var bytes = new MemoryStream(); byte[] one = new byte[1];
        while (bytes.Length < 4096)
        {
            HostCheck.Equal(1, await stream.ReadAsync(one, token)); bytes.WriteByte(one[0]);
            if (bytes.Length >= 4 && bytes.GetBuffer().AsSpan((int)bytes.Length - 4, 4).SequenceEqual("\r\n\r\n"u8)) return bytes.ToArray();
        }
        throw new InvalidDataException("startup fixture request header bound");
    }
}
