using OS.Pal;

namespace OS.Kernel.Threading;

public unsafe partial struct ThreadLocalStorage
{
    /// <summary>Exclusive caller-owned metadata; never copy or modify a live slot.
    /// TLS backing remains borrowed until this owner successfully reaps its thread.</summary>

    public struct Slot
    {
        internal uint Generation, ContextBytes;
        internal bool Retired;
        internal OSHostHandle Thread;
        internal X64ThreadContext* Context;
        internal Region Fs, Gs;
    }
}
