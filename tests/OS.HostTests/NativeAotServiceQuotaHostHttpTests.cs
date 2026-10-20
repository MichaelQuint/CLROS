using System.Text;
using OS.Tooling;

internal static class NativeAotServiceQuotaHostHttpTests
{
    private const string Date = "Tue, 20 Oct 2026 12:00:00 GMT";
    private static string Request(string path, string role = "control", string method = "GET") =>
        method + " " + path + " HTTP/1.1\r\nHost: " + (role == "control" ? "127.0.0.1:8080" : "clros.service") + "\r\n" +
        (role == "control" ? "" : "Connection: close\r\n") + (method == "POST" ? "Content-Length: 32768\r\n" : "") + "\r\n";
    private static string Response(string body, bool chunks = false, bool close = false) =>
        "HTTP/1.1 200 OK\r\nServer: Kestrel\r\nDate: " + Date + "\r\n" + (close ? "Connection: close\r\n" : "") +
        (chunks ? "Transfer-Encoding: chunked\r\n\r\n" + body.Length.ToString("X") + "\r\n" + body + "\r\n0\r\n\r\n" :
            "Content-Length: " + body.Length + "\r\n\r\n" + body);
    private static string B64(string text) => Convert.ToBase64String(Encoding.ASCII.GetBytes(text));
    private sealed class Values
    {
        internal readonly List<NativeAotServiceQuotaPeer.WireConnection> Connections = [];
        internal readonly List<NativeAotServiceQuotaPeer.WireExchange> Exchanges = [];
        internal readonly List<NativeAotServiceQuotaPeer.WireOperation> Operations = [];
        internal readonly List<NativeAotServiceQuotaPeer.WireSample> Samples = [];
        private long tick = 1;
        internal int Connect(string role = "control")
        {
            int id = Connections.Count + 1;
            Connections.Add(new(id, role, "127.0.0.1", 40000 + id, "127.0.0.1", 8080, 1, 1000)); return id;
        }
        internal void Operation(int connection, string direction, int exchange, string data, string outcome = "complete")
        {
            long offset = Operations.Where(o => o.ConnectionId == connection && o.Direction == direction && o.Transferred >= 0).Sum(o => (long)o.Transferred);
            Operations.Add(new(Operations.Count + 1, connection, direction, offset, outcome == "failed" && direction == "read" ? 8192 : data.Length,
                outcome == "failed" ? -1 : data.Length, ++tick, ++tick, outcome, B64(data), direction == "write" ? exchange : 0));
        }
        internal int Exchange(int connection, string path, string body, string? response = null, string role = "control", string method = "GET", string? request = null)
        {
            int id = Exchanges.Count + 1; long start = ++tick;
            Operation(connection, "write", id, request ?? Request(path, role, method));
            if (response is null) response = Response(body);
            if (response.Length > 0)
            { Operation(connection, "read", id, response[..^2]); Operation(connection, "read", id, response[^2..]); }
            Exchanges.Add(new(id, role, method, path, start, ++tick, role == "control" ? "complete" : "incomplete", role == "control" ? 200 : 0,
                role == "control" ? B64(body) : "", new[] { connection })); return id;
        }
        internal void Sample(string name, int exchange)
        { var e = Exchanges[exchange - 1]; Samples.Add(new(name, e.EndedTicks, e.BodyBase64, exchange, 0)); }
        internal NativeAotServiceQuotaPeer.WireTrace Trace() => new(8080, "quota-partial", 1001,
            Operations.Sum(o => (long)Convert.FromBase64String(o.DataBase64).Length), Connections, Exchanges, Operations, Samples);
    }
    private static byte[] Json(NativeAotServiceQuotaPeer.WireTrace trace) => Encoding.UTF8.GetBytes(NativeAotServiceQuotaPeer.SerializeWireReport(trace));
    private static QemuTranscripts.ServiceQuotaHostHttpInspection Read(Values values) => QemuTranscripts.InspectNativeAotServiceQuotaHostHttpForTests(Json(values.Trace()));
    private static Values Polls(bool missing = false)
    {
        Values values = new(); int connection = values.Connect();
        int first = values.Exchange(connection, "/healthz", "ONE"); values.Sample("health.startup", first);
        values.Exchange(connection, "/__clros/service/quota/status", "ONE", missing ? "" : Response("ONE", chunks: true));
        values.Exchange(connection, "/__clros/service/quota/status", "ONE"); return values;
    }

