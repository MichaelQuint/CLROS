using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using OS.Tooling;

// Independent copied Linux observations. These tests run no scheduler, process,
// network or guest operations and do not confer native execution authority.
internal static class NativeAotWebSignalRaceScheduleEvidenceTests
{
    private const string Endpoint = "http://127.0.0.1:23456", Health = "OS_MINIMAL_API_OK";
    private static readonly QemuTranscripts.WebSignalRaceSelection Selection = new(43, 44, 45, 46, 46);
    private static string Mask => "01" + new string('0', 8190);
    private const string Syscall = "232 0xc 0x700000 0x1 0x9c4 0x0 0x0 0x700100 0x400000";
    private const string Success = "CLROS_SIGNAL_RACE_OK signal=12 pid=43 actor=45 waiter=44 callback=46 callbacks=1 actorJoined=1 waiterJoined=1 consumed=90 recovery=107 recoveryWriter=46 initialValue=-1 initialErrno=4 recoveryValue=1 recoveryEvents=1 recoveryTag=344978309701 handler=4096 flags=335544324 restorer=8192 actionMask=0 originalHandler=0 originalFlags=0 originalRestorer=0 originalActionMask=0 maskBefore=2048 maskDuring=0 maskAfter=2048 altAddress=0 altFlags=2 altBytes=0 descriptorsClosed=3 actionRestored=1 altUnchanged=1";

    // Start time is a coarse kernel tick, so two genuine threads may share it.
    private static ulong Start(int tid) => tid == 42 ? 70UL : tid == 43 ? 80UL : 90UL;
    private static object Stat(int tid, string state) => new
        { Tid = tid, Parent = tid == 42 ? 1 : 42, Group = 42, Session = 42, Start = Start(tid), State = state };
    private static object Policy(int tid, int policy, int priority) => new
        { Tid = tid, Start = Start(tid), Policy = policy, Priority = priority, Affinity = Mask };
    private static object Pipe() => new
    {
        Epoll = 12, Read = 10, Write = 11, Inode = "pipe:[555]",
        EpollInfo = "pos:\t0\nflags:\t02000002\ntfd: 10 events: 19 data: 50524f4245 pos:0 ino:22b sdev:10\n",
        ReadInfo = "pos:\t0\nflags:\t02004000\nino:\t555\n",
        WriteInfo = "pos:\t0\nflags:\t02004001\nino:\t555\n",
        Syscall, WaitChannel = "ep_poll", WaiterState = Stat(44, "S")
    };
    private static List<JsonObject> Journal(bool retired = false)
    {
        List<JsonObject> rows = [];
        void Add(string operation, object observation) => rows.Add(JsonSerializer.SerializeToNode(new
        {
            schema = "clros.web.signalrace.schedule.v1", operation, observation,
            utc = "2026-10-20T12:00:00+00:00", monotonic = rows.Count * 10L, frequency = 1_000_000L
        })!.AsObject());
        foreach (int tid in new[] { 42, 43, 44, 45 })
        {
            int pid = tid == 42 ? 42 : 43;
            Add("preflight", new
            {
                Pid = pid, Tid = tid, Initial = Stat(tid, tid == 42 ? "R" : tid == 44 ? "t" : "S"),
                taskStatus = $"Name:\tworker\nTgid:\t{pid}\nPid:\t{tid}\nTracerPid:\t{(tid == 42 ? 0 : 42)}\n",
                online = "0", affinity = Mask, policy = 0, priority = 0
            });
        }
        Add("initial-wait", new { waiter = 44, pipe = Pipe() });
        Add("tracer-raised", Policy(42, 1, 2)); Add("actor-raised", Policy(45, 1, 1));
        for (int i = 0; i < 2; i++) Add("release-preflight", new
            { pipe = Pipe(), policies = new[] { Policy(42, 1, 2), Policy(43, 0, 0), Policy(44, 0, 0), Policy(45, 1, 1) } });
        Add("start-post-intent", new { actor = 45, Start = Start(45) });
        Add("tracer-restored", Policy(42, 0, 0));
        if (retired) Add("actor-retired", new { Tid = 45, Start = Start(45), nativeExit = true, reapClaim = false });
        Add("owned-session-stopped", new
        {
            Id = 42, nativeExit = true, reapClaim = false,
            tasks = new[] { 42, 43, 44, 45 }.Select(tid => new { Pid = tid == 42 ? 42 : 43, Tid = tid, Start = Start(tid), nativeExit = true }).ToArray()
        });
        return rows;
    }
    private static string Text(IEnumerable<JsonObject> rows) => string.Join('\n', rows.Select(row => row.ToJsonString())) + "\n";
    private static NativeAotWebPayload.SignalRaceScheduleSummary Read(List<JsonObject> rows) =>
        NativeAotWebPayload.InspectSignalRaceScheduleEvidence(Text(rows), Selection);
    private static void Reject(Action action, string? expected = null)
    {
        string? reason = null;
        try { action(); } catch (Exception error) when (error is InvalidDataException or JsonException) { reason = error.Message; }
        HostCheck.True(reason is not null); if (expected is not null) HostCheck.Equal(expected, reason);
    }

