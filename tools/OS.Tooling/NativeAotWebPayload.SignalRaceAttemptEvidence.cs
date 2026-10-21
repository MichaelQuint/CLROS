using System.Text;
using System.Text.Json;

namespace OS.Tooling;

public static partial class NativeAotWebPayload
{
    /// <summary>Counts copied logical HTTP attempts after every owned task has
    /// joined. SemanticComplete covers the HTTP controller contract only;
    /// these values grant no wire, native, process-death or guest authority.</summary>
    public readonly record struct SignalRaceAttemptSummary(int Attempts,int Responses,int Cancelled,int Faulted,bool SemanticComplete);

    private static readonly (string Kind,string Method,string Path)[] RaceSampleRoutes=
    [
        ("baseline-churn","GET","/__clros/churn/status"),("baseline-race","GET","/__clros/signalrace/status"),("health-before","GET","/healthz"),
        ("churn-pending","GET","/__clros/churn/status"),("recovery-armed","GET","/__clros/signalrace/status"),("churn-overlap","GET","/__clros/churn/status"),
        ("overlap-health","GET","/healthz"),("churn-cancel","POST","/__clros/churn"),("churn-drained","GET","/__clros/churn/status"),
        ("recovery-request","POST","/__clros/signalrace/recover"),("recovery-repeat","POST","/__clros/signalrace/recover"),("race-complete","POST","/__clros/signalrace"),
        ("race-final","GET","/__clros/signalrace/status"),("health-after","GET","/healthz"),("churn-complete","POST","/__clros/churn"),
        ("final-churn","GET","/__clros/churn/status"),("health-final","GET","/healthz"),("repeat-race","POST","/__clros/signalrace"),("repeat-probe","POST","/__clros/signalprobe")
    ];
    private static readonly (string Kind,string Method,string Path)[] RaceStartSampleRoutes=
    [..RaceSampleRoutes[..3],("initial-armed","GET","/__clros/signalrace/status"),RaceSampleRoutes[3],
        ("start-request","POST","/__clros/signalrace/start"),..RaceSampleRoutes[4..]];

