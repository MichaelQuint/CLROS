using OS.Pal;

namespace OS.Kernel.Threading;

public unsafe partial struct WaitAddress
{
    /// <summary>Exclusive caller storage; do not copy or modify bound slots.
    /// An exhausted generation retires rather than reissuing an old binding.</summary>

    public struct Slot
    {
        internal uint Generation, Tag;
        internal bool Retired;
        internal uint* Word;
        internal ulong Lifetime;
    }
}
