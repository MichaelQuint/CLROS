using System.Buffers.Binary;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using OS.Net;

namespace OS.Tools.QemuRunner;

/// <summary>Bounded raw controls alongside the ordinary service HTTP peer.
/// Reserve three loopback ports for this owner's entire lifetime. One control
/// resets two pending handshakes on the same tuple; a second acknowledges two
/// zero-window probes with the first reopen deliberately lost; a third finishes
/// health while persist remains blocked. Original PCAP drives every transition.
/// Caller owns the frame filter, process, cancellation and final source/serial/
/// packet joins. Host observations alone grant no guest acceptance.</summary>
public sealed class ServiceSocketControls : IDisposable
{
    public readonly record struct PacketObservation(long Ordinal, ulong Microseconds, string Hex);
    public sealed record HttpExchange(int Port, DateTimeOffset StartedUtc, DateTimeOffset CompletedUtc, string RequestHex, string ResponseHex);
    public sealed record Report(string Schema, int PendingPort, uint FirstClientSequence, uint SecondClientSequence,
        long FirstSynAckOrdinal, long SecondSynAckOrdinal, long FirstProbeOrdinal, long SecondProbeOrdinal,
        HttpExchange Persist, HttpExchange Healthy, IReadOnlyList<PacketObservation> Observations);
    private sealed record Seen(long Ordinal, uint Sequence, uint Ack, byte Flags, byte[] Payload);
    private sealed class Flow(int port, uint sequence, string name)
    {
        internal readonly int Port = port; internal readonly uint Initial = sequence;
        internal readonly string Name = name;
        internal uint Next = unchecked(sequence +1), Server;
        internal int Received, Writes;
        internal readonly byte[] Response = new byte[8192];
        internal DateTimeOffset Started;
    }
    private const string Request = "GET /healthz HTTP/1.1\r\nHost: clros.service\r\nConnection: close\r\n\r\n";
    private readonly TcpListener[] reservations;
    private readonly CancellationTokenSource lifetime = new();
    private readonly object lifecycle = new();
    private Task<Report>? completion;
    private int started, disposed;

    public ServiceSocketControls()
    {
        List<TcpListener> acquired = [];
        try
        {
            for (int i = 0; i <3; i++)
            {
                var listener = new TcpListener(IPAddress.Loopback, 0); listener.Server.ExclusiveAddressUse = true;
                listener.Start(1); acquired.Add(listener);
            }
            reservations = acquired.ToArray();
        }
        catch { foreach (var listener in acquired) listener.Stop(); lifetime.Dispose(); throw; }
    }

    /// <summary>Run once against caller-owned original serial/PCAP and the
    /// exclusive service frame writer. Keep this owner (and its reserved ports)
    /// until the whole guest ends. Failure stops the writer; caller must confirm
    /// process termination. Await the returned task before accepting a report.</summary>
    public Task<Report> RunAsync(WebFrameControl frames, string serialPath, string capturePath, CancellationToken cancellation = default)
    {
        lock (lifecycle)
        {
            ObjectDisposedException.ThrowIf(disposed !=0, this);
            if (started !=0) throw new InvalidOperationException("Service controls already started.");
            started = 1; int Port(int index) => ((IPEndPoint)reservations[index].LocalEndpoint).Port;
            frames.SelectServiceClients(Port(0), Port(1), Port(2));
            return completion = RunCoreAsync(frames, Path.GetFullPath(serialPath), Path.GetFullPath(capturePath), Port(0), Port(1), Port(2), cancellation);
        }
    }

