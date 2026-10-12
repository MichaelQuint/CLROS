namespace OS.Kernel.Compatibility.LinuxX64;

public struct X64LinuxUserTransitionPlan
{
    public ulong CodeAddress, ContextAddress, UserRoot, UserRip, UserRsp, UserFs, UserGs;
    public ulong Dispatcher, FaultReporter, CallbackContext, TssRsp0Address, OriginalGp, OriginalPf;
}

/// <summary>
/// C#-authored x64 bytes for one owned, IRQ-off, single-CPU CPL3 excursion.
/// This emitter supplies no execution authority. The native owner must prove
/// current CS=8/TR=24, descriptor identity, CPU admission, RX code and supervisor
/// RW/NX context/stack under both audited roots, an unmapped stack guard, immutable
/// user mappings, callback identity and original-state readback. FXSAVE64 covers
/// x87/SSE only; OSXSAVE and extended-state workloads are refused. The dispatcher
/// returns byte 1 only after LinuxSyscallGateway validates the captured return;
/// every other result terminates. Fault reporting returns to cleanup, never to
/// the faulting user. Kernel GP/PF frames go to the original terminal handlers.
/// The original TSS remains loaded; only its RSP0 is temporarily replaced.
/// </summary>
public static unsafe class X64LinuxUserTransition
{
    public const uint CodeBytes = 4096, DataPages = 20, DataBytes = DataPages * 4096;
    public const uint SyscallOffset = 0x600, GpOffset = 0xc00, PfOffset = 0xd00, ExitOffset = 0xe00;
    public const uint ObserveOffset = 0x400, ObservationWords = 25;
    public const uint GpPointerOffset = 0xff0, PfPointerOffset = 0xff8;
    public const uint FrameOffset = 0x100, KernelFxOffset = 0x200, UserFxOffset = 0x400;
    public const uint GdtOffset = 0x600, IdtOffset = 0x1000, ScratchOffset = 0x2000;
    public const uint GuardOffset = 0x3000, StackOffset = 0x4000;
    public const uint OutcomeOffset = 0xe0, ValueOffset = 0xe8, VectorOffset = 0xf0, ErrorOffset = 0xf8;
    public const uint FaultAddressOffset = 0x1d8, PhaseOffset = 0x1f0, FaultSelectorsOffset = 0x1f8, SavedSsOffset = 0x1fc;
    public const ushort UserCs = 0x3b, UserSs = 0x33;
    public const ulong Star = 0x0028000800000000, Fmask = 0x74700;
    private const ulong End = 0x100000000, PointerEnd = 0x0000800000000000;

    // Baseline four-level paging, no FSGSBASE/PCID/SMEP/SMAP/XSAVE/CET/LA57.
    // The owner separately verifies a single active CPU and disabled interrupts.
    public static bool AdmitCpu(ulong cr0, ulong cr4, ulong efer, uint basicEdx, uint extendedEdx, ulong flags) =>
        (cr0 & 0x80000001) == 0x80000001 && (cr0 & 0xc) == 0 &&
        (cr4 & ~0x7ffUL) == 0 && (cr4 & 0x220) == 0x220 &&
        (efer & 0xd00) == 0xd00 && (basicEdx & 0x07000000) == 0x07000000 &&
        (extendedEdx & 0x20100800) == 0x20100800 && (flags & 0x600) == 0;

