using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OS.Tooling;
using OS.Tools.QemuRunner;

// Real loopback HTTP/stream controls, with independently supplied diagnostic
// bodies. They establish no native signal, guest flow, process or syscall proof.
internal static partial class NativeAotWebSignalRaceTransportTests
{
    internal sealed class Peer : IAsyncDisposable
    {
        private readonly bool partial, unsupported, startGate, eventOwned, stall;
        private readonly TcpListener listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource stop = new(TimeSpan.FromSeconds(15));
        private readonly TaskCompletionSource recovery = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource requestObserved = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly object gate = new();
        private readonly List<TcpClient> clients = [];
        private readonly List<Task> handlers = [];
        internal readonly List<Connection> Connections = [];
        private readonly Task accepting;
        private int churnStarted, startRequests;
        private bool raceStarted, recovered, canceled, complete, released, initialObserved;
        internal int Port { get; }
        internal string Endpoint => "http://127.0.0.1:" + Port;

        internal int StartRequests
        {
            get
            {
                lock (gate)
                    return startRequests;
            }
        }

        internal Task RequestObserved => requestObserved.Task;

        internal Action? RecoveryArmed;
        // Listener/handlers exist only when an explicitly selected test runs.
        internal Peer(bool partial = false, bool unsupported = false, bool startGate = false, bool eventOwned = false, bool stall = false)
        {
            this.partial = partial;
            this.unsupported = unsupported;
            this.startGate = startGate || eventOwned;
            this.eventOwned = eventOwned;
            this.stall = stall;
            listener.Start();
            Port = ((IPEndPoint)listener.LocalEndpoint).Port;
            accepting = Accept();
        }

        private async Task Accept()
        {
            try
            {
                while (true)
                {
                    var client = await listener.AcceptTcpClientAsync(stop.Token);
                    lock (gate)
                    {
                        clients.Add(client);
                        var connection = new Connection(((IPEndPoint)client.Client.RemoteEndPoint!).Port);
                        Connections.Add(connection);
                        handlers.Add(Serve(client, connection));
                    }
                }
            }
            catch (OperationCanceledException)when (stop.IsCancellationRequested)
            {
            }
            catch (SocketException)when (stop.IsCancellationRequested)
            {
            }
            catch (ObjectDisposedException)when (stop.IsCancellationRequested)
            {
            }
        }

