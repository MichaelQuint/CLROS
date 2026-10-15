using System.Globalization;
using System.Text;
using OS.Kernel.Compatibility.LinuxX64;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Check the entire raw focused runtime leaf against its canonical
    /// package and sealed reviewed instruction lookup. Caller-owned inputs are
    /// borrowed synchronously. Parent firmware/startup/PCI/APIC/root and runner
    /// provenance remain mandatory outside this leaf. Missing, inconsistent or
    /// unsupported records throw; success supplies no independent QEMU authority.</summary>
    public static void ValidateNativeAotThreadingTail(string root, byte[] package,
        NativeAotThreadingProgram program, ReadOnlySpan<byte> rawTail, ulong userRoot, ulong kernelRoot)
    {
        Need(program is not null,"threading_program_missing");
        string app=program!.Application;
        NativeAotThreadingPackage.RequireCurrentSources(root,app); NativeAotThreadingPackage.RequirePackage(app,package);
        int offset=64+Encoding.ASCII.GetByteCount(NativeAotThreadingPackage.Manifest(app));
        var output=NativeAotThreadingOutput.Read(app,rawTail);
        _=new ThreadingProtocol(app,Rows(output),package[offset..],program.IsInstruction,program.IsSyscallReturn,
            output.WriteLengths,userRoot,kernelRoot).Validate();
        NativeAotThreadingPackage.RequireCurrentSources(root,app);
    }

    /// <summary>Validate one complete broader runtime leaf against its exact
    /// canonical package, sealed complete instruction index and original raw
    /// stdout. Kernel scheduling, signals, waits, workload phases and cleanup
    /// must agree. Parent firmware/root/runner provenance remains mandatory;
    /// this leaf alone grants no guest or runtime acceptance.</summary>
    public static void ValidateNativeAotRuntimeTail(string root, byte[] package,
        NativeAotRuntimeProgram program, ReadOnlySpan<byte> rawTail, ulong userRoot, ulong kernelRoot)
    {
        Need(program is not null,"runtime_program_missing");string app=program!.Application;
        NativeAotRuntimePackage.RequireCurrentSources(root,app);NativeAotRuntimePackage.RequirePackage(app,package);
        var output=NativeAotThreadingOutput.Read(app,rawTail);
        int offset=64+Encoding.ASCII.GetByteCount(NativeAotRuntimePackage.Manifest(app));
        _=new ThreadingProtocol(app,Rows(output),package[offset..],program.IsInstruction,program.IsSyscallReturn,
            output.WriteLengths,userRoot,kernelRoot,output.Stdout).Validate();
        NativeAotRuntimePackage.RequireCurrentSources(root,app);
    }

    /// <summary>Value-only broader parser seam for independent synthetic rows.
    /// No caller predicate, stdout or report constructs sealed program authority.</summary>
    public static ThreadingProtocolReport InspectNativeAotRuntimeProtocolForTests(string app,string[] rows,byte[] elf,
        Func<ulong,bool> instruction,Func<ulong,bool> syscallReturn,IReadOnlyList<int> lengths,string stdout,
        ulong userRoot,ulong kernelRoot)=>new ThreadingProtocol(app,rows,elf,instruction,syscallReturn,lengths,userRoot,kernelRoot,stdout).Validate();

    /// <summary>Value-only parser seam for independent synthetic vectors. Neither
    /// caller predicates nor these counts construct a prepared program or prove
    /// execution. Production uses the sealed package/index and original raw bytes.</summary>
    public static ThreadingProtocolReport InspectNativeAotThreadingProtocolForTests(string app, string[] rows, byte[] elf,
        Func<ulong,bool> instruction, Func<ulong,bool> syscallReturn, IReadOnlyList<int> writeLengths,
        ulong userRoot, ulong kernelRoot) => new ThreadingProtocol(app,rows,elf,instruction,syscallReturn,writeLengths,userRoot,kernelRoot).Validate();

    /// <summary>Derived accounting only, without execution or input authority.</summary>
    public readonly record struct ThreadingProtocolReport(int Calls,int Clones,int Reaped,int Irqs,int Drains,int Ticks,int Transfers,int SignalReturns);

    private static string[] Rows(NativeAotThreadingOutput.Capture capture)
    {
        Need(capture.NormalizedTail.EndsWith("\r\n",StringComparison.Ordinal),"threading_raw_terminal");
        return capture.NormalizedTail[..^2].Split("\r\n",StringSplitOptions.None);
    }

    // Parent framing uses normalized text only after the original byte envelopes
    // and complete stdout pass. The complete leaf still validates original bytes.
    private static string NormalizeThreadingWrites(string text,string app)
    {
        const string start="TEST_START m16-009\r\n";
        int at=text.IndexOf(start,StringComparison.Ordinal);
        Need(at>=0 && text.IndexOf(start,at+start.Length,StringComparison.Ordinal)<0,"threading_raw_leaf");
        var capture=NativeAotThreadingOutput.Read(app,Encoding.UTF8.GetBytes(text[at..]));
        return text[..at]+capture.NormalizedTail;
    }

    // Outer records are kernel output, never payload stdout. Validate the exact
    // wrapper before handing only the original inner bytes to the leaf decoder.
    private static ReadOnlySpan<byte> RuntimeRawLeaf(string app, ReadOnlySpan<byte> raw)
    {
        var markers = NativeAotRuntimeSelection.Markers(app);
        byte[] start = Encoding.ASCII.GetBytes(markers.Start + "\r\n");
        byte[] end = Encoding.ASCII.GetBytes(markers.Ready + "\r\n" + markers.Pass + "\r\n");
        int at = raw.IndexOf(start);
        Need(at >= 0 && raw[(at + start.Length)..].IndexOf(start) < 0 && raw.EndsWith(end), "runtime_profile_envelope");
        int length = raw.Length - at - start.Length - end.Length;
        Need(length > 0, "runtime_profile_extent");
        ReadOnlySpan<byte> leaf = raw.Slice(at + start.Length, length);
        Need(leaf.StartsWith("TEST_START m16-009\r\n"u8), "runtime_profile_inner_start");
        return leaf;
    }

    private static string NormalizeRuntimeWrites(string text, string app)
    {
        var markers = NativeAotRuntimeSelection.Markers(app);
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        var capture = NativeAotThreadingOutput.Read(app, RuntimeRawLeaf(app, bytes));
        int at = text.IndexOf(markers.Start + "\r\n", StringComparison.Ordinal) + markers.Start.Length + 2;
        return text[..at] + capture.NormalizedTail + markers.Ready + "\r\n" + markers.Pass + "\r\n";
    }

    /// <summary>Value-only framing inspection for synthetic controls. This
    /// validates no package, firmware, runner or sealed program authority.</summary>
    public static string InspectNativeAotRuntimeEnvelopeForTests(string app, ReadOnlySpan<byte> raw) =>
        NormalizeRuntimeWrites(DecodeUtf8(raw.ToArray()), app);

    // Both wrapper capture-hash verification and direct complete validation use
    // these bounded plain, single-link, unchanged file authorities. Reopening
    // legacy parent/runner readers cannot switch evidence during acceptance.
    internal static AuthorityFiles HoldThreadingEvidence(string root,QemuSmokeOptions options)
    {
        AuthorityFiles files=new();
        try
        {
            string serial=Path.GetFullPath(options.SerialLog,root),summary=Path.GetFullPath(options.SummaryLog,root);
            var raw=files.Open(Path.GetDirectoryName(serial)!,serial,"threading serial","threading_evidence_serial",8*1024*1024,singleLink:true);
            var report=files.Open(Path.GetDirectoryName(summary)!,summary,"threading summary","threading_evidence_summary",16*1024*1024,singleLink:true);
            AuthorityFiles.RequireDistinct(raw,report);
            return files;
        }
        catch { files.Dispose();throw; }
    }

    private sealed class RuntimeThread(ulong identity,int tid,ulong bottom,ulong top,ulong fs,ulong rip)
    {
        internal readonly ulong Identity=identity, Bottom=bottom, Top=top;
        internal readonly int Tid=tid;
        internal ulong Fs=fs, Gs, ClearTid, Mask, SavedRsp=top, SavedRip=rip, LastSequence, WaitOrder, AddressOrder;
        internal ulong TailRip,TailRsp,TailFs,TailFlags;
        internal ulong LastUserIrq;
        internal FutexReplay? WaitCall,TimedRestart;
        internal readonly Queue<CollectionSignal> SignalProofs=[];
        internal uint WaitSource, WaitMode, WaitBits;
        internal ulong WaitAddress, Deadline;
        internal long? Completion;
        internal ulong? DeliveryRsp;
        internal bool Parking, Exiting, Reaped, GroupRetired, NormalExit;
        internal int Requests, Delivered, Returned;
        internal readonly Stack<(ulong Frame,ulong Rsp,CollectionSignal? Proof)> Frames=[];
    }

    private readonly record struct FutexReplay(ulong Address,uint Op,uint Expected,ulong Timeout,uint Bits,ulong Rip,ulong Deadline);
    private sealed record CollectionSignal(ulong Identity,int Parked,bool Preempted);

    private sealed partial class ThreadingProtocol
    {
        private readonly string app;
        private readonly string[] rows;
        private readonly Func<ulong,bool> instruction,syscall;
        private readonly HashSet<ulong> exitTails;
        private readonly IReadOnlyList<int> writeLengths;
        private readonly Dictionary<int,LinuxCallWaitObservation> waits;
        private readonly Dictionary<int,LinuxCallReturnObservation> returns;
        private readonly HelloMemory memory;
        private readonly ulong userRoot,kernelRoot;
        private readonly int capacity;
        private readonly bool broader;
        private readonly uint entropyLimit;
        private readonly RuntimePhases? phases;
        private LinuxThreadExecutionBudget budget;
        private readonly RuntimeThread?[] slots;
        private readonly ulong[] generations;
        private readonly Queue<RuntimeThread> ready=[];
        private readonly List<RuntimeThread> history=[];
        private int position,active=1,nextTid=2,clones,reaped,calls,writes,epochs,irqs,drains,ticks,transfers,normalExits,signalReturns,peak=1,stdoutBytes;
        private ulong clock,waitOrder,addressOrder,currentBreak=0x400000000;
        private bool stopping,heapIntact=true,barrierRegistered;
        private bool signalPipeCreated,stackProbe;
        private ulong entropyBytes;
        private readonly HashSet<int> files=[];
        private readonly HashSet<ulong> actions=[];
        private int pipeRead,pipeWrite,pipeBytes;
        private static readonly string[] Fields="identity nr a0 a1 a2 a3 a4 a5 rip rsp flags fs gs clock valueBits action status".Split(' ').Select(k=>k+":dec").ToArray();

        internal ThreadingProtocol(string app,string[] rows,byte[] elf,Func<ulong,bool> instruction,Func<ulong,bool> syscall,
            IReadOnlyList<int> lengths,ulong userRoot,ulong kernelRoot,string? stdout=null)
        {
            broader=app is "NativeAotSuspensionStress" or "NativeAotThreadPoolStress" or "NativeAotRuntimeMatrix" or "NativeAotRuntimeSmoke";
            Need(broader || app is "NativeAotSuspensionSmoke" or "NativeAotThreadPoolSmoke","threading_application");
            Need(rows is not null && rows.Length is >=25 and <=100000 && instruction is not null && syscall is not null &&
                lengths is not null && lengths.Count is >0 and <=4096 && lengths.All(n=>n is >0 and <=4096) && lengths.Sum()<=4096,"threading_protocol_input");
            this.app=app; this.instruction=instruction!; this.syscall=syscall!; writeLengths=lengths!.ToArray();
            exitTails=FindExitTails(elf,this.instruction,this.syscall);
            waits=ReadLinuxCallWaits(rows!).ToDictionary(x=>x.RecordIndex);
            returns=ReadLinuxCallReturns(rows!).ToDictionary(x=>x.RecordIndex);
            this.rows=ValidateLinuxCallOrdinals(rows!); this.userRoot=userRoot; this.kernelRoot=kernelRoot;
            memory=new(elf); capacity=broader?16:app=="NativeAotSuspensionSmoke"?4:8;entropyLimit=broader?256U:32U;
            if(broader){Need(stdout is not null,"runtime_stdout_missing");phases=new RuntimePhases(app,stdout!,lengths!);}
            else Need(stdout is null,"threading_unexpected_stdout");
            slots=new RuntimeThread?[capacity]; generations=Enumerable.Repeat(1UL,capacity).ToArray();
            var main=new RuntimeThread(0x100000001,1,0x70000000-1572864,0x70000000,0,Entry(elf));
            slots[0]=main; history.Add(main); ready.Enqueue(main);
        }
        private static ulong Entry(byte[] elf) => System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(elf.AsSpan(24,8));
        private static ulong Bits(long value)=>unchecked((ulong)value);
        private string Take(){Need(position<rows.Length,"threading_truncated");return rows[position++];}
        private bool Starts(string prefix)=>position<rows.Length && rows[position].StartsWith(prefix,StringComparison.Ordinal);
        private void Exact(string expected)=>Need(Take()==expected,"threading_order_"+expected);
        private Dictionary<string,ulong> Row(string prefix,params string[] fields)=>ReadRecord(Take(),prefix,fields);

        internal ThreadingProtocolReport Validate()
        {
            Exact("TEST_START m16-009"); LinuxMemoryPolicy(Take(),entropyLimit,LinuxMemoryServices.MaximumPoolPages);
            int bytes=broader?NativeAotRuntimePackage.Specification(app).Bytes:app=="NativeAotSuspensionSmoke"?1320176:1577712;
            Exact("M16_007_POLICY descriptors=16 pipes=2 capacity=4096 scratch=8192 packageBytes="+bytes+
                " package=declared-immutable proc=owned-snapshot stdin=eof stdoutStderr=serial sigpipe=ignored-mechanical blocking=scheduler");
            var armed=LinuxSyscallArm(Take()); const string policy=" timer=user-irq-and-idle clock=hpet-nanoseconds reuse=off-stack";
            string line=Take(); Need(line.EndsWith(policy,StringComparison.Ordinal),"threading_bound_policy");
            var bound=ReadRecord(line[..^policy.Length],"M16_008_BOUND","root:dec","pool:dec","workers:dec","code:dec","metadata:dec","userImage:dec");
            Need(armed["root"]==userRoot && bound["root"]==kernelRoot && kernelRoot is >=4096 and <=0xfffff000 && kernelRoot%4096==0 &&
                bound["pool"]>=4096 && bound["pool"]<=0x100000000UL-LinuxThreadProgram.PoolPages*4096UL && bound["pool"]%4096==0 &&
                bound["workers"]==(ulong)capacity && bound["code"]==LinuxThreadProgram.Bytes &&
                bound["metadata"]==LinuxThreadProgram.MetadataPages*4096UL && bound["userImage"]==728,"threading_parent_geometry");
            DistinctExtents([(userRoot,4096),(kernelRoot,4096),(bound["pool"],LinuxThreadProgram.PoolPages*4096UL)],"threading_roots_pool");
            {
                var b=Row("M16_008_BUDGET","started:dec","epochs:dec","steps:dec","nanos:dec");
                Need(budget.Begin((uint)capacity,b["started"],application:true) && b["epochs"]==budget.MaximumEpochs &&
                    b["steps"]==budget.MaximumSteps && b["nanos"]==LinuxThreadExecutionBudget.RuntimeNanoseconds,"runtime_budget_policy");
            }
            while(active!=0)
            {
                Need((uint)(transfers/2+ticks)<budget.MaximumSteps,"threading_transfer_bound");
                if(ready.Count==0)
                {
                    Need(!stopping && history.Any(t=>!t.Reaped && t.WaitSource==1 && t.Completion is null && t.Deadline!=0),"threading_no_unowned_idle");
                    var tick=Row("M16_008_TICK","epoch:dec","nanos:dec","ready:dec");
                    Epoch(tick); ticks++; Need(tick["ready"]==(ulong)ready.Count,"threading_tick_ready"); continue;
                }
                RuntimeThread t=ready.Dequeue(); Need(!t.Reaped,"threading_selected_generation");
                bool exiting=stopping||t.Exiting, ending=false; ulong action=exiting?4UL:1UL;
                if(!exiting)
                {
                    if(t.WaitSource!=0)
                    {
                        Need(t.Completion.HasValue,"threading_unwon_resume");
                        long completion=t.Completion.GetValueOrDefault();
                        var resume=Row("M16_008_RESUME","identity:dec","valueBits:dec","status:dec");
                        Need(resume["identity"]==t.Identity && resume["valueBits"]==Bits(completion) && resume["status"]==1,"threading_resume");
                        if(t.WaitSource==2 && completion>0)
                        {Need(pipeBytes>=completion,"threading_pipe_completion_bytes");pipeBytes-=(int)completion;}
                        if(t.WaitSource==1 && completion==-4 && t.WaitCall is {Timeout: not 0} replay)t.TimedRestart=replay;
                        t.WaitSource=0; t.WaitAddress=t.Deadline=0; t.Completion=null;
                    }
                    ExpectDelivery(t);
                    while(Starts("M16_008_CALL ")||Starts("M16_005_WRITE"))
                    {
                        bool output=Starts("M16_005_WRITE");
                        if(output)
                        {
                            Need(writes<writeLengths.Count,"threading_extra_write");
                            string prefix="M16_005_WRITE fd=1 bytes="+writeLengths[writes].ToString(CultureInfo.InvariantCulture)+" data=";
                            Need(Take().StartsWith(prefix,StringComparison.Ordinal),"threading_write_envelope");
                        }
                        int index=position; var r=Row("M16_008_CALL",Fields);
                        Need(r["identity"]==t.Identity && syscall(r["rip"]) && output==(r["nr"]==1 && r["a0"]==1),"threading_call_binding");
                        SignalObservation(t,r,returns[index]);
                        Need((r["flags"]&0x400)==0,"threading_syscall_df");
                        if(t.TailRip!=0)
                        {
                            Envelope(t,r["rip"],r["rsp"],r["flags"],r["fs"],r["gs"],r["nr"]);
                            Need(r["nr"]==60,"threading_exit_tail_syscall");
                        }
                        Call(t,r,waits[index],output); Envelope(t,r["rip"],r["rsp"],r["flags"],r["fs"],r["gs"],r["nr"]);
                        t.SavedRsp=returns[index].Rsp; t.SavedRip=returns[index].Rip;
                        ExpectDelivery(t);
                        calls++; ending=r["action"] is 2 or 3 or 4 or 6; exiting=r["action"] is 2 or 4; action=r["action"];
                        if(ending) break;
                    }
                    if(ending)
                    {
                        var drain=Row("M16_009_DRAIN","identity:dec","epoch:dec","nanos:dec");
                        Need(drain["identity"]==t.Identity && (!stopping || drain["nanos"]==clock),"threading_drain_binding");
                        Epoch(drain); drains++;
                        if(action==6 || !exiting && t.Completion.HasValue) Enqueue(t);
                        t.Parking=false;
                    }
                    else
                    {
                        var irq=Row("M16_009_IRQ","identity:dec","epoch:dec","nanos:dec","rip:dec","rsp:dec","flags:dec","fs:dec","gs:dec","valueBits:dec");
                        Need(irq["identity"]==t.Identity && instruction(irq["rip"]),"threading_irq_binding");
                        Envelope(t,irq["rip"],irq["rsp"],irq["flags"],irq["fs"],irq["gs"],0);
                        if(t.TailRip!=0)Need(irq["valueBits"]==(irq["rip"]==t.TailRip+8?60UL:0),"threading_exit_tail_rax");
                        Epoch(irq); irqs++;t.LastUserIrq=(ulong)epochs;
                        t.SavedRsp=irq["rsp"]; t.SavedRip=irq["rip"]; Enqueue(t); action=1;
                    }
                }
                if(exiting)
                {
                    var exit=Row("M16_008_EXIT","identity:dec","clearTidResult:dec");
                    ulong clear=t.ClearTid==0 || memory.Covered(t.ClearTid,4,3)?0:Bits(-14);
                    Need(exit["identity"]==t.Identity && exit["clearTidResult"]==clear,"threading_clear_tid");
                    if(t.ClearTid!=0) Wake(t.ClearTid,0,1,uint.MaxValue);
                }
                transfers+=2;
                var supervisor=Row("M16_008_SUPERVISOR","identity:dec","action:dec","transfers:dec","ready:dec","active:dec");
                Need(supervisor["identity"]==t.Identity && supervisor["action"]==action && supervisor["transfers"]==(ulong)transfers &&
                    supervisor["ready"]==(ulong)ready.Count && supervisor["active"]==(ulong)active,"threading_supervisor");
                if(exiting)
                {
                    const string scrub=" context=zero stack=zero slot=zero"; string reap=Take();
                    Need(reap.EndsWith(scrub,StringComparison.Ordinal),"threading_reap_scrub");
                    var re=ReadRecord(reap[..^scrub.Length],"M16_008_REAP","old:dec","next:dec");
                    Need(re["old"]==t.Identity && re["next"]==t.Identity+(1UL<<32),"threading_reap_generation");
                    int slot=(int)(uint)t.Identity-1; generations[slot]++; slots[slot]=null;
                    t.Reaped=true; t.WaitSource=0; t.Completion=null; active--; reaped++;
                    if(t.NormalExit)phases?.Reaped();
                }
            }
            Need(stopping && reaped==clones+1 && writes==writeLengths.Count && irqs>0 && history.All(t=>t.Reaped),"threading_complete_lifetimes");
            if(broader)
            {
                Need(app=="NativeAotRuntimeSmoke"?peak>=5 && normalExits>=1 && clones>=5:
                    app=="NativeAotSuspensionStress"?peak>=8 && normalExits>=15 && clones>=17:
                    app=="NativeAotRuntimeMatrix"?peak>=9 && normalExits>=6:peak>=5 && clones>=4,"runtime_workload_lifetimes");
                phases!.Finish();
            }
            else Need(app=="NativeAotSuspensionSmoke" ? clones>=3 && normalExits>=1 && signalReturns>=2 && history.Any(t=>t.NormalExit && t.Returned>=2) : clones>=5,"threading_workload_lifetimes");
            var finished=Row("M16_008_BUDGET_END","nanos:dec");
            Need(finished["nanos"]>=clock && (!broader || finished["nanos"]>=phases!.LatestClock && phases.EarliestClock>=budget.StartedNanoseconds) &&
                budget.Observe(finished["nanos"]),"runtime_budget_complete");
            // Group cancellation retires pending/active signal state during
            // actual off-stack reap. It does not fabricate a user sigreturn.
            Need(history.All(t=>t.GroupRetired || t.Delivered==t.Returned && t.Requests==0),"threading_unreturned_signal");
            Need(entropyBytes>0 && barrierRegistered && signalPipeCreated && stackProbe && files.Count==0 &&
                new ulong[]{2,3,8,11,13,18,35}.All(actions.Contains),"threading_runtime_startup");
            const string cleanup=" userReserved=0 userBacked=0 process=drained timer=restored descriptors=closed guard=detached pool=scrubbed initialStack=zero reservation=retained";
            string owner=Take(); Need(owner.EndsWith(cleanup,StringComparison.Ordinal),"threading_owner_cleanup");
            var end=ReadRecord(owner[..^cleanup.Length],"M16_008_OWNER_END","reaped:dec","transfers:dec","irqs:dec","exit:dec");
            Need(end["reaped"]==(ulong)reaped && end["transfers"]==(ulong)transfers && end["irqs"]==(ulong)epochs && end["exit"]==0,"threading_owner_accounting");
            Exact("M16_009_RESTORE root=true descriptors=true ss=true msrs=true fsGs=true fp=true stack=true");
            Exact("CLROS_M16_009_READY"); Exact("TEST_PASS m16-009"); Need(position==rows.Length,"threading_extra_record");
            return new(calls,clones,reaped,irqs,drains,ticks,transfers,signalReturns);
        }

        private void Envelope(RuntimeThread t,ulong rip,ulong rsp,ulong flags,ulong fs,ulong gs,ulong number)
        {
            if(t.TailRip!=0){ExitEnvelope(t,rip,rsp,flags,fs,gs,number);return;}
            Need(rsp>=t.Bottom+8 && rsp<=t.Top && memory.Covered(rsp-8,8,3) && fs==t.Fs && gs==t.Gs &&
                LinuxSyscallGateway.ValidateUserFlags(flags,true),"threading_user_envelope");
            if(fs!=0) Need(memory.Covered(fs,8,1),"threading_live_tls");
        }
        private void Enqueue(RuntimeThread t){Need(!ready.Contains(t) && !t.Reaped,"threading_duplicate_ready");ready.Enqueue(t);}
        private static void ExpectDelivery(RuntimeThread t)
        {
            // Native PrepareUserReturn runs at dispatch and after every returning
            // call. Retain its interrupted RSP even if an IRQ captures the new
            // handler before the first handler syscall can observe its frame.
            if(t.Requests>0 && (t.Mask&(1UL<<34))==0 && t.DeliveryRsp is null)t.DeliveryRsp=t.SavedRsp;
        }
        private void Complete(RuntimeThread t,long result)
        { if(t.Completion.HasValue) return; t.Completion=result; if(!t.Parking) Enqueue(t); }
        private void Clock(ulong now)
        {
            Need(now>=clock && now<=long.MaxValue,"threading_clock"); clock=now;
            if(now!=0)Need(budget.Observe(now),"runtime_budget_clock");
            foreach(var t in history.Where(t=>!t.Reaped && t.WaitSource==1 && t.Completion is null && t.Deadline!=0 && t.Deadline<=now)
                .OrderBy(t=>t.Deadline).ThenBy(t=>t.WaitOrder)) {phases?.Deadline(t.Deadline,now);Complete(t,-110);}
        }
        private void Epoch(Dictionary<string,ulong> row)
        { Need(++epochs<=budget.MaximumEpochs && row["epoch"]==(ulong)epochs && row["nanos"]>=budget.StartedNanoseconds,"threading_epoch");Clock(row["nanos"]); }
        private int Wake(ulong address,uint mode,uint count,uint bits)
        {
            var selected=history.Where(t=>!t.Reaped && t.WaitSource==1 && t.Completion is null && t.WaitAddress==address && t.WaitMode==mode && (t.WaitBits&bits)!=0)
                .OrderBy(t=>t.AddressOrder).Take((int)Math.Min(count,int.MaxValue)).ToArray();
            foreach(var t in selected) Complete(t,0); return selected.Length;
        }

        private int Requeue(ulong source,ulong destination,uint mode,uint wake,uint move)
        {
            int count=Wake(source,mode,wake,uint.MaxValue);
            var selected=history.Where(t=>!t.Reaped && t.WaitSource==1 && t.Completion is null &&
                t.WaitAddress==source && t.WaitMode==mode).OrderBy(t=>t.AddressOrder).Take((int)Math.Min(move,int.MaxValue)).ToArray();
            if(source!=destination)foreach(var t in selected)
            {
                // Address FIFO changes; the scheduler's original deadline
                // insertion order, identity, sequence and completion do not.
                t.WaitAddress=destination;t.AddressOrder=++addressOrder;
            }
            return count+selected.Length;
        }
    }
}
