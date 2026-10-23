using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using OS.Tools.QemuRunner;

namespace OS.Tooling;

/// <summary>One-use loopback stimuli for the explicit quota fixture. Owns two
/// single-connection HTTP/1.1 clients and two held sockets, all bounded and
/// disposed on return/failure. Copied application/HTTP values grant no kernel
/// quota, process, native, packet, cleanup, Completion or guest authority.</summary>
public static partial class NativeAotServiceQuotaPeer
{
    private const string Lifecycle = "/__clros/service/lifecycle/", Quota = "/__clros/service/quota";
    private const int ChunkBytes = 1_048_576, MaximumChunks = 64, StartupSeconds = 400, OperationalSeconds = 540;

    /// <summary>Quota child lifetime and copied quota elapsed-time ceiling in
    /// seconds. Separate runner/owner clocks retain their independent bounds.</summary>
    public const int MaximumPeerSeconds = 900;

    /// <summary>Copied original response text at a monotonic elapsed100ns tick.
    /// Status samples are independent observations, not atomic snapshots.</summary>
    public readonly record struct Sample(string Name, long ElapsedTicks, string Body)
    {
        internal int WireExchangeId { get; init; }
        internal int WireConnectionId { get; init; }
    }

    /// <summary>Supplied loopback values only. The four-connection limit follows
    /// owned client configuration, not a guest transport census. Original
    /// serial/application/syscall/packet/runner/native joins remain mandatory.</summary>
    public sealed record Observation(int Port, int ConnectionLimit, long HeldTicks, long PressureStartedTicks,
        long RetainedTicks, long HealthyTicks, long WorkerProgressTicks, long OverlapTicks,
        long PressureAbortedTicks, string PressureError, ulong RetainedBytes, ulong GcCollections,
        long WorkerCallsBefore, long WorkerCallsAfter, long WorkerIterationsBefore, long WorkerIterationsAfter,
        IReadOnlyList<Sample> Samples);

    private readonly record struct QuotaStatus(ulong Phase, ulong Attempts, ulong RetainedBytes, ulong GcCollections);
    private readonly record struct PressureAbort(long Ticks, string Error);

    /// <summary>Stimulate one already-listening v3-quota service. Startup is
    /// bounded400s to cover the real initial TCP ISN quiet interval. A separate
    /// operational540s starts after first health; later exchanges retain20s.
    /// The pressure POST is never retried; any response, phase2, premature I/O end, timeout
    /// or cancellation rejects. Only pressure I/O abort after measured overlap
    /// returns values. The helper's16s deliberate waits are not a wall-time bound.
    /// Finally disposes every socket/client and joins the owned pressure task.</summary>
    public static Task<Observation> RunAsync(int port, CancellationToken cancellation = default) =>
        RunCoreAsync(port, null, cancellation);

