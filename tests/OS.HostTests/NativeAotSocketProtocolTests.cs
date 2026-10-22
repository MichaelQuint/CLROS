using System.Text;
using OS.Tooling;

internal static class NativeAotSocketProtocolTests
{
    // Independent synthetic rows exercise the full leaf model. They never
    // construct a sealed program, packet capture or guest execution receipt.
    internal static NativeAotThreadingTranscriptTests.Vector Vector(bool asynchronous=false,bool repark=false,bool deadline=false,bool idle=false,
        bool expiry=false,bool removeWatch=false,bool closedWake=false,bool messages=false,bool reusedFile=false,
        Action<NativeAotThreadingTranscriptTests.Vector,int,uint>? vectorConnection=null,string? vectorOutput=null,
        Action<NativeAotThreadingTranscriptTests.Vector>? policyPrefix=null)
    {
        var v=new NativeAotThreadingTranscriptTests.Vector(false).Start();
        for(int i=0;i<v.Rows.Count;i++)v.Rows[i]=v.Rows[i].Replace("entropyBytes=32","entropyBytes=256",StringComparison.Ordinal)
            .Replace("packageBytes=1320176","packageBytes="+(vectorConnection is null?2117424:NativeAotSocketVectorsPackage.ElfBytes),StringComparison.Ordinal).Replace("workers=4","workers=16",StringComparison.Ordinal);
        v.Rows.Insert(3,"M16_011_BOUND connections=8 rx=8192 tx=32768 epoll=4 watches=64 nic=live");
        v.Call(291,5,[0x80000]);policyPrefix?.Invoke(v);v.Call(41,6,[2,0x80001,6]);
        v.Call(16,0,[6,0x5421,0x500200]);v.Call(72,0x802,[6,3]);v.Call(72,0,[6,4,0x8802]);
        v.Call(49,0,[6,0x500200,16]);v.Call(50,0,[6,4]);v.Call(233,0,[5,1,6,0x500300]);
        if(reusedFile){v.Call(2,7,[0x500200,0,0]);v.Call(3,0,[7]);}
        var poller=v.Clone(2,2);var timed=v.Clone(3,3);
        v.Irq(10,3,3);v.Select(poller);
        v.Call(281,0,[5,0x500600,16,deadline?1UL:uint.MaxValue,0,8],3,3,deadline?35UL:0);
        v.Drain(20);v.Supervisor(3,2,3);v.Select(timed);v.Wait(0x500200,35);v.Drain(30);v.Supervisor(3,1,3);
        v.Select(v.Main);
        if(idle)
        {
            v.Wait(0x500204,34);v.Drain(31);v.Supervisor(3,0,3);
            v.Rows.Add("M16_012_POLL identity=0 clock=31 ready=0 idle=0");
            v.Rows.Add("M16_012_POLL identity=0 clock=36 ready=2 idle=1");v.Resume(-110);
        }
        v.Clock=36;v.Rows.Add($"M16_012_POLL identity={v.Main.Id} clock=36 ready={(deadline?2:1)} idle=0");
        if(removeWatch)v.Call(233,0,[5,2,6,0]);
        v.Rows.Add($"M16_012_WAKE identity={poller.Id} sequence=1 completion={(deadline?2:1)} state=1 clock=36");
        for(int i=0;i<4;i++)
        {
            v.Call(288,7,[6,0x500700,0x500780,0x80000]);v.Call(72,0,[7,4,0x802]);
            v.Call(54,0,[7,1,7,0x500200,4]);v.Call(54,0,[7,6,1,0x500200,4]);
            int requested=i==1?1:20;
            uint generation=(uint)i+(reusedFile?2U:1U)+(policyPrefix is null?0U:2U);
            if(vectorConnection is not null)vectorConnection(v,i,generation);
            else
            {
            while(requested!=0){int count=Math.Min(3,requested);if(messages)MessageIo(v,47,count,(uint)count,generation);else v.Call(45,count,[7,0x500800,(uint)count,0,0,0]);requested-=count;}
            if(i!=0){if(messages)MessageIo(v,47,i==1?-104:0,1,generation);else v.Call(45,i==1?-104:0,[7,0x500800,1,0,0,0]);}
            int remaining=i==0?65536:i==1?0:18;
            while(remaining!=0){int count=Math.Min(8192,remaining);if(messages)MessageIo(v,46,count,(uint)count,generation);else v.Call(44,count,[7,0x500000,(uint)remaining,0,0,0]);remaining-=count;}
            if(i>=2)v.Call(48,0,[7,1]);
            }
            v.Call(3,0,[7]);
        }
        v.Irq(40,3,3);
        if(!deadline){v.Select(timed);v.Resume(-110);v.Call(60,0,[0],4);v.Drain(50);v.Exit(4,2,3);}
        v.Select(poller);v.Rows.Add($"M16_012_COMPLETE identity={poller.Id} sequence=1 completion={(deadline?2:1)} valueBits={(deadline?0:1)} clock={v.Clock}");v.Resume(deadline?0:1);
        if(deadline)
        {
            v.Call(60,0,[0],4);v.Drain(50);v.Exit(4,2,3);v.Select(timed);v.Resume(-110);v.Call(60,0,[0],4);v.Drain(60);v.Exit(4,1,2);
        }
        else
        {
            v.Call(281,0,[5,0x500600,16,uint.MaxValue,0,8],3,3);
            if(repark)v.Rows.Add($"M16_012_WAKE identity={poller.Id} sequence=2 completion=1 state=3 clock={v.Clock}");
            v.Drain(60);v.Supervisor(3,repark?2:1,2);
            if(repark)
            {
                v.Select(v.Main);v.Irq(70,2,2);v.Select(poller);
                v.Rows.Add($"M16_012_REPARK identity={poller.Id} previous=2 sequence=3 deadline=0 clock=70");v.Supervisor(3,1,2);
            }
        }
        v.Select(v.Main);v.Call(3,0,[6]);
        if(closedWake)v.Rows.Add($"M16_012_WAKE identity={poller.Id} sequence=2 completion=1 state=1 clock={v.Clock}");
        if(expiry)
        {
            v.Clock+=1_000_000;v.Call(281,0,[5,0x500600,16,1,0,8]);
            v.Rows.Add($"M16_012_EXPIRE identity={v.Main.Id} ordinal={v.Main.Ordinal} deadline={v.Clock} clock={v.Clock}");
        }
        string stdout=vectorOutput??NativeAotSocketPayloadTests.FixtureOutput(asynchronous);int length=Encoding.UTF8.GetByteCount(stdout);
        v.Rows.Add($"M16_005_WRITE fd=1 bytes={length} data={stdout}");v.Call(1,length,[1,0x500000,(uint)length]);
        v.Call(231,0,[0],2);v.Drain(v.Clock);v.Exit(2,deadline?0:1,deadline?1:2);
        if(!deadline){v.Select(poller);v.Exit(4,0,1);}
        for(int fd=0;fd<=5;fd++)v.Rows.Add($"M16_011_CLOSE fd={fd} valueBits=0");v.End(3);return v;
    }
    internal static void MessageIo(NativeAotThreadingTranscriptTests.Vector vector,ulong number,int returned,uint total,uint generation,uint? firstBytes=null,uint gap=0)
    {
        const ulong header=0x500100,table=0x500200;
        vector.Call(number,returned,[7,header,0]);int index=vector.Rows.FindLastIndex(row=>row.StartsWith("M16_008_CALL ",StringComparison.Ordinal));
        var fields=vector.Rows[index].Split(' ').Skip(1).Select(token=>token.Split('='))
            .ToDictionary(pair=>pair[0],pair=>ulong.Parse(pair[1],System.Globalization.CultureInfo.InvariantCulture));
        ulong identity=fields["identity"],ordinal=fields["ordinal"],address=number==46?0x500000UL:0x500800UL;
        uint first=firstBytes??total/2,second=total-first;
        bool single=firstBytes.HasValue && second==0;
        List<string> inputs=[
            $"M16_013_MESSAGE identity={identity} ordinal={ordinal} nr={number} fd=7 generation={generation} header={header} flags=0 table={table} vectors={(single?1:2)} total={total}",
            $"M16_013_VECTOR identity={identity} ordinal={ordinal} index=0 address={address} bytes={first}"];
        if(!single)inputs.Add($"M16_013_VECTOR identity={identity} ordinal={ordinal} index=1 address={address+first+gap} bytes={second}");
        vector.Rows.InsertRange(index,inputs);
    }

