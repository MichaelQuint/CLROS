using System.Text.Json;
using System.Text.Json.Nodes;
using OS.Tooling;
using Peer = NativeAotWebSignalRaceStartTests.Peer;

// Copied HTTP-controller values only: no native wait, owner, wire or guest proof.
internal static class NativeAotWebEventOwnedControllerTests
{
    private const string Endpoint = NativeAotWebSignalRaceStartTests.Endpoint, Health = NativeAotWebSignalRaceStartTests.Health;
    private static string Output() => NativeAotWebSignalRaceStartTests.Output();
    private static async Task<string> Capture(Peer peer, string path, CancellationToken token = default, TimeSpan? timeout = null)
    {
        using var client = new HttpClient(peer) { Timeout = timeout ?? Timeout.InfiniteTimeSpan };
        return await NativeAotWebPayload.CaptureEventOwnedSignalRaceAsync(client, Endpoint, Health, path, token);
    }
    private static void Reject(Action action)
    {
        bool refused = false;
        try
        {
            action();
        }
        catch (Exception error) when (error is InvalidDataException or JsonException or ArgumentException)
        {
            refused = true;
        }
        HostCheck.True(refused);
    }
    private static async Task RejectAsync(Func<Task> action, string? reason = null)
    {
        Exception? failure = null;
        try
        {
            await action();
        }
        catch (Exception error) when (error is InvalidDataException or OperationCanceledException or ArgumentException)
        {
            failure = error;
        }
        HostCheck.True(failure is not null);
        if (reason is not null)
            HostCheck.Equal(reason, failure!.Message);
    }
    private static string Version(string method, string path) => path.StartsWith("/__clros/signalrace", StringComparison.Ordinal) ? "v3" :
        method == "POST" && path == "/__clros/signalprobe" ? "v1" : "";

    [HostTest("web event-owned v3 captures twenty-one copied roles and actual per-request signal headers")]
    private static async Task CompleteCapture()
    {
        var peer = new Peer(true);
        string path = Output(), json = await Capture(peer, path);
        HostCheck.Equal(json, File.ReadAllText(path));
        HostCheck.True(peer.InitialSeen && peer.ChurnCanceled && peer.Starts == 1);
        HostCheck.Equal(new QemuTranscripts.WebSignalRaceSelection(43, 7, 8, 9, 9), NativeAotWebPayload.ValidateSignalRaceEvidence(json, Endpoint, Health));
        var selected = JsonNode.Parse(json)!;
        HostCheck.Equal("clros.web.signalrace.v3", selected["schema"]!.GetValue<string>());
        string[] kinds = ["baseline-churn", "baseline-race", "health-before", "initial-armed", "churn-pending", "start-request", "recovery-armed", "churn-overlap", "overlap-health", "churn-cancel", "churn-drained", "recovery-request", "recovery-repeat", "race-complete", "race-final", "health-after", "churn-complete", "final-churn", "health-final", "repeat-race", "repeat-probe"];
        HostCheck.True(selected["samples"]!.AsArray().Select(row => row!["kind"]!.GetValue<string>()).SequenceEqual(kinds));
        string attempts = File.ReadAllText(path + ".attempts.json");
        var root = JsonNode.Parse(attempts)!;
        HostCheck.Equal("clros.web.signalrace.attempts.v3", root["schema"]!.GetValue<string>());
        bool firstChurn = true;
        foreach (var row in root["attempts"]!.AsArray())
        {
            HostCheck.Equal(Version(row!["method"]!.GetValue<string>(), row["path"]!.GetValue<string>()), row["signalVersion"]!.GetValue<string>());
            string expected = "";
            if (row["method"]!.GetValue<string>() == "POST" && row["path"]!.GetValue<string>() == "/__clros/churn")
            {
                expected = firstChurn ? "v2" : "v1";
                firstChurn = false;
            }
            HostCheck.Equal(expected, row["churnVersion"]!.GetValue<string>());
        }
        HostCheck.Equal(new NativeAotWebPayload.SignalRaceAttemptSummary(21, 20, 1, 0, true),
            NativeAotWebPayload.InspectSignalRaceAttempts(attempts, json, Endpoint, Health));
        // A complete synthetic legacy replay does not admit this v3 HTTP
        // contract. The explicit guard precedes selection/packet comparisons.
        var replay = NativeAotWebSignalRaceTests.Run();
        string? reason = null;
        try
        {
            _ = QemuTranscripts.InspectNativeAotWebSignalRaceRolesForTests([], attempts, json, Endpoint, Health, new(0, 0, 0, []), replay);
        }
        catch (ToolFailure error)
        {
            reason = error.Reason;
        }
        HostCheck.Equal("web_race_roles_event_contract_pending", reason);
    }