    public static bool Prepare(byte* data, uint capacity, X64LinuxUserTransitionPlan plan,
        byte* originalGdt, uint gdtBytes, byte* originalIdt, uint idtBytes)
    {
        if (!Admit(plan) || !Buffer(data, capacity, DataBytes, 4096) ||
            !Buffer(originalGdt, gdtBytes, 40, 8) || !Buffer(originalIdt, idtBytes, 4096, 16) ||
            Overlap((ulong)data, DataBytes, (ulong)originalGdt, gdtBytes) ||
            Overlap((ulong)data, DataBytes, (ulong)originalIdt, idtBytes) ||
            Overlap((ulong)data, DataBytes, plan.CodeAddress, CodeBytes)) return false;
        // Accessed bits may have been set by loading the original segments.
        if (*(ulong*)originalGdt != 0 || (*(ulong*)(originalGdt + 8) & ~0x10000000000UL) != 0x00af9a000000ffff ||
            (*(ulong*)(originalGdt + 16) & ~0x10000000000UL) != 0x00cf92000000ffff) return false;
        ulong tss = plan.TssRsp0Address - 4;
        ulong descriptor = 0x00008b0000000067 | ((tss & 0xffffff) << 16) | (((tss >> 24) & 255) << 56);
        if (*(ulong*)(originalGdt + 24) != descriptor || *(ulong*)(originalGdt + 32) != tss >> 32) return false;
        for (uint vector = 0; vector < 256; vector++)
        {
            byte* gate = originalIdt + vector * 16;
            ulong address = GateAddress(gate);
            if (address == 0 || address >= End || *(ushort*)(gate + 2) != 8 ||
                (gate[4] & ~7) != 0 || gate[5] != 0x8e || *(uint*)(gate + 12) != 0) return false;
        }
        if (GateAddress(originalIdt + 13 * 16) != plan.OriginalGp || GateAddress(originalIdt + 14 * 16) != plan.OriginalPf) return false;
        // No mutation before complete input validation. Guard bytes are cleared
        // before publication; no emitted instruction touches the unmapped guard.
        for (uint i = 0; i < DataBytes; i++) data[i] = 0;
        *(ulong*)data = plan.ContextAddress + DataBytes;
        *(ulong*)(data + 8) = plan.UserRsp; *(ulong*)(data + 0x18) = plan.UserRip;
        *(ulong*)(data + 0x28) = plan.UserRoot; *(ulong*)(data + 0x30) = plan.Dispatcher;
        *(ulong*)(data + 0x38) = plan.FaultReporter; *(ulong*)(data + 0x58) = plan.UserFs;
        *(ulong*)(data + 0x60) = plan.UserGs; *(ulong*)(data + 0xc8) = plan.TssRsp0Address;
        *(ulong*)(data + 0xd8) = plan.CallbackContext;
        *(ulong*)(data + 0x1e0) = plan.OriginalGp; *(ulong*)(data + 0x1e8) = plan.OriginalPf;
        *(ushort*)(data + 0x98) = 63; *(ulong*)(data + 0x9a) = plan.ContextAddress + GdtOffset;
        *(ushort*)(data + 0xb8) = 4095; *(ulong*)(data + 0xba) = plan.ContextAddress + IdtOffset;
        for (uint i = 0; i < 40; i++) data[GdtOffset + i] = originalGdt[i];
        *(ulong*)(data + GdtOffset + 0x30) = 0x00cff2000000ffff;
        *(ulong*)(data + GdtOffset + 0x38) = 0x00affa000000ffff;
        for (uint i = 0; i < 4096; i++) data[IdtOffset + i] = originalIdt[i];
        SetGate(data + IdtOffset + 13 * 16, plan.CodeAddress + GpOffset);
        SetGate(data + IdtOffset + 14 * 16, plan.CodeAddress + PfOffset);
        *(ushort*)(data + UserFxOffset) = 0x37f; // Empty x87 stack, default controls.
        *(uint*)(data + UserFxOffset + 24) = 0x1f80;
        return true;
    }

    public static bool Emit(byte* code, uint capacity, X64LinuxUserTransitionPlan plan)
    {
        if (!Admit(plan) || !Buffer(code, capacity, CodeBytes, 16) ||
            Overlap((ulong)code, CodeBytes, plan.ContextAddress, DataBytes)) return false;
        Writer dry = new(null, false); if (!Encode(ref dry, plan)) return false;
        Writer writer = new(code, false); return Encode(ref writer, plan);
    }

    public static bool ValidateReadBack(byte* code, uint capacity, X64LinuxUserTransitionPlan plan)
    {
        if (!Admit(plan) || !Buffer(code, capacity, CodeBytes, 16) ||
            Overlap((ulong)code, CodeBytes, plan.ContextAddress, DataBytes)) return false;
        Writer writer = new(code, true); return Encode(ref writer, plan);
    }

