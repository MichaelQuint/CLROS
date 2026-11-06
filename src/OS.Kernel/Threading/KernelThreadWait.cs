using OS.Pal;

namespace OS.Kernel.Threading;

/// <summary>One registration by one live thread. Sequence never wraps within a
/// thread lifetime; a handle alone cannot complete a later wait. Copying this
/// value acquires no resource or wake authority.</summary>
public readonly struct KernelThreadWait
{
    public readonly OSHostHandle Thread;
    public readonly ulong Sequence;
    public bool IsValid => Thread.IsValid && Sequence != 0;
    internal KernelThreadWait(OSHostHandle thread, ulong sequence)
    {
        Thread = thread;
        Sequence = sequence;
    }
}
