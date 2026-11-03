using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using OS.Tooling;

internal static partial class NativeAotStaticFileHttpPacketTests
{
    [HostTest("static HTTP packet join proves all26 persistent matrix responses HEAD ranges304 abort and recovery")]
    private static void Complete()
    {
        foreach (string mode in new[]
        {
            "none",
            "chunked",
            "retry",
            "wrap",
            "abort-full"
        }

        )
        {
            var fixture = Vector(mode);
            var result = Inspect(fixture);
            HostCheck.True(result.Connections == 3 && result.Responses.Count == 26 && result.AbortedGuestPort == 43002 && result.ConcurrentWireOverlap && result.AbortResetRecord > 0 && result.RequestBytes > 0 && result.ResponseBytes >= fixture.Bodies.Values.Sum(body => body.Length), mode + " " + result);
            HostCheck.Equal(2, result.Responses[22].Matches.Count);
            HostCheck.Equal(2, result.Responses[23].Matches.Count);
            HostCheck.True(result.Responses.Where(row => row.Ordinal is not (22 or 23)).All(row => row.Matches.Count == 1));
            var head = result.Responses[13].Matches.Single();
            HostCheck.True(head.BodyBase64 == "" && Encoding.ASCII.GetString(Convert.FromBase64String(head.RequestBase64)).StartsWith("HEAD /boundary.bin HTTP/1.1\r\n", StringComparison.Ordinal));
            HostCheck.Equal(522, Convert.FromBase64String(result.Responses[14].Matches.Single().BodyBase64).Length);
            HostCheck.True(result.Responses[16].Matches.Single().BodyBase64 == "" && result.Responses[17].Matches.Single().BodyBase64 == "");
        }
    }

    [HostTest("static HTTP packet report copies bytes and readonly matches without retaining caller buffers")]
    private static void Copies()
    {
        var fixture = Vector("none");
        byte[] packets = Capture(fixture), before = packets.ToArray();
        var result = Read(fixture, packets);
        HostCheck.True(packets.AsSpan().SequenceEqual(before));
        string body = result.Responses[10].Matches.Single().BodyBase64;
        Array.Clear(packets);
        foreach (byte[] bytes in fixture.Bodies.Values)
            Array.Clear(bytes);
        fixture.Receipt.Clear();
        fixture.Abort.Clear();
        fixture.Frames.Clear();
        HostCheck.Equal(65536, Convert.FromBase64String(body).Length);
        bool outer = false, inner = false;
        try
        {
            ((IList<QemuTranscripts.StaticFileHttpResponseJoin>)result.Responses)[0] = result.Responses[0];
        }
        catch (NotSupportedException)
        {
            outer = true;
        }

        try
        {
            ((IList<QemuTranscripts.StaticFileHttpWireMessage>)result.Responses[0].Matches)[0] = result.Responses[0].Matches[0];
        }
        catch (NotSupportedException)
        {
            inner = true;
        }

        HostCheck.True(outer && inner);
    }