    [HostTest("web race schedule accepts complete owned roles with actual later blocked wait and optional actor retirement")]
    private static void CompleteJournal()
    {
        foreach (bool retired in new[] { false, true })
        {
            var result = Read(Journal(retired));
            HostCheck.Equal(new NativeAotWebPayload.SignalRaceScheduleSummary(42, 43, 44, 45, 12, 10, 11, retired ? 13 : 12), result);
        }
        // The original waiter snapshot was tracing-stopped; only the later S
        // observations establish the copied wait. Equal worker ticks are valid.
        var sameTick = Journal(); sameTick[1]["monotonic"] = 0L; _ = Read(sameTick);
    }

    [HostTest("web race schedule refuses incomplete reordered un-restored and falsely stopped journals")]
    private static void CompletionRefusals()
    {
        foreach (Action<List<JsonObject>> change in new Action<List<JsonObject>>[]
        {
            rows => rows.RemoveAt(rows.Count - 1),
            rows => rows[8]["operation"] = "start-post-intent",
            rows => rows[10]["operation"] = "tracer-retired",
            rows => rows[10]["observation"]!["Policy"] = 1,
            rows => rows[^1]["observation"]!["nativeExit"] = false,
            rows => rows[^1]["observation"]!["reapClaim"] = true,
            rows => rows[^1]["observation"]!["tasks"]![3]!["nativeExit"] = false,
            rows => rows[^1]["observation"]!["tasks"]![3]!["Start"] = 91UL,
            rows => rows[8]["frequency"] = 99L,
            rows => rows[8]["monotonic"] = 1L,
            rows => rows[0]["schema"] = "clros.web.signalrace.schedule.v0"
        }) { var rows = Journal(); change(rows); Reject(() => Read(rows)); }
        string text = Text(Journal());
        Reject(() => NativeAotWebPayload.InspectSignalRaceScheduleEvidence(text.TrimEnd('\n'), Selection));
        Reject(() => NativeAotWebPayload.InspectSignalRaceScheduleEvidence(text + text, Selection));
        Reject(() => NativeAotWebPayload.InspectSignalRaceScheduleEvidence(text.Replace("\"schema\":", "\"schema\":\"duplicate\",\"schema\":", StringComparison.Ordinal), Selection));
    }

