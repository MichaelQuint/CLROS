using System;
using OS.Kernel.Memory;
using OS.Std.Text;
#if !NOSTDLIB_EFI || M16_027_PROBE
using Writable = OS.Kernel.Compatibility.LinuxX64.LinuxContinuingWritableDescriptorServices;

#endif
namespace OS.Kernel.Compatibility.LinuxX64;
public static unsafe partial class LinuxDescriptorServices
{
#if !NOSTDLIB_EFI || M16_025_PROBE
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
    /// <summary>Caller-zeroed descriptor metadata. Flags are per descriptor;
    /// generation survives close so tokens cannot name a reused integer.</summary>

    public struct Slot
    {
        internal uint Description, Flags, Generation;
    }
#if !NOSTDLIB_EFI || M16_025_PROBE
#endif
#if !NOSTDLIB_EFI || CLROS_LINUX_SOCKETS
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || M16_025_PROBE
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || CLROS_LINUX_SOCKETS
#endif
#if !NOSTDLIB_EFI || CLROS_LINUX_SOCKETS
#endif
#if !NOSTDLIB_EFI || CLROS_LINUX_SOCKETS
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || CLROS_LINUX_SOCKETS
#endif
#if !NOSTDLIB_EFI || M16_025_PROBE
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || M16_025_PROBE
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || CLROS_LINUX_SOCKETS
#else

#endif
#if !NOSTDLIB_EFI || M16_025_PROBE
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || CLROS_LINUX_SOCKETS
#endif
#if !NOSTDLIB_EFI || M16_025_PROBE
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if (!NOSTDLIB_EFI || M16_025_PROBE) && (!NOSTDLIB_EFI || M16_027_PROBE)
#endif
#if !NOSTDLIB_EFI || M16_025_PROBE
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || CLROS_LINUX_SOCKETS
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || CLROS_LINUX_SOCKETS
#endif
#if !NOSTDLIB_EFI || CLROS_LINUX_SOCKETS
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || M16_025_PROBE
#endif
#if !NOSTDLIB_EFI || CLROS_LINUX_SOCKETS
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || M16_025_PROBE
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || M16_025_PROBE
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || CLROS_LINUX_SOCKETS
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#endif
#if !NOSTDLIB_EFI || M16_025_PROBE
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#endif
#if (!NOSTDLIB_EFI || M16_027_PROBE) && (!NOSTDLIB_EFI || CLROS_LINUX_SOCKETS)
#if !NOSTDLIB_EFI || M16_025_PROBE
#endif
#if !NOSTDLIB_EFI || M16_025_PROBE
#endif
#if !NOSTDLIB_EFI || M16_025_PROBE
#endif
#endif
}
