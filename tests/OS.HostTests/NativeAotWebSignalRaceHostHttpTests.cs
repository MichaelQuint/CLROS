using System.Text;
using System.Text.Json.Nodes;
using OS.Tooling;
using Peer = OS.Tooling.NativeAotServiceQuotaPeer;

// Independent copied byte/metadata vectors only. No actual HTTP execution,
// packet identity, native signal, owned completion or guest evidence is supplied.
internal static class NativeAotWebSignalRaceHostHttpTests
{
    private const string Endpoint = "http://127.0.0.1:23460", Health = "OS_MINIMAL_API_OK";
    private static string B64(string text) => Convert.ToBase64String(Encoding.ASCII.GetBytes(text));
    private static string Request(string path = "/__clros/churn/status", string method = "GET", int port = 23460) =>
        method + " " + path + " HTTP/1.1\r\nHost: 127.0.0.1:" + port + "\r\n" +
        (method == "POST" ? "Content-Length: 0\r\n" + (path == "/__clros/churn" ? "X-CLROS-Churn" : "X-CLROS-Signal") + ": v1\r\n" : "") + "\r\n";
    private static string Response(string body = "ONE", int status = 200, bool chunks = false) =>
        $"HTTP/1.1 {status} Result\r\nContent-Type: text/plain\r\n" + (chunks ?
        "Transfer-Encoding: chunked\r\n\r\n" + body.Length.ToString("X") + "\r\n" + body + "\r\n0\r\n\r\n" :
        "Content-Length: " + body.Length + "\r\n\r\n" + body);

