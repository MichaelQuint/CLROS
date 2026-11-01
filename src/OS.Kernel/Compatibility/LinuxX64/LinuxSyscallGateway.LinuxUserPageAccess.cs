namespace OS.Kernel.Compatibility.LinuxX64;
public enum LinuxUserPageAccess : byte
{
    None = 0,
    Read = 1,
    Write = 2,
    Execute = 4
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
