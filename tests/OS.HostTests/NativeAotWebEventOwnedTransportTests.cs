using System.Text;
using System.Text.Json.Nodes;
using OS.Tooling;
using LoopPeer = NativeAotWebSignalRaceTransportTests.Peer;
using Values = NativeAotWebSignalRaceHostHttpTests.Values;

// Real loopback bytes and independent copied header controls only. No native
// wait, original packet association, private Completion or guest proof exists.
internal static class NativeAotWebEventOwnedTransportTests
{
    private const string Health = "OS_MINIMAL_API_OK", Endpoint = "http://127.0.0.1:23460";
    private static string Output() => NativeAotWebSignalRaceTransportTests.Output();
    private static NativeAotServiceQuotaPeer.WireTrace Wire(string path) => NativeAotWebPayload.InspectSignalRaceWire(File.ReadAllBytes(path + ".wire.json"));
    private static byte[] Bytes(NativeAotServiceQuotaPeer.WireTrace trace, int connection, string direction) =>
        trace.Operations.Where(op => op.ConnectionId == connection && op.Direction == direction && op.Outcome == "complete")
            .SelectMany(op => Convert.FromBase64String(op.DataBase64)).ToArray();
    private static QemuTranscripts.WebSignalRaceHostHttpInspection Read(string path, string endpoint) =>
        QemuTranscripts.InspectNativeAotWebSignalRaceHostHttp(File.ReadAllBytes(path + ".wire.json"), File.ReadAllText(path + ".attempts.json"), File.ReadAllText(path), endpoint, Health);

