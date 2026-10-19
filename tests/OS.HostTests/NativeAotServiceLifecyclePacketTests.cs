using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using OS.Tooling;

internal static class NativeAotServiceLifecyclePacketTests
{
    private const string Prefix = "/__clros/service/lifecycle/", Health = "CLROS_SERVICE_HEALTH_OK";
    private const string Terminal = "CLROS_LIFECYCLE_STATUS active=0 waiting=0 waits=2 canceled=2 echoes=1 streams=1 rejected=1 read=33792 written=524288";
    private static readonly DateTimeOffset Epoch = new(2026, 10, 18, 0, 0, 0, TimeSpan.Zero);

    [HostTest("service lifecycle packets reconstruct independent segmented echo stream resets closes retransmissions and sequence wrap")]
    private static void Complete()
    {
        foreach (string mode in new[] { "plain", "segmented", "retransmit", "wrap" })
        {
            var fixture = Vector(mode); byte[] raw = NativeAotSocketIntegrationTests.Capture(fixture.Frames), before = raw.ToArray();
            var result = QemuTranscripts.InspectNativeAotServiceLifecyclePacketsForTests(raw, fixture.Anchor, fixture.Arp, fixture.Ports, fixture.Report);
            HostCheck.Equal(39, result.Connections); HostCheck.Equal(29, result.LifecycleConnections);
            HostCheck.Equal(2, result.ClientResets); HostCheck.Equal(37, result.ServerActiveCloses); HostCheck.Equal(37, result.CompleteCloses);
            HostCheck.True(result.RequestBytes > 65536 && result.ResponseBytes > 524288 && raw.AsSpan().SequenceEqual(before));
            HostCheck.True(fixture.Frames.Skip(8).All(frame => frame.Length <= 1254));
            Array.Fill(raw, (byte)0); HostCheck.Equal(39, result.Connections);
        }
    }

    [HostTest("service lifecycle packets reject changed echo stream terminal counters paths dates and premature stream completion")]
    private static void ApplicationControls()
    {
        foreach (string mode in new[] { "echo", "bad-echo", "stream", "terminal", "rewind", "path", "date", "early-stream" })
            Reject(Vector(mode));
    }

    [HostTest("service lifecycle packets require both exact client resets and both acknowledgments of ordinary server active FINs")]
    private static void LifetimeControls()
    {
        foreach (string mode in new[] { "missing-reset", "reset-sequence", "reset-ack", "missing-fin-ack" }) Reject(Vector(mode));
    }

    [HostTest("service lifecycle packets reject byte gaps changed retransmissions checksum corruption and substituted ports or v1 reports")]
    private static void WireControls()
    {
        foreach (string mode in new[] { "gap", "changed-retry" }) Reject(Vector(mode));
        var fixture = Vector("plain"); var corrupt = fixture.Frames.Select(frame => frame.ToArray()).ToList(); corrupt[^1][40] ^= 1;
        Reject(fixture with { Frames = corrupt });
        Reject(fixture with { Ports = fixture.Ports.Reverse().ToArray() });
        Reject(fixture with { Report = NativeAotServicePacketTests.Peer().ToJsonString() });
    }

    private sealed record Fixture(List<byte[]> Frames, uint Anchor, QemuArpReceiveReceipt Arp, uint[] Ports, string Report);

