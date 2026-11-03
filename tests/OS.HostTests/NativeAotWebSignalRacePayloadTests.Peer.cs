using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using OS.Tooling;

internal static partial class NativeAotWebSignalRacePayloadTests
{
    private sealed class Peer : HttpMessageHandler
    {
        internal bool RefuseRace, CompleteEarly, ChurnCanceled, RaceCanceled, StaleProgress, UnselectedPolls;
        internal Action? CancelOnArm, CancelAfterStale;
        private bool raceStarted, recovered, complete;
        private int churnStarted, overlapPolls, pendingPolls, racePolls;
        private readonly TaskCompletionSource<HttpResponseMessage> churn = new(TaskCreationOptions.RunContinuationsAsynchronously), race = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private static HttpResponseMessage Reply(string body, int status = 200) => new((HttpStatusCode)status)
        {
            Content = new StringContent(body, Encoding.UTF8, "text/plain")
        };
        private string Status()
        {
            int active = churnStarted == 1 && !ChurnCanceled && !complete ? 1 : 0, work = churnStarted == 0 ? 0 : complete ? 10 : raceStarted && !StaleProgress ? 2 : 1;
            return $"CLROS_CHURN_STATUS active={active} waiting={active} peakActive={(churnStarted == 0 ? 0 : 1)} started={churnStarted} completed={(complete ? 1 : 0)} canceled={(ChurnCanceled ? 1 : 0)} failed=0 rejected=0 timers={work} workers={work} collections={work} heldBytes={active * 65536} peakHeldBytes={(churnStarted == 0 ? 0 : 65536)}";
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            string path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Get)
            {
                if (path == "/healthz")
                    return Task.FromResult(Reply(Health));
                if (path == "/__clros/churn/status")
                {
                    if (raceStarted && StaleProgress && ++overlapPolls == 3)
                    {
                        CancelAfterStale?.Invoke();
                        token.ThrowIfCancellationRequested();
                    }

                    if (UnselectedPolls && churnStarted == 1 && !raceStarted && pendingPolls++ == 0)
                        return Task.FromResult(Reply(Status().Replace("timers=1 workers=1 collections=1", "timers=0 workers=0 collections=0", StringComparison.Ordinal)));
                    return Task.FromResult(Reply(Status()));
                }

                if (path == "/__clros/signalrace/status")
                {
                    if (raceStarted)
                    {
                        CancelOnArm?.Invoke();
                        token.ThrowIfCancellationRequested();
                        if (CompleteEarly)
                        {
                            complete = true;
                            churn.TrySetResult(Reply(Complete));
                        }
                    }

                    string body = raceStarted ? $"CLROS_SIGNAL_RACE_STATUS phase={(recovered ? 7 : 4)} pid=43 waiter=7 actor=8 callback=9 recoveryRequested={(recovered ? 1 : 0)}" : "CLROS_SIGNAL_RACE_STATUS phase=0 pid=0 waiter=0 actor=0 callback=0 recoveryRequested=0";
                    if (UnselectedPolls && raceStarted && racePolls++ == 0)
                        body = "CLROS_SIGNAL_RACE_STATUS phase=2 pid=43 waiter=7 actor=8 callback=0 recoveryRequested=0";
                    return Task.FromResult(Reply(body));
                }
            }

            HostCheck.Equal(HttpMethod.Post, request.Method);
            HostCheck.Equal(0L, request.Content!.Headers.ContentLength!.Value);
            string header = path == "/__clros/churn" ? "X-CLROS-Churn" : "X-CLROS-Signal";
            HostCheck.True(request.Headers.GetValues(header).SequenceEqual(new[] { "v1" }));
            switch (path)
            {
                case "/__clros/churn":
                    churnStarted++;
                    if (churnStarted == 2)
                    {
                        complete = true;
                        return Task.FromResult(Reply(Complete));
                    }

                    token.Register(() =>
                    {
                        ChurnCanceled = true;
                        churn.TrySetCanceled(token);
                    });
                    return churn.Task;
                case "/__clros/signalrace":
                    if (raceStarted)
                        return Task.FromResult(Reply("CLROS_SIGNAL_RACE_BUSY", 409));
                    raceStarted = true;
                    if (RefuseRace)
                        return Task.FromResult(Reply("CLROS_SIGNAL_RACE_UNSUPPORTED", 501));
                    token.Register(() =>
                    {
                        RaceCanceled = true;
                        race.TrySetCanceled(token);
                    });
                    return race.Task;
                case "/__clros/signalrace/recover":
                    if (recovered)
                        return Task.FromResult(Reply("CLROS_SIGNAL_RACE_RECOVERY_REFUSED", 409));
                    recovered = true;
                    race.TrySetResult(Reply(Success));
                    return Task.FromResult(Reply("CLROS_SIGNAL_RACE_RECOVERY_REQUESTED"));
                case "/__clros/signalprobe":
                    return Task.FromResult(Reply("CLROS_SIGNAL_PROBE_BUSY", 409));
                default:
                    throw new InvalidOperationException("Unexpected HTTP path: " + path);
            }
        }
    }
}
