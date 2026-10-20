using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using OS.Tooling;

internal static class NativeAotServicePacketTests
{
    [HostTest("service HTTP packet reader joins ten synthetic exchanges exact bodies dates active closes retransmissions and wrap")]
    private static void Complete()
    {
        foreach (string mode in new[] { "none", "chunked", "retransmit", "wrap", "quiet" })
        {
            var report = Inspect(Vector(mode));
            HostCheck.True(report.Connections == 10 && report.ServerActiveCloses == 10 && report.CompleteCloses == 10 &&
                report.RequestBytes > 500 && report.ResponseBytes > 1000 && report.QuietSyns == (mode == "quiet" ? 1 : 0));
        }
    }

    [HostTest("service HTTP packets reject changed request response framing date retransmissions and incomplete active close")]
    private static void Substitutions()
    {
        foreach (string mode in new[] { "method", "host", "request-body", "body", "status", "date", "length", "chunk-size", "duplicate-header",
            "bare-lf", "bare-cr", "changed-retry", "early-fin", "missing-ack", "reset", "unknown-tuple", "reordered" }) Reject(() => Inspect(Vector(mode)));
        var f = Vector("none");
        Reject(() => Inspect(f with { Ports = f.Ports.Reverse().ToArray() }));
        Reject(() => Inspect(f with { Ports = [..f.Ports.Skip(1)] }));
        Reject(() => Inspect(f with { Report = f.Report.Replace("CLROS_SERVICE_HEALTH_OK", "OTHER", StringComparison.Ordinal) + "{}" }));
        var corrupt = f.Frames.Select(frame => frame.ToArray()).ToList(); corrupt[^1][40] ^= 1;
        Reject(() => Inspect(f with { Frames = corrupt }));
    }

    private sealed record PacketFixture(List<byte[]> Frames, uint Anchor, QemuArpReceiveReceipt Arp, uint[] Ports, string Report);
    private static QemuTranscripts.ServiceHttpPacketInspection Inspect(PacketFixture f) => QemuTranscripts.InspectNativeAotServiceHttpPacketsForTests(
        NativeAotSocketIntegrationTests.Capture(f.Frames), f.Anchor, f.Arp, f.Ports, f.Report);

    private static PacketFixture Vector(string mode)
    {
        var original = NativeAotSocketIntegrationTests.Packets(); var frames = original.Frames.Take(8).ToList();
        var report = Peer(); var exchanges = report["exchanges"]!.AsArray(); uint[] ports = Enumerable.Range(0, exchanges.Count).Select(i => (uint)(43000 + i)).ToArray();
        if (mode == "quiet") frames.Add(NativeAotSocketIntegrationTests.Tcp(false, 42999, 555, 0, 2, []));
        for (int i = 0; i < exchanges.Count; i++)
        {
            var exchange = exchanges[i]!; ushort port = (ushort)ports[i];
            uint client = mode == "wrap" ? uint.MaxValue - 10 : (uint)(1000 + i * 10000), server = mode == "wrap" ? uint.MaxValue - 20 : (uint)(2000 + i * 10000);
            void Add(bool outgoing, uint sequence, uint ack, byte flags, ReadOnlySpan<byte> data = default) =>
                frames.Add(NativeAotSocketIntegrationTests.Tcp(outgoing, port, sequence, ack, flags, data));
            Add(false, client, 0, 2); Add(true, server, unchecked(client + 1), 0x12); client++; server++; Add(false, client, server, 0x10);
            string method = exchange["method"]!.GetValue<string>(), path = exchange["path"]!.GetValue<string>();
            if (i == 3 && mode == "method") method = "GET";
            string host = i == 0 && mode == "host" ? "other.service" : "clros.service";
            byte[] request = Encoding.ASCII.GetBytes($"{method} {path} HTTP/1.1\r\nHost: {host}\r\nConnection: close\r\n" +
                (method == "POST" ? "Content-Length: 0\r\n" : "") + (i == 0 && mode == "duplicate-header" ? "Host: clros.service\r\n" : "") + "\r\n" +
                (i == 0 && mode == "request-body" ? "x" : ""));
            Add(false, client, server, 0x18, request); client += (uint)request.Length; Add(true, server, client, 0x10);
            byte[] body = Convert.FromBase64String(exchange["bodyBase64"]!.GetValue<string>());
            if (i == 0 && mode == "body") body[0] ^= 1;
            int status = exchange["status"]!.GetValue<int>(); if (i == 0 && mode == "status") status = 409;
            var date = DateTimeOffset.ParseExact(exchange["dateUtc"]!.GetValue<string>(), "O", CultureInfo.InvariantCulture);
            if (i == 0 && mode == "date") date = date.AddSeconds(1);
            bool chunked = mode is "chunked" or "chunk-size";
            string framing = chunked ? "Transfer-Encoding: chunked\r\n" : "Content-Length: " + (body.Length - (i == 0 && mode == "length" ? 1 : 0)) + "\r\n";
            byte[] entity = chunked && body.Length != 0 ? [..Encoding.ASCII.GetBytes((i == 0 && mode == "chunk-size" ? "g" : body.Length.ToString("x", CultureInfo.InvariantCulture)) + "\r\n"),
                ..body, .."\r\n0\r\n\r\n"u8.ToArray()] : chunked ? "0\r\n\r\n"u8.ToArray() : body;
            string extra = i == 0 && mode == "bare-lf" ? "X: a\nb\r\n" : i == 0 && mode == "bare-cr" ? "X: a\rb\r\n" : "";
            byte[] response = [..Encoding.ASCII.GetBytes($"HTTP/1.1 {status} {(status == 200 ? "OK" : "Conflict")}\r\n{framing}Connection: close\r\nServer: Kestrel\r\nDate: {date:r}\r\n{extra}\r\n"), ..entity];
            if (i == 0 && mode == "early-fin") { Add(false, client, server, 0x11); client++; Add(true, server, client, 0x10); }
            if (i == 0 && mode == "unknown-tuple") frames.Add(NativeAotSocketIntegrationTests.Tcp(false, 42998, client, server, 0x10, []));
            if (i == 0 && mode == "reset") Add(false, client, server, 0x14);
            Add(true, server, client, 0x18, response);
            if (mode is "retransmit" or "changed-retry")
            { var retry = response.ToArray(); if (i == 0 && mode == "changed-retry") retry[^1] ^= 1; Add(true, server, client, 0x18, retry); }
            server += (uint)response.Length; Add(false, client, server, 0x10); Add(true, server, client, 0x11); server++;
            Add(false, client, server, 0x10);
            if (!(i == 0 && mode == "early-fin")) { Add(false, client, server, 0x11); client++; }
            if (!(i == exchanges.Count - 1 && mode == "missing-ack")) Add(true, server, client, 0x10);
        }
        if (mode == "reordered") (frames[11], frames[12]) = (frames[12], frames[11]);
        return new(frames, original.Anchor, original.Arp, ports, report.ToJsonString());
    }

