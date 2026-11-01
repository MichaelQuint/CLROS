using OS.Kernel.Threading;
using OS.Kernel.Memory;

namespace OS.Kernel.Compatibility.LinuxX64;
public static unsafe partial class LinuxThreadProgram
{
#if M10_004_PROBE




#elif M16_009_PROBE || M16_012_PROBE || M07_027_PROBE





#else
#endif
    /// <summary>Retained current generation and its original entry adapter.
    /// Argument is the retained scheduler slot pointer, independently checked
    /// by the native Start callback against that generation's table entry.
    /// Update both descriptors after actual capture/reap; storage stays mapped
    /// and exclusive through verification/re-emission. It grants no binding.</summary>

    public struct Binding
    {
        public ThreadContextSwitch.Descriptor Context;
        public KernelThreadEntry.Plan Entry;
        public ulong Argument;
    }
}
