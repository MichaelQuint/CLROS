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
    /// <summary>Reusable noncopyable operation slot. Supply Scratch before
    /// Start, leave every other field zero, and never change it while mounted.
    /// Failed operations remain owned until Cancel; successful copy consumes
    /// open/read results. A directory operation ends or is explicitly cancelled.</summary>
    public struct Operation
    {
        public Scratch Storage;
        internal ulong Self, Epoch, Generation;
        internal DirectoryCursor Cursor;
        internal Metadata Candidate;
        internal ulong Offset;
        internal uint Index, FileIndex, Component, ComponentLength, PathLength, Work;
        internal uint Wanted, Filled, FatCluster, FatSlot, RequiredClusters, MatchCount;
        internal byte Phase, Kind, Waiting, FatStage;
        internal bool CandidateExact, HaveCandidate, LookupDone;
        internal Status Failure;
    }
}
