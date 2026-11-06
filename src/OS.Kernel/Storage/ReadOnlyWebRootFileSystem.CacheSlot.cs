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
    /// <summary>One paired FAT-sector cache entry. Data is512 bytes. A slot is
    /// invalid while filling and becomes valid only after both FAT copies match.
    /// The epoch and both absolute LBAs identify its complete validation scope.</summary>
    public struct CacheSlot
    {
        public byte* Data;
        internal ulong Epoch, Lba0, Lba1, OperationGeneration;
        internal uint OperationIndex;
        internal byte Phase;
    }
}
