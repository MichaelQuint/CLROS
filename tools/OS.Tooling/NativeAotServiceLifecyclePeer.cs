using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace OS.Tooling;
/// <summary>Explicit v2 loopback stimuli for the diagnostic service. Own each
/// TCP connection through EOF or deliberate reset and retain bounded bytes until
/// validation. Failures/cancellation throw; no guest/packet/native authority or
/// owned service Completion is created. Supplied observations remain untrusted
/// until joined to the independent original guest and packet consumers.</summary>

public static partial class NativeAotServiceLifecyclePeer
{
    private const string Prefix = "/__clros/service/lifecycle/";
    private const int EchoBytes = 32768, StreamBytes = 524288;
    private const int AggregateSeconds = 240;
    /// <summary>Run the fixed protocol against one already-listening explicit v2
    /// service. Uses a240-second aggregate bound,20-second normal exchanges and
    /// bounded status polling. A held response must remain active while another
    /// connection completes; reset/timeout must cause observed request drain.
    /// Tick fields use elapsed100ns TimeSpan ticks, not guest or wall-clock time.</summary>

    public static async Task<Observation> RunAsync(int port, CancellationToken cancellation = default)
    {
        Need(port is >= 1 and <= 65535, "port");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(TimeSpan.FromSeconds(AggregateSeconds));
        CancellationToken token = deadline.Token;
        var clock = Stopwatch.StartNew();
        async Task<string> Text(string path)
        {
            var response = await Exchange(port, "GET", path, [], token);
            Need(response.Status == 200, "get_status");
            return Encoding.ASCII.GetString(response.Body);
        }

        async Task<Dictionary<string, long>> Status() => ParseStatus(await Text(Prefix + "status"));
        async Task<Dictionary<string, long>> Until(Func<Dictionary<string, long>, bool> match)
        {
            for (int attempt = 0; attempt < 100; attempt++)
            {
                var status = await Status();
                if (match(status))
                    return status;
                await Task.Delay(50, token);
            }

            throw new ToolFailure("lifecycle_peer_status_progress");
        }

        var initial = await Status();
        Need(initial.Values.All(x => x == 0), "initial_status");
        for (int i = 0; i < 12; i++)
            Need(await Text("/healthz") == "CLROS_SERVICE_HEALTH_OK", "sequential_health");
        byte[] body = Pattern(EchoBytes);
        uint hash = Fnv(body);
        var echo = await Exchange(port, "POST", Prefix + "echo", body, token, partial: true);
        Need(echo.Status == 200 && Encoding.ASCII.GetString(echo.Body) == string.Create(CultureInfo.InvariantCulture, $"CLROS_LIFECYCLE_ECHO_OK bytes={EchoBytes} hash={hash:x8}"), "echo");
        body[31] ^= 1;
        var rejected = await Exchange(port, "POST", Prefix + "echo", body, token);
        Need(rejected.Status == 400 && rejected.Body.AsSpan().SequenceEqual("CLROS_LIFECYCLE_BAD_BODY"u8), "bad_body");
        Need(await Text("/healthz") == "CLROS_SERVICE_HEALTH_OK", "rejection_reuse");
        long held, healthy, released;
        byte[] streamed;
        using (var slow = await Open(port, "GET", Prefix + "stream", 0, token))
        {
            var head = await Head(slow.GetStream(), token);
            Need(head.Status == 200 && head.Length == StreamBytes, "stream_head");
            held = clock.Elapsed.Ticks;
            await Until(s => s["active"] == 1 && s["written"] < StreamBytes);
            Need(await Text("/healthz") == "CLROS_SERVICE_HEALTH_OK", "slow_peer_health");
            healthy = clock.Elapsed.Ticks;
            var pending = await Status();
            Need(pending["active"] == 1 && pending["streams"] == 0 && pending["written"] < StreamBytes, "slow_peer_not_pending");
            released = clock.Elapsed.Ticks;
            streamed = await Body(slow, StreamBytes, token);
        }

        Need(streamed.AsSpan().SequenceEqual(Pattern(StreamBytes)), "stream_body");
        for (int attempt = 1; attempt <= 2; attempt++)
        {
            using var waiting = await Open(port, "GET", Prefix + "cancel", 0, token);
            var head = await Head(waiting.GetStream(), token);
            Need(head.Status == 200 && head.Length is 0 or -1, "cancel_head");
            await Until(s => s["active"] == 1 && s["waiting"] == 1 && s["waits"] == attempt);
            if (attempt == 2)
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
                timeout.CancelAfter(100);
                bool expired = false;
                try
                {
                    _ = await waiting.GetStream().ReadAsync(new byte[1], timeout.Token);
                }
                catch (OperationCanceledException)when (!token.IsCancellationRequested)
                {
                    expired = true;
                }

                Need(expired, "request_timeout_not_observed");
            }

            waiting.LingerState = new(true, 0);
            waiting.Close();
            await Until(s => s["active"] == 0 && s["waiting"] == 0 && s["canceled"] == attempt);
            Need(await Text("/healthz") == "CLROS_SERVICE_HEALTH_OK", "cancellation_reuse");
        }

