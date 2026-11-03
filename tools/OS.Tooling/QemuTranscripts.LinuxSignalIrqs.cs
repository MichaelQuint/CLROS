using System.Runtime.InteropServices;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Manifest;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    // Dynamic timing, exact ownership: every selected generation must end in
    // one real E1 or ending-excursion drain, then its matching supervisor row.
    // No ignored IRQs, synthetic syscall completions, idle ticks or context reuse.
    private static void ValidateLinuxIrqSignalBody(string[] rows, byte[] package)
    {
        const ulong first = 0x100000001, second = 0x100000002;
        LinuxThreadExpected[] parent = [new(39),new(186),new(158,0x1002,0x402000),new(158,0x1001,0x402100),
            new(13,35,0x401100,0,8),new(131,0x401120),new(56,0x7d0f00,0x406000,0x4011b8,0x4011bc,0x403000),
            new(186),new(14,2,0,0x401158,8),new(1,1,0x4001a0,15),new(15,1,0x4001a0,15),new(1,1,0x400180,15),new(231,42)];
        LinuxThreadExpected[] child = [new(158, 0x1001, 0x403100), new(39), new(186), new(234, 1, 1, 35), new(60, 7)];
        var parentInstructions = LinuxSignalProofFixture.ExpectedIrqInvocations(false);
        var childInstructions = LinuxSignalProofFixture.ExpectedIrqInvocations(true);
        var points = LinuxSignalProofFixture.ExpectedIrqPoints().ToDictionary(p => (p.Child, p.Rip));
        ulong activationLoop = points.Values.Single(p => p.Loop).Rip;
        byte[] elf = LinuxSignalProofFixture.CreateElf(LinuxSignalProofFixture.IrqVariant);
        Need(parentInstructions.Count == parent.Length && childInstructions.Count == child.Length, "m16_009_irq_call_inventory");
        ulong initialStack = LinuxIrqInitialStack(package), clock = 0, epoch = 0, transfers = 0;
        int position = 0, parentCalls = 0, childCalls = 0, active = 1, reaped = 0, loopCaptures = 0, userIrqs = 0, drains = 0;
        bool cloned = false, queued = false, restored = false, childReaped = false, parentReaped = false, lastParentLoop = false;
        Queue<ulong> ready = new();
        ready.Enqueue(first);
        string Take()
        {
            Need(position < rows.Length, "m16_009_irq_truncated");
            return rows[position++];
        }
        void Exact(string value) => Need(Take() == value, "m16_009_irq_record_order");
        Dictionary<string, ulong> Row(string prefix, params string[] keys) => ReadRecord(Take(), prefix, keys);
        bool Starts(string prefix) => position < rows.Length && rows[position].StartsWith(prefix, StringComparison.Ordinal);

        while (ready.Count != 0)
        {
            Need(epoch < 128 && !parentReaped, "m16_009_irq_dispatch_bound");
            ulong identity = ready.Dequeue();
            bool isChild = identity == second;
            Need(identity is first or second && (!isChild || cloned && !childReaped), "m16_009_irq_selected_generation");
            bool exited = false;
            while (Starts("M16_008_CALL ") || Starts("M16_005_WRITE "))
            {
                int ordinal = isChild ? childCalls : parentCalls;
                var expected = isChild ? child : parent;
                var instructions = isChild ? childInstructions : parentInstructions;
                Need(ordinal < expected.Length && !exited, "m16_009_irq_extra_call");
                var call = expected[ordinal];
                var instruction = instructions[ordinal];
                if (call.Nr == 1)
                    Exact("M16_005_WRITE fd=1 bytes=15 data=" + (ordinal == 9 ? "M16_HANDLER_OK!" : "M16_SIGNALS_OK!"));
                var row = Row("M16_008_CALL", "identity:dec", "nr:dec", "a0:dec", "a1:dec", "a2:dec", "a3:dec", "a4:dec", "a5:dec",
                    "rip:dec", "rsp:dec", "flags:dec", "fs:dec", "gs:dec", "clock:dec", "valueBits:dec", "action:dec", "status:dec");
                ulong rip = instruction.ReturnRip;
                Need(instruction.Number == call.Nr && row["identity"] == identity && row["nr"] == call.Nr &&
                    row["a0"] == call.A0 && row["a1"] == call.A1 && row["a2"] == call.A2 && row["a3"] == call.A3 &&
                    row["a4"] == call.A4 && row["a5"] == 0 && row["rip"] == rip && rip is >= 0x400202 and < 0x400f00 &&
                    elf[(int)(rip - 0x400002)] == 0x0f && elf[(int)(rip - 0x400001)] == 5, "m16_009_irq_call_binding");
                bool handler = !isChild && ordinal is >= 7 and <= 10;
                Need(instruction.Handler == handler && (!handler || queued) && (!isChild || !childReaped), "m16_009_irq_handler_order");
                ulong rsp = isChild ? 0x406000UL : ordinal == 10 ? 0x406c40UL : handler ? 0x406c38UL : 0x405000UL;
                ulong fs = isChild ? 0x403000UL : ordinal < 2 ? 0UL : 0x402000UL;
                ulong gs = isChild ? 0x403100UL : ordinal < 3 ? 0UL : 0x402100UL;
                Need(row["rsp"] == rsp && row["fs"] == fs && row["gs"] == gs && row["clock"] == clock &&
                    LinuxSyscallGateway.ValidateUserFlags(row["flags"], true) && (row["flags"] & 0x400) == 0,
                    "m16_009_irq_call_envelope");
                exited = ordinal == expected.Length - 1;
                ulong action = exited ? isChild ? 4UL : 2UL : call.Nr == 15 ? 5UL : 1UL;
                ulong value = isChild ? ordinal == 2 ? 2UL : ordinal == 1 ? 1UL : 0 :
                    ordinal is 0 or 1 or 7 ? 1UL : ordinal == 6 ? 2UL : call.Nr == 1 ? 15UL : call.Nr == 15 ? 0x12345678UL : 0;
                Need(row["action"] == action && row["valueBits"] == value && row["status"] == (exited ? 2UL : 1UL), "m16_009_irq_call_outcome");
                if (!isChild && ordinal == 6)
                {
                    Need(!cloned && active == 1, "m16_009_irq_clone_once");
                    cloned = true;
                    active++;
                    ready.Enqueue(second);
                }
                if (isChild && ordinal == 3)
                {
                    Need(!queued && parentCalls == 7 && lastParentLoop, "m16_009_irq_activation_requires_captured_spin");
                    queued = true;
                }
                if (!isChild && ordinal == 10)
                    restored = true;
                if (!isChild && ordinal >= 11)
                    Need(restored && queued, "m16_009_irq_no_signal_bypass");
                if (!isChild && exited)
                    Need(childReaped, "m16_009_irq_join_before_group_exit");
                if (isChild)
                    childCalls++;
                else
                    parentCalls++;
            }

            if (!exited)
            {
                var irq = Row("M16_009_IRQ", "identity:dec", "epoch:dec", "nanos:dec", "rip:dec", "rsp:dec", "flags:dec", "fs:dec", "gs:dec", "valueBits:dec");
                Need(irq["identity"] == identity && irq["epoch"] == epoch + 1 && irq["nanos"] >= clock && irq["nanos"] <= long.MaxValue &&
                    points.TryGetValue((isChild, irq["rip"]), out var point), "m16_009_irq_capture_identity");
                point = points[(isChild, irq["rip"])];
                Need(point.Calls == (isChild ? childCalls : parentCalls) &&
                    point.Handler == (!isChild && queued && parentCalls is >= 7 and < 11) &&
                    irq["rsp"] == (point.Rsp == 0 ? initialStack : point.Rsp) && irq["fs"] == point.Fs && irq["gs"] == point.Gs &&
                    LinuxSyscallGateway.ValidateUserFlags(irq["flags"], true) && (irq["flags"] & 0x400) == (point.Direction ? 0x400UL : 0),
                    "m16_009_irq_instruction_envelope");
                if (!isChild)
                {
                    lastParentLoop = point.Loop;
                    if (point.Loop)
                    {
                        Need(parentCalls == 7 && !queued && irq["rip"] == activationLoop &&
                            irq["valueBits"] == 0x1122334400000000UL, "m16_009_irq_full_seed_capture");
                        loopCaptures++;
                    }
                }
                clock = irq["nanos"];
                epoch++;
                userIrqs++;
                ready.Enqueue(identity);
            }
            else
            {
                var drain = Row("M16_009_DRAIN", "identity:dec", "epoch:dec", "nanos:dec");
                Need(drain["identity"] == identity && drain["epoch"] == epoch + 1 && drain["nanos"] >= clock && drain["nanos"] <= long.MaxValue &&
                    (isChild || drain["nanos"] == clock), "m16_009_irq_ending_checkpoint");
                clock = drain["nanos"];
                epoch++;
                drains++;
                var exit = Row("M16_008_EXIT", "identity:dec", "clearTidResult:dec");
                Need(exit["identity"] == identity && exit["clearTidResult"] == 0, "m16_009_irq_clear_tid");
            }
            transfers += 2;
            var supervisor = Row("M16_008_SUPERVISOR", "identity:dec", "action:dec", "transfers:dec", "ready:dec", "active:dec");
            Need(supervisor["identity"] == identity && supervisor["action"] == (exited ? isChild ? 4UL : 2UL : 1UL) &&
                supervisor["transfers"] == transfers && supervisor["ready"] == (ulong)ready.Count && supervisor["active"] == (ulong)active,
                "m16_009_irq_supervisor_handoff");
            if (exited)
            {
                const string suffix = " context=zero stack=zero slot=zero";
                string line = Take();
                Need(line.EndsWith(suffix, StringComparison.Ordinal), "m16_009_irq_reap_scrub");
                var reap = ReadRecord(line[..^suffix.Length], "M16_008_REAP", "old:dec", "next:dec");
                Need(reap["old"] == identity && reap["next"] == identity + (1UL << 32), "m16_009_irq_reap_generation");
                if (isChild)
                    childReaped = true;
                else
                    parentReaped = true;
                active--;
                reaped++;
            }
        }
        Need(parentCalls == 13 && childCalls == 5 && queued && restored && childReaped && parentReaped &&
            reaped == 2 && active == 0 && drains == 2 && userIrqs > 0 && loopCaptures > 0, "m16_009_irq_complete_protocol");
        const string cleanup = " userReserved=0 userBacked=0 process=drained timer=restored descriptors=closed guard=detached pool=scrubbed initialStack=zero reservation=retained";
        string endLine = Take();
        Need(endLine.EndsWith(cleanup, StringComparison.Ordinal), "m16_009_irq_owner_cleanup");
        var end = ReadRecord(endLine[..^cleanup.Length], "M16_008_OWNER_END", "reaped:dec", "transfers:dec", "irqs:dec", "exit:dec");
        Need(end["reaped"] == 2 && end["transfers"] == transfers && end["irqs"] == epoch && end["exit"] == 42,
            "m16_009_irq_owner_accounting");
        Exact("M16_009_RESTORE root=true descriptors=true ss=true msrs=true fsGs=true fp=true stack=true");
        Exact("CLROS_M16_009_READY");
        Exact("TEST_PASS m16-009");
        Need(position == rows.Length, "m16_009_irq_extra_record");
    }

    // Only deterministic layout is used, as in the original startup validator.
    // Sentinel entropy and HWCAP0 are not guest entropy/CPUID observations.
    private static unsafe ulong LinuxIrqInitialStack(byte[] package)
    {
        var view = LinuxElfPackage.Validate(package);
        Need(view.Succeeded, "m16_009_irq_package");
        byte[] seed = Enumerable.Repeat((byte)1, 32).ToArray();
        byte* output = (byte*)NativeMemory.AlignedAlloc(view.Manifest.StackBytes, 4096);
        try
        {
            Need(output != null, "m16_009_irq_stack_allocation");
            fixed (byte* input = package)
            fixed (byte* random = seed)
            {
                Need(LinuxElfStartup.Build(input + view.ManifestOffset, view.ManifestBytes, input + view.PayloadOffset, view.PayloadBytes,
                    random, 32, output, view.Manifest.StackBytes, 0x70000000UL - view.Manifest.StackBytes, 0, out var plan) == LinuxElfStartupStatus.Ready,
                    "m16_009_irq_initial_stack");
                return plan.StackPointer;
            }
        }
        finally { NativeMemory.AlignedFree(output); }
    }
}
