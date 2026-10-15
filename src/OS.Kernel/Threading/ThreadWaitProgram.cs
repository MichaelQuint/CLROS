namespace OS.Kernel.Threading;

/// <summary>
/// Fixed mechanical wait/TLS workers over the existing two-worker lifecycle
/// driver, entry/return, recovery and capture/restore ABI. Each worker reports
/// four actions through its owned CALL stub, then returns normally. Optional
/// FS/GS loads make actual private TLS values observable in captured R12/R13.
/// The trusted native owner authenticates mappings, private spans, generations,
/// IRQ serialization and each scheduler decision; this emitter performs no
/// transfer, allocation, publication, MMIO or managed thread-static operation.
/// </summary>
public static unsafe class ThreadWaitProgram
{
    public const uint Bytes = KernelThreadProgram.Bytes, Actions = 4, FirstAction = 0x10, ActionStride = 0x30;
    public const uint ActionResume = FirstAction + 28, WorkerReturn = FirstAction + Actions * ActionStride;

    /// <summary>Validate all components before changing unpublished caller-owned
    /// code storage. TLS probing requires nonzero FS and user-GS bases; private
    /// span ownership and live guards remain separately admitted before execution.
    /// The 512-byte IRQ region is independently emitted and verified by its owner.</summary>
    public static bool Emit(byte* output, uint capacity, KernelThreadProgram.Plan plan, bool probeTls)
    {
        if (!Inputs(output, capacity, plan, probeTls, true) || !Encode(null, plan, probeTls, false)) return false;
        return Encode(output, plan, probeTls, false);
    }

    /// <summary>Compare every program byte except the independently owned IRQ
    /// region. Current captured/rebound descriptors are allowed; verification
    /// grants no execution or lifetime authority and changes no input.</summary>
    public static bool Verify(byte* output, uint capacity, KernelThreadProgram.Plan plan, bool probeTls) =>
        Inputs(output, capacity, plan, probeTls, false) && Encode(output, plan, probeTls, true);

    private static bool Inputs(byte* output, uint capacity, KernelThreadProgram.Plan p, bool tls, bool initial) =>
        KernelThreadProgram.Inputs(output, capacity, p, initial) &&
        (!tls || p.A.Context->FsBase != 0 && p.A.Context->UserGsBase != 0 && p.B.Context->FsBase != 0 && p.B.Context->UserGsBase != 0);

    private static bool Encode(byte* output, KernelThreadProgram.Plan p, bool tls, bool compare)
    {
        if (!KernelThreadProgram.EncodeInfrastructure(output, p, compare)) return false;
        ulong code = p.Owner.CodeStart;
        for (uint i = 0; i < 3; i++)
        {
            uint offset = i == 0 ? KernelThreadProgram.WorkerA : i == 1 ? KernelThreadProgram.WorkerB : KernelThreadProgram.ReusedWorker;
            uint back = i == 1 ? KernelThreadProgram.FromB : KernelThreadProgram.FromA;
            ThreadContextSwitch.StubWriter w = new(output == null ? null : output + offset, code + offset, compare, 256);
            w.Bytes(0x57565553, 4); w.Bytes(0x5741564155415441, 8); w.Bytes(0x28ec8348, 4);
            for (uint action = 0; action < Actions; action++)
            {
                w.Pad(FirstAction + action * ActionStride);
                if (tls)
                {
                    w.Bytes(0x25248b4c64, 5); w.Bytes(0, 4); // MOV R12,qword ptr FS:[0].
                    w.Bytes(0x252c8b4c65, 5); w.Bytes(0, 4); // MOV R13,qword ptr GS:[0].
                }
                else for (uint padding = 0; padding < 18; padding++) w.Bytes(0x90, 1);
                w.Bytes(0xba, 1); w.Bytes(action, 4); w.Call(code + back);
                w.Jump(code + offset + FirstAction + (action + 1) * ActionStride);
            }
            w.Pad(WorkerReturn);
            w.Bytes(0x28c48348, 4); w.Bytes(0x5c415d415e415f41, 8); w.Bytes(0x5b5d5e5f, 4);
            w.Bytes(0xb8, 1); w.Bytes(i == 1 ? 23U : 17U, 4); w.Bytes(0xc3, 1);
            w.Pad(); if (!w.Valid) return false;
        }
        return true;
    }
}