    internal static JsonObject Peer()
    {
        string Status(int phase, int worker, long calls, long elapsed) => new JsonObject { ["contract"] = "clros.service.v1", ["phase"] = phase,
            ["failure"] = 0, ["managedWorkerId"] = worker, ["calls"] = calls, ["randomRequests"] = phase == 0 ? 0 : 1,
            ["randomBytes"] = phase == 0 ? 0 : 8192, ["iterations"] = phase == 0 ? 0 : 1, ["elapsedMilliseconds"] = elapsed }.ToJsonString();
        var rows = new JsonArray();
        void Add(string method, string path, int status, long start, long end, string body) => rows.Add(new JsonObject { ["index"] = rows.Count,
            ["method"] = method, ["path"] = path, ["status"] = status, ["startedMilliseconds"] = start, ["completedMilliseconds"] = end,
            ["dateUtc"] = new DateTimeOffset(2026, 10, 18, 0, 0, 0, TimeSpan.Zero).AddSeconds(end / 1000).ToString("O", CultureInfo.InvariantCulture),
            ["bodyBase64"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(body)) });
        const string path = "/__clros/service/";
        Add("GET", "/healthz", 200, 0, 10, "CLROS_SERVICE_HEALTH_OK"); Add("GET", path + "status", 200, 10, 20, Status(0, 0, 0, 0));
        Add("POST", path + "stop", 409, 20, 30, ""); Add("POST", path + "start", 200, 30, 40, "CLROS_SERVICE_STARTED");
        Add("POST", path + "start", 409, 40, 50, ""); Add("POST", path + "stop", 409, 50, 60, "");
        Add("GET", path + "status", 200, 60, 70, Status(1, 7, 1, 1)); Add("GET", path + "status", 200, 300050, 300060, Status(1, 7, 4097, 300001));
        Add("GET", "/healthz", 200, 300070, 300080, "CLROS_SERVICE_HEALTH_OK"); Add("POST", path + "stop", 200, 300080, 300090, "CLROS_SERVICE_STOPPED");
        return new() { ["schema"] = "clros.nativeaot.service.peer.v1", ["port"] = 8080, ["durationMilliseconds"] = 300100,
            ["connectAttempts"] = 1, ["exchanges"] = rows };
    }

    private static void Reject(Action action)
    {
        bool refused = false; try { action(); } catch (Exception error) when (error is ToolFailure or InvalidDataException) { refused = true; }
        HostCheck.True(refused);
    }
}
