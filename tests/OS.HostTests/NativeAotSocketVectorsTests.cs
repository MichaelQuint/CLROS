using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using OS.Apps.NativeAotSocketVectors;

internal static class NativeAotSocketVectorsTests
{
    // Real loopback peers exercise the managed app, not kernel/guest syscall admission.
    [HostTest("m16-013 vector app exchanges exact bytes reset disposal cancellation and later health with external peers")]
    private static void ExternalPeers()
    {
        using var reservation = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        reservation.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        int port = ((IPEndPoint)reservation.LocalEndPoint!).Port; reservation.Dispose();
        using var output = new Markers();
        Task<int> app = Task.Run(() => SocketVectorsFixture.Run(["--port", port.ToString(CultureInfo.InvariantCulture)], output));
        output.WaitFor("SOCKET_VECTOR_LISTEN ");
        for (int index = 0; index < 4; index++)
        {
            using var peer = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            peer.ReceiveTimeout = peer.SendTimeout = 30_000; peer.ReceiveBufferSize = 4096;
            peer.Connect(IPAddress.Loopback, port);
            byte[] request = Enumerable.Range(0, 64).Select(i => (byte)((index * 37 + i * 11) % 251)).ToArray();
            if (index == 0)
            {
                Send(peer, request, 0, 3);
                output.WaitFor("SOCKET_VECTOR_RECEIVE index=0 bytes=3 total=3 ");
                Send(peer, request, 3, 61);
            }
            else Send(peer, request, 0, request.Length);
            output.WaitFor($"SOCKET_VECTOR_REQUEST index={index} bytes=64");
            if (index == 1)
            {
                peer.LingerState = new LingerOption(true, 0); peer.Dispose();
                output.WaitFor("SOCKET_VECTOR_CONNECTION index=1 ");
            }
            else if (index == 2)
            {
                output.WaitFor("SOCKET_VECTOR_PENDING index=2 pending=1");
                // Disposing the server's pending receive may close this peer with FIN or RST.
                try { HostCheck.Equal(0, peer.Receive(new byte[1])); }
                catch (SocketException error) when (error.SocketErrorCode == SocketError.ConnectionReset) { }
                output.WaitFor("SOCKET_VECTOR_CONNECTION index=2 ");
            }
            else
            {
                peer.Shutdown(SocketShutdown.Send);
                int expected = index == 0 ? 65_536 : 64, received = 0;
                byte[] buffer = new byte[1024];
                while (received < expected)
                {
                    int count = peer.Receive(buffer, 0, Math.Min(buffer.Length, expected - received), SocketFlags.None);
                    HostCheck.True(count > 0);
                    for (int i = 0; i < count; i++) HostCheck.Equal((byte)((received + i) % 251), buffer[i]);
                    received += count;
                }
                HostCheck.Equal(0, peer.Receive(buffer, 0, 1, SocketFlags.None));
            }
        }
        HostCheck.True(app.Wait(TimeSpan.FromSeconds(35)), output.Text);
        HostCheck.Equal(0, app.GetAwaiter().GetResult());
        string[] rows = output.Lines;
        HostCheck.Equal(4, rows.Count(row => row.StartsWith("SOCKET_VECTOR_CONNECTION ", StringComparison.Ordinal)));
        string first = rows.Single(row => row.StartsWith("SOCKET_VECTOR_CONNECTION index=0 ", StringComparison.Ordinal));
        HostCheck.True(first.Contains("received=64 sent=65536 ", StringComparison.Ordinal));
        HostCheck.True(!first.Contains("receive_partial=0 ", StringComparison.Ordinal));
        HostCheck.True(rows.Any(row => row.StartsWith("SOCKET_VECTOR_CONNECTION index=1 ", StringComparison.Ordinal) && row.Contains("reset=1 ", StringComparison.Ordinal)));
        HostCheck.True(rows.Any(row => row.StartsWith("SOCKET_VECTOR_CONNECTION index=2 ", StringComparison.Ordinal) && row.Contains("pending=1 aborted=1", StringComparison.Ordinal)));
        HostCheck.True(rows.Contains("TEST_PASS nativeaot.socket.vectors connections=4 received=256 sent=65600 reset=1 pending=1 aborted=1"));
        HostCheck.True(rows.Contains("APP_EXIT nativeaot.socket.vectors 0"));
        foreach (string row in rows) Console.WriteLine("VECTOR_APP_OUTPUT " + row);
    }

    [HostTest("m16-013 vector app rejects invalid ports with bounded numeric failure markers")]
    private static void InvalidArguments()
    {
        foreach (string port in new[] { "0", "65536", "peer-controlled-text" })
        {
            using var output = new StringWriter(CultureInfo.InvariantCulture);
            HostCheck.Equal(2, SocketVectorsFixture.Run(["--port", port], output));
            HostCheck.True(output.ToString().Contains("TEST_FAIL nativeaot.socket.vectors index=-1 reason=1 error=0", StringComparison.Ordinal));
            HostCheck.True(!output.ToString().Contains(port, StringComparison.Ordinal) || port == "0");
        }
    }

    private static void Send(Socket peer, byte[] bytes, int offset, int count)
    {
        while (count != 0) { int sent = peer.Send(bytes, offset, count, SocketFlags.None); HostCheck.True(sent > 0); offset += sent; count -= sent; }
    }

    private sealed class Markers : TextWriter
    {
        private readonly List<string> rows = [];
        private readonly AutoResetEvent changed = new(false);
        public override Encoding Encoding => Encoding.UTF8;
        internal string[] Lines { get { lock (rows) return rows.ToArray(); } }
        internal string Text => string.Join(Environment.NewLine, Lines);
        public override void WriteLine(string? value) { lock (rows) rows.Add(value ?? ""); changed.Set(); }
        internal void WaitFor(string prefix)
        {
            Stopwatch deadline = Stopwatch.StartNew();
            while (!Lines.Any(row => row.StartsWith(prefix, StringComparison.Ordinal)))
            {
                HostCheck.True(deadline.ElapsedMilliseconds < 30_000 && !Lines.Any(row => row.StartsWith("TEST_FAIL ", StringComparison.Ordinal)), Text);
                changed.WaitOne(50);
            }
        }
        protected override void Dispose(bool disposing) { if (disposing) changed.Dispose(); base.Dispose(disposing); }
    }
}
