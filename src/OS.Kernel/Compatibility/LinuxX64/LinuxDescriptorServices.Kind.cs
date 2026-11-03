using System;
using OS.Kernel.Memory;
using OS.Std.Text;
#if !NOSTDLIB_EFI || M16_027_PROBE
using Writable = OS.Kernel.Compatibility.LinuxX64.LinuxContinuingWritableDescriptorServices;

#endif
namespace OS.Kernel.Compatibility.LinuxX64;

public static unsafe partial class LinuxDescriptorServices
{
    internal enum Kind : uint
    {
        None,
        Input,
        Output,
        Package,
        Snapshot,
        PipeRead,
        PipeWrite,
        Socket,
        Epoll
#if !NOSTDLIB_EFI || M16_025_PROBE
    ,
        Disk
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
    ,
        WritableDisk
#endif
    }
}
