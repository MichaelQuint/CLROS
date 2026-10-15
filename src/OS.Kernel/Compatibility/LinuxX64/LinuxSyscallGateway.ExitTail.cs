namespace OS.Kernel.Compatibility.LinuxX64;

public static unsafe partial class LinuxSyscallGateway
{
    internal struct ExitTailContext { internal ulong Rip, Rsp, Fs, Flags, Start, Bytes; }
    private static bool EmptyExitTail(ExitTailContext t) => (t.Rip|t.Rsp|t.Fs|t.Flags|t.Start|t.Bytes)==0;

    // The admitted static RX libc text cannot be replaced through this bounded
    // memory adapter. Match the complete linked sequence, including munmap's
    // mov eax,11 and actual SYSCALL immediately before the return RIP.
    private static bool ExitTailCode(ulong rip,LinuxSyscallAccess user)
    {
        if (rip<4103 || rip>=0x100000000 || user.CopyFromUser==null ||
            !ValidateRange(rip-7,17,LinuxUserPageAccess.Read|LinuxUserPageAccess.Execute,user)) return false;
        byte* code=stackalloc byte[17];
        if (user.CopyFromUser(user.Context,rip-7,code,17)!=1) return false;
        return code[0]==0xb8 && code[1]==11 && code[2]==0 && code[3]==0 && code[4]==0 && code[5]==0x0f && code[6]==5 &&
            code[7]==0x48 && code[8]==0x31 && code[9]==0xff && code[10]==0xb8 && code[11]==60 && code[12]==0 &&
            code[13]==0 && code[14]==0 && code[15]==0x0f && code[16]==5;
    }
    private static bool ArmExitTail(ref LinuxSyscallGateState state,LinuxSyscallFrame frame)
    {
        ulong start=frame.Argument0,bytes=frame.Argument1;
        if (!state.Admission.EnableThreads || state.Access.ReturnPolicy==null || frame.Number!=11 || state.HasExitTail ||
            start<0x100000000 || start>=UserEnd || (start&4095)!=0 || bytes==0 || bytes>16*1024*1024 || (bytes&4095)!=0 ||
            bytes>UserEnd-start || frame.Rsp<start+8 || frame.Rsp>start+bytes || frame.FsBase<start || frame.FsBase>=start+bytes ||
            frame.GsBase!=0 || !ExitTailCode(frame.Rip,state.Access)) return false;
        for (ulong page=start;page<start+bytes;page+=4096)
            if (state.Access.PageAccess(state.Access.Context,page)!=0) return false;
        state.ExitTail=new(){Rip=frame.Rip,Rsp=frame.Rsp,Fs=frame.FsBase,Flags=frame.Rflags,Start=start,Bytes=bytes};
        return true;
    }

    /// <summary>Value-only decision for a retained per-generation gate: zero
    /// keeps ordinary mapping checks, one admits only its exact immutable libc
    /// continuation/final exit, two rejects. The IRQ-off native owner must also
    /// prove current identity, fully blocked signals and zero handler depth.
    /// No mapping, image, syscall, timer or scheduler state changes here.</summary>
    public static byte ExitTailPolicy(ref LinuxSyscallGateState state,LinuxSyscallFrame frame)
    {
        if (!state.HasExitTail) return 0;
        ExitTailContext t=state.ExitTail; LinuxSyscallAccess user=state.Access;
        if (state.StatusValue!=LinuxSyscallGateStatus.Active || frame.Rsp!=t.Rsp || frame.FsBase!=t.Fs || frame.GsBase!=0 ||
            frame.Rip<t.Rip || !ValidateUserFlags(frame.Rflags,user.UserInterrupts) || !ExitTailCode(t.Rip,user)) return 2;
        ulong offset=frame.Rip-t.Rip;
        bool position=frame.Number==11 ? offset==0 : frame.Number==60 ? offset==10 && frame.Argument0==0 :
            frame.Number==0 && (offset==0 || offset==3 || offset==8);
        if (!position || user.PageAccess(user.Context,(t.Rsp-8)&~4095UL)!=0 || user.PageAccess(user.Context,(t.Rsp-1)&~4095UL)!=0 ||
            user.PageAccess(user.Context,t.Fs&~4095UL)!=0) return 2;
        // XOR fixes CF/PF/ZF/SF/OF; AF is architecturally undefined. Other
        // admitted status/IF bits remain those actually captured at munmap.
        ulong affected=0x8d5;
        if (offset==0 ? frame.Rflags!=t.Flags : (frame.Rflags&~0x10UL)!=((t.Flags&~affected)|0x44)) return 2;
        return 1;
    }
}