        string terminal = await Text(Prefix + "status");
        var result = new Observation(port, 12, EchoBytes, hash, streamed.Length, Convert.ToHexString(SHA256.HashData(streamed)), 1, 1, held, healthy, released, terminal);
        Validate(result);
        return result;
    }

    /// <summary>Validate only supplied value observations. This performs no I/O,
    /// creates no Completion and cannot establish transport or guest execution.</summary>

    public static void Validate(Observation value)
    {
        Need(value.Port is >= 1 and <= 65535 && value.SequentialConnections == 12 && value.EchoBytes == EchoBytes && value.EchoHash == Fnv(Pattern(EchoBytes)) && value.StreamBytes == StreamBytes && value.StreamSha256 == Convert.ToHexString(SHA256.HashData(Pattern(StreamBytes))) && value.ResetRequests == 1 && value.TimeoutRequests == 1 && value.HoldStartedTicks > 0 && value.HealthyCompletedTicks >= value.HoldStartedTicks && value.HoldReleasedTicks >= value.HealthyCompletedTicks && value.HoldReleasedTicks < TimeSpan.FromSeconds(AggregateSeconds).Ticks, "report");
        var status = ParseStatus(value.TerminalStatus);
        Need(status["active"] == 0 && status["waiting"] == 0 && status["waits"] == 2 && status["canceled"] == 2 && status["echoes"] == 1 && status["streams"] == 1 && status["rejected"] == 1 && status["read"] is >= EchoBytes and <= EchoBytes * 2 && status["written"] == StreamBytes, "terminal_status");
    }

    private static Dictionary<string, long> ParseStatus(string text)
    {
        Need(text is { Length: > 0 and <= 512 }, "status_bounds");
        string[] names = ["active", "waiting", "waits", "canceled", "echoes", "streams", "rejected", "read", "written"];
        string[] fields = text.Split(' ');
        Need(fields.Length == 10 && fields[0] == "CLROS_LIFECYCLE_STATUS", "status_shape");
        Dictionary<string, long> result = new(StringComparer.Ordinal);
        for (int i = 0; i < names.Length; i++)
        {
            string prefix = names[i] + "=";
            string number = fields[i + 1];
            Need(number.StartsWith(prefix, StringComparison.Ordinal), "status_key");
            number = number[prefix.Length..];
            bool valid = long.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out long value);
            Need(valid && value is >= 0 and <= 1_000_000_000 && number == value.ToString(CultureInfo.InvariantCulture), "status_number");
            result.Add(names[i], value);
        }

        return result;
    }

    private static async Task<TcpClient> Open(int port, string method, string path, int bodyBytes, CancellationToken token, bool partial = false)
    {
        var client = new TcpClient(AddressFamily.InterNetwork)
        {
            NoDelay = true,
            ReceiveBufferSize = 4096
        };
        try
        {
            await client.ConnectAsync(IPAddress.Loopback, port, token);
            byte[] head = Encoding.ASCII.GetBytes(method + " " + path + " HTTP/1.1\r\nHost: clros.service\r\nConnection: close\r\n" + (method == "POST" ? "Content-Length: " + bodyBytes.ToString(CultureInfo.InvariantCulture) + "\r\n" : "") + "\r\n");
            for (int offset = 0; offset < head.Length;)
            {
                int length = partial ? Math.Min(3, head.Length - offset) : head.Length;
                await client.GetStream().WriteAsync(head.AsMemory(offset, length), token);
                offset += length;
            }

            return client;
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    private static async Task<NativeAotServicePeer.HttpResponseObservation> Exchange(int port, string method, string path, byte[] body, CancellationToken token, bool partial = false)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(token);
        limit.CancelAfter(TimeSpan.FromSeconds(20));
        using var client = await Open(port, method, path, body.Length, limit.Token, partial);
        for (int offset = 0; offset < body.Length;)
        {
            int length = partial ? Math.Min(97, body.Length - offset) : body.Length;
            await client.GetStream().WriteAsync(body.AsMemory(offset, length), limit.Token);
            offset += length;
            if (partial)
                await Task.Delay(1, limit.Token);
        }

        var head = await Head(client.GetStream(), limit.Token);
        Need(head.Length is >= 0 and <= StreamBytes, "response_length");
        return new(head.Status, head.Date, await Body(client, head.Length, limit.Token));
    }

    private static async Task<(int Status, DateTimeOffset Date, int Length)> Head(NetworkStream stream, CancellationToken token)
    {
        byte[] bytes = new byte[8192];
        int count = 0;
        do
        {
            Need(count < bytes.Length && await stream.ReadAsync(bytes.AsMemory(count, 1), token) == 1, "response_head");
            count++;
        }
        while (count < 4 || !bytes.AsSpan(count - 4, 4).SequenceEqual("\r\n\r\n"u8));
        return NativeAotServicePeer.ResponseHead(bytes.AsSpan(0, count - 4), StreamBytes, allowBadRequest: true);
    }

    private static async Task<byte[]> Body(TcpClient client, int length, CancellationToken token)
    {
        byte[] bytes = new byte[length + 1];
        int count = 0;
        for (;;)
        {
            int got = await client.GetStream().ReadAsync(bytes.AsMemory(count, Math.Min(257, bytes.Length - count)), token);
            if (got == 0)
                break;
            count += got;
            Need(count <= length, "response_trailing");
        }

        Need(count == length, "response_truncated");
        client.Client.Shutdown(SocketShutdown.Send);
        return bytes.AsSpan(0, count).ToArray();
    }

    private static byte[] Pattern(int bytes)
    {
        byte[] result = new byte[bytes];
        for (int i = 0; i < bytes; i++)
            result[i] = unchecked((byte)(31 + i * 17));
        return result;
    }

    private static uint Fnv(byte[] bytes)
    {
        uint hash = 2166136261;
        foreach (byte value in bytes)
            hash = unchecked((hash ^ value) * 16777619);
        return hash;
    }

    private static void Need(bool value, string reason)
    {
        if (!value)
            throw new ToolFailure("lifecycle_peer_" + reason);
    }
}
