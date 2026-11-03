using System;
using OS.Kernel.Memory;
using OS.Std.Text;
#if !NOSTDLIB_EFI || M16_027_PROBE
using Writable = OS.Kernel.Compatibility.LinuxX64.LinuxContinuingWritableDescriptorServices;

#endif
namespace OS.Kernel.Compatibility.LinuxX64;

public static unsafe partial class LinuxDescriptorServices
{
    /// <summary>Caller-zeroed descriptor metadata. Flags are per descriptor;
    /// generation survives close so tokens cannot name a reused integer.</summary>

    public struct Slot
    {
        internal uint Description, Flags, Generation;
    }
}
