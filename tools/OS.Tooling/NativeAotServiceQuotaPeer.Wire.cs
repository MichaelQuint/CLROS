using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;

namespace OS.Tooling;

public static partial class NativeAotServiceQuotaPeer
{
    // The fixture permits400s/100ms startup retries and180s/50ms polls,
    // plus setup.8192 leaves a bound above those serialized application loops;
    // unexpected handler retries or fragmentation must fail rather than truncate.
    public const int WireMaximumConnections = 8192, WireMaximumExchanges = 8192,
        WireMaximumOperations = 65536, WireMaximumOperationBytes = 65536,
        WireMaximumRawBytes = 4 * 1024 * 1024, WireMaximumReportBytes = 32 * 1024 * 1024;

    /// <summary>Actual connected host endpoints, observed after connect. IDs are
    /// session-local; ports/IPs imply no guest NAT tuple or packet identity.
    /// DisposedTicks records local stream disposal, not peer/guest termination.</summary>
    public sealed record WireConnection(int Id, string Role, string LocalAddress, int LocalPort,
        string RemoteAddress, int RemotePort, long ConnectedTicks, long DisposedTicks);

    /// <summary>One logical HTTP attempt, including unselected polls. ConnectionIds
    /// name streams on which its actual writes were attempted; automatic retries
    /// may use multiple streams. Incomplete/error outcomes never imply HTTP EOF.</summary>
    public sealed record WireExchange(int Id, string Role, string Method, string Path, long StartedTicks,
        long EndedTicks, string Outcome, int Status, string BodyBase64, IReadOnlyList<int> ConnectionIds);

    /// <summary>One actual Stream call. Offset counts only earlier successful
    /// bytes in its direction. Failed/canceled calls report transferred=-1;
    /// write data is attempted bytes and may have been partly transmitted.
    /// Their direction becomes unknown and cannot accept another operation.
    /// A requested0 read is zero, distinct from EOF of a nonempty read.</summary>
    public sealed record WireOperation(int Id, int ConnectionId, string Direction, long Offset, int Requested,
        int Transferred, long StartedTicks, long EndedTicks, string Outcome, string DataBase64, int ExchangeId);

    /// <summary>Copied selected sample and its producer identity. Text responses
    /// bind a logical exchange; cancel headers bind the actual held stream.
    /// Body bytes are copied observations, not HTTP/kernel/packet authority.</summary>
    public sealed record WireSample(string Name, long ElapsedTicks, string BodyBase64, int ExchangeId, int ConnectionId);

    /// <summary>Immutable bounded raw host I/O values. All ticks are elapsed100ns
    /// from this session's Stopwatch, with no UTC or guest-clock correlation.
    /// Purpose distinguishes the full quota stimulus from a real stream control.
    /// This is not a Completion, PCAP, syscall, termination or guest verdict.</summary>
    public sealed record WireTrace(int Port, string Purpose, long SnapshotTicks, long RawBytes,
        IReadOnlyList<WireConnection> Connections, IReadOnlyList<WireExchange> Exchanges,
        IReadOnlyList<WireOperation> Operations, IReadOnlyList<WireSample> Samples);

    /// <summary>Existing checked service values plus independently copied raw
    /// host I/O. The existing CLI/owner still emits its original v1 report;
    /// wiring this explicit companion into an owned artifact remains separate.</summary>
    public sealed record ObservedRun(Observation Service, WireTrace Wire);

    /// <summary>Run the same bounded quota stimulus with actual serialized
    /// stream I/O observations. Preserves persistent clients, held sockets and
    /// four live connections. Owns/disposes every observing stream and joins
    /// its I/O before returning; overflow/failure throws without truncation.
    /// The synchronous callback receives one copied final snapshot after disposal/join,
    /// including peer failures, before the original exception is rethrown.
    /// A failed join yields no snapshot; callback/join errors are preserved with
    /// any original peer error. Callback must not block or start owned work.
    /// This launches no child/VM and supplies no original-capture authority.</summary>
    public static async Task<ObservedRun> RunObservedAsync(int port, CancellationToken cancellation = default,
        Action<WireTrace>? completedSnapshot = null)
    {
        WireNeed(port is >=1 and <=65535, "port");
        var wire = new WireCapture(port); Observation? service = null; Exception? failure = null;
        try { service = await RunCoreAsync(port, wire, cancellation); }
        catch (Exception error) { failure = error; }
        WireTrace trace;
        try
        {
            await wire.StopAsync();
            trace = wire.Snapshot(failure is null ? "quota-run" : "quota-partial");
            completedSnapshot?.Invoke(trace);
        }
        catch (Exception error)
        {
            if (failure is not null) throw new AggregateException(failure, error);
            throw;
        }
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        return new(service!, trace);
    }

