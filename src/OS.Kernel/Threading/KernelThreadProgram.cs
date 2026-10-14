namespace OS.Kernel.Threading;

/// <summary>
/// Fixed two-worker/supervisor mechanical program for M09-006. Encodes Win64
/// CALL continuations, normal/explicit exit, two sleeps per original worker,
/// join and a reused worker control. The trusted native owner supplies all
/// lifecycle decisions, CPU/mapping admission and IRQ serialization. This type
/// performs no transfer, allocation, MMIO or publication. No arbitrary worker,
/// preemption, runtime thread, SMP or GC claim follows from these bytes.
/// </summary>
public static unsafe class KernelThreadProgram
{
    public const uint Bytes = 8192, DriverA = 0, DriverB = 0x200, DriverResume = 26;
    public const uint EntryA = 0x400, EntryB = 0x500, WorkerA = 0x600, WorkerB = 0x700;
    public const uint Recovery = 0x800, Terminal = 0xb00, ReusedWorker = 0xa00;
    public const uint TimerHandler = 0xc00, IrqSaveDisable = 0xe00, IrqRestore = 0xe10, StackPointer = 0xe20, Idle = 0xe30;
    public const uint ToA = 0x1000, ToB = 0x1400, FromA = 0x1800, FromB = 0x1c00;
    public const uint Yield1Resume = 0x1a, Sleep1Resume = 0x2a, Yield2Resume = 0x3a, Sleep2Resume = 0x4a, JoinExitResume = 0x5a;
    public const uint ReusedJoin1Resume = 0x1a, ReusedJoin2Resume = 0x2a, ReusedSleepResume = 0x3a;

    /// <summary>Pinned current descriptor resources and callback extents.
    /// A/B entries use the same contexts as the corresponding switch endpoints.
    /// Admission receives Win64 byte(control, source, destination). Start is the
    /// KernelThreadEntry gate. Recovery freshly admits the supervisor and marks
    /// failure before restoration; Terminal never returns. The witness retains
    /// actual supervisor GPR/FX/FS/GS values before driver scratch instructions.
    /// Plan storage and callback addresses grant no execution authority.</summary>
    public struct Plan
    {
        public ThreadContextSwitch.Owner Owner;
        public ThreadContextSwitch.Descriptor Supervisor, A, B;
        public ThreadContextSwitch.StubPlan Stub;
        public KernelThreadEntry.Plan First, Second;
        public X64ThreadContext* Witness;
        public ulong RecoverCallback, TerminalCallback;
    }

    /// <summary>Dry validate every component before changing unpublished output.
    /// Both initial contexts must be complete entries. TimerHandler's 512 bytes
    /// are reserved for independently emitted/admitted X64TimerInterrupt code.
    /// Keep all ranges borrowed until the native owner revokes execution and
    /// releases every switch/IRQ reference. Host emission is not native proof.</summary>
    public static bool Emit(byte* output, uint capacity, Plan plan)
    {
        if (!Inputs(output, capacity, plan, true) || !Encode(null, plan, false)) return false;
        return Encode(output, plan, false);
    }

    /// <summary>Compare program except the independently owned timer-handler
    /// region. Retained descriptors may be updated to admitted captures or a
    /// republished first binding after reap. Verify IRQ bytes separately.</summary>
    public static bool Verify(byte* output, uint capacity, Plan plan) =>
        Inputs(output, capacity, plan, false) && Encode(output, plan, true);

