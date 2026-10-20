using OS.Tooling;

internal static class NativeAotServiceProtocolTests
{
    private static NativeAotThreadingTranscriptTests.Vector Vector(bool configuredMemory = false, bool largeMapping = false)
    {
        var v=new NativeAotThreadingTranscriptTests.Vector(false).Start();
        v.Rows[1]=v.Rows[1].Replace("entropyBytes=32","entropyBytes=0",StringComparison.Ordinal);
        if(configuredMemory)v.Rows[1]=v.Rows[1].Replace("poolPages=8192","poolPages=9216",StringComparison.Ordinal);
        v.Rows[2]=v.Rows[2].Replace("packageBytes=1320176","packageBytes=8488712",StringComparison.Ordinal);
        v.Rows[4]=v.Rows[4].Replace("workers=4","workers=16",StringComparison.Ordinal);
        v.Rows[5]="M16_014_POLICY started=0 callsPerTurn=4096 progressNanos=30000000000 elapsedLimit=none counters=reject-overflow workers=individual cancellation=owned";
        v.Rows.Insert(5,"TEST_START m16.014");
        v.Rows.Insert(3,"M16_011_BOUND connections=8 rx=8192 tx=32768 epoll=4 watches=64 nic=live");
        v.Rows.Insert(v.Rows.FindIndex(row=>row.StartsWith("M16_014_POLICY ",StringComparison.Ordinal))+1,
            "M16_012_POLL identity=0 clock=0 ready=1 idle=0");
        if(largeMapping)
        {
            v.Call(9,0x140000000,[0,33*1048576,3,0x22,uint.MaxValue,0]);
            v.Call(11,0,[0x140000000,33*1048576]);
        }
        for(int turn=1;turn<=16;turn++)
        {
            for(int call=0;call<500;call++)v.Call(39,1,[]);
            v.Irq((ulong)turn*20_000_000_000,1,1);
        }
        v.Rows.Add($"M16_014_PROGRESS identity={v.Main.Id} calls={v.Main.Ordinal} started=0 nanos={v.Clock} epochs={v.Epoch} active=1 owner=live cleanup=pending");
        v.Rows.Add("CLROS_M16_014_READY");
        var first=v.Clone(2,2);var second=v.Clone(3,3);v.Irq(v.Clock+1_000_000,3,3);
        v.Select(first);v.Wait(0x500100);v.Drain(v.Clock+1_000_000);v.Supervisor(3,2,3);
        v.Select(second);v.Wait(0x500104);v.Drain(v.Clock+1_000_000);v.Supervisor(3,1,3);
        v.Select(v.Main);v.Call(41,5,[2,1,6]);v.Call(49,0,[5,0x500800,16]);v.Call(50,0,[5,512]);
        for(int i=0;i<10;i++)
        {
            v.Call(43,6,[5,0,0]);v.Call(45,1,[6,0x500800,1,0,0,0]);v.Call(44,1,[6,0x500800,1,0,0,0]);v.Call(3,0,[6]);
        }
        v.Call(3,0,[5]);v.Output();v.Call(231,0,[0],2);v.Drain(v.Clock);v.Exit(2,2,3);
        v.Select(first);v.Exit(4,1,2);v.Select(second);v.Exit(4,0,1);
        foreach(int fd in Enumerable.Range(0,5))v.Rows.Add($"M16_011_CLOSE fd={fd} valueBits=0");
        v.End(3);v.Rows.InsertRange(v.Rows.Count-3,[
            $"M16_014_END identity={v.Main.Id} calls={v.Main.Ordinal} started=0 nanos={v.Clock} reaped=3 process=drained mappings=restored pool=scrubbed random=zero reservation=retained",
            "TEST_PASS m16.014"]);
        return v;
    }
    private static QemuTranscripts.ThreadingProtocolReport Inspect(NativeAotThreadingTranscriptTests.Vector v,string[]? rows=null,bool configuredMemory=false) =>
        QemuTranscripts.InspectNativeAotServiceProtocolForTests(NativeAotThreadingTranscriptTests.Elf(),rows??v.Rows.ToArray(),
            v.Lengths,address=>address==0x400202,address=>address==0x400202,8192,12288,31,15,v.Main.Id,1,configuredMemory);
    private static void Reject(NativeAotThreadingTranscriptTests.Vector v,string[] rows)
    {
        bool rejected=false;try{Inspect(v,rows);}catch(ToolFailure){rejected=true;}HostCheck.True(rejected);
    }

