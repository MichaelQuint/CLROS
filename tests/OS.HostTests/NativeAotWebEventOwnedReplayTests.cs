using System.Globalization;
using OS.Tooling;

// Complete synthetic scalar replay only. No original native, process, PCAP or
// guest execution authority is established by these copied observations.
internal static class NativeAotWebEventOwnedReplayTests
{
    private static void Reject(Action action,string? expected=null)
    {
        string? reason=null;try{action();}catch(ToolFailure error){reason=error.Reason;}
        HostCheck.True(reason is not null);if(expected is not null)HostCheck.Equal(expected,reason);
    }
    private static void ChangeWait(List<string> rows,int which,string field,ulong value)
    {
        int at=Enumerable.Range(0,rows.Count).Where(i=>rows[i].StartsWith("M16_008_CALL ",StringComparison.Ordinal)&&
            rows[i].Contains(" nr=232 ",StringComparison.Ordinal)&&rows[i].Contains(" a0=9 ",StringComparison.Ordinal)).ElementAt(which);
        string[] tokens=rows[at].Split(' ');int token=Array.FindIndex(tokens,t=>t.StartsWith(field+"=",StringComparison.Ordinal));
        HostCheck.True(token>0);tokens[token]=field+"="+value.ToString(CultureInfo.InvariantCulture);rows[at]=string.Join(' ',tokens);
    }

    [HostTest("web event-owned scalar replay is explicit and preserves legacy report defaults")]
    private static void Profiles()
    {
        var legacy=NativeAotWebSignalRaceTests.Run();var current=NativeAotWebSignalRaceTests.Run(eventOwned:true);
        HostCheck.True(!legacy.EventOwned&&current.EventOwned&&legacy.Selection==current.Selection);
        HostCheck.True(!NativeAotWebSignalRaceTests.Run(eventWaits:true).EventOwned);
        Reject(()=>NativeAotWebSignalRaceTests.Run(eventOwned:true,eventWaits:false));
        HostCheck.True(current.Steps.Count==legacy.Steps.Count&&current.Actor.ReapRecordIndex is not null&&current.Waiter.ReapRecordIndex is null);
        var copy=current.Steps.ToArray();copy[0]=default;HostCheck.Equal("initial-wait",current.Steps[0].Kind);
        Reject(()=>NativeAotWebReplayTests.InspectProbe(_=>{},eventOwned:true),"web_race_event_selection");
    }

    [HostTest("web event-owned named initial and recovery waits require exact scalar arguments and owned zero deadline")]
    private static void WaitPredicates()
    {
        foreach(int which in new[]{0,1})
        foreach(var mutation in new (string Field,ulong Value)[]{("nr",281),("a2",2),("a3",0),("a3",1),("a3",0xffffffff),
            ("a3",ulong.MaxValue-1),("action",1),("status",1),("valueBits",1),("waitSource",0),("waitDeadline",1),("waitSequence",0)})
            Reject(()=>NativeAotWebSignalRaceTests.Run(eventOwned:true,mutate:rows=>ChangeWait(rows,which,mutation.Field,mutation.Value)));
    }

    [HostTest("web event-owned replay retains repark interrupt signal recovery and explicit cleanup requirements")]
    private static void CausalRefusals()
    {
        Reject(()=>NativeAotWebSignalRaceTests.Run(eventOwned:true,omitRepark:true));
        Reject(()=>NativeAotWebSignalRaceTests.Run(eventOwned:true,wrongRecovery:true));
        Reject(()=>NativeAotWebSignalRaceTests.Run(eventOwned:true,leaveOpen:true));
        Reject(()=>NativeAotWebSignalRaceTests.Run(eventOwned:true,repeatPipe:true));
        foreach(var change in new[]{("deadline=0","deadline=1"),("previous=1","previous=2"),("sequence=2","sequence=0")})
            Reject(()=>NativeAotWebSignalRaceTests.Run(eventOwned:true,mutate:rows=>
            {int at=rows.FindIndex(r=>r.StartsWith("M16_012_REPARK ",StringComparison.Ordinal));HostCheck.True(at>=0);rows[at]=rows[at].Replace(change.Item1,change.Item2,StringComparison.Ordinal);}));
    }
}
