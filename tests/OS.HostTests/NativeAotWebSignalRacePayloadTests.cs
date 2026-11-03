using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using OS.Tooling;

internal static partial class NativeAotWebSignalRacePayloadTests
{
    private const string Endpoint = "http://127.0.0.1:23456", Health = "OS_MINIMAL_API_OK";
    private const string Complete = "CLROS_CHURN_OK iterations=8 bytes=65536 timers=8 workers=8 collections=8";
    // Independent copied result. These addresses/identities are never native
    // inputs or proof of byte, callback, handler, process or guest execution.
    private const string Success = "CLROS_SIGNAL_RACE_OK signal=12 pid=43 actor=8 waiter=7 callback=9 callbacks=1 actorJoined=1 waiterJoined=1 consumed=90 recovery=107 recoveryWriter=9 initialValue=-1 initialErrno=4 recoveryValue=1 recoveryEvents=1 recoveryTag=344978309701 handler=4096 flags=335544324 restorer=8192 actionMask=0 originalHandler=0 originalFlags=0 originalRestorer=0 originalActionMask=0 maskBefore=2048 maskDuring=0 maskAfter=2048 altAddress=0 altFlags=2 altBytes=0 descriptorsClosed=3 actionRestored=1 altUnchanged=1";
    private static string Output()
    {
        string directory = Path.GetFullPath(Path.Combine("artifacts", "host-tests", "signal-race-" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "observations.json");
    }

    private static async Task<string> Capture(Peer peer, string? path = null, CancellationToken token = default)
    {
        using var client = new HttpClient(peer);
        return await NativeAotWebPayload.CaptureSignalRaceAsync(client, Endpoint, Health, path ?? Output(), token);
    }

    private static void Reject(Action action)
    {
        bool failed = false;
        try
        {
            action();
        }
        catch (Exception error) when (error is InvalidDataException or JsonException)
        {
            failed = true;
        }

        HostCheck.True(failed);
    }

    private static async Task RejectAsync(Func<Task> action)
    {
        bool failed = false;
        try
        {
            await action();
        }
        catch (Exception error) when (error is InvalidDataException or OperationCanceledException)
        {
            failed = true;
        }

        HostCheck.True(failed);
    }

    [HostTest("web race HTTP controller joins cancellation recovery and repeat refusals with exact copied roles")]
    private static async Task CompleteCapture()
    {
        var peer = new Peer
        {
            UnselectedPolls = true
        };
        string path = Output(), json = await Capture(peer, path);
        HostCheck.Equal(json, File.ReadAllText(path));
        HostCheck.True(peer.ChurnCanceled);
        var selection = NativeAotWebPayload.ValidateSignalRaceEvidence(json, Endpoint, Health);
        HostCheck.Equal(new QemuTranscripts.WebSignalRaceSelection(43, 7, 8, 9, 9), selection);
        using var report = JsonDocument.Parse(json);
        HostCheck.Equal(19, report.RootElement.GetProperty("samples").GetArrayLength());
        var ledger = NativeAotWebPayload.InspectSignalRaceAttempts(File.ReadAllText(path + ".attempts.json"), json, Endpoint, Health);
        HostCheck.Equal(new NativeAotWebPayload.SignalRaceAttemptSummary(21, 20, 1, 0, true), ledger);
        using var log = JsonDocument.Parse(File.ReadAllText(path + ".attempts.json"));
        var rows = log.RootElement.GetProperty("attempts");
        HostCheck.True(rows.EnumerateArray().Any(row => row.GetProperty("path").GetString() == "/__clros/signalrace/status" && Encoding.UTF8.GetString(Convert.FromBase64String(row.GetProperty("bodyBase64").GetString()!)).Contains("phase=2", StringComparison.Ordinal)));
        var cancel = log.RootElement.GetProperty("selections")[7];
        HostCheck.Equal(0, cancel.GetProperty("attempt").GetInt32());
        HostCheck.Equal("cancelled", rows[cancel.GetProperty("related").GetInt32() - 1].GetProperty("outcome").GetString());
    }

    [HostTest("web race attempt copies reject broken terminal identities bytes and selected response links")]
    private static async Task AttemptRefusals()
    {
        string path = Output(), selected = await Capture(new Peer(), path), json = File.ReadAllText(path + ".attempts.json");
        foreach (Action<JsonObject> corrupt in new Action<JsonObject>[]
        {
            o => o["schema"] = "clros.web.signalrace.attempts.v2",
            o => o["endpoint"] = "http://localhost:23456",
            o => o["extra"] = true,
            o => o["semanticComplete"] = false,
            o => o["failure"] = "false success",
            o => o["attempts"]![0]!["id"] = 2,
            o => o["attempts"]![0]!["completion"] = o["attempts"]![1]!["completion"]!.DeepClone(),
            o => o["attempts"]![0]!["outcome"] = "pending",
            o => o["attempts"]![0]!["headers"] = false,
            o => o["attempts"]![0]!["mediaType"] = "application/json",
            o => o["attempts"]![0]!["mediaTypeComplete"] = false,
            o => o["attempts"]![0]!["bodyComplete"] = false,
            o => o["attempts"]![0]!["bodyBase64"] = "wK8=",
            o => o["attempts"]![0]!["bodyBase64"] = "QQ==\n",
            o => o["attempts"]![0]!["bodyBase64"] = "!!!!",
            o => o["attempts"]![0]!["status"] = 201,
            o => o["attempts"]![0]!["error"] = "unexpected",
            o => o["attempts"]![0]!["path"] = "/unowned",
            o => o["attempts"]![0]!["method"] = "PUT",
            o => o["selections"]!.AsArray().RemoveAt(0),
            o => o["selections"]![0]!["attempt"] = 2,
            o => o["selections"]![0]!["related"] = 1,
            o => o["selections"]![7]!["attempt"] = 4,
            o => o["selections"]![7]!["related"] = 1,
            o => o["selections"]![0]!["sample"] = 1
        }

        )
        {
            var value = JsonNode.Parse(json)!.AsObject();
            corrupt(value);
            Reject(() => NativeAotWebPayload.InspectSignalRaceAttempts(value.ToJsonString(), selected, Endpoint, Health));
        }

        Reject(() => NativeAotWebPayload.InspectSignalRaceAttempts(json.Replace("\"schema\":", "\"schema\":\"duplicate\",\"schema\":", StringComparison.Ordinal), selected, Endpoint, Health));
        var reordered = JsonNode.Parse(json)!.AsObject();
        var first = reordered["attempts"]![0]!["completion"]!.DeepClone();
        reordered["attempts"]![0]!["completion"] = reordered["attempts"]![2]!["completion"]!.DeepClone();
        reordered["attempts"]![2]!["completion"] = first;
        Reject(() => NativeAotWebPayload.InspectSignalRaceAttempts(reordered.ToJsonString(), selected, Endpoint, Health));
        var incomplete = JsonNode.Parse(json)!.AsObject();
        incomplete["semanticComplete"] = false;
        incomplete["failure"] = "copied_failure";
        HostCheck.True(!NativeAotWebPayload.InspectSignalRaceAttempts(incomplete.ToJsonString(), selected, Endpoint, Health).SemanticComplete);
        foreach (string outcome in new[]
        {
            "faulted",
            "cancelled",
            "response"
        }

        )
        {
            var extra = JsonNode.Parse(json)!.AsObject();
            var rows = extra["attempts"]!.AsArray();
            var row = rows[0]!.DeepClone();
            row["id"] = rows.Count + 1;
            row["completion"] = rows.Count + 1;
            row["method"] = "POST";
            row["path"] = "/__clros/signalrace";
            row["outcome"] = outcome;
            row["error"] = outcome == "cancelled" ? "cancelled" : outcome == "faulted" ? "IOException" : "";
            rows.Add(row);
            Reject(() => NativeAotWebPayload.InspectSignalRaceAttempts(extra.ToJsonString(), selected, Endpoint, Health));
            extra["semanticComplete"] = false;
            extra["failure"] = "copied_failure";
            HostCheck.True(!NativeAotWebPayload.InspectSignalRaceAttempts(extra.ToJsonString(), selected, Endpoint, Health).SemanticComplete);
        }

        foreach (string route in new[]
        {
            "/__clros/churn/status",
            "/__clros/signalrace/status"
        }

        )
        {
            var trailing = JsonNode.Parse(json)!.AsObject();
            var rows = trailing["attempts"]!.AsArray();
            var row = rows.First(r => r!["path"]!.GetValue<string>() == route)!.DeepClone();
            row["id"] = rows.Count + 1;
            row["completion"] = rows.Count + 1;
            rows.Add(row);
            Reject(() => NativeAotWebPayload.InspectSignalRaceAttempts(trailing.ToJsonString(), selected, Endpoint, Health));
            trailing["semanticComplete"] = false;
            trailing["failure"] = "copied_failure";
            HostCheck.True(!NativeAotWebPayload.InspectSignalRaceAttempts(trailing.ToJsonString(), selected, Endpoint, Health).SemanticComplete);
        }
    }

    [HostTest("web race copied evidence rejects reordered omitted malformed and mismatched role observations")]
    private static async Task CopiedRefusals()
    {
        string json = await Capture(new Peer());
        foreach (Action<JsonObject> corrupt in new Action<JsonObject>[]
        {
            o => o["schema"] = "clros.web.churn.v1",
            o => o["endpoint"] = "http://localhost:23456",
            o => o["extra"] = true,
            o => o["samples"]!.AsArray().RemoveAt(7),
            o => o["samples"]![7]!["kind"] = "recovery-request",
            o => o["samples"]![11]!["body"] = Success.Replace("waiter=7", "waiter=8", StringComparison.Ordinal),
            o => o["samples"]![11]!["body"] = Success.Replace("consumed=90", "consumed=107", StringComparison.Ordinal),
            o => o["samples"]![11]!["body"] = Success.Replace("initialValue=-1", "initialValue=1", StringComparison.Ordinal),
            o => o["samples"]![4]!["body"] = "CLROS_SIGNAL_RACE_STATUS phase=4 pid=43 waiter=7 actor=8 callback=10 recoveryRequested=0",
            o => o["samples"]![5]!["body"] = o["samples"]![3]!["body"]!.DeepClone(),
            o => o["samples"]![15]!["body"] = o["samples"]![8]!["body"]!.DeepClone()
        }

        )
        {
            var value = JsonNode.Parse(json)!.AsObject();
            corrupt(value);
            Reject(() => NativeAotWebPayload.ValidateSignalRaceEvidence(value.ToJsonString(), Endpoint, Health));
        }

        Reject(() => NativeAotWebPayload.ValidateSignalRaceEvidence(json.Replace("\"schema\":", "\"schema\":\"duplicate\",\"schema\":", StringComparison.Ordinal), Endpoint, Health));
    }

    [HostTest("web race unsupported response retains rejected partial observations and joins original churn")]
    private static async Task Unsupported()
    {
        var peer = new Peer
        {
            RefuseRace = true
        };
        string path = Output();
        await RejectAsync(() => Capture(peer, path));
        HostCheck.True(peer.ChurnCanceled && File.Exists(path));
        Reject(() => NativeAotWebPayload.ValidateSignalRaceEvidence(File.ReadAllText(path), Endpoint, Health));
    }

    [HostTest("web race lost churn overlap remains failed without retry and joins race request")]
    private static async Task LostOverlap()
    {
        var peer = new Peer
        {
            CompleteEarly = true
        };
        string path = Output();
        await RejectAsync(() => Capture(peer, path));
        HostCheck.True(peer.RaceCanceled && File.Exists(path));
        Reject(() => NativeAotWebPayload.ValidateSignalRaceEvidence(File.ReadAllText(path), Endpoint, Health));
    }

    [HostTest("web race caller cancellation joins both pending HTTP requests and preserves partial report")]
    private static async Task Canceled()
    {
        using var stop = new CancellationTokenSource();
        var peer = new Peer
        {
            CancelOnArm = stop.Cancel
        };
        string path = Output();
        await RejectAsync(() => Capture(peer, path, stop.Token));
        HostCheck.True(peer.ChurnCanceled && peer.RaceCanceled && File.Exists(path));
        Reject(() => NativeAotWebPayload.ValidateSignalRaceEvidence(File.ReadAllText(path), Endpoint, Health));
    }

    [HostTest("web race stale progress cannot select overlap and cancellation joins both HTTP requests")]
    private static async Task StaleCounters()
    {
        using var stop = new CancellationTokenSource();
        var peer = new Peer
        {
            StaleProgress = true,
            CancelAfterStale = stop.Cancel
        };
        string path = Output();
        await RejectAsync(() => Capture(peer, path, stop.Token));
        HostCheck.True(peer.ChurnCanceled && peer.RaceCanceled);
        Reject(() => NativeAotWebPayload.ValidateSignalRaceEvidence(File.ReadAllText(path), Endpoint, Health));
    }

    [HostTest("web race qualification requires explicit flag and unsupported hosts refuse before publication")]
    private static async Task QualificationMode()
    {
        string directory = Path.GetFullPath(Path.Combine("artifacts", "host-tests", "unused-race-publication-" + Guid.NewGuid().ToString("N")));
        using var malformed = new StringWriter();
        HostCheck.Equal(1, await NativeAotWebPayload.RunAsync(Environment.CurrentDirectory, ["qualify", "--signal-race=false", "--output", directory], malformed));
        HostCheck.True(!Directory.Exists(directory));
        if (!OperatingSystem.IsLinux())
        {
            using var unsupported = new StringWriter();
            HostCheck.Equal(1, await NativeAotWebPayload.RunAsync(Environment.CurrentDirectory, ["qualify", "--signal-race", "--output", directory], unsupported));
            HostCheck.True(unsupported.ToString().Contains("linux_builder_required", StringComparison.Ordinal) && !Directory.Exists(directory));
        }
    }
}
