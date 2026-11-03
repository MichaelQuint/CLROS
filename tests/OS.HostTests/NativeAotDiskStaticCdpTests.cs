using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OS.Tooling;
using B = OS.Tooling.NativeAotDiskStaticBrowser;

internal static partial class NativeAotDiskStaticCdpTests
{
    private static readonly ProcessResult Good = new(0, "", "", false, false, false, TimeSpan.FromSeconds(1));
    [HostTest("CDP acknowledges Browser.close before clean EOF and retains normal close frames")]
    private static async Task AcknowledgedShutdown()
    {
        await Scenario(End.Eof, Good, accepted: true);
        await Scenario(End.CloseFrame, Good, accepted: true);
    }

    [HostTest("CDP EOF before Browser.close acknowledgement is not successful shutdown")]
    private static Task MissingAcknowledgement() => Scenario(End.BeforeAck, Good, accepted: false);
    [HostTest("CDP snapshots pending calls before EOF fault cleanup clears them")]
    private static Task PendingCommand() => Scenario(End.Pending, Good, accepted: false);
    [HostTest("CDP shutdown requires the exact successful Browser.close response")]
    private static async Task ExactSuccessfulAcknowledgement()
    {
        await Scenario(End.OtherAck, Good, accepted: false);
        await Scenario(End.ErrorAck, Good, accepted: false);
    }

    [HostTest("CDP premature EOF cannot excuse failed cancelled timed out or unjoined browser processes")]
    private static async Task ProcessEvidence()
    {
        foreach (var process in new[]
        {
            Good with
            {
                ExitCode = 1
            },
            Good with
            {
                Cancelled = true
            },
            Good with
            {
                TimedOut = true
            },
            Good with
            {
                OutputLimitExceeded = true
            },
            Good with
            {
                TerminationConfirmed = false
            },
            Good with
            {
                FailureReason = "forced_termination"
            }
        }

        )
            await Scenario(End.Eof, process, accepted: false);
    }

    [HostTest("CDP shutdown does not hide malformed partial empty-fragment or other transport failures")]
    private static async Task MessageAndTransportFailures()
    {
        foreach (var ending in new[]
        {
            End.BadJson,
            End.Partial,
            End.EmptyFragment,
            End.Binary,
            End.InvalidFrame
        }

        )
            await Scenario(ending, Good, accepted: false);
    }

    private static async Task<Exception?> Failure(Task operation)
    {
        try
        {
            await operation;
            return null;
        }
        catch (Exception error)when (error is not OperationCanceledException and not TimeoutException)
        {
            return error;
        }
    }

