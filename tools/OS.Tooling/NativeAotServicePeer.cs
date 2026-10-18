using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OS.Tools.QemuRunner;

namespace OS.Tooling;

/// <summary>Independent bounded HTTP stimulus for the dedicated service.
/// Owns its child beside one runner and joins both on cancellation/failure.
/// HTTP/application reports do not prove native execution or guest cleanup;
/// the complete source/native/serial/packet/lifecycle consumer remains required.</summary>
public static class NativeAotServicePeer
{
    private const string Schema = "clros.nativeaot.service.peer.v1";
    private const string StatusPath = "/__clros/service/status", StartPath = "/__clros/service/start", StopPath = "/__clros/service/stop";
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private sealed record Exchange(int Index, string Method, string Path, int Status, long StartedMilliseconds,
        long CompletedMilliseconds, string DateUtc, string BodyBase64);

    /// <summary>Value-only observations of supplied peer output, never execution authority.</summary>
    public readonly record struct Inspection(int Port, int Exchanges, int StatusSamples, long DurationMilliseconds, long Calls, long RandomBytes);

    /// <summary>Identity of this owned peer, serial path and exact runner result.
    /// Only the process-owning RunAsync factory constructs a completion.</summary>
    public sealed class Completion
    {
        internal Completion(string root, int port, string path, string hash, ProcessResult runner, string serial)
        { Root = root; Port = port; Path = path; Sha256 = hash; Runner = runner; SerialPath = serial; }
        internal string Root { get; }
        public int Port { get; }
        public string Path { get; }
        public string Sha256 { get; }
        public string SerialPath { get; }
        public ProcessResult Runner { get; }
        internal void Verify(AuthorityFiles files)
        {
            var file = files.Open(System.IO.Path.GetDirectoryName(Path)!, Path, "original service peer", "service_peer_log", 65536, singleLink: true);
            byte[] raw = file.ReadBytes(65536, "service_peer_log");
            Need(Convert.ToHexString(SHA256.HashData(raw)) == Sha256, "log_identity");
            Need(InspectOutput(new UTF8Encoding(false, true).GetString(raw)).Port == Port, "port_identity");
        }
    }

    /// <summary>Reserve one loopback forward and own peer/runner children through
    /// confirmed completion. Wait only for the actual listening serial witness
    /// before stimulus. The callback must honor cancellation and confirm exit;
    /// missing/failed children retain fresh logs and cannot create Completion.</summary>
    public static async Task<Completion> RunAsync(string root, string peerLog, string serial,
        Func<int, CancellationToken, Task<ProcessResult>> runRunner, CancellationToken cancellation = default)
    {
        root = System.IO.Path.GetFullPath(root); serial = System.IO.Path.GetFullPath(serial, root);
        var execution = await NativeAotSocketPeerLifecycle.RunAsync(root, peerLog, runRunner, cancellation,
            token => WaitForListeningAsync(root, serial, token), serial, vectors: false, service: true);
        return new(execution.Root, execution.Port, execution.Path, execution.Sha256, execution.Runner, execution.ReceiveGateSerial);
    }

    private static async Task WaitForListeningAsync(string root, string serial, CancellationToken cancellation)
    {
        AuthorityFiles.RequirePlainPath(root, System.IO.Path.GetDirectoryName(serial)!, "service_peer_serial_path");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation); deadline.CancelAfter(TimeSpan.FromSeconds(120));
        long offset = 0; string tail = ""; byte[] buffer = new byte[8192];
        while (true)
        {
            deadline.Token.ThrowIfCancellationRequested();
            if (File.Exists(serial))
            {
                AuthorityFiles.RequirePlainPath(root, serial, "service_peer_serial_path");
                using var input = new FileStream(serial, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                Need(input.Length >= offset && input.Length <= 128 * 1024 * 1024, "serial_bounds"); input.Position = offset;
                for (int count; (count = await input.ReadAsync(buffer, deadline.Token)) != 0;)
                {
                    offset += count; tail += Encoding.ASCII.GetString(buffer, 0, count);
                    if (tail.Contains("\nCLROS_M16_014_LISTENING\r\n", StringComparison.Ordinal)) return;
                    if (tail.Length > 16384) tail = tail[^8192..];
                }
            }
            await Task.Delay(100, deadline.Token);
        }
    }

