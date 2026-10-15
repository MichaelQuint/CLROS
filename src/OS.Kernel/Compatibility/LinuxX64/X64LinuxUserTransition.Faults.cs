namespace OS.Kernel.Compatibility.LinuxX64;

public static unsafe partial class X64LinuxUserTransition
{
    public const uint FaultCaptureBytes = 2048;
    public const uint UserInterruptOffset = 0x600;
    private const uint FaultCleanup = FaultCaptureBytes - 16;
    private const ulong ResumeFlag = 0x10000;

    /// <summary>Check the actual CPL0 callback after SWAPGS and adoption of the
    /// retained kernel root. FS remains the captured user's base; GS owns this
    /// gateway; KERNEL_GS holds the captured user's GS. The IRQ-off native owner
    /// supplies genuine observations and proves exclusive mapped data and its
    /// original restoration envelope. Entry-base witnesses stay immutable even
    /// when the desired return frame changes before native WRMSR. This grants
    /// no execution or copy authority.</summary>
    public static bool ValidateCallback(byte* data, uint bytes, ulong kernelRoot,
        ulong root, ulong flags, ulong cs, ulong ss, ulong tr,
        ulong fsBase, ulong gsBase, ulong kernelGsBase, ulong fs, ulong gs) =>
        Buffer(data, bytes, DataBytes, 4096) && PageRange(kernelRoot, 4096) &&
        *(ulong*)(data + PhaseOffset) == 1 && *(ulong*)(data + 0x20) == kernelRoot &&
        *(ulong*)(data + KernelRootOffset) == kernelRoot && root == kernelRoot &&
        (flags & 0x602) == 2 && cs == 8 && (ss == 16 || ss == 0) && tr == 24 && fs == 0 && gs == 0 &&
        fsBase == *(ulong*)(data + CallbackFsOffset) && gsBase == (ulong)data && kernelGsBase == *(ulong*)(data + CallbackGsOffset);

    /// <summary>Emit or compare the explicit GP/PF capture region, separate from
    /// the original 4-KiB gateway. Only normalized user GP/PF stubs and, under
    /// explicit user IRQ policy, the owned E1/IST4 stub may enter it. The owner
    /// retains supervisor RX mappings under both roots, private TSS.RSP0/stack,
    /// original kernel fault handlers, IRQ ownership and callback identity.
    /// Count/compare completes before publication. Zero entry, overlap or invalid
    /// plan changes no output; matching bytes are not execution evidence.</summary>
    public static bool EmitFaultCapture(byte* code, uint bytes, X64LinuxUserTransitionPlan plan) =>
        FaultInputs(code, bytes, plan) && EncodeFaultCapture(null, false, plan) && EncodeFaultCapture(code, false, plan);
    /// <summary>Nonmutating exact comparison of the separately retained fault
    /// entry, including padding and immutable cleanup target. The owner retains
    /// all EmitFaultCapture mapping/lifetime obligations; this grants no entry.</summary>
    public static bool VerifyFaultCapture(byte* code, uint bytes, X64LinuxUserTransitionPlan plan) =>
        FaultInputs(code, bytes, plan) && EncodeFaultCapture(code, true, plan);

    private static bool FaultInputs(byte* code, uint bytes, X64LinuxUserTransitionPlan p) =>
        Admit(p) && p.UserFaultEntry != 0 && Buffer(code, bytes, FaultCaptureBytes, 16) &&
        !Overlap((ulong)code, FaultCaptureBytes, p.ContextAddress, DataBytes) &&
        !Overlap((ulong)code, FaultCaptureBytes, p.UserRoot, 4096) &&
        !Overlap((ulong)code, FaultCaptureBytes, p.KernelRoot, 4096) &&
        !Overlap((ulong)code, FaultCaptureBytes, p.CodeAddress, CodeBytes);