    [HostTest("web event-owned observed transport retains twenty-one v3 roles and exact real loopback streams")]
    private static async Task CompleteCapture()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(12));
        await using var peer = new LoopPeer(eventOwned: true);
        string path = Output();
        string selected = await NativeAotWebPayload.CaptureObservedEventOwnedSignalRaceAsync(peer.Port, Health, path, deadline.Token);
        HostCheck.Equal(selected, File.ReadAllText(path));
        HostCheck.Equal(1, peer.StartRequests);
        var root = JsonNode.Parse(selected)!;
        HostCheck.Equal("clros.web.signalrace.v3", root["schema"]!.GetValue<string>());
        HostCheck.Equal(21, root["samples"]!.AsArray().Count);
        var trace = Wire(path);
        HostCheck.Equal("signalrace-run", trace.Purpose);
        HostCheck.True(trace.Connections.Count >= 3 && trace.Connections.All(c => c.DisposedTicks > 0));
        foreach (var c in trace.Connections)
        {
            var actual = peer.Connections.Single(server => server.Port == c.LocalPort);
            HostCheck.True(Bytes(trace, c.Id, "write").SequenceEqual(actual.Requests.SelectMany(bytes => bytes)));
            HostCheck.True(Bytes(trace, c.Id, "read").SequenceEqual(actual.Responses.SelectMany(bytes => bytes)));
        }
        HostCheck.Equal(1, trace.Exchanges.Count(e => e.Role == "churn" && e.Outcome == "cancelled"));
        var summary = NativeAotWebPayload.InspectSignalRaceAttempts(File.ReadAllText(path + ".attempts.json"), selected, peer.Endpoint, Health);
        HostCheck.True(summary.SemanticComplete && summary.Cancelled == 1 && summary.Faulted == 0);
        HostCheck.Equal(trace.Exchanges.Count, summary.Attempts);
        HostCheck.Equal(summary.Attempts - 1, summary.Responses);
        HostCheck.Equal(21, Read(path, peer.Endpoint).Samples.Count);
    }

    [HostTest("web event-owned observed truncated response keeps actual partial body and joined streams")]
    private static async Task PartialResponse()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(12));
        await using var peer = new LoopPeer(partial: true, eventOwned: true);
        string path = Output();
        bool failed = false;
        try
        {
            await NativeAotWebPayload.CaptureObservedEventOwnedSignalRaceAsync(peer.Port, Health, path, deadline.Token);
        }
        catch (Exception error) when (error is IOException or HttpRequestException) { failed = true; }
        HostCheck.True(failed);
        var trace = Wire(path);
        HostCheck.Equal("signalrace-partial", trace.Purpose);
        HostCheck.True(trace.Connections.Count == 1 && trace.Connections[0].DisposedTicks > 0);
        HostCheck.True(trace.Exchanges.Single() is { Outcome: "faulted", Status: 200, BodyBase64: "QQ==" });
        HostCheck.True(Bytes(trace, 1, "write").SequenceEqual(peer.Connections.Single().Requests.Single()));
        HostCheck.True(Bytes(trace, 1, "read").SequenceEqual(peer.Connections.Single().Responses.Single()));
        var report = Read(path, peer.Endpoint);
        HostCheck.True(report.Samples.Count == 0 && report.Messages.Single().Response is { State: "body", BodyBase64: "QQ==" });
    }

    [HostTest("web event-owned observed pre-cancellation retains v3 partial ledger without connected I/O")]
    private static async Task AlreadyCanceled()
    {
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        await using var peer = new LoopPeer(eventOwned: true);
        string path = Output();
        bool refused = false;
        try
        {
            await NativeAotWebPayload.CaptureObservedEventOwnedSignalRaceAsync(peer.Port, Health, path, canceled.Token);
        }
        catch (OperationCanceledException) { refused = true; }
        HostCheck.True(refused);
        var trace = Wire(path);
        HostCheck.True(trace.Purpose == "signalrace-partial" && trace.Connections.Count == 0 && trace.Operations.Count == 0 && trace.RawBytes == 0);
        HostCheck.Equal("cancelled", trace.Exchanges.Single().Outcome);
        HostCheck.Equal(0, peer.Connections.Count);
        var report = Read(path, peer.Endpoint);
        HostCheck.True(report.Messages.Count == 0 && report.Samples.Count == 0);
    }

    private static string Signal(string method, string path) => path.StartsWith("/__clros/signalrace", StringComparison.Ordinal) ? "v3" :
        method == "POST" && path == "/__clros/signalprobe" ? "v1" : "";
    private static string Request(string method, string path, string selectors) => method + " " + path + " HTTP/1.1\r\nHost: 127.0.0.1:23460\r\n" +
        (method == "POST" ? "Content-Length: 0\r\n" : "") + selectors + "\r\n";
    private static QemuTranscripts.WebSignalRaceHostHttpInspection Copied(string method, string path, string selectors)
    {
        Values values = new();
        int connection = values.Connect(method == "POST" && path == "/__clros/churn" ? "churn" :
            method == "POST" && path == "/__clros/signalrace" ? "race" : "control");
        values.Add(connection, path, method: method, rawRequest: Request(method, path, selectors));
        var selected = JsonNode.Parse(values.Selected())!;
        selected["schema"] = "clros.web.signalrace.v3";
        var attempts = JsonNode.Parse(values.Attempts())!;
        attempts["schema"] = "clros.web.signalrace.attempts.v3";
        foreach (var row in attempts["attempts"]!.AsArray())
        {
            row!["signalVersion"] = Signal(row["method"]!.GetValue<string>(), row["path"]!.GetValue<string>());
            row["churnVersion"] = method == "POST" && path == "/__clros/churn" ? "v2" : "";
        }
        return QemuTranscripts.InspectNativeAotWebSignalRaceHostHttp(values.Wire(), attempts.ToJsonString(), selected.ToJsonString(), Endpoint, Health);
    }
    private static void Reject(Action action, string? reason = null)
    {
        Exception? failure = null;
        try
        {
            action();
        }
        catch (Exception error) when (error is ToolFailure or InvalidDataException) { failure = error; }
        HostCheck.True(failure is not null);
        if (reason is not null)
        {
            HostCheck.True(failure is ToolFailure);
            HostCheck.Equal(reason, ((ToolFailure)failure!).Reason);
        }
    }

    [HostTest("web event-owned full raw headers bind selectors and reject missing wrong duplicate or extra names")]
    private static void HeaderRefusals()
    {
        foreach (var value in new[]{("GET","/__clros/signalrace/status"),("POST","/__clros/signalrace"),
            ("POST","/__clros/signalrace/start"),("POST","/__clros/signalrace/recover"),("POST","/__clros/signalprobe"),
            ("POST","/__clros/churn"),("GET","/__clros/churn/status"),("GET","/healthz")})
        {
            string method = value.Item1, path = value.Item2, signal = Signal(method, path);
            string selectors = (signal == "" ? "" : "X-CLROS-Signal: " + signal + "\r\n") + (path == "/__clros/churn" ? "X-CLROS-Churn: v2\r\n" : "");
            HostCheck.Equal(1, Copied(method, path, selectors).Messages.Count);
            if (signal != "")
            {
                Reject(() => Copied(method, path, selectors.Replace("X-CLROS-Signal: " + signal + "\r\n", "", StringComparison.Ordinal)), "web_race_http_request_signal");
                Reject(() => Copied(method, path, selectors.Replace("X-CLROS-Signal: " + signal, "X-CLROS-Signal: wrong", StringComparison.Ordinal)), "web_race_http_request_signal");
                Reject(() => Copied(method, path, selectors + "x-clros-signal: " + signal + "\r\n"));
            }
            else
                Reject(() => Copied(method, path, selectors + "X-CLROS-Signal: v3\r\n"), "web_race_http_request_signal");
            if (path == "/__clros/churn")
            {
                Reject(() => Copied(method, path, ""), "web_race_http_request_churn");
                Reject(() => Copied(method, path, "X-CLROS-Churn: v3\r\n"), "web_race_http_request_churn");
                Reject(() => Copied(method, path, "X-CLROS-Churn: v1\r\n"), "web_race_http_request_churn");
                Reject(() => Copied(method, path, selectors + "x-clros-churn: v1\r\n"));
            }
            else
                Reject(() => Copied(method, path, selectors + "X-CLROS-Churn: v1\r\n"), "web_race_http_request_churn");
        }
    }
}
