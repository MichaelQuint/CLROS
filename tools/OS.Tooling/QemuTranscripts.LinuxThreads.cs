using OS.Kernel.Compatibility.LinuxX64;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    public static IReadOnlyList<string> M16LinuxThreadMarkers { get; } = Array.AsReadOnly<string>(
        ["TEST_START m16-008", "M16_006_POLICY", "M16_007_POLICY", "M16_005_ARMED", "M16_008_BOUND", "M16_008_CALL",
         "M16_008_SUPERVISOR", "M16_008_RESUME", "M16_008_EXIT", "M16_008_REAP", "M16_008_TICK", "M16_005_WRITE",
         "M16_008_OWNER_END", "M16_008_RESTORE", "CLROS_M16_008_READY", "TEST_PASS m16-008"]);

    /// <summary>Validate one exact mechanical thread tail against its complete
    /// immutable package, emitted syscall continuations and independent ABI
    /// vectors. The caller separately validates all parent/provenance records.
    /// Synthetic host controls establish parser behavior, never guest execution.</summary>
    public static void ValidateLinuxThreadTail(string[] tail, byte[] package, ulong startupUserRoot, ulong restoredKernelRoot)
    {
        if (tail is null || package is null) throw new ToolFailure("m16_008_tail_input");
        Need(package.AsSpan().SequenceEqual(LinuxThreadProofFixture.CreatePackage("Threads")), "m16_008_fixture_identity");
        Need(tail.Length >= 5 && tail.Length <= 256, "m16_008_tail_inventory");
        int position = 0;
        string Take() { Need(position < tail.Length, "m16_008_truncated"); return tail[position++]; }
        void Exact(string expected) => Need(Take() == expected, "m16_008_record_order_" + expected);
        Dictionary<string, ulong> Row(string prefix, params string[] fields) => ReadRecord(Take(), prefix, fields);
        Exact("TEST_START m16-008"); LinuxMemoryPolicy(Take());
        Exact("M16_007_POLICY descriptors=16 pipes=2 capacity=4096 scratch=8192 packageBytes=24576 package=declared-immutable proc=owned-snapshot stdin=eof stdoutStderr=serial sigpipe=ignored-mechanical blocking=scheduler");
        var armed = LinuxSyscallArm(Take());
        const string boundSuffix = " timer=idle-only clock=hpet-nanoseconds reuse=off-stack";
        string boundLine = Take(); Need(boundLine.EndsWith(boundSuffix, StringComparison.Ordinal), "m16_008_bound_policy");
        var bound = ReadRecord(boundLine[..^boundSuffix.Length], "M16_008_BOUND", "root:dec", "pool:dec", "workers:dec", "code:dec", "metadata:dec", "userImage:dec");
        Need(armed["root"] == startupUserRoot && bound["root"] == restoredKernelRoot, "m16_008_parent_roots");
        Need(bound["root"] is >= 4096 and <= 0xfffff000 && bound["root"] % 4096 == 0 &&
            bound["pool"] is >= 4096 and <= 0xfffc0000 && bound["pool"] % 4096 == 0 && bound["workers"] == 2 &&
            bound["code"] == 16384 && bound["metadata"] == 45056 && bound["userImage"] == 728, "m16_008_bound_geometry");
        DistinctExtents([(bound["root"], 4096), (armed["root"], 4096), (bound["pool"], 64 * 4096)], "m16_008_bound_roots_pool");

        // Independent Linux ABI vectors; fd values and PID/TIDs are bound to
        // actual earlier rows. The child has no replayed clone syscall row.
        LinuxThreadExpected[] parent =
        [
            new(39), new(186), new(158,0x1002,0x402000), new(14,2,0x401020,0,8), new(293,0x401000),
            new(56,0x7d0f00,0x406000,0x401008,0x40100c,0x403000), new(0,0,0x401078,4), new(39),
            new(158,0x1003,0x401048), new(14,2,0,0x401030,8), new(202,0x401010,129,1), new(202,0x40100c,0,1),
            new(202,0x401010,128), new(202,0x401014,128,0,0x401058), new(3), new(3), new(1,1,0x400180,15), new(231,42)
        ];
        LinuxThreadExpected[] child =
        [
            new(39), new(186), new(158,0x1003,0x401050), new(14,2,0,0x401038,8), new(14,2,0x401028,0x401040,8),
            new(14,2,0,0x401038,8), new(158,0x1002,0x403040), new(158,0x1003,0x401050),
            new(1,0,0x401070,4), new(202,0x401010,128), new(60)
        ];
        var parentRips = LinuxThreadProofFixture.ExpectedReturnRips("Threads");
        var childRips = LinuxThreadProofFixture.ExpectedReturnRips("Threads", child: true);
        byte[] elf = LinuxThreadProofFixture.CreateElf("Threads");
        Need(parentRips.Count == parent.Length && childRips.Count == child.Length, "m16_008_instruction_inventory");
        ulong parentIdentity = 0, childIdentity = 0, pid = 0, tid = 0, readFd = 0, writeFd = 0, clock = 0, timeoutClock = 0;

        void Call(bool isChild, int ordinal)
        {
            var row = Row("M16_008_CALL", "identity:dec", "nr:dec", "a0:dec", "a1:dec", "a2:dec", "a3:dec", "a4:dec", "a5:dec",
                "rip:dec", "rsp:dec", "flags:dec", "fs:dec", "gs:dec", "clock:dec", "valueBits:dec", "action:dec", "status:dec");
            var expected = (isChild ? child : parent)[ordinal];
            if (!isChild && ordinal == 0)
            {
                parentIdentity = row["identity"]; pid = row["valueBits"];
                Need(parentIdentity >> 32 == 1 && (uint)parentIdentity == 1 && pid is > 0 and < int.MaxValue, "m16_008_parent_identity");
            }
            if (!isChild && ordinal == 5)
            { tid = row["valueBits"]; Need(tid == pid + 1 && tid <= int.MaxValue, "m16_008_clone_tid"); }
            if (isChild && ordinal == 0)
            {
                childIdentity = row["identity"];
                Need(childIdentity >> 32 == 1 && (uint)childIdentity == 2 && childIdentity != parentIdentity, "m16_008_child_identity");
            }
            if (!isChild && ordinal == 6)
            { readFd = row["a0"]; Need(readFd is >= 3 and < 16, "m16_008_read_fd"); }
            if (isChild && ordinal == 8)
            { writeFd = row["a0"]; Need(writeFd is >= 3 and < 16 && writeFd != readFd, "m16_008_write_fd"); }
            ulong a0 = !isChild && (ordinal is 6 or 14) ? readFd : !isChild && ordinal == 15 ? writeFd :
                isChild && ordinal == 8 ? writeFd : expected.A0;
            ulong rip = (isChild ? childRips : parentRips)[ordinal];
            Need(row["identity"] == (isChild ? childIdentity : parentIdentity) && row["nr"] == expected.Nr &&
                row["a0"] == a0 && row["a1"] == expected.A1 && row["a2"] == expected.A2 && row["a3"] == expected.A3 &&
                row["a4"] == expected.A4 && row["a5"] == 0 && row["rip"] == rip && rip is >= 0x400202 and < 0x400f00 &&
                elf[(int)(rip - 0x400000 - 2)] == 0x0f && elf[(int)(rip - 0x400000 - 1)] == 5, "m16_008_call_binding");
            ulong fs = isChild ? ordinal < 6 ? 0x403000UL : 0x403040UL : ordinal < 2 ? 0UL : 0x402000UL;
            bool samplesClock = isChild ? ordinal == 9 : ordinal is 11 or 13;
            Need(row["rsp"] == (isChild ? 0x406000UL : 0x405000UL) && row["fs"] == fs && row["gs"] == 0 &&
                (row["flags"] & 2) != 0 && (row["flags"] & ~LinuxSyscallGateway.UserRflagsMask) == 0 &&
                row["clock"] >= clock && row["clock"] <= long.MaxValue && (samplesClock || row["clock"] == clock), "m16_008_thread_envelope");
            clock = row["clock"];
            bool blocked = isChild ? ordinal == 9 : ordinal is 6 or 11 or 13;
            bool exited = ordinal == (isChild ? child.Length : parent.Length) - 1;
            long value = isChild ? ordinal switch { 0 => (long)pid, 1 => (long)tid, 8 => 4, _ => 0 } :
                ordinal switch { 0 or 1 or 7 => (long)pid, 5 => (long)tid, 10 => 1, 12 => -11, 16 => 15, _ => 0 };
            Need(row["valueBits"] == unchecked((ulong)value) && row["action"] == (blocked ? 3UL : exited ? isChild ? 4UL : 2UL : 1UL) &&
                row["status"] == (blocked ? 9UL : exited ? 2UL : 1UL), "m16_008_call_outcome");
            if (!isChild && ordinal == 13)
            { timeoutClock = clock; Need(timeoutClock <= (ulong)long.MaxValue - 1_000_000, "m16_008_timeout_overflow"); }
        }
        void Calls(bool isChild, int first, int last) { for (int i = first; i <= last; i++) Call(isChild, i); }
        void Resume(ulong identity, long value)
        {
            var row = Row("M16_008_RESUME", "identity:dec", "valueBits:dec", "status:dec");
            Need(row["identity"] == identity && row["valueBits"] == unchecked((ulong)value) && row["status"] == 1, "m16_008_completion");
        }
        void Supervisor(ulong identity, ulong action, ulong transfers, ulong ready, ulong active)
        {
            var row = Row("M16_008_SUPERVISOR", "identity:dec", "action:dec", "transfers:dec", "ready:dec", "active:dec");
            Need(row["identity"] == identity && row["action"] == action && row["transfers"] == transfers &&
                row["ready"] == ready && row["active"] == active, "m16_008_supervisor_confirmation");
        }
        void Exit(ulong identity)
        {
            var row = Row("M16_008_EXIT", "identity:dec", "clearTidResult:dec");
            Need(row["identity"] == identity && row["clearTidResult"] == 0, "m16_008_clear_tid");
        }
        void Reap(ulong identity)
        {
            const string suffix = " context=zero stack=zero slot=zero";
            string line = Take(); Need(line.EndsWith(suffix, StringComparison.Ordinal), "m16_008_reap_scrub");
            var row = ReadRecord(line[..^suffix.Length], "M16_008_REAP", "old:dec", "next:dec");
            Need(row["old"] == identity && row["next"] == identity + (1UL << 32), "m16_008_reap_generation");
        }

        Calls(false, 0, 6); Supervisor(parentIdentity, 3, 2, 1, 2);
        Calls(true, 0, 9); Supervisor(childIdentity, 3, 4, 1, 2);
        Resume(parentIdentity, 4); Calls(false, 7, 11); Supervisor(parentIdentity, 3, 6, 1, 2);
        Resume(childIdentity, 0); Call(true, 10); Exit(childIdentity); Supervisor(childIdentity, 4, 8, 1, 2); Reap(childIdentity);
        Resume(parentIdentity, 0); Calls(false, 12, 13); Supervisor(parentIdentity, 3, 10, 0, 1);
        ulong ticks = 0;
        while (true)
        {
            var tick = Row("M16_008_TICK", "epoch:dec", "nanos:dec", "ready:dec");
            Need(++ticks <= 128 && tick["epoch"] == ticks && tick["nanos"] > clock && tick["nanos"] <= long.MaxValue &&
                tick["ready"] <= 1, "m16_008_tick_epoch");
            clock = tick["nanos"];
            if (tick["ready"] == 1)
            { Need(clock >= timeoutClock + 1_000_000, "m16_008_real_timeout"); break; }
            Need(clock < timeoutClock + 1_000_000, "m16_008_missed_timeout");
        }
        Resume(parentIdentity, -110); Calls(false, 14, 15);
        Exact("M16_005_WRITE fd=1 bytes=15 data=M16_THREADS_OK!"); Call(false, 16);
        Call(false, 17); Exit(parentIdentity); Supervisor(parentIdentity, 2, 12, 0, 1); Reap(parentIdentity);
        const string endSuffix = " process=drained timer=restored descriptors=closed guard=detached pool=scrubbed reservation=retained";
        string endLine = Take(); Need(endLine.EndsWith(endSuffix, StringComparison.Ordinal), "m16_008_owner_cleanup");
        var end = ReadRecord(endLine[..^endSuffix.Length], "M16_008_OWNER_END", "reaped:dec", "transfers:dec", "irqs:dec", "exit:dec");
        Need(end["reaped"] == 2 && end["transfers"] == 12 && end["irqs"] == ticks && end["exit"] == 42, "m16_008_owner_accounting");
        Exact("M16_008_RESTORE root=true descriptors=true ss=true msrs=true fsGs=true fp=true stack=true");
        Exact("CLROS_M16_008_READY"); Exact("TEST_PASS m16-008");
        Need(position == tail.Length, "m16_008_extra_record");
    }

    private readonly record struct LinuxThreadExpected(ulong Nr, ulong A0 = 0, ulong A1 = 0, ulong A2 = 0, ulong A3 = 0, ulong A4 = 0);
}
