#if M16_025_PROBE
using OS.Storage;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Threading;
using Block = OS.Storage.ContinuingReadOnlyBlockService;

namespace OS.Boot;

#pragma warning disable 649 // Eight contiguous caller-owned records are written through admitted native pointers.

public static unsafe partial class EfiEntry
{
    private static partial class M03FirstExitAdapter
    {
        // Scalars only: never copy a live block, filesystem, descriptor or
        // thread owner. These observations are selected by the entire canonical
        // ExitPending package, not a path, fd, address or application marker.
        private struct NativeDiskExitObservation
        {
            internal ulong Main, Child, Ordinal, Rip, WaitSequence;
            internal ulong RequestOwner, RequestEpoch, RequestGeneration;
            internal ulong BindingGeneration, DescriptionPosition, FileGeneration, Inode;
            internal ulong FsOwner, FsEpoch, FsGeneration, BlockOwner, BlockEpoch, BlockGeneration, Sector;
            internal ulong Submitted, Completed, Copied, Cancelled, StopCompleted, StopCancelled;
            internal ulong CancelSubmitted, CancelCopied, Cursor;
            internal ulong RequestWorker, LastClock;
            internal uint RequestIndex, Fd, FdGeneration, Binding, Description, FsIndex, FileIndex, OutputBytes;
            internal uint SlotBytes;
            internal byte Phase, RequestPhase;
            internal bool SlotActive;
        }


    }
}
#pragma warning restore 649
#endif
