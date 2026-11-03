using System.Globalization;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Borrow bounded strict quota wire JSON; inspect it once, frame
    /// known successful streams and pair nonpipelined requests/responses FIFO on
    /// each original connection. Every complete logical control exchange must
    /// match exactly one complete request/response, body and original read time.
    /// Bind all selected samples; retain retries and incomplete/error suffixes.
    /// Reject skipped/extra responses, mixed write identities, inconsistent Host,
    /// body/status/date/framing and claims requiring attempted unknown bytes.
    /// Uses existing32MiB JSON/4MiB raw/8192 exchange/header bounds and returns
    /// copied read-only values. No process/file, packet/guest tuple, cross-clock,
    /// syscall/native/termination or acceptance authority is established.</summary>

    public static ServiceQuotaHostHttpInspection InspectNativeAotServiceQuotaHostHttpForTests(ReadOnlySpan<byte> wireJson) => ReadServiceQuotaHostHttp(wireJson);
    private static ServiceQuotaHostHttpInspection ReadServiceQuotaHostHttp(ReadOnlySpan<byte> wireJson) => ReadServiceQuotaHostHttp(NativeAotServiceQuotaPeer.InspectWireReport(wireJson));
    // Only callers which just ran the strict inspector may use this overload.
    private static ServiceQuotaHostHttpInspection ReadServiceQuotaHostHttp(NativeAotServiceQuotaPeer.WireTrace trace)
    {
        Need(trace.Purpose is "quota-run" or "quota-partial", "quota_host_http_purpose");
        var prefixes = ReadServiceQuotaHostPrefixes(trace);
        List<ServiceQuotaHostHttpMessage> messages = [];
        HashSet<(int Connection, int Exchange)> attempts = [];
        foreach (var connection in prefixes.Connections)
        {
            byte[] sent = Convert.FromBase64String(connection.Writes.KnownBase64), received = Convert.FromBase64String(connection.Reads.KnownBase64);
            var requests = ReadServiceQuotaHttpPrefixes(sent, responses: false).Messages;
            var responses = ReadServiceQuotaHttpPrefixes(received, responses: true).Messages;
            Need(responses.Count <= requests.Count, "quota_host_http_response_without_request");
            string role = trace.Connections[connection.ConnectionId - 1].Role;
            for (int i = 0; i < requests.Count; i++)
            {
                var request = requests[i];
                var response = i < responses.Count ? responses[i] : null;
                int[] written = Intersect(connection.Writes, request.Start, request.End);
                Need(written.Length > 0, "quota_host_http_request_without_write");
                int exchangeId = trace.Operations[written[0] - 1].ExchangeId;
                Need(exchangeId > 0 && written.All(id => trace.Operations[id - 1].ExchangeId == exchangeId), "quota_host_http_mixed_exchange");
                var exchange = trace.Exchanges[exchangeId - 1];
                Need(exchange.Role == role && attempts.Add((connection.ConnectionId, exchangeId)), "quota_host_http_role_or_retry");
                if (request.HeaderEnd >= 0)
                {
                    Need(request.First == exchange.Method + " " + exchange.Path + " HTTP/1.1", "quota_host_http_request_identity");
                    bool held = role is "cancel" or "echo";
                    Need(request.Headers.GetValueOrDefault("Host") == (held ? "clros.service" : "127.0.0.1:" + trace.Port.ToString(CultureInfo.InvariantCulture)) && request.Headers.GetValueOrDefault("Connection") == (held ? "close" : null), "quota_host_http_request_headers");
                    Need(role == "echo" ? request.Framing == "length" && request.DeclaredLength == 32768 : exchange.Method == "POST" ? request.Framing == "length" && request.DeclaredLength == 0 : request.Framing == "none", "quota_host_http_request_body");
                }

                int[] read = response is null ? [] : Intersect(connection.Reads, response.Start, response.End);
                if (response is { HeaderEnd: >= 0 })
                {
                    var headers = response.Headers;
                    string? date = headers.GetValueOrDefault("Date");
                    Need(response.First == "HTTP/1.1 200 OK" && headers.GetValueOrDefault("Server") == "Kestrel" && DateTimeOffset.TryParseExact(date, "r", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed) && parsed.Offset == TimeSpan.Zero && date == parsed.ToString("r", CultureInfo.InvariantCulture) && headers.GetValueOrDefault("Connection") == (role == "cancel" ? "close" : null), "quota_host_http_response_headers");
                }

                if (trace.Purpose == "quota-run")
                {
                    Need(role != "pressure" || response is null, "quota_host_http_pressure_response");
                    if (role == "echo")
                    {
                        byte[] body = Convert.FromBase64String(request.BodyBase64);
                        Need(request.HeaderEnd >= 0 && request.State == "body" && body.Length == 64 && body.Select((value, at) => value == unchecked((byte)(31 + at * 17))).All(value => value) && response is null, "quota_host_http_echo_prefix");
                    }
                }

                messages.Add(new(connection.ConnectionId, exchangeId, request, response, Array.AsReadOnly(written), Array.AsReadOnly(read)));
            }
        }

        var completed = new Dictionary<int, int>();
        foreach (var exchange in trace.Exchanges.Where(e => e.Outcome == "complete"))
        {
            int[] matches = Enumerable.Range(0, messages.Count).Where(i => messages[i].ExchangeId == exchange.Id && messages[i].Request.State == "complete" && messages[i].Response?.State == "complete").ToArray();
            Need(matches.Length == 1, "quota_host_http_complete_exchange");
            var message = messages[matches[0]];
            Need(message.Response!.BodyBase64 == exchange.BodyBase64 && message.ResponseOperations.Count > 0 && message.ResponseOperations.All(id => trace.Operations[id - 1].EndedTicks >= exchange.StartedTicks && trace.Operations[id - 1].EndedTicks <= exchange.EndedTicks), "quota_host_http_exchange_body_or_time");
            completed.Add(exchange.Id, matches[0]);
        }

        List<ServiceQuotaHostHttpSample> samples = [];
        foreach (var sample in trace.Samples)
        {
            if (sample.ExchangeId > 0)
            {
                Need(completed.TryGetValue(sample.ExchangeId, out int index), "quota_host_http_sample_exchange");
                string path = sample.Name.StartsWith("health.", StringComparison.Ordinal) ? "/healthz" : sample.Name.StartsWith("lifecycle.", StringComparison.Ordinal) ? "/__clros/service/lifecycle/status" : sample.Name.StartsWith("quota.", StringComparison.Ordinal) ? "/__clros/service/quota/status" : sample.Name == "worker.start" ? "/__clros/service/start" : "/__clros/service/status";
                Need(messages[index].Request.First == (sample.Name == "worker.start" ? "POST " : "GET ") + path + " HTTP/1.1", "quota_host_http_sample_request");
                samples.Add(new(sample.Name, index, false));
            }
            else
            {
                int[] found = Enumerable.Range(0, messages.Count).Where(i => messages[i].ConnectionId == sample.ConnectionId).ToArray();
                Need(found.Length == 1, "quota_host_http_sample_connection");
                var message = messages[found[0]];
                byte[] read = Convert.FromBase64String(prefixes.Connections[message.ConnectionId - 1].Reads.KnownBase64);
                Need(message.Request.State == "complete" && message.Response is { Start: 0, HeaderEnd: > 0 } && read.Length == message.Response.HeaderEnd && read.AsSpan().SequenceEqual(Convert.FromBase64String(sample.BodyBase64)) && message.Response.BodyBase64.Length == 0, "quota_host_http_sample_headers");
                samples.Add(new(sample.Name, found[0], true));
            }
        }

        return new(prefixes, Array.AsReadOnly(messages.ToArray()), Array.AsReadOnly(samples.ToArray()));
    }

    private static int[] Intersect(ServiceQuotaHostDirection direction, int start, int end) => direction.Operations.Where(op => op.Outcome == "complete" && op.Transferred > 0 && op.Offset < end && op.Offset + op.Transferred > start).Select(op => op.OperationId).ToArray();
}
