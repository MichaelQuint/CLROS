using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using OS.Tooling;
using Legacy = NativeAotWebSignalRaceScheduleEvidenceTests;

// SYNTHETIC journal/HTTP values and unsupported-host refusals only. No native
// process, actual syscall, scheduler mutation, original trace or guest authority.
internal static class NativeAotWebEventOwnedNativeScheduleTests
{
    // Pinned-wrapper raw register expectation, still requiring a fresh native
    // observation; this is not a universal Linux int(-1) encoding claim.
    private const string Untimed="232 0xc 0x700000 0x1 0xffffffffffffffff 0x0 0x0 0x700100 0x400000";
    private static List<JsonObject> Journal(bool retired=false)
    {
        var rows=Legacy.Journal(retired);
        foreach(var row in rows)
        {
            row["schema"]="clros.web.signalrace.schedule.v2";
            if(row["observation"] is JsonObject value&&value["pipe"] is JsonObject pipe)pipe["Syscall"]=Untimed;
        }
        return rows;
    }
    private static NativeAotWebPayload.SignalRaceScheduleSummary Read(List<JsonObject> rows)=>
        NativeAotWebPayload.InspectEventOwnedSignalRaceScheduleEvidence(Legacy.Text(rows),Legacy.Selection);
    private static void Reject(Action action,string? expected=null)
    {
        string? reason=null;try{action();}catch(Exception error)when(error is InvalidDataException or JsonException){reason=error.Message;}
        HostCheck.True(reason is not null);if(expected is not null)HostCheck.Equal(expected,reason);
    }
    private static (string Selected,string Attempts) Http()
    {
        var old=Legacy.Http(true);var selected=JsonNode.Parse(old.Selected)!;var attempts=JsonNode.Parse(old.Attempts)!;
        string Body(string value)=>value.Replace("CLROS_SIGNAL_RACE_STATUS","CLROS_SIGNAL_RACE_V3_STATUS",StringComparison.Ordinal)
            .Replace("CLROS_SIGNAL_RACE_OK","CLROS_SIGNAL_RACE_V3_OK",StringComparison.Ordinal);
        selected["schema"]="clros.web.signalrace.v3";attempts["schema"]="clros.web.signalrace.attempts.v3";
        foreach(var row in selected["samples"]!.AsArray())row!["body"]=Body(row!["body"]!.GetValue<string>());
        bool firstChurn=true;
        foreach(var row in attempts["attempts"]!.AsArray())
        {
            var value=row!.AsObject();
            string method=value["method"]!.GetValue<string>(),path=value["path"]!.GetValue<string>();
            value["signalVersion"]=path.StartsWith("/__clros/signalrace",StringComparison.Ordinal)?"v3":method=="POST"&&path=="/__clros/signalprobe"?"v1":"";
            value["churnVersion"]=method=="POST"&&path=="/__clros/churn"?(firstChurn?"v2":"v1"):"";
            if(method=="POST"&&path=="/__clros/churn")firstChurn=false;
            value["bodyBase64"]=Convert.ToBase64String(Encoding.UTF8.GetBytes(Body(Encoding.UTF8.GetString(Convert.FromBase64String(value["bodyBase64"]!.GetValue<string>())))));
        }
        return(selected.ToJsonString(),attempts.ToJsonString());
    }

    [HostTest("web event-owned schedule v2 accepts twelve or thirteen copied rows without admitting timed journals")]
    private static void CompleteJournal()
    {
        foreach(bool retired in new[]{false,true})
        {
            var rows=Journal(retired);string original=Legacy.Text(rows);
            HostCheck.Equal(new NativeAotWebPayload.SignalRaceScheduleSummary(42,43,44,45,12,10,11,retired?13:12),Read(rows));
            HostCheck.Equal(original,Legacy.Text(rows));
            foreach(var row in rows.Where(r=>r["observation"] is JsonObject o&&o["pipe"] is JsonObject))
                HostCheck.Equal(Untimed,row["observation"]!["pipe"]!["Syscall"]!.GetValue<string>());
            Reject(()=>NativeAotWebPayload.InspectSignalRaceScheduleEvidence(original,Legacy.Selection),"web_signalrace_schedule_schema");
        }
        Reject(()=>NativeAotWebPayload.InspectEventOwnedSignalRaceScheduleEvidence(Legacy.Text(Legacy.Journal()),Legacy.Selection),"web_signalrace_schedule_schema");
    }

