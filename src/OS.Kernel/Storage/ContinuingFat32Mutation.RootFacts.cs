using OS.Drivers;
using OS.Std.Binary;
using Block = OS.Storage.WritableBlockSession;
using Chain = OS.Storage.FATClusterChainsLoopBadClusterTruncationDetection;
using Vfat = OS.Storage.MultiClusterDirectoryVFATLongNameSupportSeparate;

namespace OS.Storage;

public static unsafe partial class ContinuingFat32Mutation
{
    /// <summary>Copied mount-root allocation facts from an actual full
    /// mirrored-FAT root-chain walk. The FAT root has no short entry, file
    /// token, Unix owner/mode or timestamp. FirstSector is its first cluster's
    /// physical sector; allocation includes every checked root cluster. These
    /// historical values grant no fd, user-copy or future device authority.</summary>
    public readonly struct RootFacts
    {
        public readonly ulong Owner, Epoch, Revision, FirstSector;
        public readonly uint ClusterBytes, AllocatedClusters;
        public readonly ulong Allocated512Blocks;
        internal RootFacts(State* s, ulong sector, uint clusterBytes, uint clusters, ulong blocks)
        {
            Owner = (ulong)s;
            Epoch = s->Input.Epoch;
            Revision = s->Revision;
            FirstSector = sector;
            ClusterBytes = clusterBytes;
            AllocatedClusters = clusters;
            Allocated512Blocks = blocks;
        }
    }
}
