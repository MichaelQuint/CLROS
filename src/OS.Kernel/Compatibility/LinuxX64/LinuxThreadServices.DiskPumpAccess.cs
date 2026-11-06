#if !NOSTDLIB_EFI || M16_025_PROBE
using OS.Kernel.Threading;

namespace OS.Kernel.Compatibility.LinuxX64;

public unsafe partial struct LinuxThreadServices
{
    /// <summary>Trusted serialized supervisor callbacks, borrowed for this
    /// invocation only. Both must be null for one-step mode, or nonnull with a
    /// stable Context. Clock returns monotonic nanoseconds; Flush retains each
    /// step's evidence before any next step. Return exactly1; never throw,
    /// reenter or mutate thread/descriptor/FS/block ownership. No user pointers.
    /// A callback failure rejects while all original backing stays retained.</summary>

    public struct DiskPumpAccess
    {
        public void* Context;
        public delegate* unmanaged<void*, ulong*, byte> Clock;
        public delegate* unmanaged<void*, byte> Flush;
        public DiskPumpMeasurements* Measurements;
    }
}
#endif
