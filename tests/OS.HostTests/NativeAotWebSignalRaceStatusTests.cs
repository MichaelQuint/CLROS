using System.Net;
using System.Text;
using System.Text.Json;
using OS.Tooling;

// Copied HTTP values exercise the real controller; no native signal or guest
// execution, callback role, process completion or syscall order is established.
internal static class NativeAotWebSignalRaceStatusTests
{
    private const string Endpoint = "http://127.0.0.1:23457", Health = "OS_MINIMAL_API_OK";
    private static string Status(int phase, int waiter = 7, int actor = 8, int callback = 9, int requested = 0) =>
        $"CLROS_SIGNAL_RACE_STATUS phase={phase} pid=43 waiter={waiter} actor={actor} callback={callback} recoveryRequested={requested}";

    private sealed class Peer(params string[] statuses) : HttpMessageHandler
    {
        internal bool ChurnJoined, RaceJoined;
        private bool churnStarted, raceStarted;
        private int position;
        private static HttpResponseMessage Reply(string body) => new(HttpStatusCode.OK)
        { Content = new StringContent(body, Encoding.UTF8, "text/plain") };

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            string path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Get)
            {
                if (path == "/healthz") return Task.FromResult(Reply(raceStarted ? "WRONG_HEALTH" : Health));
                if (path == "/__clros/signalrace/status")
                {
                    if (!raceStarted) return Task.FromResult(Reply(Status(0, 0, 0, 0).Replace("pid=43", "pid=0", StringComparison.Ordinal)));
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
            if (path == "/__clros/churn") { churnStarted = true; return Hold(false, token); }
            HostCheck.Equal("/__clros/signalrace", path); raceStarted = true; return Hold(true, token);
        }

        private async Task<HttpResponseMessage> Hold(bool race, CancellationToken token)
        {
            try { await Task.Delay(Timeout.Infinite, token); throw new InvalidOperationException("Unexpected pending completion."); }
            finally { if (race) RaceJoined = true; else ChurnJoined = true; }
        }
    }

    private static async Task Refuse(string reason, params string[] statuses)
    {
        string directory = Path.GetFullPath(Path.Combine("artifacts", "host-tests", "race-status-" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(directory); string path = Path.Combine(directory, "observations.json");
        using var peer = new Peer(statuses); using var client = new HttpClient(peer);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10)); string? failure = null;
        try { await NativeAotWebPayload.CaptureSignalRaceAsync(client, Endpoint, Health, path, deadline.Token); }
        catch (InvalidDataException error) { failure = error.Message; }
        HostCheck.Equal("web_signalrace_" + reason, failure);
        HostCheck.True(peer.ChurnJoined && peer.RaceJoined && File.Exists(path));
        string json = File.ReadAllText(path);
        using var partial = JsonDocument.Parse(json);
        HostCheck.Equal("clros.web.signalrace.v1", partial.RootElement.GetProperty("schema").GetString());
        HostCheck.True(partial.RootElement.GetProperty("samples").GetArrayLength() < 19);
        bool rejected = false;
        try { NativeAotWebPayload.ValidateSignalRaceEvidence(json, Endpoint, Health); }
        catch (InvalidDataException) { rejected = true; }
        HostCheck.True(rejected);
    }

    [HostTest("web race cleanup status preserves unknown callback and fails the explicit recovery gate")]
    private static async Task Cleanup()
    {
        await Refuse("missed_recovery_gate", "CLROS_SIGNAL_RACE_STATUS phase=6 pid=2822 waiter=2842 actor=2843 callback=0 recoveryRequested=0");
        await Refuse("missed_recovery_gate", Status(6, 0, 0, 0));
        await Refuse("missed_recovery_gate", Status(6));
    }

    [HostTest("web race failed status permits skipped roles without promoting the failed attempt")]
    private static async Task Failed()
    {
        foreach (string status in new[] { Status(8, 0, 0, 0), Status(8, 7, 0, 0), Status(8, 0, 8, 0), Status(8, 0, 0, 9), Status(8, requested: 1) })
            await Refuse("missed_recovery_gate", status);
    }

    [HostTest("web race acknowledged recovery and success statuses keep strict known roles")]
    private static async Task PositiveRoles()
    {
        await Refuse("missed_recovery_gate", Status(2, callback: 0), Status(6, callback: 0));
        await Refuse("missed_recovery_gate", Status(3), Status(6, callback: 0));
        await Refuse("health_response", Status(4));
        await Refuse("missed_recovery_gate", Status(5));
        await Refuse("missed_recovery_gate", Status(7, requested: 1));
        foreach (int phase in new[] { 3, 4, 7 })
        {
            await Refuse("status_callback", Status(phase, callback: 0));
            await Refuse("status_callback", Status(phase, callback: 7));
            await Refuse("status_threads", Status(phase, waiter: 0));
            await Refuse("status_threads", Status(phase, actor: 7));
        }
    }

    [HostTest("web race status rejects malformed ranges aliases stale gates and noncanonical fields")]
    private static async Task Malformed()
    {
        await Refuse("status_range", Status(9));
        await Refuse("status_range", Status(6, requested: 2));
        await Refuse("status_range", Status(6).Replace("pid=43", "pid=2147483648", StringComparison.Ordinal));
        await Refuse("idle", Status(0));
        await Refuse("status_threads", Status(2, 0, 0, 0));
        await Refuse("status_threads", Status(6, actor: 7, callback: 0));
        await Refuse("status_callback", Status(8, callback: 7));
        await Refuse("status_request", Status(2, callback: 0, requested: 1));
        await Refuse("pid", Status(6, 0, 0, 0).Replace("pid=43", "pid=0", StringComparison.Ordinal));
        await Refuse("number", Status(6, 0, 0, 0).Replace("phase=6", "phase=06", StringComparison.Ordinal));
        await Refuse("number", Status(6).Replace("waiter=7", "waiter=-1", StringComparison.Ordinal));
        await Refuse("field_name", Status(6).Replace("actor=8 callback=9", "callback=9 actor=8", StringComparison.Ordinal));
        await Refuse("fields", Status(6) + " callback=0");
    }
}