    /// <summary>Real connected-loopback stream control only. The caller owns
    /// its TcpClient and callback task until return; this scope closes its actual
    /// NetworkStream and joins observed I/O even on cancellation. Callback errors
    /// propagate. No fake stream, process, HTTP or guest success is supplied.</summary>
    public static async Task<WireTrace> ObserveConnectedStreamForTestsAsync(TcpClient client,
        Func<Stream, CancellationToken, Task> exercise, CancellationToken cancellation = default,
        Action<WireTrace>? completedSnapshot = null)
    {
        ArgumentNullException.ThrowIfNull(client); ArgumentNullException.ThrowIfNull(exercise);
        WireNeed(client.Connected && client.Client.RemoteEndPoint is IPEndPoint, "test_connection");
        int port = ((IPEndPoint)client.Client.RemoteEndPoint!).Port;
        var wire = new WireCapture(port); wire.BindClock(Stopwatch.StartNew());
        try { await exercise(wire.Wrap(client.Client, client.GetStream(), "control"), cancellation); }
        finally { await wire.StopAsync(); completedSnapshot?.Invoke(wire.Snapshot("stream-control")); }
        return wire.Snapshot("stream-control");
    }

    private static void WireNeed([DoesNotReturnIf(false)] bool value, string reason)
    { if (!value) throw new ToolFailure("quota_wire_" + reason); }

    internal sealed class WireCapture(int port,bool signalRace=false)
    {
        private sealed class Connected(WireConnection value)
        {
            internal WireConnection Value = value;
            internal long Read, Written;
            internal bool ReadUnknown, WriteUnknown, Reading, Writing;
        }
        private sealed class Exchange(int id, string role, string method, string path, long started)
        {
            internal readonly int Id = id;
            internal readonly string Role = role, Method = method, Path = path;
            internal readonly long Started = started;
            internal long Ended;
            internal string Outcome = "pending", Body = "";
            internal int Status;
            internal readonly List<int> Connections = [];
            internal WireExchange Copy() => new(Id, Role, Method, Path, Started, Ended, Outcome, Status,
                Body, Array.AsReadOnly(Connections.ToArray()));
        }
        private sealed class Operation(int id, int connection, bool write, long offset, int requested, long started, string data, int exchange)
        {
            internal readonly int Id = id, Connection = connection, Requested = requested, ExchangeId = exchange;
            internal readonly bool Write = write;
            internal readonly long Offset = offset, Started = started;
            internal long Ended;
            internal int Transferred = -1;
            internal string Outcome = "pending", Data = data;
            internal WireOperation Copy() => new(Id, Connection, Write ? "write" : "read", Offset, Requested,
                Transferred, Started, Ended, Outcome, Data, ExchangeId);
        }
        private readonly object sync = new();
        private readonly List<Connected> connections = [];
        private readonly List<WireStream> streams = [];
        private readonly List<Exchange> exchanges = [];
        private readonly List<Operation> operations = [];
        private readonly List<WireSample> samples = [];
        private readonly Dictionary<string, Exchange> active = new(StringComparer.Ordinal);
        private Stopwatch? clock;
        private long rawBytes, reservedReads;
        private int pending, connecting;
        private bool stopping;
        private TaskCompletionSource? idle;
        internal long Now { get { WireNeed(clock is not null, "clock"); return clock.Elapsed.Ticks; } }

