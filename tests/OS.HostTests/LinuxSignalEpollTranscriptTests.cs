using OS.Tooling;
using F = OS.Tooling.LinuxSignalProofFixture;
using T = LinuxSignalTranscriptTests;

internal static class LinuxSignalEpollTranscriptTests
{
    [HostTest("m16-020 epoll synthetic race binds original rows stolen repark interrupted handler and fresh readiness")]
    private static void Positive()
    {
        string[] rows=Synthetic(),before=(string[])rows.Clone();
        string[] parent=Inspect(rows);
        HostCheck.True(rows.SequenceEqual(before)&&!ReferenceEquals(rows,parent));
        HostCheck.Equal(31,parent.Count(r=>r.StartsWith("M16_008_CALL ",StringComparison.Ordinal)));
        HostCheck.Equal(4,parent.Count(r=>r.StartsWith("M16_008_SUPERVISOR ",StringComparison.Ordinal)));
        HostCheck.Equal(2,parent.Count(r=>r.StartsWith("M16_008_REAP ",StringComparison.Ordinal)));
        HostCheck.True(parent.All(r=>!r.StartsWith("M16_012_",StringComparison.Ordinal)&&!r.StartsWith("M16_011_BOUND ",StringComparison.Ordinal)&&!r.StartsWith("M16_011_CLOSE ",StringComparison.Ordinal)));
        HostCheck.True(parent.SequenceEqual(rows.Where(r=>!r.StartsWith("M16_012_",StringComparison.Ordinal)&&!r.StartsWith("M16_011_BOUND ",StringComparison.Ordinal)&&!r.StartsWith("M16_011_CLOSE ",StringComparison.Ordinal))));
        Array.Fill(rows,"changed");HostCheck.True(parent[0]=="TEST_START m16-009"&&parent[^1]=="TEST_PASS m16-009");
    }

    [HostTest("m16-020 epoll race rejects forged syscall arguments generations ordinals wait and restored signal frames")]
    private static void Calls()
    {
        string[] rows=Synthetic();
        int Call(ulong nr,bool child=false,bool last=false)
        {
            int[] found=Enumerable.Range(0,rows.Length).Where(i=>rows[i].StartsWith("M16_008_CALL ",StringComparison.Ordinal)&&
                T.Field(rows[i],"nr")==nr&&T.Field(rows[i],"identity")== (child?4294967298UL:4294967297UL)).ToArray();
            return last?found[^1]:found[0];
        }
        foreach(int at in new[]{Call(232),Call(1,true),Call(234,true),Call(0,true),Call(15),Call(232,last:true)})
            foreach(string field in new[]{"identity","a0","a1","a2","a3","rip","rsp","valueBits","action","status","ordinal"})
                Reject(T.Change(rows,at,field,unchecked(T.Field(rows[at],field)+1)));
        foreach(string field in new[]{"waitSource","waitDeadline","waitSequence","signalDepth","signalFrame","blockedSignals","returnRip","returnRsp","returnFlags","returnFs","returnGs","returnValueBits"})
        {
            int at=field.StartsWith("wait",StringComparison.Ordinal)?Call(232):Call(15);
            Reject(T.Change(rows,at,field,unchecked(T.Field(rows[at],field)+1)));
        }
        Reject(T.Change(rows,Call(232),"clock",ulong.MaxValue));
        bool rejected=false;try{QemuTranscripts.InspectSignalEpollForTests(rows,F.CreatePackage(F.StolenWaitVariant),0x200000,0x100000);}catch(ToolFailure){rejected=true;}
        HostCheck.True(rejected);
    }

