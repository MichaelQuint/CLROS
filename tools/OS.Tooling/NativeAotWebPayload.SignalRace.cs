using System.Text;
using System.Text.Json;

namespace OS.Tooling;

public static partial class NativeAotWebPayload
{
    /// <summary>Borrow an owned app endpoint/client and capture one bounded,
    /// non-retried race during a pending churn request. Owns and joins every
    /// request task it starts; the caller owns the app, client and process death.
    /// Fresh selected output and a .attempts.json companion retain every logical
    /// attempt, partial response and failure after request joins. Logical IDs
    /// grant no wire connection/exchange or sent-byte authority. A report grants no
    /// native/guest execution, callback-role or pointed-to-content authority.</summary>

    public static Task<string> CaptureSignalRaceAsync(HttpClient client, string endpoint, string expectedHealthBody, string observationPath, CancellationToken cancellation = default) => CaptureSignalRaceCoreAsync(client, endpoint, expectedHealthBody, observationPath, cancellation, null);
    private static async Task<string> CaptureSignalRaceCoreAsync(HttpClient client, string endpoint, string expectedHealthBody, string observationPath, CancellationToken cancellation, NativeAotServiceQuotaPeer.WireCapture? wire, Action<CreatedRaceReport, CreatedRaceReport>? retainCreated = null, bool startGate = false, RaceStartHooks? startHooks = null, bool eventOwned = false)
    {
        ArgumentNullException.ThrowIfNull(client);
        RaceInputs(endpoint, expectedHealthBody);
        RaceNeed(startGate || startHooks is null, "start_mode");
        RaceNeed(!eventOwned || startGate, "event_mode");
        RaceNeed(!eventOwned || client.Timeout == Timeout.InfiniteTimeSpan || client.Timeout >= TimeSpan.FromSeconds(150), "event_client_timeout");
        RaceNeed(!eventOwned || !client.DefaultRequestHeaders.Contains("X-CLROS-Signal") && !client.DefaultRequestHeaders.Contains("X-CLROS-Churn"), "event_client_headers");
        string path = Path.GetFullPath(observationPath), parent = Path.GetDirectoryName(path)!;
        AuthorityFiles.RequirePlainPath(parent, parent, "web_signalrace_output_path");
        using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        using var attemptOutput = new FileStream(path + ".attempts.json", FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(TimeSpan.FromSeconds(eventOwned ? 150 : 30));
        using var cancelChurn = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
        using var cancelRace = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
        using var churnRequest = Post("/__clros/churn", eventOwned);
        using var raceRequest = Post("/__clros/signalrace");
        Task<RaceHttpReply>? churn = null, race = null;
        List<RaceHttpSample> samples = [];
        RaceAttemptLog attempts = new(endpoint, wire, startGate, eventOwned);
        bool semanticComplete = false, primaryFailure = false;
        string failure = "capture_incomplete";
        RaceHttpAttempt? churnCall = null, raceCall = null;
        HttpRequestMessage Post(string target, bool continuous = false)
        {
            var message = new HttpRequestMessage(HttpMethod.Post, endpoint + target)
            {
                Content = new ByteArrayContent([])
            };
            message.Headers.Add(target == "/__clros/churn" ? "X-CLROS-Churn" : "X-CLROS-Signal", target == "/__clros/churn" ? (continuous ? "v2" : "v1") : eventOwned && target.StartsWith("/__clros/signalrace", StringComparison.Ordinal) ? "v3" : "v1");
            return message;
        }

        async Task<RaceHttpReply> Get(string target)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, endpoint + target);
            if (eventOwned && target == "/__clros/signalrace/status")
                request.Headers.Add("X-CLROS-Signal", "v3");
            return await attempts.SendAsync(client, request, target, deadline.Token);
        }

        void Select(string kind, RaceHttpReply reply)
        {
            attempts.Select(samples.Count, reply);
            samples.Add(new(kind, reply.Status, reply.Body));
        }

        async Task Observe(string kind, string target)
        {
            Select(kind, await Get(target));
            if (target == "/healthz")
                RaceNeed(samples[^1].status == 200 && samples[^1].body == expectedHealthBody, "health_response");
        }

        void Pending(bool both)
        {
            RaceNeed(churn is not null && !churn.IsCompleted && churnCall is not null && !churnCall.headers && (!both || race is not null && !race.IsCompleted && raceCall is not null && !raceCall.headers), "lost_overlap");
        }

