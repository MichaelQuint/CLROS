using OS.Std.Memory;

namespace OS.Drivers;
public struct VirtioNetQueueReceipt
{
    public ushort Index, MaximumSize, Size, NotifyOffset, AvailableIndex;
    public ulong Descriptor, Available, Used, Notify;
    public bool Configured, Published, DescriptorReleased;
}