    [HostTest("web event-owned unselected churn polls permit continuous progress beyond eight")]
    private static async Task UnselectedContinuousPoll()
    {
        string path = Output(), json = await Capture(new Peer(true), path);
        var selected = JsonNode.Parse(json)!.AsObject();
        var ledger = JsonNode.Parse(File.ReadAllText(path + ".attempts.json"))!.AsObject();
        var samples = selected["samples"]!.AsArray();
        var rows = ledger["attempts"]!.AsArray();
        var links = ledger["selections"]!.AsArray();
        HostCheck.Equal(21, rows.Count);

        int Index(string kind) => Array.FindIndex(samples.ToArray(), s => s!["kind"]!.GetValue<string>() == kind);
        string Work(string body, int count) => System.Text.RegularExpressions.Regex.Replace(body,
            @"\b(timers|workers|collections)=\d+", m => m.Groups[1].Value + "=" + count.ToString(System.Globalization.CultureInfo.InvariantCulture));
        string Encoded(string body) => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(body));
        void SetSelectedWork(string kind, int count)
        {
            int sample = Index(kind);
            string body = Work(samples[sample]!["body"]!.GetValue<string>(), count);
            samples[sample]!["body"] = body;
            int id = links[sample]!["attempt"]!.GetValue<int>();
            rows[id - 1]!["bodyBase64"] = Encoded(body);
        }

        // Copied progress stays monotonic, and finite recovery adds exactly eight.
        SetSelectedWork("churn-overlap", 10);
        SetSelectedWork("churn-drained", 10);
        SetSelectedWork("final-churn", 18);
        int after = links[Index("recovery-armed")]!["attempt"]!.GetValue<int>();
        int completion = rows[after - 1]!["completion"]!.GetValue<int>() + 1;
        int overlap = links[Index("churn-overlap")]!["attempt"]!.GetValue<int>();
        var poll = rows[overlap - 1]!.DeepClone();
        poll["bodyBase64"] = Encoded(Work(samples[Index("churn-overlap")]!["body"]!.GetValue<string>(), 9));
        foreach (var row in rows)
        {
            var value = row!;
            int id = value["id"]!.GetValue<int>(), done = value["completion"]!.GetValue<int>();
            if (id > after)
                value["id"] = id + 1;
            if (done >= completion)
                value["completion"] = done + 1;
        }
        foreach (var link in links)
            foreach (string key in new[] { "attempt", "related" })
            {
                var value = link!;
                int id = value[key]!.GetValue<int>();
                if (id > after)
                    value[key] = id + 1;
            }
        poll["id"] = after + 1;
        poll["completion"] = completion;
        rows.Insert(after, poll);
        HostCheck.Equal(new NativeAotWebPayload.SignalRaceAttemptSummary(22, 21, 1, 0, true),
            NativeAotWebPayload.InspectSignalRaceAttempts(ledger.ToJsonString(), selected.ToJsonString(), Endpoint, Health));

