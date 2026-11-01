using OS.Std.Binary;
using B = OS.Storage.WritableBlockSession;
using Gpt = OS.Storage.PrimaryGPTIndependentlyBoundsCRC;
using Fat = OS.Storage.FATClusterChainsLoopBadClusterTruncationDetection;

namespace OS.Storage;

public static unsafe partial class ContinuingFat32Bootstrap
{
    /// <summary>Exact512B sector and16384B entry scratch, disjoint from each
    /// other, State and all block/observer extents. DeviceEpoch identifies the
    /// actual original running device; Epoch uniquely identifies this zero-State
    /// bootstrap. SectorBudget1..MaximumReads counts actual submissions.</summary>
    public struct Admission
    {
        public B.State* Device;
        public ulong Epoch, DeviceEpoch;
        public byte* Sector, Entries;
        public uint SectorBudget;
    }
}
