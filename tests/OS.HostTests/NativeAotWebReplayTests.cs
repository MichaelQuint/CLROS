using System.Text;
using System.Globalization;
using OS.Tooling;

internal static class NativeAotWebReplayTests
{
    private const int WebElfBytes=8_766_792;
    // Independent synthetic observations over the existing ELF/thread vector;
    // caller predicates do not grant prepared-program or guest authority.
    private static List<string> Vector(bool groupExit=false,bool reopenSettings=false,bool earlySettings=false,
        long? readAfterShutdown=null,long? sendAfterShutdown=null,bool invalidDescriptor=false,ulong? failedWrite=null,int? zeroSendMessage=null,bool zeroAfterShutdown=false,
        bool controls=false,bool quota=false,bool badOpen=false,bool healthyClosed=false,bool retireAfterRequest=false,
        ulong? clockId=null,long clockResult=0,ulong clockAddress=0x500800,
        Action<NativeAotThreadingTranscriptTests.Vector>? probe=null)
    {
        var v=new NativeAotThreadingTranscriptTests.Vector(false).Start();
        for(int i=0;i<v.Rows.Count;i++)v.Rows[i]=v.Rows[i].Replace("entropyBytes=32","entropyBytes=4096",StringComparison.Ordinal)
            .Replace("packageBytes=1320176","packageBytes="+WebElfBytes.ToString(CultureInfo.InvariantCulture),StringComparison.Ordinal).Replace("workers=4","workers=16",StringComparison.Ordinal)
            .Replace("epochs=8192 steps=16384","epochs=1000000 steps=4000000",StringComparison.Ordinal);
        v.Rows.Insert(3,"M16_011_BOUND connections=8 rx=8192 tx=32768 epoll=4 watches=64 nic=live");
        if(clockId is ulong id)v.Call(228,clockResult,[id,clockAddress]);
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
        int ordinary=quota?5:6;var expected=controls?NativeAotWebTransportTests.Report(quota):null;
        for(int i=0;i<(controls?ordinary+2:2);i++)
        {
            if(controls)v.Rows.Add(i<ordinary?NativeAotWebTransportTests.Fresh(i,1,30000+i,(ulong)(i+1)):
                NativeAotWebTransportTests.Fresh(i==ordinary?0:ordinary,i==ordinary?2:1,41000+i,(ulong)(i==ordinary?100:101)));
            int fd=controls && badOpen && i==ordinary+1?8:7;
            v.Call(288,fd,[6,0x500700,0x500780,0x80000]);v.Call(72,0,[(ulong)fd,4,0x802]);
            if(controls && i>=ordinary)
            {
                var exchange=i==ordinary?expected!.Sockets.Deadline:expected!.Sockets.Before;
                void Io(OS.Tools.QemuRunner.WebSocketControls.Exchange request)
                {
                    int received=request.RequestHex.Length/2,sent=request.ResponseHex.Length/2;
                    v.Call(45,received,[(ulong)fd,0x500800,(ulong)received,0,0,0]);v.Call(44,sent,[(ulong)fd,0x500800,(ulong)sent,0,0,0]);
                }
                Io(exchange);
                if(i==ordinary){if(!badOpen)v.Call(3,0,[(ulong)fd]);continue;}
                if(healthyClosed)v.Call(3,0,[(ulong)fd]);
                v.Rows.Add(expected.Sockets.Observations.DropRow);v.Rows.Add(expected.Sockets.Observations.TimeoutRow);
                string retired=NativeAotWebTransportTests.Retire(0,2,41000+ordinary,NativeAotWebTransportTests.Timeout+1);
                if(!retireAfterRequest)v.Rows.Add(retired);
                Io(expected.Sockets.After);
                if(retireAfterRequest)v.Rows.Add(retired);
                v.Call(48,0,[(ulong)fd,2]);v.Call(3,0,[(ulong)fd]);continue;
            }
            v.Call(45,1,[7,0x500800,1,2,0,0]);v.Call(45,20,[7,0x500800,20,0,0,0]);
            v.Call(44,18,[7,0x500800,18,0,0,0]);
            if(zeroSendMessage is int zero && !zeroAfterShutdown)NativeAotSocketProtocolTests.MessageIo(v,46,zero,0,(uint)i+1);
            v.Call(48,0,[7,2]);
            if(zeroSendMessage is int after && zeroAfterShutdown)NativeAotSocketProtocolTests.MessageIo(v,46,after,0,(uint)i+1);
            if(readAfterShutdown is long read)v.Call(45,read,[7,0x500800,1,0,0,0]);
            if(sendAfterShutdown is long send)v.Call(44,send,[7,0x500800,1,0,0,0]);
            v.Call(3,0,[7]);
            if(controls && i==0)v.Rows.Add(NativeAotWebTransportTests.Retire(0,1,30000,2));
        }
        for(int i=0;i<20;i++)v.Call(318,16,[0x500800,16,0]);
        v.Rows.Add("M16_005_WRITE fd=1 bytes=5 data=hello");v.Call(1,5,[1,0x500800,5]);
        if(invalidDescriptor)v.Call(20,-9,[1234,0x500800,1]);
        if(failedWrite is ulong failed)v.Call(failed,-5,[1,0x500800,failed==1?5UL:2UL]);
        probe?.Invoke(v);
        if(groupExit)v.Call(231,0,[0],2);
        return v.Rows;
    }
    private static QemuTranscripts.WebReplayReport Inspect(List<string> rows,string fragment="M16_008_CALL identity=4294967297 nr=202",
        QemuTranscripts.WebControlInspection? controls=null,bool quota=false)=>
        QemuTranscripts.InspectNativeAotWebReplayForTests(NativeAotThreadingTranscriptTests.Elf(),
            Encoding.UTF8.GetBytes(string.Join("\r\n",rows)+"\r\n"+fragment),_=>true,_=>true,8192,12288,51,5,controls,quota);
    private static void Reject(Action action)
    { bool failed=false;try{action();}catch(ToolFailure){failed=true;}HostCheck.True(failed); }

