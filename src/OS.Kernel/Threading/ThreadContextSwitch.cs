namespace OS.Kernel.Threading;

/// <summary>
/// Admission for the bounded single-CPU CPL0 cooperative switch. Context values
/// use M09-001; mappings, guards, lifetime and CPU observations are supplied by
/// the trusted owner. Admission is read-only and does not execute a transfer or
/// grant mapping/restore authority. Complete operations require external ownership
/// and IRQ-off serialization. General suspension, CPL3, XSAVE/AVX, CET and SMP
/// are unsupported. A captured continuation never replaces a parked return slot.
/// </summary>
public static unsafe class ThreadContextSwitch
{
    public enum Status : byte
    {
        Ok, Uninitialized, InvalidOwner, UnsupportedCpu, InvalidRoot, InvalidRange,
        Overlap, InvalidIdentity, StaleIdentity, InvalidContext, InvalidStack,
        InvalidContinuation, InvalidFlags, InvalidTls
    }
    public enum ResumeKind : byte { Entry = 1, CapturedCall = 2 }
    public const uint Page = 4096, PoolPages = 32;
    public const uint StubBytes = 1024, ProgramBytes = 8192;
    public const uint OriginalResumeOffset = 26, WorkerAEntryOffset = 0x200, WorkerAResumeOffset = 0x209;
    public const uint WorkerBEntryOffset = 0x300, WorkerBResumeOffset = 0x35c, WorkerBFinalResumeOffset = 0x324;
    public const uint RecoveryOffset = 0x600, TerminalOffset = 0x780, FirstStubOffset = 0x800;
    public const uint ObserverBytes = 512, FxSnapshotBytes = 16, OwnedSegmentsBytes = 96, ObservationWords = 19;
    public const ulong AdmittedFlags = 0x00200cd7; // Arithmetic, DF, ID, mandatory bit1; no IF/TF/RF/AC/privilege bits.

    /// <summary>
    /// Independently captured CPU/ownership observations and declared live ranges.
    /// True proof fields mean the trusted caller performed actual guard/W^X checks;
    /// copying or fabricating this POD supplies no authority. Context/code/switch
    /// stack ranges are disjoint; guard extents include two nonpresent end pages.
    /// Only the current four-level identity root below4GiB is admitted here.
    /// </summary>
    public struct Owner
    {
        public bool Initialized, GuardsVerified, MappingsVerified;
        public ulong Key, CurrentKey, Root, CurrentRoot, KernelGs, CurrentKernelGs;
        public ulong Cr0, Cr4, Efer, Flags;
        public uint CpuCount, BasicEdx, ExtendedEdx, ObservedMxcsrMask;
        public ushort Cs, Ss, Tr, Fs, Gs;
        public ulong ContextArena, ContextArenaBytes, CodeStart, CodeBytes;
        public ulong SwitchStackGuardStart, SwitchStackBytes;
    }

    /// <summary>
    /// One pinned current generation, its live mapped context and guarded stack.
    /// Identity uses the existing generation-high32/slot-plus-one-low32 contract.
    /// ResumeRip is independently retained by the owner: initial entry or exact
    /// instruction after CALL. CapturedCall RSP is entryRSP+8, not an intermediate
    /// helper/pushed RSP. FS/user-GS may be zero or point within the declared owned
    /// context arena. Descriptors neither issue identities nor own CPU selectors.
    /// </summary>
    public struct Descriptor
    {
        public ulong Identity, CurrentIdentity;
        public X64ThreadContext* Context;
        public uint ContextBytes;
        public ulong StackGuardStart, StackBytes, ResumeRip;
        public ResumeKind Kind;
    }

    /// <summary>
    /// Fixed source/destination CALL stub, not a general scheduler entry. The
    /// trusted owner pins separately admitted CPL0 callback/rejection code and
    /// the callback's declared control extent. The callback uses Win64
    /// byte(control, source, destination), returns exactly1 only after fresh
    /// ownership/CPU/context admission, and may not alter pinned identities or
    /// context storage. HelperFx contains initialized empty Win64 x87/SSE state.
    /// JumpSlot is separate owned RW/NX storage; it is not a parked return slot.
    /// RejectRip names an owned terminal/recovery path outside this stub, able
    /// to restore the independently retained original owner state. No field
    /// supplies execution, mapping, callback or recovery authority by itself.
    /// </summary>
    public struct StubPlan
    {
        public uint CodeOffset, CallbackBytes;
        public ulong AdmissionCallback, CallbackContext, HelperFx, JumpSlot, RejectRip;
    }

    /// <summary>
    /// One original driver and two mechanical workers with fixed code labels.
    /// The callback owns bounded progress and pins the three descriptors/control
    /// extents. Witness is separate live context-sized storage written by the
    /// resumed driver before it changes any GPR. BCount and FlagScratch lie in
    /// callback control storage; the latter retains actual restored flags across
    /// B's bounded branch, without substituting expected flags. RecoveryCallback
    /// returns byte1 only after fresh original-context admission, records failure
    /// and preserves context storage. TerminalCallback is owned no-return failure
    /// reporting. Both callback addresses need separate RX/image admission.
    /// These declarations grant no transfer or resource-cleanup authority.
    /// </summary>
    public struct ProgramPlan
    {
        public Owner Owner;
        public Descriptor Original, A, B;
        public StubPlan Stub;
        public X64ThreadContext* Witness;
        public ulong BCount, FlagScratch, RecoveryCallback, TerminalCallback;
        public byte Cycles;
    }