    private static bool Encode(ref Writer w, X64LinuxUserTransitionPlan plan)
    {
        w.Bytes(0xfa1e0ff3, 4); // endbr64
        w.Bytes(0xfa9c, 2); // pushfq; cli (save caller flags before changing them)
        w.Bytes(0x57565553, 4); w.Bytes(0x5741564155415441, 8); // Win64 nonvolatile GPRs
        w.Immediate(14, plan.ContextAddress);
        w.Store(4, 0x10); // original RSP, including saved caller registers/flags
        w.Fx(false, KernelFxOffset);
        w.Bytes(0xd8200f, 3); w.Store(0, 0x20); // RAX = CR3
        w.Descriptor(0, 0x88); w.Descriptor(1, 0xa8); // SGDT/SIDT
        for (uint segment = 0; segment < 5; segment++) w.SaveSegment(segment);
        w.ReadMsr(0xc0000100); w.Store(0, 0x40); // FS_BASE
        w.ReadMsr(0xc0000101); w.Store(0, 0x48); // GS_BASE
        w.ReadMsr(0xc0000102); w.Store(0, 0x50); // KERNEL_GS_BASE
        for (uint index = 0; index < 3; index++)
        { w.ReadMsr(0xc0000080 + index); w.Store(0, 0x68 + index * 8); }
        w.ReadMsr(0xc0000084); w.Store(0, 0x80);
        w.Load(0, 0xc8); w.Bytes(0x108b48, 3); w.Store(2, 0xd0); // original TSS.RSP0
        w.Load(2, 0); w.Bytes(0x108948, 3); // dedicated kernel stack -> TSS.RSP0
        w.Descriptor(2, 0x98); w.Descriptor(3, 0xb8); // private LGDT/LIDT, no LTR
        w.Bytes(0xc031, 2); // null DS/ES/FS/GS before assigning MSR bases
        w.Bytes(0xc08ed88e, 4); w.Bytes(0xe88ee08e, 4);
        w.Load(0, 0x58); w.WriteVerifyMsr(0xc0000100);
        w.Load(0, 0x60); w.WriteVerifyMsr(0xc0000101);
        w.Immediate(0, plan.ContextAddress); w.WriteVerifyMsr(0xc0000102);
        w.Load(0, 0x68); w.Bytes(0x01c88348, 4); w.WriteVerifyMsr(0xc0000080); // EFER.SCE
        w.Immediate(0, Star); w.WriteVerifyMsr(0xc0000081);
        w.Immediate(0, plan.CodeAddress + SyscallOffset); w.WriteVerifyMsr(0xc0000082);
        w.Immediate(0, Fmask); w.WriteVerifyMsr(0xc0000084);
        w.Fx(true, UserFxOffset);
        w.Load(0, 0x28); w.Bytes(0xd8220f, 3); // publish admitted user CR3
        w.Load(4, 0); w.Set(PhaseOffset, 1);
        w.Bytes(0x336a, 2); w.PushField(8); w.Bytes(0x026a, 2); w.Bytes(0x3b6a, 2); w.PushField(0x18);
        // Clear every GPR before entering user code; no kernel pointer escapes.
        for (uint register = 0; register < 16; register++)
            if (register != 4) w.Zero(register);
        w.Bytes(0xcf48, 2); // IRETQ: RIP,CS,RFLAGS,RSP,SS; IRQs remain disabled

        // Win64 void(ulong* ownedOutput), CPL0 only. The owner proves the whole
        // output extent, initializes it and compares original/live observations.
        // Words: CR0/CR3/CR4/flags/CS/SS/TR/basic EDX/ext max/ext EDX;
        // EFER/STAR/LSTAR/FMASK/FS/GS/KGS; GDTR(16),IDTR(16); DS/ES/FS/GS.
        w.Pad(ObserveOffset); Observe(ref w);

        w.Pad(SyscallOffset);
        w.Bytes(0xf8010f, 3); // SWAPGS: obtain the private supervisor context
        w.GsMemory(false, 4, 8); w.GsMemory(true, 4, 0); // save hostile user RSP before switching
        w.GsMemory(false, 14, SavedOffset(14)); w.Immediate(14, plan.ContextAddress);
        w.Store(0, FrameOffset);
        for (uint register = 1; register < 16; register++)
            if (register != 4 && register != 14) w.Store(register, SavedOffset(register));
        w.Store(7, 0x108); w.Store(6, 0x110); w.Store(2, 0x118);
        w.Store(10, 0x120); w.Store(8, 0x128); w.Store(9, 0x130);
        w.Store(1, 0x138); w.Store(11, 0x148); w.Load(0, 8); w.Store(0, 0x140);
        w.ReadMsr(0xc0000100); w.Store(0, 0x150);
        w.ReadMsr(0xc0000102); w.Store(0, 0x158); // SWAPGS placed user GS here
        w.Fx(false, UserFxOffset); w.Fx(true, KernelFxOffset); w.Bytes(0xfc, 1);
        w.Bytes(0xd8200f, 3); w.CompareField(0, 0x28); w.Branch(0x85, ExitOffset);
        w.Load(1, 0xd8); w.Lea(2, FrameOffset); w.Call(0x30);
        w.Bytes(0x013c, 2); // cmp al,1: only validated returns may resume
        w.Branch(0x85, ExitOffset);
        w.Load(0, 0x150); w.WriteMsr(0xc0000100);
        w.Load(0, 0x158); w.WriteMsr(0xc0000102);
        w.Bytes(0x336a, 2); w.PushField(0x140); w.PushField(0x148); w.Bytes(0x3b6a, 2); w.PushField(0x138);
        w.Fx(true, UserFxOffset);
        for (uint register = 1; register < 16; register++)
            if (register != 4 && register != 14) w.Load(register, SavedOffset(register));
        w.Load(0, ValueOffset); w.Load(14, SavedOffset(14));
        w.Bytes(0xf8010f, 3); w.Bytes(0xcf48, 2); // SWAPGS; validated IRETQ, no SYSRET window

        w.Pad(GpOffset); Fault(ref w, plan, 13, GpPointerOffset);
        w.Pad(PfOffset); Fault(ref w, plan, 14, PfPointerOffset);
        w.Pad(ExitOffset);
        w.Immediate(14, plan.ContextAddress); w.Bytes(0xfcfa, 2);
        w.Load(0, 0x20); w.Bytes(0xd8220f, 3); // original supervisor root
        w.Descriptor(2, 0x88); w.Descriptor(3, 0xa8);
        w.Load(0, 0xc8); w.Load(2, 0xd0); w.Bytes(0x108948, 3); // original TSS.RSP0
        // Interrupt entry from CPL3 sets SS to NULL. A near RET cannot restore
        // it; reload the original SS as well as DS/ES/FS/GS under the old GDT.
        for (uint segment = 0; segment < 5; segment++) w.RestoreSegment(segment);
        w.Load(0, 0x40); w.WriteMsr(0xc0000100);
        w.Load(0, 0x48); w.WriteMsr(0xc0000101);
        w.Load(0, 0x50); w.WriteMsr(0xc0000102);
        w.Load(0, 0x70); w.WriteMsr(0xc0000081);
        w.Load(0, 0x78); w.WriteMsr(0xc0000082);
        w.Load(0, 0x80); w.WriteMsr(0xc0000084);
        w.Load(0, 0x68); w.WriteMsr(0xc0000080);
        w.Fx(true, KernelFxOffset); w.Set(PhaseOffset, 2); w.Load(4, 0x10);
        w.Bytes(0x5c415d415e415f41, 8); w.Bytes(0x5b5d5e5f, 4); // nonvolatile GPRs
        w.Bytes(0x9d, 1); w.Bytes(0x00000001b8, 5); w.Bytes(0xc3, 1); // restore flags; return 1 (cleanup only)
        w.Pad(GpPointerOffset); w.Bytes(plan.OriginalGp, 8); w.Bytes(plan.OriginalPf, 8);
        return w.Valid;
    }

