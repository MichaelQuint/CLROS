using System.Text;
using System.Text.Json.Nodes;
using OS.Tooling;
using Q = OS.Tooling.QemuTranscripts;
using Peer = OS.Tooling.NativeAotServiceQuotaPeer;
using Values = NativeAotWebSignalRaceHostHttpTests.Values;

// Complete synthetic replay plus strict copied HTTP/stream values. No original
// process, native code, pointed-to bytes, actual GC or guest evidence is supplied.
internal static class NativeAotWebSignalRaceRoleTests
{
    private const string Endpoint="http://127.0.0.1:22080",Health="OS_MINIMAL_API_OK";
    private const string Success="CLROS_SIGNAL_RACE_OK signal=12 pid=1 actor=3 waiter=1 callback=2 callbacks=1 actorJoined=1 waiterJoined=1 consumed=90 recovery=107 recoveryWriter=2 initialValue=-1 initialErrno=4 recoveryValue=1 recoveryEvents=1 recoveryTag=344978309701 handler=4096 flags=335544324 restorer=8192 actionMask=0 originalHandler=0 originalFlags=0 originalRestorer=0 originalActionMask=0 maskBefore=2048 maskDuring=0 maskAfter=2048 altAddress=0 altFlags=2 altBytes=0 descriptorsClosed=3 actionRestored=1 altUnchanged=1";
    private static string Data(string text)=>Convert.ToBase64String(Encoding.ASCII.GetBytes(text));
    private static string Request(string path,string method)=>method+" "+path+" HTTP/1.1\r\nHost: 127.0.0.1:22080\r\n"+
        (method=="POST"?"Content-Length: 0\r\n"+(path=="/__clros/churn"?"X-CLROS-Churn":"X-CLROS-Signal")+": v1\r\n":"")+"\r\n";
    private static string Response(string body,int status=200)=>$"HTTP/1.1 {status} Result\r\nContent-Type: text/plain\r\nContent-Length: {body.Length}\r\n\r\n"+body;
    private static string Churn(int started,int completed,int canceled,int active,int work)=>
        $"CLROS_CHURN_STATUS active={active} waiting={active} peakActive={(started==0?0:1)} started={started} completed={completed} canceled={canceled} failed=0 rejected=0 timers={work} workers={work} collections={work} heldBytes={active*65536} peakHeldBytes={(started==0?0:65536)}";
    private static string Status(int phase)=>phase==0?"CLROS_SIGNAL_RACE_STATUS phase=0 pid=0 waiter=0 actor=0 callback=0 recoveryRequested=0":
        $"CLROS_SIGNAL_RACE_STATUS phase={phase} pid=1 waiter=1 actor=3 callback={(phase==2?0:2)} recoveryRequested={(phase==7?1:0)}";