    private static bool EncodeFaultCapture(byte* code, bool compare, X64LinuxUserTransitionPlan p)
    {
        Writer w = new(code, compare, FaultCaptureBytes);
        w.Bytes(0xfa1e0ff3, 4); w.Bytes(0xf8010f, 3); // ENDBR64; SWAPGS. Hardware cleared IF.
        w.GsMemory(false, 14, SavedOffset(14)); w.Immediate(14, p.ContextAddress);
        w.Store(0, ValueOffset); // Original user RAX, before CR2/MSR/frame scratch use.
        if (p.UserInterrupts) w.Store(4,CaptureStackOffset);
        for (uint register = 1; register < 16; register++)
            if (register != 4 && register != 14) w.Store(register, SavedOffset(register));
        w.Set(FrameOffset, 0); // This is a fault, never a synthetic syscall.
        w.Store(7, 0x108); w.Store(6, 0x110); w.Store(2, 0x118);
        w.Store(10, 0x120); w.Store(8, 0x128); w.Store(9, 0x130);
        w.StackLoad(0); w.Store(0, VectorOffset); w.StackLoad(8); w.Store(0, ErrorOffset);
        w.StackLoad(16); w.Store(0, 0x138); w.StackLoad(32); w.Store(0, 0x148);
        w.StackLoad(40); w.Store(0, 0x140); w.Store(0, 8);
        w.StackLoad(24); w.StoreWord(FaultSelectorsOffset); w.StackLoad(48); w.StoreWord(FaultSelectorsOffset + 2);
        w.Bytes(0xd0200f, 3); w.Store(0, FaultAddressOffset);
        w.ReadMsr(0xc0000100); w.Store(0, 0x150); w.Store(0, CallbackFsOffset);
        w.ReadMsr(0xc0000102); w.Store(0, 0x158); w.Store(0, CallbackGsOffset);
        w.Set(OutcomeOffset, 3); w.Fx(false, UserFxOffset); w.Fx(true, KernelFxOffset); w.Bytes(0xfc, 1);
        w.Bytes(0xd8200f, 3); w.CompareField(0, 0x28); w.Branch(0x85, FaultCleanup);
        w.Load(0, 0x20); w.Bytes(0xd8220f, 3); // No C# callback under the user root.
        w.Load(4, 0); // Discard the hardware frame; use the admitted aligned kernel stack.
        w.Load(1, 0xd8); w.Bytes(0xf2894c, 3); w.Call(0x38);
        w.Bytes(0x013c, 2); w.Branch(0x85, FaultCleanup);
        w.Load(0, 0x28); w.Bytes(0xd8220f, 3);
        w.Load(0, 0x150); w.WriteMsr(0xc0000100);
        w.Load(0, 0x158); w.WriteMsr(0xc0000102);
        w.Bytes(0x336a, 2); w.PushField(0x140); w.PushField(0x148); w.Bytes(0x3b6a, 2); w.PushField(0x138);
        w.Fx(true, UserFxOffset);
        for (uint register = 1; register < 16; register++)
            if (register != 4 && register != 14) w.Load(register, SavedOffset(register));
        w.Load(0, ValueOffset); w.Load(14, SavedOffset(14)); w.Bytes(0xf8010f, 3); w.Bytes(0xcf48, 2);
        if (p.UserInterrupts)
        {
            w.Pad(UserInterruptOffset); w.Bytes(0xfa1e0ff3,4);
            w.Bytes(0x3b08247c8348,6); // CMP qword[RSP+8],3B: only genuine CPL3 no-error IRQ frames.
            w.Branch(0x85,FaultCleanup-16);
            w.Bytes(0x006a,2); w.Bytes(0x68,1); w.Bytes(0xe1,4); // Positive vector225, not sign-extended imm8.
            w.Jump(0); // Shared complete capture after normalized [vector,error,RIP,CS,flags,RSP,SS].
            w.Pad(FaultCleanup-16); w.Bytes(0xf4fa,2); w.Bytes(0xfdeb,2); // CLI; HLT; JMP HLT, retain ownership.
        }
        w.Pad(FaultCleanup); w.Bytes(0x25ff, 2); w.Bytes(0, 4); w.Bytes(p.CodeAddress + ExitOffset, 8);
        w.Pad(FaultCaptureBytes); return w.Valid;
    }

