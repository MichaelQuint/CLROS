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
#if !NOSTDLIB_EFI || M16_025_PROBE || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || CLROS_LINUX_SOCKETS
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || CLROS_LINUX_SOCKETS
#endif
