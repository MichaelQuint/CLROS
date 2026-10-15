using OS.Kernel.Compatibility.LinuxX64;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>
    /// Checks the complete unchanged Hello leaf against its exact package,
    /// immutable reviewed instruction index and original stdout bytes. The full
    /// caller must separately validate firmware, startup, PCI/APIC and runner
    /// provenance. No synthetic model report is accepted as execution authority.
    /// Input storage remains caller-owned and unchanged for this synchronous call.
    /// Missing, forged, unsupported or incomplete records throw ToolFailure.
    /// </summary>
    public static void ValidateLinuxHelloTail(string root, string[] tail, byte[] package,
        NativeAotHelloProgram program, ReadOnlySpan<byte> rawTail, ulong userRoot, ulong kernelRoot)
    {
        Need(program is not null, "hello_program_missing");
        NativeAotHelloGuest.RequireCurrentSources(root); NativeAotHelloGuest.RequirePackage(package);
        NativeAotHelloOutput.Validate(rawTail);
        int offset = 64 + System.Text.Encoding.ASCII.GetByteCount(NativeAotHelloGuest.Manifest);
        _ = new HelloProtocol(tail, package[offset..], program!.IsInstruction, program!.IsSyscallReturn, userRoot, kernelRoot).Validate();
        NativeAotHelloGuest.RequireCurrentSources(root);
    }

    /// <summary>
    /// Independent parser/model test seam with caller-supplied ELF and instruction
    /// predicates. Returned counts grant no prepared-program or QEMU authority.
    /// Production admission uses only ValidateLinuxHelloTail and the sealed exact
    /// package/index. Synthetic rows never establish execution or runtime support.
    /// </summary>
    public static HelloProtocolReport InspectLinuxHelloProtocolForTests(string[] tail, byte[] elf,
        Func<ulong, bool> instruction, Func<ulong, bool> syscallReturn, ulong userRoot, ulong kernelRoot) =>
        new HelloProtocol(tail, elf, instruction, syscallReturn, userRoot, kernelRoot).Validate();

    /// <summary>Value-only protocol accounting, without execution or input authority.</summary>
    public readonly record struct HelloProtocolReport(int Calls, int Irqs, int Drains, int Transfers);

    // Optional diagnostics never replace clock, scheduling or cleanup evidence.
    private static void LinuxTiming(string line,ulong identity,ulong epoch)
    {
        var r=ReadRecord(line,"M16_008_TIMING","identity:dec","epoch:dec","start:dec","arm:dec","capture:dec","ready:dec","periodFs:dec");
        Need(r["identity"]==identity && r["epoch"]==epoch && r["periodFs"] is >0 and <=1_000_000_000 &&
            r["start"]<=r["arm"] && r["arm"]<=r["capture"] && r["capture"]<=r["ready"],"linux_timing_observation");
    }

    private static Dictionary<string,ulong> LinuxSyscallTiming(string line,ulong identity,ulong ordinal,ulong number)
    {
        var r=ReadRecord(line,"M16_008_SYSCALL_TIME","identity:dec","ordinal:dec","nr:dec","entered:dec","admitted:dec","dispatched:dec","prepared:dec","periodFs:dec");
        Need(r["identity"]==identity && r["ordinal"]==ordinal && r["nr"]==number && r["periodFs"] is >0 and <=1_000_000_000 &&
            r["entered"]<=r["admitted"] && r["admitted"]<=r["dispatched"] && r["dispatched"]<=r["prepared"],"linux_syscall_timing_observation");
        return r;
    }

    // Validate original bytes before removing only the three known payload LFs
    // for the existing CRLF record parser and runner's trimmed marker summary.
    private static string NormalizeHelloWrites(string text)
    {
        const string start = "TEST_START m16-009\r\n";
        int at = text.IndexOf(start,StringComparison.Ordinal);
        Need(at >= 0 && text.IndexOf(start,at+start.Length,StringComparison.Ordinal) < 0,"hello_raw_leaf");
        NativeAotHelloOutput.Validate(System.Text.Encoding.UTF8.GetBytes(text[at..]));
        foreach (string payload in new[] {"TEST_START nativeaot.hello","NATIVEAOT_HELLO_OK","TEST_PASS nativeaot.hello"})
            text = text.Replace("data="+payload+"\n\r\n","data="+payload+"\r\n",StringComparison.Ordinal);
        return text;
    }

    private sealed class HelloThread(int tid, ulong bottom, ulong top, ulong fs)
    {
        internal readonly int Tid = tid;
        internal readonly ulong Bottom = bottom, Top = top;
        internal ulong Fs = fs, WaitAddress;
        internal int Calls, Pending; // 1 futex, 2 signal-pipe read.
        internal bool ReadyCompletion, Reaped, StartupWake, MaskSet;
        internal ulong Identity => 0x100000000UL + (uint)Tid;
    }

    private sealed class HelloProtocol
    {
        private readonly string[] rows;
        private readonly Func<ulong, bool> instruction, syscall;
        private readonly ulong userRoot, kernelRoot;
        private readonly HelloMemory memory;
        private LinuxThreadExecutionBudget budget;
        private readonly HelloThread?[] threads = new HelloThread?[4];
        private readonly Queue<int> ready = new();
        private readonly HashSet<int> files = [];
        private readonly HashSet<ulong> signalQueries = [], signalInstalls = [];
        private int position, clones, active = 1, reaped, transfers, epochs, irqs, drains, writes, calls, pipeRead, pipeWrite;
        private ulong clock, mainAllocation, currentBreak = 0x400000000;
        private bool stopping, heapIntact = true, mainWait, stackGuardProbe, barrierRegistered, entropy;
        private static readonly string[] CallFields = "identity nr a0 a1 a2 a3 a4 a5 rip rsp flags fs gs clock valueBits action status".Split(' ').Select(k => k + ":dec").ToArray();
        private static readonly string[] WriteRows = [
            "M16_005_WRITE fd=1 bytes=27 data=TEST_START nativeaot.hello",
            "M16_005_WRITE fd=1 bytes=19 data=NATIVEAOT_HELLO_OK",
            "M16_005_WRITE fd=1 bytes=26 data=TEST_PASS nativeaot.hello"];

        internal HelloProtocol(string[] rows, byte[] elf, Func<ulong, bool> instruction, Func<ulong, bool> syscall,
            ulong userRoot, ulong kernelRoot)
        {
            Need(rows is not null && rows.Length is >= 35 and <= 16000 && instruction is not null && syscall is not null,
                "hello_protocol_input");
            this.rows = ValidateLinuxCallOrdinals(rows!); this.instruction = instruction!; this.syscall = syscall!;
            this.userRoot = userRoot; this.kernelRoot = kernelRoot;
            memory = new(elf);
            threads[1] = new(1, 0x70000000 - 1_572_864, 0x70000000, 0);
            ready.Enqueue(1);
        }

        private string Take() { Need(position < rows.Length, "hello_truncated"); return rows[position++]; }
        private bool Starts(string prefix) => position < rows.Length && rows[position].StartsWith(prefix, StringComparison.Ordinal);
        private void Exact(string expected) => Need(Take() == expected, "hello_record_order_" + expected);
        private Dictionary<string, ulong> Row(string prefix, params string[] fields) => ReadRecord(Take(), prefix, fields);
        private static ulong Err(long value) => unchecked((ulong)value);

        internal HelloProtocolReport Validate()
        {
            Exact("TEST_START m16-009"); LinuxMemoryPolicy(Take(), expectedPoolPages: LinuxMemoryServices.MaximumPoolPages);
            Exact("M16_007_POLICY descriptors=16 pipes=2 capacity=4096 scratch=8192 packageBytes=1241768 package=declared-immutable proc=owned-snapshot stdin=eof stdoutStderr=serial sigpipe=ignored-mechanical blocking=scheduler");
            var armed = LinuxSyscallArm(Take());
            const string suffix = " timer=user-irq-and-idle clock=hpet-nanoseconds reuse=off-stack";
            string line = Take(); Need(line.EndsWith(suffix, StringComparison.Ordinal), "hello_bound_policy");
            var bound = ReadRecord(line[..^suffix.Length], "M16_008_BOUND", "root:dec", "pool:dec", "workers:dec", "code:dec", "metadata:dec", "userImage:dec");
            Need(armed["root"] == userRoot && bound["root"] == kernelRoot && kernelRoot is >= 4096 and <= 0xfffff000 && kernelRoot % 4096 == 0 &&
                bound["pool"] >= 4096 && bound["pool"] <= 0x100000000UL - LinuxThreadProgram.PoolPages * 4096UL && bound["pool"] % 4096 == 0 &&
                bound["workers"] == 4 && bound["code"] == LinuxThreadProgram.Bytes && bound["metadata"] == LinuxThreadProgram.MetadataPages * 4096UL && bound["userImage"] == 728, "hello_parent_geometry");
            DistinctExtents([(kernelRoot,4096),(userRoot,4096),(bound["pool"],LinuxThreadProgram.PoolPages*4096UL)], "hello_roots_pool");
            var limits = Row("M16_008_BUDGET", "started:dec", "epochs:dec", "steps:dec", "nanos:dec");
            Need(budget.Begin(4, limits["started"], application: true) && limits["epochs"] == budget.MaximumEpochs &&
                limits["steps"] == budget.MaximumSteps && limits["nanos"] == LinuxThreadExecutionBudget.RuntimeNanoseconds,
                "hello_budget_policy");
            while (ready.Count != 0)
            {
                Need((uint)(transfers / 2) < budget.MaximumSteps, "hello_dispatch_bound");
                HelloThread t = threads[ready.Dequeue()]!;
                Need(!t.Reaped, "hello_selected_generation");
                bool ending = false, exiting = stopping;
                if (!stopping)
                {
                    if (t.Pending != 0)
                    {
                        Need(t.Pending == 1 && t.ReadyCompletion, "hello_no_unwon_resume");
                        var resume = Row("M16_008_RESUME", "identity:dec", "valueBits:dec", "status:dec");
                        Need(resume["identity"] == t.Identity && resume["valueBits"] == 0 && resume["status"] == 1, "hello_resume");
                        t.Pending = 0; t.ReadyCompletion = false;
                    }
                    while (Starts("M16_008_CALL ") || Starts("M16_005_WRITE"))
                    {
                        bool output = Starts("M16_005_WRITE");
                        if (output) { Need(writes < 3, "hello_extra_write"); Exact(WriteRows[writes]); }
                        var call = Row("M16_008_CALL", CallFields);
                        Need(call["identity"] == t.Identity && syscall(call["rip"]) && ++t.Calls <= 4096 && !ending, "hello_call_binding");
                        Need(output == (call["nr"] == 1), "hello_write_call_pair");
                        Call(t, call);
                        Envelope(t, call, false);
                        if(Starts("M16_008_SYSCALL_TIME "))LinuxSyscallTiming(Take(),t.Identity,(ulong)t.Calls,call["nr"]);
                        calls++;
                        ending = call["action"] is 2 or 3 or 4;
                        exiting = call["action"] is 2 or 4;
                        if (ending) break;
                    }
                    if (ending)
                    {
                        var drain = Row("M16_009_DRAIN", "identity:dec", "epoch:dec", "nanos:dec");
                        Epoch(t, drain, stopping); drains++;
                    }
                    else
                    {
                        var irq = Row("M16_009_IRQ", "identity:dec", "epoch:dec", "nanos:dec", "rip:dec", "rsp:dec", "flags:dec", "fs:dec", "gs:dec", "valueBits:dec");
                        Need(instruction(irq["rip"]), "hello_irq_instruction"); Envelope(t, irq, true);
                        Epoch(t, irq, false); irqs++; ready.Enqueue(t.Tid);
                    }
                }
                // Cancelled blocked children never enter user mode or acquire a
                // new timer; no invented RESUME, syscall, IRQ or DRAIN is allowed.
                if (exiting)
                {
                    var exit = Row("M16_008_EXIT", "identity:dec", "clearTidResult:dec");
                    Need(exit["identity"] == t.Identity && exit["clearTidResult"] == 0, "hello_clear_tid");
                }
                transfers += 2;
                var supervisor = Row("M16_008_SUPERVISOR", "identity:dec", "action:dec", "transfers:dec", "ready:dec", "active:dec");
                ulong action = exiting ? t.Tid == 1 ? 2UL : 4UL : ending ? 3UL : 1UL;
                Need(supervisor["identity"] == t.Identity && supervisor["action"] == action && supervisor["transfers"] == (ulong)transfers &&
                    supervisor["ready"] == (ulong)ready.Count && supervisor["active"] == (ulong)active, "hello_supervisor");
                if(Starts("M16_008_TIMING "))
                { Need(action==1 && !ending && !exiting,"hello_timing_irq_only");LinuxTiming(Take(),t.Identity,(ulong)epochs); }
                if (exiting)
                {
                    const string scrub = " context=zero stack=zero slot=zero";
                    string reapLine = Take(); Need(reapLine.EndsWith(scrub, StringComparison.Ordinal), "hello_reap_scrub");
                    var reap = ReadRecord(reapLine[..^scrub.Length], "M16_008_REAP", "old:dec", "next:dec");
                    Need(reap["old"] == t.Identity && reap["next"] == t.Identity + (1UL << 32), "hello_reap_generation");
                    t.Reaped = true; active--; reaped++;
                }
            }
            Need(stopping && clones == 2 && active == 0 && reaped == 3 && writes == 3 && irqs > 0 &&
                threads.Skip(1).All(t => t is not null && t.Reaped), "hello_complete_lifetimes");
            var finished = Row("M16_008_BUDGET_END", "nanos:dec");
            Need(finished["nanos"] >= clock && budget.Observe(finished["nanos"]), "hello_budget_complete");
            const string cleanup = " userReserved=0 userBacked=0 process=drained timer=restored descriptors=closed guard=detached pool=scrubbed initialStack=zero reservation=retained";
            string owner = Take(); Need(owner.EndsWith(cleanup, StringComparison.Ordinal), "hello_owner_cleanup");
            var end = ReadRecord(owner[..^cleanup.Length], "M16_008_OWNER_END", "reaped:dec", "transfers:dec", "irqs:dec", "exit:dec");
            Need(end["reaped"] == 3 && end["transfers"] == (ulong)transfers && end["irqs"] == (ulong)epochs && end["exit"] == 0, "hello_owner_accounting");
            Exact("M16_009_RESTORE root=true descriptors=true ss=true msrs=true fsGs=true fp=true stack=true");
            Exact("CLROS_M16_009_READY"); Exact("TEST_PASS m16-009"); Need(position == rows.Length, "hello_extra_record");
            return new(calls, irqs, drains, transfers);
        }

        private void Envelope(HelloThread t, Dictionary<string, ulong> row, bool irq)
        {
            Need(row["rsp"] >= t.Bottom + 8 && row["rsp"] <= t.Top && memory.Covered(row["rsp"] - 8, 8, 3) &&
                row["fs"] == t.Fs && row["gs"] == 0 && LinuxSyscallGateway.ValidateUserFlags(row["flags"], true) &&
                (irq || (row["flags"] & 0x400) == 0), "hello_user_envelope");
            if (t.Fs != 0) Need(memory.Covered(t.Fs, 8, 1), "hello_live_tls");
        }

        private void Epoch(HelloThread t, Dictionary<string, ulong> row, bool stopped)
        {
            Need(++epochs <= budget.MaximumEpochs && row["identity"] == t.Identity && row["epoch"] == (ulong)epochs &&
                row["nanos"] >= clock && budget.Observe(row["nanos"]) && (!stopped || row["nanos"] == clock), "hello_epoch_clock");
            clock = row["nanos"];
        }

        private void Call(HelloThread t, Dictionary<string, ulong> r)
        {
            ulong nr = r["nr"], a = r["a0"], b = r["a1"], c = r["a2"], d = r["a3"], e = r["a4"], f = r["a5"], value = r["valueBits"];
            bool wait = nr == 202 && b == 128;
            Need(r["clock"] >= clock && r["clock"] <= long.MaxValue && (wait || r["clock"] == clock), "hello_call_clock");
            // Initial calls retain the scheduler's unobserved zero, even when
            // budget admission has already read HPET. Waits observe the clock.
            Need(r["clock"] == 0 || budget.Observe(r["clock"]), "hello_budget_clock");
            clock = r["clock"];
            ulong action = 1, status = 1;
            if (t.Tid == 1 && t.Calls <= 3)
                Need(nr == (t.Calls == 1 ? 9UL : t.Calls == 2 ? 158UL : 218UL), "hello_startup_order");
            if (t.Tid != 1)
                Need(nr is 14 or 39 or 186 or 157 or 9 or 202 or 0, "hello_child_call_inventory");
            switch (nr)
            {
                case 9:
                    Need(c is 0 or 1 or 3 && b > 0 && b <= 0x400000000 && d is 0x22 or 0x32 && unchecked((uint)e) == uint.MaxValue && f == 0 &&
                        value >= 0x100000000 && value < 0x1000000000, "hello_mmap");
                    if (t.Tid == 1 && t.Calls == 1) { Need(a == 0 && b == 488 && c == 3 && d == 0x22, "hello_initial_tcb"); mainAllocation = value; }
                    if (d == 0x32) { Need(a == value, "hello_fixed_address"); BorrowFree(a, HelloMemory.Round(b)); if (HelloMemory.Overlap(a, HelloMemory.Round(b), 0x400000000, 0x200000)) heapIntact = false; }
                    memory.Map(value, b, (uint)c, d == 0x32); break;
                case 10:
                    Need(c <= uint.MaxValue && a >= 4096 && (a & 4095) == 0 && b > 0 && b <= 0x1000000000, "hello_protection");
                    if (a < 0x100000000)
                    {
                        bool allowed = memory.CanProtectImage(a, b, (uint)c, out long error);
                        Need(value == Err(error), "hello_image_protect_result");
                        if (allowed)
                        {
                            if ((c == 3) != memory.Covered(a, HelloMemory.Round(b), 3)) BorrowFree(a, HelloMemory.Round(b));
                            memory.Protect(a, b, (uint)c);
                        }
                    }
                    else { Need(c is 0 or 1 or 3 && value == 0, "hello_protect_result"); BorrowFree(a, HelloMemory.Round(b)); memory.Protect(a,b,(uint)c); }
                    break;
                case 11:
                    Need(value == 0 && a >= 0x100000000, "hello_unmap"); BorrowFree(a, HelloMemory.Round(b));
                    if (HelloMemory.Overlap(a, HelloMemory.Round(b), 0x400000000, 0x200000)) heapIntact = false;
                    memory.Unmap(a,b); break;
                case 12:
                    Need(t.Tid == 1, "hello_brk_owner");
                    if (a == 0) Need(value == currentBreak, "hello_brk_query");
                    else if (heapIntact && a >= 0x400000000 && a <= 0x400200000)
                    { Need(value == a, "hello_brk_growth"); memory.Protect(0x400000000,0x200000,0); memory.Protect(0x400000000, HelloMemory.Round(a - 0x400000000),3, true); currentBreak = a; }
                    else Need(value == currentBreak, "hello_brk_failure");
                    break;
                case 25:
                    Need(t.Tid == 1 && b == 4096 && c == 8192 && d == 0 && (a & 4095) == 0 && a >= t.Bottom-4096 && a < t.Top,
                        "hello_stack_probe");
                    bool mapped = memory.Covered(a,4096,0);
                    Need(value == Err(mapped ? -12 : -14), "hello_stack_probe_result"); if (!mapped) stackGuardProbe = true;
                    break;
                case 28:
                    Need(value == 0 && c is 4 or 8 or 16 or 17 && a >= 0x100000000 && (a & 4095) == 0 &&
                        memory.Covered(a,HelloMemory.Round(b),0), "hello_advice");
                    if (c is 4 or 8) BorrowFree(a,HelloMemory.Round(b)); break;
                case 158:
                    Need(t.Tid == 1 && a == 0x1002 && b == mainAllocation + 0x120 && memory.Covered(b,8,1) && value == 0 && t.Fs == 0,
                        "hello_main_fs"); t.Fs = b; break;
                case 218: Need(t.Tid == 1 && a == 0x53a370 && memory.Covered(a,4,3) && value == 1, "hello_clear_tid_registration"); break;
                case 39: Need(value == 1, "hello_pid"); break;
                case 186: Need(value == (ulong)t.Tid, "hello_tid"); break;
                case 13:
                    Need(t.Tid == 1 && a is 2 or 3 or 8 or 11 or 13 or 18 or 35 && d == 8 && value == 0 &&
                        (b == 0 || memory.Covered(b,32,1)) && (c == 0 || memory.Covered(c,32,3)), "hello_sigaction");
                    if (b == 0) { Need(c != 0, "hello_action_query"); signalQueries.Add(a); }
                    else { Need(a == 13 || signalQueries.Contains(a), "hello_action_query_order"); signalInstalls.Add(a); }
                    break;
                case 14:
                    Need(a <= 2 && d == 8 && value == 0 && (b == 0 || memory.Covered(b,8,1)) && (c == 0 || memory.Covered(c,8,3)), "hello_sigmask");
                    if (a == 2 && b != 0) t.MaskSet = true; break;
                case 56:
                    Need(t.Tid == 1 && clones < 2 && a == 0x7d0f00 && value == (ulong)clones + 2 &&
                        memory.Covered(c,4,3) && d == 0x53a370 && memory.Covered(d,4,3), "hello_clone");
                    var stack = memory.DynamicWritable(b); Need(stack.End - stack.Start >= 1_572_864 && b >= stack.Start + 128 && b < stack.End &&
                        e > b && e + 512 <= stack.End && memory.Covered(stack.Start - 8192,8192,0) &&
                        !memory.Covered(stack.Start - 8192,8192,1), "hello_clone_stack_tls");
                    Need(threads.Skip(1).Where(x => x is not null).All(x => x!.Fs != e), "hello_private_tls");
                    clones++; int child = clones + 1; threads[child] = new(child,stack.Start,b,e); active++; ready.Enqueue(child); break;
                case 202:
                    Need((a & 3) == 0 && memory.Covered(a,4,3) && b is 128 or 129 && c <= uint.MaxValue, "hello_futex");
                    if (b == 128)
                    {
                        Need(d == 0 && (t.Tid == 1 ? a == 0x5394c0 && c == 0x80000002 : t.Tid == 2 && c == 2), "hello_untimed_wait");
                        if (t.Tid == 1) mainWait = true;
                        if (r["action"] == 3) { Need(value == 0, "hello_block_value"); t.Pending = 1; t.WaitAddress = a; action = 3; status = 9; }
                        else Need(t.Tid == 1 && value == Err(-11) && threads[2]?.StartupWake == true, "hello_no_fake_wait_success");
                    }
                    else
                    {
                        Need(t.Tid == 2 && a == 0x5394c0 && c == 1 && !t.StartupWake, "hello_finalizer_wake");
                        HelloThread main = threads[1]!; bool pending = main.Pending == 1 && !main.ReadyCompletion;
                        Need(value == (pending ? 1UL : 0UL), "hello_wake_count");
                        if (pending) { main.ReadyCompletion = true; ready.Enqueue(1); } t.StartupWake = true;
                    }
                    break;
                case 2: case 257:
                    Need(t.Tid == 1 && memory.Covered(nr == 2 ? a : b,1,1) && (nr != 257 || a == Err(-100)), "hello_open");
                    Need(((nr == 2 ? b : c) & ~0x98000UL) == 0, "hello_open_flags");
                    if (value == Err(-2)) break;
                    Need(value is >= 3 and < 16 && !files.Contains((int)value) && value != (ulong)pipeRead && value != (ulong)pipeWrite, "hello_open_result");
                    int lowest = Enumerable.Range(3,13).First(fd => !files.Contains(fd) && fd != pipeRead && fd != pipeWrite);
                    Need(value == (ulong)lowest, "hello_lowest_descriptor"); files.Add((int)value); break;
                case 3: Need(t.Tid == 1 && a <= int.MaxValue && files.Remove((int)a) && value == 0, "hello_close_file"); break;
                case 0:
                    Need(c > 0 && c <= 8192 && memory.Covered(b,c,3), "hello_read_buffer");
                    if (t.Tid == 3)
                    {
                        Need(a == (ulong)pipeRead && pipeWrite != 0 && c == 1 && value == 0 && t.MaskSet, "hello_signal_read");
                        t.Pending = 2; action = 3; status = 9;
                    }
                    else Need(t.Tid == 1 && a <= int.MaxValue && files.Contains((int)a) && value <= c, "hello_read_file");
                    break;
                case 72: Need(a <= int.MaxValue && files.Contains((int)a) && b == 2 && c == 1 && value == 0, "hello_fd_cloexec"); break;
                case 293:
                    Need(t.Tid == 1 && clones == 1 && pipeRead == 0 && files.Count == 0 && memory.Covered(a,8,3) && b == 0x80000 && value == 0,
                        "hello_signal_pipe"); pipeRead = 3; pipeWrite = 4; break;
                case 16:
                    Need(t.Tid == 1 && a is 0 or 1 && b is 0x5401 or 0x5413 && memory.Covered(c,b == 0x5401 ? 36UL : 8UL,3) && value == Err(-25),
                        "hello_not_tty"); break;
                case 1:
                    Need(t.Tid == 1 && a == 1 && writes < 3 && c == (writes == 0 ? 27UL : writes == 1 ? 19UL : 26UL) &&
                        value == c && memory.Covered(b,c,1), "hello_write_result"); StartupComplete(children:false); writes++; break;
                case 231:
                    Need(t.Tid == 1 && a == 0 && value == 0 && writes == 3, "hello_exit_group"); StartupComplete(children:true);
                    stopping = true; action = 2; status = 2;
                    foreach (int id in new[] { 2,3 }) { Need(!ready.Contains(id), "hello_cancel_parked"); ready.Enqueue(id); }
                    break;
                case 99: Need(memory.Covered(a,112,3) && value == 0, "hello_sysinfo"); break;
                case 228:
                    Need(value == Err(a is 1 or 6 ? memory.Covered(b,16,3) ? 0 : -14 : -22), "hello_clock_result"); break;
                case 204: Need(a is 0 or 1 && b is >= 8 and <= 4096 && memory.Covered(c,8,3) && value == 8, "hello_affinity"); break;
                case 302: Need(a is 0 or 1 && b == 9 && c == 0 && memory.Covered(d,16,3) && value == 0, "hello_limit"); break;
                case 318: Need(t.Tid == 1 && b is > 0 and <= 32 && c <= 1 && memory.Covered(a,b,3) && value == b && !entropy, "hello_entropy"); entropy = true; break;
                case 324:
                    Need(b == 0 && c == 0 && a is 0 or 8 or 16 && value == (a == 0 ? 0x18UL : 0UL) && (a != 8 || barrierRegistered), "hello_barrier");
                    if (a == 16) barrierRegistered = true; break;
                case 157: Need(t.Tid != 1 && a == 15 && memory.Covered(b,16,1) && value == Err(-38), "hello_name_unsupported"); break;
                case 137: Need(memory.Covered(a,1,1) && memory.Covered(b,120,3) && value == Err(-38), "hello_statfs_unsupported"); break;
                case 217: Need(a <= int.MaxValue && files.Contains((int)a) && c is > 0 and <= 8192 && memory.Covered(b,c,3) && value == Err(-38), "hello_getdents_unsupported"); break;
                case 239: Need(a == 0 && b == 0 && c == 0 && d == 0 && e == 0 && value == Err(-38), "hello_numa_unsupported"); break;
                default: throw new ToolFailure("hello_uninventoried_syscall_" + nr);
            }
            Need(r["action"] == action && r["status"] == status, "hello_call_outcome");
        }

        private void StartupComplete(bool children)
        {
            // Uncontended user-space mutex acquisition/release emits no futex
            // pair. Child creation need not give the signal child a timeslice
            // before stdout. Both actual child masks/waits remain mandatory
            // before exit_group can cancel them and begin off-stack cleanup.
            Need(clones == 2 && files.Count == 0 && pipeRead == 3 && pipeWrite == 4 && stackGuardProbe && entropy && barrierRegistered &&
                signalInstalls.SetEquals(new ulong[] {2,3,8,11,13,18,35}), "hello_runtime_startup");
            if(children)Need(threads[2] is { Pending:1, MaskSet:true } finalizer &&
                finalizer.StartupWake == mainWait &&
                threads[3] is { Pending:2, MaskSet:true }, "hello_runtime_children_and_startup");
        }

        private void BorrowFree(ulong start, ulong bytes)
        {
            foreach (HelloThread? t in threads.Skip(1)) if (t is not null && !t.Reaped)
                Need((t.Fs == 0 || !HelloMemory.Overlap(start,bytes,t.Fs,8)) &&
                    (t.Tid == 1 || !HelloMemory.Overlap(start,bytes,t.Bottom,t.Top-t.Bottom)) &&
                    (t.Pending != 1 || !HelloMemory.Overlap(start,bytes,t.WaitAddress,4)), "hello_mapping_borrow");
        }
    }

    private readonly record struct HelloRange(ulong Start, ulong End, uint Protection, bool Dynamic);

    private sealed class HelloMemory
    {
        private List<HelloRange> ranges = [];
        private readonly HelloRange[] image;
        private HelloRange? imageProtection;
        internal unsafe HelloMemory(byte[] elf)
        {
            fixed (byte* source = elf)
            {
                Need(LinuxElfLoader.Validate(source,(uint)elf.Length,64*1024*1024,1_572_864,out var plan) == LinuxElfLoadStatus.Ready, "hello_model_elf");
                for (uint i = 0; i < plan.LoadCount; i++)
                {
                    uint at = i * 5; ulong start = plan.Loads[at] & ~4095UL;
                    ulong flags = plan.Loads[at+4];
                    uint protection = ((flags & 4) != 0 ? 1U : 0) | ((flags & 2) != 0 ? 2U : 0) | ((flags & 1) != 0 ? 4U : 0);
                    ranges.Add(new(start, Round(plan.Loads[at]+plan.Loads[at+3]), protection, false));
                }
            }
            image = ranges.ToArray();
            ranges.Add(new(0x70000000-1_572_864,0x70000000,3,false));
            ranges.Add(new(0x400000000,0x400200000,0,true));
        }
        internal static ulong Round(ulong bytes) { Need(bytes <= 0x1000000000 - 4095,"hello_memory_extent"); return (bytes+4095)&~4095UL; }
        internal static bool Overlap(ulong a, ulong n, ulong b, ulong m) => (decimal)a+n > b && (decimal)b+m > a;
        internal bool Covered(ulong start, ulong bytes, uint protection)
        {
            if (bytes == 0 || start < 4096 || start >= 0x1000000000 || bytes > 0x1000000000-start) return false;
            ulong cursor = start, end = start + bytes;
            foreach (var r in EffectiveRanges().OrderBy(r=>r.Start))
            {
                if (r.End <= cursor) continue;
                if (r.Start > cursor || (r.Protection & protection) != protection) return false;
                cursor = Math.Min(end,r.End); if (cursor == end) return true;
            }
            return false;
        }
        internal HelloRange DynamicWritable(ulong address)
        {
            var found = ranges.Where(r=>r.Dynamic && r.Protection==3 && address>=r.Start && address<r.End).ToArray();
            Need(found.Length==1,"hello_dynamic_stack"); return found[0];
        }
        internal void Map(ulong start,ulong bytes,uint protection,bool replace)
        {
            bytes=Round(bytes); Dynamic(start,bytes);
            Need(replace || ranges.All(r=>!Overlap(start,bytes,r.Start,r.End-r.Start)),"hello_mmap_overlap");
            if(replace) Unmap(start,bytes);
            ranges.Add(new(start,start+bytes,protection,true)); Audit();
        }
        internal void Protect(ulong start,ulong bytes,uint protection,bool empty=false)
        {
            if (start < 0x100000000)
            {
                Need(CanProtectImage(start, bytes, protection, out _), "hello_image_protection");
                bytes = Round(bytes);
                HelloRange original = image.Single(r => start >= r.Start && start < r.End && bytes <= r.End - start);
                imageProtection = protection == original.Protection ? null : new(start, start + bytes, protection, false);
                return;
            }
            bytes=Round(bytes); if(empty && bytes==0) return;
            Dynamic(start,bytes); Need(Covered(start,bytes,0),"hello_protect_coverage");
            Rewrite(start,bytes,protection); Audit();
        }
        // Original ELF rows remain provenance. Only one exact non-executable
        // data window may differ; another window cannot supersede that owner.
        internal bool CanProtectImage(ulong start, ulong bytes, uint protection, out long error)
        {
            error = -22;
            if (protection is not (1 or 3) || start < 4096 || start >= 0x100000000 || (start & 4095) != 0 ||
                bytes == 0 || bytes > 0x1000000000 - 4095) return false;
            bytes = Round(bytes);
            if (!image.Any(r => r.Protection is 1 or 3 && start >= r.Start && start < r.End && bytes <= r.End - start)) return false;
            if (imageProtection is { } prior && (prior.Start != start || prior.End - prior.Start != bytes))
            { error = -12; return false; }
            error = 0; return true;
        }
        private IEnumerable<HelloRange> EffectiveRanges()
        {
            foreach (HelloRange r in ranges)
            {
                if (imageProtection is not { } window || r.Dynamic || !Overlap(r.Start, r.End-r.Start, window.Start, window.End-window.Start))
                { yield return r; continue; }
                if (r.Start < window.Start) yield return r with { End = window.Start };
                yield return window;
                if (r.End > window.End) yield return r with { Start = window.End };
            }
        }
        internal void Unmap(ulong start,ulong bytes)
        { bytes=Round(bytes); Dynamic(start,bytes); Rewrite(start,bytes,null); Audit(); }
        private static void Dynamic(ulong start,ulong bytes) => Need(start>=0x100000000 && start<0x1000000000 &&
            (start&4095)==0 && bytes>0 && bytes<=0x1000000000-start,"hello_dynamic_extent");
        private void Rewrite(ulong start,ulong bytes,uint? protection)
        {
            List<HelloRange> next=[]; ulong end=start+bytes;
            foreach(var r in ranges)
            {
                if(!Overlap(start,bytes,r.Start,r.End-r.Start)) { next.Add(r); continue; }
                Need(r.Dynamic,"hello_static_mutation");
                if(r.Start<start) next.Add(r with {End=start});
                if(protection.HasValue) next.Add(new(Math.Max(start,r.Start),Math.Min(end,r.End),protection.Value,true));
                if(r.End>end) next.Add(r with {Start=end});
            }
            ranges=next;
        }
        private void Audit() => Need(ranges.Where(r=>r.Dynamic).Sum(r=>(decimal)(r.End-r.Start))<=0x400000000 &&
            ranges.Where(r=>r.Dynamic && r.Protection!=0).Sum(r=>(decimal)(r.End-r.Start))<=LinuxMemoryServices.MaximumPoolPages*4096UL,"hello_memory_quota");
    }
}
