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
    /// <summary>Stable noncopyable owner. Failed block activity revokes cache
    /// publication and all new work. Preserve admission/backing until Stop
    /// succeeds; failed device cleanup remains the platform owner's duty.</summary>
    public struct Mount
    {
        internal ulong Self, DeviceEpoch, PoolEpoch;
        internal Admission Input;
        internal LiveFat32Volume Volume;
        internal Metadata Root;
        internal Block.ReadToken IoToken;
        internal ulong IoGeneration;
        internal uint IoIndex, NextVictim;
        internal byte Phase, DataReadSectors;
        internal bool IoActive;
        internal Status Failure;
        internal ulong Reads, Hits, Misses;
        internal byte* ImmutableDirectoryCacheBacking;
        internal uint ImmutableDirectoryCacheSlots;
        internal byte* ImmutableFileChainCacheBacking;
        internal uint ImmutableFileChainCacheSlots;
#if !NOSTDLIB_EFI || M16_027_PROBE
        internal Port.State* ReadPort;
        internal Port.Inspection PortBinding;
        internal Port.Token PortIo;
#endif
        public readonly bool Ready => Phase == Mounted;
        public readonly bool IsRetired => Phase == Retired;
        public readonly bool RequiresCleanup => Self != 0 && Phase != Retired;
        public readonly Status LastFailure => Failure;
        public readonly ulong SectorReads => Reads;
        public readonly ulong CacheHits => Hits;
        public readonly ulong CacheMisses => Misses;
    }
}
