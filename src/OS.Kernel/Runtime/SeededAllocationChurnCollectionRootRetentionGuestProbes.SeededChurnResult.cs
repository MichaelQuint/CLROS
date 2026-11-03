using OS.Std;

namespace OS.Kernel.Runtime;

public struct SeededChurnResult
{
    public SeededChurnStatus Status
    {
        get; internal set;
    }
    public RhpNewFastStatus AllocationStatus
    {
        get; internal set;
    }
    public BoundedCollectionStatus CollectionStatus
    {
        get; internal set;
    }
    public uint CompletedRounds
    {
        get; internal set;
    }
    public uint Allocations
    {
        get; internal set;
    }
    public uint Collections
    {
        get; internal set;
    }
    public uint Reclaimed
    {
        get; internal set;
    }
    public uint InitialReclaimed
    {
        get; internal set;
    }
    public ulong InitialReclaimedBytes
    {
        get; internal set;
    }
    public uint BaselineLiveObjects
    {
        get; internal set;
    }
    public uint ReusedAddresses
    {
        get; internal set;
    }
    public ulong Fingerprint
    {
        get; internal set;
    }
    public bool Succeeded => Status == SeededChurnStatus.Success;
}
