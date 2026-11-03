using OS.Kernel.Compatibility.LinuxX64;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    public static IReadOnlyList<string> M16LinuxSignalMarkers
    {
        get;
    } = Array.AsReadOnly<string>(
        ["TEST_START m16-009", "M16_006_POLICY", "M16_007_POLICY", "M16_005_ARMED", "M16_008_BOUND", "M16_008_CALL",
         "M16_008_EXIT", "M16_008_SUPERVISOR", "M16_008_REAP", "M16_008_OWNER_END", "M16_009_RESTORE",
         "CLROS_M16_009_READY", "TEST_PASS m16-009"]);

    /// <summary>Validate the complete mechanical signal tail against canonical
    /// package bytes, actual instruction continuations and independent Linux ABI
    /// vectors. Parent startup/PCI/APIC/root provenance is checked separately by
    /// the full transcript caller. This accepts neither unchanged Hello nor
    /// upstream activation/suspension evidence. Synthetic rows test parsing,
    /// never handler execution. Any missing/extra/replayed row fails.</summary>
    public static void ValidateLinuxSignalTail(string[] tail, byte[] package, ulong startupUserRoot, ulong restoredKernelRoot)
    {
        if (tail is null || package is null)
            throw new ToolFailure("m16_009_tail_input");
        tail = ValidateLinuxCallOrdinals(tail);
        string variant = LinuxSignalProofFixture.PackageVariant(package);
        bool irq = LinuxSignalProofFixture.IsIrqVariant(variant);
        bool wait = LinuxSignalProofFixture.IsWaitVariant(variant), nested = LinuxSignalProofFixture.IsNestedVariant(variant);
        bool hardwareFault = LinuxSignalProofFixture.IsFaultVariant(variant);
        bool badRestorer = variant == "BadRestorer", badReturn = variant == "BadReturn";
        uint exit = LinuxSignalProofFixture.ExpectedExitCode(variant);
        var nestedCalls = nested ? LinuxSignalProofFixture.ExpectedNestedInvocations(variant) : null;
        int count = wait ? LinuxSignalProofFixture.ExpectedWaitInvocations(variant).Count + 19 +
            (variant == LinuxSignalProofFixture.StolenWaitVariant ? 1 : 0) :
            nested ? nestedCalls!.Count + 12 + nestedCalls.Count(i => i.Number == 1) :
            hardwareFault ? variant == "FaultFatal" ? 19 : variant == "FaultReturn" ? 25 : 30 : badRestorer ? 21 : badReturn ? 27 : 32;
        Need(irq ? tail.Length is >= 39 and <= 289 : tail.Length == count, "m16_009_tail_inventory");
        int position = 0;
        string Take()
        {
            Need(position < tail.Length, "m16_009_truncated");
            return tail[position++];
        }
        void Exact(string expected) => Need(Take() == expected, "m16_009_record_order_" + expected);
        Dictionary<string, ulong> Row(string prefix, params string[] fields) => ReadRecord(Take(), prefix, fields);
        Exact("TEST_START m16-009");
        LinuxMemoryPolicy(Take());
        Exact("M16_007_POLICY descriptors=16 pipes=2 capacity=4096 scratch=8192 packageBytes=" + (wait ? "40960" : "28672") + " package=declared-immutable proc=owned-snapshot stdin=eof stdoutStderr=serial sigpipe=ignored-mechanical blocking=scheduler");
        var armed = LinuxSyscallArm(Take());
        string boundSuffix = irq ? " timer=user-irq-and-idle clock=hpet-nanoseconds reuse=off-stack" :
            " timer=idle-only clock=hpet-nanoseconds reuse=off-stack";
        string line = Take();
        Need(line.EndsWith(boundSuffix, StringComparison.Ordinal), "m16_009_bound_policy");
        var bound = ReadRecord(line[..^boundSuffix.Length], "M16_008_BOUND", "root:dec", "pool:dec", "workers:dec", "code:dec", "metadata:dec", "userImage:dec");
        Need(armed["root"] == startupUserRoot && bound["root"] == restoredKernelRoot &&
            bound["root"] is >= 4096 and <= 0xfffff000 && bound["root"] % 4096 == 0 &&
            bound["pool"] >= 4096 && bound["pool"] <= 0x100000000UL - LinuxThreadProgram.SignalPoolPages * 4096UL && bound["pool"] % 4096 == 0 &&
            bound["workers"] == 2 && bound["code"] == LinuxThreadProgram.Bytes && bound["metadata"] == LinuxThreadProgram.MetadataPages * 4096UL && bound["userImage"] == 728,
            "m16_009_parent_geometry");
        DistinctExtents([(bound["root"], 4096), (armed["root"], 4096), (bound["pool"], LinuxThreadProgram.SignalPoolPages * 4096UL)], "m16_009_roots_pool");

        if (irq)
        {
            ValidateLinuxIrqSignalBody(tail[5..], package);
            return;
        }
        if (wait)
        {
            ValidateLinuxSignalWaitBody(tail[5..], variant);
            return;
        }

        ulong identity;
        if (nested)
            ValidateLinuxNestedSignalCalls(Take, variant, out identity);
        else if (hardwareFault)
            ValidateLinuxFaultSignalCalls(Take, variant, out identity);
        else
        {
            // Literal kernel ABI vectors are independent of the fixture's writer.
            // Only PID/TID are late-bound; handler/restorer stack geometry is fixed.
            LinuxThreadExpected[] expected = [
                new(39),new(186),new(158,0x1002,0x402000),new(13,10,0x401100,0,8),new(131,0x401120),
                new(14,2,0x401140,0,8),new(234,0,0,10),new(14,2,0,0x401150,8),new(14,2,0x401148,0,8),
                new(186),new(14,2,0,0x401158,8),new(131,0,0x4011a0),new(1,1,0x4001a0,15),new(15,1,0x4001a0,15),
                new(14,2,0,0x401150,8),new(131,0,0x401180),new(1,1,0x400180,15),new(231,42)];
            var instructions = LinuxSignalProofFixture.ExpectedInvocations(variant);
            byte[] elf = LinuxSignalProofFixture.CreateElf(variant);
            Need(instructions.Count == (badRestorer ? 9 : badReturn ? 14 : 18), "m16_009_call_inventory");
            ulong pid = 0, handlerRsp = variant == "UserStack" ? 0x404bb8UL : 0x406c38UL;
            identity = 0;
            for (int i = 0; i < instructions.Count; i++)
            {
                if (i is 12 or 16)
                    Exact("M16_005_WRITE fd=1 bytes=15 data=" + (i == 12 ? "M16_HANDLER_OK!" : "M16_SIGNALS_OK!"));
                var row = Row("M16_008_CALL", "identity:dec", "nr:dec", "a0:dec", "a1:dec", "a2:dec", "a3:dec", "a4:dec", "a5:dec",
                    "rip:dec", "rsp:dec", "flags:dec", "fs:dec", "gs:dec", "clock:dec", "valueBits:dec", "action:dec", "status:dec");
                if (i == 0)
                {
                    pid = row["valueBits"];
                    identity = row["identity"];
                    Need(pid is > 0 and < int.MaxValue && identity == 0x100000001, "m16_009_identity");
                }
                var call = expected[i];
                var instruction = instructions[i];
                ulong rip = instruction.ReturnRip, rsp = i == 13 ? handlerRsp + 8 : i is >= 9 and <= 12 ? handlerRsp : 0x405000;
                Need(instruction.Number == call.Nr && row["identity"] == identity && row["nr"] == call.Nr &&
                    row["a0"] == (i == 6 ? pid : call.A0) && row["a1"] == (i == 6 ? pid : call.A1) &&
                    row["a2"] == call.A2 && row["a3"] == call.A3 && row["a4"] == 0 && row["a5"] == 0 && row["rip"] == rip &&
                    rip is >= 0x400202 and < 0x400f00 && elf[(int)(rip - 0x400002)] == 0x0f && elf[(int)(rip - 0x400001)] == 5,
                    "m16_009_instruction_binding");
                Need(row["rsp"] == rsp && row["fs"] == (i < 2 ? 0UL : 0x402000UL) && row["gs"] == 0 && row["clock"] == 0 &&
                    (row["flags"] & 2) != 0 && (row["flags"] & ~LinuxSyscallGateway.UserRflagsMask) == 0 &&
                    (row["flags"] & 0x400) == (i == 8 ? 0x400UL : 0), "m16_009_register_envelope");
                ulong action = i == 17 || i == 13 && badReturn ? 2UL : i == 13 ? 5UL : 1UL;
                ulong value = i is 0 or 1 or 9 ? pid : i is 12 or 16 ? 15UL : i == 13 && !badReturn ? 0x12345678UL : 0;
                Need(row["valueBits"] == value && row["action"] == action && row["status"] == (action == 2 ? 2UL : 1UL), "m16_009_call_outcome");
            }
        }
        var exited = Row("M16_008_EXIT", "identity:dec", "clearTidResult:dec");
        Need(exited["identity"] == identity && exited["clearTidResult"] == 0, "m16_009_owned_exit");
        var supervisor = Row("M16_008_SUPERVISOR", "identity:dec", "action:dec", "transfers:dec", "ready:dec", "active:dec");
        Need(supervisor["identity"] == identity && supervisor["action"] == 2 && supervisor["transfers"] == 2 &&
            supervisor["ready"] == 0 && supervisor["active"] == 1, "m16_009_supervisor_confirmation");
        const string scrubSuffix = " context=zero stack=zero slot=zero";
        line = Take();
        Need(line.EndsWith(scrubSuffix, StringComparison.Ordinal), "m16_009_reap_scrub");
        var reap = ReadRecord(line[..^scrubSuffix.Length], "M16_008_REAP", "old:dec", "next:dec");
        Need(reap["old"] == identity && reap["next"] == identity + (1UL << 32), "m16_009_reap_generation");
        const string endSuffix = " userReserved=0 userBacked=0 process=drained timer=restored descriptors=closed guard=detached pool=scrubbed initialStack=zero reservation=retained";
        line = Take();
        Need(line.EndsWith(endSuffix, StringComparison.Ordinal), "m16_009_owner_cleanup");
        var end = ReadRecord(line[..^endSuffix.Length], "M16_008_OWNER_END", "reaped:dec", "transfers:dec", "irqs:dec", "exit:dec");
        Need(end["reaped"] == 1 && end["transfers"] == 2 && end["irqs"] == 0 && end["exit"] == exit, "m16_009_owner_accounting");
        Exact("M16_009_RESTORE root=true descriptors=true ss=true msrs=true fsGs=true fp=true stack=true");
        Exact("CLROS_M16_009_READY");
        Exact("TEST_PASS m16-009");
        Need(position == tail.Length, "m16_009_extra_record");
    }
}
