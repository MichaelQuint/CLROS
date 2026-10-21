using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using OS.Tooling;

// Deterministic copied HTTP responses only. No native wait, scheduling,
// pointed-to bytes, process ownership or guest execution is supplied.
internal static class NativeAotWebSignalRaceStartTests
{
    private const string Endpoint="http://127.0.0.1:23456",Health="OS_MINIMAL_API_OK";
    private const string Complete="CLROS_CHURN_OK iterations=8 bytes=65536 timers=8 workers=8 collections=8";
    private const string Success="CLROS_SIGNAL_RACE_OK signal=12 pid=43 actor=8 waiter=7 callback=9 callbacks=1 actorJoined=1 waiterJoined=1 consumed=90 recovery=107 recoveryWriter=9 initialValue=-1 initialErrno=4 recoveryValue=1 recoveryEvents=1 recoveryTag=344978309701 handler=4096 flags=335544324 restorer=8192 actionMask=0 originalHandler=0 originalFlags=0 originalRestorer=0 originalActionMask=0 maskBefore=2048 maskDuring=0 maskAfter=2048 altAddress=0 altFlags=2 altBytes=0 descriptorsClosed=3 actionRestored=1 altUnchanged=1";
    private sealed class Peer : HttpMessageHandler
    {
        internal readonly List<string> Calls=[];
        internal bool RefuseStart,EarlyPhase,InitialSeen,ChurnCanceled,RaceCanceled;
        internal int Starts;
        internal Action? CancelInitial;
        private bool raceStarted,released,recovered,complete;
        private int churnStarted;
        private readonly TaskCompletionSource<HttpResponseMessage> churn=new(TaskCreationOptions.RunContinuationsAsynchronously),race=new(TaskCreationOptions.RunContinuationsAsynchronously);
        private static HttpResponseMessage Reply(string body,int status=200)=>new((HttpStatusCode)status){Content=new StringContent(body,Encoding.UTF8,"text/plain")};
        private string Churn()
        {
            int active=churnStarted==1&&!ChurnCanceled?1:0,work=churnStarted==0?0:complete?10:released?2:1;
            return $"CLROS_CHURN_STATUS active={active} waiting={active} peakActive={(churnStarted==0?0:1)} started={churnStarted} completed={(complete?1:0)} canceled={(ChurnCanceled?1:0)} failed=0 rejected=0 timers={work} workers={work} collections={work} heldBytes={active*65536} peakHeldBytes={(churnStarted==0?0:65536)}";
        }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            token.ThrowIfCancellationRequested();string path=request.RequestUri!.AbsolutePath;Calls.Add(request.Method.Method+" "+path);
            if(request.Method==HttpMethod.Get)
            {
                if(path=="/healthz")return Task.FromResult(Reply(Health));
                if(path=="/__clros/churn/status")return Task.FromResult(Reply(Churn()));
                HostCheck.Equal("/__clros/signalrace/status",path);
                if(!raceStarted)return Task.FromResult(Reply("CLROS_SIGNAL_RACE_STATUS phase=0 pid=0 waiter=0 actor=0 callback=0 recoveryRequested=0"));
                if(!released){InitialSeen=true;CancelInitial?.Invoke();token.ThrowIfCancellationRequested();}
                int phase=recovered?7:released||EarlyPhase?4:2;
                return Task.FromResult(Reply($"CLROS_SIGNAL_RACE_STATUS phase={phase} pid=43 waiter=7 actor=8 callback={(phase==2?0:9)} recoveryRequested={(recovered?1:0)}"));
            }
            HostCheck.Equal(HttpMethod.Post,request.Method);HostCheck.Equal(0L,request.Content!.Headers.ContentLength!.Value);
            HostCheck.True(request.Headers.GetValues(path=="/__clros/churn"?"X-CLROS-Churn":"X-CLROS-Signal").SequenceEqual(new[]{"v1"}));
            switch(path)
            {
                case "/__clros/signalrace":
                    if(raceStarted)return Task.FromResult(Reply("CLROS_SIGNAL_RACE_BUSY",409));
                    HostCheck.Equal(0,churnStarted);raceStarted=true;
                    token.Register(()=>{RaceCanceled=true;race.TrySetCanceled(token);});return race.Task;
                case "/__clros/churn":
                    churnStarted++;
                    if(churnStarted==2){complete=true;return Task.FromResult(Reply(Complete));}
                    HostCheck.True(raceStarted&&InitialSeen&&!released);
                    token.Register(()=>{ChurnCanceled=true;churn.TrySetCanceled(token);});return churn.Task;
                case "/__clros/signalrace/start":
                    Starts++;HostCheck.True(InitialSeen&&churnStarted==1&&!ChurnCanceled&&!released);
                    if(RefuseStart)return Task.FromResult(Reply("CLROS_SIGNAL_RACE_START_REFUSED",409));
                    released=true;return Task.FromResult(Reply("CLROS_SIGNAL_RACE_START_REQUESTED"));
                case "/__clros/signalrace/recover":
                    if(recovered)return Task.FromResult(Reply("CLROS_SIGNAL_RACE_RECOVERY_REFUSED",409));
                    recovered=true;race.TrySetResult(Reply(Success));return Task.FromResult(Reply("CLROS_SIGNAL_RACE_RECOVERY_REQUESTED"));
                case "/__clros/signalprobe":return Task.FromResult(Reply("CLROS_SIGNAL_PROBE_BUSY",409));
                default:throw new InvalidOperationException(path);
            }
        }
    }
    private static string Output()
    {
        string folder=Path.Combine(HostToolProcess.FindRepositoryRoot(),"artifacts","host-tests","signal-race-start-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);return Path.Combine(folder,"selected.json");
    }
    private static async Task<string> Capture(Peer peer,string path,CancellationToken token=default)
    {using var client=new HttpClient(peer);return await NativeAotWebPayload.CaptureStartedSignalRaceAsync(client,Endpoint,Health,path,token);}
    private static void Reject(Action action)
    {bool refused=false;try{action();}catch(Exception error)when(error is InvalidDataException or JsonException){refused=true;}HostCheck.True(refused);}
    private static async Task RejectAsync(Func<Task> action,string? expected=null)
    {Exception? failure=null;try{await action();}catch(Exception error)when(error is InvalidDataException or OperationCanceledException){failure=error;}HostCheck.True(failure is not null);if(expected is not null)HostCheck.Equal(expected,failure!.Message);}

    [HostTest("web race v2 observes initial gate before churn and sends exactly one start during copied progress")]
    private static async Task CompleteCapture()
    {
        var peer=new Peer();string path=Output(),json=await Capture(peer,path);
        HostCheck.Equal(json,File.ReadAllText(path));HostCheck.True(peer.InitialSeen&&peer.ChurnCanceled&&peer.Starts==1);
        HostCheck.Equal(new QemuTranscripts.WebSignalRaceSelection(43,7,8,9,9),NativeAotWebPayload.ValidateSignalRaceEvidence(json,Endpoint,Health));
        var selected=JsonNode.Parse(json)!;HostCheck.Equal("clros.web.signalrace.v2",selected["schema"]!.GetValue<string>());
        string[] kinds=["baseline-churn","baseline-race","health-before","initial-armed","churn-pending","start-request","recovery-armed","churn-overlap","overlap-health","churn-cancel","churn-drained","recovery-request","recovery-repeat","race-complete","race-final","health-after","churn-complete","final-churn","health-final","repeat-race","repeat-probe"];
        HostCheck.True(selected["samples"]!.AsArray().Select(row=>row!["kind"]!.GetValue<string>()).SequenceEqual(kinds));
        HostCheck.True(peer.Calls.IndexOf("POST /__clros/signalrace")<peer.Calls.IndexOf("POST /__clros/churn")&&
            peer.Calls.IndexOf("POST /__clros/churn")<peer.Calls.IndexOf("POST /__clros/signalrace/start"));
        HostCheck.Equal(new NativeAotWebPayload.SignalRaceAttemptSummary(21,20,1,0,true),
            NativeAotWebPayload.InspectSignalRaceAttempts(File.ReadAllText(path+".attempts.json"),json,Endpoint,Health));
    }

    [HostTest("web race v2 copied selected contract rejects stale schema roles initial identities and start reply")]
    private static async Task SelectedRefusals()
    {
        string json=await Capture(new Peer(),Output());
        foreach(Action<JsonObject> change in new Action<JsonObject>[]
        {
            o=>o["schema"]="clros.web.signalrace.v1",o=>o["schema"]="clros.web.signalrace.v3",
            o=>o["samples"]!.AsArray().RemoveAt(3),o=>o["samples"]![3]!["kind"]="churn-pending",
            o=>o["samples"]![3]!["body"]="CLROS_SIGNAL_RACE_STATUS phase=2 pid=43 waiter=7 actor=8 callback=9 recoveryRequested=0",
            o=>o["samples"]![3]!["body"]="CLROS_SIGNAL_RACE_STATUS phase=2 pid=44 waiter=7 actor=8 callback=0 recoveryRequested=0",
            o=>o["samples"]![5]!["status"]=409,o=>o["samples"]![5]!["body"]="CLROS_SIGNAL_RACE_START_REFUSED"
        })
        {var value=JsonNode.Parse(json)!.AsObject();change(value);Reject(()=>NativeAotWebPayload.ValidateSignalRaceEvidence(value.ToJsonString(),Endpoint,Health));}
        Reject(()=>NativeAotWebPayload.ValidateSignalRaceEvidence(json.Replace("\"schema\":","\"schema\":\"duplicate\",\"schema\":",StringComparison.Ordinal),Endpoint,Health));
    }

    [HostTest("web race v2 attempt companion binds start POST and refuses schema role link and extra start substitutions")]
    private static async Task AttemptRefusals()
    {
        string path=Output(),selected=await Capture(new Peer(),path),json=File.ReadAllText(path+".attempts.json");
        foreach(Action<JsonObject> change in new Action<JsonObject>[]
        {
            o=>o["schema"]="clros.web.signalrace.attempts.v1",o=>o["selections"]![5]!["attempt"]=o["selections"]![11]!["attempt"]!.DeepClone(),
            o=>o["attempts"]!.AsArray().Single(row=>row!["path"]!.GetValue<string>()=="/__clros/signalrace/start")!["method"]="GET",
            o=>o["attempts"]!.AsArray().Single(row=>row!["path"]!.GetValue<string>()=="/__clros/signalrace/start")!["path"]="/__clros/signalrace/recover",
            o=>o["selections"]![3]!["sample"]=4
        })
        {var value=JsonNode.Parse(json)!.AsObject();change(value);Reject(()=>NativeAotWebPayload.InspectSignalRaceAttempts(value.ToJsonString(),selected,Endpoint,Health));}
        var extra=JsonNode.Parse(json)!.AsObject();var rows=extra["attempts"]!.AsArray();
        var start=rows.Single(row=>row!["path"]!.GetValue<string>()=="/__clros/signalrace/start")!.DeepClone();
        start["id"]=rows.Count+1;start["completion"]=rows.Count+1;rows.Add(start);
        Reject(()=>NativeAotWebPayload.InspectSignalRaceAttempts(extra.ToJsonString(),selected,Endpoint,Health));
        Reject(()=>NativeAotWebPayload.InspectSignalRaceAttempts(json.Replace("\"schema\":","\"schema\":\"duplicate\",\"schema\":",StringComparison.Ordinal),selected,Endpoint,Health));
    }

    [HostTest("web race v2 cancellation and start refusal join pending tasks without starting or retrying the actor")]
    private static async Task CancellationAndRefusal()
    {
        using var stop=new CancellationTokenSource();var canceled=new Peer{CancelInitial=stop.Cancel};string path=Output();
        await RejectAsync(()=>Capture(canceled,path,stop.Token));HostCheck.True(canceled.RaceCanceled&&!canceled.ChurnCanceled&&canceled.Starts==0&&!canceled.Calls.Contains("POST /__clros/churn"));
        HostCheck.True(!NativeAotWebPayload.InspectSignalRaceAttempts(File.ReadAllText(path+".attempts.json"),File.ReadAllText(path),Endpoint,Health).SemanticComplete);
        var refused=new Peer{RefuseStart=true};path=Output();await RejectAsync(()=>Capture(refused,path),"web_signalrace_request_result");
        HostCheck.True(refused.Starts==1&&refused.ChurnCanceled&&refused.RaceCanceled);
        HostCheck.True(!NativeAotWebPayload.InspectSignalRaceAttempts(File.ReadAllText(path+".attempts.json"),File.ReadAllText(path),Endpoint,Health).SemanticComplete);
        var early=new Peer{EarlyPhase=true};await RejectAsync(()=>Capture(early,Output()),"web_signalrace_missed_initial_gate");HostCheck.True(early.RaceCanceled&&early.Starts==0&&!early.Calls.Contains("POST /__clros/churn"));
    }
}