    internal sealed class Values(int port = 23460)
    {
        internal readonly List<Peer.WireConnection> Connections = [];
        internal readonly List<Peer.WireOperation> Operations = [];
        internal readonly List<Peer.WireExchange> Exchanges = [];
        internal readonly List<JsonObject> Samples = [], Links = [];
        internal readonly Dictionary<int, int> Completions = [];
        private long tick = 1;
        internal int Connect(string role = "control")
        {
            int id = Connections.Count + 1;
            Connections.Add(new(id, role, "127.0.0.1", 41000 + id, "127.0.0.1", port, 0, 10000)); return id;
        }
        internal void Op(int connection, string direction, int exchange, string data, string outcome = "complete")
        {
            long offset = Operations.Where(op => op.ConnectionId == connection && op.Direction == direction && op.Transferred >= 0).Sum(op => (long)op.Transferred);
            Operations.Add(new(Operations.Count + 1, connection, direction, offset, direction == "read" && outcome != "complete" ? 1024 : data.Length,
                outcome == "complete" ? data.Length : -1, ++tick, ++tick, outcome, B64(data), direction == "write" ? exchange : 0));
        }
        internal int Add(int connection, string path = "/__clros/churn/status", string body = "ONE", string? rawResponse = null,
            string method = "GET", string outcome = "response", int status = 200, string? rawRequest = null)
        {
            int id = Exchanges.Count + 1; long start = ++tick;
            Op(connection, "write", id, rawRequest ?? Request(path, method, port));
            string response = rawResponse ?? Response(body, status);
            if (response.Length > 0)
            { Op(connection, "read", id, response[..^1]); Op(connection, "read", id, response[^1..]); }
            Exchanges.Add(new(id, Connections[connection - 1].Role, method, path, start, ++tick, outcome, status, B64(body), new[] { connection })); return id;
        }
        internal void Select(string kind, int exchange)
        {
            var value = Exchanges[exchange - 1];
            Samples.Add(new() { ["kind"] = kind, ["status"] = value.Status, ["body"] = Encoding.ASCII.GetString(Convert.FromBase64String(value.BodyBase64)) });
            Links.Add(new() { ["sample"] = Links.Count, ["attempt"] = exchange, ["related"] = 0 });
        }
        internal void Cancel(int exchange)
        {
            Samples.Add(new() { ["kind"] = "churn-cancel", ["status"] = 0, ["body"] = "" });
            Links.Add(new() { ["sample"] = Links.Count, ["attempt"] = 0, ["related"] = exchange });
        }
        internal byte[] Wire() => Encoding.UTF8.GetBytes(NativeAotWebPayload.SerializeSignalRaceWire(new(port, "signalrace-partial", 10001,
            Operations.Sum(op => (long)Convert.FromBase64String(op.DataBase64).Length), Connections, Exchanges, Operations, [])));
        internal string Selected() => new JsonObject { ["schema"] = "clros.web.signalrace.v1", ["endpoint"] = "http://127.0.0.1:" + port,
            ["samples"] = new JsonArray(Samples.Select(value => (JsonNode?)value.DeepClone()).ToArray()) }.ToJsonString();
        internal string Attempts()
        {
            var rows = Exchanges.Select(e => (JsonNode?)new JsonObject
            {
                ["id"] = e.Id, ["method"] = e.Method, ["path"] = e.Path, ["completion"] = Completions.GetValueOrDefault(e.Id, e.Id),
                ["headers"] = e.Status > 0, ["status"] = e.Status, ["mediaType"] = e.Status > 0 ? "text/plain" : "", ["mediaTypeComplete"] = true,
                ["bodyBase64"] = e.BodyBase64, ["bodyComplete"] = e.Outcome == "response", ["outcome"] = e.Outcome,
                ["error"] = e.Outcome == "response" ? "" : e.Outcome == "cancelled" ? "cancelled" : "IOException"
            }).ToArray();
            return new JsonObject { ["schema"] = "clros.web.signalrace.attempts.v1", ["endpoint"] = "http://127.0.0.1:" + port,
                ["semanticComplete"] = false, ["failure"] = "copied_failure", ["attempts"] = new JsonArray(rows),
                ["selections"] = new JsonArray(Links.Select(value => (JsonNode?)value.DeepClone()).ToArray()) }.ToJsonString();
        }
    }
    private static QemuTranscripts.WebSignalRaceHostHttpInspection Read(Values values) =>
        QemuTranscripts.InspectNativeAotWebSignalRaceHostHttp(values.Wire(), values.Attempts(), values.Selected(), Endpoint, Health);
    private static void Reject(Action action)
    { bool refused = false; try { action(); } catch (Exception error) when (error is ToolFailure or InvalidDataException) { refused = true; } HostCheck.True(refused); }

    [HostTest("web race host HTTP binds identical persistent FIFO bodies to exact request exchange and operation identities")]
    private static void PersistentFifo()
    {
        Values values = new(); int connection = values.Connect();
        for (int i = 0; i < 3; i++) values.Add(connection, rawResponse: Response(chunks: i == 1));
        values.Select("baseline-churn", 1); byte[] wire = values.Wire();
        var report = QemuTranscripts.InspectNativeAotWebSignalRaceHostHttp(wire, values.Attempts(), values.Selected(), Endpoint, Health);
        wire.AsSpan().Clear(); values.Operations.Clear();
        HostCheck.Equal(3, report.Messages.Count); HostCheck.Equal(1, report.Samples.Count);
        for (int i = 0; i < 3; i++)
        {
            var message = report.Messages[i]; HostCheck.Equal(i + 1, message.ExchangeId);
            HostCheck.True(message.Request.State == "complete" && message.Response is { State: "complete", BodyBase64: "T05F" });
            HostCheck.True(message.RequestOperations.SequenceEqual(new[] { i * 3 + 1 }) && message.ResponseOperations.SequenceEqual(new[] { i * 3 + 2, i * 3 + 3 }));
            if (i > 0) HostCheck.True(message.Request.Start == report.Messages[i - 1].Request.End && message.Response!.Start == report.Messages[i - 1].Response!.End);
        }
        HostCheck.True(report.Samples[0] is { Sample: 0, ExchangeId: 1, MessageIndex: 0, HostCancellation: false });
        bool immutable = false; try { ((IList<QemuTranscripts.ServiceQuotaHostHttpMessage>)report.Messages).Clear(); } catch (NotSupportedException) { immutable = true; }
        HostCheck.True(immutable && report.Prefixes.KnownReadBytes > 0);
    }

