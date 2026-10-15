using System.Buffers.Binary;
using System.Globalization;
using System.Text.RegularExpressions;
using OS.Tooling;

internal static class LinuxHelloTranscriptTests
{
    [HostTest("nativeaot.hello complete pool envelope rejects old geometry physical overflow and late pool root overlap")]
    private static void PoolEnvelope()
    {
        var rows = Build(); _ = Inspect(Change(rows, "M16_008_BOUND ", "pool", 0xfff5e000));
        Reject(Change(rows, "M16_008_BOUND ", "pool", 0xfff5f000));
        Reject(Change(rows, "M16_008_BOUND ", "code", 16384)); Reject(Change(rows, "M16_008_BOUND ", "metadata", 45056));
        var overlap = Change(Change(rows, "M16_008_BOUND ", "root", 0x300000), "M16_008_BOUND ", "pool", 0x29c000);
        bool rejected = false;
        try { _ = QemuTranscripts.InspectLinuxHelloProtocolForTests(overlap, Elf(), r => r == 0x400202, r => r == 0x400202, 0x2000, 0x300000); }
        catch (ToolFailure failure) { rejected = failure.Reason == "hello_roots_pool_alias"; }
        HostCheck.True(rejected);
    }

    [HostTest("nativeaot.hello protocol models genuine child waits FIFO IRQ wake exit-group and three off-stack reaps")]
    private static void CompleteProtocol()
    {
        var rows = Build();
        var report = Inspect(rows);
        HostCheck.True(report.Calls > 30 && report.Irqs == 1 && report.Drains == 4 && report.Transfers == 14);
        // Intentionally nonzero unused registers must not be treated as ABI inputs.
        HostCheck.True(rows.Any(r=>r.Contains("nr=39 a0=4660",StringComparison.Ordinal)));
        var wakeFirst=Inspect(Build(wakeBeforeWait:true));
        HostCheck.True(wakeFirst.Calls==report.Calls && wakeFirst.Irqs==2 && wakeFirst.Drains==3 && wakeFirst.Transfers==14);
    }

    [HostTest("nativeaot.hello protocol rejects invented wait success wrong wake duplicate child and counterfeit scheduler accounting")]
    private static void ScheduleNegatives()
    {
        string[] rows = Build();
        Reject(Change(rows,"nr=202 ","action",1));
        Reject(Change(rows,"nr=202 ","valueBits",unchecked((ulong)-11L)));
        Reject(Change(rows," a1=129 ","valueBits",0));
        Reject(Change(rows,"nr=56 ","valueBits",3));
        Reject(Change(rows,"M16_008_RESUME ","valueBits",unchecked((ulong)-11L)));
        Reject(Change(rows,"M16_008_SUPERVISOR ","ready",2));
        Reject(Change(rows,"M16_008_SUPERVISOR ","transfers",4));
        Reject(Change(rows,"M16_008_REAP ","next",0x100000001));
        var lateResume=rows.ToList(); int cancel=lateResume.FindLastIndex(r=>r.StartsWith("M16_008_EXIT ",StringComparison.Ordinal));
        lateResume.Insert(cancel,"M16_008_RESUME identity=4294967299 valueBits=1 status=1"); Reject(lateResume.ToArray());
    }

    [HostTest("nativeaot.hello protocol rejects ABI memory FS instruction raw output pairing and incomplete cleanup mutations")]
    private static void ProtocolNegatives()
    {
        string[] rows=Build();
        foreach(var mutation in new[] {
            Change(rows,"nr=158 ","a0",0x1001), Change(rows,"nr=218 ","a0",0x53a374),
            Change(rows,"nr=56 ","a0",0x7d0f01), Change(rows,"nr=56 ","a4",0x100000120),
            Change(rows,"nr=293 ","a1",0), Change(rows,"nr=1 ","a2",26), Change(rows,"nr=231 ","a0",42),
            Change(rows,"nr=318 ","valueBits",0), Change(rows,"nr=324 ","valueBits",0),
            Change(rows,"M16_009_IRQ ","fs",0), Change(rows,"M16_009_IRQ ","flags",2),
            Change(rows,"M16_009_IRQ ","epoch",7), Change(rows,"M16_008_OWNER_END ","reaped",2),
            rows.Select(r=>r.Replace("initialStack=zero","initialStack=dirty",StringComparison.Ordinal)).ToArray(),
            rows.Select(r=>r.Replace("userBacked=0","userBacked=1",StringComparison.Ordinal)).ToArray(),
            rows.Where(r=>!r.StartsWith("M16_005_WRITE fd=1 bytes=19",StringComparison.Ordinal)).ToArray(),
            rows.Concat(new[]{"CLROS_M16_009_READY"}).ToArray() }) Reject(mutation);
        Reject(rows, instruction:_=>false); Reject(rows, syscall:_=>false);
    }