    private static void RejectClock(Action action)
    { string? reason=null;try{action();}catch(ToolFailure error){reason=error.Reason;}HostCheck.Equal("threading_monotonic_clock",reason); }

    [HostTest("web alternate stack query checks complete writable span and permits null without claiming contents")]
    private static void AlternateStackAccess()
    {
        const ulong page=0x100500000;
        void Check(ulong pointer,long result,ulong protection=3)=>HostCheck.Equal(2,Inspect(Vector(probe:v=>
        {
            v.Call(9,(long)page,[0,4096,protection,0x22,ulong.MaxValue,0]);
            v.Call(131,result,[0,pointer]);
        })).SocketAccepts);
        Check(0,0);Check(0x500800,0);Check(page+4096-24,0);
        foreach(ulong pointer in new[]{1UL,4095UL,page+4096-23,page+4096,ulong.MaxValue-11})Check(pointer,-14);
        Check(page,-14,1);Check(page,-14,0);
        HostCheck.Equal(2,Inspect(Vector(probe:v=>
        {
            v.Call(9,(long)page,[0,8192,3,0x22,ulong.MaxValue,0]);
            v.Call(131,0,[0,page+4096-12]);
            v.Call(10,0,[page+4096,4096,1]);
            v.Call(131,-14,[0,page+4096-12]);
        })).SocketAccepts);
    }

    [HostTest("web alternate stack query rejects mutation fabricated results and nonweb admission")]
    private static void AlternateStackRefusals()
    {
        foreach(var call in new (long Result,ulong Input,ulong Output)[]{(0,0,1),(-14,0,0),(-14,0,0x500800),(0,0x500800,0),(-14,1,0)})
            Reject(()=>Inspect(Vector(probe:v=>v.Call(131,call.Result,[call.Input,call.Output]))));
        var ordinary=NativeAotSocketProtocolTests.Vector(policyPrefix:v=>v.Call(131,0,[0,0]));
        Reject(()=>QemuTranscripts.InspectNativeAotSocketProtocolForTests(false,NativeAotThreadingTranscriptTests.Elf(),
            Encoding.UTF8.GetBytes(string.Join("\r\n",ordinary.Rows)+"\r\n"),
            address=>address==0x400200 || address==0x400202,address=>address==0x400202,8192,12288));
    }

    [HostTest("web nonblocking probe pipe isolates legacy pipe bytes and closes with EOF broken pipe and fd reuse")]
    private static void ProbePipeLifetime()
    {
        var report=Inspect(Vector(probe:v=>
        {
            v.Call(293,0,[0x500500,0x80800]);
            v.Call(1,1,[4,0x500800,1]);v.Call(0,-11,[7,0x500800,1]);
            v.Call(1,1,[8,0x500800,1]);v.Call(0,1,[7,0x500800,1]);v.Call(0,1,[3,0x500800,1]);
            v.Call(0,-9,[8,0x500800,1]);v.Call(1,-9,[7,0x500800,1]);
            v.Call(0,-11,[7,0x500800,1]);v.Call(3,0,[8]);v.Call(0,0,[7,0x500800,1]);v.Call(3,0,[7]);
            v.Call(293,0,[0x500500,0x80800]);v.Call(3,0,[7]);v.Call(1,-32,[8,0x500800,1]);v.Call(3,0,[8]);
        }));
        HostCheck.True(report.Active==2 && report.Ready==0 && report.Sent==36 && report.Received==40 && report.Observations.StdoutBytes==5);
    }