    [HostTest("web race host HTTP keeps failed retry and unknown write separate from uniquely complete response")]
    private static void RetryAndUnknown()
    {
        Values retry = new(); int first = retry.Connect(), second = retry.Connect();
        retry.Op(first, "write", 1, Request()); retry.Op(first, "read", 1, "", "failed");
        retry.Op(second, "write", 1, Request()); retry.Op(second, "read", 1, Response());
        retry.Exchanges.Add(new(1, "control", "GET", "/__clros/churn/status", 1, 100, "response", 200, B64("ONE"), new[] { first, second }));
        retry.Select("baseline-churn", 1); var report = Read(retry);
        HostCheck.True(report.Messages.Count == 2 && report.Messages[0].Response is null && report.Messages[1].Response?.State == "complete" && report.Samples[0].MessageIndex == 1);
        Values unknown = new(); int connection = unknown.Connect();
        unknown.Op(connection, "write", 1, "GET /__clros/churn/status HTTP/1.1\r\nHo");
        unknown.Op(connection, "write", 1, "st: 127.0.0.1:23460\r\n\r\n", "failed");
        unknown.Op(connection, "read", 1, Response());
        unknown.Exchanges.Add(new(1, "control", "GET", "/__clros/churn/status", 1, 100, "response", 200, B64("ONE"), new[] { connection }));
        Reject(() => Read(unknown));
        unknown.Operations.RemoveAt(2); unknown.Exchanges[0] = unknown.Exchanges[0] with { Outcome = "faulted", Status = 0, BodyBase64 = "" };
        var incomplete = Read(unknown); var prefix = incomplete.Prefixes.Connections.Single();
        HostCheck.True(incomplete.Messages.Single().Request.State == "header" && prefix.Writes.UnknownWrite is not null &&
            Encoding.ASCII.GetString(Convert.FromBase64String(prefix.Writes.KnownBase64)).EndsWith("\r\nHo", StringComparison.Ordinal));
        Values zero = new(); zero.Exchanges.Add(new(1, "control", "GET", "/__clros/churn/status", 0, 1, "cancelled", 0, "", Array.Empty<int>()));
        var noIo = Read(zero); HostCheck.True(noIo.Messages.Count == 0 && noIo.Prefixes.Connections.Count == 0 && noIo.Prefixes.KnownReadBytes == 0);
    }

    [HostTest("web race host HTTP preserves diagnostic700 decoded chunks incomplete entities and explicit host cancellation")]
    private static void DiagnosticsAndCancellation()
    {
        Values diagnostic = new(); int control = diagnostic.Connect(), race = diagnostic.Connect("race");
        diagnostic.Add(control, body: "SEVEN", status: 700); diagnostic.Add(control, rawResponse: Response(chunks: true));
        string partial = "HTTP/1.1 200 Result\r\nContent-Type: text/plain\r\nTransfer-Encoding: chunked\r\n\r\n3\r\nab";
        diagnostic.Add(race, "/__clros/signalrace", "a", partial, "POST", "faulted");
        var report = Read(diagnostic); HostCheck.True(report.Messages[0].Response!.First.StartsWith("HTTP/1.1 700 ", StringComparison.Ordinal));
        HostCheck.True(report.Messages[1].Response is { Framing: "chunked", State: "complete", BodyBase64: "T05F" });
        HostCheck.True(report.Messages[2].Response is { State: "chunk-data", BodyBase64: "YWI=" });
        Reject(() => QemuTranscripts.InspectNativeAotServiceQuotaHttpPrefixesForTests(Encoding.ASCII.GetBytes(Response(status: 700)), true));
        diagnostic.Exchanges[2] = diagnostic.Exchanges[2] with { BodyBase64 = B64("z") }; Reject(() => Read(diagnostic));

        Values cancel = new(); control = cancel.Connect(); int churn = cancel.Connect("churn");
        string[] names = ["baseline-churn", "baseline-race", "health-before", "churn-pending", "recovery-armed", "churn-overlap", "overlap-health"];
        string[] paths = ["/__clros/churn/status", "/__clros/signalrace/status", "/healthz", "/__clros/churn/status", "/__clros/signalrace/status", "/__clros/churn/status", "/healthz"];
        for (int i = 0; i < names.Length; i++)
        {
            if (i == 3) cancel.Add(churn, "/__clros/churn", "", "", "POST", "cancelled", 0);
            cancel.Select(names[i], cancel.Add(control, paths[i]));
        }
        cancel.Cancel(4); cancel.Completions[4] = 8;
        for (int id = 5; id <= 8; id++) cancel.Completions[id] = id - 1;
        cancel.Exchanges[3] = cancel.Exchanges[3] with { EndedTicks = 9999 };
        var canceled = Read(cancel); HostCheck.True(canceled.Samples[7] is { Sample: 7, ExchangeId: 4, MessageIndex: -1, HostCancellation: true });
        HostCheck.True(canceled.Messages.Single(message => message.ExchangeId == 4).Response is null);
    }