    /// <summary>
    /// Emit the fixed original-driver/worker/recovery program into unpublished
    /// writable caller storage. Planning permits incomplete live mapping proof;
    /// execution must wait for real RX/RW-NX/guard/CPU/ownership admission. All
    /// inputs and every dry encoding pass precede any output mutation. No context,
    /// control count or parked return slot is changed here. Code capacity is8192
    /// and all embedded data/recovery displacements must fit signed32 bits.
    /// This program supplies cooperative mechanics only, not timer/preemption,
    /// lifecycle, synchronization, CPL3, SMP or upstream runtime support.
    /// </summary>
    public static bool EmitProgram(byte* output, uint capacity, ProgramPlan plan)
    {
        if (!ProgramInputs(output, capacity, plan) || !EncodeProgram(null, plan, false)) return false;
        return EncodeProgram(output, plan, false);
    }

    /// <summary>Read-only identity check against the retained mechanical plan.
    /// Updated entry/captured descriptors may describe the same fixed code after
    /// execution. Matching bytes is not live mapping or transfer evidence.</summary>
    public static bool VerifyProgram(byte* output, uint capacity, ProgramPlan plan) =>
        ProgramInputs(output, capacity, plan) && EncodeProgram(output, plan, true);

    private static bool ProgramInputs(byte* output, uint capacity, ProgramPlan p)
    {
        Owner o = p.Owner; ulong witness = (ulong)p.Witness;
        if (!CodeBuffer(output, capacity, ProgramBytes) || o.CodeBytes < ProgramBytes ||
            p.Cycles < 2 || p.Cycles > 16 || p.Original.Kind != ResumeKind.CapturedCall ||
            p.Original.ResumeRip != o.CodeStart + OriginalResumeOffset ||
            !ProgramContinuation(o, p.A, true) || !ProgramContinuation(o, p.B, false) ||
            AdmitCore(o, p.Original, p.A, false) != Status.Ok || AdmitCore(o, p.Original, p.B, false) != Status.Ok ||
            AdmitCore(o, p.A, p.B, false) != Status.Ok ||
            (witness & 15) != 0 || !Contains(o.ContextArena, o.ContextArenaBytes, witness, X64ThreadContext.Bytes) ||
            p.Stub.RejectRip != o.CodeStart + RecoveryOffset ||
            p.RecoveryCallback == 0 || !Canonical(p.RecoveryCallback) || p.TerminalCallback == 0 || !Canonical(p.TerminalCallback) ||
            Contains(o.CodeStart, ProgramBytes, p.RecoveryCallback, 1) || Contains(o.CodeStart, ProgramBytes, p.TerminalCallback, 1) ||
            (p.BCount & 3) != 0 || (p.FlagScratch & 7) != 0 ||
            !Contains(p.Stub.CallbackContext, p.Stub.CallbackBytes, p.BCount, 4) ||
            !Contains(p.Stub.CallbackContext, p.Stub.CallbackBytes, p.FlagScratch, 8) || Overlap(p.BCount, 4, p.FlagScratch, 8) ||
            Overlap((ulong)output, ProgramBytes, o.ContextArena, o.ContextArenaBytes) ||
            Overlap((ulong)output, ProgramBytes, p.Original.StackGuardStart, p.Original.StackBytes) ||
            Overlap((ulong)output, ProgramBytes, p.A.StackGuardStart, p.A.StackBytes) ||
            Overlap((ulong)output, ProgramBytes, p.B.StackGuardStart, p.B.StackBytes) ||
            Overlap((ulong)output, ProgramBytes, o.SwitchStackGuardStart, o.SwitchStackBytes) ||
            Overlap(witness, X64ThreadContext.Bytes, (ulong)p.Original.Context, p.Original.ContextBytes) ||
            Overlap(witness, X64ThreadContext.Bytes, (ulong)p.A.Context, p.A.ContextBytes) ||
            Overlap(witness, X64ThreadContext.Bytes, (ulong)p.B.Context, p.B.ContextBytes) ||
            Overlap(witness, X64ThreadContext.Bytes, p.Stub.CallbackContext, p.Stub.CallbackBytes) ||
            Overlap(witness, X64ThreadContext.Bytes, p.Stub.HelperFx, 512) ||
            Overlap(witness, X64ThreadContext.Bytes, p.Stub.JumpSlot, 8)) return false;
        for (uint index = 0; index < 4; index++)
        {
            uint offset = FirstStubOffset + index * StubBytes;
            StubPlan stub = p.Stub; stub.CodeOffset = offset;
            Descriptor from = index == 0 ? p.Original : index == 1 ? p.A : p.B;
            Descriptor to = index == 0 ? p.A : index == 1 ? p.B : index == 2 ? p.A : p.Original;
            if (!StubInputs(output + offset, capacity - offset, o, from, to, stub, true)) return false;
        }
        return true;
    }
    private static bool ProgramContinuation(Owner owner, Descriptor value, bool first) =>
        value.Kind == ResumeKind.Entry
            ? value.ResumeRip == owner.CodeStart + (first ? WorkerAEntryOffset : WorkerBEntryOffset)
            : value.Kind == ResumeKind.CapturedCall && (value.ResumeRip == owner.CodeStart +
                (first ? WorkerAResumeOffset : WorkerBResumeOffset) || (!first && value.ResumeRip == owner.CodeStart + WorkerBFinalResumeOffset));
    private static bool EncodeProgram(byte* output, ProgramPlan p, bool compare)
    {
        ulong code = p.Owner.CodeStart, witness = (ulong)p.Witness;
        StubWriter w = new(output, code, compare, FirstStubOffset);
        w.Bytes(0xfa1e0ff3, 4); w.Bytes(0x9c, 1);
        w.Bytes(0x57565553, 4); w.Bytes(0x5741564155415441, 8); // outer Win64 nonvolatile GPRs/flags
        w.Bytes(0x20ec8348, 4); w.Call(code + FirstStubOffset); // return offset26
        w.Set(witness, 0);
        for (uint register = 0; register < 16; register++) w.Memory(false, register, witness + RegisterOffset(register));
        w.Immediate(11, code + OriginalResumeOffset); w.Memory(false, 11, witness + X64ThreadContext.RipOffset);
        w.Bytes(0x5b419c, 3); w.Memory(false, 11, witness + X64ThreadContext.RflagsOffset);
        w.Fx(false, witness + X64ThreadContext.FxSaveOffset);
        w.ReadMsr(0xc0000100); w.Memory(false, 0, witness + X64ThreadContext.FsBaseOffset);
        w.ReadMsr(0xc0000101); w.Memory(false, 0, witness + X64ThreadContext.UserGsBaseOffset);
        w.Set(witness + X64ThreadContext.ReservedOffset, 0); w.Set(witness, 1);
        w.Bytes(0x20c48348, 4); w.Bytes(0x5c415d415e415f41, 8); w.Bytes(0x5b5d5e5f, 4);
        w.Bytes(0x9d, 1); w.Bytes(0x00000001b8, 5); w.Bytes(0xc3, 1);

        w.Pad(WorkerAEntryOffset); w.Bytes(0x28ec8348, 4); // entry8mod16 -> cooperative CALL0mod16
        w.Call(code + FirstStubOffset + StubBytes); w.Jump(code + WorkerAEntryOffset + 4);
        w.Pad(WorkerBEntryOffset); w.Bytes(0x28ec8348, 4);
        w.Bytes(0x9c, 1); w.PopMemory(p.FlagScratch); // actual flags before count branch; no GPR scratch
        w.CompareByteLimit(p.BCount, (byte)(p.Cycles - 1)); w.Below(code + 0x350);
        w.PushMemory(p.FlagScratch); w.Bytes(0x9d, 1);
        w.Call(code + FirstStubOffset + 3 * StubBytes); w.Jump(code + RecoveryOffset);
        w.Pad(0x350); w.PushMemory(p.FlagScratch); w.Bytes(0x9d, 1);
        w.Call(code + FirstStubOffset + 2 * StubBytes); w.Jump(code + WorkerBEntryOffset + 4);

        w.Pad(RecoveryOffset); w.Bytes(0xfc, 1); w.Fx(true, p.Stub.HelperFx);
        w.Immediate(1, p.Stub.CallbackContext); w.Immediate(2, (ulong)p.Original.Context);
        w.Immediate(11, p.RecoveryCallback);
        w.Bytes(0x20ec8348, 4); w.Bytes(0xd3ff41, 3); w.Bytes(0x20c48348, 4);
        w.Bytes(0x013c, 2); w.Reject(code + TerminalOffset);
        Restore(ref w, (ulong)p.Original.Context, p.Stub.JumpSlot, code + TerminalOffset);
        w.Pad(TerminalOffset); w.Bytes(0xfc, 1); w.Fx(true, p.Stub.HelperFx);
        w.Immediate(1, p.Stub.CallbackContext); w.Immediate(11, p.TerminalCallback);
        w.Bytes(0x20ec8348, 4); w.Bytes(0xd3ff41, 3); w.Bytes(0x0b0f, 2); // no-return owner report; UD2 on violation
        w.Pad(); if (!w.Valid) return false;
        for (uint index = 0; index < 4; index++)
        {
            uint offset = FirstStubOffset + index * StubBytes; StubPlan stub = p.Stub; stub.CodeOffset = offset;
            Descriptor from = index == 0 ? p.Original : index == 1 ? p.A : p.B;
            Descriptor to = index == 0 ? p.A : index == 1 ? p.B : index == 2 ? p.A : p.Original;
            StubWriter part = new(output == null ? null : output + offset, code + offset, compare);
            if (!EncodeStub(ref part, p.Owner, from, to, stub)) return false;
        }
        StubWriter tail = new(output == null ? null : output + 0x1800, code + 0x1800, compare, ProgramBytes - 0x1800);
        tail.Pad(); return tail.Valid;
    }