    [HostTest("quota host HTTP binds every persistent unselected poll and copied sample to original byte intervals")]
    private static void PollBindings()
    {
        var values = Polls(); byte[] bytes = Json(values.Trace());
        var result = QemuTranscripts.InspectNativeAotServiceQuotaHostHttpForTests(bytes); bytes.AsSpan().Clear();
        HostCheck.Equal(3, result.Messages.Count); HostCheck.Equal(1, result.Samples.Count);
        for (int i = 0; i < result.Messages.Count; i++)
        {
            var message = result.Messages[i];
            HostCheck.True(message.ExchangeId == i + 1 && message.Request.State == "complete" && message.Response?.State == "complete" &&
                message.Response.BodyBase64 == B64("ONE") && message.RequestOperations.Count == 1 && message.ResponseOperations.Count == 2);
            if (i > 0) HostCheck.True(message.Request.Start == result.Messages[i - 1].Request.End && message.Response!.Start == result.Messages[i - 1].Response!.End);
        }
        HostCheck.True(result.Samples[0] is { Name: "health.startup", MessageIndex: 0, HeadersOnly: false });
        bool immutable = false; try { ((IList<QemuTranscripts.ServiceQuotaHostHttpMessage>)result.Messages).Clear(); }
        catch (NotSupportedException) { immutable = true; } HostCheck.True(immutable);
        Reject(() => Read(Polls(missing: true)), "quota_host_http_exchange_body_or_time");
    }

    [HostTest("quota host HTTP retains failed retry requests without inventing a response or failed write transfer")]
    private static void RetryAndUnknown()
    {
        Values values = new(); int first = values.Connect(), second = values.Connect();
        values.Operation(first, "write", 1, Request("/healthz")); values.Operation(first, "read", 1, "", "failed");
        values.Operation(second, "write", 1, Request("/healthz")); values.Operation(second, "read", 1, Response("ONE"));
        values.Exchanges.Add(new(1, "control", "GET", "/healthz", 1, 100, "complete", 200, B64("ONE"), new[] { first, second }));
        values.Sample("health.startup", 1);
        var result = Read(values); HostCheck.True(result.Messages.Count == 2 && result.Messages[0].Response is null &&
            result.Samples[0].MessageIndex == 1 && result.Messages[1].Response?.State == "complete");
        Values unknown = new(); int connection = unknown.Connect();
        unknown.Operation(connection, "write", 1, "GET /healthz HTTP/1.1\r\nHo");
        unknown.Operation(connection, "write", 1, "st: 127.0.0.1:8080\r\n\r\n", "failed");
        unknown.Operation(connection, "read", 1, Response("ONE"));
        unknown.Exchanges.Add(new(1, "control", "GET", "/healthz", 1, 100, "complete", 200, B64("ONE"), new[] { connection }));
        Reject(() => Read(unknown), "quota_host_http_complete_exchange");
    }