    private static void Observe(ref Writer w)
    {
        w.Bytes(0xfa1e0ff3, 4); w.Bytes(0x5753, 2); w.Bytes(0xcf8948, 3); // preserve RBX/RDI; RDI=output
        w.Bytes(0xc0200f, 3); w.ObservationStore(0);
        w.Bytes(0xd8200f, 3); w.ObservationStore(8);
        w.Bytes(0xe0200f, 3); w.ObservationStore(16);
        w.Bytes(0x589c, 2); w.ObservationStore(24);
        w.Bytes(0xc031, 2); w.Bytes(0xc88c66, 3); w.ObservationStore(32); // CS
        w.Bytes(0xc031, 2); w.Bytes(0xd08c66, 3); w.ObservationStore(40); // SS
        w.Bytes(0xc031, 2); w.Bytes(0xc8000f66, 4); w.ObservationStore(48); // STR AX
        w.Bytes(0x00000001b8, 5); w.Bytes(0xc931, 2); w.Bytes(0xa20f, 2); w.Bytes(0xd089, 2); w.ObservationStore(56);
        w.Bytes(0x80000000b8, 5); w.Bytes(0xc931, 2); w.Bytes(0xa20f, 2); w.ObservationStore(64);
        w.Bytes(0x80000001b8, 5); w.Bytes(0xc931, 2); w.Bytes(0xa20f, 2); w.Bytes(0xd089, 2); w.ObservationStore(72);
        for (uint index = 0; index < 3; index++) { w.ReadMsr(0xc0000080 + index); w.ObservationStore(80 + index * 8); }
        w.ReadMsr(0xc0000084); w.ObservationStore(104);
        for (uint index = 0; index < 3; index++) { w.ReadMsr(0xc0000100 + index); w.ObservationStore(112 + index * 8); }
        w.Bytes(0x87010f, 3); w.Bytes(136, 4); w.Bytes(0x8f010f, 3); w.Bytes(152, 4); // SGDT/SIDT
        for (uint segment = 0; segment < 4; segment++)
        { w.Bytes(0xc031, 2); w.ObservationSegment(segment); w.ObservationStore(168 + segment * 8); }
        w.Bytes(0xc35b5f, 3);
    }