    [HostTest("static HTTP receipt rejects incomplete extra reordered unsafe body hashes and matrix substitutions")]
    private static void ReceiptMutants()
    {
        foreach (string mode in new[]
        {
            "incomplete",
            "missing",
            "extra",
            "ordinal",
            "method",
            "target",
            "status",
            "hash",
            "body-file",
            "request-header",
            "response-header",
            "unknown",
            "frequency",
            "overlap"
        }

        )
        {
            var f = Vector("none");
            var rows = f.Receipt["Responses"]!.AsArray();
            switch (mode)
            {
                case "incomplete":
                    f.Receipt["Complete"] = false;
                    break;
                case "missing":
                    rows.RemoveAt(25);
                    break;
                case "extra":
                    rows.Add(rows[0]!.DeepClone());
                    break;
                case "ordinal":
                    rows[1]!["Ordinal"] = 0;
                    break;
                case "method":
                    rows[13]!["Method"] = "GET";
                    break;
                case "target":
                    rows[18]!["Path"] = "/other.txt";
                    break;
                case "status":
                    rows[16]!["Status"] = 200;
                    break;
                case "hash":
                    rows[10]!["Sha256"] = new string ('0', 64);
                    break;
                case "body-file":
                    rows[0]!["BodyFile"] = "../000.body";
                    break;
                case "request-header":
                    rows[14]!["RequestHeaders"] = new JsonArray("Range: bytes=511-1031");
                    break;
                case "response-header":
                    rows[10]!["ResponseHeaders"]!.AsArray().Add("Date: Fri, 23 Oct 2026 00:00:00 GMT");
                    break;
                case "unknown":
                    f.Receipt["Accepted"] = true;
                    break;
                case "frequency":
                    f.Receipt["ClockFrequency"] = 0;
                    break;
                case "overlap":
                    rows[23]!["Started"] = 2600;
                    break;
            }

            Reject(() => Inspect(f));
        }

        var changed = Vector("none");
        changed.Bodies["010.body"][0] ^= 1;
        Reject(() => Inspect(changed));
        var duplicate = Vector("none");
        byte[] json = Encoding.UTF8.GetBytes(duplicate.Receipt.ToJsonString().Replace("\"Schema\":", "\"Complete\":true,\"Schema\":", StringComparison.Ordinal));
        Reject(() => Read(duplicate, receipt: json));
    }

    [HostTest("static HTTP packets reject method target conditional range entity status and header wire mutants")]
    private static void WireMutants()
    {
        foreach (string mode in new[]
        {
            "request-method",
            "request-target",
            "request-host",
            "request-range",
            "request-condition",
            "response-body",
            "response-status",
            "response-header",
            "range-header",
            "head-body",
            "304-body",
            "length",
            "chunk-size",
            "duplicate-header",
            "bare-lf",
            "dual-framing"
        }

        )
            Reject(() => Inspect(Vector(mode)));
    }

    [HostTest("static HTTP accept-order join rejects speculative pooled and reset handshakes before prior responses")]
    private static void AcceptCausality()
    {
        Reject(() => Inspect(Vector("early-second")));
        Reject(() => Inspect(Vector("early-reset")));
    }

    [HostTest("static HTTP packet join rejects missing extra flows requests responses and unowned quiet SYNs")]
    private static void Inventory()
    {
        foreach (string mode in new[]
        {
            "missing-request",
            "extra-request",
            "extra-response",
            "quiet",
            "unknown-flow"
        }

        )
            Reject(() => Inspect(Vector(mode)));
        var f = Vector("none");
        Reject(() => Read(f, ports: [43000, 43002, 43001]));
        Reject(() => Read(f, ports: [43000, 43001]));
        Reject(() => Read(f, ports: [43000, 43000, 43002]));
    }

    [HostTest("static HTTP packet join retains strict TCP checksums conflicting retransmits sequence and close proof")]
    private static void TcpMutants()
    {
        Reject(() => Inspect(Vector("changed-retry")));
        var checksum = Vector("none");
        int payload = checksum.Frames.FindIndex(frame => frame.Length > 1000);
        checksum.Frames[payload][60] ^= 1;
        Reject(() => Inspect(checksum));
        var missing = Vector("none");
        missing.Frames.RemoveAt(missing.Frames.Count - 1);
        Reject(() => Inspect(missing));
        var forged = Vector("none");
        int response = forged.Frames.FindIndex(frame => frame.Length > 1000);
        byte[] frame = forged.Frames[response];
        forged.Frames[response] = NativeAotSocketIntegrationTests.Tcp(true, 43000, U32(frame, 38) + 1, U32(frame, 42), 0x18, frame.AsSpan(54));
        Reject(() => Inspect(forged));
    }

    [HostTest("static HTTP packet join requires actual concurrent wire overlap FIFO and postreset recovery")]
    private static void Temporal()
    {
        foreach (string mode in new[]
        {
            "no-wire-overlap",
            "pipeline",
            "recovery-before-reset"
        }

        )
            Reject(() => Inspect(Vector(mode)));
    }

