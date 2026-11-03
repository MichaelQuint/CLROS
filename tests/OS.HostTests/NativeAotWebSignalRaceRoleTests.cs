using System.Text;
using System.Text.Json.Nodes;
using OS.Tooling;
using Q = OS.Tooling.QemuTranscripts;
using Peer = OS.Tooling.NativeAotServiceQuotaPeer;
using Values = NativeAotWebSignalRaceHostHttpTests.Values;

// Complete synthetic replay plus strict copied HTTP/stream values. No original
// process, native code, pointed-to bytes, actual GC or guest evidence is supplied.
internal static partial class NativeAotWebSignalRaceRoleTests
{
    private const string Endpoint = "http://127.0.0.1:22080", Health = "OS_MINIMAL_API_OK";
    private const string Success = "CLROS_SIGNAL_RACE_OK signal=12 pid=1 actor=3 waiter=1 callback=2 callbacks=1 actorJoined=1 waiterJoined=1 consumed=90 recovery=107 recoveryWriter=2 initialValue=-1 initialErrno=4 recoveryValue=1 recoveryEvents=1 recoveryTag=344978309701 handler=4096 flags=335544324 restorer=8192 actionMask=0 originalHandler=0 originalFlags=0 originalRestorer=0 originalActionMask=0 maskBefore=2048 maskDuring=0 maskAfter=2048 altAddress=0 altFlags=2 altBytes=0 descriptorsClosed=3 actionRestored=1 altUnchanged=1";
    private static string Data(string text) => Convert.ToBase64String(Encoding.ASCII.GetBytes(text));
    private static string Request(string path, string method, bool eventOwned = false) => method + " " + path + " HTTP/1.1\r\nHost: 127.0.0.1:22080\r\n" + (method == "POST" ? "Content-Length: 0\r\n" : "") + (eventOwned && path.StartsWith("/__clros/signalrace", StringComparison.Ordinal) ? "X-CLROS-Signal: v3\r\n" : method == "POST" ? (path == "/__clros/churn" ? "X-CLROS-Churn" : "X-CLROS-Signal") + ": v1\r\n" : "") + "\r\n";
    private static string Response(string body, int status = 200) => $"HTTP/1.1 {status} Result\r\nContent-Type: text/plain\r\nContent-Length: {body.Length}\r\n\r\n" + body;
    private static string Churn(int started, int completed, int canceled, int active, int work) => $"CLROS_CHURN_STATUS active={active} waiting={active} peakActive={(started == 0 ? 0 : 1)} started={started} completed={completed} canceled={canceled} failed=0 rejected=0 timers={work} workers={work} collections={work} heldBytes={active * 65536} peakHeldBytes={(started == 0 ? 0 : 65536)}";
    private static string Status(int phase) => phase == 0 ? "CLROS_SIGNAL_RACE_STATUS phase=0 pid=0 waiter=0 actor=0 callback=0 recoveryRequested=0" : $"CLROS_SIGNAL_RACE_STATUS phase={phase} pid=1 waiter=1 actor=3 callback={(phase == 2 ? 0 : 2)} recoveryRequested={(phase == 7 ? 1 : 0)}";
    private static void Reject(Action action, string? expected = null)
    {
        string? reason = null;
        try
        {
            action();
        }
        catch (ToolFailure error)
        {
            reason = error.Reason;
        }
        catch (InvalidDataException error)
        {
            reason = error.Message;
        }

        HostCheck.True(reason is not null);
        if (expected is not null)
            HostCheck.Equal(expected, reason);
    }

    [HostTest("web race roles bind complete model original socket CALLs and all nineteen selected HTTP witnesses")]
    private static void CompleteRoles()
    {
        var fixture = new Fixture();
        var result = fixture.Read();
        HostCheck.True(result.Selection == fixture.Replay.Selection && result.Samples.Count == 19 && result.Order.Count > 0 && result.Streams.Bindings.Count == 4);
        var canceled = result.Samples[7];
        HostCheck.True(canceled.HostCancellation && canceled.Response is null && canceled.Request.Packet.Kind == "cancel-request");
        HostCheck.True(result.Temporal.Intervals.Count == 36 && result.Samples.Where(s => !s.HostCancellation).All(s => s.Response is not null));
        HostCheck.True(result.Order.All(o => o.BeforeRecordIndex < o.AfterRecordIndex) && result.Samples.SelectMany(s => s.Request.Slices).All(s => fixture.Replay.SocketCalls.Contains(s.Original)));
        bool immutable = false;
        try
        {
            ((IList<Q.WebSignalRaceRoleSample>)result.Samples).Clear();
        }
        catch (NotSupportedException)
        {
            immutable = true;
        }

        HostCheck.True(immutable);
    }

