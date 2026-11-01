using OS.Boot;
using OS.Kernel.Memory;

namespace OS.Kernel.Runtime;
public readonly struct BoundedCollectionResult
{
    internal BoundedCollectionResult(BoundedCollectionStatus status, RhpNewFastStatus ownerStatus = default, uint live = 0, uint reclaimed = 0, ulong bytes = 0)
    {
        Status = status;
        OwnerStatus = ownerStatus;
        LiveObjects = live;
        ReclaimedObjects = reclaimed;
        ReclaimedBytes = bytes;
    }

    public BoundedCollectionStatus Status { get; }
    public RhpNewFastStatus OwnerStatus { get; }
    public uint LiveObjects { get; }
    public uint ReclaimedObjects { get; }
    public ulong ReclaimedBytes { get; }
    public bool Succeeded => Status == BoundedCollectionStatus.Success;
}
