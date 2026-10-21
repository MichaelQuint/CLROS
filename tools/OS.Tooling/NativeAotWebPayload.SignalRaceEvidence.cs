using System.Globalization;
using System.Text;
using System.Text.Json;

namespace OS.Tooling;

public static partial class NativeAotWebPayload
{
    /// <summary>Validate copied HTTP stimulus/progress order for one fresh app
    /// session and return copied role selection. This starts no I/O and grants
    /// no original process, code-role, pointed-to contents or guest authority.
    /// Incomplete, reordered, inconsistent or unsupported values throw.</summary>
    public static QemuTranscripts.WebSignalRaceSelection ValidateSignalRaceEvidence(string json,string expectedEndpoint,string expectedHealthBody)
    {
        RaceInputs(expectedEndpoint,expectedHealthBody);
        RaceNeed(json is {Length:>0 and <=65536}&&Encoding.UTF8.GetByteCount(json)<=65536,"input");
        using var document=JsonDocument.Parse(json,new JsonDocumentOptions{MaxDepth=8});var root=document.RootElement;
        ChurnKeys(root,"schema","endpoint","samples");
        RaceNeed(ChurnString(root.GetProperty("schema"),64)=="clros.web.signalrace.v1"&&ChurnString(root.GetProperty("endpoint"),64)==expectedEndpoint,"schema_endpoint");
        var samples=root.GetProperty("samples");RaceNeed(samples.ValueKind==JsonValueKind.Array&&samples.GetArrayLength()==19,"samples");
        int position=0;ulong[]? previous=null;
        string Row(string kind,int status)
        {
            var row=samples[position++];ChurnKeys(row,"kind","status","body");
            RaceNeed(ChurnString(row.GetProperty("kind"),32)==kind&&ChurnInt(row.GetProperty("status"))==status,"sequence");
            return ChurnString(row.GetProperty("body"),4096);
        }
        void Body(string kind,int status,string text)=>RaceNeed(Row(kind,status)==text,"body");
        ulong[] Churn(string kind,ulong started,ulong completed,ulong canceled,ulong active)
        {
            var value=ChurnStatus(Row(kind,200));
            RaceNeed(value[0]==active&&value[1]==active&&value[3]==started&&value[4]==completed&&value[5]==canceled&&
                value[2]==(started==0?0UL:1UL)&&value[11]==active*65536&&value[12]==(started==0?0UL:65536UL),"churn_lifetime");
            if(previous is not null)foreach(int field in new[]{2,3,4,5,6,7,8,9,10,12})RaceNeed(value[field]>=previous[field],"churn_monotonic");
            previous=value;return value;
        }
        RaceNeed(Churn("baseline-churn",0,0,0,0).All(n=>n==0),"baseline");
        RaceNeed(ReadRaceStatus(Row("baseline-race",200)).All(n=>n==0),"idle");Body("health-before",200,expectedHealthBody);
        var pending=Churn("churn-pending",1,0,0,1);RaceNeed(pending[8]>=1&&pending[9]>=1&&pending[10]>=1,"churn_progress");
        var armed=ReadRaceStatus(Row("recovery-armed",200));RaceNeed(armed[0]==4&&armed[5]==0,"recovery_gate");
        var overlap=Churn("churn-overlap",1,0,0,1);foreach(int field in new[]{8,9,10})RaceNeed(overlap[field]>pending[field],"overlap_progress");
        Body("overlap-health",200,expectedHealthBody);Body("churn-cancel",0,"");
        var drained=Churn("churn-drained",1,0,1,0);
        Body("recovery-request",200,"CLROS_SIGNAL_RACE_RECOVERY_REQUESTED");Body("recovery-repeat",409,"CLROS_SIGNAL_RACE_RECOVERY_REFUSED");
        var selection=ReadRaceResult(Row("race-complete",200));
        RaceNeed(armed[1]==(ulong)selection.Pid&&armed[2]==(ulong)selection.WaiterTid&&armed[3]==(ulong)selection.ActorTid&&armed[4]==(ulong)selection.CallbackTid,"role_binding");
        var final=ReadRaceStatus(Row("race-final",200));
        RaceNeed(final[0]==7&&final[5]==1&&final.AsSpan(1,4).SequenceEqual(armed.AsSpan(1,4)),"final_roles");
        Body("health-after",200,expectedHealthBody);Body("churn-complete",200,"CLROS_CHURN_OK iterations=8 bytes=65536 timers=8 workers=8 collections=8");
        var done=Churn("final-churn",2,1,1,0);foreach(int field in new[]{8,9,10})RaceNeed(done[field]==drained[field]+8,"fresh_progress");
        Body("health-final",200,expectedHealthBody);Body("repeat-race",409,"CLROS_SIGNAL_RACE_BUSY");Body("repeat-probe",409,"CLROS_SIGNAL_PROBE_BUSY");
        return selection;
    }