    private async Task<Report> RunCoreAsync(WebFrameControl frames, string serialPath, string capturePath,
        int pendingPort, int persistPort, int healthyPort, CancellationToken cancellation)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellation, lifetime.Token);
        limit.CancelAfter(TimeSpan.FromSeconds(700)); CancellationToken token = limit.Token;
        try
        {
            ulong peer = await WaitForNetworkAsync(serialPath, capturePath, token).ConfigureAwait(false);
            limit.CancelAfter(TimeSpan.FromSeconds(45));
            using var capture = new ServicePacketCapture(capturePath);
            List<PacketObservation> observed = []; Dictionary<int, Queue<Seen>> queued = new()
            { [pendingPort] = new(), [persistPort] = new(), [healthyPort] = new() };
            void Poll()
            {
                if (frames.Completion.IsCompleted) throw new IOException("Service frame writer ended during controls.");
                foreach (var item in capture.Poll())
                {
                    var bytes = item.Bytes.AsSpan();
                    if (bytes.Length <54 || BinaryPrimitives.ReadUInt16BigEndian(bytes[12..]) !=0x0800 || bytes[23] !=6) continue;
                    int ipLength = (bytes[14] &15) *4;
                    if (ipLength <20 || bytes.Length <14 +ipLength +20) continue;
                    bool fromGuest = BinaryPrimitives.ReadUInt32BigEndian(bytes[26..]) ==0x0a00020f;
                    int port = BinaryPrimitives.ReadUInt16BigEndian(bytes[(14 +ipLength +(fromGuest ? 2 : 0))..]);
                    if (!queued.ContainsKey(port)) continue;
                    Need(Ipv4Header.TryParse(bytes[14..], out var ip, out var segment) ==PacketParseStatus.Success && ip.Protocol ==6 &&
                        (ip.FlagsAndFragmentOffset &0x3fff) ==0 && ip.TimeToLive !=0 &&
                        ip.Source.Equals(fromGuest ? new Ipv4Address(10, 0, 2, 15) : new Ipv4Address(10, 0, 2, 2)) &&
                        ip.Destination.Equals(fromGuest ? new Ipv4Address(10, 0, 2, 2) : new Ipv4Address(10, 0, 2, 15)), "control IPv4");
                    Need(TcpHeader.TryParse(segment, out var tcp, out _, out var payload) ==PacketParseStatus.Success &&
                        Ipv4TransportChecksum.Validate(ip.Source, ip.Destination, 6, segment, out var checksum) ==Ipv4TransportChecksumStatus.Success && checksum.IsValid &&
                        (fromGuest ? tcp.SourcePort : tcp.DestinationPort) ==8080 && (tcp.Flags &0xe0) ==0, "control TCP");
                    Need(observed.Count <512, "control observation capacity"); observed.Add(new(item.Ordinal, item.Microseconds, Convert.ToHexString(bytes)));
                    if (fromGuest) queued[port].Enqueue(new(item.Ordinal, tcp.SequenceNumber, tcp.AcknowledgementNumber, tcp.Flags, payload.ToArray()));
                }
            }
            async Task<Seen> Next(int port)
            {
                for (;;)
                {
                    token.ThrowIfCancellationRequested(); Poll();
                    if (queued[port].TryDequeue(out var packet)) return packet;
                    await Task.Delay(25, token).ConfigureAwait(false);
                }
            }
            async Task Send(Flow flow, uint sequence, uint ack, byte flags, ushort window, string payload = "", string? tag = null, bool forward = true)
            {
                byte[] frame = ContinuingTcpScenario.Write(new(false, (ushort)flow.Port, sequence, ack, flags, window, payload), peer);
                await frames.InjectServiceFrameAsync(frame, tag ??flow.Name +"." +(++flow.Writes).ToString(CultureInfo.InvariantCulture), forward).WaitAsync(token).ConfigureAwait(false);
            }
            async Task<Seen> Syn(Flow flow, ushort window)
            {
                flow.Started = DateTimeOffset.UtcNow; await Send(flow, flow.Initial, 0, 2, window).ConfigureAwait(false);
                Seen reply = await Next(flow.Port).ConfigureAwait(false);
                Need(reply.Flags ==0x12 && reply.Ack ==flow.Next && reply.Payload.Length ==0, "control SYN ACK");
                flow.Server = unchecked(reply.Sequence +1); return reply;
            }
            async Task BeginHttp(Flow flow, ushort window)
            {
                await Syn(flow, window).ConfigureAwait(false); await Send(flow, flow.Next, flow.Server, 0x10, window).ConfigureAwait(false);
                await Send(flow, flow.Next, flow.Server, 0x18, window, Request).ConfigureAwait(false); flow.Next = unchecked(flow.Next +(uint)Request.Length);
            }
            void Copy(Flow flow, Seen packet)
            {
                uint relative = unchecked(packet.Sequence -flow.Server);
                Need(packet.Ack ==flow.Next && relative <=(uint)flow.Received && (ulong)relative +(uint)packet.Payload.Length <=(uint)flow.Response.Length, "control stream sequence");
                for (int i = 0; i <packet.Payload.Length; i++)
                {
                    int at = (int)relative +i; Need(at >=flow.Received || flow.Response[at] ==packet.Payload[i], "control retransmission");
                    flow.Response[at] = packet.Payload[i];
                }
                flow.Received = Math.Max(flow.Received, checked((int)relative +packet.Payload.Length));
            }
            async Task<Seen> Probe(Flow flow)
            {
                for (;;)
                {
                    Seen packet = await Next(flow.Port).ConfigureAwait(false);
                    Need(packet.Flags is 0x10 or 0x18 && packet.Ack ==flow.Next, "control persist flags");
                    if (packet.Payload.Length ==0) continue;
                    Need(packet.Payload.Length ==1 && packet.Sequence ==unchecked(flow.Server +(uint)flow.Received), "control persist probe");
                    Copy(flow, packet); return packet;
                }
            }
            async Task<HttpExchange> FinishHttp(Flow flow)
            {
                for (;;)
                {
                    Seen packet = await Next(flow.Port).ConfigureAwait(false);
                    Need(packet.Flags is 0x10 or 0x18 or 0x11 or 0x19 && packet.Ack ==flow.Next, "control HTTP flags");
                    if (packet.Payload.Length ==0 && (packet.Flags &1) ==0) continue;
                    Copy(flow, packet); bool fin = (packet.Flags &1) !=0;
                    Need(!fin || unchecked(packet.Sequence -flow.Server) +(uint)packet.Payload.Length ==flow.Received, "control FIN sequence");
                    await Send(flow, flow.Next, unchecked(flow.Server +(uint)flow.Received +(fin ? 1U : 0)), 0x10, 8192).ConfigureAwait(false);
                    if (fin) break;
                }
                uint serverEnd = unchecked(flow.Server +(uint)flow.Received +1);
                await Send(flow, flow.Next, serverEnd, 0x11, 8192).ConfigureAwait(false); flow.Next = unchecked(flow.Next +1);
                Seen closed = await Next(flow.Port).ConfigureAwait(false);
                Need(closed.Flags ==0x10 && closed.Sequence ==serverEnd && closed.Ack ==flow.Next && closed.Payload.Length ==0, "control final ACK");
                var exchange = new HttpExchange(flow.Port, flow.Started, DateTimeOffset.UtcNow, Convert.ToHexString(Encoding.ASCII.GetBytes(Request)),
                    Convert.ToHexString(flow.Response.AsSpan(0, flow.Received)));
                RequireHealth(exchange); return exchange;
            }
            var first = new Flow(pendingPort, 0xfffffff0, "pending.first"); Seen firstSyn = await Syn(first, 8192).ConfigureAwait(false);
            await Send(first, first.Next, 0, 4, 8192, tag: "pending.first.reset").ConfigureAwait(false);
            var second = new Flow(pendingPort, 0xfffff800, "pending.second"); Seen secondSyn = await Syn(second, 8192).ConfigureAwait(false);
            Need(firstSyn.Sequence !=secondSyn.Sequence, "control continuing ISN");
            await Send(second, second.Next, 0, 4, 8192, tag: "pending.second.reset").ConfigureAwait(false);
            var persist = new Flow(persistPort, 0xffffff00, "persist"); await BeginHttp(persist, 0).ConfigureAwait(false);
            Seen firstProbe = await Probe(persist).ConfigureAwait(false);
            await Send(persist, persist.Next, unchecked(persist.Server +1), 0x10, 0, tag: "persist.zero.ack").ConfigureAwait(false);
            await Send(persist, persist.Next, unchecked(persist.Server +1), 0x10, 8192, tag: "persist.reopen.lost", forward: false).ConfigureAwait(false);
            var healthy = new Flow(healthyPort, 0xfffffe00, "healthy"); await BeginHttp(healthy, 8192).ConfigureAwait(false);
            HttpExchange health = await FinishHttp(healthy).ConfigureAwait(false);
            Seen secondProbe = await Probe(persist).ConfigureAwait(false);
            await Send(persist, persist.Next, unchecked(persist.Server +2), 0x10, 8192, tag: "persist.reopen.fresh").ConfigureAwait(false);
            HttpExchange resumed = await FinishHttp(persist).ConfigureAwait(false); Poll();
            Need(queued.Values.All(queue => queue.Count ==0), "control unexpected trailing packet");
            return new("clros.nativeaot.service.controls.v1", pendingPort, first.Initial, second.Initial,
                firstSyn.Ordinal, secondSyn.Ordinal, firstProbe.Ordinal, secondProbe.Ordinal, resumed, health, Array.AsReadOnly(observed.ToArray()));
        }
        catch { frames.Stop(); throw; }
    }

    private static async Task<ulong> WaitForNetworkAsync(string serialPath, string capturePath, CancellationToken token)
    {
        long offset = 0, maximum = 0; ulong peer = 0; string tail = ""; byte[] bytes = new byte[8192];
        for (;;)
        {
            token.ThrowIfCancellationRequested();
            if (File.Exists(serialPath))
            {
                using var input = new FileStream(serialPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                Need(input.Length >=maximum && input.Length <=16 *1024 *1024, "control serial bounds"); maximum = input.Length; input.Position = offset;
                for (int count; (count = await input.ReadAsync(bytes, token).ConfigureAwait(false)) !=0;)
                {
                    offset +=count; tail +=Encoding.ASCII.GetString(bytes, 0, count); int end;
                    while ((end = tail.IndexOf("\r\n", StringComparison.Ordinal)) >=0)
                    {
                        string row = tail[..end]; tail = tail[(end +2)..];
                        if (row.StartsWith("M16_011_PEER mac=", StringComparison.Ordinal))
                        {
                            string value = row.Split(' ')[1][4..];
                            Need(peer ==0 && ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out peer) && peer is >0 and <=0xffffffffffff, "control peer MAC");
                        }
                        if (row.StartsWith("M16_011_SLOT event=0 ", StringComparison.Ordinal))
                        { Need(peer !=0 && File.Exists(capturePath), "control network gate"); return peer; }
                        Need(!row.StartsWith("TEST_FAIL ", StringComparison.Ordinal), "control guest failure");
                    }
                    Need(tail.Length <=16384, "control serial line");
                }
            }
            await Task.Delay(25, token).ConfigureAwait(false);
        }
    }

    private static void RequireHealth(HttpExchange exchange)
    {
        byte[] bytes = Convert.FromHexString(exchange.ResponseHex); int end = bytes.AsSpan().IndexOf("\r\n\r\n"u8);
        Need(end is >0 and <=4096 && bytes.AsSpan(end +4).SequenceEqual("CLROS_SERVICE_HEALTH_OK"u8), "control health body");
        string[] header = Encoding.ASCII.GetString(bytes, 0, end).Split("\r\n", StringSplitOptions.None);
        Need(header[0] =="HTTP/1.1 200 OK" && header.Contains("Connection: close") && header.Contains("Content-Length: " +"CLROS_SERVICE_HEALTH_OK".Length.ToString(CultureInfo.InvariantCulture)) &&
            header.Count(row => row.StartsWith("Date: ", StringComparison.Ordinal)) ==1, "control health header");
        string date = header.Single(row => row.StartsWith("Date: ", StringComparison.Ordinal))[6..];
        Need(DateTimeOffset.TryParseExact(date, "r", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed) &&
            parsed >=exchange.StartedUtc.AddSeconds(-5) && parsed <=exchange.CompletedUtc.AddSeconds(5), "control health Date");
    }

    private static void Need(bool condition, string reason) { if (!condition) throw new InvalidDataException(reason); }

    /// <summary>Cancel and join the control task before releasing reserved ports
    /// or its cancellation source. A two-second join timeout throws and retains
    /// ownership; await the task and retry disposal. Concurrent disposal rejects.</summary>
    public void Dispose()
    {
        Task<Report>? running;
        lock (lifecycle)
        {
            if (disposed ==2) return;
            if (disposed !=0) throw new InvalidOperationException("Service controls disposal in progress.");
            disposed = 1; running = completion;
        }
        try
        {
            lifetime.Cancel(); bool joined = running is null || running.IsCompleted;
            if (!joined) try { joined = running!.Wait(TimeSpan.FromSeconds(2)); } catch (AggregateException) { joined = running!.IsCompleted; }
            if (!joined) throw new TimeoutException("Service controls join incomplete; reservations retained.");
            foreach (var reservation in reservations) reservation.Stop(); lifetime.Dispose();
            lock (lifecycle) disposed = 2;
        }
        finally { lock (lifecycle) if (disposed !=2) disposed = 0; }
    }
}