    [HostTest("web event-owned journal requires each exact untimed syscall and matching epoll pipe snapshot")]
    private static void WaitRefusals()
    {
        foreach(int index in new[]{4,7,8})
        foreach(string timeout in new[]{"0x9c4","0x0","0xffffffff","0xfffffffffffffffe","-1"})
        {
            var rows=Journal();rows[index]["observation"]!["pipe"]!["Syscall"]=Untimed.Replace("0xffffffffffffffff",timeout,StringComparison.Ordinal);
            Reject(()=>Read(rows));
        }
        foreach(Action<List<JsonObject>> change in new Action<List<JsonObject>>[]
        {
            r=>r[4]["observation"]!["pipe"]!["Syscall"]=Untimed.Replace("0xc ","0xd ",StringComparison.Ordinal),
            r=>r[4]["observation"]!["pipe"]!["Syscall"]=Untimed.Replace("0x1 ","0x2 ",StringComparison.Ordinal),
            r=>r[4]["observation"]!["pipe"]!["WaiterState"]!["State"]="R",
            r=>r[8]["observation"]!["pipe"]!["WriteInfo"]="flags:\t02004001\nino:\t556\n",
            r=>r[7]["observation"]!["pipe"]!["EpollInfo"]="tfd: 10 events: 19 data: 0 pos:0\n",
            r=>r[8]["observation"]!["pipe"]!["WaitChannel"]="futex_wait_queue"
        }){var rows=Journal();change(rows);Reject(()=>Read(rows));}
        var drift=Journal();
        drift[7]["observation"]!["pipe"]!["Syscall"]=Untimed.Replace("232 ","232  ",StringComparison.Ordinal);
        Reject(()=>Read(drift),"web_signalrace_schedule_pipe_drift");
    }

    [HostTest("web event-owned schedule rejects mixed schemas clock order ownership restoration and stop substitutions")]
    private static void CompletionRefusals()
    {
        foreach(Action<List<JsonObject>> change in new Action<List<JsonObject>>[]
        {
            r=>r[7]["schema"]="clros.web.signalrace.schedule.v1",r=>r[7]["schema"]="clros.web.signalrace.schedule.v3",
            r=>r[8]["frequency"]=99L,r=>r[8]["monotonic"]=1L,r=>r[8]["operation"]="start-post-intent",
            r=>r[3]["observation"]!["Initial"]!["Start"]=91UL,
            r=>r[7]["observation"]!["policies"]![2]!["Policy"]=1,
            r=>r[10]["observation"]!["Policy"]=1,r=>r[^1]["observation"]!["nativeExit"]=false,
            r=>r[^1]["observation"]!["tasks"]![3]!["Start"]=91UL,r=>r.RemoveAt(r.Count-1)
        }){var rows=Journal();change(rows);Reject(()=>Read(rows));}
        string text=Legacy.Text(Journal());
        Reject(()=>NativeAotWebPayload.InspectEventOwnedSignalRaceScheduleEvidence(text.TrimEnd('\n'),Legacy.Selection));
        Reject(()=>NativeAotWebPayload.InspectEventOwnedSignalRaceScheduleEvidence(text.Replace("\"schema\":","\"schema\":\"duplicate\",\"schema\":",StringComparison.Ordinal),Legacy.Selection));
    }