    /// <summary>Copy a genuine full GP/PF capture as untrusted signal-context
    /// data. The native owner first authenticates this exact active gateway,
    /// current generation and kernel-root callback. Unlike return admission,
    /// unmapped/NX RIP, invalid RSP and hardware RF are allowed here; they grant
    /// no continuation. Selectors, error provenance and FP mask must match. A
    /// checked handler image or checked sigreturn is required before IRETQ.</summary>
    public static bool CaptureFaultUserImage(byte* data, uint bytes, UserImage* image, uint imageBytes, uint mxcsr,bool userInterrupts=false)
    {
        if (!ImageBuffers(data, bytes, image, imageBytes) || *(ulong*)(data + PhaseOffset) != 1 ||
            *(ulong*)(data + OutcomeOffset) != 3 || *(ushort*)(data + FaultSelectorsOffset) != UserCs ||
            *(ushort*)(data + FaultSelectorsOffset + 2) != UserSs ||
            !FaultProvenance(*(ulong*)(data + VectorOffset), *(ulong*)(data + ErrorOffset))) return false;
        UserImage next = default; ReadCapturedImage(data,&next);
        if (!ValidateFaultUserImage(&next, UserImageBytes, mxcsr,userInterrupts)) return false;
        *image = next; return true;
    }

    internal static bool FaultProvenance(ulong vector, ulong error) =>
        vector == 13 ? error <= 0xffff : vector == 14 && (error & ~0x17UL) == 0 && (error & 4) != 0;
    internal static bool ValidateFaultUserImage(UserImage* image, uint bytes, uint mxcsr,bool userInterrupts=false) =>
        ImageStorage(image, bytes) && image->Frame.Number == 0 &&
        LinuxSyscallGateway.ValidateUserFlags(image->Frame.Rflags & ~ResumeFlag,userInterrupts) &&
        (image->Frame.FsBase == 0 || image->Frame.FsBase >= 4096 && image->Frame.FsBase < LinuxSyscallGateway.UserEnd) &&
        (image->Frame.GsBase == 0 || image->Frame.GsBase >= 4096 && image->Frame.GsBase < LinuxSyscallGateway.UserEnd) && UserFx(image->Fx, mxcsr);

    /// <summary>Copy a complete actual E1 CPL3 interrupt capture under an
    /// explicitly admitted user-IRQ policy. Native ownership/IST/root/APIC
    /// provenance remain the caller's obligation. Unlike a fault image, its
    /// continuation must pass user mapping/IF/FP admission or the current
    /// gate's exact retained exit-tail policy. Hardware-entry RF is cleared in
    /// the copied continuation only; raw evidence stays intact. Debug resume
    /// semantics are unsupported. Ordinary syscall/signal returns gain no RF
    /// authority. No
    /// syscall, scheduling, time advance or user resumption is performed.</summary>
    public static bool CaptureInterruptUserImage(byte* data,uint bytes,UserImage* image,uint imageBytes,uint mxcsr,LinuxSyscallAccess user)
    {
        if (!user.UserInterrupts || !ImageBuffers(data,bytes,image,imageBytes) || *(ulong*)(data+PhaseOffset)!=1 ||
            *(ulong*)(data+OutcomeOffset)!=3 || *(ulong*)(data+VectorOffset)!=0xe1 || *(ulong*)(data+ErrorOffset)!=0 ||
            *(ushort*)(data+FaultSelectorsOffset)!=UserCs || *(ushort*)(data+FaultSelectorsOffset+2)!=UserSs) return false;
        UserImage next=default; ReadCapturedImage(data,&next);
        next.Frame.Rflags &= ~ResumeFlag;
        if (next.Frame.Number!=0 || !ValidateUserImage(&next,UserImageBytes,mxcsr,user)) return false;
        *image=next; return true;
    }
    private static void ReadCapturedImage(byte* data,UserImage* image)
    {
        image->Frame=*(LinuxSyscallFrame*)(data+FrameOffset); image->Value=*(long*)(data+ValueOffset);
        for (uint i=0;i<14;i++) image->Gpr[i]=*(ulong*)(data+0x160+i*8);
        for (uint i=0;i<512;i++) image->Fx[i]=data[UserFxOffset+i];
    }
}