    private static readonly string[] RaceStatusKeys="phase pid waiter actor callback recoveryRequested".Split(' ');
    private static readonly string[] RaceResultKeys=("signal pid actor waiter callback callbacks actorJoined waiterJoined consumed recovery recoveryWriter initialValue initialErrno "+
        "recoveryValue recoveryEvents recoveryTag handler flags restorer actionMask originalHandler originalFlags originalRestorer originalActionMask "+
        "maskBefore maskDuring maskAfter altAddress altFlags altBytes descriptorsClosed actionRestored altUnchanged").Split(' ');
    private static ulong[] RaceFields(string text,string prefix,string[] keys)
    {
        RaceNeed(text.StartsWith(prefix,StringComparison.Ordinal),"prefix");string[] tokens=text[prefix.Length..].Split(' ');
        RaceNeed(tokens.Length==keys.Length,"fields");ulong[] values=new ulong[keys.Length];
        for(int i=0;i<keys.Length;i++)
        {
            string name=keys[i]+"=";RaceNeed(tokens[i].StartsWith(name,StringComparison.Ordinal),"field_name");string value=tokens[i][name.Length..];
            if(keys[i]=="initialValue"){RaceNeed(value=="-1","initial_value");values[i]=ulong.MaxValue;continue;}
            RaceNeed(value.Length is >0 and <=20&&(value.Length==1||value[0]!='0')&&value.All(c=>c is >= '0' and <= '9')&&
                ulong.TryParse(value,NumberStyles.None,CultureInfo.InvariantCulture,out values[i]),"number");
        }
        return values;
    }
    private static ulong[] ReadRaceStatus(string text)
    {
        var value=RaceFields(text,"CLROS_SIGNAL_RACE_STATUS ",RaceStatusKeys);
        RaceNeed(value[0]<=8&&value[5]<=1&&value.Skip(1).Take(4).All(n=>n<=int.MaxValue),"status_range");
        if(value[0]==0)RaceNeed(value.All(n=>n==0),"idle");
        else
        {
            RaceNeed(value[1]>0,"pid");
            // Cleanup/failure may skip milestones and preserve unknown IDs.
            // Known IDs still cannot alias the waiter; only acknowledged or
            // successful phases require all identities to have been latched.
            bool live=value[0] is >=2 and <=5 or 7,acknowledged=value[0] is >=3 and <=5 or 7;
            RaceNeed((!live||value[2]>0&&value[3]>0)&&(value[2]==0||value[2]!=value[3]),"status_threads");
            RaceNeed((!acknowledged||value[4]>0)&&(value[2]==0||value[2]!=value[4]),"status_callback");
            RaceNeed(value[5]==0||value[0]>=4,"status_request");
        }
        return value;
    }
    private static QemuTranscripts.WebSignalRaceSelection ReadRaceResult(string text)
    {
        var value=RaceFields(text,"CLROS_SIGNAL_RACE_OK ",RaceResultKeys);
        ulong Get(string key)=>value[Array.IndexOf(RaceResultKeys,key)];
        foreach(string key in new[]{"callbacks","actorJoined","waiterJoined","recoveryValue","recoveryEvents","actionRestored","altUnchanged"})RaceNeed(Get(key)==1,"result_flags");
        RaceNeed(Get("signal")==12&&Get("consumed")==90&&Get("recovery")==107&&Get("initialErrno")==4&&Get("recoveryTag")==0x50524f4245&&Get("descriptorsClosed")==3,"result_values");
        foreach(string key in new[]{"pid","waiter","actor","callback","recoveryWriter"})RaceNeed(Get(key) is >0 and <=int.MaxValue,"result_threads");
        RaceNeed(Get("waiter")!=Get("actor")&&Get("waiter")!=Get("callback")&&Get("waiter")!=Get("recoveryWriter"),"distinct_waiter");
        RaceNeed(Get("handler")>1&&Get("restorer")!=0&&(Get("flags")&0x14000004)==0x14000004&&Get("originalHandler")==0,"action_shape");
        const ulong usr2=1UL<<11,unblockable=(1UL<<8)|(1UL<<18);
        RaceNeed(Get("maskDuring")== (Get("maskBefore")&~usr2)&&Get("maskAfter")==Get("maskBefore")&&
            ((Get("maskBefore")|Get("maskDuring")|Get("maskAfter"))&unblockable)==0&&Get("altFlags")<=uint.MaxValue,"thread_state");
        return new((int)Get("pid"),(int)Get("waiter"),(int)Get("actor"),(int)Get("callback"),(int)Get("recoveryWriter"));
    }
    private static void RaceInputs(string endpoint,string health)
    {
        RaceNeed(endpoint is {Length:>0 and <=64}&&Uri.TryCreate(endpoint,UriKind.Absolute,out var uri)&&uri.Scheme=="http"&&uri.Host=="127.0.0.1"&&
            uri.Port is >=1 and <=65535&&endpoint=="http://127.0.0.1:"+uri.Port.ToString(CultureInfo.InvariantCulture),"endpoint");
        RaceNeed(health is {Length:>0 and <=1024}&&!health.Any(char.IsControl),"health");
    }
    private static void RaceNeed(bool condition,string reason){if(!condition)throw new InvalidDataException("web_signalrace_"+reason);}
}