    [HostTest("web race roles reject copied selected body and identity substitution despite complete native model")]
    private static void BodyAndRoles()
    {
        var fixture = new Fixture();
        fixture.Host.Samples[4]["body"] = Status(4).Replace("actor=3", "actor=99", StringComparison.Ordinal);
        Reject(() => fixture.Read());
        fixture = new Fixture();
        fixture.Host.Samples[6]["body"] = "FORGED_HEALTH";
        Reject(() => fixture.Read());
        fixture = new Fixture();
        fixture.Host.Links[4]["attempt"] = fixture.Host.Links[5]["attempt"]!.DeepClone();
        Reject(() => fixture.Read());
    }

    [HostTest("web race roles require original interrupt recovery and explicit cleanup boundaries before claimed HTTP phases")]
    private static void NativeBoundaries()
    {
        Reject(() => new Fixture(earlyOverlap: true).Read(), "web_race_roles_order_actor_read_before_armed");
        Reject(() => new Fixture(beforeCleanup: true).Read(), "web_race_roles_order_recovery-read_before_race_reply");
    }

    [HostTest("web race canceled request coverage permits actual unknown-write delivery without fabricating cancellation response")]
    private static void UnknownCanceledWrite()
    {
        var fixture = new Fixture(unknownCancel: true);
        var result = fixture.Read();
        var canceled = result.Samples[7];
        HostCheck.True(canceled.HostCancellation && canceled.Response is null && canceled.Request.Slices.Count > 0 && result.Streams.Host.Prefixes.Connections.Single(c => c.ConnectionId == canceled.Request.Packet.ConnectionId).Writes.UnknownWrite is not null);
        HostCheck.True(result.Streams.Bindings.Single(b => b.ConnectionId == canceled.Request.Packet.ConnectionId).Compared.MinimumAttemptedWritePrefixBytes == Request("/__clros/churn", "POST").Length);
        var guest = fixture.Guest();
        var flows = guest.Streams.ToArray();
        var churn = flows.Single(s => s.RemotePort == 43001);
        var range = churn.Requests.FirstRanges[0];
        byte[] wrong = Convert.FromBase64String(range.DataBase64);
        wrong[^1] = (byte)'X';
        flows[Array.IndexOf(flows, churn)] = churn with
        {
            Requests = churn.Requests with
            {
                KnownPrefixBase64 = Convert.ToBase64String(wrong),
                FirstRanges = [range with
                {
                    DataBase64 = Convert.ToBase64String(wrong)
                }

                ]
            }
        };
        Reject(() => Q.InspectNativeAotWebSignalRaceRolesForTests(fixture.Wire(), fixture.Attempts(), fixture.Host.Selected(), Endpoint, Health, guest with { Streams = flows }, fixture.Replay), "quota_stream_join_unmatched");
        Reject(() => new Fixture(unknownCancel: true, peekCancel: true).Read(), "web_race_roles_cancel_request");
    }

    [HostTest("web race v2 start request consumes after actual initial wait and before actor write without ordering its reply")]
    private static void InitialStartBoundary()
    {
        var fixture = new Fixture(startGate: true);
        var report = fixture.Read();
        HostCheck.True(report.Samples.Count == 21 && report.Temporal.Intervals.Count == 40 && report.Selection == fixture.Replay.Selection);
        int wait = fixture.Replay.Steps.Single(s => s.Kind == "initial-wait").RecordIndex;
        int write = fixture.Replay.Steps.Single(s => s.Kind == "actor-write").RecordIndex;
        var initial = report.Samples.Single(s => s.Kind == "initial-armed");
        var start = report.Samples.Single(s => s.Kind == "start-request");
        HostCheck.True(initial.Response!.Slices[^1].Original.CallRecordIndex < wait && wait < start.Request.Slices[0].Original.CallRecordIndex && start.Request.Slices[^1].Original.CallRecordIndex < write && start.Response!.Slices[0].Original.CallRecordIndex > write);
        HostCheck.True(report.Order.Any(o => o.Relation == "initial_wait_before_start_request") && report.Order.Any(o => o.Relation == "start_request_before_actor_write"));
        Reject(() => new Fixture(startGate: true, earlyStart: true).Read(), "web_race_roles_order_initial_wait_before_start_request");
        Reject(() => new Fixture(startGate: true, lateStartConsumption: true).Read(), "web_race_roles_order_start_request_before_actor_write");
    }