    /// <summary>Inspect borrowed copied attempt/selection JSON, including failed
    /// captures, without I/O. Requires bounded terminal attempts, raw body bytes,
    /// unique logical IDs/completion ordinals and exact selected-response links.
    /// Failed diagnostics remain failed; only a complete selected contract can
    /// report SemanticComplete. IDs identify API calls, never connections, wire
    /// exchanges, sent bytes, actual native execution or process cleanup.
    /// Malformed, inconsistent, incomplete-task or unsupported values throw.</summary>
    public static SignalRaceAttemptSummary InspectSignalRaceAttempts(string attemptsJson,string selectedJson,string expectedEndpoint,string expectedHealthBody)
    {
        RaceInputs(expectedEndpoint,expectedHealthBody);
        RaceNeed(attemptsJson is {Length:>0 and <=8*1024*1024}&&Encoding.UTF8.GetByteCount(attemptsJson)<=8*1024*1024,"attempt_input");
        RaceNeed(selectedJson is {Length:>0 and <=65536}&&Encoding.UTF8.GetByteCount(selectedJson)<=65536,"input");
        using var document=JsonDocument.Parse(attemptsJson,new JsonDocumentOptions{MaxDepth=12});var root=document.RootElement;
        using var selectedDocument=JsonDocument.Parse(selectedJson,new JsonDocumentOptions{MaxDepth=8});var selected=selectedDocument.RootElement;
        ChurnKeys(root,"schema","endpoint","semanticComplete","failure","attempts","selections");ChurnKeys(selected,"schema","endpoint","samples");
        string schema=ChurnString(selected.GetProperty("schema"),64);bool eventOwned=schema=="clros.web.signalrace.v3",startGate=eventOwned||schema=="clros.web.signalrace.v2";
        var routes=startGate?RaceStartSampleRoutes:RaceSampleRoutes;
        RaceNeed((startGate||schema=="clros.web.signalrace.v1")&&
            ChurnString(root.GetProperty("schema"),64)==(eventOwned?"clros.web.signalrace.attempts.v3":startGate?"clros.web.signalrace.attempts.v2":"clros.web.signalrace.attempts.v1")&&
            ChurnString(root.GetProperty("endpoint"),64)==expectedEndpoint&&ChurnString(selected.GetProperty("endpoint"),64)==expectedEndpoint,"attempt_schema_endpoint");
        bool complete=RaceBoolean(root.GetProperty("semanticComplete"));string failure=ChurnString(root.GetProperty("failure"),128);
        RaceNeed(!failure.Any(char.IsControl)&&complete==(failure.Length==0),"attempt_failure");
        var attempts=root.GetProperty("attempts");var samples=selected.GetProperty("samples");var links=root.GetProperty("selections");
        RaceNeed(attempts.ValueKind==JsonValueKind.Array&&attempts.GetArrayLength() is >0 and <=MaximumRaceAttempts,"attempt_count");
        RaceNeed(samples.ValueKind==JsonValueKind.Array&&samples.GetArrayLength()<=routes.Length&&links.ValueKind==JsonValueKind.Array&&links.GetArrayLength()==samples.GetArrayLength(),"attempt_selections");
        int responses=0,cancelled=0,faulted=0,serialCompletion=0,firstChurn=0,firstRace=0;HashSet<int> completions=[];List<byte[]> bodies=[];
        foreach(var row in attempts.EnumerateArray())
        {
            string[] keys=["id","method","path","completion","headers","status","mediaType","mediaTypeComplete","bodyBase64","bodyComplete","outcome","error"];
            ChurnKeys(row,eventOwned?[..keys,"signalVersion","churnVersion"]:keys);
            int id=ChurnInt(row.GetProperty("id")),completion=ChurnInt(row.GetProperty("completion")),status=ChurnInt(row.GetProperty("status"));
            RaceNeed(id==bodies.Count+1&&completion>=1&&completion<=attempts.GetArrayLength()&&completions.Add(completion),"attempt_identity");
            string method=ChurnString(row.GetProperty("method"),4),path=ChurnString(row.GetProperty("path"),64);
            RaceNeed(routes.Any(route=>route.Method==method&&route.Path==path),"attempt_route");
            if(eventOwned)RaceNeed(ChurnString(row.GetProperty("signalVersion"),3)==EventSignalVersion(method,path),"attempt_signal_version");
            if(eventOwned)RaceNeed(ChurnString(row.GetProperty("churnVersion"),3)==EventChurnVersion(method,path,firstChurn==0),"attempt_churn_version");
            // Only the first two long-lived POSTs overlap later API calls.
            bool pending=false;
            if(method=="POST"&&path=="/__clros/churn"&&firstChurn==0){firstChurn=id;pending=true;}
            if(method=="POST"&&path=="/__clros/signalrace"&&firstRace==0){firstRace=id;pending=true;}
            if(!pending){RaceNeed(completion>serialCompletion,"attempt_completion_order");serialCompletion=completion;}
            bool headers=RaceBoolean(row.GetProperty("headers")),bodyComplete=RaceBoolean(row.GetProperty("bodyComplete")),mediaComplete=RaceBoolean(row.GetProperty("mediaTypeComplete"));
            string media=ChurnString(row.GetProperty("mediaType"),128),encoded=ChurnString(row.GetProperty("bodyBase64"),5464),outcome=ChurnString(row.GetProperty("outcome"),16),error=ChurnString(row.GetProperty("error"),128);
            byte[] bytes;try{bytes=Convert.FromBase64String(encoded);}catch(FormatException){throw new InvalidDataException("web_signalrace_attempt_base64");}
            RaceNeed(bytes.Length<=4096&&Convert.ToBase64String(bytes)==encoded,"attempt_body");bodies.Add(bytes);
            RaceNeed(headers?status is >=100 and <=999:status==0&&media==""&&mediaComplete&&!bodyComplete&&bytes.Length==0,"attempt_headers");
            RaceNeed(!error.Any(char.IsControl),"attempt_error");
            switch(outcome)
            {
                case "response":
                    RaceNeed(headers&&mediaComplete&&media=="text/plain"&&bodyComplete&&error=="","attempt_response");
                    _=RaceDecode(bytes);responses++;break;
                case "cancelled":RaceNeed(error=="cancelled","attempt_cancelled");cancelled++;break;
                case "faulted":RaceNeed(error.Length>0,"attempt_faulted");faulted++;break;
                default:throw new InvalidDataException("web_signalrace_attempt_terminal");
            }
        }
        HashSet<int> selectedAttempts=[];int cancellationOwner=0,serialSelection=0;
        for(int index=0;index<samples.GetArrayLength();index++)
        {
            var sample=samples[index];var link=links[index];var route=routes[index];
            ChurnKeys(sample,"kind","status","body");ChurnKeys(link,"sample","attempt","related");
            int id=ChurnInt(link.GetProperty("attempt")),related=ChurnInt(link.GetProperty("related"));
            RaceNeed(ChurnInt(link.GetProperty("sample"))==index&&ChurnString(sample.GetProperty("kind"),32)==route.Kind,"attempt_sample");
            string body=ChurnString(sample.GetProperty("body"),4096);int status=ChurnInt(sample.GetProperty("status"));
            if(route.Kind=="churn-cancel")
            {
                RaceNeed(id==0&&related>0&&related<=attempts.GetArrayLength()&&status==0&&body=="","attempt_cancel_link");
                var row=attempts[related-1];
                RaceNeed(row.GetProperty("method").GetString()==route.Method&&row.GetProperty("path").GetString()==route.Path&&row.GetProperty("outcome").GetString()=="cancelled"&&
                    !attempts.EnumerateArray().Take(related-1).Any(prior=>prior.GetProperty("path").GetString()==route.Path&&prior.GetProperty("method").GetString()==route.Method),"attempt_cancel_owner");
                cancellationOwner=related;
            }
            else
            {
                RaceNeed(id>0&&id<=attempts.GetArrayLength()&&related==0&&selectedAttempts.Add(id),"attempt_response_link");
                var row=attempts[id-1];
                RaceNeed(row.GetProperty("method").GetString()==route.Method&&row.GetProperty("path").GetString()==route.Path&&row.GetProperty("outcome").GetString()=="response"&&
                    ChurnInt(row.GetProperty("status"))==status&&RaceDecode(bodies[id-1])==body,"attempt_selected_response");
                if(route.Kind!="race-complete"){RaceNeed(id>serialSelection,"attempt_selection_order");serialSelection=id;}
            }
        }
        if(complete)
        {
            _=ValidateSignalRaceEvidence(selectedJson,expectedEndpoint,expectedHealthBody);
            RaceNeed(faulted==0&&cancelled==1&&cancellationOwner>0,"attempt_complete_outcomes");
            int Selected(int sample)=>ChurnInt(links[sample].GetProperty("attempt"));
            int Kind(string kind)=>Selected(Array.FindIndex(routes,r=>r.Kind==kind));
            int Completed(int id)=>ChurnInt(attempts[id-1].GetProperty("completion"));
            bool initialOrder=startGate?
                firstRace>Kind("health-before")&&firstRace<Kind("initial-armed")&&firstChurn>Kind("initial-armed")&&firstChurn<Kind("churn-pending"):
                firstChurn>Kind("health-before")&&firstChurn<Kind("churn-pending")&&firstRace>Kind("churn-pending")&&firstRace<Kind("recovery-armed");
            RaceNeed(cancellationOwner==firstChurn&&initialOrder&&
                Completed(firstChurn)>Completed(Kind("overlap-health"))&&Completed(firstChurn)<Completed(Kind("churn-drained"))&&
                Completed(firstRace)>Completed(Kind("churn-drained"))&&Completed(firstRace)<Completed(Kind("race-final")),"attempt_pending_order");
            RaceNeed(Selected(routes.Length-1)==attempts.GetArrayLength(),"attempt_terminal_request");
            foreach(var row in attempts.EnumerateArray())
            {
                int id=ChurnInt(row.GetProperty("id"));if(id==cancellationOwner||selectedAttempts.Contains(id))continue;
                string path=row.GetProperty("path").GetString()!;
                RaceNeed(row.GetProperty("method").GetString()=="GET"&&ChurnInt(row.GetProperty("status"))==200&&
                    path is "/__clros/churn/status" or "/__clros/signalrace/status","attempt_unselected_request");
                if(path=="/__clros/churn/status")
                {
                    RaceNeed(id<Kind("baseline-churn")||id>firstChurn&&id<Kind("churn-pending")||
                        id>Kind("recovery-armed")&&id<Kind("churn-overlap")||id>Kind("overlap-health")&&id<Kind("churn-drained")||
                        id>Kind("churn-complete")&&id<Kind("final-churn"),"attempt_churn_poll_window");
                    _=ChurnStatus(RaceDecode(bodies[id-1]));
                }
                else
                {
                    ulong phase=ReadRaceStatus(RaceDecode(bodies[id-1]),eventOwned)[0];
                    RaceNeed(startGate?
                        id>firstRace&&id<Kind("initial-armed")&&phase<2||id>Kind("start-request")&&id<Kind("recovery-armed")&&phase<4:
                        id>firstRace&&id<Kind("recovery-armed")&&phase<4,"attempt_unselected_status");
                }
            }
        }
        return new(attempts.GetArrayLength(),responses,cancelled,faulted,complete);
    }
    private static bool RaceBoolean(JsonElement value)
    {RaceNeed(value.ValueKind is JsonValueKind.True or JsonValueKind.False,"attempt_boolean");return value.GetBoolean();}
    private static string RaceDecode(byte[] bytes)
    {try{return new UTF8Encoding(false,true).GetString(bytes);}catch(DecoderFallbackException){throw new InvalidDataException("web_signalrace_attempt_utf8");}}
}
