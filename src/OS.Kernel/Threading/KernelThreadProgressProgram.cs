namespace OS.Kernel.Threading;

/// <summary>
/// Fixed cooperative progress workers over the existing supervisor, entry/return
/// and capture/restore program. Each iteration checks an owned stop word, then
/// increments an owned counter once and yields once. The serialized native owner
/// retains code, control, contexts and guarded stacks through final restoration;
/// this emitter neither executes nor publishes code or schedules a worker.
/// Arbitrary workers, interrupts, SMP and general runtime threads are unsupported.
/// </summary>
public static unsafe class KernelThreadProgressProgram
{
    /// <summary>Program extent and worker-relative loop, captured CALL continuation
    /// and normal-return epilogue offsets. A/B/reused bodies share this layout.</summary>
    public const uint Bytes = KernelThreadProgram.Bytes, Loop = 0x10, YieldResume = 0x3a, WorkerReturn = 0x40;

    /// <summary>Validate and dry encode before changing unpublished output.
    /// Progress is an eight-byte-aligned ulong and stop a four-byte-aligned uint,
    /// disjoint and wholly inside retained callback-control storage. Their values
    /// are neither read nor changed here. Execution requires separate live CPU,
    /// IRQ-off, guard, mapping, callback and lifetime admission. Invalid inputs
    /// leave every byte unchanged; the IRQ region remains independently owned.</summary>
    public static bool Emit(byte* output, uint capacity, KernelThreadProgram.Plan plan, ulong progress, ulong stop)
    {
        if (!Inputs(output, capacity, plan, progress, stop, true) || !Encode(null, plan, progress, stop, false))
            return false;
        return Encode(output, plan, progress, stop, false);
    }

    /// <summary>Compare program bytes except the independently owned IRQ region.
    /// Captured/rebound descriptors are allowed. Control values may change while
    /// their addresses and ownership stay pinned; verification changes nothing
    /// and grants no execution, mapping or resource-release authority.</summary>
    public static bool Verify(byte* output, uint capacity, KernelThreadProgram.Plan plan, ulong progress, ulong stop) =>
        Inputs(output, capacity, plan, progress, stop, false) && Encode(output, plan, progress, stop, true);

    private static bool Inputs(byte* output, uint capacity, KernelThreadProgram.Plan p, ulong progress, ulong stop, bool initial) =>
        KernelThreadProgram.Inputs(output, capacity, p, initial) && (progress & 7) == 0 && (stop & 3) == 0 &&
        Contains(p.Stub.CallbackContext, p.Stub.CallbackBytes, progress, 8) &&
        Contains(p.Stub.CallbackContext, p.Stub.CallbackBytes, stop, 4) &&
        (progress <= stop ? stop - progress >= 8 : progress - stop >= 4);

    private static bool Contains(ulong start, uint bytes, ulong address, uint count) =>
        count <= bytes && address >= start && address - start <= bytes - count;

    private static bool Encode(byte* output, KernelThreadProgram.Plan p, ulong progress, ulong stop, bool compare)
    {
        if (!KernelThreadProgram.EncodeInfrastructure(output, p, compare, recoverySwitchStack: true))
            return false;
        ulong code = p.Owner.CodeStart;
        for (uint i = 0; i < 3; i++)
        {
            uint offset = i == 0 ? KernelThreadProgram.WorkerA : i == 1 ? KernelThreadProgram.WorkerB : KernelThreadProgram.ReusedWorker;
            uint back = i == 1 ? KernelThreadProgram.FromB : KernelThreadProgram.FromA;
            ThreadContextSwitch.StubWriter w = new(output == null ? null : output + offset, code + offset, compare, 256);
            w.Bytes(0x57565553, 4);
            w.Bytes(0x5741564155415441, 8);
            w.Bytes(0x28ec8348, 4);
            w.Immediate(0, stop);
            w.Bytes(0x003883, 3); // CMP dword ptr [RAX],0.
            w.Reject(code + offset + WorkerReturn); // JNE normal return before increment or transfer.
            w.Immediate(0, progress);
            w.Bytes(0x00ff48, 3); // INC qword ptr [RAX].
            w.Bytes(0xba, 1);
            w.Bytes(0, 4);
            w.Call(code + back);
            w.Jump(code + offset + Loop);
            w.Pad(WorkerReturn);
            w.Bytes(0x28c48348, 4);
            w.Bytes(0x5c415d415e415f41, 8);
            w.Bytes(0x5b5d5e5f, 4);
            w.Bytes(0xb8, 1);
            w.Bytes(17, 4);
            w.Bytes(0xc3, 1);
            w.Pad();
            if (!w.Valid)
                return false;
        }
        return true;
    }
}