    /// <summary>Emit a CPL0 Win64 void(ulong[19]*) CPU/selector/MSR observation
    /// leaf. The owner pins mapped output before calling. It preserves RBX/flags,
    /// executes no FX instruction and changes no CPU control state. Unsupported
    /// CPU facts are rejected by subsequent admission, not normalized to success.</summary>
    public static bool EmitObserver(byte* output, uint capacity)
    {
        if (!CodeBuffer(output, capacity, ObserverBytes)) return false;
        StubWriter w = new(output, (ulong)output, false, ObserverBytes);
        w.Bytes(0xfa1e0ff3, 4); w.Bytes(0x539c, 2); w.Bytes(0xc88949, 3); // flags/RBX; r8=output
        w.Bytes(0xc0200f, 3); ObservationStore(ref w, 0);
        w.Bytes(0xd8200f, 3); ObservationStore(ref w, 1);
        w.Bytes(0xe0200f, 3); ObservationStore(ref w, 2);
        w.Bytes(0x0824448b48, 5); ObservationStore(ref w, 3);
        w.Bytes(0xc031, 2); w.Bytes(0xc88c66, 3); ObservationStore(ref w, 4);
        w.Bytes(0xc031, 2); w.Bytes(0xd08c66, 3); ObservationStore(ref w, 5);
        w.Bytes(0xc031, 2); w.Bytes(0xc8000f66, 4); ObservationStore(ref w, 6);
        w.Bytes(0x00000001b8, 5); w.Bytes(0xc931, 2); w.Bytes(0xa20f, 2);
        w.Bytes(0xd089, 2); ObservationStore(ref w, 7); w.Bytes(0xd889, 2); ObservationStore(ref w, 8);
        w.Bytes(0x80000000b8, 5); w.Bytes(0xc931, 2); w.Bytes(0xa20f, 2); ObservationStore(ref w, 9);
        w.Bytes(0x80000001b8, 5); w.Bytes(0xc931, 2); w.Bytes(0xa20f, 2); w.Bytes(0xd089, 2); ObservationStore(ref w, 10);
        w.ReadMsr(0xc0000080); ObservationStore(ref w, 11);
        for (uint index = 0; index < 3; index++) { w.ReadMsr(0xc0000100 + index); ObservationStore(ref w, 12 + index); }
        for (uint index = 0; index < 4; index++)
        {
            w.Bytes(0xc031, 2); w.Bytes(index == 0 ? 0xe08c66UL : index == 1 ? 0xe88c66UL : index == 2 ? 0xd88c66UL : 0xc08c66UL, 3);
            ObservationStore(ref w, 15 + index);
        }
        w.Bytes(0xc39d5b, 3); w.Pad(); return w.Valid;
    }
    private static void ObservationStore(ref StubWriter w, uint word)
    { w.Bytes(0x808949, 3); w.Bytes(word * 8, 4); } // mov [r8+disp32],rax
    /// <summary>Emit a Win64 CPL0 void(aligned512-byte-output*) FXSAVE64 leaf.
    /// The owner must first admit CPU FXSR/CR0/CR4 and the writable mapped output;
    /// no callback, allocation or CPU-control change occurs. It preserves flags.</summary>
    public static bool EmitFxSnapshot(byte* output, uint capacity)
    {
        if (!CodeBuffer(output, capacity, FxSnapshotBytes)) return false;
        StubWriter w = new(output, (ulong)output, false, FxSnapshotBytes);
        w.Bytes(0x01ae0f48, 4); w.Bytes(0xc3, 1); w.Pad(); return w.Valid;
    }
    /// <summary>Emit a CPL0 Win64 leaf establishing owned NULL FS/GS selectors
    /// while preserving their full bases and caller flags. The trusted owner must
    /// admit the current GDT/MSRs and call once before the restoration baseline;
    /// this is not restoration of old firmware selector caches.</summary>
    public static bool EmitOwnedSegments(byte* output, uint capacity)
    {
        if (!CodeBuffer(output, capacity, OwnedSegmentsBytes)) return false;
        StubWriter w = new(output, (ulong)output, false, OwnedSegmentsBytes);
        w.Bytes(0xfa1e0ff3, 4); w.Bytes(0x9c, 1);
        for (uint index = 0; index < 2; index++)
        {
            w.ReadMsr(0xc0000100 + index); w.Bytes(0xc08949, 3); w.Bytes(0xc031, 2);
            w.Bytes(index == 0 ? 0xe08eUL : 0xe88eUL, 2); w.Bytes(0xc0894c, 3);
            w.Bytes(0xb9, 1); w.Bytes(0xc0000100 + index, 4); w.Bytes(0xc28948, 3); w.Bytes(0x20eac148, 4); w.Bytes(0x300f, 2);
        }
        w.Bytes(0xc39d, 2); w.Pad(); return w.Valid;
    }
    private static bool CodeBuffer(byte* output, uint capacity, uint bytes) =>
        capacity >= bytes && ((ulong)output & 15) == 0 && Range((ulong)output, bytes);

