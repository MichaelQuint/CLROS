using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;

internal static partial class WhpxPrelaunchTests
{
    [HostTest("WHPX QMP connection survives handshake writer disposal until child join")]
    private static async Task QmpConnectionSurvivesHandshake()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var client = new TcpClient();
        Task connect = client.ConnectAsync((IPEndPoint)listener.LocalEndpoint, deadline.Token).AsTask();
        using TcpClient server = await listener.AcceptTcpClientAsync(deadline.Token);
        await connect;
        object owned = CreateQmpConnection(client);
        try
        {
            NetworkStream stream = BorrowQmpStream(owned);
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 4096, leaveOpen: true) { AutoFlush = true })
                await writer.WriteLineAsync("cont".AsMemory(), deadline.Token);
            // StreamWriter uses the platform newline. Read the exact emitted
            // command without a sleep or an inferred socket-disconnect delay.
            byte[] handshake = new byte[Encoding.UTF8.GetByteCount("cont" + Environment.NewLine)];
            await server.GetStream().ReadExactlyAsync(handshake, deadline.Token);
            HostCheck.Equal("cont" + Environment.NewLine, Encoding.UTF8.GetString(handshake));
            await stream.WriteAsync(new byte[] { 0x42 }, deadline.Token);
            byte[] afterHandshake = new byte[1];
            await server.GetStream().ReadExactlyAsync(afterHandshake, deadline.Token);
            HostCheck.Equal((byte)0x42, afterHandshake[0]);
        }
        finally
        {
            CloseQmpConnection(owned, joined: true);
        }
        byte[] end = new byte[1];
        HostCheck.Equal(0, await server.GetStream().ReadAsync(end, deadline.Token));
        CloseQmpConnection(owned, joined: true);
    }

    [HostTest("WHPX QMP connection rejects early close without losing the live channel")]
    private static async Task QmpConnectionRejectsUnjoinedClose()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var client = new TcpClient();
        Task connect = client.ConnectAsync((IPEndPoint)listener.LocalEndpoint, deadline.Token).AsTask();
        using TcpClient server = await listener.AcceptTcpClientAsync(deadline.Token);
        await connect;
        object owned = CreateQmpConnection(client);
        try
        {
            bool rejected = false;
            try
            {
                CloseQmpConnection(owned, joined: false);
            }
            catch (TargetInvocationException error) when (error.InnerException is IOException { Message: "whpx_prelaunch_qmp_child_unjoined" })
            {
                rejected = true;
            }
            HostCheck.True(rejected);
            await BorrowQmpStream(owned).WriteAsync(new byte[] { 0x7f }, deadline.Token);
            byte[] received = new byte[1];
            await server.GetStream().ReadExactlyAsync(received, deadline.Token);
            HostCheck.Equal((byte)0x7f, received[0]);
        }
        finally
        {
            CloseQmpConnection(owned, joined: true);
        }
    }

    private static Type QmpConnectionType => Target.GetNestedType("QmpConnection", BindingFlags.NonPublic)!;

    private static object CreateQmpConnection(TcpClient client) => Activator.CreateInstance(QmpConnectionType,
        BindingFlags.Instance | BindingFlags.NonPublic, binder: null, args: [client], culture: null)!;

    private static NetworkStream BorrowQmpStream(object owned) => (NetworkStream)QmpConnectionType
        .GetProperty("Stream", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owned)!;

    private static void CloseQmpConnection(object owned, bool joined) => QmpConnectionType
        .GetMethod("CloseAfterChildJoin", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(owned, [joined]);
}
