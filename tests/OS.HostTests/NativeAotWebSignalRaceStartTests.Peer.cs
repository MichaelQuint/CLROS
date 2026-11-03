using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using OS.Tooling;

// Deterministic copied HTTP responses only. No native wait, scheduling,
// pointed-to bytes, process ownership or guest execution is supplied.
internal static partial class NativeAotWebSignalRaceStartTests
{
    internal sealed class Peer(bool eventOwned = false) : HttpMessageHandler
    {
        internal readonly List<string> Calls = [];
        internal bool RefuseStart, EarlyPhase, InitialSeen, ChurnCanceled, RaceCanceled, LegacyStatusPrefix, LegacyResultPrefix;
        internal int Starts;
        internal KestrelChurnExercise? ActualChurn;
        internal bool FiniteInitialChurn;
        internal Action? CancelInitial;
        private bool raceStarted, released, recovered, complete;
        private int churnStarted;
        private readonly TaskCompletionSource<HttpResponseMessage> churn = new(TaskCreationOptions.RunContinuationsAsynchronously), race = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private string StatusPrefix => eventOwned && !LegacyStatusPrefix ? "CLROS_SIGNAL_RACE_V3_STATUS" : "CLROS_SIGNAL_RACE_STATUS";

        private static HttpResponseMessage Reply(string body, int status = 200) => new((HttpStatusCode)status)
        {
            Content = new StringContent(body, Encoding.UTF8, "text/plain")
        };
        private string Churn()
        {
            if (ActualChurn is not null)
                return ActualChurn.Read();
            int active = churnStarted == 1 && !ChurnCanceled ? 1 : 0, work = churnStarted == 0 ? 0 : complete ? 10 : released ? 2 : 1;
            return $"CLROS_CHURN_STATUS active={active} waiting={active} peakActive={(churnStarted == 0 ? 0 : 1)} started={churnStarted} completed={(complete ? 1 : 0)} canceled={(ChurnCanceled ? 1 : 0)} failed=0 rejected=0 timers={work} workers={work} collections={work} heldBytes={active * 65536} peakHeldBytes={(churnStarted == 0 ? 0 : 65536)}";
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            string path = request.RequestUri!.AbsolutePath;
            Calls.Add(request.Method.Method + " " + path);
            if (request.Method == HttpMethod.Get)
            {
                string version = eventOwned && path == "/__clros/signalrace/status" ? "v3" : "";
                HostCheck.True(version == "" ? !request.Headers.Contains("X-CLROS-Signal") : request.Headers.GetValues("X-CLROS-Signal").SequenceEqual(new[] { version }));
                if (path == "/healthz")
                    return Task.FromResult(Reply(Health));
                if (path == "/__clros/churn/status")
                    return Task.FromResult(Reply(Churn()));
                HostCheck.Equal("/__clros/signalrace/status", path);
                if (!raceStarted)
                    return Task.FromResult(Reply(StatusPrefix + " phase=0 pid=0 waiter=0 actor=0 callback=0 recoveryRequested=0"));
                if (!released)
                {
                    InitialSeen = true;
                    CancelInitial?.Invoke();
                    token.ThrowIfCancellationRequested();
                }

                int phase = recovered ? 7 : released || EarlyPhase ? 4 : 2;
                string body = $"{StatusPrefix} phase={phase} pid=43 waiter=7 actor=8 callback={(phase == 2 ? 0 : 9)} recoveryRequested={(recovered ? 1 : 0)}";
                return ActualChurn is not null && phase == 4 ? AfterRealProgress(body, token) : Task.FromResult(Reply(body));
            }

            HostCheck.Equal(HttpMethod.Post, request.Method);
            HostCheck.Equal(0L, request.Content!.Headers.ContentLength!.Value);
            string expected = path == "/__clros/churn" ? (eventOwned && churnStarted == 0 ? "v2" : "v1") : eventOwned && path.StartsWith("/__clros/signalrace", StringComparison.Ordinal) ? "v3" : "v1";
            HostCheck.True(request.Headers.GetValues(path == "/__clros/churn" ? "X-CLROS-Churn" : "X-CLROS-Signal").SequenceEqual(new[] { expected }));
            if (path == "/__clros/churn")
                HostCheck.True(!request.Headers.Contains("X-CLROS-Signal"));
            switch (path)
            {
                case "/__clros/signalrace":
                    if (raceStarted)
                        return Task.FromResult(Reply("CLROS_SIGNAL_RACE_BUSY", 409));
                    HostCheck.Equal(0, churnStarted);
                    raceStarted = true;
                    token.Register(() =>
                    {
                        RaceCanceled = true;
                        race.TrySetCanceled(token);
                    });
                    return race.Task;
                case "/__clros/churn":
                    churnStarted++;
                    if (ActualChurn is not null)
                        return RealChurn(expected, token);
                    if (churnStarted == 2)
                    {
                        complete = true;
                        return Task.FromResult(Reply(Complete));
                    }

                    HostCheck.True(raceStarted && InitialSeen && !released);
                    token.Register(() =>
                    {
                        ChurnCanceled = true;
                        churn.TrySetCanceled(token);
                    });
                    return churn.Task;
                case "/__clros/signalrace/start":
                    Starts++;
                    HostCheck.True(InitialSeen && churnStarted == 1 && !ChurnCanceled && !released);
                    if (RefuseStart)
                        return Task.FromResult(Reply("CLROS_SIGNAL_RACE_START_REFUSED", 409));
                    released = true;
                    return Task.FromResult(Reply("CLROS_SIGNAL_RACE_START_REQUESTED"));
                case "/__clros/signalrace/recover":
                    if (recovered)
                        return Task.FromResult(Reply("CLROS_SIGNAL_RACE_RECOVERY_REFUSED", 409));
                    recovered = true;
                    race.TrySetResult(Reply(eventOwned && !LegacyResultPrefix ? Success.Replace("CLROS_SIGNAL_RACE_OK", "CLROS_SIGNAL_RACE_V3_OK", StringComparison.Ordinal) : Success));
                    return Task.FromResult(Reply("CLROS_SIGNAL_RACE_RECOVERY_REQUESTED"));
                case "/__clros/signalprobe":
                    return Task.FromResult(Reply("CLROS_SIGNAL_PROBE_BUSY", 409));
                default:
                    throw new InvalidOperationException(path);
            }
        }

        private async Task<HttpResponseMessage> AfterRealProgress(string body, CancellationToken token)
        {
            while (ActualChurn!.Inspect() is not { Workers: >= 9, Waiting: 1 } && ActualChurn!.Inspect().Completed == 0)
                await Task.Delay(1, token);
            return Reply(body);
        }

        private async Task<HttpResponseMessage> RealChurn(string version, CancellationToken token)
        {
            try
            {
                string? result = version == "v2" && !FiniteInitialChurn ? await ActualChurn!.RunUntilCanceledAsync(token) : await ActualChurn!.RunAsync(token);
                HostCheck.True(result is not null);
                complete = true;
                return Reply(result!);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                ChurnCanceled = true;
                throw;
            }
        }
    }
}