    [HostTest("static HTTP abort receipt joins exact request prefix entity and reset without claiming completion")]
    private static void AbortMutants()
    {
        foreach (string mode in new[]
        {
            "abort-body",
            "abort-header",
            "no-reset"
        }

        )
            Reject(() => Inspect(Vector(mode)));
        foreach (string mode in new[]
        {
            "prefix",
            "request",
            "close",
            "time",
            "unknown"
        }

        )
        {
            var f = Vector("none");
            switch (mode)
            {
                case "prefix":
                    f.Abort["Prefix"] = Convert.ToBase64String("HTTP/1.1 404"u8);
                    break;
                case "request":
                    f.Abort["Request"] = Convert.ToBase64String("GET /other HTTP/1.1\r\nHost: 127.0.0.1:5555\r\n\r\n"u8);
                    break;
                case "close":
                    f.Abort["Close"] = "fin";
                    break;
                case "time":
                    f.Abort["Closed"] = 3001;
                    break;
                case "unknown":
                    f.Abort["Success"] = true;
                    break;
            }

            Reject(() => Inspect(f));
        }
    }

    [HostTest("static HTTP pure parser bounds PCAP receipt abort bodies and rejects duplicate JSON fields")]
    private static void Bounds()
    {
        var f = Vector("none");
        Reject(() => Read(f, new byte[4 * 1024 * 1024 + 1]));
        Reject(() => Read(f, receipt: new byte[256 * 1024 + 1]));
        Reject(() => Read(f, abort: new byte[4097]));
        f.Bodies["000.body"] = new byte[65537];
        Reject(() => Inspect(f));
        var extra = Vector("none");
        extra.Bodies.Add("extra.body", []);
        Reject(() => Inspect(extra));
    }