    private static QemuTranscripts.HelloProtocolReport Inspect(string[] rows,Func<ulong,bool>? instruction=null,Func<ulong,bool>? syscall=null) =>
        QemuTranscripts.InspectLinuxHelloProtocolForTests(rows,Elf(),instruction ?? (r=>r==0x400202),syscall ?? (r=>r==0x400202),0x2000,0x3000);

    private static void Reject(string[] rows,Func<ulong,bool>? instruction=null,Func<ulong,bool>? syscall=null)
    { bool rejected=false; try { Inspect(rows,instruction,syscall); } catch(ToolFailure) { rejected=true; } HostCheck.True(rejected,"hello synthetic mutation accepted"); }

    private static string[] Change(string[] rows,string selector,string key,ulong value)
    {
        string[] copy=rows.ToArray(); int at=Array.FindIndex(copy,r=>r.Contains(selector,StringComparison.Ordinal));
        HostCheck.True(at>=0,"mutation selector"); copy[at]=Regex.Replace(copy[at],@"\b"+key+"=[0-9]+",key+"="+value.ToString(CultureInfo.InvariantCulture)); return copy;
    }

    private static byte[] Elf()
    {
        byte[] elf=LinuxThreadProofFixture.CreateElf("Threads");
        BinaryPrimitives.WriteUInt64LittleEndian(elf.AsSpan(136),0x500000); // Independent enlarged static RW/BSS range.
        BinaryPrimitives.WriteUInt64LittleEndian(elf.AsSpan(160),0x40000);
        return elf;
    }