    [HostTest("web nonblocking probe pipe checks exact capacity and atomic insufficient space without consuming")]
    private static void ProbePipeCapacity()
    {
        HostCheck.Equal(2,Inspect(Vector(probe:v=>
        {
            v.Call(293,0,[0x500500,0x80800]);v.Call(1,4096,[8,0x500800,4096]);
            v.Call(1,-11,[8,0x500800,1]);v.Call(0,1,[7,0x500800,1]);
            v.Call(1,-11,[8,0x500800,2]);v.Call(0,4095,[7,0x500800,4096]);v.Call(0,-11,[7,0x500800,1]);
            v.Call(3,0,[8]);v.Call(0,0,[7,0x500800,1]);v.Call(3,0,[7]);
        })).SocketAccepts);
    }

    [HostTest("web nonblocking probe pipe rejects impossible creation IO aliases and unmeasured descriptor operations")]
    private static void ProbePipeRefusals()
    {
        foreach(var create in new (ulong Pointer,ulong Flags,long Result)[]{(1,0x80800,0),(0x500500,0x80000,0),(0x500500,0x80801,0),(0x500500,0x80800,1)})
            Reject(()=>Inspect(Vector(probe:v=>v.Call(293,create.Result,[create.Pointer,create.Flags]))));
        Action<NativeAotThreadingTranscriptTests.Vector>[] invalid=[
            v=>v.Call(293,0,[0x500500,0x80800]),
            v=>v.Call(0,1,[7,0x500800,1]),v=>v.Call(1,1,[7,0x500800,1]),v=>v.Call(0,1,[8,0x500800,1]),
            v=>v.Call(1,1,[9,0x500800,1]),v=>v.Call(1,1,[8,1,1]),v=>v.Call(0,0,[7,1,1]),
            v=>v.Call(1,1,[8,0x500800,2]),v=>v.Call(1,4097,[8,0x500800,4097]),
            v=>v.Call(17,0,[7,0x500800,1,0]),v=>v.Call(72,0,[7,2,1]),
            v=>{v.Call(3,0,[7]);v.Call(3,0,[8]);v.Call(1,1,[8,0x500800,1]);}];
        foreach(var corrupt in invalid)Reject(()=>Inspect(Vector(probe:v=>{v.Call(293,0,[0x500500,0x80800]);corrupt(v);} )));
    }

    [HostTest("web nonblocking probe pipe refuses fabricated EOF EAGAIN and partial atomic writes")]
    private static void ProbePipeResults()
    {
        foreach(long result in new long[]{0,1,-32})Reject(()=>Inspect(Vector(probe:v=>
        {v.Call(293,0,[0x500500,0x80800]);v.Call(0,result,[7,0x500800,1]);})));
        Reject(()=>Inspect(Vector(probe:v=>
        {v.Call(293,0,[0x500500,0x80800]);v.Call(1,-11,[8,0x500800,1]);})));
        foreach(long result in new long[]{0,1,2})Reject(()=>Inspect(Vector(probe:v=>
        {v.Call(293,0,[0x500500,0x80800]);v.Call(1,4095,[8,0x500800,4095]);v.Call(1,result,[8,0x500800,2]);})));
        Reject(()=>Inspect(Vector(probe:v=>
        {v.Call(293,0,[0x500500,0x80800]);v.Call(1,1,[8,0x500800,1]);v.Call(0,0,[7,0x500800,1]);})));
    }

    [HostTest("web runtime replay requires checked successful realtime and preserves monotonic and unsupported clock IDs")]
    private static void Realtime()
    {
        foreach(ulong id in new ulong[]{0,1,6})HostCheck.Equal(2,Inspect(Vector(clockId:id)).SocketAccepts);
        foreach(long result in new long[]{-22,-5})RejectClock(()=>Inspect(Vector(clockId:0,clockResult:result)));
        foreach(ulong address in new ulong[]{0,ulong.MaxValue})RejectClock(()=>Inspect(Vector(clockId:0,clockAddress:address)));
        foreach(ulong id in new ulong[]{2,7})
        { HostCheck.Equal(2,Inspect(Vector(clockId:id,clockResult:-22)).SocketAccepts);RejectClock(()=>Inspect(Vector(clockId:id))); }
        foreach(ulong id in new ulong[]{1,6})RejectClock(()=>Inspect(Vector(clockId:id,clockResult:-22)));
    }

    [HostTest("ordinary socket runtime replay retains unsupported realtime without borrowing web admission")]
    private static void RealtimeScope()
    {
        QemuTranscripts.ThreadingProtocolReport Run(long result)
        {
            var vector=NativeAotSocketProtocolTests.Vector(policyPrefix:v=>v.Call(228,result,[0,0x500800]));
            return QemuTranscripts.InspectNativeAotSocketProtocolForTests(false,NativeAotThreadingTranscriptTests.Elf(),
                Encoding.UTF8.GetBytes(string.Join("\r\n",vector.Rows)+"\r\n"),
                address=>address==0x400200 || address==0x400202,address=>address==0x400202,8192,12288);
        }
        _=Run(-22);RejectClock(()=>Run(0));
    }
    private static List<string> CallChange(int number,Func<string,string> change)
    {
        var rows=Vector();int index=rows.FindIndex(r=>r.StartsWith("M16_008_CALL ",StringComparison.Ordinal) && r.Contains(" nr="+number+" ",StringComparison.Ordinal));
        HostCheck.True(index>=0);rows[index]=change(rows[index]);return rows;
    }

