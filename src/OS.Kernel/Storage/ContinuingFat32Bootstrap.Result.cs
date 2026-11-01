using OS.Std.Binary;
using B = OS.Storage.WritableBlockSession;
using Gpt = OS.Storage.PrimaryGPTIndependentlyBoundsCRC;
using Fat = OS.Storage.FATClusterChainsLoopBadClusterTruncationDetection;

namespace OS.Storage;

public static unsafe partial class ContinuingFat32Bootstrap
{
    /// <summary>Copied primary-only geometry and identity, not I/O authority or
    /// an injected mount. Only attributes0 are admitted; this rejects read-only
    /// and all other unimplemented BasicData attribute semantics.</summary>
    public readonly struct Result
    {
        public readonly ulong Owner, Epoch, DeviceOwner, DeviceEpoch, DiskGuidLow, DiskGuidHigh, PartitionGuidLow, PartitionGuidHigh;
        public readonly uint EntryIndex, Reads;
        public readonly LiveFat32Volume Volume;
        internal Result(State* s)
        {
            Owner = (ulong)s; Epoch = s->Input.Epoch; DeviceOwner = s->DeviceOwner; DeviceEpoch = s->Input.DeviceEpoch;
            DiskGuidLow = s->Header.DiskGuidLow; DiskGuidHigh = s->Header.DiskGuidHigh; PartitionGuidLow = s->PartitionLow; PartitionGuidHigh = s->PartitionHigh;
            EntryIndex = s->Selected; Reads = s->Reads; Volume = s->Volume;
        }
    }
}