    /// <summary>
    /// Emit bounded CPL0 cooperative capture/admission/restore bytes into live
    /// writable unpublished storage. Code uses the declared execution address,
    /// which may differ from the output alias. All validation and a dry encoding
    /// precede mutation. Emission checks declared shapes and observed CPU values;
    /// it does not require completed live mapping/guard proof before writing
    /// unpublished writable code. Admit/AdmitResume still require that proof.
    /// The owner must publish RX code, RW/NX data and guarded
    /// stacks under the admitted root, enter only by CALL on the declared source
    /// stack with IRQs off, and verify callback/recovery code before execution.
    /// This writes no context or parked return slot and executes no instruction.
    /// Captured GPR/FP state is complete before publication or any C# callback.
    /// Unsupported or unreachable addresses leave the output unchanged.
    /// </summary>
    public static bool EmitStub(byte* output, uint capacity, Owner owner, Descriptor source,
        Descriptor destination, StubPlan plan)
    {
        if (!StubInputs(output, capacity, owner, source, destination, plan)) return false;
        StubWriter dry = new(null, owner.CodeStart + plan.CodeOffset, false);
        if (!EncodeStub(ref dry, owner, source, destination, plan)) return false;
        StubWriter writer = new(output, owner.CodeStart + plan.CodeOffset, false);
        return EncodeStub(ref writer, owner, source, destination, plan);
    }

