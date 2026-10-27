using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Copied FIFO message coordinates and original first-byte records.
    /// RemotePort is the guest TCP tuple, never the peer's host socket identity.
    /// Raw response headers and decoded entity bytes are copied as Base64.</summary>
    public sealed record StaticFileHttpWireMessage(uint RemotePort, int MessageIndex, int RequestFirstRecord,
        int ResponseLastRecord, string RequestBase64, string ResponseHeaderBase64, string BodyBase64, string ResponseSha256);

    /// <summary>A receipt row joins every byte-equivalent permitted wire match.
    /// Only the two concurrent identical requests may have two candidates; no
    /// unobservable ordinal-to-NAT identity is invented for that pair.</summary>
    public sealed record StaticFileHttpResponseJoin(int Ordinal, IReadOnlyList<StaticFileHttpWireMessage> Matches);

    /// <summary>Copied HTTP/packet facts only. Reset reception and recovery do
    /// not prove descriptor release, process exit, source or native authority.</summary>
    public sealed record StaticFileHttpPacketInspection(int Connections, int TcpRecords, int RequestBytes,
        int ResponseBytes, IReadOnlyList<StaticFileHttpResponseJoin> Responses, uint AbortedGuestPort,
        int AbortResetRecord, int AbortWireBytes, bool ConcurrentWireOverlap,
        IReadOnlyList<StaticFileHttpFlow> Flows);

    /// <summary>Copied unique stream extents in original SYN order. Queued
    /// application bytes can exceed transmitted bytes only on the reset flow;
    /// the scheduler/transport consumer must independently bound that suffix.</summary>
    public readonly record struct StaticFileHttpFlow(uint RemotePort, int RequestBytes, int ResponseBytes, bool Reset);

    private sealed record StaticPeerRow(int Ordinal, string Method, string Path, int Status,
        Dictionary<string, string> Request, Dictionary<string, string> Response, byte[] Body, long Started, long Finished);
    private sealed record StaticHttpMessage(ServiceWireFlow Flow, int Index, string Method, string Path,
        Dictionary<string, string> RequestHeaders, Dictionary<string, string> ResponseHeaders, int Status,
        byte[] Request, byte[] Header, byte[] Body, byte[] RawResponse, int RequestFirst, int RequestLast,
        int ResponseFirst, int ResponseLast)
    {
        internal StaticFileHttpWireMessage Copy() => new(Flow.Port, Index, RequestFirst, ResponseLast,
            Convert.ToBase64String(Request), Convert.ToBase64String(Header), Convert.ToBase64String(Body),
            Convert.ToHexString(SHA256.HashData(RawResponse)));
    }

    /// <summary>Borrow owned original classic-PCAP bytes, ordered guest ports,
    /// complete clros-staged-http-v1 receipt, its26 named body byte arrays and
    /// abort.json bytes. Reuse strict parent/MAC/IP/TCP/ACK/retry reconstruction;
    /// require exactly two pooled connections plus one client-reset connection,
    /// all26 expected requests/responses, exact recorded headers/entities and
    /// independent staged-asset/JSON/range/HEAD/304/missing contracts. Verify
    /// FIFO and cross-flow packet ordering, concurrent wire overlap and two
    /// complete post-reset exchanges. Four-MiB PCAP/stream aggregate,256-KiB
    /// receipt,4-KiB abort,512-KiB body aggregate and bounded message/header counts
    /// reject before unbounded growth. No I/O, retained input, source/native,
    /// clock-correlation or guest qualification authority is supplied. Malformed
    /// or unsupported values throw ToolFailure/InvalidDataException/JsonException.
    /// The caller must hold all borrowed input bytes unchanged for this call.</summary>
    public static StaticFileHttpPacketInspection InspectNativeAotStaticFileHttpPackets(byte[] packets, uint anchor,
        QemuArpReceiveReceipt arp, IReadOnlyList<uint> orderedGuestPorts, ReadOnlySpan<byte> receiptJson,
        IReadOnlyDictionary<string, byte[]> bodyFiles, ReadOnlySpan<byte> abortJson)
    {
        Need(packets is { Length: > 24 and <= 4 * 1024 * 1024 } &&
            orderedGuestPorts is { Count: 3 } && receiptJson.Length is > 0 and <= 256 * 1024 &&
            abortJson.Length is > 0 and <= 4096 && bodyFiles is { Count: 26 }, "static_http_input");
        using var receipt = JsonDocument.Parse(receiptJson.ToArray(), new() { MaxDepth = 8 });
        var root = receipt.RootElement;
        StaticObject(root, "Schema", "Port", "Complete", "ClockFrequency", "Responses");
        int port = root.GetProperty("Port").GetInt32(); long frequency = root.GetProperty("ClockFrequency").GetInt64();
        Need(root.GetProperty("Schema").GetString() == "clros-staged-http-v1" && root.GetProperty("Complete").GetBoolean() &&
            port is > 0 and <= 65535 && frequency is > 0 and <= 1_000_000_000 &&
            root.GetProperty("Responses").ValueKind == JsonValueKind.Array && root.GetProperty("Responses").GetArrayLength() == 26,
            "static_http_receipt");
        List<StaticPeerRow> rows = []; long total = 0;
        foreach (var row in root.GetProperty("Responses").EnumerateArray())
        {
            StaticObject(row, "Ordinal", "Method", "Path", "Status", "RequestHeaders", "ResponseHeaders", "Bytes", "Sha256", "BodyFile", "Started", "Finished");
            int ordinal = rows.Count; string name = ordinal.ToString("D3", CultureInfo.InvariantCulture) + ".body";
            Need(row.GetProperty("Ordinal").GetInt32() == ordinal && row.GetProperty("BodyFile").GetString() == name &&
                bodyFiles.TryGetValue(name, out byte[]? body) && body is { Length: <= 65536 }, "static_http_body_file");
            body = bodyFiles[name]; total += body.Length; Need(total <= 512 * 1024, "static_http_body_bound");
            Need(row.GetProperty("Bytes").GetInt32() == body.Length &&
                row.GetProperty("Sha256").GetString() == Convert.ToHexString(SHA256.HashData(body)), "static_http_body_hash");
            long started = row.GetProperty("Started").GetInt64(), finished = row.GetProperty("Finished").GetInt64();
            Need(started > 0 && finished >= started && (Int128)finished - started <= (Int128)90 * frequency &&
                (ordinal == 0 || started >= rows[^1].Started) &&
                (ordinal == 0 || ordinal is 23 or 24 || started >= rows[^1].Finished), "static_http_host_order");
            rows.Add(new(ordinal, row.GetProperty("Method").GetString()!, row.GetProperty("Path").GetString()!,
                row.GetProperty("Status").GetInt32(), StaticHeaderList(row.GetProperty("RequestHeaders")),
                StaticHeaderList(row.GetProperty("ResponseHeaders")), body, started, finished));
        }
        Need((Int128)rows[^1].Finished - rows[0].Started <= (Int128)NativeAotStagedStaticPeer.MatrixTimeoutSeconds * frequency &&
            Math.Max(rows[22].Started, rows[23].Started) < Math.Min(rows[22].Finished, rows[23].Finished), "static_http_host_overlap");
        StaticMatrix(rows);
        using var abortDocument = JsonDocument.Parse(abortJson.ToArray(), new() { MaxDepth = 4 });
        var abort = abortDocument.RootElement;
        StaticObject(abort, "LocalPort", "Request", "Prefix", "Started", "Closed", "Close");
        Need(abort.GetProperty("LocalPort").GetInt32() is > 0 and <= 65535 && abort.GetProperty("Close").GetString() == "reset",
            "static_http_abort_receipt");
        byte[] abortRequest = StaticBase64(abort.GetProperty("Request").GetString()!, 1024),
            abortPrefix = StaticBase64(abort.GetProperty("Prefix").GetString()!, 512);
        long abortStarted = abort.GetProperty("Started").GetInt64(), abortClosed = abort.GetProperty("Closed").GetInt64();
        Need(abortStarted >= Math.Max(rows[22].Finished, rows[23].Finished) && abortClosed >= abortStarted &&
            rows[24].Started >= abortClosed && rows[25].Started >= rows[24].Finished &&
            abortPrefix.Length >= 12 && abortPrefix.AsSpan().StartsWith("HTTP/1.1 200"u8) &&
            abortRequest.AsSpan().SequenceEqual(Encoding.ASCII.GetBytes("GET /boundary.bin HTTP/1.1\r\nHost: 127.0.0.1:" +
                port.ToString(CultureInfo.InvariantCulture) + "\r\nConnection: keep-alive\r\n\r\n")), "static_http_abort_receipt");

        List<ServiceWireByteRange> ranges = [];
        var capture = ServiceWirePackets(packets, anchor, arp, orderedGuestPorts, concurrent: true, lifecycle: true,
            observe: range => { Need(ranges.Count < MaximumServiceWireRanges, "static_http_range_bound"); ranges.Add(range); }, quotaStreams: true);
        Need(capture.QuietSyns == 0 && capture.Flows.Count(flow => flow.ResetRecord != 0) == 1, "static_http_flow_inventory");
        var reset = capture.Flows.Single(flow => flow.ResetRecord != 0);
        Need(reset.ClientSeen == reset.RequestExtent && reset.ServerSeen == reset.ResponseExtent &&
            reset.Request.AsSpan(0, reset.RequestExtent).SequenceEqual(abortRequest) &&
            reset.Response.AsSpan(0, reset.ResponseExtent).StartsWith(abortPrefix), "static_http_abort_wire");
        StaticAbortResponse(reset.Response.AsSpan(0, reset.ResponseExtent), rows[10]);
        List<StaticHttpMessage> messages = [];
        foreach (var flow in capture.Flows.Where(flow => flow != reset))
        {
            ServiceWireClose(flow, serverFirst: false);
            int requestAt = 0, responseAt = 0, index = 0;
            while (requestAt < flow.RequestExtent)
            {
                Need(messages.Count < 26, "static_http_extra_request");
                var request = ServiceHttpHead(flow.Request.AsSpan(requestAt, flow.RequestExtent - requestAt));
                string[] first = request.First.Split(' ');
                Need(first.Length == 3 && first[0] is "GET" or "HEAD" or "POST" && first[2] == "HTTP/1.1" &&
                    first[1].Length is > 0 and <= 256 && first[1][0] == '/' &&
                    !request.Headers.ContainsKey("Transfer-Encoding") && !request.Headers.ContainsKey("Content-Encoding") &&
                    (!request.Headers.TryGetValue("Content-Length", out string? length) || first[0] == "POST" && length == "0"),
                    "static_http_request");
                int requestEnd = requestAt + request.End;
                var response = StaticResponse(flow.Response.AsSpan(responseAt, flow.ResponseExtent - responseAt), first[0]);
                int responseEnd = responseAt + response.End;
                int First(bool outgoing, int start, int end) => StaticRecords(ranges, flow.Port, outgoing, start, end, false);
                int Last(bool outgoing, int start, int end) => StaticRecords(ranges, flow.Port, outgoing, start, end, true);
                int requestFirst = First(false, requestAt, requestEnd), requestLast = Last(false, requestAt, requestEnd),
                    responseFirst = First(true, responseAt, responseEnd), responseLast = Last(true, responseAt, responseEnd);
                Need(responseFirst >= requestLast, "static_http_response_order");
                messages.Add(new(flow, index++, first[0], first[1], request.Headers, response.Headers, response.Status,
                    flow.Request.AsSpan(requestAt, request.End).ToArray(), flow.Response.AsSpan(responseAt, response.HeaderEnd).ToArray(),
                    response.Body, flow.Response.AsSpan(responseAt, response.End).ToArray(), requestFirst, requestLast, responseFirst, responseLast));
                requestAt = requestEnd; responseAt = responseEnd;
            }
            Need(index > 0 && requestAt == flow.RequestExtent && responseAt == flow.ResponseExtent, "static_http_stream_inventory");
        }
        Need(messages.Count == 26, "static_http_response_inventory");
        // The peer creates its second pooled connection only after the first
        // 22 exchanges; it creates the reset connection after both overlapping
        // responses. Prove that causal order before joining SYN order to the
        // scheduler's accept order. A generic FIFO claim alone is insufficient
        // when handshakes on different tuples could finish out of order.
        Need(capture.Flows.Count==3 && reset==capture.Flows[2] && messages.Count(message=>message.Flow==capture.Flows[0] &&
            message.ResponseLast<capture.Flows[1].SynRecord)>=22,"static_http_accept_order");
        List<StaticFileHttpResponseJoin> joined = []; int previous = 0;
        for (int ordinal = 0; ordinal < 22; ordinal++) JoinOne(ordinal);
        var pair = messages.OrderBy(message => message.RequestFirst).Take(2).ToArray();
        Need(pair.Length == 2 && pair[0].Flow != pair[1].Flow && pair.All(message => message.RequestFirst >= previous) &&
            Math.Max(pair[0].RequestFirst, pair[1].RequestFirst) < Math.Min(pair[0].ResponseLast, pair[1].ResponseLast),
            "static_http_wire_overlap");
        bool direct = StaticMatches(pair[0], rows[22], port) && StaticMatches(pair[1], rows[23], port),
            reverse = StaticMatches(pair[1], rows[22], port) && StaticMatches(pair[0], rows[23], port);
        Need(direct || reverse, "static_http_concurrent_join");
        joined.Add(new(22, Array.AsReadOnly((direct && reverse ? pair : [direct ? pair[0] : pair[1]]).Select(message => message.Copy()).ToArray())));
        joined.Add(new(23, Array.AsReadOnly((direct && reverse ? pair : [direct ? pair[1] : pair[0]]).Select(message => message.Copy()).ToArray())));
        foreach (var message in pair) messages.Remove(message);
        previous = Math.Max(pair[0].ResponseLast, pair[1].ResponseLast);
        Need(reset.SynRecord>previous,"static_http_reset_accept_order");
        int resetFirst = StaticRecords(ranges, reset.Port, false, 0, reset.RequestExtent, false);
        Need(resetFirst > previous && reset.ResetRecord > resetFirst, "static_http_abort_order"); previous = reset.ResetRecord;
        JoinOne(24); JoinOne(25); Need(messages.Count == 0, "static_http_extra_message");
        return new(capture.Flows.Count, capture.TcpRecords, capture.Flows.Sum(flow => flow.RequestExtent),
            capture.Flows.Sum(flow => flow.ResponseExtent), Array.AsReadOnly(joined.ToArray()), reset.Port,
            reset.ResetRecord, reset.ResponseExtent, true, Array.AsReadOnly(capture.Flows
                .Select(flow=>new StaticFileHttpFlow(flow.Port,flow.RequestExtent,flow.ResponseExtent,flow.ResetRecord!=0)).ToArray()));

        void JoinOne(int ordinal)
        {
            Need(messages.Count > 0, "static_http_missing_message");
            int first = messages.Min(message => message.RequestFirst);
            var candidates = messages.Where(message => message.RequestFirst == first).ToArray();
            Need(candidates.Length == 1 && candidates[0].RequestFirst >= previous && StaticMatches(candidates[0], rows[ordinal], port),
                "static_http_receipt_wire_join");
            var message = candidates[0]; messages.Remove(message); previous = message.ResponseLast;
            joined.Add(new(ordinal, Array.AsReadOnly(new[] { message.Copy() })));
        }
    }

    private static int StaticRecords(List<ServiceWireByteRange> ranges, uint port, bool outgoing, int start, int end, bool last)
    {
        var found = ranges.Where(range => range.RemotePort == port && range.ServerToClient == outgoing &&
            range.StreamOffset < end && range.StreamOffset + range.ByteCount > start).Select(range => range.Record).ToArray();
        Need(found.Length > 0, "static_http_missing_range"); return last ? found.Max() : found.Min();
    }

    private static bool StaticMatches(StaticHttpMessage message, StaticPeerRow row, int port)
    {
        if (message.Method != row.Method || message.Path != row.Path || message.Status != row.Status ||
            !message.Body.AsSpan().SequenceEqual(row.Body) || !StaticHeadersEqual(message.ResponseHeaders, row.Response)) return false;
        Dictionary<string, string> request = new(row.Request, StringComparer.OrdinalIgnoreCase)
        { ["Host"] = "127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture) };
        if (row.Method == "POST") request.Add("Content-Length", "0");
        return StaticHeadersEqual(message.RequestHeaders, request);
    }

    private static (int End, int HeaderEnd, int Status, Dictionary<string, string> Headers, byte[] Body) StaticResponse(
        ReadOnlySpan<byte> bytes, string method,bool diskStaticFull=false)
    {
        var head = ServiceHttpHead(bytes);
        Need(head.First.Length >= 13 && head.First.StartsWith("HTTP/1.1 ", StringComparison.Ordinal) && head.First[12] == ' ', "static_http_status");
        int status = int.Parse(head.First.AsSpan(9, 3), NumberStyles.None, CultureInfo.InvariantCulture);
        Need(status is 200 or 206 or 304 or 404 or 416 && head.First == "HTTP/1.1 " + status.ToString(CultureInfo.InvariantCulture) + " " + (status switch
        { 200 => "OK", 206 => "Partial Content", 304 => "Not Modified", 404 => "Not Found", 416 => "Range Not Satisfiable", _ => "" }), "static_http_status");
        bool chunked = head.Headers.TryGetValue("Transfer-Encoding", out string? transfer), hasLength = head.Headers.TryGetValue("Content-Length", out string? text);
        Need(!head.Headers.ContainsKey("Trailer") && !head.Headers.ContainsKey("Content-Encoding") &&
            (!chunked || !hasLength && transfer == "chunked"), "static_http_framing");
        int length = hasLength ? QuotaHttpPrefixLength(text!) : 0; Need(length <= (diskStaticFull?DiskStaticHttpContract.MaximumAssetBytes:65536), "static_http_entity_bound");
        if (method == "HEAD" || status == 304)
        { Need(!chunked && (method != "HEAD" || hasLength), "static_http_no_body"); return (head.End, head.End, status, head.Headers, []); }
        Need(chunked || hasLength, "static_http_framing");
        if (chunked)
        {
            var decoded = QuotaHttpPrefixChunks(bytes[head.End..], diskStaticFull?DiskStaticHttpContract.MaximumAssetBytes:65536);
            Need(decoded.State == "complete", "static_http_chunk_complete");
            return (head.End + decoded.Consumed, head.End, status, head.Headers, Convert.FromBase64String(decoded.Body));
        }
        Need(length <= bytes.Length - head.End, "static_http_entity_complete");
        return (head.End + length, head.End, status, head.Headers, bytes.Slice(head.End, length).ToArray());
    }

    private static void StaticAbortResponse(ReadOnlySpan<byte> response, StaticPeerRow boundary)
    {
        var head = ServiceHttpHead(response);
        Dictionary<string, string> expected = new(boundary.Response, StringComparer.OrdinalIgnoreCase);
        expected["Date"] = head.Headers.GetValueOrDefault("Date", "");
        Need(head.First == "HTTP/1.1 200 OK" && head.Headers.GetValueOrDefault("Content-Length") == "65536" &&
            StaticDate(expected["Date"]) && StaticHeadersEqual(head.Headers, expected) &&
            !head.Headers.ContainsKey("Transfer-Encoding") && !head.Headers.ContainsKey("Content-Encoding") &&
            head.Headers.GetValueOrDefault("Server") == "Kestrel" && head.Headers.GetValueOrDefault("Cache-Control") == "no-store" &&
            head.Headers.GetValueOrDefault("Content-Type") == boundary.Response.GetValueOrDefault("Content-Type") &&
            head.Headers.GetValueOrDefault("ETag") == boundary.Response.GetValueOrDefault("ETag") &&
            head.Headers.GetValueOrDefault("Last-Modified") == boundary.Response.GetValueOrDefault("Last-Modified") &&
            response.Length - head.End <= boundary.Body.Length && boundary.Body.AsSpan().StartsWith(response[head.End..]), "static_http_abort_entity");
    }

    private static void StaticMatrix(List<StaticPeerRow> rows)
    {
        var assets = NativeAotStaticFilePackage.StagedAssets();
        string[] paths = ["/healthz", "/config", "/api/status", "/api/items", "/api/items?empty=true", ..assets.Select(asset => "/" + asset.Path[8..]),
            "/", "/boundary.bin", "/boundary.bin", "/boundary.bin", "/boundary.bin", "/boundary.bin", "/missing.txt", "/appsettings.json",
            "/index.html.exe", "/one.bin", "/boundary.bin", "/boundary.bin", "/healthz", "/boundary.bin"];
        Need(paths.Length == rows.Count, "static_http_matrix_inventory");
        string etag = rows[10].Response.GetValueOrDefault("ETag", ""), modified = rows[10].Response.GetValueOrDefault("Last-Modified", "");
        Need(etag.Length is > 2 and <= 128 && etag[0] == '"' && etag[^1] == '"' && StaticDate(modified), "static_http_validators");
        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i]; int expectedStatus = i == 14 ? 206 : i == 15 ? 416 : i is 16 or 17 ? 304 : i is >= 18 and <= 21 ? 404 : 200;
            Need(row.Method == (i == 13 ? "HEAD" : i == 21 ? "POST" : "GET") && row.Path == paths[i] && row.Status == expectedStatus &&
                row.Response.GetValueOrDefault("Server") == "Kestrel" && StaticDate(row.Response.GetValueOrDefault("Date", "")), "static_http_matrix");
            Dictionary<string, string> expectedHeaders = new(StringComparer.OrdinalIgnoreCase);
            if (i is 14 or 15) expectedHeaders.Add("Range", i == 14 ? "bytes=510-1031" : "bytes=65536-");
            if (i == 16) expectedHeaders.Add("If-None-Match", etag);
            if (i == 17) expectedHeaders.Add("If-Modified-Since", modified);
            Need(StaticHeadersEqual(row.Request, expectedHeaders), "static_http_matrix_request_headers");
            byte[]? expected = i is 0 or 24 ? "OS_MINIMAL_API_OK"u8.ToArray() : i is >= 5 and <= 11 ? assets[i - 5].Bytes :
                i == 12 ? assets[0].Bytes : i is 22 or 23 or 25 ? assets[5].Bytes : i == 14 ? assets[5].Bytes.AsSpan(510, 522).ToArray() :
                i is >= 13 and <= 21 ? [] : i == 4 ? "{\"items\":[]}"u8.ToArray() : null;
            if (expected is not null) Need(row.Body.AsSpan().SequenceEqual(expected), "static_http_matrix_body");
            if (i is 1 or 2 or 3) StaticJsonBody(row.Body, i);
            if (i is >= 0 and <= 4 or 24)
            {
                string media = i is 0 or 24 ? "text/plain" : "application/json";
                Need(row.Response.GetValueOrDefault("Content-Type") is { } type &&
                    (type == media || type.StartsWith(media + ";", StringComparison.Ordinal)), "static_http_media");
            }
            bool staticAsset = i is >= 5 and <= 17 or 22 or 23 or 25;
            if (staticAsset)
            {
                Need(row.Response.GetValueOrDefault("Cache-Control") == "no-store", "static_http_cache");
                if (i is >= 13 and <= 17)
                {
                    if (i == 15) Need(row.Response.GetValueOrDefault("Content-Range") == "bytes */65536", "static_http_range");
                    else Need(row.Response.GetValueOrDefault("ETag") == etag && row.Response.GetValueOrDefault("Last-Modified") == modified, "static_http_validator_join");
                }
                if (i == 14) Need(row.Response.GetValueOrDefault("Content-Range") == "bytes 510-1031/65536", "static_http_range");
                if (i is 22 or 23 or 25) Need(row.Response.GetValueOrDefault("ETag") == etag &&
                    row.Response.GetValueOrDefault("Last-Modified") == modified, "static_http_validator_join");
                if (i is not (15 or 16 or 17))
                {
                    int length = i == 13 ? 65536 : row.Body.Length;
                    Need(row.Response.GetValueOrDefault("Content-Length") == length.ToString(CultureInfo.InvariantCulture), "static_http_length");
                    string type = Path.GetExtension(i == 12 ? "/index.html" : row.Path) switch
                    { ".html" => "text/html", ".css" => "text/css", ".js" => "text/javascript", ".json" => "application/json", ".txt" => "text/plain", ".bin" => "application/octet-stream", _ => "" };
                    Need(row.Response.GetValueOrDefault("Content-Type") is { } media && (media == type || media.StartsWith(type + ";", StringComparison.Ordinal)), "static_http_media");
                }
            }
        }
    }

    private static void StaticJsonBody(byte[] bytes, int ordinal)
    {
        Need(bytes.Length <= 4096, "static_http_json_bound");
        using var doc = JsonDocument.Parse(bytes, new() { MaxDepth = 6 }); var value = doc.RootElement;
        if (ordinal == 1)
        { StaticObject(value, "appId", "profile", "webRoot"); Need(value.GetProperty("appId").GetString() == "minimal.api.static-files" && value.GetProperty("profile").GetString() == "v1" && value.GetProperty("webRoot").GetString() == "/app/wwwroot", "static_http_configuration"); }
        else if (ordinal == 2)
        { StaticObject(value, "profile", "ready", "message"); Need(value.GetProperty("profile").GetString() == "v1" && value.GetProperty("ready").GetBoolean() && value.GetProperty("message").GetString() == "CLROS ready \u2014 caf\u00e9", "static_http_json_status"); }
        else
        {
            StaticObject(value, "items"); var items = value.GetProperty("items"); Need(items.ValueKind == JsonValueKind.Array && items.GetArrayLength() == 2, "static_http_items");
            for (int i = 0; i < 2; i++)
            { StaticObject(items[i], "id", "name", "detail"); Need(items[i].GetProperty("id").GetInt32() == i + 1 && items[i].GetProperty("name").GetString() == (i == 0 ? "Blazor" : "FAT32") &&
                (i == 0 ? items[i].GetProperty("detail").GetString() == "Standalone WebAssembly" : items[i].GetProperty("detail").ValueKind == JsonValueKind.Null), "static_http_items"); }
        }
    }

    private static bool StaticDate(string value) => DateTimeOffset.TryParseExact(value, "r", CultureInfo.InvariantCulture,
        DateTimeStyles.AssumeUniversal, out var parsed) && parsed.ToString("r", CultureInfo.InvariantCulture) == value;
    private static bool StaticHeadersEqual(Dictionary<string, string> left, Dictionary<string, string> right) =>
        left.Count == right.Count && left.All(pair => right.TryGetValue(pair.Key, out string? value) && pair.Value == value);
    private static Dictionary<string, string> StaticHeaderList(JsonElement value)
    {
        Need(value.ValueKind == JsonValueKind.Array && value.GetArrayLength() <= 32, "static_http_header_list");
        Dictionary<string, string> result = new(StringComparer.OrdinalIgnoreCase); int total = 0;
        foreach (var row in value.EnumerateArray())
        {
            string line = row.GetString()!; Need(line is { Length: > 0 and <= 1024 } && line.All(c => c is >= ' ' and <= '~'), "static_http_header_list"); total += line.Length;
            Need(total <= 4096, "static_http_header_bound"); QuotaHttpPrefixHeader(line, result);
        }
        return result;
    }
    private static byte[] StaticBase64(string value, int maximum)
    { Need(value is not null && value.Length <= ((maximum + 2) / 3) * 4, "static_http_base64_bound"); byte[] result = Convert.FromBase64String(value!); Need(result.Length <= maximum && Convert.ToBase64String(result) == value, "static_http_base64"); return result; }
    private static void StaticObject(JsonElement value, params string[] names)
    { Need(value.ValueKind == JsonValueKind.Object, "static_http_object"); var actual = value.EnumerateObject().Select(property => property.Name).ToArray(); Need(actual.Length == names.Length && actual.Distinct(StringComparer.Ordinal).Count() == actual.Length && actual.All(name => names.Contains(name, StringComparer.Ordinal)), "static_http_object_fields"); }
}
