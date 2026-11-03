namespace OS.Kernel.Compatibility.LinuxX64;

public enum LinuxSyscallGateStatus : byte
{
    Uninitialized,
    Active,
    Exited,
    InvalidAdmission,
    AlreadyConsumed,
    InvalidReturn,
    CallLimit,
    CopyFailed,
    SinkFailed,
    Blocked
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
