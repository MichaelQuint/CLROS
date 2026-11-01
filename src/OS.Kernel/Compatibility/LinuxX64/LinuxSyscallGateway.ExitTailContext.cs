namespace OS.Kernel.Compatibility.LinuxX64;
public static unsafe partial class LinuxSyscallGateway
{
    internal struct ExitTailContext
    {
        internal ulong Rip, Rsp, Fs, Flags, Start, Bytes;
    }
}