    // Each vector is invented host input. The independent Ethernet/TCP helpers
    // supply wire framing/checksums; no app, native or guest evidence is implied.
    private static Fixture Vector(string mode)
    {
        var original = NativeAotSocketIntegrationTests.Packets(); var frames = original.Frames.Take(8).ToList(); List<uint> ports = [];
        var report = NativeAotServicePacketTests.Peer(); report["schema"] = "clros.nativeaot.service.peer.v2";
        report["lifecycle"] = new JsonObject { ["port"] = 8080, ["sequentialConnections"] = 12, ["echoBytes"] = 32768,
            ["echoHash"] = 1653448133L, ["streamBytes"] = 524288,
            ["streamSha256"] = "0A131BA05D7CBD5F28DD2FAB7DA2B8BECE4122EB1A76FDA0D0159C66B520F8A1",
            ["resetRequests"] = 1, ["timeoutRequests"] = 1, ["holdStartedTicks"] = 100L,
            ["healthyCompletedTicks"] = 200L, ["holdReleasedTicks"] = 300L, ["terminalStatus"] = Terminal };
        var ordinary = report["exchanges"]!.AsArray();
        Flow Open(string method, string path, byte[]? body = null)
        {
            ushort port = checked((ushort)(45000 + ports.Count)); ports.Add(port);
            var flow = new Flow(frames, port, mode); body ??= [];
            byte[] request = [..Encoding.ASCII.GetBytes(method + " " + path + " HTTP/1.1\r\nHost: clros.service\r\nConnection: close\r\n" +
                (method == "POST" ? "Content-Length: " + body.Length.ToString(CultureInfo.InvariantCulture) + "\r\n" : "") + "\r\n"), ..body];
            flow.Send(false, request); return flow;
        }
        void Ordinary(int index)
        {
            var row = ordinary[index]!; var flow = Open(row["method"]!.GetValue<string>(), row["path"]!.GetValue<string>());
            var date = DateTimeOffset.ParseExact(row["dateUtc"]!.GetValue<string>(), "O", CultureInfo.InvariantCulture);
            flow.Send(true, Response(Convert.FromBase64String(row["bodyBase64"]!.GetValue<string>()), row["status"]!.GetValue<int>(), date));
            flow.Close();
        }
        Flow Exchange(string path, string body, int status = 200, string method = "GET", byte[]? request = null, bool initial = false)
        {
            var flow = Open(method, path, request);
            flow.Send(true, Response(Encoding.ASCII.GetBytes(body), status, initial && mode == "date" ? Epoch.AddSeconds(1) : Epoch));
            flow.Close(omitFinalAck: initial && mode == "missing-fin-ack"); return flow;
        }
        void Healthy(bool first = false) => Exchange(first && mode == "path" ? "/unknown" : "/healthz", Health);
        void Status(int active, int waiting, int waits, int canceled, int echoes, int streams, int rejected, int read, int written, bool initial = false) =>
            Exchange(Prefix + "status", string.Create(CultureInfo.InvariantCulture,
                $"CLROS_LIFECYCLE_STATUS active={active} waiting={waiting} waits={waits} canceled={canceled} echoes={echoes} streams={streams} rejected={rejected} read={read} written={written}"), initial: initial);

        for (int i = 0; i < 6; i++) Ordinary(i);
        Status(0, 0, 0, 0, 0, 0, 0, 0, 0, initial: true);
        for (int i = 0; i < 12; i++) Healthy(first: i == 0);
        byte[] echo = Pattern(32768); if (mode == "echo") echo[1000] ^= 1;
        Exchange(Prefix + "echo", "CLROS_LIFECYCLE_ECHO_OK bytes=32768 hash=628d9dc5", method: "POST", request: echo);
        byte[] bad = Pattern(32768); bad[31] ^= 1; if (mode == "bad-echo") bad[32] ^= 1;
        Exchange(Prefix + "echo", "CLROS_LIFECYCLE_BAD_BODY", 400, "POST", bad); Healthy();

        var stream = Open("GET", Prefix + "stream"); byte[] streamBody = Pattern(524288); if (mode == "stream") streamBody[10000] ^= 1;
        byte[] response = Response(streamBody, 200, Epoch); int heldBytes = response.Length - streamBody.Length + 1024;
        stream.Send(true, response.AsSpan(0, heldBytes), duplicate: mode is "retransmit" or "changed-retry", changedRetry: mode == "changed-retry");
        Status(1, 0, 0, 0, 1, 0, 1, 33792, 1024);
        if (mode == "early-stream") { stream.Send(true, response.AsSpan(heldBytes)); stream.Close(); }
        Healthy(); Status(1, 0, 0, 0, 1, 0, 1, 33792, mode == "rewind" ? 0 : 1024);
        if (mode != "early-stream") { stream.Send(true, response.AsSpan(heldBytes), gap: mode == "gap"); stream.Close(); }

        for (int attempt = 1; attempt <= 2; attempt++)
        {
            var cancel = Open("GET", Prefix + "cancel");
            cancel.Send(true, Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\nConnection: close\r\nServer: Kestrel\r\nDate: " + Epoch.ToString("r", CultureInfo.InvariantCulture) + "\r\n\r\n"));
            Status(1, 1, attempt, attempt - 1, 1, 1, 1, 33792, 524288);
            if (!(attempt == 1 && mode == "missing-reset")) cancel.Reset(attempt == 1 && mode == "reset-sequence", attempt == 1 && mode == "reset-ack");
            Status(0, 0, attempt, attempt, 1, 1, 1, 33792, 524288); Healthy();
        }
        Status(0, 0, 2, 2, 1, 1, 1, mode == "terminal" ? 33793 : 33792, 524288);
        for (int i = 6; i < ordinary.Count; i++) Ordinary(i);
        return new(frames, original.Anchor, original.Arp, ports.ToArray(), report.ToJsonString());
    }

    private sealed class Flow
    {
        private readonly List<byte[]> frames;
        private readonly ushort port;
        private readonly string mode;
        private uint client, server;

        internal Flow(List<byte[]> frames, ushort port, string mode)
        {
            this.frames = frames; this.port = port; this.mode = mode;
            client = mode == "wrap" ? uint.MaxValue - 10 : (uint)(1000 + (port - 45000) * 10000);
            server = mode == "wrap" ? uint.MaxValue - 20 : (uint)(2000 + (port - 45000) * 10000);
            Add(false, client, 0, 2); Add(true, server, unchecked(client + 1), 0x12);
            client = unchecked(client + 1); server = unchecked(server + 1); Add(false, client, server, 0x10);
        }

        private void Add(bool outgoing, uint sequence, uint ack, byte flags, ReadOnlySpan<byte> bytes = default) =>
            frames.Add(NativeAotSocketIntegrationTests.Tcp(outgoing, port, sequence, ack, flags, bytes));

        internal void Send(bool outgoing, ReadOnlySpan<byte> bytes, bool duplicate = false, bool changedRetry = false, bool gap = false)
        {
            for (int at = 0; at < bytes.Length;)
            {
                int count = Math.Min(mode == "segmented" && at == 0 ? 7 : mode == "segmented" ? 317 : 1200, bytes.Length - at);
                uint sequence = outgoing ? server : client, ack = outgoing ? client : server;
                var part = bytes.Slice(at, count);
                if (!(gap && at == 0)) Add(outgoing, sequence, ack, 0x18, part);
                if (duplicate && at == 0)
                {
                    byte[] retry = part.ToArray(); if (changedRetry) retry[^1] ^= 1;
                    Add(outgoing, sequence, ack, 0x18, retry);
                }
                if (outgoing) server = unchecked(server + (uint)count); else client = unchecked(client + (uint)count);
                Add(!outgoing, outgoing ? client : server, outgoing ? server : client, 0x10); at += count;
            }
        }

        internal void Close(bool omitFinalAck = false)
        {
            Add(true, server, client, 0x11); server = unchecked(server + 1); Add(false, client, server, 0x10);
            Add(false, client, server, 0x11); client = unchecked(client + 1);
            if (!omitFinalAck) Add(true, server, client, 0x10);
        }

        internal void Reset(bool wrongSequence, bool wrongAck) =>
            Add(false, unchecked(client + (wrongSequence ? 1U : 0)), unchecked(server + (wrongAck ? 1U : 0)), 0x14);
    }

    private static byte[] Pattern(int bytes) => Enumerable.Range(0, bytes).Select(i => unchecked((byte)(i * 17 + 31))).ToArray();

    private static byte[] Response(byte[] body, int status, DateTimeOffset date) => [..Encoding.ASCII.GetBytes(
        "HTTP/1.1 " + status.ToString(CultureInfo.InvariantCulture) + " " + (status == 200 ? "OK" : status == 400 ? "Bad Request" : "Conflict") +
        "\r\nContent-Length: " + body.Length.ToString(CultureInfo.InvariantCulture) + "\r\nConnection: close\r\nServer: Kestrel\r\nDate: " + date.ToString("r", CultureInfo.InvariantCulture) + "\r\n\r\n"), ..body];

    private static void Reject(Fixture fixture)
    {
        bool refused = false;
        try { _ = QemuTranscripts.InspectNativeAotServiceLifecyclePacketsForTests(NativeAotSocketIntegrationTests.Capture(fixture.Frames),
            fixture.Anchor, fixture.Arp, fixture.Ports, fixture.Report); }
        catch (Exception error) when (error is ToolFailure or InvalidDataException) { refused = true; }
        HostCheck.True(refused);
    }
}
