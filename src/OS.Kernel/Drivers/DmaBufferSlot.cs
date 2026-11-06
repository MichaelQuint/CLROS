using OS.Kernel.Platform;

namespace OS.Drivers;

public struct DmaBufferSlot
{
    internal ulong Generation;
    internal uint FirstPage, Pages;
    internal DmaBufferOwnership Owner;
}
