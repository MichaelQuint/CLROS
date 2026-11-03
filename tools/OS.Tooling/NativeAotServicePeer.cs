using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OS.Tools.QemuRunner;

namespace OS.Tooling;
/// <summary>Independent bounded HTTP stimulus for the dedicated service.
/// Owns its child beside one runner and joins both on cancellation/failure.
/// HTTP/application reports do not prove native execution or guest cleanup;
/// the complete source/native/serial/packet/lifecycle consumer remains required.</summary>

public static partial class NativeAotServicePeer
{
    private const string Schema = "clros.nativeaot.service.peer.v1";
    private const string StatusPath = "/__clros/service/status", StartPath = "/__clros/service/start", StopPath = "/__clros/service/stop";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    /// <summary>Reserve one loopback forward and own peer/runner children through
    /// confirmed completion. Wait only for the actual listening serial witness
    /// before stimulus. The callback must honor cancellation and confirm exit;
    /// missing/failed children retain fresh logs and cannot create Completion.</summary>

    public static async Task<Completion> RunAsync(string root, string peerLog, string serial, Func<int, CancellationToken, Task<ProcessResult>> runRunner, CancellationToken cancellation = default, bool lifecycle = false)
    {
        root = System.IO.Path.GetFullPath(root);
        serial = System.IO.Path.GetFullPath(serial, root);
        var execution = await NativeAotSocketPeerLifecycle.RunAsync(root, peerLog, runRunner, cancellation, token => WaitForListeningAsync(root, serial, token), serial, vectors: false, service: true, serviceLifecycle: lifecycle);
        return new(execution.Root, execution.Port, execution.Path, execution.Sha256, execution.Runner, execution.ReceiveGateSerial, lifecycle);
    }

    private static async Task WaitForListeningAsync(string root, string serial, CancellationToken cancellation)
    {
        AuthorityFiles.RequirePlainPath(root, System.IO.Path.GetDirectoryName(serial)!, "service_peer_serial_path");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(TimeSpan.FromSeconds(120));
        long offset = 0;
        string tail = "";
        byte[] buffer = new byte[8192];
        while (true)
        {
            deadline.Token.ThrowIfCancellationRequested();
            if (File.Exists(serial))
            {
                AuthorityFiles.RequirePlainPath(root, serial, "service_peer_serial_path");
                using var input = new FileStream(serial, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                Need(input.Length >= offset && input.Length <= 128 * 1024 * 1024, "serial_bounds");
                input.Position = offset;
                for (int count; (count = await input.ReadAsync(buffer, deadline.Token)) != 0;)
                {
                    offset += count;
                    tail += Encoding.ASCII.GetString(buffer, 0, count);
                    if (tail.Contains("\nCLROS_M16_014_LISTENING\r\n", StringComparison.Ordinal))
                        return;
                    if (tail.Length > 16384)
                        tail = tail[^8192..];
                }
            }

            await Task.Delay(100, deadline.Token);
        }
    }

    /// <summary>Run the fixed HTTP/1.1 loopback peer protocol. Ordinary exchanges
    /// wait for genuine server EOF before client FIN and copy at most8192 body
    /// bytes; v2 additionally awaits the separately bounded lifecycle helper.
    /// One startup health connection may wait through the real240-second
    /// ISN quiet period, bounded by the remaining400-second startup window;
    /// later exchanges retain20-second deadlines. Own TCP resources until return;
    /// failure throws and no successful report is written. Never returns random
    /// bytes or secrets. Explicit lifecycle mode awaits the bounded v2 protocol
    /// while the existing long-running worker advances, then emits a strict v2
    /// report. Output is untrusted telemetry until the final guest join.</summary>

    public static async Task RunGuestAsync(int port, TextWriter output, bool lifecycle = false)
    {
        Need(port is >= 1 and <= 65535, "port");
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(800));
        CancellationToken token = deadline.Token;
        var clock = Stopwatch.StartNew();
        List<Exchange> exchanges = [];
        async Task<Exchange> Send(string method, string path, bool startup = false)
        {
            long started = clock.ElapsedMilliseconds;
            long budget = startup ? 400_000 - started : 20_000;
            Need(budget > 0, "startup_deadline");
            var response = await ExchangeAsync(port, method, path, TimeSpan.FromMilliseconds(budget), token);
            return new(exchanges.Count, method, path, response.Status, started, clock.ElapsedMilliseconds, response.DateUtc.ToString("O", CultureInfo.InvariantCulture), Convert.ToBase64String(response.Body));
        }

        int attempts = 0;
        while (true)
        {
            Need(++attempts <= 400 && clock.ElapsedMilliseconds < 400_000, "startup_deadline");
            try
            {
                exchanges.Add(await Send("GET", "/healthz", startup: true));
                break;
            }
            catch (Exception error)when ((error is SocketException or IOException or OperationCanceledException) && !token.IsCancellationRequested)
            {
                await Task.Delay(1000, token);
            }
        }

        Require(exchanges[^1], "GET", "/healthz", 200, "CLROS_SERVICE_HEALTH_OK");
        exchanges.Add(await Send("GET", StatusPath));
        var prior = ServiceStatus.Parse(Body(exchanges[^1]));
        Need(Idle(prior), "initial_status");
        exchanges.Add(await Send("POST", StopPath));
        Require(exchanges[^1], "POST", StopPath, 409, "");
        exchanges.Add(await Send("POST", StartPath));
        Require(exchanges[^1], "POST", StartPath, 200, "CLROS_SERVICE_STARTED");
        long begun = exchanges[^1].CompletedMilliseconds;
        exchanges.Add(await Send("POST", StartPath));
        Require(exchanges[^1], "POST", StartPath, 409, "");
        exchanges.Add(await Send("POST", StopPath));
        Require(exchanges[^1], "POST", StopPath, 409, "");
        NativeAotServiceLifecyclePeer.Observation? lifecycleObservation = null;
        if (lifecycle)
            lifecycleObservation = await NativeAotServiceLifecyclePeer.RunAsync(port, token);
        for (int samples = 0;; samples++)
        {
            Need(samples < 65, "sample_bound");
            exchanges.Add(await Send("GET", StatusPath));
            var current = ServiceStatus.Parse(Body(exchanges[^1]));
            current.RequireProgressFrom(prior);
            Need(current.Phase == 1, "running_phase");
            prior = current;
            if (Ready(current) && clock.ElapsedMilliseconds - begun > 300_000)
                break;
            await Task.Delay(10_000, token);
        }

        exchanges.Add(await Send("GET", "/healthz"));
        Require(exchanges[^1], "GET", "/healthz", 200, "CLROS_SERVICE_HEALTH_OK");
        exchanges.Add(await Send("POST", StopPath));
        Require(exchanges[^1], "POST", StopPath, 200, "CLROS_SERVICE_STOPPED");
        string report = lifecycle ? JsonSerializer.Serialize(new { schema = "clros.nativeaot.service.peer.v2", port, durationMilliseconds = clock.ElapsedMilliseconds, connectAttempts = attempts, exchanges, lifecycle = lifecycleObservation }, JsonOptions) : JsonSerializer.Serialize(new { schema = Schema, port, durationMilliseconds = clock.ElapsedMilliseconds, connectAttempts = attempts, exchanges }, JsonOptions);
        _ = InspectOutput(report, lifecycle);
        await output.WriteLineAsync(report);
    }

