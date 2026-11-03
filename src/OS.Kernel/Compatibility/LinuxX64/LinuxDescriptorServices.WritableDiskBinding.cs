#if !NOSTDLIB_EFI || M16_027_PROBE
using OS.Kernel.Memory;
using OS.Pal;
using W = OS.Kernel.Compatibility.LinuxX64.LinuxContinuingWritableDescriptorServices;
using C = OS.Storage.ContinuingFat32Mutation;

namespace OS.Kernel.Compatibility.LinuxX64;

public static unsafe partial class LinuxDescriptorServices
{
    /// <summary>One original provider handle per global description; private
    /// provider fd integers never enter the Linux namespace. No dup in this slice.
    /// Closing retains the binding while a real pending request borrows it.</summary>

    public struct WritableDiskBinding
    {
        internal W.DescriptorToken File;
        internal ulong Generation;
        internal uint Active, Closing;
    }
}
#endif
