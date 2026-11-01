using OS.Kernel.Threading;

namespace OS.Kernel.Compatibility.LinuxX64;
public unsafe partial struct LinuxThreadServices
{
    /// <summary>Exclusive kernel bookkeeping for one address/mode/backing
    /// lifetime. References include completed waits until their actual worker
    /// consumes completion. Never copy/modify a bound cell or free its alias.</summary>

    public struct FutexCell
    {
        internal ulong Address, Lifetime;
        internal uint* Alias;
        internal uint Mode, References;
        internal WaitAddress.Binding Binding;
    }
#if !NOSTDLIB_EFI || M16_025_PROBE
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
}
