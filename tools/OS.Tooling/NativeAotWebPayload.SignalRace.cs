using System.Text;
using System.Text.Json;

namespace OS.Tooling;

public static partial class NativeAotWebPayload
{
    private sealed record RaceHttpSample(string kind,int status,string body);

    /// <summary>Borrow an owned app endpoint/client and capture one bounded,
    /// non-retried race during a pending churn request. Owns and joins every
    /// request task it starts; the caller owns the app, client and process death.
    /// Fresh selected output and a .attempts.json companion retain every logical
    /// attempt, partial response and failure after request joins. Logical IDs
    /// grant no wire connection/exchange or sent-byte authority. A report grants no
    /// native/guest execution, callback-role or pointed-to-content authority.</summary>
    public static async Task<string> CaptureSignalRaceAsync(HttpClient client,string endpoint,string expectedHealthBody,
        string observationPath,CancellationToken cancellation=default)
    {
        ArgumentNullException.ThrowIfNull(client);RaceInputs(endpoint,expectedHealthBody);
        string path=Path.GetFullPath(observationPath),parent=Path.GetDirectoryName(path)!;
        AuthorityFiles.RequirePlainPath(parent,parent,"web_signalrace_output_path");
        using var output=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.Read);
        using var attemptOutput=new FileStream(path+".attempts.json",FileMode.CreateNew,FileAccess.Write,FileShare.Read);
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancellation);deadline.CancelAfter(TimeSpan.FromSeconds(30));
        using var cancelChurn=CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
        using var cancelRace=CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
        using var churnRequest=Post("/__clros/churn");using var raceRequest=Post("/__clros/signalrace");
        Task<RaceHttpReply>? churn=null,race=null;List<RaceHttpSample> samples=[];RaceAttemptLog attempts=new(endpoint);
        bool semanticComplete=false,primaryFailure=false;string failure="capture_incomplete";RaceHttpAttempt? churnCall=null,raceCall=null;
        HttpRequestMessage Post(string target)
        {
            var message=new HttpRequestMessage(HttpMethod.Post,endpoint+target){Content=new ByteArrayContent([])};
            message.Headers.Add(target=="/__clros/churn"?"X-CLROS-Churn":"X-CLROS-Signal","v1");return message;
        }
        async Task<RaceHttpReply> Get(string target)
        {
            using var request=new HttpRequestMessage(HttpMethod.Get,endpoint+target);
            return await attempts.SendAsync(client,request,target,deadline.Token);
        }
        void Select(string kind,RaceHttpReply reply){attempts.Select(samples.Count,reply);samples.Add(new(kind,reply.Status,reply.Body));}
        async Task Observe(string kind,string target)
        {
            Select(kind,await Get(target));
            if(target=="/healthz")RaceNeed(samples[^1].status==200&&samples[^1].body==expectedHealthBody,"health_response");
        }
        void Pending(bool both)
        {RaceNeed(churn is not null&&!churn.IsCompleted&&churnCall is not null&&!churnCall.headers&&
            (!both||race is not null&&!race.IsCompleted&&raceCall is not null&&!raceCall.headers),"lost_overlap");}
        async Task Churn(string kind,bool pending,ulong[]? after=null)
        {
            for(int attempt=0;attempt<200;attempt++)
            {
                if(pending)Pending(race is not null);
                var response=await Get("/__clros/churn/status");RaceNeed(response.Status==200,"churn_status");var value=ChurnStatus(response.Body);
                if(pending)Pending(race is not null);
                if(value[0]==(pending?1UL:0)&&value[1]==value[0]&&(!pending||value[8]>=1&&value[9]>=1&&value[10]>=1)&&
                    (after is null||value[8]>after[8]&&value[9]>after[9]&&value[10]>after[10]))
                {Select(kind,response);return;}
                await Task.Delay(5,deadline.Token);
            }
            throw new InvalidDataException("web_signalrace_churn_progress");
        }
        async Task Request(string kind,string target,int status,string expected)
        {
            using var message=Post(target);Select(kind,await attempts.SendAsync(client,message,target,deadline.Token));
            RaceNeed(samples[^1].status==status&&samples[^1].body==expected,"request_result");
        }
        string Json()=>JsonSerializer.Serialize(new{schema="clros.web.signalrace.v1",endpoint,samples});
        try
        {
            await Churn("baseline-churn",false);await Observe("baseline-race","/__clros/signalrace/status");await Observe("health-before","/healthz");
            // Reject an already-used app before starting either active request.
            RaceNeed(ChurnStatus(samples[0].body).All(n=>n==0)&&ReadRaceStatus(samples[1].body).All(n=>n==0)&&samples[1].status==200&&samples[2].status==200&&samples[2].body==expectedHealthBody,"fresh_session");
            churn=attempts.SendAsync(client,churnRequest,"/__clros/churn",cancelChurn.Token);
            churnCall=attempts.LastStarted;
            await Churn("churn-pending",true);
            var beforeRace=ChurnStatus(samples[^1].body);
            race=attempts.SendAsync(client,raceRequest,"/__clros/signalrace",cancelRace.Token);
            raceCall=attempts.LastStarted;
            bool armed=false;
            for(int attempt=0;attempt<200;attempt++)
            {
                Pending(true);var response=await Get("/__clros/signalrace/status");
                RaceNeed(response.Status==200,"race_status");var status=ReadRaceStatus(response.Body);Pending(true);
                RaceNeed(status[0]<=4,"missed_recovery_gate");
                if(status[0]==4){Select("recovery-armed",response);armed=true;break;}
                await Task.Delay(5,deadline.Token);
            }
            RaceNeed(armed,"recovery_gate_timeout");await Churn("churn-overlap",true,beforeRace);await Observe("overlap-health","/healthz");Pending(true);
            cancelChurn.Cancel();bool canceled=false;
            try{_=await churn;}catch(OperationCanceledException)when(cancelChurn.IsCancellationRequested){canceled=true;}
            RaceNeed(canceled,"churn_cancellation");attempts.SelectCancellation(samples.Count,churnCall.id);samples.Add(new("churn-cancel",0,""));await Churn("churn-drained",false);
            RaceNeed(!race.IsCompleted,"race_ended_before_recovery");
            await Request("recovery-request","/__clros/signalrace/recover",200,"CLROS_SIGNAL_RACE_RECOVERY_REQUESTED");
            await Request("recovery-repeat","/__clros/signalrace/recover",409,"CLROS_SIGNAL_RACE_RECOVERY_REFUSED");
            Select("race-complete",await race);
            await Observe("race-final","/__clros/signalrace/status");await Observe("health-after","/healthz");
            await Request("churn-complete","/__clros/churn",200,"CLROS_CHURN_OK iterations=8 bytes=65536 timers=8 workers=8 collections=8");
            await Churn("final-churn",false);await Observe("health-final","/healthz");
            await Request("repeat-race","/__clros/signalrace",409,"CLROS_SIGNAL_RACE_BUSY");
            await Request("repeat-probe","/__clros/signalprobe",409,"CLROS_SIGNAL_PROBE_BUSY");
            string json=Json();_=ValidateSignalRaceEvidence(json,endpoint,expectedHealthBody);semanticComplete=true;failure="";return json;
        }
        catch(Exception error){primaryFailure=true;failure=RaceCaptureFailure(error);throw;}
        finally
        {
            cancelChurn.Cancel();cancelRace.Cancel();Exception? joinFailure=null;
            foreach(var pending in new[]{churn,race})if(pending is not null)
            {
                try{_=await pending;}catch(OperationCanceledException)when(cancelChurn.IsCancellationRequested||cancelRace.IsCancellationRequested){}
                catch(Exception error){joinFailure??=error;}
            }
            if(joinFailure is not null){semanticComplete=false;if(!primaryFailure)failure="web_signalrace_request_join_failed";}
            try{byte[] json=Encoding.UTF8.GetBytes(Json());await output.WriteAsync(json);await output.FlushAsync();}
            finally{byte[] json=Encoding.UTF8.GetBytes(attempts.Json(semanticComplete,failure));await attemptOutput.WriteAsync(json);await attemptOutput.FlushAsync();}
            if(joinFailure is not null&&!primaryFailure)throw new InvalidDataException("web_signalrace_request_join_failed",joinFailure);
        }
    }

    private static void ValidateRecordedSignalRace(string destination,bool requireAttempts=false)
    {
        string path=Path.Combine(destination,"web-signalrace.json");Require(new FileInfo(path).Length is >0 and <=65536,"signalrace_recorded_size");
        using var http=JsonDocument.Parse(File.ReadAllText(Path.Combine(destination,"web-http-signalrace.json")));
        _=ValidateSignalRaceEvidence(File.ReadAllText(path),http.RootElement.GetProperty("endpoint").GetString()!,DefaultWebBody);
        if(requireAttempts)
        {
            string companion=path+".attempts.json";Require(new FileInfo(companion).Length is >0 and <=8*1024*1024,"signalrace_attempt_size");
            Require(InspectSignalRaceAttempts(File.ReadAllText(companion),File.ReadAllText(path),http.RootElement.GetProperty("endpoint").GetString()!,DefaultWebBody).SemanticComplete,"signalrace_attempts_incomplete");
        }
    }
}