    [HostTest("web native v8 pairs only selected attempts v3 and event journal while v5 v6 v7 stay historical")]
    private static void ReceiptPairing()
    {
        var current=Http();var timed=Legacy.Http(true);var old=Legacy.Http(false);
        string eventJournal=Legacy.Text(Journal()),timedJournal=Legacy.Text(Legacy.Journal());
        QemuTranscripts.WebSignalRaceSelection Read(string schema,string selected,string? attempts,string? journal)=>
            NativeAotWebPayload.ValidateSignalRaceQualificationEvidence(schema,selected,attempts,journal,Legacy.Endpoint,Legacy.Health);
        HostCheck.Equal(Legacy.Selection,Read("clros.nativeaot.web.v8",current.Selected,current.Attempts,eventJournal));
        HostCheck.Equal(Legacy.Selection,Read("clros.nativeaot.web.v7",timed.Selected,timed.Attempts,timedJournal));
        HostCheck.Equal(Legacy.Selection,Read("clros.nativeaot.web.v6",old.Selected,old.Attempts,null));
        HostCheck.Equal(Legacy.Selection,Read("clros.nativeaot.web.v5",old.Selected,null,null));
        foreach(string schema in new[]{"clros.nativeaot.web.v5","clros.nativeaot.web.v6","clros.nativeaot.web.v7"})
            Reject(()=>Read(schema,current.Selected,current.Attempts,eventJournal),"web_signalrace_schedule_protocol_pairing");
        Reject(()=>Read("clros.nativeaot.web.v8",timed.Selected,timed.Attempts,eventJournal),"web_signalrace_schedule_protocol_pairing");
        Reject(()=>Read("clros.nativeaot.web.v8",current.Selected,null,eventJournal),"web_signalrace_schedule_attempts_required");
        Reject(()=>Read("clros.nativeaot.web.v8",current.Selected,current.Attempts,null),"web_signalrace_schedule_required");
        Reject(()=>Read("clros.nativeaot.web.v8",current.Selected,timed.Attempts,eventJournal));
        Reject(()=>Read("clros.nativeaot.web.v8",current.Selected,current.Attempts,timedJournal),"web_signalrace_schedule_schema");
    }

    [HostTest("web event-owned native CLI is explicit and both private schedule constructors refuse unsupported hosts")]
    private static async Task CliAndPlatform()
    {
        string directory=Path.GetFullPath(Path.Combine("artifacts","host-tests","unused-event-native-"+Guid.NewGuid().ToString("N")));
        foreach(string[] args in new[]{new[]{"qualify","--event-owned-signal-race=false","--output",directory},
            new[]{"qualify","--event-owned-signal-race","--signal-race","--output",directory},
            new[]{"qualify","--signal-race","--event-owned-signal-race","--output",directory},
            new[]{"qualify","--event-owned-signal-race","--event-owned-signal-race","--output",directory},
            new[]{"qualify","--event-owned-signal-race","--output"}})
        {using var output=new StringWriter();HostCheck.Equal(1,await NativeAotWebPayload.RunAsync(Environment.CurrentDirectory,args,output));HostCheck.True(!Directory.Exists(directory));}
        var type=typeof(NativeAotWebPayload).GetNestedType("SignalRaceNativeSchedule",BindingFlags.NonPublic)!;
        HostCheck.True(type.GetConstructors(BindingFlags.Instance|BindingFlags.NonPublic).Any(c=>c.GetParameters().Length==3));
        HostCheck.True(type.GetConstructors(BindingFlags.Instance|BindingFlags.NonPublic).Any(c=>c.GetParameters().Length==4&&c.GetParameters()[3].ParameterType==typeof(bool)));
        if(!OperatingSystem.IsLinux())
        {
            using var output=new StringWriter();
            HostCheck.Equal(1,await NativeAotWebPayload.RunAsync(Environment.CurrentDirectory,["qualify","--event-owned-signal-race","--output",directory],output));
            HostCheck.True(output.ToString().Contains("linux_builder_required",StringComparison.Ordinal)&&!Directory.Exists(directory));
            using var process=Process.GetCurrentProcess();
            foreach(bool eventOwned in new[]{false,true})
            {
                Exception? failure=null;
                try{_=Activator.CreateInstance(type,BindingFlags.Instance|BindingFlags.NonPublic,null,[process,"unused",directory,eventOwned],null);}
                catch(TargetInvocationException error){failure=error.InnerException;}
                HostCheck.True(failure is InvalidDataException&&failure.Message.Contains("signalrace_schedule_platform",StringComparison.Ordinal)&&!Directory.Exists(directory));
            }
        }
    }
}
