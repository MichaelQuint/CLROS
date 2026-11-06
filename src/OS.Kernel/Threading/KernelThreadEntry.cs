namespace OS.Kernel.Threading;
/// <summary>
/// Fixed Win64 first-entry/normal-return adapter for one bounded CPL0 worker.
/// Emits into unpublished caller storage; it neither maps nor executes it.
/// The owner retains the context, guarded stack, control and independently RX
/// admitted callbacks through off-stack exit. Entry/CapturedCall are IRQ-off;
/// this is not an interrupt-frame adapter or arbitrary C# thread startup.
/// </summary>

public static unsafe partial class KernelThreadEntry
{
    public const uint Bytes = 256, RunOffset = 0x60, ExitOffset = 0xa0, ExitResumeOffset = ExitOffset + 5;
    public const int CancelledExit = -1;
    /// <summary>Validate and dry encode before writing any output. Initial RIP
    /// names this trampoline, RSP is 8mod16 with at least 72 lower stack bytes,
    /// and Win64 x87/MXCSR controls are initialized with an empty x87 tag.
    /// The trampoline reserves 40 bytes: CALL sites are 16-aligned and provide
    /// 32-byte shadow space. Its abandoned frame is scrubbed only after the
    /// exit transfer. Rejection preserves output/context/control/stack bytes.
    /// Actual callback stack usage and native ABI still require original review.</summary>

    public static bool Emit(byte* output, uint capacity, Plan plan)
    {
        if (!Inputs(output, capacity, plan, true) || !Encode(null, plan, false))
            return false;
        return Encode(output, plan, false);
    }

    /// <summary>Read-only comparison with the retained plan. Matching bytes do
    /// not establish RX mappings, IRQ serialization or execution correctness.
    /// Initial may be updated to its admitted captured descriptor after transfer;
    /// its identity/resources stay pinned until comparison finishes.</summary>

    public static bool Verify(byte* output, uint capacity, Plan plan) => Inputs(output, capacity, plan, false) && Encode(output, plan, true);
    internal static bool Inputs(byte* output, uint capacity, Plan p, bool initialState)
    {
        var o = p.Owner;
        var d = p.Initial;
        if (ThreadContextSwitch.AdmitResumePlan(o, d) != ThreadContextSwitch.Status.Ok || capacity < Bytes || ((ulong)output & 15) != 0 || !Range((ulong)output, Bytes) || (p.CodeOffset & 15) != 0 || p.CodeOffset > o.CodeBytes || Bytes > o.CodeBytes - p.CodeOffset || !Contains(o.CodeStart, o.CodeBytes, p.Entry, 1) || !Contains(o.CodeStart, o.CodeBytes, p.ExitStub, ThreadContextSwitch.StubBytes) || !Contains(o.CodeStart, o.CodeBytes, p.RejectRip, 1) || !Range(p.Start, 1) || p.ControlBytes < 8 || (p.Control & 7) != 0 || !Contains(o.ContextArena, o.ContextArenaBytes, p.Control, p.ControlBytes))
            return false;
        ulong code = o.CodeStart + p.CodeOffset;
        if (initialState && (d.Kind != ThreadContextSwitch.ResumeKind.Entry || d.ResumeRip != code || d.Context->Rsp < d.StackGuardStart + ThreadContextSwitch.Page + 72))
            return false;
        if (Overlap(code, Bytes, p.ExitStub, ThreadContextSwitch.StubBytes) || Contains(code, Bytes, p.Entry, 1) || Contains(code, Bytes, p.Start, 1) || Contains(code, Bytes, p.RejectRip, 1) || Contains(p.ExitStub, ThreadContextSwitch.StubBytes, p.Entry, 1) || Contains(p.ExitStub, ThreadContextSwitch.StubBytes, p.Start, 1) || Contains(p.ExitStub, ThreadContextSwitch.StubBytes, p.RejectRip, 1) || Overlap(p.Control, p.ControlBytes, (ulong)d.Context, d.ContextBytes) || Overlap((ulong)output, Bytes, o.ContextArena, o.ContextArenaBytes) || Overlap((ulong)output, Bytes, d.StackGuardStart, d.StackBytes) || Overlap((ulong)output, Bytes, o.SwitchStackGuardStart, o.SwitchStackBytes))
            return false;
        byte* fx = (byte*)d.Context + X64ThreadContext.FxSaveOffset;
        return !initialState || (*(ushort*)fx == X64ThreadContext.InitialControlWord && fx[4] == 0 && *(uint*)(fx + 24) == X64ThreadContext.InitialMxcsr);
    }

    internal static bool Encode(byte* output, Plan p, bool compare)
    {
        ulong code = p.Owner.CodeStart + p.CodeOffset;
        ThreadContextSwitch.StubWriter w = new(output, code, compare, Bytes);
        w.Bytes(0xfc, 1);
        w.Bytes(0x28ec8348, 4); // CLD; SUB RSP,40 (8mod16 -> 0mod16).
        w.Immediate(1, p.Control);
        w.Immediate(2, p.Initial.Identity);
        w.Immediate(11, p.Start);
        w.Bytes(0xd3ff41, 3);
        w.Bytes(0x013c, 2); // CALL R11; CMP AL,1.
        w.Bytes(0x840f, 2);
        w.Displacement(code + RunOffset, 4);
        w.Bytes(0x023c, 2);
        w.Reject(p.RejectRip);
        w.Bytes(0xba, 1);
        w.Bytes(unchecked((uint)CancelledExit), 4);
        w.Jump(code + ExitOffset);
        w.Pad(RunOffset);
        w.Immediate(1, p.Argument);
        w.Immediate(11, p.Entry);
        w.Bytes(0xd3ff41, 3);
        w.Bytes(0xc289, 2);
        w.Jump(code + ExitOffset); // MOV EDX,EAX; no worker-stack cleanup.
        w.Pad(ExitOffset);
        w.Call(p.ExitStub);
        w.Jump(p.RejectRip);
        w.Pad();
        return w.Valid;
    }

    private static bool Range(ulong start, ulong bytes) => start != 0 && bytes != 0 && bytes - 1 <= ulong.MaxValue - start && Canonical(start) && Canonical(start + bytes - 1) && ((start ^ (start + bytes - 1)) & (1UL << 47)) == 0;
    private static bool Canonical(ulong value) => value >> 48 == ((value & (1UL << 47)) == 0 ? 0UL : 0xffffUL);
    private static bool Contains(ulong outer, ulong bytes, ulong inner, ulong count) => Range(inner, count) && inner >= outer && inner - outer <= bytes && count <= bytes - (inner - outer);
    private static bool Overlap(ulong a, ulong an, ulong b, ulong bn) => a <= b + (bn - 1) && b <= a + (an - 1);
}
