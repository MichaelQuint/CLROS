using OS.Drivers;
using OS.Std.Binary;
using OS.Std.Time;
using Block = OS.Storage.ContinuingReadOnlyBlockService;
using Chain = OS.Storage.FATClusterChainsLoopBadClusterTruncationDetection;
using Vfat = OS.Storage.MultiClusterDirectoryVFATLongNameSupportSeparate;
#if !NOSTDLIB_EFI || M16_027_PROBE
using Port = OS.Storage.WritableBlockReadPort;
#endif

namespace OS.Storage;

public static unsafe partial class ReadOnlyWebRootFileSystem
{
#if !NOSTDLIB_EFI || M16_027_PROBE
    /// <summary>Borrow one exclusively retained read port on the original
    /// writable device. Exact readonly bounds/capacity come from authenticated
    /// port facts, never a caller-selected subset or fabricated readonly owner.
    /// Common scratch/slot lifetime is identical to Admission. The outer owner
    /// retains this port until FS Stop, then stops port before releasing C/B.
    /// Busy is retryable without reservation, output or result consumption.</summary>
    public struct BorrowedAdmission
    {
        public Port.State* ReadPort;
        public ulong PortEpoch, Epoch;
        public Operation* Operations;
        public FileHandle* Files;
        public CacheSlot* Cache;
        public uint OperationCount, FileCount, CacheCount, DirectoryEntryBudget;
    }
#endif
}
