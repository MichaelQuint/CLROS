namespace OS.Kernel.Compatibility.LinuxX64;

public static unsafe partial class X64LinuxUserTransition
{
    /// <summary>User-only saved image. This is copied storage, not an FXRSTOR
    /// operand: its Fx field need not be 16-byte aligned. Never alias a live
    /// image with the fixed gateway or another thread's retained image. The
    /// owning thread generation and all backing/mapping lifetimes stay external.</summary>

    public struct UserImage
    {
        public LinuxSyscallFrame Frame;
        public long Value;
        // Native 160h..1CFh: RBX,RBP,RSI,RDI,RDX,R8,R9,R10,R12-R15,RCX,R11.
        public fixed ulong Gpr[14];
        public fixed byte Fx[512];
    }
}
