using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;

namespace OS.Tooling;
public static partial class NativeAotServiceQuotaPeer
{
    internal sealed partial class WireCapture(int port, bool signalRace = false)
    {
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
        internal long Now
        {
            get
            {
                WireNeed(clock is not null, "clock");
                return clock.Elapsed.Ticks;
            }
        }

        internal void BindClock(Stopwatch value)
        {
            lock (sync)
            {
                WireNeed(clock is null && value.IsRunning && port is >= 1 and <= 65535, "clock");
                clock = value;
            }
        }

        internal async ValueTask<Stream> ConnectAsync(string role, CancellationToken cancellation)
        {
            lock (sync)
            {
                WireNeed(!stopping && WireRole(role, signalRace) && connections.Count + connecting < WireMaximumConnections && connections.Count(c => c.Value.DisposedTicks == 0) + connecting < 4, "connect_state");
                connecting++;
                if (++pending == 1)
                    idle = new(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            Socket? socket = null;
            try
            {
                socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
                {
                    NoDelay = true
                };
                await socket.ConnectAsync(IPAddress.Loopback, port, cancellation);
                return Wrap(socket, new NetworkStream(socket, ownsSocket: true), role);
            }
            catch
            {
                socket?.Dispose();
                throw;
            }
            finally
            {
                lock (sync)
                {
                    connecting--;
                    if (--pending == 0)
                        idle!.TrySetResult();
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
                    var local = (IPEndPoint)socket.LocalEndPoint!;
                    var remote = (IPEndPoint)socket.RemoteEndPoint!;
                    WireNeed(local.Address.Equals(IPAddress.Loopback) && remote.Address.Equals(IPAddress.Loopback) && remote.Port == port && local.Port > 0 && WireRole(role, signalRace) && connections.Count < WireMaximumConnections && connections.Count(c => c.Value.DisposedTicks == 0) < 4, "connection_bound");
                    WireNeed(!connections.Any(c => c.Value.DisposedTicks == 0 && c.Value.LocalPort == local.Port && c.Value.RemotePort == remote.Port), "connection_alias");
                    int id = connections.Count + 1;
                    connections.Add(new(new(id, role, local.Address.ToString(), local.Port, remote.Address.ToString(), remote.Port, Now, 0)));
                    var result = new WireStream(this, inner, id);
                    streams.Add(result);
                    return result;
                }
            }
            catch
            {
                inner.Dispose();
                throw;
            }
        }

        internal int BeginExchange(string role, string method, string path)
        {
            lock (sync)
            {
                WireNeed(!stopping && exchanges.Count < WireMaximumExchanges && WireRequest(role, method, path, signalRace) && !active.ContainsKey(role), "exchange_bound");
                var value = new Exchange(exchanges.Count + 1, role, method, path, Now);
                exchanges.Add(value);
                active.Add(role, value);
                return value.Id;
            }
        }

        internal void EndExchange(int id, string outcome, int status, string body, long ended)
        {
            WireNeed(!signalRace && body.Length <= 8192, "exchange_profile");
            EndExchangeCore(id, outcome, status, Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes(body)), ended);
        }

        internal void EndSignalRaceExchange(int id, string outcome, int status, byte[] body)
        {
            WireNeed(signalRace && body.Length <= 4096, "exchange_profile");
            EndExchangeCore(id, outcome, status, Convert.ToBase64String(body), Now);
        }

        private void EndExchangeCore(int id, string outcome, int status, string body, long ended)
        {
            lock (sync)
            {
                WireNeed(id > 0 && id <= exchanges.Count, "exchange_identity");
                var value = exchanges[id - 1];
                // Stream disposal can finish held requests before its setup
                // failure unwinds. Do not replace that real incomplete witness.
                if (value.Outcome == "incomplete")
                    return;
                WireNeed(value.Outcome == "pending" && ended >= value.Started, "exchange_end");
                value.Ended = ended;
                value.Outcome = outcome;
                value.Status = status;
                value.Body = body;
                active.Remove(value.Role);
            }
        }

        internal void BindSample(string name, Sample sample)
        {
            lock (sync)
            {
                WireNeed(samples.Count < ReportSampleNames.Length, "sample_bound");
                samples.Add(new(name, sample.ElapsedTicks, Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes(sample.Body)), sample.WireExchangeId, sample.WireConnectionId));
            }
        }