        foreach (int version in new[] { 1, 2 })
        {
            var legacy = selected.DeepClone().AsObject();
            legacy["schema"] = "clros.web.signalrace.v" + version;
            var oldSamples = legacy["samples"]!.AsArray();
            for (int i = oldSamples.Count - 1; i >= 0; i--)
            {
                string kind = oldSamples[i]!["kind"]!.GetValue<string>();
                if (version == 1 && (kind is "initial-armed" or "start-request"))
                {
                    oldSamples.RemoveAt(i);
                    continue;
                }
                oldSamples[i]!["body"] = oldSamples[i]!["body"]!.GetValue<string>()
                    .Replace("CLROS_SIGNAL_RACE_V3_STATUS", "CLROS_SIGNAL_RACE_STATUS", StringComparison.Ordinal)
                    .Replace("CLROS_SIGNAL_RACE_V3_OK", "CLROS_SIGNAL_RACE_OK", StringComparison.Ordinal);
            }
            string? reason = null;
            try
            {
                _ = NativeAotWebPayload.ValidateSignalRaceEvidence(legacy.ToJsonString(), Endpoint, Health);
            }
            catch (InvalidDataException error)
            {
                reason = error.Message;
            }
            HostCheck.Equal("web_churn_status_invariant", reason);
        }
    }

    [HostTest("web event-owned controller keeps actual churn beyond eight operations until explicit cancellation and exact finite recovery")]
    private static async Task ActualContinuingChurn()
    {
        var exercise = new KestrelChurnExercise();
        var peer = new Peer(true) { ActualChurn = exercise };
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        string path = Output();
        string json = await Capture(peer, path, stop.Token);
        var final = exercise.Inspect();
        HostCheck.True(peer.ChurnCanceled && final is { Active: 0, Waiting: 0, HeldBytes: 0, Completed: 1, Canceled: 1, Failed: 0, Workers: >= 17 });
        _ = NativeAotWebPayload.ValidateSignalRaceEvidence(json, Endpoint, Health);
        HostCheck.True(NativeAotWebPayload.InspectSignalRaceAttempts(File.ReadAllText(path + ".attempts.json"), json, Endpoint, Health).SemanticComplete);
    }

    [HostTest("web event-owned controller retains lost-overlap failure when real finite churn completes before a delayed phase response")]
    private static async Task ActualFiniteChurnRefused()
    {
        var exercise = new KestrelChurnExercise();
        var peer = new Peer(true) { ActualChurn = exercise, FiniteInitialChurn = true };
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        string path = Output();
        await RejectAsync(() => Capture(peer, path, stop.Token), "web_signalrace_lost_overlap");
        HostCheck.True(peer.Starts == 1 && peer.RaceCanceled && !peer.ChurnCanceled &&
            exercise.Inspect() is { Completed: 1, Canceled: 0, Failed: 0, Active: 0, Waiting: 0, HeldBytes: 0, Workers: 8 });
        var attempts = JsonNode.Parse(File.ReadAllText(path + ".attempts.json"))!;
        HostCheck.True(!attempts["semanticComplete"]!.GetValue<bool>() && attempts["failure"]!.GetValue<string>() == "web_signalrace_lost_overlap");
    }

    [HostTest("web event-owned v3 rejects short borrowed-client timeouts before files or requests")]
    private static async Task TimeoutAdmission()
    {
        foreach (TimeSpan? timeout in new TimeSpan?[] { null, TimeSpan.FromSeconds(149), TimeSpan.FromSeconds(150) - TimeSpan.FromTicks(1) })
        {
            var peer = new Peer(true);
            using var client = new HttpClient(peer);
            if (timeout is TimeSpan value)
                client.Timeout = value;
            string path = Output();
            await RejectAsync(() => NativeAotWebPayload.CaptureEventOwnedSignalRaceAsync(client, Endpoint, Health, path));
            HostCheck.Equal(0, peer.Calls.Count);
            HostCheck.True(!File.Exists(path) && !File.Exists(path + ".attempts.json"));
        }
        foreach (TimeSpan timeout in new[] { TimeSpan.FromSeconds(150), Timeout.InfiniteTimeSpan })
            HostCheck.True((await Capture(new Peer(true), Output(), timeout: timeout)).Contains("clros.web.signalrace.v3", StringComparison.Ordinal));
        foreach (string header in new[] { "X-CLROS-Signal", "X-CLROS-Churn" })
        {
            var peer = new Peer(true);
            using var client = new HttpClient(peer) { Timeout = Timeout.InfiniteTimeSpan };
            client.DefaultRequestHeaders.Add(header, "v3");
            string path = Output();
            await RejectAsync(() => NativeAotWebPayload.CaptureEventOwnedSignalRaceAsync(client, Endpoint, Health, path), "web_signalrace_event_client_headers");
            HostCheck.Equal(0, peer.Calls.Count);
            HostCheck.True(!File.Exists(path) && !File.Exists(path + ".attempts.json"));
        }
    }

    [HostTest("web event-owned v3 cancellation and refused start retain partial reports and join both pending requests")]
    private static async Task CancellationAndRefusal()
    {
        using var stop = new CancellationTokenSource();
        var canceled = new Peer(true) { CancelInitial = stop.Cancel };
        string path = Output();
        await RejectAsync(() => Capture(canceled, path, stop.Token));
        HostCheck.True(canceled.RaceCanceled && !canceled.ChurnCanceled && canceled.Starts == 0 && !canceled.Calls.Contains("POST /__clros/churn"));
        HostCheck.True(!NativeAotWebPayload.InspectSignalRaceAttempts(File.ReadAllText(path + ".attempts.json"), File.ReadAllText(path), Endpoint, Health).SemanticComplete);
        var refused = new Peer(true) { RefuseStart = true };
        path = Output();
        await RejectAsync(() => Capture(refused, path), "web_signalrace_request_result");
        HostCheck.True(refused.Starts == 1 && refused.ChurnCanceled && refused.RaceCanceled);
        HostCheck.True(!NativeAotWebPayload.InspectSignalRaceAttempts(File.ReadAllText(path + ".attempts.json"), File.ReadAllText(path), Endpoint, Health).SemanticComplete);
    }

    [HostTest("web event-owned v3 selected schema prefix and role substitutions are refused")]
    private static async Task SelectedRefusals()
    {
        string json = await Capture(new Peer(true), Output());
        foreach (Action<JsonObject> change in new Action<JsonObject>[]
        {
            o=>o["schema"]="clros.web.signalrace.v1",o=>o["schema"]="clros.web.signalrace.v2",
            o=>o["samples"]![1]!["body"]=o["samples"]![1]!["body"]!.GetValue<string>().Replace("CLROS_SIGNAL_RACE_V3_STATUS","CLROS_SIGNAL_RACE_STATUS",StringComparison.Ordinal),
            o=>o["samples"]![13]!["body"]=o["samples"]![13]!["body"]!.GetValue<string>().Replace("CLROS_SIGNAL_RACE_V3_OK","CLROS_SIGNAL_RACE_OK",StringComparison.Ordinal),
            o=>o["samples"]![3]!["kind"]="recovery-armed",o=>o["samples"]!.AsArray().RemoveAt(3),o=>o["extra"]=true
        })
        {
            var value = JsonNode.Parse(json)!.AsObject();
            change(value);
            Reject(() => NativeAotWebPayload.ValidateSignalRaceEvidence(value.ToJsonString(), Endpoint, Health));
        }
        Reject(() => NativeAotWebPayload.ValidateSignalRaceEvidence(json.Replace("\"schema\":", "\"schema\":\"duplicate\",\"schema\":", StringComparison.Ordinal), Endpoint, Health));
    }

    [HostTest("web event-owned v3 attempts require exact signalVersion fields and refuse legacy schema pairing")]
    private static async Task AttemptRefusals()
    {
        string path = Output(), selected = await Capture(new Peer(true), path), json = File.ReadAllText(path + ".attempts.json");
        foreach (Action<JsonObject> change in new Action<JsonObject>[]
        {
            o=>o["schema"]="clros.web.signalrace.attempts.v2",
            o=>o["attempts"]![0]!.AsObject().Remove("signalVersion"),
            o=>o["attempts"]![1]!["signalVersion"]="v1",
            o=>o["attempts"]![0]!["signalVersion"]="v3",
            o=>o["attempts"]!.AsArray().Single(r=>r!["path"]!.GetValue<string>()=="/__clros/churn"&&r!["outcome"]!.GetValue<string>()=="cancelled")!["signalVersion"]="v1",
            o=>o["attempts"]!.AsArray().Single(r=>r!["path"]!.GetValue<string>()=="/__clros/signalprobe")!["signalVersion"]="v3",
            o=>o["attempts"]![0]!.AsObject().Remove("churnVersion"),
            o=>o["attempts"]![0]!["churnVersion"]="v2",
            o=>o["attempts"]!.AsArray().Single(r=>r!["path"]!.GetValue<string>()=="/__clros/churn"&&r!["outcome"]!.GetValue<string>()=="cancelled")!["churnVersion"]="v1",
            o=>o["attempts"]!.AsArray().Single(r=>r!["path"]!.GetValue<string>()=="/__clros/churn"&&r!["outcome"]!.GetValue<string>()=="response")!["churnVersion"]="v2"
        })
        {
            var value = JsonNode.Parse(json)!.AsObject();
            change(value);
            Reject(() => NativeAotWebPayload.InspectSignalRaceAttempts(value.ToJsonString(), selected, Endpoint, Health));
        }
        var duplicate = JsonNode.Parse(json)!.AsObject();
        string encoded = duplicate.ToJsonString();
        Reject(() => NativeAotWebPayload.InspectSignalRaceAttempts(encoded.Replace("\"signalVersion\":", "\"signalVersion\":\"duplicate\",\"signalVersion\":", StringComparison.Ordinal), selected, Endpoint, Health));
        Reject(() => NativeAotWebPayload.InspectSignalRaceAttempts(encoded.Replace("\"churnVersion\":", "\"churnVersion\":\"duplicate\",\"churnVersion\":", StringComparison.Ordinal), selected, Endpoint, Health));
        string oldPath = Output(), oldSelected;
        using (var client = new HttpClient(new Peer()))
            oldSelected = await NativeAotWebPayload.CaptureStartedSignalRaceAsync(client, Endpoint, Health, oldPath);
        string old = File.ReadAllText(oldPath + ".attempts.json");
        HostCheck.True(JsonNode.Parse(old)!["attempts"]!.AsArray().All(r => !r!.AsObject().ContainsKey("signalVersion")));
        var extra = JsonNode.Parse(old)!.AsObject();
        extra["attempts"]![0]!["signalVersion"] = "";
        Reject(() => NativeAotWebPayload.InspectSignalRaceAttempts(extra.ToJsonString(), oldSelected, Endpoint, Health));
        Reject(() => NativeAotWebPayload.InspectSignalRaceAttempts(old, selected, Endpoint, Health));
        Reject(() => NativeAotWebPayload.InspectSignalRaceAttempts(json, oldSelected, Endpoint, Health));
    }

    [HostTest("web event-owned v3 controller refuses actual copied responses with legacy status or result prefixes")]
    private static async Task ResponsePrefixRefusals()
    {
        var status = new Peer(true) { LegacyStatusPrefix = true };
        string path = Output();
        await RejectAsync(() => Capture(status, path));
        HostCheck.True(status.Starts == 0 && !status.Calls.Contains("POST /__clros/signalrace"));
        var result = new Peer(true) { LegacyResultPrefix = true };
        path = Output();
        await RejectAsync(() => Capture(result, path));
        HostCheck.True(result.Starts == 1 && result.ChurnCanceled);
        HostCheck.True(!NativeAotWebPayload.InspectSignalRaceAttempts(File.ReadAllText(path + ".attempts.json"), File.ReadAllText(path), Endpoint, Health).SemanticComplete);
    }
}