    [HostTest("quota host HTTP preserves cancel header witness and partial echo without endpoint completion")]
    private static void HeldPrefixes()
    {
        Values values = new(); int control = values.Connect();
        string[] names = ["health.startup", "lifecycle.initial", "quota.initial", "worker.initial"];
        string[] paths = ["/healthz", "/__clros/service/lifecycle/status", "/__clros/service/quota/status", "/__clros/service/status"];
        for (int i = 0; i < names.Length; i++) values.Sample(names[i], values.Exchange(control, paths[i], "ONE"));
        int cancel = values.Connect("cancel");
        string header = "HTTP/1.1 200 OK\r\nServer: Kestrel\r\nDate: " + Date + "\r\nConnection: close\r\nTransfer-Encoding: chunked\r\n\r\n";
        int held = values.Exchange(cancel, "/__clros/service/lifecycle/cancel", "", header, "cancel");
        values.Samples.Add(new("cancel.headers", values.Exchanges[held - 1].EndedTicks, B64(header), 0, cancel));
        int echo = values.Connect("echo"); byte[] pattern = Enumerable.Range(0, 64).Select(i => unchecked((byte)(31 + i * 17))).ToArray();
        int exchange = values.Exchange(echo, "/__clros/service/lifecycle/echo", "", "", "echo", "POST");
        values.Operation(echo, "write", exchange, ""); // Replace empty copied write with the binary prefix, within the held exchange.
        var op = values.Operations[^1]; values.Operations[^1] = op with { Requested = 64, Transferred = 64, DataBase64 = Convert.ToBase64String(pattern) };
        values.Exchanges[exchange - 1] = values.Exchanges[exchange - 1] with { EndedTicks = 1000 };
        var result = Read(values);
        HostCheck.True(result.Samples[^1] is { HeadersOnly: true, MessageIndex: 4 } && result.Messages[4].Response?.State == "chunk-size");
        HostCheck.True(result.Messages[^1].Request.State == "body" && result.Messages[^1].Request.DeclaredLength == 32768 &&
            result.Messages[^1].Request.BodyBase64 == Convert.ToBase64String(pattern) && result.Messages[^1].Response is null);
    }

    [HostTest("quota host HTTP rejects body time Host date extra response and mixed logical request claims")]
    private static void Rejections()
    {
        var body = Polls(); body.Exchanges[0] = body.Exchanges[0] with { BodyBase64 = B64("BAD") }; body.Samples[0] = body.Samples[0] with { BodyBase64 = B64("BAD") };
        Reject(() => Read(body), "quota_host_http_exchange_body_or_time");
        var time = Polls(); time.Exchanges[0] = time.Exchanges[0] with { EndedTicks = 5 }; time.Samples[0] = time.Samples[0] with { ElapsedTicks = 5 };
        Reject(() => Read(time), "quota_host_http_exchange_body_or_time");
        var host = Polls(); host.Operations[0] = host.Operations[0] with { DataBase64 = B64(Request("/healthz").Replace("127.0.0.1", "127.0.0.2", StringComparison.Ordinal)) };
        Reject(() => Read(host), "quota_host_http_request_headers");
        Values date = new(); int connection = date.Connect(); date.Exchange(connection, "/healthz", "ONE", Response("ONE").Replace("Tue,", "Mon,", StringComparison.Ordinal));
        Reject(() => Read(date), "quota_host_http_response_headers");
        Values extra = new(); connection = extra.Connect(); extra.Exchange(connection, "/healthz", "ONE", Response("ONE") + Response("ONE"));
        Reject(() => Read(extra), "quota_host_http_response_without_request");
        Values sample = new(); connection = sample.Connect(); sample.Sample("health.startup", sample.Exchange(connection, "/__clros/service/quota/status", "ONE"));
        Reject(() => Read(sample), "quota_host_http_sample_request");
        Values repeated = new(); connection = repeated.Connect(); repeated.Operation(connection, "write", 1, Request("/healthz") + Request("/healthz"));
        repeated.Exchanges.Add(new(1, "control", "GET", "/healthz", 1, 100, "failed", 0, "", new[] { connection }));
        Reject(() => Read(repeated), "quota_host_http_role_or_retry");
        Values mixed = new(); connection = mixed.Connect();
        mixed.Operation(connection, "write", 1, "GET /healthz HTTP/1.1\r\n");
        mixed.Exchanges.Add(new(1, "control", "GET", "/healthz", 1, 4, "failed", 0, "", new[] { connection }));
        mixed.Operation(connection, "write", 2, "Host: 127.0.0.1:8080\r\n\r\n");
        mixed.Exchanges.Add(new(2, "control", "GET", "/healthz", 4, 100, "failed", 0, "", new[] { connection }));
        Reject(() => Read(mixed), "quota_host_http_mixed_exchange");
    }
    private static void Reject(Action action, string reason)
    {
        bool rejected = false; try { action(); } catch (ToolFailure error) { rejected = true; HostCheck.Equal(reason, error.Reason); }
        HostCheck.True(rejected);
    }
}