        internal void BindClock(Stopwatch value)
        { lock (sync) { WireNeed(clock is null && value.IsRunning && port is >=1 and <=65535, "clock"); clock = value; } }

        internal async ValueTask<Stream> ConnectAsync(string role, CancellationToken cancellation)
        {
            lock (sync)
            {
                WireNeed(!stopping && WireRole(role,signalRace) && connections.Count + connecting < WireMaximumConnections &&
                    connections.Count(c => c.Value.DisposedTicks == 0) + connecting < 4, "connect_state");
                connecting++;
                if (++pending == 1) idle = new(TaskCreationOptions.RunContinuationsAsynchronously);
            }
            Socket? socket = null;
            try
            {
                socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                await socket.ConnectAsync(IPAddress.Loopback, port, cancellation);
                return Wrap(socket, new NetworkStream(socket, ownsSocket: true), role);
            }
            catch { socket?.Dispose(); throw; }
            finally
            {
                lock (sync)
                {
                    connecting--;
                    if (--pending == 0) idle!.TrySetResult();
                }
            }
        }

        internal Stream Wrap(Socket socket, Stream inner, string role)
        {
            try
            {
                lock (sync)
                {
                    WireNeed(!stopping && socket.Connected && socket.LocalEndPoint is IPEndPoint && socket.RemoteEndPoint is IPEndPoint, "endpoints");
                    var local = (IPEndPoint)socket.LocalEndPoint!; var remote = (IPEndPoint)socket.RemoteEndPoint!;
                    WireNeed(local.Address.Equals(IPAddress.Loopback) && remote.Address.Equals(IPAddress.Loopback) &&
                        remote.Port == port && local.Port > 0 && WireRole(role,signalRace) && connections.Count < WireMaximumConnections &&
                        connections.Count(c => c.Value.DisposedTicks == 0) < 4, "connection_bound");
                    WireNeed(!connections.Any(c => c.Value.DisposedTicks == 0 && c.Value.LocalPort == local.Port &&
                        c.Value.RemotePort == remote.Port), "connection_alias");
                    int id = connections.Count + 1;
                    connections.Add(new(new(id, role, local.Address.ToString(), local.Port,
                        remote.Address.ToString(), remote.Port, Now, 0)));
                    var result = new WireStream(this, inner, id); streams.Add(result); return result;
                }
            }
            catch { inner.Dispose(); throw; }
        }

        internal int BeginExchange(string role, string method, string path)
        {
            lock (sync)
            {
                WireNeed(!stopping && exchanges.Count < WireMaximumExchanges && WireRequest(role, method, path,signalRace) &&
                    !active.ContainsKey(role), "exchange_bound");
                var value = new Exchange(exchanges.Count + 1, role, method, path, Now);
                exchanges.Add(value); active.Add(role, value); return value.Id;
            }
        }

        internal void EndExchange(int id, string outcome, int status, string body, long ended)
        {
            WireNeed(!signalRace&&body.Length<=8192,"exchange_profile");
            EndExchangeCore(id,outcome,status,Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes(body)),ended);
        }

        internal void EndSignalRaceExchange(int id,string outcome,int status,byte[] body)
        {
            WireNeed(signalRace&&body.Length<=4096,"exchange_profile");
            EndExchangeCore(id,outcome,status,Convert.ToBase64String(body),Now);
        }

        private void EndExchangeCore(int id,string outcome,int status,string body,long ended)
        {
            lock (sync)
            {
                WireNeed(id > 0 && id <= exchanges.Count, "exchange_identity");
                var value = exchanges[id - 1];
                // Stream disposal can finish held requests before its setup
                // failure unwinds. Do not replace that real incomplete witness.
                if (value.Outcome == "incomplete") return;
                WireNeed(value.Outcome == "pending" && ended >= value.Started, "exchange_end");
                value.Ended = ended; value.Outcome = outcome; value.Status = status;
                value.Body = body; active.Remove(value.Role);
            }
        }

