namespace OS.Kernel.Compatibility.LinuxX64;

public enum LinuxSyscallAction : byte
{
    Rejected,
    Return,
    Terminate,
    Blocked,
    ThreadExited,
    RestoreUser,
    Yielded
}
