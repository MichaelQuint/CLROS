using System.Net;
using System.Text;
using System.Text.Json;

namespace OS.Tooling;

public static partial class NativeAotWebPayload
{
    private sealed record RaceHttpSample(string kind,int status,string body);

    /// <summary>Borrow an owned app endpoint/client and capture one bounded,
    /// non-retried race during a pending churn request. Owns and joins every
    /// request task it starts; the caller owns the app, client and process death.
    /// Fresh output is retained even on failure. A copied report grants no
    /// native/guest execution, callback-role or pointed-to-content authority.</summary>
    public static async Task<string> CaptureSignalRaceAsync(HttpClient client,string endpoint,string expectedHealthBody,
        string observationPath,CancellationToken cancellation=default)
    {
        ArgumentNullException.ThrowIfNull(client);RaceInputs(endpoint,expectedHealthBody);
        string path=Path.GetFullPath(observationPath),parent=Path.GetDirectoryName(path)!;
        AuthorityFiles.RequirePlainPath(parent,parent,"web_signalrace_output_path");
        using var output=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.Read);
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancellation);deadline.CancelAfter(TimeSpan.FromSeconds(30));
        using var cancelChurn=CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
        using var cancelRace=CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
        using var churnRequest=Post("/__clros/churn");using var raceRequest=Post("/__clros/signalrace");
        Task<HttpResponseMessage>? churn=null,race=null;List<RaceHttpSample> samples=[];
        HttpRequestMessage Post(string target)
        {
            var message=new HttpRequestMessage(HttpMethod.Post,endpoint+target){Content=new ByteArrayContent([])};
            message.Headers.Add(target=="/__clros/churn"?"X-CLROS-Churn":"X-CLROS-Signal","v1");return message;
        }
        async Task<string> Read(HttpResponseMessage response)
        {
            RaceNeed(response.Content.Headers.ContentType?.MediaType=="text/plain","content_type");
            using var stream=await response.Content.ReadAsStreamAsync(deadline.Token);using MemoryStream bytes=new();byte[] chunk=new byte[1024];
            for(;;){int n=await stream.ReadAsync(chunk,deadline.Token);if(n==0)break;RaceNeed(bytes.Length+n<=4096,"response_size");bytes.Write(chunk,0,n);}
            return new UTF8Encoding(false,true).GetString(bytes.ToArray());
        }
        async Task Observe(string kind,string target)
        {
            using var response=await client.GetAsync(endpoint+target,HttpCompletionOption.ResponseHeadersRead,deadline.Token);
            samples.Add(new(kind,(int)response.StatusCode,await Read(response)));
            if(target=="/healthz")RaceNeed(samples[^1].status==200&&samples[^1].body==expectedHealthBody,"health_response");
        }
        void Pending(bool both)
        {RaceNeed(churn is not null&&!churn.IsCompleted&&(!both||race is not null&&!race.IsCompleted),"lost_overlap");}
        async Task Churn(string kind,bool pending,ulong[]? after=null)
        {
            for(int attempt=0;attempt<200;attempt++)
            {
                if(pending)Pending(race is not null);
                using var response=await client.GetAsync(endpoint+"/__clros/churn/status",HttpCompletionOption.ResponseHeadersRead,deadline.Token);
                string body=await Read(response);RaceNeed(response.StatusCode==HttpStatusCode.OK,"churn_status");var value=ChurnStatus(body);
                if(pending)Pending(race is not null);
                if(value[0]==(pending?1UL:0)&&value[1]==value[0]&&(!pending||value[8]>=1&&value[9]>=1&&value[10]>=1)&&
                    (after is null||value[8]>after[8]&&value[9]>after[9]&&value[10]>after[10]))
                {samples.Add(new(kind,200,body));return;}
                await Task.Delay(5,deadline.Token);
            }
            throw new InvalidDataException("web_signalrace_churn_progress");
        }
        async Task Request(string kind,string target,int status,string expected)
        {
            using var message=Post(target);using var response=await client.SendAsync(message,HttpCompletionOption.ResponseHeadersRead,deadline.Token);
            samples.Add(new(kind,(int)response.StatusCode,await Read(response)));
            RaceNeed(samples[^1].status==status&&samples[^1].body==expected,"request_result");
        }
        string Json()=>JsonSerializer.Serialize(new{schema="clros.web.signalrace.v1",endpoint,samples});
        try
        {
            await Churn("baseline-churn",false);await Observe("baseline-race","/__clros/signalrace/status");await Observe("health-before","/healthz");
            // Reject an already-used app before starting either active request.
            RaceNeed(ChurnStatus(samples[0].body).All(n=>n==0)&&ReadRaceStatus(samples[1].body).All(n=>n==0)&&samples[1].status==200&&samples[2].status==200&&samples[2].body==expectedHealthBody,"fresh_session");
            churn=client.SendAsync(churnRequest,HttpCompletionOption.ResponseHeadersRead,cancelChurn.Token);
            await Churn("churn-pending",true);
            var beforeRace=ChurnStatus(samples[^1].body);
            race=client.SendAsync(raceRequest,HttpCompletionOption.ResponseHeadersRead,cancelRace.Token);
            bool armed=false;
            for(int attempt=0;attempt<200;attempt++)
            {
                Pending(true);using var response=await client.GetAsync(endpoint+"/__clros/signalrace/status",HttpCompletionOption.ResponseHeadersRead,deadline.Token);
                string body=await Read(response);RaceNeed(response.StatusCode==HttpStatusCode.OK,"race_status");var status=ReadRaceStatus(body);Pending(true);
                RaceNeed(status[0]<=4,"missed_recovery_gate");
                if(status[0]==4){samples.Add(new("recovery-armed",200,body));armed=true;break;}
                await Task.Delay(5,deadline.Token);
            }
            RaceNeed(armed,"recovery_gate_timeout");await Churn("churn-overlap",true,beforeRace);await Observe("overlap-health","/healthz");Pending(true);
            cancelChurn.Cancel();bool canceled=false;
            try{using var unexpected=await churn;}catch(OperationCanceledException)when(cancelChurn.IsCancellationRequested){canceled=true;}
            RaceNeed(canceled,"churn_cancellation");samples.Add(new("churn-cancel",0,""));await Churn("churn-drained",false);
            RaceNeed(!race.IsCompleted,"race_ended_before_recovery");
            await Request("recovery-request","/__clros/signalrace/recover",200,"CLROS_SIGNAL_RACE_RECOVERY_REQUESTED");
            await Request("recovery-repeat","/__clros/signalrace/recover",409,"CLROS_SIGNAL_RACE_RECOVERY_REFUSED");
            using(var response=await race){samples.Add(new("race-complete",(int)response.StatusCode,await Read(response)));}
            await Observe("race-final","/__clros/signalrace/status");await Observe("health-after","/healthz");
            await Request("churn-complete","/__clros/churn",200,"CLROS_CHURN_OK iterations=8 bytes=65536 timers=8 workers=8 collections=8");
            await Churn("final-churn",false);await Observe("health-final","/healthz");
            await Request("repeat-race","/__clros/signalrace",409,"CLROS_SIGNAL_RACE_BUSY");
            await Request("repeat-probe","/__clros/signalprobe",409,"CLROS_SIGNAL_PROBE_BUSY");
            string json=Json();_=ValidateSignalRaceEvidence(json,endpoint,expectedHealthBody);return json;
        }
        finally
        {
            cancelChurn.Cancel();cancelRace.Cancel();Exception? joinFailure=null;
            foreach(var pending in new[]{churn,race})if(pending is not null)
            {
                try{using var joined=await pending;}catch(OperationCanceledException)when(cancelChurn.IsCancellationRequested||cancelRace.IsCancellationRequested){}
                catch(Exception error){joinFailure??=error;}
            }
            byte[] json=Encoding.UTF8.GetBytes(Json());await output.WriteAsync(json);await output.FlushAsync();
            if(joinFailure is not null)throw new InvalidDataException("web_signalrace_request_join_failed",joinFailure);
        }
    }

    private static void ValidateRecordedSignalRace(string destination)
    {
        string path=Path.Combine(destination,"web-signalrace.json");Require(new FileInfo(path).Length is >0 and <=65536,"signalrace_recorded_size");
        using var http=JsonDocument.Parse(File.ReadAllText(Path.Combine(destination,"web-http-signalrace.json")));
        _=ValidateSignalRaceEvidence(File.ReadAllText(path),http.RootElement.GetProperty("endpoint").GetString()!,DefaultWebBody);
    }
}
