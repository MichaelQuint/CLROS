using OS.Pal;

namespace OS.Kernel.Threading;

public unsafe partial struct KernelThreadTable
{
    public enum WaitKind : byte
    {
        None,
        Park,
        Sleep,
        Join
    }
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
}