    private static async Task Scenario(End ending, ProcessResult browser, bool accepted)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        string log = Path.Combine(Path.GetTempPath(), "clros-cdp-shutdown-" + Guid.NewGuid().ToString("N") + ".jsonl");
        var pendingReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cdp = new B.Cdp(log, static (_, _) =>
        {
        });
        Task server = Serve();
        Task<JsonElement>? pending = null, close = null;
        try
        {
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            await cdp.Connect(new Uri("ws://127.0.0.1:" + port + "/devtools/page/test"), deadline.Token);
            if (ending == End.Pending)
            {
                pending = cdp.Call("Runtime.evaluate", new { expression = "1" }, deadline.Token);
                await pendingReceived.Task.WaitAsync(deadline.Token);
            }

            close = cdp.Call(ending == End.OtherAck ? "Runtime.evaluate" : "Browser.close", new { }, deadline.Token);
            // The peer has sent every byte and exited before Join is checked.
            // No sleep or guessed delay substitutes for this barrier.
            await server.WaitAsync(deadline.Token);
            Exception? commandError = await Failure(close.WaitAsync(deadline.Token));
            Exception? joinError = await Failure(cdp.Join(browser).WaitAsync(deadline.Token));
            if (accepted)
                HostCheck.True(commandError is null && joinError is null, "acknowledged_shutdown:" + ending);
            else
                HostCheck.True(joinError is not null, "shutdown_must_reject:" + ending);
            if (ending is End.BeforeAck or End.ErrorAck)
                HostCheck.True(commandError is not null, "close_command_rejected");
            else
                HostCheck.True(commandError is null, "successful_response_precedes_transport_end");
            if (pending is not null)
                HostCheck.True(await Failure(pending.WaitAsync(deadline.Token))is not null, "pending_call_failed_and_joined");
            if (ending == End.BadJson)
                HostCheck.True(joinError is JsonException, "malformed_json_preserved");
            if (ending == End.InvalidFrame)
                HostCheck.True(joinError is WebSocketException error && error.WebSocketErrorCode != WebSocketError.ConnectionClosedPrematurely, "other_websocket_failure_preserved");
        }
        finally
        {
            deadline.Cancel();
            listener.Stop();
            await cdp.DisposeAsync();
            // Cancellation only releases a failed test's owned loopback peer.
            // Every server task is observed, including setup/assertion failures.
            try
            {
                await server.WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (Exception error)when (error is OperationCanceledException or IOException or WebSocketException or ObjectDisposedException)
            {
            }

            foreach (Task? command in new Task? []
            {
                pending,
                close
            }

            )
                if (command is not null)
                    try
                    {
                        await command;
                    }
                    catch (Exception error)when (error is OperationCanceledException or IOException or WebSocketException or ToolFailure)
                    {
                    }

            if (File.Exists(log))
                File.Delete(log);
        }

        async Task Serve()
        {
            using TcpClient tcp = await listener.AcceptTcpClientAsync(deadline.Token);
            await using NetworkStream stream = tcp.GetStream();
            await Upgrade(stream, deadline.Token);
            using WebSocket socket = WebSocket.CreateFromStream(stream, isServer: true, subProtocol: null, keepAliveInterval: Timeout.InfiniteTimeSpan);
            if (ending == End.Pending)
            {
                _ = await Command(socket, "Runtime.evaluate", deadline.Token);
                pendingReceived.SetResult();
            }

            int id = await Command(socket, ending == End.OtherAck ? "Runtime.evaluate" : "Browser.close", deadline.Token);
            if (ending == End.BeforeAck)
                return;
            string response = ending == End.ErrorAck ? JsonSerializer.Serialize(new { id, error = new { code = -1, message = "rejected close" } }) : JsonSerializer.Serialize(new { id, result = new { } });
            await socket.SendAsync(Encoding.UTF8.GetBytes(response).AsMemory(), WebSocketMessageType.Text, true, deadline.Token);
            switch (ending)
            {
                case End.CloseFrame:
                    await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", deadline.Token);
                    break;
                case End.BadJson:
                    await socket.SendAsync("{"u8.ToArray().AsMemory(), WebSocketMessageType.Text, true, deadline.Token);
                    break;
                case End.Partial:
                    await socket.SendAsync("{"u8.ToArray().AsMemory(), WebSocketMessageType.Text, false, deadline.Token);
                    break;
                case End.EmptyFragment:
                    await socket.SendAsync(ReadOnlyMemory<byte>.Empty, WebSocketMessageType.Text, false, deadline.Token);
                    break;
                case End.Binary:
                    await socket.SendAsync(new byte[] { 1 }.AsMemory(), WebSocketMessageType.Binary, true, deadline.Token);
                    break;
                case End.InvalidFrame:
                    // Reserved opcode3 is a real framing error, not missing close handshake.
                    await stream.WriteAsync(new byte[] { 0x83, 0x00 }, deadline.Token);
                    break;
            }
        // Dispose the TCP transport WITHOUT a WebSocket close handshake.
        }
    }

    private static async Task<int> Command(WebSocket socket, string expected, CancellationToken token)
    {
        byte[] buffer = new byte[1024];
        using var message = new MemoryStream();
        ValueWebSocketReceiveResult part;
        do
        {
            part = await socket.ReceiveAsync(buffer.AsMemory(), token);
            HostCheck.True(part.MessageType == WebSocketMessageType.Text && message.Length + part.Count <= 8192, "loopback_command_bound");
            message.Write(buffer, 0, part.Count);
        }
        while (!part.EndOfMessage);
        using var json = JsonDocument.Parse(message.ToArray());
        HostCheck.Equal(expected, json.RootElement.GetProperty("method").GetString());
        return json.RootElement.GetProperty("id").GetInt32();
    }

    private static async Task Upgrade(NetworkStream stream, CancellationToken token)
    {
        byte[] header = new byte[8192];
        int count = 0;
        do
        {
            HostCheck.True(count < header.Length, "loopback_upgrade_bound");
            int read = await stream.ReadAsync(header.AsMemory(count, 1), token);
            HostCheck.True(read == 1, "loopback_upgrade_eof");
            count += read;
        }
        while (count < 4 || !header.AsSpan(count - 4, 4).SequenceEqual("\r\n\r\n"u8));
        string request = Encoding.ASCII.GetString(header, 0, count);
        string key = request.Split("\r\n").Single(line => line.StartsWith("Sec-WebSocket-Key:", StringComparison.OrdinalIgnoreCase)).Split(':', 2)[1].Trim();
        string accept = Convert.ToBase64String(SHA1.HashData(Encoding.ASCII.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
        await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: " + accept + "\r\n\r\n"), token);
    }
}
