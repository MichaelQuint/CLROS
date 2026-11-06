using OS.Pal;

namespace OS.Kernel.Threading;

public unsafe partial struct WaitAddress
{
    /// <summary>Exclusive caller storage for one live registration. Completed
    /// registrations are removable without touching scheduler completion state.</summary>

    public struct Registration
    {
        internal Binding Address;
        internal KernelThreadWait Wait;
        internal ulong Order;
        internal uint Bitset;
    }
}
