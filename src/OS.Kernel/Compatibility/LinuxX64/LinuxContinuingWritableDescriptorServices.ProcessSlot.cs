using C = OS.Storage.ContinuingFat32Mutation;

namespace OS.Kernel.Compatibility.LinuxX64;
public static unsafe partial class LinuxContinuingWritableDescriptorServices
{
    public struct ProcessSlot
    {
        internal ulong Generation;
        internal uint Pid;
        internal bool Active, Exiting;
    }
#if !NOSTDLIB_EFI || M16_027_TIMING_PROBE
#endif
}
