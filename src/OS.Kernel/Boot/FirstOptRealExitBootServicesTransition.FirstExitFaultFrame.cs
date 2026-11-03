using OS.Kernel.Platform;
using OS.Kernel.Diagnostics;
using OS.Kernel.Memory;
using OS.Std.Hash;

#if M16_004_PROBE || M16_005_PROBE
using OS.Kernel.Compatibility.LinuxX64;
#endif
namespace OS.Boot;
// Stack layout after the existing normalizer and the terminal stub's 15 pushes.
// The handler never restores registers or returns through this frame.
public struct FirstExitFaultFrame
{
    public ulong R15, R14, R13, R12, R11, R10, R9, R8;
    public ulong Rbp, Rdi, Rsi, Rdx, Rcx, Rbx, Rax;
    public ulong Vector, HasErrorCode, ErrorCode, Rip, Cs, Rflags, Rsp, Ss;
}
