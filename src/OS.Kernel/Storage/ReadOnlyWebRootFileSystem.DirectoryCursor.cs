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
    /// <summary>Resumable physical directory position and VFAT sequence. Only
    /// the enclosing operation owns its scratch/history. Pending metadata/name
    /// stays unpublished across OutputFull; work and visits never reset on retry.</summary>
    public struct DirectoryCursor
    {
        internal uint Cluster, SectorInCluster, EntryInSector, Visits;
        internal LiveVfatState Vfat;
        internal Metadata Pending;
        internal ushort NameLength;
        internal bool Visited, SectorReady, HasPending, Ended;
    }
}
