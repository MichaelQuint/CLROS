using System.Text;
using OS.Tooling;

internal static class NativeAotWebReplayTests
{
    // Independent synthetic observations over the existing ELF/thread vector;
    // caller predicates do not grant prepared-program or guest authority.
    private static List<string> Vector(bool groupExit=false,bool reopenSettings=false,bool earlySettings=false,
        long? readAfterShutdown=null,long? sendAfterShutdown=null,bool invalidDescriptor=false,ulong? failedWrite=null)
    {
        var v=new NativeAotThreadingTranscriptTests.Vector(false).Start();
        for(int i=0;i<v.Rows.Count;i++)v.Rows[i]=v.Rows[i].Replace("entropyBytes=32","entropyBytes=4096",StringComparison.Ordinal)
            .Replace("packageBytes=1320176","packageBytes=8746056",StringComparison.Ordinal).Replace("workers=4","workers=16",StringComparison.Ordinal)
            .Replace("epochs=8192 steps=16384","epochs=1000000 steps=4000000",StringComparison.Ordinal);
        v.Rows.Insert(3,"M16_011_BOUND connections=8 rx=8192 tx=32768 epoll=4 watches=64 nic=live");
        v.Call(2,5,[0x500200,0x88100,0]);v.Call(5,0,[5,0x500800]);
        if(earlySettings)v.Call(17,51,[5,0x500800,1024,0]);
        v.Rows.Add("M10_004_CONFIG fd=5 bytes=51 source=immutable-package copied=user");
        v.Call(17,51,[5,0x500800,1024,0]);v.Call(17,0,[5,0x500800,1024,51]);
        v.Call(4,0,[0x500200,0x500800]);v.Call(6,-2,[0x500200,0x500800]);
        v.Call(79,2,[0x500800,256]);v.Call(89,25,[0x500200,0x500800,4096]);
        v.Call(73,-38,[5,5]);v.Call(221,-38,[5,0,0,2]);v.Call(3,0,[5]);
        if(reopenSettings){v.Call(2,5,[0x500200,0x88100,0]);v.Call(17,51,[5,0x500800,1024,0]);v.Call(3,0,[5]);}
        v.Call(291,5,[0x80000]);v.Call(41,6,[2,0x80001,6]);
        v.Call(49,0,[6,0x500200,16]);v.Call(50,0,[6,512]);v.Call(51,0,[6,0x500700,0x500780]);
        var worker=v.Clone(2,2);v.Irq(10,2,2);v.Select(worker);
        v.Wait(0x500200,100);v.Drain(20);v.Supervisor(3,1,2);v.Select(v.Main);
        for(int i=0;i<2;i++)
        {
            v.Call(288,7,[6,0x500700,0x500780,0x80000]);v.Call(72,0,[7,4,0x802]);
            v.Call(45,1,[7,0x500800,1,2,0,0]);v.Call(45,20,[7,0x500800,20,0,0,0]);
            v.Call(44,18,[7,0x500800,18,0,0,0]);v.Call(48,0,[7,2]);
            if(readAfterShutdown is long read)v.Call(45,read,[7,0x500800,1,0,0,0]);
            if(sendAfterShutdown is long send)v.Call(44,send,[7,0x500800,1,0,0,0]);
            v.Call(3,0,[7]);
        }
        for(int i=0;i<20;i++)v.Call(318,16,[0x500800,16,0]);
        v.Rows.Add("M16_005_WRITE fd=1 bytes=5 data=hello");v.Call(1,5,[1,0x500800,5]);
        if(invalidDescriptor)v.Call(20,-9,[1234,0x500800,1]);
        if(failedWrite is ulong failed)v.Call(failed,-5,[1,0x500800,failed==1?5UL:2UL]);
        if(groupExit)v.Call(231,0,[0],2);
        return v.Rows;
    }
    private static QemuTranscripts.WebReplayReport Inspect(List<string> rows,string fragment="M16_008_CALL identity=4294967297 nr=202")=>
        QemuTranscripts.InspectNativeAotWebReplayForTests(NativeAotThreadingTranscriptTests.Elf(),
            Encoding.UTF8.GetBytes(string.Join("\r\n",rows)+"\r\n"+fragment),_=>true,_=>true,8192,12288,51,5);
    private static void Reject(Action action)
    { bool failed=false;try{action();}catch(ToolFailure){failed=true;}HostCheck.True(failed); }
    private static List<string> CallChange(int number,Func<string,string> change)
    {
        var rows=Vector();int index=rows.FindIndex(r=>r.StartsWith("M16_008_CALL ",StringComparison.Ordinal) && r.Contains(" nr="+number+" ",StringComparison.Ordinal));
        HostCheck.True(index>=0);rows[index]=change(rows[index]);return rows;
    }

