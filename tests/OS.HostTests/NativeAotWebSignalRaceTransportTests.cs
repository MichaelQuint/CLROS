using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OS.Tooling;
using OS.Tools.QemuRunner;

// Real loopback HTTP/stream controls, with independently supplied diagnostic
// bodies. They establish no native signal, guest flow, process or syscall proof.
internal static class NativeAotWebSignalRaceTransportTests
{
    private const string Health = "OS_MINIMAL_API_OK";
    private const string Complete = "CLROS_CHURN_OK iterations=8 bytes=65536 timers=8 workers=8 collections=8";
    private const string Success = "CLROS_SIGNAL_RACE_OK signal=12 pid=43 actor=8 waiter=7 callback=9 callbacks=1 actorJoined=1 waiterJoined=1 consumed=90 recovery=107 recoveryWriter=9 initialValue=-1 initialErrno=4 recoveryValue=1 recoveryEvents=1 recoveryTag=344978309701 handler=4096 flags=335544324 restorer=8192 actionMask=0 originalHandler=0 originalFlags=0 originalRestorer=0 originalActionMask=0 maskBefore=2048 maskDuring=0 maskAfter=2048 altAddress=0 altFlags=2 altBytes=0 descriptorsClosed=3 actionRestored=1 altUnchanged=1";
    private sealed class Connection(int port)
    {
        internal readonly int Port = port;
        internal readonly List<byte[]> Requests = [], Responses = [];
    }