        async Task Churn(string kind, bool pending, ulong[]? after = null)
        {
            for (int attempt = 0; attempt < 200; attempt++)
            {
                if (pending)
                    Pending(race is not null);
                var response = await Get("/__clros/churn/status");
                RaceNeed(response.Status == 200, "churn_status");
                var value = ChurnStatus(response.Body, eventOwned);
                if (pending)
                    Pending(race is not null);
                if (value[0] == (pending ? 1UL : 0) && value[1] == value[0] && (!pending || value[8] >= 1 && value[9] >= 1 && value[10] >= 1) && (after is null || value[8] > after[8] && value[9] > after[9] && value[10] > after[10]))
                {
                    Select(kind, response);
                    return;
                }

                await Task.Delay(5, deadline.Token);
            }

            throw new InvalidDataException("web_signalrace_churn_progress");
        }

        async Task Request(string kind, string target, int status, string expected)
        {
            using var message = Post(target);
            Select(kind, await attempts.SendAsync(client, message, target, deadline.Token));
            RaceNeed(samples[^1].status == status && samples[^1].body == expected, "request_result");
        }

        string Json() => JsonSerializer.Serialize(new
        {
            schema = eventOwned ? "clros.web.signalrace.v3" : startGate ? "clros.web.signalrace.v2" : "clros.web.signalrace.v1",
            endpoint,
            samples
        });
        try
        {
            await Churn("baseline-churn", false);
            await Observe("baseline-race", "/__clros/signalrace/status");
            await Observe("health-before", "/healthz");
            // Reject an already-used app before starting either active request.
            RaceNeed(ChurnStatus(samples[0].body, eventOwned).All(n => n == 0) && ReadRaceStatus(samples[1].body, eventOwned).All(n => n == 0) && samples[1].status == 200 && samples[2].status == 200 && samples[2].body == expectedHealthBody, "fresh_session");
            if (startGate)
            {
                race = attempts.SendAsync(client, raceRequest, "/__clros/signalrace", cancelRace.Token);
                raceCall = attempts.LastStarted;
                ulong[]? initial = null;
                for (int attempt = 0; attempt < 200; attempt++)
                {
                    RaceNeed(!race.IsCompleted && !raceCall.headers, "lost_initial_wait");
                    var response = await Get("/__clros/signalrace/status");
                    RaceNeed(response.Status == 200, "race_status");
                    var status = ReadRaceStatus(response.Body, eventOwned);
                    RaceNeed(status[0] <= 2, "missed_initial_gate");
                    RaceNeed(!race.IsCompleted && !raceCall.headers, "lost_initial_wait");
                    if (status[0] == 2)
                    {
                        Select("initial-armed", response);
                        initial = status;
                        break;
                    }

                    await Task.Delay(5, deadline.Token);
                }

                RaceNeed(initial is not null, "initial_gate_timeout");
                if (startHooks is not null)
                    await startHooks.BeforeChurn(initial!, deadline.Token);
            }

            churn = attempts.SendAsync(client, churnRequest, "/__clros/churn", cancelChurn.Token);
            churnCall = attempts.LastStarted;
            await Churn("churn-pending", true);
            var beforeRace = ChurnStatus(samples[^1].body, eventOwned);
            if (startGate)
            {
                if (startHooks is not null)
                    await startHooks.BeforeStart(deadline.Token);
                Pending(true);
                await Request("start-request", "/__clros/signalrace/start", 200, "CLROS_SIGNAL_RACE_START_REQUESTED");
            }
            else
            {
                race = attempts.SendAsync(client, raceRequest, "/__clros/signalrace", cancelRace.Token);
                raceCall = attempts.LastStarted;
            }

            bool armed = false;
            for (int attempt = 0; attempt < 200; attempt++)
            {
                Pending(true);
                var response = await Get("/__clros/signalrace/status");
                RaceNeed(response.Status == 200, "race_status");
                var status = ReadRaceStatus(response.Body, eventOwned);
                Pending(true);
                RaceNeed(status[0] <= 4, "missed_recovery_gate");
                if (status[0] == 4)
                {
                    Select("recovery-armed", response);
                    armed = true;
                    break;
                }

                await Task.Delay(5, deadline.Token);
            }

            RaceNeed(armed, "recovery_gate_timeout");
            if (startHooks is not null)
                await startHooks.AfterAcknowledged(deadline.Token);
            await Churn("churn-overlap", true, beforeRace);
            await Observe("overlap-health", "/healthz");
            Pending(true);
            cancelChurn.Cancel();
            bool canceled = false;
            try
            {
                _ = await churn;
            }
            catch (OperationCanceledException) when (cancelChurn.IsCancellationRequested)
            {
                canceled = true;
            }

            RaceNeed(canceled, "churn_cancellation");
            attempts.SelectCancellation(samples.Count, churnCall.id);
            samples.Add(new("churn-cancel", 0, ""));
            await Churn("churn-drained", false);
            RaceNeed(!race!.IsCompleted, "race_ended_before_recovery");
            await Request("recovery-request", "/__clros/signalrace/recover", 200, "CLROS_SIGNAL_RACE_RECOVERY_REQUESTED");
            await Request("recovery-repeat", "/__clros/signalrace/recover", 409, "CLROS_SIGNAL_RACE_RECOVERY_REFUSED");
            Select("race-complete", await race);
            await Observe("race-final", "/__clros/signalrace/status");
            await Observe("health-after", "/healthz");
            await Request("churn-complete", "/__clros/churn", 200, "CLROS_CHURN_OK iterations=8 bytes=65536 timers=8 workers=8 collections=8");
            await Churn("final-churn", false);
            await Observe("health-final", "/healthz");
            await Request("repeat-race", "/__clros/signalrace", 409, "CLROS_SIGNAL_RACE_BUSY");
            await Request("repeat-probe", "/__clros/signalprobe", 409, "CLROS_SIGNAL_PROBE_BUSY");
            string json = Json();
            _ = ValidateSignalRaceEvidence(json, endpoint, expectedHealthBody);
            semanticComplete = true;
            failure = "";
            return json;
        }
        catch (Exception error)
        {
            primaryFailure = true;
            failure = RaceCaptureFailure(error);
            throw;
        }
        finally
        {
            cancelChurn.Cancel();
            cancelRace.Cancel();
            Exception? joinFailure = null;
            foreach (var pending in new[]
            {
                churn,
                race
            }

            )
                if (pending is not null)
                {
                    try
                    {
                        _ = await pending;
                    }
                    catch (OperationCanceledException) when (cancelChurn.IsCancellationRequested || cancelRace.IsCancellationRequested)
                    {
                    }
                    catch (Exception error)
                    {
                        joinFailure ??= error;
                    }
                }

            if (joinFailure is not null)
            {
                semanticComplete = false;
                if (!primaryFailure)
                    failure = "web_signalrace_request_join_failed";
            }

            // Always retain final/partial reports after request joins, including
            // cancellation. Snapshot IO intentionally has no cancellation token;
            // the native-v8 owner rechecks its monotonic deadline before success.
            // Retention latency is not a hard operation or cleanup wall bound.
            byte[] selectedBytes = Encoding.UTF8.GetBytes(Json()), attemptBytes = Encoding.UTF8.GetBytes(attempts.Json(semanticComplete, failure));
            try
            {
                await output.WriteAsync(selectedBytes);
                await output.FlushAsync();
            }
            finally
            {
                await attemptOutput.WriteAsync(attemptBytes);
                await attemptOutput.FlushAsync();
            }

            if (retainCreated is not null && semanticComplete && !primaryFailure && joinFailure is null)
            {
                CreatedRaceReport? selectedFile = null, attemptFile = null;
                try
                {
                    selectedFile = CreatedRaceReport.Retain(output, selectedBytes);
                    attemptFile = CreatedRaceReport.Retain(attemptOutput, attemptBytes);
                    retainCreated(selectedFile, attemptFile);
                    selectedFile = null;
                    attemptFile = null;
                }
                finally
                {
                    selectedFile?.Dispose();
                    attemptFile?.Dispose();
                }
            }

            if (joinFailure is not null && !primaryFailure)
                throw new InvalidDataException("web_signalrace_request_join_failed", joinFailure);
        }
    }

    private static void ValidateRecordedSignalRace(string destination, string nativeSchema)
    {
        string path = Path.Combine(destination, "web-signalrace.json");
        Require(new FileInfo(path).Length is > 0 and <= 65536, "signalrace_recorded_size");
        using var http = JsonDocument.Parse(File.ReadAllText(Path.Combine(destination, "web-http-signalrace.json")));
        string? attempts = null, journal = null;
        if (nativeSchema is "clros.nativeaot.web.v6" or "clros.nativeaot.web.v7" or "clros.nativeaot.web.v8")
        {
            string companion = path + ".attempts.json";
            Require(new FileInfo(companion).Length is > 0 and <= 8 * 1024 * 1024, "signalrace_attempt_size");
            attempts = File.ReadAllText(companion);
        }

        if (nativeSchema is "clros.nativeaot.web.v7" or "clros.nativeaot.web.v8")
        {
            string schedule = Path.Combine(destination, "web-signalrace-schedule.jsonl");
            Require(new FileInfo(schedule).Length is > 0 and <= 1048576, "signalrace_schedule_size");
            journal = File.ReadAllText(schedule, new UTF8Encoding(false, true));
        }

        _ = ValidateSignalRaceQualificationEvidence(nativeSchema, File.ReadAllText(path), attempts, journal, http.RootElement.GetProperty("endpoint").GetString()!, DefaultWebBody);
    }
}
