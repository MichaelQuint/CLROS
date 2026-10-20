using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Threading;
using OS.Manifest;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Value-only control seam for the canonical two-thread mechanical
    /// epoll race. Borrows complete original leaf rows and package bytes, checks
    /// exact calls, waits and off-stack cleanup, then returns fresh parent rows
    /// with only validated socket binding/readiness/close observations removed. Input
    /// records remain unchanged. Malformed observations throw; firmware, native
    /// execution, packet capture and guest authority require the full wrapper.</summary>
    public static string[] InspectSignalEpollForTests(string[] lines,byte[] package,ulong expectedRoot,ulong expectedKernelRoot)
        => ValidateSignalEpollTail(lines,package,expectedRoot,expectedKernelRoot);

    private static string[] ValidateSignalEpollTail(string[] lines,byte[] package,ulong expectedRoot,ulong expectedKernelRoot)
    {
        Need(lines is not null&&package is not null,"signal_epoll_input");
        Need(LinuxSignalProofFixture.PackageVariant(package!)==LinuxSignalProofFixture.EpollStolenWaitVariant,"signal_epoll_package");
        var packageView=LinuxElfPackage.Validate(package!);
        Need(packageView.Succeeded&&packageView.Manifest.Ipv4==0&&packageView.Manifest.Port==8080&&
            packageView.Manifest.Threads==2&&packageView.Manifest.Descriptors==16,"signal_epoll_manifest");
        var waits=ReadLinuxCallWaits(lines!,allowEpoll:true).ToDictionary(v=>v.RecordIndex);
        var returns=ReadLinuxCallReturns(lines!,allowEpoll:true).ToDictionary(v=>v.RecordIndex);
        string[] rows=ValidateLinuxCallOrdinals(lines!,allowEpoll:true);
        var instructions=LinuxSignalProofFixture.ExpectedWaitInvocations(LinuxSignalProofFixture.EpollStolenWaitVariant);
        HashSet<int> consumed=[];int at=0,call=0;ulong clock=0,blockedFlags=0;
        string Take(){Need(at<rows.Length,"signal_epoll_truncated");return rows[at++];}
        void Exact(string text)=>Need(Take()==text,"signal_epoll_order_"+text);
        Dictionary<string,ulong> Row(string prefix,params string[] keys)=>ReadRecord(Take(),prefix,keys);
        void Clock(ulong value){Need(value>=clock&&value<=long.MaxValue,"signal_epoll_clock");clock=value;}
        void PollPair(ulong identity,ulong ready)
        {
            for(int i=0;i<2;i++)
            {
                consumed.Add(at);var poll=Row("M16_012_POLL","identity:dec","clock:dec","ready:dec","idle:dec");
                Need(poll["identity"]==identity&&poll["ready"]==ready&&poll["idle"]==0&&
                    (i!=0||poll["clock"]==clock),"signal_epoll_poll_owner");Clock(poll["clock"]);
            }
        }
        void Check(SignalWaitCall expected,ulong ready=0,bool child=false,bool handler=false)
        {
            ulong identity=child?0x100000002UL:0x100000001UL;PollPair(identity,ready);
            if(expected.Nr==1&&expected.A0==1)
                Exact("M16_005_WRITE fd=1 bytes=15 data="+(handler?"M16_HANDLER_OK!":"M16_SIGNALS_OK!"));
            int position=at;
            var actual=Row("M16_008_CALL","identity:dec","nr:dec","a0:dec","a1:dec","a2:dec","a3:dec","a4:dec","a5:dec",
                "rip:dec","rsp:dec","flags:dec","fs:dec","gs:dec","clock:dec","valueBits:dec","action:dec","status:dec");
            Need(call<instructions.Count,"signal_epoll_call_extent");var instruction=instructions[call++];
            Need(instruction.Number==expected.Nr&&instruction.Child==child&&instruction.Handler==handler&&
                actual["identity"]==identity&&actual["nr"]==expected.Nr&&actual["rip"]==instruction.ReturnRip&&
                actual["a0"]==expected.A0&&actual["a1"]==expected.A1&&actual["a2"]==expected.A2&&
                actual["a3"]==expected.A3&&actual["a4"]==expected.A4&&actual["a5"]==0,"signal_epoll_call_binding");
            ulong rsp=child?0x406000UL:handler?expected.Nr==15?0x406c40UL:0x406c38UL:0x405000UL;
            ulong fs=child?0x403000UL:call<=2?0UL:0x402000UL;
            Need(actual["clock"]==clock&&actual["rsp"]==rsp&&actual["fs"]==fs&&actual["gs"]==0&&
                LinuxSyscallGateway.ValidateUserFlags(actual["flags"],false)&&(actual["flags"]&0x400)==0&&
                actual["valueBits"]==unchecked((ulong)expected.Value)&&actual["action"]==expected.Action&&actual["status"]==expected.Status,
                "signal_epoll_call_outcome");
            var wait=waits[position];var returned=returns[position];bool blocked=expected.Action==3;
            Need(wait.Identity==identity&&returned.Identity==identity&&wait.Ordinal==returned.Ordinal&&wait.Deadline==0&&
                wait.Source==(blocked?3U:0U)&&wait.Sequence==(blocked?1UL:0UL),"signal_epoll_call_wait");
            bool inHandler=handler&&expected.Nr!=15;
            Need(returned.SignalDepth==(inHandler?1UL:0UL)&&returned.SignalFrame==(inHandler?0x406c38UL:0)&&
                returned.BlockedSignals==(inHandler?512UL:0)&&returned.ValueBits==actual["valueBits"]&&returned.Fs==fs&&returned.Gs==0,
                "signal_epoll_signal_owner");
            if(expected.Nr==15)
                Need(returned.Rip==instructions[9].ReturnRip&&returned.Rsp==0x405000&&returned.Flags==blockedFlags,"signal_epoll_sigreturn");
            else
                Need(returned.Rip==actual["rip"]&&returned.Rsp==rsp&&returned.Flags==actual["flags"],"signal_epoll_return_envelope");
            if(blocked)blockedFlags=actual["flags"];
        }
        void Wake(ulong sequence,ulong completion,ulong state)
        {
            consumed.Add(at);var wake=Row("M16_012_WAKE","identity:dec","sequence:dec","completion:dec","state:dec","clock:dec");
            Need(wake["identity"]==0x100000001&&wake["sequence"]==sequence&&wake["completion"]==completion&&
                wake["state"]==state&&wake["clock"]==clock,"signal_epoll_wake");
        }
        void Supervisor(ulong identity,ulong action,ulong transfers,ulong ready,ulong active)
        {
            var s=Row("M16_008_SUPERVISOR","identity:dec","action:dec","transfers:dec","ready:dec","active:dec");
            Need(s["identity"]==identity&&s["action"]==action&&s["transfers"]==transfers&&s["ready"]==ready&&s["active"]==active,
                "signal_epoll_supervisor");
        }
        void Exit(ulong identity)
        {var e=Row("M16_008_EXIT","identity:dec","clearTidResult:dec");Need(e["identity"]==identity&&e["clearTidResult"]==0,"signal_epoll_exit");}
        void Reap(ulong identity)
        {
            const string suffix=" context=zero stack=zero slot=zero";string line=Take();
            Need(line.EndsWith(suffix,StringComparison.Ordinal),"signal_epoll_reap_scrub");
            var reap=ReadRecord(line[..^suffix.Length],"M16_008_REAP","old:dec","next:dec");
            Need(reap["old"]==identity&&reap["next"]==identity+(1UL<<32),"signal_epoll_reap_generation");
        }
        Exact("TEST_START m16-009");LinuxMemoryPolicy(Take());
        Exact("M16_007_POLICY descriptors=16 pipes=2 capacity=4096 scratch=8192 packageBytes=40960 package=declared-immutable proc=owned-snapshot stdin=eof stdoutStderr=serial sigpipe=ignored-mechanical blocking=scheduler");
        consumed.Add(at);Exact("M16_011_BOUND connections=8 rx=8192 tx=32768 epoll=4 watches=64 nic=live");
        var armed=LinuxSyscallArm(Take());
        const string boundSuffix=" timer=idle-only clock=hpet-nanoseconds reuse=off-stack";string boundLine=Take();
        Need(boundLine.EndsWith(boundSuffix,StringComparison.Ordinal),"signal_epoll_bound_policy");
        var bound=ReadRecord(boundLine[..^boundSuffix.Length],"M16_008_BOUND","root:dec","pool:dec","workers:dec","code:dec","metadata:dec","userImage:dec");
        Need(armed["root"]==expectedRoot&&bound["root"]==expectedKernelRoot&&expectedKernelRoot is >=4096 and <=0xfffff000&&expectedKernelRoot%4096==0&&
            bound["pool"]>=4096&&bound["pool"]<=0x100000000UL-LinuxThreadProgram.SignalPoolPages*4096UL&&bound["pool"]%4096==0&&
            bound["workers"]==2&&bound["code"]==LinuxThreadProgram.Bytes&&bound["metadata"]==LinuxThreadProgram.MetadataPages*4096UL&&bound["userImage"]==728,
            "signal_epoll_parent_geometry");
        DistinctExtents([(expectedRoot,4096),(expectedKernelRoot,4096),(bound["pool"],LinuxThreadProgram.SignalPoolPages*4096UL)],"signal_epoll_roots_pool");
        PollPair(0,1);
        SignalWaitCall[] pre=[new(39,1),new(186,1),new(158,0,0x1002,0x402000),new(13,0,10,0x401100,0,8),new(131,0,0x401120),
            new(293,0,0x4011c0),new(291,5),new(233,0,5,1,3,0x4011e0),new(56,2,0x7d0f00,0x406000,0x4011b8,0x4011bc,0x403000)];
        foreach(var e in pre)Check(e);
        Check(new(232,0,5,0x409020,1,ulong.MaxValue,Action:3,Status:9),1);Supervisor(0x100000001,3,2,1,2);
        PollPair(0,1);Check(new(39,1),child:true);Check(new(186,2),child:true);Check(new(1,1,4,0x407000,1),child:true);
        Wake(1,1,1);
        Check(new(234,0,1,1,10),1,child:true);Check(new(0,1,3,0x409010,1),1,child:true);Check(new(60,0,7,Action:4,Status:2),1,child:true);
        Exit(0x100000002);Supervisor(0x100000002,4,4,1,2);Reap(0x100000002);PollPair(0,1);
        consumed.Add(at);var repark=Row("M16_012_REPARK","identity:dec","previous:dec","sequence:dec","deadline:dec","clock:dec");
        Need(repark["identity"]==0x100000001&&repark["previous"]==1&&repark["sequence"]==2&&repark["deadline"]==0&&repark["clock"]==clock,
            "signal_epoll_stolen_repark");
        // Completion is published before the repark commits: the original
        // table still owns Parking, rather than a completed Pending handoff.
        Wake(2,(ulong)KernelThreadTable.Completion.Interrupted,(ulong)KernelThreadTable.State.Parking);
        Supervisor(0x100000001,3,6,1,1);PollPair(0,1);
        consumed.Add(at);var complete=Row("M16_012_COMPLETE","identity:dec","sequence:dec","completion:dec","valueBits:dec","clock:dec");
        Need(complete["identity"]==0x100000001&&complete["sequence"]==2&&complete["completion"]==5&&
            complete["valueBits"]==unchecked((ulong)-4L)&&complete["clock"]==clock,"signal_epoll_interrupted_completion");
        var resume=Row("M16_008_RESUME","identity:dec","valueBits:dec","status:dec");
        Need(resume["identity"]==0x100000001&&resume["valueBits"]==unchecked((ulong)-4L)&&resume["status"]==1,"signal_epoll_resume");
        SignalWaitCall[] handler=[new(186,1),new(14,0,2,0,0x401158,8),new(131,0,0,0x4011a0),new(1,15,1,0x4001a0,15),
            new(15,-4,1,0x4001a0,15,Action:5)];
        foreach(var e in handler)Check(e,handler:true);
        SignalWaitCall[] post=[new(1,1,4,0x407000,1),new(232,1,5,0x409020,1,ulong.MaxValue),new(0,1,3,0x409000,1),
            new(14,0,2,0,0x401150,8),new(131,0,0,0x401180),new(3,0,5),new(3,0,3),new(3,0,4),new(1,15,1,0x400180,15),new(231,0,42,Action:2,Status:2)];
        foreach(var e in post)Check(e);
        Exit(0x100000001);Supervisor(0x100000001,2,8,0,1);Reap(0x100000001);
        for(uint fd=0;fd<3;fd++)
        {consumed.Add(at);Exact(FormattableString.Invariant($"M16_011_CLOSE fd={fd} valueBits=0"));}
        Exact("M16_008_OWNER_END reaped=2 transfers=8 irqs=0 exit=42 userReserved=0 userBacked=0 process=drained timer=restored descriptors=closed guard=detached pool=scrubbed initialStack=zero reservation=retained");
        Exact("M16_009_RESTORE root=true descriptors=true ss=true msrs=true fsGs=true fp=true stack=true");Exact("CLROS_M16_009_READY");Exact("TEST_PASS m16-009");
        Need(call==instructions.Count&&at==rows.Length,"signal_epoll_extra_record");
        return lines!.Where((_,i)=>!consumed.Contains(i)).ToArray();
    }
}
