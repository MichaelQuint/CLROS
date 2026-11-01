using OS.Drivers;
using OS.Std.Binary;
using Block = OS.Storage.WritableBlockSession;
using Chain = OS.Storage.FATClusterChainsLoopBadClusterTruncationDetection;
using Vfat = OS.Storage.MultiClusterDirectoryVFATLongNameSupportSeparate;

namespace OS.Storage;

public static unsafe partial class ContinuingFat32Mutation
{
    /// <summary>Copied successful regular-file open facts. AllocatedClusters
    /// is the actual validated full-chain count, including its final cluster;
    /// Allocated512Blocks counts that chain's sectors, not rounded file bytes.
    /// The engine rejects preallocated/overlong chains. These historical facts
    /// grant no Linux stat policy, current fd/device authority or persistence.</summary>
    public readonly struct OpenFacts
    {
        public readonly FileToken File;
        public readonly uint ClusterBytes, AllocatedClusters;
        public readonly ulong Allocated512Blocks;
        internal OpenFacts(FileToken file, uint clusterBytes, uint clusters, ulong blocks)
        { File = file; ClusterBytes = clusterBytes; AllocatedClusters = clusters; Allocated512Blocks = blocks; }
    }
}