    private sealed class Flow(int connection,int descriptor,uint port,int slot,int generation)
    {
        internal readonly int Connection=connection,Descriptor=descriptor,Slot=slot,Generation=generation;
        internal readonly uint Port=port;
        internal string RequestBytes="",ResponseBytes="";
        internal readonly List<Q.ServiceQuotaPacketRange> Requests=[],Responses=[];
        internal Q.ServiceQuotaPacketStream Copy()
        {
            Q.ServiceQuotaPacketDirection Direction(bool response)
            {string bytes=response?ResponseBytes:RequestBytes;return new(bytes.Length,bytes.Length,bytes.Length,Data(bytes),(response?Responses:Requests).ToArray());}
            return new(Port,Direction(false),Direction(true),new(Connection,true,true,false,false,false,false,false,0,0,0));
        }
    }
    private sealed class Fixture
    {
        internal readonly Values Host=new(22080);
        internal readonly List<Flow> Flows=[];
        internal readonly Q.WebSignalRaceReport Replay;
        private readonly bool startGate;
        private int packet=10,completed,churnId,raceId;
        private bool startReplyPending;
        private Flow control=null!,churn=null!,race=null!;
        internal Fixture(bool unknownCancel=false,bool earlyOverlap=false,bool beforeCleanup=false,bool peekCancel=false,
            bool startGate=false,bool earlyStart=false,bool lateStartConsumption=false)
        {
            this.startGate=startGate;
            void Phase(NativeAotThreadingTranscriptTests.Vector v,string phase)
            {
                if(phase=="before-probe")
                {
                    control=Open(v,7,"control",43000,7,1,2);
                    churn=Open(v,8,"churn",43001,0,3,3);
                    v.Rows.Add(NativeAotWebTransportTests.Retire(1,1,30001,NativeAotWebTransportTests.Timeout+4));
                    race=Open(v,9,"race",43002,1,2,5);
                    Http(v,control,"baseline-churn","/__clros/churn/status",Churn(0,0,0,0,0));
                    Http(v,control,"baseline-race","/__clros/signalrace/status",Status(0));
                    Http(v,control,"health-before","/healthz",Health);
                    if(startGate)
                    {
                        raceId=Pending(v,race,"/__clros/signalrace",false);
                        // A phase2 response may precede the actual native wait.
                        Http(v,control,"initial-armed","/__clros/signalrace/status",Status(2));
                    }
                    churnId=Pending(v,churn,"/__clros/churn",unknownCancel,peekCancel);
                    Http(v,control,"churn-pending","/__clros/churn/status",Churn(1,0,0,1,1));
                    if(!startGate)raceId=Pending(v,race,"/__clros/signalrace",false);
                    if(startGate&&earlyStart)StartRequest(v);
                    if(earlyOverlap)Http(v,control,"recovery-armed","/__clros/signalrace/status",Status(4));
                    if(beforeCleanup)Native(v,race,true,Response(Success));
                }
                else if(phase=="before-actor-write")
                {
                    if(startGate&&!earlyStart&&!lateStartConsumption)StartRequest(v);
                }
                else if(phase=="before-recovery-write")
                {
                    if(startGate&&lateStartConsumption)StartRequest(v);
                    // The start reply may be sent after the actor already ran.
                    if(startReplyPending){Native(v,control,true,Response("CLROS_SIGNAL_RACE_START_REQUESTED"));startReplyPending=false;}
                    if(!earlyOverlap)Http(v,control,"recovery-armed","/__clros/signalrace/status",Status(4));
                    Http(v,control,"churn-overlap","/__clros/churn/status",Churn(1,0,0,1,2));
                    Http(v,control,"overlap-health","/healthz",Health);
                    Host.Completions[churnId]=++completed;
                    Host.Exchanges[churnId-1]=Host.Exchanges[churnId-1] with{EndedTicks=Host.Exchanges[^1].EndedTicks};Host.Cancel(churnId);
                    v.Call(3,0,[(ulong)churn.Descriptor]);
                    v.Rows.Add(NativeAotWebTransportTests.Retire(churn.Slot,churn.Generation,(int)churn.Port,NativeAotWebTransportTests.Timeout+6));
                    Http(v,control,"churn-drained","/__clros/churn/status",Churn(1,0,1,0,2));
                    Http(v,control,"recovery-request","/__clros/signalrace/recover","CLROS_SIGNAL_RACE_RECOVERY_REQUESTED","POST");
                    Http(v,control,"recovery-repeat","/__clros/signalrace/recover","CLROS_SIGNAL_RACE_RECOVERY_REFUSED","POST",409);
                }
                else if(phase=="after-cleanup")
                {
                    FinishRace(v,!beforeCleanup);
                    Http(v,control,"race-final","/__clros/signalrace/status",Status(7));
                    Http(v,control,"health-after","/healthz",Health);
                    var fresh=Open(v,8,"churn",43003,0,4,7);
                    Http(v,fresh,"churn-complete","/__clros/churn","CLROS_CHURN_OK iterations=8 bytes=65536 timers=8 workers=8 collections=8","POST");
                    Http(v,control,"final-churn","/__clros/churn/status",Churn(2,1,1,0,10));
                    Http(v,control,"health-final","/healthz",Health);
                    Http(v,race,"repeat-race","/__clros/signalrace","CLROS_SIGNAL_RACE_BUSY","POST",409);
                    Http(v,control,"repeat-probe","/__clros/signalprobe","CLROS_SIGNAL_PROBE_BUSY","POST",409);
                }
            }
            Replay=NativeAotWebSignalRaceTests.Run(controls:true,phase:Phase,pipeBase:10);
        }
        private void StartRequest(NativeAotThreadingTranscriptTests.Vector v)
        {
            const string path="/__clros/signalrace/start",body="CLROS_SIGNAL_RACE_START_REQUESTED";
            string request=Request(path,"POST");int id=Host.Add(control.Connection,path,body,Response(body),"POST",rawRequest:request);
            Host.Completions[id]=++completed;Native(v,control,false,request);Host.Select("start-request",id);startReplyPending=true;
        }
        private Flow Open(NativeAotThreadingTranscriptTests.Vector v,int fd,string role,uint port,int slot,int generation,ulong clock)
        {
            v.Rows.Add(NativeAotWebTransportTests.Fresh(slot,generation,(int)port,NativeAotWebTransportTests.Timeout+clock));
            v.Call(288,fd,[6,0x500700,0x500780,0x80000]);
            var flow=new Flow(Host.Connect(role),fd,port,slot,generation);Flows.Add(flow);return flow;
        }
        private void Native(NativeAotThreadingTranscriptTests.Vector v,Flow flow,bool response,string bytes,bool peek=false)
        {
            v.Call(response?44UL:45UL,bytes.Length,[(ulong)flow.Descriptor,0x500800,(ulong)bytes.Length,peek?2UL:0,0,0]);
            int offset=(response?flow.ResponseBytes:flow.RequestBytes).Length;
            (response?flow.Responses:flow.Requests).Add(new(new(flow.Port,response,offset,bytes.Length,++packet,1,(uint)packet),Data(bytes)));
            if(response)flow.ResponseBytes+=bytes;else flow.RequestBytes+=bytes;
        }
        private void Http(NativeAotThreadingTranscriptTests.Vector v,Flow flow,string kind,string path,string body,string method="GET",int status=200)
        {
            string request=Request(path,method),response=Response(body,status);
            int id=Host.Add(flow.Connection,path,body,response,method,status:status,rawRequest:request);Host.Completions[id]=++completed;
            Native(v,flow,false,request);Native(v,flow,true,response);Host.Select(kind,id);
        }
        private int Pending(NativeAotThreadingTranscriptTests.Vector v,Flow flow,string path,bool unknown,bool peek=false)
        {
            int id=Host.Exchanges.Count+1;string request=Request(path,"POST");Host.Op(flow.Connection,"write",id,request,unknown?"failed":"complete");
            Host.Exchanges.Add(new(id,flow==churn?"churn":"race","POST",path,Host.Operations[^1].StartedTicks-1,9999,"cancelled",0,"",new[]{flow.Connection}));
            Native(v,flow,false,request,peek);return id;
        }
        private void FinishRace(NativeAotThreadingTranscriptTests.Vector v,bool nativeSend=true)
        {
            string response=Response(Success);Host.Op(race.Connection,"read",raceId,response);if(nativeSend)Native(v,race,true,response);
            Host.Exchanges[raceId-1]=Host.Exchanges[raceId-1] with{Outcome="response",Status=200,BodyBase64=Data(Success),EndedTicks=Host.Operations[^1].EndedTicks};
            Host.Completions[raceId]=++completed;
            Host.Select("race-complete",raceId);
        }
        internal byte[] Wire()=>Encoding.UTF8.GetBytes(NativeAotWebPayload.SerializeSignalRaceWire(new(22080,"signalrace-run",10001,
            Host.Operations.Sum(op=>(long)Convert.FromBase64String(op.DataBase64).Length),Host.Connections,Host.Exchanges,Host.Operations,[])));
        internal string Attempts()
        {var value=JsonNode.Parse(Host.Attempts())!.AsObject();value["schema"]=startGate?"clros.web.signalrace.attempts.v2":"clros.web.signalrace.attempts.v1";value["semanticComplete"]=true;value["failure"]="";return value.ToJsonString();}
        internal string Selected()
        {var value=JsonNode.Parse(Host.Selected())!.AsObject();value["schema"]=startGate?"clros.web.signalrace.v2":"clros.web.signalrace.v1";return value.ToJsonString();}
        internal Q.ServiceQuotaPacketStreams Guest()
        {var streams=Flows.Select(f=>f.Copy()).ToArray();return new(packet+20,0,streams.Sum(s=>s.Requests.Extent+s.Responses.Extent),streams);}
        internal Q.WebSignalRaceRoleInspection Read()=>Q.InspectNativeAotWebSignalRaceRolesForTests(Wire(),Attempts(),Selected(),Endpoint,Health,Guest(),Replay);
    }
    private static void Reject(Action action,string? expected=null)
    {
        string? reason=null;try{action();}catch(ToolFailure error){reason=error.Reason;}catch(InvalidDataException error){reason=error.Message;}
        HostCheck.True(reason is not null);if(expected is not null)HostCheck.Equal(expected,reason);
    }

