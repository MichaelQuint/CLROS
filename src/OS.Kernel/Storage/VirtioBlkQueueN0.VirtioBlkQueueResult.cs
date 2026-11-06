using OS.Drivers;

namespace OS.Storage;

public struct VirtioBlkQueueResult
{
    public VirtioBlkQueueStatus Status, CleanupStatus;
    public VirtioBlkDmaLayout Layout;
    public ushort MaximumSize, Size;
    public uint Fences, ResetReads;
    public bool Configured, ResetComplete, BusMasterDisabled, DmaReleased;
    public readonly bool Succeeded => Status == VirtioBlkQueueStatus.Success &&
        CleanupStatus == VirtioBlkQueueStatus.Success && Configured && ResetComplete && BusMasterDisabled && DmaReleased;
}