    [HostTest("m16-013 full synthetic socket replay joins message vectors mappings and descriptor generations")]
    private static void MessageReplay()
    {
        var vector=Vector(messages:true);var report=Inspect(vector.Rows);
        HostCheck.Equal(3,report.Reaped);HostCheck.True(NativeAotSocketMessages.Read(vector.Rows.ToArray()).Count>10);
        HostCheck.Equal(3,Inspect(Vector(messages:true,reusedFile:true).Rows).Reaped);
    }

    [HostTest("m16-013 full synthetic socket replay rejects missing snapshots unmapped vectors and stale generations")]
    private static void MessageReplayNegatives()
    {
        var rows=Vector(messages:true).Rows;
        Reject(rows.Where(row=>!row.StartsWith("M16_013_",StringComparison.Ordinal)),"socket_message_call_snapshot");
        Reject(rows.Select(row=>row.StartsWith("M16_013_VECTOR ",StringComparison.Ordinal)?
            System.Text.RegularExpressions.Regex.Replace(row,@"address=\d+","address=140733193388032"):row),"socket_message_vector_mapping");
        Reject(rows.Select(row=>row.StartsWith("M16_013_MESSAGE ",StringComparison.Ordinal)?
            System.Text.RegularExpressions.Regex.Replace(row,@"generation=\d+","generation=1"):row),"socket_message_descriptor_generation");
        Reject(rows.Select(row=>row.StartsWith("M16_013_MESSAGE ",StringComparison.Ordinal)?
            System.Text.RegularExpressions.Regex.Replace(row,@"generation=(\d+)",match=>"generation="+(uint.Parse(match.Groups[1].Value)+100)):row),"socket_message_descriptor_generation");
    }