    [HostTest("service complete scheduler replay retains one generation past4096 calls and300 seconds then genuinely reaps all workers")]
    private static void Complete()
    {
        var v=Vector();string[] before=v.Rows.ToArray();var result=Inspect(v);
        HostCheck.True(result.Calls>8000 && result.Irqs==17 && result.Clones==2 && result.Reaped==3 && result.Drains==3);
        HostCheck.True(v.Rows.SequenceEqual(before));
    }

    [HostTest("configured service replay requires its exact larger policy while legacy memory keeps its original quota")]
    private static void ConfiguredMemory()
    {
        var configured=Vector(configuredMemory:true,largeMapping:true);
        var result=Inspect(configured,configuredMemory:true);HostCheck.True(result.Calls>8000&&result.Reaped==3);
        Reject(configured,configured.Rows.ToArray());
        var legacy=Vector(largeMapping:true);Reject(legacy,legacy.Rows.ToArray());
        bool rejected=false;try{Inspect(Vector(),configuredMemory:true);}catch(ToolFailure){rejected=true;}HostCheck.True(rejected);
    }

    [HostTest("service full replay rejects fabricated progress cancellation cleanup and missing off-stack reap")]
    private static void OwnershipFailures()
    {
        var v=Vector();
        const string bootstrap="M16_012_POLL identity=0 clock=0 ready=1 idle=0";
        Reject(v,v.Rows.Where(row=>row!=bootstrap).ToArray());
        foreach(var change in new[] { ("active=1 owner=live", "active=2 owner=live"), ("callsPerTurn=4096", "callsPerTurn=4097"),
            ("random=zero reservation=retained", "random=retained reservation=retained"), ("reaped=3 process=drained", "reaped=2 process=drained"),
            ("context=zero stack=zero slot=zero", "context=zero stack=retained slot=zero") })
        {
            string[] changed=v.Rows.Select(row=>row.Replace(change.Item1,change.Item2,StringComparison.Ordinal)).ToArray();
            HostCheck.True(!changed.SequenceEqual(v.Rows));Reject(v,changed);
        }
        Reject(v,v.Rows.Where(row=>!row.StartsWith("M16_014_PROGRESS ",StringComparison.Ordinal) && row!="CLROS_M16_014_READY").ToArray());
        Reject(v,v.Rows.Where(row=>!row.StartsWith("M16_008_REAP ",StringComparison.Ordinal)).ToArray());
    }

    [HostTest("service replay requires hardware quota renewal exact cumulative peaks and the full elapsed witness")]
    private static void CountersAndTime()
    {
        var v=Vector();
        int progress=v.Rows.FindIndex(row=>row.StartsWith("M16_014_PROGRESS ",StringComparison.Ordinal));
        string peak=v.Rows[progress];int start=peak.IndexOf(" calls=",StringComparison.Ordinal)+7,end=peak.IndexOf(' ',start);
        foreach(string changed in new[] { peak[..start]+"4096"+peak[end..],
            v.Rows[progress].Replace("nanos=320000000000","nanos=300000000000",StringComparison.Ordinal),
            v.Rows[progress].Replace($"identity={v.Main.Id}",$"identity={v.Main.Id+(1UL<<32)}",StringComparison.Ordinal) })
        {
            var rows=v.Rows.ToArray();rows[progress]=changed;Reject(v,rows);
        }
        Reject(v,v.Rows.Where(row=>!row.StartsWith("M16_009_IRQ ",StringComparison.Ordinal) &&
            !row.StartsWith("M16_008_SUPERVISOR ",StringComparison.Ordinal)).ToArray());
    }
}