    /// <summary>Real loopback transport seam with the ordinary20-second bound.
    /// The optional synchronous notification signals a complete bounded entity,
    /// before waiting for transport EOF; it cannot manufacture completion. Borrow
    /// cancellation and retain the task until it exits. No report/Completion is
    /// created, and application protocol/guest authority remain separate.</summary>

    public static Task<HttpResponseObservation> ExchangeForTestsAsync(int port, string method, string path, CancellationToken cancellation = default, Action? contentReceived = null) => ExchangeAsync(port, method, path, TimeSpan.FromSeconds(20), cancellation, contentReceived);
    private static async Task<HttpResponseObservation> ExchangeAsync(int port, string method, string path, TimeSpan budget, CancellationToken cancellation, Action? contentReceived = null)
    {
        Need(port is >= 1 and <= 65535 && ((method == "GET" && (path == "/healthz" || path == StatusPath)) || (method == "POST" && (path == StartPath || path == StopPath))), "http_request");
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        limit.CancelAfter(budget);
        using var client = new TcpClient(AddressFamily.InterNetwork)
        {
            NoDelay = true
        };
        await client.ConnectAsync(IPAddress.Loopback, port, limit.Token);
        await using var stream = client.GetStream();
        byte[] request = Encoding.ASCII.GetBytes(method + " " + path + " HTTP/1.1\r\nHost: clros.service\r\nConnection: close\r\n" + (method == "POST" ? "Content-Length: 0\r\n" : "") + "\r\n");
        await stream.WriteAsync(request, limit.Token);
        byte[] bytes = new byte[16385];
        int received = 0, headerBytes = 0, bodyBytes = 0, status = 0;
        byte[]? decoded = null;
        DateTimeOffset date = default;
        bool notified = false, chunked = false, entityComplete = false;
        for (;;)
        {
            int count = await stream.ReadAsync(bytes.AsMemory(received), limit.Token);
            if (count == 0)
            {
                Need(headerBytes != 0 && entityComplete, "http_truncated");
                break;
            }

            received += count;
            Need(received <= 16384, "http_response_bounds");
            if (headerBytes == 0)
            {
                int at = bytes.AsSpan(0, received).IndexOf("\r\n\r\n"u8);
                Need(at >= 0 ? at + 4 <= 8192 : received <= 8192, "http_header_bounds");
                if (at >= 0)
                {
                    headerBytes = at + 4;
                    (status, date, bodyBytes) = ResponseHead(bytes.AsSpan(0, at));
                    chunked = bodyBytes == -1;
                }
            }

            if (headerBytes == 0)
                continue;
            if (chunked)
                entityComplete = DecodeChunks(bytes.AsSpan(headerBytes, received - headerBytes), decoded ??= new byte[8192], out bodyBytes);
            else
            {
                Need(received - headerBytes <= bodyBytes, "http_trailing");
                entityComplete = received - headerBytes == bodyBytes;
            }

            if (!notified && entityComplete)
            {
                notified = true;
                contentReceived?.Invoke();
            }
        }

        client.Client.Shutdown(SocketShutdown.Send);
        return new(status, date, chunked ? decoded!.AsSpan(0, bodyBytes).ToArray() : bytes.AsSpan(headerBytes, bodyBytes).ToArray());
    }