    // Shared unpublished admission for the independent wait/TLS worker program.
    internal static bool Inputs(byte* output, uint capacity, Plan p, bool initial)
    {
        var o = p.Owner;
        if (capacity < Bytes || ((ulong)output & 15) != 0 || !Range((ulong)output, Bytes) || o.CodeBytes != Bytes ||
            ThreadContextSwitch.AdmitPlan(o) != ThreadContextSwitch.Status.Ok ||
            ThreadContextSwitch.AdmitResumePlan(o, p.Supervisor) != ThreadContextSwitch.Status.Ok ||
            p.Supervisor.Kind != ThreadContextSwitch.ResumeKind.CapturedCall ||
            (p.Supervisor.ResumeRip != o.CodeStart + DriverResume && p.Supervisor.ResumeRip != o.CodeStart + DriverB + DriverResume) ||
            !Range(p.RecoverCallback, 1) || !Range(p.TerminalCallback, 1) ||
            Contains(o.CodeStart, Bytes, p.RecoverCallback, 1) || Contains(o.CodeStart, Bytes, p.TerminalCallback, 1) ||
            p.Stub.RejectRip != o.CodeStart + Recovery ||
            !Contains(o.ContextArena, o.ContextArenaBytes, (ulong)p.Witness, X64ThreadContext.Bytes) || ((ulong)p.Witness & 15) != 0 ||
            !EntryMatches(p, p.First, p.A, EntryA, FromA) || !EntryMatches(p, p.Second, p.B, EntryB, FromB) ||
            !KernelThreadEntry.Inputs(output + EntryA, 256, p.First, initial) ||
            !KernelThreadEntry.Inputs(output + EntryB, 256, p.Second, initial)) return false;
        ulong witness = (ulong)p.Witness;
        if (Overlap(witness, X64ThreadContext.Bytes, p.Stub.CallbackContext, p.Stub.CallbackBytes) ||
            Overlap(witness, X64ThreadContext.Bytes, p.Stub.HelperFx, 512) ||
            Overlap(witness, X64ThreadContext.Bytes, p.Stub.JumpSlot, 8)) return false;
        for (uint i = 0; i < 4; i++)
        {
            var from = i < 2 ? p.Supervisor : i == 2 ? p.A : p.B;
            var to = i < 2 ? i == 0 ? p.A : p.B : p.Supervisor;
            var stub = p.Stub; stub.CodeOffset = ToA + i * ThreadContextSwitch.StubBytes;
            if (!ThreadContextSwitch.StubInputs(output + stub.CodeOffset, 1024, o, from, to, stub, true) ||
                Overlap(witness, X64ThreadContext.Bytes, (ulong)from.Context, from.ContextBytes) ||
                Overlap(witness, X64ThreadContext.Bytes, (ulong)to.Context, to.ContextBytes)) return false;
        }
        return !Overlap((ulong)output, Bytes, o.ContextArena, o.ContextArenaBytes) &&
            !Overlap((ulong)output, Bytes, o.SwitchStackGuardStart, o.SwitchStackBytes) &&
            !Overlap((ulong)output, Bytes, p.Supervisor.StackGuardStart, p.Supervisor.StackBytes) &&
            !Overlap((ulong)output, Bytes, p.A.StackGuardStart, p.A.StackBytes) &&
            !Overlap((ulong)output, Bytes, p.B.StackGuardStart, p.B.StackBytes);
    }
    private static bool EntryMatches(Plan p, KernelThreadEntry.Plan entry, ThreadContextSwitch.Descriptor d, uint offset, uint back) =>
        SameOwner(entry.Owner, p.Owner) &&
        entry.CodeOffset == offset && entry.Initial.Identity == d.Identity && entry.Initial.Context == d.Context &&
        entry.Initial.ContextBytes == d.ContextBytes && entry.Initial.StackGuardStart == d.StackGuardStart && entry.Initial.StackBytes == d.StackBytes &&
        entry.Control == p.Stub.CallbackContext && entry.ControlBytes == p.Stub.CallbackBytes &&
        entry.ExitStub == p.Owner.CodeStart + back && entry.RejectRip == p.Stub.RejectRip &&
        (entry.Entry == p.Owner.CodeStart + (offset == EntryA ? WorkerA : WorkerB) || offset == EntryA && entry.Entry == p.Owner.CodeStart + ReusedWorker);

