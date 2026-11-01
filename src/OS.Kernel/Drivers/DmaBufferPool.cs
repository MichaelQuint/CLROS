using OS.Kernel.Platform;

namespace OS.Drivers;
public unsafe struct DmaBufferPool
{
    internal ulong Self, Epoch, Used;
    internal DmaAddressWindow Window;
    internal DmaBufferSlot* Slots;
    internal uint Count;
    public readonly bool IsInitialized => Self != 0;
    public readonly ulong AllocatedMask => Used;
    public readonly uint PageCount => Count;
}