    [HostTest("web race host HTTP rejects skipped extra mismatched and unauthenticated framing claims")]
    private static void Refusals()
    {
        foreach (string response in new[] { "", Response("BAD"), Response() + Response(), Response().Replace("text/plain", "application/json", StringComparison.Ordinal) })
        { Values values = new(); values.Add(values.Connect(), rawResponse: response); Reject(() => Read(values)); }
        Values skipped = new(); int persistent = skipped.Connect(); skipped.Add(persistent); skipped.Add(persistent, rawResponse: ""); skipped.Add(persistent); Reject(() => Read(skipped));
        Values ambiguous = new(); int first = ambiguous.Connect(), second = ambiguous.Connect();
        ambiguous.Op(first, "write", 1, Request()); ambiguous.Op(first, "read", 1, Response());
        ambiguous.Op(second, "write", 1, Request()); ambiguous.Op(second, "read", 1, Response());
        ambiguous.Exchanges.Add(new(1, "control", "GET", "/__clros/churn/status", 1, 100, "response", 200, B64("ONE"), new[] { first, second })); Reject(() => Read(ambiguous));
        Values status = new(); status.Add(status.Connect()); status.Exchanges[0] = status.Exchanges[0] with { Status = 201 }; Reject(() => Read(status));
        Values host = new(); host.Add(host.Connect(), rawRequest: Request().Replace("127.0.0.1", "127.0.0.2", StringComparison.Ordinal)); Reject(() => Read(host));
        Values header = new(); header.Add(header.Connect("churn"), "/__clros/churn", "", "", "POST", "faulted", 0,
            Request("/__clros/churn", "POST").Replace("X-CLROS-Churn: v1", "X-CLROS-Churn: v2", StringComparison.Ordinal)); Reject(() => Read(header));
        Values timing = new(); timing.Add(timing.Connect()); timing.Exchanges[0] = timing.Exchanges[0] with { EndedTicks = timing.Operations[0].EndedTicks + 1 }; Reject(() => Read(timing));
        Values mixed = new(); int connection = mixed.Connect(); mixed.Op(connection, "write", 1, "GET /__clros/churn/status HTTP/1.1\r\n");
        mixed.Exchanges.Add(new(1, "control", "GET", "/__clros/churn/status", 1, 4, "faulted", 0, "", new[] { connection }));
        mixed.Op(connection, "write", 2, "Host: 127.0.0.1:23460\r\n\r\n");
        mixed.Exchanges.Add(new(2, "control", "GET", "/__clros/churn/status", 4, 100, "faulted", 0, "", new[] { connection })); Reject(() => Read(mixed));
    }
}