    internal static bool SameOwner(ThreadContextSwitch.Owner a, ThreadContextSwitch.Owner b) =>
        a.Initialized == b.Initialized && a.GuardsVerified == b.GuardsVerified && a.MappingsVerified == b.MappingsVerified &&
        a.Key == b.Key && a.CurrentKey == b.CurrentKey && a.Root == b.Root && a.CurrentRoot == b.CurrentRoot &&
        a.KernelGs == b.KernelGs && a.CurrentKernelGs == b.CurrentKernelGs && a.Cr0 == b.Cr0 && a.Cr4 == b.Cr4 && a.Efer == b.Efer &&
        a.Flags == b.Flags && a.CpuCount == b.CpuCount && a.BasicEdx == b.BasicEdx && a.ExtendedEdx == b.ExtendedEdx &&
        a.ObservedMxcsrMask == b.ObservedMxcsrMask && a.Cs == b.Cs && a.Ss == b.Ss && a.Tr == b.Tr && a.Fs == b.Fs && a.Gs == b.Gs &&
        a.ContextArena == b.ContextArena && a.ContextArenaBytes == b.ContextArenaBytes && a.CodeStart == b.CodeStart && a.CodeBytes == b.CodeBytes &&
        a.SwitchStackGuardStart == b.SwitchStackGuardStart && a.SwitchStackBytes == b.SwitchStackBytes;

    internal static bool EncodeInfrastructure(byte* output, Plan p, bool compare) => Encode(output, p, compare, false);

