using OS.Pal;

namespace OS.Kernel.Compatibility.LinuxX64;

public static unsafe partial class LinuxSignalServices
{
    public const uint FrameBytes = 440, FpBytes = 512, MaximumNesting = 4;
    private const uint ContextOffset = 8, MachineOffset = 48, MaskOffset = 304, InformationOffset = 312;
    private const ulong Restorer = 0x04000000, SignalOnStack = 0x08000000;
    private const ulong NoDefer = 0x40000000, ResetHand = 0x80000000;

    /// <summary>Delivery at an authenticated user-return boundary. Masked is not
    /// consumption: the pending owner must retain it. Unsupported covers Linux
    /// job control, which this single-workload owner cannot truthfully emulate.
    /// BadFrame requires owned SIGSEGV termination without resuming user code.</summary>
    public enum Delivery : byte { Rejected, Masked, Ignored, Delivered, Terminate, Unsupported, BadFrame, None }

    internal static Delivery Deliver(State* s, ulong process, OSHostHandle identity, uint signal, int code,
        uint pid, X64LinuxUserTransition.UserImage* image, uint mxcsr, ref LinuxSignalMask mask,
        ulong faultAddress, ulong error, uint trap)
    {
        if (!Bound(s, process, identity) || signal is < 1 or > 64 || pid == 0 ||
            !X64LinuxUserTransition.ValidateUserImage(image, X64LinuxUserTransition.UserImageBytes, mxcsr, s->User)) return Delivery.Rejected;
        if (mask.Contains(signal)) return Delivery.Masked;
        Disposition* action = (Disposition*)s->Actions + signal - 1;
        if (action->Handler == 1) return Delivery.Ignored;
        if (action->Handler == 0)
            return signal is 17 or 18 or 23 or 28 ? Delivery.Ignored :
                signal is 19 or 20 or 21 or 22 ? Delivery.Unsupported : Delivery.Terminate;
        ThreadState* thread = (ThreadState*)s->Threads + ((uint)identity.Value - 1);
        if (thread->Depth >= MaximumNesting || (action->Flags & Restorer) == 0 ||
            !LinuxSyscallGateway.ValidateRange(action->Handler, 1, LinuxUserPageAccess.Execute, s->User) ||
            !LinuxSyscallGateway.ValidateRange(action->Restorer, 1, LinuxUserPageAccess.Execute, s->User)) return Delivery.BadFrame;

        ulong top = image->Frame.Rsp;
        bool on = OnAlternate(thread->Stack, top);
        bool useAlternate = (action->Flags & SignalOnStack) != 0 && !on && (thread->Stack.Flags & Disabled) == 0;
        if (top < 128) return Delivery.BadFrame;
        top -= 128; // Preserve the interrupted SysV red zone on the ordinary stack.
        if (useAlternate)
        {
            if (thread->Stack.Bytes == 0 || thread->Stack.Address < 4096 || thread->Stack.Bytes > LinuxSyscallGateway.UserEnd ||
                thread->Stack.Address > LinuxSyscallGateway.UserEnd - thread->Stack.Bytes) return Delivery.BadFrame;
            top = thread->Stack.Address + thread->Stack.Bytes;
        }
        if (top < FpBytes + FrameBytes + 88) return Delivery.BadFrame;
        ulong fp = (top - FpBytes) & ~63UL;
        ulong frame = ((fp - FrameBytes) & ~15UL) - 8;
        if ((useAlternate || on) && (frame < thread->Stack.Address ||
            fp + FpBytes - thread->Stack.Address > thread->Stack.Bytes)) return Delivery.BadFrame;
        if (!LinuxSyscallGateway.ValidateRange(frame - 8, FrameBytes + 8, LinuxUserPageAccess.Write, s->User) ||
            !LinuxSyscallGateway.ValidateRange(fp, FpBytes, LinuxUserPageAccess.Write, s->User)) return Delivery.BadFrame;

        byte* header = stackalloc byte[(int)FrameBytes];
        byte* fx = stackalloc byte[(int)FpBytes];
        for (uint i = 0; i < FrameBytes; i++) header[i] = 0;
        // This deliberately uses the legacy FXSR representation: no XSAVE,
        // magic record, extended size or trailing xstate payload is advertised.
        for (uint i = 0; i < FpBytes; i++) fx[i] = i < 464 ? image->Fx[i] : (byte)0;
        *(ulong*)header = action->Restorer;
        *(ulong*)(header + ContextOffset) = 6; // UC_SIGCONTEXT_SS | UC_STRICT_RESTORE_SS.
        AlternateStack saved = StackView(thread->Stack, image->Frame.Rsp);
        *(AlternateStack*)(header + 24) = saved;
        WriteMachine(header + MachineOffset, image, fp, mask.Blocked, faultAddress, error, trap);
        *(ulong*)(header + MaskOffset) = mask.Blocked;
        *(uint*)(header + InformationOffset) = signal;
        *(int*)(header + InformationOffset + 8) = code;
        if (code <= 0) *(uint*)(header + InformationOffset + 16) = pid; // uid remains zero.
        else *(ulong*)(header + InformationOffset + 16) = faultAddress;

        X64LinuxUserTransition.UserImage next = *image;
        next.Frame.Rip = action->Handler; next.Frame.Rsp = frame; next.Frame.Rflags &= ~0x400UL; // Handler starts with DF clear.
        next.Value = 0;
        next.Gpr[3] = signal; next.Gpr[2] = frame + InformationOffset; next.Gpr[4] = frame + ContextOffset;
        // Linux clears the handler's FPU state after saving the interrupted
        // image. Do not enter with an occupied x87 stack or altered controls.
        for (uint i = 0; i < FpBytes; i++) next.Fx[i] = 0;
        *(ushort*)next.Fx = 0x37f; *(uint*)(next.Fx + 24) = 0x1f80; *(uint*)(next.Fx + 28) = mxcsr;
        Arguments(&next);
        if (!X64LinuxUserTransition.ValidateUserImage(&next, X64LinuxUserTransition.UserImageBytes, mxcsr, s->User)) return Delivery.BadFrame;
        // A failed user copy can leave user bytes changed, but publishes no
        // kernel frame/mask/image. The caller terminates rather than retrying it.
        if (!Output(s, fp, fx, FpBytes) || !Output(s, frame, header, FrameBytes)) return Delivery.BadFrame;
        thread->Frames[thread->Depth++] = frame;
        if (useAlternate && (thread->Stack.Flags & AutoDisarm) != 0) thread->Stack = new() { Flags = Disabled };
        mask = new(mask.Blocked | action->Mask | ((action->Flags & NoDefer) == 0 ? 1UL << (int)(signal - 1) : 0));
        if ((action->Flags & ResetHand) != 0) action->Handler = 0;
        *image = next;
        return Delivery.Delivered;
    }

