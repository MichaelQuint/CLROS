using System;
using OS.Kernel.Memory;
using OS.Std.Text;
#if !NOSTDLIB_EFI || M16_027_PROBE
using Writable = OS.Kernel.Compatibility.LinuxX64.LinuxContinuingWritableDescriptorServices;

#endif
namespace OS.Kernel.Compatibility.LinuxX64;

public static unsafe partial class LinuxDescriptorServices
{
    /// <summary>Kernel-only borrowed token. It is not an OSHost handle or a
    /// Linux user ABI value and confers no access without IsCurrent.</summary>

    public struct Token
    {
        internal State* Owner;
        internal uint Number, Generation;
    }
}
