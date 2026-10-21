using System.Globalization;
using System.Text;
using OS.Tooling;
using OS.Tools.QemuRunner;

internal static class NativeAotWebTransportTests
{
    internal const ulong Timeout=30_000_001_000;
    internal static string Fresh(int slot,int generation,int port,ulong clock)=>
        $"M16_011_SLOT event=0 slot={slot} owner=8192 epoch=1 generation={generation} index={slot} local=167772687 localPort=8080 remote=167772674 remotePort={port} clock={clock} deadline=0 duration=0";
    internal static string Retire(int slot,int generation,int port,ulong clock)=>Fresh(slot,generation,port,clock).Replace("event=0","event=1",StringComparison.Ordinal);
    internal static QemuTranscripts.WebControlInspection Report(bool quota=false)
    {
        int ordinary=quota?5:6;var first=new DateTimeOffset(2026,10,18,12,0,0,TimeSpan.Zero);
        WebSocketControls.Exchange Exchange(int port,int second,bool keepAlive)
        {
            var start=first.AddSeconds(second);
            string request="GET /healthz HTTP/1."+(keepAlive?"1":"0")+"\r\nHost: 127.0.0.1:22080\r\nConnection: "+(keepAlive?"keep-alive":"close")+"\r\nAccept: */*\r\n\r\n";
            string response="HTTP/1.1 200 OK\r\nContent-Length: 17\r\n"+(keepAlive?"":"Connection: close\r\n")+
                "Content-Type: text/plain\r\nDate: "+start.ToString("r",CultureInfo.InvariantCulture)+"\r\nServer: Kestrel\r\n\r\nOS_MINIMAL_API_OK";
            return new(port,start,start.AddMilliseconds(100),100,start,Convert.ToHexString(Encoding.ASCII.GetBytes(request)),Convert.ToHexString(Encoding.ASCII.GetBytes(response)));
        }
        string timeout=$"M07_027_TIMEOUT slot=0 owner=8192 epoch=1 generation=2 index=0 local=167772687 localPort=8080 remote=167772674 remotePort={41000+ordinary} clock={Timeout} sharedFailed=0";
        const string drop="M07_028_DROP before=0 after=1 availableBefore=65535 availableAfter=0 delivered=0 clock=1000 running=1";
        return new(new(Exchange(41000+ordinary,0,false),Exchange(41001+ordinary,1,true),Exchange(41001+ordinary,31,false),new(timeout,drop,Timeout,1000)),
            new(42000,42001,[],100,1,[],true,true,true));
    }
    private static List<string> Rows(bool quota=false)
    {
        int ordinary=quota?5:6;List<string> rows=[Fresh(0,1,30000,1),Retire(0,1,30000,2)];
        for(int i=1;i<ordinary;i++)rows.Add(Fresh(i,1,30000+i,(ulong)(i+2)));
        rows.AddRange([Fresh(0,2,41000+ordinary,100),Fresh(ordinary,1,41001+ordinary,101),Report(quota).Sockets.Observations.DropRow,
            Report(quota).Sockets.Observations.TimeoutRow,Retire(0,2,41000+ordinary,Timeout+1)]);return rows;
    }
    private static QemuTranscripts.WebSocketControlInspection Inspect(List<string> rows,QemuTranscripts.WebControlInspection? controls=null,bool quota=false,bool signalRace=false)=>
        QemuTranscripts.InspectNativeAotWebTransportForTests(rows.ToArray(),controls??Report(quota),quota,signalRace);
    private static void Reject(Action action){bool rejected=false;try{action();}catch(ToolFailure){rejected=true;}HostCheck.True(rejected);}