        internal object Begin(int id, bool write, int requested, ReadOnlySpan<byte> attempted = default)
        {
            lock (sync)
            {
                WireNeed(!stopping && id > 0 && id <= connections.Count && requested is >= 0 and <= WireMaximumOperationBytes && operations.Count < WireMaximumOperations, "operation_bound");
                var c = connections[id - 1];
                WireNeed(c.Value.DisposedTicks == 0 && !(write ? c.Writing || c.WriteUnknown : c.Reading || c.ReadUnknown), "operation_state");
                string data = "";
                int exchangeId = 0;
                if (write)
                {
                    WireNeed(attempted.Length == requested && rawBytes + reservedReads <= WireMaximumRawBytes - attempted.Length, "raw_bound");
                    data = Convert.ToBase64String(attempted);
                    rawBytes += attempted.Length;
                    c.Writing = true;
                    if (active.TryGetValue(c.Value.Role, out var exchange))
                    {
                        exchangeId = exchange.Id;
                        if (!exchange.Connections.Contains(id))
                            exchange.Connections.Add(id);
                    }
                }
                else
                {
                    // Reserve before the real read can consume bytes. Other
                    // directions cannot spend capacity promised to this read.
                    WireNeed(rawBytes + reservedReads <= WireMaximumRawBytes - requested, "raw_bound");
                    reservedReads += requested;
                    c.Reading = true;
                }

                var op = new Operation(operations.Count + 1, id, write, write ? c.Written : c.Read, requested, Now, data, exchangeId);
                operations.Add(op);
                if (++pending == 1)
                    idle = new(TaskCreationOptions.RunContinuationsAsynchronously);
                return op;
            }
        }

        internal void Finish(object operation, int transferred, ReadOnlySpan<byte> data, Exception? error)
        {
            lock (sync)
            {
                var op = (Operation)operation;
                var c = connections[op.Connection - 1];
                WireNeed(op.Outcome == "pending", "operation_end");
                op.Ended = Now;
                op.Outcome = error is OperationCanceledException ? "canceled" : error is not null ? "failed" : op.Write ? "complete" : op.Requested == 0 ? "zero" : transferred == 0 ? "eof" : "complete";
                op.Transferred = error is null ? transferred : -1;
                try
                {
                    if (op.Write)
                    {
                        c.Writing = false;
                        if (error is null)
                            c.Written += transferred;
                        else
                            c.WriteUnknown = true;
                    }
                    else
                    {
                        reservedReads -= op.Requested;
                        c.Reading = false;
                        if (error is null)
                        {
                            WireNeed(transferred >= 0 && transferred <= op.Requested && data.Length == transferred && rawBytes + reservedReads <= WireMaximumRawBytes - transferred, "raw_bound");
                            op.Data = Convert.ToBase64String(data);
                            rawBytes += transferred;
                            c.Read += transferred;
                        }
                        else
                            c.ReadUnknown = true;
                    }
                }
                finally
                {
                    if (--pending == 0)
                        idle!.TrySetResult();
                }
            }
        }

        internal void Closed(int id)
        {
            lock (sync)
            {
                var c = connections[id - 1];
                if (c.Value.DisposedTicks != 0)
                    return;
                long ended = Now;
                c.Value = c.Value with
                {
                    DisposedTicks = ended
                };
                if ((c.Value.Role is "cancel" or "echo") && active.TryGetValue(c.Value.Role, out var held))
                {
                    held.Ended = ended;
                    held.Outcome = "incomplete";
                    active.Remove(held.Role);
                }
            }
        }

        internal async Task StopAsync()
        {
            WireStream[] owned;
            lock (sync)
            {
                stopping = true;
                owned = streams.ToArray();
            }

            Exception? failure = null;
            foreach (var stream in owned)
                try
                {
                    stream.Dispose();
                }
                catch (Exception error)
                {
                    failure ??= error;
                }

            Task joined;
            lock (sync)
                joined = pending == 0 ? Task.CompletedTask : idle!.Task;
            try
            {
                await joined.WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (TimeoutException)
            {
                throw new ToolFailure("quota_wire_io_join_timeout");
            }

            if (failure is not null)
                throw new ToolFailure("quota_wire_stream_dispose", failure.GetType().Name);
            lock (sync)
            {
                long ended = Now;
                foreach (var exchange in active.Values)
                {
                    exchange.Ended = ended;
                    exchange.Outcome = "incomplete";
                }

                active.Clear();
            }
        }

        internal WireTrace Snapshot(string purpose)
        {
            lock (sync)
            {
                WireNeed(stopping && pending == 0 && connecting == 0 && reservedReads == 0 && connections.All(c => c.Value.DisposedTicks > 0), "snapshot_live");
                return new(port, purpose, Now, rawBytes, Array.AsReadOnly(connections.Select(c => c.Value).ToArray()), Array.AsReadOnly(exchanges.Select(e => e.Copy()).ToArray()), Array.AsReadOnly(operations.Select(o => o.Copy()).ToArray()), Array.AsReadOnly(samples.ToArray()));
            }
        }
    }
}
