using OS.Pal;

namespace OS.Kernel.Threading;

public unsafe partial struct KernelThreadTable
{
    public enum State : byte
    {
        Empty,
        Ready,
        Running,
        Parking,
        Waiting,
        Pending,
        Exiting,
        Exited,
        Retired
    }
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
}