    [HostTest("web controls bind reused generation timeout private retirement and live healthy overlap")]
    private static void Positive()
    {
        foreach(bool quota in new[]{false,true})
        {
            var r=Inspect(Rows(quota),quota:quota);HostCheck.True(r.Connections==(quota?7:8) && r.Slot==0 && r.Index==0 &&
                r.Owner==8192 && r.Epoch==1 && r.Generation==2 && r.Created==100 && r.Dropped==1000 && r.TimedOut==Timeout && r.Retired==Timeout+1);
        }
    }
    [HostTest("web control transport requires exactly two ordered original events and a later retirement")]
    private static void Completion()
    {
        var rows=Rows();int drop=rows.Count-3,timeout=rows.Count-2;
        foreach(int index in new[]{drop,timeout,rows.Count-1}){var bad=rows.ToList();bad.RemoveAt(index);Reject(()=>Inspect(bad));}
        var twice=rows.ToList();twice.Insert(timeout,twice[drop]);Reject(()=>Inspect(twice));
        var order=rows.ToList();(order[drop],order[timeout])=(order[timeout],order[drop]);Reject(()=>Inspect(order));
        var early=rows.ToList();early.Insert(timeout,early[^1]);early.RemoveAt(early.Count-1);Reject(()=>Inspect(early));
        var detached=rows.ToList();detached.Insert(drop,Retire(6,1,41007,999));Reject(()=>Inspect(detached));
        Reject(()=>QemuTranscripts.InspectSocketTransportForTests(rows.ToArray(),continuingWeb:true));
    }
    [HostTest("web timeout full handle and tuple must match the current live slot even when report and raw agree")]
    private static void Identity()
    {
        foreach(var (from,to) in new[]{("slot=0","slot=1"),("owner=8192","owner=8200"),("epoch=1","epoch=2"),
            ("generation=2","generation=1"),("index=0","index=1"),("local=167772687","local=167772686"),
            ("localPort=8080","localPort=8081"),("remote=167772674","remote=167772675"),("remotePort=41006","remotePort=41005")})
        {
            var c=Report();string changed=c.Sockets.Observations.TimeoutRow.Replace(from,to,StringComparison.Ordinal);
            var rows=Rows();rows[^2]=changed;
            Reject(()=>Inspect(rows,c with{Sockets=c.Sockets with{Observations=c.Sockets.Observations with{TimeoutRow=changed}}}));
        }
        var stale=Rows();stale[7]=stale[7].Replace("generation=2","generation=3",StringComparison.Ordinal);Reject(()=>Inspect(stale));
        var duplicate=Rows();duplicate[6]=duplicate[6].Replace("remotePort=30005","remotePort=30004",StringComparison.Ordinal);Reject(()=>Inspect(duplicate));
    }
    [HostTest("web close timeout requires thirty seconds and isolated ordered live device observations")]
    private static void Clocks()
    {
        var c=Report();ulong early=30_000_000_099;
        string changed=c.Sockets.Observations.TimeoutRow.Replace(Timeout.ToString(CultureInfo.InvariantCulture),early.ToString(CultureInfo.InvariantCulture),StringComparison.Ordinal);
        var rows=Rows();rows[^2]=changed;
        Reject(()=>Inspect(rows,c with{Sockets=c.Sockets with{Observations=c.Sockets.Observations with{TimeoutRow=changed,TimeoutClock=early}}}));
        foreach(var mutation in new[]{("before=0","before=1"),("after=1","after=2"),("running=1","running=0"),("delivered=0","delivered=1")})
        {
            c=Report();changed=c.Sockets.Observations.DropRow.Replace(mutation.Item1,mutation.Item2,StringComparison.Ordinal);rows=Rows();rows[^3]=changed;
            Reject(()=>Inspect(rows,c with{Sockets=c.Sockets with{Observations=c.Sockets.Observations with{DropRow=changed}}}));
        }
        rows=Rows();string drop=rows[^3];rows.RemoveAt(rows.Count-3);rows.Insert(7,drop);Reject(()=>Inspect(rows));
    }
    [HostTest("web controls retain existing TIME-WAIT bounds without accepting it for the timed-out peer")]
    private static void TimeWait()
    {
        string Wait(int slot,int generation,int port)=>Fresh(slot,generation,port,500).Replace("M16_011_SLOT event=0","M16_011_TIMEWAIT",StringComparison.Ordinal)
            .Replace("deadline=0 duration=0","deadline=240000000500 duration=240000000000 drain=0",StringComparison.Ordinal);
        var rows=Rows();rows.Insert(rows.Count-3,Wait(1,1,30001));HostCheck.Equal(Timeout,Inspect(rows).TimedOut);
        var early=rows.ToList();early.Add(Retire(1,1,30001,Timeout+2).Replace("deadline=0 duration=0","deadline=240000000500 duration=240000000000",StringComparison.Ordinal));Reject(()=>Inspect(early));
        var bad=Rows();bad.Insert(bad.Count-3,Wait(0,2,41006));Reject(()=>Inspect(bad));
    }

    [HostTest("explicit web race transport retains exact ordinary control boundary before later generations")]
    private static void RaceLifetimes()
    {
        var rows=Rows();rows.AddRange([Fresh(7,1,42000,Timeout+2),Retire(7,1,42000,Timeout+3),Fresh(7,2,42001,Timeout+4)]);
        var report=Inspect(rows,signalRace:true);
        HostCheck.True(report.Connections==10&&report.Generation==2&&report.Retired==Timeout+1&&report.TimedOut==Timeout);
        Reject(()=>Inspect(rows));Reject(()=>Inspect(Rows(true),quota:true,signalRace:true));
        foreach(int at in new[]{7,Rows().Count-2})
        {
            var early=Rows();early.Insert(at,Fresh(7,1,42000,at==7?100UL:1001UL));
            Reject(()=>Inspect(early,signalRace:true));
        }
    }

    [HostTest("explicit web race transport bounds all lifetimes and retains duplicate stale and TIMEWAIT refusals")]
    private static void RaceLifetimeBounds()
    {
        var rows=Rows();
        for(int i=0;i<504;i++)rows.AddRange([Fresh(7,i+1,42000+i,Timeout+2+(ulong)i*2),Retire(7,i+1,42000+i,Timeout+3+(ulong)i*2)]);
        HostCheck.Equal(512,Inspect(rows,signalRace:true).Connections);
        rows.Add(Fresh(7,505,42504,Timeout+1010));Reject(()=>Inspect(rows,signalRace:true));
        foreach(string extra in new[]{Fresh(7,2,42000,Timeout+2),Fresh(7,1,30001,Timeout+2),Fresh(6,1,42000,Timeout+2)})
        {var bad=Rows();bad.Add(extra);Reject(()=>Inspect(bad,signalRace:true));}
        var wait=Rows();wait.Add(Fresh(7,1,42000,Timeout+2));
        wait.Add(Fresh(7,1,42000,Timeout+3).Replace("M16_011_SLOT event=0","M16_011_TIMEWAIT",StringComparison.Ordinal)
            .Replace("deadline=0 duration=0","deadline=270000001003 duration=240000000000 drain=0",StringComparison.Ordinal));
        wait.Add(Retire(7,1,42000,Timeout+4).Replace("deadline=0 duration=0","deadline=270000001003 duration=240000000000",StringComparison.Ordinal));
        Reject(()=>Inspect(wait,signalRace:true));
    }
}