    internal static (int Status, DateTimeOffset Date, int Length) ResponseHead(ReadOnlySpan<byte> bytes, int maximumBody = 8192, bool allowBadRequest = false)
    {
        Need(maximumBody is >= 0 and <= 524288, "http_body_limit");
        foreach (byte value in bytes)
            Need(value is 10 or 13 or >= 32 and <= 126, "http_headers");
        string[] rows = Encoding.ASCII.GetString(bytes).Split("\r\n", StringSplitOptions.None);
        Need(rows.Length is >= 2 and <= 64 && rows.All(row => row.Length is> 0 and <= 8192 && row.All(c => c is >= ' ' and <= '~')), "http_headers");
        int status = rows[0] switch
        {
            "HTTP/1.1 200 OK" => 200,
            "HTTP/1.1 409 Conflict" => 409,
            "HTTP/1.1 400 Bad Request" when allowBadRequest => 400,
            _ => 0
        };
        Dictionary<string, string> headers = new(StringComparer.OrdinalIgnoreCase);
        foreach (string row in rows.Skip(1))
        {
            int colon = row.IndexOf(':');
            Need(colon > 0 && colon + 2 < row.Length && row[colon + 1] == ' ' && row[..colon].All(c => char.IsAsciiLetterOrDigit(c) || c == '-') && headers.TryAdd(row[..colon], row[(colon + 2)..]), "http_headers");
        }

        string? text = headers.GetValueOrDefault("Content-Length"), dateText = headers.GetValueOrDefault("Date");
        bool lengthValid = int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int length);
        bool dateValid = DateTimeOffset.TryParseExact(dateText, "r", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date);
        bool chunked = headers.TryGetValue("Transfer-Encoding", out string? encoding);
        Need(status != 0 && headers.GetValueOrDefault("Connection") == "close" && !headers.ContainsKey("Trailer") && (chunked ? encoding == "chunked" && text is null : lengthValid && length >= 0 && length <= maximumBody && text == length.ToString(CultureInfo.InvariantCulture)) && dateValid && date.Offset == TimeSpan.Zero && dateText == date.ToString("r", CultureInfo.InvariantCulture), "http_headers");
        return (status, date, chunked ? -1 : length);
    }

    // Entity<=8192, header<=8192 and total wire<=16384. Chunk extensions,
    // trailers, extra encodings and bytes after the final empty chunk reject.
    private static bool DecodeChunks(ReadOnlySpan<byte> bytes, Span<byte> body, out int decoded)
    {
        decoded = 0;
        int offset = 0;
        for (;;)
        {
            int end = bytes[offset..].IndexOf("\r\n"u8);
            if (end < 0)
            {
                Need(bytes.Length - offset <= 8 || bytes.Length - offset == 9 && bytes[^1] == 13, "http_chunk_size");
                return false;
            }

            Need(end is> 0 and <= 8, "http_chunk_size");
            string hex = Encoding.ASCII.GetString(bytes.Slice(offset, end));
            bool valid = uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint size);
            Need(hex.All(char.IsAsciiHexDigit) && valid && size <= (uint)(body.Length - decoded), "http_chunk_size");
            offset += end + 2;
            if (bytes.Length - offset < size + 2)
                return false;
            Need(bytes.Slice(offset + (int)size, 2).SequenceEqual("\r\n"u8), "http_chunk_delimiter");
            if (size == 0)
            {
                Need(offset + 2 == bytes.Length, "http_trailing");
                return true;
            }

            bytes.Slice(offset, (int)size).CopyTo(body[decoded..]);
            decoded += (int)size;
            offset += (int)size + 2;
        }
    }

    /// <summary>Validate a complete bounded supplied report: fixed request
    /// order/status/bodies, real recorded peer duration, advancing successful
    /// application samples and strict stop thresholds. Unknown/duplicate fields,
    /// regressions and malformed JSON reject. Explicit lifecycle mode requires
    /// v2 and its validated observation; the default accepts only v1.
    /// Supplied text never creates an owned Completion or proves
    /// native/packet/guest cleanup.</summary>

    public static Inspection InspectOutput(string text, bool lifecycle = false)
    {
        Need(text.Length <= 65536 && Encoding.UTF8.GetByteCount(text) <= 65536, "report_bounds");
        try
        {
            using var document = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = 5 });
            var root = document.RootElement;
            if (lifecycle)
                Fields(root, "schema", "port", "durationMilliseconds", "connectAttempts", "exchanges", "lifecycle");
            else
                Fields(root, "schema", "port", "durationMilliseconds", "connectAttempts", "exchanges");
            Need(root.GetProperty("schema").GetString() == (lifecycle ? "clros.nativeaot.service.peer.v2" : Schema), "schema");
            long port = Number(root, "port"), duration = Number(root, "durationMilliseconds"), attempts = Number(root, "connectAttempts");
            Need(port is >= 1 and <= 65535 && duration is> 300_000 and < 800_000 && attempts is >= 1 and <= 400, "report_ranges");
            var observation = lifecycle ? ReadLifecycle(root.GetProperty("lifecycle"), (int)port) : null;
            var array = root.GetProperty("exchanges");
            Need(array.ValueKind == JsonValueKind.Array && array.GetArrayLength()is >= 10 and <= 73, "exchange_count");
            List<Exchange> exchanges = [];
            long previous = 0;
            DateTimeOffset priorDate = DateTimeOffset.MinValue;
            foreach (var row in array.EnumerateArray())
            {
                Fields(row, "index", "method", "path", "status", "startedMilliseconds", "completedMilliseconds", "dateUtc", "bodyBase64");
                long started = Number(row, "startedMilliseconds"), completed = Number(row, "completedMilliseconds"), status = Number(row, "status");
                string dateText = Text(row, "dateUtc");
                bool parsedDate = DateTimeOffset.TryParseExact(dateText, "O", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date);
                Need(Number(row, "index") == exchanges.Count && status is 200 or 409 && started >= previous && completed >= started && completed <= duration && parsedDate && date.Offset == TimeSpan.Zero && date >= priorDate, "exchange_metadata");
                priorDate = date;
                previous = completed;
                var exchange = new Exchange(exchanges.Count, Text(row, "method"), Text(row, "path"), (int)status, started, completed, dateText, Text(row, "bodyBase64"));
                _ = Body(exchange);
                exchanges.Add(exchange);
            }

            Require(exchanges[0], "GET", "/healthz", 200, "CLROS_SERVICE_HEALTH_OK");
            Need(exchanges[0].StartedMilliseconds >= (attempts - 1) * 1000 && exchanges[0].StartedMilliseconds < 400_000, "retry_window");
            Require(exchanges[1], "GET", StatusPath, 200);
            var prior = ServiceStatus.Parse(Body(exchanges[1]));
            Need(Idle(prior), "initial_status");
            Require(exchanges[2], "POST", StopPath, 409, "");
            Require(exchanges[3], "POST", StartPath, 200, "CLROS_SERVICE_STARTED");
            Require(exchanges[4], "POST", StartPath, 409, "");
            Require(exchanges[5], "POST", StopPath, 409, "");
            Need(exchanges[5].CompletedMilliseconds - exchanges[3].CompletedMilliseconds < 300_000, "early_stop_window");
            int samples = exchanges.Count - 8;
            for (int i = 6; i < exchanges.Count - 2; i++)
            {
                Require(exchanges[i], "GET", StatusPath, 200);
                var current = ServiceStatus.Parse(Body(exchanges[i]));
                current.RequireProgressFrom(prior);
                Need(current.Phase == 1, "running_phase");
                prior = current;
            }

            Need(samples >= 2 && Ready(prior) && exchanges[^3].CompletedMilliseconds - exchanges[3].CompletedMilliseconds > 300_000, "stop_thresholds");
            Require(exchanges[^2], "GET", "/healthz", 200, "CLROS_SERVICE_HEALTH_OK");
            Require(exchanges[^1], "POST", StopPath, 200, "CLROS_SERVICE_STOPPED");
            Need(DateTimeOffset.Parse(exchanges[^1].DateUtc, CultureInfo.InvariantCulture) - DateTimeOffset.Parse(exchanges[0].DateUtc, CultureInfo.InvariantCulture) >= TimeSpan.FromSeconds(300), "date_progress");
            return new((int)port, exchanges.Count, samples, duration, prior.Calls, prior.RandomBytes)
            {
                Lifecycle = observation
            };
        }
        catch (JsonException)
        {
            throw new ToolFailure("service_peer_json");
        }
        catch (InvalidOperationException)
        {
            throw new ToolFailure("service_peer_json_shape");
        }
    }

    private static NativeAotServiceLifecyclePeer.Observation ReadLifecycle(JsonElement value, int port)
    {
        Fields(value, "port", "sequentialConnections", "echoBytes", "echoHash", "streamBytes", "streamSha256", "resetRequests", "timeoutRequests", "holdStartedTicks", "healthyCompletedTicks", "holdReleasedTicks", "terminalStatus");
        int Integer(string name)
        {
            long number = Number(value, name);
            Need(number is >= int.MinValue and <= int.MaxValue, "lifecycle_number");
            return (int)number;
        }

        long hash = Number(value, "echoHash");
        Need(hash is >= 0 and <= uint.MaxValue, "lifecycle_number");
        var observation = new NativeAotServiceLifecyclePeer.Observation(Integer("port"), Integer("sequentialConnections"), Integer("echoBytes"), (uint)hash, Integer("streamBytes"), Text(value, "streamSha256"), Integer("resetRequests"), Integer("timeoutRequests"), Number(value, "holdStartedTicks"), Number(value, "healthyCompletedTicks"), Number(value, "holdReleasedTicks"), Text(value, "terminalStatus"));
        Need(observation.Port == port, "lifecycle_port");
        NativeAotServiceLifecyclePeer.Validate(observation);
        return observation;
    }

    private static bool Ready(ServiceStatus value) => value.Phase == 1 && value.Failure == 0 && value.ManagedWorkerId > 0 && value.ElapsedMilliseconds > 300_000 && value.Calls > 4096 && value.RandomBytes > 4096 && value.Iterations > 0;
    private static bool Idle(ServiceStatus value) => value.Phase == 0 && value.Failure == 0 && value.ManagedWorkerId == 0 && value.ElapsedMilliseconds == 0 && value.Calls == 0 && value.RandomRequests == 0 && value.RandomBytes == 0 && value.Iterations == 0;
    private static byte[] Body(Exchange exchange)
    {
        Need(exchange.BodyBase64.Length <= 10924, "body_bounds");
        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(exchange.BodyBase64);
        }
        catch (FormatException)
        {
            throw new ToolFailure("service_peer_body_encoding");
        }

        Need(bytes.Length <= 8192 && Convert.ToBase64String(bytes) == exchange.BodyBase64, "body_encoding");
        return bytes;
    }

    private static void Require(Exchange value, string method, string path, int status, string? body = null)
    {
        Need(value.Method == method && value.Path == path && value.Status == status && (body is null || Body(value).AsSpan().SequenceEqual(Encoding.UTF8.GetBytes(body))), "http_exchange");
    }

    private static long Number(JsonElement value, string name)
    {
        var field = value.GetProperty(name);
        Need(field.ValueKind == JsonValueKind.Number && field.TryGetInt64(out _), "number");
        return field.GetInt64();
    }

    private static string Text(JsonElement value, string name)
    {
        var field = value.GetProperty(name);
        Need(field.ValueKind == JsonValueKind.String, "text");
        return field.GetString()!;
    }

    private static void Fields(JsonElement value, params string[] names)
    {
        Need(value.ValueKind == JsonValueKind.Object, "object");
        var actual = value.EnumerateObject().Select(x => x.Name).ToArray();
        Need(actual.Length == names.Length && actual.Distinct(StringComparer.Ordinal).Count() == names.Length && actual.Order(StringComparer.Ordinal).SequenceEqual(names.Order(StringComparer.Ordinal)), "fields");
    }

    private static void Need(bool value, string reason)
    {
        if (!value)
            throw new ToolFailure("service_peer_" + reason);
    }
}