    private static void Fault(ref Writer w, X64LinuxUserTransitionPlan plan, uint vector, uint originalPointer)
    {
        w.Bytes(0x3b10247c8348, 6); // cmp qword [rsp+16],UserCs
        w.Bytes(0x0674, 2); // JE over immutable RIP-relative original handler jump
        w.Bytes(0x25ff, 2); w.Bytes(unchecked(originalPointer - (w.Position + 4)), 4);
        w.Bytes(0xf8010f, 3); w.Immediate(14, plan.ContextAddress); w.Bytes(0xfc, 1);
        w.StackLoad(0); w.Store(0, ErrorOffset); w.StackLoad(8); w.Store(0, 0x138);
        w.StackLoad(24); w.Store(0, 0x148); w.StackLoad(32); w.Store(0, 0x140);
        w.StackLoad(16); w.StoreWord(FaultSelectorsOffset); w.StackLoad(40); w.StoreWord(FaultSelectorsOffset + 2);
        w.Bytes(0xd0200f, 3); w.Store(0, FaultAddressOffset); // CR2, also recorded for GP
        w.Set(VectorOffset, vector); w.Set(OutcomeOffset, 3);
        w.Fx(false, UserFxOffset); w.Fx(true, KernelFxOffset);
        w.Load(4, 0); w.Load(1, 0xd8); w.Bytes(0xf2894c, 3); // RDX = supervisor R14 context
        w.Call(0x38); w.Jump(ExitOffset);
    }

    private static uint SavedOffset(uint register) => register switch
    { 3 => 0x160, 5 => 0x168, 6 => 0x170, 7 => 0x178, 2 => 0x180, 8 => 0x188, 9 => 0x190,
        10 => 0x198, 12 => 0x1a0, 13 => 0x1a8, 14 => 0x1b0, 15 => 0x1b8, 1 => 0x1c0, 11 => 0x1c8, _ => 0 };

