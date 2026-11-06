using OS.Std.Memory;

namespace OS.Drivers;

public struct VirtqueuePollingResult
{
    public VirtqueuePollingStatus Status, CleanupStatus;
    public ushort MaximumSize, Size, NotifyOffset, AvailableIndex, UsedIndex;
    public uint Polls, UsedId, UsedLength, Fences;
    public ulong CpuAddress, DeviceAddress, NotifyAddress;
    public bool Published, ResetComplete, BusMasterDisabled, DmaReleased;
    public bool InterruptObserved, InterruptsMasked;
    public readonly bool Succeeded => Status == VirtqueuePollingStatus.Success && CleanupStatus == VirtqueuePollingStatus.Success && ResetComplete && BusMasterDisabled && DmaReleased;
}