    internal static bool Return(State* s, ulong process, OSHostHandle identity, X64LinuxUserTransition.UserImage* image,
        uint mxcsr, ref LinuxSignalMask mask)
    {
        if (!Bound(s, process, identity) || image == null || image->Frame.Rsp < 8) return false;
        ThreadState* thread = (ThreadState*)s->Threads + ((uint)identity.Value - 1);
        ulong frame = image->Frame.Rsp - 8; // RET has already popped pretcode before the restorer's syscall15.
        if (thread->Depth == 0 || thread->Depth > MaximumNesting || thread->Frames[thread->Depth - 1] != frame) return false;
        byte* header = stackalloc byte[(int)FrameBytes];
        if (!Input(s, frame, header, FrameBytes) || *(ulong*)(header + ContextOffset) != 6) return false;
        byte* sc = header + MachineOffset;
        // No privilege/selector or xstate expansion is admitted by this profile.
        if (*(ushort*)(sc + 144) != X64LinuxUserTransition.UserCs || *(ushort*)(sc + 146) != 0 ||
            *(ushort*)(sc + 148) != 0 || *(ushort*)(sc + 150) != X64LinuxUserTransition.UserSs) return false;
        for (uint i = 192; i < 256; i++) if (sc[i] != 0) return false;
        ulong fp = *(ulong*)(sc + 184);
        if ((fp & 15) != 0) return false;
        X64LinuxUserTransition.UserImage next = *image;
        if (!Input(s, fp, next.Fx, FpBytes)) return false;
        for (uint i = 464; i < FpBytes; i++) if (next.Fx[i] != 0) return false;
        ulong requestedFlags = *(ulong*)(sc + 136);
        if ((requestedFlags & 2) == 0 || (requestedFlags & ~LinuxSyscallGateway.UserRflagsMask) != 0) return false;
        ReadMachine(sc, &next);
        AlternateStack stack = *(AlternateStack*)(header + 24);
        uint mode = stack.Flags & ~AutoDisarm;
        if (mode is not (0 or OnStack or Disabled)) return false;
        if (mode == Disabled) stack.Address = stack.Bytes = 0;
        else if (stack.Bytes < MinimumStackBytes || stack.Address < 4096 || stack.Bytes > LinuxSyscallGateway.UserEnd ||
            stack.Address > LinuxSyscallGateway.UserEnd - stack.Bytes) return false;
        stack.Padding = 0;
        if (!X64LinuxUserTransition.ValidateUserImage(&next, X64LinuxUserTransition.UserImageBytes, mxcsr, s->User)) return false;
        // Accept legitimate user edits to GPRs, RIP/RSP, x87/SSE and blocked
        // mask. FS/GS bases remain the current thread's values, as in Linux x64;
        // the obsolete 16-bit context fields never become privileged MSR input.
        thread->Stack = stack; thread->Frames[--thread->Depth] = 0;
        mask = new(*(ulong*)(header + MaskOffset));
        *image = next; return true;
    }

