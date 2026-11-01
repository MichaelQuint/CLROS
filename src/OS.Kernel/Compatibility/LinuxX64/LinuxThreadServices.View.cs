using OS.Kernel.Threading;
using OS.Pal;

namespace OS.Kernel.Compatibility.LinuxX64;
public unsafe partial struct LinuxThreadServices
{
#if !NOSTDLIB_EFI || M16_025_PROBE
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || CLROS_LINUX_SOCKETS
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
    /// <summary>Non-owning current generation snapshot. A default view is not a
    /// live thread. Saved user data may change at the next syscall; no pin or
    /// native execution authority is acquired by reading it.</summary>

    public readonly struct View
    {
        public readonly OSHostHandle Kernel;
        public readonly uint Tid, ExitCode;
        public readonly bool Exiting;
        public readonly ulong ClearTid, BlockedSignals;
        public readonly LinuxSyscallFrame Frame;
        public readonly KernelThreadWait Wait;
        /// <summary>Retained wait observation: 0 none, 1 futex, 2 pipe, 3 epoll,
        /// 4 read-only disk, 5 writable disk.
        /// Deadline is absolute monotonic nanoseconds, or zero for an untimed
        /// wait. Both are zero without a valid Wait. Completion may be ready
        /// while the wait and its backing remain retained until consumption.</summary>

        public readonly uint WaitSource;
        /// <summary>Absolute monotonic nanoseconds for a futex or epoll wait;
        /// zero for an untimed wait, a pipe wait or no valid Wait. Reading this
        /// snapshot neither advances the clock nor consumes completion.</summary>

        public readonly ulong WaitDeadline;
        /// <summary>Current owned signal nesting and top user frame address,
        /// with the private hardware tag removed. Both are zero without a
        /// retained frame; these values grant no read/write or return authority.</summary>

        public readonly ulong SignalDepth, SignalFrame;
        public readonly uint PipeTransferredBytes;
        public readonly bool PipePending;
        internal View(Slot s, ulong depth, ulong signalFrame)
        {
            Kernel = s.Kernel;
            Tid = s.Tid;
            ExitCode = s.ExitCode;
            Exiting = s.Exiting != 0;
            ClearTid = s.ClearTid;
            BlockedSignals = s.Mask.Blocked;
            Frame = s.Image.Frame;
            Wait = s.Wait;
            WaitSource = s.Wait.IsValid ? s.WaitSource : 0;
            WaitDeadline = s.Wait.IsValid ? s.WaitDeadline : 0;
            SignalDepth = depth;
            SignalFrame = signalFrame;
            PipeTransferredBytes = s.PipeOperation.TransferredBytes;
            PipePending = s.PipeOperation.IsActive;
        }
    }
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