    private static byte[] Raw(IEnumerable<string> rows)=>Encoding.UTF8.GetBytes(string.Join("\r\n",rows)+"\r\n");
    private static QemuTranscripts.ThreadingProtocolReport Inspect(IEnumerable<string> rows,bool asynchronous=false)=>
        QemuTranscripts.InspectNativeAotSocketProtocolForTests(asynchronous,NativeAotThreadingTranscriptTests.Elf(),Raw(rows),
            address=>address==0x400200 || address==0x400202,address=>address==0x400202,8192,12288);
    private static void Reject(IEnumerable<string> rows,string reason,bool asynchronous=false)
    {
        string? actual=null;try{Inspect(rows,asynchronous);}catch(ToolFailure error){actual=error.Message;}
        HostCheck.True(actual?.Contains(reason,StringComparison.Ordinal)==true,reason+" actual="+actual);
    }

    [HostTest("socket protocol binds original mode stdout finite IO fd reuse and complete off-stack cleanup")]
    private static void Complete()
    {
        foreach(bool asynchronous in new[]{false,true})
        {
            var report=Inspect(Vector(asynchronous).Rows,asynchronous);
            HostCheck.True(report.Clones==2 && report.Reaped==3 && report.Irqs==2 && report.Transfers==16);
        }
    }
    [HostTest("socket protocol bounds the entire async worker pool below four GiB")]
    private static void AsyncPoolExtent()
    {
        static string[] At(IEnumerable<string> rows, ulong pool) => rows.Select(row => row.Replace(
            "pool=2097152", FormattableString.Invariant($"pool={pool}"), StringComparison.Ordinal)).ToArray();
        // Both socket modes inherit the full signal/callback worker extent.
        foreach (bool asynchronous in new[] { false, true })
        {
            Reject(At(Vector(asynchronous).Rows, 0xfff40000), "threading_parent_geometry", asynchronous);
            HostCheck.Equal(3, Inspect(At(Vector(asynchronous).Rows, 0xfff1e000), asynchronous).Reaped);
            Reject(At(Vector(asynchronous).Rows, 0xfff1f000), "threading_parent_geometry", asynchronous);
        }
    }