    /// <summary>
    /// Compare every emitted/padding byte against the retained fixed plan, under
    /// exclusive ownership. This is read-only byte identity, not disassembly,
    /// mapping admission, capture/restore correctness or execution evidence.
    /// </summary>
    public static bool VerifyStub(byte* code, uint capacity, Owner owner, Descriptor source,
        Descriptor destination, StubPlan plan)
    {
        if (!StubInputs(code, capacity, owner, source, destination, plan)) return false;
        StubWriter writer = new(code, owner.CodeStart + plan.CodeOffset, true);
        return EncodeStub(ref writer, owner, source, destination, plan);
    }

    private static bool StubInputs(byte* output, uint capacity, Owner owner, Descriptor source,
        Descriptor destination, StubPlan plan, bool allowInitialEntrySource = false)
    {
        if (AdmitCore(owner, source, destination, false) != Status.Ok ||
            (!allowInitialEntrySource && source.Kind != ResumeKind.CapturedCall) ||
            capacity < StubBytes || ((ulong)output & 15) != 0 || !Range((ulong)output, StubBytes) ||
            Overlap((ulong)output, StubBytes, owner.ContextArena, owner.ContextArenaBytes) ||
            Overlap((ulong)output, StubBytes, source.StackGuardStart, source.StackBytes) ||
            Overlap((ulong)output, StubBytes, destination.StackGuardStart, destination.StackBytes) ||
            Overlap((ulong)output, StubBytes, owner.SwitchStackGuardStart, owner.SwitchStackBytes) ||
            (plan.CodeOffset & 15) != 0 || plan.CodeOffset > owner.CodeBytes || StubBytes > owner.CodeBytes - plan.CodeOffset ||
            !Contains(owner.CodeStart, owner.CodeBytes, plan.RejectRip, 1) ||
            Contains(owner.CodeStart + plan.CodeOffset, StubBytes, plan.RejectRip, 1) ||
            Contains(owner.CodeStart + plan.CodeOffset, StubBytes, source.ResumeRip, 1) ||
            Contains(owner.CodeStart + plan.CodeOffset, StubBytes, destination.ResumeRip, 1) ||
            plan.AdmissionCallback == 0 || !Canonical(plan.AdmissionCallback) ||
            Contains(owner.CodeStart + plan.CodeOffset, StubBytes, plan.AdmissionCallback, 1) ||
            plan.CallbackBytes < 8 || (plan.CallbackContext & 7) != 0 ||
            !Contains(owner.ContextArena, owner.ContextArenaBytes, plan.CallbackContext, plan.CallbackBytes) ||
            (plan.HelperFx & 15) != 0 || !Contains(owner.ContextArena, owner.ContextArenaBytes, plan.HelperFx, 512) ||
            (plan.JumpSlot & 7) != 0 || !Contains(owner.ContextArena, owner.ContextArenaBytes, plan.JumpSlot, 8)) return false;
        if (StubResourceOverlap(source, plan) || StubResourceOverlap(destination, plan) ||
            Overlap(plan.CallbackContext, plan.CallbackBytes, plan.HelperFx, 512) ||
            Overlap(plan.CallbackContext, plan.CallbackBytes, plan.JumpSlot, 8) ||
            Overlap(plan.HelperFx, 512, plan.JumpSlot, 8)) return false;
        byte* fx = (byte*)plan.HelperFx;
        // Helpers start with empty x87 and the initialized Win64 controls;
        // the independently observed hardware mask is never widened by data.
        return *(ushort*)fx == X64ThreadContext.InitialControlWord && fx[4] == 0 &&
            *(uint*)(fx + 24) == X64ThreadContext.InitialMxcsr && *(uint*)(fx + 28) == owner.ObservedMxcsrMask;
    }
    private static bool StubResourceOverlap(Descriptor value, StubPlan plan) =>
        Overlap((ulong)value.Context, value.ContextBytes, plan.CallbackContext, plan.CallbackBytes) ||
        Overlap((ulong)value.Context, value.ContextBytes, plan.HelperFx, 512) ||
        Overlap((ulong)value.Context, value.ContextBytes, plan.JumpSlot, 8);

