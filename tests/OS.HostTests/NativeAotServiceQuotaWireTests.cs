using System.Net;
using System.Net.Sockets;
using System.Text;
using OS.Tooling;

internal static class NativeAotServiceQuotaWireTests
{
    [HostTest("quota wire observer copies actual connected persistent duplex bytes offsets and EOF")]
    private static async Task Duplex()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var client = new TcpClient(AddressFamily.InterNetwork);
        var accepting = listener.AcceptTcpClientAsync(deadline.Token).AsTask();
        await client.ConnectAsync(IPAddress.Loopback, port, deadline.Token);
        using var server = await accepting;
        int local = ((IPEndPoint)client.Client.LocalEndPoint!).Port;
        byte[] original = "GET /healthz HTTP/1.1\r\nHost: clros.service\r\n\r\n"u8.ToArray(), request = original.ToArray();
        byte[] response = "HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok"u8.ToArray();
        var serving = Serve();
        async Task Serve()
        {
            byte[] received = new byte[original.Length];
            await server.GetStream().ReadExactlyAsync(received, deadline.Token);
            HostCheck.True(received.AsSpan().SequenceEqual(original));
            await server.GetStream().WriteAsync(response.AsMemory(0, 17), deadline.Token);
            await server.GetStream().WriteAsync(response.AsMemory(17), deadline.Token);
            server.Client.Shutdown(SocketShutdown.Send);
        }
        var trace = await NativeAotServiceQuotaPeer.ObserveConnectedStreamForTestsAsync(client, async (stream, token) =>
        {
            await stream.WriteAsync(request, token);
            Array.Fill(request, (byte)'x');
            byte[] buffer = new byte[7];
            while (await stream.ReadAsync(buffer, token) != 0)
            {
            }
            HostCheck.Equal(0, await stream.ReadAsync(Memory<byte>.Empty, token));
        }, deadline.Token);
        await serving;
        var checkedTrace = NativeAotServiceQuotaPeer.InspectWireReport(Encoding.UTF8.GetBytes(NativeAotServiceQuotaPeer.SerializeWireReport(trace)));
        HostCheck.True(checkedTrace.Connections.Count == 1 && checkedTrace.Connections[0].LocalPort == local &&
            checkedTrace.Connections[0].RemotePort == port && checkedTrace.Connections[0].DisposedTicks > 0);
        byte[] sent = checkedTrace.Operations.Where(o => o.Direction == "write").SelectMany(o => Convert.FromBase64String(o.DataBase64)).ToArray();
        byte[] read = checkedTrace.Operations.Where(o => o.Direction == "read").SelectMany(o => Convert.FromBase64String(o.DataBase64)).ToArray();
        HostCheck.True(sent.AsSpan().SequenceEqual(original) && read.AsSpan().SequenceEqual(response));
        HostCheck.True(checkedTrace.Operations.Any(o => o.Outcome == "eof" && o.Requested > 0) &&
            checkedTrace.Operations.Any(o => o.Outcome == "zero" && o.Requested == 0));
    }

    [HostTest("quota wire observer records actual canceled read without fabricated returned bytes")]
    private static async Task Cancellation()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var client = new TcpClient(AddressFamily.InterNetwork);
        var accepting = listener.AcceptTcpClientAsync(deadline.Token).AsTask();
        await client.ConnectAsync(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port, deadline.Token);
        using var server = await accepting;
        bool canceled = false;
        var trace = await NativeAotServiceQuotaPeer.ObserveConnectedStreamForTestsAsync(client, async (stream, ignored) =>
        {
            using var stop = new CancellationTokenSource();
            var pending = stream.ReadAsync(new byte[1], stop.Token).AsTask();
            stop.Cancel();
            try
            {
                _ = await pending;
            }
            catch (OperationCanceledException) { canceled = true; }
        }, deadline.Token);
        HostCheck.True(canceled && trace.Operations.Count == 1 && trace.Operations[0].Outcome == "canceled" &&
            trace.Operations[0].Transferred == -1 && trace.Operations[0].DataBase64 == "" && trace.RawBytes == 0);
        HostCheck.Equal(0, await server.GetStream().ReadAsync(new byte[1], deadline.Token));
        _ = NativeAotServiceQuotaPeer.InspectWireReport(Encoding.UTF8.GetBytes(NativeAotServiceQuotaPeer.SerializeWireReport(trace)));
    }

    [HostTest("quota wire operation overflow rejects before writing any bytes and closes its stream")]
    private static async Task BoundBeforeIo()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var client = new TcpClient(AddressFamily.InterNetwork);
        var accepting = listener.AcceptTcpClientAsync(deadline.Token).AsTask();
        await client.ConnectAsync(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port, deadline.Token);
        using var server = await accepting;
        bool rejected = false;
        var trace = await NativeAotServiceQuotaPeer.ObserveConnectedStreamForTestsAsync(client, async (stream, token) =>
        {
            try
            {
                await stream.WriteAsync(new byte[NativeAotServiceQuotaPeer.WireMaximumOperationBytes + 1], token);
            }
            catch (ToolFailure) { rejected = true; }
        }, deadline.Token);
        HostCheck.True(rejected && trace.Operations.Count == 0 && trace.RawBytes == 0);
        HostCheck.Equal(0, await server.GetStream().ReadAsync(new byte[1], deadline.Token));
    }
    [HostTest("quota wire actual HTTP failure retains joined raw bytes and preserves callback failure beside peer error")]
    private static async Task PartialHttpFailure()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        byte[] response = "HTTP/1.1 200 OK\r\nContent-Length: 3\r\n\r\nbad"u8.ToArray();
        byte[]? received = null;
        NativeAotServiceQuotaPeer.WireTrace? snapshot = null;
        int callbacks = 0;
        bool preserved = false;
        var serving = Serve();
        async Task Serve()
        {
            using var client = await listener.AcceptTcpClientAsync(deadline.Token);
            using var bytes = new MemoryStream();
            byte[] one = new byte[1];
            while (bytes.Length < 4096)
            {
                HostCheck.Equal(1, await client.GetStream().ReadAsync(one, deadline.Token));
                bytes.WriteByte(one[0]);
                if (bytes.Length >= 4 && bytes.GetBuffer().AsSpan((int)bytes.Length - 4, 4).SequenceEqual("\r\n\r\n"u8))
                    break;
            }
            received = bytes.ToArray();
            await client.GetStream().WriteAsync(response, deadline.Token);
            HostCheck.Equal(0, await client.GetStream().ReadAsync(one, deadline.Token));
        }
        try
        {
            _ = await NativeAotServiceQuotaPeer.RunObservedAsync(port, deadline.Token, trace =>
            {
                callbacks++;
                snapshot = NativeAotServiceQuotaPeer.InspectWireReport(Encoding.UTF8.GetBytes(
                    NativeAotServiceQuotaPeer.SerializeWireReport(trace)));
                throw new IOException("wire_sink_fixture");
            });
        }
        catch (AggregateException error)
        {
            preserved = error.InnerExceptions.Count == 2 && error.InnerExceptions[0] is ToolFailure { Reason: "quota_peer_startup_health" } &&
                error.InnerExceptions[1] is IOException { Message: "wire_sink_fixture" };
        }
        finally { await serving.WaitAsync(TimeSpan.FromSeconds(10)); }
        HostCheck.True(preserved && callbacks == 1 && snapshot is { Purpose: "quota-partial" } && received is not null);
        byte[] written = snapshot!.Operations.Where(o => o.Direction == "write" && o.Outcome == "complete")
            .SelectMany(o => Convert.FromBase64String(o.DataBase64)).ToArray();
        byte[] read = snapshot.Operations.Where(o => o.Direction == "read" && o.Outcome == "complete")
            .SelectMany(o => Convert.FromBase64String(o.DataBase64)).ToArray();
        HostCheck.True(written.AsSpan().SequenceEqual(received) && read.AsSpan().SequenceEqual(response) &&
            snapshot.Connections.Count == 1 && snapshot.Connections[0].DisposedTicks > 0 && snapshot.Exchanges.Count == 1 &&
            snapshot.Exchanges[0].Outcome == "complete" && snapshot.Samples.Count == 0);
    }

    [HostTest("quota wire already-canceled run exposes a partial snapshot without masking cancellation")]
    private static async Task AlreadyCanceled()
    {
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        NativeAotServiceQuotaPeer.WireTrace? snapshot = null;
        bool preserved = false;
        try
        {
            _ = await NativeAotServiceQuotaPeer.RunObservedAsync(8080, canceled.Token, trace => snapshot = trace);
        }
        catch (OperationCanceledException) { preserved = true; }
        HostCheck.True(preserved && snapshot is { Purpose: "quota-partial", RawBytes: 0 } && snapshot.Connections.Count == 0);
        _ = NativeAotServiceQuotaPeer.InspectWireReport(Encoding.UTF8.GetBytes(NativeAotServiceQuotaPeer.SerializeWireReport(snapshot!)));
    }

    [HostTest("quota wire read cap reserves before actual I/O and preserves serializable joined snapshot")]
    private static async Task ReadCapacityBeforeIo()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var client = new TcpClient(AddressFamily.InterNetwork);
        var accepting = listener.AcceptTcpClientAsync(deadline.Token).AsTask();
        await client.ConnectAsync(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port, deadline.Token);
        using var server = await accepting;
        var sent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var serving = Serve();
        async Task Serve()
        {
            byte[] bytes = new byte[NativeAotServiceQuotaPeer.WireMaximumOperationBytes];
            Array.Fill(bytes, (byte)0x5a);
            int left = NativeAotServiceQuotaPeer.WireMaximumRawBytes + 1;
            while (left > 0)
            {
                int count = Math.Min(left, bytes.Length);
                await server.GetStream().WriteAsync(bytes.AsMemory(0, count), deadline.Token);
                left -= count;
            }
            sent.TrySetResult();
            await release.Task.WaitAsync(deadline.Token);
        }
        NativeAotServiceQuotaPeer.WireTrace? trace = null;
        try
        {
            trace = await NativeAotServiceQuotaPeer.ObserveConnectedStreamForTestsAsync(client, async (stream, token) =>
            {
                byte[] bytes = new byte[NativeAotServiceQuotaPeer.WireMaximumOperationBytes];
                int left = NativeAotServiceQuotaPeer.WireMaximumRawBytes - 1;
                while (left > 0)
                {
                    int requested = Math.Min(left, bytes.Length);
                    int read = await stream.ReadAsync(bytes.AsMemory(0, requested), token);
                    HostCheck.True(read > 0);
                    HostCheck.True(bytes.AsSpan(0, read).IndexOfAnyExcept((byte)0x5a) < 0);
                    left -= read;
                }
                await sent.Task.WaitAsync(token);
                // Wait for the two real remaining bytes to reach the local socket.
                // Available does not consume either byte or fabricate recorder I/O.
                while (client.Available < 2)
                    await Task.Delay(1, token);
                int available = client.Available;
                bool rejected = false;
                try
                {
                    _ = await stream.ReadAsync(bytes.AsMemory(0, 2), token);
                }
                catch (ToolFailure) { rejected = true; }
                HostCheck.True(rejected && client.Available == available);
            }, deadline.Token);
        }
        finally
        {
            release.TrySetResult();
            server.Dispose();
            client.Dispose();
            await serving.WaitAsync(TimeSpan.FromSeconds(10));
        }
        var actual = trace ?? throw new InvalidOperationException("missing joined wire snapshot");
        HostCheck.True(actual.RawBytes == NativeAotServiceQuotaPeer.WireMaximumRawBytes - 1);
        HostCheck.True(actual.Operations.All(o => o.Direction == "read" && o.Outcome == "complete" &&
            o.Transferred > 0 && o.Transferred <= o.Requested));
        HostCheck.Equal(actual.RawBytes, actual.Operations.Sum(o => (long)o.Transferred));
        var copy = NativeAotServiceQuotaPeer.InspectWireReport(Encoding.UTF8.GetBytes(NativeAotServiceQuotaPeer.SerializeWireReport(actual)));
        HostCheck.True(copy.RawBytes == actual.RawBytes && copy.Operations.Count == actual.Operations.Count &&
            copy.Operations.Sum(o => (long)Convert.FromBase64String(o.DataBase64).Length) == actual.RawBytes);
    }

}
