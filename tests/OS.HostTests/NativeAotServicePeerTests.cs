using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using OS.Tooling;

internal static class NativeAotServicePeerTests
{
    [HostTest("service peer report binds ordered HTTP refusals progress duration and stop response without guest authority")]
    private static void Complete()
    {
        string text = Report().ToJsonString(); var result = NativeAotServicePeer.InspectOutput(text);
        HostCheck.Equal(8080, result.Port); HostCheck.Equal(10, result.Exchanges); HostCheck.Equal(2, result.StatusSamples);
        HostCheck.Equal(300100L, result.DurationMilliseconds); HostCheck.Equal(4097L, result.Calls); HostCheck.Equal(8192L, result.RandomBytes);
        HostCheck.Equal(text, Report().ToJsonString());
        var retries = Report(); retries["connectAttempts"] = 400;
        Shift(retries, 399000); HostCheck.Equal(699100L, NativeAotServicePeer.InspectOutput(retries.ToJsonString()).DurationMilliseconds);
    }

    [HostTest("service peer rejects malformed unknown duplicate typed ranged and oversized reports")]
    private static void Schema()
    {
        foreach (string text in new[] { "", "null", "[]", "{}", Report().ToJsonString() + "{}", new string(' ', 65537),
            Report().ToJsonString().Replace("\"port\":8080", "\"port\":8080,\"port\":8080", StringComparison.Ordinal),
            Report().ToJsonString().Replace("\"port\":8080", "\"port\":8080,\"\\u0070ort\":8080", StringComparison.Ordinal) }) Reject(text);
        foreach (Action<JsonObject> alter in new Action<JsonObject>[] {
            r => r["extra"] = 1, r => r.Remove("port"), r => r["schema"] = "other", r => r["port"] = 0, r => r["port"] = 65536,
            r => r["connectAttempts"] = 0, r => r["connectAttempts"] = 401, r => r["connectAttempts"] = 2, r => r["durationMilliseconds"] = 300000,
            r => r["durationMilliseconds"] = 800000, r => Row(r, 0)["status"] = "200", r => Row(r, 0)["extra"] = true,
            r => Row(r, 0).Remove("method"), r => Row(r, 0)["bodyBase64"] = "?", r => Row(r, 0)["bodyBase64"] = Convert.ToBase64String(new byte[8193]),
            r => Row(r, 0)["dateUtc"] = "yesterday", r => Row(r, 0)["dateUtc"] = "2026-10-18T00:00:00.0000000+01:00",
            r => r["exchanges"]!.AsArray().RemoveAt(9),
            r => r["exchanges"] = new JsonArray(Enumerable.Range(0, 74).Select(_ => (JsonNode?)Row(r, 0).DeepClone()).ToArray()) })
        { var report = Report(); alter(report); Reject(report.ToJsonString()); }
    }

    [HostTest("service peer rejects substituted requests premature stop counter identity time phase and Date regressions")]
    private static void Progress()
    {
        foreach (Action<JsonObject> alter in new Action<JsonObject>[] {
            r => Row(r, 0)["path"] = "/other", r => Row(r, 2)["status"] = 200, r => Row(r, 4)["status"] = 200,
            r => Row(r, 9)["bodyBase64"] = Encode("other"), r => Row(r, 3)["method"] = "GET", r => Row(r, 1)["index"] = 0,
            r => Row(r, 7)["startedMilliseconds"] = 0, r => Row(r, 7)["completedMilliseconds"] = 300001,
            r => Row(r, 9)["completedMilliseconds"] = 300101, r => Row(r, 7)["dateUtc"] = "2026-10-17T00:00:00.0000000+00:00",
            r => ChangeStatus(r, 1, "calls", 1), r => ChangeStatus(r, 7, "phase", 4), r => ChangeStatus(r, 7, "failure", 1),
            r => ChangeStatus(r, 7, "managedWorkerId", 8), r => ChangeStatus(r, 7, "managedWorkerId", 0),
            r => ChangeStatus(r, 7, "calls", 4096), r => ChangeStatus(r, 7, "randomBytes", 4096),
            r => ChangeStatus(r, 7, "elapsedMilliseconds", 300000), r => ChangeStatus(r, 7, "iterations", 0),
            r => ChangeStatus(r, 6, "calls", 4098), r => ChangeStatus(r, 6, "randomRequests", 2),
            r => ChangeStatus(r, 6, "elapsedMilliseconds", 300002) })
        { var report = Report(); alter(report); Reject(report.ToJsonString()); }
        var dates = Report();
        for (int i = 0; i < 10; i++) Row(dates, i)["dateUtc"] = "2026-10-18T00:00:00.0000000+00:00";
        Reject(dates.ToJsonString());
    }