    [HostTest("m16-020 epoll race rejects missing reordered unknown readiness and incomplete physical cleanup")]
    private static void Lifetimes()
    {
        string[] rows=Synthetic();
        int[] observations=Enumerable.Range(0,rows.Length).Where(i=>rows[i].StartsWith("M16_012_",StringComparison.Ordinal)&&
            !rows[i].StartsWith("M16_012_POLL ",StringComparison.Ordinal)).ToArray();
        foreach(int at in observations)
        {
            Reject(rows.Where((_,i)=>i!=at).ToArray());Reject([..rows[..at],rows[at],..rows[at..]]);
            foreach(string field in rows[at].Split(' ').Skip(1).Select(x=>x[..x.IndexOf('=')]))
                Reject(T.Change(rows,at,field,unchecked(T.Field(rows[at],field)+1)));
        }
        int wake=Array.FindIndex(rows,r=>r.StartsWith("M16_012_WAKE ",StringComparison.Ordinal));
        string[] reordered=(string[])rows.Clone();(reordered[wake-1],reordered[wake])=(reordered[wake],reordered[wake-1]);Reject(reordered);
        Reject([..rows[..wake],"M16_012_UNKNOWN identity=4294967297",..rows[wake..]]);
        int poll=Array.FindIndex(rows,r=>r.StartsWith("M16_012_POLL ",StringComparison.Ordinal));
        foreach(string field in new[]{"identity","ready","idle"})Reject(T.Change(rows,poll,field,T.Field(rows[poll],field)+1));
        Reject(rows.Where((_,i)=>i!=poll).ToArray());
        foreach(int close in Enumerable.Range(0,rows.Length).Where(i=>rows[i].StartsWith("M16_011_CLOSE ",StringComparison.Ordinal)))
        {
            Reject(rows.Where((_,i)=>i!=close).ToArray());Reject([..rows[..close],rows[close],..rows[close..]]);
            Reject(T.Change(rows,close,"fd",T.Field(rows[close],"fd")+1));Reject(T.Change(rows,close,"valueBits",1));
        }
        int end=Array.FindIndex(rows,r=>r.StartsWith("M16_008_OWNER_END ",StringComparison.Ordinal));
        foreach(string field in new[]{"reaped","transfers","irqs","exit","userReserved","userBacked"})Reject(T.Change(rows,end,field,T.Field(rows[end],field)+1));
        int reap=Array.FindIndex(rows,r=>r.StartsWith("M16_008_REAP ",StringComparison.Ordinal));Reject(T.Change(rows,reap,"next",4294967298));
        Reject(rows.Where((_,i)=>i!=reap).ToArray());Reject([..rows,rows[^1]]);
        bool rejected=false;try{QemuTranscripts.InspectSignalEpollForTests(rows,F.CreatePackage(F.EpollStolenWaitVariant),0x300000,0x100000);}catch(ToolFailure){rejected=true;}
        HostCheck.True(rejected);
    }

    [HostTest("m16-020 epoll race requires no progress before exit_group and exact nonexit poll pairs")]
    private static void ExitProgress()
    {
        string[] rows=Synthetic();
        int Call(ulong nr)=>Array.FindIndex(rows,r=>r.StartsWith("M16_008_CALL ",StringComparison.Ordinal)&&T.Field(r,"nr")==nr);
        int exit=Call(231);
        ulong clock=T.Field(rows[exit],"clock");
        string poll=FormattableString.Invariant($"M16_012_POLL identity=4294967297 clock={clock} ready=0 idle=0");
        Reject([..rows[..exit],poll,..rows[exit..]]);
        Reject([..rows[..exit],poll,poll,..rows[exit..]]);
        Reject(T.Change(rows,exit,"clock",clock-1));Reject(T.Change(rows,exit,"clock",clock+1));
        foreach(ulong nr in new ulong[]{39,60})
        {
            int at=Call(nr);
            Reject(rows.Where((_,i)=>i!=at-1).ToArray());
            Reject(rows.Where((_,i)=>i!=at-1&&i!=at-2).ToArray());
        }
        Reject(T.Change(rows,exit,"nr",60));Reject(T.Change(rows,exit,"a0",0));
        Reject(T.Change(rows,exit,"action",1));Reject(T.Change(rows,exit,"status",1));
        Reject(T.Change(rows,exit,"ordinal",T.Field(rows[exit],"ordinal")+1));
    }

