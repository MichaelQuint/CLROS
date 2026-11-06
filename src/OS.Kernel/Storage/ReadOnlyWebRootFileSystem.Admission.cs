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
    /// <summary>Borrowed immutable admission. The block owner is already idle
    /// and running; GPT authority supplies the ESP bounds. Epoch is unique for
    /// this mount. Supply2..8 operation and handle slots,1..8 cache slots, and a
    /// positive cumulative physical-directory-entry budget per operation.
    /// Scratch capacities remain independent of advertised volume size.</summary>
    public struct Admission
    {
        public Block.State* Device;
        public ulong Epoch, PartitionFirst, PartitionLast;
        public Operation* Operations;
        public FileHandle* Files;
        public CacheSlot* Cache;
        public uint OperationCount, FileCount, CacheCount, DirectoryEntryBudget;
    }
}
