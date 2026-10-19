using System.Runtime.CompilerServices;
using OS.Net;
using OS.Tooling;

internal static class NativeAotServiceTransportTests
{
    private const ulong Duration=240_000_000_000, Quiet=Duration+1;
    private static string[] Rows(bool reuse=false,bool sameClock=false,int initial=9,bool hideLastPeak=false)
    {
        List<string> rows=["M07_033_BOUND quietUntil=240000000001 clock=1 tickNanos=4000 quietNanos=240000000000 secret=independent lease=network"];
        List<(int Index,ulong Generation,ulong Deadline)> held=[];
        void Add(int number,int index,ulong born,int port)
        {
            ulong generation=(ulong)number+1,clock=born+(sameClock?0UL:1UL),deadline=clock+Duration;
            string Identity(ulong now)=>$"slot=0 owner=2097152 epoch=1 generation={generation} index=0 local=167772687 localPort=8080 remote=167772674 remotePort={port} clock={now}";
            rows.Add($"M16_011_SLOT event=0 {Identity(born)} deadline=0 duration=0");
            if(number==0)rows.Add($"M07_030_HIGHWATER bulk=1 compact=0 clock={born}");
            rows.Add($"M07_030_TRANSFER {Identity(clock)} compactIndex={index} compactGeneration={generation} originalDeadline={deadline} deadline={deadline} duration={Duration}");
            rows.Add($"M16_011_SLOT event=1 {Identity(clock)} deadline={deadline} duration={Duration}");
            rows.Add($"M07_030_RECORD event=0 index={index} generation={generation} originalDeadline={deadline} deadline={deadline} duration={Duration} clock={clock}");
            if(number<9 || held.Count==9 && !hideLastPeak)rows.Add($"M07_030_HIGHWATER bulk=1 compact={held.Count+1} clock={clock}");
            held.Add((index,generation,deadline));
        }
        void Retire((int Index,ulong Generation,ulong Deadline) value)=>
            rows.Add($"M07_030_RECORD event=1 index={value.Index} generation={value.Generation} deadline={value.Deadline} clock={value.Deadline}");
        for(int i=0;i<initial;i++)Add(i,i,Quiet+100+(ulong)i*10,35000+i);
        if(reuse)
        {
            Retire(held[0]);held.RemoveAt(0);
            Add(9,0,Quiet+100+1+Duration+1,35000);
        }
        ulong maximum=held.Max(value=>value.Deadline);
        foreach(var value in held.OrderBy(value=>value.Deadline))Retire(value);
        int count=initial+(reuse?1:0),peak=initial-(hideLastPeak?1:0);
        rows.Add($"M07_030_END transfers={count} retired={count} bulkHighWater=1 compactHighWater={peak} bytesPerRecord={Unsafe.SizeOf<TcpTimeWaitTable.Entry>()} clock={maximum+1} deadline={maximum} owner=retired");
        rows.Add($"M07_033_END issued={count} secret=zero seed=zero lease=released firmwareCallbacks=none");
        return rows.ToArray();
    }
    private static void Reject(string[] rows)
    {
        bool rejected=false;try{QemuTranscripts.InspectNativeAotServiceTransportForTests(rows);}catch(ToolFailure){rejected=true;}HostCheck.True(rejected);
    }

    [HostTest("service transport independently observes compact peaks and requires their high-water record before final retirement")]
    private static void CompactPeaks()
    {
        string[] valid=Rows(initial:10);
        HostCheck.Equal(10,QemuTranscripts.InspectNativeAotServiceTransportForTests(valid).CompactHighWater);
        Reject(Rows(initial:10,hideLastPeak:true));
        Reject(valid.Where(row=>!row.StartsWith("M07_030_HIGHWATER bulk=1 compact=10 ",StringComparison.Ordinal)).ToArray());
        Reject(valid.Select(row=>row.Replace("compactHighWater=10","compactHighWater=9",StringComparison.Ordinal)).ToArray());
    }

    [HostTest("service transport independently observes a bulk peak hidden between high-water snapshots")]
    private static void BulkPeaks()
    {
        var rows=Rows().ToList();int at=rows.FindIndex(row=>row.StartsWith("M07_030_HIGHWATER ",StringComparison.Ordinal));
        string identity=$"slot=1 owner=2097152 epoch=1 generation=1 index=1 local=167772687 localPort=8080 remote=167772674 remotePort=45000 clock={Quiet+100}";
        rows.InsertRange(at,[$"M16_011_SLOT event=0 {identity} deadline=0 duration=0",$"M16_011_SLOT event=1 {identity} deadline=0 duration=0"]);
        Reject(rows.Select(row=>row.Replace("issued=9 ","issued=10 ",StringComparison.Ordinal)).ToArray());
    }

    [HostTest("service transport rejects high-water snapshots and compact publication during an incomplete ownership transfer")]
    private static void TransferSnapshots()
    {
        var rows=Rows().ToList();int at=rows.FindIndex(row=>row.StartsWith("M07_030_TRANSFER ",StringComparison.Ordinal));
        var snapshot=rows.ToList();snapshot.Insert(at+1,$"M07_030_HIGHWATER bulk=1 compact=1 clock={Quiet+101}");Reject(snapshot.ToArray());
        (rows[at+1],rows[at+2])=(rows[at+2],rows[at+1]);Reject(rows.ToArray());
    }