    [HostTest("nativeaot.web replay retains live FIFO owner and counts peeks without consuming stream bytes")]
    private static void LiveOwner()
    {
        var report=Inspect(Vector());
        HostCheck.True(report.Active==2 && report.Ready==0 && report.Current==4294967297 && report.Protocol.Clones==1 &&
            report.Protocol.Reaped==0 && report.Protocol.Irqs==1 && report.Protocol.Drains==1 && report.Protocol.Transfers==4);
        HostCheck.True(report.SocketAccepts==2 && report.Sent==36 && report.Received==40 && report.EntropyRemaining==3760 &&
            report.Observations.StdoutBytes==5 && report.Observations.Calls==report.Protocol.Calls);
    }

    [HostTest("nativeaot.web replay rejects memory file ownership and positioned-read inconsistencies")]
    private static void Files()
    {
        Reject(()=>Inspect(CallChange(5,r=>r.Replace(" a1=5244928 "," a1=1 ",StringComparison.Ordinal))));
        Reject(()=>Inspect(CallChange(17,r=>r.Replace(" a0=5 "," a0=6 ",StringComparison.Ordinal))));
        Reject(()=>Inspect(CallChange(17,r=>r.Replace("valueBits=51 ","valueBits=50 ",StringComparison.Ordinal)
            .Replace("returnValueBits=51","returnValueBits=50",StringComparison.Ordinal))));
        Reject(()=>Inspect(CallChange(79,r=>r.Replace(" a1=256 "," a1=1 ",StringComparison.Ordinal))));
        Reject(()=>Inspect(CallChange(89,r=>r.Replace(" a2=4096 "," a2=1 ",StringComparison.Ordinal))));
        Reject(()=>Inspect(Vector(reopenSettings:true)));
        Reject(()=>Inspect(Vector(earlySettings:true)));
    }

    [HostTest("nativeaot.web replay rejects changed budget FIFO generation and capture-owner observations")]
    private static void Ownership()
    {
        var stale=Vector();int policy=stale.FindIndex(r=>r.StartsWith("M16_007_POLICY ",StringComparison.Ordinal));
        stale[policy]=stale[policy].Replace("packageBytes=8746056","packageBytes=8681864",StringComparison.Ordinal);Reject(()=>Inspect(stale));
        var budget=Vector();int at=budget.FindIndex(r=>r.StartsWith("M16_008_BUDGET ",StringComparison.Ordinal));
        budget[at]=budget[at].Replace("epochs=1000000","epochs=8192",StringComparison.Ordinal);Reject(()=>Inspect(budget));
        var fifo=Vector();at=fifo.FindIndex(r=>r.StartsWith("M16_008_SUPERVISOR ",StringComparison.Ordinal));
        fifo[at]=fifo[at].Replace("ready=2","ready=1",StringComparison.Ordinal);Reject(()=>Inspect(fifo));
        Reject(()=>Inspect(Vector(),"M16_008_CALL identity=4294967298 nr=202"));
        Reject(()=>Inspect(Vector(),"M16_009_IRQ identity=4294967298 epoch=2"));
        Reject(()=>Inspect(Vector(),"M16_012_POLL identity=4294967298 clock=20"));
        Reject(()=>Inspect(Vector(),"M16_012_COMPLETE identity=4294967297 sequence=1"));
        Reject(()=>Inspect(Vector(),"M16_008_SUPERVISOR identity=4294967297 action=1"));
        Reject(()=>Inspect(Vector(groupExit:true)));
    }

    [HostTest("nativeaot.web replay retains an IRQ return before its supervisor observation")]
    private static void IrqReturn()
    {
        var rows=Vector();
        string irq=rows.First(r=>r.StartsWith("M16_009_IRQ ",StringComparison.Ordinal))
            .Replace("epoch=1 nanos=10","epoch=3 nanos=30",StringComparison.Ordinal);
        rows.Add(irq);
        var report=Inspect(rows,"M16_008_SUPERVISOR identity=4294967297 action=1");
        HostCheck.True(report.ReturningToSupervisor && report.Ready==1 && report.Current==4294967297 &&
            report.Protocol.Irqs==2 && report.Protocol.Transfers==4 && report.Boundary=="M16_008_SUPERVISOR");
        Reject(()=>Inspect(rows,"M16_008_CALL identity=4294967297 nr=202"));
        Reject(()=>Inspect(rows,"M16_008_SUPERVISOR identity=4294967298 action=1"));
    }