        private async Task Serve(TcpClient client, Connection connection)
        {
            using (client)
            {
                try
                {
                    var stream = client.GetStream();
                    while (await Head(stream, stop.Token)is { } request)
                    {
                        lock (gate)
                            connection.Requests.Add(request);
                        string text = Encoding.ASCII.GetString(request), first = text[..text.IndexOf("\r\n", StringComparison.Ordinal)];
                        string[] words = first.Split(' ');
                        HostCheck.Equal(3, words.Length);
                        HostCheck.Equal("HTTP/1.1", words[2]);
                        if (eventOwned)
                        {
                            string signal = words[1].StartsWith("/__clros/signalrace", StringComparison.Ordinal) ? "v3" : words[0] == "POST" && words[1] == "/__clros/signalprobe" ? "v1" : "";
                            string churn = words[0] == "POST" && words[1] == "/__clros/churn" ? (churnStarted == 0 ? "v2" : "v1") : "";
                            foreach (var expected in new[]
                            {
                                (Name: "X-CLROS-Signal", Value: signal),
                                (Name: "X-CLROS-Churn", Value: churn)
                            }

                            )
                            {
                                string[] headers = text.Split("\r\n", StringSplitOptions.None).Where(line => line.StartsWith(expected.Name + ":", StringComparison.OrdinalIgnoreCase)).ToArray();
                                HostCheck.Equal(expected.Value == "" ? 0 : 1, headers.Length);
                                if (headers.Length == 1)
                                    HostCheck.Equal(expected.Value, headers[0][(expected.Name.Length + 1)..].Trim());
                            }
                        }

                        requestObserved.TrySetResult();
                        if (stall)
                            await Task.Delay(Timeout.Infinite, stop.Token);
                        if (partial)
                        {
                            byte[] response = "HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: 9\r\n\r\nA"u8.ToArray();
                            lock (gate)
                                connection.Responses.Add(response);
                            await stream.WriteAsync(response, stop.Token);
                            client.Client.Shutdown(SocketShutdown.Send);
                            return;
                        }

                        int status = 200;
                        string body;
                        bool phaseFour = false;
                        if (unsupported)
                        {
                            HostCheck.Equal("GET", words[0]);
                            HostCheck.Equal("/__clros/churn/status", words[1]);
                            status = 700;
                            body = "unsupported status body";
                        }
                        else if (words[0] == "POST")
                        {
                            HostCheck.True(text.Contains("Content-Length: 0\r\n", StringComparison.OrdinalIgnoreCase));
                            HostCheck.True(text.Contains(words[1] == "/__clros/churn" ? "X-CLROS-Churn: " + (eventOwned && churnStarted == 0 ? "v2" : "v1") + "\r\n" : "X-CLROS-Signal: " + (eventOwned && words[1].StartsWith("/__clros/signalrace", StringComparison.Ordinal) ? "v3" : "v1") + "\r\n", StringComparison.OrdinalIgnoreCase));
                            if (words[1] == "/__clros/churn")
                            {
                                int number;
                                lock (gate)
                                {
                                    number = ++churnStarted;
                                    if (startGate && number == 1)
                                        HostCheck.True(raceStarted && initialObserved && !released);
                                }

                                if (number == 1)
                                {
                                    try
                                    {
                                        HostCheck.Equal(0, await stream.ReadAsync(new byte[1], stop.Token));
                                    }
                                    catch (IOException)
                                    {
                                    }

                                    lock (gate)
                                        canceled = true;
                                    return;
                                }

                                HostCheck.Equal(2, number);
                                lock (gate)
                                    complete = true;
                                body = Complete;
                            }
                            else if (words[1] == "/__clros/signalrace")
                            {
                                bool repeat;
                                lock (gate)
                                {
                                    repeat = raceStarted;
                                    raceStarted = true;
                                }

                                if (repeat)
                                {
                                    status = 409;
                                    body = "CLROS_SIGNAL_RACE_BUSY";
                                }
                                else
                                {
                                    await recovery.Task.WaitAsync(stop.Token);
                                    body = startGate ? Success.Replace("pid=43", "pid=1", StringComparison.Ordinal) : Success;
                                    if (eventOwned)
                                        body = body.Replace("CLROS_SIGNAL_RACE_OK", "CLROS_SIGNAL_RACE_V3_OK", StringComparison.Ordinal);
                                }
                            }
                            else if (words[1] == "/__clros/signalrace/start")
                            {
                                lock (gate)
                                {
                                    HostCheck.True(startGate && raceStarted && initialObserved && churnStarted == 1 && !canceled && !released);
                                    startRequests++;
                                    released = true;
                                }

                                body = "CLROS_SIGNAL_RACE_START_REQUESTED";
                            }
                            else if (words[1] == "/__clros/signalrace/recover")
                            {
                                bool repeat;
                                lock (gate)
                                {
                                    repeat = recovered;
                                    recovered = true;
                                }

                                status = repeat ? 409 : 200;
                                body = repeat ? "CLROS_SIGNAL_RACE_RECOVERY_REFUSED" : "CLROS_SIGNAL_RACE_RECOVERY_REQUESTED";
                                recovery.TrySetResult();
                            }
                            else
                            {
                                HostCheck.Equal("/__clros/signalprobe", words[1]);
                                status = 409;
                                body = "CLROS_SIGNAL_PROBE_BUSY";
                            }
                        }
                        else
                        {
                            HostCheck.Equal("GET", words[0]);
                            lock (gate)
                            {
                                int active = churnStarted == 1 && !canceled ? 1 : 0, work = churnStarted == 0 ? 0 : complete ? 10 : (startGate ? released : raceStarted) ? 2 : 1;
                                int phase = recovered ? 7 : startGate && !released ? 2 : 4;
                                phaseFour = words[1] == "/__clros/signalrace/status" && raceStarted && phase == 4;
                                if (startGate && raceStarted && !released && words[1] == "/__clros/signalrace/status")
                                    initialObserved = true;
                                body = words[1] switch
                                {
                                    "/healthz" => Health,
                                    "/__clros/churn/status" => $"CLROS_CHURN_STATUS active={active} waiting={active} peakActive={(churnStarted == 0 ? 0 : 1)} started={churnStarted} completed={(complete ? 1 : 0)} canceled={(canceled ? 1 : 0)} failed=0 rejected=0 timers={work} workers={work} collections={work} heldBytes={active * 65536} peakHeldBytes={(churnStarted == 0 ? 0 : 65536)}",
                                    "/__clros/signalrace/status" => (eventOwned ? "CLROS_SIGNAL_RACE_V3_STATUS" : "CLROS_SIGNAL_RACE_STATUS") + (raceStarted ? $" phase={phase} pid={(startGate ? 1 : 43)} waiter=7 actor=8 callback={(phase == 2 ? 0 : 9)} recoveryRequested={(recovered ? 1 : 0)}" : " phase=0 pid=0 waiter=0 actor=0 callback=0 recoveryRequested=0"),
                                    _ => throw new InvalidDataException("Unexpected request: " + first)};
                            }
                        }

                        byte[] bytes = Encoding.ASCII.GetBytes($"HTTP/1.1 {status} {(status == 200 ? "OK" : status == 409 ? "Conflict" : "Unsupported")}\r\nContent-Type: text/plain\r\nContent-Length: {Encoding.ASCII.GetByteCount(body)}\r\n\r\n{body}");
                        lock (gate)
                            connection.Responses.Add(bytes);
                        await stream.WriteAsync(bytes.AsMemory(0, 17), stop.Token);
                        await stream.WriteAsync(bytes.AsMemory(17), stop.Token);
                        if (phaseFour)
                            Interlocked.Exchange(ref RecoveryArmed, null)?.Invoke();
                    }
                }
                catch (OperationCanceledException)when (stop.IsCancellationRequested)
                {
                }
                catch (IOException)when (stop.IsCancellationRequested)
                {
                }
                catch (ObjectDisposedException)when (stop.IsCancellationRequested)
                {
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            stop.Cancel();
            listener.Stop();
            lock (gate)
                foreach (var client in clients)
                    client.Dispose();
            await accepting.WaitAsync(TimeSpan.FromSeconds(10));
            Task[] pending;
            lock (gate)
                pending = handlers.ToArray();
            await Task.WhenAll(pending).WaitAsync(TimeSpan.FromSeconds(10));
            stop.Dispose();
        }

        private static async Task<byte[]?> Head(NetworkStream stream, CancellationToken token)
        {
            using MemoryStream bytes = new();
            byte[] one = new byte[1];
            while (bytes.Length < 8192)
            {
                int count = await stream.ReadAsync(one, token);
                if (count == 0)
                {
                    HostCheck.Equal(0L, bytes.Length);
                    return null;
                }

                bytes.WriteByte(one[0]);
                if (bytes.Length >= 4 && bytes.GetBuffer().AsSpan((int)bytes.Length - 4, 4).SequenceEqual("\r\n\r\n"u8))
                    return bytes.ToArray();
            }

            throw new InvalidDataException("Request header bound");
        }
    }
}