    private static string[] Build(bool wakeBeforeWait=false)
    {
        const ulong tc=0x100000000, one=0x100010000, two=0x100300000;
        const ulong fs1=one+0x183000-0x4c8, fs2=two+0x183000-0x4c8;
        const ulong top1=one+0x183000-0x618, top2=two+0x183000-0x618;
        List<string> rows=["TEST_START m16-009",
            "M16_006_POLICY physical=268435456 available=134217728 poolPages=4096 vaEnd=68719476736 reservationQuota=17179869184 entropyBytes=32 cpus=1 root=inactive-kernel",
            "M16_007_POLICY descriptors=16 pipes=2 capacity=4096 scratch=8192 packageBytes=1241768 package=declared-immutable proc=owned-snapshot stdin=eof stdoutStderr=serial sigpipe=ignored-mechanical blocking=scheduler",
            "M16_005_ARMED root=8192 cs=59 ss=51 kernelStack=1048576 guard=absent syscall=sce-star-lstar-fmask return=iretq fp=x87-sse kernelFsGs=owned-null",
            "M16_008_BOUND root=12288 pool=2097152 workers=4 code=53248 metadata=114688 userImage=728 timer=user-irq-and-idle clock=hpet-nanoseconds reuse=off-stack"];
        int tid=1; ulong clock=0,fs=0,rsp=0x6ffffff0;
        void Call(ulong nr,ulong value,ulong[] args,ulong action=1,ulong status=1)
        {
            ulong[] a=Enumerable.Repeat(0x1234UL,6).ToArray(); args.CopyTo(a,0);
            rows.Add($"M16_008_CALL identity={0x100000000UL+(uint)tid} nr={nr} a0={a[0]} a1={a[1]} a2={a[2]} a3={a[3]} a4={a[4]} a5={a[5]} rip=4194818 rsp={rsp} flags=514 fs={fs} gs=0 clock={clock} valueBits={value} action={action} status={status}");
        }
        void Drain(int epoch,ulong nanos) { clock=nanos; rows.Add($"M16_009_DRAIN identity={0x100000000UL+(uint)tid} epoch={epoch} nanos={clock}"); }
        void Supervisor(int action,int transfers,int ready,int active) => rows.Add($"M16_008_SUPERVISOR identity={0x100000000UL+(uint)tid} action={action} transfers={transfers} ready={ready} active={active}");
        void Exit() => rows.Add($"M16_008_EXIT identity={0x100000000UL+(uint)tid} clearTidResult=0");
        void Reap() => rows.Add($"M16_008_REAP old={0x100000000UL+(uint)tid} next={0x200000000UL+(uint)tid} context=zero stack=zero slot=zero");
        Call(9,tc,[0,488,3,0x22,ulong.MaxValue,0]); fs=tc+0x120; Call(158,0,[0x1002,fs]); Call(218,1,[0x53a370]);
        foreach(ulong signal in new ulong[]{11,8,35,2,3,18}) { Call(13,0,[signal,0,0x500000,8]); Call(13,0,[signal,0x500020,0x500000,8]); }
        Call(13,0,[13,0x500020,0x500000,8]); Call(324,0x18,[0,0,0]); Call(324,0,[16,0,0]); Call(318,8,[0x500100,8,0]);
        Call(9,one,[0,0x183000,0,0x22,ulong.MaxValue,0]); Call(10,0,[one+8192,0x181000,3]);
        Call(56,2,[0x7d0f00,top1,0x500200,0x53a370,fs1]);
        if(wakeBeforeWait)
        { clock=1; rows.Add($"M16_009_IRQ identity=4294967297 epoch=1 nanos=1 rip=4194818 rsp={rsp} flags=514 fs={fs} gs=0 valueBits=2"); Supervisor(1,2,2,2); }
        else { clock=1; Call(202,0,[0x5394c0,128,0x80000002,0],3,9); Drain(1,2); Supervisor(3,2,1,2); }
        tid=2; fs=fs1; rsp=top1-64;
        Call(14,0,[2,0x500100,0,8]); Call(186,2,[]); Call(39,1,[]); Call(157,unchecked((ulong)-38L),[15,0x500100]);
        Call(9,0x100200000,[0,16384,3,0x22,ulong.MaxValue,0]); Call(202,wakeBeforeWait?0UL:1UL,[0x5394c0,129,1]);
        clock=wakeBeforeWait?2UL:3UL; Call(202,0,[fs1+0x40,128,2,0],3,9); Drain(2,wakeBeforeWait?3UL:4UL); Supervisor(3,4,1,2);
        tid=1; fs=tc+0x120; rsp=0x6ffffff0;
        if(wakeBeforeWait) { clock=4; Call(202,unchecked((ulong)-11L),[0x5394c0,128,0x80000002,0]); }
        else rows.Add("M16_008_RESUME identity=4294967297 valueBits=0 status=1");
        Call(25,unchecked((ulong)-12L),[0x6fe80000,4096,8192,0]); Call(25,unchecked((ulong)-14L),[0x6fe7f000,4096,8192,0]);
        Call(293,0,[0x500400,0x80000]); Call(9,two,[0,0x183000,0,0x22,ulong.MaxValue,0]); Call(10,0,[two+8192,0x181000,3]);
        Call(56,3,[0x7d0f00,top2,0x500200,0x53a370,fs2]);
        clock=5; rows.Add($"M16_009_IRQ identity=4294967297 epoch=3 nanos=5 rip=4194818 rsp={rsp} flags=514 fs={fs} gs=0 valueBits=3"); Supervisor(1,6,2,3);
        tid=3; fs=fs2; rsp=top2-64; Call(14,0,[2,0x500100,0,8]); Call(157,unchecked((ulong)-38L),[15,0x500100]);
        Call(0,0,[3,rsp-64,1],3,9); Drain(4,6); Supervisor(3,8,1,3);
        tid=1; fs=tc+0x120; rsp=0x6ffffff0;
        foreach(var write in new[]{(27UL,"TEST_START nativeaot.hello"),(19UL,"NATIVEAOT_HELLO_OK"),(26UL,"TEST_PASS nativeaot.hello")})
        { rows.Add($"M16_005_WRITE fd=1 bytes={write.Item1} data={write.Item2}"); Call(1,write.Item1,[1,0x500100,write.Item1]); }
        Call(231,0,[0],2,2); Drain(5,6); Exit(); Supervisor(2,10,2,3); Reap();
        tid=2; Exit(); Supervisor(4,12,1,2); Reap(); tid=3; Exit(); Supervisor(4,14,0,1); Reap();
        rows.AddRange(["M16_008_OWNER_END reaped=3 transfers=14 irqs=5 exit=0 userReserved=0 userBacked=0 process=drained timer=restored descriptors=closed guard=detached pool=scrubbed initialStack=zero reservation=retained",
            "M16_009_RESTORE root=true descriptors=true ss=true msrs=true fsGs=true fp=true stack=true","CLROS_M16_009_READY","TEST_PASS m16-009"]);
        return LinuxCallOrdinalTests.Annotate(rows);
    }
}