    private static bool EncodeStub(ref StubWriter w, Owner owner, Descriptor source,
        Descriptor destination, StubPlan plan)
    {
        ulong from = (ulong)source.Context, to = (ulong)destination.Context;
        w.Set(from, 0); // Unpublish before the first saved field; MOV changes no flags.
        for (uint register = 0; register < 16; register++) w.Memory(false, register, from + RegisterOffset(register));
        w.Bytes(0x08245c8d4c, 5); w.Memory(false, 11, from + X64ThreadContext.RspOffset); // r11=entryRSP+8
        w.Bytes(0x241c8b4c, 4); w.Memory(false, 11, from + X64ThreadContext.RipOffset); // exact CALL continuation
        w.Immediate(4, owner.SwitchStackGuardStart + (owner.SwitchStackBytes - Page) - 64);
        w.Bytes(0x5b419c, 3); w.Memory(false, 11, from + X64ThreadContext.RflagsOffset); // pushfq; pop r11
        w.Fx(false, from + X64ThreadContext.FxSaveOffset);
        w.ReadMsr(0xc0000100); w.Memory(false, 0, from + X64ThreadContext.FsBaseOffset);
        w.ReadMsr(0xc0000101); w.Memory(false, 0, from + X64ThreadContext.UserGsBaseOffset); // CPL0: no SWAPGS
        w.Set(from + X64ThreadContext.ReservedOffset, 0); w.Set(from, 1); // Complete single-owner publication last.
        w.Bytes(0xfc, 1); w.Fx(true, plan.HelperFx); // CLD before C# helpers, worker FP already captured.
        w.Immediate(1, plan.CallbackContext); w.Immediate(2, from); w.Immediate(8, to);
        w.Immediate(11, plan.AdmissionCallback);
        w.Bytes(0x20ec8348, 4); w.Bytes(0xd3ff41, 3); w.Bytes(0x20c48348, 4); // Win64 shadow, CALL r11.
        w.Bytes(0x013c, 2); w.Reject(plan.RejectRip); // Exactly byte1 means fresh admission.
        Restore(ref w, to, plan.JumpSlot, plan.RejectRip);
        w.Pad(); return w.Valid;
    }
    private static void Restore(ref StubWriter w, ulong to, ulong jumpSlot, ulong rejectRip)
    {
        w.Memory(true, 0, to + X64ThreadContext.RipOffset); w.Memory(false, 0, jumpSlot);
        w.Memory(true, 0, to + X64ThreadContext.FsBaseOffset); w.WriteVerifyMsr(0xc0000100, rejectRip);
        w.Memory(true, 0, to + X64ThreadContext.UserGsBaseOffset); w.WriteVerifyMsr(0xc0000101, rejectRip);
        w.Fx(true, to + X64ThreadContext.FxSaveOffset);
        w.Bytes(0x35ff, 2); w.Displacement(to + X64ThreadContext.RflagsOffset, 4); w.Bytes(0x9d, 1);
        // Only MOV/JMP follows restored flags. RSP and scratch r11 are restored
        // last through RIP-relative loads; no destination-stack push or RET.
        for (uint register = 0; register < 16; register++)
            if (register != 4 && register != 11) w.Memory(true, register, to + RegisterOffset(register));
        w.Memory(true, 4, to + X64ThreadContext.RspOffset); w.Memory(true, 11, to + X64ThreadContext.R11Offset);
        w.Bytes(0x25ff, 2); w.Displacement(jumpSlot, 4);
    }
    private static uint RegisterOffset(uint register) => register switch
    {
        0 => X64ThreadContext.RaxOffset, 1 => X64ThreadContext.RcxOffset,
        2 => X64ThreadContext.RdxOffset, 3 => X64ThreadContext.RbxOffset,
        4 => X64ThreadContext.RspOffset, 5 => X64ThreadContext.RbpOffset,
        6 => X64ThreadContext.RsiOffset, 7 => X64ThreadContext.RdiOffset,
        _ => X64ThreadContext.R8Offset + (register - 8) * 8
    };
    internal struct StubWriter
    {
        private byte* _output;
        private ulong _address;
        private uint _limit;
        private bool _compare, _valid;
        private uint _position;
        public StubWriter(byte* output, ulong address, bool compare, uint limit = StubBytes)
        { _output = output; _address = address; _compare = compare; _limit = limit; _valid = true; _position = 0; }
        public bool Valid => _valid && _position == _limit;
        public void Bytes(ulong value, int count)
        {
            for (int i = 0; i < count; i++, _position++)
            {
                if (_position >= _limit) { _valid = false; continue; }
                byte next = (byte)(value >> (i * 8));
                if (_output == null) continue;
                if (_compare) { if (_output[_position] != next) _valid = false; }
                else _output[_position] = next;
            }
        }
        public void Pad() => Pad(_limit);
        public void Pad(uint target)
        { if (_position > target || target > _limit) _valid = false; while (_position < target && _position < _limit) Bytes(0xcc, 1); }
        public void Call(ulong target) { Bytes(0xe8, 1); Displacement(target, 4); }
        public void Jump(ulong target) { Bytes(0xe9, 1); Displacement(target, 4); }
        public void Below(ulong target) { Bytes(0x820f, 2); Displacement(target, 4); }
        public void PushMemory(ulong target) { Bytes(0x35ff, 2); Displacement(target, 4); }
        public void PopMemory(ulong target) { Bytes(0x058f, 2); Displacement(target, 4); }
        public void CompareByteLimit(ulong target, byte limit)
        { Bytes(0x3d83, 2); Displacement(target, 5); Bytes(limit, 1); } // cmp dword [rip+disp32],imm8
        public void Displacement(ulong target, uint trailingBytes)
        {
            ulong next = _address + _position + trailingBytes;
            uint bits;
            if (target >= next)
            { ulong distance = target - next; if (distance > 0x7fffffff) _valid = false; bits = (uint)distance; }
            else
            { ulong distance = next - target; if (distance > 0x80000000) _valid = false; bits = unchecked(0U - (uint)distance); }
            Bytes(bits, 4);
        }
        public void Memory(bool load, uint register, ulong address)
        { Bytes(register < 8 ? 0x48UL : 0x4cUL, 1); Bytes(load ? 0x8bUL : 0x89UL, 1);
            Bytes(5 | ((register & 7) << 3), 1); Displacement(address, 4); }
        public void Set(ulong address, uint value)
        { Bytes(0x05c748, 3); Displacement(address, 8); Bytes(value, 4); }
        public void Immediate(uint register, ulong value)
        { Bytes(register < 8 ? 0x48UL : 0x49UL, 1); Bytes(0xb8 + (register & 7), 1); Bytes(value, 8); }
        public void Fx(bool restore, ulong address)
        { Bytes(0xae0f48, 3); Bytes(restore ? 0x0dUL : 5UL, 1); Displacement(address, 4); }
        public void ReadMsr(uint index)
        { Bytes(0xb9, 1); Bytes(index, 4); Bytes(0x320f, 2); Bytes(0x20e2c148, 4); Bytes(0xd00948, 3); }
        public void WriteVerifyMsr(uint index, ulong reject)
        {
            Bytes(0xc28949, 3); // r10=expected RAX, before WRMSR scratch.
            Bytes(0xb9, 1); Bytes(index, 4); Bytes(0xc28948, 3); Bytes(0x20eac148, 4); Bytes(0x300f, 2);
            ReadMsr(index); Bytes(0xd0394c, 3); Reject(reject); // cmp rax,r10.
        }
        public void Reject(ulong target) { Bytes(0x850f, 2); Displacement(target, 4); }
    }

