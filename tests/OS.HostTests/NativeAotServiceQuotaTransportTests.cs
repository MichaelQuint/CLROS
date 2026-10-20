using OS.Tooling;

internal static class NativeAotServiceQuotaTransportTests
{
    private const ulong Quiet=240_000_000_001,Duration=240_000_000_000;
    private static string[] Rows(bool compact)
    {
        List<string> rows=["M07_033_BOUND quietUntil=240000000001 clock=1 tickNanos=4000 quietNanos=240000000000 secret=independent lease=network"];
        string Identity(int slot,ulong clock)=>$"slot={slot} owner=2097152 epoch=1 generation=1 index={slot} local=167772687 localPort=8080 remote=167772674 remotePort={35000+slot} clock={clock}";
        for(int i=0;i<4;i++)
        {
            rows.Add($"M16_011_SLOT event=0 {Identity(i,Quiet+(ulong)i)} deadline=0 duration=0");
            rows.Add($"M07_030_HIGHWATER bulk={i+1} compact=0 clock={Quiet+(ulong)i}");
        }
        if(compact)
        {
            ulong clock=Quiet+4,deadline=clock+Duration;
            rows.Add($"M07_030_TRANSFER {Identity(0,clock)} compactIndex=0 compactGeneration=1 originalDeadline={deadline} deadline={deadline} duration={Duration}");
            rows.Add($"M16_011_SLOT event=1 {Identity(0,clock)} deadline={deadline} duration={Duration}");
            rows.Add($"M07_030_RECORD event=0 index=0 generation=1 originalDeadline={deadline} deadline={deadline} duration={Duration} clock={clock}");
            rows.Add($"M07_030_HIGHWATER bulk=4 compact=1 clock={clock}");
        }
        rows.Add($"M16_016_QUOTA_SOCKETS bulkAborted={(compact?3:4)} compactDiscarded={(compact?1:0)} clock={Quiet+5} retained=0 transport=aborted timewait=discarded buffers=scrubbed descriptors=closed table=retired");
        rows.Add("M07_033_END issued=4 secret=zero seed=zero lease=released firmwareCallbacks=none");
        return rows.ToArray();
    }

    [HostTest("quota transport distinguishes live bulk abort and premature compact discard from real expiry")]
    private static void ExactOwners()
    {
        foreach(bool compact in new[]{false,true})
        {
            var result=QemuTranscripts.InspectNativeAotServiceQuotaTransportForTests(Rows(compact));
            HostCheck.Equal(compact?3UL:4UL,result.BulkAborted);HostCheck.Equal(compact?1UL:0UL,result.CompactDiscarded);
            HostCheck.Equal(4,result.History.Connections);HostCheck.Equal(0,result.History.Retired);
            if(compact)HostCheck.True(result.AbortClock<result.History.MaximumDeadline);
        }
    }

    [HostTest("quota transport rejects false counts peaks clocks generations missing erasure and post-abort mutations")]
    private static void Rejections()
    {
        string[] valid=Rows(true);
        foreach(string[] bad in new[]{
            valid.Select(r=>r.Replace("bulkAborted=3","bulkAborted=2",StringComparison.Ordinal)).ToArray(),
            valid.Select(r=>r.Replace("compactDiscarded=1","compactDiscarded=0",StringComparison.Ordinal)).ToArray(),
            valid.Select(r=>r.Replace($"clock={Quiet+5}",$"clock={Quiet+3}",StringComparison.Ordinal)).ToArray(),
            valid.Select(r=>r.Replace("compactGeneration=1","compactGeneration=2",StringComparison.Ordinal)).ToArray(),
            valid.Where(r=>!r.StartsWith("M07_030_HIGHWATER bulk=4 compact=1",StringComparison.Ordinal)).ToArray(),
            valid[..^1],valid.Append(valid[^2]).ToArray(),valid.Append(valid[1]).ToArray(),
            valid.Prepend("M07_030_END invalid").ToArray() }) Reject(bad);
        bool positiveRejected=false;
        try{_=QemuTranscripts.InspectNativeAotServiceTransportForTests(valid);}catch(ToolFailure){positiveRejected=true;}
        HostCheck.True(positiveRejected);
    }

    private static void Reject(string[] rows)
    {
        bool rejected=false;try{_=QemuTranscripts.InspectNativeAotServiceQuotaTransportForTests(rows);}catch(ToolFailure){rejected=true;}
        HostCheck.True(rejected);
    }
}