    [HostTest("web race schedule refuses substituted roles generations CPU policies and unblocked or changed pipe observations")]
    private static void ObservationRefusals()
    {
        foreach (Action<List<JsonObject>> change in new Action<List<JsonObject>>[]
        {
            rows => rows[3]["observation"]!["Pid"] = 45,
            rows => rows[3]["observation"]!["taskStatus"] = "Tgid:\t43\nPid:\t45\nTracerPid:\t99\n",
            rows => rows[2]["observation"]!["Initial"]!["Session"] = 43,
            rows => rows[0]["observation"]!["affinity"] = "03" + new string('0', 8190),
            rows => rows[3]["observation"]!["online"] = "0-1",
            rows => rows[7]["observation"]!["policies"]![3]!["Start"] = 91UL,
            rows => rows[7]["observation"]!["policies"]![2]!["Policy"] = 1,
            rows => rows[9]["observation"]!["actor"] = 44,
            rows => rows[4]["observation"]!["pipe"]!["WaiterState"]!["State"] = "R",
            rows => rows[8]["observation"]!["pipe"]!["Syscall"] = Syscall.Replace("0x9c4", "0x0", StringComparison.Ordinal),
            rows => rows[4]["observation"]!["pipe"]!["ReadInfo"] = "flags:\t02004001\nino:\t555\n",
            rows => rows[4]["observation"]!["pipe"]!["WriteInfo"] = "flags:\t02004001\nino:\t556\n",
            rows => rows[4]["observation"]!["pipe"]!["EpollInfo"] = "tfd: 10 events: 19 data: 0 pos:0\n",
            rows => rows[8]["observation"]!["pipe"]!["WaitChannel"] = "futex_wait_queue"
        }) { var rows = Journal(); change(rows); Reject(() => Read(rows)); }
        Reject(() => NativeAotWebPayload.InspectSignalRaceScheduleEvidence(Text(Journal()), Selection with { ActorTid = 47 }));
    }

    private static (string Selected, string Attempts) Http(bool controlled)
    {
        List<JsonObject> samples = [], attempts = [], links = [];
        string Churn(int started, int completed, int canceled, int active, int work) =>
            $"CLROS_CHURN_STATUS active={active} waiting={active} peakActive={(started == 0 ? 0 : 1)} started={started} completed={completed} canceled={canceled} failed=0 rejected=0 timers={work} workers={work} collections={work} heldBytes={active * 65536} peakHeldBytes={(started == 0 ? 0 : 65536)}";
        string Status(int phase) => phase == 0 ? "CLROS_SIGNAL_RACE_STATUS phase=0 pid=0 waiter=0 actor=0 callback=0 recoveryRequested=0" :
            $"CLROS_SIGNAL_RACE_STATUS phase={phase} pid=43 waiter=44 actor=45 callback={(phase == 2 ? 0 : 46)} recoveryRequested={(phase == 7 ? 1 : 0)}";
        int completion = 0;
        int Attempt(string path, string method = "GET")
        {
            int id = attempts.Count + 1;
            attempts.Add(new() { ["id"] = id, ["method"] = method, ["path"] = path }); return id;
        }
        void Select(string kind, int id, string body, int status = 200)
        {
            var attempt = attempts[id - 1];
            attempt["completion"] = ++completion; attempt["headers"] = status != 0; attempt["status"] = status;
            attempt["mediaType"] = status == 0 ? "" : "text/plain"; attempt["mediaTypeComplete"] = true;
            attempt["bodyBase64"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(body)); attempt["bodyComplete"] = status != 0;
            attempt["outcome"] = status == 0 ? "cancelled" : "response"; attempt["error"] = status == 0 ? "cancelled" : "";
            links.Add(new() { ["sample"] = samples.Count, ["attempt"] = status == 0 ? 0 : id, ["related"] = status == 0 ? id : 0 });
            samples.Add(new() { ["kind"] = kind, ["status"] = status, ["body"] = body });
        }
        void Get(string kind, string path, string body) => Select(kind, Attempt(path), body);
        void Post(string kind, string path, string body, int status = 200) => Select(kind, Attempt(path, "POST"), body, status);
        Get("baseline-churn", "/__clros/churn/status", Churn(0, 0, 0, 0, 0));
        Get("baseline-race", "/__clros/signalrace/status", Status(0)); Get("health-before", "/healthz", Health);
        int race = 0;
        if (controlled) { race = Attempt("/__clros/signalrace", "POST"); Get("initial-armed", "/__clros/signalrace/status", Status(2)); }
        int churn = Attempt("/__clros/churn", "POST"); Get("churn-pending", "/__clros/churn/status", Churn(1, 0, 0, 1, 1));
        if (controlled) Post("start-request", "/__clros/signalrace/start", "CLROS_SIGNAL_RACE_START_REQUESTED");
        else race = Attempt("/__clros/signalrace", "POST");
        Get("recovery-armed", "/__clros/signalrace/status", Status(4)); Get("churn-overlap", "/__clros/churn/status", Churn(1, 0, 0, 1, 2));
        Get("overlap-health", "/healthz", Health); Select("churn-cancel", churn, "", 0);
        Get("churn-drained", "/__clros/churn/status", Churn(1, 0, 1, 0, 2));
        Post("recovery-request", "/__clros/signalrace/recover", "CLROS_SIGNAL_RACE_RECOVERY_REQUESTED");
        Post("recovery-repeat", "/__clros/signalrace/recover", "CLROS_SIGNAL_RACE_RECOVERY_REFUSED", 409); Select("race-complete", race, Success);
        Get("race-final", "/__clros/signalrace/status", Status(7)); Get("health-after", "/healthz", Health);
        Post("churn-complete", "/__clros/churn", "CLROS_CHURN_OK iterations=8 bytes=65536 timers=8 workers=8 collections=8");
        Get("final-churn", "/__clros/churn/status", Churn(2, 1, 1, 0, 10)); Get("health-final", "/healthz", Health);
        Post("repeat-race", "/__clros/signalrace", "CLROS_SIGNAL_RACE_BUSY", 409); Post("repeat-probe", "/__clros/signalprobe", "CLROS_SIGNAL_PROBE_BUSY", 409);
        string selected = JsonSerializer.Serialize(new { schema = controlled ? "clros.web.signalrace.v2" : "clros.web.signalrace.v1", endpoint = Endpoint, samples });
        string companion = JsonSerializer.Serialize(new { schema = controlled ? "clros.web.signalrace.attempts.v2" : "clros.web.signalrace.attempts.v1", endpoint = Endpoint,
            semanticComplete = true, failure = "", attempts, selections = links });
        return (selected, companion);
    }

