using OS.Std.Memory;

namespace OS.Drivers;

public struct VirtioNetQueueResult
{
    public VirtioNetQueueStatus Status, CleanupStatus;
    public VirtioNetQueueReceipt Receive, Transmit;
    public ulong CpuAddress, DeviceAddress;
    public uint Fences, ResetReads, Polls, UsedId, UsedLength;
    public ushort UsedIndex;
    public bool ResetComplete, BusMasterDisabled, DmaReleased, TransmitCompleted;
    public readonly bool Succeeded => Status == VirtioNetQueueStatus.Success && CleanupStatus == VirtioNetQueueStatus.Success && Receive.Configured && Transmit.Configured && Receive.DescriptorReleased && Transmit.DescriptorReleased && ResetComplete && BusMasterDisabled && DmaReleased;
}