    private static bool Admit(X64LinuxUserTransitionPlan p) =>
        PageRange(p.CodeAddress, CodeBytes) && PageRange(p.ContextAddress, DataBytes) &&
        PageRange(p.UserRoot, 4096) && !Overlap(p.CodeAddress, CodeBytes, p.ContextAddress, DataBytes) &&
        !Overlap(p.UserRoot, 4096, p.CodeAddress, CodeBytes) && !Overlap(p.UserRoot, 4096, p.ContextAddress, DataBytes) &&
        p.UserRip >= 4096 && p.UserRip < End && p.UserRsp >= 4104 && p.UserRsp <= End &&
        (p.UserFs == 0 || p.UserFs >= 4096 && p.UserFs < End) && (p.UserGs == 0 || p.UserGs >= 4096 && p.UserGs < End) &&
        Address(p.Dispatcher) && Address(p.FaultReporter) && Address(p.CallbackContext) &&
        p.TssRsp0Address >= 4100 && p.TssRsp0Address <= End - 100 && (p.TssRsp0Address & 15) == 4 &&
        Address(p.OriginalGp) && Address(p.OriginalPf);
    private static bool Address(ulong address) => address >= 4096 && address < End;
    private static bool PageRange(ulong address, uint bytes) => address >= 4096 && (address & 4095) == 0 && address <= End - bytes;
    private static bool Buffer(byte* pointer, uint capacity, uint required, uint alignment) =>
        pointer != null && capacity >= required && ((ulong)pointer & (alignment - 1)) == 0 && (ulong)pointer <= PointerEnd - required;
    private static bool Overlap(ulong a, ulong aBytes, ulong b, ulong bBytes) => a < b + bBytes && b < a + aBytes;
    private static ulong GateAddress(byte* gate) => *(ushort*)gate | ((ulong)*(ushort*)(gate + 6) << 16) | ((ulong)*(uint*)(gate + 8) << 32);
    private static void SetGate(byte* gate, ulong address)
    { *(ushort*)gate = (ushort)address; *(ushort*)(gate + 2) = 8; gate[4] = 0; gate[5] = 0x8e; *(ushort*)(gate + 6) = (ushort)(address >> 16); *(uint*)(gate + 8) = (uint)(address >> 32); *(uint*)(gate + 12) = 0; }