    [HostTest("web race roles bind complete model original socket CALLs and all nineteen selected HTTP witnesses")]
    private static void CompleteRoles()
    {
        var fixture=new Fixture();var result=fixture.Read();
        HostCheck.True(result.Selection==fixture.Replay.Selection&&result.Samples.Count==19&&result.Order.Count>0&&result.Streams.Bindings.Count==4);
        var canceled=result.Samples[7];HostCheck.True(canceled.HostCancellation&&canceled.Response is null&&canceled.Request.Packet.Kind=="cancel-request");
        HostCheck.True(result.Temporal.Intervals.Count==36&&result.Samples.Where(s=>!s.HostCancellation).All(s=>s.Response is not null));
        HostCheck.True(result.Order.All(o=>o.BeforeRecordIndex<o.AfterRecordIndex)&&result.Samples.SelectMany(s=>s.Request.Slices).All(s=>
            fixture.Replay.SocketCalls.Contains(s.Original)));
        bool immutable=false;try{((IList<Q.WebSignalRaceRoleSample>)result.Samples).Clear();}catch(NotSupportedException){immutable=true;}HostCheck.True(immutable);
    }

    [HostTest("web race roles reject copied selected body and identity substitution despite complete native model")]
    private static void BodyAndRoles()
    {
        var fixture=new Fixture();fixture.Host.Samples[4]["body"]=Status(4).Replace("actor=3","actor=99",StringComparison.Ordinal);Reject(()=>fixture.Read());
        fixture=new Fixture();fixture.Host.Samples[6]["body"]="FORGED_HEALTH";Reject(()=>fixture.Read());
        fixture=new Fixture();fixture.Host.Links[4]["attempt"]=fixture.Host.Links[5]["attempt"]!.DeepClone();Reject(()=>fixture.Read());
    }

