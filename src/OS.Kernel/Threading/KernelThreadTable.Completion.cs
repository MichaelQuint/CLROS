using OS.Pal;

namespace OS.Kernel.Threading;

public unsafe partial struct KernelThreadTable
{
    public enum Completion : byte
    {
        None,
        Woken,
        Deadline,
        Joined,
        Cancelled,
        Interrupted
    }
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
}
