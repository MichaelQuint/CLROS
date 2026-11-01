using C = OS.Storage.ContinuingFat32Mutation;

namespace OS.Kernel.Compatibility.LinuxX64;
public static unsafe partial class LinuxContinuingWritableDescriptorServices
{
    internal enum Operation : byte
    {
        Open,
        Read,
        Write,
        Sync,
        Grow,
        Truncate,
        Unlink,
        Stat,
        PathStat
    }
#if !NOSTDLIB_EFI || M16_027_TIMING_PROBE
#endif
}
