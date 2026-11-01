using C = OS.Storage.ContinuingFat32Mutation;

namespace OS.Kernel.Compatibility.LinuxX64;
public static unsafe partial class LinuxContinuingWritableDescriptorServices
{
    internal enum Step : byte
    {
        Lookup,
        Create,
        CreatedLookup,
        Authenticate,
        Read,
        Write,
        Flush,
        Done,
        Grow,
        Truncate,
        DeleteLookup,
        Delete,
        FindFree,
        RootStat
    }
#if !NOSTDLIB_EFI || M16_027_TIMING_PROBE
#endif
}
