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
    private sealed class Fixture
    {
        internal readonly Values Host = new(22080);
        internal readonly List<Flow> Flows = [];
        internal readonly Q.WebSignalRaceReport Replay;
        private readonly bool startGate, eventOwned, badSignal;
        private int packet = 10, completed, churnId, raceId;
        private bool startReplyPending;
        private Flow control = null!, churn = null!, race = null!;
        internal Fixture(bool unknownCancel = false, bool earlyOverlap = false, bool beforeCleanup = false, bool peekCancel = false, bool startGate = false, bool earlyStart = false, bool lateStartConsumption = false, bool eventOwned = false, bool badSignal = false, bool lateActor = false, bool lateWaiter = false, bool extraCancelSignal = false)
        {
            startGate |= eventOwned;
            this.startGate = startGate;
            this.eventOwned = eventOwned;
            this.badSignal = badSignal;
            void Phase(NativeAotThreadingTranscriptTests.Vector v, string phase)
            {
                if (phase == "before-probe")
                {
                    control = Open(v, 7, "control", 43000, 7, 1, 2);
                    churn = Open(v, 8, "churn", 43001, 0, 3, 3);
                    v.Rows.Add(NativeAotWebTransportTests.Retire(1, 1, 30001, NativeAotWebTransportTests.Timeout + 4));
                    race = Open(v, 9, "race", 43002, 1, 2, 5);
                    Http(v, control, "baseline-churn", "/__clros/churn/status", Churn(0, 0, 0, 0, 0));
                    Http(v, control, "baseline-race", "/__clros/signalrace/status", Status(0));
                    Http(v, control, "health-before", "/healthz", Health);
                    if (startGate)
                    {
                        raceId = Pending(v, race, "/__clros/signalrace", false);
                        // A phase2 response may precede the actual native wait.
                        Http(v, control, "initial-armed", "/__clros/signalrace/status", Status(2));
                    }

                    churnId = Pending(v, churn, "/__clros/churn", unknownCancel, peekCancel, extraCancelSignal);
                    Http(v, control, "churn-pending", "/__clros/churn/status", Churn(1, 0, 0, 1, 1));
                    if (!startGate)
                        raceId = Pending(v, race, "/__clros/signalrace", false);
                    if (startGate && earlyStart)
                        StartRequest(v);
                    if (earlyOverlap && !startGate)
                        Http(v, control, "recovery-armed", "/__clros/signalrace/status", Status(4));
                    if (beforeCleanup)
                        Native(v, race, true, Response(Body(Success)));
                }
                else if (phase == "before-actor-write")
                {
                    if (startGate && !earlyStart && !lateStartConsumption)
                        StartRequest(v);
                    if (startGate && earlyOverlap)
                    {
                        // Keep the selected/FIFO start reply before armed, but
                        // place the claimed phase4 before the actual actor read.
                        if (startReplyPending)
                        {
                            Native(v, control, true, Response("CLROS_SIGNAL_RACE_START_REQUESTED"));
                            startReplyPending = false;
                        }

                        Http(v, control, "recovery-armed", "/__clros/signalrace/status", Status(4));
                    }
                }
                else if (phase == "before-recovery-write")
                {
                    if (startGate && lateStartConsumption)
                        StartRequest(v);
                    // The start reply may be sent after the actor already ran.
                    if (startReplyPending)
                    {
                        Native(v, control, true, Response("CLROS_SIGNAL_RACE_START_REQUESTED"));
                        startReplyPending = false;
                    }

                    if (!earlyOverlap)
                        Http(v, control, "recovery-armed", "/__clros/signalrace/status", Status(4));
                    Http(v, control, "churn-overlap", "/__clros/churn/status", Churn(1, 0, 0, 1, 2));
                    Http(v, control, "overlap-health", "/healthz", Health);
                    Host.Completions[churnId] = ++completed;
                    Host.Exchanges[churnId - 1] = Host.Exchanges[churnId - 1] with
                    {
                        EndedTicks = Host.Exchanges[^1].EndedTicks
                    };
                    Host.Cancel(churnId);
                    v.Call(3, 0, [(ulong)churn.Descriptor]);
                    v.Rows.Add(NativeAotWebTransportTests.Retire(churn.Slot, churn.Generation, (int)churn.Port, NativeAotWebTransportTests.Timeout + 6));
                    Http(v, control, "churn-drained", "/__clros/churn/status", Churn(1, 0, 1, 0, 2));
                    Http(v, control, "recovery-request", "/__clros/signalrace/recover", "CLROS_SIGNAL_RACE_RECOVERY_REQUESTED", "POST");
                    Http(v, control, "recovery-repeat", "/__clros/signalrace/recover", "CLROS_SIGNAL_RACE_RECOVERY_REFUSED", "POST", 409);
                }
                else if (phase == "after-cleanup")
                {
                    FinishRace(v, !beforeCleanup);
                    Http(v, control, "race-final", "/__clros/signalrace/status", Status(7));
                    Http(v, control, "health-after", "/healthz", Health);
                    var fresh = Open(v, 8, "churn", 43003, 0, 4, 7);
                    Http(v, fresh, "churn-complete", "/__clros/churn", "CLROS_CHURN_OK iterations=8 bytes=65536 timers=8 workers=8 collections=8", "POST");
                    Http(v, control, "final-churn", "/__clros/churn/status", Churn(2, 1, 1, 0, 10));
                    Http(v, control, "health-final", "/healthz", Health);
                    Http(v, race, "repeat-race", "/__clros/signalrace", "CLROS_SIGNAL_RACE_BUSY", "POST", 409);
                    Http(v, control, "repeat-probe", "/__clros/signalprobe", "CLROS_SIGNAL_PROBE_BUSY", "POST", 409);
                }
            }

            Replay = NativeAotWebSignalRaceTests.Run(controls: true, phase: Phase, pipeBase: 10, eventOwned: eventOwned, lateActor: lateActor, lateWaiter: lateWaiter);
        }

        private string Body(string value) => eventOwned ? value.Replace("CLROS_SIGNAL_RACE_STATUS", "CLROS_SIGNAL_RACE_V3_STATUS", StringComparison.Ordinal).Replace("CLROS_SIGNAL_RACE_OK", "CLROS_SIGNAL_RACE_V3_OK", StringComparison.Ordinal).Replace("actor=3 waiter=1", "actor=4 waiter=3", StringComparison.Ordinal).Replace("waiter=1 actor=3", "waiter=3 actor=4", StringComparison.Ordinal).Replace("recoveryWriter=2", "recoveryWriter=1", StringComparison.Ordinal) : value;
        private string RequestBytes(string path, string method)
        {
            string value = Request(path, method, eventOwned);
            return badSignal ? value.Replace("X-CLROS-Signal: v3", "X-CLROS-Signal: v1", StringComparison.Ordinal) : value;
        }

        private void StartRequest(NativeAotThreadingTranscriptTests.Vector v)
        {
            const string path = "/__clros/signalrace/start", body = "CLROS_SIGNAL_RACE_START_REQUESTED";
            string request = RequestBytes(path, "POST");
            int id = Host.Add(control.Connection, path, body, Response(body), "POST", rawRequest: request);
            Host.Completions[id] = ++completed;
            Native(v, control, false, request);
            Host.Select("start-request", id);
            startReplyPending = true;
        }

        private Flow Open(NativeAotThreadingTranscriptTests.Vector v, int fd, string role, uint port, int slot, int generation, ulong clock)
        {
            v.Rows.Add(NativeAotWebTransportTests.Fresh(slot, generation, (int)port, NativeAotWebTransportTests.Timeout + clock));
            v.Call(288, fd, [6, 0x500700, 0x500780, 0x80000]);
            var flow = new Flow(Host.Connect(role), fd, port, slot, generation);
            Flows.Add(flow);
            return flow;
        }

        private void Native(NativeAotThreadingTranscriptTests.Vector v, Flow flow, bool response, string bytes, bool peek = false)
        {
            v.Call(response ? 44UL : 45UL, bytes.Length, [(ulong)flow.Descriptor, 0x500800, (ulong)bytes.Length, peek ? 2UL : 0, 0, 0]);
            int offset = (response ? flow.ResponseBytes : flow.RequestBytes).Length;
            (response ? flow.Responses : flow.Requests).Add(new(new(flow.Port, response, offset, bytes.Length, ++packet, 1, (uint)packet), Data(bytes)));
            if (response)
                flow.ResponseBytes += bytes;
            else
                flow.RequestBytes += bytes;
        }

        private void Http(NativeAotThreadingTranscriptTests.Vector v, Flow flow, string kind, string path, string body, string method = "GET", int status = 200)
        {
            body = Body(body);
            string request = RequestBytes(path, method), response = Response(body, status);
            int id = Host.Add(flow.Connection, path, body, response, method, status: status, rawRequest: request);
            Host.Completions[id] = ++completed;
            Native(v, flow, false, request);
            Native(v, flow, true, response);
            Host.Select(kind, id);
        }

        private int Pending(NativeAotThreadingTranscriptTests.Vector v, Flow flow, string path, bool unknown, bool peek = false, bool extraSignal = false)
        {
            int id = Host.Exchanges.Count + 1;
            string request = RequestBytes(path, "POST");
            if (eventOwned && flow == churn)
                request = request.Replace("X-CLROS-Churn: v1", "X-CLROS-Churn: v2", StringComparison.Ordinal);
            if (extraSignal)
                request = request.Replace("\r\n\r\n", "\r\nX-CLROS-Signal: v3\r\n\r\n", StringComparison.Ordinal);
            Host.Op(flow.Connection, "write", id, request, unknown ? "failed" : "complete");
            Host.Exchanges.Add(new(id, flow == churn ? "churn" : "race", "POST", path, Host.Operations[^1].StartedTicks - 1, 9999, "cancelled", 0, "", new[] { flow.Connection }));
            Native(v, flow, false, request, peek);
            return id;
        }

        private void FinishRace(NativeAotThreadingTranscriptTests.Vector v, bool nativeSend = true)
        {
            string body = Body(Success), response = Response(body);
            Host.Op(race.Connection, "read", raceId, response);
            if (nativeSend)
                Native(v, race, true, response);
            Host.Exchanges[raceId - 1] = Host.Exchanges[raceId - 1] with
            {
                Outcome = "response",
                Status = 200,
                BodyBase64 = Data(body),
                EndedTicks = Host.Operations[^1].EndedTicks
            };
            Host.Completions[raceId] = ++completed;
            Host.Select("race-complete", raceId);
        }

        internal byte[] Wire() => Encoding.UTF8.GetBytes(NativeAotWebPayload.SerializeSignalRaceWire(new(22080, "signalrace-run", 10001, Host.Operations.Sum(op => (long)Convert.FromBase64String(op.DataBase64).Length), Host.Connections, Host.Exchanges, Host.Operations, [])));
        internal string Attempts()
        {
            var value = JsonNode.Parse(Host.Attempts())!.AsObject();
            value["schema"] = eventOwned ? "clros.web.signalrace.attempts.v3" : startGate ? "clros.web.signalrace.attempts.v2" : "clros.web.signalrace.attempts.v1";
            value["semanticComplete"] = true;
            value["failure"] = "";
            int churnPosts = 0;
            if (eventOwned)
                foreach (var row in value["attempts"]!.AsArray())
                {
                    var attempt = row!.AsObject();
                    string path = attempt["path"]!.GetValue<string>(), method = attempt["method"]!.GetValue<string>();
                    attempt["signalVersion"] = path.StartsWith("/__clros/signalrace", StringComparison.Ordinal) ? "v3" : method == "POST" && path == "/__clros/signalprobe" ? "v1" : "";
                    attempt["churnVersion"] = method == "POST" && path == "/__clros/churn" ? (++churnPosts == 1 ? "v2" : "v1") : "";
                }

            return value.ToJsonString();
        }

        internal string Selected()
        {
            var value = JsonNode.Parse(Host.Selected())!.AsObject();
            value["schema"] = eventOwned ? "clros.web.signalrace.v3" : startGate ? "clros.web.signalrace.v2" : "clros.web.signalrace.v1";
            return value.ToJsonString();
        }

        internal Q.ServiceQuotaPacketStreams Guest()
        {
            var streams = Flows.Select(f => f.Copy()).ToArray();
            return new(packet + 20, 0, streams.Sum(s => s.Requests.Extent + s.Responses.Extent), streams);
        }

        internal Q.WebSignalRaceRoleInspection Read() => Q.InspectNativeAotWebSignalRaceRolesForTests(Wire(), Attempts(), Selected(), Endpoint, Health, Guest(), Replay);
    }
}