    // Deterministic controls pin the Parking state and standard close inventory
    // observed in failed r03. These synthetic clocks/rows grant no guest proof.
    private static string[] Synthetic()
    {
        List<string> rows=T.Synthetic("AlternateStack").Take(5).Select(r=>r.Replace("packageBytes=28672","packageBytes=40960",StringComparison.Ordinal)).ToList();
        rows.Insert(3,"M16_011_BOUND connections=8 rx=8192 tx=32768 epoll=4 watches=64 nic=live");
        var instructions=F.ExpectedWaitInvocations(F.EpollStolenWaitVariant);int call=0;ulong clock=0;bool child=false,handler=false;
        Dictionary<ulong,int> ordinals=[];
        void Poll(ulong identity,ulong ready)
        {
            rows.Add(FormattableString.Invariant($"M16_012_POLL identity={identity} clock={clock} ready={ready} idle=0"));clock+=3;
            rows.Add(FormattableString.Invariant($"M16_012_POLL identity={identity} clock={clock} ready={ready} idle=0"));
        }
        void Emit(ulong nr,long value=0,ulong a=0,ulong b=0,ulong c=0,ulong d=0,ulong e=0,ulong action=1,ulong status=1,ulong ready=0)
        {
            ulong identity=child?4294967298UL:4294967297UL;
            if(nr!=231)Poll(identity,ready);
            if(nr==1&&a==1)rows.Add("M16_005_WRITE fd=1 bytes=15 data="+(handler?"M16_HANDLER_OK!":"M16_SIGNALS_OK!"));
            var instruction=instructions[call++];int ordinal=ordinals.GetValueOrDefault(identity)+1;ordinals[identity]=ordinal;
            ulong rsp=child?0x406000UL:handler?nr==15?0x406c40UL:0x406c38UL:0x405000UL,fs=child?0x403000UL:call<=2?0UL:0x402000UL;
            bool inHandler=handler&&nr!=15;
            rows.Add(FormattableString.Invariant($"M16_008_CALL identity={identity} nr={nr} a0={a} a1={b} a2={c} a3={d} a4={e} a5=0 rip={instruction.ReturnRip} rsp={rsp} flags=70 fs={fs} gs=0 clock={clock} valueBits={unchecked((ulong)value)} action={action} status={status} ordinal={ordinal} waitSource={(action==3?3:0)} waitDeadline=0 waitSequence={(action==3?1:0)} signalDepth={(inHandler?1:0)} signalFrame={(inHandler?0x406c38UL:0)} blockedSignals={(inHandler?512:0)} returnRip={(nr==15?instructions[9].ReturnRip:instruction.ReturnRip)} returnRsp={(nr==15?0x405000UL:rsp)} returnFlags=70 returnFs={fs} returnGs=0 returnValueBits={unchecked((ulong)value)}"));
        }
        void Supervisor(ulong identity,uint action,uint transfers,uint ready,uint active)=>rows.Add(FormattableString.Invariant($"M16_008_SUPERVISOR identity={identity} action={action} transfers={transfers} ready={ready} active={active}"));
        void Exit(ulong identity)=>rows.Add(FormattableString.Invariant($"M16_008_EXIT identity={identity} clearTidResult=0"));
        void Reap(ulong identity)=>rows.Add(FormattableString.Invariant($"M16_008_REAP old={identity} next={identity+(1UL<<32)} context=zero stack=zero slot=zero"));
        Poll(0,1);Emit(39,1);Emit(186,1);Emit(158,0,0x1002,0x402000);Emit(13,0,10,0x401100,0,8);Emit(131,0,0x401120);
        Emit(293,0,0x4011c0);Emit(291,5);Emit(233,0,5,1,3,0x4011e0);Emit(56,2,0x7d0f00,0x406000,0x4011b8,0x4011bc,0x403000);
        Emit(232,0,5,0x409020,1,ulong.MaxValue,action:3,status:9,ready:1);Supervisor(4294967297,3,2,1,2);
        Poll(0,1);child=true;Emit(39,1);Emit(186,2);Emit(1,1,4,0x407000,1);
        rows.Add(FormattableString.Invariant($"M16_012_WAKE identity=4294967297 sequence=1 completion=1 state=1 clock={clock}"));
        Emit(234,0,1,1,10,ready:1);Emit(0,1,3,0x409010,1,ready:1);Emit(60,0,7,action:4,status:2,ready:1);
        Exit(4294967298);Supervisor(4294967298,4,4,1,2);Reap(4294967298);child=false;Poll(0,1);
        rows.Add(FormattableString.Invariant($"M16_012_REPARK identity=4294967297 previous=1 sequence=2 deadline=0 clock={clock}"));
        rows.Add(FormattableString.Invariant($"M16_012_WAKE identity=4294967297 sequence=2 completion=5 state=3 clock={clock}"));
        Supervisor(4294967297,3,6,1,1);Poll(0,1);
        rows.Add(FormattableString.Invariant($"M16_012_COMPLETE identity=4294967297 sequence=2 completion=5 valueBits={unchecked((ulong)-4L)} clock={clock}"));
        rows.Add("M16_008_RESUME identity=4294967297 valueBits=18446744073709551612 status=1");
        handler=true;Emit(186,1);Emit(14,0,2,0,0x401158,8);Emit(131,0,0,0x4011a0);Emit(1,15,1,0x4001a0,15);Emit(15,-4,1,0x4001a0,15,action:5);handler=false;
        Emit(1,1,4,0x407000,1);Emit(232,1,5,0x409020,1,ulong.MaxValue);Emit(0,1,3,0x409000,1);
        Emit(14,0,2,0,0x401150,8);Emit(131,0,0,0x401180);Emit(3,0,5);Emit(3,0,3);Emit(3,0,4);Emit(1,15,1,0x400180,15);Emit(231,0,42,action:2,status:2);
        Exit(4294967297);Supervisor(4294967297,2,8,0,1);Reap(4294967297);
        rows.AddRange(["M16_011_CLOSE fd=0 valueBits=0","M16_011_CLOSE fd=1 valueBits=0","M16_011_CLOSE fd=2 valueBits=0"]);
        rows.AddRange(["M16_008_OWNER_END reaped=2 transfers=8 irqs=0 exit=42 userReserved=0 userBacked=0 process=drained timer=restored descriptors=closed guard=detached pool=scrubbed initialStack=zero reservation=retained",
            "M16_009_RESTORE root=true descriptors=true ss=true msrs=true fsGs=true fp=true stack=true","CLROS_M16_009_READY","TEST_PASS m16-009"]);
        HostCheck.Equal(instructions.Count,call);return rows.ToArray();
    }
    private static string[] Inspect(string[] rows)=>QemuTranscripts.InspectSignalEpollForTests(rows,F.CreatePackage(F.EpollStolenWaitVariant),0x200000,0x100000);
    private static void Reject(string[] rows)
    {bool rejected=false;try{Inspect(rows);}catch(ToolFailure){rejected=true;}HostCheck.True(rejected);}
}
