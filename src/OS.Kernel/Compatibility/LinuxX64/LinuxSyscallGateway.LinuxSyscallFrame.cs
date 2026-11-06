namespace OS.Kernel.Compatibility.LinuxX64;
/// <summary>Captured Linux x64 syscall values; RCX/R11 supply return RIP/flags.</summary>

public struct LinuxSyscallFrame
{
    public ulong Number, Argument0, Argument1, Argument2, Argument3, Argument4, Argument5;
    public ulong Rip, Rsp, Rflags, FsBase, GsBase;
}