    /// <summary>
    /// Validate trusted owner observations/range shape before context access.
    /// No hardware read, mapping or privileged instruction occurs. Rejection
    /// changes no input; valid-looking pointers still require mapped storage.
    /// </summary>
    public static Status AdmitOwner(Owner owner) => OwnerShape(owner, true);

    /// <summary>Validate declared ranges and independently observed CPU/root
    /// values before emitting or publishing a plan. Live guard/mapping proof is
    /// intentionally not established here; resume admission still requires it.</summary>
    public static Status AdmitPlan(Owner owner) => OwnerShape(owner, false);

    private static Status OwnerShape(Owner owner, bool requireMappingProof)
    {
        if (!owner.Initialized) return Status.Uninitialized;
        if (owner.Key == 0 || owner.Key != owner.CurrentKey ||
            (requireMappingProof && (!owner.GuardsVerified || !owner.MappingsVerified)))
            return Status.InvalidOwner;
        if (!Root(owner.Root) || owner.Root != owner.CurrentRoot) return Status.InvalidRoot;
        if (owner.CpuCount != 1 || owner.Cs != 8 || owner.Ss != 16 || owner.Tr != 24 || owner.Fs != 0 || owner.Gs != 0 ||
            owner.KernelGs != owner.CurrentKernelGs || !Canonical(owner.KernelGs) ||
            (owner.Cr0 & 0x80000001) != 0x80000001 || (owner.Cr0 & 0xc) != 0 ||
            (owner.Cr4 & ~0x7ffUL) != 0 || (owner.Cr4 & 0x220) != 0x220 ||
            (owner.Efer & 0xd00) != 0xd00 || (owner.BasicEdx & 0x07000000) != 0x07000000 ||
            (owner.ExtendedEdx & 0x20100000) != 0x20100000 || (owner.Flags & 2) == 0 ||
            (owner.Flags & ~AdmittedFlags) != 0 || (owner.Flags & 0x400) != 0)
            return Status.UnsupportedCpu;
        uint mask = owner.ObservedMxcsrMask == 0 ? X64ThreadContext.FallbackMxcsrMask : owner.ObservedMxcsrMask;
        if ((mask & ~0xffffU) != 0 || (X64ThreadContext.InitialMxcsr & ~mask) != 0) return Status.UnsupportedCpu;
        if (!Pages(owner.ContextArena, owner.ContextArenaBytes) || !Pages(owner.CodeStart, owner.CodeBytes) ||
            !Stack(owner.SwitchStackGuardStart, owner.SwitchStackBytes)) return Status.InvalidRange;
        if (Overlap(owner.ContextArena, owner.ContextArenaBytes, owner.CodeStart, owner.CodeBytes) ||
            Overlap(owner.ContextArena, owner.ContextArenaBytes, owner.SwitchStackGuardStart, owner.SwitchStackBytes) ||
            Overlap(owner.CodeStart, owner.CodeBytes, owner.SwitchStackGuardStart, owner.SwitchStackBytes)) return Status.Overlap;
        return Status.Ok;
    }

    /// <summary>
    /// Read-only pre-transfer admission of two complete contexts. The owner pins
    /// descriptor identities/ranges and all bytes until transfer/restoration ends.
    /// Entry RSP is8mod16 with return/shadow space; captured CALL continuation RSP
    /// is0mod16 with shadow space. Exact retained RIP, complete FP state and stricter
    /// IRQ-off flags are required. Failure leaves both contexts/owner unchanged.
    /// Native capture/restore and final guest qualification are separate from this
    /// admission result; never count Ok as execution evidence.
    /// </summary>
    public static Status Admit(Owner owner, Descriptor source, Descriptor destination)
        => AdmitCore(owner, source, destination, true);

    /// <summary>Read-only admission of one complete resume context for the trusted
    /// original-context recovery path. The same live ownership/mapping/CPU proof
    /// is required; this neither captures nor authorizes execution on its own.</summary>
    public static Status AdmitResume(Owner owner, Descriptor value)
        => ResumeCore(owner, value, true);