    private static bool Encode(byte* output, Plan p, bool compare, bool workers = true)
    {
        ulong code = p.Owner.CodeStart;
        for (uint i = 0; i < 2; i++)
        {
            uint offset = i == 0 ? DriverA : DriverB;
            ThreadContextSwitch.StubWriter w = new(output == null ? null : output + offset, code + offset, compare, 512);
            w.Bytes(0xfa1e0ff3, 4); w.Bytes(0x9c, 1); w.Bytes(0x57565553, 4); w.Bytes(0x5741564155415441, 8);
            w.Bytes(0x20ec8348, 4); w.Call(code + (i == 0 ? ToA : ToB));
            ThreadContextSwitch.WriteWitness(ref w, (ulong)p.Witness, code + offset + DriverResume);
            w.Bytes(0x20c48348, 4); w.Bytes(0x5c415d415e415f41, 8); w.Bytes(0x5b5d5e5f, 4); w.Bytes(0x9d, 1);
            w.Bytes(0x00000001b8, 5); w.Bytes(0xc3, 1); w.Pad(); if (!w.Valid) return false;
        }
        if (!KernelThreadEntry.Encode(output == null ? null : output + EntryA, p.First, compare) ||
            !KernelThreadEntry.Encode(output == null ? null : output + EntryB, p.Second, compare)) return false;
        for (uint i = 0; workers && i < 3; i++)
        {
            uint offset = i == 0 ? WorkerA : i == 1 ? WorkerB : ReusedWorker;
            ThreadContextSwitch.StubWriter w = new(output == null ? null : output + offset, code + offset, compare, 256);
            w.Bytes(0x57565553, 4); w.Bytes(0x5741564155415441, 8); w.Bytes(0x28ec8348, 4);
            uint count = i == 2 ? 3U : 5U;
            for (uint step = 0; step < count; step++)
            {
                w.Pad(0x10 + step * 0x10);
                uint value = i == 2 ? step == 2 ? 1U : 0 : step == 1 ? i == 0 ? 2U : 3U : step == 3 ? 2U : i == 1 && step == 4 ? 23U : 0;
                w.Bytes(0xba, 1); w.Bytes(value, 4); w.Call(code + (i == 1 ? FromB : FromA));
                w.Jump(code + offset + 0x10 + (step + 1) * 0x10);
            }
            w.Pad(0x10 + count * 0x10);
            if (i == 1) w.Jump(code + Recovery); // Explicit exit must never resume here.
            else
            {
                w.Bytes(0x28c48348, 4); w.Bytes(0x5c415d415e415f41, 8); w.Bytes(0x5b5d5e5f, 4);
                w.Bytes(0xb8, 1); w.Bytes(i == 0 ? 17U : uint.MaxValue, 4); w.Bytes(0xc3, 1);
            }
            w.Pad(); if (!w.Valid) return false;
        }
        ThreadContextSwitch.StubWriter recovery = new(output == null ? null : output + Recovery, code + Recovery, compare, 512);
        recovery.Bytes(0xfc, 1); recovery.Fx(true, p.Stub.HelperFx);
        recovery.Immediate(1, p.Stub.CallbackContext); recovery.Immediate(2, (ulong)p.Supervisor.Context); recovery.Immediate(11, p.RecoverCallback);
        recovery.Bytes(0x20ec8348, 4); recovery.Bytes(0xd3ff41, 3); recovery.Bytes(0x20c48348, 4); recovery.Bytes(0x013c, 2); recovery.Reject(code + Terminal);
        ThreadContextSwitch.Restore(ref recovery, (ulong)p.Supervisor.Context, p.Stub.JumpSlot, code + Terminal);
        recovery.Pad(); if (!recovery.Valid) return false;
        ThreadContextSwitch.StubWriter terminal = new(output == null ? null : output + Terminal, code + Terminal, compare, 256);
        terminal.Bytes(0xfafc, 2); terminal.Fx(true, p.Stub.HelperFx); terminal.Immediate(1, p.Stub.CallbackContext); terminal.Immediate(11, p.TerminalCallback);
        terminal.Bytes(0x20ec8348, 4); terminal.Bytes(0xd3ff41, 3); terminal.Bytes(0x0b0f, 2); terminal.Pad(); if (!terminal.Valid) return false;
        if (!compare && output != null) for (uint i = TimerHandler; i < TimerHandler + 512; i++) output[i] = 0xcc;
        ThreadContextSwitch.StubWriter helpers = new(output == null ? null : output + IrqSaveDisable, code + IrqSaveDisable, compare, 512);
        helpers.Bytes(0xc3fa589c, 4); helpers.Pad(0x10); // PUSHFQ; POP RAX; CLI; RET.
        helpers.Bytes(0xc39d51, 3); helpers.Pad(0x20); // PUSH RCX; POPFQ; RET.
        helpers.Bytes(0xe08948, 3); helpers.Bytes(0x08c08348, 4); helpers.Bytes(0xc3, 1); helpers.Pad(0x30);
        helpers.Bytes(0xfaf4fb, 3); helpers.Bytes(0x00000001b8, 5); helpers.Bytes(0xc3, 1); helpers.Pad(); if (!helpers.Valid) return false;
        for (uint i = 0; i < 4; i++)
        {
            var stub = p.Stub; stub.CodeOffset = ToA + i * ThreadContextSwitch.StubBytes;
            var from = i < 2 ? p.Supervisor : i == 2 ? p.A : p.B;
            var to = i < 2 ? i == 0 ? p.A : p.B : p.Supervisor;
            ThreadContextSwitch.StubWriter w = new(output == null ? null : output + stub.CodeOffset, code + stub.CodeOffset, compare);
            if (!ThreadContextSwitch.EncodeStub(ref w, p.Owner, from, to, stub)) return false;
        }
        return true;
    }
    private static bool Range(ulong start, ulong bytes) => start != 0 && bytes != 0 && bytes - 1 <= ulong.MaxValue - start &&
        (start >> 48 == ((start & (1UL << 47)) == 0 ? 0UL : 0xffffUL)) &&
        ((start + bytes - 1) >> 48 == (((start + bytes - 1) & (1UL << 47)) == 0 ? 0UL : 0xffffUL)) &&
        ((start ^ (start + bytes - 1)) & (1UL << 47)) == 0;
    private static bool Contains(ulong outer, ulong bytes, ulong inner, ulong count) => Range(inner, count) && inner >= outer &&
        inner - outer <= bytes && count <= bytes - (inner - outer);
    private static bool Overlap(ulong a, ulong an, ulong b, ulong bn) => a <= b + (bn - 1) && b <= a + (an - 1);
}
