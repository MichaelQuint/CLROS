using OS.Drivers;

namespace OS.Storage;

public struct VirtioBlkReadResult
{
    public VirtioBlkReadStatus Status;
    public VirtioBlkQueueResult Queue;
    public ulong Sector, Capacity, NotifyAddress;
    public uint CapacityReads, Polls, Fences, UsedId, UsedLength, BytesRead;
    public ushort NotifyOffset, UsedIndex;
    public byte Generation, DeviceStatus;
    public bool Published, Notified;
    public bool InterruptAttempted, InterruptPrepared, InterruptStopped;
    public BlockCompletionRoute CompletionRoute;
    public readonly bool Succeeded => Status == VirtioBlkReadStatus.Success && Queue.Succeeded && BytesRead == 512;
}
