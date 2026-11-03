using System.Net;
using System.Text;
using System.Text.Json;
using OS.Tooling;

// Copied HTTP values exercise the real controller; no native signal or guest
// execution, callback role, process completion or syscall order is established.
internal static partial class NativeAotWebSignalRaceStatusTests
{
    private sealed class Peer(params string[] statuses) : HttpMessageHandler
    {
        internal bool ChurnJoined, RaceJoined;
        private bool churnStarted, raceStarted;
        private int position;
        private static HttpResponseMessage Reply(string body) => new(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "text/plain")
        };
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            string path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Get)
            {
                if (path == "/healthz")
                    return Task.FromResult(Reply(raceStarted ? "WRONG_HEALTH" : Health));
                if (path == "/__clros/signalrace/status")
                {
                    if (!raceStarted)
                        return Task.FromResult(Reply(Status(0, 0, 0, 0).Replace("pid=43", "pid=0", StringComparison.Ordinal)));
                    HostCheck.True(position < statuses.Length);
                    return Task.FromResult(Reply(statuses[position++]));
                }

                if (path == "/__clros/churn/status")
                {
                    int active = churnStarted ? 1 : 0, work = raceStarted ? 2 : active;
                    return Task.FromResult(Reply($"CLROS_CHURN_STATUS active={active} waiting={active} peakActive={active} started={active} completed=0 canceled=0 failed=0 rejected=0 timers={work} workers={work} collections={work} heldBytes={active * 65536} peakHeldBytes={active * 65536}"));
                }
            }

            HostCheck.Equal(HttpMethod.Post, request.Method);
            HostCheck.Equal(0L, request.Content!.Headers.ContentLength!.Value);
            HostCheck.True(request.Headers.GetValues(path == "/__clros/churn" ? "X-CLROS-Churn" : "X-CLROS-Signal").SequenceEqual(new[] { "v1" }));
            if (path == "/__clros/churn")
            {
                churnStarted = true;
                return Hold(false, token);
            }

            HostCheck.Equal("/__clros/signalrace", path);
            raceStarted = true;
            return Hold(true, token);
        }

        private async Task<HttpResponseMessage> Hold(bool race, CancellationToken token)
        {
            try
            {
                await Task.Delay(Timeout.Infinite, token);
                throw new InvalidOperationException("Unexpected pending completion.");
            }
            finally
            {
                if (race)
                    RaceJoined = true;
                else
                    ChurnJoined = true;
            }
        }
    }
}