    /// <summary>Format at most eight copied exception nodes for failed host CLI
    /// diagnostics, including aggregate siblings. Omits messages, stacks and
    /// payloads; bounded tokens/output confer no transport or guest verdict.</summary>
    public static string DescribeFailure(Exception error)
    {
        ArgumentNullException.ThrowIfNull(error);
        var pending = new Queue<(Exception Error, int Parent)>(); pending.Enqueue((error, -1));
        var output = new StringBuilder(); int node = 0;
        static string Token(string? value, int maximum) => value is { Length: > 0 } && value.Length <= maximum &&
            value.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '.') ? value : "-";
        while (pending.Count > 0 && node < 8)
        {
            var (current, parent) = pending.Dequeue();
            string http = current is HttpRequestException request ? request.HttpRequestError.ToString() :
                current is HttpIOException input ? input.HttpRequestError.ToString() : "-";
            string socket = current is SocketException socketError ? socketError.SocketErrorCode.ToString() : "-";
            string native = current is SocketException nativeError ? nativeError.NativeErrorCode.ToString(CultureInfo.InvariantCulture) : "-";
            output.Append("QUOTA_PEER_ERROR node=").Append(node.ToString(CultureInfo.InvariantCulture))
                .Append(" parent=").Append(parent.ToString(CultureInfo.InvariantCulture))
                .Append(" type=").Append(Token(current.GetType().FullName, 160))
                .Append(" hresult=").Append(current.HResult.ToString("X8", CultureInfo.InvariantCulture))
                .Append(" http=").Append(Token(http, 48)).Append(" socket=").Append(Token(socket, 48))
                .Append(" native=").Append(native).Append(" reason=")
                .Append(current is ToolFailure failure ? Token(failure.Reason, 96) : "-").Append("\r\n");
            if (current is AggregateException aggregate)
            {
                // Queue only the bounded prefix; retain one overflow sentinel.
                foreach (var child in aggregate.InnerExceptions)
                { if (pending.Count >= 9) break; pending.Enqueue((child, node)); }
            }
            else if (current.InnerException is Exception inner) pending.Enqueue((inner, node));
            node++;
        }
        if (pending.Count > 0) output.Append("QUOTA_PEER_ERROR truncated=true\r\n");
        return output.ToString();
    }

    private static async Task<Observation> RunCoreAsync(int port, WireCapture? wire, CancellationToken cancellation)
    {
        Need(port is >= 1 and <= 65535, "port");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        var token = deadline.Token;
        using var control = Client(port, TimeSpan.FromSeconds(StartupSeconds), wire, "control");
        using var pressureClient = Client(port, null, wire, "pressure");
        var clock = Stopwatch.StartNew(); wire?.BindClock(clock); var samples = new List<Sample>();
        HeldClient? cancel = null, echo = null; Task<PressureAbort>? pressure = null;
        void Keep(string name, Sample sample)
        { wire?.BindSample(name, sample); samples.Add(sample with { Name = name }); }
        void Pending() => Need(pressure is { IsCompleted: false }, "pressure_ended_before_overlap");
        void Held()
        {
            Need(cancel is not null && echo is not null &&
                !cancel.Socket.Poll(0, SelectMode.SelectRead) && !echo.Socket.Poll(0, SelectMode.SelectRead), "held_connection_ended");
        }
        async Task<Sample> Get(string path) => await Text(control, "GET", path, clock, token, wire: wire);
        async Task<(Sample Sample, ServiceStatus Worker)> Worker(ServiceStatus? prior, bool advance)
        {
            for (;;)
            {
                if (pressure is not null) Pending();
                var sample = await Get("/__clros/service/status"); var value = ServiceStatus.Parse(Encoding.ASCII.GetBytes(sample.Body));
                if (prior is not null) value.RequireProgressFrom(prior);
                Need(value.Phase == 1 && value.Failure == 0, "worker_failed");
                if (pressure is not null) Pending();
                if (value.ManagedWorkerId > 0 && value.Calls > 0 && value.Iterations > 0 &&
                    // Before pressure, finish the common fixture's large
                    // partial/error controls and >32 replenished requests.
                    // Later overlap still requires fresh worker progress.
                    (pressure is not null || value.RandomRequests > 32 && value.RandomBytes > 4096) &&
                    (!advance || prior is not null && value.Calls > prior.Calls && value.Iterations > prior.Iterations)) return (sample, value);
                await Task.Delay(50, token);
            }
        }
        async Task<Sample> WaitHeld(bool both)
        {
            for (;;)
            {
                var sample = await Get(Lifecycle + "status"); var value = ReadLifecycle(sample.Body);
                Need(value["active"] <= (both ? 2UL : 1UL) && value["waiting"] <= 1 && value["waits"] <= 1 &&
                    value["read"] <= (both ? 64UL : 0UL), "held_status_bounds");
                if (HeldStatus(value, both)) return sample;
                await Task.Delay(50, token);
            }
        }
        try
        {
            using (var startup = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                startup.CancelAfter(TimeSpan.FromSeconds(StartupSeconds));
                for (;;)
                {
                    try
                    {
                        long remaining = StartupSeconds * 1000L - clock.ElapsedMilliseconds; Need(remaining > 0, "startup_deadline");
                        var health = await Text(control, "GET", "/healthz", clock, startup.Token, TimeSpan.FromMilliseconds(remaining), wire);
                        Need(health.Body == "CLROS_SERVICE_HEALTH_OK", "startup_health"); Keep("health.startup", health); break;
                    }
                    catch (Exception error) when (StartupIoAbort(error) && !startup.IsCancellationRequested)
                    { await Task.Delay(100, startup.Token); }
                }
            }
            deadline.CancelAfter(TimeSpan.FromSeconds(OperationalSeconds));
            var lifecycleInitial = await Get(Lifecycle + "status");
            Need(ReadLifecycle(lifecycleInitial.Body).Values.All(v => v == 0), "initial_lifecycle"); Keep("lifecycle.initial", lifecycleInitial);
            var quotaInitial = await Get(Quota + "/status"); var quota = ReadQuota(quotaInitial.Body);
            Need(quota == default, "initial_quota"); Keep("quota.initial", quotaInitial);
            var idle = await Get("/__clros/service/status"); var idleWorker = ServiceStatus.Parse(Encoding.ASCII.GetBytes(idle.Body));
            Need(idleWorker.Phase == 0 && idleWorker.Failure == 0 && idleWorker.ManagedWorkerId == 0 && idleWorker.Calls == 0 &&
                idleWorker.Iterations == 0 && idleWorker.RandomRequests == 0 && idleWorker.RandomBytes == 0 && idleWorker.ElapsedMilliseconds == 0, "initial_worker");
            Keep("worker.initial", idle);

            cancel = await OpenHeld(port, "GET", Lifecycle + "cancel", [], token, wire);
            var head = await CancelHead(cancel.Stream, clock, token); Keep("cancel.headers", head);
            Keep("lifecycle.cancel", await WaitHeld(false));
            byte[] prefix = new byte[64]; for (int i = 0; i < prefix.Length; i++) prefix[i] = unchecked((byte)(31 + i * 17));
            echo = await OpenHeld(port, "POST", Lifecycle + "echo", prefix, token, wire);
            var held = await WaitHeld(true); Keep("lifecycle.held", held); Held(); long heldTicks = held.ElapsedTicks;
            var started = await Text(control, "POST", "/__clros/service/start", clock, token, wire: wire);
            Need(started.Body == "CLROS_SERVICE_STARTED", "worker_start"); Keep("worker.start", started);
            var running = await Worker(idleWorker, false); Keep("worker.running", running.Sample);
            var advancing = await Worker(running.Worker, true); Keep("worker.prepressure", advancing.Sample); Held();
            Need(HeldStatus(ReadLifecycle((await Get(Lifecycle + "status")).Body), true), "held_before_pressure");
            var readyQuota = await Get(Quota + "/status");
            Need(ReadQuota(readyQuota.Body) == default, "quota_already_started"); Keep("quota.prepressure", readyQuota);

            long pressureStarted = clock.Elapsed.Ticks;
            pressure = Pressure(pressureClient, clock, token, wire);
            Sample retained;
            for (;;)
            {
                Pending(); Held(); var sample = await Get(Quota + "/status"); var next = ReadQuota(sample.Body);
                Progress(next, quota); quota = next; Pending();
                if (quota.Phase == 1 && quota.RetainedBytes >= ChunkBytes && quota.GcCollections > 0)
                { retained = sample; break; }
                await Task.Delay(50, token);
            }
            Keep("quota.retained", retained);
            var before = await Worker(advancing.Worker, false); Keep("worker.after_retained", before.Sample);
            Pending(); Held(); var healthy = await Get("/healthz");
            Need(healthy.Body == "CLROS_SERVICE_HEALTH_OK", "overlap_health"); Keep("health.overlap", healthy); Pending();
            var progressed = await Worker(before.Worker, true); Keep("worker.progress", progressed.Sample);
            Held(); Pending(); var heldAfter = await Get(Lifecycle + "status");
            Need(HeldStatus(ReadLifecycle(heldAfter.Body), true), "held_after_progress"); Keep("lifecycle.overlap", heldAfter);
            var after = await Get(Quota + "/status"); var afterQuota = ReadQuota(after.Body); Progress(afterQuota, quota);
            Need(afterQuota.Phase == 1 && afterQuota.RetainedBytes >= ChunkBytes && afterQuota.GcCollections > 0, "quota_overlap");
            quota = afterQuota; Keep("quota.overlap", after); Pending(); Held(); long overlap = clock.Elapsed.Ticks;
            Sample lastQuota = after;
            while (!pressure.IsCompleted)
            {
                try
                {
                    var sample = await Get(Quota + "/status"); var next = ReadQuota(sample.Body); Progress(next, quota);
                    Need(next.Phase == 1, "quota_stopped"); quota = next; lastQuota = sample;
                }
                catch (Exception error) when (TerminalIoAbort(error) && !token.IsCancellationRequested) { break; }
                if (!pressure.IsCompleted) await Task.Delay(50, token);
            }
            var aborted = await pressure; token.ThrowIfCancellationRequested();
            Need(heldTicks > 0 && pressureStarted >= heldTicks && retained.ElapsedTicks >= pressureStarted &&
                healthy.ElapsedTicks >= retained.ElapsedTicks && progressed.Sample.ElapsedTicks >= healthy.ElapsedTicks &&
                overlap >= progressed.Sample.ElapsedTicks && aborted.Ticks > overlap, "overlap_order");
            Keep("quota.last", lastQuota);
            return new(port, 4, heldTicks, pressureStarted, retained.ElapsedTicks, healthy.ElapsedTicks,
                progressed.Sample.ElapsedTicks, overlap, aborted.Ticks, aborted.Error, afterQuota.RetainedBytes,
                afterQuota.GcCollections, before.Worker.Calls, progressed.Worker.Calls, before.Worker.Iterations,
                progressed.Worker.Iterations, Array.AsReadOnly(samples.ToArray()));
        }
        finally
        {
            deadline.Cancel(); cancel?.Dispose(); echo?.Dispose(); pressureClient.Dispose(); control.Dispose();
            if (pressure is not null)
            {
                try { _ = await pressure.WaitAsync(TimeSpan.FromSeconds(5)); }
                catch (TimeoutException) { throw new ToolFailure("quota_peer_pressure_join_timeout"); }
                catch { /* The original rejection/cancellation remains the result after the owned task joined. */ }
            }
        }
    }

    private static HttpClient Client(int port, TimeSpan? connectTimeout = null, WireCapture? wire = null, string role = "control")
    {
        var handler = new SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = false, UseCookies = false,
            MaxConnectionsPerServer = 1, ConnectTimeout = connectTimeout ?? TimeSpan.FromSeconds(10), MaxResponseHeadersLength = 8 };
        if (wire is not null) handler.ConnectCallback = async (context, cancellation) =>
        {
            WireNeed(context.DnsEndPoint.Port == port && context.DnsEndPoint.Host == "127.0.0.1", "connect_target");
            return await wire.ConnectAsync(role, cancellation);
        };
        return new(handler, disposeHandler: true) { BaseAddress = new Uri("http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture)),
            Timeout = Timeout.InfiniteTimeSpan, DefaultRequestVersion = HttpVersion.Version11,
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact };
    }

    private static async Task<Sample> Text(HttpClient client, string method, string path, Stopwatch clock, CancellationToken token,
        TimeSpan? timeout = null, WireCapture? wire = null)
    {
        int exchange = wire?.BeginExchange("control", method, path) ?? 0;
        try
        {
            using var limit = CancellationTokenSource.CreateLinkedTokenSource(token); limit.CancelAfter(timeout ?? TimeSpan.FromSeconds(20));
            using var request = new HttpRequestMessage(new HttpMethod(method), path)
            { Version = HttpVersion.Version11, VersionPolicy = HttpVersionPolicy.RequestVersionExact };
            if (method == "POST") request.Content = new ByteArrayContent([]);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, limit.Token);
            Need(response.StatusCode == HttpStatusCode.OK && response.Version == HttpVersion.Version11 &&
                response.Headers.ConnectionClose != true && response.Content.Headers.ContentLength is not > 8192, "control_response");
            await using var input = await response.Content.ReadAsStreamAsync(limit.Token);
            byte[] bytes = new byte[8193]; int count = 0;
            for (int read; (read = await input.ReadAsync(bytes.AsMemory(count), limit.Token)) != 0;)
            { count += read; Need(count <= 8192, "control_body_bounds"); }
            Need(count > 0, "control_body");
            for (int i = 0; i < count; i++) Need(bytes[i] is >= 32 and <= 126, "control_body");
            string body = Encoding.ASCII.GetString(bytes, 0, count); long ended = clock.Elapsed.Ticks;
            wire?.EndExchange(exchange, "complete", 200, body, ended);
            return new(path, ended, body) { WireExchangeId = exchange };
        }
        catch (Exception error)
        { wire?.EndExchange(exchange, error is OperationCanceledException ? "canceled" : "failed", 0, "", clock.Elapsed.Ticks); throw; }
    }

    private static async Task<PressureAbort> Pressure(HttpClient client, Stopwatch clock, CancellationToken token, WireCapture? wire)
    {
        int exchange = wire?.BeginExchange("pressure", "POST", Quota) ?? 0; bool ended = false;
        using var request = new HttpRequestMessage(HttpMethod.Post, Quota)
        { Version = HttpVersion.Version11, VersionPolicy = HttpVersionPolicy.RequestVersionExact, Content = new ByteArrayContent([]) };
        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
            wire?.EndExchange(exchange, "response", (int)response.StatusCode, "", clock.Elapsed.Ticks); ended = true;
            throw new ToolFailure("quota_peer_pressure_http_response", ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture));
        }
        catch (Exception error) when (TerminalIoAbort(error) && !token.IsCancellationRequested)
        {
            string kind = PressureErrorKind(error);
            long at = clock.Elapsed.Ticks; wire?.EndExchange(exchange, "io-error", 0, "", at); ended = true;
            return new(at, kind);
        }
        finally
        { if (!ended) wire?.EndExchange(exchange, token.IsCancellationRequested ? "canceled" : "failed", 0, "", clock.Elapsed.Ticks); }
    }

    private sealed class HeldClient(TcpClient client, Stream stream) : IDisposable
    {
        internal Socket Socket => client.Client;
        internal Stream Stream => stream;
        public void Dispose() { try { stream.Dispose(); } finally { client.Dispose(); } }
    }

    private static async Task<HeldClient> OpenHeld(int port, string method, string path, byte[] body,
        CancellationToken token, WireCapture? wire)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(token); limit.CancelAfter(TimeSpan.FromSeconds(20));
        var client = new TcpClient(AddressFamily.InterNetwork) { NoDelay = true, ReceiveBufferSize = 4096 };
        Stream? observed = null; string role = method == "GET" ? "cancel" : "echo";
        int exchange = wire?.BeginExchange(role, method, path) ?? 0;
        try
        {
            await client.ConnectAsync(IPAddress.Loopback, port, limit.Token);
            observed = wire?.Wrap(client.Client, client.GetStream(), role) ?? client.GetStream();
            byte[] head = Encoding.ASCII.GetBytes(method + " " + path + " HTTP/1.1\r\nHost: clros.service\r\nConnection: close\r\n" +
                (method == "POST" ? "Content-Length: 32768\r\n" : "") + "\r\n");
            await observed.WriteAsync(head, limit.Token);
            if (body.Length != 0) await observed.WriteAsync(body, limit.Token);
            return new(client, observed); // The echo body deliberately remains incomplete; never send FIN here.
        }
        catch
        {
            observed?.Dispose(); client.Dispose();
            wire?.EndExchange(exchange, "failed", 0, "", wire.Now); throw;
        }
    }

    private static async Task<Sample> CancelHead(Stream stream, Stopwatch clock, CancellationToken token)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(token); limit.CancelAfter(TimeSpan.FromSeconds(20));
        byte[] bytes = new byte[8192]; int count = 0;
        do
        {
            Need(count < bytes.Length && await stream.ReadAsync(bytes.AsMemory(count, 1), limit.Token) == 1, "cancel_head"); count++;
        } while (count < 4 || !bytes.AsSpan(count - 4, 4).SequenceEqual("\r\n\r\n"u8));
        var head = NativeAotServicePeer.ResponseHead(bytes.AsSpan(0, count - 4));
        Need(head.Status == 200 && head.Length is 0 or -1, "cancel_response");
        return new("cancel.headers", clock.Elapsed.Ticks, Encoding.ASCII.GetString(bytes, 0, count))
        { WireConnectionId = (stream as WireStream)?.Id ?? 0 };
    }

    private static Dictionary<string, ulong> ReadLifecycle(string text)
    {
        var values = QemuTranscripts.ReadRecord(text, "CLROS_LIFECYCLE_STATUS", "active:dec", "waiting:dec", "waits:dec",
            "canceled:dec", "echoes:dec", "streams:dec", "rejected:dec", "read:dec", "written:dec");
        Need(values.Values.All(v => v <= 1_000_000_000) && values["canceled"] == 0 && values["echoes"] == 0 &&
            values["streams"] == 0 && values["rejected"] == 0 && values["written"] == 0, "lifecycle_completed_or_failed");
        return values;
    }
    private static bool HeldStatus(Dictionary<string, ulong> values, bool both) => values["active"] == (both ? 2UL : 1UL) &&
        values["waiting"] == 1 && values["waits"] == 1 && values["read"] == (both ? 64UL : 0UL);

    private static QuotaStatus ReadQuota(string text)
    {
        var value = QemuTranscripts.ReadRecord(text, "CLROS_QUOTA_STATUS", "phase:dec", "attempts:dec", "retainedBytes:dec", "gcCollections:dec");
        Need(value["phase"] <= 1 && value["attempts"] <= MaximumChunks && value["retainedBytes"] <= (ulong)MaximumChunks * ChunkBytes &&
            value["retainedBytes"] % ChunkBytes == 0 && value["gcCollections"] <= int.MaxValue, "quota_failed_or_bounds");
        return new(value["phase"], value["attempts"], value["retainedBytes"], value["gcCollections"]);
    }
    private static void Progress(QuotaStatus value, QuotaStatus prior) => Need(value.Phase >= prior.Phase && value.Attempts >= prior.Attempts &&
        value.RetainedBytes >= prior.RetainedBytes && value.GcCollections >= prior.GcCollections, "quota_regression");
    private static bool IoAbort(Exception error) => error is HttpRequestException { HttpRequestError: HttpRequestError.ConnectionError or HttpRequestError.ResponseEnded } or
        HttpIOException { HttpRequestError: HttpRequestError.ConnectionError or HttpRequestError.ResponseEnded } or
        IOException { InnerException: SocketException } or SocketException;
    private static bool WrappedConnectionReset(Exception error) => error is HttpRequestException
    {
        HttpRequestError: HttpRequestError.Unknown,
        InnerException: IOException { InnerException: SocketException { SocketErrorCode: SocketError.ConnectionReset } }
    };
    private static bool StartupIoAbort(Exception error) => IoAbort(error) || WrappedConnectionReset(error);
    private static bool TerminalIoAbort(Exception error) => IoAbort(error) || WrappedConnectionReset(error);
    private static string PressureErrorKind(Exception error)
    {
        Need(TerminalIoAbort(error), "pressure_error_kind");
        return WrappedConnectionReset(error) ? nameof(HttpRequestError.ConnectionError) :
            error is HttpRequestException request ? request.HttpRequestError.ToString() :
            error is HttpIOException input ? input.HttpRequestError.ToString() : nameof(HttpRequestError.ConnectionError);
    }

    /// <summary>Classify copied host exceptions using the actual initial-health
    /// or pressure predicate. Supplies no retry, deadline, socket or guest proof.</summary>
    public static bool IsTransportFailureForTests(Exception error, bool startup) => startup ? StartupIoAbort(error) : TerminalIoAbort(error);

    /// <summary>Normalize copied admitted terminal transport errors to the actual
    /// pressure report category. Other shapes throw; no I/O or guest proof follows.</summary>
    public static string PressureErrorKindForTests(Exception error) => PressureErrorKind(error);
    private static void Need(bool value, string reason) { if (!value) throw new ToolFailure("quota_peer_" + reason); }
}
