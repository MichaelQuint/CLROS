namespace OS.Kernel.Compatibility.LinuxX64;
public unsafe struct LinuxSyscallAccess
{
    public void* Context;
    // Trusted owner policy, copied at admission. Only explicit threaded user
    // IRQ mode may retain IF=1; kernel callbacks themselves still require IF=0.
    public bool UserInterrupts;
    // Only actual present USER leaves may return access. Read is mandatory;
    // RW+X is forbidden. The owner binds lookup/copy to the active admitted root.
    public delegate* unmanaged<void*, ulong, byte> PageAccess;
    // After complete range preflight, copy exactly the requested user bytes.
    // The owner prevents mapping changes and proves scratch/frame nonaliasing.
    public delegate* unmanaged<void*, ulong, byte*, uint, byte> CopyFromUser;
    public delegate* unmanaged<void*, uint, byte*, uint, byte> Write;
    // Explicit M16-006 extension only; executes on the separately owned kernel
    // root, publishes no transfer, and returns a Linux value/negative errno.
    public delegate* unmanaged<void*, LinuxSyscallFrame*, long> Startup;
    // Explicit M16-007 fd extension; owns its separate retained tables/scratch.
    public delegate* unmanaged<void*, LinuxSyscallFrame*, long> Descriptors;
    // Explicit M16-008 thread AND descriptor extension. It wraps every fd
    // mutation so pipe readiness cannot bypass the actual thread wait owner.
    // ARCH_SET_FS/GS and explicit checked syscall15 RestoreUser may change the
    // return frame; all other return/control fields remain immutable. A blocked
    // result is a staged scheduler wait, not a fabricated Linux return value.
    public delegate* unmanaged<void*, LinuxSyscallFrame*, LinuxSyscallResult> Threads;
    // Optional threaded-owner continuation policy: 0 retains ordinary mapped
    // admission, 1 admits its authenticated exit tail, every other value rejects.
    // Called under the same IRQ-off root/current-generation ownership as lookup.
    // The frame is borrowed for this synchronous call; do not retain or mutate it.
    public delegate* unmanaged<void*, LinuxSyscallFrame*, byte> ReturnPolicy;
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