    [HostTest("nativeaot.web replay rejects socket request and shutdown inconsistencies")]
    private static void Sockets()
    {
        Reject(()=>Inspect(CallChange(50,r=>r.Replace(" a1=512 "," a1=4 ",StringComparison.Ordinal))));
        Reject(()=>Inspect(CallChange(45,r=>r.Replace(" a3=2 "," a3=4 ",StringComparison.Ordinal))));
        Reject(()=>Inspect(CallChange(48,r=>r.Replace(" a1=2 "," a1=3 ",StringComparison.Ordinal))));
        Reject(()=>Inspect(CallChange(51,r=>r.Replace(" a1=5244672 "," a1=1 ",StringComparison.Ordinal))));
    }

    [HostTest("nativeaot.web replay rejects impossible scalar writes unread peeks and shutdown results")]
    private static void IoResults()
    {
        Reject(()=>Inspect(CallChange(1,r=>r.Replace(" a2=5 "," a2=6 ",StringComparison.Ordinal))));
        var eof=Vector();int at=eof.FindIndex(r=>r.StartsWith("M16_008_CALL ",StringComparison.Ordinal) &&
            r.Contains(" nr=45 ",StringComparison.Ordinal) && r.Contains("valueBits=20 ",StringComparison.Ordinal));
        HostCheck.True(at>=0);eof[at]=eof[at].Replace("valueBits=20 ","valueBits=0 ",StringComparison.Ordinal)
            .Replace("returnValueBits=20","returnValueBits=0",StringComparison.Ordinal);Reject(()=>Inspect(eof));
        Reject(()=>Inspect(Vector(readAfterShutdown:-11)));
        Reject(()=>Inspect(Vector(readAfterShutdown:-32)));
        Reject(()=>Inspect(Vector(sendAfterShutdown:0)));
        Reject(()=>Inspect(Vector(sendAfterShutdown:-11)));
        Reject(()=>Inspect(Vector(readAfterShutdown:-104,sendAfterShutdown:-104)));
        HostCheck.True(Inspect(Vector(readAfterShutdown:0,sendAfterShutdown:-32)).SocketAccepts==2);
        HostCheck.True(Inspect(Vector(readAfterShutdown:-104,sendAfterShutdown:-32)).SocketAccepts==2);
    }

    [HostTest("nativeaot.web replay joins vector output table access channel and invalid descriptor results")]
    private static void VectorOutput()
    {
        var rows=Vector(invalidDescriptor:true);
        int at=rows.FindIndex(r=>r.StartsWith("M16_008_CALL ",StringComparison.Ordinal) && r.Contains(" nr=1 ",StringComparison.Ordinal));
        HostCheck.True(at>0 && rows[at-1]=="M16_005_WRITE fd=1 bytes=5 data=hello");
        rows[at-1]="M16_005_WRITE fd=2 bytes=5 data=hello";
        rows[at]=rows[at].Replace(" nr=1 a0=1 "," nr=20 a0=2 ",StringComparison.Ordinal).Replace(" a2=5 "," a2=2 ",StringComparison.Ordinal);
        var report=Inspect(rows);HostCheck.True(report.Observations.StdoutBytes==0 && report.Observations.StderrBytes==5 && report.Observations.Writes==1);
        foreach(var mutation in new[]{(" a1=5244928 "," a1=1 "),(" a2=2 "," a2=17 "),(" a0=2 "," a0=3 ")})
        {var changed=rows.ToList();changed[at]=changed[at].Replace(mutation.Item1,mutation.Item2,StringComparison.Ordinal);Reject(()=>Inspect(changed));}
        var invalid=rows.ToList();int bad=invalid.FindIndex(r=>r.StartsWith("M16_008_CALL ",StringComparison.Ordinal) && r.Contains(" a0=1234 ",StringComparison.Ordinal));
        HostCheck.True(bad>=0);invalid[bad]=invalid[bad].Replace("valueBits=18446744073709551607 ","valueBits=18446744073709551611 ",StringComparison.Ordinal)
            .Replace("returnValueBits=18446744073709551607","returnValueBits=18446744073709551611",StringComparison.Ordinal);Reject(()=>Inspect(invalid));
        Reject(()=>Inspect(Vector(failedWrite:1)));Reject(()=>Inspect(Vector(failedWrite:20)));
    }
}