    private static AlternateStack StackView(AlternateStack stack, ulong rsp)
    {
        stack.Flags = (stack.Bytes == 0 ? Disabled : OnAlternate(stack, rsp) ? OnStack : 0) | (stack.Flags & AutoDisarm);
        stack.Padding = 0; return stack;
    }
    private static void WriteMachine(byte* sc, X64LinuxUserTransition.UserImage* image, ulong fp, ulong mask,
        ulong faultAddress, ulong error, uint trap)
    {
        // Linux sigcontext, deliberately independent of the gateway's GPR order.
        ulong* q = (ulong*)sc;
        q[0] = image->Gpr[5]; q[1] = image->Gpr[6]; q[2] = image->Gpr[7]; q[3] = image->Gpr[13];
        q[4] = image->Gpr[8]; q[5] = image->Gpr[9]; q[6] = image->Gpr[10]; q[7] = image->Gpr[11];
        q[8] = image->Gpr[3]; q[9] = image->Gpr[2]; q[10] = image->Gpr[1]; q[11] = image->Gpr[0];
        q[12] = image->Gpr[4]; q[13] = unchecked((ulong)image->Value); q[14] = image->Gpr[12];
        q[15] = image->Frame.Rsp; q[16] = image->Frame.Rip; q[17] = image->Frame.Rflags;
        *(ushort*)(sc + 144) = X64LinuxUserTransition.UserCs; *(ushort*)(sc + 150) = X64LinuxUserTransition.UserSs;
        q[19] = error; q[20] = trap; q[21] = mask; q[22] = faultAddress; q[23] = fp;
    }
    private static void ReadMachine(byte* sc, X64LinuxUserTransition.UserImage* image)
    {
        ulong* q = (ulong*)sc;
        image->Gpr[5] = q[0]; image->Gpr[6] = q[1]; image->Gpr[7] = q[2]; image->Gpr[13] = q[3];
        image->Gpr[8] = q[4]; image->Gpr[9] = q[5]; image->Gpr[10] = q[6]; image->Gpr[11] = q[7];
        image->Gpr[3] = q[8]; image->Gpr[2] = q[9]; image->Gpr[1] = q[10]; image->Gpr[0] = q[11];
        image->Gpr[4] = q[12]; image->Value = unchecked((long)q[13]); image->Gpr[12] = q[14];
        image->Frame.Rsp = q[15]; image->Frame.Rip = q[16];
        // Linux FIX_EFLAGS: user-editable status/control flags only. The active
        // admission further refuses TF/RF/AC and keeps IF/IOPL outside user input.
        image->Frame.Rflags = (image->Frame.Rflags & ~0x50dd5UL) | (q[17] & 0x50dd5UL);
        Arguments(image);
    }
    private static void Arguments(X64LinuxUserTransition.UserImage* image)
    {
        image->Frame.Argument0 = image->Gpr[3]; image->Frame.Argument1 = image->Gpr[2]; image->Frame.Argument2 = image->Gpr[4];
        image->Frame.Argument3 = image->Gpr[7]; image->Frame.Argument4 = image->Gpr[5]; image->Frame.Argument5 = image->Gpr[6];
    }
}