    [HostTest("web runtime controls consume original events with private bad fd and same live healthy fd before later progress")]
    private static void Controls()
    {
        foreach(bool quota in new[]{false,true})foreach(bool later in new[]{false,true})
        {
            var c=NativeAotWebTransportTests.Report(quota);var report=Inspect(Vector(controls:true,quota:quota,retireAfterRequest:later),controls:c,quota:quota);
            HostCheck.True(report.TransportControlsChecked && report.SocketAccepts==(quota?7:8) && report.Active==2 && report.Protocol.Reaped==0);
            int ordinary=quota?5:6;
            HostCheck.Equal((ulong)(ordinary*18+(c.Sockets.Deadline.ResponseHex.Length+c.Sockets.Before.ResponseHex.Length+c.Sockets.After.ResponseHex.Length)/2),report.Sent);
            HostCheck.Equal((ulong)(ordinary*20+(c.Sockets.Deadline.RequestHex.Length+c.Sockets.Before.RequestHex.Length+c.Sockets.After.RequestHex.Length)/2),report.Received);
        }
        HostCheck.True(!Inspect(Vector()).TransportControlsChecked);
    }
    [HostTest("web runtime control events cannot be stripped duplicated or moved before healthy response completion")]
    private static void ControlPositions()
    {
        var c=NativeAotWebTransportTests.Report();var rows=Vector(controls:true);
        Reject(()=>Inspect(rows));
        int drop=rows.FindIndex(r=>r==c.Sockets.Observations.DropRow);
        var early=rows.ToList();string row=early[drop];early.RemoveAt(drop);
        int send=early.FindLastIndex(drop-1,r=>r.StartsWith("M16_008_CALL ",StringComparison.Ordinal) && r.Contains(" nr=44 ",StringComparison.Ordinal));
        HostCheck.True(send>0);early.Insert(send,row);Reject(()=>Inspect(early,controls:c));
        foreach(string kind in new[]{"M07_028_DROP","M07_027_TIMEOUT","M16_011_SLOT event=1 slot=0 owner=8192 epoch=1 generation=2"})
        {
            var missing=rows.Where(r=>!r.StartsWith(kind,StringComparison.Ordinal)).ToList();Reject(()=>Inspect(missing,controls:c));
        }
        var duplicate=rows.ToList();duplicate.Insert(drop,c.Sockets.Observations.DropRow);Reject(()=>Inspect(duplicate,controls:c));
    }
    [HostTest("web runtime timeout requires bad descriptor private and retained healthy descriptor open with exact IO")]
    private static void ControlDescriptors()
    {
        var c=NativeAotWebTransportTests.Report();
        Reject(()=>Inspect(Vector(controls:true,badOpen:true),controls:c));
        Reject(()=>Inspect(Vector(controls:true,healthyClosed:true),controls:c));
        foreach(bool before in new[]{false,true})
        {
            var changed=before?c.Sockets with{Before=c.Sockets.Before with{ResponseHex=c.Sockets.Before.ResponseHex+"00"}}:
                c.Sockets with{Deadline=c.Sockets.Deadline with{RequestHex=c.Sockets.Deadline.RequestHex+"00"}};
            Reject(()=>Inspect(Vector(controls:true),controls:c with{Sockets=changed}));
        }
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

    [HostTest("m16-013 web replay preserves zero-length send policy errors and shutdown state")]
    private static void ZeroLengthMessages()
    {
        foreach(int result in new[]{0,-11,-104,-32})HostCheck.Equal(36UL,Inspect(Vector(zeroSendMessage:result)).Sent);
        foreach(int result in new[]{-104,-32})HostCheck.Equal(36UL,Inspect(Vector(zeroSendMessage:result,zeroAfterShutdown:true)).Sent);
        Reject(()=>Inspect(Vector(zeroSendMessage:1)));
        Reject(()=>Inspect(Vector(zeroSendMessage:0,zeroAfterShutdown:true)));
        Reject(()=>Inspect(Vector(zeroSendMessage:-11,zeroAfterShutdown:true)));
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
        stale[policy]=stale[policy].Replace("packageBytes="+WebElfBytes.ToString(CultureInfo.InvariantCulture),"packageBytes=8681864",StringComparison.Ordinal);Reject(()=>Inspect(stale));
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