    // Real separate peer child against a deliberately wrong local endpoint.
    // The synthetic serial/runner supplies no guest or execution authority.
    [HostTest("service peer rejects wrong HTTP endpoint and joins cancelled runner before completion")]
    private static void OwnedFailure() => OwnedFailureAsync().GetAwaiter().GetResult();
    private static async Task OwnedFailureAsync()
    {
        string root = HostToolProcess.FindRepositoryRoot(), directory = Path.Combine(root, "artifacts", "host-tests", "service-peer-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory); string serial = Path.Combine(directory, "serial.log"), log = Path.Combine(directory, "peer.log");
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20)); bool stopped = false, served = false; int starts = 0;
        string? failure = null;
        try
        {
            _ = await NativeAotServicePeer.RunAsync(root, log, serial, async (port, token) =>
            {
                starts++; using var listener = new TcpListener(IPAddress.Loopback, port); listener.Start();
                File.WriteAllText(serial, "\nCLROS_M16_014_LISTENING\r\n");
                try
                {
                    using var client = await listener.AcceptTcpClientAsync(token); await using var stream = client.GetStream();
                    using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
                    while (await reader.ReadLineAsync(token) is { Length: > 0 }) { }
                    await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nDate: Sun, 18 Oct 2026 00:00:00 GMT\r\nContent-Length: 5\r\nConnection: close\r\n\r\nwrong"), token);
                    await stream.FlushAsync(token); served = true; client.Close();
                    await Task.Delay(Timeout.Infinite, token);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { }
                finally { stopped = true; }
                return new ProcessResult(1, "synthetic-runner", "", false, false, false, TimeSpan.Zero);
            }, deadline.Token);
        }
        catch (ToolFailure error) { failure = error.Reason; }
        HostCheck.True(served && stopped); HostCheck.Equal("service_peer_or_runner_failed", failure ?? "accepted");
        HostCheck.True(File.Exists(log) && File.ReadAllText(log).Length == 0 &&
            File.ReadAllText(log + ".stderr.log").Contains("service_peer_http_exchange", StringComparison.Ordinal));
        bool fresh = false;
        try { _ = await NativeAotServicePeer.RunAsync(root, log, serial, (_, _) => { starts++; throw new InvalidOperationException(); }, deadline.Token); }
        catch (IOException) { fresh = true; }
        HostCheck.True(fresh); HostCheck.Equal(1, starts);
    }

    private static void Shift(JsonObject report, long milliseconds)
    {
        report["durationMilliseconds"] = report["durationMilliseconds"]!.GetValue<long>() + milliseconds;
        foreach (var node in report["exchanges"]!.AsArray())
        {
            var row = node!.AsObject();
            row["startedMilliseconds"] = row["startedMilliseconds"]!.GetValue<long>() + milliseconds;
            row["completedMilliseconds"] = row["completedMilliseconds"]!.GetValue<long>() + milliseconds;
            row["dateUtc"] = DateTimeOffset.Parse(row["dateUtc"]!.GetValue<string>(), CultureInfo.InvariantCulture).AddMilliseconds(milliseconds).ToString("O", CultureInfo.InvariantCulture);
        }
    }

    private static string Encode(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
    private static JsonObject Row(JsonObject report, int index) => report["exchanges"]![index]!.AsObject();
    private static void ChangeStatus(JsonObject report, int index, string field, long value)
    {
        var row = Row(report, index); var status = JsonNode.Parse(Convert.FromBase64String(row["bodyBase64"]!.GetValue<string>()))!.AsObject();
        status[field] = value; row["bodyBase64"] = Encode(status.ToJsonString());
    }
    private static JsonObject Report()
    {
        string Status(int phase, int worker, long calls, long bytes, long elapsed) => new JsonObject { ["contract"] = "clros.service.v1",
            ["phase"] = phase, ["failure"] = 0, ["managedWorkerId"] = worker, ["calls"] = calls, ["randomRequests"] = phase == 0 ? 0 : 1,
            ["randomBytes"] = bytes, ["iterations"] = phase == 0 ? 0 : 1, ["elapsedMilliseconds"] = elapsed }.ToJsonString();
        var exchanges = new JsonArray();
        void Add(string method, string path, int status, long start, long end, string body)
        {
            exchanges.Add(new JsonObject { ["index"] = exchanges.Count, ["method"] = method, ["path"] = path, ["status"] = status,
                ["startedMilliseconds"] = start, ["completedMilliseconds"] = end, ["dateUtc"] = new DateTimeOffset(2026, 10, 18, 0, 0, 0, TimeSpan.Zero).AddSeconds(end / 1000).ToString("O", CultureInfo.InvariantCulture),
                ["bodyBase64"] = Encode(body) });
        }
        const string path = "/__clros/service/";
        Add("GET", "/healthz", 200, 0, 10, "CLROS_SERVICE_HEALTH_OK"); Add("GET", path + "status", 200, 10, 20, Status(0, 0, 0, 0, 0));
        Add("POST", path + "stop", 409, 20, 30, ""); Add("POST", path + "start", 200, 30, 40, "CLROS_SERVICE_STARTED");
        Add("POST", path + "start", 409, 40, 50, ""); Add("POST", path + "stop", 409, 50, 60, "");
        Add("GET", path + "status", 200, 60, 70, Status(1, 7, 1, 8192, 1)); Add("GET", path + "status", 200, 300050, 300060, Status(1, 7, 4097, 8192, 300001));
        Add("GET", "/healthz", 200, 300070, 300080, "CLROS_SERVICE_HEALTH_OK"); Add("POST", path + "stop", 200, 300080, 300090, "CLROS_SERVICE_STOPPED");
        return new JsonObject { ["schema"] = "clros.nativeaot.service.peer.v1", ["port"] = 8080, ["durationMilliseconds"] = 300100L, ["connectAttempts"] = 1, ["exchanges"] = exchanges };
    }
    private static void Reject(string text)
    {
        bool rejected = false; try { _ = NativeAotServicePeer.InspectOutput(text); }
        catch (Exception error) when (error is ToolFailure or InvalidDataException) { rejected = true; }
        HostCheck.True(rejected);
    }
}