    private static QemuTranscripts.StaticFileHttpPacketInspection Inspect(Fixture f) => Read(f);
    private static QemuTranscripts.StaticFileHttpPacketInspection Read(Fixture f, byte[]? packets = null, uint[]? ports = null, byte[]? receipt = null, byte[]? abort = null) => QemuTranscripts.InspectNativeAotStaticFileHttpPackets(packets ?? Capture(f), f.Anchor, f.Arp, ports ?? f.Ports, receipt ?? Encoding.UTF8.GetBytes(f.Receipt.ToJsonString()), f.Bodies, abort ?? Encoding.UTF8.GetBytes(f.Abort.ToJsonString()));
    private static byte[] Capture(Fixture f) => NativeAotSocketIntegrationTests.Capture(f.Frames);
    private static uint U32(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset));
    private static void Reject(Action action)
    {
        try
        {
            action();
        }
        catch (Exception error)when (error is ToolFailure or InvalidDataException or System.Text.Json.JsonException or FormatException)
        {
            return;
        }

        throw new InvalidOperationException("expected strict static HTTP rejection");
    }

    private static Fixture Vector(string mode)
    {
        var original = NativeAotSocketIntegrationTests.Packets();
        var frames = original.Frames.Take(8).Select(frame => frame.ToArray()).ToList();
        JsonArray rows = [];
        Dictionary<string, byte[]> bodies = [];
        var assets = NativeAotStaticFilePackage.StagedAssets();
        string date = new DateTimeOffset(2026, 10, 23, 0, 0, 0, TimeSpan.Zero).ToString("r", CultureInfo.InvariantCulture), modified = "Tue, 01 Jan 1980 00:00:00 GMT", etag = "\"abc123\"";
        byte[] large = assets[5].Bytes;
        string[] paths = ["/healthz", "/config", "/api/status", "/api/items", "/api/items?empty=true", ..assets.Select(asset => "/" + asset.Path[8..]), "/", "/boundary.bin", "/boundary.bin", "/boundary.bin", "/boundary.bin", "/boundary.bin", "/missing.txt", "/appsettings.json", "/index.html.exe", "/one.bin", "/boundary.bin", "/boundary.bin", "/healthz", "/boundary.bin"];
        List<byte[]> requests = [], responses = [];
        for (int i = 0; i < 26; i++)
        {
            string method = i == 13 ? "HEAD" : i == 21 ? "POST" : "GET";
            int status = i == 14 ? 206 : i == 15 ? 416 : i is 16 or 17 ? 304 : i is >= 18 and <= 21 ? 404 : 200;
            byte[] body = i is 0 or 24 ? "OS_MINIMAL_API_OK"u8.ToArray() : i == 1 ? "{\"appId\":\"minimal.api.static-files\",\"profile\":\"v1\",\"webRoot\":\"/app/wwwroot\"}"u8.ToArray() : i == 2 ? Encoding.UTF8.GetBytes("{\"profile\":\"v1\",\"ready\":true,\"message\":\"CLROS ready \u2014 caf\u00e9\"}") : i == 3 ? "{\"items\":[{\"id\":1,\"name\":\"Blazor\",\"detail\":\"Standalone WebAssembly\"},{\"id\":2,\"name\":\"FAT32\",\"detail\":null}]}"u8.ToArray() : i == 4 ? "{\"items\":[]}"u8.ToArray() : i is >= 5 and <= 11 ? assets[i - 5].Bytes : i == 12 ? assets[0].Bytes : i == 14 ? large.AsSpan(510, 522).ToArray() : i is 22 or 23 or 25 ? large : [];
            string name = i.ToString("D3", CultureInfo.InvariantCulture) + ".body";
            bodies.Add(name, body.ToArray());
            List<string> requestHeaders = [];
            if (i is 14 or 15)
                requestHeaders.Add("Range: " + (i == 14 ? "bytes=510-1031" : "bytes=65536-"));
            if (i == 16)
                requestHeaders.Add("If-None-Match: " + etag);
            if (i == 17)
                requestHeaders.Add("If-Modified-Since: " + modified);
            bool chunked = (mode is "chunked" or "chunk-size") && (i is 1 or 2 or 3);
            List<string> responseHeaders = ["Date: " + date, "Server: Kestrel"];
            if (i is not (16 or 17))
                responseHeaders.Add(chunked ? "Transfer-Encoding: chunked" : "Content-Length: " + (i == 13 ? 65536 : body.Length).ToString(CultureInfo.InvariantCulture));
            if (i is >= 5 and <= 17 or 22 or 23 or 25)
            {
                responseHeaders.Add("Cache-Control: no-store");
                if (i != 15)
                {
                    responseHeaders.Add("Accept-Ranges: bytes");
                    responseHeaders.Add("ETag: " + etag);
                    responseHeaders.Add("Last-Modified: " + modified);
                }

                if (i == 14)
                    responseHeaders.Add("Content-Range: bytes 510-1031/65536");
                if (i == 15)
                    responseHeaders.Add("Content-Range: bytes */65536");
            }

            string type = i is 0 or 24 ? "text/plain" : i is >= 1 and <= 4 ? "application/json; charset=utf-8" : Path.GetExtension(i == 12 ? "/index.html" : paths[i]) switch
            {
                ".html" => "text/html",
                ".css" => "text/css",
                ".js" => "text/javascript",
                ".json" => "application/json",
                ".txt" => "text/plain",
                _ => "application/octet-stream"
            };
            if (i is not (15 or 18 or 19 or 20 or 21))
                responseHeaders.Add("Content-Type: " + type);
            long start = i < 22 ? 100 + 100 * i : i == 22 ? 2300 : i == 23 ? 2310 : i == 24 ? 2900 : 3000, end = i == 22 ? 2550 : i == 23 ? 2650 : start + 50;
            rows.Add(new JsonObject { ["Ordinal"] = i, ["Method"] = method, ["Path"] = paths[i], ["Status"] = status, ["RequestHeaders"] = new JsonArray(requestHeaders.Select(value => (JsonNode? )JsonValue.Create(value)).ToArray()), ["ResponseHeaders"] = new JsonArray(responseHeaders.Select(value => (JsonNode? )JsonValue.Create(value)).ToArray()), ["Bytes"] = body.Length, ["Sha256"] = Convert.ToHexString(SHA256.HashData(body)), ["BodyFile"] = name, ["Started"] = start, ["Finished"] = end });
            string wireMethod = i == 13 && mode == "request-method" ? "GET" : method, wirePath = i == 18 && mode == "request-target" ? "/other.txt" : paths[i], host = i == 0 && mode == "request-host" ? "elsewhere:5555" : "127.0.0.1:5555";
            var wireRequestHeaders = requestHeaders.ToList();
            if (i == 14 && mode == "request-range")
                wireRequestHeaders[0] = "Range: bytes=511-1031";
            if (i == 16 && mode == "request-condition")
                wireRequestHeaders[0] = "If-None-Match: \"wrong\"";
            requests.Add(Encoding.ASCII.GetBytes($"{wireMethod} {wirePath} HTTP/1.1\r\nHost: {host}\r\n" + string.Concat(wireRequestHeaders.Select(value => value + "\r\n")) + (method == "POST" ? "Content-Length: 0\r\n" : "") + "\r\n"));
            var wireHeaders = responseHeaders.ToList();
            byte[] entity = body.ToArray();
            if (i == 10 && mode == "response-body")
                entity[17] ^= 1;
            if (i == 13 && mode == "head-body" || i == 16 && mode == "304-body")
                entity = [0x61];
            if (i == 10 && mode == "response-header")
                wireHeaders.Add("X-Extra: mutant");
            if (i == 14 && mode == "range-header")
                wireHeaders[wireHeaders.FindIndex(value => value.StartsWith("Content-Range:", StringComparison.Ordinal))] = "Content-Range: bytes 511-1032/65536";
            if (i == 10 && mode == "length")
                wireHeaders[wireHeaders.FindIndex(value => value.StartsWith("Content-Length:", StringComparison.Ordinal))] = "Content-Length: 65535";
            if (i == 0 && mode == "duplicate-header")
                wireHeaders.Add("Date: " + date);
            if (i == 0 && mode == "bare-lf")
                wireHeaders.Add("X: a\nb");
            if (i == 0 && mode == "dual-framing")
                wireHeaders.Add("Transfer-Encoding: chunked");
            if (chunked)
                entity = [..Encoding.ASCII.GetBytes((i == 1 && mode == "chunk-size" ? "g" : body.Length.ToString("x", CultureInfo.InvariantCulture)) + "\r\n"), ..body, .."\r\n0\r\n\r\n"u8.ToArray()];
            int wireStatus = i == 0 && mode == "response-status" ? 404 : status;
            string reason = wireStatus switch
            {
                200 => "OK",
                206 => "Partial Content",
                304 => "Not Modified",
                404 => "Not Found",
                _ => "Range Not Satisfiable"
            };
            responses.Add([..Encoding.ASCII.GetBytes($"HTTP/1.1 {wireStatus} {reason}\r\n" + string.Concat(wireHeaders.Select(value => value + "\r\n")) + "\r\n"), ..entity]);
        }

        uint client = mode == "wrap" ? uint.MaxValue - 1000 : 1000, server = mode == "wrap" ? uint.MaxValue - 500 : 2000;
        Flow firstFlow = new(43000, client, server), second = new(43001, 100000, 200000), reset = new(43002, 300000, 400000);
        void Add(Flow flow, bool outgoing, uint seq, uint ack, byte flags, ReadOnlySpan<byte> bytes = default) => frames.Add(NativeAotSocketIntegrationTests.Tcp(outgoing, flow.Port, seq, ack, flags, bytes));
        void Open(Flow flow)
        {
            Add(flow, false, flow.Client, 0, 2);
            flow.Client++;
            Add(flow, true, flow.Server, flow.Client, 0x12);
            flow.Server++;
            Add(flow, false, flow.Client, flow.Server, 0x10);
        }

        void Send(Flow flow, byte[] request)
        {
            Add(flow, false, flow.Client, flow.Server, 0x18, request);
            flow.Client = unchecked(flow.Client + (uint)request.Length);
            Add(flow, true, flow.Server, flow.Client, 0x10);
        }

        void Respond(Flow flow, byte[] response)
        {
            for (int at = 0; at < response.Length; at += 1024)
            {
                int count = Math.Min(1024, response.Length - at);
                var part = response.AsSpan(at, count);
                Add(flow, true, flow.Server, flow.Client, 0x18, part);
                if (mode is "retry" or "changed-retry")
                {
                    byte[] retry = part.ToArray();
                    if (mode == "changed-retry" && at == 0)
                        retry[^1] ^= 1;
                    Add(flow, true, flow.Server, flow.Client, 0x18, retry);
                }

                flow.Server = unchecked(flow.Server + (uint)count);
                Add(flow, false, flow.Client, flow.Server, 0x10);
            }
        }

        void Close(Flow flow)
        {
            Add(flow, false, flow.Client, flow.Server, 0x11);
            flow.Client++;
            Add(flow, true, flow.Server, flow.Client, 0x10);
            Add(flow, true, flow.Server, flow.Client, 0x11);
            flow.Server++;
            Add(flow, false, flow.Client, flow.Server, 0x10);
        }

        if (mode == "quiet")
            frames.Add(NativeAotSocketIntegrationTests.Tcp(false, 42999, 1, 0, 2, []));
        Open(firstFlow);
        if (mode == "early-second")
            Open(second);
        if (mode == "early-reset")
            Open(reset);
        for (int i = 0; i < 22; i++)
        {
            if (i == 21 && mode == "missing-request")
                continue;
            if (i == 0 && mode == "pipeline")
            {
                Send(firstFlow, requests[0]);
                Send(firstFlow, requests[1]);
                Respond(firstFlow, responses[0]);
                Respond(firstFlow, responses[1]);
                i++;
                continue;
            }

            Send(firstFlow, requests[i]);
            Respond(firstFlow, responses[i]);
        }

        Send(firstFlow, requests[22]);
        if (mode == "no-wire-overlap")
            Respond(firstFlow, responses[22]);
        if (mode != "early-second")
            Open(second);
        Send(second, requests[23]);
        if (mode != "no-wire-overlap")
            Respond(firstFlow, responses[22]);
        Respond(second, responses[23]);
        if (mode != "early-reset")
            Open(reset);
        byte[] abortRequest = Encoding.ASCII.GetBytes("GET /boundary.bin HTTP/1.1\r\nHost: 127.0.0.1:5555\r\nConnection: keep-alive\r\n\r\n");
        Send(reset, abortRequest);
        int headLength = responses[10].AsSpan().IndexOf("\r\n\r\n"u8) + 4;
        byte[] abortResponse = responses[10].AsSpan(0, headLength + (mode == "abort-full" ? 65536 : 300)).ToArray();
        if (mode == "abort-body")
            abortResponse[^1] ^= 1;
        if (mode == "abort-header")
            abortResponse[0] = (byte)'X';
        Respond(reset, abortResponse);
        int resetAt = frames.Count;
        if (mode != "no-reset")
            Add(reset, false, reset.Client, reset.Server, 0x14);
        for (int i = 24; i < 26; i++)
        {
            Send(firstFlow, requests[i]);
            Respond(firstFlow, responses[i]);
        }

        if (mode == "recovery-before-reset")
        {
            var packet = frames[resetAt];
            frames.RemoveAt(resetAt);
            frames.Insert(frames.Count - 1, packet);
        }

        if (mode == "extra-request")
        {
            Send(second, requests[0]);
            Respond(second, responses[0]);
        }

        if (mode == "extra-response")
            Respond(second, responses[0]);
        if (mode == "unknown-flow")
            frames.Add(NativeAotSocketIntegrationTests.Tcp(false, 43003, 1, 0, 2, []));
        Close(firstFlow);
        Close(second);
        return new(frames, original.Anchor, original.Arp, [43000, 43001, 43002], new() { ["Schema"] = "clros-staged-http-v1", ["Port"] = 5555, ["Complete"] = true, ["ClockFrequency"] = 1000, ["Responses"] = rows }, bodies, new() { ["LocalPort"] = 61234, ["Request"] = Convert.ToBase64String(abortRequest), ["Prefix"] = Convert.ToBase64String(abortResponse.AsSpan(0, 128)), ["Started"] = 2700, ["Closed"] = 2800, ["Close"] = "reset" });
    }
}