    // Shared planning admission for unpublished lifecycle entry code. Only the
    // live AdmitResume path accepts guard/mapping observations as execution proof.
    internal static Status AdmitResumePlan(Owner owner, Descriptor value)
        => ResumeCore(owner, value, false);

    private static Status ResumeCore(Owner owner, Descriptor value, bool requireMappingProof)
    {
        Status status = OwnerShape(owner, requireMappingProof);
        if (status != Status.Ok) return status;
        status = Shape(owner, value);
        return status != Status.Ok ? status : Values(owner, value);
    }

    private static Status AdmitCore(Owner owner, Descriptor source, Descriptor destination, bool requireMappingProof)
    {
        Status status = OwnerShape(owner, requireMappingProof);
        if (status != Status.Ok) return status;
        status = Shape(owner, source);
        if (status != Status.Ok) return status;
        status = Shape(owner, destination);
        if (status != Status.Ok) return status;
        if (source.Identity == destination.Identity || (uint)source.Identity == (uint)destination.Identity)
            return Status.StaleIdentity;
        if (Overlap((ulong)source.Context, source.ContextBytes, (ulong)destination.Context, destination.ContextBytes) ||
            Overlap(source.StackGuardStart, source.StackBytes, destination.StackGuardStart, destination.StackBytes)) return Status.Overlap;
        status = Values(owner, source);
        return status != Status.Ok ? status : Values(owner, destination);
    }

    private static Status Shape(Owner owner, Descriptor value)
    {
        if ((uint)value.Identity == 0 || (uint)(value.Identity >> 32) == 0 ||
            (uint)value.CurrentIdentity == 0 || (uint)(value.CurrentIdentity >> 32) == 0) return Status.InvalidIdentity;
        if (value.Identity != value.CurrentIdentity) return Status.StaleIdentity;
        if (value.Kind != ResumeKind.Entry && value.Kind != ResumeKind.CapturedCall) return Status.InvalidContinuation;
        ulong context = (ulong)value.Context;
        if (context == 0 || (context & 15) != 0 || value.ContextBytes < X64ThreadContext.Bytes ||
            !Contains(owner.ContextArena, owner.ContextArenaBytes, context, value.ContextBytes)) return Status.InvalidContext;
        if (!Stack(value.StackGuardStart, value.StackBytes)) return Status.InvalidStack;
        if (Overlap(value.StackGuardStart, value.StackBytes, owner.ContextArena, owner.ContextArenaBytes) ||
            Overlap(value.StackGuardStart, value.StackBytes, owner.CodeStart, owner.CodeBytes) ||
            Overlap(value.StackGuardStart, value.StackBytes, owner.SwitchStackGuardStart, owner.SwitchStackBytes)) return Status.Overlap;
        if (!Contains(owner.CodeStart, owner.CodeBytes, value.ResumeRip, 1)) return Status.InvalidContinuation;
        return Status.Ok;
    }
    private static Status Values(Owner owner, Descriptor value)
    {
        if (X64ThreadContext.Validate(value.Context, value.ContextBytes, owner.ObservedMxcsrMask) != X64ThreadContext.Status.Ok)
            return Status.InvalidContext;
        if (value.Context->Rip != value.ResumeRip) return Status.InvalidContinuation;
        if ((value.Context->Rflags & ~AdmittedFlags) != 0) return Status.InvalidFlags;
        ulong low = value.StackGuardStart + Page, high = value.StackGuardStart + (value.StackBytes - Page);
        ulong alignment = value.Kind == ResumeKind.Entry ? 8UL : 0UL;
        ulong shadow = value.Kind == ResumeKind.Entry ? 40UL : 32UL;
        if ((value.Context->Rsp & 15) != alignment || value.Context->Rsp < low + 32 || value.Context->Rsp > high - shadow)
            return Status.InvalidStack;
        if (!Tls(owner, value.Context->FsBase) || !Tls(owner, value.Context->UserGsBase)) return Status.InvalidTls;
        return Status.Ok;
    }
    private static bool Tls(Owner owner, ulong address) => address == 0 || Contains(owner.ContextArena, owner.ContextArenaBytes, address, 8);
    private static bool Root(ulong value) => value != 0 && value < 0x100000000 && (value & (Page - 1)) == 0;
    private static bool Pages(ulong address, ulong bytes) => bytes >= Page && (address & (Page - 1)) == 0 &&
        (bytes & (Page - 1)) == 0 && Range(address, bytes);
    private static bool Stack(ulong address, ulong bytes) => bytes >= 3UL * Page && Pages(address, bytes);
    private static bool Range(ulong address, ulong bytes) => address != 0 && bytes != 0 && bytes - 1 <= ulong.MaxValue - address &&
        Canonical(address) && Canonical(address + (bytes - 1)) && ((address ^ (address + (bytes - 1))) & (1UL << 47)) == 0;
    private static bool Contains(ulong outer, ulong outerBytes, ulong inner, ulong innerBytes) => Range(inner, innerBytes) &&
        inner >= outer && inner - outer <= outerBytes && innerBytes <= outerBytes - (inner - outer);
    private static bool Overlap(ulong a, ulong aBytes, ulong b, ulong bBytes) => a <= b + (bBytes - 1) && b <= a + (aBytes - 1);
    private static bool Canonical(ulong value) => value >> 48 == ((value & (1UL << 47)) == 0 ? 0UL : 0xffffUL);
}
