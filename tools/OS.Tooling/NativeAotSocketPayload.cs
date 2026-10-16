using System.Net;
using System.Net.Sockets;

namespace OS.Tooling;

/// <summary>
/// Host-side external peer for the finite System.Net.Sockets application. Owns
/// only its four loopback TCP clients and a two-minute deadline. Connection
/// refusal is retried until the deadline; wrong bytes, EOF/reset semantics or
/// socket failures abort without a pass marker. This does not launch a guest,
/// validate packets or confer CLROS acceptance.
/// </summary>
public static class NativeAotSocketPayload
{
    public static async Task<int> RunAsync(string[] args, TextWriter output)
    {
        output.WriteLine("TEST_START nativeaot.socket.peer");
        try
        {
            if (args is not ["peer", "--port", var value] || !int.TryParse(value, out int port) ||
                port is < 1 or > 65535)
                throw new ArgumentException("nativeaot socket peer --port <1..65535>");
            await RunPeerAsync(port, output);
            output.WriteLine("TEST_PASS nativeaot.socket.peer");
            return 0;
        }
        catch (Exception error)
        {
            output.WriteLine("TEST_FAIL nativeaot.socket.peer " + error.Message.ReplaceLineEndings(" "));
            return 1;
        }
    }

    /// <summary>
    /// Exchanges independent literal bytes with a separately started application
    /// through the specified loopback port (normally a QEMU forward). The first
    /// receive is deliberately held for 250ms to exercise backpressure; reset
    /// publication holds its single byte for 100ms before abortive close. These
    /// bounded integration stimuli are not exact timing assertions.
    /// </summary>
    public static async Task RunPeerAsync(int port, TextWriter output)
    {
        if (port is < 1 or > 65535) throw new ArgumentOutOfRangeException(nameof(port));
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        CancellationToken token = deadline.Token;
        byte[] request = "CLROS_SOCKET_REQUEST"u8.ToArray();
        byte[] reply = "CLROS_SOCKET_REPLY"u8.ToArray();
        byte[] buffer = new byte[4096];
        for (int index = 0; index < 4; index++)
        {
            using Socket socket = await ConnectAsync(port, token);
            if (index == 1)
            {
                if (await socket.SendAsync(new byte[] { 0x52 }, SocketFlags.None, token) != 1)
                    throw new InvalidDataException("socket_peer_reset_byte");
                await Task.Delay(100, token);
                socket.LingerState = new LingerOption(true, 0);
                output.WriteLine("SOCKET_PEER_CONNECTION index=1 sent=1 close=reset");
                continue;
            }
            await SendAllAsync(socket, request.AsMemory(0, 5), token);
            await SendAllAsync(socket, request.AsMemory(5), token);
            int expected = index == 0 ? 65536 : reply.Length;
            if (index == 0) await Task.Delay(250, token);
            else socket.Shutdown(SocketShutdown.Send);
            int received = 0;
            while (received < expected)
            {
                int count = await socket.ReceiveAsync(buffer.AsMemory(0, Math.Min(buffer.Length, expected - received)),
                    SocketFlags.None, token);
                if (count == 0) throw new InvalidDataException("socket_peer_early_eof");
                for (int i = 0; i < count; i++)
                {
                    byte wanted = index == 0 ? (byte)((received + i) % 251) : reply[received + i];
                    if (buffer[i] != wanted) throw new InvalidDataException("socket_peer_payload");
                }
                received += count;
            }
            if (await socket.ReceiveAsync(buffer.AsMemory(0, 1), SocketFlags.None, token) != 0)
                throw new InvalidDataException("socket_peer_trailing_payload");
            output.WriteLine($"SOCKET_PEER_CONNECTION index={index} sent={request.Length} received={received} close=eof");
        }
    }

    private static async Task<Socket> ConnectAsync(int port, CancellationToken token)
    {
        while (true)
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                socket.NoDelay = true;
                socket.ReceiveBufferSize = 4096;
                await socket.ConnectAsync(IPAddress.Loopback, port, token);
                return socket;
            }
            catch (SocketException error) when (error.SocketErrorCode == SocketError.ConnectionRefused)
            {
                socket.Dispose();
                await Task.Delay(50, token);
            }
            catch { socket.Dispose(); throw; }
        }
    }

    private static async Task SendAllAsync(Socket socket, ReadOnlyMemory<byte> bytes, CancellationToken token)
    {
        while (!bytes.IsEmpty)
        {
            int sent = await socket.SendAsync(bytes, SocketFlags.None, token);
            if (sent <= 0) throw new InvalidDataException("socket_peer_send");
            bytes = bytes[sent..];
        }
    }
}