    [HostTest("service transport retains more than8 compact lifetimes before expiry and reuses only expired tuples and generations")]
    private static void Complete()
    {
        foreach(var mode in new[]{(Reuse:false,Same:false),(Reuse:true,Same:false),(Reuse:false,Same:true)})
        {
            string[] rows=Rows(mode.Reuse,mode.Same),before=rows.ToArray();var result=QemuTranscripts.InspectNativeAotServiceTransportForTests(rows);
            HostCheck.Equal(mode.Reuse?10:9,result.Connections);HostCheck.Equal(result.Connections,result.Transfers);HostCheck.Equal(result.Transfers,result.Retired);
            HostCheck.Equal(1,result.BulkHighWater);HostCheck.Equal(9,result.CompactHighWater);HostCheck.True(result.MaximumDeadline>Quiet+Duration && rows.SequenceEqual(before));
        }
    }

    [HostTest("service transport binds a near-expiry FIN restart by its actual restart time even when observed after the old deadline")]
    private static void DelayedObservation()
    {
        var rows=Rows().ToList();ulong original=Quiet+101+Duration,restarted=original+Duration-1;
        int first=rows.FindIndex(row=>row.StartsWith("M07_030_RECORD event=1 index=0 ",StringComparison.Ordinal));rows.RemoveAt(first);
        rows.Insert(first,$"M07_030_RECORD event=2 index=0 generation=1 originalDeadline={original} deadline={restarted} duration={Duration} clock={original+1}");
        int end=rows.FindIndex(row=>row.StartsWith("M07_030_END ",StringComparison.Ordinal));
        rows.Insert(end,$"M07_030_RECORD event=1 index=0 generation=1 deadline={restarted} clock={restarted}");end++;
        rows[end]=$"M07_030_END transfers=9 retired=9 bulkHighWater=1 compactHighWater=9 bytesPerRecord={Unsafe.SizeOf<TcpTimeWaitTable.Entry>()} clock={restarted+1} deadline={restarted} owner=retired";
        HostCheck.Equal(restarted,QemuTranscripts.InspectNativeAotServiceTransportForTests(rows.ToArray()).MaximumDeadline);
        Reject(rows.Select(row=>row.Replace($"deadline={restarted}",$"deadline={restarted+1}",StringComparison.Ordinal)
            .Replace($"clock={restarted}",$"clock={restarted+1}",StringComparison.Ordinal).Replace($"clock={restarted+1} deadline=",$"clock={restarted+2} deadline=",StringComparison.Ordinal)).ToArray());
    }

    [HostTest("service transport rejects shortened unjoined early stale duplicate and missing lifetime records")]
    private static void LifetimeFailures()
    {
        string[] rows=Rows();
        foreach(string prefix in new[]{"M07_033_BOUND ","M07_030_TRANSFER ","M07_030_RECORD event=0 ","M07_030_RECORD event=1 ","M07_030_HIGHWATER ","M07_030_END ","M07_033_END "})
        {
            int at=Array.FindIndex(rows,row=>row.StartsWith(prefix,StringComparison.Ordinal));
            Reject(rows.Where((_,index)=>index!=at).ToArray());
            var duplicate=rows.ToList();duplicate.Insert(at,rows[at]);Reject(duplicate.ToArray());
        }
        foreach(var change in new[]{("duration=240000000000","duration=1"),("compactGeneration=1 ","compactGeneration=2 "),
            ("generation=1 originalDeadline=","generation=2 originalDeadline="),("remotePort=35001","remotePort=35000"),
            ("bulkHighWater=1","bulkHighWater=2"),("compactHighWater=9","compactHighWater=8"),
            ($"bytesPerRecord={Unsafe.SizeOf<TcpTimeWaitTable.Entry>()}","bytesPerRecord=1"),("issued=9","issued=8"),
            ("secret=zero","secret=retained"),("quietUntil=240000000001","quietUntil=240000000000")})
        {
            string[] changed=rows.Select(row=>row.Replace(change.Item1,change.Item2,StringComparison.Ordinal)).ToArray();
            HostCheck.True(!changed.SequenceEqual(rows));Reject(changed);
        }
        int retirement=Array.FindIndex(rows,row=>row.StartsWith("M07_030_RECORD event=1 ",StringComparison.Ordinal));
        var early=rows.ToArray();early[retirement]=early[retirement].Replace($"clock={Quiet+101+Duration}",$"clock={Quiet+100+Duration}",StringComparison.Ordinal);Reject(early);
        Reject(rows.Select(row=>row.Replace("compactIndex=1 compactGeneration=2 ","compactIndex=1 compactGeneration=1 ",StringComparison.Ordinal)
            .Replace("index=1 generation=2 ","index=1 generation=1 ",StringComparison.Ordinal)).ToArray());
    }
}
