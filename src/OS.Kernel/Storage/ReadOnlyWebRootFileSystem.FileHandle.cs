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
    /// <summary>Caller-owned handle slot and immutable checked cluster map.
    /// Supply1..4096 uints before Start; all other fields start zero. An active
    /// handle's map is private until Close. Directory handles need no data map.</summary>
    public struct FileHandle
    {
        public uint* Clusters;
        public uint ClusterCapacity;
        internal ulong Self, Epoch, Generation;
        internal Metadata Info;
        internal uint ClusterCount, References;
        internal byte Phase;
    }
}