    /// <summary>Run the fixed loopback peer protocol. All requests are HTTP/1.1,
    /// close their connection and decode at most8192 body bytes. Startup retries
    /// cover the real240-second ISN quiet period. Own HTTP resources until return;
    /// failure throws and no successful report is written. Never returns random
    /// bytes or secrets. Output is untrusted telemetry until the final guest join.</summary>
    public static async Task RunGuestAsync(int port, TextWriter output)
    {
        Need(port is >= 1 and <= 65535, "port");
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(800)); CancellationToken token = deadline.Token;
        using var client = new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false, UseProxy = false, UseCookies = false,
            ConnectTimeout = TimeSpan.FromSeconds(10), MaxConnectionsPerServer = 1, PooledConnectionLifetime = TimeSpan.Zero }) { Timeout = Timeout.InfiniteTimeSpan };
        var clock = Stopwatch.StartNew(); List<Exchange> exchanges = [];
        async Task<Exchange> Send(string method, string path)
        {
            using var request = new HttpRequestMessage(new HttpMethod(method), "http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture) + path)
                { Version = HttpVersion.Version11, VersionPolicy = HttpVersionPolicy.RequestVersionExact };
            request.Headers.ConnectionClose = true; request.Headers.Host = "clros.service";
            if (method == "POST") request.Content = new ByteArrayContent([]);
            using var limit = CancellationTokenSource.CreateLinkedTokenSource(token); limit.CancelAfter(TimeSpan.FromSeconds(20));
            long started = clock.ElapsedMilliseconds;
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, limit.Token);
            Need(response.Version == HttpVersion.Version11 && response.Headers.Date is not null && response.Content.Headers.ContentLength is not > 8192, "http_headers");
            await using var stream = await response.Content.ReadAsStreamAsync(limit.Token);
            byte[] body = new byte[8193]; int bytes = 0;
            for (int count; (count = await stream.ReadAsync(body.AsMemory(bytes), limit.Token)) != 0;)
            { bytes += count; Need(bytes <= 8192, "http_body_bounds"); }
            return new(exchanges.Count, method, path, (int)response.StatusCode, started, clock.ElapsedMilliseconds,
                response.Headers.Date!.Value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture), Convert.ToBase64String(body, 0, bytes));
        }
        int attempts = 0;
        while (true)
        {
            Need(++attempts <= 400 && clock.ElapsedMilliseconds < 400_000, "startup_deadline");
            try { exchanges.Add(await Send("GET", "/healthz")); break; }
            catch (Exception error) when ((error is HttpRequestException or IOException or OperationCanceledException) && !token.IsCancellationRequested)
            { await Task.Delay(1000, token); }
        }
        Require(exchanges[^1], "GET", "/healthz", 200, "CLROS_SERVICE_HEALTH_OK");
        exchanges.Add(await Send("GET", StatusPath)); var prior = ServiceStatus.Parse(Body(exchanges[^1])); Need(Idle(prior), "initial_status");
        exchanges.Add(await Send("POST", StopPath)); Require(exchanges[^1], "POST", StopPath, 409, "");
        exchanges.Add(await Send("POST", StartPath)); Require(exchanges[^1], "POST", StartPath, 200, "CLROS_SERVICE_STARTED");
        long begun = exchanges[^1].CompletedMilliseconds;
        exchanges.Add(await Send("POST", StartPath)); Require(exchanges[^1], "POST", StartPath, 409, "");
        exchanges.Add(await Send("POST", StopPath)); Require(exchanges[^1], "POST", StopPath, 409, "");
        for (int samples = 0; ; samples++)
        {
            Need(samples < 65, "sample_bound"); exchanges.Add(await Send("GET", StatusPath));
            var current = ServiceStatus.Parse(Body(exchanges[^1])); current.RequireProgressFrom(prior); Need(current.Phase == 1, "running_phase"); prior = current;
            if (Ready(current) && clock.ElapsedMilliseconds - begun > 300_000) break;
            await Task.Delay(10_000, token);
        }
        exchanges.Add(await Send("GET", "/healthz")); Require(exchanges[^1], "GET", "/healthz", 200, "CLROS_SERVICE_HEALTH_OK");
        exchanges.Add(await Send("POST", StopPath)); Require(exchanges[^1], "POST", StopPath, 200, "CLROS_SERVICE_STOPPED");
        string report = JsonSerializer.Serialize(new { schema = Schema, port, durationMilliseconds = clock.ElapsedMilliseconds, connectAttempts = attempts, exchanges }, JsonOptions);
        _ = InspectOutput(report); await output.WriteLineAsync(report);
    }

    /// <summary>Validate a complete bounded supplied report: fixed request
    /// order/status/bodies, real recorded peer duration, advancing successful
    /// application samples and strict stop thresholds. Unknown/duplicate fields,
    /// regressions and malformed JSON reject. Supplied text never creates an
    /// owned Completion or proves native/packet/guest cleanup.</summary>
    public static Inspection InspectOutput(string text)
    {
        Need(text.Length <= 65536 && Encoding.UTF8.GetByteCount(text) <= 65536, "report_bounds");
        try
        {
            using var document = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = 5 }); var root = document.RootElement;
            Fields(root, "schema", "port", "durationMilliseconds", "connectAttempts", "exchanges");
            Need(root.GetProperty("schema").GetString() == Schema, "schema");
            long port = Number(root, "port"), duration = Number(root, "durationMilliseconds"), attempts = Number(root, "connectAttempts");
            Need(port is >= 1 and <= 65535 && duration is > 300_000 and < 800_000 && attempts is >= 1 and <= 400, "report_ranges");
            var array = root.GetProperty("exchanges"); Need(array.ValueKind == JsonValueKind.Array && array.GetArrayLength() is >= 10 and <= 73, "exchange_count");
            List<Exchange> exchanges = []; long previous = 0; DateTimeOffset priorDate = DateTimeOffset.MinValue;
            foreach (var row in array.EnumerateArray())
            {
                Fields(row, "index", "method", "path", "status", "startedMilliseconds", "completedMilliseconds", "dateUtc", "bodyBase64");
                long started = Number(row, "startedMilliseconds"), completed = Number(row, "completedMilliseconds"), status = Number(row, "status");
                string dateText = Text(row, "dateUtc");
                bool parsedDate = DateTimeOffset.TryParseExact(dateText, "O", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date);
                Need(Number(row, "index") == exchanges.Count && status is 200 or 409 && started >= previous && completed >= started && completed <= duration &&
                    parsedDate && date.Offset == TimeSpan.Zero && date >= priorDate, "exchange_metadata");
                priorDate = date; previous = completed;
                var exchange = new Exchange(exchanges.Count, Text(row, "method"), Text(row, "path"), (int)status, started, completed, dateText, Text(row, "bodyBase64"));
                _ = Body(exchange); exchanges.Add(exchange);
            }
            Require(exchanges[0], "GET", "/healthz", 200, "CLROS_SERVICE_HEALTH_OK");
            Need(exchanges[0].StartedMilliseconds >= (attempts - 1) * 1000 && exchanges[0].StartedMilliseconds < 400_000, "retry_window");
            Require(exchanges[1], "GET", StatusPath, 200); var prior = ServiceStatus.Parse(Body(exchanges[1]));
            Need(Idle(prior), "initial_status");
            Require(exchanges[2], "POST", StopPath, 409, ""); Require(exchanges[3], "POST", StartPath, 200, "CLROS_SERVICE_STARTED");
            Require(exchanges[4], "POST", StartPath, 409, ""); Require(exchanges[5], "POST", StopPath, 409, "");
            Need(exchanges[5].CompletedMilliseconds - exchanges[3].CompletedMilliseconds < 300_000, "early_stop_window");
            int samples = exchanges.Count - 8;
            for (int i = 6; i < exchanges.Count - 2; i++)
            {
                Require(exchanges[i], "GET", StatusPath, 200); var current = ServiceStatus.Parse(Body(exchanges[i]));
                current.RequireProgressFrom(prior); Need(current.Phase == 1, "running_phase"); prior = current;
            }
            Need(samples >= 2 && Ready(prior) && exchanges[^3].CompletedMilliseconds - exchanges[3].CompletedMilliseconds > 300_000, "stop_thresholds");
            Require(exchanges[^2], "GET", "/healthz", 200, "CLROS_SERVICE_HEALTH_OK"); Require(exchanges[^1], "POST", StopPath, 200, "CLROS_SERVICE_STOPPED");
            Need(DateTimeOffset.Parse(exchanges[^1].DateUtc, CultureInfo.InvariantCulture) - DateTimeOffset.Parse(exchanges[0].DateUtc, CultureInfo.InvariantCulture) >= TimeSpan.FromSeconds(300), "date_progress");
            return new((int)port, exchanges.Count, samples, duration, prior.Calls, prior.RandomBytes);
        }
        catch (JsonException) { throw new ToolFailure("service_peer_json"); }
        catch (InvalidOperationException) { throw new ToolFailure("service_peer_json_shape"); }
    }

    private static bool Ready(ServiceStatus value) => value.Phase == 1 && value.Failure == 0 && value.ManagedWorkerId > 0 &&
        value.ElapsedMilliseconds > 300_000 && value.Calls > 4096 && value.RandomBytes > 4096 && value.Iterations > 0;
    private static bool Idle(ServiceStatus value) => value.Phase == 0 && value.Failure == 0 && value.ManagedWorkerId == 0 &&
        value.ElapsedMilliseconds == 0 && value.Calls == 0 && value.RandomRequests == 0 && value.RandomBytes == 0 && value.Iterations == 0;
    private static byte[] Body(Exchange exchange)
    {
        Need(exchange.BodyBase64.Length <= 10924, "body_bounds");
        byte[] bytes;
        try { bytes = Convert.FromBase64String(exchange.BodyBase64); } catch (FormatException) { throw new ToolFailure("service_peer_body_encoding"); }
        Need(bytes.Length <= 8192 && Convert.ToBase64String(bytes) == exchange.BodyBase64, "body_encoding"); return bytes;
    }
    private static void Require(Exchange value, string method, string path, int status, string? body = null)
    { Need(value.Method == method && value.Path == path && value.Status == status && (body is null || Body(value).AsSpan().SequenceEqual(Encoding.UTF8.GetBytes(body))), "http_exchange"); }
    private static long Number(JsonElement value, string name)
    { var field = value.GetProperty(name); Need(field.ValueKind == JsonValueKind.Number && field.TryGetInt64(out _), "number"); return field.GetInt64(); }
    private static string Text(JsonElement value, string name)
    { var field = value.GetProperty(name); Need(field.ValueKind == JsonValueKind.String, "text"); return field.GetString()!; }
    private static void Fields(JsonElement value, params string[] names)
    {
        Need(value.ValueKind == JsonValueKind.Object, "object"); var actual = value.EnumerateObject().Select(x => x.Name).ToArray();
        Need(actual.Length == names.Length && actual.Distinct(StringComparer.Ordinal).Count() == names.Length && actual.Order(StringComparer.Ordinal).SequenceEqual(names.Order(StringComparer.Ordinal)), "fields");
    }
    private static void Need(bool value, string reason) { if (!value) throw new ToolFailure("service_peer_" + reason); }
}
