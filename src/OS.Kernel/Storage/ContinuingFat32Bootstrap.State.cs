using OS.Std.Binary;
using B = OS.Storage.WritableBlockSession;
using Gpt = OS.Storage.PrimaryGPTIndependentlyBoundsCRC;
using Fat = OS.Storage.FATClusterChainsLoopBadClusterTruncationDetection;

namespace OS.Storage;

public static unsafe partial class ContinuingFat32Bootstrap
{
    /// <summary>Zero-initialized noncopyable POD. Faults retain all scratch and
    /// the exact original device token until drained or externally retired.
    /// Restart requires a fresh State and epoch; Stop retires only local scratch.</summary>
    public struct State
    {
        internal ulong Self, DeviceOwner, PoolEpoch, IoSector, First, Last, PartitionLow, PartitionHigh;
        internal Admission Input;
        internal B.RequestToken Io;
        internal LiveGptHeaderResult Header;
        internal LiveFat32Volume Volume;
        internal uint Reads, EntrySector, Selected;
        internal Life Phase;
        internal Step Next;
        internal bool IoActive, CancelRequested;
        internal Status Failure;
        internal B.Status BlockError;
        internal LiveGptStatus GptError;
        internal LiveFat32Status FatError;
        public readonly bool Ready => Phase == Life.Ready;
        public readonly bool Retired => Phase == Life.Retired;
        public readonly bool RequestPending => IoActive;
        public readonly uint SubmittedReads => Reads;
        public readonly Status LastFailure => Failure;
        public readonly B.Status LastBlockFailure => BlockError;
        public readonly LiveGptStatus LastGptFailure => GptError;
        public readonly LiveFat32Status LastFatFailure => FatError;
    }
}