    [HostTest("socket protocol retains real repark without fabricating a syscall timer or drain")]
    private static void Repark()
    {
        var vector=Vector(true,repark:true);var report=Inspect(vector.Rows,true);
        HostCheck.True(report.Reaped==3 && report.Irqs==3 && report.Transfers==20);
        foreach(uint state in new[]{1U,5U})
            Reject(vector.Rows.Select(row=>row.Replace("sequence=2 completion=1 state=3", "sequence=2 completion=1 state="+state,
                StringComparison.Ordinal)),"socket_wake_lifetime",true);
        var changed=vector.Rows.ToList();int at=changed.FindIndex(row=>row.StartsWith("M16_012_REPARK",StringComparison.Ordinal));
        changed.Insert(at+1,"M16_009_DRAIN identity=4294967298 epoch=7 nanos=70");Reject(changed,"record_shape",true);
    }
    [HostTest("socket protocol orders epoll and futex deadlines together and permits actual timeout zero")]
    private static void Deadlines()
    {
        var vector=Vector(true,deadline:true);HostCheck.Equal(3,Inspect(vector.Rows,true).Reaped);
        Reject(vector.Rows.Select(row=>row.Replace("clock=36 ready=2","clock=36 ready=1",StringComparison.Ordinal)),"socket_poll_ready",true);
    }
    [HostTest("socket protocol idle clock releases real deadlines before selecting the ready FIFO")]
    private static void Idle()
    {
        var vector=Vector(idle:true);HostCheck.Equal(18,Inspect(vector.Rows).Transfers);
        Reject(vector.Rows.Select(row=>row.Replace("identity=0 clock=36 ready=2 idle=1","identity=0 clock=33 ready=2 idle=1",StringComparison.Ordinal)),"socket_poll_ready");
        Reject(vector.Rows.Where(row=>row!="M16_012_POLL identity=0 clock=31 ready=0 idle=0"),"socket_idle_context");
    }
    [HostTest("socket protocol requires exact call-bound immediate positive timeout expiry")]
    private static void ImmediateExpiry()
    {
        var vector=Vector(true,expiry:true);HostCheck.Equal(3,Inspect(vector.Rows,true).Reaped);
        Reject(vector.Rows.Where(row=>!row.StartsWith("M16_012_EXPIRE",StringComparison.Ordinal)),"socket_immediate_expiry",true);
        Reject(vector.Rows.Select(row=>row.StartsWith("M16_012_EXPIRE",StringComparison.Ordinal)?
            row.Replace("deadline=1000060","deadline=1000059",StringComparison.Ordinal):row),"socket_immediate_expiry",true);
        Reject(vector.Rows.Select(row=>row.StartsWith("M16_012_EXPIRE",StringComparison.Ordinal)?
            row.Replace("identity=4294967297","identity=4294967298",StringComparison.Ordinal):row),"expiry_owner",true);
    }
    [HostTest("socket protocol rejects fabricated readiness stale generation wrong winner and resume counts")]
    private static void WaitControls()
    {
        var rows=Vector(true).Rows;
        Reject(Vector(true,removeWatch:true).Rows,"socket_wake_eligibility",true);
        Reject(Vector(true,closedWake:true).Rows,"socket_wake_eligibility",true);
        Reject(rows.Select(row=>row.Replace("completion=1 state=1 clock=36","completion=2 state=1 clock=36",StringComparison.Ordinal)),"wake_deadline",true);
        Reject(rows.Select(row=>row.Replace("M16_012_WAKE identity=4294967298","M16_012_WAKE identity=8589934594",StringComparison.Ordinal)),"wake_owner",true);
        Reject(rows.Select(row=>row.Replace("completion=1 state=1 clock=36","completion=1 state=5 clock=36",StringComparison.Ordinal)),"socket_wake_lifetime",true);
        Reject(rows.Select(row=>row.Replace("valueBits=1 clock=50","valueBits=17 clock=50",StringComparison.Ordinal)
            .Replace("M16_008_RESUME identity=4294967298 valueBits=1 ","M16_008_RESUME identity=4294967298 valueBits=17 ",StringComparison.Ordinal)),"socket_complete_result",true);
        Reject(rows.Select(row=>row.Replace("valueBits=1 clock=50","valueBits=2 clock=50",StringComparison.Ordinal)
            .Replace("M16_008_RESUME identity=4294967298 valueBits=1 ","M16_008_RESUME identity=4294967298 valueBits=2 ",StringComparison.Ordinal)),"socket_complete_result",true);
        Reject(rows.Select(row=>row.StartsWith("M16_008_CALL",StringComparison.Ordinal) && row.Contains(" nr=233 ",StringComparison.Ordinal)?
            row.Replace("a1=1 a2=6","a1=2 a2=6",StringComparison.Ordinal).Replace("valueBits=0 ","valueBits=18446744073709551614 ",StringComparison.Ordinal)
                .Replace("returnValueBits=0","returnValueBits=18446744073709551614",StringComparison.Ordinal):row),"socket_wake_eligibility",true);
    }
    [HostTest("socket protocol rejects reused fd redirection incomplete IO and omitted final descriptor closes")]
    private static void OwnershipControls()
    {
        var rows=Vector().Rows;
        Reject(rows.Select(row=>row.StartsWith("M16_008_CALL",StringComparison.Ordinal) && row.Contains(" nr=288 ",StringComparison.Ordinal)?
            row.Replace("valueBits=7","valueBits=8",StringComparison.Ordinal).Replace("returnValueBits=7","returnValueBits=8",StringComparison.Ordinal):row),"socket_new_descriptor");
        Reject(rows.Where(row=>!row.StartsWith("M16_008_CALL",StringComparison.Ordinal) || !row.Contains(" nr=48 ",StringComparison.Ordinal)),"linux_call_ordinal");
        Reject(rows.Where(row=>row!="M16_011_CLOSE fd=5 valueBits=0"),"record");
        Reject(rows.Select(row=>row=="M16_011_CLOSE fd=5 valueBits=0"?"M16_011_CLOSE fd=7 valueBits=0":row),"socket_final_close");
        Reject(rows.Select(row=>row.Replace("a2=34818","a2=100354",StringComparison.Ordinal)),"socket_set_statusflags");
    }
}