        internal void BindSample(string name, Sample sample)
        {
            lock (sync)
            {
                WireNeed(samples.Count < ReportSampleNames.Length, "sample_bound");
                samples.Add(new(name, sample.ElapsedTicks, Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes(sample.Body)),
                    sample.WireExchangeId, sample.WireConnectionId));
            }
        }

        internal object Begin(int id, bool write, int requested, ReadOnlySpan<byte> attempted = default)
        {
            lock (sync)
            {
                WireNeed(!stopping && id > 0 && id <= connections.Count && requested is >=0 and <=WireMaximumOperationBytes &&
                    operations.Count < WireMaximumOperations, "operation_bound");
                var c = connections[id - 1];
                WireNeed(c.Value.DisposedTicks == 0 && !(write ? c.Writing || c.WriteUnknown : c.Reading || c.ReadUnknown), "operation_state");
                string data = ""; int exchangeId = 0;
                if (write)
                {
                    WireNeed(attempted.Length == requested && rawBytes + reservedReads <= WireMaximumRawBytes - attempted.Length, "raw_bound");
                    data = Convert.ToBase64String(attempted); rawBytes += attempted.Length; c.Writing = true;
                    if (active.TryGetValue(c.Value.Role, out var exchange))
                    { exchangeId = exchange.Id; if (!exchange.Connections.Contains(id)) exchange.Connections.Add(id); }
                }
                else
                {
                    // Reserve before the real read can consume bytes. Other
                    // directions cannot spend capacity promised to this read.
                    WireNeed(rawBytes + reservedReads <= WireMaximumRawBytes - requested, "raw_bound");
                    reservedReads += requested; c.Reading = true;
                }
                var op = new Operation(operations.Count + 1, id, write, write ? c.Written : c.Read, requested, Now, data, exchangeId);
                operations.Add(op);
                if (++pending == 1) idle = new(TaskCreationOptions.RunContinuationsAsynchronously);
                return op;
            }
        }

        internal void Finish(object operation, int transferred, ReadOnlySpan<byte> data, Exception? error)
        {
            lock (sync)
            {
                var op = (Operation)operation; var c = connections[op.Connection - 1];
                WireNeed(op.Outcome == "pending", "operation_end");
                op.Ended = Now;
                op.Outcome = error is OperationCanceledException ? "canceled" : error is not null ? "failed" :
                    op.Write ? "complete" : op.Requested == 0 ? "zero" : transferred == 0 ? "eof" : "complete";
                op.Transferred = error is null ? transferred : -1;
                try
                {
                    if (op.Write)
                    {
                        c.Writing = false;
                        if (error is null) c.Written += transferred; else c.WriteUnknown = true;
                    }
                    else
                    {
                        reservedReads -= op.Requested;
                        c.Reading = false;
                        if (error is null)
                        {
                            WireNeed(transferred >= 0 && transferred <= op.Requested && data.Length == transferred &&
                                rawBytes + reservedReads <= WireMaximumRawBytes - transferred, "raw_bound");
                            op.Data = Convert.ToBase64String(data); rawBytes += transferred; c.Read += transferred;
                        }
                        else c.ReadUnknown = true;
                    }
                }
                finally { if (--pending == 0) idle!.TrySetResult(); }
            }
        }

        internal void Closed(int id)
        {
            lock (sync)
            {
                var c = connections[id - 1]; if (c.Value.DisposedTicks != 0) return;
                long ended = Now; c.Value = c.Value with { DisposedTicks = ended };
                if ((c.Value.Role is "cancel" or "echo") && active.TryGetValue(c.Value.Role, out var held))
                { held.Ended = ended; held.Outcome = "incomplete"; active.Remove(held.Role); }
            }
        }

        internal async Task StopAsync()
        {
            WireStream[] owned;
            lock (sync) { stopping = true; owned = streams.ToArray(); }
            Exception? failure = null;
            foreach (var stream in owned)
                try { stream.Dispose(); } catch (Exception error) { failure ??= error; }
            Task joined; lock (sync) joined = pending == 0 ? Task.CompletedTask : idle!.Task;
            try { await joined.WaitAsync(TimeSpan.FromSeconds(5)); }
            catch (TimeoutException) { throw new ToolFailure("quota_wire_io_join_timeout"); }
            if (failure is not null) throw new ToolFailure("quota_wire_stream_dispose", failure.GetType().Name);
            lock (sync)
            {
                long ended = Now;
                foreach (var exchange in active.Values)
                { exchange.Ended = ended; exchange.Outcome = "incomplete"; }
                active.Clear();
            }
        }

        internal WireTrace Snapshot(string purpose)
        {
            lock (sync)
            {
                WireNeed(stopping && pending == 0 && connecting == 0 && reservedReads == 0 &&
                    connections.All(c => c.Value.DisposedTicks > 0), "snapshot_live");
                return new(port, purpose, Now, rawBytes, Array.AsReadOnly(connections.Select(c => c.Value).ToArray()),
                    Array.AsReadOnly(exchanges.Select(e => e.Copy()).ToArray()),
                    Array.AsReadOnly(operations.Select(o => o.Copy()).ToArray()), Array.AsReadOnly(samples.ToArray()));
            }
        }
    }

    private sealed class WireStream(WireCapture owner, Stream inner, int id) : Stream
    {
        private int disposed;
        internal int Id => id;
        public override bool CanRead => inner.CanRead;
        public override bool CanWrite => inner.CanWrite;
        public override bool CanSeek => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => inner.Flush();
        public override Task FlushAsync(CancellationToken cancellation) => inner.FlushAsync(cancellation);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));
        public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));
        public override int Read(Span<byte> buffer)
        {
            object op = owner.Begin(id, false, buffer.Length);
            int count;
            try { count = inner.Read(buffer); }
            catch (Exception error) { owner.Finish(op, -1, [], error); throw; }
            owner.Finish(op, count, buffer[..count], null); return count;
        }
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            object op = owner.Begin(id, true, buffer.Length, buffer);
            try { inner.Write(buffer); }
            catch (Exception error) { owner.Finish(op, -1, [], error); throw; }
            owner.Finish(op, buffer.Length, [], null);
        }
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellation) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellation).AsTask();
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellation) =>
            WriteAsync(buffer.AsMemory(offset, count), cancellation).AsTask();
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellation = default)
        {
            object op = owner.Begin(id, false, buffer.Length); int count;
            try { count = await inner.ReadAsync(buffer, cancellation); }
            catch (Exception error) { owner.Finish(op, -1, [], error); throw; }
            owner.Finish(op, count, buffer.Span[..count], null); return count;
        }
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellation = default)
        {
            object op = owner.Begin(id, true, buffer.Length, buffer.Span);
            try { await inner.WriteAsync(buffer, cancellation); }
            catch (Exception error) { owner.Finish(op, -1, [], error); throw; }
            owner.Finish(op, buffer.Length, [], null);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing && Interlocked.Exchange(ref disposed, 1) == 0)
            { try { inner.Dispose(); } finally { owner.Closed(id); } }
            base.Dispose(disposing);
        }
        public override ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
    }

    internal static string SignalRaceWireRole(string method,string path)=>method=="POST"&&path=="/__clros/churn"?"churn":
        method=="POST"&&path=="/__clros/signalrace"?"race":"control";
    private static bool WireRole(string role,bool signalRace=false) => signalRace?role is "control" or "churn" or "race":role is "control" or "pressure" or "cancel" or "echo";
    private static bool WireRequest(string role, string method, string path,bool signalRace=false) => signalRace?
        role==SignalRaceWireRole(method,path)&&(method=="GET"&&path is "/healthz" or "/__clros/churn/status" or "/__clros/signalrace/status"||
            method=="POST"&&path is "/__clros/churn" or "/__clros/signalrace" or "/__clros/signalrace/start" or "/__clros/signalrace/recover" or "/__clros/signalprobe") : role switch
    {
        "control" => method == "GET" && (path is "/healthz" or "/__clros/service/status" or "/__clros/service/lifecycle/status" or
            "/__clros/service/quota/status") || method == "POST" && path == "/__clros/service/start",
        "pressure" => method == "POST" && path == Quota,
        "cancel" => method == "GET" && path == Lifecycle + "cancel",
        "echo" => method == "POST" && path == Lifecycle + "echo",
        _ => false
    };
}
