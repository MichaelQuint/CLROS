using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using OS.Tooling;

internal static partial class KestrelConnectionLifecycleTests
{
    // Deliberately a small HTTP transport fixture, not Kestrel or a guest. The
    // real app worker is shared; the socket framing/lifetime is controlled here.
    private sealed class PeerServer : IAsyncDisposable
    {
        private readonly TcpListener listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource lifetime = new(TimeSpan.FromSeconds(30));
        private readonly ConcurrentBag<Task> workers = [];
        private readonly Task accept;
        private readonly string fault;
        private readonly ServiceLifecycleExercise exercise = new();
        internal int Port { get; }

        internal PeerServer(string fault = "")
        {
            this.fault = fault;
            listener.Start();
            Port = ((IPEndPoint)listener.LocalEndpoint).Port;
            accept = Accept();
        }

        private async Task Accept()
        {
            try
            {
                while (!lifetime.IsCancellationRequested)
                {
                    var client = await listener.AcceptTcpClientAsync(lifetime.Token);
                    client.NoDelay = true;
                    client.SendBufferSize = 1024;
                    workers.Add(Serve(client));
                }
            }
            catch (OperationCanceledException)when (lifetime.IsCancellationRequested)
            {
            }
        }

        private async Task Serve(TcpClient client)
        {
            using (client)
            {
                try
                {
                    var stream = client.GetStream();
                    byte[] raw = new byte[8192];
                    int count = 0;
                    do
                    {
                        HostCheck.True(count < raw.Length);
                        await stream.ReadExactlyAsync(raw.AsMemory(count++, 1), lifetime.Token);
                    }
                    while (count < 4 || !raw.AsSpan(count - 4, 4).SequenceEqual("\r\n\r\n"u8));
                    string[] rows = Encoding.ASCII.GetString(raw, 0, count - 4).Split("\r\n");
                    string path = rows[0].Split(' ')[1];
                    async Task Header(int status, int length, bool chunked = false)
                    {
                        string reason = status == 400 ? "Bad Request" : "OK";
                        byte[] head = Encoding.ASCII.GetBytes($"HTTP/1.1 {status} {reason}\r\nConnection: close\r\nDate: {DateTimeOffset.UtcNow.ToString("r", CultureInfo.InvariantCulture)}\r\nServer: CLROS-host-fixture\r\n" + (chunked ? "Transfer-Encoding: chunked\r\n" : "Content-Length: " + length.ToString(CultureInfo.InvariantCulture) + "\r\n") + "\r\n");
                        await stream.WriteAsync(head, lifetime.Token);
                    }

                    async Task Reply(int status, string body)
                    {
                        byte[] bytes = Encoding.ASCII.GetBytes(body);
                        await Header(status, bytes.Length);
                        await stream.WriteAsync(bytes, lifetime.Token);
                    }

                    if (path == "/healthz")
                        await Reply(200, "CLROS_SERVICE_HEALTH_OK");
                    else if (path == "/__clros/service/lifecycle/status")
                    {
                        string text = exercise.Read();
                        if (fault == "shape")
                            text += " unexpected=1";
                        byte[] bytes = Encoding.ASCII.GetBytes(text);
                        await Header(200, bytes.Length);
                        if (fault == "truncated")
                            bytes = bytes[..^1];
                        await stream.WriteAsync(bytes, lifetime.Token);
                        if (fault == "trailing")
                            await stream.WriteAsync("x"u8.ToArray(), lifetime.Token);
                    }
                    else if (path == "/__clros/service/lifecycle/echo")
                    {
                        int length = int.Parse(rows.Single(x => x.StartsWith("Content-Length: ", StringComparison.Ordinal))[16..], CultureInfo.InvariantCulture);
                        HostCheck.Equal(32768, length);
                        byte[] input = new byte[length];
                        await stream.ReadExactlyAsync(input, lifetime.Token);
                        using var body = new MemoryStream(input);
                        var result = await exercise.ReadEchoAsync(body, length, lifetime.Token);
                        await Reply(result.Accepted ? 200 : 400, result.Text);
                    }
                    else if (path == "/__clros/service/lifecycle/stream")
                    {
                        await Header(200, ServiceLifecycleExercise.StreamBytes);
                        await exercise.WriteStreamAsync(stream, lifetime.Token);
                    }
                    else if (path == "/__clros/service/lifecycle/cancel")
                    {
                        await Header(200, 0, chunked: true);
                        using var canceled = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
                        Task wait = exercise.WaitForCancellationAsync(canceled.Token);
                        try
                        {
                            _ = await stream.ReadAsync(new byte[1], lifetime.Token);
                        }
                        catch (IOException)
                        {
                        }
                        finally
                        {
                            canceled.Cancel();
                            await wait;
                        }
                    }
                    else
                        throw new InvalidDataException("unexpected host fixture route");
                }
                catch (OperationCanceledException)when (lifetime.IsCancellationRequested)
                {
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            lifetime.Cancel();
            listener.Stop();
            await accept.WaitAsync(TimeSpan.FromSeconds(5));
            await Task.WhenAll(workers).WaitAsync(TimeSpan.FromSeconds(5));
            lifetime.Dispose();
        }
    }
}