    [HostTest("web race roles require original interrupt recovery and explicit cleanup boundaries before claimed HTTP phases")]
    private static void NativeBoundaries()
    {
        Reject(()=>new Fixture(earlyOverlap:true).Read(),"web_race_roles_order_actor_read_before_armed");
        Reject(()=>new Fixture(beforeCleanup:true).Read(),"web_race_roles_order_recovery-read_before_race_reply");
    }

    [HostTest("web race canceled request coverage permits actual unknown-write delivery without fabricating cancellation response")]
    private static void UnknownCanceledWrite()
    {
        var fixture=new Fixture(unknownCancel:true);var result=fixture.Read();var canceled=result.Samples[7];
        HostCheck.True(canceled.HostCancellation&&canceled.Response is null&&canceled.Request.Slices.Count>0&&
            result.Streams.Host.Prefixes.Connections.Single(c=>c.ConnectionId==canceled.Request.Packet.ConnectionId).Writes.UnknownWrite is not null);
        HostCheck.True(result.Streams.Bindings.Single(b=>b.ConnectionId==canceled.Request.Packet.ConnectionId).Compared.MinimumAttemptedWritePrefixBytes==
            Request("/__clros/churn","POST").Length);
        var guest=fixture.Guest();var flows=guest.Streams.ToArray();var churn=flows.Single(s=>s.RemotePort==43001);
        var range=churn.Requests.FirstRanges[0];byte[] wrong=Convert.FromBase64String(range.DataBase64);wrong[^1]=(byte)'X';
        flows[Array.IndexOf(flows,churn)]=churn with{Requests=churn.Requests with{KnownPrefixBase64=Convert.ToBase64String(wrong),
            FirstRanges=[range with{DataBase64=Convert.ToBase64String(wrong)}]}};
        Reject(()=>Q.InspectNativeAotWebSignalRaceRolesForTests(fixture.Wire(),fixture.Attempts(),fixture.Host.Selected(),Endpoint,Health,guest with{Streams=flows},fixture.Replay),
            "quota_stream_join_unmatched");
        Reject(()=>new Fixture(unknownCancel:true,peekCancel:true).Read(),"web_race_roles_cancel_request");
    }

    [HostTest("web race v2 start request consumes after actual initial wait and before actor write without ordering its reply")]
    private static void InitialStartBoundary()
    {
        var fixture=new Fixture(startGate:true);var report=fixture.Read();
        HostCheck.True(report.Samples.Count==21&&report.Temporal.Intervals.Count==40&&report.Selection==fixture.Replay.Selection);
        int wait=fixture.Replay.Steps.Single(s=>s.Kind=="initial-wait").RecordIndex;
        int write=fixture.Replay.Steps.Single(s=>s.Kind=="actor-write").RecordIndex;
        var initial=report.Samples.Single(s=>s.Kind=="initial-armed");var start=report.Samples.Single(s=>s.Kind=="start-request");
        HostCheck.True(initial.Response!.Slices[^1].Original.CallRecordIndex<wait&&
            wait<start.Request.Slices[0].Original.CallRecordIndex&&start.Request.Slices[^1].Original.CallRecordIndex<write&&
            start.Response!.Slices[0].Original.CallRecordIndex>write);
        HostCheck.True(report.Order.Any(o=>o.Relation=="initial_wait_before_start_request")&&report.Order.Any(o=>o.Relation=="start_request_before_actor_write"));
        Reject(()=>new Fixture(startGate:true,earlyStart:true).Read(),"web_race_roles_order_initial_wait_before_start_request");
        Reject(()=>new Fixture(startGate:true,lateStartConsumption:true).Read(),"web_race_roles_order_start_request_before_actor_write");
    }
}