    [HostTest("web event-owned roles bind v3 raw selectors bodies attempts and actual untimed complete replay")]
    private static void EventOwnedRoles()
    {
        foreach (bool unknown in new[]
        {
            false,
            true
        }

        )
        {
            var fixture = new Fixture(eventOwned: true, unknownCancel: unknown);
            var report = fixture.Read();
            HostCheck.True(fixture.Replay.EventOwned && report.Samples.Count == 21 && report.Temporal.Intervals.Count == 40);
            HostCheck.True(fixture.Replay.Waiter.Tid == 3 && fixture.Replay.Actor.Tid == 4 && fixture.Replay.RecoveryWriter.Tid == 1 && fixture.Replay.Callback.ReapRecordIndex is null && fixture.Replay.RecoveryWriter.ReapRecordIndex is null);
            HostCheck.True(fixture.Replay.SocketCalls.All(c => c.Identity != fixture.Replay.Actor.Identity && c.Identity != fixture.Replay.Waiter.Identity));
            HostCheck.True(report.Selection == fixture.Replay.Selection && report.Order.All(o => o.BeforeRecordIndex < o.AfterRecordIndex));
            HostCheck.True(report.Order.Any(o => o.Relation == "actor_reap_before_final_health") && report.Order.Any(o => o.Relation == "waiter_reap_before_final_health"));
            var cancel = report.Samples.Single(s => s.HostCancellation);
            HostCheck.True(cancel.Kind == "churn-cancel" && cancel.Response is null && cancel.Request.Packet.Kind == "cancel-request");
            var http = report.Streams.Host;
            HostCheck.True(http.Samples.Count == 21 && http.Messages.Any(m => m.Request.First == "GET /__clros/signalrace/status HTTP/1.1"));
            string selected = fixture.Selected(), attempts = fixture.Attempts();
            HostCheck.True(selected.Contains("CLROS_SIGNAL_RACE_V3_OK", StringComparison.Ordinal) && attempts.Contains("\"signalVersion\":\"v3\"", StringComparison.Ordinal) && attempts.Contains("\"churnVersion\":\"v2\"", StringComparison.Ordinal));
        }
    }

    [HostTest("web event-owned final healthy response requires dedicated native tails reaped without retiring framework or request owners")]
    private static void DedicatedTailOrder()
    {
        Reject(() => new Fixture(eventOwned: true, lateActor: true).Read(), "web_race_roles_order_actor_reap_before_final_health");
        Reject(() => new Fixture(eventOwned: true, lateWaiter: true).Read(), "web_race_roles_order_waiter_reap_before_final_health");
    }

    [HostTest("web event-owned roles refuse profile substitution selector drift and premature native phase claims")]
    private static void EventOwnedRefusals()
    {
        var current = new Fixture(eventOwned: true);
        var old = new Fixture(startGate: true);
        Reject(() => Q.InspectNativeAotWebSignalRaceRolesForTests(current.Wire(), current.Attempts(), current.Selected(), Endpoint, Health, current.Guest(), old.Replay), "web_race_roles_event_contract_pending");
        foreach (var legacy in new[]
        {
            new Fixture(),
            old
        }

        )
            Reject(() => Q.InspectNativeAotWebSignalRaceRolesForTests(legacy.Wire(), legacy.Attempts(), legacy.Selected(), Endpoint, Health, legacy.Guest(), current.Replay), "web_race_roles_profile");
        Reject(() => new Fixture(eventOwned: true, badSignal: true).Read(), "web_race_http_request_signal");
        Reject(() => new Fixture(eventOwned: true, unknownCancel: true, extraCancelSignal: true).Read(), "web_race_roles_cancel_request");
        Reject(() => new Fixture(eventOwned: true, earlyStart: true).Read(), "web_race_roles_order_initial_wait_before_start_request");
        Reject(() => new Fixture(eventOwned: true, lateStartConsumption: true).Read(), "web_race_roles_order_start_request_before_actor_write");
        Reject(() => new Fixture(eventOwned: true, earlyOverlap: true).Read(), "web_race_roles_order_actor_read_before_armed");
        Reject(() => new Fixture(eventOwned: true, beforeCleanup: true).Read(), "web_race_roles_order_recovery-read_before_race_reply");
    }
}