    [HostTest("web native v7 requires paired v2 evidence and complete journal while v5 and v6 preserve historical v1")]
    private static void ReceiptPairing()
    {
        var old = Http(false); var current = Http(true); string journal = Text(Journal());
        QemuTranscripts.WebSignalRaceSelection Read(string schema, string selected, string? attempts, string? schedule) =>
            NativeAotWebPayload.ValidateSignalRaceQualificationEvidence(schema, selected, attempts, schedule, Endpoint, Health);
        HostCheck.Equal(Selection, Read("clros.nativeaot.web.v5", old.Selected, null, null));
        HostCheck.Equal(Selection, Read("clros.nativeaot.web.v6", old.Selected, old.Attempts, null));
        HostCheck.Equal(Selection, Read("clros.nativeaot.web.v7", current.Selected, current.Attempts, journal));
        Reject(() => Read("clros.nativeaot.web.v6", current.Selected, current.Attempts, null), "web_signalrace_schedule_protocol_pairing");
        Reject(() => Read("clros.nativeaot.web.v7", old.Selected, old.Attempts, journal), "web_signalrace_schedule_protocol_pairing");
        Reject(() => Read("clros.nativeaot.web.v7", current.Selected, current.Attempts, null), "web_signalrace_schedule_required");
        Reject(() => Read("clros.nativeaot.web.v7", current.Selected, null, journal), "web_signalrace_schedule_attempts_required");
        Reject(() => Read("clros.nativeaot.web.v7", current.Selected, old.Attempts, journal));
        Reject(() => Read("clros.nativeaot.web.v6", old.Selected, old.Attempts, journal), "web_signalrace_schedule_historical_journal");
        Reject(() => Read("clros.nativeaot.web.v4", current.Selected, current.Attempts, journal), "web_signalrace_schedule_native_schema");
    }
}