    // Small fixed-layout encoder. Every memory operand uses supervisor R14+disp32.
    // Count/compare passes share encodings; independent host vectors audit ABI bytes.
    private struct Writer
    {
        private byte* _code; private bool _compare, _valid; public uint Position;
        public Writer(byte* code, bool compare) { _code = code; _compare = compare; _valid = true; Position = 0; }
        public bool Valid => _valid && Position == CodeBytes;
        public void Bytes(ulong value, int count)
        {
            for (int i = 0; i < count; i++, Position++)
            {
                if (Position >= CodeBytes) { _valid = false; continue; }
                byte valueByte = (byte)(value >> (8 * i));
                if (_code == null) continue;
                if (_compare) { if (_code[Position] != valueByte) _valid = false; }
                else _code[Position] = valueByte;
            }
        }
        public void Pad(uint target) { if (Position > target) _valid = false; while (Position < target) Bytes(0xcc, 1); }
        public void Immediate(uint register, ulong value)
        { Bytes(register < 8 ? 0x48UL : 0x49UL, 1); Bytes(0xb8 + (register & 7), 1); Bytes(value, 8); }
        public void Load(uint register, uint offset) => Memory(0x8b, register, offset);
        public void Store(uint register, uint offset) => Memory(0x89, register, offset);
        public void Lea(uint register, uint offset) => Memory(0x8d, register, offset);
        private void Memory(byte opcode, uint register, uint offset)
        { Bytes(register < 8 ? 0x49UL : 0x4dUL, 1); Bytes(opcode, 1); Bytes(0x86 | ((register & 7) << 3), 1); Bytes(offset, 4); }
        public void Fx(bool restore, uint offset)
        { Bytes(0x49, 1); Bytes(0xae0f, 2); Bytes(restore ? 0x8eUL : 0x86UL, 1); Bytes(offset, 4); }
        public void Descriptor(uint operation, uint offset)
        { Bytes(0x41, 1); Bytes(0x010f, 2); Bytes(0x86 | operation * 8, 1); Bytes(offset, 4); }
        public void Set(uint offset, uint value)
        { Bytes(0x49, 1); Bytes(0xc7, 1); Bytes(0x86, 1); Bytes(offset, 4); Bytes(value, 4); }
        public void ReadMsr(uint index)
        { Bytes(0xb9, 1); Bytes(index, 4); Bytes(0x320f, 2); Bytes(0x20e2c148, 4); Bytes(0xd00948, 3); }
        public void WriteMsr(uint index)
        { Bytes(0xb9, 1); Bytes(index, 4); Bytes(0xc28948, 3); Bytes(0x20eac148, 4); Bytes(0x300f, 2); }
        public void WriteVerifyMsr(uint index)
        { Bytes(0xc48949, 3); WriteMsr(index); ReadMsr(index); Bytes(0xe0394c, 3); Branch(0x85, ExitOffset); }
        public void CompareField(uint register, uint offset) => Memory(0x3b, register, offset);
        public void Branch(byte condition, uint target)
        { Bytes(0x0f, 1); Bytes(condition, 1); Bytes(unchecked(target - (Position + 4)), 4); }
        public void Jump(uint target)
        { Bytes(0xe9, 1); Bytes(unchecked(target - (Position + 4)), 4); }
        public void PushField(uint offset) { Load(0, offset); Bytes(0x50, 1); }
        public void Zero(uint register)
        { if (register >= 8) Bytes(0x45, 1); Bytes(0x31, 1); Bytes(0xc0 | ((register & 7) << 3) | (register & 7), 1); }
        public void GsMemory(bool load, uint register, uint offset)
        { Bytes(0x65, 1); Bytes(register < 8 ? 0x48UL : 0x4cUL, 1); Bytes(load ? 0x8bUL : 0x89UL, 1);
            Bytes(0x04 | ((register & 7) << 3), 1); Bytes(0x25, 1); Bytes(offset, 4); }
        public void Call(uint offset)
        { Load(0, offset); Bytes(0x20ec8348, 4); Bytes(0xd0ff, 2); Bytes(0x20c48348, 4); }
        public void StackLoad(byte offset)
        { Bytes(0x48, 1); Bytes(0x8b, 1); Bytes(0x2444, 2); Bytes(offset, 1); }
        public void StoreWord(uint offset)
        { Bytes(0x66, 1); Bytes(0x41, 1); Bytes(0x89, 1); Bytes(0x86, 1); Bytes(offset, 4); }
        private static uint SegmentCode(uint segment) => segment switch { 0 => 3, 1 => 0, 2 => 4, 3 => 5, _ => 2 };
        public void ObservationStore(uint offset) { Bytes(0x878948, 3); Bytes(offset, 4); }
        public void ObservationSegment(uint segment) { Bytes(0x66, 1); Bytes(0x8c, 1); Bytes(0xc0 | SegmentCode(segment) * 8, 1); }
        private static uint SegmentOffset(uint segment) => segment == 4 ? SavedSsOffset : 0x1d0 + segment * 2;
        public void SaveSegment(uint segment)
        { Bytes(0x66, 1); Bytes(0x8c, 1); Bytes(0xc0 | SegmentCode(segment) * 8, 1); StoreWord(SegmentOffset(segment)); }
        public void RestoreSegment(uint segment)
        { Bytes(0x66, 1); Bytes(0x41, 1); Bytes(0x8b, 1); Bytes(0x86, 1); Bytes(SegmentOffset(segment), 4);
            Bytes(0x8e, 1); Bytes(0xc0 | SegmentCode(segment) * 8, 1); }
    }
}
