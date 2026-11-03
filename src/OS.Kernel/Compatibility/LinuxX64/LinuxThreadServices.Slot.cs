using OS.Kernel.Threading;
using OS.Pal;

namespace OS.Kernel.Compatibility.LinuxX64;

public unsafe partial struct LinuxThreadServices
{
    /// <summary>Exclusive kernel metadata, never user-mapped or copied live.
    /// Image is a borrowed saved user value; the process memory owner, not this
    /// slot, owns the application's shared address space, stack and TLS backing.</summary>

    public struct Slot
    {
        internal OSHostHandle Kernel;
        internal uint Tid, ExitCode, Exiting;
        internal ulong ClearTid;
        internal LinuxSignalMask Mask;
        internal KernelThreadWait Wait;
        internal uint FutexCellIndex, WaitSource;
        internal LinuxDescriptorServices.PipeOperation PipeOperation;
#if !NOSTDLIB_EFI || M16_025_PROBE
        internal LinuxDescriptorServices.DiskToken DiskOperation;
        internal uint DiskOutputBytes;
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
        internal LinuxDescriptorServices.WritableDiskToken WritableDiskOperation;
        internal uint WritableDiskOutputBytes;
#endif
#if !NOSTDLIB_EFI || CLROS_LINUX_SOCKETS
        internal LinuxEpollServices.Operation EpollOperation;
#endif
        internal uint RestartMode; // Unresolved decision: 1 original syscall, 2 timed restart.
        internal LinuxSyscallFrame WaitCall;
        internal ulong WaitDeadline;
        internal LinuxSyscallFrame TimedRestartCall;
        internal ulong TimedRestartDeadline;
        internal bool TimedRestartArmed;
        internal X64LinuxUserTransition.UserImage Image;
    }
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || CLROS_LINUX_SOCKETS
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || CLROS_LINUX_SOCKETS
#endif
#if !NOSTDLIB_EFI || M16_025_PROBE
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || CLROS_LINUX_SOCKETS
#endif
#if !NOSTDLIB_EFI || M16_025_PROBE
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || CLROS_LINUX_SOCKETS
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
}
