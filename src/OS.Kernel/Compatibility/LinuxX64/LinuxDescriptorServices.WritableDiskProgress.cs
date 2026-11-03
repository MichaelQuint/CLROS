#if !NOSTDLIB_EFI || M16_027_PROBE
using OS.Kernel.Memory;
using OS.Pal;
using W = OS.Kernel.Compatibility.LinuxX64.LinuxContinuingWritableDescriptorServices;
using C = OS.Storage.ContinuingFat32Mutation;

namespace OS.Kernel.Compatibility.LinuxX64;

public static unsafe partial class LinuxDescriptorServices
{
    public enum WritableDiskProgress : byte
    {
        Rejected,
        NotHandled,
        Immediate,
        Pending,
        Ready,
        Draining,
        RecoveryRequired
    }
}
#endif