    private sealed class Peer : IAsyncDisposable
    {
        private readonly bool partial, unsupported, startGate;
        private readonly TcpListener listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource stop = new(TimeSpan.FromSeconds(15));
        private readonly TaskCompletionSource recovery = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly object gate = new();
        private readonly List<TcpClient> clients = [];
        private readonly List<Task> handlers = [];
        internal readonly List<Connection> Connections = [];
        private readonly Task accepting;
        private int churnStarted,startRequests;
        private bool raceStarted, recovered, canceled, complete, released,initialObserved;
        internal int Port { get; }
        internal string Endpoint => "http://127.0.0.1:" + Port;
        internal int StartRequests { get { lock(gate)return startRequests; } }
        // Listener/handlers exist only when an explicitly selected test runs.
        internal Peer(bool partial = false, bool unsupported = false, bool startGate = false)
        {
            this.partial = partial; this.unsupported = unsupported; this.startGate=startGate; listener.Start();
            Port = ((IPEndPoint)listener.LocalEndpoint).Port; accepting = Accept();
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
                        Connections.Add(connection); handlers.Add(Serve(client, connection));
                    }
                }
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
            catch (SocketException) when (stop.IsCancellationRequested) { }
            catch (ObjectDisposedException) when (stop.IsCancellationRequested) { }
        }

        private async Task Serve(TcpClient client, Connection connection)
        {
            using (client)
            {
                try
                {
                    var stream = client.GetStream();
                    while (await Head(stream, stop.Token) is { } request)
                    {
                        lock (gate) connection.Requests.Add(request);
                        string text = Encoding.ASCII.GetString(request), first = text[..text.IndexOf("\r\n", StringComparison.Ordinal)];
                        string[] words = first.Split(' '); HostCheck.Equal(3, words.Length); HostCheck.Equal("HTTP/1.1", words[2]);
                        if (partial)
                        {
                            byte[] response = "HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: 9\r\n\r\nA"u8.ToArray();
                            lock (gate) connection.Responses.Add(response);
                            await stream.WriteAsync(response, stop.Token); client.Client.Shutdown(SocketShutdown.Send); return;
                        }
                        int status = 200; string body;
                        if (unsupported)
                        {
                            HostCheck.Equal("GET", words[0]); HostCheck.Equal("/__clros/churn/status", words[1]);
                            status = 700; body = "unsupported status body";
                        }
                        else if (words[0] == "POST")
                        {
                            HostCheck.True(text.Contains("Content-Length: 0\r\n", StringComparison.OrdinalIgnoreCase));
                            HostCheck.True(text.Contains(words[1] == "/__clros/churn" ? "X-CLROS-Churn: v1\r\n" : "X-CLROS-Signal: v1\r\n", StringComparison.OrdinalIgnoreCase));
                            if (words[1] == "/__clros/churn")
                            {
                                int number; lock (gate)
                                {
                                    number = ++churnStarted;
                                    if(startGate&&number==1)HostCheck.True(raceStarted&&initialObserved&&!released);
                                }
                                if (number == 1)
                                {
                                    try { HostCheck.Equal(0, await stream.ReadAsync(new byte[1], stop.Token)); }
                                    catch (IOException) { }
                                    lock (gate) canceled = true; return;
                                }
                                HostCheck.Equal(2, number); lock (gate) complete = true; body = Complete;
                            }
                            else if (words[1] == "/__clros/signalrace")
                            {
                                bool repeat; lock (gate) { repeat = raceStarted; raceStarted = true; }
                                if (repeat) { status = 409; body = "CLROS_SIGNAL_RACE_BUSY"; }
                                else { await recovery.Task.WaitAsync(stop.Token); body = startGate?Success.Replace("pid=43","pid=1",StringComparison.Ordinal):Success; }
                            }
                            else if(words[1]=="/__clros/signalrace/start")
                            {
                                lock(gate)
                                {
                                    HostCheck.True(startGate&&raceStarted&&initialObserved&&churnStarted==1&&!canceled&&!released);
                                    startRequests++;released=true;
                                }
                                body="CLROS_SIGNAL_RACE_START_REQUESTED";
                            }
                            else if (words[1] == "/__clros/signalrace/recover")
                            {
                                bool repeat; lock (gate) { repeat = recovered; recovered = true; }
                                status = repeat ? 409 : 200; body = repeat ? "CLROS_SIGNAL_RACE_RECOVERY_REFUSED" : "CLROS_SIGNAL_RACE_RECOVERY_REQUESTED";
                                recovery.TrySetResult();
                            }
                            else { HostCheck.Equal("/__clros/signalprobe", words[1]); status = 409; body = "CLROS_SIGNAL_PROBE_BUSY"; }
                        }
                        else
                        {
                            HostCheck.Equal("GET", words[0]);
                            lock (gate)
                            {
                                int active = churnStarted == 1 && !canceled ? 1 : 0, work = churnStarted == 0 ? 0 : complete ? 10 : (startGate?released:raceStarted) ? 2 : 1;
                                int phase=recovered?7:startGate&&!released?2:4;
                                if(startGate&&raceStarted&&!released&&words[1]=="/__clros/signalrace/status")initialObserved=true;
                                body = words[1] switch
                                {
                                    "/healthz" => Health,
                                    "/__clros/churn/status" => $"CLROS_CHURN_STATUS active={active} waiting={active} peakActive={(churnStarted == 0 ? 0 : 1)} started={churnStarted} completed={(complete ? 1 : 0)} canceled={(canceled ? 1 : 0)} failed=0 rejected=0 timers={work} workers={work} collections={work} heldBytes={active * 65536} peakHeldBytes={(churnStarted == 0 ? 0 : 65536)}",
                                    "/__clros/signalrace/status" => raceStarted ? $"CLROS_SIGNAL_RACE_STATUS phase={phase} pid={(startGate?1:43)} waiter=7 actor=8 callback={(phase==2?0:9)} recoveryRequested={(recovered ? 1 : 0)}" : "CLROS_SIGNAL_RACE_STATUS phase=0 pid=0 waiter=0 actor=0 callback=0 recoveryRequested=0",
                                    _ => throw new InvalidDataException("Unexpected request: " + first)
                                };
                            }
                        }
                        byte[] bytes = Encoding.ASCII.GetBytes($"HTTP/1.1 {status} {(status == 200 ? "OK" : status == 409 ? "Conflict" : "Unsupported")}\r\nContent-Type: text/plain\r\nContent-Length: {Encoding.ASCII.GetByteCount(body)}\r\n\r\n{body}");
                        lock (gate) connection.Responses.Add(bytes);
                        await stream.WriteAsync(bytes.AsMemory(0, 17), stop.Token); await stream.WriteAsync(bytes.AsMemory(17), stop.Token);
                    }
                }
                catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
                catch (IOException) when (stop.IsCancellationRequested) { }
                catch (ObjectDisposedException) when (stop.IsCancellationRequested) { }
            }
        }

        public async ValueTask DisposeAsync()
        {
            stop.Cancel(); listener.Stop();
            lock (gate) foreach (var client in clients) client.Dispose();
            await accepting.WaitAsync(TimeSpan.FromSeconds(10));
            Task[] pending; lock (gate) pending = handlers.ToArray();
            await Task.WhenAll(pending).WaitAsync(TimeSpan.FromSeconds(10)); stop.Dispose();
        }

        private static async Task<byte[]?> Head(NetworkStream stream, CancellationToken token)
        {
            using MemoryStream bytes = new(); byte[] one = new byte[1];
            while (bytes.Length < 8192)
            {
                int count = await stream.ReadAsync(one, token);
                if (count == 0) { HostCheck.Equal(0L, bytes.Length); return null; }
                bytes.WriteByte(one[0]);
                if (bytes.Length >= 4 && bytes.GetBuffer().AsSpan((int)bytes.Length - 4, 4).SequenceEqual("\r\n\r\n"u8)) return bytes.ToArray();
            }
            throw new InvalidDataException("Request header bound");
        }
    }

    private static string Output()
    {
        string directory = Path.GetFullPath(Path.Combine("artifacts", "host-tests", "signal-race-transport-" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(directory); return Path.Combine(directory, "observations.json");
    }

    private static NativeAotServiceQuotaPeer.WireTrace Wire(string path) =>
        NativeAotWebPayload.InspectSignalRaceWire(File.ReadAllBytes(path + ".wire.json"));
    private static byte[] Bytes(NativeAotServiceQuotaPeer.WireTrace trace, int connection, string direction) =>
        trace.Operations.Where(op => op.ConnectionId == connection && op.Direction == direction && op.Outcome == "complete")
            .SelectMany(op => Convert.FromBase64String(op.DataBase64)).ToArray();

    [HostTest("web race owner reaches actual loopback DONE with creator held reports then refuses failed runner")]
    private static async Task OwnedReportsBeforeDone()
    {
        using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using var peer = new Peer(startGate:true); string path = Output(), summary = path + ".summary",serial=path+".serial";
        string root = HostToolProcess.FindRepositoryRoot();
        // Literal copied provisional gate only. These rows drive the live
        // stimulus predicate; they are not a complete replay or guest evidence.
        const string initial="TEST_START m10.004\r\n"+
            "M16_008_CALL identity=4294967303 nr=186 a0=0 a1=0 a2=0 a3=0 a4=0 a5=0 rip=4194818 rsp=4296794048 flags=514 fs=4296796160 gs=0 clock=20 valueBits=7 action=1 status=1 ordinal=1 waitSource=0 waitDeadline=0 waitSequence=0 signalDepth=0 signalFrame=0 blockedSignals=0 returnRip=4194818 returnRsp=4296794048 returnFlags=514 returnFs=4296796160 returnGs=0 returnValueBits=7\r\n"+
            "M16_008_CALL identity=4294967303 nr=232 a0=10 a1=5244928 a2=1 a3=1 a4=0 a5=0 rip=4194818 rsp=4296794048 flags=514 fs=4296796160 gs=0 clock=20 valueBits=0 action=3 status=9 ordinal=2 waitSource=3 waitDeadline=2500000000 waitSequence=1 signalDepth=0 signalFrame=0 blockedSignals=0 returnRip=4194818 returnRsp=4296794048 returnFlags=514 returnFs=4296796160 returnGs=0 returnValueBits=0\r\n";
        byte[] serialBytes=Encoding.UTF8.GetBytes(initial);File.WriteAllBytes(serial,serialBytes);
        Task<ProcessResult>? callback = null; Task<NativeAotWebRacePeer.Completion>? owner = null;
        bool done = false, joined = false; NativeAotWebRacePeer.Completion? completion = null;
        Task<ProcessResult> Run(string name, CancellationToken token) => callback = RunCore(name, token);
        async Task<ProcessResult> RunCore(string name, CancellationToken token)
        {
            try
            {
                var report = await WebRaceRendezvous.WaitInRunnerAsync(name, peer.Port, Environment.ProcessId, TimeSpan.FromSeconds(10), cancellation: token);
                HostCheck.Equal(peer.Port, report.Port); HostCheck.Equal(Environment.ProcessId, report.QemuProcessId);
                string Hash(string file) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)));
                HostCheck.Equal(Hash(path), report.Digests.SelectedSha256);
                HostCheck.Equal(Hash(path + ".attempts.json"), report.Digests.AttemptsSha256);
                HostCheck.Equal(Hash(path + ".wire.json"), report.Digests.WireSha256);
                done = true;
                // Deliberate failure after genuine coordinated HTTP capture;
                // no successful runner, summary or Completion is fabricated.
                return new(1, "failed-after-done", "", false, false, false, TimeSpan.Zero);
            }
            finally { joined = true; }
        }
        try
        {
            owner = NativeAotWebRacePeer.RunOwnedAsync(root, peer.Port, Health, path, summary, Run, limit.Token,initialWaitSerial:serial);
            string? reason = null;
            try { completion = await owner; } catch (ToolFailure error) { reason = error.Reason; }
            HostCheck.Equal("web_race_owner_runner_failed", reason);
            HostCheck.True(completion is null && done && joined && callback is { IsCompleted: true } && owner.IsCompleted&&peer.StartRequests==1);
            HostCheck.True(File.ReadAllBytes(serial).SequenceEqual(serialBytes));
            HostCheck.Equal("failed-after-done", File.ReadAllText(path + ".runner.log"));
            HostCheck.Equal("", File.ReadAllText(path + ".runner.stderr.log")); HostCheck.True(!File.Exists(summary));
            foreach (string file in new[] { path, path + ".attempts.json", path + ".wire.json", path + ".runner.log", path + ".runner.stderr.log" })
            { using var closed = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.None); }
            HostCheck.Equal("signalrace-run", Wire(path).Purpose);
            using var selected=JsonDocument.Parse(File.ReadAllText(path));using var attempts=JsonDocument.Parse(File.ReadAllText(path+".attempts.json"));
            HostCheck.Equal("clros.web.signalrace.v2",selected.RootElement.GetProperty("schema").GetString());
            HostCheck.Equal("clros.web.signalrace.attempts.v2",attempts.RootElement.GetProperty("schema").GetString());
            HostCheck.Equal(new QemuTranscripts.WebSignalRaceSelection(1,7,8,9,9),
                NativeAotWebPayload.ValidateSignalRaceEvidence(File.ReadAllText(path),peer.Endpoint,Health));
            HostCheck.Equal(21, QemuTranscripts.InspectNativeAotWebSignalRaceHostHttp(File.ReadAllBytes(path + ".wire.json"),
                File.ReadAllText(path + ".attempts.json"), File.ReadAllText(path), peer.Endpoint, Health).Samples.Count);
        }
        finally
        {
            limit.Cancel();
            if (owner is not null) try { _ = await owner.WaitAsync(TimeSpan.FromSeconds(10)); } catch when (owner.IsCompleted) { }
            if (callback is not null) try { _ = await callback.WaitAsync(TimeSpan.FromSeconds(10)); } catch when (callback.IsCompleted) { }
        }
    }

    [HostTest("web race observed transport owns persistent role connections and exact real HTTP bytes")]
    private static async Task CompleteTransport()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(12));
        await using var peer = new Peer(); string path = Output();
        string selected = await NativeAotWebPayload.CaptureObservedSignalRaceAsync(peer.Port, Health, path, deadline.Token);
        HostCheck.Equal(selected, File.ReadAllText(path));
        var summary = NativeAotWebPayload.InspectSignalRaceAttempts(File.ReadAllText(path + ".attempts.json"), selected, peer.Endpoint, Health);
        HostCheck.True(summary.SemanticComplete && summary.Cancelled == 1 && summary.Faulted == 0);
        NativeAotWebPayload.ValidateSignalRaceTransport(File.ReadAllText(path + ".attempts.json"), selected, File.ReadAllBytes(path + ".wire.json"), peer.Endpoint, Health);
        var trace = Wire(path); HostCheck.Equal("signalrace-run", trace.Purpose); HostCheck.Equal(peer.Port, trace.Port);
        var framed = QemuTranscripts.InspectNativeAotWebSignalRaceHostHttp(File.ReadAllBytes(path + ".wire.json"), File.ReadAllText(path + ".attempts.json"), selected, peer.Endpoint, Health);
        HostCheck.Equal(19, framed.Samples.Count); HostCheck.True(framed.Samples[7] is { Sample: 7, MessageIndex: -1, HostCancellation: true });
        HostCheck.True(framed.Samples.Where(s => !s.HostCancellation).All(s => framed.Messages[s.MessageIndex].ExchangeId == s.ExchangeId && framed.Messages[s.MessageIndex].Response?.State == "complete"));
        HostCheck.Equal(summary.Attempts, trace.Exchanges.Count); HostCheck.Equal(0, trace.Samples.Count);
        HostCheck.True(trace.Connections.Count is >= 3 and <= 4 && trace.Connections.All(c => c.DisposedTicks > 0));
        HostCheck.True(trace.Connections.Select(c => c.Role).Distinct().Order().SequenceEqual(new[] { "churn", "control", "race" }));
        using var attempts = JsonDocument.Parse(File.ReadAllText(path + ".attempts.json"));
        for (int i = 0; i < trace.Exchanges.Count; i++)
        {
            var exchange = trace.Exchanges[i]; var row = attempts.RootElement.GetProperty("attempts")[i];
            HostCheck.Equal(i + 1, exchange.Id); HostCheck.Equal(row.GetProperty("method").GetString(), exchange.Method);
            HostCheck.Equal(row.GetProperty("path").GetString(), exchange.Path); HostCheck.Equal(row.GetProperty("outcome").GetString(), exchange.Outcome);
            HostCheck.Equal(row.GetProperty("status").GetInt32(), exchange.Status); HostCheck.Equal(row.GetProperty("bodyBase64").GetString(), exchange.BodyBase64);
        }
        foreach (var connection in trace.Connections)
        {
            var server = peer.Connections.Single(c => c.Port == connection.LocalPort);
            HostCheck.True(Bytes(trace, connection.Id, "write").SequenceEqual(server.Requests.SelectMany(bytes => bytes)));
            HostCheck.True(Bytes(trace, connection.Id, "read").SequenceEqual(server.Responses.SelectMany(bytes => bytes)));
        }
        HostCheck.True(trace.Operations.Where(op => op.Direction == "read").All(op => op.ExchangeId == 0));
        HostCheck.True(trace.Operations.Where(op => op.Direction == "write" && op.Requested > 0).All(op => op.ExchangeId > 0));
        HostCheck.True(trace.Exchanges.Count(e => e.Role == "churn" && e.Outcome == "cancelled") == 1);
    }

    [HostTest("web race observed truncated response retains actual partial bytes and joined failure")]
    private static async Task PartialTransport()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(12));
        await using var peer = new Peer(partial: true); string path = Output(); bool failed = false;
        try { await NativeAotWebPayload.CaptureObservedSignalRaceAsync(peer.Port, Health, path, deadline.Token); }
        catch (Exception error) when (error is IOException or HttpRequestException) { failed = true; }
        HostCheck.True(failed); var trace = Wire(path);
        HostCheck.Equal("signalrace-partial", trace.Purpose); HostCheck.Equal(1, trace.Connections.Count);
        HostCheck.Equal(1, trace.Exchanges.Count); HostCheck.Equal("faulted", trace.Exchanges[0].Outcome);
        HostCheck.Equal(200, trace.Exchanges[0].Status); HostCheck.Equal("QQ==", trace.Exchanges[0].BodyBase64);
        HostCheck.True(trace.Connections[0].DisposedTicks > 0);
        HostCheck.True(Bytes(trace, 1, "write").SequenceEqual(peer.Connections.Single().Requests.Single()));
        HostCheck.True(Bytes(trace, 1, "read").SequenceEqual(peer.Connections.Single().Responses.Single()));
        HostCheck.True(!NativeAotWebPayload.InspectSignalRaceAttempts(File.ReadAllText(path + ".attempts.json"), File.ReadAllText(path), peer.Endpoint, Health).SemanticComplete);
        var framed = QemuTranscripts.InspectNativeAotWebSignalRaceHostHttp(File.ReadAllBytes(path + ".wire.json"), File.ReadAllText(path + ".attempts.json"), File.ReadAllText(path), peer.Endpoint, Health);
        HostCheck.True(framed.Messages.Single().Response is { State: "body", BodyBase64: "QQ==" } && framed.Samples.Count == 0);
        NativeAotWebPayload.ValidateSignalRaceTransport(File.ReadAllText(path + ".attempts.json"), File.ReadAllText(path), File.ReadAllBytes(path + ".wire.json"), peer.Endpoint, Health);
    }

    [HostTest("web race observed already canceled admission retains partial trace with no connected I/O")]
    private static async Task AlreadyCanceled()
    {
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        await using var peer = new Peer(); string path = Output(); bool refused = false;
        try { await NativeAotWebPayload.CaptureObservedSignalRaceAsync(peer.Port, Health, path, canceled.Token); }
        catch (OperationCanceledException) { refused = true; }
        HostCheck.True(refused); var trace = Wire(path); HostCheck.Equal("signalrace-partial", trace.Purpose);
        HostCheck.Equal(0, trace.Connections.Count); HostCheck.Equal(0, trace.Operations.Count); HostCheck.Equal(0L, trace.RawBytes);
        HostCheck.Equal(0, peer.Connections.Count); HostCheck.Equal(1, trace.Exchanges.Count);
        HostCheck.Equal("cancelled", trace.Exchanges[0].Outcome);
        var framed = QemuTranscripts.InspectNativeAotWebSignalRaceHostHttp(File.ReadAllBytes(path + ".wire.json"), File.ReadAllText(path + ".attempts.json"), File.ReadAllText(path), peer.Endpoint, Health);
        HostCheck.True(framed.Messages.Count == 0 && framed.Samples.Count == 0);
        NativeAotWebPayload.ValidateSignalRaceTransport(File.ReadAllText(path + ".attempts.json"), File.ReadAllText(path), File.ReadAllBytes(path + ".wire.json"), peer.Endpoint, Health);
    }

    [HostTest("web race observed unsupported HTTP700 retains actual response bytes without promoting success")]
    private static async Task UnsupportedStatus()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(12));
        await using var peer = new Peer(unsupported: true); string path = Output(); string? failure = null;
        try { await NativeAotWebPayload.CaptureObservedSignalRaceAsync(peer.Port, Health, path, deadline.Token); }
        catch (InvalidDataException error) { failure = error.Message; }
        HostCheck.Equal("web_signalrace_churn_status", failure); var trace = Wire(path);
        HostCheck.Equal("signalrace-partial", trace.Purpose); HostCheck.Equal(1, trace.Connections.Count);
        HostCheck.Equal(1, trace.Exchanges.Count); HostCheck.Equal("response", trace.Exchanges[0].Outcome);
        HostCheck.Equal(700, trace.Exchanges[0].Status);
        HostCheck.Equal("unsupported status body", Encoding.ASCII.GetString(Convert.FromBase64String(trace.Exchanges[0].BodyBase64)));
        HostCheck.True(trace.Connections[0].DisposedTicks > 0);
        HostCheck.True(Bytes(trace, 1, "write").SequenceEqual(peer.Connections.Single().Requests.Single()));
        HostCheck.True(Bytes(trace, 1, "read").SequenceEqual(peer.Connections.Single().Responses.Single()));
        var summary = NativeAotWebPayload.InspectSignalRaceAttempts(File.ReadAllText(path + ".attempts.json"), File.ReadAllText(path), peer.Endpoint, Health);
        HostCheck.Equal(new NativeAotWebPayload.SignalRaceAttemptSummary(1, 1, 0, 0, false), summary);
        var framed = QemuTranscripts.InspectNativeAotWebSignalRaceHostHttp(File.ReadAllBytes(path + ".wire.json"), File.ReadAllText(path + ".attempts.json"), File.ReadAllText(path), peer.Endpoint, Health);
        HostCheck.True(framed.Messages.Single().Response is { State: "complete" } && framed.Samples.Count == 0);
        NativeAotWebPayload.ValidateSignalRaceTransport(File.ReadAllText(path + ".attempts.json"), File.ReadAllText(path), File.ReadAllBytes(path + ".wire.json"), peer.Endpoint, Health);
    }

    [HostTest("web race observed fresh wire collision refuses before connection and preserves original")]
    private static async Task WireCollision()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(12));
        await using var peer = new Peer(); string path = Output(); File.WriteAllText(path + ".wire.json", "retained original");
        bool refused = false;
        try { await NativeAotWebPayload.CaptureObservedSignalRaceAsync(peer.Port, Health, path, deadline.Token); }
        catch (IOException) { refused = true; }
        HostCheck.True(refused); HostCheck.Equal("retained original", File.ReadAllText(path + ".wire.json"));
        HostCheck.Equal(0, peer.Connections.Count);
    }
}
